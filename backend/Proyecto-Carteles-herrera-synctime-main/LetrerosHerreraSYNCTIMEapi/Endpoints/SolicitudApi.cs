using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Repositories;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

public static class SolicitudApi
{
    private static readonly string[] EstadosValidos = ["solicitado", "cotizado", "aceptado", "rechazado"];

    public static void MapSolicitudApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/solicitudes").WithTags("Solicitudes")
            .RequireAuthorization(Politicas.Empleado).RequireRateLimiting("ApiGeneral");

        group.MapGet("/", async (ISolicitudRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetAllAsync(ct)));

        group.MapGet("/{id}", async (int id, ISolicitudRepository repo, CancellationToken ct) =>
        {
            var s = await repo.GetByIdAsync(id, ct);
            return s is null ? Results.NotFound() : Results.Ok(s);
        });

        group.MapPost("/", async (CrearSolicitudRequest req, ClaimsPrincipal user, ISolicitudRepository repo, CancellationToken ct) =>
        {
            var error = await SolicitudValidacion.ValidarAsync(req.DescripcionGeneral, req.Detalles, repo, ct);
            if (error is not null)
                return Results.BadRequest(new { error });

            if (!await repo.ClienteExistsAsync(req.IdCliente, ct))
                return Results.BadRequest(new { error = "Cliente no existe." });

            var solicitud = new Solicitud
            {
                IdCliente = req.IdCliente,
                // El origen presencial depende de este dato: se toma del token, nunca del cuerpo.
                IdEmpleadoRegistra = user.IdUsuario(),
                DescripcionGeneral = req.DescripcionGeneral,
                Detalles = SolicitudValidacion.ToDetalles(req.Detalles)
            };

            await repo.AddAsync(solicitud, ct);
            await repo.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/solicitudes/{solicitud.IdSolicitud}", solicitud);
        });

        // El estado no se edita aqui: solo cambia con PATCH /estado, reservado a gestion.
        group.MapPut("/{id}", async (int id, ActualizarSolicitudRequest req, ISolicitudRepository repo, CancellationToken ct) =>
        {
            var solicitud = await repo.GetByIdAsync(id, ct);
            if (solicitud is null) return Results.NotFound();

            var conflicto = Concurrencia.Verificar(req.RowVersion, solicitud.RowVersion);
            if (conflicto is not null) return conflicto;

            if (string.IsNullOrWhiteSpace(req.DescripcionGeneral) && solicitud.Detalles.Count == 0)
                return Results.BadRequest(new { error = "Indique una descripcion general: la solicitud no tiene detalles." });

            solicitud.DescripcionGeneral = req.DescripcionGeneral;

            await repo.SaveChangesAsync(ct);
            return Results.Ok(solicitud);
        });

        group.MapPatch("/{id}/estado", async (int id, CambiarEstadoRequest req, ISolicitudRepository repo, CancellationToken ct) =>
        {
            if (!EstadosValidos.Contains(req.Estado))
                return EstadoInvalido();

            var solicitud = await repo.GetByIdAsync(id, ct);
            if (solicitud is null) return Results.NotFound();

            var conflicto = Concurrencia.Verificar(req.RowVersion, solicitud.RowVersion);
            if (conflicto is not null) return conflicto;

            solicitud.Estado = req.Estado;
            // Al salir de "rechazado" el motivo deja de aplicar, y el cliente lo ve en su portal.
            solicitud.MotivoRechazo = req.Estado == "rechazado" ? req.MotivoRechazo : null;

            await repo.SaveChangesAsync(ct);
            return Results.Ok(solicitud);
        })
        .RequireAuthorization(Politicas.Gestion);

        // El borrado fisico queda solo para sp_depurar_solicitudes_no_concretadas:
        // asi no se pierde la metrica de conversion.
        group.MapDelete("/{id}", (int id, ISolicitudRepository repo, CancellationToken ct) =>
            EliminarLogicoAsync(id, repo, ct, _ => Results.NoContent()))
            .RequireAuthorization(Politicas.Gestion);

        group.MapPatch("/{id}/eliminar-logico", (int id, ISolicitudRepository repo, CancellationToken ct) =>
            EliminarLogicoAsync(id, repo, ct, s => Results.Ok(s)))
            .RequireAuthorization(Politicas.Gestion);

        // ── Metrica del Dashboard: conversion de solicitudes online en pedidos ──
        app.MapGet("/api/v1/metricas/conversion-online", async (LetrerosHerreradbContext db, CancellationToken ct) =>
            Results.Ok(await db.MetricasConversionOnline.AsNoTracking()
                .OrderByDescending(m => m.Anio).ThenByDescending(m => m.Mes)
                .ToListAsync(ct)))
            .WithTags("Metricas")
            .RequireAuthorization(Politicas.Gestion)
            .RequireRateLimiting("ApiGeneral");
    }

    private static IResult EstadoInvalido() =>
        Results.BadRequest(new { error = $"Estado inválido. Valores: {string.Join(", ", EstadosValidos)}" });

    private static async Task<IResult> EliminarLogicoAsync(int id, ISolicitudRepository repo, CancellationToken ct, Func<Solicitud, IResult> respuesta)
    {
        var solicitud = await repo.GetByIdAsync(id, ct);
        if (solicitud is null) return Results.NotFound();

        if (await repo.TienePedidoAsync(id, ct))
            return Results.Conflict(new { error = "La solicitud ya tiene un pedido y no se puede eliminar." });

        solicitud.EliminadoLogico = true;
        solicitud.FechaEliminacionLogica = DateTime.Now;
        await repo.SaveChangesAsync(ct);
        return respuesta(solicitud);
    }
}

// Reglas comunes a las solicitudes presenciales y online.
public static class SolicitudValidacion
{
    // El formulario es publico: el tope evita que una sola peticion dispare una consulta enorme.
    public const int MaxDetalles = 50;

    public static async Task<string?> ValidarAsync(string? descripcionGeneral, List<CrearSolicitudDetalleRequest>? detalles, ISolicitudRepository repo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(descripcionGeneral) && detalles is null or { Count: 0 })
            return "Indique una descripcion general o al menos un detalle.";

        if (detalles is null)
            return null;

        if (detalles.Count > MaxDetalles)
            return $"Una solicitud admite hasta {MaxDetalles} detalles.";

        foreach (var d in detalles)
        {
            if (string.IsNullOrWhiteSpace(d.Descripcion))
                return "Cada detalle requiere una descripcion.";
            if (d.Cantidad <= 0)
                return "La cantidad debe ser mayor que cero.";
            var sinMedidas = d.AnchoMm is null && d.AltoMm is null;
            var conMedidas = d.AnchoMm > 0 && d.AltoMm > 0;
            if (!sinMedidas && !conMedidas)
                return "Ancho y alto deben indicarse juntos y ser mayores que cero.";
        }

        return await repo.ProductosDisponiblesAsync(detalles.Select(d => d.IdProducto), ct)
            ? null
            : "Uno de los productos no existe o no esta disponible.";
    }

    public static List<SolicitudDetalle> ToDetalles(List<CrearSolicitudDetalleRequest>? detalles) =>
        detalles?.Select(d => new SolicitudDetalle
        {
            IdProducto = d.IdProducto,
            Descripcion = d.Descripcion,
            AnchoMm = d.AnchoMm,
            AltoMm = d.AltoMm,
            Cantidad = d.Cantidad
        }).ToList() ?? [];
}

public record CrearSolicitudRequest(int IdCliente, string? DescripcionGeneral, List<CrearSolicitudDetalleRequest>? Detalles);
public record CrearSolicitudDetalleRequest(int IdProducto, string Descripcion, int? AnchoMm, int? AltoMm, int Cantidad = 1);
public record ActualizarSolicitudRequest(string? DescripcionGeneral, byte[]? RowVersion);
public record CambiarEstadoRequest(string Estado, string? MotivoRechazo, byte[]? RowVersion);
