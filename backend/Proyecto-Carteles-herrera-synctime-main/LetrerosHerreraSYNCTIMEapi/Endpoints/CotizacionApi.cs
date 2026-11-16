using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Middleware;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Cotizaciones;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Cotizaciones vistas por el personal (fase 8): todo el personal cotiza y registra la respuesta del cliente
// (RFU12, RFU08). Editar, responder y eliminar exigen la version vista (If-Match). Sin consultas directas a la
// base: todo pasa por ServicioCotizaciones. Las antiguas POST /cotizaciones y DELETE fisico se retiraron.
public static class CotizacionApi
{
    public static void MapCotizacionApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/cotizaciones").WithTags("Cotizaciones")
            .RequireAuthorization(Politicas.Empleado).RequireRateLimiting("ApiGeneral");

        group.MapGet("/", async (ServicioCotizaciones servicio, CancellationToken ct, string? estado, DateOnly? desde, DateOnly? hasta,
            int? idCliente, string? q, string? orden, int? pagina, int? tamano) =>
        {
            var errores = ValidacionCotizacion.ValidarFiltro(estado, orden);
            if (errores.Count > 0)
                return ErrorApi.Invalido(ValidacionCotizacion.Resumen(errores), errores);
            return Results.Ok(await servicio.ListarAsync(new FiltroCotizaciones(estado, desde, hasta, idCliente, orden),
                new ParametrosListado(pagina, tamano, orden, q), ct));
        });

        group.MapGet("/{id:int}", async (int id, HttpContext http, ServicioCotizaciones servicio, CancellationToken ct) =>
        {
            var ficha = await servicio.FichaAsync(id, idClienteDueno: null, ct);
            if (ficha is null)
                return Results.NotFound();
            http.Response.Headers.ETag = Concurrencia.ETag(ficha.RowVersion);
            return Results.Ok(ficha);
        });

        group.MapPut("/{id:int}", async (int id, CotizacionRequest req, HttpContext http, ServicioCotizaciones servicio, CancellationToken ct) =>
        {
            if (!Concurrencia.TryLeerIfMatch(http.Request, out var version))
                return IfMatchIlegible();
            return RespuestasCotizacion.Responder(await servicio.EditarAsync(id, req.Entrada(), version ?? req.RowVersion, ct), http,
                r => Results.Ok(r.Ficha));
        });

        // El cliente confirmo por telefono, WhatsApp o en el local: queda quien la marco y por que via.
        group.MapPost("/{id:int}/respuesta", async (int id, RespuestaCotizacionRequest req, ClaimsPrincipal user, HttpContext http,
            ServicioCotizaciones servicio, CancellationToken ct) =>
        {
            if (!Concurrencia.TryLeerIfMatch(http.Request, out var version))
                return IfMatchIlegible();
            var resultado = await servicio.ResponderAsync(id, QuienResponde.Personal(user.IdUsuario(), req.Via), req.Decision, req.Motivo,
                version ?? req.RowVersion, ct);
            return RespuestasCotizacion.Responder(resultado, http, r => Results.Ok(r.Ficha));
        });

        // Borrado logico: solo una «pendiente» o «rechazada» sin pedido.
        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, HttpContext http, ServicioCotizaciones servicio, CancellationToken ct) =>
        {
            if (!Concurrencia.TryLeerIfMatch(http.Request, out var version))
                return IfMatchIlegible();
            return RespuestasCotizacion.Responder(await servicio.EliminarAsync(id, user.IdUsuario(), version, ct), http, _ => Results.NoContent());
        })
        .RequireAuthorization(Politicas.Gestion);

        // Emitir: quien la elabora sale del token. Responde 201 con la cotizacion y su ETag.
        app.MapPost("/api/v1/solicitudes/{id:int}/cotizaciones", async (int id, CotizacionRequest req, ClaimsPrincipal user, HttpContext http,
                ServicioCotizaciones servicio, CancellationToken ct) =>
            RespuestasCotizacion.Responder(await servicio.EmitirAsync(id, user.IdUsuario(), req.Entrada(), ct), http,
                r => Results.Created($"/api/v1/cotizaciones/{r.Ficha.IdCotizacion}", r.Ficha)))
            .WithTags("Cotizaciones")
            .RequireAuthorization(Politicas.Empleado)
            .RequireRateLimiting("ApiGeneral")
            .ConIdempotencia("POST /solicitudes/{id}/cotizaciones");
    }

    internal static IResult IfMatchIlegible() =>
        ErrorApi.Invalido("La cabecera If-Match no es una versión válida. Vuelve a cargar la cotización.");
}

// Traduce el resultado del servicio a la respuesta HTTP, igual para el cliente y para el personal.
public static class RespuestasCotizacion
{
    public static IResult Responder(ResultadoCotizacion resultado, HttpContext http, Func<ResultadoCotizacion.Correcta, IResult> correcta)
    {
        switch (resultado)
        {
            case ResultadoCotizacion.Correcta ok:
                http.Response.Headers.ETag = Concurrencia.ETag(ok.Ficha.RowVersion);
                return correcta(ok);
            case ResultadoCotizacion.Eliminada:
                return correcta(null!);
            case ResultadoCotizacion.Invalida { Errores: var errores }:
                return ErrorApi.Invalido(ValidacionCotizacion.Resumen(errores), errores);
            case ResultadoCotizacion.Conflicto { Codigo: var codigo, Mensaje: var mensaje, DesdeLaBase: var desdeLaBase }:
                return ErrorApi.Crear(StatusCodes.Status409Conflict, codigo, mensaje, nivel: desdeLaBase ? ErrorApi.NivelBaseDeDatos : ErrorApi.NivelApi);
            case ResultadoCotizacion.SinVersion:
                return ErrorApi.Crear(StatusCodes.Status400BadRequest, "sin_version", "Indica la versión que viste (cabecera If-Match con el ETag, o rowVersion).");
            case ResultadoCotizacion.VersionObsoleta:
                return ErrorApi.Crear(StatusCodes.Status409Conflict, Concurrencia.CodigoVersionObsoleta, ServicioCotizaciones.MensajeVersionObsoleta);
            default:
                return Results.NotFound();
        }
    }
}

// Los montos y la cantidad son nulables: si no vienen, la validacion responde 400 por campo en vez de tomar 0.
public record CotizacionMaterialRequest(int IdMaterial, string? MedidaAplicada, decimal? Cantidad, decimal? CostoUnitario);

public record CotizacionRequest(List<CotizacionMaterialRequest>? Materiales, decimal? CostoManoObra, decimal? OtrosCostos, string? Observaciones,
    byte[]? RowVersion = null)
{
    public EntradaCotizacion Entrada() => new(
        Materiales?.Select(m => new EntradaMaterial(m.IdMaterial, m.MedidaAplicada, m.Cantidad, m.CostoUnitario)).ToList(),
        CostoManoObra, OtrosCostos, Observaciones);
}

public record RespuestaCotizacionRequest(string? Decision, string? Via, string? Motivo, byte[]? RowVersion = null);

public record MiRespuestaCotizacionRequest(string? Decision, string? Motivo, byte[]? RowVersion = null);
