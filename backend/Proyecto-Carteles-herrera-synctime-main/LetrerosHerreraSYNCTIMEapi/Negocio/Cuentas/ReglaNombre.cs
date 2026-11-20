using System.Text.RegularExpressions;

namespace LetrerosHerreraSYNCTIMEapi.Negocio;

// Fase 12D: regla única de nombres y apellidos de personas. Solo letras (con tildes, diéresis y ñ), hasta
// 20 caracteres en total, sin espacios al inicio ni al final y un solo espacio entre dos palabras, con
// 5 espacios como máximo (seis palabras). No se cambia lo que la persona escribió: si no cumple, se avisa.
// El frontend tiene la misma regla en src/utilidades/reglaNombre.js; los dos se prueban con los mismos
// casos (LetrerosHerreraSYNCTIMEapi.UnitTests/CasosNombre.json).
public static partial class ReglaNombre
{
    public const int MaximoCaracteres = 20;
    public const int MaximoEspacios = 5;
    private const int TopeDeRevision = 200;

    public const string Vacio = "vacio";
    public const string Bordes = "bordes";
    public const string Caracteres = "caracteres";
    public const string Espacios = "espacios";
    public const string Largo = "largo";

    // Letras con sus marcas combinadas (una tilde escrita aparte), separadas por un solo espacio.
    [GeneratedRegex(@"^[\p{L}\p{M}]+( [\p{L}\p{M}]+)*$")]
    private static partial Regex SoloLetras();

    // null si el valor cumple; si no, el código de la primera regla que falla (orden: vacío, bordes,
    // caracteres, espacios, largo).
    public static string? Revisar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return Vacio;
        if (char.IsWhiteSpace(valor[0]) || char.IsWhiteSpace(valor[^1]))
            return Bordes;
        // Un texto enorme no se recorre entero para decir que es largo.
        if (valor.Length > TopeDeRevision)
            return Largo;
        foreach (var c in valor)
            if (c != ' ' && !char.IsLetter(c) && char.GetUnicodeCategory(c) is not (System.Globalization.UnicodeCategory.NonSpacingMark
                or System.Globalization.UnicodeCategory.SpacingCombiningMark or System.Globalization.UnicodeCategory.EnclosingMark))
                return Caracteres;
        // Cada palabra empieza con una letra: una marca combinante suelta («́») no es un nombre.
        if (valor.Split(' ').Any(palabra => palabra.Length > 0 && !char.IsLetter(palabra[0])))
            return Caracteres;
        if (!SoloLetras().IsMatch(valor) || valor.Count(c => c == ' ') > MaximoEspacios)
            return Espacios;
        return valor.Length > MaximoCaracteres ? Largo : null;
    }

    public static string Mensaje(string codigo) => codigo switch
    {
        Bordes => "Quita los espacios al inicio y al final.",
        Espacios => $"Deja un solo espacio entre palabras, hasta {MaximoEspacios + 1} palabras.",
        _ => $"Usa solo letras, hasta {MaximoCaracteres} caracteres.",
    };

    // El mensaje para mostrar junto al campo, o null si el valor es válido. «mensajeVacio» es el del
    // formulario («Escribe tu nombre.»).
    public static string? Error(string? valor, string mensajeVacio) => Revisar(valor) switch
    {
        null => null,
        Vacio => mensajeVacio,
        var codigo => Mensaje(codigo),
    };
}
