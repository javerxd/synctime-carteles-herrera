namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 9 (RFU20): una franja semanal en que el empleado atiende, por ejemplo «lunes 9:00-13:00». Se
// repite cada semana (decision 2 del 14-11-2026). La portada muestra la union de las franjas de todo el
// personal activo, sin nombres.
public class DisponibilidadEmpleado
{
    public int IdDisponibilidad { get; set; }
    public int IdEmpleado { get; set; }

    // 1 = lunes ... 7 = domingo, igual que horarios_atencion.
    public byte DiaSemana { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }

    public virtual Empleado? Empleado { get; set; }
}
