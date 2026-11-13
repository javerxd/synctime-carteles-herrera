using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LetrerosHerreraSYNCTIMEapi.Negocio.Imagenes;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// RNFP22: las imagenes que sube el taller se guardan en WebP en dos tamaños y se sirven con cache larga.
[Collection(ApiCollection.Name)]
public sealed class ImagenApiTests(ApiFixture api)
{
    private static byte[] Png(int ancho, int alto)
    {
        using var mapa = new SKBitmap(ancho, alto);
        using (var lienzo = new SKCanvas(mapa))
            lienzo.Clear(new SKColor(0xB4, 0x53, 0x09));
        using var imagen = SKImage.FromBitmap(mapa);
        return imagen.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private static MultipartFormDataContent Formulario(byte[] datos, string nombre = "foto.png", string tipo = "image/png")
    {
        var archivo = new ByteArrayContent(datos);
        archivo.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        return new MultipartFormDataContent { { archivo, "archivo", nombre } };
    }

    private async Task<System.Text.Json.JsonElement> SubirAsync(byte[] datos) =>
        await api.Cliente(await api.TokenAdminAsync()).PostAsync("/api/v1/imagenes", Formulario(datos)).EsperarAsync(HttpStatusCode.Created);

    [Fact]
    public async Task UnaFotoSubida_SeGuardaEnWebpEnDosTamanos_YSeSirveConCacheLarga()
    {
        var subida = await SubirAsync(Png(3000, 2000));

        var clave = subida.GetProperty("clave").GetString()!;
        Assert.Matches("^[0-9a-f]{32}$", clave);
        Assert.Equal(3000, subida.GetProperty("ancho").GetInt32());
        Assert.Equal(2000, subida.GetProperty("alto").GetInt32());
        Assert.Equal($"/api/v1/imagenes/{clave}-grande.webp", subida.GetProperty("urlGrande").GetString());
        Assert.Equal($"/api/v1/imagenes/{clave}-miniatura.webp", subida.GetProperty("urlMiniatura").GetString());

        using var grande = await api.Cliente().GetAsync(subida.GetProperty("urlGrande").GetString());
        var bytes = await grande.Content.ReadAsByteArrayAsync();
        Assert.Equal(HttpStatusCode.OK, grande.StatusCode);
        Assert.Equal("image/webp", grande.Content.Headers.ContentType?.MediaType);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WEBP", Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.Equal("public, max-age=31536000, immutable", grande.Headers.CacheControl?.ToString());
        Assert.NotNull(grande.Headers.ETag);

        // El navegador que ya la tiene no la vuelve a descargar.
        using var peticion = new HttpRequestMessage(HttpMethod.Get, subida.GetProperty("urlMiniatura").GetString());
        using var primera = await api.Cliente().SendAsync(peticion);
        using var condicional = new HttpRequestMessage(HttpMethod.Get, subida.GetProperty("urlMiniatura").GetString());
        condicional.Headers.IfNoneMatch.Add(primera.Headers.ETag!);
        using var segunda = await api.Cliente().SendAsync(condicional);
        Assert.Equal(HttpStatusCode.NotModified, segunda.StatusCode);

        var registro = await api.ConsultarAsync(db => db.Imagenes.AsNoTracking().SingleAsync(i => i.Clave == clave));
        Assert.Equal(await api.IdAdminAsync(), registro.IdEmpleadoSube);
        Assert.Equal((3000, 2000), (registro.Ancho, registro.Alto));
        Assert.True(registro.BytesGrande > 0 && registro.BytesMiniatura > 0);
        Assert.True(File.Exists(Path.Combine(api.CarpetaImagenes, $"{clave}-grande.webp")));
    }

    [Fact]
    public async Task UnArchivoQueNoEsImagen_SeRechazaAunqueDigaSerPng()
    {
        var antes = await api.ConsultarAsync(db => db.Imagenes.CountAsync());

        var error = await api.Cliente(await api.TokenAdminAsync())
            .PostAsync("/api/v1/imagenes", Formulario("<svg><script>alert(1)</script></svg>"u8.ToArray()))
            .EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Contains("JPG, PNG o WebP", error.GetProperty("error").GetString());
        Assert.Equal(antes, await api.ConsultarAsync(db => db.Imagenes.CountAsync()));
    }

    [Fact]
    public async Task SinArchivo_SeRechaza()
    {
        var error = await api.Cliente(await api.TokenAdminAsync())
            .PostAsync("/api/v1/imagenes", new MultipartFormDataContent { { new StringContent("hola"), "otro" } })
            .EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Contains("Adjunte una imagen", error.GetProperty("error").GetString());
    }

    [Fact]
    public async Task UnArchivoDeMasDeDiezMegas_SeRechaza()
    {
        var error = await api.Cliente(await api.TokenAdminAsync())
            .PostAsync("/api/v1/imagenes", Formulario(new byte[ReglasImagen.MaxBytes + 1]))
            .EsperarAsync(HttpStatusCode.RequestEntityTooLarge);

        Assert.Contains("10 MB", error.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("..%2F..%2Fappsettings.json")]
    [InlineData("..%5Cappsettings.json")]
    [InlineData("foto.webp")]
    [InlineData("0123456789abcdef0123456789abcdef-grande.png")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF-grande.webp")]
    [InlineData("0123456789abcdef0123456789abcdef-enorme.webp")]
    public async Task UnNombreQueNoEsDeImagen_NoSeSirve(string nombre)
    {
        using var respuesta = await api.Cliente().GetAsync($"/api/v1/imagenes/{nombre}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // Un archivo real junto a la carpeta de imagenes: si el nombre no se validara, "..\" lo alcanzaria.
    [Fact]
    public async Task UnaRutaQueSaleDeLaCarpeta_NoLlegaAlDisco()
    {
        Directory.CreateDirectory(api.CarpetaImagenes);
        var trampa = Path.GetFullPath(Path.Combine(api.CarpetaImagenes, "..", "trampa.webp"));
        await File.WriteAllTextAsync(trampa, "no deberia servirse");

        foreach (var nombre in new[] { "..%5Ctrampa.webp", "..%2Ftrampa.webp", "%2E%2E%5Ctrampa.webp" })
        {
            using var respuesta = await api.Cliente().GetAsync($"/api/v1/imagenes/{nombre}");
            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        }
    }

    [Fact]
    public async Task UnaImagenQueNoExiste_Responde404()
    {
        using var respuesta = await api.Cliente().GetAsync($"/api/v1/imagenes/{Guid.NewGuid():N}-grande.webp");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
