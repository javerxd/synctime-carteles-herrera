using LetrerosHerreraSYNCTIMEapi.Datos.Indicadores;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Indicadores;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Datos.Reportes;

// Fase 13A (RFS10, RNFO11, S18, S24): las consultas de los tres reportes. Mismas vistas y MISMA definicion de «pedido vigente» que
// el dashboard (ConsultasIndicadores.Pedidos), para que los numeros de los dos coincidan; proyecciones directas, sin cargar
// entidades completas. Los totales de una tabla se suman sobre sus filas; aqui solo hay filas y cifras agregadas en SQL.

public sealed record FilaPedidoReporte(
    int IdPedido, string Cliente, DateOnly FechaPedido, DateOnly FechaEntregaEstimada, string EstadoPedido, string EstadoPago,
    int Monto, int Abonado, int Saldo);

public sealed record FilaEntregaReporte(int IdPedido, string Cliente, DateOnly Estimada, DateOnly Real);

public sealed record FilaSolicitudReporte(int IdSolicitud, DateOnly Fecha, string Cliente, string Origen, string Estado);

/// <summary>Un pedido entregado con lo que costo. Si no tiene cotizacion, los costos son nulos: no hay costo registrado.</summary>
public sealed record FilaEntregadoGerencial(
    int IdPedido, string Cliente, DateOnly Entrega, int Ingreso, decimal? Materiales, decimal? ManoObra, decimal? Otros);

/// <summary>Las cifras del periodo del gerencial. Los costos son los de los pedidos que tienen cotizacion; los demas no cuentan.</summary>
public sealed record CifrasGerenciales(
    decimal Ingresos, int Entregados, decimal IngresosConCosto, int ConCosto, decimal Materiales, decimal ManoObra, decimal Otros)
{
    public decimal Costos => Materiales + ManoObra + Otros;
}

public sealed record DiaGerencial(DateOnly Dia, decimal Ingresos, decimal Costos);

public sealed class ConsultasReportes(LetrerosHerreradbContext db, ConsultasIndicadores indicadores)
{
    private static DateTime Inicio(DateOnly dia) => dia.ToDateTime(TimeOnly.MinValue);

    // El costo de materiales se redondea al peso en cada pedido (como el monto base del pedido): asi cada fila es un entero y la
    // suma de las filas es el total, sin diferencias de centavos entre la tabla, los tramos y el resumen.
    private IQueryable<LineaEntregada> Entregados(FiltroIndicadores f, Periodo periodo) =>
        from v in ConsultasIndicadores.EntregadosEn(indicadores.Pedidos(f), periodo)
        join p in db.Pedidos.AsNoTracking() on v.IdPedido equals p.IdPedido
        join c in db.Cotizaciones.AsNoTracking() on p.IdCotizacion equals c.IdCotizacion into cotizaciones
        from c in cotizaciones.DefaultIfEmpty()
        select new LineaEntregada
        {
            IdPedido = v.IdPedido,
            IdCliente = v.IdCliente,
            Entrega = v.FechaEntregaReal!.Value,
            Ingreso = v.MontoTotalFinal,
            ConCosto = c != null,
            Materiales = c == null ? 0 : Math.Round(c.CostoMaterialesTotal, 0),
            ManoObra = c == null ? 0 : c.CostoManoObra,
            Otros = c == null ? 0 : c.OtrosCostos
        };

    private sealed class LineaEntregada
    {
        public int IdPedido { get; init; }
        public int IdCliente { get; init; }
        public DateOnly Entrega { get; init; }
        public int Ingreso { get; init; }
        public bool ConCosto { get; init; }
        public decimal Materiales { get; init; }
        public decimal ManoObra { get; init; }
        public decimal Otros { get; init; }
    }

    // ── Operativo ──

    /// <summary>Los pedidos creados en el periodo, con los anulados incluidos (el reporte los muestra marcados).</summary>
    private IQueryable<VistaIndicadorPedido> PedidosOperativos(FiltroIndicadores f, Periodo periodo) =>
        ConsultasIndicadores.CreadosEn(indicadores.Pedidos(f, incluirAnulados: true), periodo);

    public Task<int> ContarPedidosOperativosAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct) =>
        PedidosOperativos(f, periodo).CountAsync(ct);

    public async Task<IReadOnlyList<FilaPedidoReporte>> PedidosOperativosAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct) =>
        await Proyectar(PedidosOperativos(f, periodo)).ToListAsync(ct);

    private IQueryable<FilaPedidoReporte> Proyectar(IQueryable<VistaIndicadorPedido> pedidos) =>
        from p in pedidos
        join c in db.Clientes.AsNoTracking() on p.IdCliente equals c.IdCliente
        orderby p.FechaPedido, p.IdPedido
        select new FilaPedidoReporte(p.IdPedido, (c.Nombre + " " + c.Apellido).Trim(), p.FechaPedido, p.FechaEntregaEstimada,
            p.EstadoPedido, p.EstadoPago, p.MontoTotalFinal, p.MontoAbonado, p.SaldoPendiente);

    private IQueryable<VistaIndicadorPedido> EntregasDelPeriodo(FiltroIndicadores f, Periodo periodo) =>
        ConsultasIndicadores.EntregadosEn(indicadores.Pedidos(f), periodo);

    public Task<int> ContarEntregasAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct) =>
        EntregasDelPeriodo(f, periodo).CountAsync(ct);

    /// <summary>Pedidos entregados en el periodo (por su fecha de entrega real): la misma regla de «entregas a tiempo» del dashboard.</summary>
    public async Task<IReadOnlyList<FilaEntregaReporte>> EntregasAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct) =>
        await (from p in EntregasDelPeriodo(f, periodo)
               join c in db.Clientes.AsNoTracking() on p.IdCliente equals c.IdCliente
               orderby p.FechaEntregaReal, p.IdPedido
               select new FilaEntregaReporte(p.IdPedido, (c.Nombre + " " + c.Apellido).Trim(), p.FechaEntregaEstimada, p.FechaEntregaReal!.Value))
            .ToListAsync(ct);

    // Las solicitudes (no eliminadas) del periodo; solo las filtra el cliente: el estado del pedido y el pago son de los pedidos.
    private IQueryable<Solicitud> SolicitudesDelPeriodo(FiltroIndicadores f, Periodo periodo)
    {
        var desde = Inicio(periodo.Desde);
        var hasta = Inicio(periodo.Hasta.AddDays(1));
        var consulta = db.Solicitudes.AsNoTracking().Where(s => !s.EliminadoLogico && s.FechaSolicitud >= desde && s.FechaSolicitud < hasta);
        if (f.Cliente is int cliente)
            consulta = consulta.Where(s => s.IdCliente == cliente);
        return consulta;
    }

    public Task<int> ContarSolicitudesAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct) =>
        SolicitudesDelPeriodo(f, periodo).CountAsync(ct);

    public async Task<IReadOnlyList<FilaSolicitudReporte>> SolicitudesAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct)
    {
        var filas = await (from s in SolicitudesDelPeriodo(f, periodo)
                           join c in db.Clientes.AsNoTracking() on s.IdCliente equals c.IdCliente
                           orderby s.FechaSolicitud, s.IdSolicitud
                           select new { s.IdSolicitud, s.FechaSolicitud, Cliente = (c.Nombre + " " + c.Apellido).Trim(), s.Origen, s.Estado })
            .ToListAsync(ct);
        return filas.Select(s => new FilaSolicitudReporte(s.IdSolicitud, DateOnly.FromDateTime(s.FechaSolicitud), s.Cliente, s.Origen, s.Estado)).ToList();
    }

    // ── Parametrizable ──

    /// <summary>Los pedidos no anulados que cumplen los filtros. «Vigente» no limita por fecha (la cobranza de toda la deuda).</summary>
    private IQueryable<VistaIndicadorPedido> PedidosFiltrados(FiltroIndicadores f, Periodo periodo, bool vigente) =>
        vigente ? indicadores.Pedidos(f) : ConsultasIndicadores.CreadosEn(indicadores.Pedidos(f), periodo);

    public Task<int> ContarPedidosFiltradosAsync(FiltroIndicadores f, Periodo periodo, bool vigente, CancellationToken ct) =>
        PedidosFiltrados(f, periodo, vigente).CountAsync(ct);

    public async Task<IReadOnlyList<FilaPedidoReporte>> PedidosFiltradosAsync(FiltroIndicadores f, Periodo periodo, bool vigente, CancellationToken ct) =>
        await Proyectar(PedidosFiltrados(f, periodo, vigente)).ToListAsync(ct);

    // ── Gerencial ──

    public Task<int> ContarEntregadosAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct) =>
        EntregasDelPeriodo(f, periodo).CountAsync(ct);

    /// <summary>Ingresos y costos del periodo en UNA consulta agregada. Los ingresos son los mismos del dashboard (entregados en el periodo).</summary>
    public async Task<CifrasGerenciales> CifrasAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct)
    {
        var cifras = await Entregados(f, periodo).GroupBy(_ => 1)
            .Select(g => new
            {
                Ingresos = g.Sum(x => (decimal)x.Ingreso),
                Entregados = g.Count(),
                IngresosConCosto = g.Where(x => x.ConCosto).Sum(x => (decimal)x.Ingreso),
                ConCosto = g.Count(x => x.ConCosto),
                Materiales = g.Sum(x => x.Materiales),
                ManoObra = g.Sum(x => x.ManoObra),
                Otros = g.Sum(x => x.Otros)
            }).FirstOrDefaultAsync(ct);
        return cifras is null
            ? new CifrasGerenciales(0, 0, 0, 0, 0, 0, 0)
            : new CifrasGerenciales(cifras.Ingresos, cifras.Entregados, cifras.IngresosConCosto, cifras.ConCosto, cifras.Materiales, cifras.ManoObra, cifras.Otros);
    }

    /// <summary>Ingresos y costos por dia de entrega (la API los reparte en tramos): una fila por dia con entregas.</summary>
    public async Task<IReadOnlyList<DiaGerencial>> PorDiaAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct) =>
        await Entregados(f, periodo).GroupBy(x => x.Entrega).OrderBy(g => g.Key)
            .Select(g => new DiaGerencial(g.Key, g.Sum(x => (decimal)x.Ingreso), g.Sum(x => x.Materiales + x.ManoObra + x.Otros)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<FilaEntregadoGerencial>> EntregadosAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct) =>
        await (from x in Entregados(f, periodo)
               join c in db.Clientes.AsNoTracking() on x.IdCliente equals c.IdCliente
               orderby x.Entrega, x.IdPedido
               select new FilaEntregadoGerencial(x.IdPedido, (c.Nombre + " " + c.Apellido).Trim(), x.Entrega, x.Ingreso,
                   x.ConCosto ? x.Materiales : null, x.ConCosto ? x.ManoObra : null, x.ConCosto ? x.Otros : null))
            .ToListAsync(ct);

    /// <summary>Ingresos por tipo de letrero (producto): la misma cifra del dashboard, repartida entre los items del pedido.</summary>
    public Task<IReadOnlyList<FilaDemanda>> TiposDeLetreroAsync(FiltroIndicadores f, Periodo periodo, CancellationToken ct) =>
        indicadores.DemandaAsync(f with { Agrupar = "producto" }, periodo, ct);
}
