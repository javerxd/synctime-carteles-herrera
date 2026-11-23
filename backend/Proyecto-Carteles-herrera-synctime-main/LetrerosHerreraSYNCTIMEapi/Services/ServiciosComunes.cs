using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Imagenes;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Consentimientos;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Avisos al taller ──

public interface IAvisosDelTaller
{
    // Fase 7 (outbox): deja el aviso de una solicitud nueva en la cola de la transaccion que la guarda.
    // A diferencia de los demas avisos, si algo falla lanza la excepcion: quien llama revierte la
    // transaccion completa, asi nunca queda una solicitud guardada cuyo aviso se perdio.
    // Fase 11: tambien las presenciales, a los destinos que las quieren. Es un correo operativo: ninguna baja ni
    // preferencia del cliente lo afecta.
    Task SolicitudNuevaAsync(IServicioCorreo colaDeLaTransaccion, ComprobanteSolicitud solicitud, string? registradaPor, CancellationToken ct);

    // Una consulta del formulario de contacto, ya guardada. Nunca lanza: si algo falla, lo registra.
    Task ConsultaContactoAsync(ConsultaParaAviso consulta);
}

// Guarda cuando fallo por ultima vez dejar un aviso en la cola. Lo consulta /salud/correo: un aviso que
// ni siquiera llego a la cola no aparece en correos_salientes, y sin esto nadie se enteraria.
public sealed class RegistroFallosAvisos(TimeProvider reloj)
{
    private long ultimoFallo = DateTimeOffset.MinValue.UtcTicks;

    public void Anotar() => Interlocked.Exchange(ref ultimoFallo, reloj.GetUtcNow().UtcTicks);

    public bool HuboFalloDesde(DateTimeOffset desde) => Interlocked.Read(ref ultimoFallo) >= desde.UtcTicks;
}

// Objetivo principal del sistema (13-11-2026): que el dueño reciba un aviso por cada solicitud nueva.
// Encolar es solo insertar una fila, asi que la peticion no espera al servidor de correo.
// Tope por hora (Correo:TopeAvisosPorHora): ante un envio masivo de solicitudes falsas, en vez de un
// correo por cada una se envia un solo aviso de "muchas solicitudes". Asi no se agota el cupo diario de
// Gmail (y la cuenta no queda bloqueada) justo cuando llegan las solicitudes reales.
public sealed class AvisosDelTaller(
    IDestinatariosAvisos destinatarios,
    IServicioCorreo correo,
    IConfiguration configuracion,
    IOptions<OpcionesCorreo> opciones,
    RegistroFallosAvisos fallos,
    TimeProvider reloj,
    ILogger<AvisosDelTaller> logger) : IAvisosDelTaller
{
    public Task SolicitudNuevaAsync(IServicioCorreo colaDeLaTransaccion, ComprobanteSolicitud solicitud, string? registradaPor, CancellationToken ct) =>
        AvisarAsync(colaDeLaTransaccion, $"solicitud {solicitud.IdSolicitud}", TiposCorreo.AvisoSolicitudNueva, sublimite: 0,
            solicitud.Origen == "presencial" ? OrigenAviso.Presencial : OrigenAviso.EnLinea,
            (para, url, _) => Task.FromResult<MensajeCorreo?>(AvisoSolicitudNueva.Componer(solicitud, registradaPor, para, url)), ct);

    public async Task ConsultaContactoAsync(ConsultaParaAviso consulta)
    {
        // Sin el token de la peticion a proposito: si quien escribio cierra la pagina justo despues, el
        // aviso igual debe quedar en la cola.
        try
        {
            // Una consulta llega desde la pagina: la reciben los destinos de las solicitudes en linea.
            await AvisarAsync(correo, $"consulta {consulta.IdConsulta}", TiposCorreo.ConsultaContacto, opciones.Value.TopeConsultasPorHora, OrigenAviso.EnLinea,
                (para, url, _) => Task.FromResult<MensajeCorreo?>(AvisoConsultaContacto.Componer(consulta, para, url)), CancellationToken.None);
        }
        catch (Exception error)
        {
            fallos.Anotar();
            logger.LogError(error, "No se pudo dejar en la cola el aviso de la consulta {IdConsulta}: el taller no recibira ese aviso. Lo recibido quedo guardado.", consulta.IdConsulta);
        }
    }

    // Solicitudes y consultas comparten el tope por hora: las dos salen de la misma cuenta de Gmail. Las
    // consultas, ademas, tienen un sublimite propio para no dejar sin cupo a las solicitudes.
    private async Task AvisarAsync(IServicioCorreo cola, string que, string tipo, int sublimite, OrigenAviso origen,
        Func<IReadOnlyList<string>, string?, CancellationToken, Task<MensajeCorreo?>> componer, CancellationToken ct)
    {
        // Un aviso que nadie quiere recibir no se encola.
        var para = (await destinatarios.LeerAsync(origen, ct)).Correos;
        if (para.Count == 0)
            return;
        var urlPublica = configuracion["Sitio:UrlPublica"];

        var haceUnaHora = reloj.GetUtcNow().AddHours(-1);
        if (sublimite > 0 && await cola.ContarAsync(tipo, haceUnaHora, ct) >= sublimite)
        {
            logger.LogWarning("Se alcanzo el tope de {Tope} avisos por hora de este tipo: la {Que} quedo guardada sin aviso propio.", sublimite, que);
            return;
        }

        var tope = opciones.Value.TopeAvisosPorHora;
        if (tope > 0)
        {
            var enviados = await cola.ContarAsync(TiposCorreo.AvisoSolicitudNueva, haceUnaHora, ct)
                         + await cola.ContarAsync(TiposCorreo.ConsultaContacto, haceUnaHora, ct);
            if (enviados >= tope)
            {
                if (await cola.ContarAsync(TiposCorreo.AvisoTopeSolicitudes, haceUnaHora, ct) == 0)
                    await cola.EncolarAsync(AvisoTopeSolicitudes.Componer(para, tope, urlPublica), ct);
                logger.LogWarning("Se alcanzo el tope de {Tope} avisos por hora: la {Que} quedo guardada sin aviso propio.", tope, que);
                return;
            }
        }

        if (await componer(para, urlPublica, ct) is { } mensaje)
            await cola.EncolarAsync(mensaje, ct);
    }
}

// ── Consentimientos (RFS12) ──

public sealed record ResultadoConsentimiento(string? Error, int? IdConsentimiento);
public sealed record ConsentimientoVigente(int IdConsentimiento, string Version, DateTimeOffset FechaAceptacion);

public interface IServicioConsentimientos
{
    // Registra que el titular acepto la version vigente de un documento. Si la version que la persona
    // leyo ya no es la vigente, no registra nada: debe volver a leer el documento.
    Task<ResultadoConsentimiento> RegistrarAsync(TitularConsentimiento titular, string tipo, string versionAceptada, CancellationToken ct);

    // Marca como retirado lo que el titular tenga vigente de ese tipo. false si no habia nada vigente.
    Task<bool> RevocarAsync(TitularConsentimiento titular, string tipo, CancellationToken ct);

    Task<ConsentimientoVigente?> VigenteAsync(TitularConsentimiento titular, string tipo, CancellationToken ct);
}

public sealed class ServicioConsentimientos(LetrerosHerreradbContext db, TimeProvider reloj) : IServicioConsentimientos
{
    private IQueryable<Consentimiento> DelTitular(TitularConsentimiento titular, string tipo) =>
        db.Consentimientos.Where(c => c.Tipo == tipo
            && c.IdCliente == titular.IdCliente
            && c.IdEmpleado == titular.IdEmpleado
            && c.ReferenciaEnvio == titular.ReferenciaEnvio);

    public async Task<ResultadoConsentimiento> RegistrarAsync(TitularConsentimiento titular, string tipo, string versionAceptada, CancellationToken ct)
    {
        if (!DocumentosLegales.EsTipo(tipo))
            return new ResultadoConsentimiento("El tipo de documento no existe.", null);

        var vigente = DocumentosLegales.VersionVigente(tipo)!;
        if (versionAceptada != vigente)
            return new ResultadoConsentimiento("El documento cambió y la versión aceptada ya no es la vigente. Vuelva a leerlo.", null);

        // Aceptar dos veces lo mismo (por ejemplo, un doble clic) no crea un segundo registro.
        var existente = await DelTitular(titular, tipo)
            .Where(c => c.VersionDocumento == vigente && c.FechaRevocacion == null)
            .Select(c => (int?)c.IdConsentimiento)
            .FirstOrDefaultAsync(ct);
        if (existente is not null)
            return new ResultadoConsentimiento(null, existente);

        var registro = new Consentimiento
        {
            IdCliente = titular.IdCliente,
            IdEmpleado = titular.IdEmpleado,
            ReferenciaEnvio = titular.ReferenciaEnvio,
            Tipo = tipo,
            VersionDocumento = vigente,
            FechaAceptacion = reloj.GetUtcNow()
        };
        db.Consentimientos.Add(registro);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Otra peticion igual (un doble clic) lo registro un instante antes: los indices unicos lo impiden.
            db.Entry(registro).State = EntityState.Detached;
            var otro = await DelTitular(titular, tipo)
                .Where(c => c.VersionDocumento == vigente && c.FechaRevocacion == null)
                .Select(c => c.IdConsentimiento)
                .FirstAsync(ct);
            return new ResultadoConsentimiento(null, otro);
        }
        return new ResultadoConsentimiento(null, registro.IdConsentimiento);
    }

    public async Task<bool> RevocarAsync(TitularConsentimiento titular, string tipo, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var retirados = await DelTitular(titular, tipo)
            .Where(c => c.FechaRevocacion == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaRevocacion, ahora), ct);
        return retirados > 0;
    }

    public Task<ConsentimientoVigente?> VigenteAsync(TitularConsentimiento titular, string tipo, CancellationToken ct) =>
        DelTitular(titular, tipo)
            .Where(c => c.FechaRevocacion == null)
            .OrderByDescending(c => c.FechaAceptacion).ThenByDescending(c => c.IdConsentimiento)
            .Select(c => new ConsentimientoVigente(c.IdConsentimiento, c.VersionDocumento, c.FechaAceptacion))
            .FirstOrDefaultAsync(ct);
}

// ── Imagenes (RNFP22) ──

// Fase 9: con su id, para enlazarla a una publicacion de la vitrina (idImagen).
public sealed record ImagenSubida(int IdImagen, string Clave, int Ancho, int Alto, string UrlGrande, string UrlMiniatura);

public sealed class ServicioImagenes(LetrerosHerreradbContext db, AlmacenImagenes almacen, TimeProvider reloj)
{
    public const string RutaBase = "/api/v1/imagenes";

    // Procesar una foto grande ocupa memoria y procesador: como mucho dos a la vez.
    private static readonly SemaphoreSlim Procesando = new(2, 2);

    public async Task<ImagenSubida> SubirAsync(byte[] datos, int idEmpleado, CancellationToken ct)
    {
        ImagenProcesada imagen;
        await Procesando.WaitAsync(ct);
        try
        {
            imagen = await Task.Run(() => ProcesadorImagenes.Procesar(datos), ct);
        }
        finally
        {
            Procesando.Release();
        }

        var clave = AlmacenImagenes.NuevaClave();
        Imagen registro;
        try
        {
            await almacen.GuardarAsync(clave, imagen, ct);
            registro = new Imagen
            {
                Clave = clave,
                Ancho = imagen.Ancho,
                Alto = imagen.Alto,
                BytesGrande = imagen.Grande.Length,
                BytesMiniatura = imagen.Miniatura.Length,
                IdEmpleadoSube = idEmpleado,
                FechaSubida = reloj.GetUtcNow()
            };
            db.Imagenes.Add(registro);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Sin registro en la base (o si fallo la segunda escritura), los archivos quedarian huerfanos.
            await almacen.BorrarAsync(clave);
            throw;
        }

        return new ImagenSubida(registro.IdImagen, clave, imagen.Ancho, imagen.Alto,
            $"{RutaBase}/{AlmacenImagenes.NombreGrande(clave)}",
            $"{RutaBase}/{AlmacenImagenes.NombreMiniatura(clave)}");
    }
}
