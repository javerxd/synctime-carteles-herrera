using LetrerosHerreraSYNCTIMEapi.Datos;
using LetrerosHerreraSYNCTIMEapi.Datos.Correo;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Cotizaciones;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Fase 8: cotizaciones (RFS05, RFU08, RFU12, E06, E07, S06) ──

public abstract record ResultadoCotizacion
{
    public sealed record Correcta(FichaCotizacion Ficha) : ResultadoCotizacion;
    public sealed record Invalida(IReadOnlyDictionary<string, string[]> Errores) : ResultadoCotizacion;
    public sealed record NoEncontrada : ResultadoCotizacion;
    // DesdeLaBase: la regla la hizo cumplir la base (nivel «base_de_datos» en la respuesta).
    public sealed record Conflicto(string Codigo, string Mensaje, bool DesdeLaBase = false) : ResultadoCotizacion;
    public sealed record SinVersion : ResultadoCotizacion;
    public sealed record VersionObsoleta : ResultadoCotizacion;
    public sealed record Eliminada : ResultadoCotizacion;
}

// Codigos estables de los 409 de la fase 8 (el frontend decide con ellos que mostrar).
public static class CodigosConflicto
{
    public const string TransicionInvalida = "transicion_invalida";
    public const string CotizacionPendiente = "cotizacion_pendiente";
    public const string CotizacionConPedido = "cotizacion_con_pedido";
    public const string PedidoAnulado = "pedido_anulado";
    public const string AbonoExcedeSaldo = "abono_excede_saldo";
    public const string MontoBajoAbonado = "monto_bajo_abonado";
    public const string AbonoAnulado = "abono_anulado";
    public const string TieneAbonos = "tiene_abonos";
    public const string FacturaDistinta = "factura_distinta";
}

public sealed record FiltroCotizaciones(string? Estado, DateOnly? Desde, DateOnly? Hasta, int? IdCliente, string? Orden);

// Quien responde una cotizacion: el cliente desde la web, o el personal cuando el cliente confirmo por
// telefono, WhatsApp o en el local (queda quien la marco y por que via).
public sealed record QuienResponde(int? IdCliente, int? IdEmpleado, string? Via)
{
    public static QuienResponde Cliente(int idCliente) => new(idCliente, null, ViasRespuesta.ClienteWeb);
    public static QuienResponde Personal(int idEmpleado, string? via) => new(null, idEmpleado, via);
}

public sealed class ServicioCotizaciones(
    LetrerosHerreradbContext db,
    ColaEnTransaccion cola,
    AvisosAlCliente avisosAlCliente,
    IOptions<OpcionesCorreo> opcionesCorreo,
    IConfiguration configuracion,
    TimeProvider reloj)
{
    public const string MensajeVersionObsoleta = "Otra persona modificó esta cotización mientras la veías. Vuelve a cargarla para ver los cambios.";
    public const string MensajeCotizacionPendiente = "Esta solicitud ya tiene una cotización pendiente. Edítala o espera la respuesta del cliente.";

    // Etiqueta de la consulta que busca una pendiente: las pruebas la usan para provocar el choque con el
    // indice unico justo despues de esa consulta.
    public const string EtiquetaPendiente = "verificar_cotizacion_pendiente";
    private const string IndiceUnaPendiente = "uq_cotizaciones_una_pendiente";

    // ── Emitir ──

    // POST /solicitudes/{id}/cotizaciones. En una transaccion: la cotizacion y sus lineas, el paso de la
    // solicitud a «cotizado» con su historial y, si el cliente tiene el correo confirmado, el aviso «Tu
    // cotizacion esta lista» en la cola. La fila de la solicitud se bloquea primero: dos personas que cotizan
    // la misma solicitud a la vez quedan en fila, y la segunda ve la pendiente de la primera.
    public async Task<ResultadoCotizacion> EmitirAsync(int idSolicitud, int idEmpleado, EntradaCotizacion entrada, CancellationToken ct)
    {
        var errores = ValidacionCotizacion.Validar(entrada);
        await ConsultasDeCotizacion.ValidarMaterialesAsync(db, entrada.Materiales, errores, ct);
        if (errores.Count > 0)
            return new ResultadoCotizacion.Invalida(errores);

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var solicitud = (await db.Solicitudes
            .FromSql($"SELECT * FROM dbo.solicitudes WITH (UPDLOCK, ROWLOCK) WHERE id_solicitud = {idSolicitud}")
            .ToListAsync(ct)).SingleOrDefault();
        if (solicitud is null || solicitud.EliminadoLogico)
            return new ResultadoCotizacion.NoEncontrada();

        // La pendiente se revisa antes que el estado: es el caso comun (la solicitud ya esta «cotizado») y
        // su mensaje dice que hacer.
        if (await db.Cotizaciones.TagWith(EtiquetaPendiente)
                .AnyAsync(c => c.IdSolicitud == idSolicitud && c.Estado == EstadosCotizacion.Pendiente && !c.EliminadoLogico, ct))
            return new ResultadoCotizacion.Conflicto(CodigosConflicto.CotizacionPendiente, MensajeCotizacionPendiente);
        if (solicitud.Estado != EstadosSolicitud.Solicitado)
            return new ResultadoCotizacion.Conflicto(CodigosConflicto.TransicionInvalida,
                $"Solo se cotiza una solicitud «solicitado». Esta está «{solicitud.Estado}».");

        var ahora = reloj.GetUtcNow();
        var cotizacion = ConsultasDeCotizacion.Nueva(idSolicitud, idEmpleado, entrada, ahora);
        db.Cotizaciones.Add(cotizacion);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (ErroresSql.EsDuplicado(e, IndiceUnaPendiente))
        {
            // La base lo garantiza aunque otra cotizacion se haya colado despues de la revision.
            return new ResultadoCotizacion.Conflicto(CodigosConflicto.CotizacionPendiente, MensajeCotizacionPendiente, DesdeLaBase: true);
        }

        // Cotizar lo hace el sistema (TransicionesSolicitud): el historial no lleva empleado ni cliente;
        // quien la elaboro queda en la cotizacion.
        db.SolicitudesHistorial.Add(new SolicitudHistorial
        {
            IdSolicitud = idSolicitud,
            EstadoAnterior = solicitud.Estado,
            EstadoNuevo = EstadosSolicitud.Cotizado,
            Motivo = $"Se emitió la cotización {ValidacionCotizacion.Numero(cotizacion.IdCotizacion)}.",
            Fecha = ahora
        });
        solicitud.Estado = EstadosSolicitud.Cotizado;
        await db.SaveChangesAsync(ct);

        var ficha = (await FichaAsync(cotizacion.IdCotizacion, idClienteDueno: null, ct))!;
        // Outbox: el correo se escribe en la cola con el mismo COMMIT que la cotizacion. Solo a un correo
        // confirmado: el de un cliente presencial lo pudo escribir mal el personal. Fase 11: es un aviso de estado
        // (opcional), con el enlace de baja, y no sale si el cliente apago esa preferencia o dio de baja la cuenta.
        var destino = await avisosAlCliente.DestinoAsync(solicitud.IdCliente, ct);
        var avisa = destino is not null;
        if (destino is not null)
        {
            await cola.EncolarAsync(CotizacionLista.Componer(ficha, destino.Correo, opcionesCorreo.Value.ContactoTaller,
                configuracion["Sitio:UrlPublica"], destino.TokenBaja), ct);
            await db.SaveChangesAsync(ct);
        }
        // Sin el token de la peticion: si ya se llego aqui, la cotizacion y su aviso quedan guardados juntos.
        await transaccion.CommitAsync(CancellationToken.None);
        if (avisa)
            cola.AvisarAlTrabajador();
        return new ResultadoCotizacion.Correcta(ficha);
    }

    // ── Editar ──

    // Solo una «pendiente», con la version vista. Reemplaza las lineas. Primero se guarda la cabecera con la
    // version (si otra persona la cambio, no se pisa) y despues las lineas: el trigger de las lineas
    // recalcula el total y cambia la version, asi que no pueden ir en el mismo UPDATE que la comprueba.
    public async Task<ResultadoCotizacion> EditarAsync(int id, EntradaCotizacion entrada, byte[]? version, CancellationToken ct)
    {
        if (version is null or { Length: 0 })
            return new ResultadoCotizacion.SinVersion();
        var errores = ValidacionCotizacion.Validar(entrada);
        await ConsultasDeCotizacion.ValidarMaterialesAsync(db, entrada.Materiales, errores, ct);
        if (errores.Count > 0)
            return new ResultadoCotizacion.Invalida(errores);

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var cotizacion = await db.Cotizaciones.Include(c => c.Materiales)
            .FirstOrDefaultAsync(c => c.IdCotizacion == id && !c.EliminadoLogico && !c.Solicitud!.EliminadoLogico, ct);
        if (cotizacion is null)
            return new ResultadoCotizacion.NoEncontrada();
        if (!version.AsSpan().SequenceEqual(cotizacion.RowVersion))
            return new ResultadoCotizacion.VersionObsoleta();
        if (cotizacion.Estado != EstadosCotizacion.Pendiente)
            return new ResultadoCotizacion.Conflicto(CodigosConflicto.TransicionInvalida,
                $"Solo se edita una cotización «pendiente». Esta ya está «{cotizacion.Estado}» y queda como historial.");

        cotizacion.CostoManoObra = entrada.CostoManoObra!.Value;
        cotizacion.OtrosCostos = entrada.OtrosCostos!.Value;
        cotizacion.Observaciones = ValidacionCotizacion.Limpiar(entrada.Observaciones);
        // Toda edicion cambia la version, aunque solo cambien las lineas.
        db.Entry(cotizacion).Property(c => c.Observaciones).IsModified = true;
        db.Entry(cotizacion).Property(c => c.RowVersion).OriginalValue = version;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoCotizacion.VersionObsoleta();
        }

        db.CotizacionMateriales.RemoveRange(cotizacion.Materiales);
        foreach (var linea in ConsultasDeCotizacion.Lineas(entrada.Materiales))
            cotizacion.Materiales.Add(linea);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(CancellationToken.None);
        // Ya guardada: la respuesta se arma aunque se cancele la peticion.
        return new ResultadoCotizacion.Correcta((await FichaAsync(id, idClienteDueno: null, CancellationToken.None))!);
    }

    // ── Responder ──

    // Aprobar: la solicitud pasa a «aceptado» (el sistema) y se abre el paso a pedido. Rechazar: vuelve a
    // «solicitado» (el cliente o el personal) con la nota «Cotizacion #N rechazada: motivo», y se puede
    // cotizar de nuevo. Solo sobre una «pendiente» y con la version vista. El cliente solo responde las suyas.
    public async Task<ResultadoCotizacion> ResponderAsync(int id, QuienResponde quien, string? decision, string? motivo, byte[]? version,
        CancellationToken ct)
    {
        var errores = ValidacionCotizacion.ValidarRespuesta(decision, quien.Via, motivo, delPersonal: quien.IdEmpleado is not null);
        if (errores.Count > 0)
            return new ResultadoCotizacion.Invalida(errores);
        if (version is null or { Length: 0 })
            return new ResultadoCotizacion.SinVersion();

        var cotizacion = await db.Cotizaciones.Include(c => c.Solicitud)
            .FirstOrDefaultAsync(c => c.IdCotizacion == id && !c.EliminadoLogico && !c.Solicitud!.EliminadoLogico
                && (quien.IdCliente == null || c.Solicitud.IdCliente == quien.IdCliente), ct);
        if (cotizacion is null)
            return new ResultadoCotizacion.NoEncontrada();
        if (!version.AsSpan().SequenceEqual(cotizacion.RowVersion))
            return new ResultadoCotizacion.VersionObsoleta();
        if (cotizacion.Estado != EstadosCotizacion.Pendiente)
            return new ResultadoCotizacion.Conflicto(CodigosConflicto.TransicionInvalida,
                $"Esta cotización ya está «{cotizacion.Estado}»: solo se responde una cotización «pendiente».");

        var solicitud = cotizacion.Solicitud!;
        var aprueba = decision == ValidacionCotizacion.Aprobar;
        var nuevoEstado = aprueba ? EstadosSolicitud.Aceptado : EstadosSolicitud.Solicitado;
        var actor = aprueba ? ActorEstado.Sistema : quien.IdCliente is not null ? ActorEstado.Cliente : ActorEstado.Personal;
        if (TransicionesSolicitud.Explicar(solicitud.Estado, nuevoEstado, actor) is { } explicacion)
            return new ResultadoCotizacion.Conflicto(CodigosConflicto.TransicionInvalida, explicacion);

        var ahora = reloj.GetUtcNow();
        var numero = ValidacionCotizacion.Numero(cotizacion.IdCotizacion);
        var motivoLimpio = aprueba ? null : ValidacionCotizacion.Limpiar(motivo);
        cotizacion.Estado = decision!;
        cotizacion.FechaRespuesta = ahora;
        cotizacion.ViaRespuesta = quien.Via;
        cotizacion.IdEmpleadoRespuesta = quien.IdEmpleado;
        cotizacion.MotivoRechazo = motivoLimpio;

        db.SolicitudesHistorial.Add(new SolicitudHistorial
        {
            IdSolicitud = solicitud.IdSolicitud,
            EstadoAnterior = solicitud.Estado,
            EstadoNuevo = nuevoEstado,
            Motivo = aprueba ? $"Cotización {numero} aprobada."
                : Recortar(motivoLimpio is null ? $"Cotización {numero} rechazada." : $"Cotización {numero} rechazada: {motivoLimpio}", ValidacionSolicitud.MaxMotivo),
            // Aprobar lo hace el sistema; rechazar, quien respondio.
            IdEmpleado = aprueba ? null : quien.IdEmpleado,
            IdCliente = aprueba ? null : quien.IdCliente,
            Fecha = ahora
        });
        solicitud.Estado = nuevoEstado;
        // Fase 11 (S09): al aprobarse, el cliente recibe el aviso de su solicitud aceptada, en el mismo guardado.
        var avisa = aprueba && await avisosAlCliente.EstadoSolicitudAsync(cola, solicitud.IdSolicitud, solicitud.IdCliente, EstadosSolicitud.Aceptado, null, ct);

        // La base compara con la version que vio quien responde (de la cotizacion y de la solicitud): si
        // otra persona guardo entre la lectura y este UPDATE, no se pisa su trabajo. Un solo SaveChanges va
        // en una transaccion.
        db.Entry(cotizacion).Property(c => c.RowVersion).OriginalValue = version;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoCotizacion.VersionObsoleta();
        }
        if (avisa)
            cola.AvisarAlTrabajador();
        return new ResultadoCotizacion.Correcta((await FichaAsync(id, idClienteDueno: null, CancellationToken.None))!);
    }

    // ── Eliminar (gestion) ──

    // Borrado logico, solo de una «pendiente» o una «rechazada» sin pedido. Si estaba pendiente, la
    // solicitud vuelve a «solicitado», con historial.
    public async Task<ResultadoCotizacion> EliminarAsync(int id, int idEmpleado, byte[]? version, CancellationToken ct)
    {
        if (version is null or { Length: 0 })
            return new ResultadoCotizacion.SinVersion();

        var cotizacion = await db.Cotizaciones.Include(c => c.Solicitud)
            .FirstOrDefaultAsync(c => c.IdCotizacion == id && !c.EliminadoLogico, ct);
        if (cotizacion is null)
            return new ResultadoCotizacion.NoEncontrada();
        if (!version.AsSpan().SequenceEqual(cotizacion.RowVersion))
            return new ResultadoCotizacion.VersionObsoleta();
        if (!EstadosCotizacion.PuedeEliminarse(cotizacion.Estado))
            return new ResultadoCotizacion.Conflicto(CodigosConflicto.TransicionInvalida,
                "Solo se elimina una cotización «pendiente» o «rechazada»: una aprobada queda como historial.");
        if (await db.Pedidos.AnyAsync(p => p.IdCotizacion == id, ct))
            return new ResultadoCotizacion.Conflicto(CodigosConflicto.CotizacionConPedido, "La cotización ya tiene un pedido y no se puede eliminar.");

        var ahora = reloj.GetUtcNow();
        cotizacion.EliminadoLogico = true;
        cotizacion.FechaEliminacionLogica = ahora;
        var solicitud = cotizacion.Solicitud!;
        if (cotizacion.Estado == EstadosCotizacion.Pendiente && solicitud.Estado == EstadosSolicitud.Cotizado)
        {
            db.SolicitudesHistorial.Add(new SolicitudHistorial
            {
                IdSolicitud = solicitud.IdSolicitud,
                EstadoAnterior = solicitud.Estado,
                EstadoNuevo = EstadosSolicitud.Solicitado,
                Motivo = $"Se eliminó la cotización {ValidacionCotizacion.Numero(id)}.",
                IdEmpleado = idEmpleado,
                Fecha = ahora
            });
            solicitud.Estado = EstadosSolicitud.Solicitado;
        }

        db.Entry(cotizacion).Property(c => c.RowVersion).OriginalValue = version;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoCotizacion.VersionObsoleta();
        }
        return new ResultadoCotizacion.Eliminada();
    }

    // ── Leer ──

    // La cotizacion (S06). Para el cliente (idClienteDueno), solo si es de una solicitud suya; lo ajeno o
    // eliminado responde null (404).
    public async Task<FichaCotizacion?> FichaAsync(int id, int? idClienteDueno, CancellationToken ct)
    {
        var fila = await db.Cotizaciones.AsNoTracking()
            .Where(c => c.IdCotizacion == id && !c.EliminadoLogico && !c.Solicitud!.EliminadoLogico
                && (idClienteDueno == null || c.Solicitud.IdCliente == idClienteDueno))
            .Select(c => new
            {
                c.IdCotizacion,
                c.IdSolicitud,
                c.FechaCotizacion,
                c.Estado,
                c.Observaciones,
                c.CostoMaterialesTotal,
                c.CostoManoObra,
                c.OtrosCostos,
                c.TotalCotizacion,
                c.RowVersion,
                c.FechaRespuesta,
                c.ViaRespuesta,
                c.MotivoRechazo,
                RegistradaPor = c.EmpleadoRespuesta == null ? null : c.EmpleadoRespuesta.Nombre + " " + c.EmpleadoRespuesta.Apellido,
                c.IdEmpleado,
                ElaboradaPor = c.Empleado!.Nombre + " " + c.Empleado.Apellido,
                Cliente = new DatosCliente(c.Solicitud!.IdCliente, c.Solicitud.Cliente!.Nombre, c.Solicitud.Cliente.Apellido,
                    c.Solicitud.Cliente.Telefono, c.Solicitud.Cliente.Email),
                Items = c.Solicitud.Detalles.OrderBy(d => d.IdSolicitudDetalle)
                    .Select(d => new { Producto = d.Producto!.Nombre, d.Descripcion, d.AnchoMm, d.AltoMm, d.Cantidad })
                    .ToList(),
                Materiales = c.Materiales.OrderBy(m => m.IdCotizacionMaterial)
                    .Select(m => new { m.IdMaterial, Material = m.Material!.Nombre, Unidad = m.Material.UnidadMedida, m.MedidaAplicada, m.Cantidad, m.CostoUnitario, m.Subtotal })
                    .ToList(),
                // Una cotizacion da a lo mas un pedido, aunque despues se elimine: se informa igual, marcado.
                Pedido = db.Pedidos.Where(p => p.IdCotizacion == c.IdCotizacion).Select(p => new { p.IdPedido, p.EliminadoLogico }).FirstOrDefault()
            })
            .AsSplitQuery()
            .SingleOrDefaultAsync(ct);
        if (fila is null)
            return null;

        var items = fila.Items.Select(d => new ItemCotizacion(d.Producto, string.IsNullOrWhiteSpace(d.Descripcion) ? null : d.Descripcion,
            d.AnchoMm, d.AltoMm, d.Cantidad, MedidasSolicitud.AreaM2(d.AnchoMm, d.AltoMm, d.Cantidad))).ToList();
        var materiales = fila.Materiales.Select(m => new MaterialCotizacion(m.IdMaterial, m.Material, m.Unidad, m.MedidaAplicada, m.Cantidad,
            (int)m.CostoUnitario, m.Subtotal)).ToList();
        var respuesta = fila.FechaRespuesta is { } fecha
            ? new RespuestaCotizacion(HoraChile.Convertir(fecha), fila.ViaRespuesta!, fila.MotivoRechazo,
                fila.RegistradaPor is null ? null : new PersonaDelTaller(null, fila.RegistradaPor))
            : null;

        return new FichaCotizacion(fila.IdCotizacion, ValidacionCotizacion.Numero(fila.IdCotizacion), fila.IdSolicitud,
            MedidasSolicitud.Numero(fila.IdSolicitud), fila.FechaCotizacion, fila.Estado, fila.Cliente, items, materiales,
            fila.CostoMaterialesTotal, (int)fila.CostoManoObra, (int)fila.OtrosCostos, fila.TotalCotizacion,
            ValidacionCotizacion.Redondear(fila.TotalCotizacion), fila.Observaciones, new PersonaDelTaller(fila.IdEmpleado, fila.ElaboradaPor),
            respuesta, fila.Pedido?.IdPedido, fila.Pedido?.EliminadoLogico == true, fila.RowVersion);
    }

    // Listado del personal: filtros por estado, fecha (de Chile, incluidas) y cliente; busqueda por numero
    // de cotizacion o de solicitud, nombre, telefono o RUT; orden y paginacion. Sin las eliminadas.
    public Task<ResultadoPaginado<FilaCotizacion>> ListarAsync(FiltroCotizaciones filtro, ParametrosListado parametros, CancellationToken ct)
    {
        var consulta = db.Cotizaciones.AsNoTracking().Where(c => !c.EliminadoLogico && !c.Solicitud!.EliminadoLogico);

        if (EstadosCotizacion.Existe(filtro.Estado))
            consulta = consulta.Where(c => c.Estado == filtro.Estado);
        if (filtro.Desde is DateOnly desde)
        {
            var inicio = desde.ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(c => c.FechaCotizacion >= inicio);
        }
        if (filtro.Hasta is DateOnly hasta && hasta < DateOnly.MaxValue)
        {
            var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(c => c.FechaCotizacion < fin);
        }
        if (filtro.IdCliente is int idCliente)
            consulta = consulta.Where(c => c.Solicitud!.IdCliente == idCliente);

        if (parametros.Buscar is { } buscar)
        {
            var numero = int.TryParse(buscar.TrimStart('#').Trim(), out var n) ? n : -1;
            var digitos = new string(buscar.Where(char.IsDigit).ToArray());
            var rut = buscar.Replace(".", "").Trim().ToUpperInvariant();
            consulta = consulta.Where(c => c.IdCotizacion == numero || c.IdSolicitud == numero
                || (c.Solicitud!.Cliente!.Nombre + " " + c.Solicitud.Cliente.Apellido).Contains(buscar)
                || (digitos.Length >= 3 && c.Solicitud.Cliente.Telefono.Contains(digitos))
                || (c.Solicitud.Cliente.Rut != null && c.Solicitud.Cliente.Rut.Contains(rut)));
        }

        consulta = filtro.Orden switch
        {
            "fecha_asc" => consulta.OrderBy(c => c.FechaCotizacion).ThenBy(c => c.IdCotizacion),
            "total_desc" => consulta.OrderByDescending(c => c.TotalCotizacion).ThenByDescending(c => c.IdCotizacion),
            _ => consulta.OrderByDescending(c => c.FechaCotizacion).ThenByDescending(c => c.IdCotizacion)
        };

        return consulta
            .Select(c => new FilaCotizacion(
                c.IdCotizacion,
                "#" + c.IdCotizacion.ToString(),
                c.IdSolicitud,
                c.FechaCotizacion,
                new ClienteFila(c.Solicitud!.IdCliente, c.Solicitud.Cliente!.Nombre, c.Solicitud.Cliente.Apellido, c.Solicitud.Cliente.Telefono),
                c.TotalCotizacion,
                c.Estado,
                new PersonaDelTaller(c.IdEmpleado, c.Empleado!.Nombre + " " + c.Empleado.Apellido)))
            .PaginarAsync(parametros, ct);
    }

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..(largo - 1)] + "…";
}

// Lo que comparten la emision de una cotizacion y el pedido directo (que crea su cotizacion aprobada).
internal static class ConsultasDeCotizacion
{
    // Cada linea debe usar un material activo del catalogo.
    public static async Task ValidarMaterialesAsync(LetrerosHerreradbContext db, IReadOnlyList<EntradaMaterial>? lineas,
        Dictionary<string, string[]> errores, CancellationToken ct)
    {
        if (lineas is not { Count: > 0 and <= ValidacionCotizacion.MaxLineas })
            return;
        var pedidos = lineas.Select(l => l.IdMaterial).Distinct().ToList();
        var activos = await db.Materiales.AsNoTracking()
            .Where(m => pedidos.Contains(m.IdMaterial) && m.Activo)
            .Select(m => m.IdMaterial)
            .ToListAsync(ct);
        for (var i = 0; i < lineas.Count; i++)
        {
            var campo = $"materiales[{i}].idMaterial";
            if (!errores.ContainsKey(campo) && !activos.Contains(lineas[i].IdMaterial))
                errores[campo] = [ValidacionCotizacion.MensajeMaterial];
        }
    }

    // Solo con una entrada ya validada: los montos no son nulos.
    public static Cotizacion Nueva(int idSolicitud, int idEmpleado, EntradaCotizacion entrada, DateTimeOffset ahora) => new()
    {
        IdSolicitud = idSolicitud,
        IdEmpleado = idEmpleado,
        FechaCotizacion = TiempoChile.Fecha(ahora),
        Observaciones = ValidacionCotizacion.Limpiar(entrada.Observaciones),
        CostoManoObra = entrada.CostoManoObra!.Value,
        OtrosCostos = entrada.OtrosCostos!.Value,
        Estado = EstadosCotizacion.Pendiente,
        Materiales = Lineas(entrada.Materiales)
    };

    // Solo con una entrada ya validada: cantidad y costo no son nulos.
    public static List<CotizacionMaterial> Lineas(IReadOnlyList<EntradaMaterial>? lineas) =>
        (lineas ?? []).Select(l => new CotizacionMaterial
        {
            IdMaterial = l.IdMaterial,
            MedidaAplicada = ValidacionCotizacion.Limpiar(l.MedidaAplicada),
            Cantidad = l.Cantidad!.Value,
            CostoUnitario = l.CostoUnitario!.Value
        }).ToList();
}

// La hora de Chile del servidor (HoraChile), para lo que se guarda o se decide con ella.
internal static class TiempoChile
{
    // Las fechas datetime2(0) en hora de Chile: se truncan a segundos para que lo guardado y lo devuelto coincidan.
    public static DateTime Fecha(DateTimeOffset instante)
    {
        var chile = HoraChile.Convertir(instante);
        return new DateTime(chile.Ticks - chile.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Unspecified);
    }

    // «Hoy» es la fecha de Chile, no la del servidor.
    public static DateOnly Hoy(TimeProvider reloj) => DateOnly.FromDateTime(HoraChile.Convertir(reloj.GetUtcNow()));
}

// Reconoce los rechazos de la base que la fase 8 traduce a un 409 propio. Los nombres de los indices y las
// reglas sirven aunque SQL Server responda en otro idioma.
internal static class ErroresSql
{
    public static bool EsDuplicado(DbUpdateException e, params string[] indices) =>
        e.InnerException is SqlException { Number: 2601 or 2627 } sql && indices.Any(i => sql.Message.Contains(i, StringComparison.Ordinal));

    public static bool EsRegla(DbUpdateException e, string regla) =>
        e.InnerException is SqlException { Number: 547 } sql && sql.Message.Contains($"\"{regla}\"", StringComparison.Ordinal);
}
