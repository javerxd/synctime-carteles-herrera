using System.Net;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 12B (RFS09, RFU19, RFU27, E21, S10 a S14, S19, S24): el dashboard contra SQL Server, con datos CONOCIDOS y cada cifra
// esperada calculada a mano. Los datos viven en marzo y febrero de 2021 (ninguna otra prueba usa ese año) y los
// totales «vigentes» se acotan con el cliente, porque otras pruebas dejan pedidos de hoy en la misma base.
//
//   Periodo: marzo de 2021.            Comparacion: febrero de 2021.            Clientes: A y B.
//   Productos: 1 (categoria 1, «Impresión y publicidad gráfica») y 9 (categoria 2, «Letreros y estructuras»).
//
//   Solicitud            origen      fecha       items               cotizacion   pedido
//   S1  cliente A        online      03-03-02    1×(1)               aprobada     P1
//   S2  cliente B        online      03-03-03    1×(9)               aprobada     P2 (ANULADO)
//   S3  cliente A        presencial  03-03-06    1×(1) + 3×(9)       —            P3
//   S4  cliente B        presencial  03-03-10    1×(9)               —            P4
//   S5  cliente A        online      03-03-05    1×(1)               —            —
//   S6  cliente B        online      03-03-07    1×(1)               rechazada    —
//   S7  cliente A        presencial  03-01-15    1×(9)               —            P5
//   S8  cliente A        online      03-02-10    1×(1)               aprobada     P6
//   S9  cliente B        online      03-02-11    2×(9)               rechazada    —
//
//   Pedido  creado    estimada  real      estado      base     abonado  saldo   pago
//   P1      03-04     03-10     03-09     entregado   100.000  100.000  0       pagado_total   (a tiempo)
//   P2      03-05     03-20     —         anulado      50.000        0  50.000  no_pagado      (anulado: nunca cuenta)
//   P3      03-06     03-12     03-14     entregado    80.000        0  80.000  no_pagado      (atrasado)
//   P4      03-10     03-25     —         en_proceso   60.000   20.000  40.000  abonado
//   P5      01-15     01-30     —         terminado    90.000        0  90.000  no_pagado      (deuda antigua)
//   P6      02-12     02-20     02-20     entregado   120.000   40.000  80.000  abonado        (a tiempo, el mismo dia)
[Collection(ApiCollection.Name)]
public sealed class IndicadoresFase12bTests(ApiFixture api)
{
    private sealed record Datos(int ClienteA, int ClienteB, int ClienteSinPedidos);

    private static readonly SemaphoreSlim Candado = new(1, 1);
    private static Datos? _datos;

    private async Task<Datos> DatosAsync()
    {
        await Candado.WaitAsync();
        try
        {
            return _datos ??= await SembrarAsync();
        }
        finally
        {
            Candado.Release();
        }
    }

    private async Task<Datos> SembrarAsync()
    {
        var a = await api.CrearClienteAsync();
        var b = await api.CrearClienteAsync();
        var sin = await api.CrearClienteAsync();
        var admin = await api.IdAdminAsync();

        await api.ConsultarAsync(async db =>
        {
            async Task<int> Solicitud(int cliente, string origen, string fecha, (int Producto, int Cantidad)[] items, string? cotizacion)
            {
                var solicitud = new Solicitud
                {
                    IdCliente = cliente, Origen = origen, FechaSolicitud = DateTime.Parse($"2021-{fecha}T10:00:00"), DescripcionGeneral = "Datos del dashboard",
                    Estado = cotizacion == "aprobada" ? "aceptado" : cotizacion == "rechazada" ? "rechazado" : "solicitado",
                    MotivoRechazo = cotizacion == "rechazada" ? "No se concretó en la prueba" : null,
                    IdEmpleadoRegistra = origen == "presencial" ? admin : null
                };
                foreach (var (producto, cantidad) in items)
                    solicitud.Detalles.Add(new SolicitudDetalle { IdProducto = producto, Descripcion = "", Cantidad = cantidad });
                if (cotizacion is not null)
                {
                    solicitud.Cotizaciones.Add(new Cotizacion
                    {
                        IdEmpleado = admin, FechaCotizacion = solicitud.FechaSolicitud, Estado = cotizacion, FechaRespuesta = DateTimeOffset.Parse($"2021-{fecha}T12:00:00-03:00"),
                        ViaRespuesta = "presencial", CostoManoObra = 1000
                    });
                }
                db.Solicitudes.Add(solicitud);
                await db.SaveChangesAsync();
                return solicitud.IdSolicitud;
            }

            async Task Pedido(int solicitud, string creado, string estimada, string? real, string estado, int baseTrabajo, int abonado)
            {
                // monto_abonado lo mantiene un trigger a partir de los pagos: el abono se registra como un pago real.
                var pedido = new Pedido
                {
                    IdSolicitud = solicitud, IdEmpleadoResponsable = admin, Origen = "directo", EstadoPedido = estado, MontoBaseTrabajo = baseTrabajo,
                    FechaPedido = DateTime.Parse($"2021-{creado}T10:00:00"), FechaEntregaEstimada = DateTime.Parse($"2021-{estimada}T00:00:00"),
                    FechaEntregaReal = real is null ? null : DateTime.Parse($"2021-{real}T00:00:00"),
                    MotivoAnulacion = estado == "anulado" ? "Anulado en la prueba" : null,
                    FechaAnulacion = estado == "anulado" ? DateTimeOffset.Parse("2021-03-06T10:00:00-03:00") : null
                };
                if (abonado > 0)
                {
                    pedido.Pagos.Add(new Pago
                    {
                        Monto = abonado, FechaPago = DateOnly.Parse($"2021-{creado}"), MedioPago = "transferencia", IdEmpleadoRegistra = admin, FechaRegistro = pedido.FechaPedido
                    });
                }
                db.Pedidos.Add(pedido);
                await db.SaveChangesAsync();
            }

            var s1 = await Solicitud(a, "online", "03-02", [(1, 1)], "aprobada");
            var s2 = await Solicitud(b, "online", "03-03", [(9, 1)], "aprobada");
            var s3 = await Solicitud(a, "presencial", "03-06", [(1, 1), (9, 3)], null);
            var s4 = await Solicitud(b, "presencial", "03-10", [(9, 1)], null);
            await Solicitud(a, "online", "03-05", [(1, 1)], null);
            await Solicitud(b, "online", "03-07", [(1, 1)], "rechazada");
            var s7 = await Solicitud(a, "presencial", "01-15", [(9, 1)], null);
            var s8 = await Solicitud(a, "online", "02-10", [(1, 1)], "aprobada");
            await Solicitud(b, "online", "02-11", [(9, 2)], "rechazada");

            await Pedido(s1, "03-04", "03-10", "03-09", "entregado", 100_000, 100_000);
            await Pedido(s2, "03-05", "03-20", null, "anulado", 50_000, 0);
            await Pedido(s3, "03-06", "03-12", "03-14", "entregado", 80_000, 0);
            await Pedido(s4, "03-10", "03-25", null, "en_proceso", 60_000, 20_000);
            await Pedido(s7, "01-15", "01-30", null, "terminado", 90_000, 0);
            await Pedido(s8, "02-12", "02-20", "02-20", "entregado", 120_000, 40_000);
            return 0;
        });
        return new Datos(a, b, sin);
    }

    // ── Utilidades ──

    private async Task<JsonElement> PedirAsync(string ruta, string consulta = "", HttpStatusCode esperado = HttpStatusCode.OK)
    {
        await DatosAsync();
        return await api.Cliente(await api.TokenAdminAsync()).GetAsync($"/api/v1/indicadores/{ruta}?{consulta}").EsperarAsync(esperado);
    }

    private const string Marzo = "anio=2021&mes=3";

    private static JsonElement Tarjeta(JsonElement resumen, string clave) =>
        resumen.GetProperty("tarjetas").EnumerateArray().Single(t => t.GetProperty("clave").GetString() == clave);

    private static decimal? Numero(JsonElement elemento, string campo) =>
        elemento.GetProperty(campo) is { ValueKind: not JsonValueKind.Null } v ? v.GetDecimal() : null;

    private static void Esperado(JsonElement tarjeta, decimal? valor, decimal? anterior, decimal? variacion)
    {
        Assert.Equal(valor, Numero(tarjeta, "valor"));
        Assert.Equal(anterior, Numero(tarjeta, "anterior"));
        Assert.Equal(variacion, Numero(tarjeta, "variacion"));
    }

    // ── Acceso ──

    [Fact]
    public async Task SoloGestionVeElDashboard_OtrosRolesRecibenRechazo()
    {
        await DatosAsync();
        await api.Cliente().GetAsync("/api/v1/indicadores/resumen").EsperarAsync(HttpStatusCode.Unauthorized);
        await api.Cliente((await api.RegistrarClienteAsync()).Token).GetAsync("/api/v1/indicadores/resumen").EsperarAsync(HttpStatusCode.Forbidden);
        await api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario)).GetAsync("/api/v1/indicadores/resumen").EsperarAsync(HttpStatusCode.Forbidden);
        await api.Cliente(await api.TokenEmpleadoAsync(Roles.Disenador)).GetAsync("/api/v1/indicadores/por-cobrar").EsperarAsync(HttpStatusCode.Forbidden);
        await api.Cliente(await api.TokenAdminAsync()).GetAsync("/api/v1/indicadores/resumen").EsperarAsync(HttpStatusCode.OK);
    }

    // ── S10 a S14, ticket y entregas a tiempo, con su comparacion ──

    [Fact]
    public async Task S10_LosPedidos_NoCuentanLosAnulados_YSeDesglosanPorEstado()
    {
        var resumen = await PedirAsync("resumen", Marzo);

        // Marzo: P1, P3 y P4 (P2 esta anulado). Febrero: P6. Variacion: (3 − 1) ÷ 1 × 100 = 200.
        var pedidos = Tarjeta(resumen, "pedidos");
        Esperado(pedidos, 3, 1, 200);
        var porEstado = pedidos.GetProperty("detalle").GetProperty("porEstado").EnumerateArray().ToDictionary(e => e.GetProperty("estado").GetString()!, e => e.GetProperty("cantidad").GetInt32());
        Assert.Equal(new Dictionary<string, int> { ["aceptado"] = 0, ["en_proceso"] = 1, ["terminado"] = 0, ["entregado"] = 2 }, porEstado);
        Assert.Equal(["aceptado", "en_proceso", "terminado", "entregado"], pedidos.GetProperty("detalle").GetProperty("porEstado").EnumerateArray().Select(e => e.GetProperty("estado").GetString()));
    }

    [Fact]
    public async Task S11_LosIngresos_SonLosPedidosEntregadosPorSuFechaDeEntregaReal()
    {
        var resumen = await PedirAsync("resumen", Marzo);

        // Marzo: P1 (100.000, entregado el 9) + P3 (80.000, el 14) = 180.000. Febrero: P6 = 120.000. (180 − 120) ÷ 120 = +50 %.
        Esperado(Tarjeta(resumen, "ingresos"), 180_000, 120_000, 50);
        Assert.Equal("pesos", Tarjeta(resumen, "ingresos").GetProperty("unidad").GetString());
    }

    [Fact]
    public async Task S12_ComparaLasSolicitudesEnLineaConLasPresenciales()
    {
        var resumen = await PedirAsync("resumen", Marzo);

        // Marzo, en linea: S1, S2, S5 y S6 = 4; presenciales: S3 y S4 = 2 (S7 es de enero). 4 ÷ 6 = 66,7 %. Febrero en linea: S8 y S9 = 2.
        var origen = Tarjeta(resumen, "origen");
        Esperado(origen, 4, 2, 100);
        Assert.Equal(2, origen.GetProperty("detalle").GetProperty("presenciales").GetInt32());
        Assert.Equal(66.7m, origen.GetProperty("detalle").GetProperty("porcentajeEnLinea").GetDecimal());
    }

    [Fact]
    public async Task S13_LasConcretadas_NoCuentanLasSolicitudesCuyoPedidoFueAnulado()
    {
        var resumen = await PedirAsync("resumen", Marzo);

        // S1 tiene el pedido P1; S2 tiene P2, pero esta ANULADO: no cuenta. Marzo = 1. Febrero: S8 con P6 = 1.
        var concretadas = Tarjeta(resumen, "concretadas");
        Esperado(concretadas, 1, 1, 0);
        Assert.Equal(4, concretadas.GetProperty("detalle").GetProperty("solicitudesEnLinea").GetInt32());
    }

    [Fact]
    public async Task S14_LaConversion_VariaEnPuntosPorcentuales()
    {
        var resumen = await PedirAsync("resumen", Marzo);

        // Marzo: 1 ÷ 4 × 100 = 25,0. Febrero: 1 ÷ 2 × 100 = 50,0. Diferencia: 25 − 50 = −25 pp (no −50 %).
        var conversion = Tarjeta(resumen, "conversion");
        Esperado(conversion, 25, 50, -25);
        Assert.Equal("puntos", conversion.GetProperty("tipoVariacion").GetString());
        Assert.Equal("porcentaje", Tarjeta(resumen, "ingresos").GetProperty("tipoVariacion").GetString());
    }

    [Fact]
    public async Task ElTicketPromedio_SonLosIngresosSobreLosPedidosEntregados()
    {
        var resumen = await PedirAsync("resumen", Marzo);

        // Marzo: 180.000 ÷ 2 entregados = 90.000. Febrero: 120.000 ÷ 1 = 120.000. (90 − 120) ÷ 120 = −25 %.
        Esperado(Tarjeta(resumen, "ticketPromedio"), 90_000, 120_000, -25);
    }

    [Fact]
    public async Task LasEntregasATiempo_ComparanLaFechaRealConLaEstimada_EnPuntos()
    {
        var resumen = await PedirAsync("resumen", Marzo);

        // Marzo: P1 (real 9 ≤ estimada 10) a tiempo; P3 (real 14 > estimada 12) atrasado → 1 de 2 = 50 %.
        // Febrero: P6, real igual a estimada (20 ≤ 20) = a tiempo → 100 %. Diferencia: −50 pp.
        Esperado(Tarjeta(resumen, "entregasATiempo"), 50, 100, -50);
    }

    [Fact]
    public async Task S24_ElSaldoPorCobrar_EsElDelPeriodo_ConSuComparacion()
    {
        var resumen = await PedirAsync("resumen", Marzo);

        // Pedidos de marzo no anulados sin pagar del todo: P3 (80.000) y P4 (40.000, abonado) = 120.000 en 2 pedidos.
        // P1 esta pagado y P2 anulado. Febrero: P6 = 80.000. (120 − 80) ÷ 80 = +50 %.
        var saldo = resumen.GetProperty("saldo");
        Assert.Equal("periodo", saldo.GetProperty("alcance").GetString());
        Assert.Equal((120_000m, 2, 80_000m, 50m), (saldo.GetProperty("valor").GetDecimal(), saldo.GetProperty("cantidad").GetInt32(), Numero(saldo, "anterior"), Numero(saldo, "variacion")));
    }

    [Fact]
    public async Task ElSaldoVigente_IgnoraLasFechas_YIncluyeLaDeudaAntigua()
    {
        var d = await DatosAsync();

        var resumen = await PedirAsync("resumen", $"{Marzo}&alcanceSaldo=vigente&cliente={d.ClienteA}");

        // Cliente A: P3 (80.000) + P5 (90.000, de enero) + P6 (80.000, de febrero) = 250.000 en 3 pedidos. Sin comparacion.
        var saldo = resumen.GetProperty("saldo");
        Assert.Equal("vigente", saldo.GetProperty("alcance").GetString());
        Assert.Equal((250_000m, 3), (saldo.GetProperty("valor").GetDecimal(), saldo.GetProperty("cantidad").GetInt32()));
        Assert.Null(Numero(saldo, "anterior"));
        Assert.Null(Numero(saldo, "variacion"));
        // Las demas tarjetas siguen siendo del periodo (cliente A en marzo: P1 y P3).
        Assert.Equal(2, Numero(Tarjeta(resumen, "pedidos"), "valor"));
    }

    [Fact]
    public async Task ElPeriodoYLaComparacion_SeInforman_ConSusFechas()
    {
        var resumen = await PedirAsync("resumen", Marzo);

        Assert.Equal(("mes", "2021-03-01", "2021-03-31", "marzo de 2021"),
            (resumen.GetProperty("periodo").GetProperty("tipo").GetString(), resumen.GetProperty("periodo").GetProperty("desde").GetString(),
             resumen.GetProperty("periodo").GetProperty("hasta").GetString(), resumen.GetProperty("periodo").GetProperty("etiqueta").GetString()));
        Assert.Equal(("2021-02-01", "2021-02-28", "febrero de 2021"),
            (resumen.GetProperty("comparacion").GetProperty("desde").GetString(), resumen.GetProperty("comparacion").GetProperty("hasta").GetString(),
             resumen.GetProperty("comparacion").GetProperty("etiqueta").GetString()));
        Assert.Equal(["ingresos", "pedidos", "origen", "concretadas", "conversion", "ticketPromedio", "entregasATiempo"],
            resumen.GetProperty("tarjetas").EnumerateArray().Select(t => t.GetProperty("clave").GetString()));
    }

    [Fact]
    public async Task SinComparacion_NoHayValorAnteriorNiVariacion()
    {
        var resumen = await PedirAsync("resumen", $"{Marzo}&comparar=ninguno");

        Assert.Equal(JsonValueKind.Null, resumen.GetProperty("comparacion").ValueKind);
        Esperado(Tarjeta(resumen, "pedidos"), 3, null, null);
        Esperado(Tarjeta(resumen, "conversion"), 25, null, null);
    }

    // ── Division por cero, clientes sin pedidos, periodos vacios ──

    [Fact]
    public async Task UnPeriodoSinMovimiento_DaCeroONulo_NuncaUnaDivisionPorCero()
    {
        // Julio de 2021 y junio (su comparacion): sin solicitudes ni pedidos.
        var resumen = await PedirAsync("resumen", "anio=2021&mes=7");

        Esperado(Tarjeta(resumen, "pedidos"), 0, 0, null);                      // 0 ÷ 0: sin base, variacion nula
        Esperado(Tarjeta(resumen, "ingresos"), 0, 0, null);
        Esperado(Tarjeta(resumen, "conversion"), null, null, null);             // sin solicitudes en linea
        Esperado(Tarjeta(resumen, "ticketPromedio"), null, null, null);         // sin entregas
        Esperado(Tarjeta(resumen, "entregasATiempo"), null, null, null);
        Assert.Equal(0, resumen.GetProperty("saldo").GetProperty("valor").GetDecimal());
    }

    [Fact]
    public async Task UnClienteSinPedidos_TieneTodoEnCero()
    {
        var d = await DatosAsync();

        var resumen = await PedirAsync("resumen", $"{Marzo}&cliente={d.ClienteSinPedidos}&alcanceSaldo=vigente");
        var cobranza = await PedirAsync("cobranza", $"{Marzo}&cliente={d.ClienteSinPedidos}");
        var porCobrar = await PedirAsync("por-cobrar", $"{Marzo}&cliente={d.ClienteSinPedidos}");

        Assert.Equal(0, Numero(Tarjeta(resumen, "pedidos"), "valor"));
        Assert.Equal(0, resumen.GetProperty("saldo").GetProperty("valor").GetDecimal());
        Assert.All(cobranza.GetProperty("segmentos").EnumerateArray(), s => Assert.Equal(0, s.GetProperty("cantidad").GetInt32()));
        Assert.Equal((0, 0, 0m), (porCobrar.GetProperty("total").GetInt32(), porCobrar.GetProperty("elementos").GetArrayLength(), porCobrar.GetProperty("totalSaldo").GetDecimal()));
    }

    // ── Filtros combinables ──

    [Fact]
    public async Task ElFiltroPorCliente_CambiaLasCifras()
    {
        var d = await DatosAsync();

        var resumen = await PedirAsync("resumen", $"{Marzo}&cliente={d.ClienteA}");

        // Cliente A en marzo: pedidos P1 y P3 = 2; ingresos 180.000; en linea S1 y S5 = 2, presenciales S3 = 1; concretadas S1 = 1 → 50 %.
        Esperado(Tarjeta(resumen, "pedidos"), 2, 1, 100);
        Assert.Equal(180_000, Numero(Tarjeta(resumen, "ingresos"), "valor"));
        Assert.Equal(2, Numero(Tarjeta(resumen, "origen"), "valor"));
        Assert.Equal(1, Tarjeta(resumen, "origen").GetProperty("detalle").GetProperty("presenciales").GetInt32());
        Assert.Equal(50, Numero(Tarjeta(resumen, "conversion"), "valor"));
    }

    [Fact]
    public async Task ElFiltroPorOrigen_PresencialNoTieneConversion()
    {
        var resumen = await PedirAsync("resumen", $"{Marzo}&origen=presencial");

        // Presenciales de marzo: P3 y P4 (2 pedidos), y ninguna solicitud en linea que convertir.
        Assert.Equal(2, Numero(Tarjeta(resumen, "pedidos"), "valor"));
        Assert.Null(Numero(Tarjeta(resumen, "conversion"), "valor"));
        Assert.Equal(0, Numero(Tarjeta(resumen, "origen"), "valor"));
    }

    [Fact]
    public async Task ElFiltroPorEstadoDePago_PorCobrar_DejaSoloLoPendiente()
    {
        var resumen = await PedirAsync("resumen", $"{Marzo}&estadoPago=por_cobrar");

        // Marzo sin pagar: P3 (entregado) y P4 (en proceso): 2 pedidos; los ingresos son solo los de P3 (80.000).
        Assert.Equal(2, Numero(Tarjeta(resumen, "pedidos"), "valor"));
        Assert.Equal(80_000, Numero(Tarjeta(resumen, "ingresos"), "valor"));
    }

    [Fact]
    public async Task ElFiltroPorEstadoDelPedido_AdmiteVarios_YNuncaTrae_Anulados()
    {
        var uno = await PedirAsync("resumen", $"{Marzo}&estadoPedido=en_proceso");
        var dos = await PedirAsync("resumen", $"{Marzo}&estadoPedido=en_proceso,entregado");

        Assert.Equal(1, Numero(Tarjeta(uno, "pedidos"), "valor"));
        Assert.Equal(0, Numero(Tarjeta(uno, "ingresos"), "valor"));
        Assert.Null(Numero(Tarjeta(uno, "ticketPromedio"), "valor"));
        Assert.Equal(3, Numero(Tarjeta(dos, "pedidos"), "valor"));
        await PedirAsync("resumen", $"{Marzo}&estadoPedido=anulado", esperado: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ElFiltroPorCategoria_ElPedidoEntraSiAlgunoDeSusItemsEsDeEsaCategoria()
    {
        var resumen = await PedirAsync("resumen", $"{Marzo}&categoria=2");

        // Categoria 2: P3 (tiene 3 items del producto 9) y P4 → 2 pedidos. Solicitudes en linea cuyo producto principal es de la
        // categoria 2: solo S2 (cuyo pedido esta anulado) → 1 solicitud y 0 concretadas = 0 %.
        Assert.Equal(2, Numero(Tarjeta(resumen, "pedidos"), "valor"));
        Assert.Equal(1, Numero(Tarjeta(resumen, "origen"), "valor"));
        Assert.Equal(0, Numero(Tarjeta(resumen, "conversion"), "valor"));
    }

    [Fact]
    public async Task ElAccesoPorAnioYMes_YElRangoPropio_CambianElPeriodo()
    {
        var enero = await PedirAsync("resumen", "anio=2021&mes=1&comparar=ninguno");
        var rango = await PedirAsync("resumen", "desde=2021-03-01&hasta=2021-03-06&comparar=ninguno");

        // Enero: P5 se creo el 15 (terminado, sin entregar). Rango del 1 al 6 de marzo: P1 (4) y P3 (6) creados; P4 es del 10.
        Assert.Equal(1, Numero(Tarjeta(enero, "pedidos"), "valor"));
        Assert.Equal(90_000, enero.GetProperty("saldo").GetProperty("valor").GetDecimal());
        Assert.Equal(2, Numero(Tarjeta(rango, "pedidos"), "valor"));
        Assert.Equal("rango", rango.GetProperty("periodo").GetProperty("tipo").GetString());
    }

    [Fact]
    public async Task UnFiltroInvalido_Responde400_ConElErrorPorCampo()
    {
        var rechazo = await PedirAsync("resumen", "periodo=decada&origen=web&categoria=0", HttpStatusCode.BadRequest);

        var errores = rechazo.GetProperty("errores");
        foreach (var campo in new[] { "periodo", "origen", "categoria" })
            Assert.True(errores.TryGetProperty(campo, out _), $"Falta el error de {campo}: {errores}");
        await PedirAsync("por-cobrar", "tamano=101", HttpStatusCode.BadRequest);
        await PedirAsync("evolucion", "desde=2021-03-10&hasta=2021-03-01", HttpStatusCode.BadRequest);
    }

    // ── Evolucion, cobranza, embudo, demanda y por cobrar ──

    [Fact]
    public async Task LaEvolucion_AgrupaPorSemana_YAlineaLaComparacionPorPosicion()
    {
        var evolucion = await PedirAsync("evolucion", Marzo);

        Assert.Equal("semana", evolucion.GetProperty("granularidad").GetString());
        var serie = evolucion.GetProperty("serie").EnumerateArray().ToList();
        Assert.Equal(5, serie.Count);
        Assert.Equal(["01-03", "08-03", "15-03", "22-03", "29-03"], serie.Select(t => t.GetProperty("etiqueta").GetString()));
        // Pedidos creados: P1 (4) y P3 (6) en la 1.ª semana, P4 (10) en la 2.ª. Ingresos: P1 (9) y P3 (14) en la 2.ª.
        Assert.Equal([2, 1, 0, 0, 0], serie.Select(t => t.GetProperty("pedidos").GetInt32()));
        Assert.Equal([0m, 180_000m, 0m, 0m, 0m], serie.Select(t => t.GetProperty("ingresos").GetDecimal()));
        // Febrero (4 semanas): P6 creado el 12 (2.ª semana) y entregado el 20 (3.ª). La 5.ª semana de marzo no tiene par.
        Assert.Equal([0, 1, 0, 0], serie.Take(4).Select(t => t.GetProperty("pedidosComparacion").GetInt32()));
        Assert.Equal([0m, 0m, 120_000m, 0m], serie.Take(4).Select(t => t.GetProperty("ingresosComparacion").GetDecimal()));
        Assert.Equal(JsonValueKind.Null, serie[4].GetProperty("pedidosComparacion").ValueKind);
        Assert.Equal(JsonValueKind.Null, serie[4].GetProperty("ingresosComparacion").ValueKind);
    }

    [Fact]
    public async Task LaEvolucionDelAnio_AgrupaPorMes()
    {
        var evolucion = await PedirAsync("evolucion", "anio=2021&comparar=ninguno");

        Assert.Equal("mes", evolucion.GetProperty("granularidad").GetString());
        var serie = evolucion.GetProperty("serie").EnumerateArray().ToList();
        Assert.Equal(12, serie.Count);
        // Pedidos creados por mes: enero P5 (1), febrero P6 (1), marzo P1, P3 y P4 (3). Ingresos: febrero 120.000, marzo 180.000.
        Assert.Equal([1, 1, 3], serie.Take(3).Select(t => t.GetProperty("pedidos").GetInt32()));
        Assert.Equal([0m, 120_000m, 180_000m], serie.Take(3).Select(t => t.GetProperty("ingresos").GetDecimal()));
    }

    [Fact]
    public async Task LaCobranza_DaLosTresEstadosDePago_ConSuSaldo()
    {
        var cobranza = await PedirAsync("cobranza", Marzo);

        // Marzo no anulado: P1 pagado_total, P3 no_pagado (80.000) y P4 abonado (saldo 40.000).
        var segmentos = cobranza.GetProperty("segmentos").EnumerateArray().ToList();
        Assert.Equal(["no_pagado", "abonado", "pagado_total"], segmentos.Select(s => s.GetProperty("estadoPago").GetString()));
        Assert.Equal([(1, 80_000m), (1, 40_000m), (1, 0m)], segmentos.Select(s => (s.GetProperty("cantidad").GetInt32(), s.GetProperty("saldo").GetDecimal())));
        Assert.Equal((120_000m, 2, "periodo"), (cobranza.GetProperty("saldoTotal").GetDecimal(), cobranza.GetProperty("cantidadPorCobrar").GetInt32(), cobranza.GetProperty("alcance").GetString()));
    }

    [Fact]
    public async Task LaCobranzaVigente_SumaTodaLaDeudaDelCliente()
    {
        var d = await DatosAsync();

        var cobranza = await PedirAsync("cobranza", $"{Marzo}&alcanceSaldo=vigente&cliente={d.ClienteA}");

        // Cliente A: no_pagado = P3 y P5 (170.000), abonado = P6 (80.000), pagado_total = P1.
        var segmentos = cobranza.GetProperty("segmentos").EnumerateArray().ToList();
        Assert.Equal([(2, 170_000m), (1, 80_000m), (1, 0m)], segmentos.Select(s => (s.GetProperty("cantidad").GetInt32(), s.GetProperty("saldo").GetDecimal())));
        Assert.Equal((250_000m, 3, "vigente"), (cobranza.GetProperty("saldoTotal").GetDecimal(), cobranza.GetProperty("cantidadPorCobrar").GetInt32(), cobranza.GetProperty("alcance").GetString()));
    }

    [Fact]
    public async Task ElEmbudo_VaDeLasSolicitudesEnLineaALosPedidosConcretados()
    {
        var embudo = await PedirAsync("embudo", Marzo);

        // En linea de marzo: S1, S2, S5, S6 = 4. Cotizadas: S1, S2, S6 = 3. Aprobadas: S1 y S2 = 2. Concretadas (pedido no anulado): S1 = 1.
        var pasos = embudo.GetProperty("pasos").EnumerateArray().ToList();
        Assert.Equal(["solicitudes", "cotizadas", "aprobadas", "concretadas"], pasos.Select(p => p.GetProperty("clave").GetString()));
        Assert.Equal([4, 3, 2, 1], pasos.Select(p => p.GetProperty("cantidad").GetInt32()));
        Assert.Equal([100m, 75m, 50m, 25m], pasos.Select(p => p.GetProperty("porcentaje").GetDecimal()));
        Assert.Equal([null, 75m, 66.7m, 50m], pasos.Select(p => Numero(p, "porcentajePaso")));
    }

    [Fact]
    public async Task ElEmbudoPresencial_EstaVacio_YElDeUnPeriodoSinSolicitudes_NoDivideEntreCero()
    {
        var presencial = await PedirAsync("embudo", $"{Marzo}&origen=presencial");
        var vacio = await PedirAsync("embudo", "anio=2021&mes=7");

        Assert.All(presencial.GetProperty("pasos").EnumerateArray(), p => Assert.Equal(0, p.GetProperty("cantidad").GetInt32()));
        Assert.All(vacio.GetProperty("pasos").EnumerateArray(), p => Assert.Null(Numero(p, "porcentaje")));
    }

    [Fact]
    public async Task LaDemanda_RepartePorCategoriaElMontoDeLosEntregados_SegunLaCantidadDeCadaItem()
    {
        var demanda = await PedirAsync("demanda", Marzo);

        // Entregados de marzo: P1 (100.000, 1 item de la categoria 1) y P3 (80.000: 1 item de la categoria 1 y 3 de la 2 →
        // 20.000 y 60.000). Categoria 1: 120.000 en 2 unidades y 2 pedidos. Categoria 2: 60.000 en 3 unidades y 1 pedido.
        // La suma (180.000) es igual a los ingresos.
        var categorias = demanda.GetProperty("categorias").EnumerateArray().ToList();
        Assert.Equal(["Impresión y publicidad gráfica", "Letreros y estructuras"], categorias.Select(c => c.GetProperty("nombre").GetString()));
        Assert.Equal([(120_000m, 2, 2), (60_000m, 3, 1)], categorias.Select(c => (c.GetProperty("monto").GetDecimal(), c.GetProperty("cantidad").GetInt32(), c.GetProperty("pedidos").GetInt32())));
        Assert.Equal([1, 2], categorias.Select(c => c.GetProperty("idCategoria").GetInt32()));
        Assert.Equal(180_000m, categorias.Sum(c => c.GetProperty("monto").GetDecimal()));
    }

    [Fact]
    public async Task LaDemandaPorTipoDeLetrero_AgrupaPorProducto()
    {
        var demanda = await PedirAsync("demanda", $"{Marzo}&agrupar=producto");

        var filas = demanda.GetProperty("categorias").EnumerateArray().ToList();
        Assert.Equal("producto", demanda.GetProperty("agrupar").GetString());
        Assert.Equal([1, 9], filas.Select(f => f.GetProperty("idProducto").GetInt32()));
        Assert.Equal([120_000m, 60_000m], filas.Select(f => f.GetProperty("monto").GetDecimal()));
        Assert.All(filas, f => Assert.False(f.TryGetProperty("idCategoria", out _)));
    }

    [Fact]
    public async Task LaDemanda_ConFiltroDeCategoria_MuestraSoloEsa()
    {
        var demanda = await PedirAsync("demanda", $"{Marzo}&categoria=2");

        // Con categoria 2 solo entra P3 (tiene items de ella) y solo se muestran los items de la categoria 2: 60.000.
        var fila = Assert.Single(demanda.GetProperty("categorias").EnumerateArray());
        Assert.Equal((2, 60_000m), (fila.GetProperty("idCategoria").GetInt32(), fila.GetProperty("monto").GetDecimal()));
    }

    [Fact]
    public async Task PedidosPorCobrar_VanDeMayorAMenorSaldo_ConElTotalDeTodasLasPaginas()
    {
        var porCobrar = await PedirAsync("por-cobrar", Marzo);

        // Marzo: P3 (80.000, entregado y sin ningun pago) y P4 (40.000, abonado).
        var elementos = porCobrar.GetProperty("elementos").EnumerateArray().ToList();
        Assert.Equal([80_000, 40_000], elementos.Select(e => e.GetProperty("saldoPendiente").GetInt32()));
        Assert.Equal([true, false], elementos.Select(e => e.GetProperty("listoSinPagar").GetBoolean()));
        Assert.Equal(["entregado", "en_proceso"], elementos.Select(e => e.GetProperty("estadoPedido").GetString()));
        Assert.Equal((2, 120_000m, 1, 1), (porCobrar.GetProperty("total").GetInt32(), porCobrar.GetProperty("totalSaldo").GetDecimal(), porCobrar.GetProperty("pagina").GetInt32(), porCobrar.GetProperty("totalPaginas").GetInt32()));
        // Los datos son de 2021 (pasado): los dias desde el pedido son negativos, y el cliente viene con su nombre.
        Assert.All(elementos, e => Assert.True(e.GetProperty("diasDesdePedido").GetInt32() > 0));
        Assert.All(elementos, e => Assert.Equal("Cli Local", e.GetProperty("cliente").GetProperty("nombre").GetString()));
    }

    [Fact]
    public async Task PedidosPorCobrarVigentes_SePaginan_ConElMasAntiguoPrimeroAIgualSaldo()
    {
        var d = await DatosAsync();
        var consulta = $"{Marzo}&alcanceSaldo=vigente&cliente={d.ClienteA}&tamano=2";

        var primera = await PedirAsync("por-cobrar", consulta);
        var segunda = await PedirAsync("por-cobrar", consulta + "&pagina=2");
        var fuera = await PedirAsync("por-cobrar", consulta + "&pagina=9");

        // Cliente A: P5 (90.000), luego P6 y P3 con 80.000 cada uno (el de febrero antes que el de marzo).
        Assert.Equal([90_000, 80_000], primera.GetProperty("elementos").EnumerateArray().Select(e => e.GetProperty("saldoPendiente").GetInt32()));
        Assert.Equal([true, false], primera.GetProperty("elementos").EnumerateArray().Select(e => e.GetProperty("listoSinPagar").GetBoolean()));   // P5 terminado sin pago; P6 abonado
        Assert.Equal(["2021-01-15", "2021-02-12"], primera.GetProperty("elementos").EnumerateArray().Select(e => e.GetProperty("fechaPedido").GetString()));
        var ultima = Assert.Single(segunda.GetProperty("elementos").EnumerateArray());
        Assert.Equal("2021-03-06", ultima.GetProperty("fechaPedido").GetString());
        Assert.Equal((3, 250_000m, 2, "vigente"), (primera.GetProperty("total").GetInt32(), primera.GetProperty("totalSaldo").GetDecimal(), primera.GetProperty("totalPaginas").GetInt32(), primera.GetProperty("alcance").GetString()));
        // Una pagina fuera de rango devuelve la ultima.
        Assert.Equal(2, fuera.GetProperty("pagina").GetInt32());
        Assert.Equal("2021-03-06", Assert.Single(fuera.GetProperty("elementos").EnumerateArray()).GetProperty("fechaPedido").GetString());
    }

    [Fact]
    public async Task ElSegmentoDelAnillo_FiltraLaTabla()
    {
        var abonados = await PedirAsync("por-cobrar", $"{Marzo}&estadoPago=abonado");

        var fila = Assert.Single(abonados.GetProperty("elementos").EnumerateArray());
        Assert.Equal("abonado", fila.GetProperty("estadoPago").GetString());
        Assert.Equal(40_000m, abonados.GetProperty("totalSaldo").GetDecimal());
    }

    [Fact]
    public async Task LasConsultas_SonAgregadasEnSql_SinTraerLasFilas()
    {
        // RNFP26: los pedidos y las solicitudes se suman en la base. Las vistas existen y se consultan por GROUP BY.
        var (vistas, filasVista) = await api.ConsultarAsync(async db => (
            await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sys.views WHERE name LIKE 'vw_indicadores_%' ORDER BY name").ToListAsync(),
            await db.IndicadoresPedidos.CountAsync(p => p.IdPedido > 0)));

        Assert.Equal(["vw_indicadores_items", "vw_indicadores_pedidos", "vw_indicadores_solicitudes"], vistas);
        Assert.True(filasVista >= 6);
    }
}
