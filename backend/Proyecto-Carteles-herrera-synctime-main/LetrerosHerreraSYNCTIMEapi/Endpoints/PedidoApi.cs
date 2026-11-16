using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Middleware;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Pedidos;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Pedidos, abonos, cobranza y calendario (fase 8). Todo el personal crea pedidos, registra su avance y sus
// abonos (RFU13, RFU14); anular un pedido con abonos, anular abonos ajenos o antiguos, eliminar y ver las
// alertas es de gestion. Editar, avanzar y eliminar exigen la version vista (If-Match); registrar o anular
// un abono no, porque solo suma o resta y la base protege el tope. Sin consultas directas a la base.
// Las antiguas POST /pedidos y DELETE fisico se retiraron.
public static class PedidoApi
{
    public static void MapPedidoApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/pedidos").WithTags("Pedidos")
            .RequireAuthorization(Politicas.Empleado).RequireRateLimiting("ApiGeneral");

        group.MapGet("/", async (ServicioPedidos servicio, CancellationToken ct, string? estadoPedido, string? estadoPago, bool? cobranza,
            int? anio, int? mes, int? idCliente, int? idResponsable, string? q, string? orden, int? pagina, int? tamano) =>
        {
            var errores = ReglasPedido.ValidarFiltro(estadoPedido, estadoPago, anio, mes, orden);
            if (errores.Count > 0)
                return ErrorApi.Invalido(ReglasPedido.Resumen(errores), errores);
            var filtro = new FiltroPedidos(estadoPedido, estadoPago, cobranza == true, anio, mes, idCliente, idResponsable, orden);
            return Results.Ok(await servicio.ListarAsync(filtro, new ParametrosListado(pagina, tamano, orden, q), ct));
        });

        // Calendario de entregas (S08): como mucho 62 dias por consulta.
        group.MapGet("/calendario", async (ServicioPedidos servicio, CancellationToken ct, DateOnly? desde, DateOnly? hasta) =>
            ReglasPedido.ValidarRango(desde, hasta) is { } error
                ? ErrorApi.Invalido(error)
                : Results.Ok(await servicio.CalendarioAsync(desde!.Value, hasta!.Value, ct)));

        group.MapGet("/alertas", async (ServicioPedidos servicio, CancellationToken ct) => Results.Ok(await servicio.AlertasAsync(ct)))
            .RequireAuthorization(Politicas.Gestion);

        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, HttpContext http, ServicioPedidos servicio, CancellationToken ct) =>
        {
            var ficha = await servicio.FichaAsync(id, Quien(user), ct);
            if (ficha is null)
                return Results.NotFound();
            http.Response.Headers.ETag = Concurrencia.ETag(ficha.RowVersion);
            return Results.Ok(ficha);
        });

        // Pedido presencial directo (RFU35, E26): el cliente se busca o se crea antes (GET /clientes/buscar, POST /clientes).
        group.MapPost("/directo", async (PedidoDirectoRequest req, ClaimsPrincipal user, HttpContext http, ServicioPedidos servicio, CancellationToken ct) =>
            RespuestasPedido.Responder(await servicio.CrearDirectoAsync(Quien(user), req.Entrada(), ct), http,
                r => Results.Created($"/api/v1/pedidos/{r.Ficha.IdPedido}", r.Ficha)))
            .ConIdempotencia("POST /pedidos/directo");

        group.MapPut("/{id:int}", async (int id, EditarPedidoRequest req, ClaimsPrincipal user, HttpContext http, ServicioPedidos servicio, CancellationToken ct) =>
        {
            if (!Concurrencia.TryLeerIfMatch(http.Request, out var version))
                return IfMatchIlegible();
            return RespuestasPedido.Responder(await servicio.EditarAsync(id, Quien(user), req.Datos(), req.MontoBaseTrabajo, version ?? req.RowVersion, ct),
                http, r => Results.Ok(r.Ficha));
        });

        group.MapPatch("/{id:int}/estado", async (int id, EstadoPedidoRequest req, ClaimsPrincipal user, HttpContext http, ServicioPedidos servicio,
            CancellationToken ct) =>
        {
            if (!Concurrencia.TryLeerIfMatch(http.Request, out var version))
                return IfMatchIlegible();
            var resultado = await servicio.CambiarEstadoAsync(id, Quien(user), req.Estado, req.FechaEntregaReal, req.Motivo, req.Nota,
                version ?? req.RowVersion, ct);
            return RespuestasPedido.Responder(resultado, http, r => Results.Ok(r.Ficha));
        });

        group.MapPost("/{id:int}/pagos", async (int id, AbonoRequest req, ClaimsPrincipal user, HttpContext http, ServicioPedidos servicio, CancellationToken ct) =>
            RespuestasPedido.Responder(await servicio.RegistrarAbonoAsync(id, Quien(user), req.Entrada(), ct), http,
                r => Results.Created($"/api/v1/pedidos/{id}", r.Ficha)))
            .ConIdempotencia("POST /pedidos/{id}/pagos");

        group.MapPost("/{id:int}/pagos/{idPago:int}/anular", async (int id, int idPago, AnularAbonoRequest req, ClaimsPrincipal user, HttpContext http,
                ServicioPedidos servicio, CancellationToken ct) =>
            RespuestasPedido.Responder(await servicio.AnularAbonoAsync(id, idPago, Quien(user), req.Motivo, ct), http, r => Results.Ok(r.Ficha)));

        // Borrado logico: solo un pedido «anulado» sin abonos vigentes.
        group.MapDelete("/{id:int}", async (int id, HttpContext http, ServicioPedidos servicio, CancellationToken ct) =>
        {
            if (!Concurrencia.TryLeerIfMatch(http.Request, out var version))
                return IfMatchIlegible();
            return RespuestasPedido.Responder(await servicio.EliminarAsync(id, version, ct), http, _ => Results.NoContent());
        })
        .RequireAuthorization(Politicas.Gestion);

        // Desde la cotizacion aprobada y sin pedido (E08): responde 201 con la ficha.
        app.MapPost("/api/v1/cotizaciones/{id:int}/pedido", async (int id, PedidoDesdeCotizacionRequest req, ClaimsPrincipal user, HttpContext http,
                ServicioPedidos servicio, CancellationToken ct) =>
            RespuestasPedido.Responder(await servicio.CrearDesdeCotizacionAsync(id, Quien(user), req.Datos(), req.AbonoInicial?.Entrada(), ct), http,
                r => Results.Created($"/api/v1/pedidos/{r.Ficha.IdPedido}", r.Ficha)))
            .WithTags("Pedidos")
            .RequireAuthorization(Politicas.Empleado)
            .RequireRateLimiting("ApiGeneral")
            .ConIdempotencia("POST /cotizaciones/{id}/pedido");
    }

    // Quien opera sale del token: su id y si es de gestion.
    internal static PersonalQueOpera Quien(ClaimsPrincipal user) =>
        new(user.IdUsuario(), user.IsInRole(Roles.Dueno) || user.IsInRole(Roles.Administrador));

    private static IResult IfMatchIlegible() =>
        ErrorApi.Invalido("La cabecera If-Match no es una versión válida. Vuelve a cargar el pedido.");
}

// Traduce el resultado del servicio a la respuesta HTTP.
public static class RespuestasPedido
{
    public static IResult Responder(ResultadoPedido resultado, HttpContext http, Func<ResultadoPedido.Correcto, IResult> correcto)
    {
        switch (resultado)
        {
            case ResultadoPedido.Correcto ok:
                http.Response.Headers.ETag = Concurrencia.ETag(ok.Ficha.RowVersion);
                return correcto(ok);
            case ResultadoPedido.Eliminado:
                return correcto(null!);
            case ResultadoPedido.Invalido { Errores: var errores }:
                return ErrorApi.Invalido(ReglasPedido.Resumen(errores), errores);
            case ResultadoPedido.Conflicto { Codigo: var codigo, Mensaje: var mensaje, DesdeLaBase: var desdeLaBase }:
                return ErrorApi.Crear(StatusCodes.Status409Conflict, codigo, mensaje, nivel: desdeLaBase ? ErrorApi.NivelBaseDeDatos : ErrorApi.NivelApi);
            case ResultadoPedido.SinVersion:
                return ErrorApi.Crear(StatusCodes.Status400BadRequest, "sin_version", "Indica la versión que viste (cabecera If-Match con el ETag, o rowVersion).");
            case ResultadoPedido.VersionObsoleta:
                return ErrorApi.Crear(StatusCodes.Status409Conflict, Concurrencia.CodigoVersionObsoleta, ServicioPedidos.MensajeVersionObsoleta);
            case ResultadoPedido.SoloGestion { Mensaje: var mensaje }:
                return ErrorApi.Crear(StatusCodes.Status403Forbidden, "solo_gestion", mensaje);
            default:
                return Results.NotFound();
        }
    }
}

public record AbonoRequest(decimal Monto, DateOnly? FechaPago, string? MedioPago, string? Observacion, string? NumeroFactura = null)
{
    public EntradaAbono Entrada() => new(Monto, FechaPago, MedioPago, Observacion, NumeroFactura);
}

public record AnularAbonoRequest(string? Motivo);

public record PedidoDesdeCotizacionRequest(
    int IdEmpleadoResponsable,
    DateOnly? FechaEntregaEstimada,
    string? ModalidadEntrega,
    bool? RequiereInstalacion,
    string? DireccionInstalacion,
    decimal? CostoInstalacionAdicional,
    string? NumeroFactura,
    AbonoRequest? AbonoInicial)
{
    public EntradaDatosPedido Datos() =>
        new(IdEmpleadoResponsable, FechaEntregaEstimada, ModalidadEntrega, RequiereInstalacion, DireccionInstalacion, CostoInstalacionAdicional, NumeroFactura);
}

public record PedidoDirectoRequest(
    int IdCliente,
    string? DescripcionGeneral,
    List<CrearSolicitudDetalleRequest>? Detalles,
    List<CotizacionMaterialRequest>? Materiales,
    decimal? CostoManoObra,
    decimal? OtrosCostos,
    string? Observaciones,
    int IdEmpleadoResponsable,
    DateOnly? FechaEntregaEstimada,
    string? ModalidadEntrega,
    bool? RequiereInstalacion,
    string? DireccionInstalacion,
    decimal? CostoInstalacionAdicional,
    string? NumeroFactura,
    AbonoRequest? AbonoInicial)
{
    public EntradaPedidoDirecto Entrada() => new(
        IdCliente,
        DescripcionGeneral,
        RespuestasSolicitud.DetallesONull(Detalles),
        new CotizacionRequest(Materiales, CostoManoObra, OtrosCostos, Observaciones).Entrada(),
        new EntradaDatosPedido(IdEmpleadoResponsable, FechaEntregaEstimada, ModalidadEntrega, RequiereInstalacion, DireccionInstalacion,
            CostoInstalacionAdicional, NumeroFactura),
        AbonoInicial?.Entrada());
}

public record EditarPedidoRequest(
    int IdEmpleadoResponsable,
    DateOnly? FechaEntregaEstimada,
    string? ModalidadEntrega,
    bool? RequiereInstalacion,
    string? DireccionInstalacion,
    decimal? CostoInstalacionAdicional,
    string? NumeroFactura,
    decimal? MontoBaseTrabajo,
    byte[]? RowVersion = null)
{
    public EntradaDatosPedido Datos() =>
        new(IdEmpleadoResponsable, FechaEntregaEstimada, ModalidadEntrega, RequiereInstalacion, DireccionInstalacion, CostoInstalacionAdicional, NumeroFactura);
}

public record EstadoPedidoRequest(string? Estado, DateOnly? FechaEntregaReal, string? Motivo, string? Nota, byte[]? RowVersion = null);
