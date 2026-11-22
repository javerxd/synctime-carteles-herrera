using LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;
using SkiaSharp;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;

// Fase 13A (RFS10): los graficos del reporte gerencial como imagen PNG para el PDF, dibujados con SkiaSharp (ya esta en el
// proyecto para las fotos). Barras agrupadas por etiqueta, o barras horizontales. Cada grafico va con la tabla de sus datos en el
// PDF, asi que la imagen es un apoyo visual y nada depende solo de ella.
public static class GraficosReporte
{
    private const int Ancho = 1500;
    private static readonly SKColor Tinta = SKColor.Parse("#1F2937");
    private static readonly SKColor Rejilla = SKColor.Parse("#D1D5DB");
    private static readonly SKColor[] Colores = [SKColor.Parse("#1E40AF"), SKColor.Parse("#B45309"), SKColor.Parse("#047857")];

    public static byte[] Dibujar(GraficoReporte grafico)
    {
        var horizontal = grafico.Forma == "barras-horizontales";
        var alto = horizontal ? Math.Clamp(120 + grafico.Etiquetas.Count * 64, 260, 1100) : 560;
        using var mapa = new SKBitmap(new SKImageInfo(Ancho, alto, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var lienzo = new SKCanvas(mapa))
        {
            lienzo.Clear(SKColors.White);
            using var datosFuente = SKData.CreateCopy(FuentesReporte.Datos(negrita: false));
            using var tipo = SKTypeface.FromData(datosFuente);
            if (horizontal)
                Horizontales(lienzo, grafico, tipo, alto);
            else
                Verticales(lienzo, grafico, tipo, alto);
        }
        using var imagen = SKImage.FromBitmap(mapa);
        using var datos = imagen.Encode(SKEncodedImageFormat.Png, 100);
        return datos.ToArray();
    }

    // ── Barras verticales agrupadas ──

    private static void Verticales(SKCanvas lienzo, GraficoReporte g, SKTypeface tipo, int alto)
    {
        using var fuente = new SKFont(tipo, 24);
        using var pincel = new SKPaint { IsAntialias = true, Color = Tinta };
        const float izquierda = 190, derecha = 40, arriba = 70, abajo = 70;
        var area = new SKRect(izquierda, arriba, Ancho - derecha, alto - abajo);
        var maximo = Math.Max(g.Series.SelectMany(s => s.Valores).DefaultIfEmpty(0).Max(), 1m);
        var tope = (float)Techo(maximo);

        // Rejilla y valores del eje.
        using var linea = new SKPaint { Color = Rejilla, StrokeWidth = 2, IsStroke = true };
        for (var i = 0; i <= 4; i++)
        {
            var y = area.Bottom - area.Height * i / 4;
            lienzo.DrawLine(area.Left, y, area.Right, y, linea);
            lienzo.DrawText(FormatoReporte.Pesos((decimal)(tope * i / 4)), area.Left - 14, y + 8, SKTextAlign.Right, fuente, pincel);
        }

        // Leyenda.
        var x = area.Left;
        for (var s = 0; s < g.Series.Count; s++)
        {
            using var color = new SKPaint { Color = Colores[s % Colores.Length], IsAntialias = true };
            lienzo.DrawRect(x, 22, 26, 26, color);
            lienzo.DrawText(g.Series[s].Nombre, x + 36, 44, SKTextAlign.Left, fuente, pincel);
            x += 90 + fuente.MeasureText(g.Series[s].Nombre);
        }

        var grupos = Math.Max(g.Etiquetas.Count, 1);
        var anchoGrupo = area.Width / grupos;
        var anchoBarra = Math.Min(anchoGrupo * 0.8f / Math.Max(g.Series.Count, 1), 90f);
        for (var i = 0; i < g.Etiquetas.Count; i++)
        {
            var inicio = area.Left + anchoGrupo * i + (anchoGrupo - anchoBarra * g.Series.Count) / 2;
            for (var s = 0; s < g.Series.Count; s++)
            {
                var valor = i < g.Series[s].Valores.Count ? (float)g.Series[s].Valores[i] : 0;
                var alturaBarra = area.Height * Math.Max(valor, 0) / tope;
                using var color = new SKPaint { Color = Colores[s % Colores.Length], IsAntialias = true };
                lienzo.DrawRect(inicio + anchoBarra * s, area.Bottom - alturaBarra, anchoBarra - 4, alturaBarra, color);
            }
            using var etiqueta = new SKFont(tipo, 22);
            lienzo.DrawText(Recortar(g.Etiquetas[i], 14), area.Left + anchoGrupo * i + anchoGrupo / 2, area.Bottom + 38, SKTextAlign.Center, etiqueta, pincel);
        }
    }

    // ── Barras horizontales (una serie) ──

    private static void Horizontales(SKCanvas lienzo, GraficoReporte g, SKTypeface tipo, int alto)
    {
        using var fuente = new SKFont(tipo, 24);
        using var pincel = new SKPaint { IsAntialias = true, Color = Tinta };
        const float izquierda = 380, derecha = 220, arriba = 24, abajo = 24;
        var serie = g.Series.FirstOrDefault();
        var maximo = Math.Max(serie?.Valores.DefaultIfEmpty(0).Max() ?? 0, 1m);
        var anchoTotal = Ancho - izquierda - derecha;
        var fila = (alto - arriba - abajo) / (float)Math.Max(g.Etiquetas.Count, 1);
        using var color = new SKPaint { Color = Colores[0], IsAntialias = true };
        for (var i = 0; i < g.Etiquetas.Count; i++)
        {
            var valor = serie is not null && i < serie.Valores.Count ? (float)serie.Valores[i] : 0;
            var y = arriba + fila * i;
            var largo = anchoTotal * Math.Max(valor, 0) / (float)maximo;
            lienzo.DrawText(Recortar(g.Etiquetas[i], 30), izquierda - 14, y + fila / 2 + 8, SKTextAlign.Right, fuente, pincel);
            lienzo.DrawRect(izquierda, y + fila * 0.18f, Math.Max(largo, 2), fila * 0.64f, color);
            lienzo.DrawText(FormatoReporte.Pesos((decimal)valor), izquierda + largo + 12, y + fila / 2 + 8, SKTextAlign.Left, fuente, pincel);
        }
    }

    /// <summary>El techo del eje: el maximo redondeado hacia arriba a una cifra «redonda» (1, 2 o 5 por una potencia de 10).</summary>
    private static decimal Techo(decimal maximo)
    {
        var potencia = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)maximo)));
        foreach (var paso in new[] { 1m, 2m, 2.5m, 5m, 10m })
            if (maximo <= paso * potencia)
                return paso * potencia;
        return 10 * potencia;
    }

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..(largo - 1)] + "…";
}
