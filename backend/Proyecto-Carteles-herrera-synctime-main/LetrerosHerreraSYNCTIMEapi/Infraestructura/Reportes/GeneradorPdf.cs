using LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;

// Fase 13A (RFS10, RNFP18, S18): el reporte como PDF (PDFsharp y MigraDoc, MIT). Hoja A4 horizontal con titulo, fecha de emision y
// filtros aplicados; cada tabla con su encabezado repetido en cada pagina y su fila de totales; «Página X de Y» al pie. El texto es
// TEXTO (fuente incrustada, seleccionable), no una imagen. En el gerencial, cada grafico va como imagen sobre la tabla de sus datos.
// Limite conocido: no es un PDF accesible etiquetado (PDF/UA); queda anotado en docs/plan/desviaciones.md.
public static class GeneradorPdf
{
    private const double AnchoUtilMm = 267;   // A4 horizontal (297 mm) menos 15 mm de margen a cada lado
    private static readonly Color Tinta = new(31, 41, 55);
    private static readonly Color Encabezado = new(220, 230, 242);
    private static readonly Color Linea = new(156, 163, 175);

    // PDFsharp guarda cache de fuentes por proceso: se arma un PDF a la vez.
    private static readonly SemaphoreSlim UnoALaVez = new(1, 1);

    public static byte[] Generar(DocumentoReporte documento, CancellationToken ct = default)
    {
        UnoALaVez.Wait(ct);
        try
        {
            FuentesReporte.Registrar();
            var pdf = Construir(documento, ct);
            var renderizador = new PdfDocumentRenderer { Document = pdf };
            renderizador.RenderDocument();
            using var memoria = new MemoryStream();
            renderizador.PdfDocument.Save(memoria, false);
            return memoria.ToArray();
        }
        finally
        {
            UnoALaVez.Release();
        }
    }

    private static Document Construir(DocumentoReporte d, CancellationToken ct)
    {
        var pdf = new Document();
        pdf.Info.Title = d.Titulo;
        pdf.Info.Author = "Letreros Herrera";

        var normal = pdf.Styles[StyleNames.Normal]!;
        normal.Font.Name = FuentesReporte.Familia;
        normal.Font.Size = 8.5;
        normal.Font.Color = Tinta;

        var seccion = pdf.AddSection();
        seccion.PageSetup.PageFormat = PageFormat.A4;
        seccion.PageSetup.Orientation = Orientation.Landscape;
        seccion.PageSetup.LeftMargin = Unit.FromMillimeter(15);
        seccion.PageSetup.RightMargin = Unit.FromMillimeter(15);
        seccion.PageSetup.TopMargin = Unit.FromMillimeter(14);
        seccion.PageSetup.BottomMargin = Unit.FromMillimeter(18);
        seccion.PageSetup.FooterDistance = Unit.FromMillimeter(8);

        var pie = seccion.Footers.Primary.AddParagraph();
        pie.Format.Alignment = ParagraphAlignment.Right;
        pie.Format.Font.Size = 8;
        pie.AddText("Página ");
        pie.AddPageField();
        pie.AddText(" de ");
        pie.AddNumPagesField();

        var titulo = seccion.AddParagraph(d.Titulo);
        titulo.Format.Font.Size = 17;
        titulo.Format.Font.Bold = true;
        titulo.Format.SpaceAfter = Unit.FromMillimeter(1);

        var emision = seccion.AddParagraph($"Emitido el {FormatoReporte.FechaHora(d.EmitidoEn)} (hora de Chile) · Letreros Herrera");
        emision.Format.Font.Size = 8.5;
        emision.Format.SpaceAfter = Unit.FromMillimeter(3);

        if (d.Filtros.Count > 0)
        {
            var cabecera = seccion.AddParagraph("Filtros aplicados");
            cabecera.Format.Font.Bold = true;
            cabecera.Format.SpaceAfter = Unit.FromMillimeter(0.5);
            foreach (var filtro in d.Filtros)
            {
                var linea = seccion.AddParagraph();
                linea.AddFormattedText($"{filtro.Etiqueta}: ", TextFormat.Bold);
                linea.AddText(filtro.Valor);
            }
        }
        foreach (var nota in d.Notas)
        {
            var parrafo = seccion.AddParagraph(nota);
            parrafo.Format.Font.Italic = false;
            parrafo.Format.SpaceBefore = Unit.FromMillimeter(1.5);
        }

        var sinTabla = d.Graficos.Where(g => d.Tablas.All(t => t.Clave != g.Clave)).ToList();
        foreach (var tabla in d.Tablas)
        {
            var subtitulo = seccion.AddParagraph(tabla.Titulo);
            subtitulo.Format.Font.Size = 12;
            subtitulo.Format.Font.Bold = true;
            subtitulo.Format.SpaceBefore = Unit.FromMillimeter(6);
            subtitulo.Format.SpaceAfter = Unit.FromMillimeter(1.5);
            subtitulo.Format.KeepWithNext = true;

            if (d.Graficos.FirstOrDefault(g => g.Clave == tabla.Clave) is { } grafico)
                Imagen(seccion, grafico);
            if (tabla.Nota is not null)
                seccion.AddParagraph(tabla.Nota).Format.SpaceAfter = Unit.FromMillimeter(1.5);
            Tabla(seccion, tabla, ct);
        }
        foreach (var grafico in sinTabla)
        {
            var subtitulo = seccion.AddParagraph(grafico.Titulo);
            subtitulo.Format.Font.Size = 12;
            subtitulo.Format.Font.Bold = true;
            subtitulo.Format.SpaceBefore = Unit.FromMillimeter(6);
            subtitulo.Format.KeepWithNext = true;
            Imagen(seccion, grafico);
        }
        return pdf;
    }

    private static void Imagen(Section seccion, GraficoReporte grafico)
    {
        var png = GraficosReporte.Dibujar(grafico);
        var parrafo = seccion.AddParagraph();
        parrafo.Format.KeepWithNext = true;
        parrafo.Format.SpaceAfter = Unit.FromMillimeter(2);
        var imagen = parrafo.AddImage("base64:" + Convert.ToBase64String(png));
        imagen.LockAspectRatio = true;
        imagen.Width = Unit.FromMillimeter(grafico.Forma == "barras-horizontales" ? 200 : 215);
    }

    private static void Tabla(Section seccion, TablaReporte t, CancellationToken ct)
    {
        if (t.Filas.Count == 0)
        {
            seccion.AddParagraph("Sin resultados para estos filtros.").Format.SpaceBefore = Unit.FromMillimeter(1);
            return;
        }

        var tabla = seccion.AddTable();
        tabla.Borders.Color = Linea;
        tabla.Borders.Width = 0.25;
        tabla.Format.Font.Size = 8;
        tabla.Rows.LeftIndent = 0;
        var anchos = Anchos(t);
        foreach (var ancho in anchos)
            tabla.AddColumn(Unit.FromMillimeter(ancho));

        var encabezado = tabla.AddRow();
        encabezado.HeadingFormat = true;   // se repite en cada pagina
        encabezado.Shading.Color = Encabezado;
        encabezado.Format.Font.Bold = true;
        encabezado.VerticalAlignment = VerticalAlignment.Center;
        for (var i = 0; i < t.Columnas.Count; i++)
            Celda(encabezado.Cells[i], t.Columnas[i].Titulo, Alineacion(t.Columnas[i]));

        foreach (var fila in t.Filas)
        {
            ct.ThrowIfCancellationRequested();
            var linea = tabla.AddRow();
            linea.VerticalAlignment = VerticalAlignment.Center;
            for (var i = 0; i < t.Columnas.Count; i++)
                Celda(linea.Cells[i], FormatoReporte.Celda(fila[i], t.Columnas[i]), Alineacion(t.Columnas[i]));
            if (t.Excluida(fila))
                linea.Format.Font.Color = new Color(107, 114, 128);
        }

        if (t.TieneTotales)
        {
            var totales = t.Totales;
            var resumen = tabla.AddRow();
            resumen.Format.Font.Bold = true;
            resumen.Shading.Color = Encabezado;
            for (var i = 0; i < t.Columnas.Count; i++)
            {
                var texto = i == 0 ? "Total" : totales[i] is decimal valor ? FormatoReporte.Celda(valor, t.Columnas[i]) : "";
                Celda(resumen.Cells[i], texto, Alineacion(t.Columnas[i]));
            }
        }
    }

    private static void Celda(Cell celda, string texto, ParagraphAlignment alineacion)
    {
        celda.Format.Alignment = alineacion;
        celda.Format.LeftIndent = Unit.FromMillimeter(1);
        celda.Format.RightIndent = Unit.FromMillimeter(1);
        celda.AddParagraph(texto);
    }

    private static ParagraphAlignment Alineacion(ColumnaReporte c) =>
        c.Tipo is TiposColumna.Pesos or TiposColumna.Entero or TiposColumna.Porcentaje ? ParagraphAlignment.Right : ParagraphAlignment.Left;

    // Repartir el ancho util segun lo que cada columna necesita (su titulo y lo mas largo que lleva), con un minimo por tipo.
    private static double[] Anchos(TablaReporte t)
    {
        var necesidad = t.Columnas.Select((c, i) =>
        {
            var contenido = t.Filas.Take(300).Select(f => FormatoReporte.Celda(f[i], c).Length).DefaultIfEmpty(0).Max();
            var minimo = c.Tipo switch { TiposColumna.Entero => 7, TiposColumna.Fecha => 11, TiposColumna.Pesos => 12, TiposColumna.Porcentaje => 9, _ => 10 };
            return Math.Clamp(Math.Max(Math.Max(c.Titulo.Length * 0.9, contenido), minimo), minimo, 38);
        }).ToArray();
        var escala = AnchoUtilMm / necesidad.Sum();
        return necesidad.Select(n => n * escala).ToArray();
    }
}
