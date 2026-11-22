using System.Text.Json.Serialization;

namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 14A (RFS03): la cuenta de otro proveedor (por ahora, Google) vinculada a un cliente. Fase 14C: o a un empleado (exactamente uno de los dos: CHECK en la base). La identidad es el «sub» del
// proveedor, que no cambia, y no el correo (que la persona puede cambiar). Un mismo sub no puede estar en dos cuentas
// y una cuenta no puede tener dos Google: lo impide la base con indices unicos, aunque dos peticiones lleguen a la vez.
// Nunca se guarda el token de Google: solo quien es y cuando entro.
public class IdentidadExterna
{
    public const string Google = "google";

    public int IdIdentidad { get; set; }
    public int? IdCliente { get; set; }
    // Fase 14C: el empleado que vinculo su Google desde Mi perfil (el personal tambien entra por Google).
    public int? IdEmpleado { get; set; }
    public string Proveedor { get; set; } = Google;
    public string Sub { get; set; } = null!;
    // El correo que verifico el proveedor al vincular (es el de la cuenta en ese momento).
    public string Correo { get; set; } = null!;
    public DateTimeOffset FechaVinculacion { get; set; }
    public DateTimeOffset? FechaUltimoIngreso { get; set; }

    [JsonIgnore]
    public virtual Cliente? Cliente { get; set; }

    [JsonIgnore]
    public virtual Empleado? Empleado { get; set; }
}
