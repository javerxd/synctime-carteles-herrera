using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 7: solicitudes y clientes (RFS04, RFS09, RNFP32, RNFP33, RFU06, RFU10, RFU11, RFU19, E03-E05, S03, S05).
// Las pruebas de la coleccion Api corren de a una, asi que contar filas antes y despues es fiable.
[Collection(ApiCollection.Name)]
public sealed class SolicitudesFase7Tests(ApiFixture api)
{
    // S25: el aviso de contacto directo, igual para todos los motivos.
    private const string Mensaje = "Para esta solicitud es preferible contactarse directamente con el local";

    // ── Utilidades ──

    private static byte[] Png(int ancho = 800, int alto = 600)
    {
        using var mapa = new SKBitmap(ancho, alto);
        using (var lienzo = new SKCanvas(mapa))
            lienzo.Clear(new SKColor(0x1D, 0x4E, 0xD8));
        using var imagen = SKImage.FromBitmap(mapa);
        return imagen.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n");
    private static readonly byte[] Svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

    private static MultipartFormDataContent Formulario(byte[] datos, string nombre, string tipo = "application/octet-stream")
    {
        var archivo = new ByteArrayContent(datos);
        archivo.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        return new MultipartFormDataContent { { archivo, "archivo", nombre } };
    }

    private async Task<int> SubirBorradorAsync(string token, byte[]? datos = null, string nombre = "logo.png")
    {
        var subido = await api.Cliente(token).PostAsync("/api/v1/mis/adjuntos", Formulario(datos ?? Png(), nombre)).EsperarAsync(HttpStatusCode.Created);
        return subido.GetProperty("idAdjunto").GetInt32();
    }

    private string IpNueva() => $"10.{api.Siguiente() % 250}.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}";

    private Task<HttpResponseMessage> EnviarAsync(string token, object? cuerpo = null, string? ip = null)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/v1/mis/solicitudes") { Content = JsonContent.Create(cuerpo ?? ApiFixture.CuerpoSolicitud()) };
        if (ip is not null)
            peticion.Headers.Add(ApiFixture.CabeceraIp, ip);
        return api.Cliente(token).SendAsync(peticion);
    }

    private sealed record Conteo(int Solicitudes, int Vinculados, int Correos, int Historial);

    private Task<Conteo> ContarAsync() => api.ConsultarAsync(async db => new Conteo(
        await db.Solicitudes.CountAsync(),
        await db.AdjuntosSolicitud.CountAsync(a => a.IdSolicitud != null),
        await db.CorreosSalientes.CountAsync(),
        await db.SolicitudesHistorial.CountAsync()));

    private async Task<JsonElement> RechazarAsync(int idSolicitud, string motivo = "No trabajamos ese material", string? token = null) =>
        await api.Cliente(token ?? await api.TokenAdminAsync())
            .PatchAsJsonAsync($"/api/v1/solicitudes/{idSolicitud}/estado", new { estado = "rechazado", motivo, rowVersion = await api.VersionAsync(idSolicitud) })
            .EsperarAsync(HttpStatusCode.OK);

    private async Task<HttpResponseMessage> EstadoAsync(HttpClient cliente, int id, object cuerpo) =>
        await cliente.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/solicitudes/{id}/estado", await api.VersionAsync(id), cuerpo));

    // ── Envio en linea ──

    [Fact]
    public async Task SinCorreoConfirmado_Responde403_YNoGuardaNada()
    {
        var (_, _, token) = await api.RegistrarClienteAsync();
        var antes = await ContarAsync();

        var rechazo = await EnviarAsync(token).EsperarAsync(HttpStatusCode.Forbidden);

        Assert.Equal("correo_sin_confirmar", rechazo.GetProperty("codigo").GetString());
        Assert.Equal(antes, await ContarAsync());
    }

    [Fact]
    public async Task UnaSolicitudValida_DevuelveElComprobante_VinculaLosAdjuntos_YDejaHistorialAvisoYConfirmacion()
    {
        var (idCliente, correo, token) = await api.CrearClienteConfirmadoAsync();
        var adjunto = await SubirBorradorAsync(token);
        var pdf = await SubirBorradorAsync(token, Pdf, "diseño final.pdf");

        var creada = await EnviarAsync(token, ApiFixture.CuerpoSolicitud(adjuntos: [adjunto, pdf])).EsperarAsync(HttpStatusCode.Created);

        var id = creada.GetProperty("idSolicitud").GetInt32();
        Assert.Equal($"#{id}", creada.GetProperty("numero").GetString());
        Assert.Equal("online", creada.GetProperty("origen").GetString());
        Assert.Equal("solicitado", creada.GetProperty("estado").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$", creada.GetProperty("fechaChile").GetString());
        Assert.Equal(correo, creada.GetProperty("cliente").GetProperty("correo").GetString());
        var detalle = Assert.Single(creada.GetProperty("detalles").EnumerateArray());
        Assert.Equal(1000, detalle.GetProperty("anchoMm").GetInt32());
        // 1 m × 2 m × 2 unidades, calculado en el servidor.
        Assert.Equal(4m, detalle.GetProperty("areaM2").GetDecimal());
        Assert.Equal(4m, creada.GetProperty("areaTotalM2").GetDecimal());
        var adjuntos = creada.GetProperty("adjuntos").EnumerateArray().ToList();
        Assert.Equal(2, adjuntos.Count);
        Assert.Equal($"/api/v1/adjuntos/{adjunto}/miniatura", adjuntos[0].GetProperty("urlMiniatura").GetString());
        Assert.Equal(JsonValueKind.Null, adjuntos[1].GetProperty("urlMiniatura").ValueKind);
        Assert.Equal("diseño final.pdf", adjuntos[1].GetProperty("nombre").GetString());
        Assert.False(string.IsNullOrEmpty(creada.GetProperty("rowVersion").GetString()));

        var historial = await api.ConsultarAsync(db => db.SolicitudesHistorial.AsNoTracking().SingleAsync(h => h.IdSolicitud == id));
        Assert.Null(historial.EstadoAnterior);
        Assert.Equal("solicitado", historial.EstadoNuevo);
        Assert.Equal(idCliente, historial.IdCliente);
        var huella = await api.ConsultarAsync(db => db.Solicitudes.Where(s => s.IdSolicitud == id).Select(s => s.HuellaIp).SingleAsync());
        Assert.Null(huella);

        var aviso = await api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking().SingleAsync(c => c.Referencia == $"solicitud:{id}"));
        Assert.Equal($"Nueva solicitud #{id} - Cli Web", aviso.Asunto);
        Assert.Contains("2 archivos adjuntos (se ven en el panel)", aviso.CuerpoTexto);
        Assert.Contains("4 m²", aviso.CuerpoTexto);
        Assert.Contains($"https://synctime.test/panel/solicitudes/{id}", aviso.CuerpoHtml);
        var confirmacion = await api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking().SingleAsync(c => c.Referencia == $"confirmacion:{id}"));
        Assert.Equal(correo, confirmacion.Destinatarios);
        Assert.Equal($"Recibimos tu solicitud #{id}", confirmacion.Asunto);
        Assert.Contains("no respondas", confirmacion.CuerpoTexto);

        // El cliente ve su comprobante y su lista; la de otro cliente no existe para el.
        var comprobante = await api.Cliente(token).GetAsync($"/api/v1/mis/solicitudes/{id}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(id, comprobante.GetProperty("idSolicitud").GetInt32());
        var lista = await api.Cliente(token).GetAsync("/api/v1/mis/solicitudes?pagina=1&tamano=5").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(1, lista.GetProperty("total").GetInt32());
        Assert.Equal(2, lista.GetProperty("elementos")[0].GetProperty("cantidadAdjuntos").GetInt32());
        var (_, _, otro) = await api.CrearClienteConfirmadoAsync();
        await api.Cliente(otro).GetAsync($"/api/v1/mis/solicitudes/{id}").EsperarAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LosErrores_VanPorCampo()
    {
        var (_, _, token) = await api.CrearClienteConfirmadoAsync();
        var antes = await ContarAsync();

        var rechazo = await EnviarAsync(token, new
        {
            descripcionGeneral = "corta",
            detalles = new object[] { new { idProducto = 1, anchoMm = 100, cantidad = 1 }, new { idProducto = 999999, cantidad = 1000 } }
        }).EsperarAsync(HttpStatusCode.BadRequest);

        var errores = rechazo.GetProperty("errores");
        foreach (var campo in new[] { "descripcionGeneral", "detalles[0].altoMm", "detalles[1].idProducto", "detalles[1].cantidad" })
            Assert.True(errores.TryGetProperty(campo, out _), $"Falta el error de {campo}: {errores}");
        Assert.Equal(antes, await ContarAsync());
    }

    // ── Regla contra solicitudes falsas (RFS04, RNFP33) ──

    // Para cada motivo: 409 contacto_directo con el mismo mensaje, y la base no cambia (ni solicitud, ni
    // adjuntos vinculados, ni correos, ni historial). El borrador que se intento vincular sigue borrador.
    [Fact]
    public async Task LaReglaContraFalsas_NoEscribeNada_YDiceLoMismoPorCadaMotivo()
    {
        var mensajes = new List<string>();

        async Task ComprobarRechazoAsync(string token, string? ip = null)
        {
            var borrador = await SubirBorradorAsync(token);
            var antes = await ContarAsync();
            var rechazo = await EnviarAsync(token, ApiFixture.CuerpoSolicitud(adjuntos: [borrador]), ip).EsperarAsync(HttpStatusCode.Conflict);
            Assert.Equal("contacto_directo", rechazo.GetProperty("codigo").GetString());
            mensajes.Add(rechazo.GetProperty("error").GetString()!);
            Assert.Equal(antes, await ContarAsync());
            Assert.Null(await api.ConsultarAsync(db => db.AdjuntosSolicitud.Where(a => a.IdAdjunto == borrador).Select(a => a.IdSolicitud).SingleAsync()));
            await api.Cliente(token).DeleteAsync($"/api/v1/mis/adjuntos/{borrador}").EsperarAsync(HttpStatusCode.NoContent);
        }

        // 1. La cuenta ya tiene una solicitud en linea pendiente.
        var (_, _, cuenta) = await api.CrearClienteConfirmadoAsync();
        await EnviarAsync(cuenta).EsperarAsync(HttpStatusCode.Created);
        await ComprobarRechazoAsync(cuenta);

        // 2. Hay otra solicitud pendiente con el mismo RUT (aqui, una presencial del mismo cliente).
        var (idConRut, _, conRut) = await api.CrearClienteConfirmadoAsync(rut: api.Rut());
        await api.CrearSolicitudAsync(idConRut);
        await ComprobarRechazoAsync(conRut);

        // 3. La IP tiene una solicitud en linea pendiente (de otra cuenta).
        var ip = IpNueva();
        var (_, _, primera) = await api.CrearClienteConfirmadoAsync();
        await EnviarAsync(primera, ip: ip).EsperarAsync(HttpStatusCode.Created);
        var (_, _, segunda) = await api.CrearClienteConfirmadoAsync();
        await ComprobarRechazoAsync(segunda, ip);

        // 4. RNFP33: la IP ya envio 3 en la ultima hora (ninguna pendiente: el personal las rechazo).
        var rafaga = IpNueva();
        var deLaRafaga = new List<int>();
        for (var i = 0; i < 3; i++)
        {
            var (_, _, token) = await api.CrearClienteConfirmadoAsync();
            var creada = await EnviarAsync(token, ip: rafaga).EsperarAsync(HttpStatusCode.Created);
            deLaRafaga.Add(creada.GetProperty("idSolicitud").GetInt32());
            await RechazarAsync(deLaRafaga[^1]);
        }
        var (_, _, cuarta) = await api.CrearClienteConfirmadoAsync();
        await ComprobarRechazoAsync(cuarta, rafaga);

        // Pasada la hora, la misma IP puede volver a enviar.
        await api.ConsultarAsync(db => db.Solicitudes.Where(s => deLaRafaga.Contains(s.IdSolicitud))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.FechaSolicitud, x => x.FechaSolicitud.AddMinutes(-61))));
        await EnviarAsync(cuarta, ip: rafaga).EsperarAsync(HttpStatusCode.Created);

        Assert.Equal(4, mensajes.Count);
        Assert.All(mensajes, m => Assert.Equal(Mensaje, m));

        // La IP nunca se guarda en claro: solo su huella HMAC de 64 caracteres hexadecimales.
        var huellas = await api.ConsultarAsync(db => db.Solicitudes.Where(s => s.HuellaIp != null).Select(s => s.HuellaIp!).ToListAsync());
        Assert.NotEmpty(huellas);
        Assert.All(huellas, h => Assert.Matches("^[0-9a-f]{64}$", h));
        Assert.DoesNotContain(huellas, h => h.Contains(rafaga));
    }

    [Fact]
    public async Task OtraIp_SinPendientes_SiPuedeEnviar()
    {
        var (_, _, token) = await api.CrearClienteConfirmadoAsync();
        await EnviarAsync(token, ip: IpNueva()).EsperarAsync(HttpStatusCode.Created);
    }

    // ── Outbox: la solicitud y sus correos se guardan juntos ──

    // Si encolar falla, la transaccion completa se revierte: no queda la solicitud sin su aviso, ni los
    // adjuntos vinculados a una solicitud que no existe. El cliente recibe 500 y puede reintentar.
    [Fact]
    public async Task SiLaColaFallaAlEncolar_SeRevierteTodaLaTransaccion()
    {
        var (_, _, token) = await api.CrearClienteConfirmadoAsync();
        var borrador = await SubirBorradorAsync(token);
        var antes = await ContarAsync();

        var cliente = api.ClienteConInterceptor(new FallaAlEncolar(), token);
        using var respuesta = await cliente.PostAsJsonAsync("/api/v1/mis/solicitudes", ApiFixture.CuerpoSolicitud(adjuntos: [borrador]));

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal(antes, await ContarAsync());
        Assert.Null(await api.ConsultarAsync(db => db.AdjuntosSolicitud.Where(a => a.IdAdjunto == borrador).Select(a => a.IdSolicitud).SingleAsync()));

        // Sin la falla, la misma peticion se guarda con su aviso y su confirmacion.
        var creada = await EnviarAsync(token, ApiFixture.CuerpoSolicitud(adjuntos: [borrador])).EsperarAsync(HttpStatusCode.Created);
        var id = creada.GetProperty("idSolicitud").GetInt32();
        Assert.Equal(2, await api.ConsultarAsync(db => db.CorreosSalientes.CountAsync(c => c.Referencia == $"solicitud:{id}" || c.Referencia == $"confirmacion:{id}")));
    }

    private sealed class FallaAlEncolar : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<CorreoSaliente>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Falla simulada de la cola de correo.");
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task LaPresencial_ConfirmaAlCliente_PeroNoAvisaAlTaller()
    {
        var token = await api.TokenAdminAsync();
        var correo = $"presencial{api.Siguiente()}@correo.test";
        var cliente = await api.Cliente(token).PostAsJsonAsync("/api/v1/clientes/", new { nombre = "Ana", apellido = "Local", telefono = api.Telefono(), email = correo })
            .EsperarAsync(HttpStatusCode.Created);
        var sinCorreo = await api.CrearClienteAsync();
        var idConCorreo = cliente.GetProperty("idCliente").GetInt32();
        await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == idConCorreo)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaVerificacionCorreo, DateTimeOffset.UtcNow)));

        var conCorreo = await api.CrearSolicitudAsync(idConCorreo);
        var sinConfirmacion = await api.CrearSolicitudAsync(sinCorreo);

        var correos = await api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking()
            .Where(c => c.Referencia!.EndsWith($":{conCorreo}") || c.Referencia!.EndsWith($":{sinConfirmacion}")).ToListAsync());
        var confirmacion = Assert.Single(correos);
        Assert.Equal($"confirmacion:{conCorreo}", confirmacion.Referencia);
        Assert.Equal(correo, confirmacion.Destinatarios);
    }

    // ── Estados, historial y edicion ──

    [Fact]
    public async Task LasTransicionesInvalidas_Responden409_YLasValidasDejanHistorial()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var id = await api.CrearSolicitudAsync();
        var url = $"/api/v1/solicitudes/{id}";

        foreach (var estado in new[] { "aceptado", "cotizado", "solicitado" })
        {
            var rechazo = await EstadoAsync(admin, id, new { estado }).EsperarAsync(HttpStatusCode.Conflict);
            Assert.Equal("transicion_invalida", rechazo.GetProperty("codigo").GetString());
            Assert.False(string.IsNullOrWhiteSpace(rechazo.GetProperty("error").GetString()));
        }
        // Rechazar exige un motivo de 10 a 500 caracteres.
        var sinMotivo = await EstadoAsync(admin, id, new { estado = "rechazado", motivo = "corto" }).EsperarAsync(HttpStatusCode.BadRequest);
        // Sin version: 400 sin_version (fase 7, revision de seguridad).
        var sinVersion = await admin.PatchAsJsonAsync($"{url}/estado", new { estado = "rechazado", motivo = "No trabajamos ese material" }).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("sin_version", sinVersion.GetProperty("codigo").GetString());
        Assert.True(sinMotivo.GetProperty("errores").TryGetProperty("motivo", out _));

        // Fase 8: emitir la cotizacion la deja «cotizado» (lo hace el sistema). Devolverla a «solicitado»
        // deja la cotizacion pendiente rechazada, registrada en el local por quien la devolvio.
        var cotizacion = await api.CrearCotizacionAsync(id);
        var devuelta = await EstadoAsync(admin, id, new { estado = "solicitado" }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("solicitado", devuelta.GetProperty("estado").GetString());
        var idCotizacion = cotizacion.GetProperty("idCotizacion").GetInt32();
        var respondida = await api.ConsultarAsync(db => db.Cotizaciones.AsNoTracking().SingleAsync(c => c.IdCotizacion == idCotizacion));
        Assert.Equal("rechazada", respondida.Estado);
        Assert.Equal("presencial", respondida.ViaRespuesta);
        Assert.Equal(await api.IdAdminAsync(), respondida.IdEmpleadoRespuesta);
        Assert.NotNull(respondida.FechaRespuesta);

        // RFU11: cualquier trabajador puede rechazar (aqui, un operario).
        var operario = await api.CrearEmpleadoAsync("operario");
        var rechazada = await RechazarAsync(id, "El cliente desistió del pedido", operario.Token);
        var historial = rechazada.GetProperty("historial").EnumerateArray().ToList();
        Assert.Equal(["solicitado", "cotizado", "solicitado", "rechazado"], historial.Select(h => h.GetProperty("estadoNuevo").GetString()));
        Assert.Equal(JsonValueKind.Null, historial[0].GetProperty("estadoAnterior").ValueKind);
        Assert.Equal("sistema", historial[1].GetProperty("actor").GetString());
        Assert.Equal("cotizado", historial[2].GetProperty("estadoAnterior").GetString());
        Assert.Equal("El cliente desistió del pedido", historial[3].GetProperty("motivo").GetString());
        Assert.Equal("personal", historial[3].GetProperty("actor").GetString());
        Assert.Equal("Emp Prueba", historial[3].GetProperty("por").GetString());
        Assert.Equal("Admin Pruebas", rechazada.GetProperty("registradaPor").GetString());

        // Una rechazada ya no se edita.
        using var editar = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(new { descripcionGeneral = "Otra descripción larga" }) };
        editar.Headers.TryAddWithoutValidation("If-Match", $"\"{rechazada.GetProperty("rowVersion").GetString()}\"");
        await admin.SendAsync(editar).EsperarAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task LaEdicion_UsaIfMatch_YEnCotizadoVuelveASolicitado()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var id = await api.CrearSolicitudAsync();
        var url = $"/api/v1/solicitudes/{id}";
        // Fase 8: emitir la cotizacion deja la solicitud «cotizado».
        var cotizacion = await api.CrearCotizacionAsync(id);

        using var abierta = await admin.GetAsync(url);
        var etag = abierta.Headers.ETag!.Tag;

        HttpRequestMessage Put(string tag, string descripcion) =>
            new(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(new { descripcionGeneral = descripcion, detalles = new[] { new { idProducto = 1, anchoMm = 500, altoMm = 500, cantidad = 3 } } }),
                Headers = { { "If-Match", tag } }
            };

        var editada = await admin.SendAsync(Put(etag, "Ahora son tres letreros cuadrados")).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("solicitado", editada.GetProperty("estado").GetString());
        Assert.Equal(0.75m, editada.GetProperty("areaTotalM2").GetDecimal());
        var idCotizacion = cotizacion.GetProperty("idCotizacion").GetInt32();
        Assert.Equal("rechazada", await api.ConsultarAsync(db => db.Cotizaciones.Where(c => c.IdCotizacion == idCotizacion).Select(c => c.Estado).SingleAsync()));
        Assert.Contains(editada.GetProperty("historial").EnumerateArray(), h => h.GetProperty("estadoAnterior").GetString() == "cotizado");

        // Con la version vieja: otra persona ya cambio la solicitud.
        var obsoleta = await admin.SendAsync(Put(etag, "Edición con datos viejos")).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("version_obsoleta", obsoleta.GetProperty("codigo").GetString());
        Assert.StartsWith("Otra persona modificó esta solicitud", obsoleta.GetProperty("error").GetString());
        var actual = await admin.GetAsync(url).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("Ahora son tres letreros cuadrados", actual.GetProperty("descripcionGeneral").GetString());
    }

    [Fact]
    public async Task ElBorradoLogico_SoloEnSolicitadoORechazado()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var id = await api.CrearSolicitudAsync();
        await api.ConsultarAsync(db => db.Solicitudes.Where(s => s.IdSolicitud == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, "cotizado")));

        // RFU11: el borrado logico es de todo el personal; exige la version vigente.
        var operario = api.Cliente(await api.TokenEmpleadoAsync("operario"));
        HttpRequestMessage Eliminar(string version) => ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/solicitudes/{id}/eliminar-logico", version);
        var sinVersion = await operario.PatchAsync($"/api/v1/solicitudes/{id}/eliminar-logico", null).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("sin_version", sinVersion.GetProperty("codigo").GetString());
        await operario.SendAsync(Eliminar(await api.VersionAsync(id))).EsperarAsync(HttpStatusCode.Conflict);
        var vieja = await api.VersionAsync(id);
        await EstadoAsync(operario, id, new { estado = "rechazado", motivo = "Pedido duplicado por error" }).EsperarAsync(HttpStatusCode.OK);
        var obsoleta = await operario.SendAsync(Eliminar(vieja)).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("version_obsoleta", obsoleta.GetProperty("codigo").GetString());
        await operario.SendAsync(Eliminar(await api.VersionAsync(id))).EsperarAsync(HttpStatusCode.OK);
        // El DELETE sigue siendo solo de gestion.
        await operario.SendAsync(ApiFixture.ConVersion(HttpMethod.Delete, $"/api/v1/solicitudes/{id}", vieja)).EsperarAsync(HttpStatusCode.Forbidden);

        Assert.True(await api.ConsultarAsync(db => db.Solicitudes.Where(s => s.IdSolicitud == id).Select(s => s.EliminadoLogico).SingleAsync()));
        await admin.GetAsync($"/api/v1/solicitudes/{id}").EsperarAsync(HttpStatusCode.NotFound);
    }

    // ── Adjuntos privados ──

    [Fact]
    public async Task LosAdjuntos_SeReconocenPorSuContenido()
    {
        var (_, _, token) = await api.CrearClienteConfirmadoAsync();

        var imagen = await api.Cliente(token).PostAsync("/api/v1/mis/adjuntos", Formulario(Png(1200, 900), "logo.png", "image/png")).EsperarAsync(HttpStatusCode.Created);
        Assert.Equal("imagen", imagen.GetProperty("tipo").GetString());
        Assert.Equal(1200, imagen.GetProperty("ancho").GetInt32());
        Assert.Equal("logo.png", imagen.GetProperty("nombre").GetString());

        var pdf = await api.Cliente(token).PostAsync("/api/v1/mis/adjuntos", Formulario(Pdf, "../../diseño.pdf", "application/pdf")).EsperarAsync(HttpStatusCode.Created);
        Assert.Equal("pdf", pdf.GetProperty("tipo").GetString());
        Assert.Equal("diseño.pdf", pdf.GetProperty("nombre").GetString());
        Assert.Equal(JsonValueKind.Null, pdf.GetProperty("urlMiniatura").ValueKind);

        // Un SVG (puede llevar codigo) no pasa aunque diga ser PNG, y un PNG renombrado a .pdf sigue siendo imagen.
        foreach (var (datos, nombre, tipo) in new[] { (Svg, "logo.svg", "image/svg+xml"), (Svg, "logo.png", "image/png"), (Encoding.ASCII.GetBytes("PDF falso"), "a.pdf", "application/pdf") })
        {
            var rechazo = await api.Cliente(token).PostAsync("/api/v1/mis/adjuntos", Formulario(datos, nombre, tipo)).EsperarAsync(HttpStatusCode.BadRequest);
            Assert.Equal("formato_no_aceptado", rechazo.GetProperty("codigo").GetString());
            Assert.Contains("expórtalo a PDF o PNG", rechazo.GetProperty("error").GetString());
        }
        var renombrado = await api.Cliente(token).PostAsync("/api/v1/mis/adjuntos", Formulario(Png(), "logo.pdf")).EsperarAsync(HttpStatusCode.Created);
        Assert.Equal("imagen", renombrado.GetProperty("tipo").GetString());

        // Mas de 10 MB: 400 por archivo.
        var grande = new byte[10 * 1024 * 1024 + 1];
        Pdf.CopyTo(grande, 0);
        var muyGrande = await api.Cliente(token).PostAsync("/api/v1/mis/adjuntos", Formulario(grande, "enorme.pdf")).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("archivo_muy_grande", muyGrande.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task ElTopeDeAdjuntos_EsDe5()
    {
        var (_, _, token) = await api.CrearClienteConfirmadoAsync();
        for (var i = 0; i < 5; i++)
            await SubirBorradorAsync(token, Pdf, $"plano{i}.pdf");

        var tope = await api.Cliente(token).PostAsync("/api/v1/mis/adjuntos", Formulario(Pdf, "sexto.pdf")).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("tope_adjuntos", tope.GetProperty("codigo").GetString());

        // El personal tampoco pasa de 5 en una solicitud.
        var admin = api.Cliente(await api.TokenAdminAsync());
        var id = await api.CrearSolicitudAsync();
        for (var i = 0; i < 5; i++)
            await admin.PostAsync($"/api/v1/solicitudes/{id}/adjuntos", Formulario(Pdf, $"plano{i}.pdf")).EsperarAsync(HttpStatusCode.Created);
        await admin.PostAsync($"/api/v1/solicitudes/{id}/adjuntos", Formulario(Pdf, "sexto.pdf")).EsperarAsync(HttpStatusCode.Conflict);
        var ficha = await admin.GetAsync($"/api/v1/solicitudes/{id}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(5, ficha.GetProperty("adjuntos").GetArrayLength());
    }

    [Fact]
    public async Task UnAdjunto_SoloLoVenSuDuenoYElPersonal()
    {
        var (_, _, duena) = await api.CrearClienteConfirmadoAsync();
        var (_, _, ajeno) = await api.CrearClienteConfirmadoAsync();
        var imagen = await SubirBorradorAsync(duena);
        var pdf = await SubirBorradorAsync(duena, Pdf, "diseño final.pdf");
        await EnviarAsync(duena, ApiFixture.CuerpoSolicitud(adjuntos: [imagen, pdf])).EsperarAsync(HttpStatusCode.Created);

        foreach (var ruta in new[] { $"/api/v1/adjuntos/{imagen}/miniatura", $"/api/v1/adjuntos/{imagen}/archivo", $"/api/v1/adjuntos/{pdf}/archivo" })
        {
            await api.Cliente(ajeno).GetAsync(ruta).EsperarAsync(HttpStatusCode.NotFound);
            await api.Cliente().GetAsync(ruta).EsperarAsync(HttpStatusCode.Unauthorized);
            foreach (var token in new[] { duena, await api.TokenEmpleadoAsync("operario") })
            {
                using var respuesta = await api.Cliente(token).GetAsync(ruta);
                Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
                Assert.Contains("no-store", respuesta.Headers.CacheControl!.ToString());
                Assert.Contains("private", respuesta.Headers.CacheControl!.ToString());
                Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
            }
        }

        using var descarga = await api.Cliente(duena).GetAsync($"/api/v1/adjuntos/{pdf}/archivo");
        Assert.Equal("application/pdf", descarga.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", descarga.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(Pdf, await descarga.Content.ReadAsByteArrayAsync());
        using var webp = await api.Cliente(duena).GetAsync($"/api/v1/adjuntos/{imagen}/archivo");
        Assert.Equal("image/webp", webp.Content.Headers.ContentType!.MediaType);
        // Un PDF no tiene miniatura.
        await api.Cliente(duena).GetAsync($"/api/v1/adjuntos/{pdf}/miniatura").EsperarAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnBorradorAjeno_NoSePuedeVincularNiBorrar()
    {
        var (_, _, duena) = await api.CrearClienteConfirmadoAsync();
        var (_, _, otro) = await api.CrearClienteConfirmadoAsync();
        var borrador = await SubirBorradorAsync(duena);
        var antes = await ContarAsync();

        var rechazo = await EnviarAsync(otro, ApiFixture.CuerpoSolicitud(adjuntos: [borrador])).EsperarAsync(HttpStatusCode.BadRequest);

        Assert.True(rechazo.GetProperty("errores").TryGetProperty("adjuntos", out _));
        Assert.Equal(antes, await ContarAsync());
        await api.Cliente(otro).DeleteAsync($"/api/v1/mis/adjuntos/{borrador}").EsperarAsync(HttpStatusCode.NotFound);
        await api.Cliente(otro).GetAsync($"/api/v1/adjuntos/{borrador}/miniatura").EsperarAsync(HttpStatusCode.NotFound);
        using (var propia = await api.Cliente(duena).GetAsync($"/api/v1/adjuntos/{borrador}/miniatura"))
            Assert.Equal(HttpStatusCode.OK, propia.StatusCode);
        await api.Cliente(duena).DeleteAsync($"/api/v1/mis/adjuntos/{borrador}").EsperarAsync(HttpStatusCode.NoContent);
        await api.Cliente(duena).GetAsync($"/api/v1/adjuntos/{borrador}/miniatura").EsperarAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LaPurga_BorraLosBorradoresDeMasDe24Horas_ConSusArchivos()
    {
        var (_, _, token) = await api.CrearClienteConfirmadoAsync();
        var viejo = await SubirBorradorAsync(token);
        var nuevo = await SubirBorradorAsync(token, Pdf, "plano.pdf");
        var clave = await api.ConsultarAsync(db => db.AdjuntosSolicitud.Where(a => a.IdAdjunto == viejo).Select(a => a.Clave).SingleAsync());
        await api.ConsultarAsync(db => db.AdjuntosSolicitud.Where(a => a.IdAdjunto == viejo)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.FechaSubida, DateTimeOffset.UtcNow.AddHours(-25))));
        Assert.NotEmpty(Directory.GetFiles(api.CarpetaAdjuntos, $"{clave}*"));

        await using (var alcance = api.Services.CreateAsyncScope())
            await alcance.ServiceProvider.GetRequiredService<Services.PurgaDatosPersonales>().PurgarAsync(CancellationToken.None);

        var quedan = await api.ConsultarAsync(db => db.AdjuntosSolicitud.Where(a => a.IdAdjunto == viejo || a.IdAdjunto == nuevo).Select(a => a.IdAdjunto).ToListAsync());
        Assert.Equal([nuevo], quedan);
        Assert.Empty(Directory.GetFiles(api.CarpetaAdjuntos, $"{clave}*"));
    }

    // ── Revision de seguridad ──

    [Fact]
    public async Task SubirUnAdjunto_ExigeCorreoConfirmado()
    {
        var (_, _, token) = await api.RegistrarClienteAsync();
        var antes = await api.ConsultarAsync(db => db.AdjuntosSolicitud.CountAsync());

        var rechazo = await api.Cliente(token).PostAsync("/api/v1/mis/adjuntos", Formulario(Pdf, "plano.pdf")).EsperarAsync(HttpStatusCode.Forbidden);

        Assert.Equal("correo_sin_confirmar", rechazo.GetProperty("codigo").GetString());
        Assert.Equal(antes, await api.ConsultarAsync(db => db.AdjuntosSolicitud.CountAsync()));
    }

    [Fact]
    public async Task LasSubidas_TienenSuPropioLimitePorMinuto()
    {
        await using var conLimite = api.ConVariable("RateLimiting__Adjuntos", "2");
        var (_, _, token) = await api.CrearClienteConfirmadoAsync();
        var cliente = conLimite.Cliente(token);

        await cliente.PostAsync("/api/v1/mis/adjuntos", Formulario(Pdf, "a.pdf")).EsperarAsync(HttpStatusCode.Created);
        await cliente.PostAsync("/api/v1/mis/adjuntos", Formulario(Pdf, "b.pdf")).EsperarAsync(HttpStatusCode.Created);
        await cliente.PostAsync("/api/v1/mis/adjuntos", Formulario(Pdf, "c.pdf")).EsperarAsync(HttpStatusCode.TooManyRequests);
        // Las demas rutas del cliente no comparten ese limite.
        await cliente.GetAsync("/api/v1/mis/solicitudes").EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task QuitarOSubirUnAdjuntoDelPersonal_CambiaLaVersion_YQuitarloQuedaEnElHistorial()
    {
        var operario = api.Cliente(await api.TokenEmpleadoAsync("operario"));
        var id = await api.CrearSolicitudAsync();
        var v0 = await api.VersionAsync(id);

        var subido = await operario.PostAsync($"/api/v1/solicitudes/{id}/adjuntos", Formulario(Pdf, "plano.pdf")).EsperarAsync(HttpStatusCode.Created);
        var v1 = await api.VersionAsync(id);
        Assert.NotEqual(v0, v1);

        await operario.DeleteAsync($"/api/v1/solicitudes/{id}/adjuntos/{subido.GetProperty("idAdjunto").GetInt32()}").EsperarAsync(HttpStatusCode.NoContent);
        var ficha = await operario.GetAsync($"/api/v1/solicitudes/{id}").EsperarAsync(HttpStatusCode.OK);
        Assert.NotEqual(v1, ficha.GetProperty("rowVersion").GetString());
        Assert.Equal(0, ficha.GetProperty("adjuntos").GetArrayLength());
        var ultimo = ficha.GetProperty("historial").EnumerateArray().Last();
        Assert.Equal("solicitado", ultimo.GetProperty("estadoAnterior").GetString());
        Assert.Equal("solicitado", ultimo.GetProperty("estadoNuevo").GetString());
        Assert.Equal("Se quitó el adjunto plano.pdf", ultimo.GetProperty("motivo").GetString());
        Assert.Equal("personal", ultimo.GetProperty("actor").GetString());
    }

    [Fact]
    public async Task LaPresencial_NoConfirmaAUnCorreoSinVerificar()
    {
        var token = await api.TokenAdminAsync();
        var cliente = await api.Cliente(token).PostAsJsonAsync("/api/v1/clientes/", new { nombre = "Ana", apellido = "Local", telefono = api.Telefono(), email = $"sinverificar{api.Siguiente()}@correo.test" })
            .EsperarAsync(HttpStatusCode.Created);

        var id = await api.CrearSolicitudAsync(cliente.GetProperty("idCliente").GetInt32());

        Assert.False(await api.ConsultarAsync(db => db.CorreosSalientes.AnyAsync(c => c.Referencia == $"confirmacion:{id}")));
    }

    [Fact]
    public async Task SiNoSeConsigueElTurno_Responde503ConRetryAfter_YNoGuardaNada()
    {
        var (_, _, token) = await api.CrearClienteConfirmadoAsync();
        await using var conEsperaCorta = api.ConVariable("Solicitudes__EsperaBloqueoMs", "300");
        var conexion = await api.ConsultarAsync(db => Task.FromResult(db.Database.GetConnectionString()!));
        await using var otra = new Microsoft.Data.SqlClient.SqlConnection(conexion);
        await otra.OpenAsync();
        await using var transaccion = (Microsoft.Data.SqlClient.SqlTransaction)await otra.BeginTransactionAsync();
        await using (var tomar = new Microsoft.Data.SqlClient.SqlCommand(
            "EXEC sp_getapplock @Resource = N'synctime_solicitudes_en_linea', @LockMode = N'Exclusive', @LockOwner = N'Transaction'", otra, transaccion))
            await tomar.ExecuteNonQueryAsync();
        var antes = await ContarAsync();

        using var respuesta = await conEsperaCorta.Cliente(token).PostAsJsonAsync("/api/v1/mis/solicitudes", ApiFixture.CuerpoSolicitud());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.NotNull(respuesta.Headers.RetryAfter);
        var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("servicio_ocupado", cuerpo.GetProperty("codigo").GetString());
        await transaccion.RollbackAsync();
        Assert.Equal(antes, await ContarAsync());
    }

    [Fact]
    public async Task LosFiltrosInvalidos_Responden400()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        foreach (var filtro in new[] { "estado=inventado", "origen=telefono", "orden=nombre" })
            await admin.GetAsync($"/api/v1/solicitudes?{filtro}").EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnAdjuntoDeUnaSolicitudEliminada_YaNoLoVeElCliente_PeroSiElPersonal()
    {
        var (_, _, token) = await api.CrearClienteConfirmadoAsync();
        var adjunto = await SubirBorradorAsync(token, Pdf, "plano.pdf");
        var id = (await EnviarAsync(token, ApiFixture.CuerpoSolicitud(adjuntos: [adjunto])).EsperarAsync(HttpStatusCode.Created)).GetProperty("idSolicitud").GetInt32();
        var admin = api.Cliente(await api.TokenAdminAsync());
        await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/solicitudes/{id}/eliminar-logico", await api.VersionAsync(id))).EsperarAsync(HttpStatusCode.OK);

        await api.Cliente(token).GetAsync($"/api/v1/adjuntos/{adjunto}/archivo").EsperarAsync(HttpStatusCode.NotFound);
        using var delPersonal = await admin.GetAsync($"/api/v1/adjuntos/{adjunto}/archivo");
        Assert.Equal(HttpStatusCode.OK, delPersonal.StatusCode);
    }

    [Fact]
    public async Task LosBorradores_SeRecuperan_SoloLosPropiosYSinVincular()
    {
        var (_, _, token) = await api.CrearClienteConfirmadoAsync();
        var (_, _, otro) = await api.CrearClienteConfirmadoAsync();
        var vinculado = await SubirBorradorAsync(token, Pdf, "enviado.pdf");
        await EnviarAsync(token, ApiFixture.CuerpoSolicitud(adjuntos: [vinculado])).EsperarAsync(HttpStatusCode.Created);
        var primero = await SubirBorradorAsync(token);
        var segundo = await SubirBorradorAsync(token, Pdf, "plano.pdf");
        await SubirBorradorAsync(otro, Pdf, "ajeno.pdf");

        var borradores = await api.Cliente(token).GetAsync("/api/v1/mis/adjuntos").EsperarAsync(HttpStatusCode.OK);

        var lista = borradores.EnumerateArray().ToList();
        Assert.Equal([primero, segundo], lista.Select(a => a.GetProperty("idAdjunto").GetInt32()));
        Assert.Equal("imagen", lista[0].GetProperty("tipo").GetString());
        Assert.Equal("logo.png", lista[0].GetProperty("nombre").GetString());
        Assert.Equal($"/api/v1/adjuntos/{primero}/miniatura", lista[0].GetProperty("urlMiniatura").GetString());
        Assert.True(lista[0].GetProperty("ancho").GetInt32() > 0);
        Assert.Equal("pdf", lista[1].GetProperty("tipo").GetString());
        Assert.Equal(JsonValueKind.Null, lista[1].GetProperty("urlMiniatura").ValueKind);
        Assert.Equal(JsonValueKind.Null, lista[1].GetProperty("ancho").ValueKind);
        Assert.True(lista[1].GetProperty("bytes").GetInt32() > 0);
        await api.Cliente(await api.TokenAdminAsync()).GetAsync("/api/v1/mis/adjuntos").EsperarAsync(HttpStatusCode.Forbidden);
    }

    // ── Bandeja del personal y clientes ──

    [Fact]
    public async Task LaBandeja_FiltraBuscaYPagina()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var (idCliente, _, token) = await api.CrearClienteConfirmadoAsync();
        var enLinea = (await EnviarAsync(token).EsperarAsync(HttpStatusCode.Created)).GetProperty("idSolicitud").GetInt32();
        var presencial = await api.CrearSolicitudAsync(idCliente);
        var telefono = await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == idCliente).Select(c => c.Telefono).SingleAsync());

        var porTelefono = await admin.GetAsync($"/api/v1/solicitudes?q={telefono}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal([presencial, enLinea], porTelefono.GetProperty("elementos").EnumerateArray().Select(e => e.GetProperty("idSolicitud").GetInt32()));
        var fila = porTelefono.GetProperty("elementos")[1];
        Assert.Equal($"#{enLinea}", fila.GetProperty("numero").GetString());
        Assert.Equal("online", fila.GetProperty("origen").GetString());
        Assert.Equal(1, fila.GetProperty("cantidadItems").GetInt32());
        Assert.Equal(0, fila.GetProperty("cantidadAdjuntos").GetInt32());
        Assert.Equal(JsonValueKind.Null, fila.GetProperty("registradaPor").ValueKind);
        Assert.Equal(idCliente, fila.GetProperty("cliente").GetProperty("idCliente").GetInt32());

        var soloEnLinea = await admin.GetAsync($"/api/v1/solicitudes?q={telefono}&origen=online&orden=fecha_asc").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(enLinea, Assert.Single(soloEnLinea.GetProperty("elementos").EnumerateArray()).GetProperty("idSolicitud").GetInt32());
        var porNumero = await admin.GetAsync($"/api/v1/solicitudes?q=%23{presencial}").EsperarAsync(HttpStatusCode.OK);
        Assert.Contains(porNumero.GetProperty("elementos").EnumerateArray(), e => e.GetProperty("idSolicitud").GetInt32() == presencial);
        var hoy = DateTime.Now.ToString("yyyy-MM-dd");
        var pagina = await admin.GetAsync($"/api/v1/solicitudes?estado=solicitado&desde={hoy}&hasta={hoy}&pagina=1&tamano=1").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(1, pagina.GetProperty("elementos").GetArrayLength());
        Assert.True(pagina.GetProperty("total").GetInt32() >= 2);
        Assert.True(pagina.GetProperty("totalPaginas").GetInt32() >= 2);
        var manana = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
        var vacia = await admin.GetAsync($"/api/v1/solicitudes?desde={manana}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(0, vacia.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task LosClientes_SeListanPaginados_YSeBuscanPorTelefonoORut()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var rut = "12.345.678-5";
        var telefono = api.Telefono();
        // Solo letras (regla de nombres de la fase 12D): el contador se escribe con letras, a-j en lugar de 0-9.
        var apellido = "Buscable" + string.Concat(api.Siguiente().ToString().Select(d => (char)('a' + (d - '0'))));
        var existente = await api.ConsultarAsync(db => db.Clientes.Where(c => c.Rut == "12345678-5").Select(c => (int?)c.IdCliente).FirstOrDefaultAsync());
        var creado = existente ?? (await admin.PostAsJsonAsync("/api/v1/clientes/", new { rut = "12345678-5", nombre = "Rosa", apellido, telefono })
            .EsperarAsync(HttpStatusCode.Created)).GetProperty("idCliente").GetInt32();
        if (existente is not null)
            await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == creado).ExecuteUpdateAsync(s => s.SetProperty(c => c.Apellido, apellido).SetProperty(c => c.Telefono, telefono)));

        var lista = await admin.GetAsync($"/api/v1/clientes?q={apellido}&pagina=1&tamano=10").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(1, lista.GetProperty("total").GetInt32());
        Assert.Equal(creado, lista.GetProperty("elementos")[0].GetProperty("idCliente").GetInt32());
        var todos = await admin.GetAsync("/api/v1/clientes?tamano=2").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(2, todos.GetProperty("elementos").GetArrayLength());

        var porTelefono = await admin.GetAsync($"/api/v1/clientes/buscar?telefono=%2B56{telefono}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(creado, Assert.Single(porTelefono.EnumerateArray()).GetProperty("idCliente").GetInt32());
        var porRut = await admin.GetAsync($"/api/v1/clientes/buscar?rut={Uri.EscapeDataString(rut)}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(creado, Assert.Single(porRut.EnumerateArray()).GetProperty("idCliente").GetInt32());
        var nadie = await admin.GetAsync("/api/v1/clientes/buscar?telefono=911111111&rut=1-9").EsperarAsync(HttpStatusCode.OK);
        Assert.Empty(nadie.EnumerateArray());
        await admin.GetAsync("/api/v1/clientes/buscar").EsperarAsync(HttpStatusCode.BadRequest);
    }
}
