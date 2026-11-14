using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Fase 7: el cliente con sesion envia sus solicitudes en linea y ve las suyas, con sus adjuntos. El id
// sale siempre del token, nunca de la peticion. id_empleado_registra queda NULL: eso marca la solicitud
// como «online».
public static class MisSolicitudesApi
{
    public static void MapMisSolicitudesApi(this WebApplication app)
    {
        var mis = app.MapGroup("/api/v1/mis").WithTags("Portal cliente")
            .RequireAuthorization(Politicas.Cliente).RequireRateLimiting("ApiGeneral");

        mis.MapGet("/solicitudes", async (ClaimsPrincipal user, ServicioSolicitudes servicio, CancellationToken ct, int? pagina, int? tamano) =>
            Results.Ok(await servicio.ListarDelClienteAsync(user.IdUsuario(), new ParametrosListado(pagina, tamano), ct)));

        // Lo ajeno responde 404, igual que lo que no existe.
        mis.MapGet("/solicitudes/{id}", async (int id, ClaimsPrincipal user, ServicioSolicitudes servicio, CancellationToken ct) =>
            await servicio.ComprobanteAsync(id, user.IdUsuario(), ct) is { } comprobante ? Results.Ok(comprobante) : Results.NotFound());

        // Exige correo confirmado (403 correo_sin_confirmar) y pasa la regla contra solicitudes falsas
        // (409 contacto_directo, sin escribir nada). La huella de la IP se calcula con la clave del servidor.
        mis.MapPost("/solicitudes", async (CrearMiSolicitudRequest req, ClaimsPrincipal user, HttpContext http, ServicioSolicitudes servicio, CancellationToken ct) =>
        {
            var entrada = new EntradaSolicitud(req.DescripcionGeneral, RespuestasSolicitud.Detalles(req.Detalles), req.Adjuntos);
            var resultado = await servicio.CrearEnLineaAsync(user.IdUsuario(), entrada, Ip(http), ct);
            return RespuestasSolicitud.Responder(resultado, http,
                r => Results.Created($"/api/v1/mis/solicitudes/{r.Comprobante.IdSolicitud}", r.Comprobante));
        });

        // Cada archivo se sube al elegirlo y queda como borrador del cliente hasta que envie la solicitud.
        // La API usa tokens Bearer, no cookies: la proteccion antifalsificacion de formularios no aplica.
        mis.MapPost("/adjuntos", async (HttpContext http, ClaimsPrincipal user, ServicioAdjuntos adjuntos, CancellationToken ct) =>
        {
            var leido = await ArchivoSubido.LeerAsync(http, ct);
            if (leido.Error is not null)
                return leido.Error;
            return ArchivoSubido.Responder(await adjuntos.SubirBorradorAsync(user.IdUsuario(), leido.Datos!, leido.Nombre, ct));
        })
        .DisableAntiforgery()
        .RequireRateLimiting(ArchivoSubido.PoliticaDeLimite);

        // Los borradores propios, con la misma forma que la respuesta de la subida.
        mis.MapGet("/adjuntos", async (ClaimsPrincipal user, ServicioAdjuntos adjuntos, CancellationToken ct) =>
            Results.Ok((await adjuntos.BorradoresAsync(user.IdUsuario(), ct))
                .Select(a => new { a.IdAdjunto, a.Tipo, a.Nombre, a.Bytes, a.Ancho, a.Alto, a.UrlMiniatura })));

        mis.MapDelete("/adjuntos/{id}", async (int id, ClaimsPrincipal user, ServicioAdjuntos adjuntos, CancellationToken ct) =>
            await adjuntos.BorrarBorradorAsync(user.IdUsuario(), id, ct) ? Results.NoContent() : Results.NotFound());
    }

    // IPv4 escrita como IPv6 (::ffff:1.2.3.4) cuenta como la misma IPv4.
    private static string? Ip(HttpContext http)
    {
        var ip = http.Connection.RemoteIpAddress;
        if (ip is null)
            return null;
        return (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();
    }
}

public record CrearMiSolicitudRequest(string? DescripcionGeneral, List<CrearSolicitudDetalleRequest>? Detalles, List<int>? Adjuntos);

// Fase 7: los adjuntos son privados. Solo los ven el cliente dueño y el personal, con sesion; a los demas
// se les responde 404. Nunca se guardan en cache ni se interpretan con otro tipo.
public static class AdjuntoApi
{
    public static void MapAdjuntoApi(this WebApplication app)
    {
        var grupo = app.MapGroup(Adjuntos.RutaBase).WithTags("Adjuntos")
            .RequireAuthorization()
            // Una bandeja muestra varias miniaturas a la vez: mismo limite que las imagenes de la vitrina.
            .RequireRateLimiting("Imagenes");

        grupo.MapGet("/{id}/miniatura", (int id, ClaimsPrincipal user, HttpContext http, ServicioAdjuntos servicio, CancellationToken ct) =>
            ServirAsync(id, Infraestructura.Adjuntos.VersionAdjunto.Miniatura, user, http, servicio, ct));

        grupo.MapGet("/{id}/archivo", (int id, ClaimsPrincipal user, HttpContext http, ServicioAdjuntos servicio, CancellationToken ct) =>
            ServirAsync(id, Infraestructura.Adjuntos.VersionAdjunto.Archivo, user, http, servicio, ct));
    }

    private static async Task<IResult> ServirAsync(int id, Infraestructura.Adjuntos.VersionAdjunto version, ClaimsPrincipal user, HttpContext http,
        ServicioAdjuntos servicio, CancellationToken ct)
    {
        var lector = user.IsInRole(Roles.Cliente) ? LectorAdjunto.DeCliente(user.IdUsuario()) : LectorAdjunto.Personal;
        var archivo = await servicio.AbrirAsync(id, lector, version, ct);
        http.Response.Headers.CacheControl = "private, no-store";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        if (archivo is null)
            return Results.NotFound();
        return archivo.NombreDescarga is { } nombre
            ? Results.File(archivo.Ruta, archivo.TipoContenido, fileDownloadName: nombre)
            : Results.File(archivo.Ruta, archivo.TipoContenido);
    }
}
