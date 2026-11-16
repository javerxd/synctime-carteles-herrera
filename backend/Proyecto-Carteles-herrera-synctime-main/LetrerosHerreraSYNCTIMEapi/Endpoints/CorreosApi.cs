using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Fase 11: la baja de los correos opcionales desde el enlace (sin sesion) y «Correos no enviados» (gestion).
public static class CorreosApi
{
    public static void MapCorreosApi(this WebApplication app)
    {
        var correos = app.MapGroup("/api/v1/correos").WithTags("Correos");

        // ── Baja (RFS08, RFU09). Un token invalido, alterado o viejo responde 404, sin decir de quien era. ──
        var baja = correos.MapGroup("/baja").AllowAnonymous().RequireRateLimiting("ApiGeneral");

        // Solo muestra: algunos programas de correo abren los enlaces solos, y abrirlo no debe dar de baja a nadie.
        baja.MapGet("/", async (string? t, HttpContext http, ServicioBajaCorreos servicio, CancellationToken ct) =>
        {
            SinReferer(http);
            return await servicio.VerAsync(t, ct) is { } estado ? Results.Ok(estado) : Results.NotFound();
        });

        // Sin campos apaga los dos; con un campo en false, solo ese. Repetirla da el mismo resultado.
        baja.MapPost("/", async (BajaCorreosRequest req, HttpContext http, ServicioBajaCorreos servicio, CancellationToken ct) =>
        {
            SinReferer(http);
            var ninguno = req.AvisosEstado is null && req.Marketing is null;
            return await servicio.BajaAsync(req.T, ninguno || req.AvisosEstado == false, ninguno || req.Marketing == false, ct) is { } preferencias
                ? Results.Ok(preferencias)
                : Results.NotFound();
        })
        .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        // RFC 8058: el programa de correo envia «List-Unsubscribe=One-Click» por POST a la direccion de la cabecera
        // List-Unsubscribe (con el token en la consulta). Apaga los dos y responde 200 sin cuerpo.
        baja.MapPost("/un-clic", async (HttpContext http, ServicioBajaCorreos servicio, CancellationToken ct) =>
        {
            SinReferer(http);
            if (!http.Request.HasFormContentType)
                return ErrorApi.Invalido("La baja con un clic se envía como formulario con List-Unsubscribe=One-Click.");
            var formulario = await http.Request.ReadFormAsync(ct);
            if (formulario["List-Unsubscribe"].ToString() != "One-Click")
                return ErrorApi.Invalido("La baja con un clic se envía como formulario con List-Unsubscribe=One-Click.");
            var token = http.Request.Query["t"].ToString() is { Length: > 0 } enConsulta ? enConsulta : formulario["t"].ToString();
            return await servicio.BajaAsync(token, apagarAvisos: true, apagarMarketing: true, ct) is null ? Results.NotFound() : Results.Ok();
        })
        .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        // ── «Correos no enviados» (decisiones 2 y 3): solo dueño y administrador ──
        var gestion = correos.MapGroup("/no-enviados").RequireAuthorization(Politicas.Gestion).RequireRateLimiting("ApiGeneral");

        gestion.MapGet("/", async (ServicioCorreosNoEnviados servicio, CancellationToken ct, int? pagina, int? tamano, string? tipo, DateOnly? desde,
            DateOnly? hasta, bool? revisado) =>
            Results.Ok(await servicio.ListarAsync(new FiltroNoEnviados(tipo?.Trim(), desde, hasta, revisado), new ParametrosListado(pagina, tamano), ct)));

        gestion.MapGet("/resumen", async (ServicioCorreosNoEnviados servicio, CancellationToken ct) => Results.Ok(await servicio.ResumenAsync(ct)));

        // 202: el correo nuevo queda en la cola. Una sola vez por fallo.
        gestion.MapPost("/{id:long}/reenviar", async (long id, ClaimsPrincipal user, ServicioCorreosNoEnviados servicio, CancellationToken ct) =>
            await servicio.ReenviarAsync(id, user.IdUsuario(), ct) switch
            {
                ResultadoReenvio.Encolado { IdCorreoNuevo: var nuevo } => Results.Accepted(value: new CorreoReenviado(nuevo)),
                ResultadoReenvio.YaReenviado { IdCorreoNuevo: var nuevo } => YaReenviado(nuevo),
                ResultadoReenvio.SinContenido => ErrorApi.Crear(StatusCodes.Status409Conflict, "sin_contenido",
                    "El contenido de este correo ya se borró por antigüedad: no se puede reenviar."),
                ResultadoReenvio.SinDestinos => ErrorApi.Crear(StatusCodes.Status409Conflict, "sin_destinos",
                    "Hoy ningún destino recibe este tipo de aviso. Revisa «Avisos del taller» en tu perfil."),
                ResultadoReenvio.ClienteNoRecibe => ErrorApi.Crear(StatusCodes.Status409Conflict, "cliente_no_recibe",
                    "El cliente ya no recibe estos avisos (se dio de baja o cambió su correo): no se reenvía."),
                _ => Results.NotFound()
            });

        gestion.MapPost("/{id:long}/revisado", async (long id, ServicioCorreosNoEnviados servicio, CancellationToken ct) =>
            await servicio.MarcarRevisadoAsync(id, ct) ? Results.NoContent() : Results.NotFound());
    }

    // El token viaja en la direccion: que no se filtre en la cabecera Referer de lo que se cargue despues.
    private static void SinReferer(HttpContext http) => http.Response.Headers["Referrer-Policy"] = "no-referrer";

    private static IResult YaReenviado(long idCorreoNuevo)
    {
        var problema = ErrorApi.Detalle(StatusCodes.Status409Conflict, "ya_reenviado", "Este correo ya se reenvió.");
        problema.Extensions["idCorreoNuevo"] = idCorreoNuevo;
        return Results.Problem(problema);
    }
}

public record BajaCorreosRequest(string? T, bool? AvisosEstado = null, bool? Marketing = null);
