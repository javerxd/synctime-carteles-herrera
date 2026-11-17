using System.Globalization;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Indicadores;

// Fase 12B (RFS09, RFU27, E21, S10 a S14, S24): reglas puras del dashboard de gestion. Periodos, comparacion,
// filtros y formulas; sin base de datos ni HTTP. El contrato esta en docs/plan/fase-12-especificacion.md.

public sealed record Periodo(string Tipo, DateOnly Desde, DateOnly Hasta, string Etiqueta);

public sealed record Tramo(DateOnly Desde, DateOnly Hasta, string Etiqueta);

/// <summary>Los filtros ya validados. Las listas vacias no filtran.</summary>
public sealed record FiltroIndicadores(
    string Periodo, DateOnly? Desde, DateOnly? Hasta, int? Anio, int? Mes, string Comparar,
    IReadOnlyList<string> EstadosPedido, IReadOnlyList<string> EstadosPago, int? Categoria, string? Origen, int? Cliente,
    string AlcanceSaldo, string Agrupar, int Pagina, int Tamano);

public static class ReglasIndicadores
{
    public const int TamanoPorOmision = 10;
    public const int TamanoMaximo = 100;

    // El rango propio tiene tope (como el año, 2020 a 2100): sin él, un rango enorme crearia cientos de miles de tramos.
    public static readonly DateOnly RangoMinimo = new(2020, 1, 1);
    public static readonly DateOnly RangoMaximo = new(2100, 12, 31);
    public const int DiasMaximosDeRango = 3660;

    // «Anulado» no se ofrece: ningun indicador cuenta los pedidos anulados.
    public static readonly string[] EstadosPedido = ["aceptado", "en_proceso", "terminado", "entregado"];
    public static readonly string[] EstadosPago = ["no_pagado", "abonado", "pagado_total"];
    public static readonly string[] PorCobrar = ["no_pagado", "abonado"];

    private static readonly string[] Meses = ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    // ── Filtros ──

    /// <summary>
    /// Valida los parametros de la consulta (los vacios cuentan como ausentes). Devuelve los errores por campo, con el
    /// nombre del parametro, o el filtro completo con sus valores por omision. `conPagina` lo usa el listado por cobrar.
    /// </summary>
    public static (FiltroIndicadores? Filtro, Dictionary<string, string[]> Errores) Validar(IReadOnlyDictionary<string, string?> consulta, bool conPagina = false)
    {
        var errores = new Dictionary<string, string[]>();
        string? Valor(string campo) => consulta.TryGetValue(campo, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
        void Error(string campo, string mensaje) => errores[campo] = [mensaje];

        var periodo = Valor("periodo") ?? "mes";
        if (periodo is not ("semana" or "mes" or "anio"))
            Error("periodo", "Período inválido. Valores: semana, mes, anio.");

        DateOnly? Fecha(string campo)
        {
            if (Valor(campo) is not { } texto)
                return null;
            if (DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
                return fecha;
            Error(campo, "Usa la fecha como aaaa-mm-dd.");
            return null;
        }
        var desde = Fecha("desde");
        var hasta = Fecha("hasta");
        if ((desde is null) != (hasta is null) && !errores.ContainsKey("desde") && !errores.ContainsKey("hasta"))
            Error(desde is null ? "desde" : "hasta", "Indica «desde» y «hasta», o ninguna de las dos.");
        else if (desde is { } d && hasta is { } h && d > h)
            Error("hasta", "«Hasta» debe ser igual o posterior a «desde».");
        else if (desde is { } d2 && hasta is { } h2 && (d2 < RangoMinimo || h2 > RangoMaximo || h2.DayNumber - d2.DayNumber + 1 > DiasMaximosDeRango))
            Error("hasta", $"El rango debe estar entre el {RangoMinimo:dd-MM-yyyy} y el {RangoMaximo:dd-MM-yyyy}, y abarcar como máximo {DiasMaximosDeRango / 365} años.");

        int? Entero(string campo, int minimo, int maximo, string mensaje)
        {
            if (Valor(campo) is not { } texto)
                return null;
            if (int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= minimo && n <= maximo)
                return n;
            Error(campo, mensaje);
            return null;
        }
        var anio = Entero("anio", 2020, 2100, "El año debe estar entre 2020 y 2100.");
        var mes = Entero("mes", 1, 12, "El mes debe estar entre 1 y 12.");
        if (mes is not null && anio is null && !errores.ContainsKey("anio"))
            Error("mes", "El mes necesita el año.");

        var comparar = Valor("comparar") ?? "anterior";
        if (comparar is not ("anterior" or "anio_anterior" or "ninguno"))
            Error("comparar", "Comparación inválida. Valores: anterior, anio_anterior, ninguno.");

        IReadOnlyList<string> Lista(string campo, string[] permitidos, string mensaje)
        {
            if (Valor(campo) is not { } texto)
                return [];
            var partes = texto.Split(',', StringSplitOptions.TrimEntries);
            if (partes.Length == 0 || partes.Distinct().Count() != partes.Length || partes.Any(p => !permitidos.Contains(p)))
            {
                Error(campo, mensaje);
                return [];
            }
            return partes;
        }
        var estadosPedido = Lista("estadoPedido", EstadosPedido, $"Estado del pedido inválido. Valores: {string.Join(", ", EstadosPedido)} (separados por coma).");
        IReadOnlyList<string> estadosPago;
        if (Valor("estadoPago") == "por_cobrar")
            estadosPago = PorCobrar;
        else
            estadosPago = Lista("estadoPago", EstadosPago, $"Estado de pago inválido. Valores: {string.Join(", ", EstadosPago)} (separados por coma) o por_cobrar.");

        var categoria = Entero("categoria", 1, int.MaxValue, "Indica el número de la categoría.");
        var cliente = Entero("cliente", 1, int.MaxValue, "Indica el número del cliente.");
        var origen = Valor("origen");
        if (origen is not null && origen is not ("online" or "presencial"))
        {
            Error("origen", "Origen inválido. Valores: online, presencial.");
            origen = null;
        }
        var alcance = Valor("alcanceSaldo") ?? "periodo";
        if (alcance is not ("periodo" or "vigente"))
            Error("alcanceSaldo", "Alcance inválido. Valores: periodo, vigente.");
        var agrupar = Valor("agrupar") ?? "categoria";
        if (agrupar is not ("categoria" or "producto"))
            Error("agrupar", "Agrupación inválida. Valores: categoria, producto.");

        var pagina = conPagina ? Entero("pagina", 1, int.MaxValue, "La página parte en 1.") ?? 1 : 1;
        var tamano = conPagina ? Entero("tamano", 1, TamanoMaximo, $"El tamaño debe estar entre 1 y {TamanoMaximo}.") ?? TamanoPorOmision : TamanoPorOmision;

        if (errores.Count > 0)
            return (null, errores);
        return (new FiltroIndicadores(periodo, desde, hasta, anio, mes, comparar, estadosPedido, estadosPago, categoria, origen, cliente, alcance, agrupar, pagina, tamano), errores);
    }

    // ── Periodos ──

    private static string Fecha(DateOnly fecha) => fecha.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    private static Periodo DeMes(int anio, int mes) =>
        new("mes", new DateOnly(anio, mes, 1), new DateOnly(anio, mes, DateTime.DaysInMonth(anio, mes)), $"{Meses[mes - 1]} de {anio}");

    private static Periodo DeAnio(int anio) => new("anio", new DateOnly(anio, 1, 1), new DateOnly(anio, 12, 31), anio.ToString(CultureInfo.InvariantCulture));

    private static Periodo DeSemana(DateOnly lunes) =>
        new("semana", lunes, lunes.AddDays(6), $"la semana del {Fecha(lunes)} al {Fecha(lunes.AddDays(6))}");

    private static Periodo DeRango(DateOnly desde, DateOnly hasta) => new("rango", desde, hasta, $"del {Fecha(desde)} al {Fecha(hasta)}");

    /// <summary>
    /// El periodo pedido, con «hoy» en Chile. Prioridad: rango propio (desde y hasta), luego año y mes, y al final el
    /// periodo relativo a hoy (semana de lunes a domingo, mes o año calendario).
    /// </summary>
    public static Periodo ResolverPeriodo(FiltroIndicadores filtro, DateOnly hoy)
    {
        if (filtro.Desde is { } desde && filtro.Hasta is { } hasta)
            return DeRango(desde, hasta);
        if (filtro.Anio is int anio)
            return filtro.Mes is int mes ? DeMes(anio, mes) : DeAnio(anio);
        return filtro.Periodo switch
        {
            "semana" => DeSemana(hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7))),
            "anio" => DeAnio(hoy.Year),
            _ => DeMes(hoy.Year, hoy.Month)
        };
    }

    /// <summary>El periodo con el que se compara: el inmediatamente anterior de igual forma, el del año anterior o ninguno.</summary>
    public static Periodo? PeriodoComparacion(Periodo periodo, string comparar)
    {
        if (comparar == "ninguno")
            return null;
        if (comparar == "anio_anterior")
        {
            return periodo.Tipo switch
            {
                "mes" => DeMes(periodo.Desde.Year - 1, periodo.Desde.Month),
                "anio" => DeAnio(periodo.Desde.Year - 1),
                _ => DeRango(periodo.Desde.AddYears(-1), periodo.Hasta.AddYears(-1)) with { Tipo = periodo.Tipo }
            };
        }
        return periodo.Tipo switch
        {
            "mes" => periodo.Desde.Month == 1 ? DeMes(periodo.Desde.Year - 1, 12) : DeMes(periodo.Desde.Year, periodo.Desde.Month - 1),
            "anio" => DeAnio(periodo.Desde.Year - 1),
            "semana" => DeSemana(periodo.Desde.AddDays(-7)),
            _ => DeRango(periodo.Desde.AddDays(-Dias(periodo)), periodo.Desde.AddDays(-1))
        };
    }

    private static int Dias(Periodo periodo) => periodo.Hasta.DayNumber - periodo.Desde.DayNumber + 1;

    /// <summary>Como se agrupa la evolucion: por dia (semana o hasta 14 dias), por semana (mes o hasta 92 dias) o por mes.</summary>
    public static string Granularidad(Periodo periodo) => periodo.Tipo switch
    {
        "semana" => "dia",
        "mes" => "semana",
        "anio" => "mes",
        _ => Dias(periodo) <= 14 ? "dia" : Dias(periodo) <= 92 ? "semana" : "mes"
    };

    /// <summary>Los tramos del periodo, desde su inicio. El ultimo tramo se recorta al fin del periodo.</summary>
    public static IReadOnlyList<Tramo> Tramos(Periodo periodo, string granularidad)
    {
        var tramos = new List<Tramo>();
        if (granularidad == "mes")
        {
            for (var inicio = new DateOnly(periodo.Desde.Year, periodo.Desde.Month, 1); inicio <= periodo.Hasta; inicio = inicio.AddMonths(1))
            {
                var fin = inicio.AddMonths(1).AddDays(-1);
                tramos.Add(new Tramo(inicio < periodo.Desde ? periodo.Desde : inicio, fin > periodo.Hasta ? periodo.Hasta : fin, Meses[inicio.Month - 1][..3]));
            }
            return tramos;
        }
        var paso = granularidad == "semana" ? 7 : 1;
        for (var inicio = periodo.Desde; inicio <= periodo.Hasta; inicio = inicio.AddDays(paso))
        {
            var fin = inicio.AddDays(paso - 1);
            tramos.Add(new Tramo(inicio, fin > periodo.Hasta ? periodo.Hasta : fin, inicio.ToString("dd-MM", CultureInfo.InvariantCulture)));
        }
        return tramos;
    }

    /// <summary>La posicion del tramo que contiene la fecha (para alinear el periodo actual con el de comparacion).</summary>
    public static int IndiceDeTramo(Periodo periodo, string granularidad, DateOnly fecha) => granularidad switch
    {
        "mes" => (fecha.Year - periodo.Desde.Year) * 12 + fecha.Month - periodo.Desde.Month,
        "semana" => (fecha.DayNumber - periodo.Desde.DayNumber) / 7,
        _ => fecha.DayNumber - periodo.Desde.DayNumber
    };

    // ── Formulas ──

    private static decimal Redondear1(decimal valor) => Math.Round(valor, 1, MidpointRounding.AwayFromZero);

    /// <summary>(valor − anterior) ÷ anterior × 100 con un decimal; sin base de comparacion (nula o cero), nula.</summary>
    public static decimal? VariacionPorcentual(decimal? valor, decimal? anterior) =>
        valor is null || anterior is null or 0 ? null : Redondear1((valor.Value - anterior.Value) / anterior.Value * 100);

    /// <summary>La diferencia de dos porcentajes, en puntos porcentuales, con un decimal.</summary>
    public static decimal? VariacionPuntos(decimal? valor, decimal? anterior) =>
        valor is null || anterior is null ? null : Redondear1(valor.Value - anterior.Value);

    /// <summary>parte ÷ total × 100 con un decimal; con total cero, nulo (nunca una division por cero).</summary>
    public static decimal? Porcentaje(int parte, int total) => total == 0 ? null : Redondear1((decimal)parte / total * 100);

    /// <summary>Ingresos ÷ pedidos entregados, redondeado al peso; sin entregas, nulo.</summary>
    public static int? TicketPromedio(decimal ingresos, int entregados) =>
        entregados == 0 ? null : (int)Math.Round(ingresos / entregados, 0, MidpointRounding.AwayFromZero);
}
