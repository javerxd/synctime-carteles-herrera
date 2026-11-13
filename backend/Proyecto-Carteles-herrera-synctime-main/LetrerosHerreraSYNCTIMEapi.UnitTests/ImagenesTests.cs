using System.Buffers.Binary;
using System.Text;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Imagenes;
using LetrerosHerreraSYNCTIMEapi.Negocio.Imagenes;
using SkiaSharp;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Subida de imagenes (RNFP22): el tipo se decide por el contenido, nunca por el nombre ni por lo que
// diga el navegador; se guardan en WebP en dos tamaños, sin agrandar, derechas y sin metadatos.
public class ImagenesTests
{
    // ── Imagenes de prueba ──

    private static byte[] Imagen(int ancho, int alto, SKEncodedImageFormat formato, bool transparente = false)
    {
        using var mapa = new SKBitmap(new SKImageInfo(ancho, alto, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var lienzo = new SKCanvas(mapa))
        {
            lienzo.Clear(transparente ? SKColors.Transparent : SKColors.White);
            using var pincel = new SKPaint { Color = new SKColor(0xB4, 0x53, 0x09) };
            // Una franja en la mitad izquierda: permite saber si la imagen se giro.
            lienzo.DrawRect(0, 0, ancho / 2f, alto, pincel);
        }
        using var imagen = SKImage.FromBitmap(mapa);
        return imagen.Encode(formato, 90).ToArray();
    }

    private static (int Ancho, int Alto) Medidas(byte[] webp)
    {
        using var codec = SKCodec.Create(new MemoryStream(webp));
        Assert.NotNull(codec);
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        return (codec.Info.Width, codec.Info.Height);
    }

    private static bool EsWebp(byte[] datos) =>
        datos.Length > 12 && Encoding.ASCII.GetString(datos, 0, 4) == "RIFF" && Encoding.ASCII.GetString(datos, 8, 4) == "WEBP";

    // ── Deteccion por contenido ──

    [Fact]
    public void ElFormato_SeDetectaPorLosPrimerosBytes()
    {
        Assert.Equal(FormatoImagen.Jpeg, ReglasImagen.Detectar(Imagen(4, 4, SKEncodedImageFormat.Jpeg)));
        Assert.Equal(FormatoImagen.Png, ReglasImagen.Detectar(Imagen(4, 4, SKEncodedImageFormat.Png)));
        Assert.Equal(FormatoImagen.Webp, ReglasImagen.Detectar(Imagen(4, 4, SKEncodedImageFormat.Webp)));
    }

    [Theory]
    [InlineData("GIF89a\x01\x00\x01\x00")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("<?xml version=\"1.0\"?><svg/>")]
    [InlineData("%PDF-1.7")]
    [InlineData("")]
    public void LoQueNoEsJpegPngNiWebp_NoSeAcepta(string contenido)
    {
        Assert.Equal(FormatoImagen.Desconocido, ReglasImagen.Detectar(Encoding.Latin1.GetBytes(contenido)));
    }

    // ── Tamaños ──

    [Theory]
    [InlineData(3000, 2000, 1600, 1600, 1067)]
    [InlineData(2000, 3000, 640, 427, 640)]
    [InlineData(300, 200, 1600, 300, 200)]
    [InlineData(1600, 1600, 1600, 1600, 1600)]
    public void LasMedidas_SeAjustanAlLadoMayorSinAgrandar(int ancho, int alto, int lado, int anchoEsperado, int altoEsperado)
    {
        Assert.Equal((anchoEsperado, altoEsperado), ReglasImagen.Ajustar(ancho, alto, lado));
    }

    [Fact]
    public void UnaFoto_SeGuardaEnWebpEnDosTamanos()
    {
        var resultado = ProcesadorImagenes.Procesar(Imagen(3000, 2000, SKEncodedImageFormat.Png));

        Assert.Equal((3000, 2000), (resultado.Ancho, resultado.Alto));
        Assert.True(EsWebp(resultado.Grande));
        Assert.True(EsWebp(resultado.Miniatura));
        Assert.Equal((1600, 1067), Medidas(resultado.Grande));
        Assert.Equal((640, 427), Medidas(resultado.Miniatura));
        Assert.Equal((1600, 1067), (resultado.AnchoGrande, resultado.AltoGrande));
        Assert.Equal((640, 427), (resultado.AnchoMiniatura, resultado.AltoMiniatura));
        Assert.True(resultado.Miniatura.Length < resultado.Grande.Length);
    }

    [Fact]
    public void UnaFotoPequena_NoSeAgranda()
    {
        var resultado = ProcesadorImagenes.Procesar(Imagen(300, 200, SKEncodedImageFormat.Jpeg));

        Assert.Equal((300, 200), Medidas(resultado.Grande));
        Assert.Equal((300, 200), Medidas(resultado.Miniatura));
    }

    [Fact]
    public void UnaFotoGrandeDeCelular_SeProcesaSinDecodificarlaCompleta()
    {
        var resultado = ProcesadorImagenes.Procesar(Imagen(4000, 3000, SKEncodedImageFormat.Jpeg));

        Assert.Equal((4000, 3000), (resultado.Ancho, resultado.Alto));
        Assert.Equal((1600, 1200), Medidas(resultado.Grande));
        Assert.Equal((640, 480), Medidas(resultado.Miniatura));
    }

    [Fact]
    public void UnPngConTransparencia_LaConserva()
    {
        var resultado = ProcesadorImagenes.Procesar(Imagen(200, 100, SKEncodedImageFormat.Png, transparente: true));

        using var mapa = SKBitmap.Decode(resultado.Grande);
        Assert.Equal(0, mapa.GetPixel(190, 50).Alpha);
        Assert.Equal(255, mapa.GetPixel(10, 50).Alpha);
    }

    // ── Orientacion y metadatos ──

    // Inserta un segmento EXIF con la orientacion indicada, como el que agregan las camaras de los celulares.
    private static byte[] ConOrientacionExif(byte[] jpeg, ushort orientacion)
    {
        var tiff = new byte[26];
        "MM\0*"u8.CopyTo(tiff);
        BinaryPrimitives.WriteUInt32BigEndian(tiff.AsSpan(4), 8);      // primer directorio
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(8), 1);      // una entrada
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(10), 0x0112); // Orientation
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(12), 3);      // SHORT
        BinaryPrimitives.WriteUInt32BigEndian(tiff.AsSpan(14), 1);      // un valor
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(18), orientacion);

        var app1 = new List<byte> { 0xFF, 0xE1 };
        var largo = (ushort)(2 + 6 + tiff.Length);
        app1.Add((byte)(largo >> 8));
        app1.Add((byte)largo);
        app1.AddRange("Exif\0\0"u8.ToArray());
        app1.AddRange(tiff);

        return [.. jpeg[..2], .. app1, .. jpeg[2..]];
    }

    [Fact]
    public void UnaFotoTomadaDeLado_SeGuardaDerecha()
    {
        // Orientacion 6: la camara guardo la foto acostada y pide girarla 90 grados al mostrarla.
        var jpeg = ConOrientacionExif(Imagen(400, 200, SKEncodedImageFormat.Jpeg), orientacion: 6);
        using (var codec = SKCodec.Create(new MemoryStream(jpeg)))
            Assert.Equal(SKEncodedOrigin.RightTop, codec.EncodedOrigin);

        var resultado = ProcesadorImagenes.Procesar(jpeg);

        Assert.Equal((200, 400), (resultado.Ancho, resultado.Alto));
        Assert.Equal((200, 400), Medidas(resultado.Grande));
        // Girada 90 grados a la derecha, la franja que estaba a la izquierda queda arriba.
        using var mapa = SKBitmap.Decode(resultado.Grande);
        Assert.True(mapa.GetPixel(100, 20).Red > 150 && mapa.GetPixel(100, 20).Blue < 80);
        Assert.True(mapa.GetPixel(100, 380).Blue > 200);
    }

    [Fact]
    public void LaImagenGuardada_NoConservaLosMetadatosDeLaCamara()
    {
        var jpeg = ConOrientacionExif(Imagen(400, 200, SKEncodedImageFormat.Jpeg), orientacion: 1);
        Assert.Contains("Exif", Encoding.Latin1.GetString(jpeg));

        var resultado = ProcesadorImagenes.Procesar(jpeg);

        Assert.DoesNotContain("Exif", Encoding.Latin1.GetString(resultado.Grande));
        Assert.DoesNotContain("Exif", Encoding.Latin1.GetString(resultado.Miniatura));
    }

    // ── Archivos que no se aceptan ──

    [Fact]
    public void UnArchivoDeTexto_NoSeAcepta()
    {
        var error = Assert.Throws<ImagenInvalidaException>(() => ProcesadorImagenes.Procesar("hola, no soy una imagen"u8.ToArray()));
        Assert.Contains("JPG, PNG o WebP", error.Message);
    }

    [Fact]
    public void UnSvg_NoSeAcepta()
    {
        Assert.Throws<ImagenInvalidaException>(() =>
            ProcesadorImagenes.Procesar("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray()));
    }

    [Fact]
    public void UnaImagenDanada_NoSeAcepta()
    {
        var png = Imagen(300, 200, SKEncodedImageFormat.Png);
        var danada = png[..(png.Length / 3)];

        Assert.Throws<ImagenInvalidaException>(() => ProcesadorImagenes.Procesar(danada));
    }

    [Fact]
    public void UnaImagenDeDimensionesEnormes_SeRechazaSinDecodificarla()
    {
        // Un PNG pequeño que declara 20.000 x 20.000 pixeles: decodificarlo pediria 1,6 GB de memoria.
        var error = Assert.Throws<ImagenInvalidaException>(() => ProcesadorImagenes.Procesar(PngQueDeclara(20_000, 20_000)));
        Assert.Contains("demasiado grande", error.Message);
    }

    [Fact]
    public void UnArchivoDeMasDeDiezMegas_NoSeAcepta()
    {
        var error = Assert.Throws<ImagenInvalidaException>(() => ProcesadorImagenes.Procesar(new byte[ReglasImagen.MaxBytes + 1]));
        Assert.Contains("10 MB", error.Message);
    }

    private static byte[] PngQueDeclara(int ancho, int alto)
    {
        var salida = new List<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, ancho);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), alto);
        ihdr[8] = 8;  // bits por canal
        ihdr[9] = 6;  // RGBA
        Fragmento(salida, "IHDR", ihdr);
        Fragmento(salida, "IDAT", []);
        Fragmento(salida, "IEND", []);
        return [.. salida];
    }

    private static void Fragmento(List<byte> salida, string tipo, byte[] datos)
    {
        var largo = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(largo, datos.Length);
        salida.AddRange(largo);
        var tipoYDatos = Encoding.ASCII.GetBytes(tipo).Concat(datos).ToArray();
        salida.AddRange(tipoYDatos);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(tipoYDatos));
        salida.AddRange(crc);
    }

    private static uint Crc32(byte[] datos)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in datos)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
