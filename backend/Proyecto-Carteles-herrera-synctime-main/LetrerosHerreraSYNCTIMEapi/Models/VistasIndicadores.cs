namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 12B (RNFO11, RNFP26): filas de las vistas que alimentan el dashboard. Solo lectura y sin clave: las crea la migracion
// IndicadoresDashboard y la API las agrega en SQL (GROUP BY / SUM), sin traer filas a memoria.

/// <summary>vw_indicadores_pedidos: un pedido (no eliminado) con su solicitud. Los montos son pesos enteros; las fechas, dias de Chile.</summary>
public class VistaIndicadorPedido
{
    public int IdPedido { get; set; }
    public int IdSolicitud { get; set; }
    public int IdCliente { get; set; }
    public string OrigenSolicitud { get; set; } = null!;
    public DateOnly FechaPedido { get; set; }
    public DateOnly FechaEntregaEstimada { get; set; }
    public DateOnly? FechaEntregaReal { get; set; }
    public string EstadoPedido { get; set; } = null!;
    public string EstadoPago { get; set; } = null!;
    public int MontoTotalFinal { get; set; }
    public int MontoAbonado { get; set; }
    public int SaldoPendiente { get; set; }
}

/// <summary>vw_indicadores_solicitudes: una solicitud (no eliminada), con su categoria principal y su avance en el embudo.</summary>
public class VistaIndicadorSolicitud
{
    public int IdSolicitud { get; set; }
    public int IdCliente { get; set; }
    public string Origen { get; set; } = null!;
    public DateOnly FechaSolicitud { get; set; }
    public int? IdCategoriaPrincipal { get; set; }
    public bool Cotizada { get; set; }
    public bool Aprobada { get; set; }
    public bool Concretada { get; set; }
}

/// <summary>
/// vw_indicadores_items: los items de cada pedido con su producto y categoria. MontoAsignado reparte el monto final del
/// pedido entre sus items en proporcion a su cantidad (decision D del plan): la suma de los items es el monto del pedido.
/// </summary>
public class VistaIndicadorItem
{
    public int IdSolicitudDetalle { get; set; }
    public int IdPedido { get; set; }
    public int IdProducto { get; set; }
    public int IdCategoria { get; set; }
    public int Cantidad { get; set; }
    public decimal MontoAsignado { get; set; }
}
