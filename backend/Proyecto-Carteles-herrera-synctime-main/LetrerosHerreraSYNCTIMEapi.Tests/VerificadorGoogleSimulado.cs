using System.Collections.Concurrent;
using LetrerosHerreraSYNCTIMEapi.Negocio.IngresoGoogle;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 14A: el verificador de Google de las pruebas. Nunca llama a Google: cada «token» es un texto inventado que apunta a lo
// que el verificador real habria devuelto (los datos del token, un rechazo de la firma, o que Google no responde).
// Un token que no se registro es una firma que no es de Google.
public sealed class VerificadorGoogleSimulado : IVerificadorGoogle
{
    private readonly ConcurrentDictionary<string, Func<CancellationToken, Task<ResultadoVerificadorGoogle>>> tokens = new();
    private int llamadas;

    public int Llamadas => Volatile.Read(ref llamadas);

    // Los datos de un token valido de Google para este intento (el nonce es el que entrego la API).
    public static ClaimsGoogle Claims(string nonce, string sub, string correo, string nombre = "Ana", string apellido = "Pérez") =>
        new("https://accounts.google.com", [ApiFixture.GoogleClientId], sub, correo, true, nombre, apellido, $"{nombre} {apellido}", nonce,
            DateTimeOffset.UtcNow.AddMinutes(30));

    public string Registrar(ClaimsGoogle claims) => Agregar(_ => Task.FromResult<ResultadoVerificadorGoogle>(new ResultadoVerificadorGoogle.Verificado(claims)));

    public string RegistrarGoogleNoDisponible() => Agregar(_ => Task.FromResult<ResultadoVerificadorGoogle>(new ResultadoVerificadorGoogle.NoDisponible()));

    public string RegistrarFallo(Exception error) => Agregar(_ => Task.FromException<ResultadoVerificadorGoogle>(error));

    // Google que tarda: no responde hasta pasado el tiempo (o hasta que se cancele la espera).
    public string RegistrarLento(TimeSpan espera, ClaimsGoogle claims) => Agregar(async ct =>
    {
        await Task.Delay(espera, ct);
        return new ResultadoVerificadorGoogle.Verificado(claims);
    });

    // Un verificador que ignora la cancelacion (como una descarga colgada): solo el limite de espera de la API lo corta.
    public string RegistrarSinCancelacion(ClaimsGoogle claims) => Agregar(async _ =>
    {
        await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None);
        return new ResultadoVerificadorGoogle.Verificado(claims);
    });

    private string Agregar(Func<CancellationToken, Task<ResultadoVerificadorGoogle>> respuesta)
    {
        var token = $"token-simulado-{Guid.NewGuid():N}";
        tokens[token] = respuesta;
        return token;
    }

    public Task<ResultadoVerificadorGoogle> VerificarAsync(string idToken, string clientId, CancellationToken ct)
    {
        Interlocked.Increment(ref llamadas);
        return tokens.TryGetValue(idToken, out var respuesta)
            ? respuesta(ct)
            : Task.FromResult<ResultadoVerificadorGoogle>(new ResultadoVerificadorGoogle.Rechazado());
    }
}
