namespace LetrerosHerreraSYNCTIMEapi.Models;

// Acceso facial de un empleado. Hay una fila por activacion y solo una vigente por empleado.
// Al revocarse se borran los descriptores y el token de equipo; quedan las fechas y la version
// del consentimiento, porque probar que existio le corresponde al responsable de los datos.
// Empleado no tiene navegacion hacia aqui para que estos datos no puedan salir en sus respuestas.
//
// Fase 10: tres muestras cifradas (de frente y con un giro moderado hacia cada lado) reemplazan al
// descriptor promedio, que deja de usarse. Una fila vigente sin las tres muestras debe registrarse de nuevo.
public class RostroEmpleado
{
    // 2: tres muestras cifradas sin el lado en los datos asociados (primera version de la fase 10). Ya no se
    // escribe: cuenta como «debe registrarse de nuevo».
    public const byte FormatoTresMuestrasSinLado = 2;
    // 3: tres muestras, cada una cifrada con su lado (frente, izquierda o derecha) en los datos asociados.
    public const byte FormatoMuestrasPorLado = 3;

    public int IdRostro { get; set; }
    public int IdEmpleado { get; set; }
    // Anterior a la fase 10: ya no se escribe ni se compara; solo se borra al revocar.
    public byte[]? DescriptorCifrado { get; set; }
    public DateTimeOffset FechaConsentimiento { get; set; }
    public string VersionConsentimiento { get; set; } = null!;
    public DateTimeOffset? FechaRevocacion { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTimeOffset? BloqueadoHasta { get; set; }
    public byte[]? TokenEquipoHash { get; set; }
    public DateTimeOffset? FechaAutorizacionEquipo { get; set; }

    // Fase 10: las tres muestras, cifradas igual que el descriptor antiguo (AES-GCM, atadas a su empleado).
    public byte[]? DescriptorFrente { get; set; }
    public byte[]? DescriptorIzquierda { get; set; }
    public byte[]? DescriptorDerecha { get; set; }
    // Quien inicio el registro: el mismo titular, o el dueño o el administrador con el titular presente.
    public int? IdEmpleadoRegistra { get; set; }
    // 3 = tres muestras atadas a su lado; 2 = la version anterior, sin el lado. null antes de la fase 10.
    public byte? FormatoMuestras { get; set; }

    public bool TieneTresMuestras =>
        FormatoMuestras == FormatoMuestrasPorLado && DescriptorFrente is not null && DescriptorIzquierda is not null && DescriptorDerecha is not null;

    public virtual Empleado? Empleado { get; set; }
    public virtual Empleado? EmpleadoRegistra { get; set; }
}
