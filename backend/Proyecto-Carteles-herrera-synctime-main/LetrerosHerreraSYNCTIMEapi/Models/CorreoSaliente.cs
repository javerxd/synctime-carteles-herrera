namespace LetrerosHerreraSYNCTIMEapi.Models;

// Cola de salida de correos (RFS08). La peticion que origina un correo solo inserta aqui una fila; el
// proceso TrabajadorColaCorreo la envia despues, con reintentos. Asi un servidor de correo lento o caido
// nunca hace esperar ni falla a quien envio, por ejemplo, una solicitud.
//   pendiente -> enviando -> enviado
//                         -> pendiente (reintento, con proximo_intento mas adelante)
//                         -> fallido   (se agotaron los intentos o el servidor lo rechazo para siempre)
public class CorreoSaliente
{
    public long IdCorreo { get; set; }

    // aviso_solicitud_nueva, prueba, ... (TiposCorreo).
    public string Tipo { get; set; } = null!;

    // A que se refiere, para rastrearlo sin abrir el cuerpo: por ejemplo "solicitud:123".
    public string? Referencia { get; set; }

    // Direcciones separadas por punto y coma, ya validadas al encolar.
    public string Destinatarios { get; set; } = null!;
    public string? ResponderA { get; set; }
    public string Asunto { get; set; } = null!;
    public string CuerpoHtml { get; set; } = null!;
    public string CuerpoTexto { get; set; } = null!;

    public string Estado { get; set; } = EstadosCorreo.Pendiente;
    public int Intentos { get; set; }
    public DateTimeOffset ProximoIntento { get; set; }

    // Mientras un proceso lo envia, nadie mas lo toma; si ese proceso se cae, al vencer se puede retomar.
    public DateTimeOffset? BloqueadoHasta { get; set; }

    // Categoria y mensaje fijo en español (ClasificadorFallos). Nunca el texto que devolvio el servidor,
    // que puede repetir el usuario de la cuenta o la direccion de destino.
    public string? UltimoError { get; set; }

    public DateTimeOffset FechaCreacion { get; set; }
    public DateTimeOffset? FechaEnvio { get; set; }
}

public static class EstadosCorreo
{
    public const string Pendiente = "pendiente";
    public const string Enviando = "enviando";
    public const string Enviado = "enviado";
    public const string Fallido = "fallido";
}
