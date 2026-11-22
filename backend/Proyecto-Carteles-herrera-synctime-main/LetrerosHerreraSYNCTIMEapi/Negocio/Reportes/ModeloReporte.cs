using System.Text.Json.Serialization;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;

// Fase 13A (RFS10, RFU24 a RFU26, S18): el documento que describe un reporte. Es UNO solo para la pantalla (JSON), el Excel, el
// CSV y el PDF: «lo que ves es lo que sale». Sin base de datos ni HTTP; los generadores de archivos lo leen tal cual.

/// <summary>Como se muestra y se escribe cada columna. Texto simple para que viaje en el JSON sin conversiones.</summary>
public static class TiposColumna
{
    public const string Texto = "texto";
    public const string Entero = "entero";
    public const string Pesos = "pesos";
    public const string Fecha = "fecha";
    public const string Porcentaje = "porcentaje";
}

/// <param name="Suma">La fila de totales suma esta columna.</param>
/// <param name="TextoNulo">Lo que se escribe en una celda sin valor (por ejemplo «Sin costo»); por omision, nada.</param>
public sealed record ColumnaReporte(string Clave, string Titulo, string Tipo, bool Suma = false, string TextoNulo = "");

/// <summary>Una fila cuya celda de esa columna vale <paramref name="Valor"/> se muestra, pero no suma en los totales (los anulados).</summary>
public sealed record ExclusionDeTotales(int Columna, string Valor);

/// <summary>
/// Una tabla: columnas, filas (cada celda es texto, numero, <see cref="DateOnly"/> o nula) y, si alguna columna suma, sus totales.
/// <paramref name="Principal"/> es la unica tabla que lleva el CSV, que no admite varias (decision 5 del plan).
/// </summary>
public sealed record TablaReporte(
    string Clave, string Titulo, IReadOnlyList<ColumnaReporte> Columnas, IReadOnlyList<object?[]> Filas,
    bool Principal = false, ExclusionDeTotales? Exclusion = null, string? Nota = null)
{
    [JsonIgnore]
    public bool TieneTotales => Columnas.Any(c => c.Suma);

    /// <summary>Una suma por columna (nula en las que no suman). Las filas excluidas y las celdas vacias no cuentan.</summary>
    public IReadOnlyList<decimal?> Totales
    {
        get
        {
            var incluidas = Filas.Where(f => !Excluida(f)).ToList();
            return Columnas.Select((c, i) => c.Suma ? incluidas.Sum(f => Numero(f[i])) : (decimal?)null).ToList();
        }
    }

    public bool Excluida(object?[] fila) =>
        Exclusion is { } e && string.Equals(Convert.ToString(fila[e.Columna], System.Globalization.CultureInfo.InvariantCulture), e.Valor, StringComparison.Ordinal);

    private static decimal Numero(object? valor) =>
        valor is null ? 0 : Convert.ToDecimal(valor, System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record SerieGrafico(string Nombre, IReadOnlyList<decimal> Valores);

/// <summary>Los datos de un grafico (la pantalla lo dibuja; el PDF lo lleva como imagen). <paramref name="Forma"/>: barras o barras-horizontales.</summary>
public sealed record GraficoReporte(string Clave, string Titulo, string Forma, IReadOnlyList<string> Etiquetas, IReadOnlyList<SerieGrafico> Series);

/// <summary>Un filtro tal como se le muestra a la persona («Período: noviembre de 2026»).</summary>
public sealed record FiltroAplicado(string Clave, string Etiqueta, string Valor);

public sealed record DocumentoReporte(
    string Reporte, string Titulo, DateTimeOffset EmitidoEn, IReadOnlyList<FiltroAplicado> Filtros,
    IReadOnlyList<TablaReporte> Tablas, IReadOnlyList<GraficoReporte> Graficos, IReadOnlyList<string> Notas)
{
    [JsonIgnore]
    public int TotalDeFilas => Tablas.Sum(t => t.Filas.Count);

    /// <summary>La tabla que exporta el CSV: la marcada como principal, o la primera.</summary>
    [JsonIgnore]
    public TablaReporte Principal => Tablas.FirstOrDefault(t => t.Principal) ?? Tablas[0];
}
