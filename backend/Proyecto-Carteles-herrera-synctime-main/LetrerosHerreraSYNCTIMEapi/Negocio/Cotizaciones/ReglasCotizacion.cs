using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Cotizaciones;

// ── Fase 8: reglas de las cotizaciones (RFS05, RFU08, RFU12, E06, E07) ──
// Sin HTTP ni base de datos: se prueban solas en LetrerosHerreraSYNCTIMEapi.UnitTests.
// Montos en pesos chilenos con IVA incluido (decision 1 del 14-11-2026): todo entero salvo la cantidad de
// un material, que admite 2 decimales.

public static class EstadosCotizacion
{
    public const string Pendiente = "pendiente";
    public const string Aprobada = "aprobada";
    public const string Rechazada = "rechazada";

    public static readonly string[] Todos = [Pendiente, Aprobada, Rechazada];

    public static bool Existe(string? estado) => estado is not null && Todos.Contains(estado);

    // Una aprobada ya dio (o dara) un pedido: queda como historial y no se borra.
    public static bool PuedeEliminarse(string estado) => estado is Pendiente or Rechazada;
}

// Por donde llego la respuesta del cliente. El personal registra las tres ultimas; la primera es la del
// propio cliente en «Mis solicitudes».
public static class ViasRespuesta
{
    public const string ClienteWeb = "cliente_web";
    public const string Telefono = "telefono";
    public const string Whatsapp = "whatsapp";
    public const string Presencial = "presencial";

    public static readonly string[] DelPersonal = [Telefono, Whatsapp, Presencial];
}

// Lo que llega para emitir o editar una cotizacion, ya separado del formato de la peticion.
// Los montos y la cantidad son obligatorios: null (no vino en la peticion) es un error del campo, nunca un 0.
public sealed record EntradaMaterial(int IdMaterial, string? MedidaAplicada, decimal? Cantidad, decimal? CostoUnitario);

public sealed record EntradaCotizacion(IReadOnlyList<EntradaMaterial>? Materiales, decimal? CostoManoObra, decimal? OtrosCostos, string? Observaciones);

public static class ValidacionCotizacion
{
    public const int MaxLineas = 30;
    public const int MaxMedida = 100;
    public const decimal MaxCantidad = 100_000;
    public const decimal MaxCosto = 100_000_000;
    public const decimal MaxTotal = 2_000_000_000;
    public const int MaxObservaciones = 2000;
    public const int MaxMotivo = 500;

    public const string Aprobar = EstadosCotizacion.Aprobada;
    public const string Rechazar = EstadosCotizacion.Rechazada;

    public const string MensajeMaterial = "Uno de los materiales no existe o no está activo.";

    // Errores por campo, con las claves del formulario: materiales, materiales[0].cantidad, costoManoObra,
    // otrosCostos, observaciones y total. Vacio si todo esta bien (los materiales se revisan contra la base).
    public static Dictionary<string, string[]> Validar(EntradaCotizacion entrada)
    {
        var errores = new Dictionary<string, string[]>();
        var lineas = entrada.Materiales ?? [];
        if (lineas.Count > MaxLineas)
            errores["materiales"] = [$"Una cotización admite hasta {MaxLineas} materiales."];
        else
        {
            for (var i = 0; i < lineas.Count; i++)
                ValidarLinea(lineas[i], $"materiales[{i}]", errores);
        }

        if (entrada.CostoManoObra is null)
            errores["costoManoObra"] = ["Indica el costo de mano de obra ($0 si no tiene)."];
        else if (!EsPesoEnRango(entrada.CostoManoObra.Value))
            errores["costoManoObra"] = [$"La mano de obra va en pesos, sin decimales, de $0 a {FormatoChile.Pesos((long)MaxCosto)}."];
        if (entrada.OtrosCostos is null)
            errores["otrosCostos"] = ["Indica los otros costos ($0 si no hay)."];
        else if (!EsPesoEnRango(entrada.OtrosCostos.Value))
            errores["otrosCostos"] = [$"Los otros costos van en pesos, sin decimales, de $0 a {FormatoChile.Pesos((long)MaxCosto)}."];
        if (entrada.Observaciones?.Trim().Length > MaxObservaciones)
            errores["observaciones"] = [$"Las observaciones admiten hasta {MaxObservaciones} caracteres."];

        // El total solo se calcula con montos validos: con valores fuera de rango la suma no tiene sentido.
        if (errores.Count == 0)
        {
            var total = Total(entrada);
            if (total <= 0)
                errores["total"] = ["El total de la cotización debe ser mayor que $0."];
            else if (total > MaxTotal)
                errores["total"] = [$"El total de la cotización no puede superar {FormatoChile.Pesos((long)MaxTotal)}."];
        }
        return errores;
    }

    private static void ValidarLinea(EntradaMaterial linea, string campo, Dictionary<string, string[]> errores)
    {
        if (linea.IdMaterial <= 0)
            errores[$"{campo}.idMaterial"] = ["Elige el material."];
        if (linea.MedidaAplicada?.Trim().Length > MaxMedida)
            errores[$"{campo}.medidaAplicada"] = [$"La medida admite hasta {MaxMedida} caracteres."];
        if (linea.Cantidad is not decimal cantidad || cantidad <= 0 || cantidad > MaxCantidad || cantidad != Math.Round(cantidad, 2))
            errores[$"{campo}.cantidad"] = ["La cantidad debe ser mayor que 0 y como máximo 100.000, con hasta 2 decimales."];
        if (linea.CostoUnitario is null)
            errores[$"{campo}.costoUnitario"] = ["Indica el costo unitario ($0 si no tiene)."];
        else if (!EsPesoEnRango(linea.CostoUnitario.Value))
            errores[$"{campo}.costoUnitario"] = [$"El costo unitario va en pesos, sin decimales, de $0 a {FormatoChile.Pesos((long)MaxCosto)}."];
    }

    private static bool EsPesoEnRango(decimal monto) => Reglas.EsMontoEnPesos(monto) && monto <= MaxCosto;

    // Cantidad con 2 decimales por un costo entero: el subtotal es exacto, con a lo mas 2 decimales. La
    // base lo calcula igual (columna subtotal) y su trigger suma el costo de materiales. Solo se usa con
    // una entrada ya validada (sin nulos).
    public static decimal Subtotal(EntradaMaterial linea) => (linea.Cantidad ?? 0) * (linea.CostoUnitario ?? 0);

    public static decimal Total(EntradaCotizacion entrada) =>
        (entrada.Materiales ?? []).Sum(Subtotal) + (entrada.CostoManoObra ?? 0) + (entrada.OtrosCostos ?? 0);

    // El monto base del pedido es el total redondeado al peso, con la mitad hacia arriba.
    public static int Redondear(decimal total) => (int)Math.Round(total, 0, MidpointRounding.AwayFromZero);

    // decision: aprobada o rechazada. El personal indica por donde respondio el cliente; el cliente no
    // (siempre es la web). El motivo es opcional y solo se guarda si se rechaza.
    public static Dictionary<string, string[]> ValidarRespuesta(string? decision, string? via, string? motivo, bool delPersonal)
    {
        var errores = new Dictionary<string, string[]>();
        if (decision is not (Aprobar or Rechazar))
            errores["decision"] = ["Indica si la cotización se aprueba o se rechaza."];
        if (delPersonal && (via is null || !ViasRespuesta.DelPersonal.Contains(via)))
            errores["via"] = ["Indica por dónde respondió el cliente: teléfono, WhatsApp o en el local."];
        if (motivo?.Trim().Length > MaxMotivo)
            errores["motivo"] = [$"El motivo admite hasta {MaxMotivo} caracteres."];
        return errores;
    }

    // Filtros del listado: un valor desconocido es un error (400), no se ignora en silencio.
    public static Dictionary<string, string[]> ValidarFiltro(string? estado, string? orden)
    {
        var errores = new Dictionary<string, string[]>();
        if (!string.IsNullOrEmpty(estado) && !EstadosCotizacion.Existe(estado))
            errores["estado"] = [$"Estado inválido. Valores: {string.Join(", ", EstadosCotizacion.Todos)}."];
        if (!string.IsNullOrEmpty(orden) && orden is not ("fecha_desc" or "fecha_asc" or "total_desc"))
            errores["orden"] = ["Orden inválido. Valores: fecha_desc, fecha_asc, total_desc."];
        return errores;
    }

    public static string Numero(int idCotizacion) => $"#{idCotizacion.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    // Mensaje principal del 400: el error si hay uno solo; si no, uno general (el detalle va por campo).
    public static string Resumen(IReadOnlyDictionary<string, string[]> errores) =>
        errores.Count == 1 ? errores.First().Value[0] : "Revisa los datos de la cotización.";

    // Un texto opcional: sin espacios a los lados, y null si quedo vacio.
    public static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
