using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Negocio.Mantenedores;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Fase 9: traduce el resultado de un servicio de gestion a la respuesta HTTP, con los mismos codigos que las
// fases 7 y 8 (400 sin_version, 409 version_obsoleta, 409 con codigo propio). Si lo devuelto tiene version,
// va tambien como ETag.
public static class RespuestasGestion
{
    public static IResult Responder<T>(ResultadoGestion<T> resultado, HttpContext http, Func<T, IResult> correcta, string mensajeVersionObsoleta)
    {
        switch (resultado)
        {
            case ResultadoGestion<T>.Correcto { Valor: var valor }:
                if (valor is IConVersion conVersion)
                    http.Response.Headers.ETag = Concurrencia.ETag(conVersion.RowVersion);
                return correcta(valor);
            case ResultadoGestion<T>.Invalido { Errores: var errores, Mensaje: var mensaje }:
                return ErrorApi.Invalido(mensaje ?? ValidacionMantenedores.Resumen(errores), errores.Count > 0 ? errores : null);
            case ResultadoGestion<T>.Conflicto { Codigo: var codigo, Mensaje: var mensaje, DesdeLaBase: var desdeLaBase }:
                return ErrorApi.Crear(StatusCodes.Status409Conflict, codigo, mensaje, nivel: desdeLaBase ? ErrorApi.NivelBaseDeDatos : ErrorApi.NivelApi);
            case ResultadoGestion<T>.SinVersion:
                return SinVersion();
            case ResultadoGestion<T>.VersionObsoleta:
                return ErrorApi.Crear(StatusCodes.Status409Conflict, Concurrencia.CodigoVersionObsoleta, mensajeVersionObsoleta);
            case ResultadoGestion<T>.DemasiadosIntentos { Mensaje: var mensaje }:
                return ErrorApi.Crear(StatusCodes.Status429TooManyRequests, "demasiados_intentos", mensaje);
            case ResultadoGestion<T>.NoDisponible { Mensaje: var mensaje }:
                return ErrorApi.NoDisponible(mensaje);
            case ResultadoGestion<T>.ReautenticacionFallida:
                return ReautenticacionFallida();
            default:
                return Results.NotFound();
        }
    }

    // 400 datos_invalidos con errores.passwordActual (no 401: el frontend cierra la sesion ante cualquier 401).
    public static IResult ReautenticacionFallida() =>
        ErrorApi.Invalido(Reautenticacion.Incorrecta, new Dictionary<string, string[]> { ["passwordActual"] = [Reautenticacion.Incorrecta] });

    public static IResult SinVersion() =>
        ErrorApi.Crear(StatusCodes.Status400BadRequest, "sin_version", "Indica la versión que viste (cabecera If-Match con el ETag, o rowVersion).");

    public static IResult IfMatchIlegible() =>
        ErrorApi.Invalido("La cabecera If-Match no es una versión válida. Vuelve a cargar el registro.");

    // La version vista: la cabecera If-Match o, si no vino, rowVersion en el cuerpo. null si la cabecera es ilegible.
    public static bool TryVersion(HttpRequest peticion, byte[]? delCuerpo, out byte[]? version)
    {
        if (!Concurrencia.TryLeerIfMatch(peticion, out var cabecera))
        {
            version = null;
            return false;
        }
        version = cabecera ?? delCuerpo;
        return true;
    }

    // Con la version como ETag, para que el formulario la envie al guardar.
    public static IResult ConETag<T>(T? valor, HttpContext http) where T : class, IConVersion
    {
        if (valor is null)
            return Results.NotFound();
        http.Response.Headers.ETag = Concurrencia.ETag(valor.RowVersion);
        return Results.Ok(valor);
    }
}

// Cambiar si esta activo (o si se ve, en la vitrina): { activo } o { visible }, con la version vista.
public record CambioActivoRequest(bool? Activo, byte[]? RowVersion = null);
public record CambioVisibleRequest(bool? Visible, byte[]? RowVersion = null);
