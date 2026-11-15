namespace LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

// Un correo ya compuesto, listo para la cola: el negocio decide que decir y a quien; enviarlo es de
// Infraestructura. Referencia identifica a que se refiere (por ejemplo "solicitud:123").
public sealed record MensajeCorreo(
    string Tipo,
    IReadOnlyList<string> Para,
    string Asunto,
    string Html,
    string Texto,
    string? ResponderA = null,
    string? Referencia = null);

public static class TiposCorreo
{
    public const string AvisoSolicitudNueva = "aviso_solicitud_nueva";
    public const string AvisoTopeSolicitudes = "aviso_tope_solicitudes";
    public const string ConsultaContacto = "consulta_contacto";
    public const string VerificacionCorreo = "verificacion_correo";
    public const string RecuperacionCuenta = "recuperacion_cuenta";
    public const string AvisoCambioCorreo = "aviso_cambio_correo";
    public const string ConfirmacionSolicitud = "confirmacion_solicitud";
    public const string CotizacionLista = "cotizacion_lista";
    public const string Prueba = "prueba";
}

// Deja un correo en la cola de salida. Nunca espera al servidor de correo.
public interface IServicioCorreo
{
    Task EncolarAsync(MensajeCorreo mensaje, CancellationToken ct);

    // Cuantos correos de un tipo se encolaron desde un momento (para el tope de avisos por hora).
    Task<int> ContarAsync(string tipo, DateTimeOffset desde, CancellationToken ct);
}

// A quien le llegan los avisos del taller (por ejemplo, el de solicitud nueva). Es el unico lugar que lo
// sabe: hoy lo lee de la configuracion y en la fase 9 leera primero el valor que guarde el dueño.
public interface IDestinatariosAvisos
{
    Task<IReadOnlyList<string>> ObtenerAsync(CancellationToken ct);
}
