using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

public static class PedidoApi
{
    private static readonly string[] Modalidades = ["retiro_local", "entrega_domicilio", "instalacion_terreno"];
    private static readonly string[] Estados = ["aceptado", "en_proceso", "terminado", "entregado", "cancelado"];

    public static void MapPedidoApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/pedidos").WithTags("Pedidos")
            .RequireAuthorization(Politicas.Empleado).RequireRateLimiting("ApiGeneral");

        group.MapGet("/", async (LetrerosHerreradbContext db) =>
            Results.Ok(await db.Pedidos.AsNoTracking()
                .Include(p => p.Solicitud!).ThenInclude(s => s!.Cliente)
                .Include(p => p.EmpleadoResponsable)
                .ToListAsync()));

        group.MapGet("/{id}", async (int id, LetrerosHerreradbContext db) =>
        {
            var p = await db.Pedidos
                .Include(p => p.Solicitud!).ThenInclude(s => s!.Cliente)
                .Include(p => p.Solicitud!).ThenInclude(s => s!.Detalles)
                .Include(p => p.Cotizacion).ThenInclude(c => c!.Materiales)
                .Include(p => p.EmpleadoResponsable)
                .FirstOrDefaultAsync(p => p.IdPedido == id);
            return p is null ? Results.NotFound() : Results.Ok(p);
        });

        group.MapPost("/", async (CrearPedidoRequest req, LetrerosHerreradbContext db) =>
        {
            var solicitud = await db.Solicitudes.AsNoTracking()
                .FirstOrDefaultAsync(s => s.IdSolicitud == req.IdSolicitud && !s.EliminadoLogico);
            if (solicitud is null)
                return Results.BadRequest(new { error = "Solicitud no existe." });
            if (solicitud.Estado != "aceptado")
                return Results.BadRequest(new { error = "La solicitud debe estar aceptada para crear el pedido." });
            if (!await db.Empleados.AnyAsync(e => e.IdEmpleado == req.IdEmpleadoResponsable && e.Activo))
                return Results.BadRequest(new { error = "Empleado no existe." });
            if (await db.Pedidos.AnyAsync(p => p.IdSolicitud == req.IdSolicitud))
                return Results.Conflict(new { error = "Ya existe un pedido para esta solicitud." });
            if (!Modalidades.Contains(req.ModalidadEntrega))
                return ModalidadInvalida();

            Cotizacion? cotizacion = null;
            if (req.IdCotizacion is not null)
            {
                cotizacion = await db.Cotizaciones.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.IdCotizacion == req.IdCotizacion && c.IdSolicitud == req.IdSolicitud);
                if (cotizacion is null)
                    return Results.BadRequest(new { error = "La cotizacion no pertenece a esta solicitud." });
                if (cotizacion.Estado != "aprobada")
                    return Results.BadRequest(new { error = "La cotizacion debe estar aprobada para crear el pedido." });
            }

            var pedido = new Pedido
            {
                IdSolicitud = req.IdSolicitud,
                IdCotizacion = req.IdCotizacion,
                IdEmpleadoResponsable = req.IdEmpleadoResponsable,
                FechaEntregaEstimada = req.FechaEntregaEstimada,
                ModalidadEntrega = req.ModalidadEntrega,
                RequiereInstalacion = req.RequiereInstalacion,
                DireccionInstalacion = req.DireccionInstalacion,
                CostoInstalacionAdicional = req.CostoInstalacionAdicional,
                // Si no se indica un monto, se respeta el total de la cotizacion aprobada.
                MontoBaseTrabajo = req.MontoBaseTrabajo ?? cotizacion?.TotalCotizacion ?? 0m
            };

            db.Pedidos.Add(pedido);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/pedidos/{pedido.IdPedido}", pedido);
        })
        .RequireAuthorization(p => p.RequireRole("dueño", "administrador", "operario"));

        group.MapPut("/{id}", async (int id, ActualizarPedidoRequest req, LetrerosHerreradbContext db) =>
        {
            if (!Estados.Contains(req.Estado))
                return Results.BadRequest(new { error = $"Estado inválido. Valores: {string.Join(", ", Estados)}" });
            if (!Modalidades.Contains(req.ModalidadEntrega))
                return ModalidadInvalida();

            var pedido = await db.Pedidos.FindAsync(id);
            if (pedido is null) return Results.NotFound();

            var conflicto = Concurrencia.Verificar(req.RowVersion, pedido.RowVersion);
            if (conflicto is not null) return conflicto;

            pedido.Estado = req.Estado;
            pedido.FechaEntregaEstimada = req.FechaEntregaEstimada;
            pedido.FechaEntregaReal = req.FechaEntregaReal;
            pedido.ModalidadEntrega = req.ModalidadEntrega;
            pedido.RequiereInstalacion = req.RequiereInstalacion;
            pedido.DireccionInstalacion = req.DireccionInstalacion;
            pedido.CostoInstalacionAdicional = req.CostoInstalacionAdicional;
            pedido.MontoBaseTrabajo = req.MontoBaseTrabajo;

            await db.SaveChangesAsync();
            return Results.Ok(pedido);
        })
        .RequireAuthorization(p => p.RequireRole("dueño", "administrador", "operario"));

        group.MapDelete("/{id}", async (int id, LetrerosHerreradbContext db) =>
        {
            var p = await db.Pedidos.FindAsync(id);
            if (p is null) return Results.NotFound();
            db.Pedidos.Remove(p);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireAuthorization(p => p.RequireRole("dueño", "administrador"));
    }

    private static IResult ModalidadInvalida() =>
        Results.BadRequest(new { error = $"Modalidad inválida. Valores: {string.Join(", ", Modalidades)}" });
}

public record CrearPedidoRequest(int IdSolicitud, int? IdCotizacion, int IdEmpleadoResponsable, DateTime FechaEntregaEstimada, string ModalidadEntrega = "retiro_local", bool RequiereInstalacion = false, string? DireccionInstalacion = null, decimal CostoInstalacionAdicional = 0m, decimal? MontoBaseTrabajo = null);
public record ActualizarPedidoRequest(string Estado, DateTime FechaEntregaEstimada, DateTime? FechaEntregaReal, string ModalidadEntrega, bool RequiereInstalacion, string? DireccionInstalacion, decimal CostoInstalacionAdicional, decimal MontoBaseTrabajo, byte[]? RowVersion);
