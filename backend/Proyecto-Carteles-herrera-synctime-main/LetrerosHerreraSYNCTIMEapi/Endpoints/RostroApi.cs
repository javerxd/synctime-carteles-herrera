using System.Diagnostics;
using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Negocio.Rostro;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Login facial solo para dueño y administrador (fase 10): complementa a email y contraseña, no los reemplaza.
// Cada ingreso pide antes un desafio con el orden de los giros (prueba de vida). La logica vive en
// ServicioRostros y ServicioCalibracionRostro; aqui solo se traduce a HTTP.
public static class RostroApi
{
    // Tres capturas con su descriptor y sus 68 puntos ocupan unos 12 KB en JSON.
    private const long TamanoDesafio = 1024;
    private const long TamanoLogin = 32 * 1024;
    private const long TamanoRegistro = 64 * 1024;

    public static void MapRostroApi(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Login facial");

        // Cupo propio (RateLimiting:DesafioFacial): pedir desafios no gasta los intentos de ingreso.
        auth.MapPost("/login-facial/desafio", PedirDesafioAsync)
            .AllowAnonymous()
            .RequireRateLimiting("DesafioFacial")
            .WithMetadata(new RequestSizeLimitAttribute(TamanoDesafio));

        auth.MapPost("/login-facial", IniciarSesionAsync)
            .AllowAnonymous()
            .RequireRateLimiting("LoginFacial")
            .WithMetadata(new RequestSizeLimitAttribute(TamanoLogin));

        var rostro = auth.MapGroup("/rostro").RequireAuthorization(Politicas.Empleado);

        rostro.MapGet("/", async (ClaimsPrincipal user, ServicioRostros servicio, CancellationToken ct) =>
                Results.Ok(await servicio.EstadoAsync(user.IdUsuario(), ct)))
            .RequireRateLimiting("ApiGeneral");

        // Retirar el consentimiento no pide contraseña: debe ser tan expedito como sea posible.
        rostro.MapDelete("/", async (ClaimsPrincipal user, ServicioRostros servicio, CancellationToken ct) =>
            {
                await servicio.DesactivarAsync(user.IdUsuario(), user.IdUsuario(), ct);
                return Results.NoContent();
            })
            .RequireRateLimiting("ApiGeneral");

        rostro.MapPut("/", async (RegistrarRostroRequest req, ClaimsPrincipal user, ServicioRostros servicio, CancellationToken ct) =>
                Responder(await servicio.RegistrarPropioAsync(user.IdUsuario(),
                    new RegistroRostro(req.Password, req.Consentimiento, req.VersionConsentimiento, req.Muestras), ct), Results.Ok))
            .RequireRateLimiting("Reautenticacion")
            .WithMetadata(new RequestSizeLimitAttribute(TamanoRegistro));

        rostro.MapPost("/equipo", async (AutorizarEquipoRequest req, ClaimsPrincipal user, ServicioRostros servicio, CancellationToken ct) =>
                Responder(await servicio.AutorizarEquipoAsync(user.IdUsuario(), req.Password, ct), Results.Ok))
            .RequireRateLimiting("Reautenticacion");

        // «Rostros registrados» (S16). Listar y registrar son de gestion; desactivar, de gestion o del titular.
        var rostros = app.MapGroup("/api/v1/rostros").WithTags("Rostros registrados").RequireAuthorization(Politicas.Empleado);

        rostros.MapGet("/", async (ServicioRostros servicio, CancellationToken ct) => Results.Ok(await servicio.ListarAsync(ct)))
            .RequireAuthorization(Politicas.Gestion)
            .RequireRateLimiting("ApiGeneral");

        rostros.MapPut("/{idEmpleado:int}", async (int idEmpleado, RegistrarRostroTitularRequest req, ClaimsPrincipal user, ServicioRostros servicio,
                CancellationToken ct) =>
                Responder(await servicio.RegistrarParaOtroAsync(user.IdUsuario(), idEmpleado,
                    new RegistroRostro(req.PasswordTitular, req.Consentimiento, req.VersionConsentimiento, req.Muestras), ct), Results.Ok))
            .RequireAuthorization(Politicas.Gestion)
            .RequireRateLimiting("Reautenticacion")
            .WithMetadata(new RequestSizeLimitAttribute(TamanoRegistro));

        rostros.MapDelete("/{idEmpleado:int}", async (int idEmpleado, ClaimsPrincipal user, ServicioRostros servicio, CancellationToken ct) =>
            {
                var quien = user.IdUsuario();
                if (quien != idEmpleado && !await servicio.EsGestionActivoAsync(quien, ct))
                    return Results.Forbid();
                await servicio.DesactivarAsync(idEmpleado, quien, ct);
                return Results.NoContent();
            })
            .RequireRateLimiting("ApiGeneral");

        // Calibracion (decision 3): no existe mientras RostroFacial:Calibracion:Activa no sea true.
        var calibracion = rostros.MapGroup("/calibracion")
            .RequireAuthorization(Politicas.Gestion)
            .RequireRateLimiting("ApiGeneral")
            .AddEndpointFilter(async (contexto, siguiente) =>
                contexto.HttpContext.RequestServices.GetRequiredService<ServicioCalibracionRostro>().Activa
                    ? await siguiente(contexto)
                    : Results.NotFound());

        calibracion.MapPost("/", async (MedicionCalibracionRequest req, ClaimsPrincipal user, ServicioCalibracionRostro servicio, CancellationToken ct) =>
                Responder(await servicio.MedirAsync(user.IdUsuario(), new PedidoMedicion(req.IdEmpleado, req.Etiqueta, req.Desafio, req.Capturas), ct),
                    m => Results.Created($"/api/v1/rostros/calibracion/{m.IdMedicion}", m)))
            .WithMetadata(new RequestSizeLimitAttribute(TamanoLogin));

        calibracion.MapGet("/", async (ServicioCalibracionRostro servicio, CancellationToken ct) => Results.Ok(await servicio.ConsultarAsync(ct)));

        calibracion.MapDelete("/", async (ClaimsPrincipal user, ServicioCalibracionRostro servicio, CancellationToken ct) =>
        {
            await servicio.BorrarAsync(user.IdUsuario(), ct);
            return Results.NoContent();
        });
    }

    // Siempre 200 con la misma forma, exista o no el email: la respuesta no revela cuentas.
    private static async Task<IResult> PedirDesafioAsync(DesafioRequest req, RostroFacialService rostros, ServicioRostros servicio, CancellationToken ct)
    {
        if (!rostros.Configurado)
            return NoDisponible();
        // Solo el formato, que no depende de que la cuenta exista.
        if (ServicioRostros.NormalizarCorreo(req.Email) is not { } correo)
            return ErrorApi.Invalido("Escribe tu correo.", new Dictionary<string, string[]> { ["email"] = ["Escribe tu correo."] });

        var desafio = await servicio.EmitirDesafioAsync(correo, ct);
        return Results.Ok(new { desafio = desafio.Token, orden = OrdenGiros.ParaElNavegador(desafio.Orden), venceEnSegundos = desafio.Segundos });
    }

    private static async Task<IResult> IniciarSesionAsync(
        LoginFacialRequest req, HttpContext contexto, RostroFacialService rostros, ServicioRostros servicio, AuthService auth,
        ServicioSesiones sesiones, CancellationToken ct)
    {
        if (!rostros.Configurado)
            return NoDisponible();

        // Aqui no se usa el limite por cuenta de los ingresos con contraseña: contaria tambien los intentos
        // sin equipo autorizado, y un anonimo podria bloquear el rostro de otra persona. Este ingreso se
        // protege con el limite por IP de la ruta y con el bloqueo propio del rostro.
        var inicio = Stopwatch.GetTimestamp();
        var empleado = await servicio.IngresarAsync(new IngresoFacial(req.Email, req.Desafio, req.TokenEquipo, req.Capturas), ct);
        if (empleado is not null)
            // Mismo tipo de sesion que con la contraseña, mas el nombre y el rol de quien entro (S15).
            return await AuthApi.SesionEmpleadoAsync(contexto, empleado, auth, sesiones, ct, conNombreYRol: true);

        // Una sola salida para todo fallo: no revela si el email existe, si tiene rostro ni por que no entro.
        await rostros.EsperarTiempoMinimoAsync(inicio, ct);
        return Results.Unauthorized();
    }

    private static IResult Responder<T>(ResultadoRostro<T> resultado, Func<T, IResult> correcta) => resultado switch
    {
        ResultadoRostro<T>.Correcto { Valor: var valor } => correcta(valor),
        ResultadoRostro<T>.Invalido { Errores: var errores, Mensaje: var mensaje } => ErrorApi.Invalido(mensaje, errores.Count > 0 ? errores : null),
        ResultadoRostro<T>.SoloGestion => ErrorApi.Crear(StatusCodes.Status403Forbidden, "solo_gestion", ServicioRostros.MensajeSoloGestion),
        ResultadoRostro<T>.NoDisponible => NoDisponible(),
        ResultadoRostro<T>.DemasiadosIntentos => ErrorApi.DemasiadosIntentos(),
        ResultadoRostro<T>.ReautenticacionFallida { Campo: var campo, Mensaje: var mensaje } =>
            ErrorApi.Invalido(mensaje, new Dictionary<string, string[]> { [campo] = [mensaje] }),
        _ => Results.NotFound()
    };

    // No depende del email, asi que no revela nada de ninguna cuenta.
    private static IResult NoDisponible() =>
        ErrorApi.NoDisponible("El acceso con rostro no esta disponible. Use su contraseña.");
}

public record DesafioRequest(string? Email);
public record LoginFacialRequest(string? Email, string? Desafio, string? TokenEquipo, CapturaRostro?[]? Capturas);
public record RegistrarRostroRequest(string? Password, bool? Consentimiento, string? VersionConsentimiento, MuestrasRostro? Muestras);
public record RegistrarRostroTitularRequest(string? PasswordTitular, bool? Consentimiento, string? VersionConsentimiento, MuestrasRostro? Muestras);
public record AutorizarEquipoRequest(string? Password);
public record MedicionCalibracionRequest(int? IdEmpleado, string? Etiqueta, string? Desafio, CapturaRostro?[]? Capturas);
