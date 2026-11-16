namespace LetrerosHerreraSYNCTIMEapi.Models;

public class CartelDigital
{
    public int IdCartel { get; set; }
    public bool AbiertoEnLocal { get; set; } = true;
    public string MensajeEstado { get; set; } = "Local abierto y atendiendo";
    public string? TiempoEstimadoRegreso { get; set; }
    public DateTime FechaUltimaModificacion { get; set; }
    public int? IdEmpleadoModifica { get; set; }

    // Fase 11: version para guardar con If-Match.
    public byte[] RowVersion { get; set; } = null!;

    public virtual Empleado? EmpleadoModifica { get; set; }
}

