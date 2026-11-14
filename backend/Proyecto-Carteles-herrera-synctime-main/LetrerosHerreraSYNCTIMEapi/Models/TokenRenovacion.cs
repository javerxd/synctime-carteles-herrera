namespace LetrerosHerreraSYNCTIMEapi.Models;

// Token de renovacion de la sesion (fase 6, RNFP08). El valor viaja solo en una cookie httpOnly; aqui
// se guarda su hash SHA-256. Cada renovacion marca este token como usado y emite otro de la misma
// familia (una familia = un inicio de sesion), que conserva la fecha de expiracion del primero: la
// sesion dura como maximo eso desde que se ingreso. Si un token ya usado vuelve a presentarse, alguien
// lo copio: se revoca la familia entera.
public class TokenRenovacion
{
    public long IdToken { get; set; }
    public int? IdCliente { get; set; }
    public int? IdEmpleado { get; set; }
    public string Hash { get; set; } = null!;
    public Guid Familia { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }
    public DateTimeOffset FechaExpiracion { get; set; }
    public DateTimeOffset? FechaUso { get; set; }
    public DateTimeOffset? FechaRevocacion { get; set; }

    public virtual Cliente? Cliente { get; set; }
    public virtual Empleado? Empleado { get; set; }
}
