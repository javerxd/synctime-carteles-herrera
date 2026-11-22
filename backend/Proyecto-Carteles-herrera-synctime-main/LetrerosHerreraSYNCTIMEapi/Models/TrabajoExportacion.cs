namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 13A (RNFP18, RFS10): un archivo de reporte que se prepara en segundo plano. La persona pide el reporte (202), un servicio
// en segundo plano lo genera y lo guarda en almacen/exportaciones/ con un nombre que sale del identificador (nunca de texto de la
// persona), y ella lo descarga mientras no haya vencido. El registro queda aunque el archivo se borre al vencer.
public class TrabajoExportacion
{
    // Aleatorio (no adivinable): quien no lo conoce no puede pedir el archivo de otra persona.
    public Guid IdTrabajo { get; set; }
    public string Reporte { get; set; } = null!;
    public string Formato { get; set; } = null!;

    // Los filtros ya validados, en JSON: solo lo que la persona pidio, sin datos personales (el cliente va por su numero).
    public string Filtros { get; set; } = "{}";
    public string Estado { get; set; } = EstadosExportacion.Pendiente;

    public int IdEmpleado { get; set; }
    // El rol con el que se pidio: la bitacora registra «quien y con que rol» tambien al terminar.
    public string RolSolicitante { get; set; } = null!;

    public DateTimeOffset FechaPedido { get; set; }
    public DateTimeOffset? FechaInicio { get; set; }
    public DateTimeOffset? FechaTermino { get; set; }
    public DateTimeOffset? FechaVencimiento { get; set; }

    // Mientras un trabajo esta «procesando», hasta cuando es de quien lo tomo; pasado ese momento se considera abandonado.
    public DateTimeOffset? BloqueadoHasta { get; set; }
    public byte Intentos { get; set; }

    // Lo que ve quien lo descarga: reporte-operativo-2026-11-20.xlsx.
    public string? NombreArchivo { get; set; }
    public int? TamanoBytes { get; set; }
    public int? Filas { get; set; }
    public string? Error { get; set; }

    // Cuando se borro el contenido (al vencer). El registro del trabajo queda.
    public DateTimeOffset? ArchivoBorradoEn { get; set; }

    public virtual Empleado? Empleado { get; set; }
}

public static class EstadosExportacion
{
    public const string Pendiente = "pendiente";
    public const string Procesando = "procesando";
    public const string Listo = "listo";
    public const string Fallido = "fallido";
}
