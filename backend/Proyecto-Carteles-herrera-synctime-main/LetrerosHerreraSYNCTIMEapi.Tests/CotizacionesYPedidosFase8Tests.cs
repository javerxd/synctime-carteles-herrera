using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 8: cotizaciones, pedidos, abonos, cobranza y calendario (RFS05, RFS06, RFS04, RFU07, RFU08, RFU12-RFU15,
// RFU35, RFU36, E06-E09, E25-E27, S04, S06-S08, S24). Las pruebas de la coleccion Api corren de a una.
[Collection(ApiCollection.Name)]
public sealed class CotizacionesYPedidosFase8Tests(ApiFixture api)
{
    private static DateOnly Hoy => ApiFixture.HoyChile();

    private static string Version(JsonElement registro) => registro.GetProperty("rowVersion").GetString()!;

    private static string Codigo(JsonElement error) => error.GetProperty("codigo").GetString()!;

    private async Task<HttpClient> AdminAsync() => api.Cliente(await api.TokenAdminAsync());

    private static Task<HttpResponseMessage> ConVersion(HttpClient cliente, HttpMethod metodo, string url, string version, object? cuerpo = null) =>
        cliente.SendAsync(ApiFixture.ConVersion(metodo, url, version, cuerpo));

    private static Task<HttpResponseMessage> Abonar(HttpClient cliente, int idPedido, decimal monto, string medio = "efectivo", object? fechaPago = null) =>
        cliente.PostAsJsonAsync($"/api/v1/pedidos/{idPedido}/pagos", new { monto, medioPago = medio, fechaPago });

    private static async Task<JsonElement> AvanzarAsync(HttpClient cliente, JsonElement pedido, params string[] estados)
    {
        foreach (var estado in estados)
            pedido = await ConVersion(cliente, HttpMethod.Patch, $"/api/v1/pedidos/{pedido.GetProperty("idPedido").GetInt32()}/estado", Version(pedido),
                new { estado }).EsperarAsync(HttpStatusCode.OK);
        return pedido;
    }

    private async Task<int> SolicitudEnLineaAsync(string token) =>
        (await api.Cliente(token).PostAsJsonAsync("/api/v1/mis/solicitudes", ApiFixture.CuerpoSolicitud()).EsperarAsync(HttpStatusCode.Created))
            .GetProperty("idSolicitud").GetInt32();

    private Task<int> CorreosDeCotizacionAsync(int idCotizacion) =>
        api.ConsultarAsync(db => db.CorreosSalientes.CountAsync(c => c.Referencia == $"cotizacion:{idCotizacion}"));

    // ── De punta a punta ──

    [Fact]
    public async Task ElFlujoCompleto_SolicitudCotizacionAprobacionDelClientePedidoYEntrega()
    {
        var cliente = await api.CrearClienteConfirmadoAsync();
        var idSolicitud = await SolicitudEnLineaAsync(cliente.Token);
        var operario = api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario));

        // RFU12: el operario cotiza. La solicitud pasa a «cotizado» y el cliente recibe el aviso.
        var emitida = await operario.PostAsJsonAsync($"/api/v1/solicitudes/{idSolicitud}/cotizaciones",
            ApiFixture.CuerpoCotizacion(observaciones: "Incluye el diseño")).EsperarAsync(HttpStatusCode.Created);
        var idCotizacion = emitida.GetProperty("idCotizacion").GetInt32();
        Assert.Equal("pendiente", emitida.GetProperty("estado").GetString());
        Assert.Equal($"#{idSolicitud}", emitida.GetProperty("numeroSolicitud").GetString());
        Assert.Single(emitida.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, emitida.GetProperty("respuesta").ValueKind);
        var admin = await AdminAsync();
        Assert.Equal("cotizado", (await admin.GetAsync($"/api/v1/solicitudes/{idSolicitud}").EsperarAsync(HttpStatusCode.OK)).GetProperty("estado").GetString());
        Assert.Equal(1, await CorreosDeCotizacionAsync(idCotizacion));

        // S04: el cliente la ve en su seguimiento y la abre (S06), sin el id de quien la elaboro.
        var seguimiento = await api.Cliente(cliente.Token).GetAsync($"/api/v1/mis/solicitudes/{idSolicitud}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(idCotizacion, seguimiento.GetProperty("cotizacion").GetProperty("idCotizacion").GetInt32());
        Assert.Equal("pendiente", seguimiento.GetProperty("cotizacion").GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, seguimiento.GetProperty("pedido").ValueKind);
        using var abierta = await api.Cliente(cliente.Token).GetAsync($"/api/v1/mis/cotizaciones/{idCotizacion}");
        Assert.Equal(HttpStatusCode.OK, abierta.StatusCode);
        var vista = JsonDocument.Parse(await abierta.Content.ReadAsStringAsync()).RootElement.Clone();
        Assert.False(vista.GetProperty("elaboradaPor").TryGetProperty("idEmpleado", out _));
        Assert.Equal("Emp", vista.GetProperty("elaboradaPor").GetProperty("nombre").GetString());
        Assert.Equal("Incluye el diseño", vista.GetProperty("observaciones").GetString());

        // RFU08: el cliente la aprueba desde la web, con la version vista (ETag).
        var aprobada = await api.Cliente(cliente.Token).SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/mis/cotizaciones/{idCotizacion}/respuesta")
        {
            Content = JsonContent.Create(new { decision = "aprobada" }),
            Headers = { { "If-Match", abierta.Headers.ETag!.Tag } }
        }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("aprobada", aprobada.GetProperty("estado").GetString());
        Assert.Equal("cliente_web", aprobada.GetProperty("respuesta").GetProperty("via").GetString());
        Assert.Equal(JsonValueKind.Null, aprobada.GetProperty("respuesta").GetProperty("registradaPor").ValueKind);
        var solicitud = await admin.GetAsync($"/api/v1/solicitudes/{idSolicitud}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("aceptado", solicitud.GetProperty("estado").GetString());
        Assert.Equal(["solicitado", "cotizado", "aceptado"], solicitud.GetProperty("historial").EnumerateArray().Select(h => h.GetProperty("estadoNuevo").GetString()));

        // RFU13: el diseñador crea el pedido con el total redondeado de la cotizacion.
        var disenador = api.Cliente(await api.TokenEmpleadoAsync(Roles.Disenador));
        var pedido = await disenador.PostAsJsonAsync($"/api/v1/cotizaciones/{idCotizacion}/pedido", ApiFixture.DatosPedido(await api.IdAdminAsync()))
            .EsperarAsync(HttpStatusCode.Created);
        Assert.Equal("aceptado", pedido.GetProperty("estadoPedido").GetString());
        Assert.Equal("cotizacion", pedido.GetProperty("origen").GetString());
        Assert.Equal(emitida.GetProperty("totalRedondeado").GetInt32(), pedido.GetProperty("montoBaseTrabajo").GetInt32());
        Assert.Equal(["en_proceso", "anulado"], pedido.GetProperty("transicionesPosibles").EnumerateArray().Select(t => t.GetString()));
        var idPedido = pedido.GetProperty("idPedido").GetInt32();
        Assert.Equal(idPedido, (await admin.GetAsync($"/api/v1/cotizaciones/{idCotizacion}").EsperarAsync(HttpStatusCode.OK)).GetProperty("idPedido").GetInt32());

        // E09: avance hasta la entrega; la fecha real es hoy si no se indica (hoy de Chile, antes o despues de
        // la medianoche si la prueba pasa justo por ella).
        var antes = Hoy;
        var entregado = await AvanzarAsync(disenador, pedido, "en_proceso", "terminado", "entregado");
        Assert.Equal("entregado", entregado.GetProperty("estadoPedido").GetString());
        Assert.Contains(DateOnly.Parse(entregado.GetProperty("fechaEntregaReal").GetString()!, CultureInfo.InvariantCulture), new[] { antes, Hoy });
        Assert.Empty(entregado.GetProperty("transicionesPosibles").EnumerateArray());
        var historial = entregado.GetProperty("historial").EnumerateArray().ToList();
        Assert.Equal(["aceptado", "en_proceso", "terminado", "entregado"], historial.Select(h => h.GetProperty("estadoNuevo").GetString()));
        Assert.Equal(JsonValueKind.Null, historial[0].GetProperty("estadoAnterior").ValueKind);
        Assert.Equal("Emp Prueba", historial[3].GetProperty("empleado").GetString());

        // S04: el cliente ve su pedido, sin el historial interno.
        var misPedidos = await api.Cliente(cliente.Token).GetAsync("/api/v1/mis/pedidos").EsperarAsync(HttpStatusCode.OK);
        var mio = Assert.Single(misPedidos.EnumerateArray());
        Assert.Equal(idSolicitud, mio.GetProperty("idSolicitud").GetInt32());
        Assert.Equal("entregado", mio.GetProperty("estadoPedido").GetString());
        Assert.False(mio.TryGetProperty("historial", out _));
        var lista = await api.Cliente(cliente.Token).GetAsync("/api/v1/mis/solicitudes").EsperarAsync(HttpStatusCode.OK);
        var fila = Assert.Single(lista.GetProperty("elementos").EnumerateArray());
        Assert.Equal("aprobada", fila.GetProperty("cotizacion").GetProperty("estado").GetString());
        Assert.Equal(idPedido, fila.GetProperty("pedido").GetProperty("idPedido").GetInt32());
        Assert.Equal("no_pagado", fila.GetProperty("pedido").GetProperty("estadoPago").GetString());
    }

    // ── Pedido directo (RFU35, E26) ──

    private static object CuerpoDirecto(int idCliente, int idResponsable, decimal abono = 20_000, decimal manoObra = 50_000) => new
    {
        idCliente,
        descripcionGeneral = "Letrero para la entrada del almacén",
        detalles = new[] { new { idProducto = 1, descripcion = "Con el logo", anchoMm = 1000, altoMm = 500, cantidad = 1 } },
        // 0,75 x 333 = 249,75: el total (50.249,75) se redondea a 50.250 en el monto base.
        materiales = new[] { new { idMaterial = 1, medidaAplicada = "1 x 0,5", cantidad = 0.75m, costoUnitario = 333m } },
        costoManoObra = manoObra,
        otrosCostos = 0,
        idEmpleadoResponsable = idResponsable,
        fechaEntregaEstimada = Hoy.AddDays(5),
        modalidadEntrega = "retiro_local",
        requiereInstalacion = false,
        costoInstalacionAdicional = 0,
        abonoInicial = abono > 0 ? new { monto = abono, medioPago = "transferencia" } : null
    };

    private sealed record Conteo(int Solicitudes, int Cotizaciones, int Pedidos, int Pagos, int Correos);

    private Task<Conteo> ContarAsync() => api.ConsultarAsync(async db => new Conteo(
        await db.Solicitudes.CountAsync(), await db.Cotizaciones.CountAsync(), await db.Pedidos.CountAsync(),
        await db.Pagos.CountAsync(), await db.CorreosSalientes.CountAsync()));

    [Fact]
    public async Task ElPedidoDirecto_CreaTodoEnUnaTransaccion_ConSuAbono_YSinCorreos()
    {
        var cliente = await api.CrearClienteConfirmadoAsync();
        var (idEmpleado, _, token) = await api.CrearEmpleadoAsync(Roles.Operario);
        var antes = await ContarAsync();

        var pedido = await api.Cliente(token).PostAsJsonAsync("/api/v1/pedidos/directo", CuerpoDirecto(cliente.Id, idEmpleado))
            .EsperarAsync(HttpStatusCode.Created);

        Assert.Equal("directo", pedido.GetProperty("origen").GetString());
        Assert.Equal("aceptado", pedido.GetProperty("estadoPedido").GetString());
        Assert.Equal(50_250, pedido.GetProperty("montoBaseTrabajo").GetInt32());
        Assert.Equal(20_000, pedido.GetProperty("montoAbonado").GetInt32());
        Assert.Equal("abonado", pedido.GetProperty("estadoPago").GetString());
        Assert.Equal(30_250, pedido.GetProperty("saldoPendiente").GetInt32());
        var pago = Assert.Single(pedido.GetProperty("pagos").EnumerateArray());
        Assert.Equal("transferencia", pago.GetProperty("medioPago").GetString());
        Assert.Equal("Emp Prueba", pago.GetProperty("registradoPor").GetString());
        Assert.Equal("Pedido directo en el taller", Assert.Single(pedido.GetProperty("historial").EnumerateArray()).GetProperty("nota").GetString());

        var idSolicitud = pedido.GetProperty("solicitud").GetProperty("idSolicitud").GetInt32();
        var idCotizacion = pedido.GetProperty("cotizacion").GetProperty("idCotizacion").GetInt32();
        var solicitud = await (await AdminAsync()).GetAsync($"/api/v1/solicitudes/{idSolicitud}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("presencial", solicitud.GetProperty("origen").GetString());
        Assert.Equal("aceptado", solicitud.GetProperty("estado").GetString());
        Assert.Equal("Pedido directo en el taller", Assert.Single(solicitud.GetProperty("historial").EnumerateArray()).GetProperty("motivo").GetString());
        var cotizacion = await (await AdminAsync()).GetAsync($"/api/v1/cotizaciones/{idCotizacion}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("aprobada", cotizacion.GetProperty("estado").GetString());
        Assert.Equal("presencial", cotizacion.GetProperty("respuesta").GetProperty("via").GetString());
        Assert.Equal(50_249.75m, cotizacion.GetProperty("total").GetDecimal());

        // Una solicitud, una cotizacion, un pedido, un abono y ningun correo (tampoco «cotizacion lista»).
        Assert.Equal(antes with { Solicitudes = antes.Solicitudes + 1, Cotizaciones = antes.Cotizaciones + 1, Pedidos = antes.Pedidos + 1, Pagos = antes.Pagos + 1 },
            await ContarAsync());
        Assert.Equal(0, await CorreosDeCotizacionAsync(idCotizacion));
    }

    [Fact]
    public async Task ElPedidoDirecto_SiFalla_NoEscribeNada()
    {
        var cliente = await api.CrearClienteConfirmadoAsync();
        var idAdmin = await api.IdAdminAsync();
        var token = await api.TokenAdminAsync();
        var antes = await ContarAsync();

        // Un abono que supera el total: 409 antes de escribir.
        var excede = await api.Cliente(token).PostAsJsonAsync("/api/v1/pedidos/directo", CuerpoDirecto(cliente.Id, idAdmin, abono: 60_000))
            .EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("abono_excede_saldo", Codigo(excede));
        // Un item invalido: 400 por campo.
        var invalido = await api.Cliente(token).PostAsJsonAsync("/api/v1/pedidos/directo", new { idCliente = cliente.Id, descripcionGeneral = "corta" })
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(invalido.GetProperty("errores").TryGetProperty("descripcionGeneral", out _));
        // La base rechaza el abono cuando ya se escribieron la solicitud, la cotizacion y el pedido: se revierte todo.
        var conInterceptor = api.ClienteConInterceptor(new AbonoInflado(Centinela), token);
        var rechazo = await conInterceptor.PostAsJsonAsync("/api/v1/pedidos/directo", CuerpoDirecto(cliente.Id, idAdmin, abono: Centinela))
            .EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("abono_excede_saldo", Codigo(rechazo));
        Assert.Equal("base_de_datos", rechazo.GetProperty("nivel").GetString());

        Assert.Equal(antes, await ContarAsync());
    }

    // ── Una pendiente por solicitud ──

    [Fact]
    public async Task UnaSolicitud_TieneUnaSolaCotizacionPendiente_EnLaApiYEnLaBase()
    {
        var admin = await AdminAsync();
        var idSolicitud = await api.CrearSolicitudAsync();
        await api.CrearCotizacionAsync(idSolicitud);

        var segunda = await admin.PostAsJsonAsync($"/api/v1/solicitudes/{idSolicitud}/cotizaciones", ApiFixture.CuerpoCotizacion(5000))
            .EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("cotizacion_pendiente", Codigo(segunda));
        Assert.Equal("api", segunda.GetProperty("nivel").GetString());
        Assert.Equal("Esta solicitud ya tiene una cotización pendiente. Edítala o espera la respuesta del cliente.", segunda.GetProperty("error").GetString());

        // La base lo garantiza por si sola (indice unico filtrado).
        var idAdmin = await api.IdAdminAsync();
        var error = await Assert.ThrowsAnyAsync<DbException>(() => api.ConsultarAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.cotizaciones (id_solicitud, id_empleado, costo_mano_obra, otros_costos, estado) VALUES ({idSolicitud}, {idAdmin}, 1000, 0, N'pendiente')")));
        Assert.Contains("uq_cotizaciones_una_pendiente", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, await api.ConsultarAsync(db => db.Cotizaciones.CountAsync(c => c.IdSolicitud == idSolicitud)));
    }

    // Si otra pendiente se cuela justo despues de la revision, el indice unico la frena y la API responde el mismo 409.
    [Fact]
    public async Task SiOtraPendienteSeCuela_ElIndiceDeLaBaseRespondeElMismo409()
    {
        var idSolicitud = await api.CrearSolicitudAsync();
        var idAdmin = await api.IdAdminAsync();
        var colar = () => api.ConsultarAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.cotizaciones (id_solicitud, id_empleado, costo_mano_obra, otros_costos, estado) VALUES ({idSolicitud}, {idAdmin}, 1000, 0, N'pendiente')"));

        var rechazo = await api.ClienteConInterceptor(new SeCuelaTrasLaRevision(colar), await api.TokenAdminAsync())
            .PostAsJsonAsync($"/api/v1/solicitudes/{idSolicitud}/cotizaciones", ApiFixture.CuerpoCotizacion())
            .EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal("cotizacion_pendiente", Codigo(rechazo));
        Assert.Equal("base_de_datos", rechazo.GetProperty("nivel").GetString());
        Assert.Equal(1, await api.ConsultarAsync(db => db.Cotizaciones.CountAsync(c => c.IdSolicitud == idSolicitud)));
        Assert.Equal("solicitado", await api.ConsultarAsync(db => db.Solicitudes.Where(s => s.IdSolicitud == idSolicitud).Select(s => s.Estado).SingleAsync()));
    }

    // ── Respuesta del cliente ──

    [Fact]
    public async Task ElClienteRechaza_LaSolicitudVuelveASolicitado_YSePuedeCotizarDeNuevo()
    {
        var cliente = await api.CrearClienteConfirmadoAsync();
        var idSolicitud = await SolicitudEnLineaAsync(cliente.Token);
        var emitida = await api.CrearCotizacionAsync(idSolicitud);
        var idCotizacion = emitida.GetProperty("idCotizacion").GetInt32();

        var rechazada = await ConVersion(api.Cliente(cliente.Token), HttpMethod.Post, $"/api/v1/mis/cotizaciones/{idCotizacion}/respuesta",
            Version(emitida), new { decision = "rechazada", motivo = "Está muy caro para mí" }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("rechazada", rechazada.GetProperty("estado").GetString());
        Assert.Equal("Está muy caro para mí", rechazada.GetProperty("respuesta").GetProperty("motivo").GetString());

        var solicitud = await (await AdminAsync()).GetAsync($"/api/v1/solicitudes/{idSolicitud}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("solicitado", solicitud.GetProperty("estado").GetString());
        var ultimo = solicitud.GetProperty("historial").EnumerateArray().Last();
        Assert.Equal("cliente", ultimo.GetProperty("actor").GetString());
        Assert.Equal($"Cotización #{idCotizacion} rechazada: Está muy caro para mí", ultimo.GetProperty("motivo").GetString());

        // Ya respondida no se responde de nuevo, y la solicitud se cotiza otra vez.
        await ConVersion(api.Cliente(cliente.Token), HttpMethod.Post, $"/api/v1/mis/cotizaciones/{idCotizacion}/respuesta",
            Version(rechazada), new { decision = "aprobada" }).EsperarAsync(HttpStatusCode.Conflict);
        await api.CrearCotizacionAsync(idSolicitud);
    }

    [Fact]
    public async Task ElCliente_NoVeNiRespondeCotizacionesAjenas()
    {
        var duena = await api.CrearClienteConfirmadoAsync();
        var otro = await api.CrearClienteConfirmadoAsync();
        var emitida = await api.CrearCotizacionAsync(await SolicitudEnLineaAsync(duena.Token));
        var url = $"/api/v1/mis/cotizaciones/{emitida.GetProperty("idCotizacion").GetInt32()}";

        await api.Cliente(otro.Token).GetAsync(url).EsperarAsync(HttpStatusCode.NotFound);
        await ConVersion(api.Cliente(otro.Token), HttpMethod.Post, $"{url}/respuesta", Version(emitida), new { decision = "aprobada" })
            .EsperarAsync(HttpStatusCode.NotFound);
        await api.Cliente(duena.Token).GetAsync(url).EsperarAsync(HttpStatusCode.OK);
        // Sigue pendiente: la respuesta ajena no hizo nada.
        Assert.Equal("pendiente", (await api.Cliente(duena.Token).GetAsync(url).EsperarAsync(HttpStatusCode.OK)).GetProperty("estado").GetString());
    }

    // ── Errores estandar ──

    [Fact]
    public async Task UnaTransicionInvalida_Responde409ConLaExplicacion()
    {
        var admin = await AdminAsync();
        var pedido = await api.CrearPedidoAsync();
        var url = $"/api/v1/pedidos/{pedido.GetProperty("idPedido").GetInt32()}/estado";

        var saltando = await ConVersion(admin, HttpMethod.Patch, url, Version(pedido), new { estado = "entregado" }).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("transicion_invalida", Codigo(saltando));
        Assert.Equal("Solo un pedido «terminado» se puede entregar.", saltando.GetProperty("error").GetString());

        var entregado = await AvanzarAsync(admin, pedido, "en_proceso", "terminado", "entregado");
        var final = await ConVersion(admin, HttpMethod.Patch, url, Version(entregado), new { estado = "anulado", motivo = "Ya no lo quiere" })
            .EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("Un pedido «entregado» ya no cambia de estado.", final.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SinVersion400_YVersionVieja409_EnCotizacionesYPedidos()
    {
        var admin = await AdminAsync();
        var emitida = await api.CrearCotizacionAsync(await api.CrearSolicitudAsync());
        var urlRespuesta = $"/api/v1/cotizaciones/{emitida.GetProperty("idCotizacion").GetInt32()}/respuesta";

        var sinVersion = await admin.PostAsJsonAsync(urlRespuesta, new { decision = "aprobada", via = "telefono" }).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("sin_version", Codigo(sinVersion));
        var editada = await ConVersion(admin, HttpMethod.Put, $"/api/v1/cotizaciones/{emitida.GetProperty("idCotizacion").GetInt32()}", Version(emitida),
            ApiFixture.CuerpoCotizacion(1500)).EsperarAsync(HttpStatusCode.OK);
        var vieja = await ConVersion(admin, HttpMethod.Post, urlRespuesta, Version(emitida), new { decision = "aprobada", via = "telefono" })
            .EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("version_obsoleta", Codigo(vieja));
        Assert.StartsWith("Otra persona modificó esta cotización", vieja.GetProperty("error").GetString());
        await ConVersion(admin, HttpMethod.Post, urlRespuesta, Version(editada), new { decision = "aprobada", via = "whatsapp" }).EsperarAsync(HttpStatusCode.OK);

        var pedido = await api.CrearPedidoAsync();
        var urlPedido = $"/api/v1/pedidos/{pedido.GetProperty("idPedido").GetInt32()}";
        Assert.Equal("sin_version", Codigo(await admin.PatchAsJsonAsync($"{urlPedido}/estado", new { estado = "en_proceso" }).EsperarAsync(HttpStatusCode.BadRequest)));
        // Un abono cambia la version del pedido (su respuesta trae la nueva).
        var abonado = await Abonar(admin, pedido.GetProperty("idPedido").GetInt32(), 1000).EsperarAsync(HttpStatusCode.Created);
        Assert.NotEqual(Version(pedido), Version(abonado));
        Assert.Equal("version_obsoleta", Codigo(await ConVersion(admin, HttpMethod.Patch, $"{urlPedido}/estado", Version(pedido), new { estado = "en_proceso" })
            .EsperarAsync(HttpStatusCode.Conflict)));
        await ConVersion(admin, HttpMethod.Patch, $"{urlPedido}/estado", Version(abonado), new { estado = "en_proceso" }).EsperarAsync(HttpStatusCode.OK);
    }

    // ── Abonos (E25) ──

    [Fact]
    public async Task UnAbonoQueSuperaElSaldo_Responde409ConElSaldo_YUnPedidoAnuladoNoRecibeAbonos()
    {
        var admin = await AdminAsync();
        var pedido = await api.CrearPedidoAsync(100_000);
        var id = pedido.GetProperty("idPedido").GetInt32();

        var primero = await Abonar(admin, id, 60_000, "transferencia", Hoy.AddDays(-3)).EsperarAsync(HttpStatusCode.Created);
        Assert.Equal(40_000, primero.GetProperty("saldoPendiente").GetInt32());
        Assert.Equal(Hoy.AddDays(-3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), primero.GetProperty("pagos")[0].GetProperty("fechaPago").GetString());

        var excede = await Abonar(admin, id, 40_001).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("abono_excede_saldo", Codigo(excede));
        Assert.Equal("api", excede.GetProperty("nivel").GetString());
        Assert.Equal("El abono supera el saldo pendiente ($40.000).", excede.GetProperty("error").GetString());
        Assert.Equal(1, await api.ConsultarAsync(db => db.Pagos.CountAsync(p => p.IdPedido == id)));

        var invalido = await Abonar(admin, id, 10.5m, "bitcoin", Hoy.AddDays(2)).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["fechaPago", "medioPago", "monto"], invalido.GetProperty("errores").EnumerateObject().Select(e => e.Name).Order());

        var anulado = await ConVersion(admin, HttpMethod.Patch, $"/api/v1/pedidos/{id}/estado", Version(primero),
            new { estado = "anulado", motivo = "El cliente se cambió de ciudad" }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(60_000, anulado.GetProperty("aFavorDelCliente").GetInt32());
        var rechazo = await Abonar(admin, id, 1000).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("pedido_anulado", Codigo(rechazo));
    }

    // Si un abono llega a la base y la regla del pedido lo frena, la API responde el mismo 409 (nivel base_de_datos).
    [Fact]
    public async Task SiLaBaseFrenaElAbono_RespondeElMismo409()
    {
        var pedido = await api.CrearPedidoAsync(100_000);
        var id = pedido.GetProperty("idPedido").GetInt32();

        var rechazo = await Abonar(api.ClienteConInterceptor(new AbonoInflado(Centinela), await api.TokenAdminAsync()), id, Centinela)
            .EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal("abono_excede_saldo", Codigo(rechazo));
        Assert.Equal("base_de_datos", rechazo.GetProperty("nivel").GetString());
        Assert.Equal("El abono supera el saldo pendiente ($100.000).", rechazo.GetProperty("error").GetString());
        Assert.Equal(0, await api.ConsultarAsync(db => db.Pagos.CountAsync(p => p.IdPedido == id)));
    }

    // Dos abonos a la vez sobre el mismo pedido: el primero toma el pedido y el segundo espera su turno, asi ve
    // el saldo que dejo el primero. El interceptor detiene al primero justo antes de guardar su abono, cuando
    // ya tiene el bloqueo, para que el segundo llegue en ese momento exacto.
    [Fact]
    public async Task DosAbonosSimultaneos_SeRegistranUnoTrasOtro_YNuncaSuperanElMontoFinal()
    {
        var token = await api.TokenAdminAsync();
        var id = (await api.CrearPedidoAsync(100_000)).GetProperty("idPedido").GetInt32();
        var enEspera = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var primero = Abonar(api.ClienteConInterceptor(new PausaAntesDelAbono(enEspera, TimeSpan.FromSeconds(2)), token), id, 60_000);
        await enEspera.Task.WaitAsync(TimeSpan.FromSeconds(60));
        var segundo = Abonar(api.Cliente(token), id, 60_000);

        await primero.EsperarAsync(HttpStatusCode.Created);
        var rechazo = await segundo.EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("abono_excede_saldo", Codigo(rechazo));
        // Lo detecto la API con el saldo ya actualizado, no la base.
        Assert.Equal("api", rechazo.GetProperty("nivel").GetString());
        Assert.Equal("El abono supera el saldo pendiente ($40.000).", rechazo.GetProperty("error").GetString());

        var (abonado, suma) = await api.ConsultarAsync(async db => (
            await db.Pedidos.AsNoTracking().Where(p => p.IdPedido == id).Select(p => p.MontoAbonado).SingleAsync(),
            await db.Pagos.Where(p => p.IdPedido == id && !p.Anulado).SumAsync(p => p.Monto)));
        Assert.Equal(60_000, abonado);
        Assert.Equal(abonado, suma);
    }

    // Muchos abonos a la vez: se aceptan los que caben y el total nunca supera el monto final.
    [Fact]
    public async Task VariosAbonosSimultaneos_SoloSeAceptanLosQueCaben()
    {
        var token = await api.TokenAdminAsync();
        var id = (await api.CrearPedidoAsync(100_000)).GetProperty("idPedido").GetInt32();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Abonar(api.Cliente(token), id, 30_000)));

        Assert.Equal(3, respuestas.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(3, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(90_000, await api.ConsultarAsync(db => db.Pedidos.AsNoTracking().Where(p => p.IdPedido == id).Select(p => p.MontoAbonado).SingleAsync()));
    }

    [Fact]
    public async Task AnularUnAbono_LoResta_YQuedaTachadoEnElHistorial()
    {
        var admin = await AdminAsync();
        var id = (await api.CrearPedidoAsync(100_000)).GetProperty("idPedido").GetInt32();
        var conAbono = await Abonar(admin, id, 30_000).EsperarAsync(HttpStatusCode.Created);
        var idPago = conAbono.GetProperty("pagos")[0].GetProperty("idPago").GetInt32();
        var url = $"/api/v1/pedidos/{id}/pagos/{idPago}/anular";

        await admin.PostAsJsonAsync(url, new { motivo = "Error" }).EsperarAsync(HttpStatusCode.BadRequest);
        var anulado = await admin.PostAsJsonAsync(url, new { motivo = "Se ingresó dos veces" }).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(0, anulado.GetProperty("montoAbonado").GetInt32());
        Assert.Equal("no_pagado", anulado.GetProperty("estadoPago").GetString());
        Assert.NotEqual(Version(conAbono), Version(anulado));
        var pago = Assert.Single(anulado.GetProperty("pagos").EnumerateArray());
        Assert.True(pago.GetProperty("anulado").GetBoolean());
        Assert.Equal("Se ingresó dos veces", pago.GetProperty("motivoAnulacion").GetString());
        Assert.Equal("Admin Pruebas", pago.GetProperty("anuladoPor").GetString());
        Assert.NotEqual(JsonValueKind.Null, pago.GetProperty("fechaAnulacionChile").ValueKind);
        Assert.False(pago.GetProperty("puedeAnular").GetBoolean());

        var otraVez = await admin.PostAsJsonAsync(url, new { motivo = "Se ingresó dos veces" }).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("abono_anulado", Codigo(otraVez));
    }

    [Fact]
    public async Task SinSerDeGestion_NoSeAnulaUnAbonoAjenoOAntiguo_NiUnPedidoConAbonos()
    {
        var admin = await AdminAsync();
        var operario = api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario));
        var pedido = await api.CrearPedidoAsync(100_000);
        var id = pedido.GetProperty("idPedido").GetInt32();

        var delAdmin = (await Abonar(admin, id, 10_000).EsperarAsync(HttpStatusCode.Created)).GetProperty("pagos")[0];
        var ajeno = await operario.PostAsJsonAsync($"/api/v1/pedidos/{id}/pagos/{delAdmin.GetProperty("idPago").GetInt32()}/anular",
            new { motivo = "Lo registró mal otra persona" }).EsperarAsync(HttpStatusCode.Forbidden);
        Assert.Equal("solo_gestion", Codigo(ajeno));

        // El propio, de las ultimas 24 horas, si lo anula; el propio antiguo, no.
        var conPropios = await Abonar(operario, id, 5_000).EsperarAsync(HttpStatusCode.Created);
        var propio = conPropios.GetProperty("pagos").EnumerateArray().Single(p => p.GetProperty("monto").GetInt32() == 5_000);
        Assert.True(propio.GetProperty("puedeAnular").GetBoolean());
        Assert.False(conPropios.GetProperty("pagos").EnumerateArray().Single(p => p.GetProperty("monto").GetInt32() == 10_000).GetProperty("puedeAnular").GetBoolean());
        await operario.PostAsJsonAsync($"/api/v1/pedidos/{id}/pagos/{propio.GetProperty("idPago").GetInt32()}/anular", new { motivo = "Me equivoqué de pedido" })
            .EsperarAsync(HttpStatusCode.OK);
        var idAntiguo = (await Abonar(operario, id, 7_000).EsperarAsync(HttpStatusCode.Created)).GetProperty("pagos").EnumerateArray()
            .Single(p => p.GetProperty("monto").GetInt32() == 7_000).GetProperty("idPago").GetInt32();
        var haceUnDia = HoraChile.Convertir(DateTimeOffset.UtcNow).AddHours(-25);
        await api.ConsultarAsync(db => db.Pagos.Where(p => p.IdPago == idAntiguo).ExecuteUpdateAsync(s => s.SetProperty(p => p.FechaRegistro, haceUnDia)));
        Assert.Equal("solo_gestion", Codigo(await operario.PostAsJsonAsync($"/api/v1/pedidos/{id}/pagos/{idAntiguo}/anular", new { motivo = "Me equivoqué de pedido" })
            .EsperarAsync(HttpStatusCode.Forbidden)));

        // Con abonos vigentes, el operario no anula el pedido (ni se le ofrece); gestion si.
        var paraOperario = await operario.GetAsync($"/api/v1/pedidos/{id}").EsperarAsync(HttpStatusCode.OK);
        Assert.DoesNotContain("anulado", paraOperario.GetProperty("transicionesPosibles").EnumerateArray().Select(t => t.GetString()));
        var rechazo = await ConVersion(operario, HttpMethod.Patch, $"/api/v1/pedidos/{id}/estado", Version(paraOperario),
            new { estado = "anulado", motivo = "El cliente desistió del trabajo" }).EsperarAsync(HttpStatusCode.Forbidden);
        Assert.Equal("solo_gestion", Codigo(rechazo));
        var paraGestion = await admin.GetAsync($"/api/v1/pedidos/{id}").EsperarAsync(HttpStatusCode.OK);
        Assert.Contains("anulado", paraGestion.GetProperty("transicionesPosibles").EnumerateArray().Select(t => t.GetString()));
        var anulado = await ConVersion(admin, HttpMethod.Patch, $"/api/v1/pedidos/{id}/estado", Version(paraGestion),
            new { estado = "anulado", motivo = "El cliente desistió del trabajo" }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(17_000, anulado.GetProperty("aFavorDelCliente").GetInt32());

        // Un pedido sin abonos si lo anula cualquiera del personal.
        var otro = await api.CrearPedidoAsync();
        await ConVersion(operario, HttpMethod.Patch, $"/api/v1/pedidos/{otro.GetProperty("idPedido").GetInt32()}/estado", Version(otro),
            new { estado = "anulado", motivo = "El cliente desistió del trabajo" }).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnPedidoEntregadoSinPagar_CierraSuCicloAlCompletarElPago()
    {
        var admin = await AdminAsync();
        var cliente = await api.CrearClienteConfirmadoAsync();
        var pedido = await api.CrearPedidoAsync(80_000, idSolicitud: await SolicitudEnLineaAsync(cliente.Token));
        var id = pedido.GetProperty("idPedido").GetInt32();

        var entregado = await AvanzarAsync(admin, pedido, "en_proceso", "terminado", "entregado");
        Assert.False(entregado.GetProperty("cicloCerrado").GetBoolean());
        await Abonar(admin, id, 30_000).EsperarAsync(HttpStatusCode.Created);
        var pagado = await Abonar(admin, id, 50_000).EsperarAsync(HttpStatusCode.Created);

        Assert.Equal("pagado_total", pagado.GetProperty("estadoPago").GetString());
        Assert.True(pagado.GetProperty("cicloCerrado").GetBoolean());
        var mio = Assert.Single((await api.Cliente(cliente.Token).GetAsync("/api/v1/mis/pedidos").EsperarAsync(HttpStatusCode.OK)).EnumerateArray());
        Assert.True(mio.GetProperty("cicloCerrado").GetBoolean());
        Assert.Equal(0, mio.GetProperty("saldoPendiente").GetInt32());
    }

    // ── Cobranza (S24) ──

    // Cinco pedidos de un mismo cliente, calculados a mano: por cobrar quedan el no pagado (100.000), el
    // abonado (80.000 - 30.000 = 50.000) y el entregado sin pagar (40.000). No cuentan el pagado ni el anulado.
    [Fact]
    public async Task LaCobranza_YElTotalPorCobrar_CoincidenConUnCalculoAMano()
    {
        var admin = await AdminAsync();
        var idCliente = await api.CrearClienteAsync();
        async Task<JsonElement> PedidoAsync(int monto) => await api.CrearPedidoAsync(monto, idSolicitud: await api.CrearSolicitudAsync(idCliente));

        var noPagado = await PedidoAsync(100_000);
        var abonado = await PedidoAsync(80_000);
        await Abonar(admin, abonado.GetProperty("idPedido").GetInt32(), 30_000).EsperarAsync(HttpStatusCode.Created);
        var pagado = await PedidoAsync(50_000);
        await Abonar(admin, pagado.GetProperty("idPedido").GetInt32(), 50_000).EsperarAsync(HttpStatusCode.Created);
        var anulado = await PedidoAsync(70_000);
        var conAbono = await Abonar(admin, anulado.GetProperty("idPedido").GetInt32(), 10_000).EsperarAsync(HttpStatusCode.Created);
        await ConVersion(admin, HttpMethod.Patch, $"/api/v1/pedidos/{anulado.GetProperty("idPedido").GetInt32()}/estado", Version(conAbono),
            new { estado = "anulado", motivo = "El cliente desistió del trabajo" }).EsperarAsync(HttpStatusCode.OK);
        var entregadoSinPagar = await AvanzarAsync(admin, await PedidoAsync(40_000), "en_proceso", "terminado", "entregado");

        // El resumen cubre todo el filtro, aunque la pagina traiga una sola fila.
        var cobranza = await admin.GetAsync($"/api/v1/pedidos?idCliente={idCliente}&cobranza=true&tamano=1&orden=saldo_desc").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(3, cobranza.GetProperty("total").GetInt32());
        Assert.Equal(3, cobranza.GetProperty("totalPaginas").GetInt32());
        Assert.Equal(noPagado.GetProperty("idPedido").GetInt32(), Assert.Single(cobranza.GetProperty("elementos").EnumerateArray()).GetProperty("idPedido").GetInt32());
        Assert.Equal(190_000, cobranza.GetProperty("resumen").GetProperty("totalPorCobrar").GetInt64());
        Assert.Equal(3, cobranza.GetProperty("resumen").GetProperty("cantidadPorCobrar").GetInt32());

        // Sin el filtro de cobranza estan los cinco, y el resumen sigue siendo lo por cobrar de ellos.
        var todos = await admin.GetAsync($"/api/v1/pedidos?idCliente={idCliente}&tamano=2").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(5, todos.GetProperty("total").GetInt32());
        Assert.Equal(190_000, todos.GetProperty("resumen").GetProperty("totalPorCobrar").GetInt64());
        // «abonado» incluye al anulado con abono (se ve en la lista), pero lo por cobrar no lo cuenta.
        var soloAbonados = await admin.GetAsync($"/api/v1/pedidos?idCliente={idCliente}&estadoPago=abonado").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal([abonado.GetProperty("idPedido").GetInt32(), anulado.GetProperty("idPedido").GetInt32()],
            soloAbonados.GetProperty("elementos").EnumerateArray().Select(p => p.GetProperty("idPedido").GetInt32()).Order());
        Assert.Equal(50_000, soloAbonados.GetProperty("resumen").GetProperty("totalPorCobrar").GetInt64());
        Assert.Equal(1, soloAbonados.GetProperty("resumen").GetProperty("cantidadPorCobrar").GetInt32());
        // El año y el mes del pedido salen de su propia fecha (la del servidor).
        var fechaPedido = entregadoSinPagar.GetProperty("fechaPedido").GetDateTime();
        var delMes = await admin.GetAsync($"/api/v1/pedidos?idCliente={idCliente}&anio={fechaPedido.Year}&mes={fechaPedido.Month}&estadoPedido=entregado")
            .EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(1, delMes.GetProperty("total").GetInt32());
        var filaEntregada = Assert.Single(delMes.GetProperty("elementos").EnumerateArray());
        Assert.Equal(40_000, filaEntregada.GetProperty("saldoPendiente").GetInt32());
        // Las filas traen la fecha real de entrega (null mientras no se entrega).
        Assert.Equal(entregadoSinPagar.GetProperty("fechaEntregaReal").GetString(), filaEntregada.GetProperty("fechaEntregaReal").GetString());
        Assert.Equal(JsonValueKind.Null, cobranza.GetProperty("elementos")[0].GetProperty("fechaEntregaReal").ValueKind);
        var otroMes = fechaPedido.AddMonths(1);
        Assert.Equal(0, (await admin.GetAsync($"/api/v1/pedidos?idCliente={idCliente}&anio={otroMes.Year}&mes={otroMes.Month}").EsperarAsync(HttpStatusCode.OK))
            .GetProperty("total").GetInt32());

        await admin.GetAsync("/api/v1/pedidos?estadoPedido=cancelado").EsperarAsync(HttpStatusCode.BadRequest);
        await admin.GetAsync("/api/v1/pedidos?mes=13").EsperarAsync(HttpStatusCode.BadRequest);
    }

    // ── Calendario (S08) y alertas ──

    [Fact]
    public async Task ElCalendario_LimitaElRango_YUbicaPendientesEntregadosYAtrasados()
    {
        var admin = await AdminAsync();
        string Fecha(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string Url(DateOnly desde, DateOnly hasta) => $"/api/v1/pedidos/calendario?desde={Fecha(desde)}&hasta={Fecha(hasta)}";

        await admin.GetAsync("/api/v1/pedidos/calendario").EsperarAsync(HttpStatusCode.BadRequest);
        await admin.GetAsync(Url(Hoy, Hoy.AddDays(-1))).EsperarAsync(HttpStatusCode.BadRequest);
        await admin.GetAsync(Url(Hoy, Hoy.AddDays(62))).EsperarAsync(HttpStatusCode.BadRequest);
        await admin.GetAsync(Url(Hoy, Hoy.AddDays(61))).EsperarAsync(HttpStatusCode.OK);
        await admin.GetAsync(Url(DateOnly.MaxValue.AddDays(-10), DateOnly.MaxValue)).EsperarAsync(HttpStatusCode.BadRequest);

        var pedidoPendiente = await api.CrearPedidoAsync();
        var pendiente = pedidoPendiente.GetProperty("idPedido").GetInt32();
        var pedidoEntregado = await AvanzarAsync(admin, await api.CrearPedidoAsync(), "en_proceso", "terminado", "entregado");
        var entregado = pedidoEntregado.GetProperty("idPedido").GetInt32();
        var atrasado = (await api.CrearPedidoAsync()).GetProperty("idPedido").GetInt32();
        var proximo = (await api.CrearPedidoAsync()).GetProperty("idPedido").GetInt32();
        await api.ConsultarAsync(db => db.Pedidos.Where(p => p.IdPedido == atrasado)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.FechaEntregaEstimada, Hoy.AddDays(-2).ToDateTime(TimeOnly.MinValue))));
        await api.ConsultarAsync(db => db.Pedidos.Where(p => p.IdPedido == proximo)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.FechaEntregaEstimada, Hoy.AddDays(2).ToDateTime(TimeOnly.MinValue))));
        var otro = await api.CrearPedidoAsync();
        await ConVersion(admin, HttpMethod.Patch, $"/api/v1/pedidos/{otro.GetProperty("idPedido").GetInt32()}/estado", Version(otro),
            new { estado = "anulado", motivo = "El cliente desistió del trabajo" }).EsperarAsync(HttpStatusCode.OK);

        var calendario = (await admin.GetAsync(Url(Hoy.AddDays(-5), Hoy.AddDays(10))).EsperarAsync(HttpStatusCode.OK)).EnumerateArray().ToList();
        JsonElement De(int id) => calendario.Single(e => e.GetProperty("idPedido").GetInt32() == id);
        // Los pendientes en su fecha estimada y los entregados en la real, tal como los guardo el servidor.
        Assert.Equal(pedidoPendiente.GetProperty("fechaEntregaEstimada").GetString(), De(pendiente).GetProperty("fecha").GetString());
        Assert.False(De(pendiente).GetProperty("atrasado").GetBoolean());
        Assert.Equal(pedidoEntregado.GetProperty("fechaEntregaReal").GetString(), De(entregado).GetProperty("fecha").GetString());
        Assert.Equal("entregado", De(entregado).GetProperty("estadoPedido").GetString());
        Assert.True(De(atrasado).GetProperty("atrasado").GetBoolean());
        Assert.DoesNotContain(calendario, e => e.GetProperty("idPedido").GetInt32() == otro.GetProperty("idPedido").GetInt32());
        Assert.Equal(calendario.Select(e => e.GetProperty("fecha").GetString()).Order(StringComparer.Ordinal), calendario.Select(e => e.GetProperty("fecha").GetString()));

        // Alertas para gestion: de hoy a hoy + 3, las atrasadas y el total por cobrar.
        var alertas = await admin.GetAsync("/api/v1/pedidos/alertas").EsperarAsync(HttpStatusCode.OK);
        Assert.Contains(alertas.GetProperty("proximas").EnumerateArray(), e => e.GetProperty("idPedido").GetInt32() == proximo);
        Assert.DoesNotContain(alertas.GetProperty("proximas").EnumerateArray(), e => e.GetProperty("idPedido").GetInt32() == pendiente);
        Assert.Contains(alertas.GetProperty("atrasadas").EnumerateArray(), e => e.GetProperty("idPedido").GetInt32() == atrasado);
        Assert.True(alertas.GetProperty("totalPorCobrar").GetInt64() > 0);
        await api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario)).GetAsync("/api/v1/pedidos/alertas").EsperarAsync(HttpStatusCode.Forbidden);
    }

    // ── Aviso «Tu cotizacion esta lista» ──

    [Fact]
    public async Task ElAvisoDeCotizacionLista_SoloSaleAUnCorreoConfirmado()
    {
        var admin = await AdminAsync();

        // Presencial sin correo, y con un correo que nadie confirmo: no sale.
        var sinCorreo = (await api.CrearCotizacionAsync(await api.CrearSolicitudAsync())).GetProperty("idCotizacion").GetInt32();
        var idSinConfirmar = (await admin.PostAsJsonAsync("/api/v1/clientes/", new
        {
            nombre = "Cli", apellido = "Sin Confirmar", telefono = api.Telefono(), email = $"sinconfirmar{api.Siguiente()}@correo.test"
        }).EsperarAsync(HttpStatusCode.Created)).GetProperty("idCliente").GetInt32();
        var sinConfirmar = (await api.CrearCotizacionAsync(await api.CrearSolicitudAsync(idSinConfirmar))).GetProperty("idCotizacion").GetInt32();
        Assert.Equal(0, await CorreosDeCotizacionAsync(sinCorreo));
        Assert.Equal(0, await CorreosDeCotizacionAsync(sinConfirmar));

        // Confirmado: sale con el asunto, el total redondeado y el boton a la solicitud en su cuenta.
        var cliente = await api.CrearClienteConfirmadoAsync();
        var idSolicitud = await SolicitudEnLineaAsync(cliente.Token);
        var emitida = await api.CrearCotizacionAsync(idSolicitud);
        var idCotizacion = emitida.GetProperty("idCotizacion").GetInt32();
        var correo = await api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking().SingleAsync(c => c.Referencia == $"cotizacion:{idCotizacion}"));
        Assert.Equal("cotizacion_lista", correo.Tipo);
        Assert.Equal(cliente.Correo, correo.Destinatarios);
        Assert.Equal($"Tu cotización #{idCotizacion} está lista", correo.Asunto);
        Assert.Contains($"https://synctime.test/cuenta/solicitudes/{idSolicitud}", correo.CuerpoHtml, StringComparison.Ordinal);
        Assert.Contains("Ver cotización", correo.CuerpoTexto, StringComparison.Ordinal);
        Assert.Contains("$2.000", correo.CuerpoTexto, StringComparison.Ordinal);
        Assert.Contains("no respondas", correo.CuerpoTexto, StringComparison.Ordinal);
    }

    // ── Permisos (RFU12-RFU14, diferencias 3 y 4 de permisos.md) ──

    [Fact]
    public async Task ElOperarioYElDisenador_CotizanRespondenYCreanPedidos()
    {
        var operario = api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario));
        var (idDisenador, _, tokenDisenador) = await api.CrearEmpleadoAsync(Roles.Disenador);
        var disenador = api.Cliente(tokenDisenador);

        var emitida = await operario.PostAsJsonAsync($"/api/v1/solicitudes/{await api.CrearSolicitudAsync()}/cotizaciones", ApiFixture.CuerpoCotizacion())
            .EsperarAsync(HttpStatusCode.Created);
        var idCotizacion = emitida.GetProperty("idCotizacion").GetInt32();
        var aprobada = await ConVersion(disenador, HttpMethod.Post, $"/api/v1/cotizaciones/{idCotizacion}/respuesta", Version(emitida),
            new { decision = "aprobada", via = "telefono" }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("Emp Prueba", aprobada.GetProperty("respuesta").GetProperty("registradaPor").GetProperty("nombre").GetString());
        Assert.Equal("telefono", aprobada.GetProperty("respuesta").GetProperty("via").GetString());

        var pedido = await disenador.PostAsJsonAsync($"/api/v1/cotizaciones/{idCotizacion}/pedido", ApiFixture.DatosPedido(idDisenador))
            .EsperarAsync(HttpStatusCode.Created);
        Assert.Equal(idDisenador, pedido.GetProperty("responsable").GetProperty("idEmpleado").GetInt32());
        await Abonar(operario, pedido.GetProperty("idPedido").GetInt32(), 500).EsperarAsync(HttpStatusCode.Created);
        await AvanzarAsync(disenador, await disenador.GetAsync($"/api/v1/pedidos/{pedido.GetProperty("idPedido").GetInt32()}").EsperarAsync(HttpStatusCode.OK), "en_proceso");
        await operario.PostAsJsonAsync("/api/v1/pedidos/directo", CuerpoDirecto(await api.CrearClienteAsync(), idDisenador, abono: 0))
            .EsperarAsync(HttpStatusCode.Created);

        // Eliminar sigue siendo de gestion.
        var otra = await api.CrearCotizacionAsync(await api.CrearSolicitudAsync());
        await ConVersion(operario, HttpMethod.Delete, $"/api/v1/cotizaciones/{otra.GetProperty("idCotizacion").GetInt32()}", Version(otra))
            .EsperarAsync(HttpStatusCode.Forbidden);
    }

    // ── Eliminar (gestion, borrado logico) ──

    [Fact]
    public async Task EliminarUnaPendiente_DevuelveLaSolicitudASolicitado_YUnPedidoAnuladoSinAbonosSeElimina()
    {
        var admin = await AdminAsync();
        var idSolicitud = await api.CrearSolicitudAsync();
        var emitida = await api.CrearCotizacionAsync(idSolicitud);
        await ConVersion(admin, HttpMethod.Delete, $"/api/v1/cotizaciones/{emitida.GetProperty("idCotizacion").GetInt32()}", Version(emitida))
            .EsperarAsync(HttpStatusCode.NoContent);
        var solicitud = await admin.GetAsync($"/api/v1/solicitudes/{idSolicitud}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("solicitado", solicitud.GetProperty("estado").GetString());
        Assert.Equal($"Se eliminó la cotización #{emitida.GetProperty("idCotizacion").GetInt32()}.",
            solicitud.GetProperty("historial").EnumerateArray().Last().GetProperty("motivo").GetString());
        await api.CrearCotizacionAsync(idSolicitud);

        var pedido = await api.CrearPedidoAsync();
        var url = $"/api/v1/pedidos/{pedido.GetProperty("idPedido").GetInt32()}";
        var anulado = await ConVersion(admin, HttpMethod.Patch, $"{url}/estado", Version(pedido), new { estado = "anulado", motivo = "El cliente desistió del trabajo" })
            .EsperarAsync(HttpStatusCode.OK);
        await ConVersion(admin, HttpMethod.Delete, url, Version(anulado)).EsperarAsync(HttpStatusCode.NoContent);
        await admin.GetAsync(url).EsperarAsync(HttpStatusCode.NotFound);
        Assert.True(await api.ConsultarAsync(db => db.Pedidos.Where(p => p.IdPedido == pedido.GetProperty("idPedido").GetInt32()).Select(p => p.EliminadoLogico).SingleAsync()));
    }

    // ── Listado de cotizaciones ──

    [Fact]
    public async Task ElListadoDeCotizaciones_FiltraBuscaYOrdena()
    {
        var admin = await AdminAsync();
        var idCliente = await api.CrearClienteAsync();
        var barata = await api.CrearCotizacionAsync(await api.CrearSolicitudAsync(idCliente));
        var idSolicitudCara = await api.CrearSolicitudAsync(idCliente);
        var cara = await admin.PostAsJsonAsync($"/api/v1/solicitudes/{idSolicitudCara}/cotizaciones", ApiFixture.CuerpoCotizacion(90_000))
            .EsperarAsync(HttpStatusCode.Created);
        await api.ResponderCotizacionAsync(cara.GetProperty("idCotizacion").GetInt32(), "rechazada", "Muy cara");

        var delCliente = await admin.GetAsync($"/api/v1/cotizaciones?idCliente={idCliente}&orden=total_desc").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal([cara.GetProperty("idCotizacion").GetInt32(), barata.GetProperty("idCotizacion").GetInt32()],
            delCliente.GetProperty("elementos").EnumerateArray().Select(c => c.GetProperty("idCotizacion").GetInt32()));
        var fila = delCliente.GetProperty("elementos")[0];
        Assert.Equal(idCliente, fila.GetProperty("cliente").GetProperty("idCliente").GetInt32());
        Assert.Equal("Admin Pruebas", fila.GetProperty("elaboradaPor").GetProperty("nombre").GetString());

        var pendientes = await admin.GetAsync($"/api/v1/cotizaciones?idCliente={idCliente}&estado=pendiente").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(barata.GetProperty("idCotizacion").GetInt32(), Assert.Single(pendientes.GetProperty("elementos").EnumerateArray()).GetProperty("idCotizacion").GetInt32());
        var porSolicitud = await admin.GetAsync($"/api/v1/cotizaciones?q=%23{idSolicitudCara}").EsperarAsync(HttpStatusCode.OK);
        Assert.Contains(porSolicitud.GetProperty("elementos").EnumerateArray(), c => c.GetProperty("idSolicitud").GetInt32() == idSolicitudCara);
        await admin.GetAsync("/api/v1/cotizaciones?estado=inventado").EsperarAsync(HttpStatusCode.BadRequest);
    }

    // ── Revision de ECC ──

    // Un pedido eliminado sigue ocupando su cotizacion (y su solicitud): la cotizacion, la ficha de la
    // solicitud y el seguimiento del cliente lo informan marcado, y la API no deja crear otro.
    [Fact]
    public async Task UnPedidoEliminado_SigueOcupandoSuCotizacion_YSeInforma()
    {
        var admin = await AdminAsync();
        var cliente = await api.CrearClienteConfirmadoAsync();
        var idSolicitud = await SolicitudEnLineaAsync(cliente.Token);
        var rechazada = (await api.CrearCotizacionAsync(idSolicitud)).GetProperty("idCotizacion").GetInt32();
        await api.ResponderCotizacionAsync(rechazada, "rechazada", "Prefiero otro material");
        var emitida = await api.CrearCotizacionAsync(idSolicitud);
        var idCotizacion = emitida.GetProperty("idCotizacion").GetInt32();

        // La ficha de la solicitud trae sus cotizaciones, de la mas nueva a la mas antigua.
        var ficha = await admin.GetAsync($"/api/v1/solicitudes/{idSolicitud}").EsperarAsync(HttpStatusCode.OK);
        var cotizaciones = ficha.GetProperty("cotizaciones").EnumerateArray().ToList();
        Assert.Equal([idCotizacion, rechazada], cotizaciones.Select(c => c.GetProperty("idCotizacion").GetInt32()));
        Assert.Equal($"#{idCotizacion}", cotizaciones[0].GetProperty("numero").GetString());
        Assert.Equal("pendiente", cotizaciones[0].GetProperty("estado").GetString());
        Assert.Equal(2000m, cotizaciones[0].GetProperty("total").GetDecimal());
        Assert.Equal(JsonValueKind.Null, cotizaciones[0].GetProperty("idPedido").ValueKind);
        Assert.False(cotizaciones[0].GetProperty("pedidoEliminado").GetBoolean());

        await api.AprobarCotizacionAsync(idCotizacion);
        var pedido = await admin.PostAsJsonAsync($"/api/v1/cotizaciones/{idCotizacion}/pedido", ApiFixture.DatosPedido(await api.IdAdminAsync()))
            .EsperarAsync(HttpStatusCode.Created);
        var idPedido = pedido.GetProperty("idPedido").GetInt32();
        var anulado = await ConVersion(admin, HttpMethod.Patch, $"/api/v1/pedidos/{idPedido}/estado", Version(pedido),
            new { estado = "anulado", motivo = "El cliente desistió del trabajo" }).EsperarAsync(HttpStatusCode.OK);
        await ConVersion(admin, HttpMethod.Delete, $"/api/v1/pedidos/{idPedido}", Version(anulado)).EsperarAsync(HttpStatusCode.NoContent);

        var cotizacion = await admin.GetAsync($"/api/v1/cotizaciones/{idCotizacion}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(idPedido, cotizacion.GetProperty("idPedido").GetInt32());
        Assert.True(cotizacion.GetProperty("pedidoEliminado").GetBoolean());
        var otraVez = await admin.PostAsJsonAsync($"/api/v1/cotizaciones/{idCotizacion}/pedido", ApiFixture.DatosPedido(await api.IdAdminAsync()))
            .EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("cotizacion_con_pedido", Codigo(otraVez));
        Assert.Equal($"Esta cotización ya tuvo el pedido #{idPedido} (eliminado).", otraVez.GetProperty("error").GetString());
        var enLaFicha = (await admin.GetAsync($"/api/v1/solicitudes/{idSolicitud}").EsperarAsync(HttpStatusCode.OK)).GetProperty("cotizaciones")[0];
        Assert.Equal(idPedido, enLaFicha.GetProperty("idPedido").GetInt32());
        Assert.True(enLaFicha.GetProperty("pedidoEliminado").GetBoolean());

        // El cliente ve que su pedido se anulo.
        var seguimiento = (await api.Cliente(cliente.Token).GetAsync($"/api/v1/mis/solicitudes/{idSolicitud}").EsperarAsync(HttpStatusCode.OK)).GetProperty("pedido");
        Assert.Equal(idPedido, seguimiento.GetProperty("idPedido").GetInt32());
        Assert.Equal("anulado", seguimiento.GetProperty("estadoPedido").GetString());
        Assert.True(seguimiento.GetProperty("eliminado").GetBoolean());
        Assert.True(Assert.Single((await api.Cliente(cliente.Token).GetAsync("/api/v1/mis/pedidos").EsperarAsync(HttpStatusCode.OK)).EnumerateArray())
            .GetProperty("eliminado").GetBoolean());
        Assert.Equal(cotizacion.GetProperty("idPedido").GetInt32(),
            (await api.Cliente(cliente.Token).GetAsync($"/api/v1/mis/cotizaciones/{idCotizacion}").EsperarAsync(HttpStatusCode.OK)).GetProperty("idPedido").GetInt32());
    }

    // Cambiar los montos de un pedido es de gestion y queda en el historial; lo demas lo edita todo el personal.
    [Fact]
    public async Task CambiarLosMontosDeUnPedido_EsDeGestion_YQuedaEnElHistorial()
    {
        var admin = await AdminAsync();
        var operario = api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario));
        var pedido = await api.CrearPedidoAsync(100_000);
        var url = $"/api/v1/pedidos/{pedido.GetProperty("idPedido").GetInt32()}";
        var idResponsable = pedido.GetProperty("responsable").GetProperty("idEmpleado").GetInt32();
        object Cuerpo(decimal montoBase, object fecha, string modalidad = "retiro_local", string? factura = null) => new
        {
            idEmpleadoResponsable = idResponsable, fechaEntregaEstimada = fecha, modalidadEntrega = modalidad,
            requiereInstalacion = false, costoInstalacionAdicional = 0, montoBaseTrabajo = montoBase, numeroFactura = factura
        };

        var rechazo = await ConVersion(operario, HttpMethod.Put, url, Version(pedido), Cuerpo(90_000, Hoy.AddDays(9))).EsperarAsync(HttpStatusCode.Forbidden);
        Assert.Equal("solo_gestion", Codigo(rechazo));
        // Sin tocar los montos, el operario si edita (y no queda una fila de montos en el historial).
        var editado = await ConVersion(operario, HttpMethod.Put, url, Version(pedido), Cuerpo(100_000, Hoy.AddDays(9))).EsperarAsync(HttpStatusCode.OK);
        Assert.Single(editado.GetProperty("historial").EnumerateArray());

        var conMontos = await ConVersion(admin, HttpMethod.Put, url, Version(editado), Cuerpo(90_000, Hoy.AddDays(9))).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(90_000, conMontos.GetProperty("montoTotalFinal").GetInt32());
        var cambio = conMontos.GetProperty("historial").EnumerateArray().Last();
        Assert.Equal("aceptado", cambio.GetProperty("estadoAnterior").GetString());
        Assert.Equal("aceptado", cambio.GetProperty("estadoNuevo").GetString());
        Assert.Equal("Monto final: $100.000 → $90.000 (monto base $100.000 → $90.000).", cambio.GetProperty("nota").GetString());
        Assert.Equal("Admin Pruebas", cambio.GetProperty("empleado").GetString());

        // La fecha estimada que ya tenia se acepta aunque haya pasado (el pedido esta atrasado).
        await api.ConsultarAsync(db => db.Pedidos.Where(p => p.IdPedido == pedido.GetProperty("idPedido").GetInt32())
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.FechaEntregaEstimada, Hoy.AddDays(-3).ToDateTime(TimeOnly.MinValue))));
        var atrasado = await operario.GetAsync(url).EsperarAsync(HttpStatusCode.OK);
        var mismaFecha = await ConVersion(operario, HttpMethod.Put, url, Version(atrasado),
            Cuerpo(90_000, atrasado.GetProperty("fechaEntregaEstimada").GetString()!)).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(atrasado.GetProperty("fechaEntregaEstimada").GetString(), mismaFecha.GetProperty("fechaEntregaEstimada").GetString());

        // Entregado: solo cuenta el numero de factura; el resto (aun invalido) se ignora.
        var entregado = await AvanzarAsync(admin, mismaFecha, "en_proceso", "terminado", "entregado");
        var conFactura = await ConVersion(operario, HttpMethod.Put, url, Version(entregado), Cuerpo(1, Hoy.AddDays(-30), "inventada", "F-77"))
            .EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("F-77", conFactura.GetProperty("numeroFactura").GetString());
        Assert.Equal(90_000, conFactura.GetProperty("montoBaseTrabajo").GetInt32());
        Assert.Equal("retiro_local", conFactura.GetProperty("modalidadEntrega").GetString());
    }

    // Un monto obligatorio que no viene es un 400 por campo, no un 0. Las claves son las del formulario.
    [Fact]
    public async Task LosMontosQueNoVienen_Responden400PorCampo()
    {
        var admin = await AdminAsync();
        var sinCostos = await admin.PostAsJsonAsync($"/api/v1/solicitudes/{await api.CrearSolicitudAsync()}/cotizaciones",
            new { materiales = new[] { new { idMaterial = 1, cantidad = 0 } } }).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["costoManoObra", "materiales[0].cantidad", "materiales[0].costoUnitario", "otrosCostos"],
            sinCostos.GetProperty("errores").EnumerateObject().Select(e => e.Name).Order());

        var pedido = await api.CrearPedidoAsync();
        var sinMontos = await ConVersion(admin, HttpMethod.Put, $"/api/v1/pedidos/{pedido.GetProperty("idPedido").GetInt32()}", Version(pedido), new
        {
            idEmpleadoResponsable = await api.IdAdminAsync(), fechaEntregaEstimada = Hoy.AddDays(5), modalidadEntrega = "retiro_local", requiereInstalacion = false
        }).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["costoInstalacionAdicional", "montoBaseTrabajo"], sinMontos.GetProperty("errores").EnumerateObject().Select(e => e.Name).Order());

        var idCotizacion = (await api.CrearCotizacionAsync(await api.CrearSolicitudAsync())).GetProperty("idCotizacion").GetInt32();
        await api.AprobarCotizacionAsync(idCotizacion);
        var conAbonoInvalido = await admin.PostAsJsonAsync($"/api/v1/cotizaciones/{idCotizacion}/pedido", new
        {
            idEmpleadoResponsable = await api.IdAdminAsync(), fechaEntregaEstimada = Hoy.AddDays(5), modalidadEntrega = "retiro_local",
            requiereInstalacion = false, abonoInicial = new { monto = 0, medioPago = "bitcoin" }
        }).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["abonoInicial.medioPago", "abonoInicial.monto", "costoInstalacionAdicional"],
            conAbonoInvalido.GetProperty("errores").EnumerateObject().Select(e => e.Name).Order());
    }

    // Registrar un abono, anularlo y despues anular el pedido no sirve para saltarse a gestion.
    [Fact]
    public async Task UnPedidoConAbonosAnulados_SoloLoAnulaGestion()
    {
        var admin = await AdminAsync();
        var operario = api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario));
        var id = (await api.CrearPedidoAsync()).GetProperty("idPedido").GetInt32();
        var conAbono = await Abonar(operario, id, 5_000).EsperarAsync(HttpStatusCode.Created);
        var sinAbono = await operario.PostAsJsonAsync($"/api/v1/pedidos/{id}/pagos/{conAbono.GetProperty("pagos")[0].GetProperty("idPago").GetInt32()}/anular",
            new { motivo = "Me equivoqué de pedido" }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(0, sinAbono.GetProperty("montoAbonado").GetInt32());

        Assert.DoesNotContain("anulado", sinAbono.GetProperty("transicionesPosibles").EnumerateArray().Select(t => t.GetString()));
        var rechazo = await ConVersion(operario, HttpMethod.Patch, $"/api/v1/pedidos/{id}/estado", Version(sinAbono),
            new { estado = "anulado", motivo = "El cliente desistió del trabajo" }).EsperarAsync(HttpStatusCode.Forbidden);
        Assert.Equal("solo_gestion", Codigo(rechazo));
        await ConVersion(admin, HttpMethod.Patch, $"/api/v1/pedidos/{id}/estado", Version(sinAbono),
            new { estado = "anulado", motivo = "El cliente desistió del trabajo" }).EsperarAsync(HttpStatusCode.OK);
    }

    // Un abono que llega mientras el operario anula el pedido: nunca queda un pedido anulado con abonos por
    // alguien que no es de gestion. El interceptor detiene la anulacion justo antes de guardarla.
    [Fact]
    public async Task UnAbonoQueLlegaMientrasSeAnula_NuncaDejaUnAnuladoConAbonos()
    {
        var admin = await AdminAsync();
        var tokenOperario = await api.TokenEmpleadoAsync(Roles.Operario);
        var pedido = await api.CrearPedidoAsync();
        var id = pedido.GetProperty("idPedido").GetInt32();
        var llego = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seguir = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var anulacion = ConVersion(api.ClienteConInterceptor(new PausaAntesDeGuardarElPedido(llego, seguir), tokenOperario), HttpMethod.Patch,
            $"/api/v1/pedidos/{id}/estado", Version(pedido), new { estado = "anulado", motivo = "El cliente desistió del trabajo" });
        await llego.Task.WaitAsync(TimeSpan.FromSeconds(60));
        await Abonar(admin, id, 1_000).EsperarAsync(HttpStatusCode.Created);
        seguir.SetResult();

        var rechazo = await anulacion.EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("version_obsoleta", Codigo(rechazo));
        var actual = await admin.GetAsync($"/api/v1/pedidos/{id}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("aceptado", actual.GetProperty("estadoPedido").GetString());
        Assert.Equal(1_000, actual.GetProperty("montoAbonado").GetInt32());
        // Con la version nueva, el operario ya no puede: el pedido tiene abonos.
        Assert.Equal("solo_gestion", Codigo(await ConVersion(api.Cliente(tokenOperario), HttpMethod.Patch, $"/api/v1/pedidos/{id}/estado", Version(actual),
            new { estado = "anulado", motivo = "El cliente desistió del trabajo" }).EsperarAsync(HttpStatusCode.Forbidden)));
    }

    // El pedido solo nace de una cotizacion aprobada cuya solicitud sigue «aceptado» y no esta eliminada.
    [Fact]
    public async Task ElPedido_SoloNaceDeUnaSolicitudAceptada()
    {
        var admin = await AdminAsync();
        var idSolicitud = await api.CrearSolicitudAsync();
        var idCotizacion = (await api.CrearCotizacionAsync(idSolicitud)).GetProperty("idCotizacion").GetInt32();
        await api.AprobarCotizacionAsync(idCotizacion);
        var url = $"/api/v1/cotizaciones/{idCotizacion}/pedido";

        await api.ConsultarAsync(db => db.Solicitudes.Where(s => s.IdSolicitud == idSolicitud).ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, "cotizado")));
        var noAceptada = await admin.PostAsJsonAsync(url, ApiFixture.DatosPedido(await api.IdAdminAsync())).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("transicion_invalida", Codigo(noAceptada));
        Assert.Contains("«aceptado»", noAceptada.GetProperty("error").GetString(), StringComparison.Ordinal);

        await api.ConsultarAsync(db => db.Solicitudes.Where(s => s.IdSolicitud == idSolicitud)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, "aceptado").SetProperty(x => x.EliminadoLogico, true)));
        var eliminada = await admin.PostAsJsonAsync(url, ApiFixture.DatosPedido(await api.IdAdminAsync())).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("transicion_invalida", Codigo(eliminada));
        Assert.False(await api.ConsultarAsync(db => db.Pedidos.AnyAsync(p => p.IdCotizacion == idCotizacion)));
    }

    // La factura de un abono solo completa la del pedido; si ya tiene otra, se cambia editando el pedido.
    [Fact]
    public async Task LaFacturaDeUnAbono_SoloCompletaLaDelPedido()
    {
        var admin = await AdminAsync();
        var id = (await api.CrearPedidoAsync()).GetProperty("idPedido").GetInt32();
        Task<HttpResponseMessage> AbonarConFactura(string factura) =>
            admin.PostAsJsonAsync($"/api/v1/pedidos/{id}/pagos", new { monto = 1_000, medioPago = "efectivo", numeroFactura = factura });

        Assert.Equal("F-100", (await AbonarConFactura("F-100").EsperarAsync(HttpStatusCode.Created)).GetProperty("numeroFactura").GetString());
        await AbonarConFactura("f-100").EsperarAsync(HttpStatusCode.Created);
        var distinta = await AbonarConFactura("F-200").EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("factura_distinta", Codigo(distinta));
        Assert.Equal("El pedido ya tiene la factura F-100; cámbiala editando el pedido.", distinta.GetProperty("error").GetString());
        Assert.Equal(2, await api.ConsultarAsync(db => db.Pagos.CountAsync(p => p.IdPedido == id)));
    }

    // Las rutas nuevas solo aceptan ids numericos: otro texto no existe (404), no es un error del servidor.
    [Fact]
    public async Task LasRutasNuevas_SoloAceptanIdsNumericos()
    {
        var admin = await AdminAsync();
        await admin.GetAsync("/api/v1/pedidos/abc").EsperarAsync(HttpStatusCode.NotFound);
        await admin.GetAsync("/api/v1/cotizaciones/abc").EsperarAsync(HttpStatusCode.NotFound);
        await admin.PostAsJsonAsync("/api/v1/pedidos/1/pagos/abc/anular", new { motivo = "Se ingresó dos veces" }).EsperarAsync(HttpStatusCode.NotFound);
    }

    // ── Interceptores: provocan el momento exacto que se quiere probar ──

    // Detiene el guardado de un cambio del pedido (su UPDATE) hasta que la prueba lo deja seguir.
    private sealed class PausaAntesDeGuardarElPedido(TaskCompletionSource llego, TaskCompletionSource seguir) : DbCommandInterceptor
    {
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE [pedidos]", StringComparison.Ordinal))
            {
                llego.TrySetResult();
                await seguir.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
            }
            return result;
        }
    }

    private const int Centinela = 12_345;

    // Cambia el monto del abono por uno enorme justo al guardarlo, para que lo frene la regla de la base
    // (ck_pedidos_monto_abonado) y no la revision del servicio.
    private sealed class AbonoInflado(int centinela) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Inflar(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Inflar(command);
            return ValueTask.FromResult(result);
        }

        private void Inflar(DbCommand command)
        {
            if (!command.CommandText.Contains("INSERT INTO [pagos]", StringComparison.Ordinal))
                return;
            foreach (DbParameter parametro in command.Parameters)
                if (parametro.Value is int valor && valor == centinela)
                    parametro.Value = 999_999_999;
        }
    }

    // Detiene un abono justo antes de guardarlo (ya con el pedido bloqueado) y avisa que llego ahi.
    private sealed class PausaAntesDelAbono(TaskCompletionSource enEspera, TimeSpan pausa) : DbCommandInterceptor
    {
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO [pagos]", StringComparison.Ordinal))
            {
                enEspera.TrySetResult();
                await Task.Delay(pausa, cancellationToken);
            }
            return result;
        }
    }

    // Justo despues de que el servicio reviso que no hay otra pendiente, otra persona guarda una.
    private sealed class SeCuelaTrasLaRevision(Func<Task<int>> colar) : DbCommandInterceptor
    {
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(ServicioCotizaciones.EtiquetaPendiente, StringComparison.Ordinal))
                await colar();
            return result;
        }
    }
}
