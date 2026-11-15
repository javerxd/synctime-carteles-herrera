using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Endpoints;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Middleware;

// Traduce los errores causados por los datos recibidos a 400/409 con el formato estandar, para que no
// lleguen como un 500 generico. Lo que no reconoce lo deja pasar: sale como 500 sin detalles internos.
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problema = Traducir(exception);
        if (problema is null)
            return false;

        httpContext.Response.StatusCode = problema.Status!.Value;
        if (exception is Services.OperacionOcupadaException)
            httpContext.Response.Headers.RetryAfter = "5";
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problema });
    }

    private static ProblemDetails? Traducir(Exception exception) => exception switch
    {
        // En Development, ASP.NET lanza esta excepcion ante un JSON mal formado en vez de responder 400.
        BadHttpRequestException error => Predeterminado(error.StatusCode),
        // Fase 7: no se consiguio el turno (sp_getapplock) a tiempo. Se puede reintentar en unos segundos.
        Services.OperacionOcupadaException ocupada => ErrorApi.Detalle(StatusCodes.Status503ServiceUnavailable, "servicio_ocupado", ocupada.Message),
        DbUpdateConcurrencyException => ErrorApi.Detalle(StatusCodes.Status409Conflict, Concurrencia.CodigoVersionObsoleta, Concurrencia.VersionObsoleta),
        DbUpdateException { InnerException: SqlException sql } => TraducirSql(sql.Number, sql.Message),
        _ => null
    };

    private static ProblemDetails Predeterminado(int estado)
    {
        var (codigo, mensaje) = ErrorApi.Predeterminado(estado);
        return ErrorApi.Detalle(estado, codigo, mensaje);
    }

    // Tercer nivel de validacion (RFS31): lo que rechazan las restricciones de la base.
    // Los nombres de restriccion del modelo (ck_, fk_) sirven aunque SQL Server responda en otro idioma.
    // Recibe el numero y el mensaje del error (no la excepcion) para poder probarse sin una base.
    public static ProblemDetails? TraducirSql(int numero, string mensaje) => numero switch
    {
        2601 or 2627 => DeLaBase(StatusCodes.Status409Conflict, "registro_duplicado", "Ya existe un registro con esos datos."),
        2628 or 8152 => DeLaBase(StatusCodes.Status400BadRequest, "texto_muy_largo", "Un texto supera el largo permitido."),
        547 when mensaje.Contains("\"ck_") => DeLaBase(StatusCodes.Status400BadRequest, "regla_de_datos", "Los datos no cumplen las reglas de negocio."),
        547 when mensaje.Contains("DELETE") => DeLaBase(StatusCodes.Status409Conflict, "tiene_registros_asociados", "No se puede eliminar porque tiene registros asociados."),
        547 => DeLaBase(StatusCodes.Status400BadRequest, "referencia_inexistente", "Hace referencia a un registro que no existe."),
        // Fase 8: el trigger de pagos no deja reactivar un abono anulado.
        50080 => DeLaBase(StatusCodes.Status409Conflict, "abono_anulado", "Un abono anulado no se puede reactivar."),
        _ => null
    };

    private static ProblemDetails DeLaBase(int estado, string codigo, string mensaje) =>
        ErrorApi.Detalle(estado, codigo, mensaje, nivel: ErrorApi.NivelBaseDeDatos);
}
