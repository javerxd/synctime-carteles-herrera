using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Indicadores;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Datos.Indicadores;

// Fase 12B (RNFO11, RNFP26): las consultas del dashboard. Cada una es UNA consulta agregada (GROUP BY / SUM / COUNT) sobre
// las vistas vw_indicadores_*: nunca se traen pedidos a memoria para sumarlos aqui. Los pedidos anulados nunca cuentan.

public sealed record MedicionPeriodo(
    decimal Ingresos, int Entregados, int EntregadosATiempo,
    int Pedidos, IReadOnlyDictionary<string, int> PedidosPorEstado,
    int SolicitudesEnLinea, int SolicitudesPresenciales, int Concretadas,
    decimal SaldoPorCobrar, int CantidadPorCobrar);

public sealed record SegmentoCobranza(string EstadoPago, int Cantidad, decimal Saldo);

public sealed record PasoEmbudo(string Clave, int Cantidad);

public sealed record FilaDemanda(int Id, string Nombre, decimal Monto, int Cantidad, int Pedidos);

public sealed record FilaPorCobrar(
    int IdPedido, int IdCliente, string Cliente, DateOnly FechaPedido, DateOnly FechaEntregaEstimada, string EstadoPedido, string EstadoPago,
    int MontoTotalFinal, int MontoAbonado, int SaldoPendiente);

public sealed record PaginaPorCobrar(IReadOnlyList<FilaPorCobrar> Filas, int Total, decimal TotalSaldo);

public sealed class ConsultasIndicadores(LetrerosHerreradbContext db)
{
    // ── Filtros combinables ──

    /// <summary>Pedidos vigentes y no anulados que cumplen los filtros (no las fechas: esas las pone cada indicador).</summary>
    private IQueryable<VistaIndicadorPedido> Pedidos(FiltroIndicadores f)
    {
        var consulta = db.IndicadoresPedidos.AsNoTracking().Where(p => p.EstadoPedido != "anulado");
        if (f.EstadosPedido.Count > 0)
        {
            var estados = f.EstadosPedido.ToList();
            consulta = consulta.Where(p => estados.Contains(p.EstadoPedido));
        }
        if (f.EstadosPago.Count > 0)
        {
            var pagos = f.EstadosPago.ToList();
            consulta = consulta.Where(p => pagos.Contains(p.EstadoPago));
        }
        if (f.Origen is { } origen)
            consulta = consulta.Where(p => p.OrigenSolicitud == origen);
        if (f.Cliente is int cliente)
            consulta = consulta.Where(p => p.IdCliente == cliente);
        // Un pedido entra si alguno de sus items es de la categoria.
        if (f.Categoria is int categoria)
            consulta = consulta.Where(p => db.IndicadoresItems.Any(i => i.IdPedido == p.IdPedido && i.IdCategoria == categoria));
        return consulta;
    }

    /// <summary>
    /// Solicitudes vigentes. Las filtran categoria (la del producto principal), cliente y origen; el estado del pedido
    /// y el del pago no, porque son de los pedidos.
    /// </summary>
    private IQueryable<VistaIndicadorSolicitud> Solicitudes(FiltroIndicadores f)
    {
        var consulta = db.IndicadoresSolicitudes.AsNoTracking();
        if (f.Origen is { } origen)
            consulta = consulta.Where(s => s.Origen == origen);
        if (f.Cliente is int cliente)
            consulta = consulta.Where(s => s.IdCliente == cliente);
        if (f.Categoria is int categoria)
            consulta = consulta.Where(s => s.IdCategoriaPrincipal == categoria);
        return consulta;
    }

    private static IQueryable<VistaIndicadorPedido> CreadosEn(IQueryable<VistaIndicadorPedido> pedidos, Periodo p) =>
        pedidos.Where(x => x.FechaPedido >= p.Desde && x.FechaPedido <= p.Hasta);

    private static IQueryable<VistaIndicadorPedido> EntregadosEn(IQueryable<VistaIndicadorPedido> pedidos, Periodo p) =>
        pedidos.Where(x => x.EstadoPedido == "entregado" && x.FechaEntregaReal >= p.Desde && x.FechaEntregaReal <= p.Hasta);

    // ── Resumen ──

    /// <summary>Las cifras de un periodo: ingresos, pedidos por estado, solicitudes, concretadas, entregas y saldo del periodo.</summary>
    public async Task<MedicionPeriodo> MedirAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct)
    {
        var pedidos = Pedidos(f);

        var porEstado = await CreadosEn(pedidos, periodo).GroupBy(p => p.EstadoPedido)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() }).ToListAsync(ct);

        var entregas = await EntregadosEn(pedidos, periodo).GroupBy(_ => 1)
            .Select(g => new
            {
                Cantidad = g.Count(),
                Suma = g.Sum(p => (decimal)p.MontoTotalFinal),
                ATiempo = g.Count(p => p.FechaEntregaReal <= p.FechaEntregaEstimada)
            }).FirstOrDefaultAsync(ct);

        var solicitudes = await Solicitudes(f).Where(s => s.FechaSolicitud >= periodo.Desde && s.FechaSolicitud <= periodo.Hasta)
            .GroupBy(s => s.Origen)
            .Select(g => new { Origen = g.Key, Cantidad = g.Count(), Concretadas = g.Count(s => s.Concretada) }).ToListAsync(ct);

        var saldo = await CreadosEn(pedidos, periodo).Where(p => p.EstadoPago != "pagado_total").GroupBy(_ => 1)
            .Select(g => new { Cantidad = g.Count(), Suma = g.Sum(p => (decimal)p.SaldoPendiente) }).FirstOrDefaultAsync(ct);

        var enLinea = solicitudes.FirstOrDefault(s => s.Origen == "online");
        return new MedicionPeriodo(
            entregas?.Suma ?? 0, entregas?.Cantidad ?? 0, entregas?.ATiempo ?? 0,
            porEstado.Sum(e => e.Cantidad), porEstado.ToDictionary(e => e.Estado, e => e.Cantidad),
            enLinea?.Cantidad ?? 0, solicitudes.FirstOrDefault(s => s.Origen == "presencial")?.Cantidad ?? 0, enLinea?.Concretadas ?? 0,
            saldo?.Suma ?? 0, saldo?.Cantidad ?? 0);
    }

    /// <summary>Toda la deuda vigente del taller (pedidos no anulados en «no pagado» o «abonado»), sin limite de fecha.</summary>
    public async Task<(decimal Saldo, int Cantidad)> SaldoVigenteAsync(FiltroIndicadores f, CancellationToken ct)
    {
        var saldo = await Pedidos(f).Where(p => p.EstadoPago != "pagado_total").GroupBy(_ => 1)
            .Select(g => new { Cantidad = g.Count(), Suma = g.Sum(p => (decimal)p.SaldoPendiente) }).FirstOrDefaultAsync(ct);
        return (saldo?.Suma ?? 0, saldo?.Cantidad ?? 0);
    }

    // ── Evolucion ──

    /// <summary>Ingresos y pedidos por DIA del periodo (la API los agrupa en tramos): una fila por dia con movimiento.</summary>
    public async Task<(IReadOnlyDictionary<DateOnly, decimal> Ingresos, IReadOnlyDictionary<DateOnly, int> Pedidos)> PorDiaAsync(
        FiltroIndicadores f, Periodo periodo, CancellationToken ct)
    {
        var pedidos = Pedidos(f);
        var ingresos = await EntregadosEn(pedidos, periodo).GroupBy(p => p.FechaEntregaReal!.Value)
            .Select(g => new { Dia = g.Key, Suma = g.Sum(p => (decimal)p.MontoTotalFinal) }).ToListAsync(ct);
        var creados = await CreadosEn(pedidos, periodo).GroupBy(p => p.FechaPedido)
            .Select(g => new { Dia = g.Key, Cantidad = g.Count() }).ToListAsync(ct);
        return (ingresos.ToDictionary(i => i.Dia, i => i.Suma), creados.ToDictionary(c => c.Dia, c => c.Cantidad));
    }

    // ── Cobranza ──

    /// <summary>Pedidos y saldo por estado de pago. Con `vigente`, sin limite de fecha.</summary>
    public async Task<IReadOnlyList<SegmentoCobranza>> CobranzaAsync(FiltroIndicadores f, Periodo periodo, bool vigente, CancellationToken ct)
    {
        var pedidos = vigente ? Pedidos(f) : CreadosEn(Pedidos(f), periodo);
        var filas = await pedidos.GroupBy(p => p.EstadoPago)
            .Select(g => new SegmentoCobranza(g.Key, g.Count(), g.Sum(p => (decimal)p.SaldoPendiente))).ToListAsync(ct);
        // Siempre los tres, en este orden, aunque no tengan pedidos.
        return ReglasIndicadores.EstadosPago.Select(e => filas.FirstOrDefault(s => s.EstadoPago == e) ?? new SegmentoCobranza(e, 0, 0)).ToList();
    }

    // ── Embudo ──

    /// <summary>Solicitudes en linea del periodo → cotizadas → aprobadas → con un pedido no anulado.</summary>
    public async Task<IReadOnlyList<PasoEmbudo>> EmbudoAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct)
    {
        // El embudo es del canal web: con «presencial» no hay solicitudes en linea que seguir.
        if (f.Origen == "presencial")
            return [new("solicitudes", 0), new("cotizadas", 0), new("aprobadas", 0), new("concretadas", 0)];
        var cifras = await Solicitudes(f).Where(s => s.Origen == "online" && s.FechaSolicitud >= periodo.Desde && s.FechaSolicitud <= periodo.Hasta)
            .GroupBy(_ => 1)
            // Acumulativo: una solicitud con pedido (por ejemplo, convertida directo por el taller) tambien paso por los pasos anteriores.
            .Select(g => new
            {
                Total = g.Count(),
                Cotizadas = g.Count(s => s.Cotizada || s.Aprobada || s.Concretada),
                Aprobadas = g.Count(s => s.Aprobada || s.Concretada),
                Concretadas = g.Count(s => s.Concretada)
            })
            .FirstOrDefaultAsync(ct);
        return [new("solicitudes", cifras?.Total ?? 0), new("cotizadas", cifras?.Cotizadas ?? 0), new("aprobadas", cifras?.Aprobadas ?? 0), new("concretadas", cifras?.Concretadas ?? 0)];
    }

    // ── Demanda ──

    /// <summary>
    /// Ingresos por categoria (o por tipo de letrero): el monto final de cada pedido entregado en el periodo, repartido entre
    /// sus items segun su cantidad (la suma coincide con los ingresos), mas las unidades y los pedidos. De mayor a menor monto.
    /// </summary>
    public async Task<IReadOnlyList<FilaDemanda>> DemandaAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct)
    {
        var entregados = EntregadosEn(Pedidos(f), periodo);
        var items = db.IndicadoresItems.AsNoTracking().Where(i => entregados.Any(p => p.IdPedido == i.IdPedido));
        if (f.Categoria is int categoria)
            items = items.Where(i => i.IdCategoria == categoria);

        var porProducto = f.Agrupar == "producto";
        var filas = await items.GroupBy(i => porProducto ? i.IdProducto : i.IdCategoria)
            .Select(g => new { Id = g.Key, Monto = g.Sum(i => i.MontoAsignado), Cantidad = g.Sum(i => i.Cantidad), Pedidos = g.Select(i => i.IdPedido).Distinct().Count() })
            .ToListAsync(ct);
        var ids = filas.Select(x => x.Id).ToList();
        var nombres = porProducto
            ? await db.Productos.AsNoTracking().Where(p => ids.Contains(p.IdProducto)).ToDictionaryAsync(p => p.IdProducto, p => p.Nombre, ct)
            : await db.Categorias.AsNoTracking().Where(c => ids.Contains(c.IdCategoria)).ToDictionaryAsync(c => c.IdCategoria, c => c.Nombre, ct);
        var cultura = System.Globalization.CultureInfo.GetCultureInfo("es-CL");
        // Cada grupo se redondea al peso, pero la suma debe ser igual a los ingresos: el resto se reparte por el metodo del mayor resto.
        var montos = Repartir(filas.Select(x => x.Monto).ToList());
        return filas.Select((x, i) => new FilaDemanda(x.Id, nombres.GetValueOrDefault(x.Id, $"N.º {x.Id}"), montos[i], x.Cantidad, x.Pedidos))
            .OrderByDescending(x => x.Monto).ThenBy(x => x.Nombre, StringComparer.Create(cultura, true)).ToList();
    }

    /// <summary>Redondea cada monto al peso conservando el total (metodo del mayor resto): la suma de lo devuelto es la suma redondeada de lo recibido.</summary>
    public static IReadOnlyList<decimal> Repartir(IReadOnlyList<decimal> montos)
    {
        var abajo = montos.Select(Math.Floor).ToArray();
        var faltan = (int)(Math.Round(montos.Sum(), 0, MidpointRounding.AwayFromZero) - abajo.Sum());
        foreach (var i in montos.Select((m, i) => (Resto: m - abajo[i], i)).OrderByDescending(x => x.Resto).ThenBy(x => x.i).Take(Math.Max(faltan, 0)).Select(x => x.i))
            abajo[i] += 1;
        return abajo;
    }

    // ── Pedidos por cobrar ──

    /// <summary>Pedidos no anulados en «no pagado» o «abonado», de mayor a menor saldo (a igual saldo, el mas antiguo), paginados en SQL.</summary>
    public async Task<PaginaPorCobrar> PorCobrarAsync(FiltroIndicadores f, Periodo periodo, bool vigente, CancellationToken ct)
    {
        var deuda = (vigente ? Pedidos(f) : CreadosEn(Pedidos(f), periodo)).Where(p => p.EstadoPago != "pagado_total");
        var totales = await deuda.GroupBy(_ => 1).Select(g => new { Total = g.Count(), Suma = g.Sum(p => (decimal)p.SaldoPendiente) }).FirstOrDefaultAsync(ct);
        var total = totales?.Total ?? 0;

        var totalPaginas = (int)Math.Ceiling(total / (double)f.Tamano);
        var pagina = Math.Min(f.Pagina, Math.Max(totalPaginas, 1));   // una pagina fuera de rango devuelve la ultima
        var filas = await (from p in deuda
                           join c in db.Clientes.AsNoTracking() on p.IdCliente equals c.IdCliente
                           orderby p.SaldoPendiente descending, p.FechaPedido, p.IdPedido
                           select new FilaPorCobrar(p.IdPedido, p.IdCliente, (c.Nombre + " " + c.Apellido).Trim(), p.FechaPedido, p.FechaEntregaEstimada,
                               p.EstadoPedido, p.EstadoPago, p.MontoTotalFinal, p.MontoAbonado, p.SaldoPendiente))
            .Skip((pagina - 1) * f.Tamano).Take(f.Tamano).ToListAsync(ct);
        return new PaginaPorCobrar(filas, total, totales?.Suma ?? 0);
    }
}
