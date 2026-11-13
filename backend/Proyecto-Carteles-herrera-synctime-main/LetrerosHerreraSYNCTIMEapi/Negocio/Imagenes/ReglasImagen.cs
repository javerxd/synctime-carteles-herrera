namespace LetrerosHerreraSYNCTIMEapi.Negocio.Imagenes;

public enum FormatoImagen { Desconocido, Jpeg, Png, Webp }

// Reglas de las imagenes que sube el taller (RNFP22). El tipo se decide por los primeros bytes del
// archivo, nunca por su nombre ni por el tipo que declara el navegador: un SVG (que puede llevar
// codigo) o cualquier otro archivo renombrado a .png no pasa.
public static class ReglasImagen
{
    public const long MaxBytes = 10 * 1024 * 1024;

    // Una foto de celular de 48 MP mide unos 8000 x 6000. Un PNG se decodifica entero (no admite
    // decodificar a menor escala), por eso su limite es menor: asi ninguna imagen pide mas de unos 64 MB.
    public const int MaxLado = 16_384;
    public const long MaxPixeles = 50_000_000;
    public const long MaxPixelesPng = 16_000_000;

    // Lado mayor de cada version guardada. Nunca se agranda una imagen mas chica.
    public const int LadoGrande = 1600;
    public const int LadoMiniatura = 640;

    public const string MensajeFormato = "El archivo no es una imagen JPG, PNG o WebP.";
    public const string MensajeTamano = "La imagen supera los 10 MB. Reduzca su tamaño e inténtelo de nuevo.";
    public const string MensajeDemasiadoGrande = "La imagen es demasiado grande: admite hasta 50 megapíxeles (16 en PNG).";
    public const string MensajeDanada = "La imagen está dañada o incompleta. Pruebe con otro archivo.";

    public static FormatoImagen Detectar(ReadOnlySpan<byte> datos)
    {
        if (datos.Length >= 3 && datos[0] == 0xFF && datos[1] == 0xD8 && datos[2] == 0xFF)
            return FormatoImagen.Jpeg;
        if (datos.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return FormatoImagen.Png;
        if (datos.Length >= 12 && datos[..4].SequenceEqual("RIFF"u8) && datos[8..12].SequenceEqual("WEBP"u8))
            return FormatoImagen.Webp;
        return FormatoImagen.Desconocido;
    }

    // Medidas para que el lado mayor no pase de "lado", manteniendo la proporcion y sin agrandar.
    public static (int Ancho, int Alto) Ajustar(int ancho, int alto, int lado)
    {
        var mayor = Math.Max(ancho, alto);
        if (mayor <= lado)
            return (ancho, alto);
        var escala = (double)lado / mayor;
        return ancho >= alto
            ? (lado, Math.Max(1, (int)Math.Round(alto * escala)))
            : (Math.Max(1, (int)Math.Round(ancho * escala)), lado);
    }
}
