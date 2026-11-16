using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 11 (RFS12, RFU34, RNFE01, E24, S23): «Mis datos» (descarga y baja de la cuenta) y la purga de los
// cuerpos de la cola de correo.
[Collection(ApiCollection.Name)]
public sealed class PrivacidadFase11Tests(ApiFixture api)
{
    private async Task<int> AdjuntoAsync(int idCliente, int idSolicitud, string nombre) => await api.ConsultarAsync(async db =>
    {
        var adjunto = new AdjuntoSolicitud
        {
            Clave = Guid.NewGuid().ToString("N"),
            Tipo = AdjuntoSolicitud.TipoPdf,
            NombreOriginal = nombre,
            Bytes = 1234,
            IdCliente = idCliente,
            IdSolicitud = idSolicitud,
            FechaSubida = DateTimeOffset.UtcNow,
            FechaVinculo = DateTimeOffset.UtcNow
        };
        db.AdjuntosSolicitud.Add(adjunto);
        await db.SaveChangesAsync();
        return adjunto.IdAdjunto;
    });

    // Un cliente con todo: solicitud con adjunto, cotizacion aprobada y pedido con abono.
    private async Task<(int IdCliente, string Correo, string Token, int IdSolicitud, int IdPedido)> ClienteCompletoAsync(string marca)
    {
        var (idSolicitud, idCliente, correo, token) = await api.SolicitudEnLineaAsync(descripcion: $"Encargo {marca} para la vitrina");
        await AdjuntoAsync(idCliente, idSolicitud, $"plano-{marca}.pdf");
        var idCotizacion = (await api.CrearCotizacionAsync(idSolicitud)).GetProperty("idCotizacion").GetInt32();
        await api.AprobarCotizacionAsync(idCotizacion);
        var admin = api.Cliente(await api.TokenAdminAsync());
        var pedido = await admin.PostAsJsonAsync($"/api/v1/cotizaciones/{idCotizacion}/pedido",
            ApiFixture.DatosPedido(await api.IdAdminAsync(), new { monto = 500, medioPago = "efectivo" })).EsperarAsync(HttpStatusCode.Created);
        return (idCliente, correo, token, idSolicitud, pedido.GetProperty("idPedido").GetInt32());
    }

    [Fact]
    public async Task MisDatos_TraeTodoLoDelCliente_YNadaDeOtro()
    {
        var uno = await ClienteCompletoAsync($"UNO{api.Siguiente()}");
        var otro = await ClienteCompletoAsync($"OTRO{api.Siguiente()}");
        await api.ConsultarAsync(db =>
        {
            db.Consentimientos.Add(new Consentimiento { IdCliente = uno.IdCliente, Tipo = "terminos", VersionDocumento = "1.0", FechaAceptacion = DateTimeOffset.UtcNow });
            return db.SaveChangesAsync();
        });
        var cliente = api.Cliente(uno.Token);

        var mala = await cliente.PostAsJsonAsync("/api/v1/mi-cuenta/datos", new { password = "no-es-la-clave" }).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("datos_invalidos", Fase11Apoyo.Codigo(mala));

        using var respuesta = await cliente.PostAsJsonAsync("/api/v1/mi-cuenta/datos", new { password = ApiFixture.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/json", respuesta.Content.Headers.ContentType?.MediaType);
        var disposicion = respuesta.Content.Headers.ContentDisposition!;
        Assert.Equal("attachment", disposicion.DispositionType);
        Assert.Equal($"mis-datos-letreros-herrera-{ApiFixture.HoyChile():yyyyMMdd}.json", disposicion.FileName?.Trim('"'));
        var texto = await respuesta.Content.ReadAsStringAsync();
        var datos = JsonDocument.Parse(texto).RootElement;

        Assert.True(datos.TryGetProperty("generado", out _));
        Assert.Equal(uno.Correo, datos.GetProperty("cuenta").GetProperty("correo").GetString());
        var terminos = Assert.Single(datos.GetProperty("consentimientos").EnumerateArray());
        Assert.Equal("terminos", terminos.GetProperty("tipo").GetString());
        Assert.Equal("1.0", terminos.GetProperty("version").GetString());
        var solicitud = Assert.Single(datos.GetProperty("solicitudes").EnumerateArray());
        Assert.Equal(uno.IdSolicitud, solicitud.GetProperty("idSolicitud").GetInt32());
        Assert.NotEmpty(solicitud.GetProperty("detalles").EnumerateArray());
        var adjunto = Assert.Single(solicitud.GetProperty("adjuntos").EnumerateArray());
        Assert.StartsWith("plano-UNO", adjunto.GetProperty("nombre").GetString());
        Assert.Equal("pdf", adjunto.GetProperty("tipo").GetString());
        Assert.Equal(1234, adjunto.GetProperty("tamano").GetInt32());
        Assert.Single(datos.GetProperty("cotizaciones").EnumerateArray());
        var pedido = Assert.Single(datos.GetProperty("pedidos").EnumerateArray());
        Assert.Equal(uno.IdPedido, pedido.GetProperty("idPedido").GetInt32());
        Assert.Equal(500, Assert.Single(pedido.GetProperty("pagos").EnumerateArray()).GetProperty("monto").GetInt32());
        Assert.Contains(datos.GetProperty("correosEnviados").EnumerateArray(), c => c.GetProperty("tipo").GetString() == TiposCorreo.ConfirmacionSolicitud);
        Assert.DoesNotContain(datos.GetProperty("correosEnviados").EnumerateArray(), c => c.GetProperty("tipo").GetString() == TiposCorreo.AvisoSolicitudNueva);

        // Nada del otro cliente.
        Assert.DoesNotContain(otro.Correo, texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OTRO", texto);
        Assert.DoesNotContain($"\"idPedido\":{otro.IdPedido},", texto);
        // Ni datos internos.
        Assert.DoesNotContain("passwordHash", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("huella", texto, StringComparison.OrdinalIgnoreCase);

        Assert.NotEmpty(await api.AuditoriaAsync("cliente", uno.IdCliente.ToString(), "descarga_datos"));
    }

    [Fact]
    public async Task LaBaja_ImpideIngresarYRecibirCorreos_YConservaLosPedidos()
    {
        var (idCliente, correo, token, _, idPedido) = await ClienteCompletoAsync($"BAJA{api.Siguiente()}");
        await api.LoginClienteAsync(correo, ApiFixture.AdminPassword);
        var cliente = api.Cliente(token);
        await api.ConsultarAsync(db =>
        {
            db.Consentimientos.Add(new Consentimiento { IdCliente = idCliente, Tipo = "marketing", VersionDocumento = "1.0", FechaAceptacion = DateTimeOffset.UtcNow });
            return db.SaveChangesAsync();
        });

        var sinConfirmar = await cliente.PostAsJsonAsync("/api/v1/mi-cuenta/baja", new { password = ApiFixture.AdminPassword, confirmacion = "dar de baja" })
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(sinConfirmar.GetProperty("errores").TryGetProperty("confirmacion", out _));
        await cliente.PostAsJsonAsync("/api/v1/mi-cuenta/baja", new { password = "otra", confirmacion = "DAR DE BAJA" }).EsperarAsync(HttpStatusCode.BadRequest);

        var baja = await cliente.PostAsJsonAsync("/api/v1/mi-cuenta/baja", new { password = ApiFixture.AdminPassword, confirmacion = "DAR DE BAJA" })
            .EsperarAsync(HttpStatusCode.OK);
        Assert.Equal($"BAJA-{idCliente}-{ApiFixture.HoyChile():yyyyMMdd}", baja.GetProperty("comprobante").GetProperty("numero").GetString());

        var fila = await api.ConsultarAsync(db => db.Clientes.AsNoTracking().SingleAsync(c => c.IdCliente == idCliente));
        Assert.NotNull(fila.InhabilitadoEn);
        Assert.Null(fila.PasswordHash);
        Assert.False(fila.RecibeAvisosEstado);
        Assert.Equal(0, await api.ConsultarAsync(db => db.TokensRenovacion.CountAsync(t => t.IdCliente == idCliente)));
        Assert.Equal(0, await api.ConsultarAsync(db => db.CodigosCuenta.CountAsync(c => c.IdCliente == idCliente)));
        Assert.False(await api.ConsultarAsync(db => db.Consentimientos.AnyAsync(c => c.IdCliente == idCliente && c.Tipo == "marketing" && c.FechaRevocacion == null)));
        // El comprobante tambien llega por correo (es esencial: sin enlace de baja).
        var comprobante = Assert.Single(await api.CorreosDeTipoAsync(TiposCorreo.BajaCuenta, correo));
        Assert.Null(comprobante.EnlaceBaja);
        Assert.NotEmpty(await api.AuditoriaAsync("cliente", idCliente.ToString(), "baja_cuenta"));

        // No vuelve a ingresar, ni con su token, ni crea otra cuenta con ese correo.
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/login-cliente", new { correo, password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Unauthorized);
        await cliente.GetAsync("/api/v1/mi-cuenta").EsperarAsync(HttpStatusCode.NotFound);
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", api.Registro(api.Telefono(), correo)).EsperarAsync(HttpStatusCode.Conflict);

        // Sus pedidos se conservan, el personal los ve con la cuenta marcada, y no le llegan mas correos.
        var admin = api.Cliente(await api.TokenAdminAsync());
        var pedido = await admin.GetAsync($"/api/v1/pedidos/{idPedido}").EsperarAsync(HttpStatusCode.OK);
        await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/pedidos/{idPedido}/estado", pedido.GetProperty("rowVersion").GetString()!,
            new { estado = "en_proceso" })).EsperarAsync(HttpStatusCode.OK);
        Assert.Empty(await api.CorreosAsync($"estado:pedido:{idPedido}"));
        var ficha = await admin.GetAsync($"/api/v1/clientes/{idCliente}").EsperarAsync(HttpStatusCode.OK);
        Assert.True(ficha.GetProperty("cuentaDadaDeBaja").GetBoolean());
        Assert.True(await api.ConsultarAsync(db => db.Pagos.AnyAsync(p => p.IdPedido == idPedido)));
    }

    private async Task<long> CorreoAsync(string estado, int diasAtras, string destinatario)
    {
        var hace = DateTimeOffset.UtcNow.AddDays(-diasAtras);
        var fila = new CorreoSaliente
        {
            Tipo = TiposCorreo.EstadoPedido,
            Referencia = $"estado:pedido:{api.Siguiente() + 900000}",
            Destinatarios = destinatario,
            ResponderA = "contacto@taller.test",
            Asunto = "Asunto con datos",
            CuerpoHtml = "<p>Cuerpo con datos personales</p>",
            CuerpoTexto = "Cuerpo con datos personales",
            EnlaceBaja = "https://synctime.test/api/v1/correos/baja/un-clic?t=abc",
            Estado = estado,
            Intentos = 1,
            ProximoIntento = hace,
            FechaCreacion = hace,
            FechaEnvio = estado == EstadosCorreo.Enviado ? hace : null,
            UltimoError = estado == EstadosCorreo.Fallido ? "mensaje: El servidor rechazó el mensaje." : null
        };
        return await api.ConsultarAsync(async db =>
        {
            db.CorreosSalientes.Add(fila);
            await db.SaveChangesAsync();
            return fila.IdCorreo;
        });
    }

    [Fact]
    public async Task LaPurga_BorraLosCuerposEnviadosYLosFallidosViejos_YConservaLasFilas()
    {
        var enviadoViejo = await CorreoAsync(EstadosCorreo.Enviado, 31, "enviado.viejo@correo.test");
        var enviadoNuevo = await CorreoAsync(EstadosCorreo.Enviado, 5, "enviado.nuevo@correo.test");
        var fallidoReciente = await CorreoAsync(EstadosCorreo.Fallido, 31, "fallido.reciente@correo.test");
        var fallidoViejo = await CorreoAsync(EstadosCorreo.Fallido, 91, "fallido.viejo@correo.test");
        await api.ConsultarAsync(db =>
        {
            db.ClavesIdempotencia.Add(new ClaveIdempotencia
            {
                Clave = Guid.NewGuid().ToString(), Ambito = "POST /api/v1/solicitudes", IdUsuario = 1, TipoUsuario = "empleado",
                HashCuerpo = new string('0', 64), Fecha = DateTimeOffset.UtcNow.AddDays(-2), Vence = DateTimeOffset.UtcNow.AddDays(-1)
            });
            return db.SaveChangesAsync();
        });

        await using (var alcance = api.Services.CreateAsyncScope())
            await alcance.ServiceProvider.GetRequiredService<PurgaDatosPersonales>().PurgarAsync(CancellationToken.None);

        var filas = await api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking()
            .Where(c => new[] { enviadoViejo, enviadoNuevo, fallidoReciente, fallidoViejo }.Contains(c.IdCorreo)).ToDictionaryAsync(c => c.IdCorreo));
        Assert.Equal(4, filas.Count);
        foreach (var purgado in new[] { filas[enviadoViejo], filas[fallidoViejo] })
        {
            Assert.Equal("", purgado.CuerpoHtml);
            Assert.Equal("", purgado.CuerpoTexto);
            Assert.Equal("", purgado.Asunto);
            Assert.Null(purgado.EnlaceBaja);
            Assert.Null(purgado.ResponderA);
            Assert.DoesNotContain("viejo@correo.test", purgado.Destinatarios);
            Assert.Contains("•••", purgado.Destinatarios);
            Assert.StartsWith("estado:pedido:", purgado.Referencia);
        }
        Assert.Equal("Cuerpo con datos personales", filas[enviadoNuevo].CuerpoTexto);
        Assert.Equal("Cuerpo con datos personales", filas[fallidoReciente].CuerpoTexto);
        Assert.Equal(0, await api.ConsultarAsync(db => db.ClavesIdempotencia.CountAsync(c => c.Vence < DateTimeOffset.UtcNow)));

        var admin = api.Cliente(await api.TokenAdminAsync());
        var lista = await admin.GetAsync("/api/v1/correos/no-enviados?tamano=100&tipo=estado_pedido").EsperarAsync(HttpStatusCode.OK);
        var elementos = lista.GetProperty("elementos").EnumerateArray().ToList();
        Assert.False(elementos.Single(e => e.GetProperty("idCorreo").GetInt64() == fallidoViejo).GetProperty("reenviable").GetBoolean());
        Assert.True(elementos.Single(e => e.GetProperty("idCorreo").GetInt64() == fallidoReciente).GetProperty("reenviable").GetBoolean());
    }

    [Fact]
    public async Task LaPurga_ConservaLasSolicitudesRechazadasYEliminadas()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var id = await api.CrearSolicitudAsync();
        await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/solicitudes/{id}/estado", await api.VersionAsync(id),
            new { estado = "rechazado", motivo = "No corresponde" })).EsperarAsync(HttpStatusCode.OK);
        await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/solicitudes/{id}/eliminar-logico", await VersionEnBaseAsync(id)))
            .EsperarAsync(HttpStatusCode.OK);

        await using (var alcance = api.Services.CreateAsyncScope())
            await alcance.ServiceProvider.GetRequiredService<PurgaDatosPersonales>().PurgarAsync(CancellationToken.None);

        Assert.True(await api.ConsultarAsync(db => db.Solicitudes.AnyAsync(s => s.IdSolicitud == id && s.EliminadoLogico && s.Estado == "rechazado")));
        Assert.NotEmpty(await api.AuditoriaAsync("solicitud", id.ToString(), "eliminar"));
    }

    private Task<string> VersionEnBaseAsync(int id) => api.ConsultarAsync(async db =>
        Convert.ToBase64String(await db.Solicitudes.Where(s => s.IdSolicitud == id).Select(s => s.RowVersion).SingleAsync()));
}
