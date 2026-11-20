using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Mantenedores;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Categorias, tipos de letrero (productos) y la vitrina (catalogo). Fase 9 (RFS07, E12, E13): las
// lecturas siguen publicas para lo que se ofrece; administrar es de gestion, con version (If-Match) y sin
// borrado fisico (se desactiva u oculta). Sin consultas directas a la base: todo pasa por los servicios.
// Se retiraron los DELETE fisicos y las respuestas con la entidad de EF.
public static class CatalogoApi
{
    public static void MapCatalogoApi(this WebApplication app)
    {
        MapCategorias(app);
        MapProductos(app);
        MapCatalogo(app);
    }

    private static void MapCategorias(WebApplication app)
    {
        var categorias = app.MapGroup("/api/v1/categorias").WithTags("Categorias").RequireRateLimiting("ApiGeneral");

        // Publico: solo las activas. Gestion: todas por defecto, o ?activo=true|false|todos.
        categorias.MapGet("/", async (ClaimsPrincipal user, ServicioCategorias servicio, CancellationToken ct, string? q, string? activo, string? orden,
            int? pagina, int? tamano) =>
        {
            if (!FiltroActivo.TryLeer(activo, out var filtro))
                return FiltroInvalido();
            return Results.Ok(await servicio.ListarAsync(new ParametrosListado(pagina, tamano, orden, q), EsGestion(user) ? filtro : true, ct));
        })
        .AllowAnonymous();

        categorias.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, HttpContext http, ServicioCategorias servicio, CancellationToken ct) =>
            RespuestasGestion.ConETag(await servicio.ObtenerAsync(id, EsGestion(user), ct), http))
            .AllowAnonymous();

        categorias.MapPost("/", async (CategoriaRequest req, HttpContext http, ServicioCategorias servicio, CancellationToken ct) =>
            RespuestasGestion.Responder(await servicio.CrearAsync(req.Entrada(), req.Activo ?? true, ct), http,
                c => Results.Created($"/api/v1/categorias/{c.IdCategoria}", c), ServicioCategorias.MensajeVersionObsoleta))
            .RequireAuthorization(Politicas.Gestion);

        categorias.MapPut("/{id:int}", async (int id, CategoriaRequest req, HttpContext http, ServicioCategorias servicio, CancellationToken ct) =>
            !RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version)
                ? RespuestasGestion.IfMatchIlegible()
                : RespuestasGestion.Responder(await servicio.EditarAsync(id, req.Entrada(), version, ct), http, Results.Ok, ServicioCategorias.MensajeVersionObsoleta))
            .RequireAuthorization(Politicas.Gestion);

        categorias.MapPatch("/{id:int}/activo", async (int id, CambioActivoRequest req, HttpContext http, ServicioCategorias servicio, CancellationToken ct) =>
        {
            if (req.Activo is not bool activo)
                return FaltaActivo();
            return !RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version)
                ? RespuestasGestion.IfMatchIlegible()
                : RespuestasGestion.Responder(await servicio.CambiarActivoAsync(id, activo, version, ct), http, Results.Ok, ServicioCategorias.MensajeVersionObsoleta);
        })
        .RequireAuthorization(Politicas.Gestion);
    }

    private static void MapProductos(WebApplication app)
    {
        var productos = app.MapGroup("/api/v1/productos").WithTags("Productos").RequireRateLimiting("ApiGeneral");

        // Publico (y el personal que no es de gestion, en las solicitudes): solo lo que se ofrece, activo y de
        // una categoria activa. Gestion: todo por defecto, o ?activo=true|false|todos. Filtro ?categoria=.
        productos.MapGet("/", async (ClaimsPrincipal user, ServicioProductos servicio, CancellationToken ct, string? q, string? activo, string? orden,
            int? categoria, int? pagina, int? tamano) =>
        {
            if (!FiltroActivo.TryLeer(activo, out var filtro))
                return FiltroInvalido();
            var gestion = EsGestion(user);
            return Results.Ok(await servicio.ListarAsync(new ParametrosListado(pagina, tamano, orden, q), filtro, soloOfrecidos: !gestion, categoria, ct));
        })
        .AllowAnonymous();

        productos.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, HttpContext http, ServicioProductos servicio, CancellationToken ct) =>
            RespuestasGestion.ConETag(await servicio.ObtenerAsync(id, soloOfrecidos: !EsGestion(user), ct), http))
            .AllowAnonymous();

        productos.MapPost("/", async (ProductoRequest req, HttpContext http, ServicioProductos servicio, CancellationToken ct) =>
            RespuestasGestion.Responder(await servicio.CrearAsync(req.Entrada(), req.Activo ?? true, ct), http,
                p => Results.Created($"/api/v1/productos/{p.IdProducto}", p), ServicioProductos.MensajeVersionObsoleta))
            .RequireAuthorization(Politicas.Gestion);

        productos.MapPut("/{id:int}", async (int id, ProductoRequest req, HttpContext http, ServicioProductos servicio, CancellationToken ct) =>
            !RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version)
                ? RespuestasGestion.IfMatchIlegible()
                : RespuestasGestion.Responder(await servicio.EditarAsync(id, req.Entrada(), version, ct), http, Results.Ok, ServicioProductos.MensajeVersionObsoleta))
            .RequireAuthorization(Politicas.Gestion);

        // Fase 12D: limites de medida, medidas fijas y opciones de un producto (solo gestion, con If-Match).
        productos.MapPut("/{id:int}/medidas-y-opciones", async (int id, MedidasYOpcionesRequest req, HttpContext http, ServicioProductos servicio, CancellationToken ct) =>
            !RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version)
                ? RespuestasGestion.IfMatchIlegible()
                : RespuestasGestion.Responder(await servicio.EditarMedidasYOpcionesAsync(id, req.Entrada(), version, ct), http, Results.Ok,
                    ServicioProductos.MensajeVersionObsoleta))
            .RequireAuthorization(Politicas.Gestion);

        productos.MapPatch("/{id:int}/activo", async (int id, CambioActivoRequest req, HttpContext http, ServicioProductos servicio, CancellationToken ct) =>
        {
            if (req.Activo is not bool activo)
                return FaltaActivo();
            return !RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version)
                ? RespuestasGestion.IfMatchIlegible()
                : RespuestasGestion.Responder(await servicio.CambiarActivoAsync(id, activo, version, ct), http, Results.Ok, ServicioProductos.MensajeVersionObsoleta);
        })
        .RequireAuthorization(Politicas.Gestion);
    }

    private static void MapCatalogo(WebApplication app)
    {
        var catalogo = app.MapGroup("/api/v1/catalogo").WithTags("Catalogo").RequireRateLimiting("ApiGeneral");

        // Vitrina publica paginada y filtrable por categoria (fase 5). Gestion puede pedir tambien las ocultas
        // (?incluirOcultos=true), buscar (?q=) y filtrar (?visible=true|false). Los visitantes y el resto del
        // personal reciben TrabajoPublico, sin los datos de gestion.
        catalogo.MapGet("/", async (ClaimsPrincipal user, ServicioVitrina vitrina, CancellationToken ct, int? categoria = null, int? pagina = null,
            int? tamano = null, bool incluirOcultos = false, string? q = null, string? visible = null) =>
        {
            if (!FiltroActivo.TryLeer(visible, out var filtroVisible))
                return ErrorApi.Invalido("El filtro «visible» admite true, false o todos.");
            var gestion = EsGestion(user);
            var resultado = await vitrina.ListarAsync(categoria, new ParametrosListado(pagina, tamano, buscar: q), incluirOcultos && gestion, ct, filtroVisible);
            return gestion
                ? Results.Ok(resultado)
                : Results.Ok(new ResultadoPaginado<TrabajoPublico>(
                    resultado.Elementos.Select(t => t.ParaPublico()).ToList(), resultado.Total, resultado.Pagina, resultado.Tamano));
        })
        .AllowAnonymous();

        catalogo.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, HttpContext http, ServicioVitrina vitrina, CancellationToken ct) =>
        {
            var gestion = EsGestion(user);
            return await vitrina.ObtenerAsync(id, gestion, ct) switch
            {
                null => Results.NotFound(),
                var item when gestion => RespuestasGestion.ConETag(item, http),
                var item => Results.Ok(item.ParaPublico())
            };
        })
        .AllowAnonymous();

        catalogo.MapPost("/", async (CatalogoRequest req, ClaimsPrincipal user, HttpContext http, ServicioVitrina vitrina, CancellationToken ct) =>
            RespuestasGestion.Responder(await vitrina.CrearAsync(req.Datos(), user.IdUsuario(), ct), http,
                t => Results.Created($"/api/v1/catalogo/{t.IdCatalogo}", t), ServicioVitrina.MensajeVersionObsoleta))
            .RequireAuthorization(Politicas.Gestion);

        catalogo.MapPut("/{id:int}", async (int id, CatalogoRequest req, HttpContext http, ServicioVitrina vitrina, CancellationToken ct) =>
            !RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version)
                ? RespuestasGestion.IfMatchIlegible()
                : RespuestasGestion.Responder(await vitrina.EditarAsync(id, req.Datos(), version, ct), http, Results.Ok, ServicioVitrina.MensajeVersionObsoleta))
            .RequireAuthorization(Politicas.Gestion);

        catalogo.MapPatch("/{id:int}/visible", async (int id, CambioVisibleRequest req, HttpContext http, ServicioVitrina vitrina, CancellationToken ct) =>
        {
            if (req.Visible is not bool visible)
                return ErrorApi.Invalido("Indica si la publicación se ve (visible: true o false).",
                    new Dictionary<string, string[]> { ["visible"] = ["Indica true o false."] });
            return !RespuestasGestion.TryVersion(http.Request, req.RowVersion, out var version)
                ? RespuestasGestion.IfMatchIlegible()
                : RespuestasGestion.Responder(await vitrina.CambiarVisibleAsync(id, visible, version, ct), http, Results.Ok, ServicioVitrina.MensajeVersionObsoleta);
        })
        .RequireAuthorization(Politicas.Gestion);

        // RNFO17: quien registro la autorizacion del cliente para mostrar su logo, y cuando. Si viene If-Match,
        // se comprueba la version.
        catalogo.MapPut("/{id:int}/autorizacion-logo", async (int id, AutorizacionLogoRequest req, ClaimsPrincipal user, HttpContext http,
            ServicioVitrina vitrina, CancellationToken ct) =>
        {
            if (!Concurrencia.TryLeerIfMatch(http.Request, out var version))
                return RespuestasGestion.IfMatchIlegible();
            return await vitrina.AutorizarLogoAsync(id, user.IdUsuario(), req.Autorizado, ct, version) switch
            {
                { VersionObsoleta: true } => ErrorApi.Crear(StatusCodes.Status409Conflict, Concurrencia.CodigoVersionObsoleta, ServicioVitrina.MensajeVersionObsoleta),
                { Error: { } error } => ErrorApi.Invalido(error),
                { Trabajo: { } item } => RespuestasGestion.ConETag(item, http),
                _ => Results.NotFound()
            };
        })
        .RequireAuthorization(Politicas.Gestion);
    }

    internal static bool EsGestion(ClaimsPrincipal user) => user.IsInRole(Roles.Dueno) || user.IsInRole(Roles.Administrador);

    internal static IResult FiltroInvalido() => ErrorApi.Invalido("El filtro «activo» admite true, false o todos.",
        new Dictionary<string, string[]> { ["activo"] = ["Usa true, false o todos."] });

    internal static IResult FaltaActivo() => ErrorApi.Invalido("Indica si queda activo (activo: true o false).",
        new Dictionary<string, string[]> { ["activo"] = ["Indica true o false."] });
}

// «activo» solo al crear (por defecto true); para cambiarlo despues esta PATCH /{id}/activo.
public record CategoriaRequest(string? Nombre, string? Descripcion, bool? Activo = null, byte[]? RowVersion = null)
{
    public EntradaCategoria Entrada() => new(Nombre, Descripcion);
}

public record ProductoRequest(int? IdCategoria, string? Nombre, string? Descripcion, decimal? PrecioBaseRef, bool? Activo = null, byte[]? RowVersion = null)
{
    public EntradaProducto Entrada() => new(IdCategoria, Nombre, Descripcion, PrecioBaseRef);
}

// Fase 12D: medidas en milimetros (la pantalla las muestra y recibe en centimetros).
public record MedidasYOpcionesRequest(int? AnchoMinMm, int? AnchoMaxMm, int? AltoMinMm, int? AltoMaxMm, List<MedidaFijaEntrada>? MedidasFijas,
    List<GrupoOpcionesEntrada>? Opciones, byte[]? RowVersion = null)
{
    public EntradaMedidasYOpciones Entrada() => new(AnchoMinMm, AnchoMaxMm, AltoMinMm, AltoMaxMm, MedidasFijas, Opciones);
}

// imagenUrl queda por compatibilidad con las fotos anteriores a la subida de imagenes (fase 5).
public record CatalogoRequest(int IdProducto, string? Titulo, string? Descripcion, int? IdImagen = null, int? AnchoMm = null, int? AltoMm = null,
    decimal? PrecioReferencial = null, bool IncluyeLogoCliente = false, bool Visible = true, string? ImagenUrl = null, byte[]? RowVersion = null)
{
    public DatosPublicacion Datos() => new(IdProducto, Titulo, Descripcion, ImagenUrl, AnchoMm, AltoMm, PrecioReferencial, Visible, IdImagen, IncluyeLogoCliente);
}

public record AutorizacionLogoRequest(bool Autorizado);
