using System.Security.Cryptography;
using System.Text.RegularExpressions;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Almacen;
using LetrerosHerreraSYNCTIMEapi.Negocio.Imagenes;
using SkiaSharp;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Imagenes;

public sealed class ImagenInvalidaException(string mensaje) : Exception(mensaje);

public sealed record ImagenProcesada(
    int Ancho, int Alto,
    byte[] Grande, int AnchoGrande, int AltoGrande,
    byte[] Miniatura, int AnchoMiniatura, int AltoMiniatura);

// Convierte una foto subida en dos WebP (RNFP22). Antes de decodificar revisa las medidas que declara el
// archivo, para que una imagen pequeña que dice medir 20.000 x 20.000 no agote la memoria. Las fotos
// grandes de celular se decodifican directamente a menor escala. La foto se endereza segun la
// orientacion que guardo la camara, y al volver a codificarla se pierden sus metadatos (EXIF), que
// pueden traer la ubicacion GPS donde se tomo.
public static class ProcesadorImagenes
{
    private const int Calidad = 82;
    private static readonly SKSamplingOptions Muestreo = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public static ImagenProcesada Procesar(byte[] datos)
    {
        if (datos.LongLength > ReglasImagen.MaxBytes)
            throw new ImagenInvalidaException(ReglasImagen.MensajeTamano);

        var formato = ReglasImagen.Detectar(datos);
        if (formato == FormatoImagen.Desconocido)
            throw new ImagenInvalidaException(ReglasImagen.MensajeFormato);

        using var flujo = new SKMemoryStream(datos);
        using var codec = SKCodec.Create(flujo) ?? throw new ImagenInvalidaException(ReglasImagen.MensajeDanada);
        if (codec.EncodedFormat != Esperado(formato))
            throw new ImagenInvalidaException(ReglasImagen.MensajeFormato);

        var info = codec.Info;
        var pixeles = (long)info.Width * info.Height;
        if (info.Width <= 0 || info.Height <= 0)
            throw new ImagenInvalidaException(ReglasImagen.MensajeDanada);
        if (info.Width > ReglasImagen.MaxLado || info.Height > ReglasImagen.MaxLado || pixeles > ReglasImagen.MaxPixeles
            || (formato == FormatoImagen.Png && pixeles > ReglasImagen.MaxPixelesPng))
            throw new ImagenInvalidaException(ReglasImagen.MensajeDemasiadoGrande);

        // JPEG y WebP pueden decodificarse ya reducidos (nunca por debajo de lo que se necesita).
        var escala = Math.Min(1f, (float)ReglasImagen.LadoGrande / Math.Max(info.Width, info.Height));
        var medidas = codec.GetScaledDimensions(escala);
        var destino = new SKImageInfo(medidas.Width, medidas.Height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        using var decodificada = new SKBitmap(destino);
        if (codec.GetPixels(destino, decodificada.GetPixels()) != SKCodecResult.Success)
            throw new ImagenInvalidaException(ReglasImagen.MensajeDanada);

        var gira = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var (ancho, alto) = gira ? (info.Height, info.Width) : (info.Width, info.Height);

        // La mayoria de las fotos ya vienen derechas: solo se copia la imagen si hay que girarla.
        using var girada = codec.EncodedOrigin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default ? null : Enderezar(decodificada, codec.EncodedOrigin);
        var derecha = girada ?? decodificada;
        var (anchoGrande, altoGrande) = ReglasImagen.Ajustar(ancho, alto, ReglasImagen.LadoGrande);
        var (anchoMiniatura, altoMiniatura) = ReglasImagen.Ajustar(ancho, alto, ReglasImagen.LadoMiniatura);
        using var grande = Redimensionar(derecha, anchoGrande, altoGrande);
        using var miniatura = Redimensionar(grande, anchoMiniatura, altoMiniatura);

        return new ImagenProcesada(
            ancho, alto,
            Webp(grande), anchoGrande, altoGrande,
            Webp(miniatura), anchoMiniatura, altoMiniatura);
    }

    private static SKEncodedImageFormat Esperado(FormatoImagen formato) => formato switch
    {
        FormatoImagen.Jpeg => SKEncodedImageFormat.Jpeg,
        FormatoImagen.Png => SKEncodedImageFormat.Png,
        _ => SKEncodedImageFormat.Webp
    };

    // Aplica la orientacion EXIF (las ocho posibles): la imagen guardada queda como se ve en la camara.
    private static SKBitmap Enderezar(SKBitmap fuente, SKEncodedOrigin origen)
    {
        float w = fuente.Width, h = fuente.Height;
        var (ancho, alto, matriz) = origen switch
        {
            SKEncodedOrigin.TopRight => (fuente.Width, fuente.Height, new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1)),
            SKEncodedOrigin.BottomRight => (fuente.Width, fuente.Height, new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1)),
            SKEncodedOrigin.BottomLeft => (fuente.Width, fuente.Height, new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1)),
            SKEncodedOrigin.LeftTop => (fuente.Height, fuente.Width, new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1)),
            SKEncodedOrigin.RightTop => (fuente.Height, fuente.Width, new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1)),
            SKEncodedOrigin.RightBottom => (fuente.Height, fuente.Width, new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1)),
            SKEncodedOrigin.LeftBottom => (fuente.Height, fuente.Width, new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1)),
            _ => (fuente.Width, fuente.Height, SKMatrix.Identity)
        };

        var resultado = new SKBitmap(new SKImageInfo(ancho, alto, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb()));
        try
        {
            using var lienzo = new SKCanvas(resultado);
            lienzo.Clear(SKColors.Transparent);
            lienzo.SetMatrix(matriz);
            // Girar o reflejar mueve pixeles enteros: no hace falta interpolar.
            using var imagen = SKImage.FromBitmap(fuente);
            lienzo.DrawImage(imagen, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
            return resultado;
        }
        catch
        {
            resultado.Dispose();
            throw;
        }
    }

    private static SKBitmap Redimensionar(SKBitmap fuente, int ancho, int alto)
    {
        var resultado = new SKBitmap(new SKImageInfo(ancho, alto, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb()));
        using var lienzo = new SKCanvas(resultado);
        lienzo.Clear(SKColors.Transparent);
        using var imagen = SKImage.FromBitmap(fuente);
        lienzo.DrawImage(imagen, new SKRect(0, 0, ancho, alto), Muestreo);
        return resultado;
    }

    private static byte[] Webp(SKBitmap mapa)
    {
        using var imagen = SKImage.FromBitmap(mapa);
        using var datos = imagen.Encode(SKEncodedImageFormat.Webp, Calidad)
            ?? throw new InvalidOperationException("No se pudo codificar la imagen en WebP.");
        return datos.ToArray();
    }
}

public sealed class OpcionesImagenes
{
    public const string Seccion = "Imagenes";

    // Fuera de la carpeta del sitio y del repositorio (.gitignore). Relativa a la carpeta de la API. Solo con Almacen:Modo=Disco.
    public string Carpeta { get; set; } = "almacen/imagenes";
}

// Archivos de las imagenes: "{clave}-grande.webp" y "{clave}-miniatura.webp". Solo se sirve un nombre
// que cumpla exactamente ese formato, asi una ruta como "../appsettings.json" nunca llega al almacen.
// Desde la fase 16C-1 el almacen es IAlmacenArchivos (disco o Blob).
public sealed partial class AlmacenImagenes(IAlmacenArchivos almacen)
{
    public static string NuevaClave() => RandomNumberGenerator.GetHexString(32, lowercase: true);
    public static string NombreGrande(string clave) => $"{clave}-grande.webp";
    public static string NombreMiniatura(string clave) => $"{clave}-miniatura.webp";

    // \z y no $: $ tambien aceptaria un salto de linea al final.
    [GeneratedRegex(@"^[0-9a-f]{32}-(grande|miniatura)\.webp\z")]
    private static partial Regex Nombre();

    public static bool NombreValido(string archivo) => Nombre().IsMatch(archivo);

    public async Task GuardarAsync(string clave, ImagenProcesada imagen, CancellationToken ct)
    {
        await almacen.GuardarAsync(AreasAlmacen.Imagenes, NombreGrande(clave), imagen.Grande, ct);
        await almacen.GuardarAsync(AreasAlmacen.Imagenes, NombreMiniatura(clave), imagen.Miniatura, ct);
    }

    // Limpieza tras un fallo al subir: sin lanzar, para no tapar el error original (y sin cancelacion: debe correr aunque la peticion se cancele).
    public async Task BorrarAsync(string clave)
    {
        foreach (var nombre in new[] { NombreGrande(clave), NombreMiniatura(clave) })
            await almacen.BorrarAsync(AreasAlmacen.Imagenes, nombre);
    }

    // La imagen abierta para enviarla, o null si el nombre no es de una imagen o no existe.
    public Task<ArchivoAbierto?> AbrirAsync(string archivo, CancellationToken ct) =>
        NombreValido(archivo) ? almacen.AbrirAsync(AreasAlmacen.Imagenes, archivo, ct) : Task.FromResult<ArchivoAbierto?>(null);
}
