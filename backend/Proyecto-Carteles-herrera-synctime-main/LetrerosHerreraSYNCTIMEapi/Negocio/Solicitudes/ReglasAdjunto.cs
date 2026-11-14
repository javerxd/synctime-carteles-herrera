using System.Globalization;
using System.Text;
using LetrerosHerreraSYNCTIMEapi.Negocio.Imagenes;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;

public enum FormatoAdjunto { Desconocido, Imagen, Pdf }

// Fase 7: logos y diseños que el cliente adjunta a su solicitud. El formato se decide por el contenido,
// nunca por el nombre ni por lo que declara el navegador: un SVG o un AI (que pueden llevar codigo)
// renombrados a .png o .pdf no pasan.
public static class ReglasAdjunto
{
    public const long MaxBytes = ReglasImagen.MaxBytes;
    public const int MaxPorSolicitud = ValidacionSolicitud.MaxAdjuntos;
    public const int LargoMaximoNombre = 120;
    public static readonly TimeSpan VidaBorrador = TimeSpan.FromHours(24);

    public const string MensajeFormato = "Formato no aceptado. Sube JPG, PNG, WebP o PDF; si tu diseño es SVG o AI, expórtalo a PDF o PNG.";
    public const string MensajeTamano = "El archivo supera los 10 MB. Reduce su tamaño e inténtalo de nuevo.";
    public const string MensajeTope = "Puedes adjuntar hasta 5 archivos por solicitud.";
    public const string MensajeSinArchivo = "Adjunta un archivo JPG, PNG, WebP o PDF en el campo «archivo».";

    public static FormatoAdjunto Detectar(ReadOnlySpan<byte> datos)
    {
        if (ReglasImagen.Detectar(datos) != FormatoImagen.Desconocido)
            return FormatoAdjunto.Imagen;
        if (datos.StartsWith("%PDF-"u8))
            return FormatoAdjunto.Pdf;
        return FormatoAdjunto.Desconocido;
    }

    // Solo se muestra (nunca nombra el archivo en disco): sin carpetas, sin caracteres de control o
    // invisibles y sin los que molestan en una descarga. Conserva la extension si hay que recortarlo.
    public static string SanearNombre(string? nombre, FormatoAdjunto formato)
    {
        var base_ = (nombre ?? "").Replace('\\', '/');
        base_ = base_[(base_.LastIndexOf('/') + 1)..];
        var limpio = new StringBuilder();
        foreach (var c in base_)
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
            if (char.IsControl(c) || categoria is UnicodeCategory.Format or UnicodeCategory.Surrogate or UnicodeCategory.OtherNotAssigned)
                continue;
            limpio.Append(c is '"' or '<' or '>' or ':' or '|' or '?' or '*' or ';' or '%' ? '_' : c);
        }
        var texto = string.Join(' ', limpio.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim('.', ' ');
        if (texto.Length == 0)
            texto = formato == FormatoAdjunto.Pdf ? "diseño.pdf" : "imagen";
        if (texto.Length > LargoMaximoNombre)
        {
            var extension = Path.GetExtension(texto);
            if (extension.Length is 0 or > 10)
                extension = "";
            texto = texto[..(LargoMaximoNombre - extension.Length)].TrimEnd() + extension;
        }
        return texto;
    }

    // Nombre para descargar el PDF: el saneado, siempre terminado en .pdf.
    public static string NombreDescargaPdf(string nombre) =>
        nombre.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? nombre : $"{nombre}.pdf";
}
