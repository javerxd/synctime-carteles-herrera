using LetrerosHerreraSYNCTIMEapi.Api;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Evita que una edicion hecha con datos viejos pise el trabajo de otra persona.
// Solo solicitudes y pedidos tienen row_version en la base.
public static class Concurrencia
{
    public const string VersionObsoleta = "El registro fue modificado por otra persona. Vuelva a cargarlo.";

    // Codigo propio: el cliente lo usa para ofrecer "volver a cargar" en vez de un error generico.
    public const string CodigoVersionObsoleta = "version_obsoleta";

    // Devuelve la respuesta de error, o null si la version enviada es la vigente.
    // Si otro guarda justo despues de esta comprobacion, lo detecta EF al guardar y ApiExceptionHandler responde 409.
    public static IResult? Verificar(byte[]? enviada, byte[] vigente)
    {
        if (enviada is null or { Length: 0 })
            return ErrorApi.Invalido("Indique rowVersion: es el valor que entrega el GET del registro.");

        return enviada.AsSpan().SequenceEqual(vigente)
            ? null
            : ErrorApi.Crear(StatusCodes.Status409Conflict, CodigoVersionObsoleta, VersionObsoleta);
    }

    // Fase 7: la version vigente viaja como ETag ("base64") y vuelve en la cabecera If-Match. Devuelve
    // false si la cabecera viene pero no se puede leer; version queda null si no vino.
    public static bool TryLeerIfMatch(HttpRequest peticion, out byte[]? version)
    {
        version = null;
        var valor = peticion.Headers.IfMatch.ToString().Trim();
        if (valor.Length == 0)
            return true;
        if (valor.StartsWith("W/", StringComparison.Ordinal))
            valor = valor[2..];
        valor = valor.Trim('"');
        try
        {
            version = Convert.FromBase64String(valor);
            return version.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string ETag(byte[] version) => $"\"{Convert.ToBase64String(version)}\"";
}
