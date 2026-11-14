using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 6: cuentas y sesion (RFS03, RFU04, RFU05, RNFP06-08, RNFP13, RNFE01, E01, E02, E23).
[Collection(ApiCollection.Name)]
public sealed partial class CuentasYSesionTests(ApiFixture api)
{
    // ── Utilidades: cookies de la sesion ──

    private sealed record Cookies(string Renovacion, string Csrf);

    private static Cookies LeerCookies(HttpResponseMessage respuesta)
    {
        var valores = respuesta.Headers.TryGetValues("Set-Cookie", out var lista) ? lista.ToList() : [];
        string Valor(string nombre) => valores.Select(v => v.Split(';')[0]).First(v => v.StartsWith(nombre + "=")).Split('=', 2)[1];
        return new Cookies(Valor("synctime_renovacion"), Valor("synctime_csrf"));
    }

    private Task<HttpResponseMessage> RenovarAsync(Cookies cookies, bool conCsrf = true)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/renovar");
        peticion.Headers.Add("Cookie", $"synctime_renovacion={cookies.Renovacion}; synctime_csrf={cookies.Csrf}");
        if (conCsrf)
            peticion.Headers.Add("X-CSRF-Token", cookies.Csrf);
        return api.Cliente().SendAsync(peticion);
    }

    private async Task<(int Id, string Correo, Cookies Cookies, string Token)> RegistrarAsync(bool marketing = false)
    {
        var telefono = api.Telefono();
        var respuesta = await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", api.Registro(telefono, marketing: marketing));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
        return (cuerpo.GetProperty("usuario").GetProperty("idCliente").GetInt32(), ApiFixture.CorreoDe(telefono), LeerCookies(respuesta),
            cuerpo.GetProperty("token").GetString()!);
    }

    private Task<string> UltimoCorreoAsync(string para, string tipo) => api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking()
        .Where(c => c.Destinatarios == para && c.Tipo == tipo).OrderByDescending(c => c.IdCorreo).Select(c => c.CuerpoTexto).FirstAsync());

    [GeneratedRegex(@"token=([A-Za-z0-9_\-]+)")]
    private static partial Regex EnlaceVerificacion();

    [GeneratedRegex(@"\b(\d{6})\b")]
    private static partial Regex CodigoDeSeisDigitos();

    // ── Registro (RFU04, E01, E23) ──

    [Fact]
    public async Task ElRegistro_CreaLaCuentaConSusConsentimientos_EnviaLaVerificacion_YAbreLaSesion()
    {
        var telefono = api.Telefono();
        var respuesta = await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", api.Registro(telefono, marketing: true));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cookies = respuesta.Headers.GetValues("Set-Cookie").ToList();
        var renovacion = cookies.Single(c => c.StartsWith("synctime_renovacion="));
        Assert.Contains("httponly", renovacion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", renovacion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", renovacion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", renovacion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("httponly", cookies.Single(c => c.StartsWith("synctime_csrf=")), StringComparison.OrdinalIgnoreCase);

        var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
        var id = cuerpo.GetProperty("usuario").GetProperty("idCliente").GetInt32();
        Assert.False(cuerpo.GetProperty("usuario").GetProperty("correoVerificado").GetBoolean());
        var tipos = await api.ConsultarAsync(db => db.Consentimientos.Where(c => c.IdCliente == id).Select(c => c.Tipo).OrderBy(t => t).ToListAsync());
        Assert.Equal(["marketing", "privacidad", "terminos"], tipos);
        Assert.Matches(EnlaceVerificacion(), await UltimoCorreoAsync(ApiFixture.CorreoDe(telefono), "verificacion_correo"));
        // El token de acceso es corto (RNFP08): 15 minutos.
        var token = new JwtSecurityTokenHandler().ReadJwtToken(cuerpo.GetProperty("token").GetString());
        Assert.InRange((token.ValidTo - DateTime.UtcNow).TotalMinutes, 14, 15.5);
    }

    [Fact]
    public async Task ElRegistro_SinElMarketing_NoLoRegistra_YSinAceptarLosTerminosNoCreaNada()
    {
        var (id, _, _, _) = await RegistrarAsync();
        Assert.False(await api.ConsultarAsync(db => db.Consentimientos.AnyAsync(c => c.IdCliente == id && c.Tipo == "marketing")));

        var telefono = api.Telefono();
        var sinAceptar = new
        {
            nombre = "A", apellido = "B", telefono, correo = ApiFixture.CorreoDe(telefono), password = ApiFixture.AdminPassword,
            aceptaTerminosYPrivacidad = false, versionTerminos = "1.0", versionPrivacidad = "1.0"
        };
        var error = await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", sinAceptar).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(error.GetProperty("errores").TryGetProperty("aceptaTerminosYPrivacidad", out _));
        Assert.False(await api.ConsultarAsync(db => db.Clientes.AnyAsync(c => c.Telefono == telefono)));
    }

    [Fact]
    public async Task ElRegistro_RechazaDuplicadosYDatosInvalidos_ConMensajesClaros()
    {
        var (_, correo, _, _) = await RegistrarAsync();

        var correoRepetido = await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", api.Registro(api.Telefono(), correo: correo.ToUpperInvariant()))
            .EsperarAsync(HttpStatusCode.Conflict);
        Assert.Contains("correo", correoRepetido.GetProperty("error").GetString());

        // Un RUT valido y unico en esta base de pruebas: se arma con el digito verificador correcto.
        var rut = RutValidoNuevo();
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", api.Registro(api.Telefono(), rut: rut)).EsperarAsync(HttpStatusCode.Created);
        var rutRepetido = await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", api.Registro(api.Telefono(), rut: rut.Replace(".", "")))
            .EsperarAsync(HttpStatusCode.Conflict);
        Assert.Contains("RUT", rutRepetido.GetProperty("error").GetString());

        var rutMalo = await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", api.Registro(api.Telefono(), rut: "12.345.678-9"))
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(rutMalo.GetProperty("errores").TryGetProperty("rut", out _));

        var telefonoMalo = await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", api.Registro("123"))
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(telefonoMalo.GetProperty("errores").TryGetProperty("telefono", out _));

        var version = new
        {
            nombre = "A", apellido = "B", telefono = api.Telefono(), correo = $"v{api.Siguiente()}@correo.test", password = ApiFixture.AdminPassword,
            aceptaTerminosYPrivacidad = true, versionTerminos = "0.9", versionPrivacidad = "1.0"
        };
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", version).EsperarAsync(HttpStatusCode.Conflict);
    }

    // RUT con digito verificador correcto (modulo 11), en un rango alto para no chocar con otras pruebas.
    private string RutValidoNuevo()
    {
        var numero = 30_000_000 + api.Siguiente();
        var suma = 0;
        var factor = 2;
        for (var n = numero; n > 0; n /= 10)
        {
            suma += n % 10 * factor;
            factor = factor == 7 ? 2 : factor + 1;
        }
        var resto = 11 - suma % 11;
        var dv = resto == 11 ? "0" : resto == 10 ? "K" : resto.ToString();
        return $"{numero:N0}".Replace(",", ".") + "-" + dv;
    }

    // ── Ingreso con correo (RFS03, RFU05) ──

    [Fact]
    public async Task ElCliente_IngresaConSuCorreo_NoConSuTelefono()
    {
        var (_, correo, _, _) = await RegistrarAsync();

        var ingreso = await api.Cliente().PostAsJsonAsync("/api/v1/auth/login-cliente", new { correo = $"  {correo.ToUpperInvariant()} ", password = ApiFixture.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);
        Assert.False(string.IsNullOrEmpty(LeerCookies(ingreso).Renovacion));

        await api.Cliente().PostAsJsonAsync("/api/v1/auth/login-cliente", new { correo, password = "otra-clave-larga" }).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    // ── Renovacion (RNFP08) ──

    [Fact]
    public async Task UnaSesion_SeRenueva_ConUnaCookieNueva_YLaAnteriorYaNoSirve()
    {
        var (_, _, cookies, _) = await RegistrarAsync();

        using var renovada = await RenovarAsync(cookies);
        Assert.Equal(HttpStatusCode.OK, renovada.StatusCode);
        var nuevas = LeerCookies(renovada);
        Assert.NotEqual(cookies.Renovacion, nuevas.Renovacion);
        var cuerpo = JsonDocument.Parse(await renovada.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("cliente", cuerpo.GetProperty("tipo").GetString());
        await api.Cliente(cuerpo.GetProperty("token").GetString()).GetAsync("/api/v1/mi-cuenta").EsperarAsync(HttpStatusCode.OK);

        // Dentro de unos segundos, la misma cookie vieja es otra pestaña renovando a la vez: se pide reintentar.
        using var simultanea = await RenovarAsync(cookies);
        Assert.Equal(HttpStatusCode.Conflict, simultanea.StatusCode);
        // Y la sesion sigue viva con la cookie nueva.
        using var siguiente = await RenovarAsync(nuevas);
        Assert.Equal(HttpStatusCode.OK, siguiente.StatusCode);
    }

    [Fact]
    public async Task ReutilizarUnTokenYaUsado_CierraLaSesionCompleta()
    {
        var (_, _, cookies, _) = await RegistrarAsync();
        using var renovada = await RenovarAsync(cookies);
        var nuevas = LeerCookies(renovada);

        // El token viejo se uso hace un minuto: ya no es una renovacion simultanea, es una copia.
        var hashViejo = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(cookies.Renovacion)));
        await api.ConsultarAsync(db => db.TokensRenovacion.Where(t => t.Hash == hashViejo)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.FechaUso, DateTimeOffset.UtcNow.AddMinutes(-1))));

        using var robo = await RenovarAsync(cookies);
        Assert.Equal(HttpStatusCode.Unauthorized, robo.StatusCode);
        using var legitima = await RenovarAsync(nuevas);
        Assert.Equal(HttpStatusCode.Unauthorized, legitima.StatusCode);
    }

    [Fact]
    public async Task Renovar_SinLaCabeceraCsrf_OSinCookie_NoFunciona()
    {
        var (_, _, cookies, _) = await RegistrarAsync();

        using var sinCsrf = await RenovarAsync(cookies, conCsrf: false);
        Assert.Equal(HttpStatusCode.BadRequest, sinCsrf.StatusCode);

        using var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/renovar");
        const string csrf = "0123456789abcdef0123456789abcdef";
        peticion.Headers.Add("Cookie", $"synctime_csrf={csrf}");
        peticion.Headers.Add("X-CSRF-Token", csrf);
        using var sinCookie = await api.Cliente().SendAsync(peticion);
        Assert.Equal(HttpStatusCode.Unauthorized, sinCookie.StatusCode);

        // Lo rechazado no gasto la cookie: la sesion sigue renovandose.
        using var valida = await RenovarAsync(cookies);
        Assert.Equal(HttpStatusCode.OK, valida.StatusCode);
    }

    [Fact]
    public async Task Salir_RevocaLaSesion_YUnaCuentaDesactivadaNoRenueva()
    {
        var (_, _, cookies, _) = await RegistrarAsync();
        using var salida = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/salir");
        salida.Headers.Add("Cookie", $"synctime_renovacion={cookies.Renovacion}; synctime_csrf={cookies.Csrf}");
        salida.Headers.Add("X-CSRF-Token", cookies.Csrf);
        Assert.Equal(HttpStatusCode.NoContent, (await api.Cliente().SendAsync(salida)).StatusCode);
        using var trasSalir = await RenovarAsync(cookies);
        Assert.Equal(HttpStatusCode.Unauthorized, trasSalir.StatusCode);

        var (id, _, otras, _) = await RegistrarAsync();
        await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Activo, false)));
        using var desactivada = await RenovarAsync(otras);
        Assert.Equal(HttpStatusCode.Unauthorized, desactivada.StatusCode);
    }

    [Fact]
    public async Task ElPersonal_TambienRenueva_YConservaSuRol()
    {
        var (_, email, _) = await api.CrearEmpleadoAsync(Security.Roles.Operario);
        var ingreso = await api.Cliente().PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiFixture.AdminPassword });
        var cuerpo = await RenovarAsync(LeerCookies(ingreso)).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("empleado", cuerpo.GetProperty("tipo").GetString());
        Assert.Equal(Security.Roles.Operario, cuerpo.GetProperty("usuario").GetProperty("rol").GetString());
    }

    // ── Correo verificado ──

    [Fact]
    public async Task ElEnlaceDeVerificacion_ConfirmaElCorreo_UnaSolaVez()
    {
        var (_, correo, _, token) = await RegistrarAsync();
        var enlace = EnlaceVerificacion().Match(await UltimoCorreoAsync(correo, "verificacion_correo")).Groups[1].Value;

        await api.Cliente().PostAsJsonAsync("/api/v1/auth/verificar-correo", new { token = enlace }).EsperarAsync(HttpStatusCode.OK);
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/verificar-correo", new { token = enlace }).EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/verificar-correo", new { token = "inventado" }).EsperarAsync(HttpStatusCode.BadRequest);

        var cuenta = await api.Cliente(token).GetAsync("/api/v1/mi-cuenta").EsperarAsync(HttpStatusCode.OK);
        Assert.True(cuenta.GetProperty("correoVerificado").GetBoolean());
    }

    [Fact]
    public async Task CambiarElCorreo_ExigeLaContraseña_YLoDejaSinVerificar()
    {
        var (_, correo, _, token) = await RegistrarAsync();
        var enlace = EnlaceVerificacion().Match(await UltimoCorreoAsync(correo, "verificacion_correo")).Groups[1].Value;
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/verificar-correo", new { token = enlace }).EsperarAsync(HttpStatusCode.OK);
        var nuevo = $"nuevo{api.Siguiente()}@correo.test";

        await api.Cliente(token).PutAsJsonAsync("/api/v1/mi-cuenta/correo", new { correo = nuevo, passwordActual = "no-es-la-clave" }).EsperarAsync(HttpStatusCode.BadRequest);
        var cambiado = await api.Cliente(token).PutAsJsonAsync("/api/v1/mi-cuenta/correo", new { correo = nuevo, passwordActual = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(nuevo, cambiado.GetProperty("correo").GetString());
        Assert.False(cambiado.GetProperty("correoVerificado").GetBoolean());
        Assert.Matches(EnlaceVerificacion(), await UltimoCorreoAsync(nuevo, "verificacion_correo"));
    }

    // ── Recuperacion de la contraseña (RNFP13) ──

    [Fact]
    public async Task LaRecuperacion_ConElCodigoDelCorreo_CambiaLaClave_YCierraLasSesiones()
    {
        var (_, correo, cookies, _) = await RegistrarAsync();

        var respuesta = await api.Cliente().PostAsJsonAsync("/api/v1/auth/recuperar", new { tipo = "cliente", correo }).EsperarAsync(HttpStatusCode.Accepted);
        var inexistente = await api.Cliente().PostAsJsonAsync("/api/v1/auth/recuperar", new { tipo = "cliente", correo = $"nadie{api.Siguiente()}@correo.test" })
            .EsperarAsync(HttpStatusCode.Accepted);
        Assert.Equal(respuesta.GetProperty("mensaje").GetString(), inexistente.GetProperty("mensaje").GetString());

        var codigo = CodigoDeSeisDigitos().Match(await UltimoCorreoAsync(correo, "recuperacion_cuenta")).Groups[1].Value;
        const string nueva = "nueva-clave-segura";
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/restablecer", new { tipo = "cliente", correo, codigo, password = nueva }).EsperarAsync(HttpStatusCode.NoContent);

        await api.Cliente().PostAsJsonAsync("/api/v1/auth/restablecer", new { tipo = "cliente", correo, codigo, password = nueva }).EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/login-cliente", new { correo, password = nueva }).EsperarAsync(HttpStatusCode.OK);
        using var vieja = await RenovarAsync(cookies);
        Assert.Equal(HttpStatusCode.Unauthorized, vieja.StatusCode);
        // Recibir el codigo en ese correo prueba que es suyo.
        Assert.True(await api.ConsultarAsync(db => db.Clientes.Where(c => c.Email == correo).Select(c => c.FechaVerificacionCorreo != null).SingleAsync()));
    }

    [Fact]
    public async Task ElCodigo_DejaDeServirTrasCincoIntentosFallidos()
    {
        var (_, correo, _, _) = await RegistrarAsync();
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/recuperar", new { tipo = "cliente", correo }).EsperarAsync(HttpStatusCode.Accepted);
        var codigo = CodigoDeSeisDigitos().Match(await UltimoCorreoAsync(correo, "recuperacion_cuenta")).Groups[1].Value;
        var equivocado = codigo == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
            await api.Cliente().PostAsJsonAsync("/api/v1/auth/restablecer", new { tipo = "cliente", correo, codigo = equivocado, password = "otra-clave-segura" })
                .EsperarAsync(HttpStatusCode.BadRequest);

        await api.Cliente().PostAsJsonAsync("/api/v1/auth/restablecer", new { tipo = "cliente", correo, codigo, password = "otra-clave-segura" })
            .EsperarAsync(HttpStatusCode.BadRequest);
    }

    // Revision de seguridad: el tope de intentos se reserva en la base, asi que muchas peticiones
    // simultaneas no prueban mas de cinco codigos.
    [Fact]
    public async Task LosIntentosSimultaneos_NoSuperanElTope()
    {
        var (_, correo, _, _) = await RegistrarAsync();
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/recuperar", new { tipo = "cliente", correo }).EsperarAsync(HttpStatusCode.Accepted);
        var codigo = CodigoDeSeisDigitos().Match(await UltimoCorreoAsync(correo, "recuperacion_cuenta")).Groups[1].Value;

        var equivocados = Enumerable.Range(0, 20).Select(i => ((int.Parse(codigo) + 1 + i) % 1_000_000).ToString("D6"))
            .Select(otro => api.Cliente().PostAsJsonAsync("/api/v1/auth/restablecer", new { tipo = "cliente", correo, codigo = otro, password = "otra-clave-segura" }));
        await Task.WhenAll(equivocados);

        var intentos = await api.ConsultarAsync(db => db.CodigosCuenta.Where(c => c.Correo == correo && c.Proposito == "recuperacion").Select(c => c.Intentos).SingleAsync());
        Assert.Equal(5, intentos);
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/restablecer", new { tipo = "cliente", correo, codigo, password = "otra-clave-segura" })
            .EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ElPersonal_TambienRecuperaSuContraseña()
    {
        var (_, email, _) = await api.CrearEmpleadoAsync(Security.Roles.Disenador);
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/recuperar", new { tipo = "personal", correo = email }).EsperarAsync(HttpStatusCode.Accepted);
        var codigo = CodigoDeSeisDigitos().Match(await UltimoCorreoAsync(email, "recuperacion_cuenta")).Groups[1].Value;

        await api.Cliente().PostAsJsonAsync("/api/v1/auth/restablecer", new { tipo = "personal", correo = email, codigo, password = "clave-del-personal" })
            .EsperarAsync(HttpStatusCode.NoContent);
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/login", new { email, password = "clave-del-personal" }).EsperarAsync(HttpStatusCode.OK);
    }

    // ── Limites (RNFP07) ──

    [Fact]
    public async Task ElIntentoOnceEnUnMinuto_Recibe429ConMensaje()
    {
        await using var otra = api.ConVariables(new Dictionary<string, string> { ["RateLimiting__Login"] = "10", ["RateLimiting__PorCuenta"] = "100000" });
        var cliente = otra.Cliente();
        for (var i = 0; i < 10; i++)
            await cliente.PostAsJsonAsync("/api/v1/auth/login-cliente", new { correo = $"x{i}@correo.test", password = "clave-equivocada" }).EsperarAsync(HttpStatusCode.Unauthorized);

        var bloqueado = await cliente.PostAsJsonAsync("/api/v1/auth/login-cliente", new { correo = "x@correo.test", password = "clave-equivocada" })
            .EsperarAsync(HttpStatusCode.TooManyRequests);
        Assert.False(string.IsNullOrWhiteSpace(bloqueado.GetProperty("error").GetString()));
    }

    // ── Mi cuenta (RNFE01) ──

    [Fact]
    public async Task ElCliente_VeYCorrigeSusDatos_SinPoderTomarUnTelefonoAjeno()
    {
        var (_, _, _, token) = await RegistrarAsync();
        var (_, telefonoAjeno, _) = await api.RegistrarClienteAsync();

        var cuenta = await api.Cliente(token).GetAsync("/api/v1/mi-cuenta").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("Cli", cuenta.GetProperty("nombre").GetString());

        var nuevoTelefono = api.Telefono();
        var corregida = await api.Cliente(token).PutAsJsonAsync("/api/v1/mi-cuenta",
                new { nombre = "Camila", apellido = "Rojas", telefono = $"+56 {nuevoTelefono[..1]} {nuevoTelefono[1..]}", direccion = "Villagrán 810" })
            .EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("Camila", corregida.GetProperty("nombre").GetString());
        Assert.Equal(nuevoTelefono, corregida.GetProperty("telefono").GetString());

        await api.Cliente(token).PutAsJsonAsync("/api/v1/mi-cuenta", new { nombre = "Camila", apellido = "Rojas", telefono = telefonoAjeno })
            .EsperarAsync(HttpStatusCode.Conflict);
        await api.Cliente(await api.TokenAdminAsync()).GetAsync("/api/v1/mi-cuenta").EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CambiarLaContraseña_CierraLasOtrasSesiones()
    {
        var (_, correo, cookiesDeOtroEquipo, token) = await RegistrarAsync();

        await api.Cliente(token).PutAsJsonAsync("/api/v1/mi-cuenta/password", new { passwordActual = "no-es", passwordNueva = "clave-nueva-larga" }).EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente(token).PutAsJsonAsync("/api/v1/mi-cuenta/password", new { passwordActual = ApiFixture.AdminPassword, passwordNueva = "clave-nueva-larga" }).EsperarAsync(HttpStatusCode.OK);

        using var otroEquipo = await RenovarAsync(cookiesDeOtroEquipo);
        Assert.Equal(HttpStatusCode.Unauthorized, otroEquipo.StatusCode);
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/login-cliente", new { correo, password = "clave-nueva-larga" }).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ElMarketing_SeAceptaYSeRetira_DesdeLaCuenta()
    {
        var (id, _, _, token) = await RegistrarAsync();

        await api.Cliente(token).PutAsJsonAsync("/api/v1/mi-cuenta/marketing", new { acepta = true, version = "1.0" }).EsperarAsync(HttpStatusCode.NoContent);
        Assert.True((await api.Cliente(token).GetAsync("/api/v1/mi-cuenta").EsperarAsync(HttpStatusCode.OK)).GetProperty("marketing").GetBoolean());

        await api.Cliente(token).PutAsJsonAsync("/api/v1/mi-cuenta/marketing", new { acepta = false }).EsperarAsync(HttpStatusCode.NoContent);
        Assert.False((await api.Cliente(token).GetAsync("/api/v1/mi-cuenta").EsperarAsync(HttpStatusCode.OK)).GetProperty("marketing").GetBoolean());
        Assert.True(await api.ConsultarAsync(db => db.Consentimientos.AnyAsync(c => c.IdCliente == id && c.Tipo == "marketing" && c.FechaRevocacion != null)));
    }
}
