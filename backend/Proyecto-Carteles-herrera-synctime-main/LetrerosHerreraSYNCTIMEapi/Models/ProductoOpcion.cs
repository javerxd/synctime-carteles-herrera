using System.Text.Json.Serialization;

namespace LetrerosHerreraSYNCTIMEapi.Models;

// Una medida unica o disponible de un producto (milimetros): X Banner 800 x 2000, caballete 1000 x 500...
public class ProductoMedidaFija
{
    public int IdMedida { get; set; }
    public int IdProducto { get; set; }
    public int AnchoMm { get; set; }
    public int AltoMm { get; set; }

    [JsonIgnore]
    public virtual Producto? Producto { get; set; }
}

// Un valor elegible de un producto: Grupo «Material», Valor «Tela PVC de 13 onzas». El cliente elige uno por grupo.
public class ProductoOpcion
{
    public int IdOpcion { get; set; }
    public int IdProducto { get; set; }
    public string Grupo { get; set; } = null!;
    public string Valor { get; set; } = null!;
    public int Orden { get; set; }

    [JsonIgnore]
    public virtual Producto? Producto { get; set; }
}
