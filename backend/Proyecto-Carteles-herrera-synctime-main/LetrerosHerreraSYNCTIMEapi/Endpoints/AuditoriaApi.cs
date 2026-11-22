using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Fase 11 (RFS12, RFU28, S20): el visor de la bitacora y de los consentimientos. Solo dueño y administrador; el visor
// no se exporta (las exportaciones de la fase 13 son las de los reportes, que aqui quedan como «Exportó un reporte»).
public static class AuditoriaApi
{
    public static void MapAuditoriaApi(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/v1/auditoria").WithTags("Auditoría")
            .RequireAuthorization(Politicas.Gestion).RequireRateLimiting("ApiGeneral");

        // Del mas reciente al mas antiguo. q busca el identificador exacto de la entidad (por ejemplo, el numero de la solicitud).
        grupo.MapGet("/", async (ServicioVisorAuditoria servicio, CancellationToken ct, int? pagina, int? tamano, DateOnly? desde, DateOnly? hasta,
            string? entidad, string? accion, int? idEmpleado, string? q) =>
            Results.Ok(await servicio.ListarAsync(new FiltroAuditoria(desde, hasta, Limpiar(entidad, 40), Limpiar(accion, 30), idEmpleado, Limpiar(q, 40)),
                new ParametrosListado(pagina, tamano), ct)));

        grupo.MapGet("/consentimientos", async (ServicioVisorAuditoria servicio, CancellationToken ct, int? pagina, int? tamano, string? tipo,
            DateOnly? desde, DateOnly? hasta) =>
            Results.Ok(await servicio.ConsentimientosAsync(new FiltroConsentimientos(Limpiar(tipo, 20), desde, hasta), new ParametrosListado(pagina, tamano), ct)));
    }

    private static string? Limpiar(string? texto, int largo)
    {
        var limpio = texto?.Trim();
        return string.IsNullOrEmpty(limpio) ? null : limpio.Length > largo ? limpio[..largo] : limpio;
    }
}
