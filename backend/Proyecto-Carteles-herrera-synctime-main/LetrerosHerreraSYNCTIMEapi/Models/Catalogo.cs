namespace LetrerosHerreraSYNCTIMEapi.Models;

public class Catalogo
{
    public int IdCatalogo { get; set; }
    public int IdProducto { get; set; }
    public int? IdEmpleadoCreador { get; set; }
    public string Titulo { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    // Direccion de una foto antigua (antes de la subida de imagenes). Desde la fase 5 se usa IdImagen.
    public string? ImagenUrl { get; set; }
    public int? IdImagen { get; set; }
    public int? AnchoMm { get; set; }
    public int? AltoMm { get; set; }
    public decimal? PrecioReferencial { get; set; }
    public DateTime FechaPublicacion { get; set; }
    public bool Visible { get; set; } = true;

    // RNFO17: si la foto muestra el logotipo de un cliente, la publicacion solo puede ser visible con su
    // autorizacion registrada (quien la registro y cuando). Lo exige ck_catalogo_logo en la base.
    public bool IncluyeLogoCliente { get; set; }
    public int? IdEmpleadoAutorizaLogo { get; set; }
    public DateTimeOffset? FechaAutorizacionLogo { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public virtual Imagen? Imagen { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public virtual Empleado? EmpleadoAutorizaLogo { get; set; }

    public virtual Producto? Producto { get; set; }
    public virtual Empleado? EmpleadoCreador { get; set; }
}
