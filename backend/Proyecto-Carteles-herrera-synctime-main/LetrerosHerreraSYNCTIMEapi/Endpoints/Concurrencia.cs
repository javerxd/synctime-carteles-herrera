namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Evita que una edicion hecha con datos viejos pise el trabajo de otra persona.
// Solicitudes, cotizaciones (fase 8) y pedidos tienen row_version en la base; los servicios comparan la
// version que vio quien edita y la base la vuelve a comparar al guardar.
public static class Concurrencia
{
    // Mensaje de ApiExceptionHandler cuando EF detecta el cambio al guardar fuera de un servicio.
    public const string VersionObsoleta = "El registro fue modificado por otra persona. Vuelva a cargarlo.";

    // Codigo propio: el cliente lo usa para ofrecer "volver a cargar" en vez de un error generico.
    public const string CodigoVersionObsoleta = "version_obsoleta";

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
