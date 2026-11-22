using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.IngresoGoogle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 14C (RFS03, RFU05, E02, RNFE02, RNFP17, RNFO13): el ingreso con Google también es del personal, contra SQL Server y con el
// verificador simulado (ninguna prueba llama a Google). El empleado vincula su Google desde Mi perfil con su contraseña actual;
// los cuatro roles pueden usarlo; un correo de empleado sin vincular nunca se fusiona ni crea una cuenta de cliente.
[Collection(ApiCollection.Name)]
public sealed class GoogleFase14cTests(ApiFixture api)
{
    private const string Ruta = "/api/v1/auth/google";
    private const string RutaPerfil = "/api/v1/mi-perfil/google";

    // ── Utilidades ──

    private string NuevoSub() => $"2{api.Siguiente():D20}";

    private static string Codigo(JsonElement error) => error.GetProperty("codigo").GetString()!;

    // Lo que hace el navegador: pide el nonce y Google firma un token que lo lleva dentro.
    private async Task<(string IdToken, string Nonce)> TokenAsync(string sub, string correo, Func<ClaimsGoogle, ClaimsGoogle>? cambio = null, HttpClient? cliente = null)
    {
        var nonce = (await (cliente ?? api.Cliente()).GetAsync($"{Ruta}/nonce").EsperarAsync(HttpStatusCode.OK)).GetProperty("nonce").GetString()!;
        var claims = VerificadorGoogleSimulado.Claims(nonce, sub, correo);
        return (api.Google.Registrar(cambio is null ? claims : cambio(claims)), nonce);
    }

    private static Task<HttpResponseMessage> IngresarAsync(HttpClient cliente, string idToken, string nonce) =>
        cliente.PostAsJsonAsync(Ruta, new { idToken, nonce });

    private HttpClient Con(string token) => api.Cliente(token);

    private static Task<HttpResponseMessage> Vincular(HttpClient cliente, string idToken, string nonce, string? passwordActual) =>
        cliente.PostAsJsonAsync(RutaPerfil, new { idToken, nonce, passwordActual });

    private static Task<HttpResponseMessage> Desvincular(HttpClient cliente, string? passwordActual) =>
        cliente.SendAsync(new HttpRequestMessage(HttpMethod.Delete, RutaPerfil) { Content = JsonContent.Create(new { passwordActual }) });

    private sealed record Empleado14c(int Id, string Correo, string Token, string Rol);

    private async Task<Empleado14c> NuevoEmpleadoAsync(string rol)
    {
        var (id, correo, token) = await api.CrearEmpleadoAsync(rol);
        return new Empleado14c(id, correo, token, rol);
    }

    // Empleado que ya vinculó su Google (por la API, como lo haría desde Mi perfil).
    private async Task<(Empleado14c Empleado, string Sub)> EmpleadoConGoogleAsync(string rol = "operario")
    {
        var empleado = await NuevoEmpleadoAsync(rol);
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, empleado.Correo);
        await Vincular(Con(empleado.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);
        return (empleado, sub);
    }

    private Task<int> IdentidadesAsync(string sub) => api.ConsultarAsync(db => db.IdentidadesExternas.CountAsync(i => i.Sub == sub));

    private Task<int> ClientesConCorreoAsync(string correo) => api.ConsultarAsync(db => db.Clientes.CountAsync(c => c.Email == correo));

    private Task DesactivarAsync(int idEmpleado) => api.ConsultarAsync(async db =>
    {
        var empleado = await db.Empleados.FirstAsync(e => e.IdEmpleado == idEmpleado);
        empleado.Activo = false;
        await db.SaveChangesAsync();
        return 0;
    });

    private Task GuardarAsync(params IdentidadExterna[] identidades) => api.ConsultarAsync(async db =>
    {
        db.IdentidadesExternas.AddRange(identidades);
        await db.SaveChangesAsync();
        return 0;
    });

    // ── Vincular desde Mi perfil: los cuatro roles ──

    [Theory]
    [InlineData("dueño")]
    [InlineData("administrador")]
    [InlineData("diseñador")]
    [InlineData("operario")]
    public async Task ElEmpleado_VinculaSuGoogle_YDespuesEntraPorGoogle_ConSuMismoRol(string rol)
    {
        var empleado = await NuevoEmpleadoAsync(rol);
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, empleado.Correo);

        var vinculada = await Vincular(Con(empleado.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);
        Assert.True(vinculada.GetProperty("vinculado").GetBoolean());
        Assert.Equal(empleado.Correo, vinculada.GetProperty("correo").GetString());
        Assert.Equal(1, await IdentidadesAsync(sub));

        var (token2, nonce2) = await TokenAsync(sub, empleado.Correo);
        using var ingreso = await IngresarAsync(api.Cliente(), token2, nonce2);
        var cuerpo = JsonDocument.Parse(await ingreso.Content.ReadAsStringAsync()).RootElement;

        Assert.True(ingreso.StatusCode == HttpStatusCode.OK, cuerpo.ToString());
        Assert.Equal("empleado", cuerpo.GetProperty("tipo").GetString());
        Assert.Equal(rol, cuerpo.GetProperty("usuario").GetProperty("rol").GetString());
        Assert.Equal(empleado.Id, cuerpo.GetProperty("usuario").GetProperty("idEmpleado").GetInt32());
        // Es una sesión de personal de verdad: su token abre un endpoint del personal y deja las cookies de renovación.
        using var delPersonal = await Con(cuerpo.GetProperty("token").GetString()!).GetAsync("/api/v1/mi-disponibilidad");
        Assert.Equal(HttpStatusCode.OK, delPersonal.StatusCode);
        Assert.Contains(ingreso.Headers.GetValues("Set-Cookie"), c => c.StartsWith("synctime_renovacion="));
        Assert.Contains(ingreso.Headers.GetValues("Set-Cookie"), c => c.StartsWith("synctime_csrf="));
        // Y la contraseña sigue funcionando igual.
        await api.LoginEmpleadoAsync(empleado.Correo, ApiFixture.AdminPassword);
    }

    [Fact]
    public async Task LaSesionPorGoogle_TieneLaMismaFormaQueLaSesionConContrasena()
    {
        var (empleado, sub) = await EmpleadoConGoogleAsync("administrador");
        var (idToken, nonce) = await TokenAsync(sub, empleado.Correo);
        var porGoogle = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.OK);
        var porContrasena = await api.Cliente().PostAsJsonAsync("/api/v1/auth/login", new { email = empleado.Correo, password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(porContrasena.GetProperty("tipo").GetString(), porGoogle.GetProperty("tipo").GetString());
        Assert.Equal(porContrasena.GetProperty("usuario").GetRawText(), porGoogle.GetProperty("usuario").GetRawText());
        Assert.Equal(porContrasena.EnumerateObject().Select(p => p.Name).Order(), porGoogle.EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task LaSesionPorGoogle_SeRenueva_ComoUnaSesionDePersonal()
    {
        var (empleado, sub) = await EmpleadoConGoogleAsync("operario");
        var (idToken, nonce) = await TokenAsync(sub, empleado.Correo);
        using var ingreso = await IngresarAsync(api.Cliente(), idToken, nonce);
        Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);
        var cookies = ingreso.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).ToDictionary(c => c.Split('=', 2)[0], c => c.Split('=', 2)[1]);

        var renovacion = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/renovar");
        renovacion.Headers.Add("Cookie", $"synctime_renovacion={cookies["synctime_renovacion"]}; synctime_csrf={cookies["synctime_csrf"]}");
        renovacion.Headers.Add("X-CSRF-Token", cookies["synctime_csrf"]);
        using var respuesta = await api.Cliente().SendAsync(renovacion);
        var renovada = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;

        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, renovada.ToString());
        Assert.Equal("empleado", renovada.GetProperty("tipo").GetString());
        Assert.Equal(empleado.Id, renovada.GetProperty("usuario").GetProperty("idEmpleado").GetInt32());
    }

    [Fact]
    public async Task ElEstadoDeMiPerfil_DiceSiHayGoogleVinculado_YConQueCorreo()
    {
        var empleado = await NuevoEmpleadoAsync("diseñador");
        var antes = await Con(empleado.Token).GetAsync(RutaPerfil).EsperarAsync(HttpStatusCode.OK);
        Assert.False(antes.GetProperty("vinculado").GetBoolean());

        var (idToken, nonce) = await TokenAsync(NuevoSub(), empleado.Correo);
        await Vincular(Con(empleado.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);
        var despues = await Con(empleado.Token).GetAsync(RutaPerfil).EsperarAsync(HttpStatusCode.OK);

        Assert.True(despues.GetProperty("vinculado").GetBoolean());
        Assert.Equal(empleado.Correo, despues.GetProperty("correo").GetString());
    }

    // ── Vincular: las condiciones ──

    [Fact]
    public async Task Vincular_ConLaContrasenaIncorrecta_Da400_NoVinculaNada_YNoGastaElNonce()
    {
        var empleado = await NuevoEmpleadoAsync("operario");
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, empleado.Correo);

        var error = await Vincular(Con(empleado.Token), idToken, nonce, "una-contraseña-equivocada").EsperarAsync(HttpStatusCode.BadRequest);

        Assert.True(error.GetProperty("errores").TryGetProperty("passwordActual", out _));
        Assert.Equal(0, await IdentidadesAsync(sub));
        // La contraseña se revisa antes de gastar el nonce: el mismo token sirve al corregirla.
        await Vincular(Con(empleado.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(1, await IdentidadesAsync(sub));
    }

    [Fact]
    public async Task Vincular_SinContrasena_Da400()
    {
        var empleado = await NuevoEmpleadoAsync("operario");
        var (idToken, nonce) = await TokenAsync(NuevoSub(), empleado.Correo);

        var error = await Vincular(Con(empleado.Token), idToken, nonce, null).EsperarAsync(HttpStatusCode.BadRequest);

        Assert.True(error.GetProperty("errores").TryGetProperty("passwordActual", out _));
    }

    [Fact]
    public async Task Vincular_ConUnGoogleConOtroCorreo_Da409_YNoVinculaNada()
    {
        var empleado = await NuevoEmpleadoAsync("operario");
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, $"otra.persona{api.Siguiente()}@correo.test");

        var error = await Vincular(Con(empleado.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal("google_correo_distinto", Codigo(error));
        Assert.Equal(0, await IdentidadesAsync(sub));
    }

    [Fact]
    public async Task Vincular_ConElCorreoEnOtraEscritura_FuncionaIgual_PorqueElCorreoSeNormaliza()
    {
        var empleado = await NuevoEmpleadoAsync("operario");
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, $"  {empleado.Correo.ToUpperInvariant()} ");

        await Vincular(Con(empleado.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(1, await IdentidadesAsync(sub));
    }

    [Fact]
    public async Task Vincular_UnaCuentaQueYaTieneGoogle_Da409()
    {
        var (empleado, _) = await EmpleadoConGoogleAsync();
        var (idToken, nonce) = await TokenAsync(NuevoSub(), empleado.Correo);

        var error = await Vincular(Con(empleado.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal("google_ya_vinculada", Codigo(error));
    }

    [Fact]
    public async Task Vincular_UnGoogleQueYaEstaEnOtroEmpleado_Da409_YNoSeVuelveAVincular()
    {
        var (_, subDelOtro) = await EmpleadoConGoogleAsync();
        var empleado = await NuevoEmpleadoAsync("operario");
        var (idToken, nonce) = await TokenAsync(subDelOtro, empleado.Correo);

        var error = await Vincular(Con(empleado.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal("google_en_uso", Codigo(error));
        Assert.Equal(1, await IdentidadesAsync(subDelOtro));
    }

    [Fact]
    public async Task Vincular_UnGoogleQueYaEstaEnUnCliente_Da409()
    {
        var cuentaCliente = await api.CrearClienteConfirmadoAsync();
        var subDelCliente = NuevoSub();
        await GuardarAsync(new IdentidadExterna { IdCliente = cuentaCliente.Id, Sub = subDelCliente, Correo = cuentaCliente.Correo, FechaVinculacion = DateTimeOffset.UtcNow });
        var empleado = await NuevoEmpleadoAsync("operario");
        var (idToken, nonce) = await TokenAsync(subDelCliente, empleado.Correo);

        var error = await Vincular(Con(empleado.Token), idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal("google_en_uso", Codigo(error));
    }

    [Fact]
    public async Task Vincular_UnTokenQueNoCumpleLasCondiciones_Da400_ConElMismoCodigoSinImportarElMotivo()
    {
        var empleado = await NuevoEmpleadoAsync("operario");
        var cliente = Con(empleado.Token);
        var intentos = new List<(string IdToken, string Nonce)>
        {
            await TokenAsync(NuevoSub(), empleado.Correo, c => c with { CorreoVerificado = false }),
            await TokenAsync(NuevoSub(), empleado.Correo, c => c with { Audiencias = ["otra-app.apps.googleusercontent.com"] }),
            await TokenAsync(NuevoSub(), empleado.Correo, c => c with { Expira = DateTimeOffset.UtcNow.AddMinutes(-1) }),
            await TokenAsync(NuevoSub(), empleado.Correo, c => c with { Nonce = "otro-nonce" }),
            ("token-que-no-es-de-google", "nonce-que-nadie-emitio"),
        };

        foreach (var (idToken, nonce) in intentos)
        {
            var error = await Vincular(cliente, idToken, nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.BadRequest);
            // En Mi perfil es 400 (no 401): un 401 significaría «tu sesión venció» y el navegador intentaría renovarla.
            Assert.Equal("google_token_invalido", Codigo(error));
        }
    }

    [Fact]
    public async Task Vincular_ConGoogleCaido_Da503_SinBloquear()
    {
        var empleado = await NuevoEmpleadoAsync("operario");
        var nonce = (await api.Cliente().GetAsync($"{Ruta}/nonce").EsperarAsync(HttpStatusCode.OK)).GetProperty("nonce").GetString()!;

        var error = await Vincular(Con(empleado.Token), api.Google.RegistrarGoogleNoDisponible(), nonce, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.ServiceUnavailable);

        Assert.Equal("google_no_disponible", Codigo(error));
    }

    // ── Desvincular ──

    [Fact]
    public async Task Desvincular_ConLaContrasena_Da200ConUnaSesionNueva_Y_YaNoEntraPorGoogle()
    {
        var (empleado, sub) = await EmpleadoConGoogleAsync();

        using var respuesta = await Desvincular(Con(empleado.Token), ApiFixture.AdminPassword);
        var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;

        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, cuerpo.ToString());
        // Como al cambiar la contraseña: se cerraron todas las sesiones y se abre una nueva en este equipo.
        using var conLaNueva = await Con(cuerpo.GetProperty("token").GetString()!).GetAsync("/api/v1/mi-disponibilidad");
        Assert.Equal(HttpStatusCode.OK, conLaNueva.StatusCode);
        Assert.Contains(respuesta.Headers.GetValues("Set-Cookie"), c => c.StartsWith("synctime_renovacion="));
        Assert.Equal(0, await IdentidadesAsync(sub));
        var (idToken, nonce) = await TokenAsync(sub, empleado.Correo);
        var rechazo = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("cuenta_personal_existente", Codigo(rechazo));
    }

    [Fact]
    public async Task Desvincular_CierraTodasLasSesionesDelEmpleado_AunLaQueAbrioElGoogleComprometido()
    {
        var (empleado, sub) = await EmpleadoConGoogleAsync();
        // Una sesion abierta POR GOOGLE (la del atacante, si el Google estuviera comprometido).
        var (idToken, nonce) = await TokenAsync(sub, empleado.Correo);
        using var ingreso = await IngresarAsync(api.Cliente(), idToken, nonce);
        Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);
        var cookies = ingreso.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).ToDictionary(c => c.Split('=', 2)[0], c => c.Split('=', 2)[1]);

        using var desvincula = await Desvincular(Con(empleado.Token), ApiFixture.AdminPassword);
        Assert.Equal(HttpStatusCode.OK, desvincula.StatusCode);

        var renovacion = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/renovar");
        renovacion.Headers.Add("Cookie", $"synctime_renovacion={cookies["synctime_renovacion"]}; synctime_csrf={cookies["synctime_csrf"]}");
        renovacion.Headers.Add("X-CSRF-Token", cookies["synctime_csrf"]);
        using var intento = await api.Cliente().SendAsync(renovacion);
        Assert.Equal(HttpStatusCode.Unauthorized, intento.StatusCode);
    }

    [Fact]
    public async Task Desvincular_ConLaContrasenaIncorrecta_Da400_YNoQuitaNada()
    {
        var (empleado, sub) = await EmpleadoConGoogleAsync();

        var error = await Desvincular(Con(empleado.Token), "mala").EsperarAsync(HttpStatusCode.BadRequest);

        Assert.True(error.GetProperty("errores").TryGetProperty("passwordActual", out _));
        Assert.Equal(1, await IdentidadesAsync(sub));
    }

    [Fact]
    public async Task Desvincular_SinGoogleVinculado_Da404()
    {
        var empleado = await NuevoEmpleadoAsync("operario");

        using var respuesta = await Desvincular(Con(empleado.Token), ApiFixture.AdminPassword);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ── El ingreso: los caminos con personal ──

    [Fact]
    public async Task UnCorreoDeEmpleadoSinVincular_Da409_NoCreaClienteNiEntregaPase_NiSesion()
    {
        var empleado = await NuevoEmpleadoAsync("administrador");
        var sub = NuevoSub();
        var (idToken, nonce) = await TokenAsync(sub, empleado.Correo);

        using var respuesta = await IngresarAsync(api.Cliente(), idToken, nonce);
        var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("cuenta_personal_existente", Codigo(cuerpo));
        Assert.Contains("Mi perfil", cuerpo.GetProperty("error").GetString());
        Assert.False(cuerpo.TryGetProperty("token", out _));
        Assert.False(cuerpo.TryGetProperty("pase", out _));
        Assert.Equal(0, await ClientesConCorreoAsync(empleado.Correo));
        Assert.Equal(0, await IdentidadesAsync(sub));
        Assert.False(respuesta.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task UnCorreoQueNoEsDeNadie_SigueSiendoUnRegistroPendiente()
    {
        var (idToken, nonce) = await TokenAsync(NuevoSub(), $"nadie{api.Siguiente()}@correo.test");

        var respuesta = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal("registro_pendiente", respuesta.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task SiElCorreoEsDeUnClienteConCuenta_ManaLaCuentaDelCliente()
    {
        var cliente = await api.CrearClienteConfirmadoAsync();
        var (idToken, nonce) = await TokenAsync(NuevoSub(), cliente.Correo);

        var error = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal("cuenta_existente", Codigo(error));
    }

    [Fact]
    public async Task UnEmpleadoDesactivado_ConGoogleVinculado_RecibeLaRespuestaUnicaDeUnTokenMalo()
    {
        var (empleado, sub) = await EmpleadoConGoogleAsync();
        await DesactivarAsync(empleado.Id);
        var (idToken, nonce) = await TokenAsync(sub, empleado.Correo);
        var (malo, nonceMalo) = await TokenAsync(NuevoSub(), $"x{api.Siguiente()}@correo.test", c => c with { CorreoVerificado = false });

        var desactivado = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.Unauthorized);
        var tokenMalo = await IngresarAsync(api.Cliente(), malo, nonceMalo).EsperarAsync(HttpStatusCode.Unauthorized);

        Assert.Equal("google_token_invalido", Codigo(desactivado));
        Assert.Equal(tokenMalo.GetProperty("error").GetString(), desactivado.GetProperty("error").GetString());
    }

    [Fact]
    public async Task UnEmpleadoDesactivado_SinVincular_TampocoRevelaQueExiste()
    {
        var empleado = await NuevoEmpleadoAsync("operario");
        await DesactivarAsync(empleado.Id);
        var (idToken, nonce) = await TokenAsync(NuevoSub(), empleado.Correo);

        var error = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.Unauthorized);

        Assert.Equal("google_token_invalido", Codigo(error));
        Assert.Equal(0, await ClientesConCorreoAsync(empleado.Correo));
    }

    // Por la API (como lo hace gestion): desactivar desvincula su Google; reactivarlo no se lo devuelve.
    private async Task CambiarActivoPorApiAsync(int idEmpleado, bool activo)
    {
        var admin = Con(await api.TokenAdminAsync());
        var ficha = await admin.GetAsync($"/api/v1/empleados/{idEmpleado}").EsperarAsync(HttpStatusCode.OK);
        var peticion = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/empleados/{idEmpleado}/activo")
        {
            Content = JsonContent.Create(new { activo, rowVersion = ficha.GetProperty("rowVersion").GetString() })
        };
        using var respuesta = await admin.SendAsync(peticion);
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DesactivarAlEmpleado_DesvinculaSuGoogle_YAlReactivarloDebeVolverAVincularlo()
    {
        var (empleado, sub) = await EmpleadoConGoogleAsync();

        await CambiarActivoPorApiAsync(empleado.Id, activo: false);
        Assert.Equal(0, await IdentidadesAsync(sub));
        await CambiarActivoPorApiAsync(empleado.Id, activo: true);

        var (idToken, nonce) = await TokenAsync(sub, empleado.Correo);
        var rechazo = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("cuenta_personal_existente", Codigo(rechazo));
        // Con su contraseña lo vincula de nuevo y entra.
        var nuevoToken = await api.LoginEmpleadoAsync(empleado.Correo, ApiFixture.AdminPassword);
        var (v, n) = await TokenAsync(sub, empleado.Correo);
        await Vincular(Con(nuevoToken), v, n, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);
        var (i, ni) = await TokenAsync(sub, empleado.Correo);
        var sesion = await IngresarAsync(api.Cliente(), i, ni).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("empleado", sesion.GetProperty("tipo").GetString());
    }

    [Fact]
    public async Task UnVinculoQueQuedoConUnCorreoQueYaNoEsElDeLaFicha_NoAbreSesion()
    {
        // Defensa extra para el personal: aunque el vinculo exista, el correo verificado del token debe ser el de la ficha ahora.
        var (empleado, sub) = await EmpleadoConGoogleAsync();
        await api.ConsultarAsync(async db =>
        {
            (await db.Empleados.FirstAsync(e => e.IdEmpleado == empleado.Id)).Email = $"otro.correo{api.Siguiente()}@test.local";
            await db.SaveChangesAsync();
            return 0;
        });
        var (idToken, nonce) = await TokenAsync(sub, empleado.Correo);

        var error = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.Unauthorized);

        Assert.Equal("google_token_invalido", Codigo(error));
    }

    [Fact]
    public async Task ElEstadoDeMiPerfil_NoGastaElCupoDeLasConfirmacionesConContrasena()
    {
        var empleado = await NuevoEmpleadoAsync("operario");
        await using var otra = api.ConVariable("RateLimiting__Reautenticacion", "2");
        var cliente = otra.Cliente(empleado.Token);

        for (var i = 0; i < 6; i++)
            await cliente.GetAsync(RutaPerfil).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VincularYDesvincular_SiTienenElLimiteDeLasConfirmacionesConContrasena()
    {
        var empleado = await NuevoEmpleadoAsync("operario");
        await using var otra = api.ConVariable("RateLimiting__Reautenticacion", "2");
        var cliente = otra.Cliente(empleado.Token);
        var estados = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            using var r = await Vincular(cliente, "token-cualquiera", "nonce-cualquiera", "equivocada");
            estados.Add(r.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, estados);
    }

    [Fact]
    public async Task UnClienteConGoogleVinculado_SigueEntrandoComoCliente()
    {
        var cliente = await api.CrearClienteConfirmadoAsync();
        var sub = NuevoSub();
        await GuardarAsync(new IdentidadExterna { IdCliente = cliente.Id, Sub = sub, Correo = cliente.Correo, FechaVinculacion = DateTimeOffset.UtcNow });
        var (idToken, nonce) = await TokenAsync(sub, cliente.Correo);

        var sesion = await IngresarAsync(api.Cliente(), idToken, nonce).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal("cliente", sesion.GetProperty("tipo").GetString());
    }

    // ── Cambios en la ficha del empleado ──

    [Fact]
    public async Task CambiarElCorreoDelEmpleado_DesvinculaSuGoogle()
    {
        var (empleado, sub) = await EmpleadoConGoogleAsync("operario");
        var admin = Con(await api.TokenAdminAsync());
        var ficha = await admin.GetAsync($"/api/v1/empleados/{empleado.Id}").EsperarAsync(HttpStatusCode.OK);
        var nuevoCorreo = $"nuevo.correo{api.Siguiente()}@test.local";

        await admin.PutAsJsonAsync($"/api/v1/empleados/{empleado.Id}", new
        {
            rut = ficha.GetProperty("rut").GetString(), nombre = ficha.GetProperty("nombre").GetString(), apellido = ficha.GetProperty("apellido").GetString(),
            telefono = ficha.GetProperty("telefono").GetString(), email = nuevoCorreo, rol = "operario", passwordActual = ApiFixture.AdminPassword,
            rowVersion = ficha.GetProperty("rowVersion").GetString()
        }).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(0, await IdentidadesAsync(sub));
        var estado = await Con(await api.LoginEmpleadoAsync(nuevoCorreo, ApiFixture.AdminPassword)).GetAsync(RutaPerfil).EsperarAsync(HttpStatusCode.OK);
        Assert.False(estado.GetProperty("vinculado").GetBoolean());
    }

    [Fact]
    public async Task EditarOtrosDatosDelEmpleado_NoDesvinculaSuGoogle()
    {
        var (empleado, sub) = await EmpleadoConGoogleAsync("operario");
        var admin = Con(await api.TokenAdminAsync());
        var ficha = await admin.GetAsync($"/api/v1/empleados/{empleado.Id}").EsperarAsync(HttpStatusCode.OK);

        await admin.PutAsJsonAsync($"/api/v1/empleados/{empleado.Id}", new
        {
            rut = ficha.GetProperty("rut").GetString(), nombre = "Otro nombre", apellido = ficha.GetProperty("apellido").GetString(),
            telefono = ficha.GetProperty("telefono").GetString(), email = empleado.Correo, rol = "operario", passwordActual = ApiFixture.AdminPassword,
            rowVersion = ficha.GetProperty("rowVersion").GetString()
        }).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(1, await IdentidadesAsync(sub));
    }

    // ── Quién puede usar Mi perfil ──

    [Fact]
    public async Task ElClienteYElVisitante_NoPuedenUsarLasRutasDeMiPerfil()
    {
        var cliente = await api.CrearClienteConfirmadoAsync();
        var (idToken, nonce) = await TokenAsync(NuevoSub(), cliente.Correo);

        using var clienteVincula = await Vincular(Con(cliente.Token), idToken, nonce, ApiFixture.AdminPassword);
        using var clienteDesvincula = await Desvincular(Con(cliente.Token), ApiFixture.AdminPassword);
        using var clienteConsulta = await Con(cliente.Token).GetAsync(RutaPerfil);
        Assert.Equal(HttpStatusCode.Forbidden, clienteVincula.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, clienteDesvincula.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, clienteConsulta.StatusCode);

        using var visitanteVincula = await Vincular(api.Cliente(), idToken, nonce, ApiFixture.AdminPassword);
        using var visitanteDesvincula = await Desvincular(api.Cliente(), ApiFixture.AdminPassword);
        using var visitanteConsulta = await api.Cliente().GetAsync(RutaPerfil);
        Assert.Equal(HttpStatusCode.Unauthorized, visitanteVincula.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, visitanteDesvincula.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, visitanteConsulta.StatusCode);
    }

    [Fact]
    public async Task LasContrasenasEquivocadasAlVincular_TienenLimite_PorCuenta()
    {
        var empleado = await NuevoEmpleadoAsync("operario");
        await using var otra = api.ConVariable("RateLimiting__PorCuenta", "3");
        var cliente = otra.Cliente(empleado.Token);
        var estados = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            // La contraseña se revisa antes que el token: basta un token cualquiera para contar el fallo.
            using var respuesta = await Vincular(cliente, "token-cualquiera", "nonce-cualquiera", $"equivocada-{i}");
            estados.Add(respuesta.StatusCode);
        }

        Assert.Equal([HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], estados);
        // El límite es de ESA cuenta: otro empleado no se ve afectado.
        var otroEmpleado = await NuevoEmpleadoAsync("operario");
        using var libre = await Vincular(otra.Cliente(otroEmpleado.Token), "token-cualquiera", "nonce-cualquiera", "equivocada");
        Assert.Equal(HttpStatusCode.BadRequest, libre.StatusCode);
    }

    // ── La bitácora y los registros ──

    [Fact]
    public async Task ElIngresoLaVinculacionYLaDesvinculacion_QuedanEnLaBitacora_ConElEmpleadoComoActor_SinElSub()
    {
        var empleado = await NuevoEmpleadoAsync("dueño");
        var sub = NuevoSub();
        var (v1, n1) = await TokenAsync(sub, empleado.Correo);
        await Vincular(Con(empleado.Token), v1, n1, ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);
        var (v2, n2) = await TokenAsync(sub, empleado.Correo);
        await IngresarAsync(api.Cliente(), v2, n2).EsperarAsync(HttpStatusCode.OK);
        using var desvincula = await Desvincular(Con(empleado.Token), ApiFixture.AdminPassword);
        Assert.Equal(HttpStatusCode.OK, desvincula.StatusCode);

        var filas = await api.ConsultarAsync(db => db.Auditoria.AsNoTracking()
            .Where(a => a.IdEmpleado == empleado.Id && (a.Accion == "ingreso_google" || a.Accion == "vincular_google" || a.Accion == "desvincular_google"))
            .OrderBy(a => a.IdAuditoria).ToListAsync());

        Assert.Equal(["vincular_google", "ingreso_google", "desvincular_google"], filas.Select(f => f.Accion));
        Assert.All(filas, f =>
        {
            Assert.Null(f.IdCliente);
            Assert.Equal("dueño", f.Rol);
            Assert.Equal("empleado", f.Entidad);
            Assert.Equal(empleado.Id.ToString(), f.IdEntidad);
            Assert.True(f.Fecha > DateTimeOffset.UtcNow.AddMinutes(-5));
            Assert.DoesNotContain(sub, f.Detalle ?? "");
        });
        var todas = await api.ConsultarAsync(db => db.Auditoria.AsNoTracking().Where(a => a.Entidad == "identidad_externa").Select(a => a.Detalle ?? "").ToListAsync());
        Assert.DoesNotContain(todas, d => d.Contains(sub));
    }

    [Fact]
    public async Task NiElToken_NiElNonce_NiElSub_AparecenEnLosRegistros_NiEnLasRespuestasDeError()
    {
        var logs = new LogsEnMemoria();
        var cliente = api.ClienteConLogs(logs);
        var empleado = await NuevoEmpleadoAsync("operario");
        var sub = NuevoSub();
        var secretos = new List<string>();
        var respuestas = new List<string>();

        var (idToken, nonce) = await TokenAsync(sub, empleado.Correo, cliente: cliente);
        secretos.AddRange([idToken, nonce]);
        respuestas.Add(await (await IngresarAsync(cliente, idToken, nonce)).Content.ReadAsStringAsync());
        var (v, n) = await TokenAsync(sub, empleado.Correo, cliente: cliente);
        secretos.AddRange([v, n]);
        cliente.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", empleado.Token);
        using (var vinculada = await Vincular(cliente, v, n, ApiFixture.AdminPassword))
            respuestas.Add(await vinculada.Content.ReadAsStringAsync());
        var (malo, nonceMalo) = await TokenAsync(NuevoSub(), empleado.Correo, c => c with { CorreoVerificado = false }, cliente);
        secretos.AddRange([malo, nonceMalo]);
        respuestas.Add(await (await Vincular(cliente, malo, nonceMalo, ApiFixture.AdminPassword)).Content.ReadAsStringAsync());
        cliente.DefaultRequestHeaders.Authorization = null;
        var (ingresoOk, nonceOk) = await TokenAsync(sub, empleado.Correo, cliente: cliente);
        secretos.AddRange([ingresoOk, nonceOk]);
        respuestas.Add(await (await IngresarAsync(cliente, ingresoOk, nonceOk)).Content.ReadAsStringAsync());

        foreach (var secreto in secretos.Where(s => s.Length > 8))
        {
            Assert.DoesNotContain(logs.Mensajes, m => m.Contains(secreto));
            Assert.DoesNotContain(respuestas, r => r.Contains(secreto));
        }
        Assert.DoesNotContain(logs.Mensajes, m => m.Contains(sub));
    }

    // ── La base ──

    [Fact]
    public async Task LaBase_ExigeQueLaIdentidadSeaDeUnClienteODeUnEmpleado_NuncaDeNingunoNiDeAmbos()
    {
        var cliente = await api.CrearClienteConfirmadoAsync();
        var empleado = await NuevoEmpleadoAsync("operario");
        var ninguno = new IdentidadExterna { Sub = NuevoSub(), Correo = "x@correo.test", FechaVinculacion = DateTimeOffset.UtcNow };
        var ambos = new IdentidadExterna { IdCliente = cliente.Id, IdEmpleado = empleado.Id, Sub = NuevoSub(), Correo = "x@correo.test", FechaVinculacion = DateTimeOffset.UtcNow };

        var errorNinguno = await Assert.ThrowsAsync<DbUpdateException>(() => GuardarAsync(ninguno));
        var errorAmbos = await Assert.ThrowsAsync<DbUpdateException>(() => GuardarAsync(ambos));

        Assert.Contains("ck_identidades_externas_destino", errorNinguno.InnerException!.Message);
        Assert.Contains("ck_identidades_externas_destino", errorAmbos.InnerException!.Message);
    }

    [Fact]
    public async Task LaBase_NoDejaDosGoogleEnUnMismoEmpleado_PeroSiUnoEnCadaEmpleado_YEnCadaCliente()
    {
        var uno = await NuevoEmpleadoAsync("operario");
        var dos = await NuevoEmpleadoAsync("operario");
        var cliente = await api.CrearClienteConfirmadoAsync();
        var otroCliente = await api.CrearClienteConfirmadoAsync();
        var ahora = DateTimeOffset.UtcNow;

        // Varias filas con id_cliente nulo (empleados) y varias con id_empleado nulo (clientes) conviven: los índices únicos son filtrados.
        await GuardarAsync(
            new IdentidadExterna { IdEmpleado = uno.Id, Sub = NuevoSub(), Correo = "a@correo.test", FechaVinculacion = ahora },
            new IdentidadExterna { IdEmpleado = dos.Id, Sub = NuevoSub(), Correo = "b@correo.test", FechaVinculacion = ahora },
            new IdentidadExterna { IdCliente = cliente.Id, Sub = NuevoSub(), Correo = "c@correo.test", FechaVinculacion = ahora },
            new IdentidadExterna { IdCliente = otroCliente.Id, Sub = NuevoSub(), Correo = "d@correo.test", FechaVinculacion = ahora });

        var segundo = await Assert.ThrowsAsync<DbUpdateException>(() =>
            GuardarAsync(new IdentidadExterna { IdEmpleado = uno.Id, Sub = NuevoSub(), Correo = "e@correo.test", FechaVinculacion = ahora }));
        Assert.Contains("uq_identidades_externas_empleado", segundo.InnerException!.Message);
    }

    [Fact]
    public async Task ElSubEsUnico_AunqueUnoSeaDeUnClienteYElOtroDeUnEmpleado()
    {
        var empleado = await NuevoEmpleadoAsync("operario");
        var cliente = await api.CrearClienteConfirmadoAsync();
        var sub = NuevoSub();
        var ahora = DateTimeOffset.UtcNow;
        await GuardarAsync(new IdentidadExterna { IdEmpleado = empleado.Id, Sub = sub, Correo = "a@correo.test", FechaVinculacion = ahora });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
            GuardarAsync(new IdentidadExterna { IdCliente = cliente.Id, Sub = sub, Correo = "b@correo.test", FechaVinculacion = ahora }));

        Assert.Contains("uq_identidades_externas_sub", error.InnerException!.Message);
    }

    [Fact]
    public async Task SiSeBorraAlEmpleado_SuIdentidadSeVaConEl()
    {
        var (empleado, sub) = await EmpleadoConGoogleAsync();

        await api.ConsultarAsync(async db =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM empleados WHERE id_empleado = {empleado.Id}");
            return 0;
        });

        Assert.Equal(0, await IdentidadesAsync(sub));
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
