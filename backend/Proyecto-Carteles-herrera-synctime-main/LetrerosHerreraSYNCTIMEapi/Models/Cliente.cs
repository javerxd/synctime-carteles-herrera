using System.Text.Json.Serialization;

namespace LetrerosHerreraSYNCTIMEapi.Models;

public class Cliente
{
    public int IdCliente { get; set; }
    public string? Rut { get; set; }
    public string Nombre { get; set; } = null!;
    public string Apellido { get; set; } = null!;
    public string Telefono { get; set; } = null!;
    public string? Email { get; set; }
    public string? Direccion { get; set; }

    [JsonIgnore]
    public string? PasswordHash { get; set; }

    public bool Registrado { get; set; }
    public DateTime FechaRegistro { get; set; }
    public bool Activo { get; set; } = true;

    // Fase 6: cuando confirmo que el correo es suyo (enlace o codigo de recuperacion). Null = sin verificar.
    public DateTimeOffset? FechaVerificacionCorreo { get; set; }

    // Fase 9 (RNFP10): version para que una edicion con datos viejos no pise la de otra persona.
    [JsonIgnore]
    public byte[] RowVersion { get; set; } = null!;

    // Fase 11 (RFU09, decision 4): si recibe los avisos de estado (cotizada, aceptada, pedido en proceso...).
    // Cada cambio queda tambien en consentimientos (tipo avisos_estado).
    public bool RecibeAvisosEstado { get; set; } = true;

    // Fase 11 (RFU34): la baja de la cuenta. La cuenta queda inhabilitada (sin contraseña ni sesiones) y se
    // conservan sus solicitudes, pedidos y abonos.
    public DateTimeOffset? InhabilitadoEn { get; set; }

    [JsonIgnore]
    public virtual ICollection<Solicitud> Solicitudes { get; set; } = new List<Solicitud>();

    // Fase 14A (RFS03): sus cuentas de Google vinculadas (a lo mas una). Una cuenta creada con Google no tiene contraseña.
    [JsonIgnore]
    public virtual ICollection<IdentidadExterna> IdentidadesExternas { get; set; } = new List<IdentidadExterna>();
}
