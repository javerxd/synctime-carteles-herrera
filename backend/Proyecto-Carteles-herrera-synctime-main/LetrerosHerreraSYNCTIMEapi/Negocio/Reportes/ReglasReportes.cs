using System.Globalization;
using LetrerosHerreraSYNCTIMEapi.Negocio.Indicadores;
using LetrerosHerreraSYNCTIMEapi.Security;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;

// Fase 13A (RFS10, RFU24 a RFU26, RFU36, RNFP18, E16, S18, S24): reglas puras de los reportes. Reutilizan las del dashboard
// (ReglasIndicadores) para que los mismos filtros signifiquen lo mismo en las dos pantallas.

public static class TiposReporte
{
    public const string Operativo = "operativo";
    public const string Gerencial = "gerencial";
    public const string Parametrizable = "parametrizable";

    public static readonly string[] Todos = [Operativo, Gerencial, Parametrizable];

    public static bool Existe(string? reporte) => reporte is not null && Todos.Contains(reporte);

    /// <summary>El operativo lo ve todo el personal (RFU24, RFU26); el gerencial y el parametrizable, solo el dueño y el administrador (RFU25).</summary>
    public static bool PuedeVer(string? reporte, string? rol) => reporte switch
    {
        Operativo => rol is not null && Roles.Empleados.Contains(rol),
        Gerencial or Parametrizable => rol is Roles.Dueno or Roles.Administrador,
        _ => false
    };
}

public static class FormatosReporte
{
    public const string Xlsx = "xlsx";
    public const string Csv = "csv";
    public const string Pdf = "pdf";

    public static readonly string[] Todos = [Xlsx, Csv, Pdf];

    public static bool Existe(string? formato) => formato is not null && Todos.Contains(formato);

    public static string TipoMime(string formato) => formato switch
    {
        Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        Csv => "text/csv; charset=utf-8",
        Pdf => "application/pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato desconocido.")
    };
}

/// <summary>
/// Los filtros ya validados. <paramref name="Canonico"/> es lo que se guarda en el trabajo y en la bitacora: solo lo que la persona
/// pidio, recortado y en orden fijo; nunca datos personales (el cliente va por su numero).
/// </summary>
public sealed record FiltroReporte(string Reporte, FiltroIndicadores Indicadores, IReadOnlyDictionary<string, string> Canonico);

public static class ReglasReportes
{
    public const int MaximoDeFilasPorOmision = 50_000;

    // Un PDF con mas filas que esto tarda minutos y usa gigas de memoria (medido: 10.000 filas, 9,5 s; 50.000, 232 s y 1,3 GB): Excel y CSV si llegan a 50.000.
    public const int MaximoDeFilasPdfPorOmision = 10_000;

    private static readonly string[] Comunes = ["periodo", "desde", "hasta", "anio", "mes", "estadoPedido", "estadoPago", "cliente"];

    // El orden en que se guardan los filtros, igual para cualquier orden de llegada.
    private static readonly string[] Orden = ["periodo", "desde", "hasta", "anio", "mes", "comparar", "estadoPedido", "estadoPago", "cliente", "alcanceSaldo"];

    private static string[] Permitidos(string reporte) => reporte switch
    {
        TiposReporte.Gerencial => [.. Comunes, "comparar"],
        TiposReporte.Parametrizable => [.. Comunes, "alcanceSaldo"],
        _ => Comunes
    };

    /// <summary>
    /// Valida los filtros de un reporte (los vacios cuentan como ausentes). Un filtro que el reporte no tiene se rechaza con su nombre:
    /// nada se ignora en silencio. Devuelve los errores por campo, o el filtro con sus valores por omision.
    /// </summary>
    public static (FiltroReporte? Filtro, Dictionary<string, string[]> Errores) Validar(string reporte, IReadOnlyDictionary<string, string?> consulta)
    {
        if (!TiposReporte.Existe(reporte))
            return (null, new Dictionary<string, string[]> { ["reporte"] = ["El reporte no existe. Valores: operativo, gerencial, parametrizable."] });

        var permitidos = Permitidos(reporte);
        var presentes = consulta.Where(p => !string.IsNullOrWhiteSpace(p.Value)).ToDictionary(p => p.Key, p => p.Value!.Trim(), StringComparer.Ordinal);

        var errores = new Dictionary<string, string[]>();
        foreach (var clave in presentes.Keys.Where(k => !permitidos.Contains(k)))
            errores[clave] = [$"El filtro «{clave}» no existe en el reporte {reporte}."];

        var (indicadores, erroresIndicadores) = ReglasIndicadores.Validar(
            presentes.Where(p => permitidos.Contains(p.Key)).ToDictionary(p => p.Key, p => (string?)p.Value, StringComparer.Ordinal));
        foreach (var (campo, mensajes) in erroresIndicadores)
            errores[campo] = mensajes;
        if (errores.Count > 0 || indicadores is null)
            return (null, errores);

        var canonico = new Dictionary<string, string>();
        // Lo guardado es el valor ya normalizado (sin espacios ni ceros a la izquierda), no el texto crudo: asi cabe siempre en la columna.
        foreach (var clave in Orden.Where(presentes.ContainsKey))
            canonico[clave] = Normalizar(clave, presentes[clave]);
        return (new FiltroReporte(reporte, indicadores, canonico), errores);
    }

    private static string Normalizar(string clave, string valor) => clave switch
    {
        "anio" or "mes" or "cliente" => int.Parse(valor, System.Globalization.NumberStyles.None, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        "estadoPedido" or "estadoPago" => string.Join(',', valor.Split(',', StringSplitOptions.TrimEntries)),
        _ => valor
    };

    /// <summary>Los filtros como se le muestran a la persona, en el encabezado del PDF y en la hoja «Filtros» del Excel.</summary>
    public static IReadOnlyList<FiltroAplicado> DescribirFiltros(FiltroReporte filtro, Periodo periodo, Periodo? comparacion)
    {
        var f = filtro.Indicadores;
        var lista = new List<FiltroAplicado> { new("periodo", "Período", periodo.Etiqueta) };
        if (comparacion is not null && filtro.Reporte == TiposReporte.Gerencial)
            lista.Add(new("comparar", "Comparado con", comparacion.Etiqueta));
        if (f.EstadosPedido.Count > 0)
            lista.Add(new("estadoPedido", "Estado del pedido", string.Join(", ", f.EstadosPedido.Select(EtiquetasDeEstado.Pedido))));
        if (f.EstadosPago.Count > 0)
            lista.Add(new("estadoPago", "Estado de pago", f.EstadosPago.SequenceEqual(ReglasIndicadores.PorCobrar) ? "Por cobrar" : string.Join(", ", f.EstadosPago.Select(EtiquetasDeEstado.Pago))));
        if (f.Cliente is int cliente)
            lista.Add(new("cliente", "Cliente", $"N.º {cliente}"));
        if (filtro.Reporte == TiposReporte.Parametrizable)
            lista.Add(new("alcanceSaldo", "Alcance del saldo", f.AlcanceSaldo == "vigente" ? "Toda la deuda vigente" : "Pedidos del período"));
        return lista;
    }

    /// <summary>Nombre del archivo que se descarga: el tipo de reporte y la fecha de emision; nunca texto de la persona.</summary>
    public static string NombreArchivo(string reporte, string formato, DateOnly emision)
    {
        if (!TiposReporte.Existe(reporte) || !FormatosReporte.Existe(formato))
            throw new ArgumentException("Reporte o formato desconocido.");
        return $"reporte-{reporte}-{emision.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.{formato}";
    }

    public static bool ExcedeElTope(int filas, int maximo) => filas > maximo;

    public static string MensajeDelTopePdf(int maximo) =>
        $"Un PDF admite hasta {FormatoReporte.Miles(maximo)} filas. Acota el rango de fechas o agrega filtros, o descarga el reporte en Excel o CSV.";

    public static string MensajeDelTope(int maximo) =>
        $"El reporte tiene más de {FormatoReporte.Miles(maximo)} filas. Acota el rango de fechas o agrega filtros y vuelve a intentarlo.";
}

/// <summary>Como se escriben los estados en las tablas (los mismos textos que ve la persona en la pantalla).</summary>
public static class EtiquetasDeEstado
{
    public static string Pedido(string estado) => estado switch
    {
        "aceptado" => "Aceptado",
        "en_proceso" => "En proceso",
        "terminado" => "Terminado",
        "entregado" => "Entregado",
        "anulado" => "Anulado",
        _ => estado
    };

    public static string Pago(string estado) => estado switch
    {
        "no_pagado" => "No pagado",
        "abonado" => "Abonado",
        "pagado_total" => "Pagado total",
        _ => estado
    };

    public static string Origen(string origen) => origen switch
    {
        "online" => "En línea",
        "presencial" => "Presencial",
        _ => origen
    };

    public static string Solicitud(string estado) => estado switch
    {
        "solicitado" => "Solicitado",
        "cotizado" => "Cotizado",
        "aceptado" => "Aceptado",
        "rechazado" => "Rechazado",
        "cancelado" => "Cancelado",
        _ => estado.Length == 0 ? estado : char.ToUpperInvariant(estado[0]) + estado[1..].Replace('_', ' ')
    };
}

public sealed record CostoPedido(decimal Materiales, decimal ManoObra, decimal Otros)
{
    public decimal Total => Materiales + ManoObra + Otros;
}

/// <summary>
/// El costo y el margen del reporte gerencial. El costo de un pedido es el de su cotizacion aceptada (materiales, mano de obra y
/// otros costos); un pedido sin cotizacion no tiene costo registrado y NO se inventa uno: el margen solo mira los pedidos con costo.
/// </summary>
public static class ReglasGerencial
{
    public static CostoPedido? CostoDe(bool tieneCotizacion, decimal materiales, decimal manoObra, decimal otros) =>
        tieneCotizacion ? new CostoPedido(materiales, manoObra, otros) : null;

    /// <summary>Ganancia bruta: los ingresos de los pedidos con costo menos su costo de materiales (decision del usuario, 21-11-2026). La mano de obra y los otros costos se muestran, pero no se descuentan.</summary>
    public static decimal Margen(decimal ingresosConCosto, decimal materiales) => ingresosConCosto - materiales;

    /// <summary>Margen ÷ ingresos con costo × 100, con un decimal; sin ingresos con costo, nulo (nunca una division por cero).</summary>
    public static decimal? MargenPorcentaje(decimal ingresosConCosto, decimal materiales) =>
        ingresosConCosto == 0 ? null : Math.Round(Margen(ingresosConCosto, materiales) / ingresosConCosto * 100, 1, MidpointRounding.AwayFromZero);
}

/// <summary>Como se muestra lo que se escribe a mano en un archivo (el PDF y la hoja de filtros). Sin depender de la cultura del equipo.</summary>
public static class FormatoReporte
{
    public static string Fecha(DateOnly fecha) => fecha.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    /// <summary>El instante en hora de Chile (no la del servidor, que puede ser UTC): «20-11-2026 14:35».</summary>
    public static string FechaHora(DateTimeOffset instante) => FechaHora(LetrerosHerreraSYNCTIMEapi.Negocio.Local.HoraChile.Convertir(instante));

    public static string FechaHora(DateTime fecha) => fecha.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>1234567 → «1.234.567».</summary>
    public static string Miles(long numero)
    {
        var digitos = Math.Abs(numero).ToString(CultureInfo.InvariantCulture);
        var partes = new List<string>();
        for (var fin = digitos.Length; fin > 0; fin -= 3)
            partes.Insert(0, digitos[Math.Max(0, fin - 3)..fin]);
        return (numero < 0 ? "-" : "") + string.Join('.', partes);
    }

    /// <summary>Pesos chilenos sin decimales: «$1.234.567»; los negativos, «-$5.000».</summary>
    public static string Pesos(decimal monto)
    {
        var entero = (long)Math.Round(monto, 0, MidpointRounding.AwayFromZero);
        return (entero < 0 ? "-" : "") + "$" + Miles(Math.Abs(entero));
    }

    public static string Porcentaje(decimal valor) =>
        valor.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " %";

    /// <summary>El texto de una celda para el PDF; una celda vacia lleva el texto de vacio de su columna.</summary>
    public static string Celda(object? valor, ColumnaReporte columna) => valor switch
    {
        null => columna.TextoNulo,
        DateOnly fecha => Fecha(fecha),
        _ when columna.Tipo == TiposColumna.Pesos => Pesos(Convert.ToDecimal(valor, CultureInfo.InvariantCulture)),
        _ when columna.Tipo == TiposColumna.Porcentaje => Porcentaje(Convert.ToDecimal(valor, CultureInfo.InvariantCulture)),
        _ when columna.Tipo == TiposColumna.Entero => Convert.ToInt64(valor, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(valor, CultureInfo.InvariantCulture) ?? ""
    };
}
