using System.Globalization;
using System.Text;
using LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;

// Fase 13A (RFS10, RNFP18, S18): el reporte como CSV. Una sola tabla (la principal: un CSV no admite varias).
//   - UTF-8 CON marca de orden de bytes: sin ella, Excel lee mal las tildes y la ñ.
//   - Separador «;»: el que Excel en español de Chile espera; las lineas terminan en CRLF (RFC 4180).
//   - Fechas aaaa-mm-dd; pesos enteros, sin separador de miles; porcentajes con coma. Sin fila de totales: son solo datos.
//   - Contra la inyeccion de formulas: un TEXTO que empieza con = + - @ (o con tabulacion o retorno) se escribe con un apostrofo
//     delante, para que Excel lo trate como texto. Los numeros no se tocan (un -5000 es un numero, no un texto).
public static class GeneradorCsv
{
    private const char Separador = ';';
    private const string SaltoDeLinea = "\r\n";
    private static readonly char[] InicioDeFormula = ['=', '+', '-', '@', '\t', '\r'];

    public static byte[] Generar(DocumentoReporte documento, CancellationToken ct = default)
    {
        var tabla = documento.Principal;
        var texto = new StringBuilder();
        Linea(texto, tabla.Columnas.Select(c => Texto(c.Titulo)));
        foreach (var fila in tabla.Filas)
        {
            ct.ThrowIfCancellationRequested();
            Linea(texto, tabla.Columnas.Select((c, i) => Celda(fila[i], c)));
        }

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. utf8.GetPreamble(), .. utf8.GetBytes(texto.ToString())];
    }

    private static void Linea(StringBuilder salida, IEnumerable<string> campos) =>
        salida.Append(string.Join(Separador, campos)).Append(SaltoDeLinea);

    private static string Celda(object? valor, ColumnaReporte columna)
    {
        if (valor is null)
            return Texto(columna.TextoNulo);
        return valor switch
        {
            DateOnly fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ when columna.Tipo == TiposColumna.Pesos => Math.Round(Convert.ToDecimal(valor, CultureInfo.InvariantCulture), 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture),
            _ when columna.Tipo == TiposColumna.Entero => Convert.ToInt64(valor, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            _ when columna.Tipo == TiposColumna.Porcentaje => Convert.ToDecimal(valor, CultureInfo.InvariantCulture).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ','),
            _ => Texto(Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "")
        };
    }

    /// <summary>Un texto como campo: defendido contra formulas y, si lo necesita, entre comillas con las comillas duplicadas.</summary>
    private static string Texto(string texto)
    {
        if (texto.Length > 0 && InicioDeFormula.Contains(texto[0]))
            texto = "'" + texto;
        return texto.IndexOfAny([Separador, '"', '\r', '\n']) >= 0 ? "\"" + texto.Replace("\"", "\"\"") + "\"" : texto;
    }
}
