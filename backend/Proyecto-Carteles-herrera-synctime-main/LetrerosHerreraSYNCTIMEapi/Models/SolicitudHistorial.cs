namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 7: cada cambio de estado de una solicitud, tambien su creacion (EstadoAnterior null). Dice quien
// lo hizo: un empleado, el cliente, o nadie cuando lo hace el sistema (por ejemplo, al cotizar).
public class SolicitudHistorial
{
    public int IdHistorial { get; set; }
    public int IdSolicitud { get; set; }
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = null!;
    public string? Motivo { get; set; }
    public int? IdEmpleado { get; set; }
    public int? IdCliente { get; set; }
    public DateTimeOffset Fecha { get; set; }

    public virtual Solicitud? Solicitud { get; set; }
    public virtual Empleado? Empleado { get; set; }
    public virtual Cliente? Cliente { get; set; }
}
