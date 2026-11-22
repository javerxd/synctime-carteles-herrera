using System.Globalization;
using LetrerosHerreraSYNCTIMEapi.Datos;
using LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;
using LetrerosHerreraSYNCTIMEapi.Datos.Correo;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Adjuntos;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Imagenes;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Cotizaciones;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using LetrerosHerreraSYNCTIMEapi.Negocio.Pedidos;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Fase 7: solicitudes, adjuntos y clientes (RFS04, RFS09, RFU06, RFU10, RFU11, RFU19) ──

public sealed record EntradaSolicitud(string? DescripcionGeneral, IReadOnlyList<EntradaDetalle>? Detalles, IReadOnlyList<int>? Adjuntos);

public sealed record FiltroSolicitudes(string? Estado, string? Origen, DateOnly? Desde, DateOnly? Hasta, string? Orden);

public abstract record ResultadoSolicitud
{
    public sealed record Correcta(ComprobanteSolicitud Comprobante, FichaSolicitud Ficha) : ResultadoSolicitud;
    public sealed record Invalida(IReadOnlyDictionary<string, string[]> Errores) : ResultadoSolicitud;
    public sealed record CorreoSinConfirmar : ResultadoSolicitud;
    public sealed record ContactoDirecto : ResultadoSolicitud;
    public sealed record NoEncontrada : ResultadoSolicitud;
    public sealed record Conflicto(string Mensaje) : ResultadoSolicitud;
    public sealed record SinVersion : ResultadoSolicitud;
    public sealed record VersionObsoleta : ResultadoSolicitud;
    public sealed record Eliminada : ResultadoSolicitud;
}

public sealed class ServicioSolicitudes(
    LetrerosHerreradbContext db,
    ColaEnTransaccion cola,
    IAvisosDelTaller avisos,
    AvisosAlCliente avisosAlCliente,
    IAuditoria auditoria,
    IOptions<OpcionesCorreo> opcionesCorreo,
    IConfiguration configuracion,
    TimeProvider reloj,
    ILogger<ServicioSolicitudes> logger)
{
    public const string MensajeVersionObsoleta = "Otra persona modificó esta solicitud mientras la veías. Vuelve a cargarla para ver los cambios.";
    public const string MensajeCorreoSinConfirmar = "Confirma tu correo para enviar solicitudes en línea. Te enviamos un enlace; si no lo encuentras, pide uno nuevo.";
    private const string Pendiente = "pendiente";
    private const string Rechazada = "rechazada";

    // ── Crear ──

    // Solicitud en linea del cliente con sesion (RFS04). Todo en una transaccion: la solicitud, sus items,
    // su historial, el vinculo de los adjuntos y los dos correos (aviso al taller y confirmacion). Si la
    // regla contra solicitudes falsas la frena, no se escribe nada.
    public async Task<ResultadoSolicitud> CrearEnLineaAsync(int idCliente, EntradaSolicitud entrada, string? ip, CancellationToken ct)
    {
        var cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.IdCliente == idCliente && c.Activo && c.InhabilitadoEn == null, ct);
        if (cliente is null)
            return new ResultadoSolicitud.NoEncontrada();
        if (cliente.FechaVerificacionCorreo is null)
            return new ResultadoSolicitud.CorreoSinConfirmar();

        var errores = await ValidarAsync(entrada.DescripcionGeneral, entrada.Detalles, entrada.Adjuntos, idCliente, ct);
        if (errores.Count > 0)
            return new ResultadoSolicitud.Invalida(errores);

        // Sin IP (por ejemplo, detras de un proxy mal configurado) las reglas por IP no se pueden aplicar.
        if (ip is null)
            logger.LogWarning("Una solicitud en linea llego sin IP: las reglas por IP de la regla contra solicitudes falsas no se aplican a este envio.");
        var huella = HuellaIp.Calcular(ip, ClaveHuella());
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        // Dos envios simultaneos (doble clic, dos pestañas, un robot) no pueden pasar los dos la regla:
        // los envios en linea se revisan y guardan de a uno.
        await BloqueoDeAplicacion.TomarAsync(db, "synctime_solicitudes_en_linea", BloqueoDeAplicacion.Espera(configuracion), ct);

        var maxPorIp = configuracion.GetValue("Solicitudes:MaxEnLineaPorIp", ReglaContraSolicitudesFalsas.MaxEnLineaPorIpPredeterminado);
        if (ReglaContraSolicitudesFalsas.Frena(await HechosAsync(cliente, huella, ct), maxPorIp))
        {
            // Sin datos personales en el registro: solo el numero de cliente.
            logger.LogInformation("La regla contra solicitudes falsas freno un envio en linea del cliente {IdCliente}.", idCliente);
            return new ResultadoSolicitud.ContactoDirecto();
        }

        return await GuardarAsync(cliente, idEmpleado: null, entrada, huella, transaccion, ct);
    }

    // Registro presencial del personal: sin regla contra falsas y con confirmacion al cliente si tiene correo. Fase 11
    // (decision 1): avisa al taller solo a los destinos que eligieron recibir las presenciales.
    public async Task<ResultadoSolicitud> CrearPresencialAsync(int idEmpleado, int idCliente, EntradaSolicitud entrada, CancellationToken ct)
    {
        var errores = await ValidarAsync(entrada.DescripcionGeneral, entrada.Detalles, adjuntos: null, idCliente: null, ct);
        var cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.IdCliente == idCliente, ct);
        if (cliente is null)
            errores["idCliente"] = ["El cliente no existe."];
        else if (!cliente.Activo)
            errores["idCliente"] = ["El cliente está desactivado: actívalo antes de registrarle una solicitud."];
        if (errores.Count > 0)
            return new ResultadoSolicitud.Invalida(errores);

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        return await GuardarAsync(cliente!, idEmpleado, entrada with { Adjuntos = null }, huella: null, transaccion, ct);
    }

    private async Task<ResultadoSolicitud> GuardarAsync(Cliente cliente, int? idEmpleado, EntradaSolicitud entrada, string? huella,
        IDbContextTransaction transaccion, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var solicitud = new Solicitud
        {
            IdCliente = cliente.IdCliente,
            IdEmpleadoRegistra = idEmpleado,
            DescripcionGeneral = entrada.DescripcionGeneral!.Trim(),
            FechaSolicitud = FechaChile(ahora),
            Estado = EstadosSolicitud.Solicitado,
            HuellaIp = huella,
            Detalles = Detalles(entrada.Detalles!)
        };
        solicitud.Historial.Add(new SolicitudHistorial
        {
            EstadoNuevo = EstadosSolicitud.Solicitado,
            IdEmpleado = idEmpleado,
            IdCliente = idEmpleado is null ? cliente.IdCliente : null,
            Fecha = ahora
        });
        db.Solicitudes.Add(solicitud);
        await db.SaveChangesAsync(ct);

        var ids = entrada.Adjuntos?.Distinct().ToList() ?? [];
        if (ids.Count > 0)
        {
            // Solo borradores del mismo cliente: un id ajeno o ya usado no se puede vincular.
            var vinculados = await db.AdjuntosSolicitud
                .Where(a => ids.Contains(a.IdAdjunto) && a.IdCliente == cliente.IdCliente && a.IdSolicitud == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.IdSolicitud, (int?)solicitud.IdSolicitud)
                    .SetProperty(a => a.FechaVinculo, (DateTimeOffset?)ahora), ct);
            if (vinculados != ids.Count)
                return new ResultadoSolicitud.Invalida(ErrorAdjuntos());
        }

        var leida = (await LeerAsync(solicitud.IdSolicitud, idClienteDueno: null, ct))!;

        // Outbox: los correos se escriben en la cola con el mismo COMMIT que la solicitud. El aviso al taller es
        // operativo: sale aunque el cliente se haya dado de baja de los correos o de la cuenta.
        await avisos.SolicitudNuevaAsync(cola, leida.Comprobante, registradaPor: idEmpleado is null ? null : leida.RegistradaPor, ct);
        // Solo a un correo confirmado (el personal puede haber escrito mal el de un cliente presencial) y a una
        // cuenta que no se dio de baja.
        if (Reglas.EsCorreo(cliente.Email) && cliente.FechaVerificacionCorreo is not null && cliente.InhabilitadoEn is null)
            await cola.EncolarAsync(ConfirmacionSolicitud.Componer(leida.Comprobante, cliente.Email!, opcionesCorreo.Value.ContactoTaller,
                configuracion["Sitio:UrlPublica"]), ct);
        await db.SaveChangesAsync(ct);
        // Sin el token de la peticion: si ya se llego aqui, la solicitud y sus avisos quedan guardados juntos.
        await transaccion.CommitAsync(CancellationToken.None);
        cola.AvisarAlTrabajador();

        // Ya guardada: aunque quien la envio cierre la pagina, la respuesta se arma igual.
        return new ResultadoSolicitud.Correcta(leida.Comprobante, await FichaAsync(leida, CancellationToken.None));
    }

    private async Task<ReglaContraSolicitudesFalsas.Hechos> HechosAsync(Cliente cliente, string? huella, CancellationToken ct)
    {
        var pendientes = db.Solicitudes.AsNoTracking()
            .Where(s => !s.EliminadoLogico && (s.Estado == EstadosSolicitud.Solicitado || s.Estado == EstadosSolicitud.Cotizado));

        var cuenta = await pendientes.AnyAsync(s => s.IdCliente == cliente.IdCliente && s.IdEmpleadoRegistra == null, ct);
        var rut = cliente.Rut is { } r && await pendientes.AnyAsync(s => s.Cliente!.Rut == r, ct);
        var ip = huella is not null && await pendientes.AnyAsync(s => s.HuellaIp == huella && s.IdEmpleadoRegistra == null, ct);
        var ventana = TimeSpan.FromMinutes(Math.Max(1, configuracion.GetValue("Solicitudes:VentanaIpMinutos",
            ReglaContraSolicitudesFalsas.VentanaIpMinutosPredeterminada)));
        var desde = FechaChile(reloj.GetUtcNow() - ventana);
        var enviosIp = huella is null ? 0 : await db.Solicitudes.AsNoTracking()
            .CountAsync(s => s.HuellaIp == huella && s.IdEmpleadoRegistra == null && s.FechaSolicitud >= desde, ct);
        return new ReglaContraSolicitudesFalsas.Hechos(cuenta, rut, ip, enviosIp);
    }

    // ── Leer ──

    // El comprobante (para el cliente, solo si es suyo).
    public async Task<ComprobanteSolicitud?> ComprobanteAsync(int id, int? idClienteDueno, CancellationToken ct) =>
        (await LeerAsync(id, idClienteDueno, ct))?.Comprobante;

    public async Task<FichaSolicitud?> FichaAsync(int id, CancellationToken ct) =>
        await LeerAsync(id, idClienteDueno: null, ct) is { } leida ? await FichaAsync(leida, ct) : null;

    private sealed record Leida(ComprobanteSolicitud Comprobante, int IdCliente, string? RegistradaPor);

    private async Task<Leida?> LeerAsync(int id, int? idClienteDueno, CancellationToken ct)
    {
        var fila = await db.Solicitudes.AsNoTracking()
            .Where(s => s.IdSolicitud == id && !s.EliminadoLogico && (idClienteDueno == null || s.IdCliente == idClienteDueno))
            .Select(s => new
            {
                s.IdSolicitud,
                s.FechaSolicitud,
                s.Origen,
                s.Estado,
                s.MotivoRechazo,
                s.IdCliente,
                s.DescripcionGeneral,
                s.RowVersion,
                Cliente = new ClienteComprobante(s.Cliente!.Nombre, s.Cliente.Apellido, s.Cliente.Telefono, s.Cliente.Email),
                RegistradaPor = s.EmpleadoRegistra == null ? null : s.EmpleadoRegistra.Nombre + " " + s.EmpleadoRegistra.Apellido,
                Detalles = s.Detalles.OrderBy(d => d.IdSolicitudDetalle)
                    .Select(d => new { d.IdProducto, Producto = d.Producto!.Nombre, d.Descripcion, d.AnchoMm, d.AltoMm, d.Cantidad, d.Opciones })
                    .ToList(),
                Adjuntos = s.Adjuntos.OrderBy(a => a.IdAdjunto)
                    .Select(a => new { a.IdAdjunto, a.Tipo, a.NombreOriginal, a.Bytes, a.Ancho, a.Alto })
                    .ToList()
            })
            .AsSplitQuery()
            .SingleOrDefaultAsync(ct);
        if (fila is null)
            return null;

        var detalles = fila.Detalles.Select(d => new DetalleComprobante(d.IdProducto, d.Producto, string.IsNullOrWhiteSpace(d.Descripcion) ? null : d.Descripcion,
            d.AnchoMm, d.AltoMm, d.Cantidad, MedidasSolicitud.AreaM2(d.AnchoMm, d.AltoMm, d.Cantidad), ReglasCatalogo.Leer(d.Opciones))).ToList();
        var adjuntos = fila.Adjuntos.Select(a => new AdjuntoComprobante(a.IdAdjunto, a.Tipo, a.NombreOriginal, a.Bytes, a.Ancho, a.Alto,
            a.Tipo == AdjuntoSolicitud.TipoImagen ? Adjuntos.UrlMiniatura(a.IdAdjunto) : null, Adjuntos.UrlArchivo(a.IdAdjunto))).ToList();

        var comprobante = new ComprobanteSolicitud(fila.IdSolicitud, MedidasSolicitud.Numero(fila.IdSolicitud), fila.FechaSolicitud, fila.Origen,
            fila.Estado, fila.MotivoRechazo, fila.Cliente, detalles, MedidasSolicitud.AreaTotal(detalles.Select(d => d.AreaM2)),
            fila.DescripcionGeneral, adjuntos, fila.RowVersion);
        return new Leida(comprobante, fila.IdCliente, fila.RegistradaPor);
    }

    private async Task<FichaSolicitud> FichaAsync(Leida leida, CancellationToken ct)
    {
        var historial = await db.SolicitudesHistorial.AsNoTracking()
            .Where(h => h.IdSolicitud == leida.Comprobante.IdSolicitud)
            .OrderBy(h => h.Fecha).ThenBy(h => h.IdHistorial)
            .Select(h => new
            {
                h.EstadoAnterior,
                h.EstadoNuevo,
                h.Motivo,
                h.Fecha,
                h.IdEmpleado,
                h.IdCliente,
                Empleado = h.Empleado == null ? null : h.Empleado.Nombre + " " + h.Empleado.Apellido,
                Cliente = h.Cliente == null ? null : h.Cliente.Nombre + " " + h.Cliente.Apellido
            })
            .ToListAsync(ct);

        var cambios = historial.Select(h => new CambioDeEstado(h.EstadoAnterior, h.EstadoNuevo, h.Motivo, HoraChile.Convertir(h.Fecha),
            h.IdEmpleado is not null ? "personal" : h.IdCliente is not null ? "cliente" : "sistema",
            h.Empleado ?? h.Cliente)).ToList();
        // Fase 8: sus cotizaciones, con el pedido que dio cada una (tambien si despues se elimino).
        var cotizaciones = (await db.Cotizaciones.AsNoTracking()
            .Where(c => c.IdSolicitud == leida.Comprobante.IdSolicitud && !c.EliminadoLogico)
            .OrderByDescending(c => c.FechaCotizacion).ThenByDescending(c => c.IdCotizacion)
            .Select(c => new
            {
                c.IdCotizacion,
                c.FechaCotizacion,
                c.TotalCotizacion,
                c.Estado,
                Pedido = db.Pedidos.Where(p => p.IdCotizacion == c.IdCotizacion).Select(p => new { p.IdPedido, p.EliminadoLogico }).FirstOrDefault()
            })
            .ToListAsync(ct))
            .Select(c => new CotizacionDeLaSolicitud(c.IdCotizacion, ValidacionCotizacion.Numero(c.IdCotizacion), c.FechaCotizacion, c.TotalCotizacion,
                c.Estado, c.Pedido?.IdPedido, c.Pedido?.EliminadoLogico == true))
            .ToList();
        return FichaSolicitud.Desde(leida.Comprobante, leida.IdCliente, leida.RegistradaPor, cambios, cotizaciones);
    }

    // Bandeja del personal: filtros por estado, origen y fecha (de Chile), busqueda por numero, nombre,
    // telefono o RUT, orden por fecha y paginacion.
    public Task<ResultadoPaginado<FilaSolicitud>> ListarAsync(FiltroSolicitudes filtro, ParametrosListado parametros, CancellationToken ct)
    {
        var consulta = db.Solicitudes.AsNoTracking().Where(s => !s.EliminadoLogico);

        if (EstadosSolicitud.Existe(filtro.Estado))
            consulta = consulta.Where(s => s.Estado == filtro.Estado);
        if (filtro.Origen == "online")
            consulta = consulta.Where(s => s.IdEmpleadoRegistra == null);
        else if (filtro.Origen == "presencial")
            consulta = consulta.Where(s => s.IdEmpleadoRegistra != null);
        if (filtro.Desde is DateOnly desde)
        {
            var inicio = desde.ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(s => s.FechaSolicitud >= inicio);
        }
        if (filtro.Hasta is DateOnly hasta && hasta < DateOnly.MaxValue)
        {
            var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(s => s.FechaSolicitud < fin);
        }

        if (parametros.Buscar is { } buscar)
        {
            var numero = int.TryParse(buscar.TrimStart('#').Trim(), out var n) ? n : -1;
            var digitos = new string(buscar.Where(char.IsDigit).ToArray());
            var rut = buscar.Replace(".", "").Trim().ToUpperInvariant();
            consulta = consulta.Where(s => s.IdSolicitud == numero
                || (s.Cliente!.Nombre + " " + s.Cliente.Apellido).Contains(buscar)
                || (digitos.Length >= 3 && s.Cliente.Telefono.Contains(digitos))
                || (s.Cliente.Rut != null && s.Cliente.Rut.Contains(rut)));
        }

        consulta = filtro.Orden == "fecha_asc"
            ? consulta.OrderBy(s => s.FechaSolicitud).ThenBy(s => s.IdSolicitud)
            : consulta.OrderByDescending(s => s.FechaSolicitud).ThenByDescending(s => s.IdSolicitud);

        return consulta
            .Select(s => new FilaSolicitud(
                s.IdSolicitud,
                "#" + s.IdSolicitud.ToString(),
                s.FechaSolicitud,
                new ClienteFila(s.IdCliente, s.Cliente!.Nombre, s.Cliente.Apellido, s.Cliente.Telefono),
                s.Origen,
                s.Estado,
                s.Detalles.Count(),
                s.Adjuntos.Count(),
                s.EmpleadoRegistra == null ? null : s.EmpleadoRegistra.Nombre + " " + s.EmpleadoRegistra.Apellido,
                // Fase 12D: el primer letrero (para las filas cortas de «Solicitudes por atender»).
                s.Detalles.OrderBy(d => d.IdSolicitudDetalle).Select(d => d.Producto!.Nombre).FirstOrDefault()))
            .PaginarAsync(parametros, ct);
    }

    // «Mis solicitudes»: solo las del cliente del token. Fase 8 (S04): cada una con su cotizacion vigente (la
    // ultima no eliminada) y su pedido, si los tiene.
    public async Task<ResultadoPaginado<ResumenSolicitud>> ListarDelClienteAsync(int idCliente, ParametrosListado parametros, CancellationToken ct)
    {
        var pagina = await db.Solicitudes.AsNoTracking()
            .Where(s => s.IdCliente == idCliente && !s.EliminadoLogico)
            .OrderByDescending(s => s.FechaSolicitud).ThenByDescending(s => s.IdSolicitud)
            .Select(s => new
            {
                s.IdSolicitud,
                s.FechaSolicitud,
                s.Origen,
                s.Estado,
                s.MotivoRechazo,
                Resumen = s.DescripcionGeneral == null ? null : s.DescripcionGeneral.Substring(0, 160),
                Items = s.Detalles.Count(),
                Adjuntos = s.Adjuntos.Count(),
                Cotizacion = s.Cotizaciones.Where(c => !c.EliminadoLogico).OrderByDescending(c => c.IdCotizacion)
                    .Select(c => new FilaSeguimientoCotizacion(c.IdCotizacion, c.TotalCotizacion, c.Estado)).FirstOrDefault(),
                Pedido = db.Pedidos.Where(p => p.IdSolicitud == s.IdSolicitud)
                    .Select(p => new FilaSeguimientoPedido(p.IdPedido, p.EstadoPedido, p.EstadoPago, p.SaldoPendiente, p.FechaEntregaEstimada, p.ModalidadEntrega, p.EliminadoLogico))
                    .FirstOrDefault()
            })
            .PaginarAsync(parametros, ct);

        var filas = pagina.Elementos.Select(f => new ResumenSolicitud(f.IdSolicitud, MedidasSolicitud.Numero(f.IdSolicitud), f.FechaSolicitud,
            f.Origen, f.Estado, f.MotivoRechazo, f.Resumen, f.Items, f.Adjuntos, f.Cotizacion?.Seguimiento(), f.Pedido?.Seguimiento())).ToList();
        return new ResultadoPaginado<ResumenSolicitud>(filas, pagina.Total, pagina.Pagina, pagina.Tamano);
    }

    // «Mis solicitudes» por id: el comprobante con su cotizacion y su pedido. Lo ajeno no existe (null).
    public async Task<MiSolicitud?> MiSolicitudAsync(int id, int idCliente, CancellationToken ct)
    {
        if (await LeerAsync(id, idCliente, ct) is not { } leida)
            return null;
        var cotizacion = await db.Cotizaciones.AsNoTracking()
            .Where(c => c.IdSolicitud == id && !c.EliminadoLogico).OrderByDescending(c => c.IdCotizacion)
            .Select(c => new FilaSeguimientoCotizacion(c.IdCotizacion, c.TotalCotizacion, c.Estado))
            .FirstOrDefaultAsync(ct);
        var pedido = await db.Pedidos.AsNoTracking()
            .Where(p => p.IdSolicitud == id)
            .Select(p => new FilaSeguimientoPedido(p.IdPedido, p.EstadoPedido, p.EstadoPago, p.SaldoPendiente, p.FechaEntregaEstimada, p.ModalidadEntrega, p.EliminadoLogico))
            .FirstOrDefaultAsync(ct);
        return MiSolicitud.Desde(leida.Comprobante, cotizacion?.Seguimiento(), pedido?.Seguimiento());
    }

    private sealed record FilaSeguimientoCotizacion(int IdCotizacion, decimal Total, string Estado)
    {
        public SeguimientoCotizacion Seguimiento() => new(IdCotizacion, ValidacionCotizacion.Numero(IdCotizacion), Total, Estado);
    }

    // Un pedido eliminado (anulado y borrado de forma logica) se sigue mostrando, marcado: el cliente ve que se anulo.
    private sealed record FilaSeguimientoPedido(int IdPedido, string EstadoPedido, string EstadoPago, int SaldoPendiente, DateTime FechaEntregaEstimada,
        string ModalidadEntrega, bool Eliminado)
    {
        public SeguimientoPedido Seguimiento() => new(IdPedido, ReglasPedido.Numero(IdPedido), EstadoPedido, EstadoPago, SaldoPendiente,
            DateOnly.FromDateTime(FechaEntregaEstimada), ModalidadEntrega, ReglasPedido.CicloCerrado(EstadoPedido, EstadoPago), Eliminado);
    }

    public async Task<IReadOnlyList<MetricaConversionOnline>> MetricasConversionAsync(CancellationToken ct) =>
        await db.MetricasConversionOnline.AsNoTracking()
            .OrderByDescending(m => m.Anio).ThenByDescending(m => m.Mes)
            .ToListAsync(ct);

    // ── Editar, cambiar de estado y eliminar ──

    // Solo en «solicitado» o «cotizado», con la version que vio quien edita (If-Match). Editar en
    // «cotizado» deja la cotizacion pendiente como «rechazada» y la solicitud vuelve a «solicitado».
    public async Task<ResultadoSolicitud> EditarAsync(int id, int idEmpleado, string? descripcion, IReadOnlyList<EntradaDetalle>? detalles,
        byte[]? version, CancellationToken ct)
    {
        if (version is null or { Length: 0 })
            return new ResultadoSolicitud.SinVersion();

        var solicitud = await db.Solicitudes.Include(s => s.Detalles).FirstOrDefaultAsync(s => s.IdSolicitud == id && !s.EliminadoLogico, ct);
        if (solicitud is null)
            return new ResultadoSolicitud.NoEncontrada();
        if (!version.AsSpan().SequenceEqual(solicitud.RowVersion))
            return new ResultadoSolicitud.VersionObsoleta();
        if (!EstadosSolicitud.EsEditable(solicitud.Estado))
            return new ResultadoSolicitud.Conflicto($"Una solicitud «{solicitud.Estado}» ya no se puede editar: solo «solicitado» o «cotizado».");

        var errores = new Dictionary<string, string[]>();
        ValidacionSolicitud.ValidarDescripcion(descripcion, errores);
        if (detalles is not null)
            errores = Unir(errores, await ValidarAsync(descripcion, detalles, adjuntos: null, idCliente: null, ct));
        if (errores.Count > 0)
            return new ResultadoSolicitud.Invalida(errores);

        // Solo hace falta una transaccion si tambien cambia la cotizacion; si no, basta el SaveChanges.
        await using var transaccion = solicitud.Estado == EstadosSolicitud.Cotizado ? await db.Database.BeginTransactionAsync(ct) : null;
        var ahora = reloj.GetUtcNow();
        if (solicitud.Estado == EstadosSolicitud.Cotizado)
        {
            await RechazarCotizacionPendienteAsync(id, idEmpleado, "Se editó la solicitud.", ahora, ct);
            Registrar(solicitud, EstadosSolicitud.Solicitado, "Se editó la solicitud: la cotización pendiente quedó rechazada.", idEmpleado, ahora);
        }

        solicitud.DescripcionGeneral = descripcion!.Trim();
        // Toda edicion cambia la version, aunque solo cambien los items.
        db.Entry(solicitud).Property(s => s.DescripcionGeneral).IsModified = true;
        if (detalles is not null)
        {
            db.SolicitudDetalles.RemoveRange(solicitud.Detalles);
            foreach (var detalle in Detalles(detalles))
                solicitud.Detalles.Add(detalle);
        }

        return await GuardarCambiosAsync(solicitud, version, transaccion, ct);
    }

    // PATCH /estado (todo el personal, RFU11): rechazar con motivo, o devolver una «cotizado» a «solicitado».
    // Exige la version que vio quien decide (If-Match o rowVersion), igual que la edicion.
    public async Task<ResultadoSolicitud> CambiarEstadoAsync(int id, int idEmpleado, string? estado, string? motivo, byte[]? version, CancellationToken ct)
    {
        if (!EstadosSolicitud.Existe(estado))
            return new ResultadoSolicitud.Invalida(new Dictionary<string, string[]>
            {
                ["estado"] = [$"Estado inválido. Valores: {string.Join(", ", EstadosSolicitud.Todos)}."]
            });

        if (version is null or { Length: 0 })
            return new ResultadoSolicitud.SinVersion();

        var solicitud = await db.Solicitudes.FirstOrDefaultAsync(s => s.IdSolicitud == id && !s.EliminadoLogico, ct);
        if (solicitud is null)
            return new ResultadoSolicitud.NoEncontrada();
        if (!version.AsSpan().SequenceEqual(solicitud.RowVersion))
            return new ResultadoSolicitud.VersionObsoleta();

        if (TransicionesSolicitud.Explicar(solicitud.Estado, estado!, ActorEstado.Personal) is { } explicacion)
            return new ResultadoSolicitud.Conflicto(explicacion);
        if (estado == EstadosSolicitud.Rechazado && ValidacionSolicitud.ValidarMotivo(motivo) is { } errorMotivo)
            return new ResultadoSolicitud.Invalida(new Dictionary<string, string[]> { ["motivo"] = [errorMotivo] });

        // Solo hace falta una transaccion si tambien cambia la cotizacion; si no, basta el SaveChanges.
        await using var transaccion = solicitud.Estado == EstadosSolicitud.Cotizado ? await db.Database.BeginTransactionAsync(ct) : null;
        var motivoLimpio = estado == EstadosSolicitud.Rechazado ? motivo!.Trim() : null;
        var ahora = reloj.GetUtcNow();
        if (solicitud.Estado == EstadosSolicitud.Cotizado)
            await RechazarCotizacionPendienteAsync(id, idEmpleado,
                motivoLimpio is null ? "La solicitud volvió a «solicitado»." : "Se rechazó la solicitud.", ahora, ct);
        Registrar(solicitud, estado!, motivoLimpio, idEmpleado, ahora);
        solicitud.MotivoRechazo = motivoLimpio;
        // Fase 11 (S09): el cliente recibe el aviso de su solicitud rechazada, en el mismo guardado.
        var avisa = estado == EstadosSolicitud.Rechazado
            && await avisosAlCliente.EstadoSolicitudAsync(cola, id, solicitud.IdCliente, EstadosSolicitud.Rechazado, motivoLimpio, ct);
        var resultado = await GuardarCambiosAsync(solicitud, version, transaccion, ct);
        if (avisa && resultado is ResultadoSolicitud.Correcta)
            cola.AvisarAlTrabajador();
        return resultado;
    }

    // Borrado logico (no desaparece de la base ni de la metrica), solo en «solicitado» o «rechazado», sin
    // pedido y con la version que vio quien borra. Todo se comprueba en el mismo UPDATE: un pedido creado
    // o un cambio de estado justo antes no se cuelan entre la revision y el borrado.
    public async Task<ResultadoSolicitud> EliminarLogicoAsync(int id, byte[]? version, CancellationToken ct)
    {
        if (version is null or { Length: 0 })
            return new ResultadoSolicitud.SinVersion();

        var fecha = FechaChile(reloj.GetUtcNow());
        // El borrado y su registro en la bitacora van juntos (el interceptor no ve un ExecuteUpdate).
        await using (var transaccion = await db.Database.BeginTransactionAsync(ct))
        {
            var eliminadas = await db.Solicitudes
                .Where(s => s.IdSolicitud == id && !s.EliminadoLogico
                    && (s.Estado == EstadosSolicitud.Solicitado || s.Estado == EstadosSolicitud.Rechazado)
                    && s.RowVersion == version
                    && !db.Pedidos.Any(p => p.IdSolicitud == id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.EliminadoLogico, true)
                    .SetProperty(x => x.FechaEliminacionLogica, (DateTime?)fecha), ct);
            if (eliminadas == 1)
            {
                await auditoria.RegistrarAsync("solicitud", id.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.Eliminar,
                    DetalleAuditoria.Libre(new Dictionary<string, object?> { ["borradoLogico"] = true }), ct);
                await transaccion.CommitAsync(CancellationToken.None);
                return new ResultadoSolicitud.Eliminada();
            }
        }

        // No se borro: se explica por que, sin haber cambiado nada.
        var actual = await db.Solicitudes.AsNoTracking().Where(s => s.IdSolicitud == id && !s.EliminadoLogico)
            .Select(s => new { s.Estado, s.RowVersion }).FirstOrDefaultAsync(ct);
        if (actual is null)
            return new ResultadoSolicitud.NoEncontrada();
        if (await db.Pedidos.AnyAsync(p => p.IdSolicitud == id, ct))
            return new ResultadoSolicitud.Conflicto("La solicitud ya tiene un pedido y no se puede eliminar.");
        if (!actual.RowVersion.AsSpan().SequenceEqual(version))
            return new ResultadoSolicitud.VersionObsoleta();
        if (!EstadosSolicitud.PuedeEliminarse(actual.Estado))
            return new ResultadoSolicitud.Conflicto($"Una solicitud «{actual.Estado}» no se puede eliminar: solo «solicitado» o «rechazado».");
        return new ResultadoSolicitud.VersionObsoleta();
    }

    private void Registrar(Solicitud solicitud, string nuevo, string? motivo, int idEmpleado, DateTimeOffset ahora)
    {
        db.SolicitudesHistorial.Add(new SolicitudHistorial
        {
            IdSolicitud = solicitud.IdSolicitud,
            EstadoAnterior = solicitud.Estado,
            EstadoNuevo = nuevo,
            Motivo = motivo,
            IdEmpleado = idEmpleado,
            Fecha = ahora
        });
        solicitud.Estado = nuevo;
    }

    // Una sola cotizacion «pendiente» a la vez: al editar, rechazar o devolver la solicitud, la vigente
    // queda «rechazada» y se conserva como historial. Fase 8: la respuesta la registra quien lo hizo, en el
    // local, con el motivo (ck_cotizaciones_respuesta exige fecha y via en toda cotizacion respondida).
    // Fase 11: el interceptor no ve este UPDATE; cada cotizacion rechazada se anota a mano y se guarda con el mismo
    // SaveChanges (y la misma transaccion) que el cambio de la solicitud.
    private async Task<int> RechazarCotizacionPendienteAsync(int idSolicitud, int idEmpleado, string motivo, DateTimeOffset ahora, CancellationToken ct)
    {
        var pendientes = await db.Cotizaciones.AsNoTracking().Where(c => c.IdSolicitud == idSolicitud && c.Estado == Pendiente && !c.EliminadoLogico)
            .Select(c => c.IdCotizacion).ToListAsync(ct);
        if (pendientes.Count == 0)
            return 0;
        var rechazadas = await db.Cotizaciones.Where(c => pendientes.Contains(c.IdCotizacion) && c.Estado == Pendiente && !c.EliminadoLogico)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Estado, Rechazada)
                .SetProperty(c => c.FechaRespuesta, (DateTimeOffset?)ahora)
                .SetProperty(c => c.ViaRespuesta, ViasRespuesta.Presencial)
                .SetProperty(c => c.IdEmpleadoRespuesta, (int?)idEmpleado)
                .SetProperty(c => c.MotivoRechazo, motivo), ct);
        foreach (var idCotizacion in pendientes)
            auditoria.Anotar("cotizacion", idCotizacion.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.Modificar,
                DetalleAuditoria.Libre(new Dictionary<string, object?>
                {
                    ["campos"] = new[] { "estado", "fechaRespuesta", "viaRespuesta", "idEmpleadoRespuesta", "motivoRechazo" },
                    ["estado"] = $"{Pendiente} → {Rechazada}"
                }));
        return rechazadas;
    }

    private async Task<ResultadoSolicitud> GuardarCambiosAsync(Solicitud solicitud, byte[] version,
        IDbContextTransaction? transaccion, CancellationToken ct)
    {
        // La base compara con la version que vio quien edita: si otra persona guardo entre la lectura y
        // este UPDATE, no se pisa su trabajo.
        db.Entry(solicitud).Property(s => s.RowVersion).OriginalValue = version;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoSolicitud.VersionObsoleta();
        }
        if (transaccion is not null)
            await transaccion.CommitAsync(CancellationToken.None);
        // Ya guardado: la respuesta se arma aunque se cancele la peticion.
        var leida = (await LeerAsync(solicitud.IdSolicitud, idClienteDueno: null, CancellationToken.None))!;
        return new ResultadoSolicitud.Correcta(leida.Comprobante, await FichaAsync(leida, CancellationToken.None));
    }

    // ── Apoyo ──

    private async Task<Dictionary<string, string[]>> ValidarAsync(string? descripcion, IReadOnlyList<EntradaDetalle>? detalles,
        IReadOnlyList<int>? adjuntos, int? idCliente, CancellationToken ct)
    {
        var errores = ValidacionSolicitud.Validar(descripcion, detalles, adjuntos);

        await ValidadorCatalogo.ValidarAsync(db, detalles, errores, ct);

        if (idCliente is int dueno && adjuntos is { Count: > 0 } && !errores.ContainsKey("adjuntos"))
        {
            var ids = adjuntos.Distinct().ToList();
            var propios = await db.AdjuntosSolicitud.AsNoTracking()
                .CountAsync(a => ids.Contains(a.IdAdjunto) && a.IdCliente == dueno && a.IdSolicitud == null, ct);
            if (propios != ids.Count)
                errores = Unir(errores, ErrorAdjuntos());
        }
        return errores;
    }

    private static Dictionary<string, string[]> ErrorAdjuntos() => new()
    {
        ["adjuntos"] = ["Uno de los archivos ya no está disponible. Quítalo y vuelve a adjuntarlo."]
    };

    private static Dictionary<string, string[]> Unir(Dictionary<string, string[]> a, Dictionary<string, string[]> b)
    {
        foreach (var (clave, valor) in b)
            a[clave] = valor;
        return a;
    }

    private static List<SolicitudDetalle> Detalles(IReadOnlyList<EntradaDetalle> detalles) =>
        detalles.Select(d => new SolicitudDetalle
        {
            IdProducto = d.IdProducto,
            // La columna es NOT NULL: sin descripcion del item se guarda vacia (el comprobante la muestra null).
            Descripcion = d.Descripcion?.Trim() ?? "",
            AnchoMm = d.AnchoMm,
            AltoMm = d.AltoMm,
            Cantidad = d.Cantidad,
            Opciones = ReglasCatalogo.Serializar(d.Opciones)
        }).ToList();

    // fecha_solicitud es datetime2(0) en hora de Chile: se trunca a segundos para que lo guardado y lo
    // devuelto coincidan.
    private static DateTime FechaChile(DateTimeOffset instante)
    {
        var chile = HoraChile.Convertir(instante);
        return new DateTime(chile.Ticks - chile.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Unspecified);
    }

    // Clave propia de la huella: Solicitudes:ClaveHuella si existe; si no, una derivada de Jwt:Key con
    // HKDF (etiqueta «synctime:huella-ip»). Asi la clave que firma los tokens nunca se usa tal cual aqui.
    private byte[] ClaveHuella()
    {
        if (configuracion["Solicitudes:ClaveHuella"] is { Length: > 0 } propia)
            return System.Text.Encoding.UTF8.GetBytes(propia);
        return configuracion["Jwt:Key"] is { Length: > 0 } jwt
            ? HuellaIp.DerivarClave(jwt)
            : throw new InvalidOperationException("No se configuro Jwt:Key.");
    }
}

// ── Adjuntos privados ──

public sealed record AdjuntoSubido(int IdAdjunto, string Tipo, string Nombre, int Bytes, int? Ancho, int? Alto, string? UrlMiniatura);

public sealed record ArchivoAdjunto(string Ruta, string TipoContenido, string? NombreDescarga);

public abstract record ResultadoAdjunto
{
    public sealed record Subido(AdjuntoSubido Adjunto) : ResultadoAdjunto;
    public sealed record Invalido(string Codigo, string Mensaje) : ResultadoAdjunto;
    public sealed record Tope : ResultadoAdjunto;
    public sealed record NoEncontrado : ResultadoAdjunto;
    public sealed record Conflicto(string Mensaje) : ResultadoAdjunto;
    public sealed record Borrado : ResultadoAdjunto;
    public sealed record CorreoSinConfirmar : ResultadoAdjunto;
}

// Quien pide un adjunto: un cliente (solo ve los suyos) o el personal (ve todos).
public sealed record LectorAdjunto(int? IdCliente)
{
    public static LectorAdjunto Personal => new((int?)null);
    public static LectorAdjunto DeCliente(int id) => new(id);
}

public sealed class ServicioAdjuntos(LetrerosHerreradbContext db, AlmacenAdjuntos almacen, IAuditoria auditoria, IConfiguration configuracion, TimeProvider reloj)
{
    // Procesar una foto grande ocupa memoria y procesador: como mucho dos a la vez.
    private static readonly SemaphoreSlim Procesando = new(2, 2);

    private sealed record Preparado(FormatoAdjunto Formato, string Nombre, int Bytes, ImagenProcesada? Imagen, byte[]? Pdf);

    // Borrador del cliente: queda sin solicitud hasta que la envie (o la purga lo borra a las 24 horas).
    // Como al enviar la solicitud, exige el correo confirmado: una cuenta recien creada por un robot no
    // puede usar el disco del taller.
    public async Task<ResultadoAdjunto> SubirBorradorAsync(int idCliente, byte[] datos, string? nombre, CancellationToken ct)
    {
        if (!await db.Clientes.AnyAsync(c => c.IdCliente == idCliente && c.Activo && c.FechaVerificacionCorreo != null, ct))
            return new ResultadoAdjunto.CorreoSinConfirmar();
        if (await db.AdjuntosSolicitud.CountAsync(a => a.IdCliente == idCliente && a.IdSolicitud == null, ct) >= ReglasAdjunto.MaxPorSolicitud)
            return new ResultadoAdjunto.Tope();

        return await SubirAsync(datos, nombre, $"synctime_adjuntos_cliente_{idCliente}",
            () => db.AdjuntosSolicitud.CountAsync(a => a.IdCliente == idCliente && a.IdSolicitud == null, ct),
            (adjunto, _) => adjunto.IdCliente = idCliente, alGuardar: null, ct);
    }

    // El personal adjunta directo a una solicitud (por ejemplo, en el registro presencial).
    public async Task<ResultadoAdjunto> SubirASolicitudAsync(int idSolicitud, int idEmpleado, byte[] datos, string? nombre, CancellationToken ct)
    {
        var estado = await db.Solicitudes.AsNoTracking()
            .Where(s => s.IdSolicitud == idSolicitud && !s.EliminadoLogico).Select(s => s.Estado).FirstOrDefaultAsync(ct);
        if (estado is null)
            return new ResultadoAdjunto.NoEncontrado();
        if (!EstadosSolicitud.EsEditable(estado))
            return new ResultadoAdjunto.Conflicto("Solo se pueden adjuntar archivos a una solicitud «solicitado» o «cotizado».");
        if (await db.AdjuntosSolicitud.CountAsync(a => a.IdSolicitud == idSolicitud, ct) >= ReglasAdjunto.MaxPorSolicitud)
            return new ResultadoAdjunto.Tope();

        return await SubirAsync(datos, nombre, $"synctime_adjuntos_solicitud_{idSolicitud}",
            () => db.AdjuntosSolicitud.CountAsync(a => a.IdSolicitud == idSolicitud, ct),
            (adjunto, ahora) =>
            {
                adjunto.IdEmpleado = idEmpleado;
                adjunto.IdSolicitud = idSolicitud;
                adjunto.FechaVinculo = ahora;
            },
            // La solicitud cambio: su version tambien, para que quien la tenga abierta lo note.
            alGuardar: () => CambiarVersionAsync(idSolicitud, ct), ct);
    }

    private async Task<ResultadoAdjunto> SubirAsync(byte[] datos, string? nombre, string recurso, Func<Task<int>> contar,
        Action<AdjuntoSolicitud, DateTimeOffset> asignar, Func<Task<int>>? alGuardar, CancellationToken ct)
    {
        var (listo, rechazo) = await PrepararAsync(datos, nombre, ct);
        if (listo is null)
            return rechazo!;

        var clave = AlmacenAdjuntos.NuevaClave();
        try
        {
            if (listo.Imagen is { } imagen)
                await almacen.GuardarImagenAsync(clave, imagen, ct);
            else
                await almacen.GuardarPdfAsync(clave, listo.Pdf!, ct);

            var ahora = reloj.GetUtcNow();
            var adjunto = new AdjuntoSolicitud
            {
                Clave = clave,
                Tipo = listo.Formato == FormatoAdjunto.Pdf ? AdjuntoSolicitud.TipoPdf : AdjuntoSolicitud.TipoImagen,
                NombreOriginal = listo.Nombre,
                Bytes = listo.Bytes,
                Ancho = listo.Imagen?.Ancho,
                Alto = listo.Imagen?.Alto,
                FechaSubida = ahora
            };
            asignar(adjunto, ahora);

            // El tope se revisa de nuevo dentro de la transaccion, con las subidas del mismo dueño en fila:
            // dos subidas simultaneas no pueden dejar 6 archivos.
            await using var transaccion = await db.Database.BeginTransactionAsync(ct);
            await BloqueoDeAplicacion.TomarAsync(db, recurso, BloqueoDeAplicacion.Espera(configuracion), ct);
            if (await contar() >= ReglasAdjunto.MaxPorSolicitud)
            {
                almacen.Borrar(clave);
                return new ResultadoAdjunto.Tope();
            }
            db.AdjuntosSolicitud.Add(adjunto);
            if (alGuardar is not null)
                await alGuardar();
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(CancellationToken.None);

            return new ResultadoAdjunto.Subido(new AdjuntoSubido(adjunto.IdAdjunto, adjunto.Tipo, adjunto.NombreOriginal, adjunto.Bytes,
                adjunto.Ancho, adjunto.Alto, adjunto.Tipo == AdjuntoSolicitud.TipoImagen ? Adjuntos.UrlMiniatura(adjunto.IdAdjunto) : null));
        }
        catch
        {
            // Sin registro en la base, los archivos quedarian huerfanos.
            almacen.Borrar(clave);
            throw;
        }
    }

    // El formato se decide por el contenido: imagen (se vuelve a codificar sin metadatos) o PDF (tal cual).
    private static async Task<(Preparado? Listo, ResultadoAdjunto? Rechazo)> PrepararAsync(byte[] datos, string? nombre, CancellationToken ct)
    {
        if (datos.Length == 0)
            return (null, new ResultadoAdjunto.Invalido("sin_archivo", ReglasAdjunto.MensajeSinArchivo));
        if (datos.LongLength > ReglasAdjunto.MaxBytes)
            return (null, new ResultadoAdjunto.Invalido("archivo_muy_grande", ReglasAdjunto.MensajeTamano));

        var formato = ReglasAdjunto.Detectar(datos);
        if (formato == FormatoAdjunto.Desconocido)
            return (null, new ResultadoAdjunto.Invalido("formato_no_aceptado", ReglasAdjunto.MensajeFormato));

        var limpio = ReglasAdjunto.SanearNombre(nombre, formato);
        if (formato == FormatoAdjunto.Pdf)
            return (new Preparado(formato, limpio, datos.Length, null, datos), null);

        await Procesando.WaitAsync(ct);
        try
        {
            var imagen = await Task.Run(() => ProcesadorImagenes.Procesar(datos), ct);
            return (new Preparado(formato, limpio, datos.Length, imagen, null), null);
        }
        catch (ImagenInvalidaException e)
        {
            return (null, new ResultadoAdjunto.Invalido("imagen_invalida", e.Message));
        }
        finally
        {
            Procesando.Release();
        }
    }

    // El cliente quita un borrador suyo. Lo ajeno, o lo ya enviado, no existe para el (404).
    // Los borradores del cliente (aun sin solicitud), del mas antiguo al mas nuevo: el formulario los
    // recupera si el cliente salio de la pantalla. Los que ya borro la purga no aparecen.
    public async Task<IReadOnlyList<AdjuntoSubido>> BorradoresAsync(int idCliente, CancellationToken ct) =>
        (await db.AdjuntosSolicitud.AsNoTracking()
            .Where(a => a.IdCliente == idCliente && a.IdSolicitud == null)
            .OrderBy(a => a.FechaSubida).ThenBy(a => a.IdAdjunto)
            .Select(a => new { a.IdAdjunto, a.Tipo, a.NombreOriginal, a.Bytes, a.Ancho, a.Alto })
            .ToListAsync(ct))
        .Select(a => new AdjuntoSubido(a.IdAdjunto, a.Tipo, a.NombreOriginal, a.Bytes, a.Ancho, a.Alto,
            a.Tipo == AdjuntoSolicitud.TipoImagen ? Adjuntos.UrlMiniatura(a.IdAdjunto) : null))
        .ToList();

    // El borrado repite las condiciones en el DELETE: si el borrador se vinculo a una solicitud justo
    // antes, no se borra (ni su archivo).
    public async Task<bool> BorrarBorradorAsync(int idCliente, int idAdjunto, CancellationToken ct)
    {
        var clave = await db.AdjuntosSolicitud.AsNoTracking()
            .Where(a => a.IdAdjunto == idAdjunto && a.IdCliente == idCliente && a.IdSolicitud == null)
            .Select(a => a.Clave).FirstOrDefaultAsync(ct);
        if (clave is null)
            return false;
        var borrados = await db.AdjuntosSolicitud
            .Where(a => a.IdAdjunto == idAdjunto && a.IdCliente == idCliente && a.IdSolicitud == null)
            .ExecuteDeleteAsync(ct);
        if (borrados != 1)
            return false;
        almacen.Borrar(clave);
        return true;
    }

    // El personal quita un adjunto: queda anotado en el historial (sin cambio de estado) y la version de
    // la solicitud cambia, todo en una transaccion. El archivo se borra despues del COMMIT.
    public async Task<ResultadoAdjunto> BorrarDeSolicitudAsync(int idSolicitud, int idAdjunto, int idEmpleado, CancellationToken ct)
    {
        var adjunto = await db.AdjuntosSolicitud.Include(a => a.Solicitud)
            .FirstOrDefaultAsync(a => a.IdAdjunto == idAdjunto && a.IdSolicitud == idSolicitud && !a.Solicitud!.EliminadoLogico, ct);
        if (adjunto is null)
            return new ResultadoAdjunto.NoEncontrado();
        var estado = adjunto.Solicitud!.Estado;
        if (!EstadosSolicitud.EsEditable(estado))
            return new ResultadoAdjunto.Conflicto("Solo se pueden quitar archivos de una solicitud «solicitado» o «cotizado».");

        await using (var transaccion = await db.Database.BeginTransactionAsync(ct))
        {
            db.AdjuntosSolicitud.Remove(adjunto);
            db.SolicitudesHistorial.Add(new SolicitudHistorial
            {
                IdSolicitud = idSolicitud,
                EstadoAnterior = estado,
                EstadoNuevo = estado,
                Motivo = $"Se quitó el adjunto {adjunto.NombreOriginal}",
                IdEmpleado = idEmpleado,
                Fecha = reloj.GetUtcNow()
            });
            await db.SaveChangesAsync(ct);
            await CambiarVersionAsync(idSolicitud, ct);
            await transaccion.CommitAsync(CancellationToken.None);
        }
        almacen.Borrar(adjunto.Clave);
        return new ResultadoAdjunto.Borrado();
    }

    // Un UPDATE sin cambios de datos: SQL Server renueva igual el row_version de la solicitud. Fase 11: queda en la
    // bitacora como un cambio de los adjuntos de la solicitud (el interceptor no ve este UPDATE).
    private async Task<int> CambiarVersionAsync(int idSolicitud, CancellationToken ct)
    {
        var cambiadas = await db.Solicitudes.Where(s => s.IdSolicitud == idSolicitud).ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, x => x.Estado), ct);
        await auditoria.RegistrarAsync("solicitud", idSolicitud.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.Modificar,
            DetalleAuditoria.Libre(new Dictionary<string, object?> { ["campos"] = new[] { "adjuntos" } }), ct);
        return cambiadas;
    }

    // El archivo pedido si quien lo pide puede verlo: el cliente dueño (lo subio el, o es de una solicitud
    // suya) o cualquier empleado. A los demas se les responde igual que si no existiera.
    public async Task<ArchivoAdjunto?> AbrirAsync(int idAdjunto, LectorAdjunto lector, VersionAdjunto version, CancellationToken ct)
    {
        var adjunto = await db.AdjuntosSolicitud.AsNoTracking()
            .Where(a => a.IdAdjunto == idAdjunto)
            .Select(a => new
            {
                a.Clave,
                a.Tipo,
                a.NombreOriginal,
                a.IdCliente,
                DuenoSolicitud = a.Solicitud == null ? (int?)null : a.Solicitud.IdCliente,
                SolicitudEliminada = a.Solicitud != null && a.Solicitud.EliminadoLogico
            })
            .FirstOrDefaultAsync(ct);
        if (adjunto is null)
            return null;
        // El cliente ve lo suyo mientras la solicitud no este eliminada; el personal lo ve siempre.
        if (lector.IdCliente is int idCliente
            && ((adjunto.IdCliente != idCliente && adjunto.DuenoSolicitud != idCliente) || adjunto.SolicitudEliminada))
            return null;

        var esPdf = adjunto.Tipo == AdjuntoSolicitud.TipoPdf;
        var ruta = almacen.Ruta(adjunto.Clave, esPdf, version);
        if (ruta is null)
            return null;
        return esPdf
            ? new ArchivoAdjunto(ruta, "application/pdf", ReglasAdjunto.NombreDescargaPdf(adjunto.NombreOriginal))
            : new ArchivoAdjunto(ruta, "image/webp", null);
    }
}

// ── Clientes (RFU10, RFU19) ──

public sealed record FilaCliente(int IdCliente, string? Rut, string Nombre, string Apellido, string Telefono, string? Email,
    string? Direccion, bool Registrado, bool CorreoConfirmado, bool Activo, DateTime FechaRegistro, bool CuentaDadaDeBaja);

public sealed class ServicioClientes(LetrerosHerreradbContext db)
{
    // Listado del personal: busca por telefono, RUT o nombre; ordenado por apellido y nombre.
    public Task<ResultadoPaginado<FilaCliente>> ListarAsync(ParametrosListado parametros, CancellationToken ct)
    {
        var consulta = db.Clientes.AsNoTracking();
        if (parametros.Buscar is { } buscar)
        {
            var digitos = new string(buscar.Where(char.IsDigit).ToArray());
            // Un telefono escrito «+56 9 1234 5678» se guarda como «912345678»; un RUT, como «12345678-5» (con o sin puntos y guion al buscar).
            var telefono = ValidacionCuenta.NormalizarTelefono(buscar);
            var rutExacto = Rut.TryNormalizar(buscar, out var normalizado) ? normalizado : null;
            var rut = buscar.Replace(".", "").Trim().ToUpperInvariant();
            // Con menos de 3 caracteres un trozo de RUT o de telefono coincide con medio padron (y «.» queda vacio y coincide con todos).
            var buscaPorRut = rut.Length >= 3;
            consulta = consulta.Where(c => (c.Nombre + " " + c.Apellido).Contains(buscar)
                || (digitos.Length >= 3 && c.Telefono.Contains(digitos))
                || (telefono != null && c.Telefono.Contains(telefono))
                || (rutExacto != null && c.Rut == rutExacto)
                || (buscaPorRut && c.Rut != null && c.Rut.Contains(rut)));
        }
        return consulta
            .OrderBy(c => c.Apellido).ThenBy(c => c.Nombre).ThenBy(c => c.IdCliente)
            .Select(Fila)
            .PaginarAsync(parametros, ct);
    }

    // Para el registro presencial: antes de crear un cliente, se busca por telefono o RUT. 0 o 1 resultado.
    public async Task<IReadOnlyList<FilaCliente>> BuscarAsync(string? telefono, string? rut, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(telefono))
        {
            var escrito = telefono.Trim();
            var formas = new List<string> { escrito };
            if (ValidacionCuenta.NormalizarTelefono(escrito) is { } nueve)
                formas.AddRange([nueve, $"+56{nueve}"]);
            var porTelefono = await db.Clientes.AsNoTracking().Where(c => formas.Contains(c.Telefono))
                .OrderBy(c => c.IdCliente).Select(Fila).FirstOrDefaultAsync(ct);
            if (porTelefono is not null)
                return [porTelefono];
        }
        if (!string.IsNullOrWhiteSpace(rut))
        {
            var formas = new List<string> { rut.Trim(), rut.Trim().ToUpperInvariant() };
            if (Rut.TryNormalizar(rut, out var normalizado))
                formas.Add(normalizado);
            var porRut = await db.Clientes.AsNoTracking().Where(c => c.Rut != null && formas.Contains(c.Rut))
                .OrderBy(c => c.IdCliente).Select(Fila).FirstOrDefaultAsync(ct);
            if (porRut is not null)
                return [porRut];
        }
        return [];
    }

    private static readonly System.Linq.Expressions.Expression<Func<Cliente, FilaCliente>> Fila = c => new FilaCliente(
        c.IdCliente, c.Rut, c.Nombre, c.Apellido, c.Telefono, c.Email, c.Direccion, c.Registrado,
        c.FechaVerificacionCorreo != null, c.Activo, c.FechaRegistro, c.InhabilitadoEn != null);
}

// Bloqueo de SQL Server con nombre (sp_getapplock), atado a la transaccion en curso: pone en fila las
// operaciones que deben revisar y escribir sin que otra se cuele en medio. Se suelta solo al COMMIT o
// al ROLLBACK. Si no se consigue a tiempo, lanza (mejor un error que saltarse la regla).
internal static class BloqueoDeAplicacion
{
    // Solicitudes:EsperaBloqueoMs (15 s por defecto): cuanto se espera el turno antes de responder 503.
    public static int Espera(IConfiguration configuracion) =>
        Math.Clamp(configuracion.GetValue("Solicitudes:EsperaBloqueoMs", 15_000), 100, 60_000);

    public static async Task TomarAsync(LetrerosHerreradbContext db, string recurso, int esperaMs, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlAsync($"""
                DECLARE @resultado int;
                EXEC @resultado = sp_getapplock @Resource = {recurso}, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = {esperaMs};
                IF @resultado < 0 THROW 50070, N'No se pudo tomar el bloqueo de la operación.', 1;
                """, ct);
        }
        catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number is 50070 or 1222)
        {
            throw new OperacionOcupadaException();
        }
    }
}

// Demasiadas operaciones iguales a la vez: el cliente puede reintentar en unos segundos (503 con Retry-After).
public sealed class OperacionOcupadaException() : Exception("El servidor está atendiendo otras solicitudes iguales. Inténtalo de nuevo en unos segundos.");
