using System.Net;
using MailKit.Security;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;

// Configuracion del correo (seccion "Correo"). Los valores por defecto sirven para Gmail; el usuario y
// la contraseña de aplicacion NO van aqui ni en appsettings: se escriben en User Secrets (desarrollo)
// o en variables de entorno (produccion), RNFP17.
// Son clases y no records a proposito: un record imprime todas sus propiedades en ToString, y bastaria
// un registro descuidado para que la contraseña terminara en un log.
public sealed class OpcionesCorreo
{
    public const string Seccion = "Correo";
    public const string ModoSmtp = "Smtp";
    public const string ModoBandeja = "Bandeja";

    // Smtp: envio real. Bandeja: archivos .eml en una carpeta (solo para desarrollo).
    public string Modo { get; set; } = ModoSmtp;
    public OpcionesRemitente Remitente { get; set; } = new();
    public OpcionesSmtp Smtp { get; set; } = new();
    public OpcionesBandeja Bandeja { get; set; } = new();
    public OpcionesCola Cola { get; set; } = new();

    // Contra un envio masivo de solicitudes falsas: pasado este tope en una hora, los avisos dejan de ir
    // uno por uno y llega un solo aviso de "muchas solicitudes". Gmail admite unos 500 envios al dia, y
    // agotarlo dejaria al dueño sin avisos de las solicitudes reales. 0 lo desactiva.
    public int TopeAvisosPorHora { get; set; } = 20;

    // Parte del tope anterior que pueden usar las consultas del formulario de contacto. Asi, aunque un
    // robot llene el formulario, las solicitudes de cotizacion conservan el resto del cupo. 0 = sin sublimite.
    public int TopeConsultasPorHora { get; set; } = 5;

    // Correo publico del taller (el que se muestra en la portada). Los correos para clientes salen desde
    // la casilla automatica de avisos y dicen que se responda aqui; nunca se publica la casilla de avisos.
    public string ContactoTaller { get; set; } = "contacto.letrerosherrera@gmail.com";

    public bool ModoValido => string.Equals(Modo, ModoSmtp, StringComparison.OrdinalIgnoreCase) || EsBandeja;

    public bool EsBandeja => string.Equals(Modo, ModoBandeja, StringComparison.OrdinalIgnoreCase);

    // Gmail solo permite enviar como la cuenta autenticada (o un alias verificado de ella). En la bandeja
    // local, mientras no haya cuenta configurada, se usa una direccion que no existe fuera del equipo.
    public string? DireccionRemitente =>
        !string.IsNullOrWhiteSpace(Remitente.Direccion) ? Remitente.Direccion.Trim()
        : !string.IsNullOrWhiteSpace(Smtp.Usuario) ? Smtp.Usuario.Trim()
        : EsBandeja ? "synctime@bandeja.local"
        : null;
}

public sealed class OpcionesRemitente
{
    public string Nombre { get; set; } = "Letreros Herrera";
    public string? Direccion { get; set; }
}

public sealed class OpcionesSmtp
{
    public string Servidor { get; set; } = "smtp.gmail.com";
    public int Puerto { get; set; } = 587;

    // StartTls (587), SslAlIniciar (465) o Ninguna (solo para un servidor en este mismo equipo).
    public string Seguridad { get; set; } = "StartTls";
    public string? Usuario { get; set; }
    public string? Password { get; set; }

    // Tiempo maximo para conectar, autenticarse y entregar un correo (RNFP17).
    public int TiempoEsperaSegundos { get; set; } = 20;

    public override string ToString() => $"{Servidor}:{Puerto}";

    internal bool TieneUsuario => !string.IsNullOrWhiteSpace(Usuario);
    internal bool TieneClave => !string.IsNullOrWhiteSpace(Password);

    internal SecureSocketOptions? Opciones => Seguridad.Trim().ToLowerInvariant() switch
    {
        "starttls" => SecureSocketOptions.StartTls,
        "sslaliniciar" => SecureSocketOptions.SslOnConnect,
        "ninguna" => SecureSocketOptions.None,
        _ => null
    };

    internal bool ServidorLocal =>
        string.Equals(Servidor, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(Servidor, out var ip) && IPAddress.IsLoopback(ip));
}

public sealed class OpcionesBandeja
{
    public string Carpeta { get; set; } = "bandeja-correo";
}

public sealed class OpcionesCola
{
    // Permite apagar el envio en un proceso (por ejemplo, en las pruebas, que procesan a mano).
    public bool Activa { get; set; } = true;

    // Cada cuanto se revisa la cola si nadie avisa antes. Un correo nuevo despierta al trabajador al instante.
    public int IntervaloSegundos { get; set; } = 30;
    public int Lote { get; set; } = 10;
    public int MaxIntentos { get; set; } = 6;

    // Lo ya enviado o fallido se borra pasado este plazo: el cuerpo lleva datos personales.
    public int DiasRetencion { get; set; } = 30;
}
