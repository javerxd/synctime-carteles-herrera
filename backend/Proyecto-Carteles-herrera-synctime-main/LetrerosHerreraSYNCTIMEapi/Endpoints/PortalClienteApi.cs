using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Fase 8 (RFU07, RFU08, S04, S06): los pedidos y las cotizaciones del cliente con sesion. El id sale siempre
// del token: lo ajeno responde 404, igual que lo que no existe. El cliente no ve el historial interno del
// pedido ni quien registro cada abono. Sus solicitudes y adjuntos estan en MisSolicitudesApi (fase 7).
public static class PortalClienteApi
{
    public static void MapPortalClienteApi(this WebApplication app)
    {
        var mis = app.MapGroup("/api/v1/mis").WithTags("Portal cliente")
            .RequireAuthorization(Politicas.Cliente).RequireRateLimiting("ApiGeneral");

        mis.MapGet("/pedidos", async (ClaimsPrincipal user, ServicioPedidos servicio, CancellationToken ct) =>
            Results.Ok(await servicio.DelClienteAsync(user.IdUsuario(), ct)));

        // La cotizacion (S06), solo si es de una solicitud suya y no esta eliminada. Va con su ETag.
        mis.MapGet("/cotizaciones/{id:int}", async (int id, ClaimsPrincipal user, HttpContext http, ServicioCotizaciones servicio, CancellationToken ct) =>
        {
            var ficha = await servicio.FichaAsync(id, user.IdUsuario(), ct);
            if (ficha is null)
                return Results.NotFound();
            http.Response.Headers.ETag = Concurrencia.ETag(ficha.RowVersion);
            return Results.Ok(ficha.ParaElCliente());
        });

        // Aprobar o rechazar (motivo opcional), con la version vista. La via es siempre «cliente_web».
        mis.MapPost("/cotizaciones/{id:int}/respuesta", async (int id, MiRespuestaCotizacionRequest req, ClaimsPrincipal user, HttpContext http,
            ServicioCotizaciones servicio, CancellationToken ct) =>
        {
            if (!Concurrencia.TryLeerIfMatch(http.Request, out var version))
                return CotizacionApi.IfMatchIlegible();
            var resultado = await servicio.ResponderAsync(id, QuienResponde.Cliente(user.IdUsuario()), req.Decision, req.Motivo,
                version ?? req.RowVersion, ct);
            return RespuestasCotizacion.Responder(resultado, http, r => Results.Ok(r.Ficha.ParaElCliente()));
        });
    }
}
