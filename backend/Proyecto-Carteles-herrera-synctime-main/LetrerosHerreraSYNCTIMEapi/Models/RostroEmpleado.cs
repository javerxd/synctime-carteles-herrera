namespace LetrerosHerreraSYNCTIMEapi.Models;

// Acceso facial de un empleado. Hay una fila por activacion y solo una vigente por empleado.
// Al revocarse se borran el descriptor y el token de equipo; quedan las fechas y la version
// del consentimiento, porque probar que existio le corresponde al responsable de los datos.
// Empleado no tiene navegacion hacia aqui para que estos datos no puedan salir en sus respuestas.
public class RostroEmpleado
{
    public int IdRostro { get; set; }
    public int IdEmpleado { get; set; }
    public byte[]? DescriptorCifrado { get; set; }
    public DateTimeOffset FechaConsentimiento { get; set; }
    public string VersionConsentimiento { get; set; } = null!;
    public DateTimeOffset? FechaRevocacion { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTimeOffset? BloqueadoHasta { get; set; }
    public byte[]? TokenEquipoHash { get; set; }
    public DateTimeOffset? FechaAutorizacionEquipo { get; set; }

    public virtual Empleado? Empleado { get; set; }
}
