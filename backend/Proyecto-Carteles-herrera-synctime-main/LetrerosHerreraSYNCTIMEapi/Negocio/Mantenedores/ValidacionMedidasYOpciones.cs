using System.Globalization;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Mantenedores;

// Fase 12D (decisión 6): lo que el taller edita de cada producto: los límites de ancho y alto, las medidas
// fijas y las opciones por grupo. Medidas en milímetros (en pantalla se muestran en centímetros). Sin HTTP ni
// base de datos: se prueba sola en LetrerosHerreraSYNCTIMEapi.UnitTests.
public sealed record MedidaFijaEntrada(int? AnchoMm, int? AltoMm);
public sealed record GrupoOpcionesEntrada(string? Grupo, IReadOnlyList<string?>? Valores);
public sealed record EntradaMedidasYOpciones(
    int? AnchoMinMm, int? AnchoMaxMm, int? AltoMinMm, int? AltoMaxMm,
    IReadOnlyList<MedidaFijaEntrada>? MedidasFijas, IReadOnlyList<GrupoOpcionesEntrada>? Opciones);

public static class ValidacionMedidasYOpciones
{
    // El mismo tope que usa el resto del catálogo (1 a 50 000 mm = 5000 cm).
    public const int TopeMedidaMm = ValidacionSolicitud.MaxMedidaMm;
    public const int MaxMedidasFijas = 20;
    public const int MaxGrupos = 10;
    public const int MaxValoresPorGrupo = 30;
    public const int LargoGrupo = 60;
    public const int LargoValor = 80;

    private static string Cm(int mm) => (mm / 10m).ToString("0.##", CultureInfo.GetCultureInfo("es-CL"));

    public static Dictionary<string, string[]> Validar(EntradaMedidasYOpciones e)
    {
        var errores = new Dictionary<string, string[]>();
        var hayRangos = e.AnchoMinMm is not null || e.AnchoMaxMm is not null || e.AltoMinMm is not null || e.AltoMaxMm is not null;
        if (hayRangos)
        {
            Limite(errores, "anchoMinMm", e.AnchoMinMm, "El ancho mínimo");
            Limite(errores, "anchoMaxMm", e.AnchoMaxMm, "El ancho máximo");
            Limite(errores, "altoMinMm", e.AltoMinMm, "El alto mínimo");
            Limite(errores, "altoMaxMm", e.AltoMaxMm, "El alto máximo");
            Orden(errores, "anchoMaxMm", e.AnchoMinMm, e.AnchoMaxMm, "ancho");
            Orden(errores, "altoMaxMm", e.AltoMinMm, e.AltoMaxMm, "alto");
        }
        MedidasFijas(errores, e.MedidasFijas ?? [], hayRangos);
        Opciones(errores, e.Opciones ?? []);
        return errores;
    }

    private static void Limite(Dictionary<string, string[]> errores, string campo, int? valor, string nombre)
    {
        if (valor is not int mm)
            errores[campo] = [$"{nombre} es obligatorio: completa los cuatro límites o déjalos todos en blanco."];
        else if (mm < 1 || mm > TopeMedidaMm)
            errores[campo] = [$"{nombre} debe estar entre 0,1 y {Cm(TopeMedidaMm)} cm."];
    }

    private static void Orden(Dictionary<string, string[]> errores, string campoMaximo, int? minimo, int? maximo, string eje)
    {
        if (minimo is int min && maximo is int max && !errores.ContainsKey(campoMaximo) && max < min)
            errores[campoMaximo] = [$"El {eje} máximo no puede ser menor que el mínimo."];
    }

    private static void MedidasFijas(Dictionary<string, string[]> errores, IReadOnlyList<MedidaFijaEntrada> medidas, bool hayRangos)
    {
        if (medidas.Count == 0)
            return;
        if (hayRangos)
        {
            errores["medidasFijas"] = ["Un tipo de letrero usa medidas fijas o límites de medida, no los dos."];
            return;
        }
        if (medidas.Count > MaxMedidasFijas)
        {
            errores["medidasFijas"] = [$"Puede tener hasta {MaxMedidasFijas} medidas fijas."];
            return;
        }
        var vistas = new HashSet<(int, int)>();
        foreach (var medida in medidas)
        {
            if (medida is null || medida.AnchoMm is not int ancho || medida.AltoMm is not int alto || ancho < 1 || alto < 1 || ancho > TopeMedidaMm || alto > TopeMedidaMm)
            {
                errores["medidasFijas"] = [$"Cada medida fija necesita ancho y alto entre 0,1 y {Cm(TopeMedidaMm)} cm."];
                return;
            }
            if (!vistas.Add((ancho, alto)))
            {
                errores["medidasFijas"] = [$"La medida {Cm(ancho)} x {Cm(alto)} cm está repetida."];
                return;
            }
        }
    }

    private static void Opciones(Dictionary<string, string[]> errores, IReadOnlyList<GrupoOpcionesEntrada> grupos)
    {
        if (grupos.Count > MaxGrupos)
        {
            errores["opciones"] = [$"Puede tener hasta {MaxGrupos} grupos de opciones."];
            return;
        }
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var grupo in grupos)
        {
            var nombre = grupo?.Grupo?.Trim() ?? "";
            if (nombre.Length == 0 || nombre.Length > LargoGrupo)
            {
                errores["opciones"] = [$"Cada grupo necesita un nombre de hasta {LargoGrupo} caracteres."];
                return;
            }
            if (!nombres.Add(nombre))
            {
                errores["opciones"] = [$"El grupo «{nombre}» está repetido."];
                return;
            }
            var valores = (grupo!.Valores ?? []).Select(v => v?.Trim() ?? "").Where(v => v.Length > 0).ToList();
            if (valores.Count == 0)
            {
                errores["opciones"] = [$"El grupo «{nombre}» necesita al menos un valor."];
                return;
            }
            if (valores.Count > MaxValoresPorGrupo)
            {
                errores["opciones"] = [$"El grupo «{nombre}» puede tener hasta {MaxValoresPorGrupo} valores."];
                return;
            }
            if (valores.Any(v => v.Length > LargoValor))
            {
                errores["opciones"] = [$"En «{nombre}», cada valor puede tener hasta {LargoValor} caracteres."];
                return;
            }
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (valores.FirstOrDefault(v => !vistos.Add(v)) is { } repetido)
            {
                errores["opciones"] = [$"En «{nombre}», el valor «{repetido}» está repetido."];
                return;
            }
        }
    }
}
