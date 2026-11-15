using System.Text.Json.Serialization;

namespace LetrerosHerreraSYNCTIMEapi.Models;

public class Material
{
    public int IdMaterial { get; set; }
    public string Nombre { get; set; } = null!;
    public string UnidadMedida { get; set; } = null!;
    public decimal CostoUnitarioAprox { get; set; }
    public bool Activo { get; set; } = true;

    // Fase 9 (RNFP10): version para que una edicion con datos viejos no pise la de otra persona.
    [JsonIgnore]
    public byte[] RowVersion { get; set; } = null!;

    [JsonIgnore]
    public virtual ICollection<CotizacionMaterial> CotizacionMateriales { get; set; } = new List<CotizacionMaterial>();
}
