using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Los pedidos del cliente con sesion (se rehace en la fase 8). Sus solicitudes y adjuntos estan en
// MisSolicitudesApi (fase 7); la solicitud publica sin cuenta (/solicitudes/online) se elimino en la fase 7.
public static class PortalClienteApi
{
    public static void MapPortalClienteApi(this WebApplication app)
    {
        // ── Cliente con sesion: solo ve lo suyo; el id sale siempre del token ──
        var mis = app.MapGroup("/api/v1/mis").WithTags("Portal cliente")
            .RequireAuthorization(Politicas.Cliente).RequireRateLimiting("ApiGeneral");

        mis.MapGet("/pedidos", async (ClaimsPrincipal user, LetrerosHerreradbContext db, CancellationToken ct) =>
        {
            var idCliente = user.IdUsuario();
            return Results.Ok(await db.Pedidos.AsNoTracking()
                .Where(p => p.Solicitud!.IdCliente == idCliente)
                .OrderByDescending(p => p.FechaPedido)
                .Select(p => new
                {
                    p.IdPedido,
                    p.IdSolicitud,
                    p.FechaPedido,
                    p.FechaEntregaEstimada,
                    p.FechaEntregaReal,
                    p.ModalidadEntrega,
                    p.RequiereInstalacion,
                    p.DireccionInstalacion,
                    p.CostoInstalacionAdicional,
                    p.MontoBaseTrabajo,
                    p.MontoTotalFinal,
                    p.EstadoPedido,
                    p.EstadoPago,
                    p.MontoAbonado,
                    p.SaldoPendiente
                })
                .ToListAsync(ct));
        });
    }
}
