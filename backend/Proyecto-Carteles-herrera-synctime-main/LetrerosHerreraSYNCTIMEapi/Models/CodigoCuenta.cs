namespace LetrerosHerreraSYNCTIMEapi.Models;

// Codigos de un solo uso de las cuentas (fase 6):
// - verificacion_correo: enlace que confirma que el correo es de quien creo la cuenta (24 horas).
// - recuperacion: codigo de 6 digitos para restablecer la contraseña (15 minutos, RNFP13, 5 intentos).
// Solo se guarda el hash; Correo es la direccion a la que se envio (la que queda verificada).
public class CodigoCuenta
{
    public const string VerificacionCorreo = "verificacion_correo";
    public const string Recuperacion = "recuperacion";

    public long IdCodigo { get; set; }
    public int? IdCliente { get; set; }
    public int? IdEmpleado { get; set; }
    public string Proposito { get; set; } = null!;
    public string Hash { get; set; } = null!;
    public string Correo { get; set; } = null!;
    public DateTimeOffset FechaCreacion { get; set; }
    public DateTimeOffset FechaExpiracion { get; set; }
    public DateTimeOffset? FechaUso { get; set; }
    public int Intentos { get; set; }

    public virtual Cliente? Cliente { get; set; }
    public virtual Empleado? Empleado { get; set; }
}
