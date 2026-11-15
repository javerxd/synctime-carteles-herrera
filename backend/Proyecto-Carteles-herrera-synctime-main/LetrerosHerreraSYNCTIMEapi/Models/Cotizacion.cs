namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 8: una cotizacion «pendiente» espera la respuesta del cliente, que llega por la web o por el
// personal (telefono, WhatsApp o en el local). La respuesta deja la fecha, la via, quien la registro y,
// si se rechazo, el motivo. Nunca se borra: el borrado es logico.
public class Cotizacion
{
    public int IdCotizacion { get; set; }
    public int IdSolicitud { get; set; }
    public int IdEmpleado { get; set; }
    public DateTime FechaCotizacion { get; set; }
    public string? Observaciones { get; set; }
    public decimal CostoMaterialesTotal { get; set; }
    public decimal CostoManoObra { get; set; }
    public decimal OtrosCostos { get; set; }
    public decimal TotalCotizacion { get; set; }
    public string Estado { get; set; } = "pendiente";
    public byte[] RowVersion { get; set; } = null!;

    public DateTimeOffset? FechaRespuesta { get; set; }

    // cliente_web, telefono, whatsapp o presencial.
    public string? ViaRespuesta { get; set; }

    // null si respondio el cliente desde la web.
    public int? IdEmpleadoRespuesta { get; set; }
    public string? MotivoRechazo { get; set; }
    public bool EliminadoLogico { get; set; }
    public DateTimeOffset? FechaEliminacionLogica { get; set; }

    public virtual Solicitud? Solicitud { get; set; }
    public virtual Empleado? Empleado { get; set; }
    public virtual Empleado? EmpleadoRespuesta { get; set; }
    public virtual ICollection<CotizacionMaterial> Materiales { get; set; } = new List<CotizacionMaterial>();
}
