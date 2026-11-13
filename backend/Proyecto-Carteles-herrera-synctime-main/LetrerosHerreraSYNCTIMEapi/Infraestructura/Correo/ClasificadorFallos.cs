using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;

// Por que fallo un envio, en palabras propias. Nunca se usa el mensaje de la excepcion: los servidores
// suelen repetir en su respuesta el usuario de la cuenta o la direccion de destino, y ese texto
// terminaria en la base (ultimo_error) o en los registros. Solo se conserva el codigo numerico SMTP.
//   Permanente: no tiene sentido reintentar (el buzon no existe).
//   DetieneLote: el problema es del servidor o de la cuenta, no de este correo; insistir con los demas
//                ahora solo sumaria intentos fallidos (y Gmail desconfia de una cuenta que falla seguido).
public sealed record FalloCorreo(string Categoria, string Mensaje, bool Permanente, bool DetieneLote, int? Codigo = null)
{
    public string Texto => Codigo is null ? $"{Categoria}: {Mensaje}" : $"{Categoria}: {Mensaje} (código {Codigo})";
}

public static class ClasificadorFallos
{
    public static FalloCorreo Clasificar(Exception error) => error switch
    {
        AuthenticationException =>
            new("autenticacion", "El servidor de correo rechazó el usuario o la contraseña de la cuenta.", false, true),

        // Solo un 5xx es definitivo; un 4xx (limite de envios, buzon lleno por ahora) se reintenta abajo.
        SmtpCommandException { ErrorCode: SmtpErrorCode.RecipientNotAccepted } e when (int)e.StatusCode >= 500 =>
            new("destinatario", "El servidor rechazó la dirección de destino.", true, false, (int)e.StatusCode),

        SmtpCommandException { ErrorCode: SmtpErrorCode.SenderNotAccepted } e =>
            new("remitente", "El servidor rechazó la dirección del remitente.", false, true, (int)e.StatusCode),

        SmtpCommandException e when (int)e.StatusCode is 530 or 534 or 535 or 538 =>
            new("autenticacion", "El servidor de correo exige iniciar sesión o rechazó la cuenta.", false, true, (int)e.StatusCode),

        SmtpCommandException { ErrorCode: SmtpErrorCode.MessageNotAccepted } e when (int)e.StatusCode >= 500 =>
            new("mensaje", "El servidor rechazó el mensaje.", true, false, (int)e.StatusCode),

        SmtpCommandException e =>
            new("servidor", "El servidor de correo no aceptó el envío por ahora.", false, true, (int)e.StatusCode),

        SmtpProtocolException =>
            new("servidor", "El servidor de correo respondió de forma inesperada.", false, true),

        SslHandshakeException =>
            new("conexion", "No se pudo establecer una conexión cifrada con el servidor de correo.", false, true),

        TimeoutException =>
            new("tiempo_agotado", "El servidor de correo no respondió a tiempo.", false, true),

        OperationCanceledException =>
            new("tiempo_agotado", "El servidor de correo no respondió a tiempo.", false, true),

        IOException { InnerException: SocketException { SocketErrorCode: SocketError.TimedOut } } =>
            new("tiempo_agotado", "El servidor de correo no respondió a tiempo.", false, true),

        SocketException or IOException or ServiceNotConnectedException =>
            new("conexion", "No se pudo conectar con el servidor de correo.", false, true),

        // Una direccion guardada que no se puede leer: reintentar no la arregla.
        ParseException or FormatException =>
            new("mensaje", "El correo tiene una dirección que no se puede usar.", true, false),

        NotSupportedException =>
            new("configuracion", "El servidor de correo no admite la forma de conexión configurada.", false, true),

        _ => new("desconocido", "El envío falló por un error inesperado.", false, false)
    };
}
