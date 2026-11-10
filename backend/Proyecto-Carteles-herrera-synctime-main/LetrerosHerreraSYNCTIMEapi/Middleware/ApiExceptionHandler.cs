using LetrerosHerreraSYNCTIMEapi.Endpoints;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Middleware;

// Traduce los errores causados por los datos recibidos a 400/409 para que no lleguen como un 500 generico.
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, titulo) = Traducir(exception);
        if (status == 0)
            return false;

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            // "error" es el mismo campo que usan los endpoints: el cliente lee el motivo siempre del mismo lugar.
            ProblemDetails = { Status = status, Title = titulo, Extensions = { ["error"] = titulo } }
        });
    }

    private static (int Status, string Titulo) Traducir(Exception exception) => exception switch
    {
        // En Development, ASP.NET lanza esta excepcion ante un JSON mal formado en vez de responder 400.
        BadHttpRequestException error => (error.StatusCode, "La solicitud no tiene un formato valido."),
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, Concurrencia.VersionObsoleta),
        DbUpdateException { InnerException: SqlException sql } => TraducirSql(sql),
        _ => (0, "")
    };

    // Los nombres de restriccion del modelo (ck_, fk_) sirven aunque SQL Server responda en otro idioma.
    private static (int Status, string Titulo) TraducirSql(SqlException sql) => sql.Number switch
    {
        2601 or 2627 => (StatusCodes.Status409Conflict, "Ya existe un registro con esos datos."),
        2628 or 8152 => (StatusCodes.Status400BadRequest, "Un texto supera el largo permitido."),
        547 when sql.Message.Contains("\"ck_") => (StatusCodes.Status400BadRequest, "Los datos no cumplen las reglas de negocio."),
        547 when sql.Message.Contains("DELETE") => (StatusCodes.Status409Conflict, "No se puede eliminar porque tiene registros asociados."),
        547 => (StatusCodes.Status400BadRequest, "Hace referencia a un registro que no existe."),
        _ => (0, "")
    };
}
