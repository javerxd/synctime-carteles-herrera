using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;

// El proveedor respondio con un error. Solo guarda el codigo HTTP: el cuerpo de un error suele repetir el correo del destinatario o datos de
// la cuenta, y ese texto terminaria en la base (ultimo_error) o en los registros (ClasificadorFallos solo usa el codigo).
public sealed class FalloApiCorreoException(int estado) : Exception("El proveedor de correo rechazo el envio.")
{
    public int Estado { get; } = estado;
}

// Fase 16C-1 (H11, D8): envio real por HTTPS con la API transaccional de Brevo (https://developers.brevo.com/reference/send-transac-email).
// Render gratuito bloquea el SMTP (puertos 25, 465 y 587), asi que en la nube este enviador reemplaza a EnviadorSmtp detras de la misma interfaz.
//
// Lo que se sabe de Brevo y como se resuelve (documentacion oficial, revisada en la fase 16C-1):
//  - No admite imagenes incrustadas (cid:): el logotipo se pide por una direccion publica del sitio (Correo:Api:UrlLogo o Sitio:UrlPublica).
//  - Un remitente @gmail.com no se puede autenticar (DKIM/DMARC) y Brevo cambia esa direccion por una propia para cumplir las reglas de Gmail:
//    la entregabilidad es el riesgo ya previsto en D8. Lo ideal es un remitente de un dominio propio; si no, queda como limite documentado.
//  - No hay clave de idempotencia: si la respuesta se pierde despues de aceptar el envio, el reintento podria entregar el aviso dos veces. Es
//    preferible a perderlo (los avisos al taller y los codigos de recuperacion no pueden desaparecer en silencio).
public sealed partial class EnviadorCorreoApi(IOptions<OpcionesCorreo> opciones, IHttpClientFactory fabrica, IConfiguration configuracion) : IEnviadorCorreo
{
    // El nombre del cliente HTTP de Program.cs (sin seguir redirecciones: la clave viaja en una cabecera propia que .NET NO retira al redirigir).
    public const string NombreDelCliente = "correo-api";

    private const string LogoPorCid = "cid:" + PlantillaCorreo.IdLogo;

    public string Descripcion => $"API {opciones.Value.Api}";

    public bool Listo(out string? motivo)
    {
        var o = opciones.Value;
        motivo = null;
        if (!o.Api.ProveedorValido)
            motivo = "Correo:Api:Proveedor debe ser Brevo.";
        else if (!o.Api.TieneClave)
            motivo = "falta Correo:Api:Clave (la clave de la API del proveedor; se carga como variable de entorno Correo__Api__Clave).";
        else if (!o.Api.UrlSegura)
            motivo = "Correo:Api:Url debe ser una direccion https sin usuario ni clave (http solo para un servidor local de pruebas).";
        else if (string.IsNullOrWhiteSpace(o.DireccionRemitente))
            motivo = "falta la dirección del remitente (Correo:Remitente:Direccion, verificada en el proveedor).";
        return motivo is null;
    }

    public async Task EnviarAsync(CorreoParaEnviar correo, CancellationToken ct)
    {
        var o = opciones.Value;
        var plazo = TimeSpan.FromSeconds(Math.Clamp(o.Api.TiempoEsperaSegundos, 1, 300));
        using var peticion = new HttpRequestMessage(HttpMethod.Post, o.Api.Url)
        {
            Content = new StringContent(ArmarCuerpo(correo.Mensaje), Encoding.UTF8, "application/json")
        };
        // La clave va SOLO en la cabecera, nunca en el cuerpo ni en la direccion.
        // TryAdd...: una clave con un salto de linea no lanza FormatException; el proveedor la rechaza (401) y se clasifica como autenticacion.
        peticion.Headers.TryAddWithoutValidation("api-key", (o.Api.Clave ?? "").Trim());
        peticion.Headers.Accept.ParseAdd("application/json");

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(plazo);
        try
        {
            using var respuesta = await fabrica.CreateClient(NombreDelCliente).SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead, limite.Token);
            if (!respuesta.IsSuccessStatusCode)
                throw new FalloApiCorreoException((int)respuesta.StatusCode);
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("El proveedor de correo no respondió a tiempo.", e);
        }
    }

    private string ArmarCuerpo(MimeMessage mensaje)
    {
        var remitente = mensaje.From.Mailboxes.FirstOrDefault() ?? throw new FormatException("El correo no tiene remitente.");
        var cabeceras = new Dictionary<string, string>();
        foreach (var nombre in new[] { "Auto-Submitted", "List-Unsubscribe", "List-Unsubscribe-Post" })
            if (mensaje.Headers[nombre] is { Length: > 0 } valor)
                cabeceras[nombre] = valor;

        var carga = new Dictionary<string, object?>
        {
            // Brevo rechaza un nombre nulo: sin nombre, solo la direccion.
            ["sender"] = string.IsNullOrWhiteSpace(remitente.Name)
                ? new Dictionary<string, string> { ["email"] = remitente.Address }
                : new Dictionary<string, string> { ["name"] = remitente.Name, ["email"] = remitente.Address },
            ["to"] = mensaje.To.Mailboxes.Select(d => new { email = d.Address }).ToArray(),
            ["subject"] = mensaje.Subject,
            ["textContent"] = mensaje.TextBody,
            ["htmlContent"] = mensaje.HtmlBody is { } html ? SinImagenesIncrustadas(html) : null,
            ["headers"] = cabeceras
        };
        if (mensaje.ReplyTo.Mailboxes.FirstOrDefault() is { } responderA)
            carga["replyTo"] = new { email = responderA.Address };
        // Lo que no existe no se envia (la API rechaza un texto vacio).
        foreach (var vacia in carga.Where(p => p.Value is null).Select(p => p.Key).ToList())
            carga.Remove(vacia);
        return JsonSerializer.Serialize(carga);
    }

    [GeneratedRegex(@"<img\b[^>]*" + LogoPorCid + @"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex EtiquetaDelLogo();

    // El logotipo viaja incrustado en el mensaje (cid:); el proveedor no lo admite, asi que se pide por la direccion publica del sitio.
    // Sin direccion publica, se quita la imagen: el correo sale igual (el logotipo es adorno, el aviso no).
    private string SinImagenesIncrustadas(string html)
    {
        if (!html.Contains(LogoPorCid, StringComparison.OrdinalIgnoreCase))
            return html;
        return UrlDelLogo() is { } url
            ? html.Replace(LogoPorCid, url, StringComparison.OrdinalIgnoreCase)
            : EtiquetaDelLogo().Replace(html, "");
    }

    private string? UrlDelLogo()
    {
        if (Absoluta(opciones.Value.Api.UrlLogo) is { } propia)
            return propia;
        return Absoluta(configuracion["Sitio:UrlPublica"]?.TrimEnd('/') + "/marca/icono-192.png");
    }

    // Solo https (o http de localhost), sin datos de usuario; AbsoluteUri deja la direccion escapada, sin comillas que rompan el atributo.
    private static string? Absoluta(string? texto) =>
        Uri.TryCreate(texto, UriKind.Absolute, out var uri) && string.IsNullOrEmpty(uri.UserInfo)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            ? uri.AbsoluteUri
            : null;
}
