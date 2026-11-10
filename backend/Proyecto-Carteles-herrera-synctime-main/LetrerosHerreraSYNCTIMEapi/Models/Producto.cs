using System.Text.Json.Serialization;

namespace LetrerosHerreraSYNCTIMEapi.Models;

public class Producto
{
    public int IdProducto { get; set; }
    public int IdCategoria { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public decimal? PrecioBaseRef { get; set; }
    public bool Activo { get; set; } = true;

    public virtual Categoria? Categoria { get; set; }

    [JsonIgnore]
    public virtual ICollection<Catalogo> Catalogos { get; set; } = new List<Catalogo>();

    [JsonIgnore]
    public virtual ICollection<SolicitudDetalle> SolicitudDetalles { get; set; } = new List<SolicitudDetalle>();
}
