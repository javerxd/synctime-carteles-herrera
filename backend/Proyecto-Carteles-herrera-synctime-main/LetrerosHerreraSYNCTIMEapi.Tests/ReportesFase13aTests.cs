using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 13A (RFS10, RFS12, RFU24 a RFU26, RFU28, RFU36, RNFP18, E16, E20, S18, S20, S24): los tres reportes y sus exportaciones contra
// SQL Server, con datos CONOCIDOS y cada cifra esperada calculada a mano. Los datos viven en marzo y febrero de 2022 (ninguna otra
// prueba usa ese año). El proceso en segundo plano esta apagado en las pruebas: cada una lo ejecuta cuando lo necesita.
//
//   Periodo: marzo de 2022.            Comparacion: febrero de 2022.            Clientes: A, B y C (sin pedidos).
//   Productos: 1 (categoria 1) y 9 (categoria 2).
//
//   Solicitud            origen      fecha       items             cotizacion  pedido
//   S1  cliente A        online      03-02       1×(1)             aprobada    P1
//   S2  cliente B        online      03-03       1×(9)             aprobada    P2 (ANULADO)
//   S3  cliente A        presencial  03-06       1×(1) + 3×(9)     —           P3 (directo, SIN costo)
//   S4  cliente B        presencial  03-10       1×(9)             aprobada    P4
//   S5  cliente A        online      03-05       1×(1)             —           —
//   S6  cliente B        online      03-07       1×(1)             rechazada   —
//   S7  cliente A        presencial  01-15       1×(9)             —           P5 (directo, SIN costo)
//   S8  cliente A        online      02-10       1×(1)             aprobada    P6
//
//   Pedido  creado  estimada  real   estado      base     abonado  saldo   pago          costo de la cotizacion
//   P1      03-04   03-10     03-09  entregado   100.000  100.000  0       pagado_total  20.000 mat + 30.000 mo + 5.000 otros = 55.000
//   P2      03-05   03-20     —      anulado      50.000        0  50.000  no_pagado     (anulado: nunca suma)
//   P3      03-06   03-12     03-14  entregado    80.000        0  80.000  no_pagado     —  (sin cotizacion: «Sin costo»)
//   P4      03-10   03-25     —      en_proceso   60.000   20.000  40.000  abonado       10.000
//   P5      01-15   01-30     —      terminado    90.000        0  90.000  no_pagado     —
//   P6      02-12   02-20     02-20  entregado   120.000   40.000  80.000  abonado       7.501,50 mat (→ 7.502) + 40.000 mo = 47.502
//
//   Marzo: ingresos = P1 + P3 = 180.000 (de los cuales 100.000 tienen costo); costos = 55.000 (materiales 20.000); margen = 100.000 − 20.000 (materiales) − 5.000 (otros) = 75.000 (75 %).
//   Febrero: ingresos 120.000; costos 47.502 (materiales 7.502, sin otros costos); margen 112.498 (sin otros costos no cambia).
[Collection(ApiCollection.Name)]
public sealed class ReportesFase13aTests(ApiFixture api)
{
    private sealed record Datos(int ClienteA, int ClienteB, int ClienteSinPedidos, IReadOnlyDictionary<string, int> Pedidos);

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

    // Una solicitud con su cotizacion (si la lleva) y, aparte, su pedido: la misma forma que usa la API al aprobar una cotizacion.
    private async Task<(int Solicitud, int? Cotizacion)> NuevaSolicitudAsync(LetrerosHerreradbContext db, int admin, int cliente, string origen, string fecha,
        (int Producto, int Cantidad)[] items, string? cotizacion, decimal materialesCantidad = 0, decimal materialesCosto = 0, decimal manoObra = 1000, decimal otros = 0, int anio = 2022)
    {
        var solicitud = new Solicitud
        {
            IdCliente = cliente, Origen = origen, FechaSolicitud = DateTime.Parse($"{anio}-{fecha}T10:00:00"), DescripcionGeneral = "Datos de los reportes",
            Estado = cotizacion == "aprobada" ? "aceptado" : cotizacion == "rechazada" ? "rechazado" : "solicitado",
            MotivoRechazo = cotizacion == "rechazada" ? "No se concretó en la prueba" : null,
            IdEmpleadoRegistra = origen == "presencial" ? admin : null
        };
        foreach (var (producto, cantidad) in items)
            solicitud.Detalles.Add(new SolicitudDetalle { IdProducto = producto, Descripcion = "", Cantidad = cantidad });
        Cotizacion? cot = null;
        if (cotizacion is not null)
        {
            cot = new Cotizacion
            {
                IdEmpleado = admin, FechaCotizacion = solicitud.FechaSolicitud, Estado = cotizacion, FechaRespuesta = DateTimeOffset.Parse($"{anio}-{fecha}T12:00:00-03:00"),
                ViaRespuesta = "presencial", CostoManoObra = manoObra, OtrosCostos = otros
            };
            if (materialesCantidad > 0)
                cot.Materiales.Add(new CotizacionMaterial { IdMaterial = 1, MedidaAplicada = "2 x 1", Cantidad = materialesCantidad, CostoUnitario = materialesCosto });
            solicitud.Cotizaciones.Add(cot);
        }
        db.Solicitudes.Add(solicitud);
        await db.SaveChangesAsync();
        return (solicitud.IdSolicitud, cot?.IdCotizacion);
    }

    private static async Task<int> NuevoPedidoAsync(LetrerosHerreradbContext db, int admin, (int Solicitud, int? Cotizacion) origen, string creado, string estimada, string? real,
        string estado, int baseTrabajo, int abonado, int anio = 2022)
    {
        // monto_abonado lo mantiene un trigger a partir de los pagos: el abono se registra como un pago real.
        var pedido = new Pedido
        {
            IdSolicitud = origen.Solicitud, IdCotizacion = origen.Cotizacion, IdEmpleadoResponsable = admin, Origen = origen.Cotizacion is null ? "directo" : "cotizacion",
            EstadoPedido = estado, MontoBaseTrabajo = baseTrabajo,
            FechaPedido = DateTime.Parse($"{anio}-{creado}T10:00:00"), FechaEntregaEstimada = DateTime.Parse($"{anio}-{estimada}T00:00:00"),
            FechaEntregaReal = real is null ? null : DateTime.Parse($"{anio}-{real}T00:00:00"),
            MotivoAnulacion = estado == "anulado" ? "Anulado en la prueba" : null,
            FechaAnulacion = estado == "anulado" ? DateTimeOffset.Parse($"{anio}-03-06T10:00:00-03:00") : null
        };
        if (abonado > 0)
        {
            pedido.Pagos.Add(new Pago
            {
                Monto = abonado, FechaPago = DateOnly.Parse($"{anio}-{creado}"), MedioPago = "transferencia", IdEmpleadoRegistra = admin, FechaRegistro = pedido.FechaPedido
            });
        }
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();
        return pedido.IdPedido;
    }

    private async Task<Datos> SembrarAsync()
    {
        var a = await api.CrearClienteAsync();
        var b = await api.CrearClienteAsync();
        var c = await api.CrearClienteAsync();
        var admin = await api.IdAdminAsync();
        var pedidos = new Dictionary<string, int>();

        await api.ConsultarAsync(async db =>
        {
            var s1 = await NuevaSolicitudAsync(db, admin, a, "online", "03-02", [(1, 1)], "aprobada", materialesCantidad: 2, materialesCosto: 10_000, manoObra: 30_000, otros: 5_000);
            var s2 = await NuevaSolicitudAsync(db, admin, b, "online", "03-03", [(9, 1)], "aprobada", manoObra: 10_000);
            var s3 = await NuevaSolicitudAsync(db, admin, a, "presencial", "03-06", [(1, 1), (9, 3)], null);
            var s4 = await NuevaSolicitudAsync(db, admin, b, "presencial", "03-10", [(9, 1)], "aprobada", manoObra: 10_000);
            await NuevaSolicitudAsync(db, admin, a, "online", "03-05", [(1, 1)], null);
            await NuevaSolicitudAsync(db, admin, b, "online", "03-07", [(1, 1)], "rechazada");
            var s7 = await NuevaSolicitudAsync(db, admin, a, "presencial", "01-15", [(9, 1)], null);
            var s8 = await NuevaSolicitudAsync(db, admin, a, "online", "02-10", [(1, 1)], "aprobada", materialesCantidad: 1.5m, materialesCosto: 5_001, manoObra: 40_000);

            pedidos["P1"] = await NuevoPedidoAsync(db, admin, s1, "03-04", "03-10", "03-09", "entregado", 100_000, 100_000);
            pedidos["P2"] = await NuevoPedidoAsync(db, admin, s2, "03-05", "03-20", null, "anulado", 50_000, 0);
            pedidos["P3"] = await NuevoPedidoAsync(db, admin, s3, "03-06", "03-12", "03-14", "entregado", 80_000, 0);
            pedidos["P4"] = await NuevoPedidoAsync(db, admin, s4, "03-10", "03-25", null, "en_proceso", 60_000, 20_000);
            pedidos["P5"] = await NuevoPedidoAsync(db, admin, s7, "01-15", "01-30", null, "terminado", 90_000, 0);
            pedidos["P6"] = await NuevoPedidoAsync(db, admin, s8, "02-12", "02-20", "02-20", "entregado", 120_000, 40_000);
            return 0;
        });
        return new Datos(a, b, c, pedidos);
    }

    // ── Utilidades ──

    private const string Marzo = "anio=2022&mes=3";

    private async Task<HttpClient> ComoAsync(string rol) => api.Cliente(await api.TokenEmpleadoAsync(rol));

    private async Task<JsonElement> ReporteAsync(string reporte, string consulta = Marzo, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        await DatosAsync();
        return await api.Cliente(await api.TokenAdminAsync()).GetAsync($"/api/v1/reportes/{reporte}?{consulta}").EsperarAsync(esperado);
    }

    private static JsonElement Tabla(JsonElement reporte, string clave) =>
        reporte.GetProperty("tablas").EnumerateArray().Single(t => t.GetProperty("clave").GetString() == clave);

    private static List<JsonElement[]> Filas(JsonElement tabla) =>
        tabla.GetProperty("filas").EnumerateArray().Select(f => f.EnumerateArray().ToArray()).ToList();

    private static decimal?[] Totales(JsonElement tabla) =>
        tabla.GetProperty("totales").EnumerateArray().Select(t => t.ValueKind == JsonValueKind.Null ? (decimal?)null : t.GetDecimal()).ToArray();

    private static List<int> IdsDePedidos(JsonElement tabla) => Filas(tabla).Select(f => f[0].GetInt32()).ToList();

    private TrabajadorExportaciones Trabajador => api.Services.GetRequiredService<TrabajadorExportaciones>();

    private static async Task<JsonElement> EstadoAsync(HttpClient cliente, string id) =>
        await cliente.GetAsync($"/api/v1/reportes/exportaciones/{id}").EsperarAsync(HttpStatusCode.OK);

    // Ejecuta el proceso en segundo plano (a mano) hasta que el trabajo termine, y devuelve su estado.
    private async Task<JsonElement> ProcesarHastaAsync(HttpClient cliente, string id)
    {
        for (var i = 0; i < 40; i++)
        {
            var estado = await EstadoAsync(cliente, id);
            if (estado.GetProperty("estado").GetString() is "listo" or "fallido")
                return estado;
            await Trabajador.ProcesarLoteAsync(default);
        }
        throw new Xunit.Sdk.XunitException("El trabajo no termino despues de 40 vueltas del proceso en segundo plano.");
    }

    private async Task ProcesarTodoAsync()
    {
        for (var i = 0; i < 40 && await Trabajador.ProcesarLoteAsync(default) > 0; i++)
        {
        }
    }

    private async Task<(JsonElement Estado, string Id)> ExportarAsync(HttpClient cliente, string reporte, string formato, object? filtros = null)
    {
        var aceptado = await cliente.PostAsJsonAsync($"/api/v1/reportes/{reporte}/exportaciones", new { formato, filtros = filtros ?? new { anio = 2022, mes = 3 } })
            .EsperarAsync(HttpStatusCode.Accepted);
        var id = aceptado.GetProperty("id").GetString()!;
        return (await ProcesarHastaAsync(cliente, id), id);
    }

    private static async Task<byte[]> DescargarAsync(HttpClient cliente, string id)
    {
        using var respuesta = await cliente.GetAsync($"/api/v1/reportes/exportaciones/{id}/archivo");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadAsByteArrayAsync();
    }

    // ── Acceso: la matriz de roles completa ──

    [Fact]
    public async Task ElOperativo_LoVeTodoElPersonal_ElGerencialYElParametrizableSoloGestion()
    {
        await DatosAsync();
        var cliente = (await api.RegistrarClienteAsync()).Token;
        var actores = new (string Nombre, string? Token, bool Operativo, bool Gestion)[]
        {
            ("visitante", null, false, false),
            ("cliente", cliente, false, false),
            ("dueño", await api.TokenEmpleadoAsync(Roles.Dueno), true, true),
            ("administrador", await api.TokenEmpleadoAsync(Roles.Administrador), true, true),
            ("diseñador", await api.TokenEmpleadoAsync(Roles.Disenador), true, false),
            ("operario", await api.TokenEmpleadoAsync(Roles.Operario), true, false)
        };

        foreach (var (nombre, token, operativo, gestion) in actores)
        {
            foreach (var (reporte, permitido) in new[] { ("operativo", operativo), ("gerencial", gestion), ("parametrizable", gestion) })
            {
                var esperado = permitido ? HttpStatusCode.OK : token is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
                using var respuesta = await api.Cliente(token).GetAsync($"/api/v1/reportes/{reporte}?{Marzo}");
                Assert.True(respuesta.StatusCode == esperado, $"GET {reporte} como {nombre}: se esperaba {(int)esperado} y llego {(int)respuesta.StatusCode}.");

                // Pedir la exportacion tiene exactamente los mismos permisos (se valida en el servidor, no en la pantalla).
                using var pedido = await api.Cliente(token).PostAsJsonAsync($"/api/v1/reportes/{reporte}/exportaciones", new { formato = "csv", filtros = new { anio = 2022, mes = 3 } });
                var esperadoPedido = permitido ? HttpStatusCode.Accepted : token is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
                Assert.True(pedido.StatusCode == esperadoPedido, $"POST exportaciones {reporte} como {nombre}: se esperaba {(int)esperadoPedido} y llego {(int)pedido.StatusCode}.");
                if (permitido)
                    await ProcesarTodoAsync();   // un trabajo por persona a la vez: se libera antes del siguiente reporte
            }
        }
    }

    // ── Filtros ──

    [Theory]
    [InlineData("operativo", "mes=5", "mes")]                                  // mes sin año
    [InlineData("operativo", "anio=2019", "anio")]                             // año fuera de rango
    [InlineData("operativo", "desde=2022-03-10&hasta=2022-03-01", "hasta")]    // rango invertido
    [InlineData("operativo", "estadoPedido=enviado", "estadoPedido")]
    [InlineData("gerencial", "estadoPago=regalado", "estadoPago")]
    [InlineData("gerencial", "comparar=ayer", "comparar")]
    [InlineData("parametrizable", "cliente=abc", "cliente")]
    [InlineData("parametrizable", "alcanceSaldo=siempre", "alcanceSaldo")]
    [InlineData("operativo", "filtroInventado=1", "filtroInventado")]         // nada se ignora en silencio
    [InlineData("operativo", "alcanceSaldo=vigente", "alcanceSaldo")]          // un filtro de otro reporte
    public async Task UnFiltroInvalido_Responde400_ConElErrorPorCampo(string reporte, string consulta, string campo)
    {
        var error = await ReporteAsync(reporte, consulta, HttpStatusCode.BadRequest);

        Assert.Equal("datos_invalidos", error.GetProperty("codigo").GetString());
        Assert.True(error.GetProperty("errores").TryGetProperty(campo, out var mensajes), $"Falta el error de «{campo}»: {error}");
        Assert.False(string.IsNullOrWhiteSpace(mensajes[0].GetString()));
    }

    [Fact]
    public async Task AlPedirLaExportacion_LosMismosFiltros_SeValidanIgual_YNoSeCreaNingunTrabajo()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var antes = await api.ConsultarAsync(db => db.TrabajosExportacion.CountAsync());

        var error = await admin.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", new { formato = "csv", filtros = new { anio = 2022, mes = 13 } })
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(error.GetProperty("errores").TryGetProperty("mes", out _));

        var formato = await admin.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", new { formato = "docx", filtros = new { anio = 2022, mes = 3 } })
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(formato.GetProperty("errores").TryGetProperty("formato", out _));

        var anidado = await admin.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", new { formato = "csv", filtros = new { cliente = new { id = 1 } } })
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(anidado.GetProperty("errores").TryGetProperty("cliente", out _));

        await admin.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", new { formato = "../../x.xlsx", filtros = new { anio = 2022, mes = 3 } })
            .EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Equal(antes, await api.ConsultarAsync(db => db.TrabajosExportacion.CountAsync()));
    }

    // ── El reporte operativo ──

    [Fact]
    public async Task Operativo_LlevaPedidosEntregasYSolicitudes_ConLosAnuladosMarcados_YFueraDeLosTotales()
    {
        var d = await DatosAsync();
        var reporte = await ReporteAsync("operativo");

        Assert.Equal("Reporte operativo", reporte.GetProperty("titulo").GetString());
        Assert.Equal(["pedidos", "entregas", "solicitudes"], reporte.GetProperty("tablas").EnumerateArray().Select(t => t.GetProperty("clave").GetString()));

        // Pedidos de marzo, el anulado incluido y marcado; en el orden en que se crearon.
        var pedidos = Tabla(reporte, "pedidos");
        Assert.Equal([d.Pedidos["P1"], d.Pedidos["P2"], d.Pedidos["P3"], d.Pedidos["P4"]], IdsDePedidos(pedidos));
        Assert.Equal(["Entregado", "Anulado", "Entregado", "En proceso"], Filas(pedidos).Select(f => f[4].GetString()));
        Assert.Equal(["Pagado total", "No pagado", "No pagado", "Abonado"], Filas(pedidos).Select(f => f[5].GetString()));
        // Monto, abonado y saldo: 240.000, 120.000 y 120.000 (sin el anulado: 50.000 en monto y en saldo).
        Assert.Equal([null, null, null, null, null, null, 240_000m, 120_000m, 120_000m], Totales(pedidos));

        // Entregas: las de la fecha real dentro de marzo. P1 llego un dia antes; P3, dos dias despues.
        var entregas = Tabla(reporte, "entregas");
        Assert.Equal([d.Pedidos["P1"], d.Pedidos["P3"]], IdsDePedidos(entregas));
        Assert.Equal(["Sí", "No"], Filas(entregas).Select(f => f[4].GetString()));
        Assert.Equal("2022-03-10", Filas(entregas)[0][2].GetString());
        Assert.Equal("2022-03-09", Filas(entregas)[0][3].GetString());

        // Solicitudes de marzo, por fecha: S1, S2, S5, S3, S6 y S4.
        var solicitudes = Filas(Tabla(reporte, "solicitudes"));
        Assert.Equal(["Aceptado", "Aceptado", "Solicitado", "Solicitado", "Rechazado", "Aceptado"], solicitudes.Select(f => f[4].GetString()));
        Assert.Equal(["En línea", "En línea", "En línea", "Presencial", "En línea", "Presencial"], solicitudes.Select(f => f[3].GetString()));
    }

    [Fact]
    public async Task Operativo_FiltraPorCliente_ElQueNoTienePedidosVeTablasVacias()
    {
        var d = await DatosAsync();

        var sinPedidos = await ReporteAsync("operativo", $"{Marzo}&cliente={d.ClienteSinPedidos}");
        Assert.All(new[] { "pedidos", "entregas", "solicitudes" }, clave => Assert.Empty(Filas(Tabla(sinPedidos, clave))));
        Assert.Equal([null, null, null, null, null, null, 0m, 0m, 0m], Totales(Tabla(sinPedidos, "pedidos")));

        var deB = await ReporteAsync("operativo", $"{Marzo}&cliente={d.ClienteB}");
        Assert.Equal([d.Pedidos["P2"], d.Pedidos["P4"]], IdsDePedidos(Tabla(deB, "pedidos")));
    }

    [Fact]
    public async Task Operativo_ElFiltroDeEstadoDelPedido_SeRespeta()
    {
        var d = await DatosAsync();

        var entregados = await ReporteAsync("operativo", $"{Marzo}&estadoPedido=entregado");

        Assert.Equal([d.Pedidos["P1"], d.Pedidos["P3"]], IdsDePedidos(Tabla(entregados, "pedidos")));
    }

    // ── El reporte parametrizable y la cobranza ──

    [Fact]
    public async Task Parametrizable_FiltraPorFechaEstadoPagoYCliente_ConSusTotales()
    {
        var d = await DatosAsync();

        var marzo = await ReporteAsync("parametrizable");
        var pedidos = Tabla(marzo, "pedidos");
        Assert.Equal([d.Pedidos["P1"], d.Pedidos["P3"], d.Pedidos["P4"]], IdsDePedidos(pedidos));   // sin el anulado
        Assert.Equal([null, null, null, null, null, null, 240_000m, 120_000m, 120_000m], Totales(pedidos));

        var rango = await ReporteAsync("parametrizable", "desde=2022-03-05&hasta=2022-03-10&estadoPago=abonado");
        Assert.Equal([d.Pedidos["P4"]], IdsDePedidos(Tabla(rango, "pedidos")));

        var deA = await ReporteAsync("parametrizable", $"anio=2022&cliente={d.ClienteA}");
        Assert.Equal(new[] { d.Pedidos["P5"], d.Pedidos["P6"], d.Pedidos["P1"], d.Pedidos["P3"] }.Order(), IdsDePedidos(Tabla(deA, "pedidos")).Order());
    }

    [Fact]
    public async Task Cobranza_PorCobrar_ListaLosNoPagadosYAbonados_ConElSaldoPorCobrar()
    {
        var d = await DatosAsync();

        var cobranza = await ReporteAsync("parametrizable", $"{Marzo}&estadoPago=por_cobrar");

        var tabla = Tabla(cobranza, "pedidos");
        Assert.Equal("Pedidos por cobrar", tabla.GetProperty("titulo").GetString());
        Assert.Equal([d.Pedidos["P3"], d.Pedidos["P4"]], IdsDePedidos(tabla));   // P1 esta pagado y P2 anulado
        Assert.Equal(120_000m, Totales(tabla)[8]);                              // saldo por cobrar: 80.000 + 40.000
        Assert.Contains(cobranza.GetProperty("filtros").EnumerateArray(), f => f.GetProperty("valor").GetString() == "Por cobrar");
    }

    [Fact]
    public async Task Cobranza_Vigente_NoLimitaPorFecha_ConToda_LaDeudaDelCliente()
    {
        var d = await DatosAsync();

        var cobranza = await ReporteAsync("parametrizable", $"estadoPago=por_cobrar&alcanceSaldo=vigente&cliente={d.ClienteA}");

        // P3 (80.000), P5 (90.000) y P6 (80.000); P1 esta pagado.
        var tabla = Tabla(cobranza, "pedidos");
        Assert.Equal(new[] { d.Pedidos["P3"], d.Pedidos["P5"], d.Pedidos["P6"] }.Order(), IdsDePedidos(tabla).Order());
        Assert.Equal(250_000m, Totales(tabla)[8]);
    }

    // ── El reporte gerencial ──

    [Fact]
    public async Task Gerencial_Ingresos_Costos_Y_Margen_SoloSobreLosPedidosConCosto()
    {
        var d = await DatosAsync();
        var reporte = await ReporteAsync("gerencial");

        Assert.Equal(["resumen", "tramos", "tipos", "pedidos"], reporte.GetProperty("tablas").EnumerateArray().Select(t => t.GetProperty("clave").GetString()));

        // Resumen de marzo (columna 1) contra febrero (columna 2).
        var resumen = Filas(Tabla(reporte, "resumen")).ToDictionary(f => f[0].GetString()!, f => f);
        Assert.Equal(180_000m, resumen["Ingresos (pedidos entregados)"][1].GetDecimal());          // P1 + P3
        Assert.Equal(100_000m, resumen["Ingresos de los pedidos con costo"][1].GetDecimal());      // solo P1 tiene cotizacion
        Assert.Equal(20_000m, resumen["Costo de materiales"][1].GetDecimal());
        Assert.Equal(30_000m, resumen["Costo de mano de obra"][1].GetDecimal());
        Assert.Equal(5_000m, resumen["Otros costos"][1].GetDecimal());
        Assert.Equal(55_000m, resumen["Costos totales"][1].GetDecimal());
        Assert.Equal(75_000m, resumen["Margen (ingresos con costo − materiales − otros costos)"][1].GetDecimal());    // 100.000 − 20.000 de materiales − 5.000 de otros: ni los 80.000 sin costo ni la mano de obra

        Assert.Equal(120_000m, resumen["Ingresos (pedidos entregados)"][2].GetDecimal());          // febrero: P6
        Assert.Equal(47_502m, resumen["Costos totales"][2].GetDecimal());                          // 7.501,50 se redondea al peso: 7.502 + 40.000
        Assert.Equal(112_498m, resumen["Margen (ingresos con costo − materiales − otros costos)"][2].GetDecimal());   // 120.000 − 7.502
        Assert.Equal(50m, resumen["Ingresos (pedidos entregados)"][3].GetDecimal());               // (180.000 − 120.000) ÷ 120.000 × 100

        // Un pedido sin cotizacion NO tiene costo: aparece sin costo, no se inventa uno.
        var detalle = Tabla(reporte, "pedidos");
        var filas = Filas(detalle).ToDictionary(f => f[0].GetInt32());
        Assert.Equal(55_000m, filas[d.Pedidos["P1"]][4].GetDecimal());
        Assert.Equal(20_000m, filas[d.Pedidos["P1"]][5].GetDecimal());   // materiales
        Assert.Equal(5_000m, filas[d.Pedidos["P1"]][6].GetDecimal());    // otros costos (la columna que hace transparente el margen)
        Assert.Equal(75_000m, filas[d.Pedidos["P1"]][7].GetDecimal());   // margen: 100.000 − 20.000 − 5.000
        Assert.Equal(JsonValueKind.Null, filas[d.Pedidos["P3"]][4].ValueKind);
        Assert.Equal(JsonValueKind.Null, filas[d.Pedidos["P3"]][6].ValueKind);   // sin cotizacion: sin costo, tampoco otros costos
        Assert.Equal(JsonValueKind.Null, filas[d.Pedidos["P3"]][7].ValueKind);
        Assert.Equal("Sin costo", detalle.GetProperty("columnas")[4].GetProperty("textoNulo").GetString());
        Assert.Equal(["Pedido N.º", "Cliente", "Entrega real", "Ingreso", "Costo total", "Materiales", "Otros costos", "Margen"],
            detalle.GetProperty("columnas").EnumerateArray().Select(c => c.GetProperty("titulo").GetString()));
        Assert.True(detalle.GetProperty("columnas")[6].GetProperty("suma").GetBoolean());
        Assert.Equal("Sin costo", detalle.GetProperty("columnas")[6].GetProperty("textoNulo").GetString());
        Assert.Equal([null, null, null, 180_000m, 55_000m, 20_000m, 5_000m, 75_000m], Totales(detalle));
        Assert.Contains(reporte.GetProperty("notas").EnumerateArray(), n => n.GetString()!.Contains("1 no") && n.GetString()!.Contains("75,0 %"));
    }

    [Fact]
    public async Task Gerencial_LosTramosYLosTiposDeLetrero_SumanLosIngresos()
    {
        var reporte = await ReporteAsync("gerencial");

        // Marzo se agrupa por semana; P1 (09-03) y P3 (14-03) caen en la segunda.
        var tramos = Tabla(reporte, "tramos");
        var porTramo = Filas(tramos);
        Assert.Equal(5, porTramo.Count);
        Assert.Equal(180_000m, porTramo[1][3].GetDecimal());
        Assert.Equal(55_000m, porTramo[1][4].GetDecimal());
        Assert.Equal([null, null, null, 180_000m, 55_000m], Totales(tramos));

        // Los 80.000 de P3 se reparten entre sus items (1 del producto 1 y 3 del producto 9): el producto 1 suma 100.000 + 20.000 y el 9, 60.000.
        var tipos = Tabla(reporte, "tipos");
        Assert.Equal(180_000m, Totales(tipos)[1]);
        Assert.Equal([60_000m, 120_000m], Filas(tipos).Select(f => f[1].GetDecimal()).Order());

        // Los graficos llevan los mismos datos que sus tablas.
        var graficos = reporte.GetProperty("graficos").EnumerateArray().ToDictionary(g => g.GetProperty("clave").GetString()!);
        Assert.Equal("barras", graficos["tramos"].GetProperty("forma").GetString());
        Assert.Equal(5, graficos["tramos"].GetProperty("etiquetas").GetArrayLength());
        Assert.Equal(180_000m, graficos["tramos"].GetProperty("series")[0].GetProperty("valores").EnumerateArray().Sum(v => v.GetDecimal()));
        Assert.Equal("barras-horizontales", graficos["tipos"].GetProperty("forma").GetString());
    }

    [Fact]
    public async Task Gerencial_SinComparacion_NoLlevaLasColumnasDelPeriodoAnterior()
    {
        var reporte = await ReporteAsync("gerencial", $"{Marzo}&comparar=ninguno");

        Assert.Equal(["concepto", "actual"], Tabla(reporte, "resumen").GetProperty("columnas").EnumerateArray().Select(c => c.GetProperty("clave").GetString()));
    }

    // ── Coherencia con el dashboard ──

    [Fact]
    public async Task LosIngresosYElSaldoPorCobrar_SonLosMismosQueLosDelDashboard()
    {
        await DatosAsync();
        var admin = api.Cliente(await api.TokenAdminAsync());
        var resumen = await admin.GetAsync($"/api/v1/indicadores/resumen?{Marzo}&comparar=ninguno").EsperarAsync(HttpStatusCode.OK);
        var ingresosDashboard = resumen.GetProperty("tarjetas").EnumerateArray().Single(t => t.GetProperty("clave").GetString() == "ingresos").GetProperty("valor").GetDecimal();
        var saldoDashboard = resumen.GetProperty("saldo");

        var gerencial = await ReporteAsync("gerencial", $"{Marzo}&comparar=ninguno");
        var cobranza = await ReporteAsync("parametrizable", $"{Marzo}&estadoPago=por_cobrar");

        // Los ingresos: el resumen, el total del detalle y el total de los tramos son lo mismo que la tarjeta del dashboard.
        var resumenIngresos = Filas(Tabla(gerencial, "resumen")).Single(f => f[0].GetString() == "Ingresos (pedidos entregados)")[1].GetDecimal();
        Assert.Equal(ingresosDashboard, resumenIngresos);
        Assert.Equal(ingresosDashboard, Totales(Tabla(gerencial, "pedidos"))[3]);
        Assert.Equal(ingresosDashboard, Totales(Tabla(gerencial, "tramos"))[3]);
        Assert.Equal(ingresosDashboard, Totales(Tabla(gerencial, "tipos"))[1]);

        // El saldo por cobrar: el total de la cobranza es el del dashboard, y la suma de sus filas es su total.
        var pedidos = Tabla(cobranza, "pedidos");
        Assert.Equal(saldoDashboard.GetProperty("valor").GetDecimal(), Totales(pedidos)[8]);
        Assert.Equal(saldoDashboard.GetProperty("cantidad").GetInt32(), Filas(pedidos).Count);
        Assert.Equal(Totales(pedidos)[8], Filas(pedidos).Sum(f => f[8].GetDecimal()));
    }

    [Fact]
    public async Task LaCobranzaVigente_EsLaDelDashboard_ParaElMismoCliente()
    {
        var d = await DatosAsync();
        var admin = api.Cliente(await api.TokenAdminAsync());
        var dashboard = await admin.GetAsync($"/api/v1/indicadores/por-cobrar?alcanceSaldo=vigente&cliente={d.ClienteA}&tamano=100").EsperarAsync(HttpStatusCode.OK);

        var cobranza = await ReporteAsync("parametrizable", $"estadoPago=por_cobrar&alcanceSaldo=vigente&cliente={d.ClienteA}");

        Assert.Equal(dashboard.GetProperty("totalSaldo").GetDecimal(), Totales(Tabla(cobranza, "pedidos"))[8]);
        Assert.Equal(dashboard.GetProperty("total").GetInt32(), Filas(Tabla(cobranza, "pedidos")).Count);
    }

    // ── El tope de filas ──

    [Fact]
    public async Task UnReporteConMasFilasQueElTope_SeRechazaConUnMensajeClaro_NoSeGeneraAMedias()
    {
        await DatosAsync();
        await using var chico = api.ConVariable("Reportes__MaximoDeFilas", "3");
        var admin = chico.Cliente(await api.TokenAdminAsync());

        var lectura = await admin.GetAsync($"/api/v1/reportes/operativo?{Marzo}").EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("reporte_muy_grande", lectura.GetProperty("codigo").GetString());
        Assert.Contains("acota", lectura.GetProperty("error").GetString()!, StringComparison.OrdinalIgnoreCase);

        var antes = await api.ConsultarAsync(db => db.TrabajosExportacion.CountAsync());
        var pedido = await admin.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", new { formato = "pdf", filtros = new { anio = 2022, mes = 3 } })
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("reporte_muy_grande", pedido.GetProperty("codigo").GetString());
        Assert.Equal(antes, await api.ConsultarAsync(db => db.TrabajosExportacion.CountAsync()));

        // Con filtros que dejan pocas filas, el mismo reporte sale.
        await admin.GetAsync("/api/v1/reportes/parametrizable?desde=2022-03-10&hasta=2022-03-10").EsperarAsync(HttpStatusCode.OK);
    }

    // ── El ciclo de una exportacion ──

    [Fact]
    public async Task UnaExportacion_Responde202_SeProcesaEnSegundoPlano_YSeDescarga()
    {
        var d = await DatosAsync();
        var cliente = await ComoAsync(Roles.Dueno);

        var aceptado = await cliente.PostAsJsonAsync("/api/v1/reportes/parametrizable/exportaciones",
            new { formato = "csv", filtros = new { anio = 2022, mes = 3, estadoPago = "por_cobrar" } });
        Assert.Equal(HttpStatusCode.Accepted, aceptado.StatusCode);
        var cuerpo = JsonDocument.Parse(await aceptado.Content.ReadAsStringAsync()).RootElement;
        var id = cuerpo.GetProperty("id").GetString()!;
        Assert.Equal($"/api/v1/reportes/exportaciones/{id}", aceptado.Headers.Location!.ToString());
        Assert.Equal("pendiente", cuerpo.GetProperty("estado").GetString());
        Assert.Equal("parametrizable", cuerpo.GetProperty("reporte").GetString());
        Assert.Equal("csv", cuerpo.GetProperty("formato").GetString());

        // Antes de que se genere, la descarga todavia no existe.
        var antes = await cliente.GetAsync($"/api/v1/reportes/exportaciones/{id}/archivo").EsperarAsync(HttpStatusCode.Conflict);
        Assert.Contains("preparando", antes.GetProperty("error").GetString()!);

        var listo = await ProcesarHastaAsync(cliente, id);
        Assert.Equal("listo", listo.GetProperty("estado").GetString());
        Assert.Equal(2, listo.GetProperty("filas").GetInt32());
        Assert.False(listo.GetProperty("vencido").GetBoolean());
        Assert.Matches(@"^reporte-parametrizable-\d{4}-\d{2}-\d{2}\.csv$", listo.GetProperty("nombreArchivo").GetString());
        Assert.Equal(listo.GetProperty("fechaTermino").GetDateTimeOffset().AddHours(24), listo.GetProperty("fechaVencimiento").GetDateTimeOffset());

        using var descarga = await cliente.GetAsync($"/api/v1/reportes/exportaciones/{id}/archivo");
        Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);
        Assert.Equal("text/csv", descarga.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", descarga.Content.Headers.ContentType.CharSet);
        Assert.Equal(listo.GetProperty("nombreArchivo").GetString(), descarga.Content.Headers.ContentDisposition!.FileName);
        Assert.Equal("attachment", descarga.Content.Headers.ContentDisposition.DispositionType);
        Assert.Contains("no-store", descarga.Headers.CacheControl!.ToString());
        var bytes = await descarga.Content.ReadAsByteArrayAsync();
        Assert.Equal(listo.GetProperty("tamanoBytes").GetInt32(), bytes.Length);
        var texto = Encoding.UTF8.GetString(bytes).TrimStart('﻿');
        Assert.Equal([d.Pedidos["P3"], d.Pedidos["P4"]], texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => int.Parse(l.Split(';')[0])));
    }

    [Fact]
    public async Task ElArchivo_SeGuardaConUnNombreDelServidor_NuncaConTextoDeLaPersona()
    {
        var cliente = await ComoAsync(Roles.Dueno);

        var (_, id) = await ExportarAsync(cliente, "operativo", "xlsx");

        var archivos = Directory.EnumerateFiles(api.CarpetaExportaciones).Select(Path.GetFileName).ToList();
        Assert.Contains($"{Guid.Parse(id):N}.xlsx", archivos);
        Assert.All(archivos, nombre => Assert.Matches(@"^[0-9a-f]{32}\.(xlsx|csv|pdf)$", nombre!));
    }

    [Fact]
    public async Task UnTercero_NoPuedeVerNiDescargar_ElTrabajoDeOtro_Recibe404()
    {
        var duenio = await ComoAsync(Roles.Dueno);
        var (_, id) = await ExportarAsync(duenio, "operativo", "csv");
        var otro = await ComoAsync(Roles.Operario);
        var otroGestion = api.Cliente(await api.TokenAdminAsync());

        // 404, no 403: no se revela que el trabajo existe. Lo mismo para quien tiene mas permisos que el solicitante.
        await otro.GetAsync($"/api/v1/reportes/exportaciones/{id}").EsperarAsync(HttpStatusCode.NotFound);
        await otro.GetAsync($"/api/v1/reportes/exportaciones/{id}/archivo").EsperarAsync(HttpStatusCode.NotFound);
        await otroGestion.GetAsync($"/api/v1/reportes/exportaciones/{id}").EsperarAsync(HttpStatusCode.NotFound);
        await otroGestion.GetAsync($"/api/v1/reportes/exportaciones/{id}/archivo").EsperarAsync(HttpStatusCode.NotFound);

        // Y un identificador que no existe responde exactamente igual.
        var inexistente = await otro.GetAsync($"/api/v1/reportes/exportaciones/{Guid.NewGuid()}").EsperarAsync(HttpStatusCode.NotFound);
        var ajeno = await otro.GetAsync($"/api/v1/reportes/exportaciones/{id}").EsperarAsync(HttpStatusCode.NotFound);
        Assert.Equal(inexistente.SinTraza(), ajeno.SinTraza());

        // Un cliente de la web ni siquiera llega: es del personal.
        await api.Cliente((await api.RegistrarClienteAsync()).Token).GetAsync($"/api/v1/reportes/exportaciones/{id}").EsperarAsync(HttpStatusCode.Forbidden);
        await api.Cliente().GetAsync($"/api/v1/reportes/exportaciones/{id}/archivo").EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("no-es-un-id")]
    [InlineData("..%2F..%2Fappsettings.json")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task UnIdentificadorRaro_Responde404_SinTocarElDisco(string id)
    {
        var cliente = await ComoAsync(Roles.Dueno);

        await cliente.GetAsync($"/api/v1/reportes/exportaciones/{id}").EsperarAsync(HttpStatusCode.NotFound);
        await cliente.GetAsync($"/api/v1/reportes/exportaciones/{id}/archivo").EsperarAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnTrabajoPorPersonaALaVez_ElSegundoPedido_EsUnConflicto()
    {
        var cliente = await ComoAsync(Roles.Dueno);
        var primero = await cliente.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", new { formato = "csv", filtros = new { anio = 2022, mes = 3 } })
            .EsperarAsync(HttpStatusCode.Accepted);

        var conflicto = await cliente.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", new { formato = "pdf", filtros = new { anio = 2022, mes = 3 } })
            .EsperarAsync(HttpStatusCode.Conflict);
        Assert.Contains("en preparación", conflicto.GetProperty("error").GetString()!);

        // Otra persona no se ve afectada, y quien termino puede pedir otro.
        await (await ComoAsync(Roles.Operario)).PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", new { formato = "csv", filtros = new { anio = 2022, mes = 3 } })
            .EsperarAsync(HttpStatusCode.Accepted);
        await ProcesarHastaAsync(cliente, primero.GetProperty("id").GetString()!);
        await cliente.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", new { formato = "pdf", filtros = new { anio = 2022, mes = 3 } })
            .EsperarAsync(HttpStatusCode.Accepted);
        await ProcesarTodoAsync();
    }

    [Fact]
    public async Task DosPedidosAlMismoTiempo_ComoMuchoUnoSeAcepta_LaBaseLoImpone()
    {
        var cliente = await ComoAsync(Roles.Dueno);
        var cuerpo = new { formato = "csv", filtros = new { anio = 2022, mes = 3 } };

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => cliente.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", cuerpo)));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Accepted));
        Assert.All(respuestas.Where(r => r.StatusCode != HttpStatusCode.Accepted), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        await ProcesarTodoAsync();
    }

    [Fact]
    public async Task PedirExportaciones_TieneSuPropioLimite_PorPersona()
    {
        await using var estricta = api.ConVariable("RateLimiting__Exportaciones", "2");
        var cliente = estricta.Cliente(await api.TokenEmpleadoAsync(Roles.Dueno));
        var cuerpo = new { formato = "csv", filtros = new { anio = 2022, mes = 3 } };

        Assert.Equal(HttpStatusCode.Accepted, (await cliente.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", cuerpo)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", cuerpo)).StatusCode);   // cuenta como intento
        var tercero = await cliente.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", cuerpo);

        Assert.Equal(HttpStatusCode.TooManyRequests, tercero.StatusCode);
        Assert.Equal("demasiados_intentos", JsonDocument.Parse(await tercero.Content.ReadAsStringAsync()).RootElement.GetProperty("codigo").GetString());
        // El limite es de quien lo pide: otra persona sigue pudiendo.
        var otro = estricta.Cliente(await api.TokenEmpleadoAsync(Roles.Disenador));
        Assert.Equal(HttpStatusCode.Accepted, (await otro.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", cuerpo)).StatusCode);
        await ProcesarTodoAsync();
    }

    [Fact]
    public async Task ElPdf_TieneUnTopeMasBajo_QueExcelYCsv_ConUnMensajeQueSugiereOtroFormato()
    {
        await DatosAsync();
        await using var chico = api.ConVariable("Reportes__MaximoDeFilasPdf", "3");
        var cliente = chico.Cliente(await api.TokenEmpleadoAsync(Roles.Dueno));
        var cuerpo = (string formato) => new { formato, filtros = new { anio = 2022, mes = 3 } };

        var pdf = await cliente.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", cuerpo("pdf")).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("reporte_muy_grande", pdf.GetProperty("codigo").GetString());
        Assert.Contains("Excel o CSV", pdf.GetProperty("error").GetString()!);

        // El mismo reporte, en CSV, sale: el tope bajo es solo del PDF.
        await cliente.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", cuerpo("csv")).EsperarAsync(HttpStatusCode.Accepted);
        await ProcesarTodoAsync();
    }

    [Fact]
    public async Task UnaPersona_NoPuedeAcumularArchivosListos_SinTope_ElDiscoNoSeLlena()
    {
        var cliente = await ComoAsync(Roles.Dueno);
        for (var i = 0; i < 5; i++)
            await ExportarAsync(cliente, "operativo", "csv");

        var sexto = await cliente.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", new { formato = "csv", filtros = new { anio = 2022, mes = 3 } })
            .EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal(ServicioExportaciones.MensajeDemasiadosArchivos, sexto.GetProperty("error").GetString());
        // Otra persona no se ve afectada.
        await (await ComoAsync(Roles.Operario)).PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", new { formato = "csv", filtros = new { anio = 2022, mes = 3 } })
            .EsperarAsync(HttpStatusCode.Accepted);
        await ProcesarTodoAsync();
    }

    [Fact]
    public async Task UnCuerpoGrande_SeRechazaAntesDeLeerlo_YNoCreaTrabajo()
    {
        var cliente = await ComoAsync(Roles.Dueno);
        var enorme = new string('a', 10_000);
        var antes = await api.ConsultarAsync(db => db.TrabajosExportacion.CountAsync());

        using var respuesta = await cliente.PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", new { formato = "csv", filtros = new { anio = 2022, mes = 3, relleno = enorme } });

        // El tope de 4 KB lo aplica el servidor al leer el cuerpo: se rechaza (400 o 413) y no queda ningun trabajo.
        Assert.True(respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge, $"Llego {(int)respuesta.StatusCode}.");
        Assert.Equal(antes, await api.ConsultarAsync(db => db.TrabajosExportacion.CountAsync()));
    }

    [Fact]
    public async Task LasRespuestasConDatosDeClientes_NoSeGuardanEnCaches()
    {
        var cliente = await ComoAsync(Roles.Dueno);
        await DatosAsync();

        using var reporte = await cliente.GetAsync($"/api/v1/reportes/operativo?{Marzo}");
        Assert.Contains("no-store", reporte.Headers.CacheControl!.ToString());
        var (_, id) = await ExportarAsync(cliente, "operativo", "csv");
        using var estado = await cliente.GetAsync($"/api/v1/reportes/exportaciones/{id}");
        Assert.Contains("no-store", estado.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task LeerReportes_TieneSuPropioLimite_MasBajoQueElGeneral()
    {
        await using var estricta = api.ConVariable("RateLimiting__Reportes", "3");
        var cliente = estricta.Cliente(await api.TokenEmpleadoAsync(Roles.Operario));

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/reportes/operativo?desde=2022-03-10&hasta=2022-03-10")).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await cliente.GetAsync("/api/v1/reportes/operativo?desde=2022-03-10&hasta=2022-03-10")).StatusCode);
    }

    [Fact]
    public async Task ElLimitePorOmision_DeLasExportaciones_EsDiezPorMinuto()
    {
        // La API de pruebas lo sube para no estorbar; el valor que rige sin configurar es el del plan.
        var fuente = await File.ReadAllTextAsync(Path.Combine(Raiz(), "LetrerosHerreraSYNCTIMEapi", "Program.cs"));

        Assert.Contains("GetValue(\"RateLimiting:Exportaciones\", 10)", fuente);
    }

    private static string Raiz()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "LetrerosHerreraSYNCTIMEapi.slnx")))
            carpeta = carpeta.Parent;
        return carpeta!.FullName;
    }

    // ── Vencimiento: 410 y borrado del contenido ──

    private sealed class RelojAdelantado(TimeSpan adelanto) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + adelanto;
    }

    [Fact]
    public async Task PasadasLas24Horas_ElArchivoVence_LaDescargaResponde410_YLaPurgaLoBorra()
    {
        var token = await api.TokenEmpleadoAsync(Roles.Dueno);
        var cliente = api.Cliente(token);
        var (listo, id) = await ExportarAsync(cliente, "operativo", "csv");
        var almacen = api.Services.GetRequiredService<AlmacenExportaciones>();
        using (var presente = await almacen.AbrirAsync(Guid.Parse(id), "csv", default))
            Assert.NotNull(presente);

        // A las 23 horas todavia se descarga; a las 25, ya no.
        var casiVencido = api.ClienteConReloj(TimeSpan.FromHours(23), token);
        Assert.Equal(HttpStatusCode.OK, (await casiVencido.GetAsync($"/api/v1/reportes/exportaciones/{id}/archivo")).StatusCode);
        var vencido = api.ClienteConReloj(TimeSpan.FromHours(25), token);
        var gone = await vencido.GetAsync($"/api/v1/reportes/exportaciones/{id}/archivo").EsperarAsync(HttpStatusCode.Gone);
        Assert.Equal("exportacion_vencida", gone.GetProperty("codigo").GetString());
        Assert.True((await EstadoAsync(vencido, id)).GetProperty("vencido").GetBoolean());

        // La purga (con el reloj adelantado) borra el contenido y deja el registro del trabajo.
        var purga = new TrabajadorExportaciones(api.Services.GetRequiredService<IServiceScopeFactory>(), almacen,
            api.Services.GetRequiredService<IOptions<OpcionesReportes>>(), new RelojAdelantado(TimeSpan.FromHours(25)), NullLogger<TrabajadorExportaciones>.Instance);
        Assert.True(await purga.PurgarAsync(default) >= 1);
        Assert.Null(await almacen.AbrirAsync(Guid.Parse(id), "csv", default));
        var registro = await api.ConsultarAsync(db => db.TrabajosExportacion.AsNoTracking().SingleAsync(t => t.IdTrabajo == Guid.Parse(id)));
        Assert.Equal(EstadosExportacion.Listo, registro.Estado);
        Assert.NotNull(registro.ArchivoBorradoEn);
        Assert.Equal(listo.GetProperty("filas").GetInt32(), registro.Filas);
    }

    [Fact]
    public async Task LaPurga_TambienRetiraLosArchivosSinRegistro_SoloConNombreDeExportacion()
    {
        var carpeta = api.CarpetaExportaciones;
        Directory.CreateDirectory(carpeta);
        var huerfano = Path.Combine(carpeta, $"{Guid.NewGuid():N}.pdf");
        var ajeno = Path.Combine(carpeta, "notas-del-taller.txt");
        await File.WriteAllBytesAsync(huerfano, [1, 2, 3]);
        await File.WriteAllBytesAsync(ajeno, [1, 2, 3]);
        File.SetLastWriteTimeUtc(huerfano, DateTime.UtcNow.AddDays(-3));
        File.SetLastWriteTimeUtc(ajeno, DateTime.UtcNow.AddDays(-3));

        await Trabajador.PurgarAsync(default);

        Assert.False(File.Exists(huerfano));
        Assert.True(File.Exists(ajeno));   // lo que no tiene nombre de exportacion no se toca
        File.Delete(ajeno);
    }

    // ── Reinicio, fallos y bitacora ──

    private async Task<(Guid Id, string Token, int IdEmpleado)> InsertarTrabajoAsync(string estado, byte intentos, DateTimeOffset? bloqueadoHasta,
        string filtros = "{\"anio\":\"2022\",\"mes\":\"3\"}", string reporte = "operativo", string formato = "csv")
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Dueno);
        var id = Guid.NewGuid();
        await api.ConsultarAsync(async db =>
        {
            db.TrabajosExportacion.Add(new TrabajoExportacion
            {
                IdTrabajo = id, Reporte = reporte, Formato = formato, Filtros = filtros, Estado = estado, IdEmpleado = empleado.Id, RolSolicitante = Roles.Dueno,
                FechaPedido = DateTimeOffset.UtcNow.AddMinutes(-5), FechaInicio = estado == EstadosExportacion.Procesando ? DateTimeOffset.UtcNow.AddMinutes(-4) : null,
                BloqueadoHasta = bloqueadoHasta, Intentos = intentos
            });
            await db.SaveChangesAsync();
            return 0;
        });
        return (id, empleado.Token, empleado.Id);
    }

    private Task<TrabajoExportacion> TrabajoAsync(Guid id) => api.ConsultarAsync(db => db.TrabajosExportacion.AsNoTracking().SingleAsync(t => t.IdTrabajo == id));

    [Fact]
    public async Task SiElServidorSeReiniciaAMitad_ElTrabajoVuelveAPendiente_YSeCompleta()
    {
        var (id, _, _) = await InsertarTrabajoAsync(EstadosExportacion.Procesando, intentos: 1, bloqueadoHasta: DateTimeOffset.UtcNow.AddMinutes(10));

        // Mientras su plazo no pase, nadie mas lo toca: solo al arrancar se recupera todo lo «procesando».
        await Trabajador.RecuperarInterrumpidosAsync(alIniciar: false, default);
        Assert.Equal(EstadosExportacion.Procesando, (await TrabajoAsync(id)).Estado);

        await Trabajador.RecuperarInterrumpidosAsync(alIniciar: true, default);
        Assert.Equal(EstadosExportacion.Pendiente, (await TrabajoAsync(id)).Estado);

        await ProcesarTodoAsync();
        var trabajo = await TrabajoAsync(id);
        Assert.Equal(EstadosExportacion.Listo, trabajo.Estado);
        Assert.Equal(2, trabajo.Intentos);
    }

    [Fact]
    public async Task UnTrabajoAbandonado_ConElPlazoVencido_SeRecuperaSinReiniciar()
    {
        var (id, _, _) = await InsertarTrabajoAsync(EstadosExportacion.Procesando, intentos: 1, bloqueadoHasta: DateTimeOffset.UtcNow.AddMinutes(-1));

        await Trabajador.RecuperarInterrumpidosAsync(alIniciar: false, default);

        Assert.Equal(EstadosExportacion.Pendiente, (await TrabajoAsync(id)).Estado);
        await ProcesarTodoAsync();
    }

    [Fact]
    public async Task UnTrabajoInterrumpidoDemasiadasVeces_QuedaFallido_NoSeRepiteSinFin()
    {
        var (id, _, idEmpleado) = await InsertarTrabajoAsync(EstadosExportacion.Procesando, intentos: 3, bloqueadoHasta: DateTimeOffset.UtcNow.AddMinutes(10));

        await Trabajador.RecuperarInterrumpidosAsync(alIniciar: true, default);

        var trabajo = await TrabajoAsync(id);
        Assert.Equal(EstadosExportacion.Fallido, trabajo.Estado);
        Assert.Contains("reinició", trabajo.Error!);
        // Y el motivo queda en la bitacora, a nombre de quien lo pidio.
        var fila = await api.ConsultarAsync(db => db.Auditoria.AsNoTracking().SingleAsync(a => a.Accion == AccionesAuditoria.ExportarReporte && a.IdEntidad == id.ToString("N")));
        Assert.Equal(idEmpleado, fila.IdEmpleado);
        Assert.Contains("\"estado\":\"fallido\"", fila.Detalle);
    }

    [Fact]
    public async Task UnTrabajoPendienteQueNuncaSeProcesa_SeCierraConUnMotivo_YLaPersonaPuedePedirOtro()
    {
        var (id, token, _) = await InsertarTrabajoAsync(EstadosExportacion.Pendiente, intentos: 0, bloqueadoHasta: null);
        await api.ConsultarAsync(db => db.TrabajosExportacion.Where(t => t.IdTrabajo == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.FechaPedido, DateTimeOffset.UtcNow.AddHours(-2))));

        await Trabajador.RecuperarInterrumpidosAsync(alIniciar: false, default);

        var trabajo = await TrabajoAsync(id);
        Assert.Equal(EstadosExportacion.Fallido, trabajo.Estado);
        Assert.Contains("no se llegó a preparar", trabajo.Error!);
        await api.Cliente(token).PostAsJsonAsync("/api/v1/reportes/operativo/exportaciones", new { formato = "csv", filtros = new { anio = 2022, mes = 3 } })
            .EsperarAsync(HttpStatusCode.Accepted);
        await ProcesarTodoAsync();
    }

    [Fact]
    public async Task UnTrabajoQueFalla_QuedaFallidoConSuMotivo_YRegistradoEnLaBitacora_SinElContenido()
    {
        var (id, token, _) = await InsertarTrabajoAsync(EstadosExportacion.Pendiente, intentos: 0, bloqueadoHasta: null, filtros: "{\"mes\":\"13\",\"anio\":\"2022\"}");
        var cliente = api.Cliente(token);

        await ProcesarTodoAsync();

        var fallido = await TrabajoAsync(id);
        Assert.Equal(EstadosExportacion.Fallido, fallido.Estado);
        Assert.False(string.IsNullOrWhiteSpace(fallido.Error));
        var estado = await EstadoAsync(cliente, id.ToString());
        Assert.Equal("fallido", estado.GetProperty("estado").GetString());
        Assert.False(string.IsNullOrWhiteSpace(estado.GetProperty("error").GetString()));
        await cliente.GetAsync($"/api/v1/reportes/exportaciones/{id}/archivo").EsperarAsync(HttpStatusCode.Conflict);
        Assert.Empty(Directory.Exists(api.CarpetaExportaciones) ? Directory.EnumerateFiles(api.CarpetaExportaciones, $"{id:N}*") : []);

        var fila = await api.ConsultarAsync(db => db.Auditoria.AsNoTracking().SingleAsync(a => a.Accion == AccionesAuditoria.ExportarReporte && a.IdEntidad == id.ToString("N")));
        Assert.Contains("\"estado\":\"fallido\"", fila.Detalle);
        Assert.Contains("motivo", fila.Detalle);
    }

    [Fact]
    public async Task LaBitacora_RegistraQuienPidioElReporte_QueFormato_ConQueFiltros_YAlTerminarCuantasFilas()
    {
        var d = await DatosAsync();
        var (id, email, token) = await api.CrearEmpleadoAsync(Roles.Administrador);
        var cliente = api.Cliente(token);

        var (listo, trabajo) = await ExportarAsync(cliente, "parametrizable", "xlsx", new { anio = 2022, mes = 3, estadoPago = "por_cobrar", cliente = d.ClienteA });

        var filas = await api.ConsultarAsync(db => db.Auditoria.AsNoTracking().Where(a => a.Accion == AccionesAuditoria.ExportarReporte && a.IdEntidad == Guid.Parse(trabajo).ToString("N"))
            .OrderBy(a => a.IdAuditoria).ToListAsync());
        Assert.Equal(2, filas.Count);
        Assert.All(filas, f =>
        {
            Assert.Equal(id, f.IdEmpleado);
            Assert.Null(f.IdCliente);
            Assert.Equal(Roles.Administrador, f.Rol);
            Assert.Equal("reporte", f.Entidad);
        });

        var pedido = JsonDocument.Parse(filas[0].Detalle!).RootElement;
        Assert.Equal("pedido", pedido.GetProperty("estado").GetString());
        Assert.Equal("parametrizable", pedido.GetProperty("reporte").GetString());
        Assert.Equal("xlsx", pedido.GetProperty("formato").GetString());
        var filtros = pedido.GetProperty("filtros");
        Assert.Equal("2022", filtros.GetProperty("anio").GetString());
        Assert.Equal("3", filtros.GetProperty("mes").GetString());
        Assert.Equal("por_cobrar", filtros.GetProperty("estadoPago").GetString());
        Assert.Equal(d.ClienteA.ToString(), filtros.GetProperty("cliente").GetString());   // el cliente va por su numero
        Assert.DoesNotContain(email, filas[0].Detalle);

        var terminado = JsonDocument.Parse(filas[1].Detalle!).RootElement;
        Assert.Equal("listo", terminado.GetProperty("estado").GetString());
        Assert.Equal(listo.GetProperty("filas").GetInt32(), terminado.GetProperty("filas").GetInt32());
        Assert.DoesNotContain("Cli", filas[1].Detalle);   // nada del contenido
    }

    [Fact]
    public async Task ElVisorDeLaBitacora_MuestraLaNuevaAccion_Y_LaFiltra()
    {
        var cliente = await ComoAsync(Roles.Dueno);
        await ExportarAsync(cliente, "operativo", "csv");

        var visor = await api.Cliente(await api.TokenAdminAsync()).GetAsync("/api/v1/auditoria?accion=exportar_reporte&tamano=50").EsperarAsync(HttpStatusCode.OK);

        var elementos = visor.GetProperty("elementos").EnumerateArray().ToList();
        Assert.NotEmpty(elementos);
        Assert.All(elementos, e =>
        {
            Assert.Equal("Exportó un reporte", e.GetProperty("accionTexto").GetString());
            Assert.Equal("Reporte", e.GetProperty("entidadTexto").GetString());
        });
    }

    // ── Los archivos se abren de verdad, y los filtros se respetan en los tres formatos ──

    private static List<string[]> FilasDeLaHoja(SpreadsheetDocument libro, string hoja)
    {
        var parte = (WorksheetPart)libro.WorkbookPart!.GetPartById(libro.WorkbookPart.Workbook.Sheets!.Elements<Sheet>().Single(s => s.Name == hoja).Id!);
        return parte.Worksheet.Descendants<Row>().Select(r => r.Elements<Cell>().Select(c =>
            c.DataType?.Value == CellValues.InlineString ? c.InlineString!.InnerText : c.CellValue?.Text ?? "").ToArray()).ToList();
    }

    [Fact]
    public async Task LaCobranza_SaleIgual_EnPantallaExcelCsvYPdf_ConLosMismosPedidos()
    {
        var d = await DatosAsync();
        var cliente = await ComoAsync(Roles.Dueno);
        var filtros = new { anio = 2022, mes = 3, estadoPago = "por_cobrar" };
        var esperados = new[] { d.Pedidos["P3"], d.Pedidos["P4"] };
        Assert.Equal(esperados, IdsDePedidos(Tabla(await ReporteAsync("parametrizable", $"{Marzo}&estadoPago=por_cobrar"), "pedidos")));

        // Excel: una hoja «Pedidos por cobrar», con fechas y pesos como numeros, y el total como formula.
        var (_, idExcel) = await ExportarAsync(cliente, "parametrizable", "xlsx", filtros);
        using (var libro = SpreadsheetDocument.Open(new MemoryStream(await DescargarAsync(cliente, idExcel)), false))
        {
            Assert.Equal(["Pedidos por cobrar", "Filtros"], libro.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Select(s => s.Name!.Value));
            var hoja = FilasDeLaHoja(libro, "Pedidos por cobrar");
            Assert.Equal("Pedido N.º", hoja[0][0]);
            Assert.Equal(esperados, hoja.Skip(1).Take(2).Select(f => int.Parse(f[0])));
            Assert.Equal("Total", hoja[3][0]);
            Assert.Equal("120000", hoja[3][8]);   // saldo por cobrar
            var datos = ((WorksheetPart)libro.WorkbookPart.GetPartById(libro.WorkbookPart.Workbook.Sheets.Elements<Sheet>().First().Id!)).Worksheet;
            Assert.Equal("SUM(I2:I3)", datos.Descendants<CellFormula>().Last().Text);
            Assert.All(FilasDeLaHoja(libro, "Filtros").SelectMany(f => f), celda => Assert.False(celda.StartsWith('=')));
        }

        // CSV: con marca UTF-8, separado por «;», solo los datos de la tabla.
        var (_, idCsv) = await ExportarAsync(cliente, "parametrizable", "csv", filtros);
        var csv = await DescargarAsync(cliente, idCsv);
        Assert.Equal([0xEF, 0xBB, 0xBF], csv.Take(3).ToArray());
        var lineas = Encoding.UTF8.GetString(csv).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Pedido N.º;Cliente;Fecha del pedido;Entrega estimada;Estado del pedido;Estado de pago;Monto;Abonado;Saldo", lineas[0]);
        Assert.Equal(esperados, lineas.Skip(1).Select(l => int.Parse(l.Split(';')[0])));
        Assert.Contains("2022-03-06;2022-03-12;Entregado;No pagado;80000;0;80000", lineas[1]);

        // PDF: texto seleccionable con el titulo, los filtros y «Página 1 de 1».
        var (_, idPdf) = await ExportarAsync(cliente, "parametrizable", "pdf", filtros);
        using var pdf = PdfDocument.Open(await DescargarAsync(cliente, idPdf));
        var palabras = pdf.GetPages().SelectMany(p => p.GetWords().Select(w => w.Text)).ToList();
        Assert.Contains("parametrizable", palabras);
        Assert.All(esperados, pedido => Assert.Contains(pedido.ToString(), palabras));
        Assert.Contains("$120.000", palabras);
        Assert.Equal(1, pdf.NumberOfPages);
        Assert.Contains("Página 1 de 1", string.Join(" ", pdf.GetPage(1).GetWords().Select(w => w.Text)));
    }

    [Fact]
    public async Task ElOperativo_EnExcelLlevaSusTresTablas_EnCsvSoloLosPedidos_YEnPdfTodo()
    {
        var d = await DatosAsync();
        var cliente = await ComoAsync(Roles.Dueno);

        var (_, idExcel) = await ExportarAsync(cliente, "operativo", "xlsx");
        using (var libro = SpreadsheetDocument.Open(new MemoryStream(await DescargarAsync(cliente, idExcel)), false))
        {
            Assert.Equal(["Pedidos", "Entregas", "Solicitudes", "Filtros"], libro.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Select(s => s.Name!.Value));
            var pedidos = FilasDeLaHoja(libro, "Pedidos");
            Assert.Equal(1 + 4 + 1, pedidos.Count);        // encabezado, 4 pedidos y los totales
            Assert.Equal("Anulado", pedidos[2][4]);         // el anulado se ve marcado...
            Assert.Equal("240000", pedidos[5][6]);          // ...y los totales no lo suman: 100.000 + 80.000 + 60.000
            var hoja = (WorksheetPart)libro.WorkbookPart.GetPartById(libro.WorkbookPart.Workbook.Sheets.Elements<Sheet>().First().Id!);
            Assert.Contains(hoja.Worksheet.Descendants<CellFormula>(), f => f.Text.StartsWith("SUMIF(E2:E5,\"<>Anulado\"", StringComparison.Ordinal));
            Assert.Equal(2, FilasDeLaHoja(libro, "Entregas").Count - 1);
            Assert.Equal(6, FilasDeLaHoja(libro, "Solicitudes").Count - 1);
        }

        var (_, idCsv) = await ExportarAsync(cliente, "operativo", "csv");
        var lineas = Encoding.UTF8.GetString(await DescargarAsync(cliente, idCsv)).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(1 + 4, lineas.Length);   // la tabla principal (pedidos): un CSV es una sola tabla
        Assert.Equal([d.Pedidos["P1"], d.Pedidos["P2"], d.Pedidos["P3"], d.Pedidos["P4"]], lineas.Skip(1).Select(l => int.Parse(l.Split(';')[0])));

        var (_, idPdf) = await ExportarAsync(cliente, "operativo", "pdf");
        using var pdf = PdfDocument.Open(await DescargarAsync(cliente, idPdf));
        var texto = string.Join(" ", pdf.GetPages().SelectMany(p => p.GetWords().Select(w => w.Text)));
        Assert.Contains("Reporte operativo", texto);
        Assert.Contains("Entregas", texto);
        Assert.Contains("Solicitudes", texto);
        Assert.Contains("Anulado", texto);
    }

    [Fact]
    public async Task ElGerencial_EnPdfLlevaSusDosGraficos_YEnExcelSusCuatroHojas()
    {
        await DatosAsync();
        var cliente = await ComoAsync(Roles.Dueno);

        var (_, idPdf) = await ExportarAsync(cliente, "gerencial", "pdf");
        using (var pdf = PdfDocument.Open(await DescargarAsync(cliente, idPdf)))
        {
            Assert.Equal(2, pdf.GetPages().Sum(p => p.GetImages().Count()));
            var texto = string.Join(" ", pdf.GetPages().SelectMany(p => p.GetWords().Select(w => w.Text)));
            Assert.Contains("Reporte gerencial", texto);
            Assert.Contains("$180.000", texto);
            Assert.Contains("$80.000", texto);
            Assert.Contains("Sin costo", texto);
        }

        var (_, idExcel) = await ExportarAsync(cliente, "gerencial", "xlsx");
        using var libro = SpreadsheetDocument.Open(new MemoryStream(await DescargarAsync(cliente, idExcel)), false);
        Assert.Equal(["Resumen del período", "Ingresos y costos por tramo", "Ingresos por tipo de letrero", "Pedidos entregados", "Filtros"],
            libro.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Select(s => s.Name!.Value));
    }

    [Fact]
    public async Task UnNombreQueEmpiezaComoFormula_SaleDefendido_EnElCsvYEnElExcel()
    {
        await DatosAsync();
        var admin = await api.IdAdminAsync();
        // Abril de 2022: no se mezcla con los numeros de marzo. El nombre se pone directo en la base (la API no deja crearlo asi).
        var cliente = await api.CrearClienteAsync();
        await api.ConsultarAsync(async db =>
        {
            await db.Clientes.Where(c => c.IdCliente == cliente).ExecuteUpdateAsync(s => s.SetProperty(c => c.Nombre, "=CMD").SetProperty(c => c.Apellido, "+x"));
            var solicitud = await NuevaSolicitudAsync(db, admin, cliente, "presencial", "04-05", [(1, 1)], null);
            await NuevoPedidoAsync(db, admin, solicitud, "04-05", "04-12", null, "aceptado", 10_000, 0);
            return 0;
        });
        var duenio = await ComoAsync(Roles.Dueno);
        var filtros = new { anio = 2022, mes = 4, cliente };

        var (_, idCsv) = await ExportarAsync(duenio, "operativo", "csv", filtros);
        var csv = Encoding.UTF8.GetString(await DescargarAsync(duenio, idCsv));
        Assert.Contains(";'=CMD +x;", csv);       // con apostrofo: Excel lo trata como texto
        Assert.DoesNotContain(";=CMD +x;", csv);

        var (_, idExcel) = await ExportarAsync(duenio, "operativo", "xlsx", filtros);
        using var libro = SpreadsheetDocument.Open(new MemoryStream(await DescargarAsync(duenio, idExcel)), false);
        var celda = FilasDeLaHoja(libro, "Pedidos")[1][1];
        Assert.Equal("=CMD +x", celda);           // como texto, nunca como formula
        var hoja = (WorksheetPart)libro.WorkbookPart!.GetPartById(libro.WorkbookPart.Workbook.Sheets!.Elements<Sheet>().First().Id!);
        Assert.All(hoja.Worksheet.Descendants<CellFormula>(), f => Assert.StartsWith("SUM", f.Text));
    }
}
