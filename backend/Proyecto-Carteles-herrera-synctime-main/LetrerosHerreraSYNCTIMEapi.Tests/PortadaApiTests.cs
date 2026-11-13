using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 5: lo que la portada pide a la API sin sesion.
[Collection(ApiCollection.Name)]
public sealed class PortadaApiTests(ApiFixture api)
{
    // ── Vitrina ──

    private async Task<(int IdCategoria, int IdProducto)> CrearTipoAsync()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var categoria = await cliente.PostAsJsonAsync("/api/v1/categorias", new { nombre = $"Categoría {api.Siguiente()}" }).EsperarAsync(HttpStatusCode.Created);
        var idCategoria = categoria.GetProperty("idCategoria").GetInt32();
        var producto = await cliente.PostAsJsonAsync("/api/v1/productos", new { idCategoria, nombre = $"Letrero de ventana {api.Siguiente()}" }).EsperarAsync(HttpStatusCode.Created);
        return (idCategoria, producto.GetProperty("idProducto").GetInt32());
    }

    private async Task<HttpResponseMessage> PublicarAsync(int idProducto, bool visible = true, bool incluyeLogoCliente = false, int? idImagen = null) =>
        await api.Cliente(await api.TokenAdminAsync()).PostAsJsonAsync("/api/v1/catalogo", new
        {
            idProducto,
            titulo = $"Trabajo {api.Siguiente()}",
            descripcion = "Letrero para una cafetería del centro",
            anchoMm = 1200,
            altoMm = 600,
            precioReferencial = 85000,
            visible,
            incluyeLogoCliente,
            idImagen
        });

    [Fact]
    public async Task LaVitrina_SeFiltraPorCategoriaYSePagina_SinDatosDelPersonal()
    {
        var (idCategoria, idProducto) = await CrearTipoAsync();
        for (var i = 0; i < 3; i++)
            await PublicarAsync(idProducto).EsperarAsync(HttpStatusCode.Created);

        using var respuesta = await api.Cliente().GetAsync($"/api/v1/catalogo?categoria={idCategoria}&tamano=2");
        var texto = await respuesta.Content.ReadAsStringAsync();
        var pagina = JsonDocument.Parse(texto).RootElement;

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(3, pagina.GetProperty("total").GetInt32());
        Assert.Equal(2, pagina.GetProperty("elementos").GetArrayLength());
        Assert.Equal(2, pagina.GetProperty("totalPaginas").GetInt32());
        var trabajo = pagina.GetProperty("elementos")[0];
        Assert.StartsWith("Letrero de ventana", trabajo.GetProperty("tipoLetrero").GetString());
        Assert.Equal(idCategoria, trabajo.GetProperty("categoria").GetProperty("idCategoria").GetInt32());
        // La vitrina publica no dice quien publico cada trabajo.
        Assert.DoesNotContain("mpleado", texto);
    }

    [Fact]
    public async Task UnaPublicacionConLogoDelCliente_NoSePuedeMostrarSinAutorizacion()
    {
        var (idCategoria, idProducto) = await CrearTipoAsync();

        var rechazo = await PublicarAsync(idProducto, visible: true, incluyeLogoCliente: true).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Contains("autorización", rechazo.GetProperty("error").GetString());

        var oculta = await PublicarAsync(idProducto, visible: false, incluyeLogoCliente: true).EsperarAsync(HttpStatusCode.Created);
        var id = oculta.GetProperty("idCatalogo").GetInt32();
        var publica = await api.Cliente().GetAsync($"/api/v1/catalogo?categoria={idCategoria}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(0, publica.GetProperty("total").GetInt32());

        var autorizada = await api.Cliente(await api.TokenAdminAsync())
            .PutAsJsonAsync($"/api/v1/catalogo/{id}/autorizacion-logo", new { autorizado = true })
            .EsperarAsync(HttpStatusCode.OK);
        Assert.True(autorizada.GetProperty("logoAutorizado").GetBoolean());
        var registro = await api.ConsultarAsync(db => db.Catalogos.AsNoTracking().SingleAsync(c => c.IdCatalogo == id));
        Assert.Equal(await api.IdAdminAsync(), registro.IdEmpleadoAutorizaLogo);
        Assert.NotNull(registro.FechaAutorizacionLogo);

        await api.ConsultarAsync(db => db.Catalogos.Where(c => c.IdCatalogo == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Visible, true)));
        publica = await api.Cliente().GetAsync($"/api/v1/catalogo?categoria={idCategoria}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(1, publica.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task LaBase_ImpideMostrarUnLogoSinAutorizacion()
    {
        var (_, idProducto) = await CrearTipoAsync();
        var oculta = await PublicarAsync(idProducto, visible: false, incluyeLogoCliente: true).EsperarAsync(HttpStatusCode.Created);
        var id = oculta.GetProperty("idCatalogo").GetInt32();

        await Assert.ThrowsAnyAsync<Exception>(() => api.ConsultarAsync(db =>
            db.Catalogos.Where(c => c.IdCatalogo == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Visible, true))));
    }

    [Fact]
    public async Task UnTrabajoConImagenSubida_LaMuestraEnDosTamanos()
    {
        var (idCategoria, idProducto) = await CrearTipoAsync();
        using var mapa = new SKBitmap(1000, 750);
        using (var lienzo = new SKCanvas(mapa))
            lienzo.Clear(new SKColor(0xB4, 0x53, 0x09));
        using var imagenPng = SKImage.FromBitmap(mapa);
        using var png = imagenPng.Encode(SKEncodedImageFormat.Png, 100);
        var archivo = new ByteArrayContent(png.ToArray());
        archivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var subida = await api.Cliente(await api.TokenAdminAsync())
            .PostAsync("/api/v1/imagenes", new MultipartFormDataContent { { archivo, "archivo", "foto.png" } })
            .EsperarAsync(HttpStatusCode.Created);
        var clave = subida.GetProperty("clave").GetString();
        var idImagen = await api.ConsultarAsync(db => db.Imagenes.Where(i => i.Clave == clave).Select(i => i.IdImagen).SingleAsync());

        await PublicarAsync(idProducto, idImagen: idImagen).EsperarAsync(HttpStatusCode.Created);

        var pagina = await api.Cliente().GetAsync($"/api/v1/catalogo?categoria={idCategoria}").EsperarAsync(HttpStatusCode.OK);
        var imagen = pagina.GetProperty("elementos")[0].GetProperty("imagen");
        Assert.Equal(subida.GetProperty("urlGrande").GetString(), imagen.GetProperty("urlGrande").GetString());
        Assert.Equal(subida.GetProperty("urlMiniatura").GetString(), imagen.GetProperty("urlMiniatura").GetString());
        Assert.Equal(1000, imagen.GetProperty("ancho").GetInt32());
        Assert.False(imagen.GetProperty("esEjemplo").GetBoolean());
    }

    [Fact]
    public async Task UnaImagenQueNoExiste_NoSePuedePublicar()
    {
        var (_, idProducto) = await CrearTipoAsync();

        var error = await PublicarAsync(idProducto, idImagen: 999999).EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Contains("imagen", error.GetProperty("error").GetString());
    }

    // ── Estado del local ──

    [Fact]
    public async Task ElEstadoDelLocal_CombinaHorarioYCartel_SinDatosDelPersonal()
    {
        var estado = await api.Cliente().GetAsync("/api/v1/local/estado").EsperarAsync(HttpStatusCode.OK);

        Assert.Contains(estado.GetProperty("motivo").GetString(), new[] { "abierto", "fuera_de_horario", "cerrado_hoy", "ausente" });
        Assert.Contains(estado.GetProperty("abiertoAhora").ValueKind, new[] { JsonValueKind.True, JsonValueKind.False });
        Assert.True(estado.TryGetProperty("horaChile", out _));
        Assert.Equal(7, estado.GetProperty("semana").GetArrayLength());
        Assert.True(estado.GetProperty("cartel").TryGetProperty("mensajeEstado", out _));
        Assert.DoesNotContain("mpleado", estado.GetRawText());
    }

    // ── Documentos legales ──

    [Fact]
    public async Task LasVersionesDeLosDocumentos_SonPublicas()
    {
        var documentos = await api.Cliente().GetAsync("/api/v1/documentos-legales").EsperarAsync(HttpStatusCode.OK);

        var tipos = documentos.EnumerateArray().Select(d => d.GetProperty("tipo").GetString()).Order();
        Assert.Equal(["cookies", "marketing", "privacidad", "terminos"], tipos);
        Assert.All(documentos.EnumerateArray(), d => Assert.Equal("1.0", d.GetProperty("version").GetString()));
    }

    // ── Formulario de contacto (RFS08, E22) ──

    private Dictionary<string, object?> Consulta(string? sitioWeb = null, bool aceptaPrivacidad = true, string version = "1.0") => new()
    {
        ["nombre"] = "Pedro Soto",
        ["correo"] = $"pedro{api.Siguiente()}@correo.cl",
        ["telefono"] = "+56 9 1234 5678",
        ["asunto"] = "Letrero para mi local",
        ["mensaje"] = "Hola, quisiera saber cuánto cuesta un letrero luminoso de dos metros.",
        ["aceptaPrivacidad"] = aceptaPrivacidad,
        ["versionPrivacidad"] = version,
        ["sitioWeb"] = sitioWeb
    };

    [Fact]
    public async Task UnaConsulta_SeGuardaConSuConsentimiento_YAvisaAlTaller()
    {
        await api.LimpiarColaDeCorreoAsync();
        var cuerpo = Consulta();
        var correo = (string)cuerpo["correo"]!;

        var respuesta = await api.Cliente().PostAsJsonAsync("/api/v1/contacto", cuerpo).EsperarAsync(HttpStatusCode.Accepted);

        Assert.True(respuesta.GetProperty("recibida").GetBoolean());
        var consulta = await api.ConsultarAsync(db => db.ConsultasContacto.AsNoTracking().SingleAsync(c => c.Correo == correo));
        Assert.Equal("Letrero para mi local", consulta.Asunto);
        Assert.False(consulta.Revisada);
        var consentimiento = await api.ConsultarAsync(db => db.Consentimientos.AsNoTracking().SingleAsync(c => c.ReferenciaEnvio == $"contacto:{consulta.IdConsulta}"));
        Assert.Equal(("privacidad", "1.0"), (consentimiento.Tipo, consentimiento.VersionDocumento));
        var aviso = await api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking().SingleAsync(c => c.Referencia == $"contacto:{consulta.IdConsulta}"));
        Assert.Equal("consulta_contacto", aviso.Tipo);
        Assert.Equal(ApiFixture.AvisosTaller, aviso.Destinatarios);
        Assert.Equal(correo, aviso.ResponderA);
        Assert.Contains("Letrero para mi local", aviso.Asunto);
    }

    [Fact]
    public async Task SinAceptarLaPrivacidad_NoSeGuardaNada()
    {
        var antes = await api.ConsultarAsync(db => db.ConsultasContacto.CountAsync());

        var error = await api.Cliente().PostAsJsonAsync("/api/v1/contacto", Consulta(aceptaPrivacidad: false)).EsperarAsync(HttpStatusCode.BadRequest);

        Assert.True(error.GetProperty("errores").TryGetProperty("aceptaPrivacidad", out _));
        Assert.Equal(antes, await api.ConsultarAsync(db => db.ConsultasContacto.CountAsync()));
    }

    [Fact]
    public async Task ConUnaVersionAntiguaDeLaPrivacidad_PideVolverALeerla()
    {
        var error = await api.Cliente().PostAsJsonAsync("/api/v1/contacto", Consulta(version: "0.9")).EsperarAsync(HttpStatusCode.Conflict);

        Assert.Contains("versión", error.GetProperty("error").GetString());
    }

    [Fact]
    public async Task UnRobotQueLlenaElCampoTrampa_RecibeLaMismaRespuesta_PeroNoSeGuardaNada()
    {
        await api.LimpiarColaDeCorreoAsync();
        var antes = await api.ConsultarAsync(db => db.ConsultasContacto.CountAsync());

        var respuesta = await api.Cliente().PostAsJsonAsync("/api/v1/contacto", Consulta(sitioWeb: "https://spam.example")).EsperarAsync(HttpStatusCode.Accepted);

        Assert.True(respuesta.GetProperty("recibida").GetBoolean());
        Assert.Equal(antes, await api.ConsultarAsync(db => db.ConsultasContacto.CountAsync()));
        Assert.Equal(0, await api.ConsultarAsync(db => db.CorreosSalientes.CountAsync()));
    }

    // ── Cookies (RFS12) ──

    [Fact]
    public async Task AceptarLasCookies_QuedaRegistradoPorVisitante()
    {
        var visitante = Guid.NewGuid();

        using var respuesta = await api.Cliente().PostAsJsonAsync("/api/v1/consentimientos/cookies", new { visitante, version = "1.0" });

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var registro = await api.ConsultarAsync(db => db.Consentimientos.AsNoTracking().SingleAsync(c => c.ReferenciaEnvio == $"cookies:{visitante:N}"));
        Assert.Equal("cookies", registro.Tipo);
    }

    [Fact]
    public async Task UnVisitanteSinIdentificador_NoSeRegistra()
    {
        await api.Cliente().PostAsJsonAsync("/api/v1/consentimientos/cookies", new { visitante = Guid.Empty, version = "1.0" })
            .EsperarAsync(HttpStatusCode.BadRequest);
    }

    // ── Correcciones de la revision de seguridad de la fase 5 ──

    [Fact]
    public async Task LaVitrinaPublica_NoDiceSiUnTrabajoLlevaElLogoDeUnCliente()
    {
        var (idCategoria, idProducto) = await CrearTipoAsync();
        var creada = await PublicarAsync(idProducto).EsperarAsync(HttpStatusCode.Created);
        var id = creada.GetProperty("idCatalogo").GetInt32();

        var lista = await api.Cliente().GetStringAsync($"/api/v1/catalogo?categoria={idCategoria}");
        var una = await api.Cliente().GetStringAsync($"/api/v1/catalogo/{id}");
        var delPersonal = await api.Cliente(await api.TokenAdminAsync()).GetStringAsync($"/api/v1/catalogo/{id}");

        foreach (var publico in new[] { lista, una })
        {
            Assert.DoesNotContain("incluyeLogoCliente", publico);
            Assert.DoesNotContain("logoAutorizado", publico);
            Assert.DoesNotContain("\"visible\"", publico);
        }
        Assert.Contains("incluyeLogoCliente", delPersonal);
    }

    [Fact]
    public async Task CambiarLaImagenDeUnaPublicacionConLogo_ExigeVolverAAutorizar()
    {
        var (_, idProducto) = await CrearTipoAsync();
        var admin = api.Cliente(await api.TokenAdminAsync());
        var creada = await PublicarAsync(idProducto, visible: false, incluyeLogoCliente: true).EsperarAsync(HttpStatusCode.Created);
        var id = creada.GetProperty("idCatalogo").GetInt32();
        await admin.PutAsJsonAsync($"/api/v1/catalogo/{id}/autorizacion-logo", new { autorizado = true }).EsperarAsync(HttpStatusCode.OK);

        object Edicion(bool visible, string? imagenUrl) => new
        {
            idProducto, titulo = "Letrero con logo", descripcion = "Logo del cliente", imagenUrl,
            anchoMm = 1200, altoMm = 600, precioReferencial = 85000, visible, incluyeLogoCliente = true
        };

        // Misma pieza: la autorizacion sigue valiendo y se puede mostrar.
        await admin.PutAsJsonAsync($"/api/v1/catalogo/{id}", Edicion(visible: true, imagenUrl: null)).EsperarAsync(HttpStatusCode.OK);

        // Otra imagen: ya no se puede mostrar con la autorizacion anterior...
        var rechazo = await admin.PutAsJsonAsync($"/api/v1/catalogo/{id}", Edicion(visible: true, imagenUrl: "/otra-foto.webp")).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Contains("autorización", rechazo.GetProperty("error").GetString());

        // ...y si se guarda oculta, la autorizacion anterior se borra.
        var oculta = await admin.PutAsJsonAsync($"/api/v1/catalogo/{id}", Edicion(visible: false, imagenUrl: "/otra-foto.webp")).EsperarAsync(HttpStatusCode.OK);
        Assert.False(oculta.GetProperty("logoAutorizado").GetBoolean());
        var registro = await api.ConsultarAsync(db => db.Catalogos.AsNoTracking().SingleAsync(c => c.IdCatalogo == id));
        Assert.Null(registro.IdEmpleadoAutorizaLogo);
        Assert.Null(registro.FechaAutorizacionLogo);
    }

    [Fact]
    public async Task AutorizarDeNuevo_ConservaQuienYCuando_YSinLogoNoHayNadaQueAutorizar()
    {
        var (_, idProducto) = await CrearTipoAsync();
        var admin = api.Cliente(await api.TokenAdminAsync());
        var conLogo = (await PublicarAsync(idProducto, visible: false, incluyeLogoCliente: true).EsperarAsync(HttpStatusCode.Created)).GetProperty("idCatalogo").GetInt32();
        var sinLogo = (await PublicarAsync(idProducto, visible: false).EsperarAsync(HttpStatusCode.Created)).GetProperty("idCatalogo").GetInt32();

        await admin.PutAsJsonAsync($"/api/v1/catalogo/{conLogo}/autorizacion-logo", new { autorizado = true }).EsperarAsync(HttpStatusCode.OK);
        var primera = await api.ConsultarAsync(db => db.Catalogos.AsNoTracking().SingleAsync(c => c.IdCatalogo == conLogo));
        await api.ConsultarAsync(db => db.Catalogos.Where(c => c.IdCatalogo == conLogo)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaAutorizacionLogo, primera.FechaAutorizacionLogo!.Value.AddDays(-3))));

        await admin.PutAsJsonAsync($"/api/v1/catalogo/{conLogo}/autorizacion-logo", new { autorizado = true }).EsperarAsync(HttpStatusCode.OK);
        var segunda = await api.ConsultarAsync(db => db.Catalogos.AsNoTracking().SingleAsync(c => c.IdCatalogo == conLogo));
        Assert.Equal(primera.FechaAutorizacionLogo!.Value.AddDays(-3), segunda.FechaAutorizacionLogo);

        await admin.PutAsJsonAsync($"/api/v1/catalogo/{sinLogo}/autorizacion-logo", new { autorizado = true }).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnaPublicacionConDatosFueraDeRango_Responde400_NoUnErrorDeLaBase()
    {
        var (_, idProducto) = await CrearTipoAsync();
        var admin = api.Cliente(await api.TokenAdminAsync());

        await admin.PostAsJsonAsync("/api/v1/catalogo", new { idProducto, titulo = new string('a', 121), descripcion = "x" }).EsperarAsync(HttpStatusCode.BadRequest);
        await admin.PostAsJsonAsync("/api/v1/catalogo", new { idProducto, titulo = "Letrero", descripcion = "x", anchoMm = 1200 }).EsperarAsync(HttpStatusCode.BadRequest);
        await admin.PostAsJsonAsync("/api/v1/catalogo", new { idProducto, titulo = "Letrero", descripcion = "x", precioReferencial = -1 }).EsperarAsync(HttpStatusCode.BadRequest);
        await admin.PostAsJsonAsync("/api/v1/catalogo", new { idProducto = int.MaxValue, titulo = "Letrero", descripcion = "x" }).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnTelefonoConMuchosEspacios_SeGuardaNormalizado()
    {
        var cuerpo = Consulta();
        cuerpo["telefono"] = "9" + new string(' ', 40) + "12345678";
        var correo = (string)cuerpo["correo"]!;

        await api.Cliente().PostAsJsonAsync("/api/v1/contacto", cuerpo).EsperarAsync(HttpStatusCode.Accepted);

        var consulta = await api.ConsultarAsync(db => db.ConsultasContacto.AsNoTracking().SingleAsync(c => c.Correo == correo));
        Assert.Equal("+56912345678", consulta.Telefono);
    }

    [Fact]
    public async Task LaMismaConsultaEnviadaDosVeces_SeGuardaYSeAvisaUnaSolaVez()
    {
        await api.LimpiarColaDeCorreoAsync();
        var cuerpo = Consulta();
        var correo = (string)cuerpo["correo"]!;

        var primera = await api.Cliente().PostAsJsonAsync("/api/v1/contacto", cuerpo).EsperarAsync(HttpStatusCode.Accepted);
        var segunda = await api.Cliente().PostAsJsonAsync("/api/v1/contacto", cuerpo).EsperarAsync(HttpStatusCode.Accepted);

        Assert.Equal(primera.GetProperty("mensaje").GetString(), segunda.GetProperty("mensaje").GetString());
        Assert.Equal(1, await api.ConsultarAsync(db => db.ConsultasContacto.CountAsync(c => c.Correo == correo)));
        Assert.Equal(1, await api.ConsultarAsync(db => db.CorreosSalientes.CountAsync(c => c.Tipo == "consulta_contacto")));
    }

    [Fact]
    public async Task LasConsultas_NoAgotanElCupoDeAvisosDeLasSolicitudes()
    {
        await api.LimpiarColaDeCorreoAsync();
        await using var otra = api.ConVariable("Correo__TopeConsultasPorHora", "1");

        await otra.Cliente().PostAsJsonAsync("/api/v1/contacto", Consulta()).EsperarAsync(HttpStatusCode.Accepted);
        await otra.Cliente().PostAsJsonAsync("/api/v1/contacto", Consulta()).EsperarAsync(HttpStatusCode.Accepted);

        // Las dos quedan guardadas, pero solo la primera genera aviso; el resto del cupo es para las solicitudes.
        Assert.Equal(1, await api.ConsultarAsync(db => db.CorreosSalientes.CountAsync(c => c.Tipo == "consulta_contacto")));
        Assert.Equal(0, await api.ConsultarAsync(db => db.CorreosSalientes.CountAsync(c => c.Tipo == "aviso_tope_solicitudes")));
    }

    [Fact]
    public async Task ElFormularioDeContacto_TieneUnLimitePorHoraEIp()
    {
        await using var otra = api.ConVariable("RateLimiting__ContactoPorHora", "1");

        await otra.Cliente().PostAsJsonAsync("/api/v1/contacto", Consulta()).EsperarAsync(HttpStatusCode.Accepted);
        using var segunda = await otra.Cliente().PostAsJsonAsync("/api/v1/contacto", Consulta());

        Assert.Equal(HttpStatusCode.TooManyRequests, segunda.StatusCode);
    }

    [Fact]
    public async Task RechazarLasCookiesDespuesDeAceptarlas_RegistraElRetiro()
    {
        var visitante = Guid.NewGuid();
        await api.Cliente().PostAsJsonAsync("/api/v1/consentimientos/cookies", new { visitante, version = "1.0" }).EsperarAsync(HttpStatusCode.NoContent);

        using var retiro = await api.Cliente().PostAsJsonAsync("/api/v1/consentimientos/cookies", new { visitante, version = "1.0", acepta = false });

        Assert.Equal(HttpStatusCode.NoContent, retiro.StatusCode);
        var registro = await api.ConsultarAsync(db => db.Consentimientos.AsNoTracking().SingleAsync(c => c.ReferenciaEnvio == $"cookies:{visitante:N}"));
        Assert.NotNull(registro.FechaRevocacion);
    }

    [Fact]
    public async Task RechazarSinHaberAceptado_NoGuardaNada()
    {
        var visitante = Guid.NewGuid();

        await api.Cliente().PostAsJsonAsync("/api/v1/consentimientos/cookies", new { visitante, version = "1.0", acepta = false }).EsperarAsync(HttpStatusCode.NoContent);

        Assert.Equal(0, await api.ConsultarAsync(db => db.Consentimientos.CountAsync(c => c.ReferenciaEnvio == $"cookies:{visitante:N}")));
    }

    // TestServer no aplica el tamaño maximo del cuerpo (Kestrel si): se comprueba que las rutas lo declaran.
    [Theory]
    [InlineData("/api/v1/contacto")]
    [InlineData("/api/v1/consentimientos/cookies")]
    public void LosEnviosAnonimos_DeclaranUnTamanoMaximo(string ruta)
    {
        var fuente = api.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>();
        var ruta1 = fuente.Endpoints.OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Single(e => "/" + e.RoutePattern.RawText?.TrimStart('/') == ruta);

        var limite = ruta1.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata>();

        Assert.Equal(16 * 1024, limite?.MaxRequestBodySize);
    }
}
