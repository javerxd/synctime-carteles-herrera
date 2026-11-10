using System.Text.Json.Serialization;

namespace LetrerosHerreraSYNCTIMEapi.Models;

public class SolicitudDetalle
{
    public int IdSolicitudDetalle { get; set; }
    public int IdSolicitud { get; set; }
    public int IdProducto { get; set; }
    public string Descripcion { get; set; } = null!;
    public int? AnchoMm { get; set; }
    public int? AltoMm { get; set; }
    public int Cantidad { get; set; } = 1;
    public decimal? AreaM2 { get; set; }

    [JsonIgnore]
    public virtual Solicitud? Solicitud { get; set; }

    public virtual Producto? Producto { get; set; }
}
