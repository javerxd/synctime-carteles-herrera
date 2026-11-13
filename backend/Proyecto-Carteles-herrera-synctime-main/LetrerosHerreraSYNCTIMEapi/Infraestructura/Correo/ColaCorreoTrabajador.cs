using LetrerosHerreraSYNCTIMEapi.Datos.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;

public sealed record ResultadoLote(int Enviados, int Reintentar, int Fallidos, bool Detenido)
{
    public int Procesados => Enviados + Reintentar + Fallidos;
}

// Lo que un operador necesita saber de la cola: avisos que no salieron y avisos que llevan mucho esperando.
public sealed record ResumenCola(int FallidosRecientes, int Atrasados, DateTimeOffset? AtrasadoDesde)
{
    public bool HayProblemas => FallidosRecientes > 0 || Atrasados > 0;
}

// Envia lo pendiente de correos_salientes. Cada correo se "reclama" con una actualizacion condicional
// antes de enviarlo, asi dos procesos nunca envian el mismo; el reclamo suma el intento.
//   - Si falla el servidor o la cuenta (caido, sin conexion, contraseña cambiada), el correo se sigue
//     intentando cada 3 horas sin agotarse: en cuanto se arregla, el aviso sale.
//   - Si falla el propio mensaje, se agotan sus intentos y queda "fallido".
//   - Si el proceso se cae mientras envia, el correo se retoma al vencer su bloqueo; cuando eso ya paso
//     tantas veces como intentos admite, queda "fallido" en vez de repetirse sin fin.
// Los registros llevan el numero, el tipo y la categoria del fallo; nunca direcciones, asuntos ni el
// texto que respondio el servidor.
public sealed class TrabajadorColaCorreo(
    IServiceScopeFactory ambitos,
    IEnviadorCorreo enviador,
    IOptions<OpcionesCorreo> opciones,
    TimeProvider reloj,
    ILogger<TrabajadorColaCorreo> logger)
{
    private const string Interrumpido = "interrumpido: El envío se cortó demasiadas veces (el proceso se detuvo mientras enviaba).";

    private readonly SemaphoreSlim unoALaVez = new(1, 1);
    private string? motivoNoListoAnunciado;

    public string Descripcion => enviador.Descripcion;

    public bool Listo(out string? motivo) => enviador.Listo(out motivo);

    public async Task<ResultadoLote> ProcesarLoteAsync(CancellationToken ct)
    {
        await unoALaVez.WaitAsync(ct);
        try
        {
            return await ProcesarAsync(ct);
        }
        finally
        {
            unoALaVez.Release();
        }
    }

    private async Task<ResultadoLote> ProcesarAsync(CancellationToken ct)
    {
        if (!enviador.Listo(out var motivo))
        {
            // Una vez por motivo, no en cada vuelta; ProcesadorColaCorreo lo recuerda cada hora.
            if (motivo != motivoNoListoAnunciado)
                logger.LogError("El correo no se puede enviar: {Motivo} Los correos esperan en la cola sin gastar intentos.", motivo);
            motivoNoListoAnunciado = motivo;
            return new ResultadoLote(0, 0, 0, Detenido: true);
        }
        motivoNoListoAnunciado = null;

        var o = opciones.Value;
        var maximo = Math.Max(1, o.Cola.MaxIntentos);
        var enviados = 0;
        var reintentar = 0;
        var fallidos = 0;

        await using var ambito = ambitos.CreateAsyncScope();
        var db = ambito.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
        fallidos += await CerrarInterrumpidosAsync(db, maximo, ct);

        for (var i = 0; i < Math.Max(1, o.Cola.Lote); i++)
        {
            var correo = await ReclamarAsync(db, maximo, ct);
            if (correo is null)
                break;

            Exception? error = null;
            try
            {
                var mensaje = ConstructorMime.Construir(correo, o);
                await enviador.EnviarAsync(new CorreoParaEnviar(correo.IdCorreo, correo.Referencia, mensaje), ct);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                error = e;
            }

            // Solo si sigue siendo nuestro: si el bloqueo vencio y otro proceso lo tomo, no se pisa su estado.
            var propio = db.CorreosSalientes.Where(c =>
                c.IdCorreo == correo.IdCorreo && c.Estado == EstadosCorreo.Enviando && c.Intentos == correo.Intentos);

            // Ya entregado: se marca aunque la aplicacion se este cerrando, para no enviarlo de nuevo.
            if (error is null)
            {
                await propio.ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Estado, EstadosCorreo.Enviado)
                    .SetProperty(c => c.FechaEnvio, reloj.GetUtcNow())
                    .SetProperty(c => c.BloqueadoHasta, (DateTimeOffset?)null)
                    .SetProperty(c => c.UltimoError, (string?)null), CancellationToken.None);
                enviados++;
                logger.LogInformation("Correo {IdCorreo} ({Tipo}) enviado por {Medio} al intento {Intento}.",
                    correo.IdCorreo, correo.Tipo, enviador.Descripcion, correo.Intentos);
                continue;
            }

            var fallo = ClasificadorFallos.Clasificar(error);
            var espera = fallo.Permanente ? null : PoliticaReintentos.Espera(correo.Intentos, maximo, problemaDelServidor: fallo.DetieneLote);
            var ahora = reloj.GetUtcNow();

            // En un correo fallido, proximo_intento guarda cuando fallo: la purga cuenta desde ahi.
            await propio.ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Estado, espera is null ? EstadosCorreo.Fallido : EstadosCorreo.Pendiente)
                .SetProperty(c => c.ProximoIntento, espera is null ? ahora : ahora + espera.Value)
                .SetProperty(c => c.BloqueadoHasta, (DateTimeOffset?)null)
                .SetProperty(c => c.UltimoError, fallo.Texto), CancellationToken.None);

            // Solo el tipo de la excepcion: su mensaje puede traer el usuario o una direccion.
            if (espera is null)
            {
                fallidos++;
                logger.LogError("Correo {IdCorreo} ({Tipo}) no se envio y se dejo de intentar tras {Intentos} intento(s): {Fallo} [{TipoError}]",
                    correo.IdCorreo, correo.Tipo, correo.Intentos, fallo.Texto, error.GetType().Name);
            }
            else
            {
                reintentar++;
                logger.LogWarning("Correo {IdCorreo} ({Tipo}) fallo el intento {Intento}: {Fallo} [{TipoError}]. Se reintenta en {Minutos} min.",
                    correo.IdCorreo, correo.Tipo, correo.Intentos, fallo.Texto, error.GetType().Name, espera.Value.TotalMinutes);
            }

            if (fallo.DetieneLote)
                return new ResultadoLote(enviados, reintentar, fallidos, Detenido: true);
        }

        return new ResultadoLote(enviados, reintentar, fallidos, Detenido: false);
    }

    // Correos que quedaron "enviando" porque el proceso se detuvo y que ya gastaron todos sus intentos.
    private async Task<int> CerrarInterrumpidosAsync(LetrerosHerreradbContext db, int maximo, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var cerrados = await db.CorreosSalientes
            .Where(c => c.Estado == EstadosCorreo.Enviando && c.BloqueadoHasta < ahora && c.Intentos >= maximo)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Estado, EstadosCorreo.Fallido)
                .SetProperty(c => c.ProximoIntento, ahora)
                .SetProperty(c => c.BloqueadoHasta, (DateTimeOffset?)null)
                .SetProperty(c => c.UltimoError, Interrumpido), ct);
        if (cerrados > 0)
            logger.LogError("{Cantidad} correo(s) quedaron fallidos: su envio se corto {Maximo} veces.", cerrados, maximo);
        return cerrados;
    }

    private async Task<CorreoSaliente?> ReclamarAsync(LetrerosHerreradbContext db, int maximo, CancellationToken ct)
    {
        // Si otro proceso gana el reclamo, se busca el siguiente; el tope evita girar sin fin.
        for (var intento = 0; intento < 5; intento++)
        {
            var ahora = reloj.GetUtcNow();
            var id = await Disponibles(db, ahora, maximo)
                .OrderBy(c => c.ProximoIntento).ThenBy(c => c.IdCorreo)
                .Select(c => (long?)c.IdCorreo)
                .FirstOrDefaultAsync(ct);
            if (id is null)
                return null;

            var bloqueo = ahora + TimeSpan.FromSeconds(Math.Clamp(opciones.Value.Smtp.TiempoEsperaSegundos, 1, 300) * 3 + 60);
            var reclamados = await Disponibles(db, ahora, maximo).Where(c => c.IdCorreo == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Estado, EstadosCorreo.Enviando)
                    .SetProperty(c => c.BloqueadoHasta, bloqueo)
                    .SetProperty(c => c.Intentos, c => c.Intentos + 1), ct);
            if (reclamados == 1)
                return await db.CorreosSalientes.AsNoTracking().SingleAsync(c => c.IdCorreo == id, ct);
        }
        return null;
    }

    // Lo pendiente cuya hora llego, y lo que quedo "enviando" de un proceso que se cayo (si le quedan intentos).
    private static IQueryable<CorreoSaliente> Disponibles(LetrerosHerreradbContext db, DateTimeOffset ahora, int maximo) =>
        db.CorreosSalientes.Where(c =>
            (c.Estado == EstadosCorreo.Pendiente && c.ProximoIntento <= ahora)
            || (c.Estado == EstadosCorreo.Enviando && c.BloqueadoHasta < ahora && c.Intentos < maximo));

    // Borra lo ya resuelto pasado el plazo de retencion, porque el cuerpo lleva datos personales. Se cuenta
    // desde que se envio o desde que fallo, no desde que se creo: un aviso que espero mucho y fallo
    // al final conserva su rastro el plazo completo.
    public async Task<int> PurgarAsync(CancellationToken ct)
    {
        var limite = reloj.GetUtcNow().AddDays(-Math.Max(1, opciones.Value.Cola.DiasRetencion));
        await using var ambito = ambitos.CreateAsyncScope();
        var db = ambito.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
        var borrados = await db.CorreosSalientes
            .Where(c => (c.Estado == EstadosCorreo.Enviado && c.FechaEnvio < limite)
                     || (c.Estado == EstadosCorreo.Fallido && c.ProximoIntento < limite))
            .ExecuteDeleteAsync(ct);
        if (borrados > 0)
            logger.LogInformation("Se borraron {Cantidad} correo(s) antiguos de la cola.", borrados);
        return borrados;
    }

    // Fallidos de los ultimos 7 dias y pendientes que llevan mas de 30 minutos sin salir.
    public async Task<ResumenCola> ResumirAsync(CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var desdeFallidos = ahora.AddDays(-7);
        var atraso = ahora.AddMinutes(-30);
        await using var ambito = ambitos.CreateAsyncScope();
        var db = ambito.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();

        var fallidos = await db.CorreosSalientes.CountAsync(c => c.Estado == EstadosCorreo.Fallido && c.ProximoIntento >= desdeFallidos, ct);
        var atrasados = db.CorreosSalientes.Where(c =>
            (c.Estado == EstadosCorreo.Pendiente || c.Estado == EstadosCorreo.Enviando) && c.FechaCreacion < atraso);
        var cantidad = await atrasados.CountAsync(ct);
        var desde = cantidad == 0 ? null : await atrasados.MinAsync(c => (DateTimeOffset?)c.FechaCreacion, ct);
        return new ResumenCola(fallidos, cantidad, desde);
    }
}

// Proceso en segundo plano: despierta cuando se encola algo o cada Correo:Cola:IntervaloSegundos, y
// procesa un lote. Un error (por ejemplo, la base no disponible) se registra y se reintenta con espera
// creciente: nunca detiene la API. Cada hora purga lo antiguo y, si hay avisos fallidos o atrasados,
// lo vuelve a decir en el registro de errores.
public sealed class ProcesadorColaCorreo(
    TrabajadorColaCorreo trabajador,
    SenalColaCorreo senal,
    IOptions<OpcionesCorreo> opciones,
    IHostEnvironment entorno,
    TimeProvider reloj,
    ILogger<ProcesadorColaCorreo> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            if (!Anunciar())
                return;
        }
        catch (Exception error)
        {
            logger.LogError(error, "La cola de correo no pudo iniciar.");
            return;
        }

        var o = opciones.Value;
        var intervalo = TimeSpan.FromSeconds(Math.Max(1, o.Cola.IntervaloSegundos));
        var esperaTrasError = intervalo;
        var proximaRevision = reloj.GetUtcNow();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var resultado = await trabajador.ProcesarLoteAsync(ct);
                if (reloj.GetUtcNow() >= proximaRevision)
                {
                    await RevisarAsync(ct);
                    proximaRevision = reloj.GetUtcNow().AddHours(1);
                }
                esperaTrasError = intervalo;

                // Lote lleno y sin problemas: puede haber mas, se sigue sin esperar.
                if (!resultado.Detenido && resultado.Procesados >= Math.Max(1, o.Cola.Lote))
                    continue;
                await senal.EsperarAsync(intervalo, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "La cola de correo fallo; se vuelve a intentar en {Segundos} s.", esperaTrasError.TotalSeconds);
                try
                {
                    await Task.Delay(esperaTrasError, reloj, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                esperaTrasError = TimeSpan.FromSeconds(Math.Min(esperaTrasError.TotalSeconds * 2, 300));
            }
        }
    }

    // false si la cola no corre en este proceso.
    private bool Anunciar()
    {
        var o = opciones.Value;
        var desarrollo = entorno.IsDevelopment();
        if (!o.Cola.Activa)
        {
            // Las pruebas la apagan a proposito; fuera de desarrollo, los avisos se acumularian sin salir.
            logger.Log(desarrollo ? LogLevel.Information : LogLevel.Warning,
                "La cola de correo no se procesa en este proceso (Correo:Cola:Activa = false): los avisos quedan guardados sin enviarse.");
            return false;
        }
        if (o.EsBandeja && !desarrollo)
            logger.LogCritical("Correo:Modo es Bandeja fuera de desarrollo: no saldra ningun correo. Use Correo:Modo = Smtp.");
        logger.LogInformation("Cola de correo activa: envio por {Medio}.", trabajador.Descripcion);
        return true;
    }

    private async Task RevisarAsync(CancellationToken ct)
    {
        await trabajador.PurgarAsync(ct);
        var resumen = await trabajador.ResumirAsync(ct);
        if (resumen.HayProblemas)
            logger.LogError(
                "Avisos por correo con problemas: {Fallidos} fallido(s) en los ultimos 7 dias y {Atrasados} sin enviar hace mas de 30 min (el mas antiguo desde {Desde:u}). Revise /salud/correo, la cuenta SMTP y los registros anteriores.",
                resumen.FallidosRecientes, resumen.Atrasados, resumen.AtrasadoDesde);
        if (!trabajador.Listo(out var motivo))
            logger.LogError("El correo sigue sin poder enviarse: {Motivo}", motivo);
    }
}
