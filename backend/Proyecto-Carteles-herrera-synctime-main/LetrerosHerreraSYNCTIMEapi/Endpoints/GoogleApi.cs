using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Ingreso con Google (fase 14A, RFS03, RFS12, RFU05, RFU06, RNFE02, RNFP17). El navegador entrega a la API el token de identidad
// de Google (un JWT firmado) y la API solo verifica la firma y las condiciones: no hay redireccion ni clave secreta de cliente.
// Para clientes y, desde la fase 14C, tambien para el personal (si ya vinculo su Google desde Mi perfil). Las rutas anonimas responden 401 si
// el token no sirve; las de Mi cuenta y Mi perfil responden 400 (un 401 alli significaria «tu sesion vencio» y el navegador intentaria renovarla).
public static class GoogleApi
{
    // Un token de Google mide ~1,2 KB y el resto son textos cortos: no hay motivo para cuerpos grandes sin autenticar.
    private const int TamanoMaximoIngreso = 8 * 1024;
    private const int TamanoMaximoCompletar = 16 * 1024;

    // Limite por IP del nonce y del ingreso con Google (RateLimiting:IngresoGoogle, por minuto); se define en Program.cs.
    public const string PoliticaDeLimite = "IngresoGoogle";

    public static void MapGoogleApi(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/v1/auth/google").WithTags("Ingreso con Google");

        // El nonce de un solo uso que Google copia dentro del token (vence a los 5 minutos).
        // Politica propia (IngresoGoogle, por IP; IPv6 por /64): pedir nonces o intentar con Google no gasta el cupo del ingreso con contraseña.
        grupo.MapGet("/nonce", (HttpContext contexto, ServicioGoogle google) =>
        {
            if (google.EmitirNonce() is not { } nonce)
                return ErrorApi.DemasiadosIntentos();
            contexto.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { nonce = nonce.Nonce, venceEn = nonce.Vence });
        })
        .AllowAnonymous()
        .RequireRateLimiting(PoliticaDeLimite);

        // Los caminos: sesion abierta (de cliente o, fase 14C, de personal), cuenta existente con contraseña (409), correo de empleado sin vincular
        // (409 cuenta_personal_existente) o correo nuevo (registro pendiente).
        grupo.MapPost("/", async (GoogleIngresoRequest req, HttpContext contexto, ServicioGoogle google, AuthService auth, ServicioSesiones sesiones, CancellationToken ct) =>
            await ResponderAsync(await google.IngresarAsync(req.IdToken, req.Nonce, ct), contexto, auth, sesiones, anonimo: true, StatusCodes.Status200OK, ct))
        .AllowAnonymous()
        .RequireRateLimiting(PoliticaDeLimite)
        .WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoIngreso));

        // «Completa tu registro»: crea la cuenta (sin contraseña, correo ya verificado), su identidad y los consentimientos.
        grupo.MapPost("/completar", async (GoogleCompletarRequest req, HttpContext contexto, ServicioGoogle google, AuthService auth, ServicioSesiones sesiones, CancellationToken ct) =>
            await ResponderAsync(await google.CompletarAsync(new DatosCompletarGoogle(req.Pase, req.Rut, req.Nombre, req.Apellido, req.Telefono, req.Direccion,
                req.AceptaTerminosYPrivacidad, req.AceptaMarketing, req.VersionTerminos, req.VersionPrivacidad, req.VersionMarketing), ct),
                contexto, auth, sesiones, anonimo: true, StatusCodes.Status201Created, ct))
        .AllowAnonymous()
        .RequireRateLimiting("Publico")
        .WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoCompletar));

        // ── Mi cuenta: vincular y desvincular (piden la contraseña, con el limite de las acciones delicadas) ──
        var miCuenta = app.MapGroup("/api/v1/mi-cuenta/google").WithTags("Mi cuenta")
            .RequireAuthorization(Politicas.Cliente)
            .RequireRateLimiting("Reautenticacion");

        miCuenta.MapPost("/", async (GoogleVincularRequest req, ClaimsPrincipal user, HttpContext contexto, ServicioGoogle google, AuthService auth,
            ServicioSesiones sesiones, LimitadorPorCuenta limite, CancellationToken ct) =>
        {
            var cuenta = $"cliente-clave:{user.IdUsuario()}";
            if (limite.EstaBloqueada(cuenta))
                return ErrorApi.DemasiadosIntentos();
            var resultado = await google.VincularAsync(user.IdUsuario(), req.IdToken, req.Nonce, req.PasswordActual, ct);
            RegistrarLimite(resultado, limite, cuenta);
            return await ResponderAsync(resultado, contexto, auth, sesiones, anonimo: false, StatusCodes.Status200OK, ct);
        })
        .WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoIngreso));

        // ── Fase 14C: Mi perfil del personal (los cuatro roles): estado, vincular y desvincular, con la contraseña actual ──
        // El estado (GET) NO gasta el cupo por IP de las acciones con contraseña: cada visita a Mi perfil lo consultaria, y todo el taller sale por una IP.
        app.MapGroup("/api/v1/mi-perfil/google").WithTags("Mi perfil")
            .RequireAuthorization(Politicas.Empleado)
            .MapGet("/", async (ClaimsPrincipal user, HttpContext contexto, ServicioGoogle google, AuthService auth, ServicioSesiones sesiones, CancellationToken ct) =>
            await ResponderAsync(await google.EstadoPersonalAsync(user.IdUsuario(), ct), contexto, auth, sesiones, anonimo: false, StatusCodes.Status200OK, ct));

        var miPerfil = app.MapGroup("/api/v1/mi-perfil/google").WithTags("Mi perfil")
            .RequireAuthorization(Politicas.Empleado)
            .RequireRateLimiting("Reautenticacion");

        miPerfil.MapPost("/", async (GoogleVincularRequest req, ClaimsPrincipal user, HttpContext contexto, ServicioGoogle google, AuthService auth,
            ServicioSesiones sesiones, LimitadorPorCuenta limite, CancellationToken ct) =>
        {
            var cuenta = $"empleado-clave:{user.IdUsuario()}";
            if (limite.EstaBloqueada(cuenta))
                return ErrorApi.DemasiadosIntentos();
            var resultado = await google.VincularPersonalAsync(user.IdUsuario(), req.IdToken, req.Nonce, req.PasswordActual, ct);
            RegistrarLimite(resultado, limite, cuenta);
            return await ResponderAsync(resultado, contexto, auth, sesiones, anonimo: false, StatusCodes.Status200OK, ct);
        })
        .WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoIngreso));

        miPerfil.MapDelete("/", async ([FromBody] GoogleDesvincularRequest req, ClaimsPrincipal user, HttpContext contexto, ServicioGoogle google, AuthService auth,
            ServicioSesiones sesiones, LimitadorPorCuenta limite, CancellationToken ct) =>
        {
            var cuenta = $"empleado-clave:{user.IdUsuario()}";
            if (limite.EstaBloqueada(cuenta))
                return ErrorApi.DemasiadosIntentos();
            var resultado = await google.DesvincularPersonalAsync(user.IdUsuario(), req.PasswordActual, ct);
            RegistrarLimite(resultado, limite, cuenta);
            return await ResponderAsync(resultado, contexto, auth, sesiones, anonimo: false, StatusCodes.Status200OK, ct);
        })
        .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        // DELETE no infiere el cuerpo: se pide con [FromBody].
        miCuenta.MapDelete("/", async ([FromBody] GoogleDesvincularRequest req, ClaimsPrincipal user, HttpContext contexto, ServicioGoogle google, AuthService auth,
            ServicioSesiones sesiones, LimitadorPorCuenta limite, CancellationToken ct) =>
        {
            var cuenta = $"cliente-clave:{user.IdUsuario()}";
            if (limite.EstaBloqueada(cuenta))
                return ErrorApi.DemasiadosIntentos();
            var resultado = await google.DesvincularAsync(user.IdUsuario(), req.PasswordActual, ct);
            RegistrarLimite(resultado, limite, cuenta);
            return await ResponderAsync(resultado, contexto, auth, sesiones, anonimo: false, StatusCodes.Status200OK, ct);
        })
        .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));
    }

    // Una contraseña equivocada cuenta contra la cuenta: con un token robado no se prueban contraseñas sin limite.
    private static void RegistrarLimite(ResultadoGoogle resultado, LimitadorPorCuenta limite, string cuenta)
    {
        if (resultado is ResultadoGoogle.Invalido { Errores: var errores } && errores.ContainsKey("passwordActual"))
            limite.RegistrarFallo(cuenta);
        else if (resultado is ResultadoGoogle.Vinculada or ResultadoGoogle.Desvinculada)
            limite.Olvidar(cuenta);
    }

    private static async Task<IResult> ResponderAsync(ResultadoGoogle resultado, HttpContext contexto, AuthService auth, ServicioSesiones sesiones, bool anonimo,
        int estadoDeLaSesion, CancellationToken ct)
    {
        switch (resultado)
        {
            case ResultadoGoogle.Sesion sesion:
                // Misma sesion que el ingreso con contraseña: token corto y cookie de renovacion. 201 si se acaba de crear la cuenta.
                contexto.Response.Headers.CacheControl = "no-store";
                return await AuthApi.SesionClienteAsync(contexto, sesion.Cliente, auth, sesiones, estadoDeLaSesion, ct);
            case ResultadoGoogle.SesionPersonal personal:
                // Fase 14C: la misma sesion de personal que da la contraseña (token corto y cookie de renovacion).
                contexto.Response.Headers.CacheControl = "no-store";
                return await AuthApi.SesionEmpleadoAsync(contexto, personal.Empleado, auth, sesiones, ct);
            case ResultadoGoogle.DesvinculadaPersonal desvinculada:
            {
                // Se cerraron todas sus sesiones: se abre una nueva en este equipo (como al cambiar la contraseña) y se entrega su token.
                contexto.Response.Headers.CacheControl = "no-store";
                var nueva = await sesiones.IniciarAsync(TitularSesion.DeEmpleado(desvinculada.Empleado.IdEmpleado), ct);
                CookiesSesion.Escribir(contexto, nueva.TokenRenovacion, nueva.Expira);
                return Results.Ok(new { token = auth.CreateTokenEmpleado(desvinculada.Empleado) });
            }
            case ResultadoGoogle.EstadoGoogle estado:
                contexto.Response.Headers.CacheControl = "no-store";
                return Results.Ok(new { vinculado = estado.Vinculado, correo = estado.Correo });
            case ResultadoGoogle.CuentaPersonalExistente:
                return ErrorApi.Crear(StatusCodes.Status409Conflict, "cuenta_personal_existente", ServicioGoogle.MensajeCuentaPersonalExistente);
            case ResultadoGoogle.RegistroPendiente pendiente:
                contexto.Response.Headers.CacheControl = "no-store";
                return Results.Ok(new
                {
                    estado = "registro_pendiente",
                    pase = pendiente.Pase,
                    venceEn = pendiente.VenceEn,
                    correo = pendiente.Identidad.Correo,
                    nombre = pendiente.Identidad.Nombre,
                    apellido = pendiente.Identidad.Apellido
                });
            case ResultadoGoogle.CuentaExistente:
                return ErrorApi.Crear(StatusCodes.Status409Conflict, "cuenta_existente", ServicioGoogle.MensajeCuentaExistente);
            case ResultadoGoogle.TokenInvalido:
                return ErrorApi.Crear(anonimo ? StatusCodes.Status401Unauthorized : StatusCodes.Status400BadRequest, "google_token_invalido", ServicioGoogle.MensajeTokenInvalido);
            case ResultadoGoogle.PaseInvalido:
                return ErrorApi.Crear(StatusCodes.Status401Unauthorized, "google_pase_invalido", ServicioGoogle.MensajePaseInvalido);
            case ResultadoGoogle.NoDisponible:
                return ErrorApi.Crear(StatusCodes.Status503ServiceUnavailable, "google_no_disponible", ServicioGoogle.MensajeNoDisponible);
            case ResultadoGoogle.NoConfigurado:
                return ErrorApi.Crear(StatusCodes.Status503ServiceUnavailable, "google_no_configurado", ServicioGoogle.MensajeNoConfigurado);
            case ResultadoGoogle.Invalido invalido:
                return ErrorApi.Invalido(invalido.Mensaje, invalido.Errores);
            case ResultadoGoogle.Conflicto conflicto:
                return ErrorApi.Crear(StatusCodes.Status409Conflict, conflicto.Codigo, conflicto.Mensaje);
            case ResultadoGoogle.Vinculada vinculada:
                return Results.Ok(new { vinculado = true, correo = vinculada.Correo });
            case ResultadoGoogle.Desvinculada:
                return Results.NoContent();
            default:
                return Results.NotFound();
        }
    }
}

public record GoogleIngresoRequest(string? IdToken, string? Nonce);
public record GoogleCompletarRequest(
    string? Pase, string? Rut, string? Nombre, string? Apellido, string? Telefono, string? Direccion,
    bool AceptaTerminosYPrivacidad, bool AceptaMarketing, string? VersionTerminos, string? VersionPrivacidad, string? VersionMarketing);
public record GoogleVincularRequest(string? IdToken, string? Nonce, string? PasswordActual);
public record GoogleDesvincularRequest(string? PasswordActual);
