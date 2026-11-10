using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Estas pruebas usan vectores sinteticos: comprueban las reglas del servidor,
// no la precision del reconocimiento, que requiere rostros y camaras reales.
[Collection(ApiCollection.Name)]
public sealed class RostroFacialTests(ApiFixture api)
{
    // ── Activacion ──

    [Fact]
    public async Task Activate_WithCorrectPassword_StoresItEncryptedWithItsConsent()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var rostro = Rostro(1);
        var antes = DateTimeOffset.UtcNow.AddSeconds(-2);

        var respuesta = await ActivarAsync(empleado.Token, rostro);

        Assert.True(respuesta.GetProperty("activo").GetBoolean());
        Assert.Equal(JsonValueKind.Null, respuesta.GetProperty("tokenEquipo").ValueKind);

        var fila = await FilaVigenteAsync(empleado.Id);
        Assert.Equal(ConsentimientoFacial.Version, fila.VersionConsentimiento);
        Assert.InRange(fila.FechaConsentimiento, antes, DateTimeOffset.UtcNow.AddSeconds(2));

        // En la base no debe aparecer ningun valor del descriptor tal como se envio.
        var cifrado = fila.DescriptorCifrado!;
        var claro = new byte[rostro.Length * sizeof(float)];
        Buffer.BlockCopy(rostro, 0, claro, 0, claro.Length);
        Assert.Equal(-1, cifrado.AsSpan().IndexOf(claro.AsSpan(0, sizeof(float))));
        Assert.Equal(-1, cifrado.AsSpan().IndexOf(claro.AsSpan(sizeof(float) * 10, sizeof(float))));

        var estado = await api.Cliente(empleado.Token).GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.OK);
        Assert.True(estado.GetProperty("activo").GetBoolean());
        Assert.Equal(ConsentimientoFacial.Version, estado.GetProperty("consentimiento").GetProperty("version").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-mi-password")]
    public async Task Activate_WithoutTheCorrectPassword_IsRejected(string? password)
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);

        var respuesta = await api.Cliente(empleado.Token)
            .PutAsJsonAsync("/api/v1/auth/rostro", ActivacionCon(Rostro(2), password))
            .EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Equal("La contraseña actual no es correcta.", respuesta.GetProperty("error").GetString());
        Assert.Equal(0, await FilasAsync(empleado.Id));
    }

    [Fact]
    public async Task Activate_WithoutConsent_IsRejected()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var cliente = api.Cliente(empleado.Token);
        var capturas = Capturas(Rostro(3));

        await cliente.PutAsJsonAsync("/api/v1/auth/rostro", new { password = ApiFixture.AdminPassword, consentimiento = false, versionConsentimiento = ConsentimientoFacial.Version, descriptores = capturas })
            .EsperarAsync(HttpStatusCode.BadRequest);
        await cliente.PutAsJsonAsync("/api/v1/auth/rostro", new { password = ApiFixture.AdminPassword, versionConsentimiento = ConsentimientoFacial.Version, descriptores = capturas })
            .EsperarAsync(HttpStatusCode.BadRequest);
        await cliente.PutAsJsonAsync("/api/v1/auth/rostro", new { password = ApiFixture.AdminPassword, consentimiento = true, versionConsentimiento = "otra", descriptores = capturas })
            .EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Equal(0, await FilasAsync(empleado.Id));
    }

    public static TheoryData<float[][]> CapturasInvalidas() => new()
    {
        { [Rostro(4), Rostro(4)] },
        { Enumerable.Repeat(Rostro(4), RostroFacialService.MaxCapturas + 1).ToArray() },
        { [Rostro(4), Rostro(4), new float[RostroFacialService.Dimensiones - 1]] },
        { [Rostro(4), Rostro(4), new float[RostroFacialService.Dimensiones]] },
        { [Rostro(4), Rostro(4), Enumerable.Repeat(2f, RostroFacialService.Dimensiones).ToArray()] },
        // Personas distintas: un cambio de rostro a mitad de la captura.
        { [Rostro(4), Rostro(4), Rostro(5)] }
    };

    [Theory]
    [MemberData(nameof(CapturasInvalidas))]
    public async Task Activate_WithInvalidCaptures_IsRejected(float[][] capturas)
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);

        await api.Cliente(empleado.Token)
            .PutAsJsonAsync("/api/v1/auth/rostro", new { password = ApiFixture.AdminPassword, consentimiento = true, versionConsentimiento = ConsentimientoFacial.Version, descriptores = capturas })
            .EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Equal(0, await FilasAsync(empleado.Id));
    }

    [Fact]
    public async Task Activate_Again_ReplacesTheFaceAndKeepsTheEvidence()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var viejo = Rostro(6);
        var nuevo = Rostro(7);

        await ActivarAsync(empleado.Token, viejo);
        await ActivarAsync(empleado.Token, nuevo);

        var filas = await api.ConsultarAsync(db => db.RostrosEmpleado.AsNoTracking().Where(r => r.IdEmpleado == empleado.Id).OrderBy(r => r.IdRostro).ToListAsync());
        Assert.Equal(2, filas.Count);
        Assert.Null(filas[0].DescriptorCifrado);
        Assert.NotNull(filas[0].FechaRevocacion);
        Assert.NotNull(filas[1].DescriptorCifrado);
        Assert.Null(filas[1].FechaRevocacion);

        await LoginFacial(empleado.Email, viejo).EsperarAsync(HttpStatusCode.Unauthorized);
        await LoginFacial(empleado.Email, nuevo).EsperarAsync(HttpStatusCode.OK);
    }

    // ── Inicio de sesion ──

    [Fact]
    public async Task Login_WithMatchingFace_ReturnsTheSameAsThePasswordLogin()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var rostro = Rostro(8);
        await ActivarAsync(empleado.Token, rostro);

        var conRostro = await LoginFacial(empleado.Email, Variar(rostro)).EsperarAsync(HttpStatusCode.OK);
        var conPassword = await api.Cliente()
            .PostAsJsonAsync("/api/v1/auth/login", new { email = empleado.Email, password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(conPassword.GetProperty("usuario").GetRawText(), conRostro.GetProperty("usuario").GetRawText());
        Assert.Equal(await ClaimsAsync(conPassword), await ClaimsAsync(conRostro));
    }

    [Theory]
    [InlineData(0.4f, HttpStatusCode.OK)]
    [InlineData(0.6f, HttpStatusCode.Unauthorized)]
    public async Task Login_ComparesAgainstTheThreshold(float desplazamiento, HttpStatusCode esperado)
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var rostro = Rostro(9);
        await ActivarAsync(empleado.Token, rostro);

        var intento = (float[])rostro.Clone();
        intento[0] += desplazamiento;

        await LoginFacial(empleado.Email, intento).EsperarAsync(esperado);
    }

    [Fact]
    public async Task Login_WithAnotherPersonsFace_IsRejected()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var otro = await api.CrearEmpleadoAsync(Roles.Operario);
        await ActivarAsync(empleado.Token, Rostro(10));
        await ActivarAsync(otro.Token, Rostro(11));

        // Es una comparacion uno a uno: coincidir con otra persona no sirve.
        await LoginFacial(empleado.Email, Rostro(11)).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Failures_AreIndistinguishable()
    {
        var sinRostro = await api.CrearEmpleadoAsync(Roles.Operario);

        var fallaRostro = await api.CrearEmpleadoAsync(Roles.Operario);
        await ActivarAsync(fallaRostro.Token, Rostro(12));

        var inactivo = await api.CrearEmpleadoAsync(Roles.Operario);
        await ActivarAsync(inactivo.Token, Rostro(13));
        await DesactivarEmpleadoAsync(inactivo.Id);

        var revocado = await api.CrearEmpleadoAsync(Roles.Operario);
        await ActivarAsync(revocado.Token, Rostro(14));
        await api.Cliente(revocado.Token).DeleteAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.NoContent);

        var bloqueado = await api.CrearEmpleadoAsync(Roles.Operario);
        await ActivarAsync(bloqueado.Token, Rostro(15));
        await BloquearAsync(bloqueado.Id, DateTimeOffset.UtcNow.AddMinutes(10));

        var dueno = await api.CrearEmpleadoAsync(Roles.Dueno);
        await ActivarAsync(dueno.Token, Rostro(16));

        var casos = new (string Email, float[] Descriptor)[]
        {
            ($"nadie{api.Siguiente()}@test.local", Rostro(17)),
            (sinRostro.Email, Rostro(17)),
            (fallaRostro.Email, Rostro(17)),
            (inactivo.Email, Rostro(13)),
            (revocado.Email, Rostro(14)),
            (bloqueado.Email, Rostro(15)),
            (dueno.Email, Rostro(16))
        };

        foreach (var (email, descriptor) in casos)
        {
            var reloj = Stopwatch.StartNew();
            using var respuesta = await LoginFacial(email, descriptor);
            reloj.Stop();

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
            Assert.Empty(await respuesta.Content.ReadAsByteArrayAsync());
            Assert.Empty(respuesta.Headers.WwwAuthenticate);
            // Se deja margen por la resolucion del temporizador.
            Assert.True(reloj.ElapsedMilliseconds >= ApiFixture.TiempoMinimoFalloMs - 15, $"{email} respondio en {reloj.ElapsedMilliseconds} ms");
        }
    }

    [Fact]
    public async Task Login_MalformedRequest_DependsOnlyOnTheFormat()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        await ActivarAsync(empleado.Token, Rostro(18));
        var inexistente = $"nadie{api.Siguiente()}@test.local";

        foreach (var descriptor in new[] { new float[10], new float[RostroFacialService.Dimensiones], Enumerable.Repeat(2f, RostroFacialService.Dimensiones).ToArray() })
        {
            var conCuenta = await LoginFacial(empleado.Email, descriptor).EsperarAsync(HttpStatusCode.BadRequest);
            var sinCuenta = await LoginFacial(inexistente, descriptor).EsperarAsync(HttpStatusCode.BadRequest);
            Assert.Equal(sinCuenta.GetRawText(), conCuenta.GetRawText());
        }

        await api.Cliente().PostAsJsonAsync("/api/v1/auth/login-facial", new { descriptor = Rostro(18) }).EsperarAsync(HttpStatusCode.BadRequest);

        // Un numero fuera del rango de float llega como infinito.
        var valores = string.Join(",", Enumerable.Repeat("0.1", RostroFacialService.Dimensiones - 1));
        using var enorme = new StringContent($$"""{"email":"{{empleado.Email}}","descriptor":[1e400,{{valores}}]}""", Encoding.UTF8, "application/json");
        await api.Cliente().PostAsync("/api/v1/auth/login-facial", enorme).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_InactiveEmployee_IsRejectedAndTheirDataIsErased()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var rostro = Rostro(19);
        await ActivarAsync(empleado.Token, rostro);
        await LoginFacial(empleado.Email, rostro).EsperarAsync(HttpStatusCode.OK);

        await DesactivarEmpleadoAsync(empleado.Id);
        await LoginFacial(empleado.Email, rostro).EsperarAsync(HttpStatusCode.Unauthorized);

        var fila = await api.ConsultarAsync(db => db.RostrosEmpleado.AsNoTracking().SingleAsync(r => r.IdEmpleado == empleado.Id));
        Assert.Null(fila.DescriptorCifrado);
        Assert.NotNull(fila.FechaRevocacion);
    }

    [Fact]
    public async Task Login_ByAnyone_ErasesTheDataOfInactiveEmployees()
    {
        var inactivo = await api.CrearEmpleadoAsync(Roles.Operario);
        await ActivarAsync(inactivo.Token, Rostro(20));
        await DesactivarEmpleadoAsync(inactivo.Id);

        // Hoy se desactiva por SQL y esa persona quizas no vuelva a intentar entrar.
        await LoginFacial($"nadie{api.Siguiente()}@test.local", Rostro(21)).EsperarAsync(HttpStatusCode.Unauthorized);

        Assert.Null(await api.ConsultarAsync(db => db.RostrosEmpleado.AsNoTracking().Where(r => r.IdEmpleado == inactivo.Id).Select(r => r.DescriptorCifrado).SingleAsync()));
    }

    [Fact]
    public async Task ReactivatedEmployee_MustConsentAgain()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var rostro = Rostro(22);
        await ActivarAsync(empleado.Token, rostro);
        await DesactivarEmpleadoAsync(empleado.Id);
        await LoginFacial(empleado.Email, rostro).EsperarAsync(HttpStatusCode.Unauthorized);

        await api.ConsultarAsync(db => db.Empleados.Where(e => e.IdEmpleado == empleado.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.Activo, true)));

        await LoginFacial(empleado.Email, rostro).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    // ── Clientes ──

    [Fact]
    public async Task Clients_CannotUseTheFaceRoutes()
    {
        var cliente = await api.RegistrarClienteAsync();
        var conToken = api.Cliente(cliente.Token);
        var filasAntes = await api.ConsultarAsync(db => db.RostrosEmpleado.CountAsync());

        await conToken.GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.Forbidden);
        await conToken.PutAsJsonAsync("/api/v1/auth/rostro", Activacion(Rostro(23))).EsperarAsync(HttpStatusCode.Forbidden);
        await conToken.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Forbidden);
        await conToken.DeleteAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.Forbidden);

        Assert.Equal(filasAntes, await api.ConsultarAsync(db => db.RostrosEmpleado.CountAsync()));
    }

    [Fact]
    public async Task Login_WithAClientsEmail_IsRejected()
    {
        var email = $"cliente{api.Siguiente()}@test.local";
        await api.Cliente()
            .PostAsJsonAsync("/api/v1/auth/registro-cliente", new { rut = api.Rut(), nombre = "Cli", apellido = "Web", telefono = api.Telefono(), email, password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.Created);

        await LoginFacial(email, Rostro(24)).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FaceRoutes_RequireASession()
    {
        var anonimo = api.Cliente();

        await anonimo.GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.Unauthorized);
        await anonimo.PutAsJsonAsync("/api/v1/auth/rostro", Activacion(Rostro(25))).EsperarAsync(HttpStatusCode.Unauthorized);
        await anonimo.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Unauthorized);
        await anonimo.DeleteAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.Unauthorized);
    }

    // ── Desactivacion ──

    [Fact]
    public async Task Deactivate_ErasesTheDataKeepsTheEvidenceAndNeedsNoPassword()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Dueno);
        var rostro = Rostro(26);
        var token = (await ActivarAsync(empleado.Token, rostro)).GetProperty("tokenEquipo").GetString();

        await api.Cliente(empleado.Token).DeleteAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.NoContent);

        var fila = await api.ConsultarAsync(db => db.RostrosEmpleado.AsNoTracking().SingleAsync(r => r.IdEmpleado == empleado.Id));
        Assert.Null(fila.DescriptorCifrado);
        Assert.Null(fila.TokenEquipoHash);
        Assert.Null(fila.FechaAutorizacionEquipo);
        Assert.NotNull(fila.FechaRevocacion);
        Assert.Equal(ConsentimientoFacial.Version, fila.VersionConsentimiento);

        await LoginFacial(empleado.Email, rostro, token).EsperarAsync(HttpStatusCode.Unauthorized);

        // Sigue pudiendo entrar con su contraseña, y desactivar otra vez no es un error.
        await api.LoginEmpleadoAsync(empleado.Email, ApiFixture.AdminPassword);
        await api.Cliente(empleado.Token).DeleteAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Deactivate_OnlyAffectsTheOwnFace()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var otro = await api.CrearEmpleadoAsync(Roles.Operario);
        var rostroOtro = Rostro(27);
        await ActivarAsync(empleado.Token, Rostro(28));
        await ActivarAsync(otro.Token, rostroOtro);

        await api.Cliente(empleado.Token).DeleteAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.NoContent);

        await LoginFacial(otro.Email, rostroOtro).EsperarAsync(HttpStatusCode.OK);
    }

    // ── Limite de intentos ──

    [Fact]
    public async Task Login_LocksTheFaceAfterFiveFailures_ButNotThePassword()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var rostro = Rostro(29);
        await ActivarAsync(empleado.Token, rostro);

        for (var i = 0; i < RostroFacialService.MaxFallos; i++)
            await LoginFacial(empleado.Email, Rostro(30)).EsperarAsync(HttpStatusCode.Unauthorized);

        // Bloqueado: ni su rostro correcto entra, pero la contraseña sigue funcionando.
        await LoginFacial(empleado.Email, rostro).EsperarAsync(HttpStatusCode.Unauthorized);
        await api.LoginEmpleadoAsync(empleado.Email, ApiFixture.AdminPassword);

        var fila = await FilaVigenteAsync(empleado.Id);
        Assert.True(fila.BloqueadoHasta > DateTimeOffset.UtcNow);

        await BloquearAsync(empleado.Id, DateTimeOffset.UtcNow.AddMinutes(-1));
        await LoginFacial(empleado.Email, rostro).EsperarAsync(HttpStatusCode.OK);

        fila = await FilaVigenteAsync(empleado.Id);
        Assert.Equal(0, fila.IntentosFallidos);
        Assert.Null(fila.BloqueadoHasta);
    }

    [Fact]
    public async Task Login_FewerThanFiveFailures_DoNotLock()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var rostro = Rostro(31);
        await ActivarAsync(empleado.Token, rostro);

        for (var i = 0; i < RostroFacialService.MaxFallos - 1; i++)
            await LoginFacial(empleado.Email, Rostro(32)).EsperarAsync(HttpStatusCode.Unauthorized);

        await LoginFacial(empleado.Email, rostro).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(0, (await FilaVigenteAsync(empleado.Id)).IntentosFallidos);
    }

    [Fact]
    public async Task Login_AppliesTheRateLimit_WithoutBlockingThePasswordLogin()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        await using var limitada = api.ConVariable("RateLimiting__LoginFacial", "3");
        var cliente = limitada.Cliente();
        var cuerpo = new { email = empleado.Email, descriptor = Rostro(33) };

        for (var i = 0; i < 3; i++)
            await cliente.PostAsJsonAsync("/api/v1/auth/login-facial", cuerpo).EsperarAsync(HttpStatusCode.Unauthorized);
        await cliente.PostAsJsonAsync("/api/v1/auth/login-facial", cuerpo).EsperarAsync(HttpStatusCode.TooManyRequests);

        // El contador es propio: quien falla con el rostro debe poder usar su contraseña.
        await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = empleado.Email, password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PasswordConfirmation_IsLimited_WithoutBlockingThePasswordLogin()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        await using var limitada = api.ConVariable("RateLimiting__Reautenticacion", "2");
        var cliente = limitada.Cliente();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", empleado.Token);

        for (var i = 0; i < 2; i++)
            await cliente.PutAsJsonAsync("/api/v1/auth/rostro", ActivacionCon(Rostro(34), "no-es-mi-password")).EsperarAsync(HttpStatusCode.BadRequest);

        await cliente.PutAsJsonAsync("/api/v1/auth/rostro", ActivacionCon(Rostro(34), "no-es-mi-password")).EsperarAsync(HttpStatusCode.TooManyRequests);
        await cliente.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = "no-es-mi-password" }).EsperarAsync(HttpStatusCode.TooManyRequests);
        await cliente.PutAsJsonAsync("/api/v1/clientes/1/credenciales", new { password = "no-es-mi-password", passwordActual = "no-es-mi-password" }).EsperarAsync(HttpStatusCode.TooManyRequests);

        // Equivocarse aqui no debe dejar sin login a quienes comparten la IP.
        await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = empleado.Email, password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_SimultaneousFailures_AreAllCounted()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var rostro = Rostro(46);
        await ActivarAsync(empleado.Token, rostro);

        var intentos = Enumerable.Range(0, RostroFacialService.MaxFallos * 3).Select(_ => LoginFacial(empleado.Email, Rostro(47)));
        foreach (var respuesta in await Task.WhenAll(intentos))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
            respuesta.Dispose();
        }

        Assert.True((await FilaVigenteAsync(empleado.Id)).BloqueadoHasta > DateTimeOffset.UtcNow);
        await LoginFacial(empleado.Email, rostro).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ACorrectAttemptAtTheLimit_StillEnters()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var rostro = Rostro(48);
        await ActivarAsync(empleado.Token, rostro);

        for (var i = 0; i < RostroFacialService.MaxFallos - 1; i++)
            await LoginFacial(empleado.Email, Rostro(49)).EsperarAsync(HttpStatusCode.Unauthorized);

        // Este intento alcanza el limite al contarse, pero es el correcto: no debe quedar bloqueado.
        await LoginFacial(empleado.Email, rostro).EsperarAsync(HttpStatusCode.OK);

        var fila = await FilaVigenteAsync(empleado.Id);
        Assert.Null(fila.BloqueadoHasta);
        Assert.Equal(0, fila.IntentosFallidos);
    }

    [Fact]
    public async Task UnreadableData_IsLoggedWithoutRevealingIt()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        var otro = await api.CrearEmpleadoAsync(Roles.Operario);
        await ActivarAsync(empleado.Token, Rostro(50));
        await ActivarAsync(otro.Token, Rostro(51));
        var ajeno = (await FilaVigenteAsync(otro.Id)).DescriptorCifrado;
        await api.ConsultarAsync(db => db.RostrosEmpleado.Where(r => r.IdEmpleado == empleado.Id && r.FechaRevocacion == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.DescriptorCifrado, ajeno)));
        var logs = new LogsEnMemoria();

        await api.ClienteConLogs(logs).PostAsJsonAsync("/api/v1/auth/login-facial", new { email = empleado.Email, descriptor = Rostro(50) })
            .EsperarAsync(HttpStatusCode.Unauthorized);

        Assert.Contains(logs.Mensajes, m => m.Contains("No se pudo descifrar") && m.Contains(empleado.Id.ToString()));
    }

    // ── Equipo autorizado ──

    [Theory]
    [InlineData(Roles.Dueno)]
    [InlineData(Roles.Administrador)]
    public async Task Privileged_NeedAnAuthorizedDevice(string rol)
    {
        var empleado = await api.CrearEmpleadoAsync(rol);
        var rostro = Rostro(35);
        var token = (await ActivarAsync(empleado.Token, rostro)).GetProperty("tokenEquipo").GetString()!;

        Assert.False(string.IsNullOrEmpty(token));
        await LoginFacial(empleado.Email, rostro).EsperarAsync(HttpStatusCode.Unauthorized);
        await LoginFacial(empleado.Email, rostro, "token-falso").EsperarAsync(HttpStatusCode.Unauthorized);
        await LoginFacial(empleado.Email, rostro, token).EsperarAsync(HttpStatusCode.OK);

        var fila = await FilaVigenteAsync(empleado.Id);
        Assert.NotEqual(Encoding.UTF8.GetBytes(token), fila.TokenEquipoHash);
        Assert.Equal(32, fila.TokenEquipoHash!.Length);
    }

    [Fact]
    public async Task Privileged_FailuresWithoutADevice_DoNotLockTheOwner()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Dueno);
        var rostro = Rostro(36);
        var token = (await ActivarAsync(empleado.Token, rostro)).GetProperty("tokenEquipo").GetString();

        for (var i = 0; i < RostroFacialService.MaxFallos + 1; i++)
            await LoginFacial(empleado.Email, Rostro(37)).EsperarAsync(HttpStatusCode.Unauthorized);

        await LoginFacial(empleado.Email, rostro, token).EsperarAsync(HttpStatusCode.OK);
    }

    // Riesgo aceptado por el usuario: un descriptor copiado o una foto bastan desde cualquier lugar para estos roles.
    [Theory]
    [InlineData(Roles.Operario)]
    [InlineData(Roles.Disenador)]
    public async Task OtherRoles_DoNotNeedADevice(string rol)
    {
        var empleado = await api.CrearEmpleadoAsync(rol);
        var rostro = Rostro(38);
        await ActivarAsync(empleado.Token, rostro);

        await LoginFacial(empleado.Email, rostro).EsperarAsync(HttpStatusCode.OK);
        await api.Cliente(empleado.Token).PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Device_ExpiresAndCanBeAuthorizedAgainWithThePassword()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Administrador);
        var rostro = Rostro(39);
        var viejo = (await ActivarAsync(empleado.Token, rostro)).GetProperty("tokenEquipo").GetString();
        var cliente = api.Cliente(empleado.Token);

        await api.ConsultarAsync(db => db.RostrosEmpleado.Where(r => r.IdEmpleado == empleado.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.FechaAutorizacionEquipo, DateTimeOffset.UtcNow.AddDays(-31))));
        await LoginFacial(empleado.Email, rostro, viejo).EsperarAsync(HttpStatusCode.Unauthorized);
        var vencido = await cliente.GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.OK);
        Assert.False(vencido.GetProperty("equipoVigente").GetBoolean());

        await cliente.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = "no-es-mi-password" }).EsperarAsync(HttpStatusCode.BadRequest);
        await cliente.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { }).EsperarAsync(HttpStatusCode.BadRequest);

        var nuevo = (await cliente.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.OK)).GetProperty("tokenEquipo").GetString();

        await LoginFacial(empleado.Email, rostro, nuevo).EsperarAsync(HttpStatusCode.OK);
        await LoginFacial(empleado.Email, rostro, viejo).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Device_CannotBeAuthorizedWithoutAFace()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Administrador);

        await api.Cliente(empleado.Token).PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.BadRequest);
    }

    // ── Acciones delicadas ──

    [Fact]
    public async Task FaceSession_CannotDoSensitiveActionsWithoutThePassword()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Dueno);
        var rostro = Rostro(40);
        var tokenEquipo = (await ActivarAsync(empleado.Token, rostro)).GetProperty("tokenEquipo").GetString();
        var sesion = (await LoginFacial(empleado.Email, rostro, tokenEquipo).EsperarAsync(HttpStatusCode.OK)).GetProperty("token").GetString();
        var conRostro = api.Cliente(sesion);
        var idCliente = await api.CrearClienteAsync();

        var nuevoEmail = $"e{api.Siguiente()}@test.local";
        await conRostro.PostAsJsonAsync("/api/v1/auth/empleados", new { rut = api.Rut(), nombre = "A", apellido = "B", telefono = api.Telefono(), email = nuevoEmail, password = ApiFixture.AdminPassword, rol = Roles.Dueno })
            .EsperarAsync(HttpStatusCode.BadRequest);
        await conRostro.PutAsJsonAsync($"/api/v1/clientes/{idCliente}/credenciales", new { password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.BadRequest);
        await conRostro.PutAsJsonAsync("/api/v1/auth/rostro", ActivacionCon(Rostro(41), "no-es-mi-password")).EsperarAsync(HttpStatusCode.BadRequest);
        await conRostro.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = "no-es-mi-password" }).EsperarAsync(HttpStatusCode.BadRequest);

        Assert.False(await api.ConsultarAsync(db => db.Empleados.AnyAsync(e => e.Email == nuevoEmail)));

        // Lo mismo con la contraseña correcta funciona: el rechazo se debe a ella.
        await conRostro.PutAsJsonAsync($"/api/v1/clientes/{idCliente}/credenciales", new { password = ApiFixture.AdminPassword, passwordActual = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.NoContent);
    }

    // ── Proteccion de los datos ──

    [Fact]
    public async Task Descriptor_CopiedToAnotherEmployee_CannotBeUsed()
    {
        var victima = await api.CrearEmpleadoAsync(Roles.Operario);
        var atacante = await api.CrearEmpleadoAsync(Roles.Operario);
        var rostroAtacante = Rostro(42);
        await ActivarAsync(victima.Token, Rostro(43));
        await ActivarAsync(atacante.Token, rostroAtacante);

        // Alguien con acceso de escritura a la tabla copia su propio descriptor a la fila de otro.
        var cifrado = (await FilaVigenteAsync(atacante.Id)).DescriptorCifrado;
        await api.ConsultarAsync(db => db.RostrosEmpleado.Where(r => r.IdEmpleado == victima.Id && r.FechaRevocacion == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.DescriptorCifrado, cifrado)));

        await LoginFacial(victima.Email, rostroAtacante).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Responses_NeverIncludeStoredFaceData()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Dueno);
        var rostro = Rostro(44);
        var activacion = await ActivarAsync(empleado.Token, rostro);
        var estado = await api.Cliente(empleado.Token).GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.OK);
        var login = await LoginFacial(empleado.Email, rostro, activacion.GetProperty("tokenEquipo").GetString()).EsperarAsync(HttpStatusCode.OK);
        var empleados = await api.Cliente(empleado.Token).GetAsync("/api/v1/empleados").EsperarAsync(HttpStatusCode.OK);

        foreach (var json in new[] { activacion, estado, login, empleados })
        {
            var texto = json.GetRawText();
            Assert.DoesNotContain("descriptor", texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hash", texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(rostro[0].ToString("R", CultureInfo.InvariantCulture), texto);
        }
    }

    [Fact]
    public async Task Logs_NeverContainDescriptorsOrDeviceTokens()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Dueno);
        var logs = new LogsEnMemoria();
        var cliente = api.ClienteConLogs(logs);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", empleado.Token);

        var rostro = Enumerable.Repeat(0.123456f, RostroFacialService.Dimensiones).ToArray();
        var token = (await cliente.PutAsJsonAsync("/api/v1/auth/rostro", Activacion(rostro)).EsperarAsync(HttpStatusCode.OK))
            .GetProperty("tokenEquipo").GetString()!;

        await cliente.PostAsJsonAsync("/api/v1/auth/login-facial", new { email = empleado.Email, descriptor = rostro, tokenEquipo = token }).EsperarAsync(HttpStatusCode.OK);
        await cliente.PostAsJsonAsync("/api/v1/auth/login-facial", new { email = empleado.Email, descriptor = rostro, tokenEquipo = "token-falso" }).EsperarAsync(HttpStatusCode.Unauthorized);
        await cliente.PostAsJsonAsync("/api/v1/auth/login-facial", new { email = empleado.Email, descriptor = new[] { 0.123456f } }).EsperarAsync(HttpStatusCode.BadRequest);

        // Un JSON roto no debe dejar el contenido en los mensajes de error.
        using var roto = new StringContent("""{"email":"x@test.local","descriptor":[0.123456,"abc" """, Encoding.UTF8, "application/json");
        await cliente.PostAsync("/api/v1/auth/login-facial", roto).EsperarAsync(HttpStatusCode.BadRequest);

        // Control: la captura funciona y llega hasta las consultas de la base.
        Assert.Contains(logs.Mensajes, m => m.Contains("activo su acceso facial"));
        Assert.Contains(logs.Mensajes, m => m.Contains("rostros_empleado"));

        Assert.DoesNotContain(logs.Mensajes, m => m.Contains("0.123456") || m.Contains("0,123456"));
        Assert.DoesNotContain(logs.Mensajes, m => m.Contains(token));
    }

    [Fact]
    public async Task WithoutAKey_FaceIsUnavailableButThePasswordWorks()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        await using var sinClave = api.ConVariable("RostroFacial__ClaveCifrado", "no-es-base64");
        var cliente = sinClave.Cliente();

        foreach (var email in new[] { empleado.Email, $"nadie{api.Siguiente()}@test.local" })
            await cliente.PostAsJsonAsync("/api/v1/auth/login-facial", new { email, descriptor = Rostro(45) }).EsperarAsync(HttpStatusCode.ServiceUnavailable);

        await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = empleado.Email, password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.OK);

        cliente.DefaultRequestHeaders.Authorization = new("Bearer", empleado.Token);
        var estado = await cliente.GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.OK);
        Assert.False(estado.GetProperty("disponible").GetBoolean());
        await cliente.PutAsJsonAsync("/api/v1/auth/rostro", Activacion(Rostro(45))).EsperarAsync(HttpStatusCode.ServiceUnavailable);
    }

    // ── Auxiliares ──

    // Vector reproducible: dos semillas distintas quedan muy lejos entre si respecto del umbral.
    private static float[] Rostro(int semilla)
    {
        var azar = new Random(semilla);
        return Enumerable.Range(0, RostroFacialService.Dimensiones).Select(_ => (float)(azar.NextDouble() * 0.6 - 0.3)).ToArray();
    }

    // La misma persona en otra captura: cambia poco.
    private static float[] Variar(float[] rostro)
    {
        var azar = new Random(rostro.Length);
        return rostro.Select(v => v + (float)(azar.NextDouble() * 0.01 - 0.005)).ToArray();
    }

    private static float[][] Capturas(float[] rostro) => [rostro, rostro, rostro];

    private static object Activacion(float[] rostro) => ActivacionCon(rostro, ApiFixture.AdminPassword);

    private static object ActivacionCon(float[] rostro, string? password) =>
        new { password, consentimiento = true, versionConsentimiento = ConsentimientoFacial.Version, descriptores = Capturas(rostro) };

    private Task<JsonElement> ActivarAsync(string token, float[] rostro) =>
        api.Cliente(token).PutAsJsonAsync("/api/v1/auth/rostro", Activacion(rostro)).EsperarAsync(HttpStatusCode.OK);

    private Task<HttpResponseMessage> LoginFacial(string email, float[] descriptor, string? tokenEquipo = null) =>
        api.Cliente().PostAsJsonAsync("/api/v1/auth/login-facial", new { email, descriptor, tokenEquipo });

    private Task<int> FilasAsync(int idEmpleado) =>
        api.ConsultarAsync(db => db.RostrosEmpleado.CountAsync(r => r.IdEmpleado == idEmpleado));

    private Task<RostroEmpleado> FilaVigenteAsync(int idEmpleado) =>
        api.ConsultarAsync(db => db.RostrosEmpleado.AsNoTracking().SingleAsync(r => r.IdEmpleado == idEmpleado && r.FechaRevocacion == null));

    private Task DesactivarEmpleadoAsync(int idEmpleado) =>
        api.ConsultarAsync(db => db.Empleados.Where(e => e.IdEmpleado == idEmpleado).ExecuteUpdateAsync(s => s.SetProperty(e => e.Activo, false)));

    private Task BloquearAsync(int idEmpleado, DateTimeOffset hasta) =>
        api.ConsultarAsync(db => db.RostrosEmpleado.Where(r => r.IdEmpleado == idEmpleado && r.FechaRevocacion == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.BloqueadoHasta, hasta)));

    // Claims del token, sin las fechas de emision y vencimiento.
    private async Task<string[]> ClaimsAsync(JsonElement login)
    {
        var claims = await api.Cliente(login.GetProperty("token").GetString()).GetAsync("/api/v1/auth/me").EsperarAsync(HttpStatusCode.OK);
        return claims.EnumerateArray()
            .Select(c => $"{c.GetProperty("type").GetString()}={c.GetProperty("value").GetString()}")
            .Where(c => !c.StartsWith("exp=") && !c.StartsWith("iat=") && !c.StartsWith("nbf="))
            .Order()
            .ToArray();
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
