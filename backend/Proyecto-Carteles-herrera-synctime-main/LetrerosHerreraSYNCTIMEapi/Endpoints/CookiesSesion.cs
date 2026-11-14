using System.Security.Cryptography;
using System.Text;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Cookies de la sesion (fase 6):
// - synctime_renovacion: el token de renovacion. httpOnly (el JavaScript de la pagina no lo lee), Secure,
//   SameSite=Strict y solo para /api/v1/auth: no viaja en ninguna otra peticion.
// - synctime_csrf: un valor aleatorio que la pagina SI lee y repite en la cabecera X-CSRF-Token al renovar
//   o salir (doble envio). Un sitio ajeno no puede leerlo, asi que no puede falsificar esas peticiones.
public static class CookiesSesion
{
    public const string Renovacion = "synctime_renovacion";
    public const string Csrf = "synctime_csrf";
    public const string CabeceraCsrf = "X-CSRF-Token";
    private const string RutaRenovacion = "/api/v1/auth";

    public static void Escribir(HttpContext contexto, string tokenRenovacion, DateTimeOffset expira)
    {
        contexto.Response.Cookies.Append(Renovacion, tokenRenovacion, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = RutaRenovacion, Expires = expira, IsEssential = true
        });
        contexto.Response.Cookies.Append(Csrf, Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)), new CookieOptions
        {
            HttpOnly = false, Secure = true, SameSite = SameSiteMode.Strict, Path = "/", Expires = expira, IsEssential = true
        });
    }

    public static void Borrar(HttpContext contexto)
    {
        contexto.Response.Cookies.Delete(Renovacion, new CookieOptions { Secure = true, SameSite = SameSiteMode.Strict, Path = RutaRenovacion });
        contexto.Response.Cookies.Delete(Csrf, new CookieOptions { Secure = true, SameSite = SameSiteMode.Strict, Path = "/" });
    }

    public static string? Leer(HttpContext contexto) => contexto.Request.Cookies[Renovacion];

    public static bool CsrfValido(HttpContext contexto)
    {
        var cookie = contexto.Request.Cookies[Csrf];
        var cabecera = contexto.Request.Headers[CabeceraCsrf].ToString();
        // Solo el formato que emite el servidor (32 hexadecimales): asi la comparacion ASCII es exacta.
        return cookie is { Length: 32 } && cookie.All(char.IsAsciiHexDigitLower) && cookie.Length == cabecera.Length
            && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(cookie), Encoding.ASCII.GetBytes(cabecera));
    }
}
