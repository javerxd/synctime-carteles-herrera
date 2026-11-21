using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;

// Fase 13A (RFS10, RNFP18, S18): el reporte como libro de Excel (.xlsx), escrito con el SDK de Open XML.
//   - Una hoja por tabla, con el encabezado en negrita y fijo al desplazar, y los anchos de columna ajustados.
//   - Las fechas son fechas (numero de serie con formato dd-mm-yyyy) y los pesos son numeros con formato de miles: no texto.
//   - La fila de totales usa formulas SUM (o SUMIF si hay filas excluidas, como los anulados), con su valor ya calculado.
//   - Las celdas de texto se escriben SIEMPRE como texto (cadenas en linea), nunca como formula: «=1+1» queda como texto.
//   - Una hoja «Filtros» con lo que se aplico y la fecha de emision.
public static class GeneradorExcel
{
    private const int MaximoCaracteresPorCelda = 32_000;

    // Posiciones en la lista de formatos de celda (ver Estilos).
    private const uint EstiloEncabezado = 1, EstiloTexto = 2, EstiloEntero = 3, EstiloPesos = 4, EstiloFecha = 5, EstiloPorcentaje = 6,
        EstiloTotalTexto = 7, EstiloTotalPesos = 8, EstiloTotalEntero = 9, EstiloTitulo = 10;

    public static byte[] Generar(DocumentoReporte documento, CancellationToken ct = default)
    {
        using var memoria = new MemoryStream();
        using (var libro = SpreadsheetDocument.Create(memoria, SpreadsheetDocumentType.Workbook))
        {
            var parteLibro = libro.AddWorkbookPart();
            parteLibro.Workbook = new Workbook();
            parteLibro.AddNewPart<WorkbookStylesPart>().Stylesheet = Estilos();
            var hojas = parteLibro.Workbook.AppendChild(new Sheets());

            var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            uint numero = 1;
            foreach (var tabla in documento.Tablas)
                AgregarHoja(parteLibro, hojas, numero++, NombreDeHoja(tabla.Titulo, nombres), HojaDeTabla(tabla, ct));
            AgregarHoja(parteLibro, hojas, numero, NombreDeHoja("Filtros", nombres), HojaDeFiltros(documento));
            parteLibro.Workbook.Save();
        }
        return memoria.ToArray();
    }

    private static void AgregarHoja(WorkbookPart libro, Sheets hojas, uint id, string nombre, Worksheet contenido)
    {
        var parte = libro.AddNewPart<WorksheetPart>();
        parte.Worksheet = contenido;
        hojas.Append(new Sheet { Id = libro.GetIdOfPart(parte), SheetId = id, Name = nombre });
    }

    // Maximo 31 caracteres, sin []:*?/\ y sin repetirse (Excel lo exige).
    private static string NombreDeHoja(string titulo, HashSet<string> usados)
    {
        var limpio = new string(titulo.Select(c => "[]:*?/\\".Contains(c) ? ' ' : c).ToArray()).Trim();
        if (limpio.Length == 0)
            limpio = "Hoja";
        var nombre = limpio.Length > 31 ? limpio[..31].TrimEnd() : limpio;
        for (var n = 2; !usados.Add(nombre); n++)
        {
            var sufijo = $" ({n})";
            nombre = (limpio.Length + sufijo.Length > 31 ? limpio[..(31 - sufijo.Length)] : limpio) + sufijo;
        }
        return nombre;
    }

    // ── Hojas ──

    private static Worksheet HojaDeTabla(TablaReporte tabla, CancellationToken ct)
    {
        var filas = new List<Row> { new(tabla.Columnas.Select((c, i) => Texto(Referencia(i, 1), c.Titulo, EstiloEncabezado))) { RowIndex = 1 } };
        for (var f = 0; f < tabla.Filas.Count; f++)
        {
            ct.ThrowIfCancellationRequested();
            var indice = (uint)f + 2;
            filas.Add(new Row(tabla.Columnas.Select((c, i) => Celda(Referencia(i, indice), tabla.Filas[f][i], c)).OfType<Cell>()) { RowIndex = indice });
        }
        if (tabla.TieneTotales)
            filas.Add(FilaDeTotales(tabla, (uint)tabla.Filas.Count + 2));

        var hoja = new Worksheet(
            new SheetViews(new SheetView(
                new Pane { VerticalSplit = 1D, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen },
                new Selection { Pane = PaneValues.BottomLeft }) { WorkbookViewId = 0U }),
            new Columns(tabla.Columnas.Select((c, i) => new Column { Min = (uint)i + 1, Max = (uint)i + 1, Width = Ancho(tabla, i), CustomWidth = true })),
            new SheetData(filas));
        return hoja;
    }

    private static Row FilaDeTotales(TablaReporte tabla, uint fila)
    {
        var totales = tabla.Totales;
        var celdas = new List<Cell>();
        for (var i = 0; i < tabla.Columnas.Count; i++)
        {
            var referencia = Referencia(i, fila);
            if (i == 0)
                celdas.Add(Texto(referencia, "Total", EstiloTotalTexto));
            else if (totales[i] is decimal total)
                celdas.Add(CeldaDeTotal(tabla, i, fila, total, referencia));
            else
                celdas.Add(new Cell { CellReference = referencia, StyleIndex = EstiloTotalTexto });
        }
        return new Row(celdas) { RowIndex = fila };
    }

    private static Cell CeldaDeTotal(TablaReporte tabla, int columna, uint fila, decimal total, string referencia)
    {
        var estilo = tabla.Columnas[columna].Tipo == TiposColumna.Pesos ? EstiloTotalPesos : EstiloTotalEntero;
        var celda = new Cell { CellReference = referencia, StyleIndex = estilo, CellValue = new CellValue(Numero(total)) };
        if (tabla.Filas.Count == 0)
            return celda;   // sin filas no hay rango que sumar: el total es cero
        var suma = $"{Letra(columna)}2:{Letra(columna)}{fila - 1}";
        celda.CellFormula = new CellFormula(tabla.Exclusion is { } e
            ? $"SUMIF({Letra(e.Columna)}2:{Letra(e.Columna)}{fila - 1},\"<>{e.Valor.Replace("\"", "\"\"")}\",{suma})"
            : $"SUM({suma})");
        return celda;
    }

    private static Worksheet HojaDeFiltros(DocumentoReporte documento)
    {
        var filas = new List<Row>();
        uint n = 1;
        void Fila(params Cell[] celdas) => filas.Add(new Row(celdas) { RowIndex = n++ });

        Fila(Texto($"A{n}", documento.Titulo, EstiloTitulo));
        Fila(Texto($"A{n}", "Emitido el", EstiloEncabezado), Texto($"B{n}", FormatoReporte.FechaHora(documento.EmitidoEn) + " (hora de Chile)", EstiloTexto));
        n++;
        Fila(Texto($"A{n}", "Filtros aplicados", EstiloEncabezado), Texto($"B{n}", "", EstiloEncabezado));
        foreach (var filtro in documento.Filtros)
            Fila(Texto($"A{n}", filtro.Etiqueta, EstiloTexto), Texto($"B{n}", filtro.Valor, EstiloTexto));
        if (documento.Notas.Count > 0)
        {
            n++;
            foreach (var nota in documento.Notas)
                Fila(Texto($"A{n}", nota, EstiloTexto));
        }
        return new Worksheet(new Columns(
            new Column { Min = 1, Max = 1, Width = 28, CustomWidth = true },
            new Column { Min = 2, Max = 2, Width = 48, CustomWidth = true }), new SheetData(filas));
    }

    // ── Celdas ──

    private static Cell? Celda(string referencia, object? valor, ColumnaReporte columna)
    {
        if (valor is null)
            return columna.TextoNulo.Length == 0 ? null : Texto(referencia, columna.TextoNulo, EstiloTexto);
        return valor switch
        {
            DateOnly fecha => new Cell { CellReference = referencia, StyleIndex = EstiloFecha, CellValue = new CellValue(NumeroDeSerie(fecha).ToString(CultureInfo.InvariantCulture)) },
            _ when columna.Tipo == TiposColumna.Pesos => Numerica(referencia, valor, EstiloPesos),
            _ when columna.Tipo == TiposColumna.Entero => Numerica(referencia, valor, EstiloEntero),
            _ when columna.Tipo == TiposColumna.Porcentaje => Numerica(referencia, valor, EstiloPorcentaje),
            _ => Texto(referencia, Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "", EstiloTexto)
        };
    }

    private static Cell Numerica(string referencia, object valor, uint estilo) =>
        new() { CellReference = referencia, StyleIndex = estilo, CellValue = new CellValue(Numero(Convert.ToDecimal(valor, CultureInfo.InvariantCulture))) };

    // Siempre texto: una cadena en linea nunca se evalua como formula, aunque empiece con «=».
    private static Cell Texto(string referencia, string texto, uint estilo) => new()
    {
        CellReference = referencia,
        StyleIndex = estilo,
        DataType = CellValues.InlineString,
        InlineString = new InlineString(new Text(Limpiar(texto)) { Space = SpaceProcessingModeValues.Preserve })
    };

    // XML 1.0 no admite los caracteres de control (salvo tabulacion y saltos de linea).
    private static string Limpiar(string texto)
    {
        var limpio = new System.Text.StringBuilder(texto.Length);
        for (var i = 0; i < texto.Length && limpio.Length < MaximoCaracteresPorCelda - 1; i++)
        {
            var c = texto[i];
            if (char.IsHighSurrogate(c) && i + 1 < texto.Length && char.IsLowSurrogate(texto[i + 1]))
                limpio.Append(c).Append(texto[++i]);   // un par valido se conserva entero
            else if (char.IsSurrogate(c) || c == '\uFFFE' || c == '\uFFFF')
                limpio.Append('\uFFFD');   // un sustituto suelto hace fallar al escritor XML
            else if (c is '\t' or '\n' or '\r' || c >= ' ')
                limpio.Append(c);
        }
        return limpio.ToString();
    }

    private static string Numero(decimal valor) => valor.ToString("0.##########", CultureInfo.InvariantCulture);

    // Excel cuenta los dias desde el 30-12-1899.
    private static double NumeroDeSerie(DateOnly fecha) => fecha.DayNumber - new DateOnly(1899, 12, 30).DayNumber;

    // ── Ubicacion y anchos ──

    private static string Letra(int columna)
    {
        var letras = "";
        for (var n = columna + 1; n > 0; n = (n - 1) / 26)
            letras = (char)('A' + (n - 1) % 26) + letras;
        return letras;
    }

    private static string Referencia(int columna, uint fila) => $"{Letra(columna)}{fila}";

    private static double Ancho(TablaReporte tabla, int columna)
    {
        var c = tabla.Columnas[columna];
        var visibles = tabla.Filas.Take(500).Select(f => FormatoReporte.Celda(f[columna], c).Length).DefaultIfEmpty(0).Max();
        var minimo = c.Tipo switch { TiposColumna.Fecha => 12, TiposColumna.Pesos => 13, _ => 8 };
        return Math.Clamp(Math.Max(Math.Max(c.Titulo.Length, visibles), minimo) * 1.15 + 2, 10, 50);
    }

    // ── Estilos ──

    private static Stylesheet Estilos()
    {
        var normal = new DocumentFormat.OpenXml.Spreadsheet.Font(new FontSize { Val = 11 }, new FontName { Val = "Calibri" });
        var negrita = new DocumentFormat.OpenXml.Spreadsheet.Font(new Bold(), new FontSize { Val = 11 }, new FontName { Val = "Calibri" });
        var grande = new DocumentFormat.OpenXml.Spreadsheet.Font(new Bold(), new FontSize { Val = 14 }, new FontName { Val = "Calibri" });

        static CellFormat Formato(uint numero, uint fuente, uint relleno = 0, uint borde = 0) => new()
        {
            NumberFormatId = numero, FontId = fuente, FillId = relleno, BorderId = borde,
            ApplyNumberFormat = numero != 0, ApplyFont = fuente != 0, ApplyFill = relleno != 0, ApplyBorder = borde != 0
        };

        return new Stylesheet(
            new NumberingFormats(
                new NumberingFormat { NumberFormatId = 164, FormatCode = "dd-mm-yyyy" },
                new NumberingFormat { NumberFormatId = 165, FormatCode = "#,##0" },
                new NumberingFormat { NumberFormatId = 166, FormatCode = "0.0\" %\"" }),
            new Fonts(normal, negrita, grande),
            new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                new Fill(new PatternFill(new ForegroundColor { Rgb = "FFDCE6F2" }) { PatternType = PatternValues.Solid })),
            new Borders(
                new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder()),
                new Border(new LeftBorder(), new RightBorder(), new TopBorder(new Color { Rgb = "FF6B7280" }) { Style = BorderStyleValues.Thin }, new BottomBorder(), new DiagonalBorder())),
            new CellStyleFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }),
            new CellFormats(
                Formato(0, 0),                  // 0 normal
                Formato(0, 1, relleno: 2),      // 1 encabezado
                Formato(0, 0),                  // 2 texto
                Formato(1, 0),                  // 3 entero
                Formato(165, 0),                // 4 pesos
                Formato(164, 0),                // 5 fecha
                Formato(166, 0),                // 6 porcentaje
                Formato(0, 1, borde: 1),        // 7 «Total»
                Formato(165, 1, borde: 1),      // 8 total en pesos
                Formato(1, 1, borde: 1),        // 9 total entero
                Formato(0, 2)),                 // 10 titulo
            new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }));
    }
}
