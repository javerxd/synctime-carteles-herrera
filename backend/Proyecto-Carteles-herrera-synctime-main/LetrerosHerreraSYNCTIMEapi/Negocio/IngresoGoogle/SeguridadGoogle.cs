using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.IngresoGoogle;

public sealed record NonceEmitido(string Nonce, DateTimeOffset Vence);

// El nonce liga cada token de Google a UN intento de ingreso (RNFE02): el navegador lo pide, se lo pasa a Google y Google lo
// copia dentro del token. Asi, un token robado o reutilizado no sirve. Es aleatorio, vence a los 5 minutos y solo se
// puede usar una vez. Vive en la memoria de la API: si la API se reinicia, los pendientes se pierden y la persona
// vuelve a pulsar el boton (falla del lado seguro). Con varias instancias detras de un balanceador habria que
// llevarlo a la base (fase 16; queda en desviaciones.md).
public sealed class NoncesGoogle(TimeProvider reloj, int maximo = 200_000)
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(5);
    // Con el tope lleno, vaciar los vencidos recorre toda la tabla: no se repite en cada peticion.
    private static readonly TimeSpan EsperaEntrePurgas = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, DateTimeOffset> pendientes = new(StringComparer.Ordinal);
    private readonly object candado = new();
    private DateTimeOffset ultimaPurga = DateTimeOffset.MinValue;

    // null si hay demasiados pendientes a la vez (quien los pide en bucle): el llamador responde «demasiados intentos».
    // El tope (200.000, unos 20 MB) queda muy por encima de lo que una persona real necesita y de lo que cada IP puede pedir.
    public NonceEmitido? Emitir()
    {
        var ahora = reloj.GetUtcNow();
        lock (candado)
        {
            if (pendientes.Count >= maximo && (ahora - ultimaPurga >= EsperaEntrePurgas || ahora < ultimaPurga))
            {
                Purgar(ahora);
                ultimaPurga = ahora;
            }
            if (pendientes.Count >= maximo)
                return null;
            var nonce = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
            var vence = ahora.Add(Vigencia);
            pendientes[nonce] = vence;
            return new NonceEmitido(nonce, vence);
        }
    }

    // true una sola vez por nonce emitido y aun vigente. Se consume al presentarlo, salga bien o mal lo demas.
    public bool Consumir(string? nonce)
    {
        if (string.IsNullOrEmpty(nonce) || nonce.Length > 100)
            return false;
        lock (candado)
            return pendientes.Remove(nonce, out var vence) && vence > reloj.GetUtcNow();
    }

    private void Purgar(DateTimeOffset ahora)
    {
        foreach (var vencido in pendientes.Where(p => p.Value <= ahora).Select(p => p.Key).ToList())
            pendientes.Remove(vencido);
    }
}

public sealed record PaseEmitido(string Pase, DateTimeOffset Vence);

public sealed record ContenidoPase(string Id, IdentidadGoogle Identidad, DateTimeOffset Vence);

// El «pase» del registro pendiente (RFS03, RFS12): cuando el correo de Google es nuevo, la API no crea nada todavia. Entrega
// este pase firmado (HMAC-SHA256) con lo que Google verifico: sub, correo y nombre. La pantalla «Completa tu registro»
// lo devuelve junto con lo que falta (telefono, consentimientos) y recien ahi se crea la cuenta. Vence a los 10 minutos y
// se usa una sola vez. El pase no es un secreto de otra persona: lleva los datos de quien lo recibe y solo vale mientras
// la firma sea autentica. Nunca se registra en logs.
public sealed class PaseGoogle(byte[] clave, TimeProvider reloj)
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(10);
    private const int LargoMaximo = 2000;
    private const int Version = 1;

    private readonly Dictionary<string, DateTimeOffset> usados = new(StringComparer.Ordinal);
    private readonly object candado = new();

    public int UsadosRecordados
    {
        get { lock (candado) return usados.Count; }
    }

    public PaseEmitido Emitir(IdentidadGoogle identidad)
    {
        var ahora = reloj.GetUtcNow();
        lock (candado)
            OlvidarVencidos(ahora);
        var vence = ahora.Add(Vigencia);
        var id = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
        var json = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["v"] = Version, ["j"] = id, ["s"] = identidad.Sub, ["c"] = identidad.Correo, ["n"] = identidad.Nombre, ["a"] = identidad.Apellido,
            ["e"] = vence.ToUnixTimeSeconds()
        });
        var carga = Base64Url.EncodeToString(json);
        return new PaseEmitido($"{carga}.{Base64Url.EncodeToString(Firmar(carga))}", DateTimeOffset.FromUnixTimeSeconds(vence.ToUnixTimeSeconds()));
    }

    // null si no es un pase nuestro, esta manipulado o ya vencio.
    public ContenidoPase? Leer(string? pase)
    {
        if (string.IsNullOrEmpty(pase) || pase.Length > LargoMaximo)
            return null;
        var partes = pase.Split('.');
        if (partes.Length != 2 || partes[0].Length == 0 || partes[1].Length == 0)
            return null;
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(Firmar(partes[0]), Base64Url.DecodeFromChars(partes[1])))
                return null;
            using var documento = JsonDocument.Parse(Base64Url.DecodeFromChars(partes[0]));
            var raiz = documento.RootElement;
            if (raiz.GetProperty("v").GetInt32() != Version)
                return null;
            var vence = DateTimeOffset.FromUnixTimeSeconds(raiz.GetProperty("e").GetInt64());
            if (vence <= reloj.GetUtcNow())
                return null;
            var sub = raiz.GetProperty("s").GetString();
            var correo = raiz.GetProperty("c").GetString();
            var id = raiz.GetProperty("j").GetString();
            if (string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(correo) || string.IsNullOrEmpty(id))
                return null;
            return new ContenidoPase(id, new IdentidadGoogle(sub, correo, raiz.GetProperty("n").GetString() ?? "", raiz.GetProperty("a").GetString() ?? ""), vence);
        }
        catch (Exception error) when (error is FormatException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        {
            return null;
        }
    }

    public bool EstaUsado(ContenidoPase pase)
    {
        lock (candado)
            return usados.ContainsKey(pase.Id);
    }

    // Se recuerda hasta que el pase venza: pasado ese momento ya no sirve por si solo.
    public void MarcarUsado(ContenidoPase pase)
    {
        lock (candado)
            usados[pase.Id] = pase.Vence;
    }

    private void OlvidarVencidos(DateTimeOffset ahora)
    {
        foreach (var id in usados.Where(u => u.Value <= ahora).Select(u => u.Key).ToList())
            usados.Remove(id);
    }

    private byte[] Firmar(string carga) => HMACSHA256.HashData(clave, Encoding.ASCII.GetBytes(carga));
}
