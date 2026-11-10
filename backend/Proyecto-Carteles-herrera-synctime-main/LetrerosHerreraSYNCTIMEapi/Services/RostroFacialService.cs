using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// Cifra y compara descriptores faciales. Nunca debe registrar en logs un descriptor ni un token de equipo.
// El descriptor lo calcula el navegador: el servidor no puede saber si viene de una persona presente.
public sealed class RostroFacialService
{
    public const int Dimensiones = 128;
    public const int MinCapturas = 3;
    public const int MaxCapturas = 10;
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

    private readonly byte[]? clave;
    private readonly byte[]? senuelo;
    private readonly double umbral;
    private readonly int tiempoMinimoMs;
    private readonly TimeSpan vigenciaEquipo;
    private readonly ILogger<RostroFacialService> logger;

    public RostroFacialService(IConfiguration configuration, ILogger<RostroFacialService> logger)
    {
        this.logger = logger;
        clave = LeerClave(configuration["RostroFacial:ClaveCifrado"]);

        var umbralConfigurado = configuration.GetValue("RostroFacial:UmbralDistancia", 0.5);
        umbral = umbralConfigurado is > 0 and <= 1 ? umbralConfigurado : 0.5;
        tiempoMinimoMs = Math.Max(0, configuration.GetValue("RostroFacial:TiempoMinimoFalloMs", 400));
        vigenciaEquipo = TimeSpan.FromDays(Math.Max(1, configuration.GetValue("RostroFacial:DiasEquipo", 30)));

        if (clave is not null)
            senuelo = Cifrar(CrearDescriptorSenuelo(), idEmpleado: 0);
    }

    // Sin clave valida el login facial queda deshabilitado, pero la API y el login con contraseña siguen funcionando.
    public bool Configurado => clave is not null;

    // Solo dueño y administrador necesitan un equipo autorizado: el resto de los roles acepta ese riesgo.
    public static bool RequiereEquipo(string rol) => rol is Roles.Dueno or Roles.Administrador;

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

    // Reduce varias capturas a un solo vector: guarda menos datos biometricos que conservarlas todas.
    // Devuelve null si alguna no es valida o si no parecen ser de la misma persona.
    public float[]? Promediar(float[][]? capturas)
    {
        if (capturas is not { Length: >= MinCapturas and <= MaxCapturas } || !capturas.All(EsDescriptorValido))
            return null;

        var promedio = new float[Dimensiones];
        for (var i = 0; i < Dimensiones; i++)
            promedio[i] = capturas.Average(captura => captura[i]);

        return capturas.All(captura => Distancia(captura, promedio) <= umbral) ? promedio : null;
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

    // Sin descriptor guardado se compara contra un señuelo: asi todos los fallos hacen el mismo trabajo
    // y el tiempo de respuesta no revela si el email existe o tiene el rostro activado.
    public bool Coincide(float[] descriptor, byte[]? cifrado, int idEmpleado)
    {
        var guardado = cifrado is not null ? Descifrar(cifrado, idEmpleado) : Descifrar(senuelo!, 0);
        if (guardado is null)
        {
            // Para quien intenta es un fallo mas. Quien administra debe enterarse: suele indicar que cambio la clave o la libreria.
            logger.LogError("No se pudo descifrar el descriptor del empleado {IdEmpleado}: debe activar su rostro de nuevo.", idEmpleado);
            return false;
        }

        var cerca = Distancia(descriptor, guardado) <= umbral;
        Array.Clear(guardado);
        return cifrado is not null && cerca;
    }

    public static string CrearTokenEquipo() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public DateTimeOffset? VencimientoEquipo(RostroEmpleado rostro) => rostro.FechaAutorizacionEquipo + vigenciaEquipo;

    public bool EquipoAutorizado(RostroEmpleado rostro, string rol, string? token, DateTimeOffset ahora)
    {
        if (!RequiereEquipo(rol))
            return true;

        return !string.IsNullOrEmpty(token)
            && rostro.TokenEquipoHash is not null
            && VencimientoEquipo(rostro) > ahora
            && CryptographicOperations.FixedTimeEquals(HashToken(token), rostro.TokenEquipoHash);
    }

    // Todos los fallos tardan al menos esto, con algo de variacion: acota lo que se puede deducir del tiempo.
    public Task EsperarTiempoMinimoAsync(long inicio)
    {
        var objetivo = tiempoMinimoMs + RandomNumberGenerator.GetInt32(0, tiempoMinimoMs / 4 + 1);
        var restante = objetivo - Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
        return restante > 0 ? Task.Delay(TimeSpan.FromMilliseconds(restante)) : Task.CompletedTask;
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

    // Ata el dato a su dueño: un descriptor copiado a la fila de otro empleado no se puede descifrar.
    private static byte[] DatosAsociados(int idEmpleado) => Encoding.UTF8.GetBytes($"{ModeloDescriptor}:{idEmpleado}");

    private static double Distancia(float[] a, float[] b)
    {
        var suma = 0d;
        for (var i = 0; i < Dimensiones; i++)
        {
            var diferencia = (double)a[i] - b[i];
            suma += diferencia * diferencia;
        }
        return Math.Sqrt(suma);
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
