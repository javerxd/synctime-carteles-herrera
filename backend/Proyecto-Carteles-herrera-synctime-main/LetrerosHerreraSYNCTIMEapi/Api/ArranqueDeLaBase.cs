using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Storage;

namespace LetrerosHerreraSYNCTIMEapi.Api;

// Fase 16C-1 (H14, D10): con Azure SQL gratuita en pausa, la primera conexion falla o tarda; con Render gratuito, ademas, el servicio se duerme.
// Los pasos que tocan la base al arrancar (migraciones pendientes, destinatarios de los avisos, primer administrador) ya no impiden que la API
// levante: fuera de desarrollo corren en segundo plano y reintentan con espera creciente hasta que la base despierta.
//   BaseDeDatos:ArranqueMinutosMaximos          cuanto se insiste antes de rendirse (por omision, 30; 1 a 120)
//   BaseDeDatos:ArranqueEsperaInicialSegundos   primera espera entre intentos (por omision, 5; 1 a 300)
//   BaseDeDatos:ArranqueEsperaMaximaSegundos    tope de la espera, que se duplica en cada intento (por omision, 60; 1 a 600)
// Un valor fuera de rango impide arrancar (como el resto de la configuracion de despliegue).
public sealed class OpcionesArranqueBase
{
    public int MinutosMaximos { get; init; } = 30;
    public int EsperaInicialSegundos { get; init; } = 5;
    public int EsperaMaximaSegundos { get; init; } = 60;

    public static OpcionesArranqueBase Desde(IConfiguration configuracion)
    {
        var o = new OpcionesArranqueBase
        {
            MinutosMaximos = configuracion.GetValue("BaseDeDatos:ArranqueMinutosMaximos", 30),
            EsperaInicialSegundos = configuracion.GetValue("BaseDeDatos:ArranqueEsperaInicialSegundos", 5),
            EsperaMaximaSegundos = configuracion.GetValue("BaseDeDatos:ArranqueEsperaMaximaSegundos", 60)
        };
        Revisar("BaseDeDatos:ArranqueMinutosMaximos", o.MinutosMaximos, 1, 120);
        Revisar("BaseDeDatos:ArranqueEsperaInicialSegundos", o.EsperaInicialSegundos, 1, 300);
        Revisar("BaseDeDatos:ArranqueEsperaMaximaSegundos", o.EsperaMaximaSegundos, 1, 600);
        if (o.EsperaMaximaSegundos < o.EsperaInicialSegundos)
            throw new InvalidOperationException("BaseDeDatos:ArranqueEsperaMaximaSegundos no puede ser menor que BaseDeDatos:ArranqueEsperaInicialSegundos.");
        return o;
    }

    private static void Revisar(string clave, int valor, int minimo, int maximo)
    {
        if (valor < minimo || valor > maximo)
            throw new InvalidOperationException($"{clave} debe estar entre {minimo} y {maximo} y es {valor}.");
    }
}

public sealed record PasoDeArranque(string Nombre, Func<CancellationToken, Task> Ejecutar);

public static class ErroresDeBase
{
    // El error de una base que no contesta (pausada, despertando, red cortada): se espera y se reintenta. Cualquier otro (una configuracion
    // invalida, un error de programa) NO se esconde: sigue su curso. EF Core envuelve el fallo en otras excepciones, asi que se revisa la cadena.
    public static bool EsTransitorio(Exception error)
    {
        for (var actual = error; actual is not null; actual = actual.InnerException)
        {
            // Una credencial rechazada o una base que no existe NO es una base dormida: esperar 30 minutos esconderia un error de configuracion.
            if (actual is SqlException sql)
                return NumeroDe(sql) is not { } numero || !EsFalloPermanente(numero);
            if (actual is TimeoutException or IOException or SocketException or RetryLimitExceededException)
                return true;
        }
        return false;
    }

    /// <summary>Los errores de SQL Server que ninguna espera arregla: inicio de sesion rechazado (18456, 18452) y base de datos inexistente o inaccesible (4060, 4064).</summary>
    public static bool EsFalloPermanente(int numero) => numero is 18456 or 18452 or 4060 or 4064;

    private static int? NumeroDe(SqlException error)
    {
        try
        {
            return error.Number;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

public static class ArranqueTolerante
{
    /// <summary>
    /// Ejecuta los pasos en orden. Ante un fallo de la base dormida espera (con espera creciente) y reintenta ese paso; si pasa el plazo se rinde y
    /// devuelve false SIN lanzar (la API sigue en pie). Un error que no es de la base se propaga. Solo se registra el tipo del error: su texto puede
    /// traer el servidor o la cadena de conexion.
    /// </summary>
    public static async Task<bool> EjecutarAsync(
        IReadOnlyList<PasoDeArranque> pasos, OpcionesArranqueBase opciones, ILogger logger, Func<TimeSpan, CancellationToken, Task> pausa, TimeProvider reloj, CancellationToken ct)
    {
        var limite = reloj.GetUtcNow().AddMinutes(opciones.MinutosMaximos);
        foreach (var paso in pasos)
        {
            var espera = TimeSpan.FromSeconds(opciones.EsperaInicialSegundos);
            var intento = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                intento++;
                try
                {
                    await paso.Ejecutar(ct);
                    if (intento > 1)
                        logger.LogInformation("La base respondio: el paso de arranque «{Paso}» termino en el intento {Intento}.", paso.Nombre, intento);
                    break;
                }
                catch (Exception error) when (error is not OperationCanceledException && ErroresDeBase.EsTransitorio(error))
                {
                    if (reloj.GetUtcNow() >= limite)
                    {
                        logger.LogError(
                            "La base de datos no respondio en {Minutos} minuto(s): el paso de arranque «{Paso}» ({Tipo}) no se completo. La API sigue en pie; reinicie el servicio cuando la base este disponible.",
                            opciones.MinutosMaximos, paso.Nombre, error.GetType().Name);
                        return false;
                    }
                    logger.LogWarning(
                        "La base de datos no responde (¿pausada?): el paso de arranque «{Paso}» ({Tipo}) fallo en el intento {Intento}; se reintenta en {Segundos} s.",
                        paso.Nombre, error.GetType().Name, intento, (int)espera.TotalSeconds);
                    await pausa(espera, ct);
                    espera = TimeSpan.FromSeconds(Math.Min(espera.TotalSeconds * 2, opciones.EsperaMaximaSegundos));
                }
            }
        }
        return true;
    }
}

// Corre los pasos de arranque que tocan la base SIN bloquear el arranque del servidor (fuera de desarrollo). Un error que no es de la base dormida
// (por ejemplo una configuracion invalida del primer administrador) se registra como critico y cierra la API: es preferible a una API que acepta
// solicitudes y nunca avisa (el mismo criterio de siempre para la configuracion).
public sealed class TareasDeArranqueDeLaBase(
    IReadOnlyList<PasoDeArranque> pasos, OpcionesArranqueBase opciones, TimeProvider reloj, IHostApplicationLifetime vida, ILogger<TareasDeArranqueDeLaBase> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            // Cede el hilo: el servidor termina de arrancar y abre el puerto antes de que este proceso toque la base.
            await Task.Yield();
            await ArranqueTolerante.EjecutarAsync(pasos, opciones, logger, (espera, token) => Task.Delay(espera, reloj, token), reloj, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // El servidor se esta cerrando.
        }
        catch (Exception error)
        {
            // El texto de un error de la base o del SDK puede traer el servidor, el usuario o parte de la cadena de conexion: solo se registra el
            // de nuestras propias comprobaciones de configuracion («Falta Bootstrap:Admin:…»); de cualquier otro, solo el tipo.
            var propio = error is InvalidOperationException && error.Message.StartsWith("Falta Bootstrap:", StringComparison.Ordinal);
            logger.LogCritical("El arranque no pudo completarse ({Tipo}); se cierra la API.{Mensaje}", error.GetType().Name, propio ? " " + error.Message : " Revise la configuracion de la base y del primer administrador.");
            vida.StopApplication();
        }
    }
}
