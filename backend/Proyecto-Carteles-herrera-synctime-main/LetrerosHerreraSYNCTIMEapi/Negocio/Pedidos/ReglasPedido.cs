using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Pedidos;

// ── Fase 8: reglas de los pedidos y sus abonos (RFS06, RFU13-RFU15, RFU35, RFU36, E08, E09, E25) ──
// Sin HTTP ni base de datos: se prueban solas en LetrerosHerreraSYNCTIMEapi.UnitTests.
// El estado del pedido y el estado de pago son independientes: el de pago lo calcula la base con la suma de
// los abonos vigentes y nunca se escribe a mano.

public static class EstadosPedido
{
    public const string Aceptado = "aceptado";
    public const string EnProceso = "en_proceso";
    public const string Terminado = "terminado";
    public const string Entregado = "entregado";
    // «anulado» y no «cancelado»: en Chile «cancelado» se entiende como «pagado».
    public const string Anulado = "anulado";

    public static readonly string[] Todos = [Aceptado, EnProceso, Terminado, Entregado, Anulado];

    public static bool Existe(string? estado) => estado is not null && Todos.Contains(estado);
}

public static class EstadosPago
{
    public const string NoPagado = "no_pagado";
    public const string Abonado = "abonado";
    public const string PagadoTotal = "pagado_total";

    public static readonly string[] Todos = [NoPagado, Abonado, PagadoTotal];

    public static bool Existe(string? estado) => estado is not null && Todos.Contains(estado);
}

public static class ModalidadesEntrega
{
    public const string RetiroLocal = "retiro_local";
    public const string EntregaDomicilio = "entrega_domicilio";
    public const string InstalacionTerreno = "instalacion_terreno";

    public static readonly string[] Todas = [RetiroLocal, EntregaDomicilio, InstalacionTerreno];
}

public static class MediosPago
{
    public static readonly string[] Todos = ["efectivo", "transferencia", "tarjeta_debito", "tarjeta_credito", "cheque", "otro"];
}

public static class OrigenesPedido
{
    public const string Cotizacion = "cotizacion";
    public const string Directo = "directo";
}

public static class TransicionesPedido
{
    // En orden: primero el avance, despues la vuelta atras (por si alguien se equivoco) y al final anular.
    private static readonly (string Desde, string Hacia)[] Permitidas =
    [
        (EstadosPedido.Aceptado, EstadosPedido.EnProceso),
        (EstadosPedido.EnProceso, EstadosPedido.Terminado),
        (EstadosPedido.Terminado, EstadosPedido.Entregado),
        (EstadosPedido.EnProceso, EstadosPedido.Aceptado),
        (EstadosPedido.Terminado, EstadosPedido.EnProceso),
        (EstadosPedido.Aceptado, EstadosPedido.Anulado),
        (EstadosPedido.EnProceso, EstadosPedido.Anulado),
        (EstadosPedido.Terminado, EstadosPedido.Anulado)
    ];

    public static bool EsValida(string desde, string hacia) => Permitidas.Any(t => t.Desde == desde && t.Hacia == hacia);

    public static IReadOnlyList<string> Posibles(string desde) =>
        Permitidas.Where(t => t.Desde == desde).Select(t => t.Hacia).ToList();

    // null si la transicion es valida; si no, la explicacion para el 409.
    public static string? Explicar(string desde, string hacia)
    {
        if (EsValida(desde, hacia))
            return null;
        if (desde == hacia)
            return $"El pedido ya está «{desde}».";
        if (desde is EstadosPedido.Entregado or EstadosPedido.Anulado)
            return $"Un pedido «{desde}» ya no cambia de estado.";
        if (hacia == EstadosPedido.Entregado)
            return "Solo un pedido «terminado» se puede entregar.";
        var posibles = Posibles(desde);
        return $"Un pedido «{desde}» no puede pasar a «{hacia}». Puede pasar a: {string.Join(", ", posibles.Select(p => $"«{p}»"))}.";
    }
}

// Datos del pedido (E08), ya separados del formato de la peticion.
public sealed record EntradaDatosPedido(
    int IdEmpleadoResponsable,
    DateOnly? FechaEntregaEstimada,
    string? ModalidadEntrega,
    bool? RequiereInstalacion,
    string? DireccionInstalacion,
    decimal? CostoInstalacionAdicional,
    string? NumeroFactura);

// Un abono (E25).
public sealed record EntradaAbono(decimal Monto, DateOnly? FechaPago, string? MedioPago, string? Observacion, string? NumeroFactura);

public static class ReglasPedido
{
    public const int MaxAniosEntrega = 2;
    public const int MaxDireccion = 300;
    public const int MaxFactura = 30;
    public const int MinMotivo = 10;
    public const int MaxMotivo = 500;
    public const int MaxNota = 500;
    public const int MaxObservacionAbono = 300;
    public const int MinMotivoAnulacionAbono = 10;
    public const int MaxMotivoAnulacionAbono = 300;
    public const int DiasAtrasFechaPago = 365;
    public const int HorasParaAnularAbonoPropio = 24;
    public const decimal MaxMontoFinal = 2_000_000_000;

    // El ciclo se cierra cuando el pedido esta entregado y pagado, en cualquier orden. Se calcula, no se guarda.
    public static bool CicloCerrado(string estadoPedido, string estadoPago) =>
        estadoPedido == EstadosPedido.Entregado && estadoPago == EstadosPago.PagadoTotal;

    // Decision 4 del 14-11-2026: un pedido con abonos solo lo anula gestion. Cuenta cualquier abono, vigente
    // o anulado: asi nadie sin ser de gestion puede registrar un abono, anularlo y despues anular el pedido.
    public static bool AnularExigeGestion(bool tieneAbonos) => tieneAbonos;

    // Errores por campo de los datos del pedido. Al editar, la fecha estimada que ya tenia se acepta aunque
    // ya haya pasado (el pedido puede estar atrasado).
    public static Dictionary<string, string[]> ValidarDatos(EntradaDatosPedido datos, DateOnly hoy, DateOnly? fechaEstimadaActual = null)
    {
        var errores = new Dictionary<string, string[]>();
        if (datos.IdEmpleadoResponsable <= 0)
            errores["idEmpleadoResponsable"] = ["Elige a la persona responsable del pedido."];

        if (datos.FechaEntregaEstimada is not DateOnly fecha)
            errores["fechaEntregaEstimada"] = ["Indica la fecha estimada de entrega."];
        else if (fecha != fechaEstimadaActual && (fecha < hoy || fecha > hoy.AddYears(MaxAniosEntrega)))
            errores["fechaEntregaEstimada"] = [$"La fecha estimada de entrega debe ser desde hoy y como mucho {MaxAniosEntrega} años adelante."];

        var modalidad = datos.ModalidadEntrega;
        if (modalidad is null || !ModalidadesEntrega.Todas.Contains(modalidad))
            errores["modalidadEntrega"] = [$"Modalidad inválida. Valores: {string.Join(", ", ModalidadesEntrega.Todas)}."];
        else
        {
            if (modalidad == ModalidadesEntrega.InstalacionTerreno && datos.RequiereInstalacion != true)
                errores["requiereInstalacion"] = ["Una instalación en terreno requiere instalación: márcala."];
            var direccion = datos.DireccionInstalacion?.Trim();
            if (modalidad != ModalidadesEntrega.RetiroLocal && string.IsNullOrEmpty(direccion))
                errores["direccionInstalacion"] = ["Indica la dirección de entrega o de instalación."];
            else if (modalidad != ModalidadesEntrega.RetiroLocal && direccion!.Length > MaxDireccion)
                errores["direccionInstalacion"] = [$"La dirección admite hasta {MaxDireccion} caracteres."];
        }

        if (datos.CostoInstalacionAdicional is not decimal costo)
            errores["costoInstalacionAdicional"] = ["Indica el costo de instalación ($0 si no hay)."];
        else if (!Reglas.EsMontoEnPesos(costo) || costo > MaxMontoFinal)
            errores["costoInstalacionAdicional"] = ["El costo de instalación va en pesos, sin decimales, desde $0."];
        else if (datos.RequiereInstalacion != true && costo != 0)
            errores["costoInstalacionAdicional"] = ["Sin instalación, el costo de instalación es $0."];

        if (datos.NumeroFactura?.Trim().Length > MaxFactura)
            errores["numeroFactura"] = [$"El número de factura admite hasta {MaxFactura} caracteres."];
        return errores;
    }

    // Monto base del pedido (al editar): pesos enteros, no negativos, y con la instalacion hasta 2.000 millones.
    public static string? ValidarMontoBase(decimal? monto, decimal? costoInstalacion)
    {
        if (monto is not decimal montoBase)
            return "Indica el monto base del trabajo.";
        if (!Reglas.EsMontoEnPesos(montoBase) || montoBase > MaxMontoFinal)
            return "El monto base va en pesos, sin decimales, desde $0.";
        return montoBase + (costoInstalacion ?? 0) > MaxMontoFinal
            ? $"El monto final no puede superar {FormatoChile.Pesos((long)MaxMontoFinal)}."
            : null;
    }

    // Con retiro en el local no hay direccion que guardar.
    public static string? DireccionAGuardar(string modalidad, string? direccion) =>
        modalidad == ModalidadesEntrega.RetiroLocal || string.IsNullOrWhiteSpace(direccion) ? null : direccion.Trim();

    public static string? ValidarFechaEntregaReal(DateOnly fecha, DateOnly hoy, DateOnly fechaPedido)
    {
        if (fecha > hoy)
            return "La fecha de entrega no puede ser futura.";
        return fecha < fechaPedido ? "La fecha de entrega no puede ser anterior a la fecha del pedido." : null;
    }

    public static string? ValidarMotivoAnulacion(string? motivo)
    {
        var largo = motivo?.Trim().Length ?? 0;
        return largo < MinMotivo || largo > MaxMotivo
            ? $"Explica el motivo de la anulación en {MinMotivo} a {MaxMotivo} caracteres."
            : null;
    }

    public static string? ValidarNota(string? nota) =>
        nota?.Trim().Length > MaxNota ? $"La nota admite hasta {MaxNota} caracteres." : null;

    // Errores por campo de un abono. Sin fecha, el abono es de hoy (fecha de Chile).
    public static Dictionary<string, string[]> ValidarAbono(EntradaAbono abono, DateOnly hoy)
    {
        var errores = new Dictionary<string, string[]>();
        if (abono.Monto <= 0 || !Reglas.EsMontoEnPesos(abono.Monto) || abono.Monto > MaxMontoFinal)
            errores["monto"] = ["El abono va en pesos, sin decimales, y debe ser mayor que $0."];
        if (abono.FechaPago is DateOnly fecha && (fecha > hoy || fecha < hoy.AddDays(-DiasAtrasFechaPago)))
            errores["fechaPago"] = [$"La fecha del pago no puede ser futura ni de hace más de {DiasAtrasFechaPago} días."];
        if (abono.MedioPago is null || !MediosPago.Todos.Contains(abono.MedioPago))
            errores["medioPago"] = [$"Medio de pago inválido. Valores: {string.Join(", ", MediosPago.Todos)}."];
        if (abono.Observacion?.Trim().Length > MaxObservacionAbono)
            errores["observacion"] = [$"La observación admite hasta {MaxObservacionAbono} caracteres."];
        if (abono.NumeroFactura?.Trim().Length > MaxFactura)
            errores["numeroFactura"] = [$"El número de factura admite hasta {MaxFactura} caracteres."];
        return errores;
    }

    public static string? ValidarMotivoAnulacionAbono(string? motivo)
    {
        var largo = motivo?.Trim().Length ?? 0;
        return largo < MinMotivoAnulacionAbono || largo > MaxMotivoAnulacionAbono
            ? $"Explica por qué se anula el abono en {MinMotivoAnulacionAbono} a {MaxMotivoAnulacionAbono} caracteres."
            : null;
    }

    // Decision 2 del 14-11-2026: gestion anula cualquier abono; el resto del personal, solo los que registro
    // el mismo en las ultimas 24 horas. Las dos horas son de Chile.
    public static bool PuedeAnularAbono(bool esGestion, int idEmpleado, int idRegistra, DateTime registroChile, DateTime ahoraChile) =>
        esGestion || (idEmpleado == idRegistra && ahoraChile - registroChile <= TimeSpan.FromHours(HorasParaAnularAbonoPropio));

    // Filtros del listado: un valor desconocido es un error (400), no se ignora en silencio.
    public static Dictionary<string, string[]> ValidarFiltro(string? estadoPedido, string? estadoPago, int? anio, int? mes, string? orden)
    {
        var errores = new Dictionary<string, string[]>();
        if (!string.IsNullOrEmpty(estadoPedido) && !EstadosPedido.Existe(estadoPedido))
            errores["estadoPedido"] = [$"Estado inválido. Valores: {string.Join(", ", EstadosPedido.Todos)}."];
        if (!string.IsNullOrEmpty(estadoPago) && !EstadosPago.Existe(estadoPago))
            errores["estadoPago"] = [$"Estado de pago inválido. Valores: {string.Join(", ", EstadosPago.Todos)}."];
        if (anio is < 2000 or > 2100)
            errores["anio"] = ["El año debe estar entre 2000 y 2100."];
        if (mes is < 1 or > 12)
            errores["mes"] = ["El mes debe estar entre 1 y 12."];
        if (!string.IsNullOrEmpty(orden) && orden is not ("fecha_desc" or "fecha_asc" or "entrega_asc" or "saldo_desc"))
            errores["orden"] = ["Orden inválido. Valores: fecha_desc, fecha_asc, entrega_asc, saldo_desc."];
        return errores;
    }

    // Calendario: como mucho 62 dias, contando el primero y el ultimo.
    public const int MaxDiasCalendario = 62;

    public static string? ValidarRango(DateOnly? desde, DateOnly? hasta)
    {
        if (desde is not DateOnly inicio || hasta is not DateOnly fin)
            return "Indica las fechas desde y hasta (aaaa-mm-dd).";
        if (fin < inicio)
            return "La fecha hasta no puede ser anterior a la fecha desde.";
        // El ultimo dia posible no tiene dia siguiente: el rango se consulta hasta el dia siguiente a hasta.
        if (fin == DateOnly.MaxValue)
            return "La fecha hasta está fuera de rango.";
        return fin.DayNumber - inicio.DayNumber + 1 > MaxDiasCalendario
            ? $"El calendario muestra como mucho {MaxDiasCalendario} días a la vez."
            : null;
    }

    public static string Numero(int idPedido) => $"#{idPedido.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    public static string Resumen(IReadOnlyDictionary<string, string[]> errores) =>
        errores.Count == 1 ? errores.First().Value[0] : "Revisa los datos del pedido.";
}
