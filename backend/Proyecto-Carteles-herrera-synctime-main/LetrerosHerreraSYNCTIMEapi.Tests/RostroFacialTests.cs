using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 10: rostro segun requisitos, con prueba de vida (giros de cabeza en un orden que elige el servidor).
// Estas pruebas usan vectores y puntos sinteticos: comprueban las reglas del servidor, no la precision del
// reconocimiento ni la del giro, que requieren rostros y camaras reales (eso lo cubre la calibracion).
//
// Cada «persona» tiene tres muestras: la de frente (F) y una por lado (I y D). I y D quedan a 0.55 entre si:
// mas que el umbral contra una muestra (0.5) y menos que el umbral entre capturas (0.6). Asi una captura con
// el descriptor del otro lado no coincide con su muestra, aunque las tres sigan pareciendo de la misma persona.
[Collection(ApiCollection.Name)]
public sealed class RostroFacialTests(ApiFixture api)
{
    private const double GiroModerado = 0.5;

    // ── Desafio ──

    [Fact]
    public async Task Desafio_EligeElOrdenAlAzar_YAparecenLosDos()
    {
        var ordenes = new HashSet<string>();
        for (var i = 0; i < 40; i++)
        {
            var (_, orden) = await DesafioAsync($"azar{api.Siguiente()}@test.local");
            ordenes.Add(string.Join(",", orden));
        }

        Assert.Equal(["derecha,izquierda", "izquierda,derecha"], ordenes.Order());
    }

    [Fact]
    public async Task Desafio_RespondeIgual_ExistaONoElEmail()
    {
        var empleado = await GestionConRostroAsync(1);

        var conCuenta = await api.Cliente().PostAsJsonAsync("/api/v1/auth/login-facial/desafio", new { email = empleado.Email }).EsperarAsync(HttpStatusCode.OK);
        var sinCuenta = await api.Cliente().PostAsJsonAsync("/api/v1/auth/login-facial/desafio", new { email = $"nadie{api.Siguiente()}@test.local" })
            .EsperarAsync(HttpStatusCode.OK);

        foreach (var respuesta in new[] { conCuenta, sinCuenta })
        {
            Assert.Equal(["desafio", "orden", "venceEnSegundos"], respuesta.EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal(43, respuesta.GetProperty("desafio").GetString()!.Length);
            Assert.Equal(2, respuesta.GetProperty("orden").GetArrayLength());
            Assert.Equal(30, respuesta.GetProperty("venceEnSegundos").GetInt32());
        }
        Assert.NotEqual(conCuenta.GetProperty("desafio").GetString(), sinCuenta.GetProperty("desafio").GetString());
    }

    [Fact]
    public async Task Desafio_SeGuardaSoloComoHash()
    {
        var email = $"Hash{api.Siguiente()}@Test.local";
        var (desafio, _) = await DesafioAsync(email);

        var hashToken = Hex(desafio);
        var fila = await api.ConsultarAsync(db => db.DesafiosRostro.AsNoTracking().SingleAsync(d => d.TokenHash == hashToken));
        Assert.Equal(Hex(email.ToLowerInvariant()), fila.CorreoHash);
        Assert.Null(fila.Usado);
        Assert.InRange((fila.Vence - fila.Emitido).TotalSeconds, 29, 31);
        Assert.False(await api.ConsultarAsync(db => db.DesafiosRostro.AnyAsync(d => d.TokenHash == desafio || d.CorreoHash == email)));
    }

    [Fact]
    public async Task Desafio_SinEmail_EsUnErrorDeFormato()
    {
        var error = await api.Cliente().PostAsJsonAsync("/api/v1/auth/login-facial/desafio", new { }).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("datos_invalidos", error.GetProperty("codigo").GetString());
    }

    // ── Ingreso ──

    [Theory]
    [InlineData(Roles.Administrador)]
    [InlineData(Roles.Dueno)]
    public async Task Ingreso_ConLasTresCapturas_EntraYDiceQuienEntro(string rol)
    {
        var empleado = await GestionConRostroAsync(2, rol);

        var conRostro = await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.OK);
        var conPassword = await api.Cliente()
            .PostAsJsonAsync("/api/v1/auth/login", new { email = empleado.Email, password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.OK);

        Assert.Equal("Emp Prueba", conRostro.GetProperty("nombre").GetString());
        Assert.Equal(rol, conRostro.GetProperty("rol").GetString());
        Assert.Equal(conPassword.GetProperty("usuario").GetRawText(), conRostro.GetProperty("usuario").GetRawText());
        Assert.Equal(await ClaimsAsync(conPassword), await ClaimsAsync(conRostro));
    }

    [Fact]
    public async Task Desafio_EsDeUnSoloUso()
    {
        var empleado = await GestionConRostroAsync(3);
        var (desafio, orden) = await DesafioAsync(empleado.Email);

        await Login(empleado.Email, desafio, empleado.TokenEquipo, Capturas(empleado.Persona, orden)).EsperarAsync(HttpStatusCode.OK);
        await Login(empleado.Email, desafio, empleado.TokenEquipo, Capturas(empleado.Persona, orden)).EsperarAsync(HttpStatusCode.Unauthorized);

        var hashToken = Hex(desafio);
        Assert.NotNull(await api.ConsultarAsync(db => db.DesafiosRostro.AsNoTracking().Where(d => d.TokenHash == hashToken).Select(d => d.Usado).SingleAsync()));
    }

    [Fact]
    public async Task Desafio_ConDosEnviosSimultaneos_SoloUnoEntra()
    {
        var empleado = await GestionConRostroAsync(4);

        for (var ronda = 0; ronda < 3; ronda++)
        {
            var (desafio, orden) = await DesafioAsync(empleado.Email);
            var cuerpo = Capturas(empleado.Persona, orden);
            var respuestas = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Login(empleado.Email, desafio, empleado.TokenEquipo, cuerpo)));

            Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.OK));
            Assert.Equal(3, respuestas.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
            foreach (var respuesta in respuestas)
                respuesta.Dispose();
            await LimpiarIntentosAsync(empleado.Id);
        }
    }

    [Fact]
    public async Task Ingreso_ConLosGirosEnElOrdenEquivocado_SeRechaza()
    {
        var empleado = await GestionConRostroAsync(5);
        var (desafio, orden) = await DesafioAsync(empleado.Email);
        var p = empleado.Persona;

        // La persona giro primero hacia el lado contrario: cada captura con su descriptor y su giro reales.
        var invertido = orden.Reverse().ToArray();
        await Login(empleado.Email, desafio, empleado.TokenEquipo, Capturas(p, invertido)).EsperarAsync(HttpStatusCode.Unauthorized);

        // Los descriptores que pide el orden, pero los puntos dicen que giro al reves: el giro lo mide el servidor.
        (desafio, orden) = await DesafioAsync(empleado.Email);
        var capturas = new[]
        {
            Captura(Variar(p.Frente), 0),
            Captura(Variar(p.Lado(orden[0])), -Signo(orden[0]) * GiroModerado),
            Captura(Variar(p.Lado(orden[1])), -Signo(orden[1]) * GiroModerado)
        };
        await Login(empleado.Email, desafio, empleado.TokenEquipo, capturas).EsperarAsync(HttpStatusCode.Unauthorized);

        // Control: en el orden pedido entra.
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.29)]
    [InlineData(0.76)]
    [InlineData(0.95)]
    public async Task Ingreso_ConUnGiroInsuficienteOExcesivo_SeRechaza(double giro)
    {
        var empleado = await GestionConRostroAsync(6);

        await IngresarAsync(empleado, giro: giro).EsperarAsync(HttpStatusCode.Unauthorized);
        await IngresarAsync(empleado, giro: GiroModerado).EsperarAsync(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(-0.3)]
    public async Task Ingreso_ConLaCapturaDeFrenteGirada_SeRechaza(double giroFrente)
    {
        var empleado = await GestionConRostroAsync(7);

        await IngresarAsync(empleado, giroFrente: giroFrente).EsperarAsync(HttpStatusCode.Unauthorized);
        await IngresarAsync(empleado, giroFrente: 0.1).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ingreso_ConUnDesafioVencido_SeRechaza()
    {
        var empleado = await GestionConRostroAsync(8);
        var (desafio, orden) = await DesafioAsync(empleado.Email);
        var despues = api.ClienteConReloj(TimeSpan.FromSeconds(31));

        await despues.PostAsJsonAsync("/api/v1/auth/login-facial", CuerpoLogin(empleado.Email, desafio, empleado.TokenEquipo, Capturas(empleado.Persona, orden)))
            .EsperarAsync(HttpStatusCode.Unauthorized);

        // Control: a los 20 segundos todavia sirve.
        (desafio, orden) = await DesafioAsync(empleado.Email);
        await api.ClienteConReloj(TimeSpan.FromSeconds(20))
            .PostAsJsonAsync("/api/v1/auth/login-facial", CuerpoLogin(empleado.Email, desafio, empleado.TokenEquipo, Capturas(empleado.Persona, orden)))
            .EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ingreso_ConUnDesafioDeOtroEmailOInventado_SeRechaza()
    {
        var empleado = await GestionConRostroAsync(9);
        var (ajeno, orden) = await DesafioAsync($"otro{api.Siguiente()}@test.local");

        await Login(empleado.Email, ajeno, empleado.TokenEquipo, Capturas(empleado.Persona, orden)).EsperarAsync(HttpStatusCode.Unauthorized);
        await Login(empleado.Email, RostroFacialService.CrearToken(), empleado.TokenEquipo, Capturas(empleado.Persona, ["izquierda", "derecha"]))
            .EsperarAsync(HttpStatusCode.Unauthorized);
        await Login(empleado.Email, null, empleado.TokenEquipo, Capturas(empleado.Persona, ["izquierda", "derecha"])).EsperarAsync(HttpStatusCode.Unauthorized);
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ingreso_ConCapturasDePersonasDistintas_SeRechaza()
    {
        var empleado = await GestionConRostroAsync(10);
        var (desafio, orden) = await DesafioAsync(empleado.Email);
        var p = empleado.Persona;

        // Cada captura queda a 0.45 de su muestra (bajo el umbral de 0.5), pero entre ellas quedan lejos:
        // no pueden ser la misma persona en un mismo intento.
        var capturas = new[]
        {
            Captura(Desplazar(p.Frente, 2, -0.45f), 0),
            Captura(Desplazar(p.Lado(orden[0]), orden[0] == "izquierda" ? 0 : 1, 0.45f), Signo(orden[0]) * GiroModerado),
            Captura(Desplazar(p.Lado(orden[1]), orden[1] == "izquierda" ? 0 : 1, 0.45f), Signo(orden[1]) * GiroModerado)
        };
        await Login(empleado.Email, desafio, empleado.TokenEquipo, capturas).EsperarAsync(HttpStatusCode.Unauthorized);

        // Y con la cara de otra persona en el segundo giro, tampoco.
        (desafio, orden) = await DesafioAsync(empleado.Email);
        var otra = Sintetica(11);
        capturas = [Captura(Variar(p.Frente), 0), Captura(Variar(p.Lado(orden[0])), Signo(orden[0]) * GiroModerado), Captura(otra.Lado(orden[1]), Signo(orden[1]) * GiroModerado)];
        await Login(empleado.Email, desafio, empleado.TokenEquipo, capturas).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ingreso_ConUnaCapturaQueNoCoincideConSuLado_SeRechaza()
    {
        var empleado = await GestionConRostroAsync(12);
        var (desafio, orden) = await DesafioAsync(empleado.Email);
        var p = empleado.Persona;

        // El primer giro trae el descriptor del otro lado (con el giro pedido): no coincide con su muestra.
        var capturas = new[]
        {
            Captura(Variar(p.Frente), 0),
            Captura(Variar(p.Lado(orden[1])), Signo(orden[0]) * GiroModerado),
            Captura(Variar(p.Lado(orden[1])), Signo(orden[1]) * GiroModerado)
        };
        await Login(empleado.Email, desafio, empleado.TokenEquipo, capturas).EsperarAsync(HttpStatusCode.Unauthorized);

        // Una captura de frente que queda a 0.55 de su muestra tampoco sirve.
        (desafio, orden) = await DesafioAsync(empleado.Email);
        capturas = [Captura(Desplazar(p.Frente, 3, 0.55f), 0), Captura(Variar(p.Lado(orden[0])), Signo(orden[0]) * GiroModerado),
            Captura(Variar(p.Lado(orden[1])), Signo(orden[1]) * GiroModerado)];
        await Login(empleado.Email, desafio, empleado.TokenEquipo, capturas).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ingreso_MalFormado_EsElMismo401()
    {
        var empleado = await GestionConRostroAsync(13);
        var p = empleado.Persona;

        async Task Rechazado(Func<string[], object[]> capturas)
        {
            var (desafio, orden) = await DesafioAsync(empleado.Email);
            await Login(empleado.Email, desafio, empleado.TokenEquipo, capturas(orden)).EsperarAsync(HttpStatusCode.Unauthorized);
        }

        await Rechazado(o => Capturas(empleado.Persona, o).Take(2).ToArray());
        await Rechazado(o => Capturas(empleado.Persona, o).Append(Captura(p.Frente, 0)).ToArray());
        await Rechazado(o => [new { descriptor = new float[10], puntos = Puntos(0), ancho = 640, alto = 480 }, .. Capturas(p, o).Skip(1)]);
        await Rechazado(o => [new { descriptor = p.Frente, puntos = Puntos(0).Take(67).ToArray(), ancho = 640, alto = 480 }, .. Capturas(p, o).Skip(1)]);
        await Rechazado(o => [new { descriptor = p.Frente, puntos = Puntos(0), ancho = 0, alto = 480 }, .. Capturas(p, o).Skip(1)]);
        await Rechazado(o => [new { descriptor = p.Frente, puntos = Puntos(0) }, .. Capturas(p, o).Skip(1)]);

        // Un JSON que no se puede leer es un 400 que no depende de la cuenta.
        using var roto = new StringContent("""{"email":"x@test.local","capturas":[1,"abc" """, Encoding.UTF8, "application/json");
        await api.Cliente().PostAsync("/api/v1/auth/login-facial", roto).EsperarAsync(HttpStatusCode.BadRequest);
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ElServidorResponde_EnTresSegundosOMenos()
    {
        var empleado = await GestionConRostroAsync(14);
        foreach (var esperado in new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.OK })
        {
            var (desafio, orden) = await DesafioAsync(empleado.Email);
            var capturas = esperado == HttpStatusCode.OK ? Capturas(empleado.Persona, orden) : Capturas(Sintetica(15), orden);

            // RNFP12: se mide desde que llegan las capturas hasta la respuesta.
            var reloj = Stopwatch.StartNew();
            await Login(empleado.Email, desafio, empleado.TokenEquipo, capturas).EsperarAsync(esperado);
            reloj.Stop();

            Assert.True(reloj.Elapsed <= TimeSpan.FromSeconds(3), $"respondio en {reloj.ElapsedMilliseconds} ms");
        }
    }

    // ── Solo gestion ──

    [Theory]
    [InlineData(Roles.Operario)]
    [InlineData(Roles.Disenador)]
    public async Task OtrosRoles_NoEntranConRostro_AunqueTenganUnoGuardado(string rol)
    {
        var empleado = await api.CrearEmpleadoAsync(rol);
        var persona = Sintetica(16);
        var tokenEquipo = await InsertarRostroAsync(empleado.Id, persona);

        var (desafio, orden) = await DesafioAsync(empleado.Email);
        await Login(empleado.Email, desafio, tokenEquipo, Capturas(persona, orden)).EsperarAsync(HttpStatusCode.Unauthorized);
        (desafio, orden) = await DesafioAsync(empleado.Email);
        await Login(empleado.Email, desafio, null, Capturas(persona, orden)).EsperarAsync(HttpStatusCode.Unauthorized);

        // Sus intentos no se cuentan: no hay nada que bloquear.
        Assert.Equal(0, (await FilaVigenteAsync(empleado.Id)).IntentosFallidos);
    }

    [Theory]
    [InlineData(Roles.Operario)]
    [InlineData(Roles.Disenador)]
    public async Task OtrosRoles_NoPuedenRegistrarSuRostro(string rol)
    {
        var empleado = await api.CrearEmpleadoAsync(rol);
        var cliente = api.Cliente(empleado.Token);

        var error = await cliente.PutAsJsonAsync("/api/v1/auth/rostro", Registro(Sintetica(17))).EsperarAsync(HttpStatusCode.Forbidden);
        Assert.Equal("solo_gestion", error.GetProperty("codigo").GetString());
        var equipo = await cliente.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Forbidden);
        Assert.Equal("solo_gestion", equipo.GetProperty("codigo").GetString());

        var estado = await cliente.GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.OK);
        Assert.False(estado.GetProperty("puedeUsarRostro").GetBoolean());
        Assert.Equal(0, await FilasAsync(empleado.Id));

        // Tampoco ven «Rostros registrados» ni la calibracion.
        await cliente.GetAsync("/api/v1/rostros").EsperarAsync(HttpStatusCode.Forbidden);
        await cliente.GetAsync("/api/v1/rostros/calibracion").EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Gestion_PuedeUsarElRostro()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Administrador);
        var estado = await api.Cliente(empleado.Token).GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.OK);

        Assert.True(estado.GetProperty("puedeUsarRostro").GetBoolean());
        Assert.False(estado.GetProperty("activo").GetBoolean());
        Assert.False(estado.GetProperty("debeRegistrarseDeNuevo").GetBoolean());
        Assert.True(estado.GetProperty("requiereEquipo").GetBoolean());
    }

    // ── Registro ──

    [Fact]
    public async Task Registro_GuardaLasTresMuestrasCifradas_ConSuConsentimiento()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Administrador);
        var persona = Sintetica(18);
        var antes = DateTimeOffset.UtcNow.AddSeconds(-2);

        var respuesta = await api.Cliente(empleado.Token).PutAsJsonAsync("/api/v1/auth/rostro", Registro(persona)).EsperarAsync(HttpStatusCode.OK);

        Assert.True(respuesta.GetProperty("activo").GetBoolean());
        Assert.False(string.IsNullOrEmpty(respuesta.GetProperty("tokenEquipo").GetString()));
        var fila = await FilaVigenteAsync(empleado.Id);
        Assert.Equal(ConsentimientoFacial.Version, fila.VersionConsentimiento);
        Assert.InRange(fila.FechaConsentimiento, antes, DateTimeOffset.UtcNow.AddSeconds(2));
        Assert.Equal(RostroEmpleado.FormatoTresMuestras, fila.FormatoMuestras);
        Assert.Equal(empleado.Id, fila.IdEmpleadoRegistra);
        Assert.Null(fila.DescriptorCifrado);

        // En la base no aparece ningun valor de las muestras tal como se enviaron, y cada una se cifra aparte.
        var columnas = new[] { fila.DescriptorFrente!, fila.DescriptorIzquierda!, fila.DescriptorDerecha! };
        Assert.Equal(3, columnas.Select(Convert.ToBase64String).Distinct().Count());
        foreach (var (cifrado, claro) in columnas.Zip(new[] { persona.Frente, persona.Izquierda, persona.Derecha }))
        {
            var bytes = new byte[claro.Length * sizeof(float)];
            Buffer.BlockCopy(claro, 0, bytes, 0, bytes.Length);
            Assert.Equal(-1, cifrado.AsSpan().IndexOf(bytes.AsSpan(0, sizeof(float) * 2)));
            Assert.Equal(-1, cifrado.AsSpan().IndexOf(bytes.AsSpan(sizeof(float) * 10, sizeof(float) * 2)));
        }

        var estado = await api.Cliente(empleado.Token).GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.OK);
        Assert.True(estado.GetProperty("activo").GetBoolean());
        Assert.False(estado.GetProperty("debeRegistrarseDeNuevo").GetBoolean());
        Assert.Equal(ConsentimientoFacial.Version, estado.GetProperty("consentimiento").GetProperty("version").GetString());
    }

    public static TheoryData<string, string, double> MuestrasConMalGiro() => new()
    {
        { "frente", "muestras.frente", 0.3 },
        { "izquierda", "muestras.izquierda", 0.1 },
        { "izquierda", "muestras.izquierda", -0.5 },
        { "izquierda", "muestras.izquierda", 0.9 },
        { "derecha", "muestras.derecha", -0.1 },
        { "derecha", "muestras.derecha", 0.5 },
        { "derecha", "muestras.derecha", -0.9 }
    };

    [Theory]
    [MemberData(nameof(MuestrasConMalGiro))]
    public async Task Registro_RevisaElGiroDeCadaMuestra(string muestra, string campo, double giro)
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Administrador);
        var p = Sintetica(19);
        var muestras = new Dictionary<string, object>
        {
            ["frente"] = Captura(p.Frente, muestra == "frente" ? giro : 0),
            ["izquierda"] = Captura(p.Izquierda, muestra == "izquierda" ? giro : GiroModerado),
            ["derecha"] = Captura(p.Derecha, muestra == "derecha" ? giro : -GiroModerado)
        };

        var error = await api.Cliente(empleado.Token).PutAsJsonAsync("/api/v1/auth/rostro", RegistroCon(muestras, ApiFixture.AdminPassword))
            .EsperarAsync(HttpStatusCode.BadRequest);

        var errores = error.GetProperty("errores");
        Assert.Equal([campo], errores.EnumerateObject().Select(e => e.Name));
        Assert.Matches("Rep(i|í)t", errores.GetProperty(campo)[0].GetString());
        Assert.Equal(0, await FilasAsync(empleado.Id));
    }

    [Fact]
    public async Task Registro_ExigeQueLasTresMuestrasSeanDeLaMismaPersona()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Administrador);
        var p = Sintetica(20);
        var otra = Sintetica(21);
        var muestras = new { frente = Captura(p.Frente, 0), izquierda = Captura(p.Izquierda, GiroModerado), derecha = Captura(otra.Derecha, -GiroModerado) };

        var error = await api.Cliente(empleado.Token).PutAsJsonAsync("/api/v1/auth/rostro", RegistroCon(muestras, ApiFixture.AdminPassword))
            .EsperarAsync(HttpStatusCode.BadRequest);

        // Se culpa a la que queda lejos de las otras dos.
        Assert.Equal(["muestras.derecha"], error.GetProperty("errores").EnumerateObject().Select(e => e.Name));
        Assert.Equal(0, await FilasAsync(empleado.Id));
    }

    [Fact]
    public async Task Registro_SinUnaMuestraOConUnaIlegible_SeRechaza()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Administrador);
        var p = Sintetica(22);

        var error = await api.Cliente(empleado.Token)
            .PutAsJsonAsync("/api/v1/auth/rostro", RegistroCon(new { frente = Captura(p.Frente, 0), izquierda = Captura(new float[128], GiroModerado) }, ApiFixture.AdminPassword))
            .EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Equal(["muestras.derecha", "muestras.izquierda"], error.GetProperty("errores").EnumerateObject().Select(e => e.Name).Order());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-mi-password")]
    public async Task Registro_SinLaContraseñaCorrecta_SeRechaza(string? password)
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Administrador);

        var error = await api.Cliente(empleado.Token).PutAsJsonAsync("/api/v1/auth/rostro", RegistroCon(Muestras(Sintetica(23)), password)).EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Equal("La contraseña actual no es correcta.", error.GetProperty("error").GetString());
        Assert.True(error.GetProperty("errores").TryGetProperty("password", out _));
        Assert.Equal(0, await FilasAsync(empleado.Id));
    }

    [Fact]
    public async Task Registro_SinConsentimiento_SeRechaza()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Administrador);
        var cliente = api.Cliente(empleado.Token);
        var muestras = Muestras(Sintetica(24));

        await cliente.PutAsJsonAsync("/api/v1/auth/rostro", new { password = ApiFixture.AdminPassword, consentimiento = false, versionConsentimiento = ConsentimientoFacial.Version, muestras })
            .EsperarAsync(HttpStatusCode.BadRequest);
        await cliente.PutAsJsonAsync("/api/v1/auth/rostro", new { password = ApiFixture.AdminPassword, versionConsentimiento = ConsentimientoFacial.Version, muestras })
            .EsperarAsync(HttpStatusCode.BadRequest);
        await cliente.PutAsJsonAsync("/api/v1/auth/rostro", new { password = ApiFixture.AdminPassword, consentimiento = true, versionConsentimiento = "2026-11-06", muestras })
            .EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Equal(0, await FilasAsync(empleado.Id));
    }

    [Fact]
    public async Task Registro_DeNuevo_ReemplazaElRostroYConservaLaConstancia()
    {
        var empleado = await GestionConRostroAsync(25);
        var nueva = Sintetica(26);

        var token = (await api.Cliente(empleado.Token).PutAsJsonAsync("/api/v1/auth/rostro", Registro(nueva)).EsperarAsync(HttpStatusCode.OK))
            .GetProperty("tokenEquipo").GetString()!;

        var filas = await api.ConsultarAsync(db => db.RostrosEmpleado.AsNoTracking().Where(r => r.IdEmpleado == empleado.Id).OrderBy(r => r.IdRostro).ToListAsync());
        Assert.Equal(2, filas.Count);
        Assert.Null(filas[0].DescriptorFrente);
        Assert.NotNull(filas[0].FechaRevocacion);
        Assert.True(filas[1].TieneTresMuestras);

        await IngresarAsync(empleado with { TokenEquipo = token }).EsperarAsync(HttpStatusCode.Unauthorized);
        await IngresarAsync(empleado with { TokenEquipo = token, Persona = nueva }).EsperarAsync(HttpStatusCode.OK);
    }

    // ── Rostro anterior a la fase 10 ──

    [Fact]
    public async Task UnRostroSinLasTresMuestras_DebeRegistrarseDeNuevo()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Administrador);
        var persona = Sintetica(27);
        var token = RostroFacialService.CrearTokenEquipo();
        var servicio = api.Services.GetRequiredService<RostroFacialService>();
        await api.ConsultarAsync(async db =>
        {
            // Como quedo el rostro del usuario en la base real: un solo descriptor promedio.
            db.RostrosEmpleado.Add(new RostroEmpleado
            {
                IdEmpleado = empleado.Id, DescriptorCifrado = servicio.Cifrar(persona.Frente, empleado.Id), FechaConsentimiento = DateTimeOffset.UtcNow,
                VersionConsentimiento = "2026-11-06", TokenEquipoHash = RostroFacialService.HashToken(token), FechaAutorizacionEquipo = DateTimeOffset.UtcNow
            });
            return await db.SaveChangesAsync();
        });

        var estado = await api.Cliente(empleado.Token).GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.OK);
        Assert.True(estado.GetProperty("debeRegistrarseDeNuevo").GetBoolean());
        Assert.False(estado.GetProperty("activo").GetBoolean());
        var fila = (await api.Cliente(await api.TokenAdminAsync()).GetAsync("/api/v1/rostros").EsperarAsync(HttpStatusCode.OK))
            .EnumerateArray().Single(f => f.GetProperty("idEmpleado").GetInt32() == empleado.Id);
        Assert.Equal("debe_registrarse", fila.GetProperty("estado").GetString());

        await IngresarAsync(new ConRostro(empleado.Id, empleado.Email, empleado.Token, token, persona)).EsperarAsync(HttpStatusCode.Unauthorized);

        // Al registrar las tres muestras, el descriptor antiguo queda borrado con la fila anterior.
        await api.Cliente(empleado.Token).PutAsJsonAsync("/api/v1/auth/rostro", Registro(persona)).EsperarAsync(HttpStatusCode.OK);
        Assert.Null(await api.ConsultarAsync(db => db.RostrosEmpleado.AsNoTracking().Where(r => r.IdEmpleado == empleado.Id && r.FechaRevocacion != null)
            .Select(r => r.DescriptorCifrado).SingleAsync()));
    }

    // ── Desactivacion ──

    [Fact]
    public async Task Desactivar_ElTitular_BorraLasCuatroColumnasCifradas_SinPedirContraseña()
    {
        var empleado = await GestionConRostroAsync(28);
        await DejarDescriptorAntiguoAsync(empleado.Id);

        await api.Cliente(empleado.Token).DeleteAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.NoContent);

        await RevocadoSinDatosAsync(empleado.Id);
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.Unauthorized);
        // Sigue entrando con su contraseña, y desactivar otra vez no es un error.
        await api.LoginEmpleadoAsync(empleado.Email, ApiFixture.AdminPassword);
        await api.Cliente(empleado.Token).DeleteAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Desactivar_PorGestion_BorraLasCuatroColumnasCifradas()
    {
        var empleado = await GestionConRostroAsync(29);
        await DejarDescriptorAntiguoAsync(empleado.Id);
        var otro = await GestionConRostroAsync(30);

        await api.Cliente(await api.TokenAdminAsync()).DeleteAsync($"/api/v1/rostros/{empleado.Id}").EsperarAsync(HttpStatusCode.NoContent);

        await RevocadoSinDatosAsync(empleado.Id);
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.Unauthorized);
        // Solo el de esa persona.
        await IngresarAsync(otro).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Desactivar_OtroRol_SoloPuedeConElPropio()
    {
        var operario = await api.CrearEmpleadoAsync(Roles.Operario);
        await InsertarRostroAsync(operario.Id, Sintetica(31));
        var gestion = await GestionConRostroAsync(32);
        var cliente = api.Cliente(operario.Token);

        await cliente.DeleteAsync($"/api/v1/rostros/{gestion.Id}").EsperarAsync(HttpStatusCode.Forbidden);
        await IngresarAsync(gestion).EsperarAsync(HttpStatusCode.OK);

        await cliente.DeleteAsync($"/api/v1/rostros/{operario.Id}").EsperarAsync(HttpStatusCode.NoContent);
        await RevocadoSinDatosAsync(operario.Id);
    }

    [Fact]
    public async Task DesactivarAlEmpleado_BorraSuRostro_YNoEntraMas()
    {
        var empleado = await GestionConRostroAsync(33);
        var admin = api.Cliente(await api.TokenAdminAsync());
        var ficha = await admin.GetAsync($"/api/v1/empleados/{empleado.Id}").EsperarAsync(HttpStatusCode.OK);

        await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/empleados/{empleado.Id}/activo", ficha.GetProperty("rowVersion").GetString()!,
            new { activo = false })).EsperarAsync(HttpStatusCode.OK);

        await RevocadoSinDatosAsync(empleado.Id);
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnEmpleadoDesactivadoPorSql_NoEntra_YSusDatosSeBorran()
    {
        var empleado = await GestionConRostroAsync(34);
        await api.ConsultarAsync(db => db.Empleados.Where(e => e.IdEmpleado == empleado.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.Activo, false)));

        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.Unauthorized);

        await RevocadoSinDatosAsync(empleado.Id);
        // Reactivarlo no le devuelve el rostro.
        await api.ConsultarAsync(db => db.Empleados.Where(e => e.IdEmpleado == empleado.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.Activo, true)));
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    // ── Limite de intentos ──

    [Fact]
    public async Task TrasCincoFallos_SeBloqueaElRostro_PeroNoLaContraseña()
    {
        var empleado = await GestionConRostroAsync(35);
        var ajena = Sintetica(36);

        for (var i = 0; i < RostroFacialService.MaxFallos; i++)
            await IngresarAsync(empleado with { Persona = ajena }).EsperarAsync(HttpStatusCode.Unauthorized);

        // Bloqueado: ni su rostro correcto entra, pero la contraseña sigue funcionando.
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.Unauthorized);
        await api.LoginEmpleadoAsync(empleado.Email, ApiFixture.AdminPassword);
        Assert.True((await FilaVigenteAsync(empleado.Id)).BloqueadoHasta > DateTimeOffset.UtcNow);

        await api.ConsultarAsync(db => db.RostrosEmpleado.Where(r => r.IdEmpleado == empleado.Id && r.FechaRevocacion == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.BloqueadoHasta, DateTimeOffset.UtcNow.AddMinutes(-1))));
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.OK);

        var fila = await FilaVigenteAsync(empleado.Id);
        Assert.Equal(0, fila.IntentosFallidos);
        Assert.Null(fila.BloqueadoHasta);
    }

    [Fact]
    public async Task MenosDeCincoFallos_NoBloquean_YUnAciertoLimpiaElContador()
    {
        var empleado = await GestionConRostroAsync(37);

        for (var i = 0; i < RostroFacialService.MaxFallos - 1; i++)
            await IngresarAsync(empleado, giro: 0.1).EsperarAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(RostroFacialService.MaxFallos - 1, (await FilaVigenteAsync(empleado.Id)).IntentosFallidos);

        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(0, (await FilaVigenteAsync(empleado.Id)).IntentosFallidos);
    }

    [Fact]
    public async Task LosFallosSinEquipo_NoBloqueanAlTitular()
    {
        var empleado = await GestionConRostroAsync(38);

        for (var i = 0; i < RostroFacialService.MaxFallos + 1; i++)
        {
            await IngresarAsync(empleado with { TokenEquipo = null! }).EsperarAsync(HttpStatusCode.Unauthorized);
            await IngresarAsync(empleado with { TokenEquipo = "token-falso" }).EsperarAsync(HttpStatusCode.Unauthorized);
        }

        Assert.Equal(0, (await FilaVigenteAsync(empleado.Id)).IntentosFallidos);
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ElLimitePorIp_CuentaElDesafioYElIngreso_SinBloquearLaContraseña()
    {
        var empleado = await GestionConRostroAsync(39);
        await using var limitada = api.ConVariable("RateLimiting__LoginFacial", "3");
        var cliente = limitada.Cliente();

        var desafio = await cliente.PostAsJsonAsync("/api/v1/auth/login-facial/desafio", new { email = empleado.Email }).EsperarAsync(HttpStatusCode.OK);
        var orden = desafio.GetProperty("orden").EnumerateArray().Select(o => o.GetString()!).ToArray();
        var cuerpo = CuerpoLogin(empleado.Email, desafio.GetProperty("desafio").GetString(), empleado.TokenEquipo, Capturas(Sintetica(40), orden));
        await cliente.PostAsJsonAsync("/api/v1/auth/login-facial", cuerpo).EsperarAsync(HttpStatusCode.Unauthorized);
        await cliente.PostAsJsonAsync("/api/v1/auth/login-facial", cuerpo).EsperarAsync(HttpStatusCode.Unauthorized);
        await cliente.PostAsJsonAsync("/api/v1/auth/login-facial", cuerpo).EsperarAsync(HttpStatusCode.TooManyRequests);
        await cliente.PostAsJsonAsync("/api/v1/auth/login-facial/desafio", new { email = empleado.Email }).EsperarAsync(HttpStatusCode.TooManyRequests);

        // El contador es propio: quien falla con el rostro debe poder usar su contraseña.
        await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = empleado.Email, password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FallosSimultaneos_SeCuentanTodos()
    {
        var empleado = await GestionConRostroAsync(41);
        var ajena = Sintetica(42);

        var intentos = Enumerable.Range(0, RostroFacialService.MaxFallos * 3).Select(_ => IngresarAsync(empleado with { Persona = ajena }));
        foreach (var respuesta in await Task.WhenAll(intentos))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
            respuesta.Dispose();
        }

        Assert.True((await FilaVigenteAsync(empleado.Id)).BloqueadoHasta > DateTimeOffset.UtcNow);
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LosFallos_SonIndistinguibles()
    {
        var sinRostro = await api.CrearEmpleadoAsync(Roles.Administrador);
        var fallaRostro = await GestionConRostroAsync(43);
        var inactivo = await GestionConRostroAsync(44);
        await api.ConsultarAsync(db => db.Empleados.Where(e => e.IdEmpleado == inactivo.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.Activo, false)));
        var revocado = await GestionConRostroAsync(45);
        await api.Cliente(revocado.Token).DeleteAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.NoContent);
        var bloqueado = await GestionConRostroAsync(46);
        await api.ConsultarAsync(db => db.RostrosEmpleado.Where(r => r.IdEmpleado == bloqueado.Id && r.FechaRevocacion == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.BloqueadoHasta, DateTimeOffset.UtcNow.AddMinutes(10))));
        var sinEquipo = await GestionConRostroAsync(47);
        var operario = await api.CrearEmpleadoAsync(Roles.Operario);
        var personaOperario = Sintetica(48);
        var equipoOperario = await InsertarRostroAsync(operario.Id, personaOperario);
        var ajena = Sintetica(49);

        var casos = new ConRostro[]
        {
            new(0, $"nadie{api.Siguiente()}@test.local", "", "token", ajena),
            new(sinRostro.Id, sinRostro.Email, sinRostro.Token, "token", ajena),
            fallaRostro with { Persona = ajena },
            inactivo,
            revocado,
            bloqueado,
            sinEquipo with { TokenEquipo = "token-falso" },
            new(operario.Id, operario.Email, operario.Token, equipoOperario, personaOperario)
        };

        var cuerpos = new HashSet<string>();
        foreach (var caso in casos)
        {
            var reloj = Stopwatch.StartNew();
            using var respuesta = await IngresarAsync(caso);
            reloj.Stop();

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
            Assert.Empty(respuesta.Headers.WwwAuthenticate);
            // Se deja margen por la resolucion del temporizador.
            Assert.True(reloj.ElapsedMilliseconds >= ApiFixture.TiempoMinimoFalloMs - 15, $"{caso.Email} respondio en {reloj.ElapsedMilliseconds} ms");

            // El cuerpo es el error estandar. Se compara sin traceId, que cambia en cada peticion y no depende del motivo.
            var cuerpo = System.Text.Json.Nodes.JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!.AsObject();
            Assert.Equal("no_autenticado", cuerpo["codigo"]!.GetValue<string>());
            cuerpo.Remove("traceId");
            cuerpos.Add(cuerpo.ToJsonString());
        }

        // Un solo cuerpo para todos los motivos: la respuesta no revela por que no entro.
        Assert.Single(cuerpos);
    }

    // ── Equipo autorizado ──

    [Fact]
    public async Task ElEquipo_EsObligatorio_YSeGuardaSoloSuHash()
    {
        var empleado = await GestionConRostroAsync(50);

        await IngresarAsync(empleado with { TokenEquipo = null! }).EsperarAsync(HttpStatusCode.Unauthorized);
        await IngresarAsync(empleado with { TokenEquipo = "token-falso" }).EsperarAsync(HttpStatusCode.Unauthorized);
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.OK);

        var fila = await FilaVigenteAsync(empleado.Id);
        Assert.NotEqual(Encoding.UTF8.GetBytes(empleado.TokenEquipo), fila.TokenEquipoHash);
        Assert.Equal(32, fila.TokenEquipoHash!.Length);
    }

    [Fact]
    public async Task ElEquipo_VenceYSeAutorizaDeNuevoConLaContraseña()
    {
        var empleado = await GestionConRostroAsync(51);
        var cliente = api.Cliente(empleado.Token);

        await api.ConsultarAsync(db => db.RostrosEmpleado.Where(r => r.IdEmpleado == empleado.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.FechaAutorizacionEquipo, DateTimeOffset.UtcNow.AddDays(-31))));
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.Unauthorized);
        Assert.False((await cliente.GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.OK)).GetProperty("equipoVigente").GetBoolean());

        await cliente.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = "no-es-mi-password" }).EsperarAsync(HttpStatusCode.BadRequest);
        await cliente.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { }).EsperarAsync(HttpStatusCode.BadRequest);
        var nuevo = (await cliente.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.OK))
            .GetProperty("tokenEquipo").GetString()!;

        await IngresarAsync(empleado with { TokenEquipo = nuevo }).EsperarAsync(HttpStatusCode.OK);
        await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ElEquipo_NoSeAutorizaSinRostroRegistrado()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Administrador);

        await api.Cliente(empleado.Token).PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.BadRequest);
    }

    // ── «Rostros registrados» (S16) ──

    [Fact]
    public async Task RostrosRegistrados_ListaALaGestionActiva_ConEstadoEHistorial()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var activo = await GestionConRostroAsync(52, Roles.Dueno);
        var desactivado = await GestionConRostroAsync(53);
        await api.Cliente(desactivado.Token).DeleteAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.NoContent);
        var sinRegistro = await api.CrearEmpleadoAsync(Roles.Administrador);
        var operario = await api.CrearEmpleadoAsync(Roles.Operario);
        // Registrado dos veces: el historial trae la constancia anterior.
        await api.Cliente(activo.Token).PutAsJsonAsync("/api/v1/auth/rostro", Registro(activo.Persona)).EsperarAsync(HttpStatusCode.OK);

        var filas = (await admin.GetAsync("/api/v1/rostros").EsperarAsync(HttpStatusCode.OK)).EnumerateArray().ToList();
        JsonElement Fila(int id) => filas.Single(f => f.GetProperty("idEmpleado").GetInt32() == id);

        Assert.DoesNotContain(filas, f => f.GetProperty("idEmpleado").GetInt32() == operario.Id);
        Assert.All(filas, f => Assert.Contains(f.GetProperty("rol").GetString(), new[] { Roles.Dueno, Roles.Administrador }));

        var a = Fila(activo.Id);
        Assert.Equal("activo", a.GetProperty("estado").GetString());
        Assert.Equal("Emp Prueba", a.GetProperty("nombre").GetString());
        Assert.Equal(Roles.Dueno, a.GetProperty("rol").GetString());
        Assert.Equal("Emp Prueba", a.GetProperty("registradoPor").GetString());
        Assert.NotEqual(JsonValueKind.Null, a.GetProperty("fechaRegistro").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, a.GetProperty("fechaConsentimiento").ValueKind);
        Assert.Equal(JsonValueKind.Null, a.GetProperty("fechaDesactivacion").ValueKind);
        Assert.Equal(1, a.GetProperty("historial").GetArrayLength());
        Assert.Equal(ConsentimientoFacial.Version, a.GetProperty("historial")[0].GetProperty("versionConsentimiento").GetString());

        var d = Fila(desactivado.Id);
        Assert.Equal("desactivado", d.GetProperty("estado").GetString());
        Assert.NotEqual(JsonValueKind.Null, d.GetProperty("fechaDesactivacion").ValueKind);
        Assert.Equal(1, d.GetProperty("historial").GetArrayLength());

        Assert.Equal("sin_registro", Fila(sinRegistro.Id).GetProperty("estado").GetString());
        Assert.Equal(0, Fila(sinRegistro.Id).GetProperty("historial").GetArrayLength());

        // Nunca sale un dato del rostro.
        var texto = string.Join("", filas.Select(f => f.GetRawText()));
        Assert.DoesNotContain("descriptor", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gestion_RegistraElRostroDeOtro_ConLaContraseñaDelTitular()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var titular = await api.CrearEmpleadoAsync(Roles.Dueno);
        var passwordTitular = $"Ti-{Guid.NewGuid():N}"[..20];
        await CambiarPasswordAsync(titular.Id, passwordTitular);
        var persona = Sintetica(54);

        // Con la contraseña de quien registra (no la del titular) no sirve.
        var error = await admin.PutAsJsonAsync($"/api/v1/rostros/{titular.Id}", RegistroTitular(persona, ApiFixture.AdminPassword))
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("datos_invalidos", error.GetProperty("codigo").GetString());
        Assert.True(error.GetProperty("errores").TryGetProperty("passwordTitular", out _));
        Assert.Equal(0, await FilasAsync(titular.Id));

        var registrado = await admin.PutAsJsonAsync($"/api/v1/rostros/{titular.Id}", RegistroTitular(persona, passwordTitular)).EsperarAsync(HttpStatusCode.OK);

        // No entrega token de equipo: el titular autoriza su equipo desde su perfil.
        Assert.Equal(JsonValueKind.Null, registrado.GetProperty("tokenEquipo").ValueKind);
        var fila = await FilaVigenteAsync(titular.Id);
        Assert.Equal(await api.IdAdminAsync(), fila.IdEmpleadoRegistra);
        Assert.True(fila.TieneTresMuestras);
        Assert.Null(fila.TokenEquipoHash);

        var sesionTitular = await api.LoginEmpleadoAsync(titular.Email, passwordTitular);
        var token = (await api.Cliente(sesionTitular).PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = passwordTitular }).EsperarAsync(HttpStatusCode.OK))
            .GetProperty("tokenEquipo").GetString()!;
        await IngresarAsync(new ConRostro(titular.Id, titular.Email, sesionTitular, token, persona)).EsperarAsync(HttpStatusCode.OK);

        var filaLista = (await admin.GetAsync("/api/v1/rostros").EsperarAsync(HttpStatusCode.OK)).EnumerateArray().Single(f => f.GetProperty("idEmpleado").GetInt32() == titular.Id);
        Assert.Equal("Admin Pruebas", filaLista.GetProperty("registradoPor").GetString());
    }

    [Fact]
    public async Task LaContraseñaDelTitular_TieneSuPropioLimite_YNoBloqueaSuIngreso()
    {
        var titular = await api.CrearEmpleadoAsync(Roles.Administrador);
        await using var limitada = api.ConVariable("RateLimiting__PorCuenta", "2");
        var admin = limitada.Cliente(await api.TokenAdminAsync());

        for (var i = 0; i < 2; i++)
            await admin.PutAsJsonAsync($"/api/v1/rostros/{titular.Id}", RegistroTitular(Sintetica(70), "no-es-su-password")).EsperarAsync(HttpStatusCode.BadRequest);
        await admin.PutAsJsonAsync($"/api/v1/rostros/{titular.Id}", RegistroTitular(Sintetica(70), ApiFixture.AdminPassword)).EsperarAsync(HttpStatusCode.TooManyRequests);

        // Otra cuenta de gestion no puede dejar al titular sin entrar con su contraseña.
        await limitada.Cliente().PostAsJsonAsync("/api/v1/auth/login", new { email = titular.Email, password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(0, await FilasAsync(titular.Id));
    }

    [Fact]
    public async Task Gestion_NoRegistraElRostroDeOtroRol_NiElPropioPorEstaRuta()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var operario = await api.CrearEmpleadoAsync(Roles.Operario);

        var error = await admin.PutAsJsonAsync($"/api/v1/rostros/{operario.Id}", RegistroTitular(Sintetica(55), ApiFixture.AdminPassword))
            .EsperarAsync(HttpStatusCode.Forbidden);
        Assert.Equal("solo_gestion", error.GetProperty("codigo").GetString());

        await admin.PutAsJsonAsync($"/api/v1/rostros/{await api.IdAdminAsync()}", RegistroTitular(Sintetica(55), ApiFixture.AdminPassword))
            .EsperarAsync(HttpStatusCode.BadRequest);
        await admin.PutAsJsonAsync("/api/v1/rostros/999999", RegistroTitular(Sintetica(55), ApiFixture.AdminPassword)).EsperarAsync(HttpStatusCode.NotFound);
        Assert.Equal(0, await FilasAsync(operario.Id));

        // Y quien no es de gestion no puede registrar a nadie.
        var otro = await api.CrearEmpleadoAsync(Roles.Administrador);
        await api.Cliente(operario.Token).PutAsJsonAsync($"/api/v1/rostros/{otro.Id}", RegistroTitular(Sintetica(55), ApiFixture.AdminPassword))
            .EsperarAsync(HttpStatusCode.Forbidden);
    }

    // ── Calibracion ──

    [Fact]
    public async Task Calibracion_ApagadaNoExiste()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());

        await admin.GetAsync("/api/v1/rostros/calibracion").EsperarAsync(HttpStatusCode.NotFound);
        await admin.PostAsJsonAsync("/api/v1/rostros/calibracion", new { idEmpleado = 1, etiqueta = "propio" }).EsperarAsync(HttpStatusCode.NotFound);
        await admin.DeleteAsync("/api/v1/rostros/calibracion").EsperarAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Calibracion_GuardaSoloNumeros_YSugiereUnUmbral()
    {
        var empleado = await GestionConRostroAsync(56);
        await using var activa = api.ConVariable("RostroFacial__Calibracion__Activa", "true");
        var admin = activa.Cliente(await api.TokenAdminAsync());
        await admin.DeleteAsync("/api/v1/rostros/calibracion").EsperarAsync(HttpStatusCode.NoContent);

        async Task<JsonElement> MedirAsync(string etiqueta, Persona persona)
        {
            var desafio = await activa.Cliente().PostAsJsonAsync("/api/v1/auth/login-facial/desafio", new { email = empleado.Email }).EsperarAsync(HttpStatusCode.OK);
            var orden = desafio.GetProperty("orden").EnumerateArray().Select(o => o.GetString()!).ToArray();
            return await admin.PostAsJsonAsync("/api/v1/rostros/calibracion",
                new { idEmpleado = empleado.Id, etiqueta, desafio = desafio.GetProperty("desafio").GetString(), capturas = Capturas(persona, orden) })
                .EsperarAsync(HttpStatusCode.Created);
        }

        var propia = await MedirAsync("propio", empleado.Persona);
        await MedirAsync("propio", empleado.Persona);
        var ajena = await MedirAsync("otra_persona", Sintetica(57));

        Assert.True(propia.GetProperty("habriaEntrado").GetBoolean());
        Assert.False(ajena.GetProperty("habriaEntrado").GetBoolean());
        Assert.InRange(propia.GetProperty("distanciaFrente").GetDouble(), 0, 0.1);
        Assert.InRange(propia.GetProperty("giroPrimero").GetDouble() * propia.GetProperty("giroSegundo").GetDouble(), -0.26, -0.24);
        Assert.Equal(0.5, propia.GetProperty("umbralVigente").GetDouble());

        var consulta = await admin.GetAsync("/api/v1/rostros/calibracion").EsperarAsync(HttpStatusCode.OK);
        Assert.True(consulta.GetProperty("activa").GetBoolean());
        Assert.Equal(0.5, consulta.GetProperty("umbralVigente").GetDouble());
        Assert.Equal(0.15, consulta.GetProperty("rangos").GetProperty("frenteMaximo").GetDouble());
        Assert.Equal(3, consulta.GetProperty("mediciones").GetArrayLength());
        var resumen = consulta.GetProperty("resumen");
        Assert.Equal(2, resumen.GetProperty("propio").GetProperty("n").GetInt32());
        Assert.Equal(1, resumen.GetProperty("otra").GetProperty("n").GetInt32());
        var sugerido = resumen.GetProperty("umbralSugerido").GetDouble();
        Assert.InRange(sugerido, resumen.GetProperty("propio").GetProperty("max").GetDouble(), resumen.GetProperty("otra").GetProperty("min").GetDouble());
        Assert.True(resumen.GetProperty("margen").GetDouble() > 0);

        // La tabla no tiene columnas donde quepa un descriptor o un punto: solo numeros, una etiqueta y fechas.
        var tipos = await api.ConsultarAsync(db => db.Database.SqlQueryRaw<string>(
            "SELECT TYPE_NAME(user_type_id) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('dbo.calibracion_rostro')").ToListAsync());
        Assert.Equal(["datetimeoffset", "float", "int", "varchar"], tipos.Distinct().Order());

        // Borrar las mediciones las deja en cero.
        await admin.DeleteAsync("/api/v1/rostros/calibracion").EsperarAsync(HttpStatusCode.NoContent);
        Assert.Equal(0, (await admin.GetAsync("/api/v1/rostros/calibracion").EsperarAsync(HttpStatusCode.OK)).GetProperty("mediciones").GetArrayLength());
    }

    [Fact]
    public async Task Calibracion_RevisaLosDatos()
    {
        var empleado = await GestionConRostroAsync(58);
        var sinRostro = await api.CrearEmpleadoAsync(Roles.Administrador);
        await using var activa = api.ConVariable("RostroFacial__Calibracion__Activa", "true");
        var admin = activa.Cliente(await api.TokenAdminAsync());
        var (desafio, orden) = await DesafioAsync(empleado.Email);

        var malo = await admin.PostAsJsonAsync("/api/v1/rostros/calibracion", new { idEmpleado = sinRostro.Id, etiqueta = "quizas", desafio, capturas = Capturas(empleado.Persona, orden).Take(2) })
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["capturas", "etiqueta", "idEmpleado"], malo.GetProperty("errores").EnumerateObject().Select(e => e.Name).Order());

        await admin.PostAsJsonAsync("/api/v1/rostros/calibracion", new { idEmpleado = empleado.Id, etiqueta = "propio", desafio, capturas = Capturas(empleado.Persona, orden) })
            .EsperarAsync(HttpStatusCode.Created);
        var reusado = await admin.PostAsJsonAsync("/api/v1/rostros/calibracion", new { idEmpleado = empleado.Id, etiqueta = "propio", desafio, capturas = Capturas(empleado.Persona, orden) })
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(reusado.GetProperty("errores").TryGetProperty("desafio", out _));
        await admin.DeleteAsync("/api/v1/rostros/calibracion").EsperarAsync(HttpStatusCode.NoContent);
    }

    // ── Purga ──

    [Fact]
    public async Task LaPurga_BorraLosDesafiosDeMasDeUnDia()
    {
        var (viejo, _) = await DesafioAsync($"purga{api.Siguiente()}@test.local");
        var (nuevo, _) = await DesafioAsync($"purga{api.Siguiente()}@test.local");
        var hashViejo = Hex(viejo);
        var hashNuevo = Hex(nuevo);
        await api.ConsultarAsync(db => db.DesafiosRostro.Where(d => d.TokenHash == hashViejo)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Emitido, DateTimeOffset.UtcNow.AddDays(-2)).SetProperty(d => d.Vence, DateTimeOffset.UtcNow.AddDays(-2).AddSeconds(30))));

        await using (var alcance = api.Services.CreateAsyncScope())
            await alcance.ServiceProvider.GetRequiredService<PurgaDatosPersonales>().PurgarAsync(CancellationToken.None);

        Assert.False(await api.ConsultarAsync(db => db.DesafiosRostro.AnyAsync(d => d.TokenHash == hashViejo)));
        Assert.True(await api.ConsultarAsync(db => db.DesafiosRostro.AnyAsync(d => d.TokenHash == hashNuevo)));
    }

    // ── Clientes y sesion ──

    [Fact]
    public async Task LosClientes_NoUsanLasRutasDelRostro()
    {
        var cliente = await api.RegistrarClienteAsync();
        var conToken = api.Cliente(cliente.Token);
        var filasAntes = await api.ConsultarAsync(db => db.RostrosEmpleado.CountAsync());

        await conToken.GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.Forbidden);
        await conToken.PutAsJsonAsync("/api/v1/auth/rostro", Registro(Sintetica(59))).EsperarAsync(HttpStatusCode.Forbidden);
        await conToken.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Forbidden);
        await conToken.DeleteAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.Forbidden);
        await conToken.GetAsync("/api/v1/rostros").EsperarAsync(HttpStatusCode.Forbidden);
        await conToken.DeleteAsync("/api/v1/rostros/1").EsperarAsync(HttpStatusCode.Forbidden);

        Assert.Equal(filasAntes, await api.ConsultarAsync(db => db.RostrosEmpleado.CountAsync()));
    }

    [Fact]
    public async Task ConElEmailDeUnCliente_NoSeEntra()
    {
        var email = $"cliente{api.Siguiente()}@test.local";
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", api.Registro(api.Telefono(), correo: email)).EsperarAsync(HttpStatusCode.Created);

        await IngresarAsync(new ConRostro(0, email, "", "token", Sintetica(60))).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LasRutasDelRostro_PidenSesion()
    {
        var anonimo = api.Cliente();

        await anonimo.GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.Unauthorized);
        await anonimo.PutAsJsonAsync("/api/v1/auth/rostro", Registro(Sintetica(61))).EsperarAsync(HttpStatusCode.Unauthorized);
        await anonimo.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Unauthorized);
        await anonimo.DeleteAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.Unauthorized);
        await anonimo.GetAsync("/api/v1/rostros").EsperarAsync(HttpStatusCode.Unauthorized);
        await anonimo.PutAsJsonAsync("/api/v1/rostros/1", RegistroTitular(Sintetica(61), "x")).EsperarAsync(HttpStatusCode.Unauthorized);
        await anonimo.DeleteAsync("/api/v1/rostros/1").EsperarAsync(HttpStatusCode.Unauthorized);
        await anonimo.GetAsync("/api/v1/rostros/calibracion").EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnaSesionConRostro_NoHaceAccionesDelicadasSinLaContraseña()
    {
        var empleado = await GestionConRostroAsync(62, Roles.Dueno);
        var sesion = (await IngresarAsync(empleado).EsperarAsync(HttpStatusCode.OK)).GetProperty("token").GetString();
        var conRostro = api.Cliente(sesion);
        var idCliente = await api.CrearClienteAsync();

        var nuevoEmail = $"e{api.Siguiente()}@test.local";
        await conRostro.PostAsJsonAsync("/api/v1/empleados", new { rut = api.Rut(), nombre = "A", apellido = "B", telefono = api.Telefono(), email = nuevoEmail, password = ApiFixture.AdminPassword, rol = Roles.Dueno })
            .EsperarAsync(HttpStatusCode.BadRequest);
        await conRostro.PutAsJsonAsync($"/api/v1/clientes/{idCliente}/credenciales", new { password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.BadRequest);
        await conRostro.PutAsJsonAsync("/api/v1/auth/rostro", RegistroCon(Muestras(Sintetica(63)), "no-es-mi-password")).EsperarAsync(HttpStatusCode.BadRequest);
        await conRostro.PostAsJsonAsync("/api/v1/auth/rostro/equipo", new { password = "no-es-mi-password" }).EsperarAsync(HttpStatusCode.BadRequest);

        Assert.False(await api.ConsultarAsync(db => db.Empleados.AnyAsync(e => e.Email == nuevoEmail)));
    }

    // ── Proteccion de los datos ──

    [Fact]
    public async Task UnaMuestraCopiadaAOtroEmpleado_NoSirve()
    {
        var victima = await GestionConRostroAsync(64);
        var atacante = await GestionConRostroAsync(65);

        // Alguien con acceso de escritura a la tabla copia sus propias muestras a la fila de otro.
        var suya = await FilaVigenteAsync(atacante.Id);
        await api.ConsultarAsync(db => db.RostrosEmpleado.Where(r => r.IdEmpleado == victima.Id && r.FechaRevocacion == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.DescriptorFrente, suya.DescriptorFrente)
                .SetProperty(r => r.DescriptorIzquierda, suya.DescriptorIzquierda).SetProperty(r => r.DescriptorDerecha, suya.DescriptorDerecha)));

        await IngresarAsync(victima with { Persona = atacante.Persona }).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnaMuestraIlegible_SeRegistraEnLosLogs_SinRevelarla()
    {
        var empleado = await GestionConRostroAsync(66);
        var otro = await GestionConRostroAsync(67);
        var ajena = (await FilaVigenteAsync(otro.Id)).DescriptorIzquierda;
        await api.ConsultarAsync(db => db.RostrosEmpleado.Where(r => r.IdEmpleado == empleado.Id && r.FechaRevocacion == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.DescriptorIzquierda, ajena)));
        var logs = new LogsEnMemoria();

        await IngresarAsync(empleado, api.ClienteConLogs(logs)).EsperarAsync(HttpStatusCode.Unauthorized);

        Assert.Contains(logs.Mensajes, m => m.Contains("No se pudo descifrar") && m.Contains(empleado.Id.ToString(CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task LasRespuestas_NuncaIncluyenDatosDelRostro()
    {
        var empleado = await GestionConRostroAsync(68, Roles.Dueno);
        var registro = await api.Cliente(empleado.Token).PutAsJsonAsync("/api/v1/auth/rostro", Registro(empleado.Persona)).EsperarAsync(HttpStatusCode.OK);
        var token = registro.GetProperty("tokenEquipo").GetString()!;
        var estado = await api.Cliente(empleado.Token).GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.OK);
        var login = await IngresarAsync(empleado with { TokenEquipo = token }).EsperarAsync(HttpStatusCode.OK);
        var empleados = await api.Cliente(empleado.Token).GetAsync("/api/v1/empleados").EsperarAsync(HttpStatusCode.OK);
        var lista = await api.Cliente(empleado.Token).GetAsync("/api/v1/rostros").EsperarAsync(HttpStatusCode.OK);

        foreach (var json in new[] { registro, estado, login, empleados, lista })
        {
            var texto = json.GetRawText();
            Assert.DoesNotContain("descriptor", texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hash", texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(empleado.Persona.Frente[5].ToString("R", CultureInfo.InvariantCulture), texto);
        }
    }

    [Fact]
    public async Task LosLogs_NuncaTienenDescriptoresPuntosTokensNiDesafios()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Dueno);
        var logs = new LogsEnMemoria();
        var cliente = api.ClienteConLogs(logs);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", empleado.Token);

        // Valores faciles de buscar: 0.123456 en los descriptores y ,4321987 en los puntos.
        var persona = PersonaMarcada();
        var tokenEquipo = (await cliente.PutAsJsonAsync("/api/v1/auth/rostro", Registro(persona)).EsperarAsync(HttpStatusCode.OK))
            .GetProperty("tokenEquipo").GetString()!;
        var conRostro = new ConRostro(empleado.Id, empleado.Email, empleado.Token, tokenEquipo, persona);

        var desafios = new List<string>();
        async Task<HttpResponseMessage> Intento(ConRostro caso, double giro)
        {
            var desafio = await cliente.PostAsJsonAsync("/api/v1/auth/login-facial/desafio", new { email = caso.Email }).EsperarAsync(HttpStatusCode.OK);
            desafios.Add(desafio.GetProperty("desafio").GetString()!);
            var orden = desafio.GetProperty("orden").EnumerateArray().Select(o => o.GetString()!).ToArray();
            return await cliente.PostAsJsonAsync("/api/v1/auth/login-facial", CuerpoLogin(caso.Email, desafios[^1], caso.TokenEquipo, Capturas(caso.Persona, orden, giro, marcados: true)));
        }

        var sesion = (await Intento(conRostro, GiroModerado).EsperarAsync(HttpStatusCode.OK)).GetProperty("token").GetString()!;
        await Intento(conRostro with { TokenEquipo = "token-falso" }, GiroModerado).EsperarAsync(HttpStatusCode.Unauthorized);
        await Intento(conRostro, 0.1).EsperarAsync(HttpStatusCode.Unauthorized);
        await cliente.PostAsJsonAsync("/api/v1/auth/login-facial", new { email = empleado.Email, desafio = desafios[0], capturas = new[] { new { descriptor = new[] { 0.123456f } } } })
            .EsperarAsync(HttpStatusCode.Unauthorized);

        // Un JSON roto no debe dejar el contenido en los mensajes de error.
        using var roto = new StringContent("""{"email":"x@test.local","capturas":[{"descriptor":[0.123456,"abc" """, Encoding.UTF8, "application/json");
        await cliente.PostAsync("/api/v1/auth/login-facial", roto).EsperarAsync(HttpStatusCode.BadRequest);

        // Control: la captura funciona y llega hasta las consultas de la base.
        Assert.Contains(logs.Mensajes, m => m.Contains("registro su acceso facial"));
        Assert.Contains(logs.Mensajes, m => m.Contains("desafios_rostro"));

        Assert.DoesNotContain(logs.Mensajes, m => m.Contains("0.123456") || m.Contains("0,123456") || m.Contains("123456"));
        Assert.DoesNotContain(logs.Mensajes, m => m.Contains("4321987"));
        Assert.DoesNotContain(logs.Mensajes, m => m.Contains(tokenEquipo) || m.Contains(sesion));
        foreach (var desafio in desafios)
            Assert.DoesNotContain(logs.Mensajes, m => m.Contains(desafio) || m.Contains(Hex(desafio)));
    }

    [Fact]
    public async Task SinClave_ElRostroNoEstaDisponible_PeroLaContraseñaSi()
    {
        var empleado = await api.CrearEmpleadoAsync(Roles.Administrador);
        await using var sinClave = api.ConVariable("RostroFacial__ClaveCifrado", "no-es-base64");
        var cliente = sinClave.Cliente();

        foreach (var email in new[] { empleado.Email, $"nadie{api.Siguiente()}@test.local" })
        {
            await cliente.PostAsJsonAsync("/api/v1/auth/login-facial/desafio", new { email }).EsperarAsync(HttpStatusCode.ServiceUnavailable);
            await cliente.PostAsJsonAsync("/api/v1/auth/login-facial", new { email, desafio = "x" }).EsperarAsync(HttpStatusCode.ServiceUnavailable);
        }

        await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = empleado.Email, password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.OK);

        cliente.DefaultRequestHeaders.Authorization = new("Bearer", empleado.Token);
        var estado = await cliente.GetAsync("/api/v1/auth/rostro").EsperarAsync(HttpStatusCode.OK);
        Assert.False(estado.GetProperty("disponible").GetBoolean());
        await cliente.PutAsJsonAsync("/api/v1/auth/rostro", Registro(Sintetica(69))).EsperarAsync(HttpStatusCode.ServiceUnavailable);
    }

    // ── Auxiliares ──

    // Una persona sintetica: su muestra de frente y una por lado (ver el comentario de la clase).
    private sealed record Persona(float[] Frente, float[] Izquierda, float[] Derecha)
    {
        public float[] Lado(string lado) => lado == "izquierda" ? Izquierda : Derecha;
    }

    private sealed record ConRostro(int Id, string Email, string Token, string TokenEquipo, Persona Persona);

    private static Persona Sintetica(int semilla)
    {
        var azar = new Random(semilla);
        var frente = Enumerable.Range(0, RostroFacialService.Dimensiones).Select(_ => (float)(azar.NextDouble() * 0.6 - 0.3)).ToArray();
        return new Persona(frente, Desplazar(frente, 0, 0.39f), Desplazar(frente, 1, 0.39f));
    }

    private static Persona PersonaMarcada()
    {
        var frente = Enumerable.Repeat(0.123456f, RostroFacialService.Dimensiones).ToArray();
        return new Persona(frente, Desplazar(frente, 0, 0.39f), Desplazar(frente, 1, 0.39f));
    }

    private static float[] Desplazar(float[] descriptor, int indice, float cuanto)
    {
        var copia = (float[])descriptor.Clone();
        copia[indice] += cuanto;
        return copia;
    }

    // La misma persona en otra captura: cambia poco.
    private static float[] Variar(float[] descriptor)
    {
        var azar = new Random(descriptor.Length + (int)(descriptor[7] * 1000));
        return descriptor.Select(v => v + (float)(azar.NextDouble() * 0.01 - 0.005)).ToArray();
    }

    private static int Signo(string lado) => lado == "izquierda" ? 1 : -1;

    // 68 puntos de un cuadro de 640 x 480 con el giro g pedido: la mandibula de x = 200 a x = 400 y la nariz en
    // 300 + 100 g. Con marcados, todas las coordenadas llevan ,4321987 para buscarlas en los logs.
    private static double[][] Puntos(double g, bool marcados = false)
    {
        var extra = marcados ? 0.4321987 : 0;
        var puntos = Enumerable.Range(0, 68).Select(_ => new[] { 320 + extra, 240 + extra }).ToArray();
        puntos[0][0] = 200 + extra;
        puntos[16][0] = 400 + extra;
        puntos[30][0] = 300 + 100 * g + extra;
        return puntos;
    }

    private static object Captura(float[] descriptor, double g, bool marcados = false) =>
        new { descriptor, puntos = Puntos(g, marcados), ancho = 640, alto = 480 };

    // Frente, primer giro y segundo giro, en el orden del desafio.
    private static object[] Capturas(Persona p, string[] orden, double giro = GiroModerado, double giroFrente = 0, bool marcados = false) =>
    [
        Captura(marcados ? p.Frente : Variar(p.Frente), giroFrente, marcados),
        Captura(marcados ? p.Lado(orden[0]) : Variar(p.Lado(orden[0])), Signo(orden[0]) * giro, marcados),
        Captura(marcados ? p.Lado(orden[1]) : Variar(p.Lado(orden[1])), Signo(orden[1]) * giro, marcados)
    ];

    private static object Muestras(Persona p) => new
    {
        frente = Captura(p.Frente, 0),
        izquierda = Captura(p.Izquierda, GiroModerado),
        derecha = Captura(p.Derecha, -GiroModerado)
    };

    private static object Registro(Persona p) => RegistroCon(Muestras(p), ApiFixture.AdminPassword);

    private static object RegistroCon(object muestras, string? password) =>
        new { password, consentimiento = true, versionConsentimiento = ConsentimientoFacial.Version, muestras };

    private static object RegistroTitular(Persona p, string passwordTitular) =>
        new { passwordTitular, consentimiento = true, versionConsentimiento = ConsentimientoFacial.Version, muestras = Muestras(p) };

    private static object CuerpoLogin(string email, string? desafio, string? tokenEquipo, object[] capturas) =>
        new { email, desafio, tokenEquipo, capturas };

    private static string Hex(string texto) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));

    private async Task<(string Desafio, string[] Orden)> DesafioAsync(string email, HttpClient? cliente = null)
    {
        var respuesta = await (cliente ?? api.Cliente()).PostAsJsonAsync("/api/v1/auth/login-facial/desafio", new { email }).EsperarAsync(HttpStatusCode.OK);
        return (respuesta.GetProperty("desafio").GetString()!, respuesta.GetProperty("orden").EnumerateArray().Select(o => o.GetString()!).ToArray());
    }

    private Task<HttpResponseMessage> Login(string email, string? desafio, string? tokenEquipo, object[] capturas, HttpClient? cliente = null) =>
        (cliente ?? api.Cliente()).PostAsJsonAsync("/api/v1/auth/login-facial", CuerpoLogin(email, desafio, tokenEquipo, capturas));

    // Un ingreso completo: pide el desafio y envia las tres capturas en su orden.
    private async Task<HttpResponseMessage> IngresarAsync(ConRostro caso, HttpClient? cliente = null, double giro = GiroModerado, double giroFrente = 0)
    {
        var (desafio, orden) = await DesafioAsync(caso.Email, cliente);
        return await Login(caso.Email, desafio, caso.TokenEquipo, Capturas(caso.Persona, orden, giro, giroFrente), cliente);
    }

    // Un empleado de gestion con su rostro registrado por el mismo, y el token de su equipo.
    private async Task<ConRostro> GestionConRostroAsync(int semilla, string rol = Roles.Administrador)
    {
        var empleado = await api.CrearEmpleadoAsync(rol);
        var persona = Sintetica(semilla);
        var respuesta = await api.Cliente(empleado.Token).PutAsJsonAsync("/api/v1/auth/rostro", Registro(persona)).EsperarAsync(HttpStatusCode.OK);
        return new ConRostro(empleado.Id, empleado.Email, empleado.Token, respuesta.GetProperty("tokenEquipo").GetString()!, persona);
    }

    // Un rostro guardado directo en la base (por ejemplo, de un rol que ya no puede registrarlo). Devuelve el token de equipo.
    private async Task<string> InsertarRostroAsync(int idEmpleado, Persona persona)
    {
        var servicio = api.Services.GetRequiredService<RostroFacialService>();
        var token = RostroFacialService.CrearTokenEquipo();
        await api.ConsultarAsync(async db =>
        {
            db.RostrosEmpleado.Add(new RostroEmpleado
            {
                IdEmpleado = idEmpleado,
                DescriptorFrente = servicio.Cifrar(persona.Frente, idEmpleado),
                DescriptorIzquierda = servicio.Cifrar(persona.Izquierda, idEmpleado),
                DescriptorDerecha = servicio.Cifrar(persona.Derecha, idEmpleado),
                FormatoMuestras = RostroEmpleado.FormatoTresMuestras,
                IdEmpleadoRegistra = idEmpleado,
                FechaConsentimiento = DateTimeOffset.UtcNow,
                VersionConsentimiento = ConsentimientoFacial.Version,
                TokenEquipoHash = RostroFacialService.HashToken(token),
                FechaAutorizacionEquipo = DateTimeOffset.UtcNow
            });
            return await db.SaveChangesAsync();
        });
        return token;
    }

    private Task DejarDescriptorAntiguoAsync(int idEmpleado) =>
        api.ConsultarAsync(db => db.RostrosEmpleado.Where(r => r.IdEmpleado == idEmpleado && r.FechaRevocacion == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.DescriptorCifrado, new byte[] { 1, 2, 3 })));

    private async Task RevocadoSinDatosAsync(int idEmpleado)
    {
        var filas = await api.ConsultarAsync(db => db.RostrosEmpleado.AsNoTracking().Where(r => r.IdEmpleado == idEmpleado).ToListAsync());
        Assert.NotEmpty(filas);
        Assert.All(filas, fila =>
        {
            Assert.NotNull(fila.FechaRevocacion);
            Assert.Null(fila.DescriptorCifrado);
            Assert.Null(fila.DescriptorFrente);
            Assert.Null(fila.DescriptorIzquierda);
            Assert.Null(fila.DescriptorDerecha);
            Assert.Null(fila.TokenEquipoHash);
            Assert.Null(fila.FechaAutorizacionEquipo);
            Assert.False(string.IsNullOrEmpty(fila.VersionConsentimiento));
        });
    }

    private Task LimpiarIntentosAsync(int idEmpleado) =>
        api.ConsultarAsync(db => db.RostrosEmpleado.Where(r => r.IdEmpleado == idEmpleado && r.FechaRevocacion == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IntentosFallidos, 0).SetProperty(r => r.BloqueadoHasta, (DateTimeOffset?)null)));

    private async Task CambiarPasswordAsync(int idEmpleado, string password)
    {
        await using var alcance = api.Services.CreateAsyncScope();
        var hash = alcance.ServiceProvider.GetRequiredService<AuthService>().HashPassword(password);
        await api.ConsultarAsync(db => db.Empleados.Where(e => e.IdEmpleado == idEmpleado).ExecuteUpdateAsync(s => s.SetProperty(e => e.PasswordHash, hash)));
    }

    private Task<int> FilasAsync(int idEmpleado) =>
        api.ConsultarAsync(db => db.RostrosEmpleado.CountAsync(r => r.IdEmpleado == idEmpleado));

    private Task<RostroEmpleado> FilaVigenteAsync(int idEmpleado) =>
        api.ConsultarAsync(db => db.RostrosEmpleado.AsNoTracking().SingleAsync(r => r.IdEmpleado == idEmpleado && r.FechaRevocacion == null));

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
