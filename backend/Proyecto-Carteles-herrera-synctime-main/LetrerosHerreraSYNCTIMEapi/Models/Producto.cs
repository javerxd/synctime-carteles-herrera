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

    // Fase 12B (catalogo real): limites de las medidas en milimetros. Todos nulos = sin medidas que validar
    // (senalizaciones, estampados). MedidasFijas: tamanos unicos o disponibles (X Banner, caballete, tarjetas).
    public int? AnchoMinMm { get; set; }
    public int? AnchoMaxMm { get; set; }
    public int? AltoMinMm { get; set; }
    public int? AltoMaxMm { get; set; }

    // Fase 9 (RNFP10): version para que una edicion con datos viejos no pise la de otra persona.
    [JsonIgnore]
    public byte[] RowVersion { get; set; } = null!;

    public virtual Categoria? Categoria { get; set; }

    [JsonIgnore]
    public virtual ICollection<ProductoMedidaFija> MedidasFijas { get; set; } = new List<ProductoMedidaFija>();

    [JsonIgnore]
    public virtual ICollection<ProductoOpcion> Opciones { get; set; } = new List<ProductoOpcion>();

    [JsonIgnore]
    public virtual ICollection<Catalogo> Catalogos { get; set; } = new List<Catalogo>();

    [JsonIgnore]
    public virtual ICollection<SolicitudDetalle> SolicitudDetalles { get; set; } = new List<SolicitudDetalle>();
}
