using System.Diagnostics;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;
using LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;
using UglyToad.PdfPig;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 13A (RFS10, RNFP18, S18): los archivos se vuelven a abrir de verdad, con las mismas librerias que los leeria otra persona:
// el .xlsx con el SDK de Open XML (hojas, formatos de fecha y de numero, totales) y el PDF con PdfPig (texto seleccionable,
// encabezado repetido, «Página X de Y»). Lo que se prueba es el archivo, no el codigo que lo escribio.
public class ArchivosDeReportesTests
{
    private static readonly DateTimeOffset Emision = new(2026, 11, 20, 14, 35, 0, TimeSpan.FromHours(-3));

    private static TablaReporte Pedidos(int cantidad = 3)
    {
        var filas = new List<object?[]>
        {
            new object?[] { 1, "Ñandú Peña", new DateOnly(2026, 11, 5), "Entregado", 100_000, 25_000 },
            new object?[] { 2, "=1+1", new DateOnly(2026, 11, 6), "Anulado", 50_000, null },
            new object?[] { 3, "@Luisa", new DateOnly(2026, 11, 7), "En proceso", 60_000, 40_000 }
        };
        for (var i = 4; i <= cantidad; i++)
            filas.Add([i, $"Cliente {i}", new DateOnly(2026, 11, 8), "Entregado", 1_000, 500]);
        return new TablaReporte("pedidos", "Pedidos",
        [
            new ColumnaReporte("id", "N.º", TiposColumna.Entero),
            new ColumnaReporte("cliente", "Cliente", TiposColumna.Texto),
            new ColumnaReporte("fecha", "Fecha del pedido", TiposColumna.Fecha),
            new ColumnaReporte("estado", "Estado", TiposColumna.Texto),
            new ColumnaReporte("monto", "Monto", TiposColumna.Pesos, Suma: true),
            new ColumnaReporte("saldo", "Saldo", TiposColumna.Pesos, Suma: true, TextoNulo: "Sin costo")
        ], filas, Principal: true, Exclusion: new ExclusionDeTotales(3, "Anulado"));
    }

    private static DocumentoReporte Documento(int pedidos = 3, bool conGraficos = false)
    {
        var filtros = new List<FiltroAplicado>
        {
            new("periodo", "Período", "noviembre de 2026"),
            new("estadoPago", "Estado de pago", "Por cobrar")
        };
        var tablas = new List<TablaReporte> { Pedidos(pedidos) };
        var graficos = new List<GraficoReporte>();
        if (conGraficos)
        {
            tablas.Add(new TablaReporte("tramos", "Ingresos y costos por tramo",
            [
                new ColumnaReporte("tramo", "Tramo", TiposColumna.Texto),
                new ColumnaReporte("ingresos", "Ingresos", TiposColumna.Pesos, Suma: true),
                new ColumnaReporte("costos", "Costos", TiposColumna.Pesos, Suma: true)
            ], [["01-11", 300_000, 120_000], ["08-11", 450_000, 200_000], ["15-11", 0, 0]]));
            graficos.Add(new GraficoReporte("tramos", "Ingresos y costos por tramo", "barras", ["01-11", "08-11", "15-11"],
                [new SerieGrafico("Ingresos", [300_000, 450_000, 0]), new SerieGrafico("Costos", [120_000, 200_000, 0])]));
            graficos.Add(new GraficoReporte("tipos", "Ingresos por tipo de letrero", "barras-horizontales", ["Letrero luminoso", "Gráfica"],
                [new SerieGrafico("Ingresos", [600_000, 150_000])]));
        }
        return new DocumentoReporte("operativo", "Reporte operativo", Emision, filtros, tablas, graficos, ["Los pedidos anulados no suman en los totales."]);
    }

    // ── Excel ──

    private static SpreadsheetDocument Abrir(byte[] datos) => SpreadsheetDocument.Open(new MemoryStream(datos), false);

    private static WorksheetPart Hoja(SpreadsheetDocument libro, string nombre)
    {
        var hoja = libro.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Single(s => s.Name == nombre);
        return (WorksheetPart)libro.WorkbookPart.GetPartById(hoja.Id!);
    }

    private static Cell Celda(WorksheetPart hoja, string referencia) =>
        hoja.Worksheet.Descendants<Cell>().Single(c => c.CellReference == referencia);

    private static string Texto(SpreadsheetDocument libro, Cell celda)
    {
        if (celda.DataType?.Value == CellValues.InlineString)
            return celda.InlineString!.InnerText;
        if (celda.DataType?.Value == CellValues.SharedString)
            return libro.WorkbookPart!.SharedStringTablePart!.SharedStringTable.Elements<SharedStringItem>().ElementAt(int.Parse(celda.CellValue!.Text)).InnerText;
        return celda.CellValue?.Text ?? "";
    }

    private static CellFormat Estilo(SpreadsheetDocument libro, Cell celda) =>
        libro.WorkbookPart!.WorkbookStylesPart!.Stylesheet.CellFormats!.Elements<CellFormat>().ElementAt((int)(celda.StyleIndex?.Value ?? 0));

    private static string? CodigoDeFormato(SpreadsheetDocument libro, Cell celda)
    {
        var id = Estilo(libro, celda).NumberFormatId!.Value;
        return libro.WorkbookPart!.WorkbookStylesPart!.Stylesheet.NumberingFormats?.Elements<NumberingFormat>().FirstOrDefault(n => n.NumberFormatId!.Value == id)?.FormatCode?.Value;
    }

    [Fact]
    public void Excel_TieneUnaHojaPorTabla_YUnaHojaDeFiltros()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Documento(conGraficos: true)));

        var nombres = libro.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Select(s => s.Name!.Value).ToList();

        Assert.Equal(["Pedidos", "Ingresos y costos por tramo", "Filtros"], nombres);
    }

    [Fact]
    public void Excel_LosNombresDeHojaSonValidos_AunqueElTituloNoLoSea()
    {
        var tabla = Pedidos() with { Titulo = "Pedidos / entregas: [todas?] * con un título larguísimo que supera el límite" };
        using var libro = Abrir(GeneradorExcel.Generar(Documento() with { Tablas = [tabla] }));

        var nombre = libro.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().First().Name!.Value!;

        Assert.InRange(nombre.Length, 1, 31);
        Assert.DoesNotContain(nombre, c => "[]:*?/\\".Contains(c));
    }

    [Fact]
    public void Excel_LosEncabezadosVanEnNegrita_YFijosAlDesplazar()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Documento()));
        var hoja = Hoja(libro, "Pedidos");

        var encabezado = Celda(hoja, "B1");
        var fuente = libro.WorkbookPart!.WorkbookStylesPart!.Stylesheet.Fonts!.Elements<DocumentFormat.OpenXml.Spreadsheet.Font>().ElementAt((int)Estilo(libro, encabezado).FontId!.Value);
        Assert.Equal("Cliente", Texto(libro, encabezado));
        Assert.NotNull(fuente.Bold);

        var panel = hoja.Worksheet.Descendants<Pane>().Single();
        Assert.Equal(PaneStateValues.Frozen, panel.State!.Value);
        Assert.Equal(1D, panel.VerticalSplit!.Value);
        Assert.Equal("A2", panel.TopLeftCell!.Value);
    }

    [Fact]
    public void Excel_LasFechasSonFechas_NoTexto()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Documento()));
        var celda = Celda(Hoja(libro, "Pedidos"), "C2");

        // Una fecha es un numero de serie con formato de fecha: 5 de noviembre de 2026 es el dia 46331 de Excel.
        Assert.Null(celda.DataType);
        Assert.Equal("46331", celda.CellValue!.Text);
        Assert.Equal("dd-mm-yyyy", CodigoDeFormato(libro, celda));
    }

    [Fact]
    public void Excel_LosPesosSonNumeros_ConFormatoDeMiles()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Documento()));
        var celda = Celda(Hoja(libro, "Pedidos"), "E2");

        Assert.Null(celda.DataType);
        Assert.Equal("100000", celda.CellValue!.Text);
        Assert.Equal("#,##0", CodigoDeFormato(libro, celda));
    }

    [Fact]
    public void Excel_UnTextoQueEmpiezaComoFormula_SeEscribeComoTexto_NuncaComoFormula()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Documento()));
        var hoja = Hoja(libro, "Pedidos");

        foreach (var (referencia, esperado) in new[] { ("B3", "=1+1"), ("B4", "@Luisa") })
        {
            var celda = Celda(hoja, referencia);
            Assert.Null(celda.CellFormula);
            Assert.Equal(esperado, Texto(libro, celda));
            Assert.True(celda.DataType?.Value == CellValues.InlineString || celda.DataType?.Value == CellValues.SharedString);
        }
        // Las unicas formulas del libro son las de los totales.
        Assert.All(hoja.Worksheet.Descendants<CellFormula>(), f => Assert.StartsWith("SUM", f.Text));
    }

    [Fact]
    public void Excel_LaFilaDeTotales_SumaConFormula_YDejaFueraLosAnulados()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Documento()));
        var hoja = Hoja(libro, "Pedidos");

        // Fila 1 encabezado, filas 2 a 4 datos, fila 5 totales.
        Assert.Equal("Total", Texto(libro, Celda(hoja, "A5")));
        var monto = Celda(hoja, "E5");
        Assert.Equal("SUMIF(D2:D4,\"<>Anulado\",E2:E4)", monto.CellFormula!.Text);
        Assert.Equal("160000", monto.CellValue!.Text);   // 100.000 + 60.000: el anulado no suma
        Assert.Equal("65000", Celda(hoja, "F5").CellValue!.Text);
        Assert.Equal("#,##0", CodigoDeFormato(libro, monto));
        Assert.NotNull(Estilo(libro, monto).FontId);
    }

    [Fact]
    public void Excel_UnaCeldaVacia_LlevaSuTextoDeVacio_YNoRompeLaSuma()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Documento()));
        var hoja = Hoja(libro, "Pedidos");

        Assert.Equal("Sin costo", Texto(libro, Celda(hoja, "F3")));
        Assert.Equal("65000", Celda(hoja, "F5").CellValue!.Text);
    }

    [Fact]
    public void Excel_LaHojaDeFiltros_DiceLoQueSeAplicoYCuandoSeEmitio()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Documento()));
        var hoja = Hoja(libro, "Filtros");
        var textos = hoja.Worksheet.Descendants<Cell>().Select(c => Texto(libro, c)).ToList();

        Assert.Contains("Período", textos);
        Assert.Contains("noviembre de 2026", textos);
        Assert.Contains("Por cobrar", textos);
        Assert.Contains("Reporte operativo", textos);
        Assert.Contains(textos, t => t.Contains("20-11-2026 14:35"));
        Assert.Contains("Los pedidos anulados no suman en los totales.", textos);
    }

    [Fact]
    public void Excel_LasColumnasTienenAnchoRazonable()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Documento()));
        var columnas = Hoja(libro, "Pedidos").Worksheet.Descendants<Column>().ToList();

        Assert.Equal(6, columnas.Count);
        Assert.All(columnas, c => Assert.InRange(c.Width!.Value, 8, 60));
        Assert.All(columnas, c => Assert.True(c.CustomWidth!.Value));
    }

    [Fact]
    public void Excel_UnaTablaSinFilas_SigueSiendoUnLibroValido_ConTotalesEnCero()
    {
        var vacia = Pedidos() with { Filas = [] };
        using var libro = Abrir(GeneradorExcel.Generar(Documento() with { Tablas = [vacia] }));
        var hoja = Hoja(libro, "Pedidos");

        Assert.Equal("Total", Texto(libro, Celda(hoja, "A2")));
        Assert.Equal("0", Celda(hoja, "E2").CellValue!.Text);
    }

    // ── PDF ──

    private static string TextoDe(UglyToad.PdfPig.Content.Page pagina) => string.Join(" ", pagina.GetWords().Select(p => p.Text));

    [Fact]
    public void Pdf_ElTextoEsSeleccionable_NoUnaImagen()
    {
        using var pdf = PdfDocument.Open(GeneradorPdf.Generar(Documento()));
        var texto = TextoDe(pdf.GetPage(1));

        Assert.Contains("Reporte operativo", texto);
        Assert.Contains("Ñandú", texto);
        Assert.Contains("Peña", texto);
        Assert.Contains("Entregado", texto);
        Assert.Empty(pdf.GetPage(1).GetImages());
    }

    [Fact]
    public void Pdf_LlevaLaFuenteIncrustada()
    {
        using var pdf = PdfDocument.Open(GeneradorPdf.Generar(Documento()));
        var fuentes = pdf.GetPage(1).Letters.Select(l => l.FontName).Distinct().ToList();

        Assert.NotEmpty(fuentes);
        Assert.All(fuentes, f => Assert.Contains("PlusJakartaSans", f!.Replace(" ", "")));
    }

    [Fact]
    public void Pdf_EsUnaHojaA4Horizontal()
    {
        using var pdf = PdfDocument.Open(GeneradorPdf.Generar(Documento()));
        var pagina = pdf.GetPage(1);

        Assert.InRange(pagina.Width, 841, 843);
        Assert.InRange(pagina.Height, 594, 596);
    }

    [Fact]
    public void Pdf_LlevaElTitulo_LaFechaDeEmision_YLosFiltrosAplicados()
    {
        using var pdf = PdfDocument.Open(GeneradorPdf.Generar(Documento()));
        var texto = TextoDe(pdf.GetPage(1));

        Assert.Contains("Reporte operativo", texto);
        Assert.Contains("20-11-2026 14:35", texto);
        Assert.Contains("Filtros aplicados", texto);
        Assert.Contains("noviembre de 2026", texto);
        Assert.Contains("Por cobrar", texto);
    }

    [Fact]
    public void Pdf_NumeraCadaPagina_PaginaXDeY_ConElTotalReal()
    {
        using var pdf = PdfDocument.Open(GeneradorPdf.Generar(Documento(pedidos: 200)));

        Assert.True(pdf.NumberOfPages >= 3, $"200 filas deberian ocupar varias paginas; ocuparon {pdf.NumberOfPages}.");
        for (var i = 1; i <= pdf.NumberOfPages; i++)
            Assert.Contains($"Página {i} de {pdf.NumberOfPages}", string.Join(" ", pdf.GetPage(i).GetWords().Select(p => p.Text)));
    }

    [Fact]
    public void Pdf_ElEncabezadoDeLaTabla_SeRepiteEnCadaPagina()
    {
        using var pdf = PdfDocument.Open(GeneradorPdf.Generar(Documento(pedidos: 200)));

        Assert.True(pdf.NumberOfPages >= 2);
        for (var i = 1; i <= pdf.NumberOfPages; i++)
        {
            var texto = TextoDe(pdf.GetPage(i));
            Assert.Contains("Cliente", texto);
            Assert.Contains("Monto", texto);
        }
    }

    [Fact]
    public void Pdf_LaFilaDeTotales_ApareceAlFinal_ConLaSumaSinLosAnulados()
    {
        using var pdf = PdfDocument.Open(GeneradorPdf.Generar(Documento()));
        var texto = TextoDe(pdf.GetPage(1));

        Assert.Contains("Total", texto);
        Assert.Contains("$160.000", texto);
        Assert.Contains("$65.000", texto);
    }

    [Fact]
    public void Pdf_ElGerencial_LlevaSusGraficosComoImagen_ConLaTablaDeSusDatos()
    {
        using var pdf = PdfDocument.Open(GeneradorPdf.Generar(Documento(conGraficos: true)));
        var imagenes = Enumerable.Range(1, pdf.NumberOfPages).Sum(i => pdf.GetPage(i).GetImages().Count());
        var texto = string.Join(" ", Enumerable.Range(1, pdf.NumberOfPages).Select(i => TextoDe(pdf.GetPage(i))));

        Assert.Equal(2, imagenes);
        Assert.Contains("Ingresos y costos por tramo", texto);
        Assert.Contains("$450.000", texto);   // el dato del grafico tambien esta escrito, no solo dibujado
    }

    [Fact]
    public void Pdf_UnaTablaSinFilas_SigueSiendoUnPdfValido()
    {
        using var pdf = PdfDocument.Open(GeneradorPdf.Generar(Documento() with { Tablas = [Pedidos() with { Filas = [] }] }));

        Assert.Equal(1, pdf.NumberOfPages);
        Assert.Contains("Sin resultados", TextoDe(pdf.GetPage(1)));
    }

    [Fact]
    public void Pdf_NadaQuedaCortadoNiFueraDeLaHoja_NiLasImagenesNiElTexto()
    {
        using var pdf = PdfDocument.Open(GeneradorPdf.Generar(Documento(pedidos: 60, conGraficos: true)));

        foreach (var pagina in pdf.GetPages())
        {
            Assert.All(pagina.Letters, l =>
            {
                Assert.InRange(l.BoundingBox.Left, 30, pagina.Width - 30);
                Assert.InRange(l.BoundingBox.Right, 30, pagina.Width - 30);
                Assert.InRange(l.BoundingBox.Bottom, 20, pagina.Height - 20);
            });
            Assert.All(pagina.GetImages(), i =>
            {
                Assert.InRange(i.BoundingBox.Left, 30, pagina.Width - 30);
                Assert.InRange(i.BoundingBox.Right, 30, pagina.Width - 30);
                Assert.InRange(i.BoundingBox.Top, 20, pagina.Height - 20);
                Assert.InRange(i.BoundingBox.Bottom, 20, pagina.Height - 20);
            });
        }
    }

    [Fact]
    public void LaFechaDeEmision_EsLaHoraDeChile_AunqueElServidorEsteEnUtc()
    {
        // 00:30 UTC del 21 de noviembre son las 21:30 del 20 en Chile (verano, UTC−3).
        var documento = Documento() with { EmitidoEn = new DateTimeOffset(2026, 11, 21, 0, 30, 0, TimeSpan.Zero) };

        using var pdf = PdfDocument.Open(GeneradorPdf.Generar(documento));
        Assert.Contains("20-11-2026 21:30", string.Join(" ", pdf.GetPage(1).GetWords().Select(p => p.Text)));
        using var libro = Abrir(GeneradorExcel.Generar(documento));
        Assert.Contains(Hoja(libro, "Filtros").Worksheet.Descendants<Cell>(), c => Texto(libro, c).Contains("20-11-2026 21:30"));
    }

    [Fact]
    public void Excel_UnTextoConSustitutosSueltos_NoHaceFallarElLibro()
    {
        var tabla = Pedidos() with { Filas = [[1, "Ana \uD83D suelto \uDE00 y par 😀", new DateOnly(2026, 11, 5), "Entregado", 1, 1]] };

        using var libro = Abrir(GeneradorExcel.Generar(Documento() with { Tablas = [tabla] }));

        var texto = Texto(libro, Celda(Hoja(libro, "Pedidos"), "B2"));
        Assert.Contains("😀", texto);       // el par valido se conserva
        Assert.Contains("�", texto);             // el suelto se reemplaza
    }

    // Ayuda para quien revisa: con SYNCTIME_VOLCAR_REPORTES=<carpeta> deja los tres archivos de ejemplo para abrirlos a mano.
    [Fact]
    public void VolcarEjemplos_SoloSiSePide()
    {
        if (Environment.GetEnvironmentVariable("SYNCTIME_VOLCAR_REPORTES") is not { Length: > 0 } carpeta)
            return;
        Directory.CreateDirectory(carpeta);
        var documento = Documento(pedidos: 60, conGraficos: true);
        File.WriteAllBytes(Path.Combine(carpeta, "ejemplo.pdf"), GeneradorPdf.Generar(documento));
        File.WriteAllBytes(Path.Combine(carpeta, "ejemplo.xlsx"), GeneradorExcel.Generar(documento));
        File.WriteAllBytes(Path.Combine(carpeta, "ejemplo.csv"), GeneradorCsv.Generar(documento));
    }

    // Medicion (no es una prueba): con SYNCTIME_MEDIR_REPORTES=1 escribe cuanto tardan y pesan los tres formatos con 10.000 y 50.000 filas.
    [Fact]
    public void Medir_Rendimiento_SoloSiSePide()
    {
        if (Environment.GetEnvironmentVariable("SYNCTIME_MEDIR_REPORTES") != "1")
            return;
        var lineas = new List<string>();
        foreach (var filas in new[] { 10_000, 50_000 })
        {
            var documento = Documento(pedidos: filas);
            foreach (var (nombre, generar) in new (string, Func<byte[]>)[]
                { ("csv", () => GeneradorCsv.Generar(documento)), ("xlsx", () => GeneradorExcel.Generar(documento)), ("pdf", () => GeneradorPdf.Generar(documento)) })
            {
                var memoria = GC.GetTotalMemory(true);
                var reloj = Stopwatch.StartNew();
                var bytes = generar();
                reloj.Stop();
                lineas.Add($"{filas} filas, {nombre}: {reloj.Elapsed.TotalSeconds:F1} s, {bytes.Length / 1024} KB, memoria +{(GC.GetTotalMemory(false) - memoria) / 1024 / 1024} MB");
            }
        }
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "synctime-medicion-reportes.txt"), lineas);
    }

    [Fact]
    public void Pdf_DosMilFilas_SeGeneranEnUnTiempoRazonable()
    {
        var reloj = Stopwatch.StartNew();
        var bytes = GeneradorPdf.Generar(Documento(pedidos: 2_000));
        reloj.Stop();

        using var pdf = PdfDocument.Open(bytes);
        Assert.True(pdf.NumberOfPages > 10);
        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(30), $"2.000 filas tardaron {reloj.Elapsed.TotalSeconds:F1} s.");
    }
}
