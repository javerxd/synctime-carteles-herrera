using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Middleware;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Solicitudes vistas por el personal (fase 7): bandeja, ficha, registro presencial, edicion con
// If-Match, cambios de estado (gestion) y adjuntos. Sin consultas directas a la base: todo pasa por
// ServicioSolicitudes y ServicioAdjuntos.
public static class SolicitudApi
{
    public static void MapSolicitudApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/solicitudes").WithTags("Solicitudes")
            .RequireAuthorization(Politicas.Empleado).RequireRateLimiting("ApiGeneral");

        group.MapGet("/", async (ServicioSolicitudes servicio, CancellationToken ct, string? estado, string? origen, DateOnly? desde, DateOnly? hasta,
            string? q, string? orden, int? pagina, int? tamano) =>
        {
            var errores = ValidacionSolicitud.ValidarFiltro(estado, origen, orden);
            if (errores.Count > 0)
                return ErrorApi.Invalido(ValidacionSolicitud.Resumen(errores), errores);
            return Results.Ok(await servicio.ListarAsync(new FiltroSolicitudes(estado, origen, desde, hasta, orden),
                new ParametrosListado(pagina, tamano, orden, q), ct));
        });

        group.MapGet("/{id}", async (int id, HttpContext http, ServicioSolicitudes servicio, CancellationToken ct) =>
        {
            var ficha = await servicio.FichaAsync(id, ct);
            if (ficha is null)
                return Results.NotFound();
            http.Response.Headers.ETag = Concurrencia.ETag(ficha.RowVersion);
            return Results.Ok(ficha);
        });

        // Registro presencial: el origen depende de quien la registra, que sale del token y nunca del cuerpo.
        group.MapPost("/", async (CrearSolicitudRequest req, ClaimsPrincipal user, HttpContext http, ServicioSolicitudes servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.CrearPresencialAsync(user.IdUsuario(), req.IdCliente,
                new EntradaSolicitud(req.DescripcionGeneral, RespuestasSolicitud.Detalles(req.Detalles), null), ct);
            return RespuestasSolicitud.Responder(resultado, http, r => Results.Created($"/api/v1/solicitudes/{r.Ficha.IdSolicitud}", r.Ficha));
        })
        .ConIdempotencia("POST /solicitudes");

        // El estado no se edita aqui: solo cambia con PATCH /estado, reservado a gestion.
        group.MapPut("/{id}", async (int id, ActualizarSolicitudRequest req, ClaimsPrincipal user, HttpContext http, ServicioSolicitudes servicio, CancellationToken ct) =>
        {
            if (!Concurrencia.TryLeerIfMatch(http.Request, out var version))
                return ErrorApi.Invalido("La cabecera If-Match no es una versión válida. Vuelve a cargar la solicitud.");
            var resultado = await servicio.EditarAsync(id, user.IdUsuario(), req.DescripcionGeneral, RespuestasSolicitud.DetallesONull(req.Detalles),
                version ?? req.RowVersion, ct);
            return RespuestasSolicitud.Responder(resultado, http, r => Results.Ok(r.Ficha));
        });

        group.MapPatch("/{id}/estado", async (int id, CambiarEstadoRequest req, ClaimsPrincipal user, HttpContext http, ServicioSolicitudes servicio, CancellationToken ct) =>
        {
            if (!Concurrencia.TryLeerIfMatch(http.Request, out var version))
                return ErrorApi.Invalido("La cabecera If-Match no es una versión válida. Vuelve a cargar la solicitud.");
            var resultado = await servicio.CambiarEstadoAsync(id, user.IdUsuario(), req.Estado, req.Motivo, version ?? req.RowVersion, ct);
            return RespuestasSolicitud.Responder(resultado, http, r => Results.Ok(r.Ficha));
        });

        // El borrado fisico queda solo para sp_depurar_solicitudes_no_concretadas:
        // asi no se pierde la metrica de conversion.
        // Las dos exigen la version vigente en If-Match. RFU11: el borrado logico es de todo el personal;
        // DELETE queda solo para gestion.
        group.MapDelete("/{id}", async (int id, HttpContext http, ServicioSolicitudes servicio, CancellationToken ct) =>
        {
            if (!Concurrencia.TryLeerIfMatch(http.Request, out var version))
                return ErrorApi.Invalido("La cabecera If-Match no es una versión válida. Vuelve a cargar la solicitud.");
            return RespuestasSolicitud.Responder(await servicio.EliminarLogicoAsync(id, version, ct), http, _ => Results.NoContent());
        })
        .RequireAuthorization(Politicas.Gestion);

        group.MapPatch("/{id}/eliminar-logico", async (int id, HttpContext http, ServicioSolicitudes servicio, CancellationToken ct) =>
        {
            if (!Concurrencia.TryLeerIfMatch(http.Request, out var version))
                return ErrorApi.Invalido("La cabecera If-Match no es una versión válida. Vuelve a cargar la solicitud.");
            return RespuestasSolicitud.Responder(await servicio.EliminarLogicoAsync(id, version, ct), http,
                _ => Results.Ok(new { IdSolicitud = id, EliminadoLogico = true }));
        });

        // ── Adjuntos que agrega el personal: se vinculan de inmediato, con el mismo tope de 5 ──
        // La API usa tokens Bearer, no cookies: la proteccion antifalsificacion de formularios no aplica.
        group.MapPost("/{id}/adjuntos", async (int id, HttpContext http, ClaimsPrincipal user, ServicioAdjuntos adjuntos, CancellationToken ct) =>
        {
            var leido = await ArchivoSubido.LeerAsync(http, ct);
            if (leido.Error is not null)
                return leido.Error;
            return ArchivoSubido.Responder(await adjuntos.SubirASolicitudAsync(id, user.IdUsuario(), leido.Datos!, leido.Nombre, ct));
        })
        .DisableAntiforgery()
        .RequireRateLimiting(ArchivoSubido.PoliticaDeLimite);

        group.MapDelete("/{id}/adjuntos/{idAdjunto}", async (int id, int idAdjunto, ClaimsPrincipal user, ServicioAdjuntos adjuntos, CancellationToken ct) =>
            ArchivoSubido.Responder(await adjuntos.BorrarDeSolicitudAsync(id, idAdjunto, user.IdUsuario(), ct)));

        // ── Metrica del Dashboard: conversion de solicitudes online en pedidos ──
        app.MapGet("/api/v1/metricas/conversion-online", async (ServicioSolicitudes servicio, CancellationToken ct) =>
            Results.Ok(await servicio.MetricasConversionAsync(ct)))
            .WithTags("Metricas")
            .RequireAuthorization(Politicas.Gestion)
            .RequireRateLimiting("ApiGeneral");
    }
}

// Traduce el resultado del servicio a la respuesta HTTP, igual para el cliente y para el personal.
public static class RespuestasSolicitud
{
    public static IResult Responder(ResultadoSolicitud resultado, HttpContext http, Func<ResultadoSolicitud.Correcta, IResult> correcta)
    {
        switch (resultado)
        {
            case ResultadoSolicitud.Correcta ok:
                http.Response.Headers.ETag = Concurrencia.ETag(ok.Comprobante.RowVersion);
                return correcta(ok);
            case ResultadoSolicitud.Eliminada:
                return correcta(null!);
            case ResultadoSolicitud.Invalida { Errores: var errores }:
                return ErrorApi.Invalido(ValidacionSolicitud.Resumen(errores), errores);
            case ResultadoSolicitud.CorreoSinConfirmar:
                return ErrorApi.Crear(StatusCodes.Status403Forbidden, "correo_sin_confirmar", ServicioSolicitudes.MensajeCorreoSinConfirmar);
            case ResultadoSolicitud.ContactoDirecto:
                return ErrorApi.Crear(StatusCodes.Status409Conflict, ReglaContraSolicitudesFalsas.Codigo, ReglaContraSolicitudesFalsas.Mensaje);
            case ResultadoSolicitud.Conflicto { Mensaje: var mensaje }:
                return ErrorApi.Crear(StatusCodes.Status409Conflict, "transicion_invalida", mensaje);
            case ResultadoSolicitud.SinVersion:
                return ErrorApi.Crear(StatusCodes.Status400BadRequest, "sin_version", "Indica la versión que viste (cabecera If-Match con el ETag, o rowVersion).");
            case ResultadoSolicitud.VersionObsoleta:
                return ErrorApi.Crear(StatusCodes.Status409Conflict, Concurrencia.CodigoVersionObsoleta, ServicioSolicitudes.MensajeVersionObsoleta);
            default:
                return Results.NotFound();
        }
    }

    public static IReadOnlyList<EntradaDetalle> Detalles(List<CrearSolicitudDetalleRequest>? detalles) => DetallesONull(detalles) ?? [];

    public static IReadOnlyList<EntradaDetalle>? DetallesONull(List<CrearSolicitudDetalleRequest>? detalles) =>
        detalles?.Select(d => new EntradaDetalle(d.IdProducto, d.Descripcion, d.AnchoMm, d.AltoMm, d.Cantidad, d.Opciones)).ToList();
}

public record CrearSolicitudRequest(int IdCliente, string? DescripcionGeneral, List<CrearSolicitudDetalleRequest>? Detalles);
public record CrearSolicitudDetalleRequest(int IdProducto, string? Descripcion, int? AnchoMm, int? AltoMm, int Cantidad = 1, Dictionary<string, string>? Opciones = null);
public record ActualizarSolicitudRequest(string? DescripcionGeneral, List<CrearSolicitudDetalleRequest>? Detalles, byte[]? RowVersion);
public record CambiarEstadoRequest(string? Estado, string? Motivo, byte[]? RowVersion);
