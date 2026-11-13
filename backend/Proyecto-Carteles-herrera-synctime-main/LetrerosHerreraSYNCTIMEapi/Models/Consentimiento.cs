namespace LetrerosHerreraSYNCTIMEapi.Models;

// Registro de consentimientos (RFS12): quien acepto, que documento, en que version y cuando; y si lo
// retiro, cuando. No se edita, salvo para marcar la revocacion. Se borra solo al vencer el plazo de la
// politica de privacidad (PurgaDatosPersonales): 24 meses.
// El titular es uno solo: un cliente, un empleado o, si es un visitante sin cuenta, el envio que hizo
// (por ejemplo "contacto:15"). Lo exige ck_consentimientos_titular.
// El consentimiento del rostro tiene su propio registro en rostros_empleado.
public class Consentimiento
{
    public int IdConsentimiento { get; set; }
    public int? IdCliente { get; set; }
    public int? IdEmpleado { get; set; }
    public string? ReferenciaEnvio { get; set; }

    // privacidad, terminos, marketing o cookies (DocumentosLegales).
    public string Tipo { get; set; } = null!;
    public string VersionDocumento { get; set; } = null!;
    public DateTimeOffset FechaAceptacion { get; set; }
    public DateTimeOffset? FechaRevocacion { get; set; }

    public virtual Cliente? Cliente { get; set; }
    public virtual Empleado? Empleado { get; set; }
}
