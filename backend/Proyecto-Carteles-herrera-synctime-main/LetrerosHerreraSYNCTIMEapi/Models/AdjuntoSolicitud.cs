namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 7: logo o diseño que acompaña una solicitud. Mientras IdSolicitud es null es un borrador del
// cliente que lo subio (se vincula al enviar la solicitud, o la purga lo borra a las 24 horas).
// Los archivos viven en la carpeta privada Adjuntos:Carpeta con un nombre aleatorio (Clave): nunca se
// sirven sin sesion, a diferencia de las fotos de la vitrina.
public class AdjuntoSolicitud
{
    public const string TipoImagen = "imagen";
    public const string TipoPdf = "pdf";

    public int IdAdjunto { get; set; }
    public int? IdSolicitud { get; set; }

    // Quien lo subio: el cliente (desde su formulario) o el personal (en la ficha). Exactamente uno.
    public int? IdCliente { get; set; }
    public int? IdEmpleado { get; set; }

    public string Clave { get; set; } = null!;
    public string Tipo { get; set; } = null!;

    // Saneado: sin rutas, sin caracteres de control y de hasta 120 caracteres. Solo se muestra.
    public string NombreOriginal { get; set; } = null!;
    public int Bytes { get; set; }
    public int? Ancho { get; set; }
    public int? Alto { get; set; }
    public DateTimeOffset FechaSubida { get; set; }
    public DateTimeOffset? FechaVinculo { get; set; }

    public virtual Solicitud? Solicitud { get; set; }
    public virtual Cliente? Cliente { get; set; }
    public virtual Empleado? Empleado { get; set; }
}
