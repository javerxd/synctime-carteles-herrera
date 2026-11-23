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
    public const string ModoApi = "Api";

    // Smtp: envio real por SMTP (compose y desarrollo). Api: envio real por HTTPS con la API de un proveedor (la nube gratuita: Render
    // bloquea el SMTP). Bandeja: archivos .eml en una carpeta (solo para desarrollo).
    public string Modo { get; set; } = ModoSmtp;
    public OpcionesRemitente Remitente { get; set; } = new();
    public OpcionesSmtp Smtp { get; set; } = new();
    public OpcionesApiCorreo Api { get; set; } = new();
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

    public bool ModoValido => string.Equals(Modo, ModoSmtp, StringComparison.OrdinalIgnoreCase) || EsBandeja || EsApi;

    public bool EsBandeja => string.Equals(Modo, ModoBandeja, StringComparison.OrdinalIgnoreCase);

    // Fase 16C-1 (H11): correo por HTTPS con la API de un proveedor (la nube gratuita bloquea el SMTP).
    public bool EsApi => string.Equals(Modo, ModoApi, StringComparison.OrdinalIgnoreCase);

    // Con la API, la direccion que envia es Remitente:Direccion (verificada en el proveedor) y debe ser distinta del contacto publico del taller.
    public bool ContactoIgualAlRemitente =>
        !string.IsNullOrWhiteSpace(Remitente.Direccion)
        && string.Equals(ContactoTaller.Trim(), Remitente.Direccion.Trim(), StringComparison.OrdinalIgnoreCase);

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

// Correo por HTTPS (fase 16C-1, D8). La clave se carga como variable de entorno (Correo__Api__Clave, secreto de Render): nunca en un archivo
// ni en un registro. ToString no la incluye.
public sealed class OpcionesApiCorreo
{
    public const string ProveedorBrevo = "Brevo";

    public string Proveedor { get; set; } = ProveedorBrevo;

    // Direccion de la API de envio. Solo https (la clave viaja en una cabecera); http se admite unicamente para un servidor simulado local.
    public string Url { get; set; } = "https://api.brevo.com/v3/smtp/email";

    public string? Clave { get; set; }

    // Brevo no admite imagenes incrustadas (cid:): el logotipo se pide por una direccion publica. Por omision, Sitio:UrlPublica + /marca/icono-192.png.
    public string? UrlLogo { get; set; }

    // Tiempo maximo de cada envio (RNFP17): un proveedor que no responde no deja al trabajador esperando para siempre.
    public int TiempoEsperaSegundos { get; set; } = 20;

    public bool ProveedorValido => string.Equals(Proveedor, ProveedorBrevo, StringComparison.OrdinalIgnoreCase);
    internal bool TieneClave => !string.IsNullOrWhiteSpace(Clave);

    internal bool UrlSegura =>
        Uri.TryCreate(Url, UriKind.Absolute, out var uri)
        && string.IsNullOrEmpty(uri.UserInfo)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));

    public override string ToString() => Proveedor;
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

    // Fase 11: el cuerpo, el asunto y la direccion completa de lo ya enviado se borran pasado este plazo (llevan datos
    // personales); la fila queda con el tipo, la referencia, el estado y las fechas. Los fallidos los conservan mas
    // tiempo, para poder reenviarlos desde «Correos no enviados».
    public int DiasCuerpoEnviados { get; set; } = 30;
    public int DiasCuerpoFallidos { get; set; } = 90;
}
