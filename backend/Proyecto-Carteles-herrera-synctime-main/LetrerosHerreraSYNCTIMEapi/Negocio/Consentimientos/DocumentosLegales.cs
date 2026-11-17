namespace LetrerosHerreraSYNCTIMEapi.Negocio.Consentimientos;

// Documentos legales con su version vigente (RFS12). Cuando un texto cambia, su version sube y quien
// lo acepto antes debe aceptarlo de nuevo: cada consentimiento guarda la version que la persona leyo.
// Los textos se publican en la fase 5 (portada) como borradores, pendientes de revision legal.
public static class DocumentosLegales
{
    public const string Privacidad = "privacidad";
    public const string Terminos = "terminos";
    public const string Marketing = "marketing";
    public const string Cookies = "cookies";

    // Fase 11: la preferencia de los avisos de estado (RFU09). Cada cambio queda como aceptacion o revocacion.
    public const string AvisosEstado = "avisos_estado";

    public static readonly IReadOnlyDictionary<string, string> VersionesVigentes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // 1.1 (17-11-2026, fase 12B): agrega el plazo de 90 dias de la IP de la bitacora de auditoria.
        [Privacidad] = "1.1",
        [Terminos] = "1.0",
        [Marketing] = "1.0",
        [Cookies] = "1.0",
        [AvisosEstado] = "1.0"
    };

    public static bool EsTipo(string? tipo) => tipo is not null && VersionesVigentes.ContainsKey(tipo);

    public static string? VersionVigente(string tipo) => VersionesVigentes.GetValueOrDefault(tipo);
}

// De quien es un consentimiento: un cliente, un empleado o, si es un visitante sin cuenta, el envio que
// hizo (por ejemplo "contacto:15"). Siempre uno solo, igual que exige la base.
public sealed record TitularConsentimiento
{
    public int? IdCliente { get; }
    public int? IdEmpleado { get; }
    public string? ReferenciaEnvio { get; }

    private TitularConsentimiento(int? idCliente, int? idEmpleado, string? referenciaEnvio)
    {
        IdCliente = idCliente;
        IdEmpleado = idEmpleado;
        ReferenciaEnvio = referenciaEnvio;
    }

    public static TitularConsentimiento Cliente(int idCliente)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(idCliente);
        return new(idCliente, null, null);
    }

    public static TitularConsentimiento Empleado(int idEmpleado)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(idEmpleado);
        return new(null, idEmpleado, null);
    }

    public static TitularConsentimiento Envio(string referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia) || referencia.Trim().Length > 64)
            throw new ArgumentException("La referencia del envío es obligatoria y tiene hasta 64 caracteres.", nameof(referencia));
        return new(null, null, referencia.Trim());
    }
}
