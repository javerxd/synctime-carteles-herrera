using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Fase 13A (RFS10, RFU24 a RFU26, RFU36, RNFP18, E16, E20, S18, S24): los tres reportes y su exportacion a Excel, PDF y CSV.
//   GET  /reportes/{operativo|gerencial|parametrizable}                    el reporte como JSON (lo que la pantalla muestra y luego se exporta)
//   POST /reportes/{reporte}/exportaciones                                 pide el archivo: responde 202 sin esperarlo
//   GET  /reportes/exportaciones/{id}  y  /archivo                         el estado y la descarga, solo para quien lo pidio
// El operativo lo ve todo el personal; el gerencial y el parametrizable, solo el dueño y el administrador: el permiso va en la
// ruta de cada reporte (nunca en un parametro). Un filtro invalido responde 400 con el error por campo. El contrato esta en
// docs/api/openapi.json y docs/plan/fase-13-plan.md.
public static class ReportesApi
{
    public const string PoliticaDeLimite = "Exportaciones";
    public const string PoliticaDeLectura = "Reportes";
    private const int TamanoMaximoDelPedido = 4 * 1024;

    public static void MapReportesApi(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/v1/reportes").WithTags("Reportes")
            .RequireAuthorization(Politicas.Empleado).RequireRateLimiting("ApiGeneral");

        // ── Los reportes ──
        grupo.MapGet("/operativo", (HttpContext http, ServicioReportes servicio, CancellationToken ct) =>
            Reporte(http, TiposReporte.Operativo, servicio, ct)).RequireRateLimiting(PoliticaDeLectura);
        grupo.MapGet("/gerencial", (HttpContext http, ServicioReportes servicio, CancellationToken ct) =>
            Reporte(http, TiposReporte.Gerencial, servicio, ct)).RequireAuthorization(Politicas.Gestion).RequireRateLimiting(PoliticaDeLectura);
        grupo.MapGet("/parametrizable", (HttpContext http, ServicioReportes servicio, CancellationToken ct) =>
            Reporte(http, TiposReporte.Parametrizable, servicio, ct)).RequireAuthorization(Politicas.Gestion).RequireRateLimiting(PoliticaDeLectura);

        // ── Pedir la exportacion (RNFP18): 202 con el trabajo; 10 por minuto por persona ──
        grupo.MapPost("/operativo/exportaciones", (SolicitudExportacion req, ClaimsPrincipal user, ServicioExportaciones servicio, CancellationToken ct) =>
            Exportar(TiposReporte.Operativo, req, user, servicio, ct)).RequireRateLimiting(PoliticaDeLimite).WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoDelPedido));
        grupo.MapPost("/gerencial/exportaciones", (SolicitudExportacion req, ClaimsPrincipal user, ServicioExportaciones servicio, CancellationToken ct) =>
            Exportar(TiposReporte.Gerencial, req, user, servicio, ct)).RequireAuthorization(Politicas.Gestion).RequireRateLimiting(PoliticaDeLimite).WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoDelPedido));
        grupo.MapPost("/parametrizable/exportaciones", (SolicitudExportacion req, ClaimsPrincipal user, ServicioExportaciones servicio, CancellationToken ct) =>
            Exportar(TiposReporte.Parametrizable, req, user, servicio, ct)).RequireAuthorization(Politicas.Gestion).RequireRateLimiting(PoliticaDeLimite).WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoDelPedido));

        // ── El estado y la descarga: de un tercero responden 404, no 403 (no se revela que el trabajo existe) ──
        grupo.MapGet("/exportaciones/{id}", async (string id, ClaimsPrincipal user, HttpContext http, ServicioExportaciones servicio, CancellationToken ct) =>
            NoGuardar(http) && Guid.TryParse(id, out var trabajo) && await servicio.ConsultarAsync(Quien(user), trabajo, ct) is { } vista
                ? Results.Ok(vista)
                : Results.NotFound());

        grupo.MapGet("/exportaciones/{id}/archivo", async (string id, ClaimsPrincipal user, HttpContext http, ServicioExportaciones servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out var trabajo))
                return Results.NotFound();
            switch (await servicio.DescargarAsync(Quien(user), trabajo, ct))
            {
                case ResultadoDescarga.Archivo archivo:
                    // Un reporte lleva datos de clientes: nada se guarda en caches intermedias.
                    http.Response.Headers.CacheControl = "no-store";
                    // Fase 16C-1: el reporte sale como flujo (en la nube vive en Blob). ASP.NET cierra el flujo al terminar.
                    return Results.Stream(archivo.Contenido.Flujo, archivo.TipoMime, archivo.Nombre);
                case ResultadoDescarga.Vencido:
                    return ErrorApi.Crear(StatusCodes.Status410Gone, "exportacion_vencida", "El archivo venció y ya no está disponible. Vuelve a pedirlo.");
                case ResultadoDescarga.NoListo { Estado: "fallido" } fallido:
                    return ErrorApi.Conflicto(fallido.Error ?? "No se pudo preparar el archivo.");
                case ResultadoDescarga.NoListo:
                    return ErrorApi.Conflicto("El archivo todavía se está preparando.");
                default:
                    return Results.NotFound();
            }
        });
    }

    private static bool NoGuardar(HttpContext http)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        return true;
    }

    private static PersonalQueExporta Quien(ClaimsPrincipal user) => new(user.IdUsuario(), user.FindFirstValue(ClaimTypes.Role) ?? "");

    private static async Task<IResult> Reporte(HttpContext http, string reporte, ServicioReportes servicio, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";   // lleva nombres de clientes y montos
        var consulta = http.Request.Query.ToDictionary(p => p.Key, p => (string?)p.Value.ToString(), StringComparer.Ordinal);
        var (filtro, errores) = ReglasReportes.Validar(reporte, consulta);
        if (filtro is null)
            return ErrorApi.Invalido(errores.Values.First()[0], errores);
        try
        {
            return Results.Ok(await servicio.ConstruirAhoraAsync(filtro, ct));
        }
        catch (ReporteDemasiadoGrandeException e)
        {
            return ErrorApi.Crear(StatusCodes.Status400BadRequest, "reporte_muy_grande", e.Message);
        }
    }

    private static async Task<IResult> Exportar(string reporte, SolicitudExportacion req, ClaimsPrincipal user, ServicioExportaciones servicio, CancellationToken ct)
    {
        var (consulta, errores) = Convertir(req.Filtros);
        if (errores.Count > 0)
            return ErrorApi.Invalido(errores.Values.First()[0], errores);

        return await servicio.SolicitarAsync(Quien(user), reporte, req.Formato, consulta, ct) switch
        {
            ResultadoExportacion.Aceptado a => Results.Accepted($"/api/v1/reportes/exportaciones/{a.Trabajo.Id}", a.Trabajo),
            ResultadoExportacion.Invalido i => ErrorApi.Invalido(i.Errores.Values.First()[0], i.Errores),
            ResultadoExportacion.DemasiadoGrande g => ErrorApi.Crear(StatusCodes.Status400BadRequest, "reporte_muy_grande", g.Mensaje),
            ResultadoExportacion.Ocupado o => ErrorApi.Conflicto(o.Mensaje),
            _ => Results.Forbid()
        };
    }

    // Los filtros del cuerpo: texto o numero (o una lista de textos, que se une con comas). Cualquier otra cosa es un error del campo.
    private static (Dictionary<string, string?> Consulta, Dictionary<string, string[]> Errores) Convertir(Dictionary<string, JsonElement>? filtros)
    {
        var consulta = new Dictionary<string, string?>(StringComparer.Ordinal);
        var errores = new Dictionary<string, string[]>();
        foreach (var (clave, valor) in filtros ?? [])
        {
            switch (valor.ValueKind)
            {
                case JsonValueKind.String:
                    consulta[clave] = valor.GetString();
                    break;
                case JsonValueKind.Number:
                    consulta[clave] = valor.GetRawText();
                    break;
                case JsonValueKind.Null:
                    break;
                case JsonValueKind.Array when valor.EnumerateArray().All(e => e.ValueKind == JsonValueKind.String):
                    consulta[clave] = string.Join(',', valor.EnumerateArray().Select(e => e.GetString()));
                    break;
                default:
                    errores[clave] = [$"El filtro «{clave}» debe ser un texto, un número o una lista de textos."];
                    break;
            }
        }
        return (consulta, errores);
    }
}

/// <summary>Cuerpo de POST /reportes/{reporte}/exportaciones: el formato (xlsx, csv o pdf) y los mismos filtros del reporte.</summary>
public sealed record SolicitudExportacion(string? Formato, Dictionary<string, JsonElement>? Filtros);
