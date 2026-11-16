using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Datos;
using LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Fase 11: visor de la bitacora y de los consentimientos (RFU28) ──

public sealed record FiltroAuditoria(DateOnly? Desde, DateOnly? Hasta, string? Entidad, string? Accion, int? IdEmpleado, string? IdEntidad);

public sealed record UsuarioAuditoria(string Tipo, int? Id, string? Nombre);

public sealed record FilaAuditoria(long IdAuditoria, DateTimeOffset Fecha, UsuarioAuditoria Usuario, string? Rol, string Entidad, string EntidadTexto,
    string? IdEntidad, string Accion, string AccionTexto, JsonElement? Detalle, string? Ip);

public sealed record FiltroConsentimientos(string? Tipo, DateOnly? Desde, DateOnly? Hasta);

public sealed record FilaConsentimiento(UsuarioAuditoria Titular, string Tipo, string Version, bool Acepta, DateTimeOffset Fecha);

public sealed class ServicioVisorAuditoria(LetrerosHerreradbContext db)
{
    public async Task<ResultadoPaginado<FilaAuditoria>> ListarAsync(FiltroAuditoria filtro, ParametrosListado parametros, CancellationToken ct)
    {
        var consulta = db.Auditoria.AsNoTracking();
        if (filtro.Desde is DateOnly desde)
        {
            var inicio = FechasChile.Inicio(desde);
            consulta = consulta.Where(a => a.Fecha >= inicio);
        }
        if (filtro.Hasta is DateOnly hasta && hasta < DateOnly.MaxValue)
        {
            var fin = FechasChile.Inicio(hasta.AddDays(1));
            consulta = consulta.Where(a => a.Fecha < fin);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Entidad))
            consulta = consulta.Where(a => a.Entidad == filtro.Entidad);
        if (!string.IsNullOrWhiteSpace(filtro.Accion))
            consulta = consulta.Where(a => a.Accion == filtro.Accion);
        if (filtro.IdEmpleado is int idEmpleado)
            consulta = consulta.Where(a => a.IdEmpleado == idEmpleado);
        if (!string.IsNullOrWhiteSpace(filtro.IdEntidad))
            consulta = consulta.Where(a => a.IdEntidad == filtro.IdEntidad.Trim());

        var pagina = await consulta
            .OrderByDescending(a => a.Fecha).ThenByDescending(a => a.IdAuditoria)
            .Select(a => new
            {
                a.IdAuditoria, a.Fecha, a.IdEmpleado, a.IdCliente, a.Rol, a.Entidad, a.IdEntidad, a.Accion, a.Detalle, a.Ip,
                Empleado = db.Empleados.Where(e => e.IdEmpleado == a.IdEmpleado).Select(e => e.Nombre + " " + e.Apellido).FirstOrDefault(),
                Cliente = db.Clientes.Where(c => c.IdCliente == a.IdCliente).Select(c => c.Nombre + " " + c.Apellido).FirstOrDefault()
            })
            .PaginarAsync(parametros, ct);

        var filas = pagina.Elementos.Select(a => new FilaAuditoria(a.IdAuditoria, a.Fecha,
            a.IdEmpleado is int e ? new UsuarioAuditoria("empleado", e, a.Empleado)
            : a.IdCliente is int c ? new UsuarioAuditoria("cliente", c, a.Cliente)
            : new UsuarioAuditoria(AccionesAuditoria.RolSistema, null, null),
            a.Rol, a.Entidad, ReglasAuditoria.TextosEntidad.GetValueOrDefault(a.Entidad, a.Entidad), a.IdEntidad, a.Accion,
            ReglasAuditoria.TextosAccion.GetValueOrDefault(a.Accion, a.Accion), Leer(a.Detalle), a.Ip)).ToList();
        return new ResultadoPaginado<FilaAuditoria>(filas, pagina.Total, pagina.Pagina, pagina.Tamano);
    }

    // Cada aceptacion y cada revocacion es un evento, del mas reciente al mas antiguo.
    public async Task<ResultadoPaginado<FilaConsentimiento>> ConsentimientosAsync(FiltroConsentimientos filtro, ParametrosListado parametros, CancellationToken ct)
    {
        var base_ = db.Consentimientos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
            base_ = base_.Where(c => c.Tipo == filtro.Tipo);
        var aceptaciones = base_.Select(c => new EventoConsentimiento
        {
            IdCliente = c.IdCliente, IdEmpleado = c.IdEmpleado, Referencia = c.ReferenciaEnvio, Tipo = c.Tipo, Version = c.VersionDocumento, Acepta = true,
            Fecha = c.FechaAceptacion, Id = c.IdConsentimiento
        });
        var revocaciones = base_.Where(c => c.FechaRevocacion != null).Select(c => new EventoConsentimiento
        {
            IdCliente = c.IdCliente, IdEmpleado = c.IdEmpleado, Referencia = c.ReferenciaEnvio, Tipo = c.Tipo, Version = c.VersionDocumento, Acepta = false,
            Fecha = c.FechaRevocacion!.Value, Id = c.IdConsentimiento
        });
        var eventos = aceptaciones.Concat(revocaciones);
        if (filtro.Desde is DateOnly desde)
        {
            var inicio = FechasChile.Inicio(desde);
            eventos = eventos.Where(e => e.Fecha >= inicio);
        }
        if (filtro.Hasta is DateOnly hasta && hasta < DateOnly.MaxValue)
        {
            var fin = FechasChile.Inicio(hasta.AddDays(1));
            eventos = eventos.Where(e => e.Fecha < fin);
        }

        var pagina = await eventos.OrderByDescending(e => e.Fecha).ThenByDescending(e => e.Id).ThenBy(e => e.Acepta).PaginarAsync(parametros, ct);
        var idsClientes = pagina.Elementos.Where(e => e.IdCliente != null).Select(e => e.IdCliente!.Value).Distinct().ToList();
        var idsEmpleados = pagina.Elementos.Where(e => e.IdEmpleado != null).Select(e => e.IdEmpleado!.Value).Distinct().ToList();
        var clientes = await db.Clientes.AsNoTracking().Where(c => idsClientes.Contains(c.IdCliente))
            .ToDictionaryAsync(c => c.IdCliente, c => c.Nombre + " " + c.Apellido, ct);
        var empleados = await db.Empleados.AsNoTracking().Where(e => idsEmpleados.Contains(e.IdEmpleado))
            .ToDictionaryAsync(e => e.IdEmpleado, e => e.Nombre + " " + e.Apellido, ct);

        var filas = pagina.Elementos.Select(e => new FilaConsentimiento(
            e.IdCliente is int c ? new UsuarioAuditoria("cliente", c, clientes.GetValueOrDefault(c))
            : e.IdEmpleado is int em ? new UsuarioAuditoria("empleado", em, empleados.GetValueOrDefault(em))
            : new UsuarioAuditoria("visitante", null, e.Referencia),
            e.Tipo, e.Version, e.Acepta, e.Fecha)).ToList();
        return new ResultadoPaginado<FilaConsentimiento>(filas, pagina.Total, pagina.Pagina, pagina.Tamano);
    }

    private static JsonElement? Leer(string? detalle)
    {
        if (string.IsNullOrWhiteSpace(detalle))
            return null;
        try
        {
            using var documento = JsonDocument.Parse(detalle);
            return documento.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class EventoConsentimiento
    {
        public int? IdCliente { get; init; }
        public int? IdEmpleado { get; init; }
        public string? Referencia { get; init; }
        public string Tipo { get; init; } = "";
        public string Version { get; init; } = "";
        public bool Acepta { get; init; }
        public DateTimeOffset Fecha { get; init; }
        public int Id { get; init; }
    }
}
