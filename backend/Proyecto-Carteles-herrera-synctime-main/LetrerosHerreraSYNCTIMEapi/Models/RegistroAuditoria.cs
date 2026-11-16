namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 11 (RFS12, RFU28, S20, E25): la bitacora general. La aplicacion solo inserta; el trigger
// trg_auditoria_inmutable rechaza todo UPDATE y DELETE. Nunca guarda contraseñas, hashes, tokens, descriptores,
// cuerpos ni asuntos de correo, ni direcciones completas: el detalle lleva los nombres de los campos y solo los
// valores de una lista permitida (ReglasAuditoria).
public class RegistroAuditoria
{
    public long IdAuditoria { get; set; }
    public DateTimeOffset Fecha { get; set; }

    // Quien: un empleado, un cliente o nadie (rol «sistema»: el trabajador de la cola, la purga, un enlace sin sesion).
    public int? IdEmpleado { get; set; }
    public int? IdCliente { get; set; }
    public string? Rol { get; set; }

    public string Entidad { get; set; } = null!;
    public string? IdEntidad { get; set; }
    public string Accion { get; set; } = null!;

    // JSON: { campos: [...], valores: { campo: { antes, despues } } } o un detalle propio de la accion.
    public string? Detalle { get; set; }
    public string? Ip { get; set; }
}

public static class AccionesAuditoria
{
    public const string Crear = "crear";
    public const string Modificar = "modificar";
    public const string Eliminar = "eliminar";
    public const string AvisoEnviado = "aviso_enviado";
    public const string CorreoFallido = "correo_fallido";
    public const string CorreoReenviado = "correo_reenviado";
    public const string CorreoRevisado = "correo_revisado";
    public const string BajaCorreo = "baja_correo";
    public const string BajaCuenta = "baja_cuenta";
    public const string DescargaDatos = "descarga_datos";

    public const string RolSistema = "sistema";
}
