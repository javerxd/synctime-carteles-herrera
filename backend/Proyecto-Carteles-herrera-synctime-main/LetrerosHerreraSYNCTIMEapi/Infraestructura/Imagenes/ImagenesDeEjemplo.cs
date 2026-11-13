using SkiaSharp;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Imagenes;

// Ilustraciones de ejemplo para la vitrina de la base demo, dibujadas aqui mismo: no son fotos de
// trabajos reales ni se descargan de terceros. La vitrina las rotula «Imagen de ejemplo» hasta que el
// taller suba sus fotos (fase 9).
public static class ImagenesDeEjemplo
{
    public sealed record Diseno(string Rotulo, string Detalle, SKColor Muro, SKColor Letrero, SKColor Texto, Forma Forma);

    public enum Forma { Fachada, Lienzo, Palomita, Vehiculo, Muro }

    public static byte[] Dibujar(Diseno diseno, int ancho = 1600, int alto = 1200)
    {
        using var mapa = new SKBitmap(ancho, alto);
        using var lienzo = new SKCanvas(mapa);

        // Muro con una luz suave desde arriba y una vereda abajo.
        using (var muro = new SKPaint { IsAntialias = true })
        {
            muro.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, alto),
                [Aclarar(diseno.Muro, 0.12f), diseno.Muro], SKShaderTileMode.Clamp);
            lienzo.DrawRect(0, 0, ancho, alto, muro);
        }
        using (var vereda = new SKPaint { Color = Oscurecer(diseno.Muro, 0.35f), IsAntialias = true })
            lienzo.DrawRect(0, alto * 0.82f, ancho, alto * 0.18f, vereda);

        var placa = diseno.Forma switch
        {
            Forma.Palomita => new SKRect(ancho * 0.36f, alto * 0.22f, ancho * 0.64f, alto * 0.78f),
            Forma.Lienzo => new SKRect(ancho * 0.08f, alto * 0.30f, ancho * 0.92f, alto * 0.58f),
            Forma.Vehiculo => new SKRect(ancho * 0.14f, alto * 0.30f, ancho * 0.86f, alto * 0.66f),
            Forma.Muro => new SKRect(ancho * 0.06f, alto * 0.12f, ancho * 0.94f, alto * 0.78f),
            _ => new SKRect(ancho * 0.12f, alto * 0.18f, ancho * 0.88f, alto * 0.52f)
        };
        var radio = diseno.Forma == Forma.Vehiculo ? 90f : 14f;

        // Sombra y placa del letrero.
        using (var sombra = new SKPaint { Color = new SKColor(0, 0, 0, 60), IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 18) })
            lienzo.DrawRoundRect(new SKRect(placa.Left - 6, placa.Top + 18, placa.Right + 6, placa.Bottom + 30), radio, radio, sombra);
        using (var fondo = new SKPaint { Color = diseno.Letrero, IsAntialias = true })
            lienzo.DrawRoundRect(placa, radio, radio, fondo);
        using (var borde = new SKPaint { Color = Aclarar(diseno.Letrero, 0.25f), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 8 })
            lienzo.DrawRoundRect(new SKRect(placa.Left + 18, placa.Top + 18, placa.Right - 18, placa.Bottom - 18), 8, 8, borde);

        if (diseno.Forma == Forma.Palomita)
        {
            using var pata = new SKPaint { Color = Oscurecer(diseno.Letrero, 0.4f), IsAntialias = true, StrokeWidth = 14, StrokeCap = SKStrokeCap.Round };
            lienzo.DrawLine(placa.Left + 30, placa.Bottom, placa.Left - 10, alto * 0.86f, pata);
            lienzo.DrawLine(placa.Right - 30, placa.Bottom, placa.Right + 10, alto * 0.86f, pata);
        }
        if (diseno.Forma == Forma.Vehiculo)
        {
            using var rueda = new SKPaint { Color = new SKColor(0x1E, 0x29, 0x3B), IsAntialias = true };
            lienzo.DrawCircle(placa.Left + placa.Width * 0.22f, placa.Bottom, alto * 0.08f, rueda);
            lienzo.DrawCircle(placa.Right - placa.Width * 0.22f, placa.Bottom, alto * 0.08f, rueda);
        }

        var tipografia = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default;
        using var texto = new SKPaint { Color = diseno.Texto, IsAntialias = true };
        using var fuente = new SKFont(tipografia, 100);
        // El rotulo ocupa a lo mas el 80 % del ancho de la placa y un tercio de su alto.
        fuente.Size = Math.Min(placa.Height * 0.32f, 100 * placa.Width * 0.8f / Math.Max(1f, fuente.MeasureText(diseno.Rotulo)));
        lienzo.DrawText(diseno.Rotulo, placa.MidX, placa.MidY + fuente.Size * 0.15f, SKTextAlign.Center, fuente, texto);

        using var fuenteDetalle = new SKFont(tipografia, fuente.Size * 0.32f);
        using var detalle = new SKPaint { Color = diseno.Texto.WithAlpha(210), IsAntialias = true };
        lienzo.DrawText(diseno.Detalle, placa.MidX, placa.MidY + fuente.Size * 0.15f + fuenteDetalle.Size * 1.6f, SKTextAlign.Center, fuenteDetalle, detalle);

        using var imagen = SKImage.FromBitmap(mapa);
        using var png = imagen.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static SKColor Aclarar(SKColor c, float f) =>
        new((byte)(c.Red + (255 - c.Red) * f), (byte)(c.Green + (255 - c.Green) * f), (byte)(c.Blue + (255 - c.Blue) * f));

    private static SKColor Oscurecer(SKColor c, float f) =>
        new((byte)(c.Red * (1 - f)), (byte)(c.Green * (1 - f)), (byte)(c.Blue * (1 - f)));
}
