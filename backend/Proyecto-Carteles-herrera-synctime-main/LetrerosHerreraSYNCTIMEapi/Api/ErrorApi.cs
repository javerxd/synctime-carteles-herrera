using Microsoft.AspNetCore.Mvc;

namespace LetrerosHerreraSYNCTIMEapi.Api;

// Formato unico de error de la API (RFS31, S21). Es un ProblemDetails (RFC 9457) con cuatro campos propios:
//   error   -> mensaje en español, listo para mostrar al usuario
//   codigo  -> identificador estable, para que el programa cliente decida que hacer
//   nivel   -> donde se detecto: "api" o "base_de_datos" (el nivel "formulario" es del frontend)
//   errores -> detalle por campo, solo cuando fallan validaciones de entrada
public static class ErrorApi
{
    public const string NivelApi = "api";
    public const string NivelBaseDeDatos = "base_de_datos";

    public static IResult Invalido(string mensaje, IReadOnlyDictionary<string, string[]>? errores = null) =>
        Crear(StatusCodes.Status400BadRequest, "datos_invalidos", mensaje, errores);

    public static IResult Conflicto(string mensaje) =>
        Crear(StatusCodes.Status409Conflict, "conflicto", mensaje);

    public static IResult NoDisponible(string mensaje) =>
        Crear(StatusCodes.Status503ServiceUnavailable, "servicio_no_disponible", mensaje);

    public static IResult DemasiadosIntentos()
    {
        var (codigo, mensaje) = Predeterminado(StatusCodes.Status429TooManyRequests);
        return Crear(StatusCodes.Status429TooManyRequests, codigo, mensaje);
    }

    public static IResult Crear(
        int estado, string codigo, string mensaje, IReadOnlyDictionary<string, string[]>? errores = null, string nivel = NivelApi) =>
        Results.Problem(Detalle(estado, codigo, mensaje, errores, nivel));

    public static ProblemDetails Detalle(
        int estado, string codigo, string mensaje, IReadOnlyDictionary<string, string[]>? errores = null, string nivel = NivelApi)
    {
        var problema = new ProblemDetails { Status = estado, Title = mensaje };
        problema.Extensions["error"] = mensaje;
        problema.Extensions["codigo"] = codigo;
        problema.Extensions["nivel"] = nivel;
        if (errores is { Count: > 0 })
            problema.Extensions["errores"] = errores;
        return problema;
    }

    // Completa lo que falte en cualquier ProblemDetails que escriba el framework: las respuestas sin
    // cuerpo (401, 403, 404, 429) y las excepciones no previstas. Lo que ya viene definido no se toca.
    public static void Completar(ProblemDetails problema)
    {
        if (problema.Extensions.ContainsKey("error"))
            return;

        var (codigo, mensaje) = Predeterminado(problema.Status ?? StatusCodes.Status500InternalServerError);
        problema.Title = mensaje;
        problema.Detail = null;
        problema.Extensions["error"] = mensaje;
        problema.Extensions["codigo"] = codigo;
        problema.Extensions["nivel"] = NivelApi;
    }

    public static (string Codigo, string Mensaje) Predeterminado(int estado) => estado switch
    {
        StatusCodes.Status400BadRequest => ("solicitud_invalida", "La solicitud no tiene un formato válido."),
        StatusCodes.Status401Unauthorized => ("no_autenticado", "Debe iniciar sesión, o sus credenciales no son válidas."),
        StatusCodes.Status403Forbidden => ("sin_permiso", "No tiene permiso para realizar esta acción."),
        StatusCodes.Status404NotFound => ("no_encontrado", "No se encontró lo solicitado."),
        StatusCodes.Status405MethodNotAllowed => ("metodo_no_permitido", "Esta operación no está disponible."),
        StatusCodes.Status409Conflict => ("conflicto", "La operación entra en conflicto con el estado actual de los datos."),
        StatusCodes.Status413PayloadTooLarge => ("solicitud_muy_grande", "La solicitud supera el tamaño permitido."),
        StatusCodes.Status415UnsupportedMediaType => ("formato_no_admitido", "El formato de la solicitud no está admitido."),
        StatusCodes.Status429TooManyRequests => ("demasiados_intentos", "Demasiados intentos. Espere un minuto y vuelva a intentarlo."),
        StatusCodes.Status503ServiceUnavailable => ("servicio_no_disponible", "El servicio no está disponible en este momento."),
        >= 500 => ("error_interno", "Ocurrió un error inesperado. Inténtelo de nuevo más tarde."),
        _ => ("error", "No se pudo completar la solicitud.")
    };
}
