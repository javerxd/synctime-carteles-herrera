using System.Text.Encodings.Web;
using System.Text.Json;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;

// Fase 12B (catalogo real): lo que el taller acepta de cada producto. Los limites vienen de la base
// (productos.ancho_min_mm..., producto_medidas_fijas y producto_opciones); estas reglas no tocan la base.
public sealed record LimitesProducto(
    string Nombre, int? AnchoMinMm, int? AnchoMaxMm, int? AltoMinMm, int? AltoMaxMm,
    IReadOnlyList<(int AnchoMm, int AltoMm)> MedidasFijas, IReadOnlyDictionary<string, IReadOnlyList<string>> Opciones);

public static class ReglasCatalogo
{
    // Sin escapar las tildes y la ñ: «Diseño» se guarda y se lee tal cual.
    private static readonly JsonSerializerOptions Formato = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public const int MaxLargoOpciones = 500;

    // Medidas en milimetros; el taller las da en centimetros, y asi se le muestran al cliente.
    private static string Cm(int mm) => (mm / 10m).ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("es-CL"));

    /// <summary>
    /// Errores de medidas de un letrero contra su producto, con las claves del formulario (anchoMm, altoMm).
    /// Sin medidas (las dos nulas) no hay nada que validar; la regla de «las dos o ninguna» vive en ValidacionSolicitud.
    /// Un producto de medidas fijas acepta solo esas combinaciones; uno con rangos, ancho y alto dentro de ellos.
    /// </summary>
    public static Dictionary<string, string[]> ValidarMedidas(int? anchoMm, int? altoMm, LimitesProducto limites)
    {
        var errores = new Dictionary<string, string[]>();
        if (anchoMm is not int ancho || altoMm is not int alto)
            return errores;

        if (limites.MedidasFijas.Count > 0)
        {
            if (!limites.MedidasFijas.Contains((ancho, alto)))
            {
                var disponibles = string.Join(", ", limites.MedidasFijas.Select(m => $"{Cm(m.AnchoMm)} x {Cm(m.AltoMm)} cm"));
                errores["anchoMm"] = [$"«{limites.Nombre}» solo se hace en {(limites.MedidasFijas.Count == 1 ? "la medida" : "las medidas")}: {disponibles}."];
            }
            return errores;
        }

        if (limites.AnchoMinMm is int anchoMin && limites.AnchoMaxMm is int anchoMax && (ancho < anchoMin || ancho > anchoMax))
            errores["anchoMm"] = [$"El ancho de «{limites.Nombre}» debe estar entre {Cm(anchoMin)} y {Cm(anchoMax)} cm."];
        if (limites.AltoMinMm is int altoMin && limites.AltoMaxMm is int altoMax && (alto < altoMin || alto > altoMax))
            errores["altoMm"] = [$"El alto de «{limites.Nombre}» debe estar entre {Cm(altoMin)} y {Cm(altoMax)} cm."];
        return errores;
    }

    /// <summary>
    /// Error de las opciones elegidas (clave «opciones»): cada grupo debe existir en el producto y su valor ser uno de
    /// los ofrecidos. Elegir es opcional: lo que el cliente deja sin elegir lo define la oficina al cotizar.
    /// </summary>
    public static string? ValidarOpciones(IReadOnlyDictionary<string, string>? elegidas, LimitesProducto limites)
    {
        if (elegidas is null || elegidas.Count == 0)
            return null;
        foreach (var (grupo, valor) in elegidas)
        {
            if (!limites.Opciones.TryGetValue(grupo, out var valores))
                return $"«{Corto(grupo)}» no es una opción de «{limites.Nombre}».";
            if (!valores.Contains(valor))
                return $"«{Corto(valor)}» no es una opción válida de «{Corto(grupo)}» para «{limites.Nombre}».";
        }
        return Serializar(elegidas)!.Length > MaxLargoOpciones ? "Las opciones elegidas son demasiado largas." : null;
    }

    // Lo que llega del cliente no se repite entero en el mensaje.
    private static string Corto(string texto) => texto.Length <= 40 ? texto : texto[..40] + "…";

    /// <summary>Lo elegido como objeto JSON ordenado por grupo (así se guarda y se compara), o null si no eligió nada.</summary>
    public static string? Serializar(IReadOnlyDictionary<string, string>? elegidas) =>
        elegidas is null || elegidas.Count == 0
            ? null
            : JsonSerializer.Serialize(elegidas.OrderBy(o => o.Key, StringComparer.Ordinal).ToDictionary(o => o.Key, o => o.Value), Formato);

    /// <summary>Lo guardado de vuelta a diccionario; un valor ilegible o vacío es «sin opciones», no un error.</summary>
    public static IReadOnlyDictionary<string, string>? Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var opciones = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return opciones is { Count: > 0 } ? opciones : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
