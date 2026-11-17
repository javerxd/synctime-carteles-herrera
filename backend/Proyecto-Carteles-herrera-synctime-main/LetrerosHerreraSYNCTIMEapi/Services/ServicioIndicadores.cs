using System.Text.Json.Serialization;
using LetrerosHerreraSYNCTIMEapi.Datos.Indicadores;
using LetrerosHerreraSYNCTIMEapi.Negocio.Indicadores;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// Fase 12B (RFS09, RFU19, RFU27, S10 a S14, S19, S24): arma las respuestas del dashboard a partir de las consultas
// agregadas y las formulas de ReglasIndicadores. El contrato esta en docs/plan/fase-12-especificacion.md.

public sealed record TarjetaIndicador(string Clave, string Unidad, decimal? Valor, decimal? Anterior, decimal? Variacion, string TipoVariacion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Detalle = null);

public sealed record SaldoPorCobrar(string Alcance, decimal Valor, int Cantidad, decimal? Anterior, decimal? Variacion);

public sealed record RespuestaResumen(Periodo Periodo, Periodo? Comparacion, IReadOnlyList<TarjetaIndicador> Tarjetas, SaldoPorCobrar Saldo);

public sealed record TramoEvolucion(DateOnly Desde, DateOnly Hasta, string Etiqueta, decimal Ingresos, int Pedidos, decimal? IngresosComparacion, int? PedidosComparacion);

public sealed record RespuestaEvolucion(Periodo Periodo, Periodo? Comparacion, string Granularidad, IReadOnlyList<TramoEvolucion> Serie);

public sealed record RespuestaCobranza(Periodo Periodo, string Alcance, IReadOnlyList<SegmentoCobranza> Segmentos, decimal SaldoTotal, int CantidadPorCobrar);

public sealed record PasoDelEmbudo(string Clave, int Cantidad, decimal? Porcentaje, decimal? PorcentajePaso);

public sealed record RespuestaEmbudo(Periodo Periodo, IReadOnlyList<PasoDelEmbudo> Pasos);

public sealed record CategoriaDemanda(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? IdCategoria,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? IdProducto,
    string Nombre, decimal Monto, int Cantidad, int Pedidos);

public sealed record RespuestaDemanda(Periodo Periodo, string Agrupar, IReadOnlyList<CategoriaDemanda> Categorias);

public sealed record ClientePorCobrar(int IdCliente, string Nombre);

public sealed record PedidoPorCobrar(
    int IdPedido, ClientePorCobrar Cliente, DateOnly FechaPedido, DateOnly FechaEntregaEstimada, int DiasDesdePedido, string EstadoPedido,
    string EstadoPago, int MontoTotalFinal, int MontoAbonado, int SaldoPendiente, bool ListoSinPagar);

public sealed record RespuestaPorCobrar(
    Periodo Periodo, string Alcance, IReadOnlyList<PedidoPorCobrar> Elementos, int Total, int Pagina, int Tamano, int TotalPaginas, decimal TotalSaldo);

public sealed class ServicioIndicadores(ConsultasIndicadores consultas, TimeProvider reloj)
{
    private DateOnly Hoy() => TiempoChile.Hoy(reloj);

    private (Periodo Periodo, Periodo? Comparacion) Periodos(FiltroIndicadores f)
    {
        var periodo = ReglasIndicadores.ResolverPeriodo(f, Hoy());
        return (periodo, ReglasIndicadores.PeriodoComparacion(periodo, f.Comparar));
    }

    private static TarjetaIndicador Tarjeta(string clave, string unidad, string tipo, decimal? valor, decimal? anterior, bool hayComparacion, object? detalle = null) =>
        new(clave, unidad, valor, hayComparacion ? anterior : null,
            !hayComparacion ? null : tipo == "puntos" ? ReglasIndicadores.VariacionPuntos(valor, anterior) : ReglasIndicadores.VariacionPorcentual(valor, anterior),
            tipo, detalle);

    // ── Resumen: las siete tarjetas y el saldo ──

    public async Task<RespuestaResumen> ResumenAsync(FiltroIndicadores f, CancellationToken ct)
    {
        var (periodo, comparacion) = Periodos(f);
        var actual = await consultas.MedirAsync(f, periodo, ct);
        var previo = comparacion is null ? null : await consultas.MedirAsync(f, comparacion, ct);
        var hay = previo is not null;

        decimal? Conversion(MedicionPeriodo m) => ReglasIndicadores.Porcentaje(m.Concretadas, m.SolicitudesEnLinea);
        decimal? Ticket(MedicionPeriodo m) => ReglasIndicadores.TicketPromedio(m.Ingresos, m.Entregados);
        decimal? ATiempo(MedicionPeriodo m) => ReglasIndicadores.Porcentaje(m.EntregadosATiempo, m.Entregados);

        var porEstado = ReglasIndicadores.EstadosPedido.Select(e => new { estado = e, cantidad = actual.PedidosPorEstado.GetValueOrDefault(e) }).ToList();
        var tarjetas = new List<TarjetaIndicador>
        {
            Tarjeta("ingresos", "pesos", "porcentaje", actual.Ingresos, previo?.Ingresos, hay),
            Tarjeta("pedidos", "cantidad", "porcentaje", actual.Pedidos, previo?.Pedidos, hay, new { porEstado }),
            Tarjeta("origen", "cantidad", "porcentaje", actual.SolicitudesEnLinea, previo?.SolicitudesEnLinea, hay, new
            {
                presenciales = actual.SolicitudesPresenciales,
                porcentajeEnLinea = ReglasIndicadores.Porcentaje(actual.SolicitudesEnLinea, actual.SolicitudesEnLinea + actual.SolicitudesPresenciales)
            }),
            Tarjeta("concretadas", "cantidad", "porcentaje", actual.Concretadas, previo?.Concretadas, hay, new { solicitudesEnLinea = actual.SolicitudesEnLinea }),
            Tarjeta("conversion", "porcentaje", "puntos", Conversion(actual), previo is null ? null : Conversion(previo), hay),
            Tarjeta("ticketPromedio", "pesos", "porcentaje", Ticket(actual), previo is null ? null : Ticket(previo), hay),
            Tarjeta("entregasATiempo", "porcentaje", "puntos", ATiempo(actual), previo is null ? null : ATiempo(previo), hay)
        };

        SaldoPorCobrar saldo;
        if (f.AlcanceSaldo == "vigente")
        {
            var (suma, cantidad) = await consultas.SaldoVigenteAsync(f, ct);
            saldo = new SaldoPorCobrar("vigente", suma, cantidad, null, null);
        }
        else
        {
            saldo = new SaldoPorCobrar("periodo", actual.SaldoPorCobrar, actual.CantidadPorCobrar, previo?.SaldoPorCobrar,
                hay ? ReglasIndicadores.VariacionPorcentual(actual.SaldoPorCobrar, previo!.SaldoPorCobrar) : null);
        }
        return new RespuestaResumen(periodo, comparacion, tarjetas, saldo);
    }

    // ── Evolucion ──

    public async Task<RespuestaEvolucion> EvolucionAsync(FiltroIndicadores f, CancellationToken ct)
    {
        var (periodo, comparacion) = Periodos(f);
        var granularidad = ReglasIndicadores.Granularidad(periodo);

        // Los dias de cada periodo se reparten en sus tramos; el de comparacion se alinea por posicion.
        async Task<(IReadOnlyList<Tramo> Tramos, decimal[] Ingresos, int[] Pedidos)> SerieAsync(Periodo p)
        {
            var tramos = ReglasIndicadores.Tramos(p, granularidad);
            var (ingresos, pedidos) = await consultas.PorDiaAsync(f, p, ct);
            var sumaIngresos = new decimal[tramos.Count];
            var sumaPedidos = new int[tramos.Count];
            foreach (var (dia, monto) in ingresos)
                sumaIngresos[Math.Clamp(ReglasIndicadores.IndiceDeTramo(p, granularidad, dia), 0, tramos.Count - 1)] += monto;
            foreach (var (dia, cantidad) in pedidos)
                sumaPedidos[Math.Clamp(ReglasIndicadores.IndiceDeTramo(p, granularidad, dia), 0, tramos.Count - 1)] += cantidad;
            return (tramos, sumaIngresos, sumaPedidos);
        }

        var actual = await SerieAsync(periodo);
        (IReadOnlyList<Tramo> Tramos, decimal[] Ingresos, int[] Pedidos)? previa = comparacion is null ? null : await SerieAsync(comparacion);
        var serie = actual.Tramos.Select((t, i) => new TramoEvolucion(t.Desde, t.Hasta, t.Etiqueta, actual.Ingresos[i], actual.Pedidos[i],
            previa is not null && i < previa.Value.Tramos.Count ? previa.Value.Ingresos[i] : null,
            previa is not null && i < previa.Value.Tramos.Count ? previa.Value.Pedidos[i] : null)).ToList();
        return new RespuestaEvolucion(periodo, comparacion, granularidad, serie);
    }

    // ── Cobranza ──

    public async Task<RespuestaCobranza> CobranzaAsync(FiltroIndicadores f, CancellationToken ct)
    {
        var (periodo, _) = Periodos(f);
        var vigente = f.AlcanceSaldo == "vigente";
        var segmentos = await consultas.CobranzaAsync(f, periodo, vigente, ct);
        var deuda = segmentos.Where(s => s.EstadoPago != "pagado_total").ToList();
        return new RespuestaCobranza(periodo, vigente ? "vigente" : "periodo", segmentos, deuda.Sum(s => s.Saldo), deuda.Sum(s => s.Cantidad));
    }

    // ── Embudo ──

    public async Task<RespuestaEmbudo> EmbudoAsync(FiltroIndicadores f, CancellationToken ct)
    {
        var (periodo, _) = Periodos(f);
        var pasos = await consultas.EmbudoAsync(f, periodo, ct);
        var baseTotal = pasos[0].Cantidad;
        return new RespuestaEmbudo(periodo, pasos.Select((p, i) => new PasoDelEmbudo(p.Clave, p.Cantidad,
            ReglasIndicadores.Porcentaje(p.Cantidad, baseTotal), i == 0 ? null : ReglasIndicadores.Porcentaje(p.Cantidad, pasos[i - 1].Cantidad))).ToList());
    }

    // ── Demanda ──

    public async Task<RespuestaDemanda> DemandaAsync(FiltroIndicadores f, CancellationToken ct)
    {
        var (periodo, _) = Periodos(f);
        var filas = await consultas.DemandaAsync(f, periodo, ct);
        var porProducto = f.Agrupar == "producto";
        return new RespuestaDemanda(periodo, f.Agrupar, filas.Select(x => new CategoriaDemanda(
            porProducto ? null : x.Id, porProducto ? x.Id : null, x.Nombre, x.Monto, x.Cantidad, x.Pedidos)).ToList());
    }

    // ── Pedidos por cobrar ──

    public async Task<RespuestaPorCobrar> PorCobrarAsync(FiltroIndicadores f, CancellationToken ct)
    {
        var (periodo, _) = Periodos(f);
        var vigente = f.AlcanceSaldo == "vigente";
        var pagina = await consultas.PorCobrarAsync(f, periodo, vigente, ct);
        var hoy = Hoy();
        var totalPaginas = (int)Math.Ceiling(pagina.Total / (double)f.Tamano);
        return new RespuestaPorCobrar(periodo, vigente ? "vigente" : "periodo",
            pagina.Filas.Select(p => new PedidoPorCobrar(p.IdPedido, new ClientePorCobrar(p.IdCliente, p.Cliente), p.FechaPedido, p.FechaEntregaEstimada,
                hoy.DayNumber - p.FechaPedido.DayNumber, p.EstadoPedido, p.EstadoPago, p.MontoTotalFinal, p.MontoAbonado, p.SaldoPendiente,
                // Terminado o entregado y sin ningun pago: el trabajo salio del taller y no se ha cobrado.
                (p.EstadoPedido is "terminado" or "entregado") && p.EstadoPago == "no_pagado")).ToList(),
            pagina.Total, Math.Min(f.Pagina, Math.Max(totalPaginas, 1)), f.Tamano, totalPaginas, pagina.TotalSaldo);
    }
}
