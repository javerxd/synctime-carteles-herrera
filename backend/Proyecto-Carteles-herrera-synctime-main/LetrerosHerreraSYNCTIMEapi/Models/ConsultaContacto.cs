namespace LetrerosHerreraSYNCTIMEapi.Models;

// Consulta del formulario de contacto de la portada (RFS08, E22). Se guarda para que el taller la revise
// (fase 9) y se avisa por correo. Su consentimiento de privacidad queda en consentimientos con la
// referencia "contacto:{id}". Se borra a los 12 meses de recibida (PurgaDatosPersonales).
public class ConsultaContacto
{
    public int IdConsulta { get; set; }
    public string Nombre { get; set; } = null!;
    public string Correo { get; set; } = null!;
    public string? Telefono { get; set; }
    public string Asunto { get; set; } = null!;
    public string Mensaje { get; set; } = null!;
    public DateTimeOffset FechaRecepcion { get; set; }
    public bool Revisada { get; set; }
    public DateTimeOffset? FechaRevision { get; set; }
}
