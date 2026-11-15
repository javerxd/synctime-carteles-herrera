using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Clientes vistos por el personal (fases 7 y 9). Fase 9 (RNFP10): editar exige la version vista (If-Match).
// Se retiro el DELETE fisico. Sin consultas directas a la base: todo pasa por los servicios.
public static class ClienteApi
{
    public static void MapClienteApi(this WebApplication app)
    {
        var clientes = app.MapGroup("/api/v1/clientes").WithTags("Clientes")
            .RequireAuthorization(Politicas.Empleado).RequireRateLimiting("ApiGeneral");

        // Fase 7: listado paginado con busqueda por telefono, RUT o nombre (?q=&pagina=&tamano=).
        clientes.MapGet("/", async (ServicioClientes servicio, CancellationToken ct, string? q, int? pagina, int? tamano) =>
            Results.Ok(await servicio.ListarAsync(new ParametrosListado(pagina, tamano, buscar: q), ct)));

        // Fase 7: para el registro presencial, buscar al cliente antes de crearlo. Responde una lista de 0 o 1.
        clientes.MapGet("/buscar", async (ServicioClientes servicio, CancellationToken ct, string? telefono, string? rut) =>
            string.IsNullOrWhiteSpace(telefono) && string.IsNullOrWhiteSpace(rut)
                ? ErrorApi.Invalido("Indica un teléfono o un RUT para buscar.")
                : Results.Ok(await servicio.BuscarAsync(telefono, rut, ct)));

        clientes.MapGet("/{id:int}", async (int id, HttpContext http, ServicioFichaClientes servicio, CancellationToken ct) =>
            RespuestasGestion.ConETag(await servicio.ObtenerAsync(id, ct), http));

        clientes.MapPost("/", async (ClienteRequest req, HttpContext http, ServicioFichaClientes servicio, CancellationToken ct) =>
            RespuestasGestion.Responder(await servicio.CrearAsync(req.Entrada(), ct), http,
                c => Results.Created($"/api/v1/clientes/{c.IdCliente}", c), ServicioFichaClientes.MensajeVersionObsoleta))
            .RequireAuthorization(p => p.RequireRole(Roles.Dueno, Roles.Administrador, Roles.Operario));

        clientes.MapPut("/{id:int}", async (int id, ClienteRequest req, HttpContext http, ServicioFichaClientes servicio, CancellationToken ct) =>
            !RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version)
                ? RespuestasGestion.IfMatchIlegible()
                : RespuestasGestion.Responder(await servicio.EditarAsync(id, req.Entrada(), version, ct), http, Results.Ok, ServicioFichaClientes.MensajeVersionObsoleta))
            .RequireAuthorization(p => p.RequireRole(Roles.Dueno, Roles.Administrador, Roles.Operario));

        // Activa el acceso web de un cliente que ya existe, una vez que el personal comprobo su identidad.
        clientes.MapPut("/{id:int}/credenciales", async (int id, AsignarCredencialesRequest req, ClaimsPrincipal user, HttpContext http,
            ServicioReautenticacion reautenticacion, ServicioFichaClientes servicio, AuthService auth, CancellationToken ct) =>
        {
            if (!await reautenticacion.ConfirmarAsync(user.IdUsuario(), req.PasswordActual, ct))
                return Reautenticacion.Rechazo();
            return RespuestasGestion.Responder(await servicio.AsignarCredencialesAsync(id, req.Password, req.Correo, auth, ct), http,
                _ => Results.NoContent(), ServicioFichaClientes.MensajeVersionObsoleta);
        })
        .RequireAuthorization(p => p.RequireRole(Roles.Dueno, Roles.Administrador, Roles.Operario))
        .RequireRateLimiting("Reautenticacion");
    }
}

// Activo: al crear, true si no viene; al editar, si no viene se conserva el valor actual.
public record ClienteRequest(string? Rut, string? Nombre, string? Apellido, string? Telefono, string? Email, string? Direccion, bool? Activo = null,
    byte[]? RowVersion = null)
{
    public EntradaCliente Entrada() => new(Rut, Nombre, Apellido, Telefono, Email, Direccion, Activo);
}

public record AsignarCredencialesRequest(string Password, string? PasswordActual, string? Correo = null);
