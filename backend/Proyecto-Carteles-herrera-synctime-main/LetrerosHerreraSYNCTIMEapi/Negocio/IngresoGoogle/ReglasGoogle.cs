using System.Security.Cryptography;
using System.Text;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.IngresoGoogle;

// Fase 14A (RFS03, RNFE02, RNFP17): el ingreso con Google usa el token de identidad (un JWT firmado por Google) que el
// navegador entrega a la API. Aqui viven las reglas puras: que condiciones debe cumplir el token, que camino sigue la
// persona y cuando se puede vincular o desvincular. No conoce HTTP ni la base: lo prueba IngresoGoogleTests sin tocar a Google.

// Lo que dice el token, ya con la firma comprobada por quien lo verifica (IVerificadorGoogle). Las condiciones del
// negocio (emisor, audiencia, vencimiento, correo verificado, nonce) se vuelven a comprobar aqui, a proposito: la
// libreria de Google verifica la firma, pero que el token sea para ESTA aplicacion y para ESTE intento lo decidimos nosotros.
public sealed record ClaimsGoogle(
    string? Emisor, IReadOnlyList<string> Audiencias, string? Sub, string? Correo, bool CorreoVerificado,
    string? Nombre, string? Apellido, string? NombreCompleto, string? Nonce, DateTimeOffset? Expira);

public abstract record ResultadoVerificadorGoogle
{
    public sealed record Verificado(ClaimsGoogle Claims) : ResultadoVerificadorGoogle;
    // La firma no es de Google, el token esta mal formado o no es para esta aplicacion.
    public sealed record Rechazado : ResultadoVerificadorGoogle;
    // No se pudo consultar a Google (red, tiempo agotado, error del servicio): no es culpa del token.
    public sealed record NoDisponible : ResultadoVerificadorGoogle;
}

// Verifica la firma del token con las claves publicas de Google. La implementacion real usa Google.Apis.Auth
// (Infraestructura/Google); las pruebas usan un verificador simulado y nunca llaman a Google.
public interface IVerificadorGoogle
{
    Task<ResultadoVerificadorGoogle> VerificarAsync(string idToken, string clientId, CancellationToken ct);
}

public sealed record IdentidadGoogle(string Sub, string Correo, string Nombre, string Apellido);

// Por que se rechazo un token. Solo sirve para pruebas y para el registro interno: la respuesta a quien llama es siempre la misma.
public enum MotivoRechazoGoogle { Emisor, Audiencia, Vencido, CorreoSinVerificar, SinIdentidad, SinCorreo, Nonce }

public sealed record ValidacionGoogle(IdentidadGoogle? Identidad, MotivoRechazoGoogle? Motivo);

public enum CaminoGoogle { IniciarSesion, CuentaExistente, RegistroPendiente, Rechazar }

public static class ReglasGoogle
{
    public const string Proveedor = "google";
    public const int LargoMaximoSub = 255;

    private static readonly string[] Emisores = ["accounts.google.com", "https://accounts.google.com"];

    public static string NormalizarCorreo(string? correo) => (correo ?? "").Trim().ToLowerInvariant();

    public static ValidacionGoogle Validar(ClaimsGoogle claims, string clientId, string? nonceEsperado, DateTimeOffset ahora)
    {
        static ValidacionGoogle No(MotivoRechazoGoogle motivo) => new(null, motivo);

        if (claims.Emisor is null || !Emisores.Contains(claims.Emisor, StringComparer.Ordinal))
            return No(MotivoRechazoGoogle.Emisor);
        // Un token emitido para otra aplicacion de Google no vale aqui, aunque la firma sea autentica.
        if (string.IsNullOrEmpty(clientId) || !claims.Audiencias.Contains(clientId, StringComparer.Ordinal))
            return No(MotivoRechazoGoogle.Audiencia);
        if (claims.Expira is not { } expira || expira <= ahora)
            return No(MotivoRechazoGoogle.Vencido);
        // Un correo sin verificar puede ser de cualquiera: con el se podria tomar la cuenta de otra persona.
        if (!claims.CorreoVerificado)
            return No(MotivoRechazoGoogle.CorreoSinVerificar);
        // La identidad es el sub (estable), nunca el correo (puede cambiar).
        // El sub de Google es texto ASCII (en la practica, solo digitos). Se exige ASCII alfanumerico: la columna es varchar y su
        // collation distingue mayusculas y minusculas, asi que ningun sub raro puede chocar con otro.
        if (string.IsNullOrWhiteSpace(claims.Sub) || claims.Sub.Length > LargoMaximoSub || !claims.Sub.Trim().All(char.IsAsciiLetterOrDigit))
            return No(MotivoRechazoGoogle.SinIdentidad);
        var correo = NormalizarCorreo(claims.Correo);
        // El correo de la cuenta mide a lo mas lo que admite clientes.email: uno mas largo no se podria guardar ni corregir en el formulario.
        if (!Reglas.EsCorreo(correo) || correo.Length > ValidacionCuenta.LargoCorreo)
            return No(MotivoRechazoGoogle.SinCorreo);
        if (!NonceIgual(claims.Nonce, nonceEsperado))
            return No(MotivoRechazoGoogle.Nonce);

        var nombre = (claims.Nombre ?? "").Trim();
        var apellido = (claims.Apellido ?? "").Trim();
        if (nombre.Length == 0)
        {
            // Sin nombre de pila: el nombre completo va en «nombre» y la persona escribe su apellido al completar el registro.
            nombre = (claims.NombreCompleto ?? "").Trim();
            apellido = "";
        }
        return new ValidacionGoogle(new IdentidadGoogle(claims.Sub.Trim(), correo, nombre, apellido), null);
    }

    private static bool NonceIgual(string? delToken, string? esperado)
    {
        if (string.IsNullOrEmpty(delToken) || string.IsNullOrEmpty(esperado))
            return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(delToken), Encoding.UTF8.GetBytes(esperado));
    }

    // Los tres caminos de POST /auth/google. Un correo que ya tiene cuenta NUNCA se fusiona solo: la persona
    // ingresa con su contraseña y vincula Google desde Mi cuenta.
    public static CaminoGoogle Decidir(bool identidadVinculada, bool cuentaHabilitada, bool correoTieneCuenta)
    {
        if (identidadVinculada)
            return cuentaHabilitada ? CaminoGoogle.IniciarSesion : CaminoGoogle.Rechazar;
        return correoTieneCuenta ? CaminoGoogle.CuentaExistente : CaminoGoogle.RegistroPendiente;
    }

    // Una cuenta sin contraseña se quedaria sin forma de entrar si se le quita Google.
    public static bool PuedeDesvincular(bool tieneContrasena) => tieneContrasena;

    // Vincular exige que el correo verificado de Google sea el de la cuenta: asi quien tiene su propio Google
    // no puede «colgarse» de una cuenta ajena.
    public static bool MismoCorreo(string? deLaCuenta, string? deGoogle)
    {
        var cuenta = NormalizarCorreo(deLaCuenta);
        var google = NormalizarCorreo(deGoogle);
        return cuenta.Length > 0 && google.Length > 0 && string.Equals(cuenta, google, StringComparison.Ordinal);
    }
}
