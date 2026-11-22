using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.IngresoGoogle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 14A (RFS03, RFS12, RFU05, RFU06, E02, RNFE02, RNFP17, RNFO13): el ingreso con Google, contra SQL Server y con el
// verificador simulado (ninguna prueba llama a Google). El ID de cliente de las pruebas es inventado.
[Collection(ApiCollection.Name)]
public sealed partial class GoogleFase14aTests(ApiFixture api)
{
    private const string Ruta = "/api/v1/auth/google";
    private const string RutaMiCuenta = "/api/v1/mi-cuenta/google";

    // ── Utilidades ──

    private string NuevoSub() => $"1{api.Siguiente():D20}";
    private string NuevoCorreo() => $"google{api.Siguiente()}@correo.test";

    private static async Task<string> NonceAsync(HttpClient cliente) =>
        (await cliente.GetAsync($"{Ruta}/nonce").EsperarAsync(HttpStatusCode.OK)).GetProperty("nonce").GetString()!;

    // Lo que hace el navegador: pide el nonce a la API y se lo da a Google, que firma un token que lo lleva dentro.
    private async Task<(string IdToken, string Nonce)> TokenAsync(string sub, string correo, Func<ClaimsGoogle, ClaimsGoogle>? cambio = null, HttpClient? cliente = null)
    {
        var nonce = await NonceAsync(cliente ?? api.Cliente());
        var claims = VerificadorGoogleSimulado.Claims(nonce, sub, correo);
        return (api.Google.Registrar(cambio is null ? claims : cambio(claims)), nonce);
    }

    private static Task<HttpResponseMessage> IngresarAsync(HttpClient cliente, string idToken, string nonce) =>
        cliente.PostAsJsonAsync(Ruta, new { idToken, nonce });

    private static object Completar(string pase, string? telefono, string? apellido = null, bool acepta = true, bool marketing = false,
        string? versionTerminos = "1.0", string? versionPrivacidad = "1.1", string? rut = null) => new
    {
        pase, rut, nombre = (string?)null, apellido, telefono, direccion = (string?)null, aceptaTerminosYPrivacidad = acepta,
        aceptaMarketing = marketing, versionTerminos, versionPrivacidad, versionMarketing = marketing ? "1.0" : null
    };

    private sealed record CuentaGoogle(int Id, string Correo, string Sub, string Telefono, string Token, HttpResponseMessage Respuesta);

    // El camino completo de una persona nueva: ingreso -> pase -> «Completa tu registro».
    private async Task<CuentaGoogle> CrearCuentaAsync(bool marketing = false, HttpClient? cliente = null)
    {
        cliente ??= api.Cliente();
        var (sub, correo, telefono) = (NuevoSub(), NuevoCorreo(), api.Telefono());
        var (idToken, nonce) = await TokenAsync(sub, correo, cliente: cliente);
        var pendiente = await IngresarAsync(cliente, idToken, nonce).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("registro_pendiente", pendiente.GetProperty("estado").GetString());
        using var respuesta = await cliente.PostAsJsonAsync($"{Ruta}/completar", Completar(pendiente.GetProperty("pase").GetString()!, telefono, marketing: marketing));
        var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
        Assert.True(respuesta.StatusCode == HttpStatusCode.Created, $"Se esperaba 201 y llego {(int)respuesta.StatusCode}: {cuerpo}");
        return new CuentaGoogle(cuerpo.GetProperty("usuario").GetProperty("idCliente").GetInt32(), correo, sub, telefono,
            cuerpo.GetProperty("token").GetString()!, respuesta);
    }

    private static (string Renovacion, string Csrf) Cookies(HttpResponseMessage respuesta)
    {
        var valores = respuesta.Headers.TryGetValues("Set-Cookie", out var lista) ? lista.ToList() : [];
        string Valor(string nombre) => valores.Select(v => v.Split(';')[0]).First(v => v.StartsWith(nombre + "=")).Split('=', 2)[1];
        return (Valor("synctime_renovacion"), Valor("synctime_csrf"));
    }

    private static string Codigo(JsonElement error) => error.GetProperty("codigo").GetString()!;

    private Task<int> IdentidadesAsync(string sub) =>
        api.ConsultarAsync(db => db.IdentidadesExternas.CountAsync(i => i.Sub == sub));

    private Task<int> ClientesConCorreoAsync(string correo) =>
        api.ConsultarAsync(db => db.Clientes.CountAsync(c => c.Email == correo));

    // Una cuenta con contraseña y correo confirmado, para vincularle Google.
    private async Task<(int Id, string Correo, string Token)> CuentaConContrasenaAsync() => await api.CrearClienteConfirmadoAsync();

    private HttpClient Con(string token) => api.Cliente(token);

    private static Task<HttpResponseMessage> Vincular(HttpClient cliente, string idToken, string nonce, string? passwordActual) =>
        cliente.PostAsJsonAsync(RutaMiCuenta, new { idToken, nonce, passwordActual });

    private static Task<HttpResponseMessage> Desvincular(HttpClient cliente, string? passwordActual)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Delete, RutaMiCuenta) { Content = JsonContent.Create(new { passwordActual }) };
        return cliente.SendAsync(peticion);
    }

    // ── El nonce ──

    [Fact]
    public async Task ElNonce_EsAnonimo_AleatorioYNoSeGuardaEnCache()
    {
        var cliente = api.Cliente();
        using var uno = await cliente.GetAsync($"{Ruta}/nonce");
        var a = JsonDocument.Parse(await uno.Content.ReadAsStringAsync()).RootElement;
        var b = await cliente.GetAsync($"{Ruta}/nonce").EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(HttpStatusCode.OK, uno.StatusCode);
        Assert.Contains("no-store", uno.Headers.CacheControl?.ToString());
        Assert.NotEqual(a.GetProperty("nonce").GetString(), b.GetProperty("nonce").GetString());
        Assert.True(a.GetProperty("nonce").GetString()!.Length >= 40);
        Assert.True(a.GetProperty("venceEn").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(4));
    }

    [Fact]
    public async Task ElNonce_TieneLimiteDeTasa_PorIp()
    {
        await using var otra = api.ConVariable("RateLimiting__IngresoGoogle", "3");
        var cliente = otra.Cliente();
        for (var i = 0; i < 3; i++)
            await cliente.GetAsync($"{Ruta}/nonce").EsperarAsync(HttpStatusCode.OK);

        var rechazo = await cliente.GetAsync($"{Ruta}/nonce").EsperarAsync(HttpStatusCode.TooManyRequests);
        Assert.Equal("demasiados_intentos", Codigo(rechazo));
    }

    // ── Camino 3: un correo nuevo ──

    [Fact]
    public async Task UnCorreoNuevo_EntregaUnPase_YNoCreaNada_HastaCompletarElRegistro()
    {
        var (sub, correo) = (NuevoSub(), NuevoCorreo());
        var (idToken, nonce) = await TokenAsync(sub, correo);

        var respuesta = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal("registro_pendiente", respuesta.GetProperty("estado").GetString());
        Assert.Equal(correo, respuesta.GetProperty("correo").GetString());
        Assert.Equal("Ana", respuesta.GetProperty("nombre").GetString());
        Assert.Equal("Pérez", respuesta.GetProperty("apellido").GetString());
        Assert.True(respuesta.GetProperty("venceEn").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(9));
        Assert.False(string.IsNullOrWhiteSpace(respuesta.GetProperty("pase").GetString()));
        // Sin sesion, sin cuenta y sin identidad: solo un pase.
        Assert.False(respuesta.TryGetProperty("token", out _));
        Assert.Equal(0, await ClientesConCorreoAsync(correo));
        Assert.Equal(0, await IdentidadesAsync(sub));
    }

    [Fact]
    public async Task CompletarElRegistro_CreaLaCuentaSinContrasena_ConSuIdentidadYConsentimientos_YAbreLaSesion()
    {
        var cuenta = await CrearCuentaAsync(marketing: true);

        var cookies = Cookies(cuenta.Respuesta);
        Assert.Contains("httponly", cuenta.Respuesta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("synctime_renovacion=")), StringComparison.OrdinalIgnoreCase);

        var (cliente, identidad, tipos, correosDeVerificacion) = await api.ConsultarAsync(async db => (
            await db.Clientes.AsNoTracking().SingleAsync(c => c.IdCliente == cuenta.Id),
            await db.IdentidadesExternas.AsNoTracking().SingleAsync(i => i.IdCliente == cuenta.Id),
            await db.Consentimientos.AsNoTracking().Where(c => c.IdCliente == cuenta.Id).Select(c => c.Tipo).ToListAsync(),
            await db.CorreosSalientes.CountAsync(c => c.Destinatarios == cuenta.Correo)));

        Assert.Null(cliente.PasswordHash);
        Assert.Equal(cuenta.Correo, cliente.Email);
        Assert.NotNull(cliente.FechaVerificacionCorreo);
        Assert.True(cliente.Activo);
        Assert.Equal(cuenta.Telefono, cliente.Telefono);
        Assert.Equal("Ana", cliente.Nombre);
        Assert.Equal("Pérez", cliente.Apellido);
        Assert.Equal("google", identidad.Proveedor);
        Assert.Equal(cuenta.Sub, identidad.Sub);
        Assert.Equal(cuenta.Correo, identidad.Correo);
        Assert.Contains("terminos", tipos);
        Assert.Contains("privacidad", tipos);
        Assert.Contains("marketing", tipos);
        // El correo ya lo verifico Google: no hay enlace de confirmacion que enviar.
        Assert.Equal(0, correosDeVerificacion);

        // La sesion es la de siempre: el token abre Mi cuenta y la cookie de renovacion funciona.
        var miCuenta = await Con(cuenta.Token).GetAsync("/api/v1/mi-cuenta").EsperarAsync(HttpStatusCode.OK);
        Assert.True(miCuenta.GetProperty("correoVerificado").GetBoolean());
        Assert.True(miCuenta.GetProperty("googleVinculado").GetBoolean());
        Assert.False(miCuenta.GetProperty("tieneContrasena").GetBoolean());

        var renovar = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/renovar");
        renovar.Headers.Add("Cookie", $"synctime_renovacion={cookies.Renovacion}; synctime_csrf={cookies.Csrf}");
        renovar.Headers.Add("X-CSRF-Token", cookies.Csrf);
        using var renovada = await api.Cliente().SendAsync(renovar);
        Assert.Equal(HttpStatusCode.OK, renovada.StatusCode);
    }

    [Fact]
    public async Task LaCuentaDeGoogle_NoTieneContrasena_YElIngresoConContrasena_LaRechazaComoSiNoExistiera()
    {
        var cuenta = await CrearCuentaAsync();

        var conCuenta = await api.Cliente().PostAsJsonAsync("/api/v1/auth/login-cliente", new { correo = cuenta.Correo, password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.Unauthorized);
        var sinCuenta = await api.Cliente().PostAsJsonAsync("/api/v1/auth/login-cliente", new { correo = NuevoCorreo(), password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.Unauthorized);

        Assert.Equal(sinCuenta.SinTraza(), conCuenta.SinTraza());
    }

    [Fact]
    public async Task CompletarElRegistro_ConElApellidoQueEscribeLaPersona_LoUsaEnVezDelDeGoogle()
    {
        var (sub, correo) = (NuevoSub(), NuevoCorreo());
        var (idToken, nonce) = await TokenAsync(sub, correo, c => c with { Apellido = null, NombreCompleto = null });
        var pendiente = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("", pendiente.GetProperty("apellido").GetString());

        // Sin apellido no se puede: el formulario lo pide.
        var falta = await api.Cliente().PostAsJsonAsync($"{Ruta}/completar", Completar(pendiente.GetProperty("pase").GetString()!, api.Telefono()))
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(falta.GetProperty("errores").TryGetProperty("apellido", out _));

        var creada = await api.Cliente().PostAsJsonAsync($"{Ruta}/completar", Completar(pendiente.GetProperty("pase").GetString()!, api.Telefono(), apellido: "Soto"))
            .EsperarAsync(HttpStatusCode.Created);
        Assert.Equal("Soto", creada.GetProperty("usuario").GetProperty("apellido").GetString());
    }

    // ── Camino 1: la identidad ya esta vinculada ──

    [Fact]
    public async Task ElIngresoRepetido_EntraPorElSub_AunqueElCorreoDeLaCuentaHayaCambiado_YAnotaElUltimoIngreso()
    {
        var cuenta = await CrearCuentaAsync();
        var otroCorreo = NuevoCorreo();
        await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == cuenta.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Email, otroCorreo)));

        // Google ahora trae el correo de siempre, pero la identidad es el sub.
        var (idToken, nonce) = await TokenAsync(cuenta.Sub, cuenta.Correo);
        var respuesta = await IngresarAsync(api.Cliente(), idToken, nonce);

        var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, cuerpo.ToString());
        Assert.Equal("cliente", cuerpo.GetProperty("tipo").GetString());
        Assert.Equal(cuenta.Id, cuerpo.GetProperty("usuario").GetProperty("idCliente").GetInt32());
        Assert.Equal(otroCorreo, cuerpo.GetProperty("usuario").GetProperty("email").GetString());
        Assert.Contains(respuesta.Headers.GetValues("Set-Cookie"), c => c.StartsWith("synctime_renovacion="));
        Assert.NotNull(await api.ConsultarAsync(db => db.IdentidadesExternas.Where(i => i.IdCliente == cuenta.Id).Select(i => i.FechaUltimoIngreso).SingleAsync()));
        // El token abre Mi cuenta.
        await Con(cuerpo.GetProperty("token").GetString()!).GetAsync("/api/v1/mi-cuenta").EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnaCuentaDadaDeBaja_OInactiva_NoEntraPorGoogle_YLaRespuestaNoDiceQueExistia()
    {
        // La baja REAL: una cuenta con contraseña y Google vinculado se da de baja desde Mi cuenta (borra su identidad y su contraseña).
        var cuentaDeBaja = await CuentaConContrasenaAsync();
        var subDeBaja = NuevoSub();
        var (tokenParaVincular, nonceParaVincular) = await TokenAsync(subDeBaja, cuentaDeBaja.Correo);
        await Vincular(Con(cuentaDeBaja.Token), tokenParaVincular, nonceParaVincular, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);
        await Con(cuentaDeBaja.Token).PostAsJsonAsync("/api/v1/mi-cuenta/baja", new { password = ApiFixture.AdminPassword, confirmacion = "DAR DE BAJA" })
            .EsperarAsync(HttpStatusCode.OK);
        var baja = new CuentaGoogle(cuentaDeBaja.Id, cuentaDeBaja.Correo, subDeBaja, "", cuentaDeBaja.Token, null!);
        // Y la que el personal desactivo (la identidad sigue vinculada).
        var inactiva = await CrearCuentaAsync();
        await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == inactiva.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Activo, false)));

        var (tokenDeBaja, nonceDeBaja) = await TokenAsync(baja.Sub, baja.Correo);
        var (tokenInactiva, nonceInactiva) = await TokenAsync(inactiva.Sub, inactiva.Correo);
        var (tokenMalo, nonceMalo) = await TokenAsync(NuevoSub(), NuevoCorreo(), c => c with { CorreoVerificado = false });

        var deBaja = await IngresarAsync(api.Cliente(), tokenDeBaja, nonceDeBaja).EsperarAsync(HttpStatusCode.Unauthorized);
        var deInactiva = await IngresarAsync(api.Cliente(), tokenInactiva, nonceInactiva).EsperarAsync(HttpStatusCode.Unauthorized);
        var invalido = await IngresarAsync(api.Cliente(), tokenMalo, nonceMalo).EsperarAsync(HttpStatusCode.Unauthorized);

        Assert.Equal(invalido.SinTraza(), deBaja.SinTraza());
        Assert.Equal(invalido.SinTraza(), deInactiva.SinTraza());
        Assert.Equal("google_token_invalido", Codigo(invalido));
    }

    // ── Camino 2: el correo ya tiene cuenta (nunca se fusiona solo) ──

    [Fact]
    public async Task ElCorreoDeUnaCuentaConContrasena_Responde409_YNoModificaNada()
    {
        var existente = await CuentaConContrasenaAsync();
        var antes = await api.ConsultarAsync(db => db.Clientes.AsNoTracking().SingleAsync(c => c.IdCliente == existente.Id));
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, existente.Correo);

        var respuesta = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal("cuenta_existente", Codigo(respuesta));
        Assert.Contains("contraseña", respuesta.GetProperty("error").GetString());
        Assert.Contains("Mi cuenta", respuesta.GetProperty("error").GetString());
        Assert.False(respuesta.TryGetProperty("pase", out _));
        Assert.Equal(0, await IdentidadesAsync(sub));
        var despues = await api.ConsultarAsync(db => db.Clientes.AsNoTracking().SingleAsync(c => c.IdCliente == existente.Id));
        Assert.Equal(antes.PasswordHash, despues.PasswordHash);
        Assert.Equal(antes.Email, despues.Email);
        Assert.Equal(antes.FechaVerificacionCorreo, despues.FechaVerificacionCorreo);
        Assert.Equal(1, await ClientesConCorreoAsync(existente.Correo));
        // Y el correo distinto en mayusculas o con espacios sigue siendo la misma cuenta.
        var (otroToken, otroNonce) = await TokenAsync(NuevoSub(), $" {existente.Correo.ToUpperInvariant()} ");
        Assert.Equal("cuenta_existente", Codigo(await IngresarAsync(api.Cliente(), otroToken, otroNonce).EsperarAsync(HttpStatusCode.Conflict)));
    }

    // Un cliente creado en el local con correo pero sin acceso web: no tiene contraseña ni se puede recuperar. Se le explica como activarlo.
    [Fact]
    public async Task ElCorreoDeUnClienteDelLocalSinAccesoWeb_Responde409ConLaSalida_YNoCreaNada()
    {
        var correo = NuevoCorreo();
        await api.ConsultarAsync(async db =>
        {
            db.Clientes.Add(new Cliente { Nombre = "Local", Apellido = "Sin acceso", Telefono = api.Telefono(), Email = correo });
            await db.SaveChangesAsync();
            return 0;
        });
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, correo);

        var respuesta = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal("sin_acceso_web", Codigo(respuesta));
        Assert.Contains("local", respuesta.GetProperty("error").GetString());
        Assert.Equal(0, await IdentidadesAsync(sub));
    }

    [Theory]
    [InlineData("12.34")]
    [InlineData("ab cd")]
    [InlineData("ñandú")]
    public async Task UnSubConCaracteresRaros_SeRechaza(string sub)
    {
        var (idToken, nonce) = await TokenAsync(sub, NuevoCorreo());
        Assert.Equal("google_token_invalido", Codigo(await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.Unauthorized)));
    }

    [Fact]
    public async Task LaColumnaDelSub_DistingueMayusculasDeMinusculas()
    {
        var a = await CuentaConContrasenaAsync();
        var b = await CuentaConContrasenaAsync();
        var sub = $"Ab{api.Siguiente()}x";
        var ahora = DateTimeOffset.UtcNow;
        await api.ConsultarAsync(async db =>
        {
            db.IdentidadesExternas.Add(new IdentidadExterna { IdCliente = a.Id, Sub = sub, Correo = a.Correo, FechaVinculacion = ahora });
            db.IdentidadesExternas.Add(new IdentidadExterna { IdCliente = b.Id, Sub = sub.ToLowerInvariant(), Correo = b.Correo, FechaVinculacion = ahora });
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Equal(2, await api.ConsultarAsync(db => db.IdentidadesExternas.CountAsync(i => i.Sub == sub || i.Sub == sub.ToLowerInvariant())));
    }

    [Fact]
    public async Task UnCorreoMasLargoQueElDeLaCuenta_SeRechazaAlIngresar_EnVezDeAtascarElRegistro()
    {
        var (idToken, nonce) = await TokenAsync(NuevoSub(), $"{new string('a', 95)}@correo.test");
        Assert.Equal("google_token_invalido", Codigo(await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.Unauthorized)));
    }

    [Fact]
    public async Task LaDescargaDeDatos_IncluyeElVinculoConGoogle_SinElSub()
    {
        var cuenta = await CuentaConContrasenaAsync();
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, cuenta.Correo);
        await Vincular(Con(cuenta.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);

        using var respuesta = await Con(cuenta.Token).PostAsJsonAsync("/api/v1/mi-cuenta/datos", new { password = ApiFixture.AdminPassword });
        var texto = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var vinculo = JsonDocument.Parse(texto).RootElement.GetProperty("ingresoConGoogle");
        Assert.Equal("google", vinculo.GetProperty("proveedor").GetString());
        Assert.Equal(cuenta.Correo, vinculo.GetProperty("correo").GetString());
        Assert.DoesNotContain(sub, texto);
    }

    // El personal que reasigna el acceso de una cuenta de Google (sin contraseña) tambien avisa a su titular, como con una cuenta con contraseña.
    [Fact]
    public async Task ReasignarElAccesoDeUnaCuentaDeGoogle_AvisaAlCorreoAnterior()
    {
        var cuenta = await CrearCuentaAsync();
        var nuevoCorreo = NuevoCorreo();

        await Con(await api.TokenAdminAsync()).PutAsJsonAsync($"/api/v1/clientes/{cuenta.Id}/credenciales",
            new { password = ApiFixture.AdminPassword, correo = nuevoCorreo, passwordActual = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.NoContent);

        Assert.True(await api.ConsultarAsync(db => db.CorreosSalientes.AnyAsync(c => c.Destinatarios == cuenta.Correo && c.Tipo == "aviso_acceso_reasignado")));
    }

    // ── Un token que no sirve ──

    [Theory]
    [InlineData("audiencia")]
    [InlineData("emisor")]
    [InlineData("vencido")]
    [InlineData("correo_sin_verificar")]
    [InlineData("sin_sub")]
    [InlineData("sin_correo")]
    [InlineData("nonce_distinto")]
    public async Task UnTokenQueNoCumpleLasCondiciones_Da401_YNoCreaNada(string caso)
    {
        var (sub, correo) = (NuevoSub(), NuevoCorreo());
        var (idToken, nonce) = await TokenAsync(sub, correo, c => caso switch
        {
            "audiencia" => c with { Audiencias = ["otra-aplicacion.apps.googleusercontent.com"] },
            "emisor" => c with { Emisor = "https://evil.test" },
            "vencido" => c with { Expira = DateTimeOffset.UtcNow.AddMinutes(-1) },
            "correo_sin_verificar" => c with { CorreoVerificado = false },
            "sin_sub" => c with { Sub = null },
            "sin_correo" => c with { Correo = null },
            _ => c with { Nonce = "otro-nonce-que-google-no-recibio" }
        });

        var respuesta = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.Unauthorized);

        Assert.Equal("google_token_invalido", Codigo(respuesta));
        Assert.Equal(0, await ClientesConCorreoAsync(correo));
        Assert.Equal(0, await IdentidadesAsync(sub));
    }

    [Fact]
    public async Task UnTokenConFirmaQueNoEsDeGoogle_Da401()
    {
        var nonce = await NonceAsync(api.Cliente());
        var respuesta = await IngresarAsync(api.Cliente(), "eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIn0.", nonce).EsperarAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("google_token_invalido", Codigo(respuesta));
    }

    [Fact]
    public async Task ElNonce_SoloSirveUnaVez_YUnNonceAjenoOInventadoNoSirve_SinLlamarAGoogle()
    {
        var (sub, correo) = (NuevoSub(), NuevoCorreo());
        var (idToken, nonce) = await TokenAsync(sub, correo);
        await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.OK);

        // El mismo token y el mismo nonce otra vez: repeticion.
        var llamadas = api.Google.Llamadas;
        Assert.Equal("google_token_invalido", Codigo(await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.Unauthorized)));
        // Un nonce que la API nunca entrego, aunque Google lo hubiera puesto en el token.
        var inventado = api.Google.Registrar(VerificadorGoogleSimulado.Claims("nonce-inventado-por-el-atacante", NuevoSub(), NuevoCorreo()));
        Assert.Equal("google_token_invalido", Codigo(await IngresarAsync(api.Cliente(), inventado, "nonce-inventado-por-el-atacante").EsperarAsync(HttpStatusCode.Unauthorized)));
        Assert.Equal("google_token_invalido", Codigo(await IngresarAsync(api.Cliente(), inventado, "otro").EsperarAsync(HttpStatusCode.Unauthorized)));
        Assert.Equal(llamadas, api.Google.Llamadas);
    }

    [Fact]
    public async Task ElNonce_QueSePresentoYFallo_YaNoSirve_NiConUnTokenBueno()
    {
        var (sub, correo) = (NuevoSub(), NuevoCorreo());
        var nonce = await NonceAsync(api.Cliente());
        await IngresarAsync(api.Cliente(), "token-malo", nonce).EsperarAsync(HttpStatusCode.Unauthorized);

        var bueno = api.Google.Registrar(VerificadorGoogleSimulado.Claims(nonce, sub, correo));
        await IngresarAsync(api.Cliente(), bueno, nonce).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    // El vencimiento del nonce (5 minutos) lo prueba IngresoGoogleTests con un reloj manual: aqui son instancias distintas de la API.
    [Theory]
    [InlineData(null, "nonce")]
    [InlineData("", "nonce")]
    [InlineData("token", null)]
    [InlineData("token", "")]
    public async Task SinTokenONonce_Da400(string? idToken, string? nonce)
    {
        var respuesta = await api.Cliente().PostAsJsonAsync(Ruta, new { idToken, nonce }).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("datos_invalidos", Codigo(respuesta));
    }

    [Fact]
    public void LasRutasAnonimas_DeclaranUnTopeDeCuerpo_YElNonceYElIngresoTienenLimiteDeTasa()
    {
        // El servidor de pruebas no aplica el tope del cuerpo (solo Kestrel): se comprueba que cada ruta lo declare.
        var rutas = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        RouteEndpoint Endpoint(string patron, string metodo) => rutas.Single(r => r.RoutePattern.RawText!.TrimEnd((char)47) == patron
            && r.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(metodo));

        Assert.Equal(8L * 1024, Endpoint("/api/v1/auth/google", "POST").Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize);
        Assert.Equal(16L * 1024, Endpoint("/api/v1/auth/google/completar", "POST").Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize);
        Assert.Equal(8L * 1024, Endpoint("/api/v1/mi-cuenta/google", "POST").Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize);
        Assert.Equal(4L * 1024, Endpoint("/api/v1/mi-cuenta/google", "DELETE").Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize);
        // Con su propia politica: pedir nonces no gasta el cupo del ingreso con contraseña.
        Assert.Equal("IngresoGoogle", Endpoint("/api/v1/auth/google", "POST").Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
        Assert.Equal("IngresoGoogle", Endpoint("/api/v1/auth/google/nonce", "GET").Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
    }

    // ── Google falla, tarda o no esta configurado ──

    [Fact]
    public async Task SiGoogleNoResponde_Da503_YNoCreaNada()
    {
        var (sub, correo) = (NuevoSub(), NuevoCorreo());
        var nonce = await NonceAsync(api.Cliente());
        var idToken = api.Google.RegistrarGoogleNoDisponible();

        var respuesta = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.ServiceUnavailable);

        Assert.Equal("google_no_disponible", Codigo(respuesta));
        Assert.Equal(0, await ClientesConCorreoAsync(correo));
        Assert.Equal(0, await IdentidadesAsync(sub));
    }

    [Fact]
    public async Task SiLaLibreriaDeGoogleLanzaUnError_Da503_SinFiltrarElDetalle()
    {
        var nonce = await NonceAsync(api.Cliente());
        var idToken = api.Google.RegistrarFallo(new HttpRequestException("detalle-interno-de-la-red"));

        var respuesta = await IngresarAsync(api.Cliente(), idToken, nonce);
        var texto = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal("google_no_disponible", Codigo(JsonDocument.Parse(texto).RootElement));
        Assert.DoesNotContain("detalle-interno-de-la-red", texto);
    }

    [Fact]
    public async Task SiGoogleTarda_LaRespuestaSale503EnMenosDe5Segundos_YNoBloqueaOtrasPeticiones()
    {
        var nonce = await NonceAsync(api.Cliente());
        var lento = api.Google.RegistrarLento(TimeSpan.FromSeconds(30), VerificadorGoogleSimulado.Claims(nonce, NuevoSub(), NuevoCorreo()));
        var reloj = Stopwatch.StartNew();

        var espera = IngresarAsync(api.Cliente(), lento, nonce);
        // Mientras tanto, la API sigue atendiendo a los demas.
        var inicioOtra = reloj.Elapsed;
        await api.Cliente().GetAsync($"{Ruta}/nonce").EsperarAsync(HttpStatusCode.OK);
        Assert.True(reloj.Elapsed - inicioOtra < TimeSpan.FromSeconds(2), "Otra peticion tuvo que esperar a Google.");

        var respuesta = await espera.EsperarAsync(HttpStatusCode.ServiceUnavailable);
        reloj.Stop();
        Assert.Equal("google_no_disponible", Codigo(respuesta));
        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(5), $"Tardo {reloj.Elapsed.TotalSeconds:F1} s.");
    }

    // El verificador REAL (Google.Apis.Auth), sin el simulado: un texto que ni siquiera es un JWT se rechaza al leerlo, sin pedir nada a Google.
    [Theory]
    [InlineData("esto-no-es-un-jwt")]
    [InlineData("a.b.c")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIn0.")]
    public async Task ElVerificadorReal_RechazaUnTokenMalFormado_SinLlamarARed(string idToken)
    {
        await using var real = api.ConVariable("Google__ClientId", "otro-id-inventado.apps.googleusercontent.com");
        var cliente = real.Cliente();
        var nonce = await NonceAsync(cliente);

        var respuesta = await IngresarAsync(cliente, idToken, nonce).EsperarAsync(HttpStatusCode.Unauthorized);

        Assert.Equal("google_token_invalido", Codigo(respuesta));
    }

    // Un verificador que no atiende la cancelacion (una descarga colgada): el limite de espera de la API lo corta igual.
    [Fact]
    public async Task SiElVerificadorIgnoraLaCancelacion_LaEsperaSeCortaIgualEn503()
    {
        var nonce = await NonceAsync(api.Cliente());
        var colgado = api.Google.RegistrarSinCancelacion(VerificadorGoogleSimulado.Claims(nonce, NuevoSub(), NuevoCorreo()));
        var reloj = Stopwatch.StartNew();

        var respuesta = await IngresarAsync(api.Cliente(), colgado, nonce).EsperarAsync(HttpStatusCode.ServiceUnavailable);

        Assert.Equal("google_no_disponible", Codigo(respuesta));
        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(5), $"Tardo {reloj.Elapsed.TotalSeconds:F1} s.");
    }

    [Fact]
    public async Task SinElIdDeClienteConfigurado_ElIngresoDa503_YNoLlamaAGoogle()
    {
        // Un espacio: una variable vacia se borra y dejaria ver el ID de los User Secrets de quien ejecuta las pruebas.
        await using var sinGoogle = api.ConVariable("Google__ClientId", " ");
        var cliente = sinGoogle.Cliente();
        var llamadas = api.Google.Llamadas;

        var respuesta = await cliente.PostAsJsonAsync(Ruta, new { idToken = "cualquiera", nonce = "cualquiera" }).EsperarAsync(HttpStatusCode.ServiceUnavailable);
        var completar = await cliente.PostAsJsonAsync($"{Ruta}/completar", Completar("pase", "912345678")).EsperarAsync(HttpStatusCode.ServiceUnavailable);

        Assert.Equal("google_no_configurado", Codigo(respuesta));
        Assert.Equal("google_no_configurado", Codigo(completar));
        Assert.Equal(llamadas, api.Google.Llamadas);
    }

    // ── Completar el registro ──

    [Fact]
    public async Task CompletarConUnTelefonoOUnRutYaConocidos_DaElMismoMensajeDeSiempre()
    {
        var conocido = await CuentaConContrasenaAsync();
        var telefonoConocido = await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == conocido.Id).Select(c => c.Telefono).SingleAsync());
        var rut = api.Rut();
        await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == conocido.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Rut, rut)));

        async Task<JsonElement> IntentarAsync(string telefono, string? rutEscrito)
        {
            var (sub, correo) = (NuevoSub(), NuevoCorreo());
            var (idToken, nonce) = await TokenAsync(sub, correo);
            var pendiente = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.OK);
            var respuesta = await api.Cliente().PostAsJsonAsync($"{Ruta}/completar", Completar(pendiente.GetProperty("pase").GetString()!, telefono, rut: rutEscrito))
                .EsperarAsync(HttpStatusCode.Conflict);
            Assert.Equal(0, await ClientesConCorreoAsync(correo));
            Assert.Equal(0, await IdentidadesAsync(sub));
            return respuesta;
        }

        var porTelefono = await IntentarAsync(telefonoConocido, null);
        var porRut = await IntentarAsync(api.Telefono(), rut);

        Assert.Equal(ServicioMiCuentaMensajes.TelefonoDelTaller, porTelefono.GetProperty("error").GetString());
        Assert.Equal(porTelefono.SinTraza(), porRut.SinTraza());
    }

    [Fact]
    public async Task CompletarConUnaVersionDeTerminosDesactualizada_Da409_NoCreaNada_YElPaseSigueSirviendoParaCorregir()
    {
        var (sub, correo) = (NuevoSub(), NuevoCorreo());
        var (idToken, nonce) = await TokenAsync(sub, correo);
        var pase = (await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.OK)).GetProperty("pase").GetString()!;

        await api.Cliente().PostAsJsonAsync($"{Ruta}/completar", Completar(pase, api.Telefono(), versionTerminos: "0.9")).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal(0, await ClientesConCorreoAsync(correo));

        await api.Cliente().PostAsJsonAsync($"{Ruta}/completar", Completar(pase, api.Telefono())).EsperarAsync(HttpStatusCode.Created);
        Assert.Equal(1, await ClientesConCorreoAsync(correo));
    }

    [Fact]
    public async Task CompletarSinLosDatosOSinAceptarLosTerminos_Da400PorCampo_NoCreaNada_YElPaseSigueSirviendo()
    {
        var (sub, correo) = (NuevoSub(), NuevoCorreo());
        var (idToken, nonce) = await TokenAsync(sub, correo);
        var pase = (await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.OK)).GetProperty("pase").GetString()!;

        var errores = (await api.Cliente().PostAsJsonAsync($"{Ruta}/completar", Completar(pase, telefono: null, acepta: false)).EsperarAsync(HttpStatusCode.BadRequest))
            .GetProperty("errores");

        Assert.True(errores.TryGetProperty("telefono", out _));
        Assert.True(errores.TryGetProperty("aceptaTerminosYPrivacidad", out _));
        Assert.False(errores.TryGetProperty("password", out _));
        Assert.Equal(0, await ClientesConCorreoAsync(correo));
        await api.Cliente().PostAsJsonAsync($"{Ruta}/completar", Completar(pase, api.Telefono())).EsperarAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ElPase_Manipulado_Vencido_Ajeno_OReutilizado_NoSirve()
    {
        var (sub, correo) = (NuevoSub(), NuevoCorreo());
        var (idToken, nonce) = await TokenAsync(sub, correo);
        var pase = (await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.OK)).GetProperty("pase").GetString()!;

        async Task AssertPaseInvalidoAsync(HttpClient cliente, string texto)
        {
            var respuesta = await cliente.PostAsJsonAsync($"{Ruta}/completar", Completar(texto, api.Telefono())).EsperarAsync(HttpStatusCode.Unauthorized);
            Assert.Equal("google_pase_invalido", Codigo(respuesta));
        }

        var partes = pase.Split('.');
        await AssertPaseInvalidoAsync(api.Cliente(), $"{partes[0][..^2]}AA.{partes[1]}");
        await AssertPaseInvalidoAsync(api.Cliente(), partes[0]);
        await AssertPaseInvalidoAsync(api.Cliente(), "inventado");
        // Vencido: la API con el reloj 11 minutos adelantado lo ve vencido (misma clave, otro reloj).
        await AssertPaseInvalidoAsync(api.ClienteConReloj(TimeSpan.FromMinutes(11)), pase);
        Assert.Equal(0, await ClientesConCorreoAsync(correo));

        // Reutilizado: el primero lo usa, el segundo no.
        await api.Cliente().PostAsJsonAsync($"{Ruta}/completar", Completar(pase, api.Telefono())).EsperarAsync(HttpStatusCode.Created);
        await AssertPaseInvalidoAsync(api.Cliente(), pase);
        Assert.Equal(1, await ClientesConCorreoAsync(correo));
        Assert.Equal(1, await IdentidadesAsync(sub));
    }

    [Fact]
    public async Task SiFallaElGuardadoDeUnConsentimiento_NoQuedaNiLaCuentaNiLaIdentidad()
    {
        var cliente = api.ClienteConInterceptor(new FallaAlGuardarConsentimientos(), "sin-uso");
        var (sub, correo) = (NuevoSub(), NuevoCorreo());
        var (idToken, nonce) = await TokenAsync(sub, correo, cliente: cliente);
        var pase = (await IngresarAsync(cliente, idToken, nonce).EsperarAsync(HttpStatusCode.OK)).GetProperty("pase").GetString()!;

        using var respuesta = await cliente.PostAsJsonAsync($"{Ruta}/completar", Completar(pase, api.Telefono()));

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal(0, await ClientesConCorreoAsync(correo));
        Assert.Equal(0, await IdentidadesAsync(sub));
    }

    private sealed class FallaAlGuardarConsentimientos : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<Consentimiento>().Any(e => e.State == EntityState.Added)
                ? throw new InvalidOperationException("falla simulada al guardar un consentimiento")
                : base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    [Fact]
    public async Task DosCompletarSimultaneosConElMismoPase_CreanUnaSolaCuenta()
    {
        var (sub, correo) = (NuevoSub(), NuevoCorreo());
        var (idToken, nonce) = await TokenAsync(sub, correo);
        var pase = (await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.OK)).GetProperty("pase").GetString()!;

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            using var r = await api.Cliente().PostAsJsonAsync($"{Ruta}/completar", Completar(pase, api.Telefono()));
            return r.StatusCode;
        }));

        Assert.Equal(1, respuestas.Count(r => r == HttpStatusCode.Created));
        // La otra se rechaza con un error de negocio (pase ya usado o cuenta que ya existe), nunca con un 500.
        Assert.All(respuestas.Where(r => r != HttpStatusCode.Created), r => Assert.True(r is HttpStatusCode.Unauthorized or HttpStatusCode.Conflict, $"Llego {(int)r}."));
        Assert.Equal(1, await ClientesConCorreoAsync(correo));
        Assert.Equal(1, await IdentidadesAsync(sub));
    }

    // ── Vincular y desvincular desde Mi cuenta ──

    [Fact]
    public async Task Vincular_ConLaContrasenaYElMismoCorreo_Funciona_QuedaEnLaBitacora_YDespuesSePuedeIngresarConGoogle()
    {
        var cuenta = await CuentaConContrasenaAsync();
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, cuenta.Correo);

        var respuesta = await Vincular(Con(cuenta.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);

        Assert.True(respuesta.GetProperty("vinculado").GetBoolean());
        Assert.Equal(cuenta.Correo, respuesta.GetProperty("correo").GetString());
        Assert.Equal(cuenta.Id, await api.ConsultarAsync(db => db.IdentidadesExternas.Where(i => i.Sub == sub).Select(i => i.IdCliente).SingleAsync()));
        var miCuenta = await Con(cuenta.Token).GetAsync("/api/v1/mi-cuenta").EsperarAsync(HttpStatusCode.OK);
        Assert.True(miCuenta.GetProperty("googleVinculado").GetBoolean());
        Assert.True(miCuenta.GetProperty("tieneContrasena").GetBoolean());

        var fila = await api.ConsultarAsync(db => db.Auditoria.AsNoTracking()
            .Where(a => a.Accion == "vincular_google" && a.IdEntidad == cuenta.Id.ToString()).SingleAsync());
        Assert.Equal(cuenta.Id, fila.IdCliente);
        Assert.Equal("cliente", fila.Rol);
        Assert.Equal("cuenta", fila.Entidad);
        Assert.DoesNotContain(sub, fila.Detalle ?? "");

        var (tokenDeIngreso, nonceDeIngreso) = await TokenAsync(sub, cuenta.Correo);
        var sesion = await IngresarAsync(api.Cliente(), tokenDeIngreso, nonceDeIngreso).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(cuenta.Id, sesion.GetProperty("usuario").GetProperty("idCliente").GetInt32());
    }

    [Fact]
    public async Task Vincular_ConLaContrasenaIncorrecta_Da400_YNoVinculaNada()
    {
        var cuenta = await CuentaConContrasenaAsync();
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, cuenta.Correo);

        var respuesta = await Vincular(Con(cuenta.Token), idToken, nonce, "contraseña-equivocada").EsperarAsync(HttpStatusCode.BadRequest);

        Assert.True(respuesta.GetProperty("errores").TryGetProperty("passwordActual", out _));
        Assert.Equal(0, await IdentidadesAsync(sub));
        await Vincular(Con(cuenta.Token), idToken, nonce, null).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Vincular_UnGoogleConOtroCorreo_Da409_YNoVincula()
    {
        var cuenta = await CuentaConContrasenaAsync();
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, NuevoCorreo());

        var respuesta = await Vincular(Con(cuenta.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal("google_correo_distinto", Codigo(respuesta));
        Assert.Equal(0, await IdentidadesAsync(sub));
    }

    [Fact]
    public async Task Vincular_ConUnTokenQueNoCumple_Da400_ConSuCodigo()
    {
        var cuenta = await CuentaConContrasenaAsync();
        var (idToken, nonce) = await TokenAsync(NuevoSub(), cuenta.Correo, c => c with { CorreoVerificado = false });

        var respuesta = await Vincular(Con(cuenta.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Equal("google_token_invalido", Codigo(respuesta));
    }

    [Fact]
    public async Task Vincular_UnGoogleYaVinculadoAOtraCuenta_Da409()
    {
        var duena = await CrearCuentaAsync();
        var otra = await CuentaConContrasenaAsync();
        var (idToken, nonce) = await TokenAsync(duena.Sub, otra.Correo);

        var respuesta = await Vincular(Con(otra.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal("google_en_uso", Codigo(respuesta));
        Assert.Equal(1, await IdentidadesAsync(duena.Sub));
    }

    [Fact]
    public async Task Vincular_UnaCuentaQueYaTieneGoogle_Da409()
    {
        var cuenta = await CuentaConContrasenaAsync();
        var (primero, nonce1) = await TokenAsync(NuevoSub(), cuenta.Correo);
        await Vincular(Con(cuenta.Token), primero, nonce1, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);
        var otroSub = NuevoSub();
        var (segundo, nonce2) = await TokenAsync(otroSub, cuenta.Correo);

        var respuesta = await Vincular(Con(cuenta.Token), segundo, nonce2, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal("google_ya_vinculada", Codigo(respuesta));
        Assert.Equal(0, await IdentidadesAsync(otroSub));
    }

    [Fact]
    public async Task Desvincular_ConLaContrasena_QuitaElVinculo_QuedaEnLaBitacora_YElCorreoVuelveAPedirLaContrasena()
    {
        var cuenta = await CuentaConContrasenaAsync();
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, cuenta.Correo);
        await Vincular(Con(cuenta.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);

        using var respuesta = await Desvincular(Con(cuenta.Token), ApiFixture.AdminPassword);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(0, await IdentidadesAsync(sub));
        Assert.Equal(1, await api.ConsultarAsync(db => db.Auditoria.CountAsync(a => a.Accion == "desvincular_google" && a.IdEntidad == cuenta.Id.ToString() && a.IdCliente == cuenta.Id)));
        // Ya no es una identidad conocida: el correo tiene una cuenta con contraseña, asi que no se fusiona.
        var (tokenNuevo, nonceNuevo) = await TokenAsync(sub, cuenta.Correo);
        Assert.Equal("cuenta_existente", Codigo(await IngresarAsync(api.Cliente(), tokenNuevo, nonceNuevo).EsperarAsync(HttpStatusCode.Conflict)));
    }

    [Fact]
    public async Task Desvincular_ConLaContrasenaIncorrecta_Da400_YSigueVinculada()
    {
        var cuenta = await CuentaConContrasenaAsync();
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, cuenta.Correo);
        await Vincular(Con(cuenta.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);

        using var respuesta = await Desvincular(Con(cuenta.Token), "contraseña-equivocada");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True(JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement.GetProperty("errores").TryGetProperty("passwordActual", out _));
        Assert.Equal(1, await IdentidadesAsync(sub));
    }

    [Fact]
    public async Task Desvincular_UnaCuentaSinContrasena_Da409_PorqueQuedariaSinForma_DeEntrar()
    {
        var cuenta = await CrearCuentaAsync();

        using var respuesta = await Desvincular(Con(cuenta.Token), ApiFixture.AdminPassword);
        var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("sin_contrasena", Codigo(cuerpo));
        Assert.Equal(1, await IdentidadesAsync(cuenta.Sub));
    }

    [Fact]
    public async Task Desvincular_SinTenerGoogleVinculado_Da404()
    {
        var cuenta = await CuentaConContrasenaAsync();
        using var respuesta = await Desvincular(Con(cuenta.Token), ApiFixture.AdminPassword);
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task ElPersonalYElVisitante_NoPuedenVincularNiDesvincular()
    {
        var (idToken, nonce) = await TokenAsync(NuevoSub(), NuevoCorreo());
        foreach (var token in new[] { await api.TokenAdminAsync(), await api.TokenEmpleadoAsync("operario") })
        {
            using var vincular = await Vincular(Con(token), idToken, nonce, ApiFixture.AdminPassword);
            using var desvincular = await Desvincular(Con(token), ApiFixture.AdminPassword);
            Assert.Equal(HttpStatusCode.Forbidden, vincular.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, desvincular.StatusCode);
        }

        using var visitanteVincula = await Vincular(api.Cliente(), idToken, nonce, ApiFixture.AdminPassword);
        using var visitanteDesvincula = await Desvincular(api.Cliente(), ApiFixture.AdminPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, visitanteVincula.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, visitanteDesvincula.StatusCode);
    }

    [Fact]
    public async Task LaFichaDelCliente_ParaElPersonal_MuestraConCuentaWebAUnaCuentaDeGoogle_AunqueNoTengaContrasena()
    {
        var cuenta = await CrearCuentaAsync();

        var ficha = await Con(await api.TokenAdminAsync()).GetAsync($"/api/v1/clientes/{cuenta.Id}").EsperarAsync(HttpStatusCode.OK);

        Assert.True(ficha.GetProperty("registrado").GetBoolean());
        Assert.True(ficha.GetProperty("correoConfirmado").GetBoolean());
    }

    [Fact]
    public async Task ElPersonal_NoEntraPorGoogle_SiNoVinculoSuCuenta_AunqueSuCorreoSeaElDeGoogle()
    {
        var (_, correoDelPersonal, _) = await api.CrearEmpleadoAsync("operario");
        var (idToken, nonce) = await TokenAsync(NuevoSub(), correoDelPersonal);

        // Fase 14C: el personal SI entra por Google, pero solo despues de vincularlo desde Mi perfil. Un correo de empleado sin vincular
        // no se fusiona ni da sesion, y tampoco se ofrece crear una cuenta de cliente con el (antes se trataba como una persona nueva).
        using var respuesta = await IngresarAsync(api.Cliente(), idToken, nonce);
        var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("cuenta_personal_existente", cuerpo.GetProperty("codigo").GetString());
        Assert.False(cuerpo.TryGetProperty("token", out _));
        Assert.False(cuerpo.TryGetProperty("pase", out _));
    }

    // ── Una cuenta sin contraseña puede crear una (la recuperacion que ya existe) ──

    [GeneratedRegex(@"\b(\d{6})\b")]
    private static partial Regex CodigoDeSeisDigitos();

    [Fact]
    public async Task UnaCuentaDeGoogle_PuedeCrearUnaContrasena_ConElCodigoDeRecuperacion_YDespuesDesvincular()
    {
        var cuenta = await CrearCuentaAsync();
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/recuperar", new { tipo = "cliente", correo = cuenta.Correo }).EsperarAsync(HttpStatusCode.Accepted);
        var cuerpo = await api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking()
            .Where(c => c.Destinatarios == cuenta.Correo && c.Tipo == "recuperacion_cuenta").OrderByDescending(c => c.IdCorreo).Select(c => c.CuerpoTexto).FirstAsync());
        var codigo = CodigoDeSeisDigitos().Match(cuerpo).Groups[1].Value;
        const string nueva = "una-contraseña-nueva-123";

        await api.Cliente().PostAsJsonAsync("/api/v1/auth/restablecer", new { tipo = "cliente", correo = cuenta.Correo, codigo, password = nueva })
            .EsperarAsync(HttpStatusCode.NoContent);

        var token = await api.LoginClienteAsync(cuenta.Correo, nueva);
        var miCuenta = await Con(token).GetAsync("/api/v1/mi-cuenta").EsperarAsync(HttpStatusCode.OK);
        Assert.True(miCuenta.GetProperty("tieneContrasena").GetBoolean());
        Assert.True(miCuenta.GetProperty("googleVinculado").GetBoolean());
        using var desvinculada = await Desvincular(Con(token), nueva);
        Assert.Equal(HttpStatusCode.NoContent, desvinculada.StatusCode);
    }

    [Fact]
    public async Task DarDeBajaLaCuenta_LiberaElGoogleVinculado()
    {
        var cuenta = await CuentaConContrasenaAsync();
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, cuenta.Correo);
        await Vincular(Con(cuenta.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);

        await Con(cuenta.Token).PostAsJsonAsync("/api/v1/mi-cuenta/baja", new { password = ApiFixture.AdminPassword, confirmacion = "DAR DE BAJA" })
            .EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(0, await IdentidadesAsync(sub));
    }

    // ── La base ──

    [Fact]
    public async Task LaBase_ImpideUnSubEnDosCuentas_UnaCuentaConDosGoogle_YUnProveedorDesconocido()
    {
        var a = await CuentaConContrasenaAsync();
        var b = await CuentaConContrasenaAsync();
        var sub = NuevoSub();
        var ahora = DateTimeOffset.UtcNow;
        await api.ConsultarAsync(async db =>
        {
            db.IdentidadesExternas.Add(new IdentidadExterna { IdCliente = a.Id, Sub = sub, Correo = a.Correo, FechaVinculacion = ahora });
            await db.SaveChangesAsync();
            return 0;
        });

        // El mismo sub en otra cuenta.
        await Assert.ThrowsAsync<DbUpdateException>(() => api.ConsultarAsync(async db =>
        {
            db.IdentidadesExternas.Add(new IdentidadExterna { IdCliente = b.Id, Sub = sub, Correo = b.Correo, FechaVinculacion = ahora });
            await db.SaveChangesAsync();
            return 0;
        }));
        // Otro sub en la misma cuenta.
        await Assert.ThrowsAsync<DbUpdateException>(() => api.ConsultarAsync(async db =>
        {
            db.IdentidadesExternas.Add(new IdentidadExterna { IdCliente = a.Id, Sub = NuevoSub(), Correo = a.Correo, FechaVinculacion = ahora });
            await db.SaveChangesAsync();
            return 0;
        }));
        // Un proveedor que la base no conoce.
        await Assert.ThrowsAsync<DbUpdateException>(() => api.ConsultarAsync(async db =>
        {
            db.IdentidadesExternas.Add(new IdentidadExterna { IdCliente = b.Id, Proveedor = "facebook", Sub = NuevoSub(), Correo = b.Correo, FechaVinculacion = ahora });
            await db.SaveChangesAsync();
            return 0;
        }));
    }

    // ── Bitacora y secretos ──

    [Fact]
    public async Task ElIngreso_QuedaEnLaBitacora_ConQuienYCuando_SinElTokenNiElSub()
    {
        var cuenta = await CrearCuentaAsync();
        var (idToken, nonce) = await TokenAsync(cuenta.Sub, cuenta.Correo);
        await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.OK);

        var filas = await api.ConsultarAsync(db => db.Auditoria.AsNoTracking()
            .Where(a => a.Accion == "ingreso_google" && a.IdEntidad == cuenta.Id.ToString()).OrderBy(a => a.IdAuditoria).ToListAsync());

        // Uno por el alta de la cuenta y otro por el ingreso repetido.
        Assert.Equal(2, filas.Count);
        Assert.All(filas, f =>
        {
            Assert.Equal(cuenta.Id, f.IdCliente);
            Assert.Equal("cliente", f.Rol);
            Assert.Equal("cuenta", f.Entidad);
            Assert.True(f.Fecha > DateTimeOffset.UtcNow.AddMinutes(-5));
            Assert.DoesNotContain(cuenta.Sub, f.Detalle ?? "");
            Assert.DoesNotContain(idToken, f.Detalle ?? "");
        });
        Assert.Contains("nueva", filas[0].Detalle);
        // Vincular y desvincular se anotan como cambios de la entidad (sin el sub, que es sensible).
        var todas = await api.ConsultarAsync(db => db.Auditoria.AsNoTracking().Where(a => a.Entidad == "identidad_externa").Select(a => a.Detalle ?? "").ToListAsync());
        Assert.DoesNotContain(todas, d => d.Contains(cuenta.Sub));
    }

    [Fact]
    public async Task NiElToken_NiElNonce_NiElPase_AparecenEnLosRegistros_NiEnLasRespuestasDeError()
    {
        var logs = new LogsEnMemoria();
        var cliente = api.ClienteConLogs(logs);
        var secretos = new List<string>();

        // Un ingreso que se completa, uno rechazado, uno con Google caido y uno que lanza un error: nada de eso debe dejar rastro.
        var (sub, correo) = (NuevoSub(), NuevoCorreo());
        var (idToken, nonce) = await TokenAsync(sub, correo, cliente: cliente);
        var pendiente = await IngresarAsync(cliente, idToken, nonce).EsperarAsync(HttpStatusCode.OK);
        var pase = pendiente.GetProperty("pase").GetString()!;
        secretos.AddRange([idToken, nonce, pase]);
        await cliente.PostAsJsonAsync($"{Ruta}/completar", Completar(pase, api.Telefono())).EsperarAsync(HttpStatusCode.Created);

        var respuestas = new List<string>();
        var (malo, nonceMalo) = await TokenAsync(NuevoSub(), NuevoCorreo(), c => c with { CorreoVerificado = false }, cliente);
        secretos.AddRange([malo, nonceMalo]);
        respuestas.Add(await (await IngresarAsync(cliente, malo, nonceMalo)).Content.ReadAsStringAsync());
        var nonceCaido = await NonceAsync(cliente);
        var caido = api.Google.RegistrarGoogleNoDisponible();
        secretos.AddRange([caido, nonceCaido]);
        respuestas.Add(await (await IngresarAsync(cliente, caido, nonceCaido)).Content.ReadAsStringAsync());
        var nonceRoto = await NonceAsync(cliente);
        var roto = api.Google.RegistrarFallo(new InvalidOperationException($"fallo con {nonceRoto}"));
        secretos.AddRange([roto, nonceRoto]);
        respuestas.Add(await (await IngresarAsync(cliente, roto, nonceRoto)).Content.ReadAsStringAsync());
        respuestas.Add(await (await cliente.PostAsJsonAsync($"{Ruta}/completar", Completar(pase, api.Telefono()))).Content.ReadAsStringAsync());

        foreach (var secreto in secretos.Where(s => s.Length > 8))
        {
            Assert.DoesNotContain(logs.Mensajes, m => m.Contains(secreto));
            Assert.DoesNotContain(respuestas, r => r.Contains(secreto));
        }
        Assert.DoesNotContain(logs.Mensajes, m => m.Contains(sub));
    }

    private sealed class LogsEnMemoria : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> mensajes = new();

        public IReadOnlyCollection<string> Mensajes => mensajes;

        public ILogger CreateLogger(string categoryName) => new Registro(mensajes);

        public void Dispose() { }

        private sealed class Registro(ConcurrentQueue<string> mensajes) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                mensajes.Enqueue($"{formatter(state, exception)} {exception}");
        }
    }
}

// El mensaje que el registro por formulario ya da cuando el telefono o el RUT son conocidos (no se repite aqui: se comprueba que sea el mismo).
internal static class ServicioMiCuentaMensajes
{
    public const string TelefonoDelTaller = LetrerosHerreraSYNCTIMEapi.Services.ServicioMiCuenta.MensajeTelefonoDelTaller;
}
