using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Negocio.Indicadores;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Fase 12B (RFS09, RFU19, RFU27, S10 a S14, S19, S24): el dashboard de gestion. Solo dueño y administrador; el contrato
// esta en docs/api/openapi.json y docs/plan/fase-12-especificacion.md. Un filtro invalido responde 400 con el error por campo.
public static class IndicadoresApi
{
    public static void MapIndicadoresApi(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/v1/indicadores").WithTags("Indicadores")
            .RequireAuthorization(Politicas.Gestion).RequireRateLimiting("ApiGeneral");

        grupo.MapGet("/resumen", (HttpContext http, ServicioIndicadores servicio, CancellationToken ct) =>
            Responder(http, false, f => servicio.ResumenAsync(f, ct)));
        grupo.MapGet("/evolucion", (HttpContext http, ServicioIndicadores servicio, CancellationToken ct) =>
            Responder(http, false, f => servicio.EvolucionAsync(f, ct)));
        grupo.MapGet("/cobranza", (HttpContext http, ServicioIndicadores servicio, CancellationToken ct) =>
            Responder(http, false, f => servicio.CobranzaAsync(f, ct)));
        grupo.MapGet("/embudo", (HttpContext http, ServicioIndicadores servicio, CancellationToken ct) =>
            Responder(http, false, f => servicio.EmbudoAsync(f, ct)));
        grupo.MapGet("/demanda", (HttpContext http, ServicioIndicadores servicio, CancellationToken ct) =>
            Responder(http, false, f => servicio.DemandaAsync(f, ct)));
        grupo.MapGet("/por-cobrar", (HttpContext http, ServicioIndicadores servicio, CancellationToken ct) =>
            Responder(http, true, f => servicio.PorCobrarAsync(f, ct)));
    }

    private static async Task<IResult> Responder<T>(HttpContext http, bool conPagina, Func<FiltroIndicadores, Task<T>> calcular)
    {
        var consulta = http.Request.Query.ToDictionary(p => p.Key, p => (string?)p.Value.ToString(), StringComparer.Ordinal);
        var (filtro, errores) = ReglasIndicadores.Validar(consulta, conPagina);
        if (filtro is null)
            return ErrorApi.Invalido(errores.Values.First()[0], errores);
        return Results.Ok(await calcular(filtro));
    }
}
