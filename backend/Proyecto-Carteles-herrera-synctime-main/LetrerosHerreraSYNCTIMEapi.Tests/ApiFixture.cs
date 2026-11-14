using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Datos;
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
// Usa la instancia local de SQL Server Express; la variable TEST_SQL_SERVER permite apuntar a otro servidor.
public sealed class ApiFixture : IAsyncLifetime
{
    public const string AdminEmail = "admin.pruebas@test.local";
    public static readonly string AdminPassword = $"Pr-{Guid.NewGuid():N}"[..24];
    public const int TiempoMinimoFalloMs = 60;
    public const string ServidorPredeterminado = @"localhost\SQLEXPRESS";
    public const string AvisosTaller = "avisos.taller@test.local";

    // El prefijo identifica las bases temporales: es lo unico que este codigo tiene permitido eliminar.
    private readonly string nombreBase = $"{BasesDeDatos.PrefijoPruebas}{Guid.NewGuid():N}";

    // Carpetas propias de esta ejecucion para la bandeja de correo y las imagenes; se borran al terminar.
    private readonly string carpetaTemporal = Path.Combine(Path.GetTempPath(), $"synctime-pruebas-{Guid.NewGuid():N}");
    public string CarpetaBandeja => Path.Combine(carpetaTemporal, "bandeja");
    public string CarpetaImagenes => Path.Combine(carpetaTemporal, "imagenes");
    private WebApplicationFactory<Program> factory = null!;
    private string? tokenAdmin;
    private int secuencia;

    public IServiceProvider Services => factory.Services;

    public async Task InitializeAsync()
    {
        var servidor = Environment.GetEnvironmentVariable("TEST_SQL_SERVER") ?? ServidorPredeterminado;
        var configuracion = new Dictionary<string, string>
        {
            // Esperas amplias: en un equipo con poca memoria libre, SQL Server Express tarda en aceptar conexiones
            // y en crear la base, y un fallo por tiempo de espera no dice nada sobre el codigo que se prueba.
            ["ConnectionStrings__DefaultConnection"] = $"Server={servidor};Database={nombreBase};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=60;Command Timeout=180;",
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
            ["RateLimiting__ContactoPorHora"] = "100000",
            ["Privacidad__PurgaActiva"] = "false",
            ["RateLimiting__CookiesPorHora"] = "100000",
            ["RateLimiting__PorCuenta"] = "100000",
            ["RateLimiting__Salud"] = "100000",
            ["RostroFacial__ClaveCifrado"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["RostroFacial__TiempoMinimoFalloMs"] = TiempoMinimoFalloMs.ToString(),
            // Los correos van a una bandeja temporal y la cola no corre sola: cada prueba procesa cuando lo
            // necesita. Las variables de entorno pisan los User Secrets del equipo, asi que ni la cuenta real
            // ni su contraseña pueden llegar a estas pruebas: el SMTP apunta a un puerto local sin servidor.
            ["Correo__Modo"] = "Bandeja",
            ["Correo__Bandeja__Carpeta"] = CarpetaBandeja,
            ["Correo__AvisosTaller"] = AvisosTaller,
            ["Correo__Cola__Activa"] = "false",
            // Muchas pruebas crean solicitudes: el tope contra envios masivos se prueba aparte.
            ["Correo__TopeAvisosPorHora"] = "100000",
            ["Correo__Smtp__Servidor"] = "127.0.0.1",
            ["Correo__Smtp__Puerto"] = "9",
            ["Correo__Smtp__Seguridad"] = "Ninguna",
            ["Correo__Smtp__Usuario"] = "sin-uso@test.local",
            ["Correo__Smtp__Password"] = "sin-uso",
            ["Correo__Remitente__Direccion"] = "synctime@test.local",
            ["Sitio__UrlPublica"] = "https://synctime.test",
            ["Imagenes__Carpeta"] = CarpetaImagenes
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
        try
        {
            // Guarda final: se pregunta a la conexion real, no al nombre que este codigo cree estar usando.
            var conectada = await ConsultarAsync(db => Task.FromResult(db.Database.GetDbConnection().Database));
            if (!BasesDeDatos.EsDePruebas(conectada))
                throw new InvalidOperationException($"Se evito eliminar la base '{conectada}': no es una base temporal de pruebas.");

            await ConsultarAsync(db => db.Database.EnsureDeletedAsync());
        }
        finally
        {
            await factory.DisposeAsync();
            if (Directory.Exists(carpetaTemporal))
                Directory.Delete(carpetaTemporal, recursive: true);
        }
    }

    // Las pruebas de la cola de correo procesan "lo pendiente" y cuentan lo enviado en la ultima hora: se
    // vacia antes, para que los avisos que dejaron otras pruebas al crear solicitudes no se mezclen.
    public Task LimpiarColaDeCorreoAsync() =>
        ConsultarAsync(db => db.CorreosSalientes.ExecuteDeleteAsync());

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
    public ApiTemporal ConVariable(string variable, string valor) =>
        ConVariables(new Dictionary<string, string> { [variable] = valor });

    // Igual, con varios valores a la vez y, si se pide, con sus logs entregados a un proveedor.
    public ApiTemporal ConVariables(IReadOnlyDictionary<string, string> variables, ILoggerProvider? logs = null)
    {
        var anteriores = variables.Keys.ToDictionary(v => v, Environment.GetEnvironmentVariable);
        foreach (var (variable, valor) in variables)
            Environment.SetEnvironmentVariable(variable, valor);

        WebApplicationFactory<Program> otra = new();
        if (logs is not null)
        {
            otra = otra.WithWebHostBuilder(host => host.ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.AddFilter(_ => true);
                logging.AddProvider(logs);
            }));
        }
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
            foreach (var (variable, valor) in anteriores)
                Environment.SetEnvironmentVariable(variable, valor);
        }
        return new ApiTemporal(otra);
    }

    public sealed class ApiTemporal(WebApplicationFactory<Program> factory) : IAsyncDisposable
    {
        public IServiceProvider Services => factory.Services;

        public HttpClient Cliente(string? token = null)
        {
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            if (token is not null)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

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
    // Correo de prueba de un cliente registrado con RegistrarClienteAsync (fase 6: se ingresa con el correo).
    public static string CorreoDe(string telefono) => $"cli{telefono}@correo.test";

    // Cuerpo de un registro valido (fase 6): con correo y los consentimientos obligatorios.
    public object Registro(string telefono, string? correo = null, string? rut = null, bool marketing = false) => new
    {
        rut, nombre = "Cli", apellido = "Web", telefono, correo = correo ?? CorreoDe(telefono), password = AdminPassword,
        aceptaTerminosYPrivacidad = true, aceptaMarketing = marketing,
        versionTerminos = "1.0", versionPrivacidad = "1.0", versionMarketing = marketing ? "1.0" : null
    };

    public async Task<(int Id, string Telefono, string Token)> RegistrarClienteAsync()
    {
        var telefono = Telefono();
        var creado = await Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", Registro(telefono)).EsperarAsync(HttpStatusCode.Created);
        var usuario = creado.GetProperty("usuario");
        return (usuario.GetProperty("idCliente").GetInt32(), telefono, creado.GetProperty("token").GetString()!);
    }

    public async Task<string> LoginClienteAsync(string correo, string password)
    {
        var respuesta = await Cliente().PostAsJsonAsync("/api/v1/auth/login-cliente", new { correo, password }).EsperarAsync(HttpStatusCode.OK);
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

    // Texto de un error sin su traceId, para comparar dos respuestas que deben ser indistinguibles.
    public static string SinTraza(this JsonElement error)
    {
        var objeto = System.Text.Json.Nodes.JsonNode.Parse(error.GetRawText())!.AsObject();
        objeto.Remove("traceId");
        return objeto.ToJsonString();
    }
}
