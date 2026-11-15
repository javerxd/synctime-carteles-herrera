using LetrerosHerreraSYNCTIMEapi.Api;
using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Ingreso, sesion y recuperacion de cuentas (fase 6, RFS03). Cada ingreso entrega un token de acceso
// corto (Jwt:ExpirationMinutes, 15 por defecto) y deja en cookies el de renovacion (CookiesSesion).
public static class AuthApi
{
    // Un inicio de sesion son dos textos cortos: no hay motivo para aceptar cuerpos grandes sin autenticar.
    private const int TamanoMaximoLogin = 8 * 1024;

    private static readonly TimeSpan TiempoMinimoRecuperacion = TimeSpan.FromMilliseconds(400);

    // Largos razonables de un correo y de una contraseña. Lo que los supera se rechaza sin consultar nada.
    private const int LargoMaximoCuenta = 254;
    private const int LargoMaximoPassword = 200;

    public static void MapAuthApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Autenticacion");

        // ── Ingreso del personal ──
        group.MapPost("/login", async (LoginRequest req, HttpContext contexto, LetrerosHerreradbContext db, AuthService auth,
            ServicioSesiones sesiones, LimitadorPorCuenta limite, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return ErrorApi.Invalido("Escribe tu correo y tu contraseña.");
            if (req.Email.Length > LargoMaximoCuenta || req.Password.Length > LargoMaximoPassword)
                return ErrorApi.Invalido("El correo o la contraseña superan el largo permitido.");
            // Se revisa antes de mirar la base, y cuentan los fallos exista o no la cuenta: el limite no revela nada.
            var cuenta = $"empleado:{ServicioCodigosCuenta.NormalizarCorreo(req.Email)}";
            if (limite.EstaBloqueada(cuenta))
                return ErrorApi.DemasiadosIntentos();

            var correo = req.Email.Trim();
            var empleado = await db.Empleados.FirstOrDefaultAsync(e => e.Email == correo && e.Activo, ct);
            // Sin cuenta tambien se calcula un hash: la demora no revela que correos tienen cuenta.
            if (!auth.VerificarOFingir(req.Password, empleado?.PasswordHash) || empleado is null)
            {
                limite.RegistrarFallo(cuenta);
                return Results.Unauthorized();
            }

            limite.Olvidar(cuenta);
            return await SesionEmpleadoAsync(contexto, empleado, auth, sesiones, ct);
        })
        .AllowAnonymous()
        .RequireRateLimiting("Login")
        .WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoLogin));

        // ── Ingreso del cliente: con su correo (RFS03) ──
        group.MapPost("/login-cliente", async (ClienteLoginRequest req, HttpContext contexto, LetrerosHerreradbContext db, AuthService auth,
            ServicioSesiones sesiones, LimitadorPorCuenta limite, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Correo) || string.IsNullOrWhiteSpace(req.Password))
                return ErrorApi.Invalido("Escribe tu correo y tu contraseña.");
            if (req.Correo.Length > LargoMaximoCuenta || req.Password.Length > LargoMaximoPassword)
                return ErrorApi.Invalido("El correo o la contraseña superan el largo permitido.");
            var correo = ServicioCodigosCuenta.NormalizarCorreo(req.Correo);
            var cuenta = $"cliente:{correo}";
            if (limite.EstaBloqueada(cuenta))
                return ErrorApi.DemasiadosIntentos();

            var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Email == correo && c.Activo, ct);
            if (!auth.VerificarOFingir(req.Password, cliente?.PasswordHash) || cliente is null)
            {
                limite.RegistrarFallo(cuenta);
                return Results.Unauthorized();
            }

            limite.Olvidar(cuenta);
            return await SesionClienteAsync(contexto, cliente, auth, sesiones, StatusCodes.Status200OK, ct);
        })
        .AllowAnonymous()
        .RequireRateLimiting("Login")
        .WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoLogin));

        // ── Registro de cliente (RFU04, E23) ──
        // Un telefono ya existente no se puede reclamar desde aqui: daria acceso a pedidos ajenos
        // conociendo solo el numero. Ese caso lo activa el personal con PUT /clientes/{id}/credenciales.
        group.MapPost("/registro-cliente", async (RegistroClienteRequest req, HttpContext contexto, ServicioMiCuenta cuentas,
            AuthService auth, ServicioSesiones sesiones, CancellationToken ct) =>
        {
            var resultado = await cuentas.RegistrarAsync(new DatosRegistro(req.Rut, req.Nombre, req.Apellido, req.Telefono, req.Correo,
                req.Direccion, req.Password, req.AceptaTerminosYPrivacidad, req.AceptaMarketing, req.VersionTerminos, req.VersionPrivacidad,
                req.VersionMarketing), ct);
            return resultado switch
            {
                ResultadoCuenta.Lista lista => await SesionClienteAsync(contexto, lista.Cliente, auth, sesiones, StatusCodes.Status201Created, ct),
                ResultadoCuenta.Invalida invalida => ErrorApi.Invalido("Revisa los datos del formulario.", invalida.Errores),
                ResultadoCuenta.Conflicto conflicto => ErrorApi.Conflicto(conflicto.Mensaje),
                _ => Results.Problem()
            };
        })
        .AllowAnonymous()
        .RequireRateLimiting("Publico")
        .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        // ── Renovacion silenciosa (RNFP08) ──
        group.MapPost("/renovar", async (HttpContext contexto, ServicioSesiones sesiones, LetrerosHerreradbContext db, AuthService auth, CancellationToken ct) =>
        {
            if (!CookiesSesion.CsrfValido(contexto))
                return ErrorApi.Invalido("Falta la verificación de origen de la sesión (cabecera X-CSRF-Token). Vuelve a ingresar.");
            switch (await sesiones.RenovarAsync(CookiesSesion.Leer(contexto), ct))
            {
                case ResultadoRenovacion.Renovada renovada:
                    CookiesSesion.Escribir(contexto, renovada.Sesion.TokenRenovacion, renovada.Sesion.Expira);
                    if (renovada.Titular.EsCliente)
                    {
                        var cliente = await db.Clientes.AsNoTracking().FirstAsync(c => c.IdCliente == renovada.Titular.IdCliente, ct);
                        return Results.Ok(RespuestaCliente(auth.CreateTokenCliente(cliente), cliente));
                    }
                    var empleado = await db.Empleados.AsNoTracking().FirstAsync(e => e.IdEmpleado == renovada.Titular.IdEmpleado, ct);
                    return Results.Ok(RespuestaEmpleado(auth.CreateTokenEmpleado(empleado), empleado));
                case ResultadoRenovacion.EnCurso:
                    return ErrorApi.Conflicto("La sesión se está renovando en otra pestaña. Vuelve a intentarlo.");
                default:
                    CookiesSesion.Borrar(contexto);
                    return Results.Unauthorized();
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting("ApiGeneral");

        // ── Cierre de sesion ──
        group.MapPost("/salir", async (HttpContext contexto, ServicioSesiones sesiones, CancellationToken ct) =>
        {
            if (!CookiesSesion.CsrfValido(contexto))
                return ErrorApi.Invalido("Falta la verificación de origen de la sesión (cabecera X-CSRF-Token).");
            await sesiones.CerrarAsync(CookiesSesion.Leer(contexto), ct);
            CookiesSesion.Borrar(contexto);
            return Results.NoContent();
        })
        .AllowAnonymous()
        .RequireRateLimiting("ApiGeneral");

        // ── Correo verificado ──
        group.MapPost("/verificar-correo", async (VerificarCorreoRequest req, ServicioCodigosCuenta codigos, CancellationToken ct) =>
            await codigos.VerificarCorreoAsync(req.Token, ct)
                ? Results.Ok(new { Verificado = true })
                : ErrorApi.Invalido("El enlace no es válido o ya venció. Ingresa a tu cuenta y pide uno nuevo."))
        .AllowAnonymous()
        .RequireRateLimiting("Login");

        group.MapPost("/reenviar-verificacion", async (ClaimsPrincipal user, LetrerosHerreradbContext db, ServicioCodigosCuenta codigos, CancellationToken ct) =>
        {
            var id = user.IdUsuario();
            var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == id && c.Activo, ct);
            if (cliente is null)
                return Results.NotFound();
            if (cliente.FechaVerificacionCorreo is null)
                await codigos.EnviarVerificacionAsync(cliente, ct);
            return Results.Accepted(value: new { Mensaje = "Si tu correo aún no está confirmado, te enviamos un enlace nuevo." });
        })
        .RequireAuthorization(Politicas.Cliente)
        .RequireRateLimiting("Login");

        // ── Recuperacion de la contraseña (RNFP13) ──
        // La respuesta es la misma exista o no la cuenta: no revela quien tiene cuenta.
        group.MapPost("/recuperar", async (RecuperarRequest req, ServicioCodigosCuenta codigos, CancellationToken ct) =>
        {
            if (req.Tipo is not (ServicioCodigosCuenta.TipoCliente or ServicioCodigosCuenta.TipoPersonal))
                return ErrorApi.Invalido("Tipo de cuenta no válido.");
            // Exista o no la cuenta, la respuesta tarda lo mismo: el tiempo no revela quien tiene cuenta.
            var inicio = System.Diagnostics.Stopwatch.GetTimestamp();
            await codigos.SolicitarRecuperacionAsync(req.Tipo, req.Correo, ct);
            var restante = TiempoMinimoRecuperacion - System.Diagnostics.Stopwatch.GetElapsedTime(inicio);
            if (restante > TimeSpan.Zero)
                await Task.Delay(restante, ct);
            return Results.Accepted(value: new
            {
                Mensaje = $"Si el correo tiene una cuenta, te enviamos un código. Revisa tu bandeja de entrada y la de correo no deseado; vence en {ServicioCodigosCuenta.MinutosRecuperacion} minutos."
            });
        })
        .AllowAnonymous()
        .RequireRateLimiting("Login")
        .WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoLogin));

        group.MapPost("/restablecer", async (RestablecerRequest req, ServicioCodigosCuenta codigos, LimitadorPorCuenta limite, CancellationToken ct) =>
        {
            if (req.Tipo is not (ServicioCodigosCuenta.TipoCliente or ServicioCodigosCuenta.TipoPersonal))
                return ErrorApi.Invalido("Tipo de cuenta no válido.");
            var cuenta = $"recuperacion:{req.Tipo}:{ServicioCodigosCuenta.NormalizarCorreo(req.Correo)}";
            if (limite.EstaBloqueada(cuenta))
                return ErrorApi.DemasiadosIntentos();

            switch (await codigos.RestablecerAsync(req.Tipo, req.Correo, req.Codigo, req.Password, ct))
            {
                case ResultadoRestablecer.Restablecida:
                    limite.Olvidar(cuenta);
                    return Results.NoContent();
                case ResultadoRestablecer.PasswordInvalida:
                    return ErrorApi.Invalido(AuthService.MensajePasswordInvalida,
                        new Dictionary<string, string[]> { ["password"] = [AuthService.MensajePasswordInvalida] });
                default:
                    limite.RegistrarFallo(cuenta);
                    return ErrorApi.Invalido(ServicioCodigosCuenta.MensajeCodigoInvalido,
                        new Dictionary<string, string[]> { ["codigo"] = [ServicioCodigosCuenta.MensajeCodigoInvalido] });
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting("Login")
        .WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoLogin));

        // El alta de empleado paso a POST /api/v1/empleados (fase 9).

        // ── Who am I? ──
        group.MapGet("/me", (HttpContext ctx) =>
        {
            var claims = ctx.User.Claims.Select(c => new { c.Type, c.Value });
            return Results.Ok(claims);
        })
        .RequireAuthorization();
    }

    // Misma respuesta para el ingreso con contraseña, con rostro y la renovacion.
    // conNombreYRol (fase 10, S15): el ingreso con rostro dice ademas quien entro, con su nombre y su rol.
    internal static async Task<IResult> SesionEmpleadoAsync(HttpContext contexto, Empleado empleado, AuthService auth, ServicioSesiones sesiones, CancellationToken ct,
        bool conNombreYRol = false)
    {
        // Si este equipo tenia otra sesion abierta (por ejemplo, alguien que no salio), se cierra.
        await sesiones.CerrarAsync(CookiesSesion.Leer(contexto), ct);
        var sesion = await sesiones.IniciarAsync(TitularSesion.DeEmpleado(empleado.IdEmpleado), ct);
        CookiesSesion.Escribir(contexto, sesion.TokenRenovacion, sesion.Expira);
        var token = auth.CreateTokenEmpleado(empleado);
        return Results.Ok(conNombreYRol ? RespuestaEmpleadoConNombreYRol(token, empleado) : RespuestaEmpleado(token, empleado));
    }

    internal static async Task<IResult> SesionClienteAsync(HttpContext contexto, Cliente cliente, AuthService auth, ServicioSesiones sesiones, int estado, CancellationToken ct)
    {
        await sesiones.CerrarAsync(CookiesSesion.Leer(contexto), ct);
        var sesion = await sesiones.IniciarAsync(TitularSesion.DeCliente(cliente.IdCliente), ct);
        CookiesSesion.Escribir(contexto, sesion.TokenRenovacion, sesion.Expira);
        var cuerpo = RespuestaCliente(auth.CreateTokenCliente(cliente), cliente);
        return estado == StatusCodes.Status201Created ? Results.Created("/api/v1/mi-cuenta", cuerpo) : Results.Ok(cuerpo);
    }

    private static object RespuestaEmpleado(string token, Empleado empleado) => new
    {
        token,
        tipo = "empleado",
        usuario = new { empleado.IdEmpleado, empleado.Nombre, empleado.Apellido, empleado.Email, empleado.Rol }
    };

    private static object RespuestaEmpleadoConNombreYRol(string token, Empleado empleado) => new
    {
        token,
        tipo = "empleado",
        usuario = new { empleado.IdEmpleado, empleado.Nombre, empleado.Apellido, empleado.Email, empleado.Rol },
        nombre = $"{empleado.Nombre} {empleado.Apellido}",
        rol = empleado.Rol
    };

    private static object RespuestaCliente(string token, Cliente cliente) => new
    {
        token,
        tipo = "cliente",
        usuario = new
        {
            cliente.IdCliente, cliente.Nombre, cliente.Apellido, cliente.Telefono, cliente.Email,
            CorreoVerificado = cliente.FechaVerificacionCorreo is not null
        }
    };
}

public record LoginRequest(string Email, string Password);
public record ClienteLoginRequest(string Correo, string Password);
public record RegistroClienteRequest(
    string? Rut, string? Nombre, string? Apellido, string? Telefono, string? Correo, string? Direccion, string? Password,
    bool AceptaTerminosYPrivacidad, bool AceptaMarketing, string? VersionTerminos, string? VersionPrivacidad, string? VersionMarketing);
public record VerificarCorreoRequest(string? Token);
public record RecuperarRequest(string? Tipo, string? Correo);
public record RestablecerRequest(string? Tipo, string? Correo, string? Codigo, string? Password);
