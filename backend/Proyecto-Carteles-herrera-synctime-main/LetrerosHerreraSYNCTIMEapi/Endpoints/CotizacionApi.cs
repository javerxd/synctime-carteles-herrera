using LetrerosHerreraSYNCTIMEapi.Api;
using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

public static class CotizacionApi
{
    private static readonly string[] EstadosValidos = ["pendiente", "aprobada", "rechazada"];

    public static void MapCotizacionApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/cotizaciones").WithTags("Cotizaciones")
            .RequireAuthorization(Politicas.Empleado).RequireRateLimiting("ApiGeneral");

        group.MapGet("/", async (LetrerosHerreradbContext db) =>
            Results.Ok(await db.Cotizaciones.AsNoTracking()
                .Include(c => c.Materiales)
                .Include(c => c.Solicitud)
                .Include(c => c.Empleado)
                .ToListAsync()));

        group.MapGet("/{id}", async (int id, LetrerosHerreradbContext db) =>
        {
            var cot = await db.Cotizaciones
                .Include(c => c.Materiales).ThenInclude(m => m.Material)
                .Include(c => c.Solicitud)
                .Include(c => c.Empleado)
                .FirstOrDefaultAsync(c => c.IdCotizacion == id);
            return cot is null ? Results.NotFound() : Results.Ok(cot);
        });

        group.MapPost("/", async (CrearCotizacionRequest req, ClaimsPrincipal user, LetrerosHerreradbContext db) =>
        {
            if (!await db.Solicitudes.AnyAsync(s => s.IdSolicitud == req.IdSolicitud && !s.EliminadoLogico))
                return ErrorApi.Invalido("Solicitud no existe.");

            var error = await ValidarCostosAsync(req.CostoManoObra, req.OtrosCostos, req.Materiales, db);
            if (error is not null)
                return ErrorApi.Invalido(error);

            var cotizacion = new Cotizacion
            {
                IdSolicitud = req.IdSolicitud,
                IdEmpleado = user.IdUsuario(),
                Observaciones = req.Observaciones,
                CostoManoObra = req.CostoManoObra,
                OtrosCostos = req.OtrosCostos,
                Materiales = ToMateriales(req.Materiales)
            };

            db.Cotizaciones.Add(cotizacion);
            await db.SaveChangesAsync();
            // El trigger recalcula costo_materiales_total en la base: se relee para no responder un total viejo.
            await db.Entry(cotizacion).ReloadAsync();
            return Results.Created($"/api/v1/cotizaciones/{cotizacion.IdCotizacion}", cotizacion);
        })
        .RequireAuthorization(p => p.RequireRole("dueño", "administrador", "diseñador"));

        group.MapPut("/{id}", async (int id, ActualizarCotizacionRequest req, LetrerosHerreradbContext db) =>
        {
            if (!EstadosValidos.Contains(req.Estado))
                return ErrorApi.Invalido($"Estado inválido. Valores: {string.Join(", ", EstadosValidos)}");

            var error = await ValidarCostosAsync(req.CostoManoObra, req.OtrosCostos, req.Materiales, db);
            if (error is not null)
                return ErrorApi.Invalido(error);

            var cot = await db.Cotizaciones.Include(c => c.Materiales).FirstOrDefaultAsync(c => c.IdCotizacion == id);
            if (cot is null) return Results.NotFound();

            cot.Observaciones = req.Observaciones;
            cot.CostoManoObra = req.CostoManoObra;
            cot.OtrosCostos = req.OtrosCostos;
            cot.Estado = req.Estado;

            // Sin la lista de materiales se conservan los actuales; una lista vacia los quita.
            if (req.Materiales is not null)
            {
                cot.Materiales.Clear();
                foreach (var material in ToMateriales(req.Materiales))
                    cot.Materiales.Add(material);
            }

            await db.SaveChangesAsync();
            await db.Entry(cot).ReloadAsync();
            return Results.Ok(cot);
        })
        .RequireAuthorization(p => p.RequireRole("dueño", "administrador", "diseñador"));

        group.MapDelete("/{id}", async (int id, LetrerosHerreradbContext db) =>
        {
            var cot = await db.Cotizaciones.FindAsync(id);
            if (cot is null) return Results.NotFound();
            db.Cotizaciones.Remove(cot);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireAuthorization(p => p.RequireRole("dueño", "administrador"));
    }

    private static async Task<string?> ValidarCostosAsync(decimal manoObra, decimal otrosCostos, List<CotizacionMaterialRequest>? materiales, LetrerosHerreradbContext db)
    {
        if (manoObra < 0 || otrosCostos < 0)
            return "Los costos no pueden ser negativos.";
        if (materiales is null or { Count: 0 })
            return null;
        if (materiales.Any(m => m.Cantidad <= 0 || m.CostoUnitario < 0))
            return "Cada material requiere cantidad mayor que cero y costo no negativo.";

        var ids = materiales.Select(m => m.IdMaterial).Distinct().ToList();
        return await db.Materiales.CountAsync(m => ids.Contains(m.IdMaterial)) == ids.Count
            ? null
            : "Uno de los materiales no existe.";
    }

    private static List<CotizacionMaterial> ToMateriales(List<CotizacionMaterialRequest>? materiales) =>
        materiales?.Select(m => new CotizacionMaterial
        {
            IdMaterial = m.IdMaterial,
            MedidaAplicada = m.MedidaAplicada,
            Cantidad = m.Cantidad,
            CostoUnitario = m.CostoUnitario
        }).ToList() ?? [];
}

public record CrearCotizacionRequest(int IdSolicitud, string? Observaciones, decimal CostoManoObra, decimal OtrosCostos, List<CotizacionMaterialRequest>? Materiales);
public record ActualizarCotizacionRequest(string? Observaciones, decimal CostoManoObra, decimal OtrosCostos, string Estado, List<CotizacionMaterialRequest>? Materiales);
public record CotizacionMaterialRequest(int IdMaterial, string? MedidaAplicada, decimal Cantidad, decimal CostoUnitario);
