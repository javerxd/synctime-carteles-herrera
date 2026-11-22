using LetrerosHerreraSYNCTIMEapi.Datos.Indicadores;
using LetrerosHerreraSYNCTIMEapi.Datos.Reportes;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;
using LetrerosHerreraSYNCTIMEapi.Negocio.Indicadores;
using LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// Fase 13A (RFS10, RFU24 a RFU26, RFU36, E16, S18, S24): arma los tres reportes. El resultado es un DocumentoReporte: el mismo que
// responde la API (JSON) y el que escriben los generadores de Excel, CSV y PDF, asi lo que se ve es lo que sale.

public sealed class ReporteDemasiadoGrandeException(string mensaje) : Exception(mensaje);

public sealed class ServicioReportes(ConsultasReportes consultas, IOptions<OpcionesReportes> opciones, TimeProvider reloj)
{
    private int Maximo => opciones.Value.MaximoDeFilas;

    /// <summary>El reporte de hoy (con «hoy» en Chile): lo que ve la pantalla.</summary>
    public Task<DocumentoReporte> ConstruirAhoraAsync(FiltroReporte filtro, CancellationToken ct) =>
        ConstruirAsync(filtro, TiempoChile.Hoy(reloj), reloj.GetUtcNow(), ct);

    private static Periodo PeriodoDe(FiltroReporte filtro, DateOnly hoy) => ReglasIndicadores.ResolverPeriodo(filtro.Indicadores, hoy);

    /// <summary>Cuantas filas tendra el reporte (todas sus tablas): se pregunta antes de pedir una exportacion y antes de armarlo.</summary>
    public async Task<int> ContarAsync(FiltroReporte filtro, DateOnly hoy, CancellationToken ct)
    {
        var f = filtro.Indicadores;
        var periodo = PeriodoDe(filtro, hoy);
        return filtro.Reporte switch
        {
            TiposReporte.Operativo => await consultas.ContarPedidosOperativosAsync(f, periodo, ct) + await consultas.ContarEntregasAsync(f, periodo, ct)
                + await consultas.ContarSolicitudesAsync(f, periodo, ct),
            TiposReporte.Gerencial => await consultas.ContarEntregadosAsync(f, periodo, ct),
            _ => await consultas.ContarPedidosFiltradosAsync(f, periodo, f.AlcanceSaldo == "vigente", ct)
        };
    }

    /// <exception cref="ReporteDemasiadoGrandeException">El reporte pasa del tope de filas: se rechaza entero, no a medias.</exception>
    public async Task<DocumentoReporte> ConstruirAsync(FiltroReporte filtro, DateOnly hoy, DateTimeOffset emitido, CancellationToken ct)
    {
        if (ReglasReportes.ExcedeElTope(await ContarAsync(filtro, hoy, ct), Maximo))
            throw new ReporteDemasiadoGrandeException(ReglasReportes.MensajeDelTope(Maximo));

        return filtro.Reporte switch
        {
            TiposReporte.Operativo => await OperativoAsync(filtro, hoy, emitido, ct),
            TiposReporte.Gerencial => await GerencialAsync(filtro, hoy, emitido, ct),
            _ => await ParametrizableAsync(filtro, hoy, emitido, ct)
        };
    }

    // ── Operativo (RFU24, S18): pedidos, entregas y solicitudes ──

    private async Task<DocumentoReporte> OperativoAsync(FiltroReporte filtro, DateOnly hoy, DateTimeOffset emitido, CancellationToken ct)
    {
        var f = filtro.Indicadores;
        var periodo = PeriodoDe(filtro, hoy);
        var pedidos = await consultas.PedidosOperativosAsync(f, periodo, ct);
        var entregas = await consultas.EntregasAsync(f, periodo, ct);
        var solicitudes = await consultas.SolicitudesAsync(f, periodo, ct);

        var tablas = new List<TablaReporte>
        {
            new("pedidos", "Pedidos", ColumnasDePedidos(), pedidos.Select(FilaDePedido).ToList(),
                Principal: true, Exclusion: new ExclusionDeTotales(4, EtiquetasDeEstado.Pedido("anulado")),
                Nota: "Los pedidos del período, también los anulados, que se muestran marcados y no suman en los totales."),
            new("entregas", "Entregas", [
                    new ColumnaReporte("id", "Pedido N.º", TiposColumna.Entero),
                    new ColumnaReporte("cliente", "Cliente", TiposColumna.Texto),
                    new ColumnaReporte("estimada", "Entrega estimada", TiposColumna.Fecha),
                    new ColumnaReporte("real", "Entrega real", TiposColumna.Fecha),
                    new ColumnaReporte("aTiempo", "A tiempo", TiposColumna.Texto)],
                entregas.Select(e => new object?[] { e.IdPedido, e.Cliente, e.Estimada, e.Real, e.Real <= e.Estimada ? "Sí" : "No" }).ToList(),
                Nota: "Pedidos entregados dentro del período, por su fecha de entrega real. «A tiempo»: se entregó el día estimado o antes."),
            new("solicitudes", "Solicitudes", [
                    new ColumnaReporte("id", "Solicitud N.º", TiposColumna.Entero),
                    new ColumnaReporte("fecha", "Fecha", TiposColumna.Fecha),
                    new ColumnaReporte("cliente", "Cliente", TiposColumna.Texto),
                    new ColumnaReporte("origen", "Origen", TiposColumna.Texto),
                    new ColumnaReporte("estado", "Estado", TiposColumna.Texto)],
                solicitudes.Select(s => new object?[] { s.IdSolicitud, s.Fecha, s.Cliente, EtiquetasDeEstado.Origen(s.Origen), EtiquetasDeEstado.Solicitud(s.Estado) }).ToList(),
                Nota: "Las solicitudes del período; solo las filtra el cliente (el estado del pedido y el de pago son de los pedidos).")
        };
        return new DocumentoReporte(TiposReporte.Operativo, "Reporte operativo", emitido, ReglasReportes.DescribirFiltros(filtro, periodo, null), tablas, [], []);
    }

    // ── Parametrizable (RFU25, RFU36, S24): pedidos filtrados; con «por cobrar» es la cobranza ──

    private async Task<DocumentoReporte> ParametrizableAsync(FiltroReporte filtro, DateOnly hoy, DateTimeOffset emitido, CancellationToken ct)
    {
        var f = filtro.Indicadores;
        var periodo = PeriodoDe(filtro, hoy);
        var porCobrar = f.EstadosPago.SequenceEqual(ReglasIndicadores.PorCobrar);
        var pedidos = await consultas.PedidosFiltradosAsync(f, periodo, f.AlcanceSaldo == "vigente", ct);

        var notas = new List<string> { "Los pedidos anulados no se incluyen." };
        if (porCobrar)
            notas.Add("Listado de cobranza: pedidos con pago «No pagado» o «Abonado». El saldo es lo que falta por cobrar.");
        var tabla = new TablaReporte("pedidos", porCobrar ? "Pedidos por cobrar" : "Pedidos", ColumnasDePedidos(), pedidos.Select(FilaDePedido).ToList(), Principal: true);
        return new DocumentoReporte(TiposReporte.Parametrizable, "Reporte parametrizable", emitido, ReglasReportes.DescribirFiltros(filtro, periodo, null), [tabla], [], notas);
    }

    // ── Gerencial (RFU25, S18): ingresos, costos y margen ──

    private async Task<DocumentoReporte> GerencialAsync(FiltroReporte filtro, DateOnly hoy, DateTimeOffset emitido, CancellationToken ct)
    {
        var f = filtro.Indicadores;
        var periodo = PeriodoDe(filtro, hoy);
        var comparacion = ReglasIndicadores.PeriodoComparacion(periodo, f.Comparar);
        var actual = await consultas.CifrasAsync(f, periodo, ct);
        var previa = comparacion is null ? null : await consultas.CifrasAsync(f, comparacion, ct);
        var entregados = await consultas.EntregadosAsync(f, periodo, ct);
        var tipos = await consultas.TiposDeLetreroAsync(f, periodo, ct);
        var (tramos, graficoTramos) = await TramosAsync(f, periodo, ct);
        var (tiposTabla, graficoTipos) = TiposTabla(tipos);

        var tablas = new List<TablaReporte> { Resumen(actual, previa), tramos, tiposTabla, DetalleGerencial(entregados) };
        var graficos = new List<GraficoReporte> { graficoTramos, graficoTipos };

        var sinCosto = actual.Entregados - actual.ConCosto;
        var margen = ReglasGerencial.MargenPorcentaje(actual.IngresosConCosto, actual.Materiales, actual.Otros);
        var notas = new List<string>
        {
            "Ingresos: pedidos entregados con fecha de entrega real dentro del período (la misma definición del panel de gestión). Los pedidos anulados no cuentan.",
            "Costos: los de la cotización aceptada de cada pedido (materiales, mano de obra y otros costos). Un pedido sin cotización aparece como «Sin costo» y no entra en el margen. Margen = ingresos de los pedidos con costo − costo de materiales − otros costos (la ganancia bruta; los otros costos son opcionales: sin ellos, no restan nada); la mano de obra se muestra, pero no se descuenta.",
            actual.Entregados == 0
                ? "No hubo pedidos entregados en el período."
                : $"De {actual.Entregados} pedido(s) entregado(s), {actual.ConCosto} tiene(n) costo registrado y {sinCosto} no. "
                  + (margen is decimal m ? $"El margen (ingresos − materiales − otros costos) es {FormatoReporte.Porcentaje(m)} de los ingresos de los pedidos con costo." : "No hay pedidos con costo: no se calcula margen.")
        };
        return new DocumentoReporte(TiposReporte.Gerencial, "Reporte gerencial", emitido, ReglasReportes.DescribirFiltros(filtro, periodo, comparacion), tablas, graficos, notas);
    }

    private static TablaReporte Resumen(CifrasGerenciales actual, CifrasGerenciales? previa)
    {
        (string Concepto, decimal Valor, decimal? Anterior)[] lineas =
        [
            ("Ingresos (pedidos entregados)", actual.Ingresos, previa?.Ingresos),
            ("Ingresos de los pedidos con costo", actual.IngresosConCosto, previa?.IngresosConCosto),
            ("Costo de materiales", actual.Materiales, previa?.Materiales),
            ("Costo de mano de obra", actual.ManoObra, previa?.ManoObra),
            ("Otros costos", actual.Otros, previa?.Otros),
            ("Costos totales", actual.Costos, previa?.Costos),
            ("Margen (ingresos con costo − materiales − otros costos)", ReglasGerencial.Margen(actual.IngresosConCosto, actual.Materiales, actual.Otros),
                previa is null ? null : ReglasGerencial.Margen(previa.IngresosConCosto, previa.Materiales, previa.Otros))
        ];
        var columnas = new List<ColumnaReporte> { new("concepto", "Concepto", TiposColumna.Texto), new("actual", "Período", TiposColumna.Pesos) };
        if (previa is not null)
        {
            columnas.Add(new("anterior", "Período de comparación", TiposColumna.Pesos));
            columnas.Add(new("variacion", "Variación", TiposColumna.Porcentaje, TextoNulo: "—"));
        }
        var filas = lineas.Select(l => previa is null
            ? new object?[] { l.Concepto, l.Valor }
            : [l.Concepto, l.Valor, l.Anterior, ReglasIndicadores.VariacionPorcentual(l.Valor, l.Anterior)]).ToList();
        return new TablaReporte("resumen", "Resumen del período", columnas, filas);
    }

    private async Task<(TablaReporte Tabla, GraficoReporte Grafico)> TramosAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct)
    {
        var granularidad = ReglasIndicadores.Granularidad(periodo);
        var tramos = ReglasIndicadores.Tramos(periodo, granularidad);
        var ingresos = new decimal[tramos.Count];
        var costos = new decimal[tramos.Count];
        foreach (var dia in await consultas.PorDiaAsync(f, periodo, ct))
        {
            var i = Math.Clamp(ReglasIndicadores.IndiceDeTramo(periodo, granularidad, dia.Dia), 0, tramos.Count - 1);
            ingresos[i] += dia.Ingresos;
            costos[i] += dia.Costos;
        }
        var grafico = new GraficoReporte("tramos", "Ingresos y costos por tramo", "barras", tramos.Select(t => t.Etiqueta).ToList(),
            [new SerieGrafico("Ingresos", ingresos), new SerieGrafico("Costos", costos)]);
        var tabla = new TablaReporte("tramos", "Ingresos y costos por tramo", [
                new ColumnaReporte("tramo", "Tramo", TiposColumna.Texto),
                new ColumnaReporte("desde", "Desde", TiposColumna.Fecha),
                new ColumnaReporte("hasta", "Hasta", TiposColumna.Fecha),
                new ColumnaReporte("ingresos", "Ingresos", TiposColumna.Pesos, Suma: true),
                new ColumnaReporte("costos", "Costos", TiposColumna.Pesos, Suma: true)],
            tramos.Select((t, i) => new object?[] { t.Etiqueta, t.Desde, t.Hasta, ingresos[i], costos[i] }).ToList(),
            Nota: $"Agrupado por {granularidad} (según la extensión del período).");
        return (tabla, grafico);
    }

    private static (TablaReporte Tabla, GraficoReporte Grafico) TiposTabla(IReadOnlyList<FilaDemanda> tipos)
    {
        var principales = tipos.Take(12).ToList();
        var grafico = new GraficoReporte("tipos", "Ingresos por tipo de letrero", "barras-horizontales", principales.Select(t => t.Nombre).ToList(),
            [new SerieGrafico("Ingresos", principales.Select(t => t.Monto).ToList())]);
        var tabla = new TablaReporte("tipos", "Ingresos por tipo de letrero", [
                new ColumnaReporte("tipo", "Tipo de letrero", TiposColumna.Texto),
                new ColumnaReporte("ingresos", "Ingresos", TiposColumna.Pesos, Suma: true),
                new ColumnaReporte("unidades", "Unidades", TiposColumna.Entero, Suma: true),
                new ColumnaReporte("pedidos", "Pedidos", TiposColumna.Entero)],
            tipos.Select(t => new object?[] { t.Nombre, t.Monto, t.Cantidad, t.Pedidos }).ToList(),
            Nota: "El monto de cada pedido se reparte entre sus ítems según su cantidad; la suma es el total de ingresos.");
        return (tabla, grafico);
    }

    private static TablaReporte DetalleGerencial(IReadOnlyList<FilaEntregadoGerencial> entregados) =>
        new("pedidos", "Pedidos entregados", [
                new ColumnaReporte("id", "Pedido N.º", TiposColumna.Entero),
                new ColumnaReporte("cliente", "Cliente", TiposColumna.Texto),
                new ColumnaReporte("entrega", "Entrega real", TiposColumna.Fecha),
                new ColumnaReporte("ingreso", "Ingreso", TiposColumna.Pesos, Suma: true),
                new ColumnaReporte("costo", "Costo total", TiposColumna.Pesos, Suma: true, TextoNulo: "Sin costo"),
                new ColumnaReporte("materiales", "Materiales", TiposColumna.Pesos, Suma: true, TextoNulo: "Sin costo"),
                new ColumnaReporte("otros", "Otros costos", TiposColumna.Pesos, Suma: true, TextoNulo: "Sin costo"),
                new ColumnaReporte("margen", "Margen", TiposColumna.Pesos, Suma: true, TextoNulo: "Sin costo")],
            entregados.Select(e =>
            {
                var costo = ReglasGerencial.CostoDe(e.Materiales is not null, e.Materiales ?? 0, e.ManoObra ?? 0, e.Otros ?? 0);
                // Materiales y otros costos son los dos que se descuentan: se muestran juntos para que el margen se pueda verificar a ojo.
                return new object?[] { e.IdPedido, e.Cliente, e.Entrega, e.Ingreso, costo?.Total, costo?.Materiales, costo?.Otros, costo is null ? null : ReglasGerencial.Margen(e.Ingreso, costo.Materiales, costo.Otros) };
            }).ToList(), Principal: true);

    // ── Piezas comunes ──

    private static IReadOnlyList<ColumnaReporte> ColumnasDePedidos() =>
    [
        new("id", "Pedido N.º", TiposColumna.Entero),
        new("cliente", "Cliente", TiposColumna.Texto),
        new("fechaPedido", "Fecha del pedido", TiposColumna.Fecha),
        new("entregaEstimada", "Entrega estimada", TiposColumna.Fecha),
        new("estadoPedido", "Estado del pedido", TiposColumna.Texto),
        new("estadoPago", "Estado de pago", TiposColumna.Texto),
        new("monto", "Monto", TiposColumna.Pesos, Suma: true),
        new("abonado", "Abonado", TiposColumna.Pesos, Suma: true),
        new("saldo", "Saldo", TiposColumna.Pesos, Suma: true)
    ];

    private static object?[] FilaDePedido(FilaPedidoReporte p) =>
        [p.IdPedido, p.Cliente, p.FechaPedido, p.FechaEntregaEstimada, EtiquetasDeEstado.Pedido(p.EstadoPedido), EtiquetasDeEstado.Pago(p.EstadoPago), p.Monto, p.Abonado, p.Saldo];
}
