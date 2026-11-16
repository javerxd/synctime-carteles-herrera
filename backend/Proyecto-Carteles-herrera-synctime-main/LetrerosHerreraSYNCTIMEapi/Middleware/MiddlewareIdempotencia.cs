using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Middleware;

// Marca una ruta de creacion que acepta Idempotency-Key. El ambito es la ruta (con sus parametros sin reemplazar).
public sealed record Idempotente(string Ambito);

public static class IdempotenciaExtensions
{
    public static RouteHandlerBuilder ConIdempotencia(this RouteHandlerBuilder ruta, string ambito) => ruta.WithMetadata(new Idempotente(ambito));
}

// Fase 11, seccion 7. Si la peticion trae Idempotency-Key (un UUID que la pantalla genera al abrir el formulario):
//   - la misma clave con el mismo cuerpo devuelve la respuesta original, con Idempotent-Replayed: true, sin crear otro;
//   - con otro cuerpo (o en otra ruta concreta, por ejemplo otro pedido), 409 clave_reutilizada;
//   - si la primera aun no termina, se espera unos segundos y, si sigue, 409 en_proceso;
//   - una respuesta de error no se guarda y libera la clave.
// Sin la cabecera la ruta se comporta como siempre. La clave es de cada usuario y dura 24 horas.
public sealed class MiddlewareIdempotencia(RequestDelegate siguiente, ILogger<MiddlewareIdempotencia> logger)
{
    public const string Cabecera = "Idempotency-Key";
    public const string CabeceraRepetida = "Idempotent-Replayed";

    // Los cuerpos de estas rutas son JSON pequeños (las fotos se suben aparte): la huella no lee mas que esto.
    public const long MaximoCuerpo = 256 * 1024;

    public async Task InvokeAsync(HttpContext http, ServicioIdempotencia servicio)
    {
        var marca = http.GetEndpoint()?.Metadata.GetMetadata<Idempotente>();
        if (marca is null || !http.Request.Headers.TryGetValue(Cabecera, out var valores) || !(http.User.Identity?.IsAuthenticated ?? false))
        {
            await siguiente(http);
            return;
        }

        var clave = valores.ToString().Trim();
        if (valores.Count != 1 || clave.Length != 36 || !Guid.TryParseExact(clave, "D", out _))
        {
            await ErrorApi.Crear(StatusCodes.Status400BadRequest, "clave_invalida",
                "La cabecera Idempotency-Key debe ser un UUID (por ejemplo, el que genera crypto.randomUUID()).").ExecuteAsync(http);
            return;
        }
        var tipoUsuario = http.User.IsInRole(Roles.Cliente) ? "cliente" : "empleado";
        var idUsuario = http.User.IdUsuario();
        if (http.Request.ContentLength > MaximoCuerpo)
        {
            await ErrorApi.Crear(StatusCodes.Status413PayloadTooLarge, "cuerpo_muy_grande", "Los datos enviados son demasiado grandes.").ExecuteAsync(http);
            return;
        }
        if (http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limite)
            limite.MaxRequestBodySize = MaximoCuerpo;
        var hash = await HuellaAsync(http.Request);

        var reserva = await servicio.ReservarAsync(clave.ToLowerInvariant(), marca.Ambito, tipoUsuario, idUsuario, hash, http.RequestAborted);
        switch (reserva)
        {
            case ReservaIdempotencia.Reutilizada:
                await ErrorApi.Crear(StatusCodes.Status409Conflict, "clave_reutilizada",
                    "Esta clave ya se usó con otros datos. Vuelve a abrir el formulario para enviar uno nuevo.").ExecuteAsync(http);
                return;
            case ReservaIdempotencia.EnProceso:
                await ErrorApi.Crear(StatusCodes.Status409Conflict, "en_proceso",
                    "El mismo envío se está procesando. Espera un momento y vuelve a cargar para ver el resultado.").ExecuteAsync(http);
                return;
            case ReservaIdempotencia.Repetida repetida:
                http.Response.StatusCode = repetida.Estado;
                http.Response.Headers[CabeceraRepetida] = "true";
                http.Response.ContentType = "application/json; charset=utf-8";
                await http.Response.WriteAsync(repetida.Respuesta, Encoding.UTF8);
                return;
        }

        var idClave = ((ReservaIdempotencia.Nueva)reserva).IdClave;
        var original = http.Response.Body;
        await using var copia = new MemoryStream();
        http.Response.Body = copia;
        try
        {
            await siguiente(http);
        }
        catch
        {
            http.Response.Body = original;
            await LiberarSinFallarAsync(servicio, idClave);
            throw;
        }
        http.Response.Body = original;

        // Lo creado ya esta guardado: si la clave no se puede actualizar, igual se entrega la respuesta.
        var estado = http.Response.StatusCode;
        try
        {
            if (estado is >= 200 and < 300)
                await servicio.GuardarAsync(idClave, estado, Encoding.UTF8.GetString(copia.ToArray()));
            else
                await servicio.LiberarAsync(idClave);
        }
        catch (Exception error)
        {
            logger.LogError("No se pudo actualizar la clave de idempotencia {IdClave} ({Estado}): [{TipoError}]", idClave, estado, error.GetType().Name);
        }
        copia.Position = 0;
        await copia.CopyToAsync(original, CancellationToken.None);
    }

    private async Task LiberarSinFallarAsync(ServicioIdempotencia servicio, long idClave)
    {
        try
        {
            await servicio.LiberarAsync(idClave);
        }
        catch (Exception error)
        {
            logger.LogError("No se pudo liberar la clave de idempotencia {IdClave}: [{TipoError}]", idClave, error.GetType().Name);
        }
    }

    // SHA-256 del metodo, la ruta concreta y el cuerpo tal como llego.
    private static async Task<string> HuellaAsync(HttpRequest peticion)
    {
        peticion.EnableBuffering();
        using var memoria = new MemoryStream();
        await peticion.Body.CopyToAsync(memoria, peticion.HttpContext.RequestAborted);
        peticion.Body.Position = 0;
        var cabecera = Encoding.UTF8.GetBytes($"{peticion.Method}\n{peticion.Path.Value}\n");
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(cabecera);
        sha.AppendData(memoria.GetBuffer(), 0, (int)memoria.Length);
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }
}
