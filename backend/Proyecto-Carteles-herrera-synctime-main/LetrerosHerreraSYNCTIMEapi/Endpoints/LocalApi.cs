using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using LetrerosHerreraSYNCTIMEapi.Negocio.Mantenedores;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Fase 9: estado del local, horario semanal y disponibilidad del personal (RFS02, RNFO03, RFU16, RFU20,
// E10, E11). El cartel lo da vuelta todo el personal (decision 1); el horario lo cambia gestion; cada
// empleado registra sus franjas, y la portada ve su union sin nombres.
public static class LocalApi
{
    private const string MensajeHorarioObsoleto = "Otra persona cambió el horario mientras lo veías. Vuelve a cargarlo para ver los cambios.";
    private const string MensajeCartelObsoleto = "Otra persona cambió el cartel mientras lo veías. Vuelve a cargarlo para ver cómo quedó.";

    public static void MapLocalApi(this WebApplication app)
    {
        // Fase 11: la respuesta sigue siendo la lista de los 7 dias; cada dia trae rowVersion (la del horario completo) y
        // la misma va en el ETag. Guardar exige esa version (If-Match o rowVersion en el cuerpo).
        app.MapGet("/api/v1/horarios", async (HttpContext http, ServicioLocal servicio, CancellationToken ct) =>
        {
            var horario = await servicio.HorarioAsync(ct);
            http.Response.Headers.ETag = Concurrencia.ETag(horario.RowVersion);
            return Results.Ok(horario.Dias);
        })
            .WithTags("Horarios")
            .AllowAnonymous()
            .RequireRateLimiting("ApiGeneral");

        app.MapPut("/api/v1/horarios", async (HorarioRequest req, ClaimsPrincipal user, HttpContext http, ServicioLocal servicio, CancellationToken ct) =>
        {
            if (!RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version))
                return RespuestasGestion.IfMatchIlegible();
            var resultado = await servicio.GuardarHorarioAsync(user.IdUsuario(), req.Dias?.Select(d => d?.Entrada()).ToList(), version, ct);
            return RespuestasGestion.Responder(resultado, http, h =>
            {
                http.Response.Headers.ETag = Concurrencia.ETag(h.RowVersion);
                return Results.Ok(h.Dias);
            }, MensajeHorarioObsoleto);
        })
            .WithTags("Horarios")
            .RequireAuthorization(Politicas.Gestion)
            .RequireRateLimiting("ApiGeneral");

        app.MapGet("/api/v1/cartel-digital", async (HttpContext http, ServicioLocal servicio, CancellationToken ct) =>
            // Ruta publica: no expone que empleado hizo el ultimo cambio.
            RespuestasGestion.ConETag(await servicio.CartelAsync(ct), http))
        .WithTags("CartelDigital")
        .AllowAnonymous()
        .RequireRateLimiting("ApiGeneral");

        app.MapPut("/api/v1/cartel-digital", async (CartelRequest req, ClaimsPrincipal user, HttpContext http, ServicioLocal servicio, CancellationToken ct) =>
        {
            if (req.AbiertoEnLocal is not bool abierto)
                return Api.ErrorApi.Invalido("Indica si hay alguien en el local (abiertoEnLocal: true o false).",
                    new Dictionary<string, string[]> { ["abiertoEnLocal"] = ["Indica true o false."] });
            if (!RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version))
                return RespuestasGestion.IfMatchIlegible();
            return RespuestasGestion.Responder(await servicio.ActualizarCartelAsync(user.IdUsuario(),
                new EntradaCartel(abierto, req.MensajeEstado, req.TiempoEstimadoRegreso), version, ct), http, Results.Ok, MensajeCartelObsoleto);
        })
            .WithTags("CartelDigital")
            .RequireAuthorization(Politicas.Empleado)
            .RequireRateLimiting("ApiGeneral");

        app.MapGet("/api/v1/mi-disponibilidad", async (ClaimsPrincipal user, ServicioLocal servicio, CancellationToken ct) =>
            Results.Ok(await servicio.MiDisponibilidadAsync(user.IdUsuario(), ct)))
            .WithTags("Disponibilidad")
            .RequireAuthorization(Politicas.Empleado)
            .RequireRateLimiting("ApiGeneral");

        app.MapPut("/api/v1/mi-disponibilidad", async (DisponibilidadRequest req, ClaimsPrincipal user, HttpContext http, ServicioLocal servicio,
            CancellationToken ct) =>
            RespuestasGestion.Responder(await servicio.GuardarMiDisponibilidadAsync(user.IdUsuario(), req.Franjas?.Select(f => f?.Entrada()).ToList(), ct),
                http, Results.Ok, ""))
            .WithTags("Disponibilidad")
            .RequireAuthorization(Politicas.Empleado)
            .RequireRateLimiting("ApiGeneral");

        app.MapGet("/api/v1/disponibilidad", async (ServicioLocal servicio, CancellationToken ct) =>
            Results.Ok(await servicio.DisponibilidadEquipoAsync(ct)))
            .WithTags("Disponibilidad")
            .RequireAuthorization(Politicas.Gestion)
            .RequireRateLimiting("ApiGeneral");

        // Portada: en que franjas hay alguien atendiendo cada dia, sin nombres ni identificadores.
        app.MapGet("/api/v1/local/atencion", async (ServicioLocal servicio, CancellationToken ct) => Results.Ok(await servicio.AtencionAsync(ct)))
            .WithTags("Portada")
            .AllowAnonymous()
            .RequireRateLimiting("ApiGeneral");
    }
}

public record DiaHorarioRequest(int DiaSemana, bool Abierto, string? HoraInicio = null, string? HoraFin = null)
{
    public EntradaDiaHorario Entrada() => new(DiaSemana, Abierto, HoraInicio, HoraFin);
}

public record HorarioRequest(List<DiaHorarioRequest?>? Dias, byte[]? RowVersion = null);

public record CartelRequest(bool? AbiertoEnLocal, string? MensajeEstado, string? TiempoEstimadoRegreso, byte[]? RowVersion = null);

public record FranjaRequest(int DiaSemana, string? HoraInicio, string? HoraFin)
{
    public EntradaFranja Entrada() => new(DiaSemana, HoraInicio, HoraFin);
}

public record DisponibilidadRequest(List<FranjaRequest?>? Franjas);
