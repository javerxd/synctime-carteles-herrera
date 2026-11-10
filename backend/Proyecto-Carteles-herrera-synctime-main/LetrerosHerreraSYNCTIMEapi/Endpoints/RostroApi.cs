using System.Diagnostics;
using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Login facial solo para empleados: complementa a email y contraseña, no los reemplaza.
public static class RostroApi
{
    // Un descriptor en JSON ocupa unos 3 KB; estos topes evitan procesar cuerpos enormes en rutas publicas.
    private const long TamanoLogin = 8 * 1024;
    private const long TamanoActivacion = 64 * 1024;

    public static void MapRostroApi(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Login facial");

        auth.MapPost("/login-facial", IniciarSesionAsync)
            .AllowAnonymous()
            .RequireRateLimiting("LoginFacial")
            .WithMetadata(new RequestSizeLimitAttribute(TamanoLogin));

        var rostro = auth.MapGroup("/rostro").RequireAuthorization(Politicas.Empleado);

        rostro.MapGet("/", ConsultarAsync).RequireRateLimiting("ApiGeneral");

        // Retirar el consentimiento no pide contraseña: debe ser tan expedito como sea posible.
        rostro.MapDelete("/", DesactivarAsync).RequireRateLimiting("ApiGeneral");

        rostro.MapPut("/", ActivarAsync)
            .RequireRateLimiting("Reautenticacion")
            .WithMetadata(new RequestSizeLimitAttribute(TamanoActivacion));

        rostro.MapPost("/equipo", AutorizarEquipoAsync).RequireRateLimiting("Reautenticacion");
    }

    private static async Task<IResult> IniciarSesionAsync(
        LoginFacialRequest req, LetrerosHerreradbContext db, AuthService auth, RostroFacialService rostros)
    {
        if (!rostros.Configurado)
            return NoDisponible();

        // El formato se revisa antes de consultar nada: este 400 no depende del email.
        if (string.IsNullOrWhiteSpace(req.Email) || !RostroFacialService.EsDescriptorValido(req.Descriptor))
            return Results.BadRequest(new { error = "Faltan el email o un descriptor valido." });

        var inicio = Stopwatch.GetTimestamp();
        var ahora = DateTimeOffset.UtcNow;
        await BorrarDatosDeInactivosAsync(db, ahora);

        var rostro = await db.RostrosEmpleado
            .AsNoTracking()
            .Include(r => r.Empleado)
            .FirstOrDefaultAsync(r => r.FechaRevocacion == null && r.Empleado!.Activo && r.Empleado.Email == req.Email);

        // La comparacion se hace siempre, tambien si no hay rostro o si esta bloqueado, para que todo fallo cueste lo mismo.
        var coincide = rostros.Coincide(req.Descriptor!, rostro?.DescriptorCifrado, rostro?.IdEmpleado ?? 0);

        // Sin un equipo valido no se cuenta el intento: asi un anonimo no puede bloquear el rostro del dueño.
        if (rostro is not null && rostros.EquipoAutorizado(rostro, rostro.Empleado!.Rol, req.TokenEquipo, ahora))
        {
            var permitido = await RegistrarIntentoAsync(db, rostro.IdRostro, ahora) == 1;
            if (permitido && coincide)
            {
                await LimpiarIntentosAsync(db, rostro.IdRostro);
                return AuthApi.RespuestaEmpleado(rostro.Empleado, auth);
            }
        }

        // Una sola salida para todo fallo: no revela si el email existe, si tiene rostro ni por que no entro.
        await rostros.EsperarTiempoMinimoAsync(inicio);
        return Results.Unauthorized();
    }

    private static async Task<IResult> ConsultarAsync(ClaimsPrincipal user, LetrerosHerreradbContext db, RostroFacialService rostros)
    {
        var id = user.IdUsuario();
        var rol = await db.Empleados.Where(e => e.IdEmpleado == id).Select(e => e.Rol).FirstAsync();
        var rostro = await db.RostrosEmpleado.AsNoTracking().FirstOrDefaultAsync(r => r.IdEmpleado == id && r.FechaRevocacion == null);

        return Results.Ok(new
        {
            disponible = rostros.Configurado,
            activo = rostro is not null,
            fechaConsentimiento = rostro?.FechaConsentimiento,
            requiereEquipo = RostroFacialService.RequiereEquipo(rol),
            equipoVence = rostro is null ? null : rostros.VencimientoEquipo(rostro),
            equipoVigente = rostro is not null && rostros.VencimientoEquipo(rostro) > DateTimeOffset.UtcNow,
            consentimiento = new { version = ConsentimientoFacial.Version, texto = ConsentimientoFacial.Texto }
        });
    }

    private static async Task<IResult> ActivarAsync(
        ActivarRostroRequest req, ClaimsPrincipal user, LetrerosHerreradbContext db, AuthService auth,
        RostroFacialService rostros, ILogger<RostroFacialService> logger)
    {
        if (!rostros.Configurado)
            return NoDisponible();
        if (req.Consentimiento != true)
            return Results.BadRequest(new { error = "Debe aceptar el consentimiento para activar el acceso con rostro." });
        if (req.VersionConsentimiento != ConsentimientoFacial.Version)
            return Results.BadRequest(new { error = "El texto del consentimiento cambio. Vuelva a leerlo." });

        var promedio = rostros.Promediar(req.Descriptores);
        if (promedio is null)
            return Results.BadRequest(new { error = "Las capturas no son validas o no parecen de la misma persona. Intente de nuevo." });

        var empleado = await Reautenticacion.ConfirmarAsync(user, req.Password, db, auth);
        if (empleado is null)
            return Reautenticacion.Rechazo();

        var ahora = DateTimeOffset.UtcNow;
        var token = RostroFacialService.RequiereEquipo(empleado.Rol) ? RostroFacialService.CrearTokenEquipo() : null;

        // Volver a activar cierra la activacion anterior, que queda como constancia. Se guarda en dos pasos
        // porque el indice unico exige revocar la fila vigente antes de insertar la nueva.
        await using var transaccion = await db.Database.BeginTransactionAsync();
        var vigente = await db.RostrosEmpleado.FirstOrDefaultAsync(r => r.IdEmpleado == empleado.IdEmpleado && r.FechaRevocacion == null);
        if (vigente is not null)
        {
            Revocar(vigente, ahora);
            await db.SaveChangesAsync();
        }

        var rostro = new RostroEmpleado
        {
            IdEmpleado = empleado.IdEmpleado,
            DescriptorCifrado = rostros.Cifrar(promedio, empleado.IdEmpleado),
            FechaConsentimiento = ahora,
            VersionConsentimiento = ConsentimientoFacial.Version,
            TokenEquipoHash = token is null ? null : RostroFacialService.HashToken(token),
            FechaAutorizacionEquipo = token is null ? null : ahora
        };
        db.RostrosEmpleado.Add(rostro);
        await db.SaveChangesAsync();
        await transaccion.CommitAsync();

        logger.LogInformation("Empleado {IdEmpleado} activo su acceso facial", empleado.IdEmpleado);
        return Results.Ok(new
        {
            activo = true,
            fechaConsentimiento = rostro.FechaConsentimiento,
            tokenEquipo = token,
            equipoVence = rostros.VencimientoEquipo(rostro)
        });
    }

    private static async Task<IResult> AutorizarEquipoAsync(
        AutorizarEquipoRequest req, ClaimsPrincipal user, LetrerosHerreradbContext db, AuthService auth,
        RostroFacialService rostros, ILogger<RostroFacialService> logger)
    {
        var empleado = await Reautenticacion.ConfirmarAsync(user, req.Password, db, auth);
        if (empleado is null)
            return Reautenticacion.Rechazo();
        if (!RostroFacialService.RequiereEquipo(empleado.Rol))
            return Results.BadRequest(new { error = "Su rol no necesita autorizar un equipo." });

        var rostro = await db.RostrosEmpleado.FirstOrDefaultAsync(r => r.IdEmpleado == empleado.IdEmpleado && r.FechaRevocacion == null);
        if (rostro is null)
            return Results.BadRequest(new { error = "Primero active el acceso con rostro." });

        var token = RostroFacialService.CrearTokenEquipo();
        rostro.TokenEquipoHash = RostroFacialService.HashToken(token);
        rostro.FechaAutorizacionEquipo = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Empleado {IdEmpleado} autorizo un equipo para su acceso facial", empleado.IdEmpleado);
        return Results.Ok(new { tokenEquipo = token, equipoVence = rostros.VencimientoEquipo(rostro) });
    }

    private static async Task<IResult> DesactivarAsync(ClaimsPrincipal user, LetrerosHerreradbContext db, ILogger<RostroFacialService> logger)
    {
        var id = user.IdUsuario();
        var ahora = DateTimeOffset.UtcNow;
        var afectadas = await db.RostrosEmpleado
            .Where(r => r.IdEmpleado == id && r.FechaRevocacion == null)
            .ExecuteUpdateAsync(Borrado(ahora));

        if (afectadas > 0)
            logger.LogInformation("Empleado {IdEmpleado} retiro su acceso facial", id);
        return Results.NoContent();
    }

    // No existe una ruta para desactivar empleados: se hace por SQL. Por eso los datos de un inactivo se
    // borran en el siguiente intento de cualquiera. Su rostro deja de aceptarse antes, en la consulta del login.
    private static Task<int> BorrarDatosDeInactivosAsync(LetrerosHerreradbContext db, DateTimeOffset ahora) =>
        db.RostrosEmpleado
            .Where(r => r.FechaRevocacion == null && !r.Empleado!.Activo)
            .ExecuteUpdateAsync(Borrado(ahora));

    private static Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<RostroEmpleado>> Borrado(DateTimeOffset ahora) => setters => setters
        .SetProperty(r => r.DescriptorCifrado, (byte[]?)null)
        .SetProperty(r => r.TokenEquipoHash, (byte[]?)null)
        .SetProperty(r => r.FechaAutorizacionEquipo, (DateTimeOffset?)null)
        .SetProperty(r => r.FechaRevocacion, ahora);

    private static void Revocar(RostroEmpleado rostro, DateTimeOffset ahora)
    {
        rostro.DescriptorCifrado = null;
        rostro.TokenEquipoHash = null;
        rostro.FechaAutorizacionEquipo = null;
        rostro.FechaRevocacion = ahora;
    }

    // El intento se cuenta antes de usar el resultado, en una sola sentencia que tambien revisa el bloqueo:
    // varias peticiones simultaneas no pueden probar mas veces que las permitidas. Devuelve 0 si esta bloqueado.
    private static Task<int> RegistrarIntentoAsync(LetrerosHerreradbContext db, int idRostro, DateTimeOffset ahora)
    {
        var hasta = ahora + RostroFacialService.DuracionBloqueo;
        return db.RostrosEmpleado
            .Where(r => r.IdRostro == idRostro && (r.BloqueadoHasta == null || r.BloqueadoHasta <= ahora))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.BloqueadoHasta, r => r.IntentosFallidos + 1 >= RostroFacialService.MaxFallos ? hasta : r.BloqueadoHasta)
                .SetProperty(r => r.IntentosFallidos, r => r.IntentosFallidos + 1 >= RostroFacialService.MaxFallos ? 0 : r.IntentosFallidos + 1));
    }

    private static Task<int> LimpiarIntentosAsync(LetrerosHerreradbContext db, int idRostro) =>
        db.RostrosEmpleado
            .Where(r => r.IdRostro == idRostro)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.IntentosFallidos, 0)
                .SetProperty(r => r.BloqueadoHasta, (DateTimeOffset?)null));

    // No depende del email, asi que no revela nada de ninguna cuenta.
    private static IResult NoDisponible() =>
        Results.Json(new { error = "El acceso con rostro no esta disponible. Use su contraseña." }, statusCode: StatusCodes.Status503ServiceUnavailable);
}

public record LoginFacialRequest(string? Email, float[]? Descriptor, string? TokenEquipo);
public record ActivarRostroRequest(string? Password, bool? Consentimiento, string? VersionConsentimiento, float[][]? Descriptores);
public record AutorizarEquipoRequest(string? Password);
