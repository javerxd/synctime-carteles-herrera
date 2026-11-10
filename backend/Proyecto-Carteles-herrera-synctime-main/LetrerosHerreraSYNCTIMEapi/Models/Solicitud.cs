using System.Text.Json.Serialization;

namespace LetrerosHerreraSYNCTIMEapi.Models;

public class Solicitud
{
    public int IdSolicitud { get; set; }
    public int IdCliente { get; set; }
    public int? IdEmpleadoRegistra { get; set; }
    public string Origen { get; set; } = null!;
    public string? DescripcionGeneral { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public string Estado { get; set; } = "solicitado";
    public string? MotivoRechazo { get; set; }
    public bool EliminadoLogico { get; set; } = false;
    public DateTime? FechaEliminacionLogica { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    public virtual Cliente? Cliente { get; set; }
    public virtual Empleado? EmpleadoRegistra { get; set; }
    public virtual ICollection<SolicitudDetalle> Detalles { get; set; } = new List<SolicitudDetalle>();

    [JsonIgnore]
    public virtual ICollection<Cotizacion> Cotizaciones { get; set; } = new List<Cotizacion>();

    [JsonIgnore]
    public virtual Pedido? Pedido { get; set; }
}
