using System.Text.Json.Serialization;

namespace LetrerosHerreraSYNCTIMEapi.Models;

public class Empleado
{
    public int IdEmpleado { get; set; }
    public string Rut { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Apellido { get; set; } = null!;
    public string Telefono { get; set; } = null!;
    public string Email { get; set; } = null!;

    [JsonIgnore]
    public string PasswordHash { get; set; } = null!;

    public string Rol { get; set; } = "administrador";
    public bool Activo { get; set; } = true;

    [JsonIgnore]
    public virtual ICollection<Solicitud> SolicitudesRegistradas { get; set; } = new List<Solicitud>();

    [JsonIgnore]
    public virtual ICollection<Cotizacion> Cotizaciones { get; set; } = new List<Cotizacion>();

    [JsonIgnore]
    public virtual ICollection<Pedido> PedidosResponsable { get; set; } = new List<Pedido>();

    [JsonIgnore]
    public virtual ICollection<Catalogo> CatalogosCreados { get; set; } = new List<Catalogo>();
}
