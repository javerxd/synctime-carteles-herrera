using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 16A: lo que la API necesita para vivir detras de un proxy (Render, Vercel o nginx), hablar con Azure SQL y arrancar en
// produccion con una configuracion que se pueda revisar. Estas pruebas se escribieron ANTES del codigo y fallaron primero.
[Collection(ApiCollection.Name)]
public sealed class ProxyFase16aTests(ApiFixture api)
{
    private const string ProxyDeConfianza = "10.1.1.1";
    private const string ProxyAjeno = "198.51.100.9";

    private static Dictionary<string, string> ConProxy(params (string Clave, string Valor)[] extra)
    {
        var variables = new Dictionary<string, string>
        {
            // /salud/lista solo deja pasar 2 por minuto y por IP: asi se ve con que IP cuenta la API.
            ["RateLimiting__Salud"] = "2",
            ["ForwardedHeaders__Habilitado"] = "true",
            ["ForwardedHeaders__RedesDeConfianza__0"] = "10.0.0.0/8",
        };
        foreach (var (clave, valor) in extra)
            variables[clave] = valor;
        return variables;
    }

    private static async Task<HttpStatusCode> SaludAsync(HttpClient cliente, string ipDirecta, string? reenviadaPor = null, string? protocolo = null)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, "/salud/lista");
        peticion.Headers.Add(ApiFixture.CabeceraIp, ipDirecta);
        if (reenviadaPor is not null)
            peticion.Headers.Add("X-Forwarded-For", reenviadaPor);
        if (protocolo is not null)
            peticion.Headers.Add("X-Forwarded-Proto", protocolo);
        using var respuesta = await cliente.SendAsync(peticion);
        return respuesta.StatusCode;
    }

    [Fact]
    public async Task SinConfiguracion_LasCabecerasReenviadasSeIgnoran_YNadiPuedeCambiarSuIp()
    {
        // Sin proxies de confianza declarados, X-Forwarded-For lo escribe cualquiera: usarlo dejaria evadir los limites de tasa.
        await using var otra = api.ConEntorno("Development", new Dictionary<string, string> { ["RateLimiting__Salud"] = "2" });
        var cliente = otra.Cliente();

        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, "203.0.113.5", "192.0.2.1"));
        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, "203.0.113.5", "192.0.2.2"));
        // Cambiar la cabecera en cada peticion no da un cupo nuevo: sigue siendo la misma IP directa.
        Assert.Equal(HttpStatusCode.TooManyRequests, await SaludAsync(cliente, "203.0.113.5", "192.0.2.3"));
    }

    [Fact]
    public async Task ConUnProxyDeConfianza_LosLimitesCuentanPorLaIpRealDelVisitante()
    {
        await using var otra = api.ConEntorno("Development", ConProxy());
        var cliente = otra.Cliente();

        // Dos visitantes distintos detras del mismo proxy: cada uno con su propio cupo (antes compartian el del proxy).
        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.10"));
        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.10"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.10"));
        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.11"));
    }

    [Fact]
    public async Task UnProxyQueNoEsDeConfianza_NoPuedeFalsearLaIp()
    {
        await using var otra = api.ConEntorno("Development", ConProxy());
        var cliente = otra.Cliente();

        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, ProxyAjeno, "192.0.2.20"));
        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, ProxyAjeno, "192.0.2.21"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SaludAsync(cliente, ProxyAjeno, "192.0.2.22"));
    }

    [Fact]
    public async Task ConUnProxyDeConfianzaPorDireccion_TambienFunciona()
    {
        var variables = ConProxy();
        variables.Remove("ForwardedHeaders__RedesDeConfianza__0");
        variables["ForwardedHeaders__ProxiesDeConfianza__0"] = ProxyDeConfianza;
        await using var otra = api.ConEntorno("Development", variables);
        var cliente = otra.Cliente();

        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.30"));
        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.30"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.30"));
        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.31"));
    }

    [Fact]
    public async Task ConVariosSaltos_ElLimiteDeSaltosElige_LaDireccionDelVisitante()
    {
        // Vercel -> Render: la cadena trae al visitante y a un proxy intermedio de confianza.
        var variables = ConProxy(("ForwardedHeaders__LimiteDeSaltos", "2"));
        await using var otra = api.ConEntorno("Development", variables);
        var cliente = otra.Cliente();

        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.40, 10.2.2.2"));
        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.40, 10.2.2.3"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.40, 10.2.2.4"));
        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.41, 10.2.2.2"));
    }

    [Fact]
    public async Task XForwardedProto_DeUnProxyDeConfianza_EvitaElBucleDeRedireccionAHttps()
    {
        // El proxy termina el HTTPS y habla HTTP con la API: sin leer X-Forwarded-Proto, UseHttpsRedirection respondia 307 a
        // https:// en cada peticion y el navegador entraba en bucle.
        var variables = ConProxy(("ASPNETCORE_HTTPS_PORT", "443"));
        await using var otra = api.ConEntorno("Development", variables);
        var cliente = otra.Cliente();

        Assert.Equal(HttpStatusCode.OK, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.50", "https"));
        // Sin la cabecera, la peticion llego por HTTP de verdad y se redirige (comportamiento que no debe perderse).
        Assert.Equal(HttpStatusCode.TemporaryRedirect, await SaludAsync(cliente, ProxyDeConfianza, "192.0.2.51"));
    }

    [Fact]
    public async Task XForwardedProto_DeQuienNoEsDeConfianza_SeIgnora()
    {
        var variables = ConProxy(("ASPNETCORE_HTTPS_PORT", "443"));
        await using var otra = api.ConEntorno("Development", variables);
        var cliente = otra.Cliente();

        Assert.Equal(HttpStatusCode.TemporaryRedirect, await SaludAsync(cliente, ProxyAjeno, "192.0.2.60", "https"));
    }

    [Fact]
    public void Habilitado_SinProxiesNiRedes_NoArranca()
    {
        // Un valor mal escrito impide arrancar: es mejor que una API que cree estar protegida y confia en cualquiera.
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Development",
            new Dictionary<string, string> { ["ForwardedHeaders__Habilitado"] = "true" }));

        Assert.Contains("ForwardedHeaders", Mensajes(error));
    }

    [Theory]
    [InlineData("10.0.0.0/33")]
    [InlineData("no-es-una-red")]
    [InlineData("0.0.0.0/0")]
    [InlineData("8.0.0.0/8")]
    [InlineData("2001:db8::/32")]
    public void UnaRedDeConfianzaInvalidaOQueAbarcaTodo_NoArranca(string red)
    {
        // 0.0.0.0/0 significaria confiar en todo internet: el mismo riesgo que no tener lista.
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Development", new Dictionary<string, string>
        {
            ["ForwardedHeaders__Habilitado"] = "true",
            ["ForwardedHeaders__RedesDeConfianza__0"] = red
        }));

        Assert.Contains("ForwardedHeaders", Mensajes(error));
    }

    internal static string Mensajes(Exception error)
    {
        var textos = new List<string>();
        for (var actual = error; actual is not null; actual = actual.InnerException!)
            textos.Add(actual.Message);
        return string.Join(" | ", textos);
    }
}

// Fase 16A (RNFP29): fuera de desarrollo, el CORS y la URL publica se declaran por entorno y se revisan al arrancar.
[Collection(ApiCollection.Name)]
public sealed class EntornoFase16aTests(ApiFixture api)
{
    private const string OrigenVercel = "https://synctime.vercel.app";

    private static Dictionary<string, string> Produccion(params (string Clave, string? Valor)[] cambios)
    {
        var variables = new Dictionary<string, string>
        {
            ["Cors__AllowedOrigins__0"] = OrigenVercel,
            ["Sitio__UrlPublica"] = OrigenVercel,
            // Una API de produccion no trae a nadie con IP: sin proxy declarado, la del servidor de pruebas.
        };
        foreach (var (clave, valor) in cambios)
        {
            if (valor is null)
                variables.Remove(clave);
            else
                variables[clave] = valor;
        }
        return variables;
    }

    private static string Mensajes(Exception e) => ProxyFase16aTests.Mensajes(e);

    [Fact]
    public async Task EnProduccion_ConUnaConfiguracionValida_ArrancaYResponde()
    {
        await using var otra = api.ConEntorno("Production", Produccion());

        using var respuesta = await otra.Cliente().GetAsync("/salud");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task EnProduccion_ElCorsSoloAceptaLosOrigenesDeclarados_ConCredenciales()
    {
        await using var otra = api.ConEntorno("Production", Produccion());
        var cliente = otra.Cliente();

        using var permitido = await Preflight(cliente, OrigenVercel);
        Assert.Equal(OrigenVercel, permitido.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", permitido.Headers.GetValues("Access-Control-Allow-Credentials").Single());

        using var ajeno = await Preflight(cliente, "https://sitio-ajeno.example");
        Assert.False(ajeno.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static Task<HttpResponseMessage> Preflight(HttpClient cliente, string origen)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        peticion.Headers.Add("Origin", origen);
        peticion.Headers.Add("Access-Control-Request-Method", "POST");
        peticion.Headers.Add("Access-Control-Request-Headers", "content-type");
        return cliente.SendAsync(peticion);
    }

    [Fact]
    public void EnProduccion_SinOrigenesCors_NoArranca_YNoCaeEnLocalhost()
    {
        // Antes, sin configuracion el CORS quedaba en https://localhost:3000 sin avisar.
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Production", Produccion(("Cors__AllowedOrigins__0", null))));

        Assert.Contains("Cors:AllowedOrigins", Mensajes(error));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://synctime.vercel.app/")]
    [InlineData("https://synctime.vercel.app/panel")]
    [InlineData("synctime.vercel.app")]
    [InlineData("http://synctime.vercel.app")]
    [InlineData("https://*.vercel.app")]
    public void EnProduccion_UnOrigenMalEscrito_NoArranca(string origen)
    {
        // La barra final o la ruta hacen que el navegador y la API nunca coincidan: el CORS fallaria sin decir por que.
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Production", Produccion(("Cors__AllowedOrigins__0", origen))));

        Assert.Contains("Cors:AllowedOrigins", Mensajes(error));
    }

    [Fact]
    public void EnProduccion_SinUrlPublica_NoArranca()
    {
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Production", Produccion(("Sitio__UrlPublica", ""))));

        Assert.Contains("Sitio:UrlPublica", Mensajes(error));
    }

    [Theory]
    [InlineData("http://synctime.vercel.app")]
    [InlineData("synctime.vercel.app")]
    [InlineData("https://synctime.vercel.app/ruta")]
    public void EnProduccion_UnaUrlPublicaQueNoEsHttpsDelSitio_NoArranca(string url)
    {
        // Los enlaces de los correos y del codigo QR salen de aqui: un enlace roto llega a clientes reales.
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Production", Produccion(("Sitio__UrlPublica", url))));

        Assert.Contains("Sitio:UrlPublica", Mensajes(error));
    }

    [Fact]
    public async Task EnDesarrollo_NoSeExigeNada_ComoSiempre()
    {
        await using var otra = api.ConEntorno("Development", new Dictionary<string, string> { ["Sitio__UrlPublica"] = "" });

        using var respuesta = await otra.Cliente().GetAsync("/salud");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}

// Fase 16A (H7): Azure SQL gratuita se pausa sola y la primera conexion tras la pausa falla unos segundos. La API reintenta
// los fallos transitorios; dentro de una transaccion NO reintenta (reejecutar a medias una transaccion que ya se perdio
// escribiria fuera de ella) y tampoco se rompe: la API usa unas cuarenta transacciones manuales.
[Collection(ApiCollection.Name)]
public sealed class ReintentosFase16aTests(ApiFixture api)
{
    // «La base de datos no esta disponible ahora»: lo que Azure responde mientras despierta de la pausa.
    private const int BaseNoDisponible = 40613;

    [Fact]
    public async Task LaApi_ReintentaLosFallosTransitorios()
    {
        var reintenta = await api.ConsultarAsync(db => Task.FromResult(db.Database.CreateExecutionStrategy().RetriesOnFailure));

        Assert.True(reintenta);
    }

    [Fact]
    public async Task UnFalloTransitorioFueraDeUnaTransaccion_SeReintenta()
    {
        var intentos = await api.ConsultarAsync(async db =>
        {
            var intentos = 0;
            await db.Database.CreateExecutionStrategy().ExecuteAsync(() =>
            {
                intentos++;
                if (intentos < 3)
                    throw ErrorSql(BaseNoDisponible);
                return Task.CompletedTask;
            });
            return intentos;
        });

        Assert.Equal(3, intentos);
    }

    [Fact]
    public async Task UnFalloQueNoEsTransitorio_NoSeReintenta()
    {
        var intentos = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => api.ConsultarAsync(async db =>
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(() =>
            {
                intentos++;
                throw new InvalidOperationException("un error de la aplicacion");
            });
            return 0;
        }));

        Assert.Equal(1, intentos);
    }

    [Fact]
    public async Task DentroDeUnaTransaccionManual_ElCodigoCorre_SinRetrocederNiExigirOtraForma()
    {
        // Con EnableRetryOnFailure a secas, EF Core lanza «la estrategia no admite transacciones iniciadas por el usuario».
        var resultado = await api.ConsultarAsync(async db =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync();
            var filas = await db.Database.SqlQueryRaw<int>("SELECT 1 AS Value").ToListAsync();
            await db.SaveChangesAsync();
            await transaccion.RollbackAsync();
            return filas.Single();
        });

        Assert.Equal(1, resultado);
    }

    [Fact]
    public async Task UnFalloTransitorioDentroDeUnaTransaccion_NoSeReintenta()
    {
        var intentos = 0;
        await Assert.ThrowsAnyAsync<Exception>(() => api.ConsultarAsync(async db =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync();
            await db.Database.CreateExecutionStrategy().ExecuteAsync(() =>
            {
                intentos++;
                throw ErrorSql(BaseNoDisponible);
            });
            return 0;
        }));

        Assert.Equal(1, intentos);
    }

    [Fact]
    public async Task UnSaveChangesAuditado_QueFallaPorUnErrorTransitorio_NoSeReintenta_NoDejaFilasAMedias_YLaSiguientePeticionFunciona()
    {
        // LIMITE CONOCIDO Y ACEPTADO: el interceptor de auditoria abre su transaccion ANTES de que EF Core llame a la estrategia, asi que
        // una escritura con auditoria cuenta como «dentro de una transaccion» y no se reintenta (reejecutar a medias la escribiria fuera
        // de ella). La peticion falla con 500 SIN dejar nada a medias y el cliente la repite (las rutas que crean aceptan Idempotency-Key).
        // Las lecturas, que son lo que despierta a Azure SQL de la pausa, si se reintentan (prueba de arriba).
        var telefono = api.Telefono();
        var interceptor = new FalloTransitorioUnaVez("INSERT INTO [clientes]");
        var admin = api.ClienteConInterceptor(interceptor, await api.TokenAdminAsync());
        var cuerpo = new { nombre = "Cli", apellido = "Reintento", telefono };

        await admin.PostAsJsonAsync("/api/v1/clientes/", cuerpo).EsperarAsync(HttpStatusCode.InternalServerError);
        Assert.Equal(0, await api.ConsultarAsync(db => db.Clientes.CountAsync(c => c.Telefono == telefono)));

        await admin.PostAsJsonAsync("/api/v1/clientes/", cuerpo).EsperarAsync(HttpStatusCode.Created);
        Assert.Equal(1, await api.ConsultarAsync(db => db.Clientes.CountAsync(c => c.Telefono == telefono)));
    }

    // Falla con «la base no esta disponible» la primera vez que ve una instruccion que contiene el fragmento.
    private sealed class FalloTransitorioUnaVez(string fragmento) : DbCommandInterceptor
    {
        private int intentos;
        public int Intentos => Volatile.Read(ref intentos);

        private void Revisar(DbCommand comando)
        {
            if (comando.CommandText.Contains(fragmento, StringComparison.OrdinalIgnoreCase) && Interlocked.Increment(ref intentos) == 1)
                throw ErrorSql(BaseNoDisponible);
            if (comando.CommandText.Contains(fragmento, StringComparison.OrdinalIgnoreCase) && Volatile.Read(ref intentos) > 1)
                Interlocked.Increment(ref intentos);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Revisar(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Revisar(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task LosReintentosSePuedenApagarPorConfiguracion()
    {
        await using var otra = api.ConEntorno("Development", new Dictionary<string, string> { ["BaseDeDatos__ReintentosMaximos"] = "0" });

        var reintenta = await otra.ConsultarAsync(db => Task.FromResult(db.Database.CreateExecutionStrategy().RetriesOnFailure));

        Assert.False(reintenta);
    }

    // Un SqlException con el numero indicado: el constructor no es publico, asi que se arma como lo hacen las pruebas de EF Core.
    internal static SqlException ErrorSql(int numero)
    {
        var error = (SqlError)RuntimeHelpers.GetUninitializedObject(typeof(SqlError));
        PonerCampo(error, "_number", numero);
        PonerCampo(error, "_message", "error de prueba");
        var errores = (SqlErrorCollection)RuntimeHelpers.GetUninitializedObject(typeof(SqlErrorCollection));
        PonerCampo(errores, "_errors", new List<object> { error });
        var excepcion = (SqlException)RuntimeHelpers.GetUninitializedObject(typeof(SqlException));
        PonerCampo(excepcion, "_errors", errores);
        return excepcion;
    }

    private static void PonerCampo(object destino, string nombre, object valor)
    {
        for (var tipo = destino.GetType(); tipo is not null; tipo = tipo.BaseType)
        {
            var campo = tipo.GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic);
            if (campo is not null)
            {
                campo.SetValue(destino, valor);
                return;
            }
        }
        throw new InvalidOperationException($"{destino.GetType().Name} no tiene el campo {nombre}: cambio la version de SqlClient.");
    }
}
