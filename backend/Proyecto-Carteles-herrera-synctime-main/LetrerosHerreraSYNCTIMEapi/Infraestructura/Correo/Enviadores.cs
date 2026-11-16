using System.Text.RegularExpressions;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;

public sealed record CorreoParaEnviar(long IdCorreo, string? Referencia, MimeMessage Mensaje);

// Entrega un mensaje ya armado. Hay dos: SMTP (real) y bandeja local (desarrollo).
public interface IEnviadorCorreo
{
    // Para los registros: nunca incluye credenciales.
    string Descripcion { get; }

    // false si falta configuracion; la cola espera sin gastar intentos.
    bool Listo(out string? motivo);

    Task EnviarAsync(CorreoParaEnviar correo, CancellationToken ct);
}

// Arma el mensaje MIME: texto y HTML alternativos, logotipo incrustado y cabeceras de correo automatico.
public static class ConstructorMime
{
    private static readonly Lazy<byte[]> Logo = new(() =>
    {
        using var recurso = typeof(ConstructorMime).Assembly.GetManifestResourceStream("SyncTime.logo-lh-correo.png")
            ?? throw new InvalidOperationException("Falta el recurso del logotipo del correo.");
        using var copia = new MemoryStream();
        recurso.CopyTo(copia);
        return copia.ToArray();
    });

    public static MimeMessage Construir(CorreoSaliente correo, OpcionesCorreo opciones)
    {
        var remitente = opciones.DireccionRemitente
            ?? throw new InvalidOperationException("Falta la dirección del remitente.");
        var dominio = remitente.Contains('@') ? remitente[(remitente.LastIndexOf('@') + 1)..] : "synctime.local";

        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(opciones.Remitente.Nombre, remitente));
        foreach (var destinatario in correo.Destinatarios.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            mensaje.To.Add(MailboxAddress.Parse(destinatario));
        // El correo que escribio el cliente: si MimeKit no lo acepta, el aviso sale igual, sin "Responder a".
        if (!string.IsNullOrWhiteSpace(correo.ResponderA) && MailboxAddress.TryParse(correo.ResponderA, out var responderA))
            mensaje.ReplyTo.Add(responderA);
        mensaje.Subject = correo.Asunto;
        // El mismo identificador en cada reintento: si un reintento llegara dos veces, el programa de
        // correo lo reconoce como el mismo mensaje.
        mensaje.MessageId = $"correo-{correo.IdCorreo}.{correo.FechaCreacion.ToUnixTimeSeconds()}@{dominio}";
        // Correo automatico (RFC 3834): evita respuestas automaticas como "estoy de vacaciones".
        mensaje.Headers.Add("Auto-Submitted", "auto-generated");
        // Fase 11: un correo opcional al cliente se puede dar de baja con un clic desde el programa de correo
        // (RFC 2369 y RFC 8058). La direccion es https y no lleva el correo del cliente.
        if (!string.IsNullOrWhiteSpace(correo.EnlaceBaja) && Uri.TryCreate(correo.EnlaceBaja, UriKind.Absolute, out var baja)
            && (baja.Scheme == Uri.UriSchemeHttps || baja.Scheme == Uri.UriSchemeHttp))
        {
            mensaje.Headers.Add("List-Unsubscribe", $"<{baja.AbsoluteUri}>");
            mensaje.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        }

        var cuerpo = new BodyBuilder { TextBody = correo.CuerpoTexto, HtmlBody = correo.CuerpoHtml };
        var logo = cuerpo.LinkedResources.Add("logo-lh.png", Logo.Value, new ContentType("image", "png"));
        logo.ContentId = PlantillaCorreo.IdLogo;
        mensaje.Body = cuerpo.ToMessageBody();
        return mensaje;
    }
}

// Envio real con MailKit (RNFE05: remitente autenticado). Cada envio tiene un tiempo maximo (RNFP17):
// un servidor que no responde no deja al trabajador esperando para siempre.
public sealed class EnviadorSmtp(IOptions<OpcionesCorreo> opciones) : IEnviadorCorreo
{
    public string Descripcion => $"SMTP {opciones.Value.Smtp}";

    public bool Listo(out string? motivo)
    {
        var o = opciones.Value;
        var smtp = o.Smtp;
        motivo = null;
        if (string.IsNullOrWhiteSpace(smtp.Servidor) || smtp.Puerto is <= 0 or > 65535)
            motivo = "falta Correo:Smtp:Servidor o su puerto no es válido.";
        else if (smtp.Opciones is null)
            motivo = "Correo:Smtp:Seguridad debe ser StartTls, SslAlIniciar o Ninguna.";
        else if (smtp.TieneUsuario != smtp.TieneClave)
            motivo = "falta Correo:Smtp:Usuario o Correo:Smtp:Password (se escriben en User Secrets).";
        else if (smtp.Opciones == MailKit.Security.SecureSocketOptions.None && !smtp.ServidorLocal)
            motivo = "sin cifrado solo se admite un servidor en este mismo equipo (los avisos llevan datos personales): use StartTls o SslAlIniciar.";
        else if (string.IsNullOrWhiteSpace(o.DireccionRemitente))
            motivo = "falta la dirección del remitente (Correo:Remitente:Direccion o Correo:Smtp:Usuario).";
        return motivo is null;
    }

    public async Task EnviarAsync(CorreoParaEnviar correo, CancellationToken ct)
    {
        var smtp = opciones.Value.Smtp;
        var plazo = TimeSpan.FromSeconds(Math.Clamp(smtp.TiempoEsperaSegundos, 1, 300));

        // El plazo propio vence antes que el de MailKit, asi un servidor colgado siempre se informa igual.
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(plazo);
        using var cliente = new SmtpClient { Timeout = (int)(plazo + TimeSpan.FromSeconds(5)).TotalMilliseconds };
        try
        {
            await cliente.ConnectAsync(smtp.Servidor, smtp.Puerto, smtp.Opciones!.Value, limite.Token);
            if (smtp.TieneUsuario)
                await cliente.AuthenticateAsync(smtp.Usuario!.Trim(), smtp.Password!, limite.Token);
            await cliente.SendAsync(correo.Mensaje, limite.Token);
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("El servidor de correo no respondió a tiempo.", e);
        }

        // El correo ya fue entregado: si despedirse del servidor falla, no se informa como un fallo,
        // o el trabajador lo reintentaria y llegaria repetido. Al cerrar, el using corta la conexion.
        try
        {
            using var despedida = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await cliente.DisconnectAsync(true, despedida.Token);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or MailKit.ServiceNotConnectedException
                                     or MailKit.Net.Smtp.SmtpCommandException or MailKit.Net.Smtp.SmtpProtocolException
                                     or System.Net.Sockets.SocketException)
        {
            // Sin consecuencias para el envio.
        }
    }
}

// Bandeja local de desarrollo: cada correo queda como un archivo .eml, que se abre con cualquier
// programa de correo o en la ruta /dev/bandeja. No sale nada del equipo.
public sealed partial class EnviadorBandejaLocal(IOptions<OpcionesCorreo> opciones, IHostEnvironment entorno, TimeProvider reloj) : IEnviadorCorreo
{
    public string Carpeta => Path.GetFullPath(Path.Combine(entorno.ContentRootPath, opciones.Value.Bandeja.Carpeta));

    public string Descripcion => "bandeja local";

    public bool Listo(out string? motivo)
    {
        // Fuera de desarrollo, "enviar" a una carpeta seria perder los avisos sin que nadie se entere.
        motivo = entorno.IsDevelopment() ? null : "la bandeja local solo se usa en desarrollo: configure Correo:Modo = Smtp.";
        return motivo is null;
    }

    public async Task EnviarAsync(CorreoParaEnviar correo, CancellationToken ct)
    {
        Directory.CreateDirectory(Carpeta);
        var sufijo = correo.Referencia is null ? "" : $"-{NoAlfanumerico().Replace(correo.Referencia.ToLowerInvariant(), "-")}";
        var nombre = $"{reloj.GetLocalNow():yyyyMMdd-HHmmss}-{correo.IdCorreo}{sufijo}.eml";
        await correo.Mensaje.WriteToAsync(Path.Combine(Carpeta, nombre), ct);
    }

    // Nombres que escribe este enviador; la ruta /dev/bandeja no sirve ningun otro archivo.
    // \z y no $: $ tambien aceptaria un salto de linea al final.
    [GeneratedRegex(@"^\d{8}-\d{6}-\d{1,19}(-[a-z0-9-]{1,64})?\.eml\z")]
    public static partial Regex NombreValido();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NoAlfanumerico();
}
