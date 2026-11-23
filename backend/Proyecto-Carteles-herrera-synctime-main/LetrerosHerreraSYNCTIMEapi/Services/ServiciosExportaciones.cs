using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// Fase 13A (RNFP18, RFS10, RFS12, RFU28): pedir, consultar y descargar una exportacion. Pedirla solo deja un trabajo en la tabla
// (responde 202 sin esperar el archivo) y lo anota en la bitacora; lo genera el proceso en segundo plano (TrabajadorExportaciones).

public sealed record PersonalQueExporta(int IdEmpleado, string Rol);

public sealed record VistaTrabajo(
    Guid Id, string Reporte, string Formato, string Estado, DateTimeOffset FechaPedido, DateTimeOffset? FechaTermino, DateTimeOffset? FechaVencimiento,
    int? Filas, int? TamanoBytes, string? NombreArchivo, string? Error, bool Vencido);

public abstract record ResultadoExportacion
{
    public sealed record Aceptado(VistaTrabajo Trabajo) : ResultadoExportacion;
    public sealed record Invalido(IReadOnlyDictionary<string, string[]> Errores) : ResultadoExportacion;
    public sealed record DemasiadoGrande(string Mensaje) : ResultadoExportacion;
    public sealed record Ocupado(string Mensaje) : ResultadoExportacion;
    public sealed record SinPermiso : ResultadoExportacion;
}

public abstract record ResultadoDescarga
{
    public sealed record NoEncontrado : ResultadoDescarga;
    public sealed record NoListo(string Estado, string? Error) : ResultadoDescarga;
    public sealed record Vencido : ResultadoDescarga;
    public sealed record Archivo(LetrerosHerreraSYNCTIMEapi.Infraestructura.Almacen.ArchivoAbierto Contenido, string TipoMime, string Nombre) : ResultadoDescarga;
}

public sealed class ServicioExportaciones(
    LetrerosHerreradbContext db, IAuditoria auditoria, ServicioReportes reportes, AlmacenExportaciones almacen, SenalExportaciones senal,
    TimeProvider reloj, IOptions<OpcionesReportes> opciones)
{
    public const string MensajeOcupado = "Ya tienes un archivo en preparación. Espera a que termine para pedir otro.";

    public const string MensajeDemasiadosArchivos = "Ya tienes varios archivos listos. Descárgalos y espera a que venzan (24 horas) antes de pedir más.";

    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // Las columnas son datetimeoffset(0): se guardan en segundos para que lo guardado y lo devuelto coincidan.
    internal static DateTimeOffset EnSegundos(DateTimeOffset instante) =>
        new(instante.Ticks - instante.Ticks % TimeSpan.TicksPerSecond, instante.Offset);

    public async Task<ResultadoExportacion> SolicitarAsync(
        PersonalQueExporta quien, string reporte, string? formato, IReadOnlyDictionary<string, string?> consulta, CancellationToken ct)
    {
        // El permiso por reporte se valida aqui, en el servidor, ademas de en la ruta.
        if (!TiposReporte.PuedeVer(reporte, quien.Rol))
            return new ResultadoExportacion.SinPermiso();

        var (filtro, errores) = ReglasReportes.Validar(reporte, consulta);
        if (!FormatosReporte.Existe(formato))
            errores["formato"] = [$"Formato inválido. Valores: {string.Join(", ", FormatosReporte.Todos)}."];
        if (filtro is null || errores.Count > 0)
            return new ResultadoExportacion.Invalido(errores);

        var ahora = EnSegundos(reloj.GetUtcNow());
        var filas = await reportes.ContarAsync(filtro, TiempoChile.Hoy(reloj), ct);
        if (ReglasReportes.ExcedeElTope(filas, opciones.Value.MaximoDeFilas))
            return new ResultadoExportacion.DemasiadoGrande(ReglasReportes.MensajeDelTope(opciones.Value.MaximoDeFilas));

        if (formato == FormatosReporte.Pdf && ReglasReportes.ExcedeElTope(filas, opciones.Value.MaximoDeFilasPdf))
            return new ResultadoExportacion.DemasiadoGrande(ReglasReportes.MensajeDelTopePdf(opciones.Value.MaximoDeFilasPdf));

        // Un trabajo pendiente o en proceso por persona a la vez: primero la pregunta amable, y la base lo impone si dos llegan juntas.
        if (await db.TrabajosExportacion.AnyAsync(t => t.IdEmpleado == quien.IdEmpleado
            && (t.Estado == EstadosExportacion.Pendiente || t.Estado == EstadosExportacion.Procesando), ct))
            return new ResultadoExportacion.Ocupado(MensajeOcupado);

        // Y un tope de archivos vivos por persona (cada uno ocupa disco hasta que vence).
        if (await db.TrabajosExportacion.CountAsync(t => t.IdEmpleado == quien.IdEmpleado && t.Estado == EstadosExportacion.Listo
            && t.ArchivoBorradoEn == null && t.FechaVencimiento > ahora, ct) >= Math.Max(opciones.Value.MaximoArchivosVivosPorPersona, 1))
            return new ResultadoExportacion.Ocupado(MensajeDemasiadosArchivos);

        var trabajo = new TrabajoExportacion
        {
            IdTrabajo = Guid.NewGuid(),
            Reporte = reporte,
            Formato = formato!,
            Filtros = JsonSerializer.Serialize(filtro.Canonico, Json),
            Estado = EstadosExportacion.Pendiente,
            IdEmpleado = quien.IdEmpleado,
            RolSolicitante = quien.Rol,
            FechaPedido = ahora
        };
        db.TrabajosExportacion.Add(trabajo);
        // El pedido queda en la bitacora en el mismo guardado: quien, que reporte, que formato y con que filtros.
        auditoria.Anotar("reporte", trabajo.IdTrabajo.ToString("N"), AccionesAuditoria.ExportarReporte, DetalleAuditoria.Libre(new Dictionary<string, object?>
        {
            ["reporte"] = reporte,
            ["formato"] = formato,
            ["estado"] = "pedido",
            ["filtros"] = filtro.Canonico
        }));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (ErroresSql.EsDuplicado(e, "ux_trabajos_exportacion_activo"))
        {
            return new ResultadoExportacion.Ocupado(MensajeOcupado);
        }
        senal.Avisar();
        return new ResultadoExportacion.Aceptado(Vista(trabajo, ahora));
    }

    /// <summary>El estado de un trabajo, solo para quien lo pidio. De un tercero (o de un rol que ya no puede ver el reporte) es null: 404, no 403.</summary>
    public async Task<VistaTrabajo?> ConsultarAsync(PersonalQueExporta quien, Guid id, CancellationToken ct)
    {
        var trabajo = await db.TrabajosExportacion.AsNoTracking().FirstOrDefaultAsync(t => t.IdTrabajo == id && t.IdEmpleado == quien.IdEmpleado, ct);
        return trabajo is null || !TiposReporte.PuedeVer(trabajo.Reporte, quien.Rol) ? null : Vista(trabajo, reloj.GetUtcNow());
    }

    public async Task<ResultadoDescarga> DescargarAsync(PersonalQueExporta quien, Guid id, CancellationToken ct)
    {
        var trabajo = await db.TrabajosExportacion.AsNoTracking().FirstOrDefaultAsync(t => t.IdTrabajo == id && t.IdEmpleado == quien.IdEmpleado, ct);
        if (trabajo is null || !TiposReporte.PuedeVer(trabajo.Reporte, quien.Rol))
            return new ResultadoDescarga.NoEncontrado();
        if (trabajo.Estado != EstadosExportacion.Listo)
            return new ResultadoDescarga.NoListo(trabajo.Estado, trabajo.Error);
        if (EstaVencido(trabajo, reloj.GetUtcNow()))
            return new ResultadoDescarga.Vencido();
        // Quien recibe el Archivo cierra el flujo (el endpoint lo entrega a la respuesta, que lo cierra al terminar).
        if (await almacen.AbrirAsync(trabajo.IdTrabajo, trabajo.Formato, ct) is not { } contenido)
            return new ResultadoDescarga.Vencido();
        return new ResultadoDescarga.Archivo(contenido, FormatosReporte.TipoMime(trabajo.Formato), trabajo.NombreArchivo!);
    }

    private static bool EstaVencido(TrabajoExportacion t, DateTimeOffset ahora) =>
        t.Estado == EstadosExportacion.Listo && (t.ArchivoBorradoEn is not null || t.FechaVencimiento is { } vence && ahora >= vence);

    private static VistaTrabajo Vista(TrabajoExportacion t, DateTimeOffset ahora) =>
        new(t.IdTrabajo, t.Reporte, t.Formato, t.Estado, t.FechaPedido, t.FechaTermino, t.FechaVencimiento, t.Filas, t.TamanoBytes, t.NombreArchivo, t.Error, EstaVencido(t, ahora));
}
