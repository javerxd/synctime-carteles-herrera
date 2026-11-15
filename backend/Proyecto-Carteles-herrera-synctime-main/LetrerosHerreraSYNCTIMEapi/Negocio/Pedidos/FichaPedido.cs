using LetrerosHerreraSYNCTIMEapi.Negocio.Cotizaciones;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Pedidos;

// La ficha del pedido (S07, fase 8), para el personal. Montos en pesos enteros. FechaPedido y las fechas
// «Chile» van en hora de Chile; las de entrega y de pago son fechas sin hora (aaaa-mm-dd).
// TransicionesPosibles y PuedeAnular dependen de quien mira: sin ser de gestion, un pedido con abonos no
// ofrece «anulado» y solo se anulan los abonos propios de las ultimas 24 horas.
public sealed record FichaPedido(
    int IdPedido,
    string Numero,
    string Origen,
    ReferenciaSolicitud Solicitud,
    ReferenciaCotizacion? Cotizacion,
    DatosCliente Cliente,
    PersonaDelTaller Responsable,
    DateTime FechaPedido,
    DateOnly FechaEntregaEstimada,
    DateOnly? FechaEntregaReal,
    string ModalidadEntrega,
    bool RequiereInstalacion,
    string? DireccionInstalacion,
    int MontoBaseTrabajo,
    int CostoInstalacionAdicional,
    int MontoTotalFinal,
    string EstadoPedido,
    string EstadoPago,
    int MontoAbonado,
    int SaldoPendiente,
    string? NumeroFactura,
    bool CicloCerrado,
    string? MotivoAnulacion,
    int AFavorDelCliente,
    IReadOnlyList<string> TransicionesPosibles,
    IReadOnlyList<CambioDePedido> Historial,
    IReadOnlyList<AbonoDelPedido> Pagos,
    byte[] RowVersion);

public sealed record ReferenciaSolicitud(int IdSolicitud, string Numero);

public sealed record ReferenciaCotizacion(int IdCotizacion, string Numero);

public sealed record CambioDePedido(DateTime FechaChile, string? EstadoAnterior, string EstadoNuevo, string? Nota, string Empleado);

public sealed record AbonoDelPedido(
    int IdPago,
    int Monto,
    DateOnly FechaPago,
    string MedioPago,
    string? Observacion,
    string RegistradoPor,
    DateTime FechaRegistroChile,
    bool Anulado,
    string? MotivoAnulacion,
    string? AnuladoPor,
    DateTime? FechaAnulacionChile,
    bool PuedeAnular);

// Una fila del listado del personal.
public sealed record FilaPedido(
    int IdPedido,
    string Numero,
    string Origen,
    DateTime FechaPedido,
    ClienteFila Cliente,
    PersonaDelTaller Responsable,
    DateOnly FechaEntregaEstimada,
    DateOnly? FechaEntregaReal,
    string ModalidadEntrega,
    string EstadoPedido,
    string EstadoPago,
    int MontoTotalFinal,
    int MontoAbonado,
    int SaldoPendiente,
    bool CicloCerrado);

// Por cobrar (S24): pedidos no anulados con pago «no_pagado» o «abonado», de todo el filtro (no solo de la pagina).
public sealed record ResumenCobranza(long TotalPorCobrar, int CantidadPorCobrar);

// El listado paginado de pedidos, con el resumen de cobranza.
public sealed record ListadoPedidos(IReadOnlyList<FilaPedido> Elementos, int Total, int Pagina, int Tamano, ResumenCobranza Resumen)
{
    public int TotalPaginas => (int)Math.Ceiling(Total / (double)Tamano);
}

// Una entrega en el calendario o en las alertas: los pendientes van en su fecha estimada y los entregados
// en su fecha real. Atrasado: sin entregar y con la fecha estimada ya pasada.
public sealed record EntregaCalendario(
    int IdPedido,
    string Numero,
    DateOnly Fecha,
    ClienteFila Cliente,
    string ModalidadEntrega,
    string EstadoPedido,
    string EstadoPago,
    bool Atrasado);

public sealed record AlertasPedidos(
    IReadOnlyList<EntregaCalendario> Proximas,
    IReadOnlyList<EntregaCalendario> Atrasadas,
    long TotalPorCobrar,
    int CantidadPorCobrar);

// El pedido en el seguimiento del cliente (S04): sin historial interno ni quien registro cada abono.
// Eliminado: el taller lo anulo y lo elimino (borrado logico); se sigue mostrando como anulado.
public sealed record SeguimientoPedido(
    int IdPedido,
    string Numero,
    string EstadoPedido,
    string EstadoPago,
    int SaldoPendiente,
    DateOnly FechaEntregaEstimada,
    string ModalidadEntrega,
    bool CicloCerrado,
    bool Eliminado);

// «Mis pedidos»: lo mismo, con la solicitud de la que viene.
public sealed record MiPedido(
    int IdPedido,
    int IdSolicitud,
    string Numero,
    string EstadoPedido,
    string EstadoPago,
    int SaldoPendiente,
    DateOnly FechaEntregaEstimada,
    string ModalidadEntrega,
    bool CicloCerrado,
    bool Eliminado);
