using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Mantenedores;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Fase 9: personal (E15, RFU18, RNFO08) y materiales (E14). Administrar es de gestion, con version
// (If-Match) y sin borrado fisico. El alta de personal paso de /auth/empleados a POST /empleados, con la
// misma reautenticacion de quien la da. Leer los materiales y la lista corta del personal activo lo puede
// todo el personal (editor de cotizaciones y de pedidos).
public static class PersonalApi
{
    public static void MapPersonalApi(this WebApplication app)
    {
        var empleados = app.MapGroup("/api/v1/empleados").WithTags("Empleados")
            .RequireAuthorization(Politicas.Gestion).RequireRateLimiting("ApiGeneral");

        empleados.MapGet("/", async (ServicioEmpleados servicio, CancellationToken ct, string? q, string? activo, string? orden, int? pagina, int? tamano) =>
            !FiltroActivo.TryLeer(activo, out var filtro)
                ? CatalogoApi.FiltroInvalido()
                : Results.Ok(await servicio.ListarAsync(new ParametrosListado(pagina, tamano, orden, q), filtro, ct)));

        // Para elegir al responsable de un pedido: { idEmpleado, nombre, rol } de los activos.
        app.MapGet("/api/v1/empleados/activos", async (ServicioEmpleados servicio, CancellationToken ct) =>
                Results.Ok(await servicio.ActivosAsync(ct)))
            .WithTags("Empleados")
            .RequireAuthorization(Politicas.Empleado)
            .RequireRateLimiting("ApiGeneral");

        empleados.MapGet("/{id:int}", async (int id, HttpContext http, ServicioEmpleados servicio, CancellationToken ct) =>
            RespuestasGestion.ConETag(await servicio.ObtenerAsync(id, ct), http));

        empleados.MapPost("/", async (EmpleadoRequest req, ClaimsPrincipal user, HttpContext http, ServicioReautenticacion reautenticacion,
            ServicioEmpleados servicio, CancellationToken ct) =>
        {
            if (!await reautenticacion.ConfirmarAsync(user.IdUsuario(), req.PasswordActual, ct))
                return RespuestasGestion.ReautenticacionFallida();
            return RespuestasGestion.Responder(await servicio.CrearAsync(new EntradaAltaEmpleado(req.Entrada(), req.Password), ct), http,
                e => Results.Created($"/api/v1/empleados/{e.IdEmpleado}", e), ServicioEmpleados.MensajeVersionObsoleta);
        })
        .RequireRateLimiting("Reautenticacion")
        .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        // Cambiar el correo o el rol exige passwordActual de quien edita (mismo limite que el alta).
        empleados.MapPut("/{id:int}", async (int id, EmpleadoRequest req, ClaimsPrincipal user, HttpContext http, ServicioEmpleados servicio, CancellationToken ct) =>
            !RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version)
                ? RespuestasGestion.IfMatchIlegible()
                : RespuestasGestion.Responder(await servicio.EditarAsync(user.IdUsuario(), id, req.Entrada(), version, req.PasswordActual, ct), http, Results.Ok,
                    ServicioEmpleados.MensajeVersionObsoleta))
            .RequireRateLimiting("Reautenticacion");

        // Desactivar borra su rostro y cierra sus sesiones en el mismo paso.
        empleados.MapPatch("/{id:int}/activo", async (int id, CambioActivoRequest req, ClaimsPrincipal user, HttpContext http, ServicioEmpleados servicio,
            CancellationToken ct) =>
        {
            if (req.Activo is not bool activo)
                return CatalogoApi.FaltaActivo();
            return !RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version)
                ? RespuestasGestion.IfMatchIlegible()
                : RespuestasGestion.Responder(await servicio.CambiarActivoAsync(user.IdUsuario(), id, activo, version, ct), http, Results.Ok,
                    ServicioEmpleados.MensajeVersionObsoleta);
        });

        var materiales = app.MapGroup("/api/v1/materiales").WithTags("Materiales")
            .RequireAuthorization(Politicas.Empleado).RequireRateLimiting("ApiGeneral");

        // El editor de cotizaciones (todo el personal) ve solo los activos; gestion, todos o ?activo=.
        materiales.MapGet("/", async (ClaimsPrincipal user, ServicioMateriales servicio, CancellationToken ct, string? q, string? activo, string? orden,
            int? pagina, int? tamano) =>
            !FiltroActivo.TryLeer(activo, out var filtro)
                ? CatalogoApi.FiltroInvalido()
                : Results.Ok(await servicio.ListarAsync(new ParametrosListado(pagina, tamano, orden, q), CatalogoApi.EsGestion(user) ? filtro : true, ct)));

        materiales.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, HttpContext http, ServicioMateriales servicio, CancellationToken ct) =>
            RespuestasGestion.ConETag(await servicio.ObtenerAsync(id, CatalogoApi.EsGestion(user), ct), http));

        materiales.MapPost("/", async (MaterialRequest req, HttpContext http, ServicioMateriales servicio, CancellationToken ct) =>
            RespuestasGestion.Responder(await servicio.CrearAsync(req.Entrada(), ct), http,
                m => Results.Created($"/api/v1/materiales/{m.IdMaterial}", m), ServicioMateriales.MensajeVersionObsoleta))
            .RequireAuthorization(Politicas.Gestion);

        materiales.MapPut("/{id:int}", async (int id, MaterialRequest req, HttpContext http, ServicioMateriales servicio, CancellationToken ct) =>
            !RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version)
                ? RespuestasGestion.IfMatchIlegible()
                : RespuestasGestion.Responder(await servicio.EditarAsync(id, req.Entrada(), version, ct), http, Results.Ok, ServicioMateriales.MensajeVersionObsoleta))
            .RequireAuthorization(Politicas.Gestion);

        materiales.MapPatch("/{id:int}/activo", async (int id, CambioActivoRequest req, HttpContext http, ServicioMateriales servicio, CancellationToken ct) =>
        {
            if (req.Activo is not bool activo)
                return CatalogoApi.FaltaActivo();
            return !RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version)
                ? RespuestasGestion.IfMatchIlegible()
                : RespuestasGestion.Responder(await servicio.CambiarActivoAsync(id, activo, version, ct), http, Results.Ok, ServicioMateriales.MensajeVersionObsoleta);
        })
        .RequireAuthorization(Politicas.Gestion);
    }
}

// password solo al crear: la contraseña inicial la escribe quien da el alta. passwordActual (la de quien
// edita) al crear, y al cambiar el correo o el rol.
public record EmpleadoRequest(string? Rut, string? Nombre, string? Apellido, string? Telefono, string? Email, string? Rol, string? Password = null,
    string? PasswordActual = null, byte[]? RowVersion = null)
{
    public EntradaEmpleado Entrada() => new(Rut, Nombre, Apellido, Telefono, Email, Rol);
}

public record MaterialRequest(string? Nombre, string? UnidadMedida, decimal? CostoUnitarioAprox, byte[]? RowVersion = null)
{
    public EntradaMaterialMantenedor Entrada() => new(Nombre, UnidadMedida, CostoUnitarioAprox);
}
