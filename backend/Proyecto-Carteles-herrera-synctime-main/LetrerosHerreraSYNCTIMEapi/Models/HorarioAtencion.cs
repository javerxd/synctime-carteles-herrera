namespace LetrerosHerreraSYNCTIMEapi.Models;

public class HorarioAtencion
{
    public int IdHorario { get; set; }
    public byte DiaSemana { get; set; }
    public string NombreDia { get; set; } = null!;
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }
    public bool Abierto { get; set; } = true;

    // Fase 9: quien hizo el ultimo cambio y cuando. La portada no lo muestra.
    public int? IdEmpleadoModifica { get; set; }
    public DateTimeOffset? FechaModificacion { get; set; }
}

