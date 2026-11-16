namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 11 (seccion 7): una Idempotency-Key ya usada. La primera peticion la reserva (estado_http null) y, si
// termina bien, guarda su respuesta; una repeticion con el mismo cuerpo recibe esa misma respuesta sin crear
// nada. Unica por (clave, ambito, tipo_usuario, id_usuario). Dura 24 horas; la purga diaria borra las vencidas.
public class ClaveIdempotencia
{
    public long IdClave { get; set; }
    public string Clave { get; set; } = null!;
    public string Ambito { get; set; } = null!;
    public int IdUsuario { get; set; }
    public string TipoUsuario { get; set; } = null!;

    // SHA-256 (hex) del metodo, la ruta concreta y el cuerpo.
    public string HashCuerpo { get; set; } = null!;
    public int? EstadoHttp { get; set; }
    public string? Respuesta { get; set; }
    public DateTimeOffset Fecha { get; set; }
    public DateTimeOffset Vence { get; set; }
}
