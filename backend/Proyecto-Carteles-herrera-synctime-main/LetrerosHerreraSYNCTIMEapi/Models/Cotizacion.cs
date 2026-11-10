namespace LetrerosHerreraSYNCTIMEapi.Models;

public class Cotizacion
{
    public int IdCotizacion { get; set; }
    public int IdSolicitud { get; set; }
    public int IdEmpleado { get; set; }
    public DateTime FechaCotizacion { get; set; }
    public string? Observaciones { get; set; }
    public decimal CostoMaterialesTotal { get; set; }
    public decimal CostoManoObra { get; set; }
    public decimal OtrosCostos { get; set; }
    public decimal TotalCotizacion { get; set; }
    public string Estado { get; set; } = "pendiente";

    public virtual Solicitud? Solicitud { get; set; }
    public virtual Empleado? Empleado { get; set; }
    public virtual ICollection<CotizacionMaterial> Materiales { get; set; } = new List<CotizacionMaterial>();
}
