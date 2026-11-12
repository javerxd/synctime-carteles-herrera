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
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problema });
    }

    private static ProblemDetails? Traducir(Exception exception) => exception switch
    {
        // En Development, ASP.NET lanza esta excepcion ante un JSON mal formado en vez de responder 400.
        BadHttpRequestException error => Predeterminado(error.StatusCode),
        DbUpdateConcurrencyException => ErrorApi.Detalle(StatusCodes.Status409Conflict, Concurrencia.CodigoVersionObsoleta, Concurrencia.VersionObsoleta),
        DbUpdateException { InnerException: SqlException sql } => TraducirSql(sql),
        _ => null
    };

    private static ProblemDetails Predeterminado(int estado)
    {
        var (codigo, mensaje) = ErrorApi.Predeterminado(estado);
        return ErrorApi.Detalle(estado, codigo, mensaje);
    }

    // Tercer nivel de validacion (RFS31): lo que rechazan las restricciones de la base.
    // Los nombres de restriccion del modelo (ck_, fk_) sirven aunque SQL Server responda en otro idioma.
    private static ProblemDetails? TraducirSql(SqlException sql) => sql.Number switch
    {
        2601 or 2627 => DeLaBase(StatusCodes.Status409Conflict, "registro_duplicado", "Ya existe un registro con esos datos."),
        2628 or 8152 => DeLaBase(StatusCodes.Status400BadRequest, "texto_muy_largo", "Un texto supera el largo permitido."),
        547 when sql.Message.Contains("\"ck_") => DeLaBase(StatusCodes.Status400BadRequest, "regla_de_datos", "Los datos no cumplen las reglas de negocio."),
        547 when sql.Message.Contains("DELETE") => DeLaBase(StatusCodes.Status409Conflict, "tiene_registros_asociados", "No se puede eliminar porque tiene registros asociados."),
        547 => DeLaBase(StatusCodes.Status400BadRequest, "referencia_inexistente", "Hace referencia a un registro que no existe."),
        _ => null
    };

    private static ProblemDetails DeLaBase(int estado, string codigo, string mensaje) =>
        ErrorApi.Detalle(estado, codigo, mensaje, nivel: ErrorApi.NivelBaseDeDatos);
}
