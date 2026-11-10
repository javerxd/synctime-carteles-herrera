namespace LetrerosHerreraSYNCTIMEapi.Models;

public class Catalogo
{
    public int IdCatalogo { get; set; }
    public int IdProducto { get; set; }
    public int? IdEmpleadoCreador { get; set; }
    public string Titulo { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public string ImagenUrl { get; set; } = null!;
    public int? AnchoMm { get; set; }
    public int? AltoMm { get; set; }
    public decimal? PrecioReferencial { get; set; }
    public DateTime FechaPublicacion { get; set; }
    public bool Visible { get; set; } = true;

    public virtual Producto? Producto { get; set; }
    public virtual Empleado? EmpleadoCreador { get; set; }
}
