using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Imagenes;
using LetrerosHerreraSYNCTIMEapi.Negocio.Imagenes;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Imagenes del taller (RNFP22). La subida es de gestion (dueño y administrador, que mantienen la vitrina
// en la fase 9); servirlas es publico, porque la portada las muestra a cualquier visitante.
public static class ImagenApi
{
    // Lo que puede ocupar el formulario ademas de la imagen (limites y nombres de los campos).
    private const long MargenFormulario = 64 * 1024;

    public static void MapImagenApi(this WebApplication app)
    {
        var grupo = app.MapGroup(ServicioImagenes.RutaBase).WithTags("Imagenes");

        // La API usa tokens Bearer, no cookies: un sitio ajeno no puede subir en nombre de nadie, asi que
        // la proteccion antifalsificacion de formularios no aplica aqui.
        grupo.MapPost("/", async (HttpContext http, ClaimsPrincipal usuario, ServicioImagenes servicio, CancellationToken ct) =>
        {
            if (http.Request.ContentLength > ReglasImagen.MaxBytes + MargenFormulario)
                return MuyGrande();
            var limite = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (limite is { IsReadOnly: false })
                limite.MaxRequestBodySize = ReglasImagen.MaxBytes + MargenFormulario;

            if (!http.Request.HasFormContentType)
                return SinArchivo();

            IFormFile? archivo;
            try
            {
                archivo = (await http.Request.ReadFormAsync(ct)).Files.GetFile("archivo");
            }
            catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                return MuyGrande();
            }
            catch (InvalidDataException)
            {
                return ErrorApi.Invalido("El formulario no se pudo leer. Vuelva a adjuntar la imagen.");
            }

            if (archivo is null || archivo.Length == 0)
                return SinArchivo();
            if (archivo.Length > ReglasImagen.MaxBytes)
                return MuyGrande();

            var datos = new byte[archivo.Length];
            await using (var flujo = archivo.OpenReadStream())
                await flujo.ReadExactlyAsync(datos, ct);

            try
            {
                var subida = await servicio.SubirAsync(datos, usuario.IdUsuario(), ct);
                return Results.Created(subida.UrlGrande, subida);
            }
            catch (ImagenInvalidaException e)
            {
                return ErrorApi.Invalido(e.Message);
            }
        })
        .RequireAuthorization(Politicas.Gestion)
        .RequireRateLimiting("ApiGeneral")
        // Sin .Accepts(): esa marca hace que el enrutamiento responda 415 antes de revisar permisos, y un
        // visitante debe recibir 401. El tipo de contenido se revisa dentro (HasFormContentType).
        .DisableAntiforgery()
        .Produces<ImagenSubida>(StatusCodes.Status201Created);

        // Los nombres son aleatorios y nunca se reutilizan, asi que el navegador puede guardarlas un año.
        grupo.MapGet("/{archivo}", (string archivo, AlmacenImagenes almacen, HttpContext http) =>
        {
            var ruta = almacen.Ruta(archivo);
            if (ruta is null)
                return Results.NotFound();

            http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return Results.File(ruta, "image/webp",
                lastModified: File.GetLastWriteTimeUtc(ruta),
                entityTag: new EntityTagHeaderValue($"\"{Path.GetFileNameWithoutExtension(archivo)}\""));
        })
        .AllowAnonymous()
        .RequireRateLimiting("Imagenes")
        .Produces(StatusCodes.Status200OK, contentType: "image/webp");
    }

    private static IResult SinArchivo() =>
        ErrorApi.Invalido("Adjunte una imagen JPG, PNG o WebP en el campo «archivo».");

    private static IResult MuyGrande() =>
        ErrorApi.Crear(StatusCodes.Status413PayloadTooLarge, "imagen_muy_grande", ReglasImagen.MensajeTamano);
}
