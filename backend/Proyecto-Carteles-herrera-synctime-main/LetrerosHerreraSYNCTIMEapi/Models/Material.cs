using System.Text.Json.Serialization;

namespace LetrerosHerreraSYNCTIMEapi.Models;

public class Material
{
    public int IdMaterial { get; set; }
    public string Nombre { get; set; } = null!;
    public string UnidadMedida { get; set; } = null!;
    public decimal CostoUnitarioAprox { get; set; }
    public bool Activo { get; set; } = true;

    [JsonIgnore]
    public virtual ICollection<CotizacionMaterial> CotizacionMateriales { get; set; } = new List<CotizacionMaterial>();
}
