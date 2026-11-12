using LetrerosHerreraSYNCTIMEapi.Models;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LetrerosHerreraSYNCTIMEapi.Api;

// Comprobaciones de salud para el orquestador de contenedores y para quien despliega:
//   /salud        -> el proceso responde (no consulta nada)
//   /salud/lista  -> ademas puede hablar con la base de datos
public sealed class SaludBaseDeDatos(LetrerosHerreradbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
}

public static class Salud
{
    public const string EtiquetaLista = "lista";

    public static void MapSalud(this WebApplication app)
    {
        app.MapHealthChecks("/salud", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = Escribir })
            .AllowAnonymous();
        // Esta si consulta la base en cada llamada: sin limite, serviria para agotar sus conexiones.
        app.MapHealthChecks("/salud/lista", new HealthCheckOptions { Predicate = c => c.Tags.Contains(EtiquetaLista), ResponseWriter = Escribir })
            .AllowAnonymous()
            .RequireRateLimiting(PoliticaDeLimite);
    }

    public const string PoliticaDeLimite = "Salud";

    // Solo "ok" o "fallo" por comprobacion: el motivo queda en los registros del servidor, no en la respuesta.
    private static Task Escribir(HttpContext context, HealthReport informe) =>
        context.Response.WriteAsJsonAsync(new
        {
            estado = Texto(informe.Status),
            comprobaciones = informe.Entries.ToDictionary(e => e.Key, e => Texto(e.Value.Status))
        });

    private static string Texto(HealthStatus estado) => estado == HealthStatus.Healthy ? "ok" : "fallo";
}

public static class CabecerasDeSeguridad
{
    // Cabeceras que toda respuesta debe llevar. La politica de contenido estricta se omite en desarrollo
    // porque bloquearia la pagina de documentacion de la API (Scalar), que carga sus propios scripts.
    public static IApplicationBuilder UseCabecerasDeSeguridad(this IApplicationBuilder app, bool politicaDeContenidoEstricta) =>
        app.Use(async (context, next) =>
        {
            // Se escriben justo antes de enviar la respuesta: el manejador de excepciones borra las cabeceras
            // puestas antes, y los errores son precisamente las respuestas que mas las necesitan.
            context.Response.OnStarting(() =>
            {
                var cabeceras = context.Response.Headers;
                cabeceras.XContentTypeOptions = "nosniff";
                cabeceras.XFrameOptions = "DENY";
                cabeceras["Referrer-Policy"] = "no-referrer";
                if (politicaDeContenidoEstricta)
                    cabeceras.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
                // Las respuestas de autenticacion llevan tokens: ningun intermediario debe guardarlas.
                if (context.Request.Path.StartsWithSegments("/api/v1/auth"))
                    cabeceras.CacheControl = "no-store";
                return Task.CompletedTask;
            });
            await next();
        });
}
