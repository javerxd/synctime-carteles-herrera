using System.Globalization;
using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Fase 9 (pedido del usuario del 14-11-2026): a que correos llegan los avisos de solicitudes nuevas. Es
// configuracion del taller: solo el dueño y el administrador la ven y la cambian; el resto del personal y
// los clientes reciben 403 tanto al leer como al cambiar. Confirmar es publico: lo abre quien recibio el
// correo, sin sesion, con el mismo limite por IP que las rutas de cuenta.
public static class DestinosAvisosApi
{
    private const string MensajeVersion = "";
    private const string MensajePreferenciaObsoleta = "Otra persona cambió este destino mientras lo veías. Vuelve a cargarlo para ver los cambios.";

    public static void MapDestinosAvisosApi(this WebApplication app)
    {
        var avisos = app.MapGroup("/api/v1/avisos-taller").WithTags("Avisos del taller");
        var gestion = avisos.MapGroup("").RequireAuthorization(Politicas.Gestion).RequireRateLimiting("ApiGeneral");

        gestion.MapGet("/", async (ServicioDestinosAvisos servicio, CancellationToken ct) => Results.Ok(await servicio.EstadoAsync(ct)));

        // Agregar, reemplazar y quitar piden la contraseña de quien lo hace (revision de seguridad), con el
        // limite de las acciones delicadas.
        gestion.MapPost("/destinos", async (DestinoAvisoRequest req, ClaimsPrincipal user, HttpContext http, ServicioReautenticacion reautenticacion,
            ServicioDestinosAvisos servicio, CancellationToken ct) =>
        {
            if (!await reautenticacion.ConfirmarAsync(user.IdUsuario(), req.PasswordActual, ct))
                return RespuestasGestion.ReautenticacionFallida();
            return RespuestasGestion.Responder(await servicio.SolicitarAsync(user.IdUsuario(), req.Correo, req.IdReemplaza, ct), http,
                d => Results.Created($"/api/v1/avisos-taller/destinos/{d.IdDestinatario}", d), MensajeVersion);
        })
        .RequireRateLimiting("Reautenticacion")
        .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        // Fase 11 (decision 1): que solicitudes avisa el destino, con la version vista (If-Match o rowVersion).
        gestion.MapPut("/destinos/{id:int}/preferencia", async (int id, PreferenciaDestinoRequest req, ClaimsPrincipal user, HttpContext http,
            ServicioDestinosAvisos servicio, CancellationToken ct) =>
        {
            if (!RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version))
                return RespuestasGestion.IfMatchIlegible();
            return RespuestasGestion.Responder(await servicio.PreferenciaAsync(user.IdUsuario(), id, req.RecibeEnLinea, req.RecibePresenciales, version, ct),
                http, Results.Ok, MensajePreferenciaObsoleta);
        })
        .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        gestion.MapPost("/destinos/{id:int}/reenviar", async (int id, ClaimsPrincipal user, HttpContext http, ServicioDestinosAvisos servicio, CancellationToken ct) =>
            RespuestasGestion.Responder(await servicio.ReenviarAsync(user.IdUsuario(), id, ct), http, Results.Ok, MensajeVersion));

        gestion.MapPost("/destinos/{id:int}/cancelar", async (int id, ClaimsPrincipal user, HttpContext http, ServicioDestinosAvisos servicio, CancellationToken ct) =>
            RespuestasGestion.Responder(await servicio.CancelarAsync(user.IdUsuario(), id, ct), http, Results.Ok, MensajeVersion));

        gestion.MapDelete("/destinos/{id:int}", async (int id, [FromBody] ReautenticacionRequest req, ClaimsPrincipal user, HttpContext http,
            ServicioReautenticacion reautenticacion, ServicioDestinosAvisos servicio, CancellationToken ct) =>
        {
            if (!await reautenticacion.ConfirmarAsync(user.IdUsuario(), req.PasswordActual, ct))
                return RespuestasGestion.ReautenticacionFallida();
            return RespuestasGestion.Responder(await servicio.QuitarAsync(user.IdUsuario(), id, ct), http, _ => Results.NoContent(), MensajeVersion);
        })
        .RequireRateLimiting("Reautenticacion")
        .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        // 202: el aviso de ejemplo queda en la cola y la pantalla consulta su estado.
        gestion.MapPost("/prueba", async (ClaimsPrincipal user, HttpContext http, ServicioDestinosAvisos servicio, CancellationToken ct) =>
            RespuestasGestion.Responder(await servicio.ProbarAsync(user.IdUsuario(), ct), http, p => Results.Accepted(value: p), MensajeVersion));

        gestion.MapGet("/prueba/estado", async (ServicioDestinosAvisos servicio, CancellationToken ct, string? ids) =>
        {
            var lista = new List<long>();
            foreach (var parte in (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!long.TryParse(parte, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
                    return ErrorApi.Invalido("Los identificadores van separados por coma, por ejemplo ?ids=12,13.");
                lista.Add(id);
            }
            if (lista.Count is 0 or > ServicioDestinosAvisos.MaximoIdsEstado)
                return ErrorApi.Invalido($"Indica entre 1 y {ServicioDestinosAvisos.MaximoIdsEstado} correos (?ids=12,13).");
            return Results.Ok(await servicio.EstadoPruebaAsync(lista.Distinct().ToList(), ct));
        });

        avisos.MapPost("/confirmar", async (ConfirmarDestinoRequest req, HttpContext http, ServicioDestinosAvisos servicio, CancellationToken ct) =>
            RespuestasGestion.Responder(await servicio.ConfirmarAsync(req.Token, ct), http, Results.Ok, MensajeVersion))
            .AllowAnonymous()
            .RequireRateLimiting("Login")
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));
    }
}

public record DestinoAvisoRequest(string? Correo, int? IdReemplaza = null, string? PasswordActual = null);
public record ReautenticacionRequest(string? PasswordActual);
public record ConfirmarDestinoRequest(string? Token);
public record PreferenciaDestinoRequest(bool? RecibeEnLinea, bool? RecibePresenciales, byte[]? RowVersion = null);
