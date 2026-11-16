using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 11: los avisos del taller son correos operativos. Cada destino elige si recibe las solicitudes en linea,
// las presenciales o ambas; la baja de un cliente no los toca; lo que no sale queda en «Correos no enviados»
// y se puede reenviar una vez, a los destinos de hoy. Las pruebas dejan la tabla de destinos vacia al terminar.
[Collection(ApiCollection.Name)]
public sealed class AvisosTallerFase11Tests(ApiFixture api)
{
    private static HashSet<string> Para(CorreoSaliente correo) =>
        correo.Destinatarios.Split(';', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private async Task<CorreoSaliente?> AvisoAsync(int idSolicitud) =>
        (await api.CorreosAsync($"solicitud:{idSolicitud}")).SingleOrDefault(c => c.Tipo == TiposCorreo.AvisoSolicitudNueva);

    // ── Preferencia por destino (decision 1) ──

    [Fact]
    public async Task LaPreferencia_EnLineaPresencialesOAmbas_EncolaExactamenteLosAvisosQueCorresponden()
    {
        await api.LimpiarDestinosAsync();
        try
        {
            var soloEnLinea = api.CorreoUnico("enlinea");
            var soloPresenciales = api.CorreoUnico("presencial");
            var ambas = api.CorreoUnico("ambas");
            await api.DestinoActivoAsync(soloEnLinea, enLinea: true, presenciales: false);
            await api.DestinoActivoAsync(soloPresenciales, enLinea: false, presenciales: true);
            await api.DestinoActivoAsync(ambas, enLinea: true, presenciales: true);

            var (enLinea, _, _, _) = await api.SolicitudEnLineaAsync();
            var presencial = await api.CrearSolicitudAsync();

            var avisoEnLinea = await AvisoAsync(enLinea);
            Assert.NotNull(avisoEnLinea);
            Assert.Equal(new HashSet<string>([soloEnLinea, ambas], StringComparer.OrdinalIgnoreCase), Para(avisoEnLinea));
            var avisoPresencial = await AvisoAsync(presencial);
            Assert.NotNull(avisoPresencial);
            Assert.Equal(new HashSet<string>([soloPresenciales, ambas], StringComparer.OrdinalIgnoreCase), Para(avisoPresencial));
            Assert.Contains("presencial", avisoPresencial.CuerpoTexto);
            Assert.Contains("Registrada por", avisoPresencial.CuerpoTexto);
        }
        finally
        {
            await api.LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task UnAvisoQueNadieQuiereRecibir_NoSeEncola_YElRespaldoSoloRecibeLasEnLinea()
    {
        await api.LimpiarDestinosAsync();
        try
        {
            // Sin destinos en la base: el respaldo de la configuracion recibe las en linea, como antes; las presenciales no.
            var (enLinea, _, _, _) = await api.SolicitudEnLineaAsync();
            var presencial = await api.CrearSolicitudAsync();
            Assert.Equal([ApiFixture.AvisosTaller], Para((await AvisoAsync(enLinea))!));
            Assert.Null(await AvisoAsync(presencial));

            // Con un destino que solo quiere las en linea, una presencial no deja ningun aviso (tampoco al respaldo).
            await api.DestinoActivoAsync(api.CorreoUnico("solo"), enLinea: true, presenciales: false);
            Assert.Null(await AvisoAsync(await api.CrearSolicitudAsync()));
        }
        finally
        {
            await api.LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task UnClienteDadoDeBajaDeLosCorreos_SigueGenerandoElAvisoAlTaller()
    {
        var (idCliente, _, token) = await api.CrearClienteConfirmadoAsync();
        var cliente = api.Cliente(token);
        await cliente.PutAsJsonAsync("/api/v1/mi-cuenta/preferencias", new { avisosEstado = false, marketing = false }).EsperarAsync(HttpStatusCode.OK);
        Assert.False(await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == idCliente).Select(c => c.RecibeAvisosEstado).SingleAsync()));

        var creada = await cliente.PostAsJsonAsync("/api/v1/mis/solicitudes", ApiFixture.CuerpoSolicitud()).EsperarAsync(HttpStatusCode.Created);

        var aviso = await AvisoAsync(creada.GetProperty("idSolicitud").GetInt32());
        Assert.NotNull(aviso);
        Assert.Equal([ApiFixture.AvisosTaller], Para(aviso));
    }

    [Fact]
    public async Task UnClienteConLaCuentaDadaDeBaja_SigueGenerandoElAvisoAlTaller()
    {
        await api.LimpiarDestinosAsync();
        try
        {
            var destino = api.CorreoUnico("presenciales");
            await api.DestinoActivoAsync(destino, enLinea: true, presenciales: true);
            var (idCliente, _, token) = await api.CrearClienteConfirmadoAsync();
            await api.Cliente(token).PostAsJsonAsync("/api/v1/mi-cuenta/baja", new { password = ApiFixture.AdminPassword, confirmacion = "DAR DE BAJA" })
                .EsperarAsync(HttpStatusCode.OK);

            // El personal le registra una solicitud en el local: el taller recibe su aviso igual.
            var presencial = await api.CrearSolicitudAsync(idCliente);

            var aviso = await AvisoAsync(presencial);
            Assert.NotNull(aviso);
            Assert.Equal([destino], Para(aviso));
        }
        finally
        {
            await api.LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task LaPreferencia_SeEditaConVersion_ExigeAlMenosUna_YQuedaEnElHistorial()
    {
        await api.LimpiarDestinosAsync();
        try
        {
            var id = await api.DestinoActivoAsync(api.CorreoUnico("pref"), enLinea: true, presenciales: false);
            var admin = api.Cliente(await api.TokenAdminAsync());
            var estado = await admin.GetAsync("/api/v1/avisos-taller").EsperarAsync(HttpStatusCode.OK);
            var destino = estado.GetProperty("destinos").EnumerateArray().Single(d => d.GetProperty("idDestinatario").GetInt32() == id);
            Assert.True(destino.GetProperty("recibeEnLinea").GetBoolean());
            Assert.False(destino.GetProperty("recibePresenciales").GetBoolean());
            var version = destino.GetProperty("rowVersion").GetString()!;
            var url = $"/api/v1/avisos-taller/destinos/{id}/preferencia";

            var ninguna = await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Put, url, version, new { recibeEnLinea = false, recibePresenciales = false }))
                .EsperarAsync(HttpStatusCode.BadRequest);
            Assert.Equal("datos_invalidos", Fase11Apoyo.Codigo(ninguna));
            Assert.True(ninguna.GetProperty("errores").TryGetProperty("preferencia", out _));
            var sinVersion = await admin.PutAsJsonAsync(url, new { recibeEnLinea = true, recibePresenciales = true }).EsperarAsync(HttpStatusCode.BadRequest);
            Assert.Equal("sin_version", Fase11Apoyo.Codigo(sinVersion));
            await api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario))
                .SendAsync(ApiFixture.ConVersion(HttpMethod.Put, url, version, new { recibeEnLinea = true, recibePresenciales = true })).EsperarAsync(HttpStatusCode.Forbidden);

            var guardado = await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Put, url, version, new { recibeEnLinea = true, recibePresenciales = true }))
                .EsperarAsync(HttpStatusCode.OK);
            Assert.True(guardado.GetProperty("recibePresenciales").GetBoolean());
            var obsoleta = await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Put, url, version, new { recibeEnLinea = false, recibePresenciales = true }))
                .EsperarAsync(HttpStatusCode.Conflict);
            Assert.Equal("version_obsoleta", Fase11Apoyo.Codigo(obsoleta));

            Assert.True(await api.ConsultarAsync(db => db.DestinatariosAvisosHistorial.AnyAsync(h => h.IdDestinatario == id && h.Accion == "preferencia"
                && h.IdEmpleado != null)));
            Assert.NotEmpty(await api.AuditoriaAsync("destinatario_aviso", id.ToString(), "modificar"));
        }
        finally
        {
            await api.LimpiarDestinosAsync();
        }
    }

    // ── Correos no enviados, auditoria del trabajador y reenvio ──

    private static Dictionary<string, string> PorSmtp(ServidorSmtpDePrueba smtp) => new()
    {
        ["Correo__Modo"] = "Smtp",
        ["Correo__Smtp__Servidor"] = "127.0.0.1",
        ["Correo__Smtp__Puerto"] = smtp.Puerto.ToString(),
        ["Correo__Smtp__Seguridad"] = "Ninguna",
        ["Correo__Smtp__Usuario"] = "cuenta.pruebas@test.local",
        ["Correo__Smtp__Password"] = "clave-de-prueba",
        ["Correo__Smtp__TiempoEsperaSegundos"] = "2",
        ["Correo__Cola__Activa"] = "false"
    };

    [Fact]
    public async Task UnAvisoQueAgotaLosIntentos_ApareceEnNoEnviados_YLaAuditoriaNoGuardaCuerpoNiDireccion()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.RechazaDestinatario);
        await api.LimpiarColaDeCorreoAsync();
        await api.LimpiarDestinosAsync();
        try
        {
            var destino = api.CorreoUnico("fallido");
            await api.DestinoActivoAsync(destino, enLinea: true, presenciales: false);
            await using var porSmtp = api.ConVariables(PorSmtp(smtp));
            var (idSolicitud, _, _, _) = await api.SolicitudEnLineaAsync(descripcion: "Texto-del-cuerpo-que-no-va-a-la-auditoria", cliente: porSmtp.Cliente());
            await porSmtp.Services.ProcesarColaAsync();

            var aviso = (await AvisoAsync(idSolicitud))!;
            Assert.Equal(EstadosCorreo.Fallido, aviso.Estado);

            var admin = api.Cliente(await api.TokenAdminAsync());
            var lista = await admin.GetAsync("/api/v1/correos/no-enviados?tipo=aviso_solicitud_nueva").EsperarAsync(HttpStatusCode.OK);
            var fila = lista.GetProperty("elementos").EnumerateArray().Single(e => e.GetProperty("idCorreo").GetInt64() == aviso.IdCorreo);
            Assert.Equal("aviso_solicitud_nueva", fila.GetProperty("tipo").GetString());
            Assert.Equal("Aviso de solicitud nueva", fila.GetProperty("tipoTexto").GetString());
            Assert.Equal($"solicitud:{idSolicitud}", fila.GetProperty("referencia").GetString());
            Assert.Equal($"/panel/solicitudes/{idSolicitud}", fila.GetProperty("enlace").GetString());
            Assert.Equal(CorreosDestinosAvisos.Enmascarar(destino), fila.GetProperty("destinoEnmascarado").GetString());
            Assert.Equal(1, fila.GetProperty("intentos").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(fila.GetProperty("motivo").GetString()));
            Assert.False(fila.GetProperty("revisado").GetBoolean());
            Assert.True(fila.GetProperty("reenviable").GetBoolean());
            Assert.Equal(JsonValueKind.Null, fila.GetProperty("reenviadoComo").ValueKind);
            Assert.DoesNotContain(destino, lista.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.True((await admin.GetAsync("/api/v1/correos/no-enviados/resumen").EsperarAsync(HttpStatusCode.OK)).GetProperty("sinRevisar").GetInt32() >= 1);

            var auditoria = Assert.Single(await api.AuditoriaAsync("correo", aviso.IdCorreo.ToString(), "correo_fallido"));
            Assert.Equal("sistema", auditoria.Rol);
            Assert.Contains(CorreosDestinosAvisos.Enmascarar(destino), auditoria.Detalle);
            Assert.Contains($"solicitud:{idSolicitud}", auditoria.Detalle);
            Assert.DoesNotContain(destino, auditoria.Detalle, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Texto-del-cuerpo", auditoria.Detalle);
            Assert.DoesNotContain(aviso.Asunto, auditoria.Detalle);
        }
        finally
        {
            await api.LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task UnAvisoEnviado_QuedaEnLaAuditoria_ConElDestinoEnmascarado()
    {
        await api.LimpiarColaDeCorreoAsync();
        var (idSolicitud, _, _, _) = await api.SolicitudEnLineaAsync(descripcion: "Cuerpo-secreto-del-aviso-enviado");
        await api.Services.ProcesarColaAsync();

        var aviso = (await AvisoAsync(idSolicitud))!;
        Assert.Equal(EstadosCorreo.Enviado, aviso.Estado);
        var registro = Assert.Single(await api.AuditoriaAsync("correo", aviso.IdCorreo.ToString(), "aviso_enviado"));
        Assert.Contains(CorreosDestinosAvisos.Enmascarar(ApiFixture.AvisosTaller), registro.Detalle);
        Assert.DoesNotContain(ApiFixture.AvisosTaller, registro.Detalle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cuerpo-secreto", registro.Detalle);
        Assert.DoesNotContain(aviso.Asunto, registro.Detalle);
        // La confirmacion al cliente no es un aviso del taller: se envia sin registro aviso_enviado.
        var confirmacion = (await api.CorreosAsync($"confirmacion:{idSolicitud}")).Single();
        Assert.Empty(await api.AuditoriaAsync("correo", confirmacion.IdCorreo.ToString(), "aviso_enviado"));
    }

    private async Task<long> FallidoAsync(string tipo, string referencia, string destinatarios, string cuerpo = "<p>Hola</p>")
    {
        var fila = new CorreoSaliente
        {
            Tipo = tipo,
            Referencia = referencia,
            Destinatarios = destinatarios,
            Asunto = "Asunto de prueba",
            CuerpoHtml = cuerpo,
            CuerpoTexto = cuerpo.Length == 0 ? "" : "Hola",
            Estado = EstadosCorreo.Fallido,
            Intentos = 6,
            ProximoIntento = DateTimeOffset.UtcNow.AddMinutes(-5),
            UltimoError = "mensaje: El servidor rechazó el mensaje.",
            FechaCreacion = DateTimeOffset.UtcNow.AddMinutes(-30)
        };
        return await api.ConsultarAsync(async db =>
        {
            db.CorreosSalientes.Add(fila);
            await db.SaveChangesAsync();
            return fila.IdCorreo;
        });
    }

    [Fact]
    public async Task Reenviar_UnAvisoDelTaller_VaALosDestinosDeHoy_UnaSolaVez_AunqueLleguenDosClicsALaVez()
    {
        await api.LimpiarDestinosAsync();
        try
        {
            var (idSolicitud, _, _, _) = await api.SolicitudEnLineaAsync();
            var hoyEnLinea = api.CorreoUnico("hoy");
            var hoyPresencial = api.CorreoUnico("hoypres");
            await api.DestinoActivoAsync(hoyEnLinea, enLinea: true, presenciales: false);
            await api.DestinoActivoAsync(hoyPresencial, enLinea: false, presenciales: true);
            var id = await FallidoAsync(TiposCorreo.AvisoSolicitudNueva, $"solicitud:{idSolicitud}", "antiguo.destino@taller.test");
            var admin = api.Cliente(await api.TokenAdminAsync());

            var respuestas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => admin.PostAsync($"/api/v1/correos/no-enviados/{id}/reenviar", null)));
            Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Accepted);
            Assert.All(respuestas, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Accepted, HttpStatusCode.Conflict }));

            var original = await api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking().SingleAsync(c => c.IdCorreo == id));
            Assert.NotNull(original.ReenviadoComo);
            Assert.True(original.Revisado);
            Assert.Equal(await api.IdAdminAsync(), original.IdEmpleadoReenvia);
            Assert.NotNull(original.FechaReenvio);
            var copias = await api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking()
                .Where(c => c.Referencia == $"solicitud:{idSolicitud}" && c.IdCorreo > id).ToListAsync());
            var copia = Assert.Single(copias);
            Assert.Equal(original.ReenviadoComo, copia.IdCorreo);
            Assert.Equal(EstadosCorreo.Pendiente, copia.Estado);
            Assert.Equal(hoyEnLinea, copia.Destinatarios);
            Assert.Equal(original.CuerpoHtml, copia.CuerpoHtml);
            Assert.Single(await api.AuditoriaAsync("correo", id.ToString(), "correo_reenviado"));

            var otraVez = await admin.PostAsync($"/api/v1/correos/no-enviados/{id}/reenviar", null).EsperarAsync(HttpStatusCode.Conflict);
            Assert.Equal("ya_reenviado", Fase11Apoyo.Codigo(otraVez));
            Assert.Equal(copia.IdCorreo, otraVez.GetProperty("idCorreoNuevo").GetInt64());
        }
        finally
        {
            await api.LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task Reenviar_UnCorreoAlCliente_VaASuMismaDireccion_SoloSiAunLoRecibe_YSinCuerpoNoSePuede()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        // Un aviso de estado real que fallo.
        var (idSolicitud, _, correo, token) = await api.SolicitudEnLineaAsync();
        var pedido = await api.CrearPedidoAsync(idSolicitud: idSolicitud);
        var idPedido = pedido.GetProperty("idPedido").GetInt32();
        await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/pedidos/{idPedido}/estado", pedido.GetProperty("rowVersion").GetString()!,
            new { estado = "en_proceso" })).EsperarAsync(HttpStatusCode.OK);
        var alCliente = (await api.CorreosAsync($"estado:pedido:{idPedido}")).Single().IdCorreo;
        await api.ConsultarAsync(db => db.CorreosSalientes.Where(c => c.IdCorreo == alCliente)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, EstadosCorreo.Fallido).SetProperty(c => c.Intentos, 6)));

        var nuevo = (await admin.PostAsync($"/api/v1/correos/no-enviados/{alCliente}/reenviar", null).EsperarAsync(HttpStatusCode.Accepted))
            .GetProperty("idCorreoNuevo").GetInt64();
        Assert.Equal(correo, await api.ConsultarAsync(db => db.CorreosSalientes.Where(c => c.IdCorreo == nuevo).Select(c => c.Destinatarios).SingleAsync()));

        // Si el cliente apago los avisos de estado, su aviso fallido ya no se reenvia.
        var otro = await FallidoAsync(TiposCorreo.EstadoPedido, $"estado:pedido:{idPedido}", correo);
        await api.Cliente(token).PutAsJsonAsync("/api/v1/mi-cuenta/preferencias", new { avisosEstado = false, marketing = false }).EsperarAsync(HttpStatusCode.OK);
        var noRecibe = await admin.PostAsync($"/api/v1/correos/no-enviados/{otro}/reenviar", null).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("cliente_no_recibe", Fase11Apoyo.Codigo(noRecibe));

        // Un correo esencial va a su misma direccion.
        var esencial = await FallidoAsync(TiposCorreo.ConfirmacionSolicitud, "confirmacion:99999", "cliente.fallido@correo.test");
        var copia = (await admin.PostAsync($"/api/v1/correos/no-enviados/{esencial}/reenviar", null).EsperarAsync(HttpStatusCode.Accepted))
            .GetProperty("idCorreoNuevo").GetInt64();
        Assert.Equal("cliente.fallido@correo.test", await api.ConsultarAsync(db => db.CorreosSalientes.Where(c => c.IdCorreo == copia).Select(c => c.Destinatarios).SingleAsync()));

        var purgado = await FallidoAsync(TiposCorreo.EstadoPedido, "estado:pedido:99998", "otro.fallido@correo.test", cuerpo: "");
        var sinContenido = await admin.PostAsync($"/api/v1/correos/no-enviados/{purgado}/reenviar", null).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("sin_contenido", Fase11Apoyo.Codigo(sinContenido));
        var lista = await admin.GetAsync("/api/v1/correos/no-enviados?tamano=100&tipo=estado_pedido").EsperarAsync(HttpStatusCode.OK);
        Assert.False(lista.GetProperty("elementos").EnumerateArray().Single(e => e.GetProperty("idCorreo").GetInt64() == purgado).GetProperty("reenviable").GetBoolean());
        // Ya reenviado: no se ofrece otra vez.
        Assert.False(lista.GetProperty("elementos").EnumerateArray().Single(e => e.GetProperty("idCorreo").GetInt64() == alCliente).GetProperty("reenviable").GetBoolean());

        await admin.PostAsync("/api/v1/correos/no-enviados/999999999/reenviar", null).EsperarAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarcarRevisado_LoSacaDelAvisoDelInicio_YSoloGestionVeLaLista()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var id = await FallidoAsync(TiposCorreo.ConsultaContacto, "contacto:99999", "taller.revisado@taller.test");
        var antes = (await admin.GetAsync("/api/v1/correos/no-enviados/resumen").EsperarAsync(HttpStatusCode.OK)).GetProperty("sinRevisar").GetInt32();

        await admin.PostAsync($"/api/v1/correos/no-enviados/{id}/revisado", null).EsperarAsync(HttpStatusCode.NoContent);

        Assert.Equal(antes - 1, (await admin.GetAsync("/api/v1/correos/no-enviados/resumen").EsperarAsync(HttpStatusCode.OK)).GetProperty("sinRevisar").GetInt32());
        var sinRevisar = await admin.GetAsync("/api/v1/correos/no-enviados?revisado=false&tamano=100").EsperarAsync(HttpStatusCode.OK);
        Assert.DoesNotContain(sinRevisar.GetProperty("elementos").EnumerateArray(), e => e.GetProperty("idCorreo").GetInt64() == id);
        await admin.PostAsync("/api/v1/correos/no-enviados/999999999/revisado", null).EsperarAsync(HttpStatusCode.NotFound);

        var operario = api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario));
        await operario.GetAsync("/api/v1/correos/no-enviados").EsperarAsync(HttpStatusCode.Forbidden);
        await operario.GetAsync("/api/v1/correos/no-enviados/resumen").EsperarAsync(HttpStatusCode.Forbidden);
        await operario.PostAsync($"/api/v1/correos/no-enviados/{id}/reenviar", null).EsperarAsync(HttpStatusCode.Forbidden);
    }
}
