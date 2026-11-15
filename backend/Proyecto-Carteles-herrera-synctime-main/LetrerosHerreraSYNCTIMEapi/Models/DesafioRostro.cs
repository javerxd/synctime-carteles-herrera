namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 10: desafio de la prueba de vida. Lo pide el navegador antes de cada ingreso con rostro y dice en que
// orden girar la cabeza. Es de un solo uso y dura poco (RostroFacial:SegundosDesafio). Se guardan solo hashes:
// ni el token ni el email quedan en la base. La purga diaria borra los de mas de un dia.
public class DesafioRostro
{
    public int IdDesafio { get; set; }
    // SHA-256 del token, en hexadecimal.
    public string TokenHash { get; set; } = null!;
    // SHA-256 del email normalizado, en hexadecimal.
    public string CorreoHash { get; set; } = null!;
    // izquierda_derecha o derecha_izquierda (Negocio.Rostro.OrdenGiros).
    public string Orden { get; set; } = null!;
    public DateTimeOffset Emitido { get; set; }
    public DateTimeOffset Vence { get; set; }
    public DateTimeOffset? Usado { get; set; }
}
