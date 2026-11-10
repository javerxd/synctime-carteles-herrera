using System.Text.Json.Serialization;

namespace LetrerosHerreraSYNCTIMEapi.Models;

public class CotizacionMaterial
{
    public int IdCotizacionMaterial { get; set; }
    public int IdCotizacion { get; set; }
    public int IdMaterial { get; set; }
    public string? MedidaAplicada { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Subtotal { get; set; }

    [JsonIgnore]
    public virtual Cotizacion? Cotizacion { get; set; }

    public virtual Material? Material { get; set; }
}
