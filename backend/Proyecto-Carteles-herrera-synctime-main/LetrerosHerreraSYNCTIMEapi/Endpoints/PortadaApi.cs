using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Negocio.Consentimientos;
using LetrerosHerreraSYNCTIMEapi.Negocio.Portada;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Lo que la portada publica pide sin sesion (fase 5): estado del local, documentos legales, formulario
// de contacto y consentimiento de cookies. Los envios que guardan algo tienen limites propios por hora
// e IP ("Contacto" y "Cookies"), un tamaño maximo, y el formulario un campo trampa contra robots.
public static class PortadaApi
{
    // Holgado para el mensaje mas largo permitido (2000 caracteres); evita leer cuerpos enormes en memoria.
    private const long TamanoMaximoEnvio = 16 * 1024;

    private const string MensajeRecibida = "Recibimos tu consulta. Te responderemos a tu correo lo antes posible.";

    public static void MapPortadaApi(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/v1").WithTags("Portada").AllowAnonymous();

        grupo.MapGet("/local/estado", async (ServicioEstadoLocal servicio, CancellationToken ct) =>
        {
            var estado = await servicio.ObtenerAsync(ct);
            return Results.Ok(new
            {
                estado.AbiertoAhora,
                estado.Motivo,
                estado.DentroDeHorario,
                estado.HoraChile,
                Hoy = estado.Hoy is null ? null : Franja(estado.Hoy),
                estado.ProximaApertura,
                Cartel = estado.Cartel is null ? null : new
                {
                    estado.Cartel.AbiertoEnLocal,
                    estado.Cartel.MensajeEstado,
                    estado.Cartel.TiempoEstimadoRegreso,
                    estado.Cartel.FechaUltimaModificacion
                },
                Semana = estado.Semana.Select(Franja)
            });
        })
        .RequireRateLimiting("ApiGeneral");

        grupo.MapGet("/documentos-legales", () =>
            // Fase 11: avisos_estado es una preferencia del cliente, no un documento publicado: no se lista aqui.
            Results.Ok(DocumentosLegales.VersionesVigentes.Where(d => d.Key != DocumentosLegales.AvisosEstado).Select(d => new { Tipo = d.Key, Version = d.Value })))
            .RequireRateLimiting("ApiGeneral");

        grupo.MapPost("/contacto", async (ContactoRequest req, ServicioContacto servicio, ILogger<ContactoRequest> logger, CancellationToken ct) =>
        {
            // Campo trampa: invisible para las personas, los robots lo llenan. Reciben la misma respuesta
            // para que no aprendan a evitarlo, pero no se guarda ni se envia nada.
            if (!string.IsNullOrWhiteSpace(req.SitioWeb))
            {
                logger.LogInformation("Consulta de contacto descartada por el campo trampa.");
                return Results.Accepted(value: new { Recibida = true, Mensaje = MensajeRecibida });
            }

            var resultado = await servicio.RecibirAsync(
                new ConsultaContactoEntrada(req.Nombre, req.Correo, req.Telefono, req.Asunto, req.Mensaje, req.AceptaPrivacidad),
                req.VersionPrivacidad, ct);

            return resultado switch
            {
                ResultadoContacto.Invalida i => ErrorApi.Invalido("Revisa los datos del formulario.", i.Errores),
                ResultadoContacto.VersionVencida v => ErrorApi.Conflicto(v.Mensaje),
                _ => Results.Accepted(value: new { Recibida = true, Mensaje = MensajeRecibida })
            };
        })
        .WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoEnvio))
        .RequireRateLimiting("Contacto");

        // Registra la aceptacion de la analitica, o su retiro si el visitante la habia aceptado antes. Quien
        // nunca la acepto no deja datos: el navegador recuerda su eleccion.
        grupo.MapPost("/consentimientos/cookies", async (CookiesRequest req, IServicioConsentimientos consentimientos, CancellationToken ct) =>
        {
            if (req.Visitante == Guid.Empty)
                return ErrorApi.Invalido("Falta el identificador del visitante.");

            var titular = TitularConsentimiento.Envio($"cookies:{req.Visitante:N}");
            if (req.Acepta == false)
            {
                await consentimientos.RevocarAsync(titular, DocumentosLegales.Cookies, ct);
                return Results.NoContent();
            }

            var resultado = await consentimientos.RegistrarAsync(titular, DocumentosLegales.Cookies, req.Version ?? "", ct);
            return resultado.Error is null ? Results.NoContent() : ErrorApi.Conflicto(resultado.Error);
        })
        .WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoEnvio))
        .RequireRateLimiting("Cookies");
    }

    private static object Franja(Negocio.Local.FranjaHorario f) =>
        new { f.DiaSemana, f.NombreDia, f.Abierto, HoraInicio = f.Inicio, HoraFin = f.Fin };
}

public record ContactoRequest(
    string? Nombre, string? Correo, string? Telefono, string? Asunto, string? Mensaje,
    bool AceptaPrivacidad, string? VersionPrivacidad, string? SitioWeb);

// Acepta: true o ausente registra la aceptacion; false la retira.
public record CookiesRequest(Guid Visitante, string? Version, bool? Acepta = null);
