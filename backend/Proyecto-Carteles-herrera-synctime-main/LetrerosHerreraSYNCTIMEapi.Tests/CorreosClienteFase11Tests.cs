using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 11 (RFS08, RFU09, S09): el cliente recibe un correo cuando su solicitud o su pedido cambian de estado, con
// el enlace de baja y las cabeceras List-Unsubscribe. La baja no actua con solo abrir el enlace, funciona sin
// sesion, sirve solo para eso y no sirve alterada.
[Collection(ApiCollection.Name)]
public sealed class CorreosClienteFase11Tests(ApiFixture api)
{
    private const string PrefijoUnClic = "https://synctime.test/api/v1/correos/baja/un-clic?t=";

    private static void TieneEnlaceDeBaja(CorreoSaliente correo)
    {
        var token = Fase11Apoyo.TokenDeBaja(correo);
        Assert.False(string.IsNullOrEmpty(token), $"El correo {correo.Tipo} no trae el enlace de baja.");
        Assert.Contains($"https://synctime.test/baja?t={token}", correo.CuerpoHtml);
        Assert.Equal(PrefijoUnClic + token, correo.EnlaceBaja);
    }

    private async Task<string> VersionCotizacionAsync(HttpClient admin, int id) =>
        (await admin.GetAsync($"/api/v1/cotizaciones/{id}").EsperarAsync(HttpStatusCode.OK)).GetProperty("rowVersion").GetString()!;

    [Fact]
    public async Task CadaCambioDeEstadoDeLaSolicitud_EncolaSuCorreo_ConElEnlaceDeBaja()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var (aceptada, _, correo, _) = await api.SolicitudEnLineaAsync();

        // Cotizada: «Tu cotizacion esta lista», ahora opcional y con enlace de baja.
        var idCotizacion = (await api.CrearCotizacionAsync(aceptada)).GetProperty("idCotizacion").GetInt32();
        var lista = Assert.Single(await api.CorreosAsync($"cotizacion:{idCotizacion}"));
        Assert.Equal(TiposCorreo.CotizacionLista, lista.Tipo);
        TieneEnlaceDeBaja(lista);

        // Aceptada.
        await api.AprobarCotizacionAsync(idCotizacion);
        var aceptadaCorreo = Assert.Single(await api.CorreosAsync($"estado:solicitud:{aceptada}"));
        Assert.Equal(TiposCorreo.EstadoSolicitud, aceptadaCorreo.Tipo);
        Assert.Equal(correo, aceptadaCorreo.Destinatarios);
        Assert.Contains("Aceptada", aceptadaCorreo.CuerpoTexto);
        Assert.Contains($"#{aceptada}", aceptadaCorreo.CuerpoTexto);
        TieneEnlaceDeBaja(aceptadaCorreo);

        // Rechazada, con su motivo.
        var (rechazada, _, _, _) = await api.SolicitudEnLineaAsync();
        await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/solicitudes/{rechazada}/estado", await api.VersionAsync(rechazada),
            new { estado = "rechazado", motivo = "No fabricamos ese tamaño" })).EsperarAsync(HttpStatusCode.OK);
        var rechazo = Assert.Single(await api.CorreosAsync($"estado:solicitud:{rechazada}"));
        Assert.Contains("Rechazada", rechazo.CuerpoTexto);
        Assert.Contains("No fabricamos ese tamaño", rechazo.CuerpoTexto);
        TieneEnlaceDeBaja(rechazo);
    }

    [Fact]
    public async Task CadaCambioDeEstadoDelPedido_EncolaSuCorreo_ConElEnlaceDeBaja()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var (idSolicitud, _, correo, _) = await api.SolicitudEnLineaAsync();
        var pedido = await api.CrearPedidoAsync(idSolicitud: idSolicitud);
        var id = pedido.GetProperty("idPedido").GetInt32();
        var version = pedido.GetProperty("rowVersion").GetString()!;

        foreach (var estado in new[] { "en_proceso", "terminado", "entregado" })
            version = (await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/pedidos/{id}/estado", version, new { estado }))
                .EsperarAsync(HttpStatusCode.OK)).GetProperty("rowVersion").GetString()!;

        var correos = await api.CorreosAsync($"estado:pedido:{id}");
        Assert.Equal(3, correos.Count);
        Assert.All(correos, c =>
        {
            Assert.Equal(TiposCorreo.EstadoPedido, c.Tipo);
            Assert.Equal(correo, c.Destinatarios);
            TieneEnlaceDeBaja(c);
        });
        Assert.Contains("En proceso", correos[0].CuerpoTexto);
        Assert.Contains("Terminado", correos[1].CuerpoTexto);
        Assert.Contains("Entregado", correos[2].CuerpoTexto);

        // Anulado, con el motivo.
        var (otraSolicitud, _, _, _) = await api.SolicitudEnLineaAsync();
        var otro = await api.CrearPedidoAsync(idSolicitud: otraSolicitud);
        var idOtro = otro.GetProperty("idPedido").GetInt32();
        await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/pedidos/{idOtro}/estado", otro.GetProperty("rowVersion").GetString()!,
            new { estado = "anulado", motivo = "El cliente desistió del encargo" })).EsperarAsync(HttpStatusCode.OK);
        var anulado = Assert.Single(await api.CorreosAsync($"estado:pedido:{idOtro}"));
        Assert.Contains("Anulado", anulado.CuerpoTexto);
        Assert.Contains("El cliente desistió del encargo", anulado.CuerpoTexto);
    }

    [Fact]
    public async Task ConLaPreferenciaApagada_NoSeEncolanLosAvisosDeEstado_PeroSiLosEsenciales()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var (idSolicitud, idCliente, _, token) = await api.SolicitudEnLineaAsync();
        await api.Cliente(token).PutAsJsonAsync("/api/v1/mi-cuenta/preferencias", new { avisosEstado = false, marketing = false }).EsperarAsync(HttpStatusCode.OK);

        var idCotizacion = (await api.CrearCotizacionAsync(idSolicitud)).GetProperty("idCotizacion").GetInt32();
        await api.AprobarCotizacionAsync(idCotizacion);
        var pedido = await admin.PostAsJsonAsync($"/api/v1/cotizaciones/{idCotizacion}/pedido", ApiFixture.DatosPedido(await api.IdAdminAsync()))
            .EsperarAsync(HttpStatusCode.Created);
        await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/pedidos/{pedido.GetProperty("idPedido").GetInt32()}/estado",
            pedido.GetProperty("rowVersion").GetString()!, new { estado = "en_proceso" })).EsperarAsync(HttpStatusCode.OK);

        Assert.Empty(await api.CorreosAsync($"cotizacion:{idCotizacion}"));
        Assert.Empty(await api.CorreosAsync($"estado:solicitud:{idSolicitud}"));
        Assert.Empty(await api.CorreosAsync($"estado:pedido:{pedido.GetProperty("idPedido").GetInt32()}"));
        // La confirmacion de la solicitud no es un aviso de estado: llega igual.
        Assert.Single(await api.CorreosAsync($"confirmacion:{idSolicitud}"));
        // Y el cambio queda como consentimiento, con su version.
        Assert.True(await api.ConsultarAsync(db => db.Consentimientos.AnyAsync(c => c.IdCliente == idCliente && c.Tipo == "avisos_estado"
            && c.VersionDocumento == "1.0" && c.FechaRevocacion != null)));
    }

    // Un token de baja real, sacado de un correo que el cliente recibio.
    private async Task<(string Token, int IdCliente, string Correo)> TokenRecibidoAsync()
    {
        var (idSolicitud, idCliente, correo, _) = await api.SolicitudEnLineaAsync();
        var idCotizacion = (await api.CrearCotizacionAsync(idSolicitud)).GetProperty("idCotizacion").GetInt32();
        return (Fase11Apoyo.TokenDeBaja(Assert.Single(await api.CorreosAsync($"cotizacion:{idCotizacion}"))), idCliente, correo);
    }

    [Fact]
    public async Task ElEnlaceDeBaja_ConGetNoCambiaNada_YConPostApagaSinSesion()
    {
        var (token, idCliente, correo) = await TokenRecibidoAsync();
        var visitante = api.Cliente();

        var vista = await visitante.GetAsync($"/api/v1/correos/baja?t={token}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(CorreosDestinosAvisos.Enmascarar(correo), vista.GetProperty("correoEnmascarado").GetString());
        Assert.True(vista.GetProperty("avisosEstado").GetBoolean());
        Assert.False(vista.GetProperty("marketing").GetBoolean());
        Assert.DoesNotContain(correo, vista.GetRawText());
        // Abrirlo (o que el programa de correo lo abra solo) no cambia nada.
        await visitante.GetAsync($"/api/v1/correos/baja?t={token}").EsperarAsync(HttpStatusCode.OK);
        Assert.True(await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == idCliente).Select(c => c.RecibeAvisosEstado).SingleAsync()));
        Assert.Empty(await api.AuditoriaAsync("cliente", idCliente.ToString(), "baja_correo"));

        var baja = await visitante.PostAsJsonAsync("/api/v1/correos/baja", new { t = token }).EsperarAsync(HttpStatusCode.OK);
        Assert.False(baja.GetProperty("avisosEstado").GetBoolean());
        Assert.False(baja.GetProperty("marketing").GetBoolean());
        Assert.False(await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == idCliente).Select(c => c.RecibeAvisosEstado).SingleAsync()));
        // Repetirla da lo mismo.
        await visitante.PostAsJsonAsync("/api/v1/correos/baja", new { t = token }).EsperarAsync(HttpStatusCode.OK);
        Assert.NotEmpty(await api.AuditoriaAsync("cliente", idCliente.ToString(), "baja_correo"));
        Assert.False((await visitante.GetAsync($"/api/v1/correos/baja?t={token}").EsperarAsync(HttpStatusCode.OK)).GetProperty("avisosEstado").GetBoolean());
    }

    // Hallado al probar en la app: el navegador que abre el enlace puede tener abierta la sesión del personal.
    // La baja la hace el cliente dueño del enlace, así que todo su registro en la bitácora debe ser a su nombre.
    [Fact]
    public async Task LaBajaDesdeElEnlace_QuedaANombreDelCliente_AunqueHayaUnaSesionDelPersonal()
    {
        var (token, idCliente, _) = await TokenRecibidoAsync();
        var conSesionDelPersonal = api.Cliente(await api.TokenAdminAsync());

        await conSesionDelPersonal.PostAsJsonAsync("/api/v1/correos/baja", new { t = token }).EsperarAsync(HttpStatusCode.OK);

        var registros = await api.AuditoriaAsync("cliente", idCliente.ToString());
        var dePreferencia = registros.Where(r => r.Accion is "modificar" or "baja_correo").ToList();
        Assert.NotEmpty(dePreferencia);
        Assert.All(dePreferencia, r =>
        {
            Assert.Equal(idCliente, r.IdCliente);
            Assert.Null(r.IdEmpleado);
            Assert.Equal("cliente", r.Rol);
        });
    }

    [Fact]
    public async Task ElEnlaceDeBaja_SoloApagaLoElegido()
    {
        var (token, idCliente, _) = await TokenRecibidoAsync();
        await api.ConsultarAsync(db =>
        {
            db.Consentimientos.Add(new Consentimiento { IdCliente = idCliente, Tipo = "marketing", VersionDocumento = "1.0", FechaAceptacion = DateTimeOffset.UtcNow });
            return db.SaveChangesAsync();
        });

        var soloMarketing = await api.Cliente().PostAsJsonAsync("/api/v1/correos/baja", new { t = token, marketing = false }).EsperarAsync(HttpStatusCode.OK);
        Assert.True(soloMarketing.GetProperty("avisosEstado").GetBoolean());
        Assert.False(soloMarketing.GetProperty("marketing").GetBoolean());
    }

    [Fact]
    public async Task UnTokenAlteradoInventadoOViejo_Responde404()
    {
        var (token, idCliente, _) = await TokenRecibidoAsync();
        var alterado = (token[0] == 'A' ? 'B' : 'A') + token[1..];
        var visitante = api.Cliente();
        foreach (var malo in new[] { alterado, "inventado", new string('A', 48), "" })
        {
            await visitante.GetAsync($"/api/v1/correos/baja?t={malo}").EsperarAsync(HttpStatusCode.NotFound);
            await visitante.PostAsJsonAsync("/api/v1/correos/baja", new { t = malo }).EsperarAsync(HttpStatusCode.NotFound);
            await visitante.PostAsync($"/api/v1/correos/baja/un-clic?t={malo}", UnClic()).EsperarAsync(HttpStatusCode.NotFound);
        }
        Assert.True(await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == idCliente).Select(c => c.RecibeAvisosEstado).SingleAsync()));

        // Si el cliente cambia de correo, el enlace viejo deja de servir.
        await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == idCliente)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Email, $"nuevo{idCliente}@correo.test")));
        await visitante.GetAsync($"/api/v1/correos/baja?t={token}").EsperarAsync(HttpStatusCode.NotFound);
    }

    private static FormUrlEncodedContent UnClic() => new([new KeyValuePair<string, string>("List-Unsubscribe", "One-Click")]);

    [Fact]
    public async Task LaBajaConUnClic_Rfc8058_ApagaLosDos()
    {
        var (token, idCliente, _) = await TokenRecibidoAsync();
        var visitante = api.Cliente();
        using (var vacio = new FormUrlEncodedContent([]))
            await visitante.PostAsync($"/api/v1/correos/baja/un-clic?t={token}", vacio).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == idCliente).Select(c => c.RecibeAvisosEstado).SingleAsync()));

        using var respuesta = await visitante.PostAsync($"/api/v1/correos/baja/un-clic?t={token}", UnClic());
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty(await respuesta.Content.ReadAsByteArrayAsync());
        Assert.False(await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == idCliente).Select(c => c.RecibeAvisosEstado).SingleAsync()));
    }

    [Fact]
    public async Task LasPreferenciasEnMiCuenta_SeVenYSeCambian_YQuedanComoConsentimiento()
    {
        var (idCliente, _, token) = await api.CrearClienteConfirmadoAsync();
        var cliente = api.Cliente(token);
        var cuenta = await cliente.GetAsync("/api/v1/mi-cuenta").EsperarAsync(HttpStatusCode.OK);
        Assert.True(cuenta.GetProperty("avisosEstado").GetBoolean());
        Assert.False(cuenta.GetProperty("marketing").GetBoolean());

        var cambiada = await cliente.PutAsJsonAsync("/api/v1/mi-cuenta/preferencias", new { avisosEstado = false, marketing = true }).EsperarAsync(HttpStatusCode.OK);
        Assert.False(cambiada.GetProperty("avisosEstado").GetBoolean());
        Assert.True(cambiada.GetProperty("marketing").GetBoolean());
        var consentimientos = await api.ConsultarAsync(db => db.Consentimientos.AsNoTracking().Where(c => c.IdCliente == idCliente).ToListAsync());
        Assert.Contains(consentimientos, c => c.Tipo == "marketing" && c.FechaRevocacion == null && c.VersionDocumento == "1.0");
        Assert.Contains(consentimientos, c => c.Tipo == "avisos_estado" && c.FechaRevocacion != null);

        await cliente.PutAsJsonAsync("/api/v1/mi-cuenta/preferencias", new { avisosEstado = true, marketing = false }).EsperarAsync(HttpStatusCode.OK);
        consentimientos = await api.ConsultarAsync(db => db.Consentimientos.AsNoTracking().Where(c => c.IdCliente == idCliente).ToListAsync());
        Assert.Contains(consentimientos, c => c.Tipo == "avisos_estado" && c.FechaRevocacion == null);
        Assert.DoesNotContain(consentimientos, c => c.Tipo == "marketing" && c.FechaRevocacion == null);

        // El alias de la fase 6 sigue funcionando.
        await cliente.PutAsJsonAsync("/api/v1/mi-cuenta/marketing", new { acepta = true, version = "1.0" }).EsperarAsync(HttpStatusCode.NoContent);
        Assert.True((await cliente.GetAsync("/api/v1/mi-cuenta").EsperarAsync(HttpStatusCode.OK)).GetProperty("marketing").GetBoolean());
    }
}
