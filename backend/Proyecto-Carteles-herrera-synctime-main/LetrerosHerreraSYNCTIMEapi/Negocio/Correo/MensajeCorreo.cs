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

    // Fase 9: destinos de los avisos del taller. La prueba desde el perfil tiene su propio tipo: no cuenta
    // para el tope de avisos por hora (que suma solo solicitudes y consultas).
    public const string PruebaAvisos = "prueba_avisos";
    public const string ConfirmacionDestinoAvisos = "confirmacion_destino_avisos";
    public const string DestinoAvisosCambiado = "destino_avisos_cambiado";
    public const string DestinoAvisosAgregado = "destino_avisos_agregado";
    public const string DestinoAvisosQuitado = "destino_avisos_quitado";
    public const string AvisoCambioCorreoPersonal = "aviso_cambio_correo_personal";
    public const string AvisoAccesoReasignado = "aviso_acceso_reasignado";
}

// Deja un correo en la cola de salida. Nunca espera al servidor de correo.
public interface IServicioCorreo
{
    Task EncolarAsync(MensajeCorreo mensaje, CancellationToken ct);

    // Cuantos correos de un tipo se encolaron desde un momento (para el tope de avisos por hora).
    Task<int> ContarAsync(string tipo, DateTimeOffset desde, CancellationToken ct);
}

// A quien le llegan los avisos del taller (por ejemplo, el de solicitud nueva). Es el unico lugar que lo
// sabe. Fase 9: primero los destinos activos que guardaron el dueño o el administrador desde su perfil; si
// no hay ninguno, el respaldo de la configuracion del servidor.
public interface IDestinatariosAvisos
{
    Task<IReadOnlyList<string>> ObtenerAsync(CancellationToken ct);

    // Lo mismo, diciendo ademas si vienen del respaldo de la configuracion.
    Task<DestinatariosVigentes> LeerAsync(CancellationToken ct);
}

public sealed record DestinatariosVigentes(IReadOnlyList<string> Correos, bool DesdeRespaldo);
