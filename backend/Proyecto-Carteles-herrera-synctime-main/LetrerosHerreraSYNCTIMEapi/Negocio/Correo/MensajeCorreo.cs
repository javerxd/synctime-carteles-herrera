namespace LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

// Un correo ya compuesto, listo para la cola: el negocio decide que decir y a quien; enviarlo es de
// Infraestructura. Referencia identifica a que se refiere (por ejemplo "solicitud:123"). Fase 11: EnlaceBaja es la
// direccion de la baja con un clic (cabecera List-Unsubscribe); solo la llevan los correos opcionales al cliente.
public sealed record MensajeCorreo(
    string Tipo,
    IReadOnlyList<string> Para,
    string Asunto,
    string Html,
    string Texto,
    string? ResponderA = null,
    string? Referencia = null,
    string? EnlaceBaja = null);

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

    // Fase 11: avisos de estado al cliente (opcionales, con enlace de baja) y el comprobante de baja (esencial).
    public const string EstadoSolicitud = "estado_solicitud";
    public const string EstadoPedido = "estado_pedido";
    public const string BajaCuenta = "baja_cuenta";
}

// Fase 11: que se puede dar de baja y que no.
//   - Avisos del taller: correos operativos del negocio; ninguna baja ni preferencia de un cliente los toca.
//   - Avisos de estado (opcionales): la preferencia avisos_estado del cliente; llevan el enlace de baja.
//   - El resto (verificacion, recuperacion, aviso de cambio de correo, comprobante de baja...) es esencial.
public static class CategoriasCorreo
{
    public const string AvisosEstado = "avisos_estado";

    private static readonly HashSet<string> DelTaller = [TiposCorreo.AvisoSolicitudNueva, TiposCorreo.AvisoTopeSolicitudes, TiposCorreo.ConsultaContacto];
    private static readonly HashSet<string> DeEstado = [TiposCorreo.EstadoSolicitud, TiposCorreo.EstadoPedido, TiposCorreo.CotizacionLista];

    private static readonly Dictionary<string, string> Textos = new(StringComparer.Ordinal)
    {
        [TiposCorreo.AvisoSolicitudNueva] = "Aviso de solicitud nueva",
        [TiposCorreo.AvisoTopeSolicitudes] = "Aviso de muchas solicitudes",
        [TiposCorreo.ConsultaContacto] = "Aviso de consulta de contacto",
        [TiposCorreo.VerificacionCorreo] = "Confirmación del correo",
        [TiposCorreo.RecuperacionCuenta] = "Código para restablecer la contraseña",
        [TiposCorreo.AvisoCambioCorreo] = "Aviso de cambio de correo",
        [TiposCorreo.ConfirmacionSolicitud] = "Confirmación de solicitud",
        [TiposCorreo.CotizacionLista] = "Cotización lista",
        [TiposCorreo.Prueba] = "Correo de prueba",
        [TiposCorreo.PruebaAvisos] = "Prueba de los avisos del taller",
        [TiposCorreo.ConfirmacionDestinoAvisos] = "Confirmación de un destino de avisos",
        [TiposCorreo.DestinoAvisosCambiado] = "Aviso de destino reemplazado",
        [TiposCorreo.DestinoAvisosAgregado] = "Aviso de destino agregado",
        [TiposCorreo.DestinoAvisosQuitado] = "Aviso de destino quitado",
        [TiposCorreo.AvisoCambioCorreoPersonal] = "Aviso de cambio de correo del personal",
        [TiposCorreo.AvisoAccesoReasignado] = "Aviso de acceso reasignado",
        [TiposCorreo.EstadoSolicitud] = "Estado de la solicitud",
        [TiposCorreo.EstadoPedido] = "Estado del pedido",
        [TiposCorreo.BajaCuenta] = "Comprobante de baja de la cuenta"
    };

    public static bool EsAvisoDelTaller(string? tipo) => tipo is not null && DelTaller.Contains(tipo);

    public static bool EsAvisoDeEstado(string? tipo) => tipo is not null && DeEstado.Contains(tipo);

    public static string Texto(string tipo) => Textos.GetValueOrDefault(tipo, tipo);

    // Varias direcciones separadas por punto y coma, enmascaradas y separadas por coma («u•••@taller.cl»).
    public static string Enmascarar(string? destinatarios) =>
        string.Join(", ", (destinatarios ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CorreosDestinosAvisos.Enmascarar));
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

    // Fase 11 (decision 1): los que quieren los avisos de ese origen. Si la tabla no tiene destinos activos, el
    // respaldo recibe solo las solicitudes en linea, como antes. Si ningun destino quiere ese origen, ninguno.
    Task<DestinatariosVigentes> LeerAsync(OrigenAviso origen, CancellationToken ct);
}

public sealed record DestinatariosVigentes(IReadOnlyList<string> Correos, bool DesdeRespaldo);

// De donde viene lo que se avisa: una solicitud en linea (o una consulta de la pagina) o una presencial.
public enum OrigenAviso
{
    EnLinea,
    Presencial
}
