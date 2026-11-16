using System.Globalization;
using LetrerosHerreraSYNCTIMEapi.Datos;
using LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;
using LetrerosHerreraSYNCTIMEapi.Datos.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Cotizaciones;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using LetrerosHerreraSYNCTIMEapi.Negocio.Pedidos;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Fase 8: pedidos, abonos, cobranza y calendario (RFS06, RFU13-RFU15, RFU35, RFU36, E08, E09, E25-E27, S07, S08, S24) ──

public abstract record ResultadoPedido
{
    public sealed record Correcto(FichaPedido Ficha) : ResultadoPedido;
    public sealed record Invalido(IReadOnlyDictionary<string, string[]> Errores) : ResultadoPedido;
    public sealed record NoEncontrado : ResultadoPedido;
    // DesdeLaBase: la regla la hizo cumplir la base (nivel «base_de_datos» en la respuesta).
    public sealed record Conflicto(string Codigo, string Mensaje, bool DesdeLaBase = false) : ResultadoPedido;
    public sealed record SinVersion : ResultadoPedido;
    public sealed record VersionObsoleta : ResultadoPedido;
    public sealed record SoloGestion(string Mensaje) : ResultadoPedido;
    public sealed record Eliminado : ResultadoPedido;
}

// Quien hace la operacion: su id y si es de gestion (dueño o administrador). Sale siempre del token.
public sealed record PersonalQueOpera(int IdEmpleado, bool EsGestion);

public sealed record FiltroPedidos(string? EstadoPedido, string? EstadoPago, bool Cobranza, int? Anio, int? Mes, int? IdCliente, int? IdResponsable, string? Orden);

// Pedido presencial directo (RFU35, E26): la solicitud con sus items, la cotizacion y los datos del pedido.
public sealed record EntradaPedidoDirecto(
    int IdCliente,
    string? DescripcionGeneral,
    IReadOnlyList<EntradaDetalle>? Detalles,
    EntradaCotizacion Cotizacion,
    EntradaDatosPedido Datos,
    EntradaAbono? AbonoInicial);

public sealed class ServicioPedidos(LetrerosHerreradbContext db, ColaEnTransaccion cola, AvisosAlCliente avisosAlCliente, IAuditoria auditoria,
    TimeProvider reloj)
{
    public const string MensajeVersionObsoleta = "Otra persona modificó este pedido mientras lo veías. Vuelve a cargarlo para ver los cambios.";
    public const string MensajePedidoAnulado = "Un pedido anulado no recibe abonos.";
    public const string MensajeAnularConAbonos = "Este pedido tiene abonos: solo el dueño o un administrador puede anularlo.";
    public const string MensajeAnularAbonoAjeno = "Solo puedes anular los abonos que registraste en las últimas 24 horas. Pide a gestión que lo anule.";

    private const string IndicePedidoCotizacion = "uq_pedidos_cotizacion";
    private const string IndicePedidoSolicitud = "uq_pedidos_solicitud";
    private const string ReglaMontoAbonado = "ck_pedidos_monto_abonado";

    public static string MensajeExcedeSaldo(int saldo) => $"El abono supera el saldo pendiente ({FormatoChile.Pesos(saldo)}).";

    public static string MensajeYaTienePedido(int idPedido, bool eliminado) => eliminado
        ? $"Esta cotización ya tuvo el pedido {ReglasPedido.Numero(idPedido)} (eliminado)."
        : $"Esta cotización ya tiene el pedido {ReglasPedido.Numero(idPedido)}.";

    public const string MensajeMontosSoloGestion = "Solo el dueño o un administrador puede cambiar los montos de un pedido.";

    // ── Crear ──

    // POST /cotizaciones/{id}/pedido. En una transaccion: el pedido «aceptado» con los montos de la
    // cotizacion aprobada (el total redondeado al peso), su historial y el abono inicial, si lo hay.
    public async Task<ResultadoPedido> CrearDesdeCotizacionAsync(int idCotizacion, PersonalQueOpera quien, EntradaDatosPedido datos,
        EntradaAbono? abono, CancellationToken ct)
    {
        var hoy = TiempoChile.Hoy(reloj);
        var errores = ReglasPedido.ValidarDatos(datos, hoy);
        await ValidarResponsableAsync(datos.IdEmpleadoResponsable, errores, ct);
        ValidarAbonoInicial(abono, hoy, errores);
        if (errores.Count > 0)
            return new ResultadoPedido.Invalido(errores);

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        // La cotizacion y su solicitud se leen dentro de la transaccion: la solicitud debe seguir «aceptado».
        var cotizacion = await db.Cotizaciones.AsNoTracking()
            .Where(c => c.IdCotizacion == idCotizacion && !c.EliminadoLogico)
            .Select(c => new { c.IdSolicitud, c.Estado, c.TotalCotizacion, EstadoSolicitud = c.Solicitud!.Estado, SolicitudEliminada = c.Solicitud.EliminadoLogico })
            .FirstOrDefaultAsync(ct);
        if (cotizacion is null)
            return new ResultadoPedido.NoEncontrado();
        if (cotizacion.Estado != EstadosCotizacion.Aprobada)
            return new ResultadoPedido.Conflicto(CodigosConflicto.TransicionInvalida,
                $"Solo una cotización «aprobada» da origen a un pedido. Esta está «{cotizacion.Estado}».");
        // Una cotizacion (y una solicitud) da a lo mas un pedido, aunque despues se haya eliminado.
        var existente = await db.Pedidos.Where(p => p.IdCotizacion == idCotizacion || p.IdSolicitud == cotizacion.IdSolicitud)
            .Select(p => new { p.IdPedido, p.EliminadoLogico }).FirstOrDefaultAsync(ct);
        if (existente is not null)
            return new ResultadoPedido.Conflicto(CodigosConflicto.CotizacionConPedido, MensajeYaTienePedido(existente.IdPedido, existente.EliminadoLogico));
        if (cotizacion.SolicitudEliminada)
            return new ResultadoPedido.Conflicto(CodigosConflicto.TransicionInvalida, "La solicitud de esta cotización fue eliminada: no se le puede crear un pedido.");
        if (cotizacion.EstadoSolicitud != EstadosSolicitud.Aceptado)
            return new ResultadoPedido.Conflicto(CodigosConflicto.TransicionInvalida,
                $"La solicitud debe estar «aceptado» para crear su pedido. Está «{cotizacion.EstadoSolicitud}».");

        var montoBase = ValidacionCotizacion.Redondear(cotizacion.TotalCotizacion);
        if (Montos(montoBase, datos, abono) is { } rechazo)
            return rechazo;

        var ahora = reloj.GetUtcNow();
        var pedido = Nuevo(cotizacion.IdSolicitud, OrigenesPedido.Cotizacion, montoBase, datos, ahora);
        pedido.IdCotizacion = idCotizacion;
        Historial(pedido, null, EstadosPedido.Aceptado, $"Creado desde la cotización {ValidacionCotizacion.Numero(idCotizacion)}.", quien, ahora);
        if (abono is not null)
            db.Pagos.Add(NuevoPago(pedido, abono, quien, hoy, ahora));
        db.Pedidos.Add(pedido);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (ErroresSql.EsDuplicado(e, IndicePedidoCotizacion, IndicePedidoSolicitud))
        {
            return new ResultadoPedido.Conflicto(CodigosConflicto.CotizacionConPedido, "Esta cotización ya tiene un pedido.", DesdeLaBase: true);
        }
        catch (DbUpdateException e) when (ErroresSql.EsRegla(e, ReglaMontoAbonado))
        {
            return new ResultadoPedido.Conflicto(CodigosConflicto.AbonoExcedeSaldo, MensajeExcedeSaldo(montoBase + (int)datos.CostoInstalacionAdicional!.Value), DesdeLaBase: true);
        }
        await transaccion.CommitAsync(CancellationToken.None);
        return new ResultadoPedido.Correcto((await FichaAsync(pedido.IdPedido, quien, CancellationToken.None))!);
    }

    // POST /pedidos/directo. En una transaccion: la solicitud presencial con sus items, ya «aceptado» y con
    // la nota «Pedido directo en el taller»; la cotizacion «aprobada» en el local por quien la registra; el
    // pedido con origen «directo» y el abono. No envia correos.
    public async Task<ResultadoPedido> CrearDirectoAsync(PersonalQueOpera quien, EntradaPedidoDirecto entrada, CancellationToken ct)
    {
        var hoy = TiempoChile.Hoy(reloj);
        var errores = ValidacionSolicitud.Validar(entrada.DescripcionGeneral, entrada.Detalles, adjuntos: null);
        await ValidarProductosAsync(entrada.Detalles, errores, ct);
        var cliente = await db.Clientes.AsNoTracking().Where(c => c.IdCliente == entrada.IdCliente).Select(c => new { c.Activo }).FirstOrDefaultAsync(ct);
        if (cliente is null)
            errores["idCliente"] = ["El cliente no existe."];
        else if (!cliente.Activo)
            errores["idCliente"] = ["El cliente está desactivado: actívalo antes de registrarle un pedido."];
        Unir(errores, ValidacionCotizacion.Validar(entrada.Cotizacion));
        await ConsultasDeCotizacion.ValidarMaterialesAsync(db, entrada.Cotizacion.Materiales, errores, ct);
        Unir(errores, ReglasPedido.ValidarDatos(entrada.Datos, hoy));
        await ValidarResponsableAsync(entrada.Datos.IdEmpleadoResponsable, errores, ct);
        ValidarAbonoInicial(entrada.AbonoInicial, hoy, errores);
        if (errores.Count > 0)
            return new ResultadoPedido.Invalido(errores);

        var montoBase = ValidacionCotizacion.Redondear(ValidacionCotizacion.Total(entrada.Cotizacion));
        if (Montos(montoBase, entrada.Datos, entrada.AbonoInicial) is { } rechazo)
            return rechazo;

        var ahora = reloj.GetUtcNow();
        const string nota = "Pedido directo en el taller";
        var solicitud = new Solicitud
        {
            IdCliente = entrada.IdCliente,
            IdEmpleadoRegistra = quien.IdEmpleado,
            DescripcionGeneral = entrada.DescripcionGeneral!.Trim(),
            FechaSolicitud = TiempoChile.Fecha(ahora),
            Estado = EstadosSolicitud.Aceptado,
            Detalles = entrada.Detalles!.Select(d => new SolicitudDetalle
            {
                IdProducto = d.IdProducto,
                // La columna es NOT NULL: sin descripcion del item se guarda vacia (el comprobante la muestra null).
                Descripcion = d.Descripcion?.Trim() ?? "",
                AnchoMm = d.AnchoMm,
                AltoMm = d.AltoMm,
                Cantidad = d.Cantidad
            }).ToList()
        };
        solicitud.Historial.Add(new SolicitudHistorial { EstadoNuevo = EstadosSolicitud.Aceptado, Motivo = nota, IdEmpleado = quien.IdEmpleado, Fecha = ahora });

        var cotizacion = ConsultasDeCotizacion.Nueva(0, quien.IdEmpleado, entrada.Cotizacion, ahora);
        cotizacion.Solicitud = solicitud;
        cotizacion.Estado = EstadosCotizacion.Aprobada;
        cotizacion.FechaRespuesta = ahora;
        cotizacion.ViaRespuesta = ViasRespuesta.Presencial;
        cotizacion.IdEmpleadoRespuesta = quien.IdEmpleado;

        var pedido = Nuevo(0, OrigenesPedido.Directo, montoBase, entrada.Datos, ahora);
        pedido.Solicitud = solicitud;
        pedido.Cotizacion = cotizacion;
        Historial(pedido, null, EstadosPedido.Aceptado, nota, quien, ahora);
        if (entrada.AbonoInicial is { } abono)
            db.Pagos.Add(NuevoPago(pedido, abono, quien, hoy, ahora));

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        db.Pedidos.Add(pedido);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (ErroresSql.EsRegla(e, ReglaMontoAbonado))
        {
            return new ResultadoPedido.Conflicto(CodigosConflicto.AbonoExcedeSaldo,
                MensajeExcedeSaldo(montoBase + (int)entrada.Datos.CostoInstalacionAdicional!.Value), DesdeLaBase: true);
        }
        await transaccion.CommitAsync(CancellationToken.None);
        return new ResultadoPedido.Correcto((await FichaAsync(pedido.IdPedido, quien, CancellationToken.None))!);
    }

    // El monto final (base + instalacion) cabe en el limite y el abono inicial no lo supera.
    private static ResultadoPedido? Montos(int montoBase, EntradaDatosPedido datos, EntradaAbono? abono)
    {
        if (ReglasPedido.ValidarMontoBase(montoBase, datos.CostoInstalacionAdicional) is { } error)
            return new ResultadoPedido.Invalido(new Dictionary<string, string[]> { ["costoInstalacionAdicional"] = [error] });
        var final = montoBase + (int)datos.CostoInstalacionAdicional!.Value;
        return abono is not null && abono.Monto > final
            ? new ResultadoPedido.Conflicto(CodigosConflicto.AbonoExcedeSaldo, MensajeExcedeSaldo(final))
            : null;
    }

    private static Pedido Nuevo(int idSolicitud, string origen, int montoBase, EntradaDatosPedido datos, DateTimeOffset ahora)
    {
        var modalidad = datos.ModalidadEntrega!;
        return new Pedido
        {
            IdSolicitud = idSolicitud,
            Origen = origen,
            IdEmpleadoResponsable = datos.IdEmpleadoResponsable,
            FechaPedido = TiempoChile.Fecha(ahora),
            FechaEntregaEstimada = datos.FechaEntregaEstimada!.Value.ToDateTime(TimeOnly.MinValue),
            ModalidadEntrega = modalidad,
            RequiereInstalacion = datos.RequiereInstalacion == true,
            DireccionInstalacion = ReglasPedido.DireccionAGuardar(modalidad, datos.DireccionInstalacion),
            CostoInstalacionAdicional = datos.CostoInstalacionAdicional!.Value,
            MontoBaseTrabajo = montoBase,
            EstadoPedido = EstadosPedido.Aceptado,
            NumeroFactura = ValidacionCotizacion.Limpiar(datos.NumeroFactura)
        };
    }

    private static void Historial(Pedido pedido, string? anterior, string nuevo, string? nota, PersonalQueOpera quien, DateTimeOffset ahora) =>
        pedido.Historial.Add(new PedidoHistorial
        {
            Pedido = pedido,
            EstadoAnterior = anterior,
            EstadoNuevo = nuevo,
            Nota = nota,
            IdEmpleado = quien.IdEmpleado,
            Fecha = ahora
        });

    private static Pago NuevoPago(Pedido pedido, EntradaAbono abono, PersonalQueOpera quien, DateOnly hoy, DateTimeOffset ahora) => new()
    {
        Pedido = pedido,
        Monto = (int)abono.Monto,
        FechaPago = abono.FechaPago ?? hoy,
        MedioPago = abono.MedioPago!,
        Observacion = ValidacionCotizacion.Limpiar(abono.Observacion),
        IdEmpleadoRegistra = quien.IdEmpleado,
        FechaRegistro = TiempoChile.Fecha(ahora)
    };

    // ── Editar y avanzar ──

    // PUT /pedidos/{id} con la version vista. Un pedido anulado no se edita; uno entregado, solo su numero de
    // factura (el resto se ignora). Cambiar los montos es de gestion y queda en el historial del pedido. El
    // monto final no puede quedar bajo lo ya abonado.
    public async Task<ResultadoPedido> EditarAsync(int id, PersonalQueOpera quien, EntradaDatosPedido datos, decimal? montoBaseTrabajo,
        byte[]? version, CancellationToken ct)
    {
        if (version is null or { Length: 0 })
            return new ResultadoPedido.SinVersion();
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.IdPedido == id && !p.EliminadoLogico, ct);
        if (pedido is null)
            return new ResultadoPedido.NoEncontrado();
        if (!version.AsSpan().SequenceEqual(pedido.RowVersion))
            return new ResultadoPedido.VersionObsoleta();
        if (pedido.EstadoPedido == EstadosPedido.Anulado)
            return new ResultadoPedido.Conflicto(CodigosConflicto.TransicionInvalida, "Un pedido anulado no se edita.");

        if (pedido.EstadoPedido == EstadosPedido.Entregado)
        {
            if (datos.NumeroFactura?.Trim().Length > ReglasPedido.MaxFactura)
                return new ResultadoPedido.Invalido(new Dictionary<string, string[]>
                {
                    ["numeroFactura"] = [$"El número de factura admite hasta {ReglasPedido.MaxFactura} caracteres."]
                });
            pedido.NumeroFactura = ValidacionCotizacion.Limpiar(datos.NumeroFactura);
        }
        else
        {
            var errores = ReglasPedido.ValidarDatos(datos, TiempoChile.Hoy(reloj), DateOnly.FromDateTime(pedido.FechaEntregaEstimada));
            await ValidarResponsableAsync(datos.IdEmpleadoResponsable, errores, ct);
            if ((montoBaseTrabajo is null || !errores.ContainsKey("costoInstalacionAdicional"))
                && ReglasPedido.ValidarMontoBase(montoBaseTrabajo, datos.CostoInstalacionAdicional) is { } error)
                errores["montoBaseTrabajo"] = [error];
            if (errores.Count > 0)
                return new ResultadoPedido.Invalido(errores);

            var montoBase = montoBaseTrabajo!.Value;
            var costo = datos.CostoInstalacionAdicional!.Value;
            var cambianMontos = montoBase != pedido.MontoBaseTrabajo || costo != pedido.CostoInstalacionAdicional;
            if (cambianMontos && !quien.EsGestion)
                return new ResultadoPedido.SoloGestion(MensajeMontosSoloGestion);
            if (montoBase + costo < pedido.MontoAbonado)
                return new ResultadoPedido.Conflicto(CodigosConflicto.MontoBajoAbonado, MensajeMontoBajoAbonado(pedido.MontoAbonado));
            // Todo cambio de montos queda en el historial (sin cambio de estado), con quien y cuando.
            if (cambianMontos)
                Historial(pedido, pedido.EstadoPedido, pedido.EstadoPedido,
                    NotaMontos(pedido.MontoBaseTrabajo, pedido.CostoInstalacionAdicional, montoBase, costo), quien, reloj.GetUtcNow());

            var modalidad = datos.ModalidadEntrega!;
            pedido.IdEmpleadoResponsable = datos.IdEmpleadoResponsable;
            pedido.FechaEntregaEstimada = datos.FechaEntregaEstimada!.Value.ToDateTime(TimeOnly.MinValue);
            pedido.ModalidadEntrega = modalidad;
            pedido.RequiereInstalacion = datos.RequiereInstalacion == true;
            pedido.DireccionInstalacion = ReglasPedido.DireccionAGuardar(modalidad, datos.DireccionInstalacion);
            pedido.CostoInstalacionAdicional = costo;
            pedido.MontoBaseTrabajo = montoBase;
            pedido.NumeroFactura = ValidacionCotizacion.Limpiar(datos.NumeroFactura);
        }

        // Toda edicion cambia la version, aunque no cambie ningun valor.
        db.Entry(pedido).Property(p => p.NumeroFactura).IsModified = true;
        return await GuardarAsync(pedido, version, quien, ct, alRechazarLaBase: () =>
            new ResultadoPedido.Conflicto(CodigosConflicto.MontoBajoAbonado, MensajeMontoBajoAbonado(pedido.MontoAbonado), DesdeLaBase: true));
    }

    // «Monto final: $120.000 → $100.000 (monto base $120.000 → $100.000).»
    private static string NotaMontos(decimal baseAntes, decimal instalacionAntes, decimal baseNueva, decimal instalacionNueva)
    {
        static string P(decimal monto) => FormatoChile.Pesos((long)monto);
        var detalle = new List<string>();
        if (baseAntes != baseNueva)
            detalle.Add($"monto base {P(baseAntes)} → {P(baseNueva)}");
        if (instalacionAntes != instalacionNueva)
            detalle.Add($"instalación {P(instalacionAntes)} → {P(instalacionNueva)}");
        return $"Monto final: {P(baseAntes + instalacionAntes)} → {P(baseNueva + instalacionNueva)} ({string.Join("; ", detalle)}).";
    }

    private static string MensajeMontoBajoAbonado(int abonado) =>
        $"El monto final no puede quedar bajo lo ya abonado ({FormatoChile.Pesos(abonado)}).";

    // PATCH /pedidos/{id}/estado con la version vista: avanza, retrocede un paso o anula. Cada cambio deja
    // una fila en el historial con quien y cuando. Anular un pedido con abonos vigentes es solo de gestion.
    public async Task<ResultadoPedido> CambiarEstadoAsync(int id, PersonalQueOpera quien, string? estado, DateOnly? fechaEntregaReal,
        string? motivo, string? nota, byte[]? version, CancellationToken ct)
    {
        if (!EstadosPedido.Existe(estado))
            return new ResultadoPedido.Invalido(new Dictionary<string, string[]>
            {
                ["estado"] = [$"Estado inválido. Valores: {string.Join(", ", EstadosPedido.Todos)}."]
            });
        if (version is null or { Length: 0 })
            return new ResultadoPedido.SinVersion();

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.IdPedido == id && !p.EliminadoLogico, ct);
        if (pedido is null)
            return new ResultadoPedido.NoEncontrado();
        if (!version.AsSpan().SequenceEqual(pedido.RowVersion))
            return new ResultadoPedido.VersionObsoleta();
        var desde = pedido.EstadoPedido;
        if (TransicionesPedido.Explicar(desde, estado!) is { } explicacion)
            return new ResultadoPedido.Conflicto(CodigosConflicto.TransicionInvalida, explicacion);

        var errores = new Dictionary<string, string[]>();
        var anula = estado == EstadosPedido.Anulado;
        if (anula)
        {
            if (!quien.EsGestion && ReglasPedido.AnularExigeGestion(await db.Pagos.AnyAsync(p => p.IdPedido == id, ct)))
                return new ResultadoPedido.SoloGestion(MensajeAnularConAbonos);
            if (ReglasPedido.ValidarMotivoAnulacion(motivo) is { } errorMotivo)
                errores["motivo"] = [errorMotivo];
        }
        if (ReglasPedido.ValidarNota(nota) is { } errorNota)
            errores["nota"] = [errorNota];
        var hoy = TiempoChile.Hoy(reloj);
        var real = fechaEntregaReal ?? hoy;
        if (estado == EstadosPedido.Entregado && ReglasPedido.ValidarFechaEntregaReal(real, hoy, DateOnly.FromDateTime(pedido.FechaPedido)) is { } errorFecha)
            errores["fechaEntregaReal"] = [errorFecha];
        if (errores.Count > 0)
            return new ResultadoPedido.Invalido(errores);

        var ahora = reloj.GetUtcNow();
        var motivoLimpio = anula ? motivo!.Trim() : null;
        Historial(pedido, desde, estado!, anula ? motivoLimpio : ValidacionCotizacion.Limpiar(nota), quien, ahora);
        pedido.EstadoPedido = estado!;
        if (estado == EstadosPedido.Entregado)
            pedido.FechaEntregaReal = real.ToDateTime(TimeOnly.MinValue);
        if (anula)
        {
            pedido.MotivoAnulacion = motivoLimpio;
            pedido.FechaAnulacion = ahora;
        }
        // Fase 11 (S09): el cliente recibe el aviso del estado nuevo, en el mismo guardado.
        var avisa = EstadosConAviso.Contains(estado!) && await avisosAlCliente.EstadoPedidoAsync(cola, id, estado!, motivoLimpio, ct);
        var resultado = await GuardarAsync(pedido, version, quien, ct);
        if (avisa && resultado is ResultadoPedido.Correcto)
            cola.AvisarAlTrabajador();
        return resultado;
    }

    private static readonly string[] EstadosConAviso = [EstadosPedido.EnProceso, EstadosPedido.Terminado, EstadosPedido.Entregado, EstadosPedido.Anulado];

    // La base compara con la version que vio quien edita: si otra persona guardo (o se registro un abono)
    // entre la lectura y este UPDATE, no se pisa su trabajo.
    private async Task<ResultadoPedido> GuardarAsync(Pedido pedido, byte[] version, PersonalQueOpera quien, CancellationToken ct,
        Func<ResultadoPedido>? alRechazarLaBase = null)
    {
        db.Entry(pedido).Property(p => p.RowVersion).OriginalValue = version;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoPedido.VersionObsoleta();
        }
        catch (DbUpdateException e) when (alRechazarLaBase is not null && ErroresSql.EsRegla(e, ReglaMontoAbonado))
        {
            return alRechazarLaBase();
        }
        // Ya guardado: la respuesta se arma aunque se cancele la peticion.
        return new ResultadoPedido.Correcto((await FichaAsync(pedido.IdPedido, quien, CancellationToken.None))!);
    }

    // ── Abonos (E25) ──

    // POST /pedidos/{id}/pagos, sin version: solo suma, y la base protege el tope. En una transaccion que
    // primero bloquea la fila del pedido, asi dos abonos simultaneos se registran uno tras otro y el segundo
    // ve el saldo que dejo el primero.
    public async Task<ResultadoPedido> RegistrarAbonoAsync(int id, PersonalQueOpera quien, EntradaAbono abono, CancellationToken ct)
    {
        var hoy = TiempoChile.Hoy(reloj);
        var errores = ReglasPedido.ValidarAbono(abono, hoy);
        if (errores.Count > 0)
            return new ResultadoPedido.Invalido(errores);

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var bloqueado = await BloquearAsync(id, ct);
        if (bloqueado is null)
            return new ResultadoPedido.NoEncontrado();
        if (bloqueado.EstadoPedido == EstadosPedido.Anulado)
            return new ResultadoPedido.Conflicto(CodigosConflicto.PedidoAnulado, MensajePedidoAnulado);
        if (abono.Monto > bloqueado.SaldoPendiente)
            return new ResultadoPedido.Conflicto(CodigosConflicto.AbonoExcedeSaldo, MensajeExcedeSaldo(bloqueado.SaldoPendiente));
        // La factura del abono solo completa la del pedido: si ya tiene otra, se cambia editando el pedido.
        var factura = ValidacionCotizacion.Limpiar(abono.NumeroFactura);
        if (factura is not null && bloqueado.NumeroFactura is { } actual && !string.Equals(actual, factura, StringComparison.OrdinalIgnoreCase))
            return new ResultadoPedido.Conflicto(CodigosConflicto.FacturaDistinta, $"El pedido ya tiene la factura {actual}; cámbiala editando el pedido.");

        var ahora = reloj.GetUtcNow();
        db.Pagos.Add(new Pago
        {
            IdPedido = id,
            Monto = (int)abono.Monto,
            FechaPago = abono.FechaPago ?? hoy,
            MedioPago = abono.MedioPago!,
            Observacion = ValidacionCotizacion.Limpiar(abono.Observacion),
            IdEmpleadoRegistra = quien.IdEmpleado,
            FechaRegistro = TiempoChile.Fecha(ahora)
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (ErroresSql.EsRegla(e, ReglaMontoAbonado))
        {
            // Si de todos modos llega a la base, ck_pedidos_monto_abonado lo frena: el mismo 409, con el saldo
            // vigente. El error nace en el trigger, que ya revirtio la transaccion: se suelta antes de leer.
            await transaccion.DisposeAsync();
            var saldo = await db.Pedidos.AsNoTracking().Where(p => p.IdPedido == id).Select(p => p.SaldoPendiente).FirstOrDefaultAsync(CancellationToken.None);
            return new ResultadoPedido.Conflicto(CodigosConflicto.AbonoExcedeSaldo, MensajeExcedeSaldo(saldo), DesdeLaBase: true);
        }
        if (factura is not null && bloqueado.NumeroFactura is null)
        {
            await db.Pedidos.Where(p => p.IdPedido == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.NumeroFactura, factura), ct);
            await auditoria.RegistrarAsync("pedido", id.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.Modificar,
                DetalleAuditoria.Libre(new Dictionary<string, object?> { ["campos"] = new[] { "numeroFactura" } }), ct);
        }
        await transaccion.CommitAsync(CancellationToken.None);
        return new ResultadoPedido.Correcto((await FichaAsync(id, quien, CancellationToken.None))!);
    }

    // Decision 2 del 14-11-2026: un abono no se edita ni se borra; se anula con motivo y deja de sumar (lo
    // resta el trigger). Gestion anula cualquiera; el resto del personal, los propios de las ultimas 24 horas.
    public async Task<ResultadoPedido> AnularAbonoAsync(int id, int idPago, PersonalQueOpera quien, string? motivo, CancellationToken ct)
    {
        if (ReglasPedido.ValidarMotivoAnulacionAbono(motivo) is { } error)
            return new ResultadoPedido.Invalido(new Dictionary<string, string[]> { ["motivo"] = [error] });

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        if (await BloquearAsync(id, ct) is null)
            return new ResultadoPedido.NoEncontrado();
        var pago = await db.Pagos.FirstOrDefaultAsync(p => p.IdPago == idPago && p.IdPedido == id, ct);
        if (pago is null)
            return new ResultadoPedido.NoEncontrado();
        if (pago.Anulado)
            return new ResultadoPedido.Conflicto(CodigosConflicto.AbonoAnulado, "Este abono ya está anulado. Un abono anulado no se puede reactivar.");
        if (!ReglasPedido.PuedeAnularAbono(quien.EsGestion, quien.IdEmpleado, pago.IdEmpleadoRegistra, pago.FechaRegistro, HoraChile.Convertir(reloj.GetUtcNow())))
            return new ResultadoPedido.SoloGestion(MensajeAnularAbonoAjeno);

        pago.Anulado = true;
        pago.MotivoAnulacion = motivo!.Trim();
        pago.IdEmpleadoAnula = quien.IdEmpleado;
        pago.FechaAnulacion = reloj.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(CancellationToken.None);
        return new ResultadoPedido.Correcto((await FichaAsync(id, quien, CancellationToken.None))!);
    }

    // Bloquea la fila del pedido hasta el fin de la transaccion (UPDLOCK): otra operacion que quiera
    // bloquearla espera su turno.
    private async Task<PedidoBloqueado?> BloquearAsync(int id, CancellationToken ct) =>
        (await db.Database.SqlQuery<PedidoBloqueado>($"""
            SELECT estado_pedido AS EstadoPedido, saldo_pendiente AS SaldoPendiente, numero_factura AS NumeroFactura
            FROM dbo.pedidos WITH (UPDLOCK, ROWLOCK)
            WHERE id_pedido = {id} AND eliminado_logico = 0
            """).ToListAsync(ct)).SingleOrDefault();

    // ── Eliminar (gestion) ──

    // Borrado logico, solo de un pedido «anulado» sin abonos vigentes y con la version vista. Todo se
    // comprueba en el mismo UPDATE: un abono registrado justo antes no se cuela entre la revision y el borrado.
    public async Task<ResultadoPedido> EliminarAsync(int id, byte[]? version, CancellationToken ct)
    {
        if (version is null or { Length: 0 })
            return new ResultadoPedido.SinVersion();

        var fecha = reloj.GetUtcNow();
        // El borrado y su registro en la bitacora van juntos (el interceptor no ve un ExecuteUpdate).
        await using (var transaccion = await db.Database.BeginTransactionAsync(ct))
        {
            var eliminados = await db.Pedidos
                .Where(p => p.IdPedido == id && !p.EliminadoLogico && p.EstadoPedido == EstadosPedido.Anulado && p.MontoAbonado == 0 && p.RowVersion == version)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.EliminadoLogico, true)
                    .SetProperty(p => p.FechaEliminacionLogica, (DateTimeOffset?)fecha), ct);
            if (eliminados == 1)
            {
                await auditoria.RegistrarAsync("pedido", id.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.Eliminar,
                    DetalleAuditoria.Libre(new Dictionary<string, object?> { ["borradoLogico"] = true }), ct);
                await transaccion.CommitAsync(CancellationToken.None);
                return new ResultadoPedido.Eliminado();
            }
        }

        // No se borro: se explica por que, sin haber cambiado nada.
        var actual = await db.Pedidos.AsNoTracking().Where(p => p.IdPedido == id && !p.EliminadoLogico)
            .Select(p => new { p.EstadoPedido, p.MontoAbonado, p.RowVersion }).FirstOrDefaultAsync(ct);
        if (actual is null)
            return new ResultadoPedido.NoEncontrado();
        if (!actual.RowVersion.AsSpan().SequenceEqual(version))
            return new ResultadoPedido.VersionObsoleta();
        if (actual.EstadoPedido != EstadosPedido.Anulado)
            return new ResultadoPedido.Conflicto(CodigosConflicto.TransicionInvalida, "Solo se elimina un pedido «anulado». Anúlalo primero.");
        if (actual.MontoAbonado > 0)
            return new ResultadoPedido.Conflicto(CodigosConflicto.TieneAbonos, "El pedido tiene abonos vigentes: anúlalos antes de eliminarlo.");
        return new ResultadoPedido.VersionObsoleta();
    }

    // ── Leer ──

    // La ficha (S07). Lo que se ofrece (transiciones y anular abonos) depende de quien la mira.
    public async Task<FichaPedido?> FichaAsync(int id, PersonalQueOpera quien, CancellationToken ct)
    {
        var fila = await db.Pedidos.AsNoTracking()
            .Where(p => p.IdPedido == id && !p.EliminadoLogico)
            .Select(p => new
            {
                p.IdPedido,
                p.Origen,
                p.IdSolicitud,
                p.IdCotizacion,
                p.FechaPedido,
                p.FechaEntregaEstimada,
                p.FechaEntregaReal,
                p.ModalidadEntrega,
                p.RequiereInstalacion,
                p.DireccionInstalacion,
                p.MontoBaseTrabajo,
                p.CostoInstalacionAdicional,
                p.MontoTotalFinal,
                p.EstadoPedido,
                p.EstadoPago,
                p.MontoAbonado,
                p.SaldoPendiente,
                p.NumeroFactura,
                p.MotivoAnulacion,
                p.RowVersion,
                Cliente = new DatosCliente(p.Solicitud!.IdCliente, p.Solicitud.Cliente!.Nombre, p.Solicitud.Cliente.Apellido,
                    p.Solicitud.Cliente.Telefono, p.Solicitud.Cliente.Email),
                p.IdEmpleadoResponsable,
                Responsable = p.EmpleadoResponsable!.Nombre + " " + p.EmpleadoResponsable.Apellido,
                Historial = p.Historial.OrderBy(h => h.Fecha).ThenBy(h => h.IdHistorial)
                    .Select(h => new { h.Fecha, h.EstadoAnterior, h.EstadoNuevo, h.Nota, Empleado = h.Empleado!.Nombre + " " + h.Empleado.Apellido })
                    .ToList(),
                Pagos = p.Pagos.OrderBy(x => x.FechaPago).ThenBy(x => x.IdPago)
                    .Select(x => new
                    {
                        x.IdPago,
                        x.Monto,
                        x.FechaPago,
                        x.MedioPago,
                        x.Observacion,
                        x.IdEmpleadoRegistra,
                        RegistradoPor = x.EmpleadoRegistra!.Nombre + " " + x.EmpleadoRegistra.Apellido,
                        x.FechaRegistro,
                        x.Anulado,
                        x.MotivoAnulacion,
                        AnuladoPor = x.EmpleadoAnula == null ? null : x.EmpleadoAnula.Nombre + " " + x.EmpleadoAnula.Apellido,
                        x.FechaAnulacion
                    })
                    .ToList()
            })
            .AsSplitQuery()
            .SingleOrDefaultAsync(ct);
        if (fila is null)
            return null;

        var ahoraChile = HoraChile.Convertir(reloj.GetUtcNow());
        var posibles = TransicionesPedido.Posibles(fila.EstadoPedido)
            .Where(t => t != EstadosPedido.Anulado || quien.EsGestion || !ReglasPedido.AnularExigeGestion(fila.Pagos.Count > 0))
            .ToList();
        var historial = fila.Historial.Select(h => new CambioDePedido(HoraChile.Convertir(h.Fecha), h.EstadoAnterior, h.EstadoNuevo, h.Nota, h.Empleado)).ToList();
        var pagos = fila.Pagos.Select(x => new AbonoDelPedido(x.IdPago, x.Monto, x.FechaPago, x.MedioPago, x.Observacion, x.RegistradoPor,
            x.FechaRegistro, x.Anulado, x.MotivoAnulacion, x.AnuladoPor, x.FechaAnulacion is { } f ? HoraChile.Convertir(f) : null,
            !x.Anulado && ReglasPedido.PuedeAnularAbono(quien.EsGestion, quien.IdEmpleado, x.IdEmpleadoRegistra, x.FechaRegistro, ahoraChile))).ToList();

        return new FichaPedido(fila.IdPedido, ReglasPedido.Numero(fila.IdPedido), fila.Origen,
            new ReferenciaSolicitud(fila.IdSolicitud, MedidasSolicitud.Numero(fila.IdSolicitud)),
            fila.IdCotizacion is int idCotizacion ? new ReferenciaCotizacion(idCotizacion, ValidacionCotizacion.Numero(idCotizacion)) : null,
            fila.Cliente, new PersonaDelTaller(fila.IdEmpleadoResponsable, fila.Responsable), fila.FechaPedido,
            DateOnly.FromDateTime(fila.FechaEntregaEstimada), fila.FechaEntregaReal is { } real ? DateOnly.FromDateTime(real) : null,
            fila.ModalidadEntrega, fila.RequiereInstalacion, fila.DireccionInstalacion, (int)fila.MontoBaseTrabajo,
            (int)fila.CostoInstalacionAdicional, (int)fila.MontoTotalFinal, fila.EstadoPedido, fila.EstadoPago, fila.MontoAbonado,
            fila.SaldoPendiente, fila.NumeroFactura, ReglasPedido.CicloCerrado(fila.EstadoPedido, fila.EstadoPago), fila.MotivoAnulacion,
            // Decision 4: lo abonado de un pedido anulado queda «a devolver o a favor del cliente».
            fila.EstadoPedido == EstadosPedido.Anulado ? fila.MontoAbonado : 0,
            posibles, historial, pagos, fila.RowVersion);
    }

    // Listado del personal (RFU36, S24): filtros por estado del pedido y de pago, cobranza, año y mes del
    // pedido, cliente y responsable; busqueda, orden y paginacion. El resumen de cobranza cubre todo el
    // filtro, no solo la pagina. Sin los eliminados.
    public async Task<ListadoPedidos> ListarAsync(FiltroPedidos filtro, ParametrosListado parametros, CancellationToken ct)
    {
        var consulta = db.Pedidos.AsNoTracking().Where(p => !p.EliminadoLogico);

        if (EstadosPedido.Existe(filtro.EstadoPedido))
            consulta = consulta.Where(p => p.EstadoPedido == filtro.EstadoPedido);
        if (EstadosPago.Existe(filtro.EstadoPago))
            consulta = consulta.Where(p => p.EstadoPago == filtro.EstadoPago);
        if (filtro.Cobranza)
            consulta = PorCobrar(consulta);
        if (filtro.Anio is int anio)
        {
            var inicio = new DateTime(anio, filtro.Mes ?? 1, 1);
            var fin = filtro.Mes is null ? inicio.AddYears(1) : inicio.AddMonths(1);
            consulta = consulta.Where(p => p.FechaPedido >= inicio && p.FechaPedido < fin);
        }
        // Con año es un rango de fechas (usa ix_pedidos_fecha_estado). Un mes sin año (ese mes de cualquier año)
        // no cabe en un rango: compara el mes y recorre los pedidos.
        else if (filtro.Mes is int mes)
            consulta = consulta.Where(p => p.FechaPedido.Month == mes);
        if (filtro.IdCliente is int idCliente)
            consulta = consulta.Where(p => p.Solicitud!.IdCliente == idCliente);
        if (filtro.IdResponsable is int idResponsable)
            consulta = consulta.Where(p => p.IdEmpleadoResponsable == idResponsable);

        if (parametros.Buscar is { } buscar)
        {
            var numero = int.TryParse(buscar.TrimStart('#').Trim(), out var n) ? n : -1;
            var digitos = new string(buscar.Where(char.IsDigit).ToArray());
            var rut = buscar.Replace(".", "").Trim().ToUpperInvariant();
            consulta = consulta.Where(p => p.IdPedido == numero
                || (p.Solicitud!.Cliente!.Nombre + " " + p.Solicitud.Cliente.Apellido).Contains(buscar)
                || (digitos.Length >= 3 && p.Solicitud.Cliente.Telefono.Contains(digitos))
                || (p.Solicitud.Cliente.Rut != null && p.Solicitud.Cliente.Rut.Contains(rut)));
        }

        var resumen = await ResumenAsync(consulta, ct);
        var ordenada = filtro.Orden switch
        {
            "fecha_asc" => consulta.OrderBy(p => p.FechaPedido).ThenBy(p => p.IdPedido),
            "entrega_asc" => consulta.OrderBy(p => p.FechaEntregaEstimada).ThenBy(p => p.IdPedido),
            "saldo_desc" => consulta.OrderByDescending(p => p.SaldoPendiente).ThenByDescending(p => p.IdPedido),
            _ => consulta.OrderByDescending(p => p.FechaPedido).ThenByDescending(p => p.IdPedido)
        };

        var pagina = await ordenada
            .Select(p => new
            {
                p.IdPedido,
                p.Origen,
                p.FechaPedido,
                Cliente = new ClienteFila(p.Solicitud!.IdCliente, p.Solicitud.Cliente!.Nombre, p.Solicitud.Cliente.Apellido, p.Solicitud.Cliente.Telefono),
                p.IdEmpleadoResponsable,
                Responsable = p.EmpleadoResponsable!.Nombre + " " + p.EmpleadoResponsable.Apellido,
                p.FechaEntregaEstimada,
                p.FechaEntregaReal,
                p.ModalidadEntrega,
                p.EstadoPedido,
                p.EstadoPago,
                p.MontoTotalFinal,
                p.MontoAbonado,
                p.SaldoPendiente
            })
            .PaginarAsync(parametros, ct);

        var filas = pagina.Elementos.Select(p => new FilaPedido(p.IdPedido, ReglasPedido.Numero(p.IdPedido), p.Origen, p.FechaPedido, p.Cliente,
            new PersonaDelTaller(p.IdEmpleadoResponsable, p.Responsable), DateOnly.FromDateTime(p.FechaEntregaEstimada),
            p.FechaEntregaReal is { } real ? DateOnly.FromDateTime(real) : null, p.ModalidadEntrega,
            p.EstadoPedido, p.EstadoPago, (int)p.MontoTotalFinal, p.MontoAbonado, p.SaldoPendiente,
            ReglasPedido.CicloCerrado(p.EstadoPedido, p.EstadoPago))).ToList();
        return new ListadoPedidos(filas, pagina.Total, pagina.Pagina, pagina.Tamano, resumen);
    }

    // Por cobrar: no anulado, con pago «no_pagado» o «abonado».
    private static IQueryable<Pedido> PorCobrar(IQueryable<Pedido> consulta) =>
        consulta.Where(p => p.EstadoPedido != EstadosPedido.Anulado
            && (p.EstadoPago == EstadosPago.NoPagado || p.EstadoPago == EstadosPago.Abonado));

    private static async Task<ResumenCobranza> ResumenAsync(IQueryable<Pedido> consulta, CancellationToken ct)
    {
        var porCobrar = PorCobrar(consulta);
        var total = await porCobrar.SumAsync(p => (long)p.SaldoPendiente, ct);
        var cantidad = await porCobrar.CountAsync(ct);
        return new ResumenCobranza(total, cantidad);
    }

    // Calendario de entregas (S08): pedidos no anulados ni eliminados. Los pendientes en su fecha estimada y
    // los entregados en su fecha real. El rango lo valida ReglasPedido.ValidarRango (como mucho 62 dias).
    public async Task<IReadOnlyList<EntregaCalendario>> CalendarioAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var inicio = desde.ToDateTime(TimeOnly.MinValue);
        var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var filas = await Entregas(db.Pedidos.AsNoTracking().Where(p => !p.EliminadoLogico && p.EstadoPedido != EstadosPedido.Anulado
                && ((p.EstadoPedido != EstadosPedido.Entregado && p.FechaEntregaEstimada >= inicio && p.FechaEntregaEstimada < fin)
                    || (p.EstadoPedido == EstadosPedido.Entregado && p.FechaEntregaReal >= inicio && p.FechaEntregaReal < fin))))
            .ToListAsync(ct);
        var hoy = TiempoChile.Hoy(reloj);
        return filas.Select(f => f.Entrega(hoy)).OrderBy(e => e.Fecha).ThenBy(e => e.IdPedido).ToList();
    }

    // Alertas para gestion: entregas de hoy a hoy + 3, las atrasadas (como mucho 20 cada una) y el total por
    // cobrar de todos los pedidos.
    public async Task<AlertasPedidos> AlertasAsync(CancellationToken ct)
    {
        const int maximo = 20;
        var hoy = TiempoChile.Hoy(reloj);
        var inicio = hoy.ToDateTime(TimeOnly.MinValue);
        var fin = hoy.AddDays(4).ToDateTime(TimeOnly.MinValue);
        var pendientes = db.Pedidos.AsNoTracking()
            .Where(p => !p.EliminadoLogico && p.EstadoPedido != EstadosPedido.Anulado && p.EstadoPedido != EstadosPedido.Entregado);

        var proximas = await Entregas(pendientes.Where(p => p.FechaEntregaEstimada >= inicio && p.FechaEntregaEstimada < fin)
            .OrderBy(p => p.FechaEntregaEstimada).ThenBy(p => p.IdPedido).Take(maximo)).ToListAsync(ct);
        var atrasadas = await Entregas(pendientes.Where(p => p.FechaEntregaEstimada < inicio)
            .OrderBy(p => p.FechaEntregaEstimada).ThenBy(p => p.IdPedido).Take(maximo)).ToListAsync(ct);
        var resumen = await ResumenAsync(db.Pedidos.AsNoTracking().Where(p => !p.EliminadoLogico), ct);

        return new AlertasPedidos(proximas.Select(f => f.Entrega(hoy)).ToList(), atrasadas.Select(f => f.Entrega(hoy)).ToList(),
            resumen.TotalPorCobrar, resumen.CantidadPorCobrar);
    }

    private sealed record FilaEntrega(int IdPedido, DateTime FechaEntregaEstimada, DateTime? FechaEntregaReal, ClienteFila Cliente,
        string ModalidadEntrega, string EstadoPedido, string EstadoPago)
    {
        public EntregaCalendario Entrega(DateOnly hoy)
        {
            var entregado = EstadoPedido == EstadosPedido.Entregado;
            var estimada = DateOnly.FromDateTime(FechaEntregaEstimada);
            var fecha = entregado && FechaEntregaReal is { } real ? DateOnly.FromDateTime(real) : estimada;
            return new EntregaCalendario(IdPedido, ReglasPedido.Numero(IdPedido), fecha, Cliente, ModalidadEntrega, EstadoPedido, EstadoPago,
                Atrasado: !entregado && estimada < hoy);
        }
    }

    private static IQueryable<FilaEntrega> Entregas(IQueryable<Pedido> consulta) =>
        consulta.Select(p => new FilaEntrega(p.IdPedido, p.FechaEntregaEstimada, p.FechaEntregaReal,
            new ClienteFila(p.Solicitud!.IdCliente, p.Solicitud.Cliente!.Nombre, p.Solicitud.Cliente.Apellido, p.Solicitud.Cliente.Telefono),
            p.ModalidadEntrega, p.EstadoPedido, p.EstadoPago));

    // «Mis pedidos» (S04): los del cliente del token, del mas reciente al mas antiguo. Un pedido eliminado (anulado
    // y borrado de forma logica) se sigue mostrando, marcado, para que el cliente sepa que se anulo.
    public async Task<IReadOnlyList<MiPedido>> DelClienteAsync(int idCliente, CancellationToken ct) =>
        (await db.Pedidos.AsNoTracking()
            .Where(p => p.Solicitud!.IdCliente == idCliente)
            .OrderByDescending(p => p.FechaPedido).ThenByDescending(p => p.IdPedido)
            .Select(p => new { p.IdPedido, p.IdSolicitud, p.EstadoPedido, p.EstadoPago, p.SaldoPendiente, p.FechaEntregaEstimada, p.ModalidadEntrega, p.EliminadoLogico })
            .ToListAsync(ct))
        .Select(p => new MiPedido(p.IdPedido, p.IdSolicitud, ReglasPedido.Numero(p.IdPedido), p.EstadoPedido, p.EstadoPago, p.SaldoPendiente,
            DateOnly.FromDateTime(p.FechaEntregaEstimada), p.ModalidadEntrega, ReglasPedido.CicloCerrado(p.EstadoPedido, p.EstadoPago), p.EliminadoLogico))
        .ToList();

    // ── Apoyo ──

    private async Task ValidarResponsableAsync(int idEmpleado, Dictionary<string, string[]> errores, CancellationToken ct)
    {
        if (!errores.ContainsKey("idEmpleadoResponsable") && !await db.Empleados.AnyAsync(e => e.IdEmpleado == idEmpleado && e.Activo, ct))
            errores["idEmpleadoResponsable"] = ["La persona responsable no existe o está desactivada."];
    }

    // Los productos de los items deben estar activos (y su categoria tambien), igual que en una solicitud.
    private async Task ValidarProductosAsync(IReadOnlyList<EntradaDetalle>? detalles, Dictionary<string, string[]> errores, CancellationToken ct)
    {
        if (detalles is not { Count: > 0 and <= ValidacionSolicitud.MaxDetalles })
            return;
        var pedidos = detalles.Select(d => d.IdProducto).Distinct().ToList();
        var disponibles = await db.Productos.AsNoTracking()
            .Where(p => pedidos.Contains(p.IdProducto) && p.Activo && p.Categoria!.Activo)
            .Select(p => p.IdProducto)
            .ToListAsync(ct);
        for (var i = 0; i < detalles.Count; i++)
        {
            var campo = $"detalles[{i}].idProducto";
            if (!errores.ContainsKey(campo) && !disponibles.Contains(detalles[i].IdProducto))
                errores[campo] = [ValidacionSolicitud.MensajeProducto];
        }
    }

    // Los errores del abono inicial van con su prefijo (abonoInicial.monto...), como los ve el formulario.
    private static void ValidarAbonoInicial(EntradaAbono? abono, DateOnly hoy, Dictionary<string, string[]> errores)
    {
        if (abono is null)
            return;
        foreach (var (campo, mensajes) in ReglasPedido.ValidarAbono(abono, hoy))
            errores[$"abonoInicial.{campo}"] = mensajes;
    }

    private static void Unir(Dictionary<string, string[]> a, Dictionary<string, string[]> b)
    {
        foreach (var (clave, valor) in b)
            a[clave] = valor;
    }
}

// La fila del pedido que lee el bloqueo (SqlQuery: las columnas llegan por nombre).
internal sealed class PedidoBloqueado
{
    public string EstadoPedido { get; set; } = "";
    public int SaldoPendiente { get; set; }
    public string? NumeroFactura { get; set; }
}
