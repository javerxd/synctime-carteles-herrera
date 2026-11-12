using System.Net.Mail;
using System.Text.RegularExpressions;

namespace LetrerosHerreraSYNCTIMEapi.Negocio;

// Reglas de formato compartidas por toda la API. No dependen de HTTP ni de la base de datos,
// asi que se prueban solas y sirven igual para un endpoint, un servicio o la semilla de datos.

// RUT chileno con digito verificador (RNFO06).
public static partial class Rut
{
    [GeneratedRegex(@"^(\d{1,2}(\.\d{3}){0,2}|\d{1,8})-?[\dkK]$")]
    private static partial Regex Formato();

    public static bool EsValido(string? rut) => TryNormalizar(rut, out _);

    // Acepta "12.345.678-5", "12345678-5" y "123456785". Devuelve siempre "12345678-5".
    public static bool TryNormalizar(string? rut, out string normalizado)
    {
        normalizado = "";
        if (string.IsNullOrWhiteSpace(rut))
            return false;

        // Solo tres formas de escribirlo: con puntos y guion, solo con guion, o todo junto.
        if (!Formato().IsMatch(rut.Trim()))
            return false;

        var limpio = rut.Trim().Replace(".", "").Replace("-", "").ToUpperInvariant();
        if (limpio.Length is < 2 or > 9)
            return false;

        var cuerpo = limpio[..^1];
        if (cuerpo[0] == '0' || !cuerpo.All(char.IsAsciiDigit) || limpio[^1] != DigitoVerificador(cuerpo))
            return false;

        normalizado = $"{cuerpo}-{limpio[^1]}";
        return true;
    }

    // Modulo 11: los digitos se multiplican de derecha a izquierda por la serie 2, 3, 4, 5, 6, 7.
    public static char DigitoVerificador(string cuerpo)
    {
        var suma = 0;
        var factor = 2;
        for (var i = cuerpo.Length - 1; i >= 0; i--)
        {
            suma += (cuerpo[i] - '0') * factor;
            factor = factor == 7 ? 2 : factor + 1;
        }

        return (11 - suma % 11) switch
        {
            11 => '0',
            10 => 'K',
            var resto => (char)('0' + resto)
        };
    }
}

public static class Reglas
{
    // Los montos son pesos chilenos: enteros y no negativos (RNFO05).
    public static bool EsMontoEnPesos(decimal monto) => monto >= 0 && monto == decimal.Truncate(monto);

    // Telefono chileno: nueve digitos, con el prefijo +56 opcional. Se ignoran los espacios.
    public static bool EsTelefono(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
            return false;

        var limpio = telefono.Replace(" ", "");
        if (limpio.StartsWith("+56", StringComparison.Ordinal))
            limpio = limpio[3..];
        return limpio.Length == 9 && limpio.All(char.IsAsciiDigit);
    }

    // Una direccion simple, sin nombre visible, y con un dominio que tenga al menos un punto.
    public static bool EsCorreo(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo) || correo.Length > 254 || correo.Any(char.IsWhiteSpace))
            return false;

        return MailAddress.TryCreate(correo, out var direccion)
            && direccion.Address == correo
            && !correo.Contains("..")
            && !direccion.Host.Contains('[')
            && direccion.Host.Contains('.')
            && !direccion.Host.StartsWith('.')
            && !direccion.Host.EndsWith('.');
    }
}

// Junta los errores de validacion por campo para devolverlos todos de una vez (RFS31, S21).
public sealed class ErroresDeCampos
{
    private readonly List<(string Campo, string Mensaje)> errores = [];

    public bool Hay => errores.Count > 0;

    // Primer mensaje: sirve de resumen cuando el cliente solo muestra una linea.
    public string Resumen => Hay ? errores[0].Mensaje : "";

    public ErroresDeCampos Si(bool condicion, string campo, string mensaje)
    {
        if (condicion)
            errores.Add((campo, mensaje));
        return this;
    }

    public ErroresDeCampos Requerido(string? valor, string campo, string mensaje) =>
        Si(string.IsNullOrWhiteSpace(valor), campo, mensaje);

    // Los campos conservan el orden en que se revisaron, que suele ser el del formulario.
    public IReadOnlyDictionary<string, string[]> PorCampo()
    {
        var porCampo = new OrderedDictionary<string, string[]>();
        foreach (var grupo in errores.GroupBy(e => e.Campo))
            porCampo.Add(grupo.Key, grupo.Select(e => e.Mensaje).ToArray());
        return porCampo;
    }
}
