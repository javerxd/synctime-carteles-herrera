using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// Fase 13A (RNFP18): genera en segundo plano los archivos que la gente pidio. Mismo patron que la cola de correo:
//   - Cada trabajo se «reclama» con una actualizacion condicional (pendiente → procesando) antes de generarlo: dos procesos nunca
//     generan el mismo. El reclamo suma el intento y fija hasta cuando es suyo.
//   - A la vez, como mucho Reportes:MaximoSimultaneos (2), cada uno con un tiempo limite.
//   - Si el servidor se reinicia a mitad, el trabajo vuelve a «pendiente»; si ya se interrumpio Reportes:MaximoIntentos veces, queda
//     «fallido» para no repetirse sin fin.
//   - Al terminar o fallar queda una fila en la bitacora (exportar_reporte) con quien lo pidio, cuantas filas y, si fallo, el motivo;
//     nunca el contenido del archivo.
//   - Los archivos vencen (Reportes:HorasDeVigencia) y la purga los borra; el registro del trabajo queda sin el contenido.
public sealed class TrabajadorExportaciones(
    IServiceScopeFactory ambitos, AlmacenExportaciones almacen, IOptions<OpcionesReportes> opciones, TimeProvider reloj, ILogger<TrabajadorExportaciones> logger)
{
    private const string MensajeInterrumpido = "El servidor se reinició mientras se preparaba el archivo y no se pudo completar. Vuelve a pedirlo.";
    private const string MensajeLento = "El reporte tardó demasiado en prepararse. Acota el rango de fechas o agrega filtros y vuelve a pedirlo.";
    private const string MensajeFallo = "No se pudo preparar el archivo. Inténtalo de nuevo; si sigue fallando, avisa al administrador.";

    private OpcionesReportes O => opciones.Value;

    /// <summary>Trabajos que se toman a la vez (el mismo valor que usa el proceso para saber si el lote salio lleno).</summary>
    public int Cupos => Math.Clamp(O.MaximoSimultaneos, 1, 8);

    // Un trabajo que lleva mas de esto «pendiente» no se va a procesar (la cola esta apagada o rota): se cierra para no bloquear a la persona.
    private static readonly TimeSpan EsperaMaximaPendiente = TimeSpan.FromHours(1);

    // ── Recuperar lo que quedo a medias ──

    /// <summary>
    /// Los trabajos «procesando» de un proceso que se cayo vuelven a «pendiente» (o quedan «fallidos» si ya se interrumpieron demasiadas
    /// veces). Con <paramref name="alIniciar"/> son todos (este proceso recien arranca: nadie mas esta generando); despues, solo
    /// los que ya pasaron su plazo.
    /// </summary>
    public async Task<int> RecuperarInterrumpidosAsync(bool alIniciar, CancellationToken ct)
    {
        await using var ambito = ambitos.CreateAsyncScope();
        var db = ambito.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
        var auditoria = ambito.ServiceProvider.GetRequiredService<IAuditoria>();
        var ahora = ServicioExportaciones.EnSegundos(reloj.GetUtcNow());
        var maximo = (byte)Math.Clamp(O.MaximoIntentos, 1, 20);

        var abandonados = await db.TrabajosExportacion.AsNoTracking()
            .Where(t => t.Estado == EstadosExportacion.Procesando && (alIniciar || t.BloqueadoHasta < ahora))
            .ToListAsync(ct);
        var recuperados = 0;
        foreach (var trabajo in abandonados)
        {
            // Cada uno con las mismas condiciones: si otro proceso lo tomo entretanto, no se pisa.
            if (trabajo.Intentos >= maximo)
            {
                var cerrados = await db.TrabajosExportacion
                    .Where(t => t.IdTrabajo == trabajo.IdTrabajo && t.Estado == EstadosExportacion.Procesando && t.Intentos == trabajo.Intentos)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.Estado, EstadosExportacion.Fallido)
                        .SetProperty(t => t.FechaTermino, ahora)
                        .SetProperty(t => t.BloqueadoHasta, (DateTimeOffset?)null)
                        .SetProperty(t => t.Error, MensajeInterrumpido), ct);
                if (cerrados == 1)
                {
                    await AuditarAsync(auditoria, trabajo, "fallido", filas: null, MensajeInterrumpido);
                    recuperados++;
                }
                continue;
            }
            var devueltos = await db.TrabajosExportacion
                .Where(t => t.IdTrabajo == trabajo.IdTrabajo && t.Estado == EstadosExportacion.Procesando && t.Intentos == trabajo.Intentos)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Estado, EstadosExportacion.Pendiente)
                    .SetProperty(t => t.BloqueadoHasta, (DateTimeOffset?)null), ct);
            recuperados += devueltos;
        }
        var limitePendiente = ahora.Subtract(EsperaMaximaPendiente);
        var antiguos = await db.TrabajosExportacion.AsNoTracking()
            .Where(t => t.Estado == EstadosExportacion.Pendiente && t.FechaPedido < limitePendiente).ToListAsync(ct);
        foreach (var trabajo in antiguos)
        {
            const string motivo = "El archivo no se llegó a preparar a tiempo. Vuelve a pedirlo; si se repite, avisa al administrador.";
            var cerrado = await db.TrabajosExportacion.Where(t => t.IdTrabajo == trabajo.IdTrabajo && t.Estado == EstadosExportacion.Pendiente)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Estado, EstadosExportacion.Fallido).SetProperty(t => t.FechaTermino, ahora).SetProperty(t => t.Error, motivo), ct);
            if (cerrado == 1)
            {
                await AuditarAsync(auditoria, trabajo, "fallido", filas: null, motivo);
                recuperados++;
            }
        }
        if (recuperados > 0)
            logger.LogWarning("{Cantidad} exportacion(es) quedaron a medias y se retomaron o cerraron.", recuperados);
        return recuperados;
    }

    // ── Procesar ──

    /// <summary>Toma los trabajos pendientes (hasta el maximo simultaneo), los genera y devuelve cuantos tomo.</summary>
    public async Task<int> ProcesarLoteAsync(CancellationToken ct)
    {
        var reclamados = new List<TrabajoExportacion>();
        await using (var ambito = ambitos.CreateAsyncScope())
        {
            var db = ambito.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
            for (var i = 0; i < Cupos; i++)
            {
                if (await ReclamarAsync(db, ct) is not { } trabajo)
                    break;
                reclamados.Add(trabajo);
            }
        }
        await Task.WhenAll(reclamados.Select(t => ProcesarUnoAsync(t, ct)));
        return reclamados.Count;
    }

    private async Task<TrabajoExportacion?> ReclamarAsync(LetrerosHerreradbContext db, CancellationToken ct)
    {
        // Si otro proceso gana el reclamo, se busca el siguiente; el tope evita girar sin fin.
        for (var intento = 0; intento < 5; intento++)
        {
            var ahora = ServicioExportaciones.EnSegundos(reloj.GetUtcNow());
            var id = await db.TrabajosExportacion.Where(t => t.Estado == EstadosExportacion.Pendiente)
                .OrderBy(t => t.FechaPedido).ThenBy(t => t.IdTrabajo)
                .Select(t => (Guid?)t.IdTrabajo).FirstOrDefaultAsync(ct);
            if (id is null)
                return null;

            var bloqueo = ahora.AddSeconds(Math.Clamp(O.SegundosLimitePorTrabajo, 5, 3600) + 60);
            var tomados = await db.TrabajosExportacion.Where(t => t.IdTrabajo == id && t.Estado == EstadosExportacion.Pendiente)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Estado, EstadosExportacion.Procesando)
                    .SetProperty(t => t.FechaInicio, ahora)
                    .SetProperty(t => t.BloqueadoHasta, bloqueo)
                    .SetProperty(t => t.Intentos, t => (byte)(t.Intentos + 1)), ct);
            if (tomados == 1)
                return await db.TrabajosExportacion.AsNoTracking().SingleAsync(t => t.IdTrabajo == id, ct);
        }
        return null;
    }

    private async Task ProcesarUnoAsync(TrabajoExportacion trabajo, CancellationToken ct)
    {
        await using var ambito = ambitos.CreateAsyncScope();
        var db = ambito.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
        var auditoria = ambito.ServiceProvider.GetRequiredService<IAuditoria>();
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(O.SegundosLimitePorTrabajo, 5, 3600)));

        try
        {
            var filtros = JsonSerializer.Deserialize<Dictionary<string, string>>(trabajo.Filtros) ?? [];
            var (filtro, errores) = ReglasReportes.Validar(trabajo.Reporte, filtros.ToDictionary(p => p.Key, p => (string?)p.Value));
            if (filtro is null)
                throw new InvalidOperationException($"Filtros guardados invalidos: {string.Join(", ", errores.Keys)}.");

            // «Hoy» es el del dia en que se pidio (no el de cuando se genere): el periodo relativo (este mes) no cambia de un dia al otro.
            var hoy = TiempoChile.Hoy(new FechaFija(trabajo.FechaPedido));
            var servicio = ambito.ServiceProvider.GetRequiredService<ServicioReportes>();
            var documento = await servicio.ConstruirAsync(filtro, hoy, reloj.GetUtcNow(), limite.Token);

            if (trabajo.Formato == FormatosReporte.Pdf && ReglasReportes.ExcedeElTope(documento.TotalDeFilas, O.MaximoDeFilasPdf))
                throw new ReporteDemasiadoGrandeException(ReglasReportes.MensajeDelTopePdf(O.MaximoDeFilasPdf));

            // Generar es CPU y no admite cancelacion: se espera con el limite de tiempo y, si vence, se descarta lo que salga.
            var archivo = await Task.Run(() => trabajo.Formato switch
            {
                FormatosReporte.Xlsx => GeneradorExcel.Generar(documento, limite.Token),
                FormatosReporte.Csv => GeneradorCsv.Generar(documento, limite.Token),
                _ => GeneradorPdf.Generar(documento, limite.Token)
            }, limite.Token).WaitAsync(limite.Token);

            await almacen.GuardarAsync(trabajo.IdTrabajo, trabajo.Formato, archivo, limite.Token);
            var termino = ServicioExportaciones.EnSegundos(reloj.GetUtcNow());
            var nombre = ReglasReportes.NombreArchivo(trabajo.Reporte, trabajo.Formato, TiempoChile.Hoy(reloj));
            var cerrado = await db.TrabajosExportacion
                .Where(t => t.IdTrabajo == trabajo.IdTrabajo && t.Estado == EstadosExportacion.Procesando && t.Intentos == trabajo.Intentos)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Estado, EstadosExportacion.Listo)
                    .SetProperty(t => t.FechaTermino, termino)
                    .SetProperty(t => t.FechaVencimiento, termino.AddHours(Math.Max(O.HorasDeVigencia, 1)))
                    .SetProperty(t => t.BloqueadoHasta, (DateTimeOffset?)null)
                    .SetProperty(t => t.NombreArchivo, nombre)
                    .SetProperty(t => t.TamanoBytes, archivo.Length)
                    .SetProperty(t => t.Filas, documento.TotalDeFilas)
                    .SetProperty(t => t.Error, (string?)null), CancellationToken.None);
            if (cerrado == 0)
            {
                // Otro proceso lo cerro mientras tanto (por ejemplo, lo dio por abandonado). Si quedo «listo», el archivo es el de ese
                // proceso (mismo nombre) y no se toca; solo sobra si el trabajo quedo en otro estado.
                var actual = await db.TrabajosExportacion.AsNoTracking().Where(t => t.IdTrabajo == trabajo.IdTrabajo).Select(t => t.Estado).FirstOrDefaultAsync(CancellationToken.None);
                if (actual != EstadosExportacion.Listo)
                    almacen.Borrar(trabajo.IdTrabajo, trabajo.Formato);
                return;
            }
            await AuditarAsync(auditoria, trabajo, "listo", documento.TotalDeFilas, motivo: null);
            logger.LogInformation("Exportacion {Id} ({Reporte}, {Formato}) lista: {Filas} fila(s), {Bytes} bytes.", trabajo.IdTrabajo, trabajo.Reporte, trabajo.Formato, documento.TotalDeFilas, archivo.Length);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // El servidor se esta cerrando: el trabajo queda «procesando» y vuelve a «pendiente» al arrancar.
        }
        catch (Exception error) when (error is OperationCanceledException or TimeoutException)
        {
            await FallarAsync(db, auditoria, trabajo, MensajeLento);
        }
        catch (ReporteDemasiadoGrandeException error)
        {
            await FallarAsync(db, auditoria, trabajo, error.Message);
        }
        catch (Exception error)
        {
            // Solo el tipo: el mensaje de una excepcion puede traer rutas o datos.
            logger.LogError("Exportacion {Id} ({Reporte}, {Formato}) fallo: [{TipoError}]", trabajo.IdTrabajo, trabajo.Reporte, trabajo.Formato, error.GetType().Name);
            await FallarAsync(db, auditoria, trabajo, MensajeFallo);
        }
    }

    private async Task FallarAsync(LetrerosHerreradbContext db, IAuditoria auditoria, TrabajoExportacion trabajo, string motivo)
    {
        var ahora = ServicioExportaciones.EnSegundos(reloj.GetUtcNow());
        almacen.Borrar(trabajo.IdTrabajo, trabajo.Formato);
        var cerrado = await db.TrabajosExportacion
            .Where(t => t.IdTrabajo == trabajo.IdTrabajo && t.Estado == EstadosExportacion.Procesando && t.Intentos == trabajo.Intentos)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Estado, EstadosExportacion.Fallido)
                .SetProperty(t => t.FechaTermino, ahora)
                .SetProperty(t => t.BloqueadoHasta, (DateTimeOffset?)null)
                .SetProperty(t => t.Error, motivo), CancellationToken.None);
        if (cerrado == 1)
            await AuditarAsync(auditoria, trabajo, "fallido", filas: null, motivo);
    }

    // Sin el contenido del archivo ni datos del cliente. Si la bitacora no se puede escribir, el trabajo no se repite: se registra el error.
    private async Task AuditarAsync(IAuditoria auditoria, TrabajoExportacion trabajo, string estado, int? filas, string? motivo)
    {
        try
        {
            var detalle = new Dictionary<string, object?> { ["reporte"] = trabajo.Reporte, ["formato"] = trabajo.Formato, ["estado"] = estado };
            if (filas is int n)
                detalle["filas"] = n;
            if (motivo is not null)
                detalle["motivo"] = motivo;
            // Quien lo pidio, con el rol con el que lo pidio: el proceso en segundo plano no tiene sesion.
            await auditoria.RegistrarAsync("reporte", trabajo.IdTrabajo.ToString("N"), AccionesAuditoria.ExportarReporte, DetalleAuditoria.Libre(detalle),
                CancellationToken.None, new ActorAuditoria(trabajo.IdEmpleado, null, trabajo.RolSolicitante, null));
        }
        catch (Exception error)
        {
            logger.LogError("No se pudo registrar en la bitacora la exportacion {Id} ({Estado}): [{TipoError}]", trabajo.IdTrabajo, estado, error.GetType().Name);
        }
    }

    // ── Vencimiento ──

    /// <summary>Borra los archivos vencidos (el registro del trabajo queda) y los que quedaron sin registro. Devuelve cuantos borro.</summary>
    public async Task<int> PurgarAsync(CancellationToken ct)
    {
        await using var ambito = ambitos.CreateAsyncScope();
        var db = ambito.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
        var ahora = ServicioExportaciones.EnSegundos(reloj.GetUtcNow());
        var vencidos = await db.TrabajosExportacion.AsNoTracking()
            .Where(t => t.Estado == EstadosExportacion.Listo && t.ArchivoBorradoEn == null && t.FechaVencimiento < ahora)
            .Select(t => new { t.IdTrabajo, t.Formato }).Take(500).ToListAsync(ct);
        foreach (var vencido in vencidos)
        {
            almacen.Borrar(vencido.IdTrabajo, vencido.Formato);
            await db.TrabajosExportacion.Where(t => t.IdTrabajo == vencido.IdTrabajo && t.ArchivoBorradoEn == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ArchivoBorradoEn, ahora), ct);
        }
        // Lo que ya tendria que haber vencido y no tiene registro (el proceso se cayo entre escribirlo y anotarlo).
        var huerfanos = almacen.PurgarHuerfanos(reloj.GetUtcNow().UtcDateTime.AddHours(-(Math.Max(O.HorasDeVigencia, 1) + 1)));
        return vencidos.Count + huerfanos;
    }

    // El reloj de «hoy» de un trabajo: la fecha en que se pidio.
    private sealed class FechaFija(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}

// Proceso en segundo plano: despierta cuando se pide una exportacion o cada Reportes:IntervaloSegundos, recupera lo que quedo a medias
// al arrancar, procesa un lote y, cada hora, purga los archivos vencidos. Un error (por ejemplo, la base no disponible) se registra y se
// reintenta con espera creciente: nunca detiene la API.
public sealed class ProcesadorExportaciones(
    TrabajadorExportaciones trabajador, SenalExportaciones senal, IOptions<OpcionesReportes> opciones, IHostEnvironment entorno, TimeProvider reloj,
    ILogger<ProcesadorExportaciones> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var o = opciones.Value;
        if (!o.ColaActiva)
        {
            // Las pruebas la apagan a proposito; fuera de desarrollo, las exportaciones se acumularian sin salir.
            logger.Log(entorno.IsDevelopment() ? LogLevel.Information : LogLevel.Warning,
                "Las exportaciones de reportes no se procesan en este proceso (Reportes:ColaActiva = false): los pedidos quedan guardados sin generarse.");
            return;
        }

        var intervalo = TimeSpan.FromSeconds(Math.Max(1, o.IntervaloSegundos));
        var esperaTrasError = intervalo;
        var proximaPurga = reloj.GetUtcNow();
        var recuperado = false;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!recuperado)
                {
                    await trabajador.RecuperarInterrumpidosAsync(alIniciar: true, ct);
                    recuperado = true;
                }
                else
                    await trabajador.RecuperarInterrumpidosAsync(alIniciar: false, ct);

                var procesados = await trabajador.ProcesarLoteAsync(ct);
                if (reloj.GetUtcNow() >= proximaPurga)
                {
                    await trabajador.PurgarAsync(ct);
                    proximaPurga = reloj.GetUtcNow().AddHours(1);
                }
                esperaTrasError = intervalo;

                // Lote lleno: puede haber mas, se sigue sin esperar.
                if (procesados >= trabajador.Cupos)
                    continue;
                await senal.EsperarAsync(intervalo, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                // Solo el tipo: el mensaje de una excepcion puede traer rutas o textos de la base.
                logger.LogError("Las exportaciones de reportes fallaron [{TipoError}]; se vuelve a intentar en {Segundos} s.", error.GetType().Name, esperaTrasError.TotalSeconds);
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
}
