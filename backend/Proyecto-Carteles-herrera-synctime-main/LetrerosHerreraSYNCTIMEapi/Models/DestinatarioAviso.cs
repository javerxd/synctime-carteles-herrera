namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 9: un correo que recibe los avisos del taller (solicitudes nuevas, consultas). Lo administran el
// dueño y el administrador desde su perfil.
//   pendiente -> activo     (alguien abrio el enlace de confirmacion)
//   pendiente -> cancelado  (gestion cancelo el cambio)
//   activo    -> retirado   (lo quitaron, o lo reemplazo otro que se confirmo)
// Mientras esta pendiente no recibe avisos: un error de tipeo no deja al taller sin avisos.
public class DestinatarioAviso
{
    public int IdDestinatario { get; set; }
    public string Correo { get; set; } = null!;
    public string Estado { get; set; } = EstadosDestinatario.Pendiente;

    // El activo que reemplaza: sigue recibiendo los avisos hasta que este se confirme.
    public int? IdReemplaza { get; set; }

    // SHA-256 del enlace de confirmacion (nunca el enlace). Solo mientras esta pendiente.
    public string? TokenHash { get; set; }
    public DateTimeOffset? Vence { get; set; }

    public int IdEmpleadoSolicita { get; set; }
    public DateTimeOffset FechaSolicitud { get; set; }
    public DateTimeOffset? FechaConfirmacion { get; set; }
    public DateTimeOffset? FechaRetiro { get; set; }

    // Fase 11 (decision 1): que solicitudes avisa este destino. Al menos una (ck_destinatarios_avisos_preferencia).
    public bool RecibeEnLinea { get; set; } = true;
    public bool RecibePresenciales { get; set; }

    // Fase 11: version para editar la preferencia con If-Match.
    public byte[] RowVersion { get; set; } = null!;

    public virtual DestinatarioAviso? Reemplaza { get; set; }
    public virtual Empleado? EmpleadoSolicita { get; set; }
}

public static class EstadosDestinatario
{
    public const string Pendiente = "pendiente";
    public const string Activo = "activo";
    public const string Retirado = "retirado";
    public const string Cancelado = "cancelado";
}

// Cada accion sobre los destinos de los avisos, con la direccion enmascarada. id_empleado es null cuando
// confirmo alguien desde el enlace, sin sesion. La bitacora general (fase 11) podra leer esta tabla.
public class DestinatarioAvisoHistorial
{
    public int IdHistorial { get; set; }
    public string Accion { get; set; } = null!;
    public string CorreoEnmascarado { get; set; } = null!;
    public int? IdDestinatario { get; set; }
    public int? IdEmpleado { get; set; }
    public DateTimeOffset Fecha { get; set; }
    public string? Detalle { get; set; }

    public virtual Empleado? Empleado { get; set; }
}

public static class AccionesDestinatario
{
    public const string Agregar = "agregar";
    public const string Reemplazar = "reemplazar";
    public const string Confirmar = "confirmar";
    public const string Cancelar = "cancelar";
    public const string Quitar = "quitar";
    public const string Reenviar = "reenviar";
    public const string Probar = "probar";
    public const string Preferencia = "preferencia";
}
