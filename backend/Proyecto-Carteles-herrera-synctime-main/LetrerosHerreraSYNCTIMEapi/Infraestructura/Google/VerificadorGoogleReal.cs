using Google.Apis.Auth;
using LetrerosHerreraSYNCTIMEapi.Negocio.IngresoGoogle;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Google;

// Fase 14A (RFS03, RNFP17): configuracion del ingreso con Google. El ID de cliente NO es un secreto (cualquiera lo ve en el
// navegador), pero se trata como configuracion y no vive en el repositorio: User Secrets en desarrollo (Google:ClientId) y
// una variable de entorno en produccion (Google__ClientId). Este flujo (token de identidad) no usa «clave secreta de cliente».
public sealed class OpcionesGoogle
{
    public const string Seccion = "Google";
    // Lo maximo que la API espera a Google; mas alla, responde que no esta disponible (el configurado nunca pasa de aqui).
    public const int TopeSegundos = 5;

    public string? ClientId { get; set; }
    public int SegundosLimite { get; set; } = TopeSegundos;

    public bool Configurado => !string.IsNullOrWhiteSpace(ClientId);
    public TimeSpan Limite => TimeSpan.FromSeconds(Math.Clamp(SegundosLimite, 1, TopeSegundos));
}

// Verifica la firma del token con las claves publicas de Google (Google.Apis.Auth, Apache-2.0, que las descarga y mantiene al dia).
// La libreria comprueba firma, emisor, audiencia y vencimiento; ReglasGoogle vuelve a comprobar lo que decide el negocio.
// Un token que la libreria rechaza es «Rechazado»; cualquier otro fallo (red, tiempo, servicio) es «NoDisponible». El texto del
// error nunca se guarda: puede incluir parte del token.
public sealed class VerificadorGoogleReal(ILogger<VerificadorGoogleReal> logger, IOptions<OpcionesGoogle> opciones) : IVerificadorGoogle
{
    // La descarga de las claves de Google usa este cliente HTTP, con el mismo tiempo maximo que la espera de la API: una descarga
    // colgada se corta de verdad y no queda trabajando por detras (el cliente por defecto espera 100 segundos).
    private sealed class FabricaConLimite(TimeSpan limite) : global::Google.Apis.Http.IHttpClientFactory
    {
        private readonly global::Google.Apis.Http.HttpClientFactory interna = new();

        public global::Google.Apis.Http.ConfigurableHttpClient CreateHttpClient(global::Google.Apis.Http.CreateHttpClientArgs args)
        {
            var cliente = interna.CreateHttpClient(args);
            cliente.Timeout = limite;
            return cliente;
        }
    }

    // Un JWT son tres partes en base64url; las dos primeras (cabecera y carga) son JSON. Lo que no tiene esa forma se rechaza aqui,
    // sin llamar a Google ni confundirlo con una caida del servicio.
    internal static bool TieneFormaDeJwt(string? idToken)
    {
        var partes = (idToken ?? "").Split('.');
        if (partes.Length != 3 || partes.Any(p => p.Length == 0))
            return false;
        try
        {
            foreach (var parte in partes.Take(2))
            {
                using var json = System.Text.Json.JsonDocument.Parse(System.Buffers.Text.Base64Url.DecodeFromChars(parte));
                if (json.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                    return false;
            }
            _ = System.Buffers.Text.Base64Url.DecodeFromChars(partes[2]);
            return true;
        }
        catch (Exception error) when (error is FormatException or System.Text.Json.JsonException or ArgumentException)
        {
            return false;
        }
    }

    public async Task<ResultadoVerificadorGoogle> VerificarAsync(string idToken, string clientId, CancellationToken ct)
    {
        if (!TieneFormaDeJwt(idToken))
            return new ResultadoVerificadorGoogle.Rechazado();
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId],
                HttpClientFactory = new FabricaConLimite(opciones.Value.Limite)
            }).WaitAsync(ct);
            return new ResultadoVerificadorGoogle.Verificado(new ClaimsGoogle(
                payload.Issuer, payload.AudienceAsList?.ToList() ?? [], payload.Subject, payload.Email, payload.EmailVerified,
                payload.GivenName, payload.FamilyName, payload.Name, payload.Nonce,
                payload.ExpirationTimeSeconds is { } exp ? DateTimeOffset.FromUnixTimeSeconds(exp) : null));
        }
        catch (InvalidJwtException)
        {
            // Mal formado, firma que no es de Google, vencido o para otra aplicacion.
            return new ResultadoVerificadorGoogle.Rechazado();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("No se pudo consultar a Google para verificar un token ({Tipo}).", error.GetType().Name);
            return new ResultadoVerificadorGoogle.NoDisponible();
        }
    }
}
