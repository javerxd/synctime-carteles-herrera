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

    [JsonIgnore]
    public virtual ICollection<Solicitud> Solicitudes { get; set; } = new List<Solicitud>();
}
