namespace LetrerosHerreraSYNCTIMEapi.Models;

public class Pedido
{
    public int IdPedido { get; set; }
    public int IdSolicitud { get; set; }
    public int? IdCotizacion { get; set; }
    public int IdEmpleadoResponsable { get; set; }
    public DateTime FechaPedido { get; set; }
    public DateTime FechaEntregaEstimada { get; set; }
    public DateTime? FechaEntregaReal { get; set; }
    public string ModalidadEntrega { get; set; } = "retiro_local";
    public bool RequiereInstalacion { get; set; } = false;
    public string? DireccionInstalacion { get; set; }
    public decimal CostoInstalacionAdicional { get; set; }
    public decimal MontoBaseTrabajo { get; set; }
    public decimal MontoTotalFinal { get; set; }
    public string Estado { get; set; } = "aceptado";
    public byte[] RowVersion { get; set; } = null!;

    public virtual Solicitud? Solicitud { get; set; }
    public virtual Cotizacion? Cotizacion { get; set; }
    public virtual Empleado? EmpleadoResponsable { get; set; }
}
