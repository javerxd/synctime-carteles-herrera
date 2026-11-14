using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.Http.Features;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Lee el campo «archivo» de un formulario multipart sin aceptar mas de 10 MB (fase 7, adjuntos). Un
// archivo grande se corta al leerlo, no despues de guardarlo en memoria completo.
public static class ArchivoSubido
{
    // Limite propio de las subidas (RateLimiting:Adjuntos por minuto y por usuario): procesar una foto
    // cuesta memoria y procesador, y cada archivo ocupa disco.
    public const string PoliticaDeLimite = "Adjuntos";

    // Lo que puede ocupar el formulario ademas del archivo (limites y nombres de los campos).
    private const long MargenFormulario = 64 * 1024;

    public sealed record Leido(byte[]? Datos, string? Nombre, IResult? Error);

    public static async Task<Leido> LeerAsync(HttpContext http, CancellationToken ct)
    {
        var maximo = ReglasAdjunto.MaxBytes + MargenFormulario;
        if (http.Request.ContentLength > maximo)
            return new(null, null, MuyGrande());
        var limite = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limite is { IsReadOnly: false })
            limite.MaxRequestBodySize = maximo;

        if (!http.Request.HasFormContentType)
            return new(null, null, SinArchivo());

        IFormFile? archivo;
        try
        {
            archivo = (await http.Request.ReadFormAsync(ct)).Files.GetFile("archivo");
        }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return new(null, null, MuyGrande());
        }
        catch (InvalidDataException)
        {
            return new(null, null, ErrorApi.Invalido("El formulario no se pudo leer. Vuelve a adjuntar el archivo."));
        }

        if (archivo is null || archivo.Length == 0)
            return new(null, null, SinArchivo());
        if (archivo.Length > ReglasAdjunto.MaxBytes)
            return new(null, null, MuyGrande());

        var datos = new byte[archivo.Length];
        await using (var flujo = archivo.OpenReadStream())
            await flujo.ReadExactlyAsync(datos, ct);
        return new(datos, archivo.FileName, null);
    }

    // Respuesta comun a las subidas de adjuntos (cliente y personal).
    public static IResult Responder(ResultadoAdjunto resultado) => resultado switch
    {
        ResultadoAdjunto.Subido { Adjunto: var a } => Results.Created(a.UrlMiniatura ?? Negocio.Solicitudes.Adjuntos.UrlArchivo(a.IdAdjunto),
            new { a.IdAdjunto, a.Tipo, a.Nombre, a.Bytes, a.Ancho, a.Alto, a.UrlMiniatura }),
        ResultadoAdjunto.Invalido { Codigo: var codigo, Mensaje: var mensaje } => ErrorApi.Crear(StatusCodes.Status400BadRequest, codigo, mensaje),
        ResultadoAdjunto.Tope => ErrorApi.Crear(StatusCodes.Status409Conflict, "tope_adjuntos", ReglasAdjunto.MensajeTope),
        ResultadoAdjunto.Conflicto { Mensaje: var mensaje } => ErrorApi.Conflicto(mensaje),
        ResultadoAdjunto.Borrado => Results.NoContent(),
        ResultadoAdjunto.CorreoSinConfirmar => ErrorApi.Crear(StatusCodes.Status403Forbidden, "correo_sin_confirmar", ServicioSolicitudes.MensajeCorreoSinConfirmar),
        _ => Results.NotFound()
    };

    private static IResult SinArchivo() =>
        ErrorApi.Crear(StatusCodes.Status400BadRequest, "sin_archivo", ReglasAdjunto.MensajeSinArchivo);

    // El plan pide 400 (no 413) para que el formulario lo muestre junto al archivo, como los demas errores.
    private static IResult MuyGrande() =>
        ErrorApi.Crear(StatusCodes.Status400BadRequest, "archivo_muy_grande", ReglasAdjunto.MensajeTamano);
}
