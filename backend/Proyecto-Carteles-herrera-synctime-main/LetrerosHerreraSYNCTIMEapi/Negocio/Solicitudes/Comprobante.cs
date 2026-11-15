namespace LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;

// El comprobante de una solicitud (fase 7): es el mismo en la pantalla del cliente, en su correo de
// confirmacion, en el aviso al taller y en el registro presencial. Las fechas, en hora de Chile.
public sealed record ComprobanteSolicitud(
    int IdSolicitud,
    string Numero,
    DateTime FechaChile,
    string Origen,
    string Estado,
    string? MotivoRechazo,
    ClienteComprobante Cliente,
    IReadOnlyList<DetalleComprobante> Detalles,
    decimal? AreaTotalM2,
    string? DescripcionGeneral,
    IReadOnlyList<AdjuntoComprobante> Adjuntos,
    byte[] RowVersion);

public sealed record ClienteComprobante(string Nombre, string Apellido, string Telefono, string? Correo);

public sealed record DetalleComprobante(int IdProducto, string Producto, string? Descripcion, int? AnchoMm, int? AltoMm, int Cantidad, decimal? AreaM2);

public sealed record AdjuntoComprobante(int IdAdjunto, string Tipo, string Nombre, int Bytes, int? Ancho, int? Alto, string? UrlMiniatura, string UrlArchivo);

// La ficha que ve el personal: el comprobante, quien la registro y su historial de estados.
public sealed record FichaSolicitud(
    int IdSolicitud,
    string Numero,
    DateTime FechaChile,
    string Origen,
    string Estado,
    string? MotivoRechazo,
    int IdCliente,
    ClienteComprobante Cliente,
    IReadOnlyList<DetalleComprobante> Detalles,
    decimal? AreaTotalM2,
    string? DescripcionGeneral,
    IReadOnlyList<AdjuntoComprobante> Adjuntos,
    byte[] RowVersion,
    string? RegistradaPor,
    IReadOnlyList<CambioDeEstado> Historial,
    IReadOnlyList<Cotizaciones.CotizacionDeLaSolicitud> Cotizaciones)
{
    // Fase 8: con sus cotizaciones (sin las eliminadas), de la mas nueva a la mas antigua.
    public static FichaSolicitud Desde(ComprobanteSolicitud c, int idCliente, string? registradaPor, IReadOnlyList<CambioDeEstado> historial,
        IReadOnlyList<Cotizaciones.CotizacionDeLaSolicitud> cotizaciones) =>
        new(c.IdSolicitud, c.Numero, c.FechaChile, c.Origen, c.Estado, c.MotivoRechazo, idCliente, c.Cliente, c.Detalles,
            c.AreaTotalM2, c.DescripcionGeneral, c.Adjuntos, c.RowVersion, registradaPor, historial, cotizaciones);
}

// Actor: "personal", "cliente" o "sistema". Por: nombre de quien lo hizo (null si fue el sistema).
public sealed record CambioDeEstado(string? EstadoAnterior, string EstadoNuevo, string? Motivo, DateTime FechaChile, string Actor, string? Por);

// Una fila de la bandeja del personal.
public sealed record FilaSolicitud(
    int IdSolicitud,
    string Numero,
    DateTime FechaChile,
    ClienteFila Cliente,
    string Origen,
    string Estado,
    int CantidadItems,
    int CantidadAdjuntos,
    string? RegistradaPor);

public sealed record ClienteFila(int IdCliente, string Nombre, string Apellido, string Telefono);

// Una fila de «Mis solicitudes». Fase 8 (S04): con su cotizacion vigente (la ultima no eliminada) y su
// pedido, si los tiene.
public sealed record ResumenSolicitud(
    int IdSolicitud,
    string Numero,
    DateTime FechaChile,
    string Origen,
    string Estado,
    string? MotivoRechazo,
    string? Resumen,
    int CantidadItems,
    int CantidadAdjuntos,
    Cotizaciones.SeguimientoCotizacion? Cotizacion,
    Pedidos.SeguimientoPedido? Pedido);

// «Mis solicitudes» por id: el comprobante con el seguimiento (fase 8, S04).
public sealed record MiSolicitud(
    int IdSolicitud,
    string Numero,
    DateTime FechaChile,
    string Origen,
    string Estado,
    string? MotivoRechazo,
    ClienteComprobante Cliente,
    IReadOnlyList<DetalleComprobante> Detalles,
    decimal? AreaTotalM2,
    string? DescripcionGeneral,
    IReadOnlyList<AdjuntoComprobante> Adjuntos,
    byte[] RowVersion,
    Cotizaciones.SeguimientoCotizacion? Cotizacion,
    Pedidos.SeguimientoPedido? Pedido)
{
    public static MiSolicitud Desde(ComprobanteSolicitud c, Cotizaciones.SeguimientoCotizacion? cotizacion, Pedidos.SeguimientoPedido? pedido) =>
        new(c.IdSolicitud, c.Numero, c.FechaChile, c.Origen, c.Estado, c.MotivoRechazo, c.Cliente, c.Detalles, c.AreaTotalM2,
            c.DescripcionGeneral, c.Adjuntos, c.RowVersion, cotizacion, pedido);
}

public static class Adjuntos
{
    public const string RutaBase = "/api/v1/adjuntos";

    public static string UrlMiniatura(int id) => $"{RutaBase}/{id}/miniatura";
    public static string UrlArchivo(int id) => $"{RutaBase}/{id}/archivo";
}
