using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}

// Levanta la API contra una base propia creada por las migraciones, nunca contra la de desarrollo.
// Usa LocalDB por defecto; la variable TEST_SQL_SERVER permite apuntar a otro servidor.
public sealed class ApiFixture : IAsyncLifetime
{
    public const string AdminEmail = "admin.pruebas@test.local";
    public static readonly string AdminPassword = $"Pr-{Guid.NewGuid():N}"[..24];
    public const int TiempoMinimoFalloMs = 60;

    private readonly string nombreBase = $"LetreriaLosHerreraDB_Test_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> factory = null!;
    private string? tokenAdmin;
    private int secuencia;

    public IServiceProvider Services => factory.Services;

    public async Task InitializeAsync()
    {
        var servidor = Environment.GetEnvironmentVariable("TEST_SQL_SERVER") ?? @"(localdb)\MSSQLLocalDB";
        var configuracion = new Dictionary<string, string>
        {
            ["ConnectionStrings__DefaultConnection"] = $"Server={servidor};Database={nombreBase};Trusted_Connection=True;TrustServerCertificate=True;",
            ["Jwt__Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
            ["Jwt__Issuer"] = "pruebas",
            ["Jwt__Audience"] = "pruebas",
            ["Bootstrap__Admin__Rut"] = "1-9",
            ["Bootstrap__Admin__Nombre"] = "Admin",
            ["Bootstrap__Admin__Apellido"] = "Pruebas",
            ["Bootstrap__Admin__Telefono"] = "900000000",
            ["Bootstrap__Admin__Email"] = AdminEmail,
            ["Bootstrap__Admin__Password"] = AdminPassword,
            ["RateLimiting__ApiGeneral"] = "100000",
            ["RateLimiting__Login"] = "100000",
            ["RateLimiting__LoginFacial"] = "100000",
            ["RateLimiting__Reautenticacion"] = "100000",
            ["RateLimiting__Publico"] = "100000",
            ["RostroFacial__ClaveCifrado"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["RostroFacial__TiempoMinimoFalloMs"] = TiempoMinimoFalloMs.ToString()
        };

        // Program lee la configuracion antes de que WebApplicationFactory pueda cambiarla,
        // y las variables de entorno tienen prioridad sobre appsettings y user-secrets.
        foreach (var (clave, valor) in configuracion)
            Environment.SetEnvironmentVariable(clave, valor);

        factory = new WebApplicationFactory<Program>();
        _ = factory.Server;

        var conexion = await ConsultarAsync(db => Task.FromResult(db.Database.GetConnectionString()));
        if (conexion?.Contains(nombreBase) != true)
            throw new InvalidOperationException("Las pruebas no estan usando su base aislada.");
    }

    public async Task DisposeAsync()
    {
        await ConsultarAsync(db => db.Database.EnsureDeletedAsync());
        await factory.DisposeAsync();
    }

    public HttpClient Cliente(string? token = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // Cliente de una API que ademas ejecuta el interceptor: permite provocar situaciones que dependen del momento exacto.
    public HttpClient ClienteConInterceptor(IInterceptor interceptor, string token)
    {
        var conInterceptor = factory.WithWebHostBuilder(host => host.ConfigureServices(services =>
            services.ConfigureDbContext<LetrerosHerreradbContext>(options => options.AddInterceptors(interceptor))));
        var client = conInterceptor.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<T> ConsultarAsync<T>(Func<LetrerosHerreradbContext, Task<T>> consulta)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await consulta(scope.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>());
    }

    public int Siguiente() => Interlocked.Increment(ref secuencia);
    public string Telefono() => $"9{Siguiente():D8}";
    public string Rut() => $"{Siguiente():D7}-K";

    public Task<int> IdAdminAsync() =>
        ConsultarAsync(db => db.Empleados.Where(e => e.Email == AdminEmail).Select(e => e.IdEmpleado).SingleAsync());

    public async Task<string> TokenAdminAsync() =>
        tokenAdmin ??= await LoginEmpleadoAsync(AdminEmail, AdminPassword);

    public async Task<string> LoginEmpleadoAsync(string email, string password)
    {
        var respuesta = await Cliente().PostAsJsonAsync("/api/v1/auth/login", new { email, password }).EsperarAsync(HttpStatusCode.OK);
        return respuesta.GetProperty("token").GetString()!;
    }

    public async Task<string> TokenEmpleadoAsync(string rol)
    {
        var email = $"empleado{Siguiente()}@test.local";
        await Cliente(await TokenAdminAsync())
            .PostAsJsonAsync("/api/v1/auth/empleados", new { rut = Rut(), nombre = "Emp", apellido = "Prueba", telefono = Telefono(), email, password = AdminPassword, rol, passwordActual = AdminPassword })
            .EsperarAsync(HttpStatusCode.Created);
        return await LoginEmpleadoAsync(email, AdminPassword);
    }

    // Empleado nuevo con su email, para las pruebas que necesitan volver a iniciar sesion.
    public async Task<(int Id, string Email, string Token)> CrearEmpleadoAsync(string rol)
    {
        var email = $"empleado{Siguiente()}@test.local";
        var creado = await Cliente(await TokenAdminAsync())
            .PostAsJsonAsync("/api/v1/auth/empleados", new { rut = Rut(), nombre = "Emp", apellido = "Prueba", telefono = Telefono(), email, password = AdminPassword, rol, passwordActual = AdminPassword })
            .EsperarAsync(HttpStatusCode.Created);
        return (creado.GetProperty("idEmpleado").GetInt32(), email, await LoginEmpleadoAsync(email, AdminPassword));
    }

    // Otra API sobre la misma base con un valor de configuracion distinto. Program lo lee al arrancar:
    // la variable se cambia solo para eso, y se restaura aunque falle, para no afectar a las demas pruebas.
    public ApiTemporal ConVariable(string variable, string valor)
    {
        var anterior = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, valor);
        var otra = new WebApplicationFactory<Program>();
        try
        {
            _ = otra.Server;
        }
        catch
        {
            otra.Dispose();
            throw;
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, anterior);
        }
        return new ApiTemporal(otra);
    }

    public sealed class ApiTemporal(WebApplicationFactory<Program> factory) : IAsyncDisposable
    {
        public HttpClient Cliente() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        public ValueTask DisposeAsync() => factory.DisposeAsync();
    }

    // Otra API sobre la misma base que entrega sus logs a este proveedor.
    public HttpClient ClienteConLogs(ILoggerProvider proveedor)
    {
        var conLogs = factory.WithWebHostBuilder(host => host.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddFilter(_ => true);
            logging.AddProvider(proveedor);
        }));
        return conLogs.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    // Registra un cliente por la web y devuelve su id y su token.
    public async Task<(int Id, string Telefono, string Token)> RegistrarClienteAsync()
    {
        var telefono = Telefono();
        var creado = await Cliente()
            .PostAsJsonAsync("/api/v1/auth/registro-cliente", new { rut = Rut(), nombre = "Cli", apellido = "Web", telefono, password = AdminPassword })
            .EsperarAsync(HttpStatusCode.Created);
        return (creado.GetProperty("idCliente").GetInt32(), telefono, await LoginClienteAsync(telefono, AdminPassword));
    }

    public async Task<string> LoginClienteAsync(string telefono, string password)
    {
        var respuesta = await Cliente().PostAsJsonAsync("/api/v1/auth/login-cliente", new { telefono, password }).EsperarAsync(HttpStatusCode.OK);
        return respuesta.GetProperty("token").GetString()!;
    }

    // Cliente sin cuenta web, creado por el personal.
    public async Task<int> CrearClienteAsync()
    {
        var creado = await Cliente(await TokenAdminAsync())
            .PostAsJsonAsync("/api/v1/clientes/", new { nombre = "Cli", apellido = "Local", telefono = Telefono() })
            .EsperarAsync(HttpStatusCode.Created);
        return creado.GetProperty("idCliente").GetInt32();
    }

    public async Task<int> CrearSolicitudAsync(int? idCliente = null, int idProducto = 1)
    {
        var creada = await Cliente(await TokenAdminAsync())
            .PostAsJsonAsync("/api/v1/solicitudes/", new
            {
                idCliente = idCliente ?? await CrearClienteAsync(),
                detalles = new[] { new { idProducto, descripcion = "Letrero de prueba", anchoMm = 1000, altoMm = 2000, cantidad = 2 } }
            })
            .EsperarAsync(HttpStatusCode.Created);
        return creada.GetProperty("idSolicitud").GetInt32();
    }

    // Cotizacion de 1000 de mano de obra mas 2 x 500 en materiales: total 2000.
    public async Task<JsonElement> CrearCotizacionAsync(int idSolicitud)
    {
        return await Cliente(await TokenAdminAsync())
            .PostAsJsonAsync("/api/v1/cotizaciones/", new
            {
                idSolicitud,
                costoManoObra = 1000m,
                otrosCostos = 0m,
                materiales = new[] { new { idMaterial = 1, medidaAplicada = "2 x 1", cantidad = 2m, costoUnitario = 500m } }
            })
            .EsperarAsync(HttpStatusCode.Created);
    }

    public async Task AprobarCotizacionAsync(int idCotizacion)
    {
        await Cliente(await TokenAdminAsync())
            .PutAsJsonAsync($"/api/v1/cotizaciones/{idCotizacion}", new { costoManoObra = 1000m, otrosCostos = 0m, estado = "aprobada" })
            .EsperarAsync(HttpStatusCode.OK);
    }

    public async Task AceptarSolicitudAsync(int idSolicitud)
    {
        var cliente = Cliente(await TokenAdminAsync());
        var solicitud = await cliente.GetAsync($"/api/v1/solicitudes/{idSolicitud}").EsperarAsync(HttpStatusCode.OK);
        await cliente
            .PatchAsJsonAsync($"/api/v1/solicitudes/{idSolicitud}/estado", new { estado = "aceptado", rowVersion = solicitud.GetProperty("rowVersion").GetString() })
            .EsperarAsync(HttpStatusCode.OK);
    }
}

public static class RespuestaExtensions
{
    // Comprueba el status mostrando el cuerpo si falla, y devuelve el JSON de la respuesta.
    public static async Task<JsonElement> EsperarAsync(this Task<HttpResponseMessage> peticion, HttpStatusCode esperado)
    {
        using var respuesta = await peticion;
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == esperado, $"Se esperaba {(int)esperado} y llego {(int)respuesta.StatusCode}: {cuerpo}");
        return cuerpo.Length == 0 ? default : JsonDocument.Parse(cuerpo).RootElement.Clone();
    }
}
