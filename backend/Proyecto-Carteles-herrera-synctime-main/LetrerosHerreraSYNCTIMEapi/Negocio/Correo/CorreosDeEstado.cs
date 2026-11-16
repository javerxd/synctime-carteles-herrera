using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

// Los dos enlaces de baja de un correo opcional: la pagina de confirmacion (en el cuerpo) y la baja con un clic
// (cabecera List-Unsubscribe, RFC 8058).
public sealed record EnlacesBaja(string Pagina, string UnClic);

// Fase 11: el enlace de baja. Es un enlace firmado de un solo proposito: lleva el numero de cliente y una firma
// HMAC-SHA256 (clave propia del servidor) sobre el cliente, su correo actual y su inhabilitacion. Asi:
//   - no lleva el correo en claro ni sirve para otra cosa;
//   - no se puede fabricar ni alterar sin la clave;
//   - vence solo cuando el cliente cambia de correo o da de baja la cuenta (la firma ya no coincide);
//   - en la base no se guarda nada que permita armarlo.
public static class EnlaceBaja
{
    public const string Etiqueta = "synctime:baja-correo";
    private const int LargoId = 4;
    private const int LargoFirma = 32;

    // Clave de 256 bits derivada con HKDF de un secreto del servidor, con su propia etiqueta.
    public static byte[] DerivarClave(string secreto) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secreto), 32, info: Encoding.UTF8.GetBytes(Etiqueta));

    public static string Token(byte[] clave, int idCliente, string correo, DateTimeOffset? inhabilitadoEn)
    {
        var bytes = new byte[LargoId + LargoFirma];
        BinaryPrimitives.WriteInt32BigEndian(bytes, idCliente);
        Firma(clave, idCliente, correo, inhabilitadoEn).CopyTo(bytes, LargoId);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // Lee el numero de cliente de un token con la forma correcta (sin comprobar la firma).
    public static bool TryLeerId(string? token, out int idCliente)
    {
        idCliente = 0;
        if (Decodificar(token) is not { } bytes)
            return false;
        idCliente = BinaryPrimitives.ReadInt32BigEndian(bytes);
        return idCliente > 0;
    }

    public static bool Valido(byte[] clave, string? token, int idCliente, string? correo, DateTimeOffset? inhabilitadoEn)
    {
        if (string.IsNullOrWhiteSpace(correo) || Decodificar(token) is not { } bytes || BinaryPrimitives.ReadInt32BigEndian(bytes) != idCliente)
            return false;
        return CryptographicOperations.FixedTimeEquals(bytes.AsSpan(LargoId), Firma(clave, idCliente, correo, inhabilitadoEn));
    }

    public static string? UrlPagina(string? urlPublica, string token) => Url(urlPublica, $"/baja?t={token}");

    public static string? UrlUnClic(string? urlPublica, string token) => Url(urlPublica, $"/api/v1/correos/baja/un-clic?t={token}");

    // Sin direccion publica valida no hay enlaces (el correo no se envia: un opcional sin baja no debe salir).
    public static EnlacesBaja? Enlaces(string? urlPublica, string? token) =>
        token is not null && UrlPagina(urlPublica, token) is { } pagina && UrlUnClic(urlPublica, token) is { } unClic ? new EnlacesBaja(pagina, unClic) : null;

    private static byte[] Firma(byte[] clave, int idCliente, string correo, DateTimeOffset? inhabilitadoEn) =>
        HMACSHA256.HashData(clave, Encoding.UTF8.GetBytes(
            $"baja:v1:{idCliente}:{correo.Trim().ToLowerInvariant()}:{inhabilitadoEn?.UtcTicks ?? 0}"));

    private static byte[]? Decodificar(string? token)
    {
        // 36 bytes en base64url sin relleno: 48 caracteres.
        if (token is null || token.Length != 48 || !token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            return null;
        try
        {
            var bytes = Convert.FromBase64String(token.Replace('-', '+').Replace('_', '/'));
            return bytes.Length == LargoId + LargoFirma ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? Url(string? urlPublica, string ruta)
    {
        if (string.IsNullOrWhiteSpace(urlPublica))
            return null;
        return Uri.TryCreate($"{urlPublica.Trim().TrimEnd('/')}{ruta}", UriKind.Absolute, out var url)
               && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
            ? url.ToString()
            : null;
    }
}

// Fase 11 (RFS08, S09): los avisos de estado al cliente y el comprobante de la baja de la cuenta. Llevan el numero,
// el estado nuevo en palabras, un mensaje breve (el motivo, si lo hay) y la fecha. Los de estado son opcionales y
// llevan el enlace de baja; el comprobante es esencial. Todo texto se escapa al componer.
public static class CorreosDeEstado
{
    public static string TextoEstadoSolicitud(string estado) => estado switch
    {
        "solicitado" => "En revisión",
        "cotizado" => "Cotizada",
        "aceptado" => "Aceptada",
        "rechazado" => "Rechazada",
        _ => estado
    };

    public static string TextoEstadoPedido(string estado) => estado switch
    {
        "aceptado" => "Aceptado",
        "en_proceso" => "En proceso",
        "terminado" => "Terminado",
        "entregado" => "Entregado",
        "anulado" => "Anulado",
        _ => estado
    };

    private static string MensajeSolicitud(string estado) => estado switch
    {
        "aceptado" => "Aprobaste la cotización: pronto nos pondremos en contacto para coordinar el trabajo.",
        "rechazado" => "Revisamos tu solicitud y no podremos hacer este trabajo.",
        _ => "Tu solicitud cambió de estado."
    };

    private static string MensajePedido(string estado) => estado switch
    {
        "en_proceso" => "Comenzamos a trabajar en tu pedido.",
        "terminado" => "Tu pedido está terminado. Te contactaremos para la entrega o instalación.",
        "entregado" => "Tu pedido fue entregado. ¡Gracias por preferirnos!",
        "anulado" => "Tu pedido fue anulado.",
        _ => "Tu pedido cambió de estado."
    };

    public static MensajeCorreo Solicitud(string para, string nombre, int idSolicitud, string estado, string? motivo, DateTime fechaChile,
        string correoContacto, string? urlPublica, string tokenBaja)
    {
        var numero = $"#{idSolicitud}";
        var texto = TextoEstadoSolicitud(estado);
        var cuerpo = Cuerpo(nombre, $"Tu solicitud {numero} ahora está: {texto}.", MensajeSolicitud(estado), "Solicitud", numero, texto, motivo, fechaChile);
        if (Url(urlPublica, $"/cuenta/solicitudes/{idSolicitud}") is { } url)
            cuerpo.Boton("Ver mi solicitud", url);
        return CorreoParaCliente.Componer(TiposCorreo.EstadoSolicitud, para, $"Tu solicitud {numero}: {texto}", $"Tu solicitud está {texto.ToLowerInvariant()}",
            $"Solicitud {numero}: {texto}", cuerpo, correoContacto, referencia: $"estado:solicitud:{idSolicitud}", baja: EnlaceBaja.Enlaces(urlPublica, tokenBaja));
    }

    public static MensajeCorreo Pedido(string para, string nombre, int idPedido, string estado, string? motivo, DateTime fechaChile,
        string correoContacto, string? urlPublica, string tokenBaja)
    {
        var numero = $"#{idPedido}";
        var texto = TextoEstadoPedido(estado);
        var cuerpo = Cuerpo(nombre, $"Tu pedido {numero} ahora está: {texto}.", MensajePedido(estado), "Pedido", numero, texto, motivo, fechaChile);
        if (Url(urlPublica, "/cuenta/pedidos") is { } url)
            cuerpo.Boton("Ver mis pedidos", url);
        return CorreoParaCliente.Componer(TiposCorreo.EstadoPedido, para, $"Tu pedido {numero}: {texto}", $"Tu pedido está {texto.ToLowerInvariant()}",
            $"Pedido {numero}: {texto}", cuerpo, correoContacto, referencia: $"estado:pedido:{idPedido}", baja: EnlaceBaja.Enlaces(urlPublica, tokenBaja));
    }

    // Esencial: sin enlace de baja (la cuenta ya no existe para recibir otros correos).
    public static MensajeCorreo ComprobanteBaja(string para, string nombre, string numero, DateTime fechaChile, string correoContacto)
    {
        var cuerpo = new CuerpoCorreo()
            .Parrafo($"Hola, {AvisoSolicitudNueva.Linea(nombre)}. Tu cuenta en Letreros Herrera quedó dada de baja.")
            .Datos(
                new DatoCorreo("Comprobante", numero),
                new DatoCorreo("Fecha", $"{FormatoChile.Fecha(fechaChile)} a las {FormatoChile.Hora(fechaChile)}"))
            .Parrafo("Ya no puedes ingresar con esta cuenta y no te enviaremos más correos. Conservamos tus solicitudes, pedidos y abonos porque son parte del registro contable del taller.")
            .Nota($"Si no lo pediste tú, escríbenos de inmediato a {correoContacto}.");
        return CorreoParaCliente.Componer(TiposCorreo.BajaCuenta, para, "Tu cuenta en Letreros Herrera quedó dada de baja", "Tu cuenta quedó dada de baja",
            $"Comprobante {numero}", cuerpo, correoContacto, referencia: $"baja:{numero}");
    }

    private static CuerpoCorreo Cuerpo(string nombre, string entrada, string mensaje, string etiqueta, string numero, string estado, string? motivo, DateTime fechaChile)
    {
        var cuerpo = new CuerpoCorreo()
            .Parrafo($"Hola, {AvisoSolicitudNueva.Linea(nombre)}. {entrada}")
            .Datos(
                new DatoCorreo(etiqueta, numero),
                new DatoCorreo("Estado", estado),
                new DatoCorreo("Fecha", $"{FormatoChile.Fecha(fechaChile)} a las {FormatoChile.Hora(fechaChile)}"))
            .Parrafo(mensaje);
        if (!string.IsNullOrWhiteSpace(motivo))
            cuerpo.Cita("Motivo", AvisoSolicitudNueva.Linea(motivo));
        return cuerpo;
    }

    private static string? Url(string? urlPublica, string ruta)
    {
        if (string.IsNullOrWhiteSpace(urlPublica))
            return null;
        return Uri.TryCreate($"{urlPublica.Trim().TrimEnd('/')}{ruta}", UriKind.Absolute, out var url)
               && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
            ? url.ToString()
            : null;
    }
}
