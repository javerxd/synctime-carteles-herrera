using System.Net;
using System.Text;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 16C-1 (H11, D8): Render gratuito bloquea el SMTP (puertos 25, 465 y 587), asi que en la nube los correos salen por HTTPS con la API de
// Brevo, detras de la misma interfaz (IEnviadorCorreo) que el SMTP y la bandeja. Estas pruebas usan un servidor SIMULADO (un manejador de HTTP
// que responde lo que la prueba decide): nunca se llama al proveedor real.
public sealed class EnviadorCorreoApiTests
{
    private const string Clave = "clave-de-prueba-que-no-es-real";

    private sealed record Capturada(HttpMethod Metodo, Uri? Direccion, Dictionary<string, string> Cabeceras, JsonElement Cuerpo, string CuerpoTexto);

    // El «proveedor» simulado: guarda lo que recibe y responde lo que se le pide.
    private sealed class ProveedorSimulado(Func<HttpRequestMessage, HttpResponseMessage>? responder = null) : HttpMessageHandler, IHttpClientFactory
    {
        public List<Capturada> Recibidas { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken ct)
        {
            var texto = peticion.Content is null ? "" : await peticion.Content.ReadAsStringAsync(ct);
            var cabeceras = peticion.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => string.Join(",", h.Value));
            if (peticion.Content is not null)
                foreach (var h in peticion.Content.Headers)
                    cabeceras[h.Key.ToLowerInvariant()] = string.Join(",", h.Value);
            Recibidas.Add(new Capturada(peticion.Method, peticion.RequestUri, cabeceras, texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone(), texto));
            return responder?.Invoke(peticion) ?? new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("""{"messageId":"<x@smtp-relay.brevo.com>"}""") };
        }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
    }

    private static OpcionesCorreo Opciones(Action<OpcionesCorreo>? ajustar = null)
    {
        var o = new OpcionesCorreo
        {
            Modo = OpcionesCorreo.ModoApi,
            Remitente = new OpcionesRemitente { Nombre = "Letreros Herrera", Direccion = "avisos@ejemplo.test" },
            Api = new OpcionesApiCorreo { Clave = Clave, TiempoEsperaSegundos = 5 }
        };
        ajustar?.Invoke(o);
        return o;
    }

    private static EnviadorCorreoApi Enviador(ProveedorSimulado proveedor, OpcionesCorreo? opciones = null, string? urlPublica = "https://synctime.vercel.app")
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(urlPublica is null ? [] : new Dictionary<string, string?> { ["Sitio:UrlPublica"] = urlPublica })
            .Build();
        return new EnviadorCorreoApi(Options.Create(opciones ?? Opciones()), proveedor, configuracion);
    }

    private static CorreoParaEnviar Correo(string destino = "cliente@ejemplo.test", string? responderA = null, string html = "<p>Hola</p><img src=\"cid:logo-lh\" width=\"40\" height=\"40\" alt=\"\">",
        bool conBaja = false)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress("Letreros Herrera", "avisos@ejemplo.test"));
        mensaje.To.Add(MailboxAddress.Parse(destino));
        if (responderA is not null)
            mensaje.ReplyTo.Add(MailboxAddress.Parse(responderA));
        mensaje.Subject = "Cotización lista";
        mensaje.Headers.Add("Auto-Submitted", "auto-generated");
        if (conBaja)
        {
            mensaje.Headers.Add("List-Unsubscribe", "<https://synctime.vercel.app/baja/abc>");
            mensaje.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        }
        var cuerpo = new BodyBuilder { TextBody = "Hola (texto)", HtmlBody = html };
        cuerpo.LinkedResources.Add("logo-lh.png", new byte[] { 1, 2, 3 }, new ContentType("image", "png")).ContentId = "logo-lh";
        mensaje.Body = cuerpo.ToMessageBody();
        return new CorreoParaEnviar(7, "solicitud-7", mensaje);
    }

    [Fact]
    public async Task Envia_UnaPeticionHttpsAlProveedor_ConLaClaveEnLaCabecera_YElMensajeEnJson()
    {
        var proveedor = new ProveedorSimulado();

        await Enviador(proveedor).EnviarAsync(Correo(responderA: "persona@ejemplo.test"), CancellationToken.None);

        var recibida = Assert.Single(proveedor.Recibidas);
        Assert.Equal(HttpMethod.Post, recibida.Metodo);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", recibida.Direccion!.ToString());
        Assert.Equal(Clave, recibida.Cabeceras["api-key"]);
        Assert.Contains("application/json", recibida.Cabeceras["accept"]);
        Assert.StartsWith("application/json", recibida.Cabeceras["content-type"]);

        var json = recibida.Cuerpo;
        Assert.Equal("avisos@ejemplo.test", json.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("Letreros Herrera", json.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal("cliente@ejemplo.test", json.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("persona@ejemplo.test", json.GetProperty("replyTo").GetProperty("email").GetString());
        Assert.Equal("Cotización lista", json.GetProperty("subject").GetString());
        Assert.Equal("Hola (texto)", json.GetProperty("textContent").GetString());
        Assert.Contains("<p>Hola</p>", json.GetProperty("htmlContent").GetString());
        Assert.Equal("auto-generated", json.GetProperty("headers").GetProperty("Auto-Submitted").GetString());
    }

    [Fact]
    public async Task LaClave_ViajaSoloEnLaCabecera_NuncaEnElCuerpo()
    {
        var proveedor = new ProveedorSimulado();

        await Enviador(proveedor).EnviarAsync(Correo(), CancellationToken.None);

        Assert.DoesNotContain(Clave, proveedor.Recibidas.Single().CuerpoTexto);
    }

    [Fact]
    public async Task ElLogoPorCid_SeCambiaPorLaDireccionPublicaDelSitio_PorqueBrevoNoAdmiteImagenesIncrustadas()
    {
        var proveedor = new ProveedorSimulado();

        await Enviador(proveedor).EnviarAsync(Correo(), CancellationToken.None);

        var html = proveedor.Recibidas.Single().Cuerpo.GetProperty("htmlContent").GetString()!;
        Assert.DoesNotContain("cid:", html);
        Assert.Contains("src=\"https://synctime.vercel.app/marca/icono-192.png\"", html);
    }

    [Fact]
    public async Task ElLogoPorCid_SinDireccionDelSitio_SeQuita_ElCorreoSaleIgual()
    {
        var proveedor = new ProveedorSimulado();

        await Enviador(proveedor, urlPublica: null).EnviarAsync(Correo(), CancellationToken.None);

        var html = proveedor.Recibidas.Single().Cuerpo.GetProperty("htmlContent").GetString()!;
        Assert.DoesNotContain("cid:", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains("<p>Hola</p>", html);
    }

    [Fact]
    public async Task ElLogo_PuedeIrEnUnaDireccionPropia()
    {
        var proveedor = new ProveedorSimulado();
        var opciones = Opciones(o => o.Api.UrlLogo = "https://cdn.ejemplo.test/logo.png");

        await Enviador(proveedor, opciones).EnviarAsync(Correo(), CancellationToken.None);

        Assert.Contains("src=\"https://cdn.ejemplo.test/logo.png\"", proveedor.Recibidas.Single().Cuerpo.GetProperty("htmlContent").GetString());
    }

    [Fact]
    public async Task LaBajaConUnClic_SeConservaEnLasCabeceras()
    {
        var proveedor = new ProveedorSimulado();

        await Enviador(proveedor).EnviarAsync(Correo(conBaja: true), CancellationToken.None);

        var cabeceras = proveedor.Recibidas.Single().Cuerpo.GetProperty("headers");
        Assert.Equal("<https://synctime.vercel.app/baja/abc>", cabeceras.GetProperty("List-Unsubscribe").GetString());
        Assert.Equal("List-Unsubscribe=One-Click", cabeceras.GetProperty("List-Unsubscribe-Post").GetString());
    }

    [Fact]
    public async Task VariosDestinatarios_VanTodosEnLaMismaPeticion()
    {
        var proveedor = new ProveedorSimulado();
        var correo = Correo();
        correo.Mensaje.To.Add(MailboxAddress.Parse("otro@ejemplo.test"));

        await Enviador(proveedor).EnviarAsync(correo, CancellationToken.None);

        var para = proveedor.Recibidas.Single().Cuerpo.GetProperty("to").EnumerateArray().Select(t => t.GetProperty("email").GetString()).ToList();
        Assert.Equal(["cliente@ejemplo.test", "otro@ejemplo.test"], para);
    }

    // ── Fallos a proposito: el proveedor caido, rechazando la clave o limitando los envios ──

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "autenticacion", true, false)]
    [InlineData(HttpStatusCode.Forbidden, "autenticacion", true, false)]
    [InlineData(HttpStatusCode.TooManyRequests, "servidor", true, false)]
    [InlineData(HttpStatusCode.InternalServerError, "servidor", true, false)]
    [InlineData(HttpStatusCode.BadGateway, "servidor", true, false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "servidor", true, false)]
    [InlineData(HttpStatusCode.BadRequest, "mensaje", false, false)]
    public async Task UnaRespuestaDeError_SeClasificaSinGuardarElCuerpo(HttpStatusCode estado, string categoria, bool detieneLote, bool permanente)
    {
        // El cuerpo del error suele repetir el correo del destinatario o la cuenta: nunca debe terminar en la base ni en un registro.
        var proveedor = new ProveedorSimulado(_ => new HttpResponseMessage(estado) { Content = new StringContent("""{"code":"x","message":"destino secreto@ejemplo.test rechazado, api-key invalida"}""") });

        var error = await Assert.ThrowsAsync<FalloApiCorreoException>(() => Enviador(proveedor).EnviarAsync(Correo(), CancellationToken.None));
        var fallo = ClasificadorFallos.Clasificar(error);

        Assert.Equal(categoria, fallo.Categoria);
        Assert.Equal(detieneLote, fallo.DetieneLote);
        Assert.Equal(permanente, fallo.Permanente);
        Assert.Equal((int)estado, fallo.Codigo);
        Assert.DoesNotContain("secreto@ejemplo.test", fallo.Texto + error.Message + error);
        Assert.DoesNotContain(Clave, fallo.Texto + error.Message + error);
    }

    [Fact]
    public async Task SiNoHayRedHaciaElProveedor_ElFalloEsDeConexion_YNoRepiteLaClave()
    {
        var proveedor = new ProveedorSimulado(_ => throw new HttpRequestException($"No se pudo conectar (api-key {Clave})"));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Enviador(proveedor).EnviarAsync(Correo(), CancellationToken.None));
        var fallo = ClasificadorFallos.Clasificar(error);

        Assert.Equal("conexion", fallo.Categoria);
        Assert.True(fallo.DetieneLote);
        Assert.DoesNotContain(Clave, fallo.Texto);
    }

    [Fact]
    public async Task UnProveedorQueNoResponde_AgotaElTiempo_ComoTimeout()
    {
        var proveedor = new ProveedorLento();
        var opciones = Opciones(o => o.Api.TiempoEsperaSegundos = 1);
        var enviador = new EnviadorCorreoApi(Options.Create(opciones), proveedor, new ConfigurationBuilder().Build());

        var error = await Assert.ThrowsAsync<TimeoutException>(() => enviador.EnviarAsync(Correo(), CancellationToken.None));

        Assert.Equal("tiempo_agotado", ClasificadorFallos.Clasificar(error).Categoria);
    }

    [Fact]
    public async Task SiLaApiSeCierra_LaCancelacionSePropaga_NoSeConfundeConUnTimeout()
    {
        var proveedor = new ProveedorLento();
        using var cierre = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var enviador = new EnviadorCorreoApi(Options.Create(Opciones()), proveedor, new ConfigurationBuilder().Build());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enviador.EnviarAsync(Correo(), cierre.Token));
    }

    private sealed class ProveedorLento : HttpMessageHandler, IHttpClientFactory
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return new HttpResponseMessage(HttpStatusCode.Created);
        }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
    }

    // ── Configuracion ──

    [Fact]
    public void Listo_ConLaClaveYLaDireccionDelRemitente()
    {
        Assert.True(Enviador(new ProveedorSimulado()).Listo(out var motivo));
        Assert.Null(motivo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SinClave_NoEstaListo_YElMotivoNombraLaVariable(string? clave)
    {
        var enviador = Enviador(new ProveedorSimulado(), Opciones(o => o.Api.Clave = clave));

        Assert.False(enviador.Listo(out var motivo));
        Assert.Contains("Correo:Api:Clave", motivo);
    }

    [Fact]
    public void SinDireccionDeRemitente_NoEstaListo()
    {
        var enviador = Enviador(new ProveedorSimulado(), Opciones(o => o.Remitente.Direccion = null));

        Assert.False(enviador.Listo(out var motivo));
        Assert.Contains("remitente", motivo);
    }

    [Theory]
    [InlineData("http://api.brevo.com/v3/smtp/email")]
    [InlineData("api.brevo.com/v3/smtp/email")]
    [InlineData("ftp://api.brevo.com")]
    [InlineData("https://usuario:clave@api.brevo.com/v3/smtp/email")]
    public void UnaDireccionDelProveedorQueNoEsHttpsSeguro_NoEstaLista(string url)
    {
        // La clave viaja en la cabecera: por http o con datos en la direccion podria verla cualquiera.
        var enviador = Enviador(new ProveedorSimulado(), Opciones(o => o.Api.Url = url));

        Assert.False(enviador.Listo(out var motivo));
        Assert.Contains("Correo:Api:Url", motivo);
    }

    [Fact]
    public void Http_SoloSeAdmiteEnElEquipoLocal_ParaLasPruebas()
    {
        var enviador = Enviador(new ProveedorSimulado(), Opciones(o => o.Api.Url = "http://127.0.0.1:5999/v3/smtp/email"));

        Assert.True(enviador.Listo(out _));
    }

    [Fact]
    public void LaDescripcion_NombraAlProveedor_YNuncaLaClave()
    {
        var enviador = Enviador(new ProveedorSimulado());

        Assert.Contains("Brevo", enviador.Descripcion);
        Assert.DoesNotContain(Clave, enviador.Descripcion);
        Assert.DoesNotContain(Clave, Opciones().Api.ToString());
        Assert.DoesNotContain(Clave, Opciones().ToString());
    }

    [Fact]
    public void ElModoApi_EsValido_YSeLeeSinImportarMayusculas()
    {
        Assert.True(new OpcionesCorreo { Modo = "api" }.ModoValido);
        Assert.True(new OpcionesCorreo { Modo = "Api" }.EsApi);
        Assert.False(new OpcionesCorreo { Modo = "Gmail" }.ModoValido);
        // Los modos de siempre no cambian.
        Assert.True(new OpcionesCorreo { Modo = "Smtp" }.ModoValido);
        Assert.True(new OpcionesCorreo { Modo = "Bandeja" }.ModoValido);
    }

    [Fact]
    public void ElModoApi_UsaLaDireccionDelRemitente_NuncaLaDeUnaCuentaSmtp()
    {
        var o = Opciones();
        o.Smtp.Usuario = "otra@gmail.com";

        Assert.Equal("avisos@ejemplo.test", o.DireccionRemitente);
    }

    [Theory]
    [InlineData("Brevo", true)]
    [InlineData("brevo", true)]
    [InlineData("Resend", false)]
    [InlineData("", false)]
    public void ElProveedorSoloPuedeSerBrevo(string proveedor, bool valido)
    {
        var o = Opciones(x => x.Api.Proveedor = proveedor);

        Assert.Equal(valido, o.Api.ProveedorValido);
    }

    [Fact]
    public void ElContactoDelTaller_NoPuedeSerLaCasillaQueEnviaLosAvisos_TampocoPorApi()
    {
        var o = Opciones(x => x.ContactoTaller = "AVISOS@ejemplo.test");

        Assert.True(o.ContactoIgualAlRemitente);
        Assert.False(Opciones().ContactoIgualAlRemitente);
    }

    [Fact]
    public void ElCuerpoSeEnviaEnUtf8()
    {
        var proveedor = new ProveedorSimulado();
        var correo = Correo();
        correo.Mensaje.Subject = "Cotización ñandú";

        Enviador(proveedor).EnviarAsync(correo, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Contains("charset=utf-8", proveedor.Recibidas.Single().Cabeceras["content-type"], StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Cotización ñandú", proveedor.Recibidas.Single().Cuerpo.GetProperty("subject").GetString());
        _ = Encoding.UTF8;
    }
}
