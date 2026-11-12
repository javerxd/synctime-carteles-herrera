using LetrerosHerreraSYNCTIMEapi.Api;
using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Repositories;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Lo que un cliente puede hacer sin pasar por el personal del local.
// id_empleado_registra queda NULL, y eso es lo que marca la solicitud como online.
public static class PortalClienteApi
{
    public static void MapPortalClienteApi(this WebApplication app)
    {
        // ── Solicitud rapida, sin cuenta ──
        // Solo confirma la recepcion: no devuelve datos del cliente para no exponerlos conociendo un telefono.
        app.MapPost("/api/v1/solicitudes/online", async (CrearSolicitudOnlineRequest req, LetrerosHerreradbContext db, ISolicitudRepository repo, CancellationToken ct) =>
        {
            var contacto = req.Cliente;
            if (contacto is null || string.IsNullOrWhiteSpace(contacto.Nombre) || string.IsNullOrWhiteSpace(contacto.Apellido) || string.IsNullOrWhiteSpace(contacto.Telefono))
                return ErrorApi.Invalido("Nombre, apellido y telefono son requeridos.");

            var error = await SolicitudValidacion.ValidarAsync(req.DescripcionGeneral, req.Detalles, repo, ct);
            if (error is not null)
                return ErrorApi.Invalido(error);

            var telefono = contacto.Telefono.Trim();
            var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Telefono == telefono, ct);

            // Los telefonos no se verifican: quien registro este numero veria en su cuenta los datos
            // de cualquiera que lo escriba aqui. El titular envia la solicitud despues de iniciar sesion.
            if (cliente?.PasswordHash is not null)
                return ErrorApi.Conflicto("Ese telefono tiene una cuenta. Inicie sesion para enviar su solicitud.");

            if (cliente is null)
            {
                var emailDisponible = !string.IsNullOrWhiteSpace(contacto.Email)
                    && !await db.Clientes.AnyAsync(c => c.Email == contacto.Email, ct);
                cliente = new Cliente
                {
                    Nombre = contacto.Nombre.Trim(),
                    Apellido = contacto.Apellido.Trim(),
                    Telefono = telefono,
                    Email = emailDisponible ? contacto.Email : null,
                    Direccion = contacto.Direccion
                };
            }

            var solicitud = new Solicitud
            {
                Cliente = cliente,
                DescripcionGeneral = req.DescripcionGeneral,
                Detalles = SolicitudValidacion.ToDetalles(req.Detalles)
            };
            await repo.AddAsync(solicitud, ct);
            await repo.SaveChangesAsync(ct);

            return Results.Created($"/api/v1/solicitudes/online/{solicitud.IdSolicitud}",
                new { solicitud.IdSolicitud, solicitud.Origen, solicitud.Estado, solicitud.FechaSolicitud });
        })
        .WithTags("Portal cliente")
        .AllowAnonymous()
        .RequireRateLimiting("Publico");

        // ── Cliente con sesion: solo ve y crea lo suyo; el id sale siempre del token ──
        var mis = app.MapGroup("/api/v1/mis").WithTags("Portal cliente")
            .RequireAuthorization(Politicas.Cliente).RequireRateLimiting("ApiGeneral");

        mis.MapGet("/solicitudes", async (ClaimsPrincipal user, LetrerosHerreradbContext db, CancellationToken ct) =>
        {
            var idCliente = user.IdUsuario();
            return Results.Ok(await db.Solicitudes.AsNoTracking()
                .Where(s => s.IdCliente == idCliente && !s.EliminadoLogico)
                .OrderByDescending(s => s.FechaSolicitud)
                .Select(s => new
                {
                    s.IdSolicitud,
                    s.Origen,
                    s.DescripcionGeneral,
                    s.FechaSolicitud,
                    s.Estado,
                    s.MotivoRechazo,
                    Detalles = s.Detalles.Select(d => new
                    {
                        d.IdSolicitudDetalle,
                        d.IdProducto,
                        Producto = d.Producto!.Nombre,
                        d.Descripcion,
                        d.AnchoMm,
                        d.AltoMm,
                        d.Cantidad,
                        d.AreaM2
                    })
                })
                .ToListAsync(ct));
        });

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
                    p.Estado
                })
                .ToListAsync(ct));
        });

        mis.MapPost("/solicitudes", async (CrearMiSolicitudRequest req, ClaimsPrincipal user, ISolicitudRepository repo, CancellationToken ct) =>
        {
            var error = await SolicitudValidacion.ValidarAsync(req.DescripcionGeneral, req.Detalles, repo, ct);
            if (error is not null)
                return ErrorApi.Invalido(error);

            var solicitud = new Solicitud
            {
                IdCliente = user.IdUsuario(),
                DescripcionGeneral = req.DescripcionGeneral,
                Detalles = SolicitudValidacion.ToDetalles(req.Detalles)
            };
            await repo.AddAsync(solicitud, ct);
            await repo.SaveChangesAsync(ct);

            return Results.Created("/api/v1/mis/solicitudes",
                new { solicitud.IdSolicitud, solicitud.Origen, solicitud.Estado, solicitud.FechaSolicitud });
        });
    }
}

public record ContactoClienteRequest(string Nombre, string Apellido, string Telefono, string? Email, string? Direccion);
public record CrearSolicitudOnlineRequest(ContactoClienteRequest? Cliente, string? DescripcionGeneral, List<CrearSolicitudDetalleRequest>? Detalles);
public record CrearMiSolicitudRequest(string? DescripcionGeneral, List<CrearSolicitudDetalleRequest>? Detalles);
