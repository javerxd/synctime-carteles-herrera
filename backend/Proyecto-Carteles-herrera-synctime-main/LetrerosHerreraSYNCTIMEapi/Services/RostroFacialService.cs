using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Rostro;
using LetrerosHerreraSYNCTIMEapi.Security;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// Cifra y compara descriptores faciales. Nunca debe registrar en logs un descriptor, un punto, un token de
// equipo ni un desafio. Los descriptores y los puntos los calcula el navegador: un cliente modificado podria
// falsificarlos. La prueba de vida (fase 10) frena una foto o una pantalla frente a la camara, pero no es
// infalible: por eso siguen la contraseña, el equipo autorizado y el bloqueo.
public sealed class RostroFacialService
{
    public const int Dimensiones = 128;
    public const int CapturasPorIntento = 3;
    public const int MaxFallos = 5;
    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    // Cambiar de modelo vuelve incomparables los descriptores: al ir en los datos asociados, los antiguos
    // dejan de descifrarse y cada empleado debe activar su rostro de nuevo.
    private const string ModeloDescriptor = "face-api-1.7.15-128";
    private const byte Formato = 1;
    private const int BytesNonce = 12;
    private const int BytesTag = 16;
    private const int BytesClave = 32;
    private const float ValorMaximo = 1.5f;

    private const double UmbralPorDefecto = 0.45; // calibrado por el usuario el 15-11-2026 (fase 10)
    private const double UmbralEntreCapturasPorDefecto = 0.6;
    private const int SegundosDesafioPorDefecto = 30;

    private readonly byte[]? clave;
    private readonly byte[]? senuelo;
    private readonly int tiempoMinimoMs;
    private readonly TimeSpan vigenciaEquipo;
    private readonly ILogger<RostroFacialService> logger;

    public RostroFacialService(IConfiguration configuration, ILogger<RostroFacialService> logger)
    {
        this.logger = logger;
        clave = LeerClave(configuration["RostroFacial:ClaveCifrado"]);

        Umbral = Entre(configuration.GetValue("RostroFacial:UmbralDistancia", UmbralPorDefecto), UmbralPorDefecto, "UmbralDistancia");
        UmbralEntreCapturas = Entre(configuration.GetValue("RostroFacial:UmbralEntreCapturas", UmbralEntreCapturasPorDefecto),
            UmbralEntreCapturasPorDefecto, "UmbralEntreCapturas");

        var rangos = new RangosGiro(
            configuration.GetValue("RostroFacial:FrenteMaximo", RangosGiro.PorDefecto.FrenteMaximo),
            configuration.GetValue("RostroFacial:GiroMinimo", RangosGiro.PorDefecto.GiroMinimo),
            configuration.GetValue("RostroFacial:GiroMaximo", RangosGiro.PorDefecto.GiroMaximo));
        if (!rangos.SonValidos)
        {
            // Un rango sin sentido dejaria entrar sin girar o no dejaria entrar a nadie: se usan los de siempre.
            logger.LogWarning("Los rangos de giro de RostroFacial (FrenteMaximo < GiroMinimo < GiroMaximo <= 1) no son validos: se usan los valores por defecto.");
            rangos = RangosGiro.PorDefecto;
        }
        Rangos = rangos;

        SegundosDesafio = Math.Clamp(configuration.GetValue("RostroFacial:SegundosDesafio", SegundosDesafioPorDefecto), 10, 120);
        tiempoMinimoMs = Math.Max(0, configuration.GetValue("RostroFacial:TiempoMinimoFalloMs", 400));
        vigenciaEquipo = TimeSpan.FromDays(Math.Max(1, configuration.GetValue("RostroFacial:DiasEquipo", 30)));

        if (clave is not null)
            senuelo = Cifrar(CrearDescriptorSenuelo(), idEmpleado: 0);
    }

    // Sin clave valida el login facial queda deshabilitado, pero la API y el login con contraseña siguen funcionando.
    public bool Configurado => clave is not null;

    // Distancia maxima entre una captura y la muestra de su lado.
    public double Umbral { get; }

    // Distancia maxima entre dos capturas del mismo intento (o dos muestras del mismo registro).
    public double UmbralEntreCapturas { get; }

    public RangosGiro Rangos { get; }

    public int SegundosDesafio { get; }

    // Fase 10: solo dueño y administrador usan el rostro, y los dos necesitan un equipo autorizado.
    public static bool EsGestion(string rol) => rol is Roles.Dueno or Roles.Administrador;

    public static bool RequiereEquipo(string rol) => EsGestion(rol);

    public static bool EsDescriptorValido(float[]? descriptor)
    {
        if (descriptor is not { Length: Dimensiones })
            return false;

        var distintoDeCero = false;
        foreach (var valor in descriptor)
        {
            if (!float.IsFinite(valor) || Math.Abs(valor) > ValorMaximo)
                return false;
            distintoDeCero |= valor != 0;
        }
        return distintoDeCero;
    }

    public byte[] Cifrar(float[] descriptor, int idEmpleado)
    {
        var claro = new byte[Dimensiones * sizeof(float)];
        Buffer.BlockCopy(descriptor, 0, claro, 0, claro.Length);

        var cifrado = new byte[1 + BytesNonce + BytesTag + claro.Length];
        cifrado[0] = Formato;
        var nonce = cifrado.AsSpan(1, BytesNonce);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(clave!, BytesTag);
        aes.Encrypt(nonce, claro, cifrado.AsSpan(1 + BytesNonce + BytesTag), cifrado.AsSpan(1 + BytesNonce, BytesTag), DatosAsociados(idEmpleado));
        CryptographicOperations.ZeroMemory(claro);
        return cifrado;
    }

    // Distancias de un intento: cada captura contra la muestra que le toca (la de frente con la de frente y
    // cada giro con la muestra de su lado, segun el orden del desafio), y la mayor entre las tres capturas.
    // Sin muestras se mide contra un señuelo: asi todo intento hace el mismo trabajo y el tiempo no revela si
    // el email existe o tiene rostro. null si alguna muestra guardada no se pudo descifrar.
    public MedicionIntento? Medir(float[][] capturas, LadoGiro primero, MuestrasCifradas? muestras, int idEmpleado)
    {
        var (frente, izquierda, derecha, id) = muestras is { } m
            ? (m.Frente, m.Izquierda, m.Derecha, idEmpleado)
            : (senuelo!, senuelo!, senuelo!, 0);

        var claroFrente = Descifrar(frente, id);
        var claroIzquierda = Descifrar(izquierda, id);
        var claroDerecha = Descifrar(derecha, id);
        try
        {
            // Para quien intenta es un fallo mas. Quien llama decide si avisarlo en el log (suele indicar que
            // cambio la clave o la libreria): el ingreso lo hace solo con un equipo valido.
            if (claroFrente is null || claroIzquierda is null || claroDerecha is null)
                return null;

            var (muestraPrimero, muestraSegundo) = primero == LadoGiro.Izquierda ? (claroIzquierda, claroDerecha) : (claroDerecha, claroIzquierda);
            return new MedicionIntento(
                Distancia(capturas[0], claroFrente),
                Distancia(capturas[1], muestraPrimero),
                Distancia(capturas[2], muestraSegundo),
                MayorDistanciaEntre(capturas));
        }
        finally
        {
            Limpiar(claroFrente);
            Limpiar(claroIzquierda);
            Limpiar(claroDerecha);
        }
    }

    // El intento coincide si cada captura queda cerca de su muestra y las tres capturas cerca entre si.
    public bool Coincide(MedicionIntento? medicion) =>
        medicion is { } m
        && m.Frente <= Umbral && m.PrimerGiro <= Umbral && m.SegundoGiro <= Umbral
        && m.MaxEntreCapturas <= UmbralEntreCapturas;

    public static double MayorDistanciaEntre(IReadOnlyList<float[]> descriptores)
    {
        var mayor = 0d;
        for (var i = 0; i < descriptores.Count; i++)
            for (var j = i + 1; j < descriptores.Count; j++)
                mayor = Math.Max(mayor, Distancia(descriptores[i], descriptores[j]));
        return mayor;
    }

    public static double Distancia(float[] a, float[] b)
    {
        var suma = 0d;
        for (var i = 0; i < Dimensiones; i++)
        {
            var diferencia = (double)a[i] - b[i];
            suma += diferencia * diferencia;
        }
        return Math.Sqrt(suma);
    }

    public static string CrearToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string CrearTokenEquipo() => CrearToken();

    public static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    // SHA-256 en hexadecimal (minusculas), para las columnas char(64).
    public static string HashHex(string texto) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));

    public DateTimeOffset? VencimientoEquipo(RostroEmpleado rostro) => rostro.FechaAutorizacionEquipo + vigenciaEquipo;

    public bool EquipoAutorizado(RostroEmpleado rostro, string rol, string? token, DateTimeOffset ahora)
    {
        if (!RequiereEquipo(rol))
            return true;

        return !string.IsNullOrEmpty(token)
            && token.Length <= 100
            && rostro.TokenEquipoHash is not null
            && VencimientoEquipo(rostro) > ahora
            && CryptographicOperations.FixedTimeEquals(HashToken(token), rostro.TokenEquipoHash);
    }

    // Todos los fallos tardan al menos esto, con algo de variacion: acota lo que se puede deducir del tiempo.
    public Task EsperarTiempoMinimoAsync(long inicio, CancellationToken ct = default)
    {
        var objetivo = tiempoMinimoMs + RandomNumberGenerator.GetInt32(0, tiempoMinimoMs / 4 + 1);
        var restante = objetivo - Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
        return restante > 0 ? Task.Delay(TimeSpan.FromMilliseconds(restante), ct) : Task.CompletedTask;
    }

    private float[]? Descifrar(byte[] cifrado, int idEmpleado)
    {
        if (cifrado.Length != 1 + BytesNonce + BytesTag + Dimensiones * sizeof(float) || cifrado[0] != Formato)
            return null;

        var claro = new byte[Dimensiones * sizeof(float)];
        try
        {
            using var aes = new AesGcm(clave!, BytesTag);
            aes.Decrypt(cifrado.AsSpan(1, BytesNonce), cifrado.AsSpan(1 + BytesNonce + BytesTag), cifrado.AsSpan(1 + BytesNonce, BytesTag), claro, DatosAsociados(idEmpleado));
        }
        catch (CryptographicException)
        {
            return null;
        }

        var descriptor = new float[Dimensiones];
        Buffer.BlockCopy(claro, 0, descriptor, 0, claro.Length);
        CryptographicOperations.ZeroMemory(claro);
        return descriptor;
    }

    private static void Limpiar(float[]? descriptor)
    {
        if (descriptor is not null)
            Array.Clear(descriptor);
    }

    // Ata el dato a su dueño: un descriptor copiado a la fila de otro empleado no se puede descifrar.
    private static byte[] DatosAsociados(int idEmpleado) => Encoding.UTF8.GetBytes($"{ModeloDescriptor}:{idEmpleado}");

    private double Entre(double valor, double porDefecto, string nombre)
    {
        if (valor is > 0 and <= 1)
            return valor;
        logger.LogWarning("RostroFacial:{Nombre} no es valido: se usa {PorDefecto}.", nombre, porDefecto);
        return porDefecto;
    }

    private static byte[]? LeerClave(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return null;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            return bytes.Length == BytesClave ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static float[] CrearDescriptorSenuelo()
    {
        var descriptor = new float[Dimensiones];
        for (var i = 0; i < Dimensiones; i++)
            descriptor[i] = RandomNumberGenerator.GetInt32(-1000, 1001) / 10000f;
        return descriptor;
    }
}

// Las tres muestras cifradas de un rostro.
public sealed record MuestrasCifradas(byte[] Frente, byte[] Izquierda, byte[] Derecha)
{
    public static MuestrasCifradas? De(RostroEmpleado rostro) =>
        rostro.TieneTresMuestras ? new MuestrasCifradas(rostro.DescriptorFrente!, rostro.DescriptorIzquierda!, rostro.DescriptorDerecha!) : null;
}

// Distancias de un intento con tres capturas: contra las muestras, y la mayor entre las capturas.
public sealed record MedicionIntento(double Frente, double PrimerGiro, double SegundoGiro, double MaxEntreCapturas);
