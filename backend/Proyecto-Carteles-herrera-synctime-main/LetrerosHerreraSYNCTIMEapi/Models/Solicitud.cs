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

    // Fase 7: HMAC-SHA256 de la IP desde la que se envio en linea (nunca la IP en claro). Solo sirve para
    // la regla contra solicitudes falsas; null en las presenciales.
    [JsonIgnore]
    public string? HuellaIp { get; set; }

    public virtual Cliente? Cliente { get; set; }
    public virtual Empleado? EmpleadoRegistra { get; set; }
    public virtual ICollection<SolicitudDetalle> Detalles { get; set; } = new List<SolicitudDetalle>();

    [JsonIgnore]
    public virtual ICollection<Cotizacion> Cotizaciones { get; set; } = new List<Cotizacion>();

    [JsonIgnore]
    public virtual Pedido? Pedido { get; set; }

    [JsonIgnore]
    public virtual ICollection<AdjuntoSolicitud> Adjuntos { get; set; } = new List<AdjuntoSolicitud>();

    [JsonIgnore]
    public virtual ICollection<SolicitudHistorial> Historial { get; set; } = new List<SolicitudHistorial>();
}
