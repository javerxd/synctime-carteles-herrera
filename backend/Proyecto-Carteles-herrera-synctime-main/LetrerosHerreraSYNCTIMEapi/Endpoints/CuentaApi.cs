using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Mi cuenta (fase 6, RNFE01): el cliente ve y corrige sus datos, cambia su correo o su contraseña y
// decide sobre el marketing. Solo sus propios datos: el id sale siempre del token, nunca de la peticion.
public static class CuentaApi
{
    public static void MapCuentaApi(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/v1/mi-cuenta").WithTags("Mi cuenta")
            .RequireAuthorization(Politicas.Cliente)
            .RequireRateLimiting("ApiGeneral");

        grupo.MapGet("/", async (ClaimsPrincipal user, ServicioMiCuenta cuentas, CancellationToken ct) =>
            await cuentas.ObtenerAsync(user.IdUsuario(), ct) is { } cuenta ? Results.Ok(cuenta) : Results.NotFound());

        grupo.MapPut("/", async (PerfilRequest req, ClaimsPrincipal user, ServicioMiCuenta cuentas, CancellationToken ct) =>
            await ResponderAsync(await cuentas.ActualizarAsync(user.IdUsuario(), new DatosPerfil(req.Rut, req.Nombre, req.Apellido, req.Telefono, req.Direccion), ct),
                user, cuentas, ct));

        // Con un token robado no se puede probar contraseñas sin limite: tambien hay limite por cuenta.
        grupo.MapPut("/correo", async (CambiarCorreoRequest req, ClaimsPrincipal user, ServicioMiCuenta cuentas, LimitadorPorCuenta limite, CancellationToken ct) =>
        {
            var cuenta = $"cliente-clave:{user.IdUsuario()}";
            if (limite.EstaBloqueada(cuenta))
                return ErrorApi.DemasiadosIntentos();
            var resultado = await cuentas.CambiarCorreoAsync(user.IdUsuario(), req.Correo, req.PasswordActual, ct);
            if (resultado is ResultadoCuenta.Invalida { Errores: var errores } && errores.ContainsKey("passwordActual"))
                limite.RegistrarFallo(cuenta);
            return await ResponderAsync(resultado, user, cuentas, ct);
        })
        .RequireRateLimiting("Reautenticacion");

        // Cambiar la contraseña cierra todas las sesiones (tambien las de otros equipos) y abre una nueva aqui.
        grupo.MapPut("/password", async (CambiarPasswordRequest req, ClaimsPrincipal user, HttpContext contexto, ServicioMiCuenta cuentas,
            ServicioSesiones sesiones, AuthService auth, LimitadorPorCuenta limite, CancellationToken ct) =>
        {
            var id = user.IdUsuario();
            var cuenta = $"cliente-clave:{id}";
            if (limite.EstaBloqueada(cuenta))
                return ErrorApi.DemasiadosIntentos();
            var resultado = await cuentas.CambiarPasswordAsync(id, req.PasswordActual, req.PasswordNueva, ct);
            if (resultado.Errores is { } errores)
            {
                if (errores.ContainsKey("passwordActual"))
                    limite.RegistrarFallo(cuenta);
                return ErrorApi.Invalido(errores.Values.First()[0], errores);
            }
            limite.Olvidar(cuenta);
            var sesion = await sesiones.IniciarAsync(TitularSesion.DeCliente(id), ct);
            CookiesSesion.Escribir(contexto, sesion.TokenRenovacion, sesion.Expira);
            return Results.Ok(new { Token = auth.CreateTokenCliente(resultado.Cliente!) });
        })
        .RequireRateLimiting("Reautenticacion");

        // Fase 11: alias de PUT /preferencias, que la reemplaza.
        grupo.MapPut("/marketing", async (MarketingRequest req, ClaimsPrincipal user, ServicioMiCuenta cuentas, CancellationToken ct) =>
            await cuentas.MarketingAsync(user.IdUsuario(), req.Acepta, req.Version, ct) is { } error
                ? ErrorApi.Conflicto(error)
                : Results.NoContent());

        // Fase 11: los correos opcionales que recibe (avisos de estado y marketing). Los de la cuenta y la seguridad
        // llegan siempre. Responde la cuenta completa.
        grupo.MapPut("/preferencias", async (PreferenciasRequest req, ClaimsPrincipal user, ServicioMiCuenta cuentas, CancellationToken ct) =>
        {
            if (req.AvisosEstado is not bool avisos || req.Marketing is not bool marketing)
                return ErrorApi.Invalido("Indica avisosEstado y marketing (true o false).", new Dictionary<string, string[]>
                {
                    ["preferencias"] = ["Indica avisosEstado y marketing (true o false)."]
                });
            if (await cuentas.PreferenciasAsync(user.IdUsuario(), avisos, marketing, req.VersionMarketing, ct) is { } error)
                return ErrorApi.Conflicto(error);
            return await cuentas.ObtenerAsync(user.IdUsuario(), ct) is { } cuenta ? Results.Ok(cuenta) : Results.NotFound();
        })
        .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(4 * 1024));

        // Fase 11 (RFU34): «Mis datos». Las dos piden la contraseña, con el limite de las acciones delicadas y el
        // limite por cuenta (un token robado no sirve para probar contraseñas).
        grupo.MapPost("/datos", async (ConfirmarPasswordRequest req, ClaimsPrincipal user, HttpContext http, ServicioPrivacidad privacidad, LimitadorPorCuenta limite,
            CancellationToken ct) =>
        {
            var cuenta = $"cliente-clave:{user.IdUsuario()}";
            if (limite.EstaBloqueada(cuenta))
                return ErrorApi.DemasiadosIntentos();
            switch (await privacidad.DescargarAsync(user.IdUsuario(), req.Password, ct))
            {
                case ResultadoPrivacidad<ArchivoMisDatos>.Correcto { Valor: var archivo }:
                    limite.Olvidar(cuenta);
                    // Datos personales: que ningun proxy ni el navegador los guarden.
                    http.Response.Headers.CacheControl = "private, no-store";
                    return Results.File(archivo.Contenido, "application/json", archivo.Nombre);
                case ResultadoPrivacidad<ArchivoMisDatos>.Invalido { Errores: var errores, Mensaje: var mensaje }:
                    limite.RegistrarFallo(cuenta);
                    return ErrorApi.Invalido(mensaje, errores);
                default:
                    return Results.NotFound();
            }
        })
        .RequireRateLimiting("Reautenticacion")
        .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(4 * 1024));

        grupo.MapPost("/baja", async (BajaCuentaRequest req, ClaimsPrincipal user, HttpContext contexto, ServicioPrivacidad privacidad,
            LimitadorPorCuenta limite, CancellationToken ct) =>
        {
            var cuenta = $"cliente-clave:{user.IdUsuario()}";
            if (limite.EstaBloqueada(cuenta))
                return ErrorApi.DemasiadosIntentos();
            switch (await privacidad.DarDeBajaAsync(user.IdUsuario(), req.Password, req.Confirmacion, ct))
            {
                case ResultadoPrivacidad<ComprobanteBaja>.Correcto { Valor: var comprobante }:
                    limite.Olvidar(cuenta);
                    // La sesion de este navegador tambien se cierra (sus tokens de renovacion ya no existen).
                    CookiesSesion.Borrar(contexto);
                    contexto.Response.Headers.CacheControl = "private, no-store";
                    return Results.Ok(new { Comprobante = comprobante });
                case ResultadoPrivacidad<ComprobanteBaja>.Invalido { Errores: var errores, Mensaje: var mensaje }:
                    if (errores.ContainsKey("password"))
                        limite.RegistrarFallo(cuenta);
                    return ErrorApi.Invalido(mensaje, errores);
                default:
                    return Results.NotFound();
            }
        })
        .RequireRateLimiting("Reautenticacion")
        .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(4 * 1024));
    }

    private static async Task<IResult> ResponderAsync(ResultadoCuenta resultado, ClaimsPrincipal user, ServicioMiCuenta cuentas, CancellationToken ct) => resultado switch
    {
        ResultadoCuenta.Lista => Results.Ok(await cuentas.ObtenerAsync(user.IdUsuario(), ct)),
        ResultadoCuenta.Invalida invalida => ErrorApi.Invalido("Revisa los datos del formulario.", invalida.Errores),
        ResultadoCuenta.Conflicto conflicto => ErrorApi.Conflicto(conflicto.Mensaje),
        _ => Results.NotFound()
    };
}

public record PerfilRequest(string? Rut, string? Nombre, string? Apellido, string? Telefono, string? Direccion);
public record CambiarCorreoRequest(string? Correo, string? PasswordActual);
public record CambiarPasswordRequest(string? PasswordActual, string? PasswordNueva);
public record MarketingRequest(bool Acepta, string? Version);
public record PreferenciasRequest(bool? AvisosEstado, bool? Marketing, string? VersionMarketing = null);
public record ConfirmarPasswordRequest(string? Password);
public record BajaCuentaRequest(string? Password, string? Confirmacion);
