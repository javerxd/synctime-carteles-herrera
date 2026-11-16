using System.Globalization;
using LetrerosHerreraSYNCTIMEapi.Datos;
using LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;
using LetrerosHerreraSYNCTIMEapi.Datos.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Fase 11: «Correos no enviados» (decisiones 2 y 3) ──

public sealed record FiltroNoEnviados(string? Tipo, DateOnly? Desde, DateOnly? Hasta, bool? Revisado);

public sealed record CorreoNoEnviado(long IdCorreo, string Tipo, string TipoTexto, string? Referencia, string? Enlace, string DestinoEnmascarado,
    int Intentos, string? Motivo, DateTimeOffset FechaCreacion, DateTimeOffset FechaFallo, bool Revisado, bool Reenviable, long? ReenviadoComo);

public sealed record ResumenNoEnviados(int SinRevisar);

public sealed record CorreoReenviado(long IdCorreoNuevo);

public abstract record ResultadoReenvio
{
    public sealed record Encolado(long IdCorreoNuevo) : ResultadoReenvio;
    public sealed record YaReenviado(long IdCorreoNuevo) : ResultadoReenvio;
    public sealed record SinContenido : ResultadoReenvio;
    public sealed record SinDestinos : ResultadoReenvio;
    public sealed record ClienteNoRecibe : ResultadoReenvio;
    public sealed record NoEncontrado : ResultadoReenvio;
}

// Lo que quedo «fallido» tras todos los reintentos, de cualquier tipo, con el destino enmascarado y el motivo en
// español. Reenviar crea una fila nueva en la cola con el mismo contenido y marca la original en una sola sentencia
// condicionada: dos clics a la vez no crean dos correos. Un aviso del taller va a los destinos activos de hoy,
// segun su preferencia y el origen de la solicitud; los demas, a su misma direccion.
public sealed class ServicioCorreosNoEnviados(
    LetrerosHerreradbContext db,
    IDestinatariosAvisos destinatarios,
    AvisosAlCliente avisosAlCliente,
    IConfiguration configuracion,
    IAuditoria auditoria,
    SenalColaCorreo senal,
    TimeProvider reloj)
{
    public async Task<ResultadoPaginado<CorreoNoEnviado>> ListarAsync(FiltroNoEnviados filtro, ParametrosListado parametros, CancellationToken ct)
    {
        var consulta = db.CorreosSalientes.AsNoTracking().Where(c => c.Estado == EstadosCorreo.Fallido);
        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
            consulta = consulta.Where(c => c.Tipo == filtro.Tipo);
        if (filtro.Revisado is bool revisado)
            consulta = consulta.Where(c => c.Revisado == revisado);
        if (filtro.Desde is DateOnly desde)
        {
            var inicio = FechasChile.Inicio(desde);
            consulta = consulta.Where(c => c.ProximoIntento >= inicio);
        }
        if (filtro.Hasta is DateOnly hasta && hasta < DateOnly.MaxValue)
        {
            var fin = FechasChile.Inicio(hasta.AddDays(1));
            consulta = consulta.Where(c => c.ProximoIntento < fin);
        }

        var pagina = await consulta
            .OrderByDescending(c => c.ProximoIntento).ThenByDescending(c => c.IdCorreo)
            .Select(c => new
            {
                c.IdCorreo, c.Tipo, c.Referencia, c.Destinatarios, c.Intentos, c.UltimoError, c.FechaCreacion, c.ProximoIntento, c.Revisado,
                c.ReenviadoComo, ConCuerpo = c.CuerpoHtml != ""
            })
            .PaginarAsync(parametros, ct);
        var filas = pagina.Elementos.Select(c => new CorreoNoEnviado(c.IdCorreo, c.Tipo, CategoriasCorreo.Texto(c.Tipo), c.Referencia, Enlace(c.Referencia),
            DestinoEnmascarado(c.Destinatarios), c.Intentos, Motivo(c.UltimoError), c.FechaCreacion, c.ProximoIntento, c.Revisado,
            Reenviable: c.ConCuerpo && c.ReenviadoComo is null, c.ReenviadoComo)).ToList();
        return new ResultadoPaginado<CorreoNoEnviado>(filas, pagina.Total, pagina.Pagina, pagina.Tamano);
    }

    public async Task<ResumenNoEnviados> ResumenAsync(CancellationToken ct) =>
        new(await db.CorreosSalientes.CountAsync(c => c.Estado == EstadosCorreo.Fallido && !c.Revisado, ct));

    public async Task<ResultadoReenvio> ReenviarAsync(long id, int idEmpleado, CancellationToken ct)
    {
        var original = await db.CorreosSalientes.AsNoTracking().FirstOrDefaultAsync(c => c.IdCorreo == id && c.Estado == EstadosCorreo.Fallido, ct);
        if (original is null)
            return new ResultadoReenvio.NoEncontrado();
        if (original.ReenviadoComo is long ya)
            return new ResultadoReenvio.YaReenviado(ya);
        if (original.CuerpoHtml.Length == 0)
            return new ResultadoReenvio.SinContenido();

        IReadOnlyList<string> para = CategoriasCorreo.EsAvisoDelTaller(original.Tipo)
            ? (await destinatarios.LeerAsync(await OrigenAsync(original, ct), ct)).Correos
            : original.Destinatarios.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // Revision de la fase 11: un aviso de estado (opcional) solo se reenvia si el cliente aun lo recibe, en la misma
        // direccion y con el mismo enlace de baja; si se dio de baja o cambio de correo, no.
        if (CategoriasCorreo.EsAvisoDeEstado(original.Tipo) && !await ClienteAunRecibeAsync(original, ct))
            return new ResultadoReenvio.ClienteNoRecibe();
        if (para.Count == 0)
            return new ResultadoReenvio.SinDestinos();

        var ahora = reloj.GetUtcNow();
        var nueva = FilaDeCola.Crear(new MensajeCorreo(original.Tipo, para, original.Asunto, original.CuerpoHtml, original.CuerpoTexto,
            original.ResponderA, original.Referencia, original.EnlaceBaja), ahora);
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        db.CorreosSalientes.Add(nueva);
        await db.SaveChangesAsync(ct);
        // Una sola sentencia condicionada: si otra peticion lo reenvio primero, esta no marca nada y se deshace su copia.
        var marcados = await db.CorreosSalientes
            .Where(c => c.IdCorreo == id && c.Estado == EstadosCorreo.Fallido && c.ReenviadoComo == null && c.CuerpoHtml != "")
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.ReenviadoComo, nueva.IdCorreo)
                .SetProperty(c => c.IdEmpleadoReenvia, idEmpleado)
                .SetProperty(c => c.FechaReenvio, ahora)
                .SetProperty(c => c.Revisado, true), ct);
        if (marcados != 1)
        {
            await transaccion.RollbackAsync(CancellationToken.None);
            var actual = await db.CorreosSalientes.AsNoTracking().Where(c => c.IdCorreo == id)
                .Select(c => new { c.ReenviadoComo, ConCuerpo = c.CuerpoHtml != "" }).FirstOrDefaultAsync(CancellationToken.None);
            return actual?.ReenviadoComo is long otro ? new ResultadoReenvio.YaReenviado(otro)
                : actual is { ConCuerpo: false } ? new ResultadoReenvio.SinContenido()
                : new ResultadoReenvio.NoEncontrado();
        }
        await auditoria.RegistrarAsync("correo", id.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.CorreoReenviado,
            DetalleAuditoria.Libre(new Dictionary<string, object?>
            {
                ["tipo"] = original.Tipo,
                ["referencia"] = original.Referencia,
                ["idCorreoNuevo"] = nueva.IdCorreo,
                ["destino"] = CategoriasCorreo.Enmascarar(nueva.Destinatarios),
                ["aDestinosDeHoy"] = CategoriasCorreo.EsAvisoDelTaller(original.Tipo)
            }), ct);
        await transaccion.CommitAsync(CancellationToken.None);
        senal.Avisar();
        return new ResultadoReenvio.Encolado(nueva.IdCorreo);
    }

    // false si no existe o no es un fallido. La marca y su registro van en una transaccion.
    public async Task<bool> MarcarRevisadoAsync(long id, CancellationToken ct)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var marcados = await db.CorreosSalientes.Where(c => c.IdCorreo == id && c.Estado == EstadosCorreo.Fallido)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Revisado, true), ct);
        if (marcados == 0)
            return false;
        await auditoria.RegistrarAsync("correo", id.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.CorreoRevisado, null, ct);
        await transaccion.CommitAsync(CancellationToken.None);
        return true;
    }

    private async Task<bool> ClienteAunRecibeAsync(CorreoSaliente correo, CancellationToken ct)
    {
        int? idCliente = Id(correo.Referencia, "estado:solicitud:") is int s
                ? await db.Solicitudes.AsNoTracking().Where(x => x.IdSolicitud == s).Select(x => (int?)x.IdCliente).FirstOrDefaultAsync(ct)
            : Id(correo.Referencia, "estado:pedido:") is int p
                ? await db.Pedidos.AsNoTracking().Where(x => x.IdPedido == p).Select(x => (int?)x.Solicitud!.IdCliente).FirstOrDefaultAsync(ct)
            : Id(correo.Referencia, "cotizacion:") is int c
                ? await db.Cotizaciones.AsNoTracking().Where(x => x.IdCotizacion == c).Select(x => (int?)x.Solicitud!.IdCliente).FirstOrDefaultAsync(ct)
            : null;
        if (idCliente is not int id || await avisosAlCliente.DestinoAsync(id, ct) is not { } destino)
            return false;
        return string.Equals(destino.Correo, correo.Destinatarios, StringComparison.OrdinalIgnoreCase)
            && EnlaceBaja.UrlUnClic(configuracion["Sitio:UrlPublica"], destino.TokenBaja) == correo.EnlaceBaja;
    }

    // El aviso de una solicitud va a los destinos de su origen; una consulta (o el aviso del tope) viene de la pagina.
    private async Task<OrigenAviso> OrigenAsync(CorreoSaliente correo, CancellationToken ct)
    {
        if (correo.Tipo == TiposCorreo.AvisoSolicitudNueva && Id(correo.Referencia, "solicitud:") is int idSolicitud)
        {
            var presencial = await db.Solicitudes.AsNoTracking().Where(s => s.IdSolicitud == idSolicitud)
                .Select(s => (bool?)(s.IdEmpleadoRegistra != null)).FirstOrDefaultAsync(ct);
            return presencial == true ? OrigenAviso.Presencial : OrigenAviso.EnLinea;
        }
        return OrigenAviso.EnLinea;
    }

    // La ruta del panel a la que se refiere el correo, si la tiene.
    public static string? Enlace(string? referencia) =>
        Id(referencia, "solicitud:") is int s ? $"/panel/solicitudes/{s}"
        : Id(referencia, "confirmacion:") is int c ? $"/panel/solicitudes/{c}"
        : Id(referencia, "estado:solicitud:") is int es ? $"/panel/solicitudes/{es}"
        : Id(referencia, "cotizacion:") is int co ? $"/panel/cotizaciones/{co}"
        : Id(referencia, "estado:pedido:") is int p ? $"/panel/pedidos/{p}"
        : null;

    private static int? Id(string? referencia, string prefijo) =>
        referencia is not null && referencia.StartsWith(prefijo, StringComparison.Ordinal)
        && int.TryParse(referencia[prefijo.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    // Ya enmascarado si la purga lo vacio.
    private static string DestinoEnmascarado(string destinatarios) =>
        destinatarios.Contains('•') ? destinatarios : CategoriasCorreo.Enmascarar(destinatarios);

    // «categoria: Mensaje en español» (ClasificadorFallos): solo el mensaje.
    private static string? Motivo(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return null;
        var separador = error.IndexOf(": ", StringComparison.Ordinal);
        return separador > 0 ? error[(separador + 2)..] : error;
    }
}

// Inicio de un dia de Chile como instante, para filtrar columnas datetimeoffset por fechas de Chile.
internal static class FechasChile
{
    public static DateTimeOffset Inicio(DateOnly dia)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, HoraChile.ZonaHoraria.GetUtcOffset(local));
    }
}
