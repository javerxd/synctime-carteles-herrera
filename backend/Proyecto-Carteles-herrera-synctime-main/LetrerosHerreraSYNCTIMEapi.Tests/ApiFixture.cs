using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Datos;
using LetrerosHerreraSYNCTIMEapi.Models;
using Microsoft.AspNetCore.Builder;
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
// Usa la instancia local de SQL Server Express; la variable TEST_SQL_SERVER permite apuntar a otro servidor, y TEST_SQL_PASSWORD (con
// TEST_SQL_USER, por omision «sa») cambia a la autenticacion de SQL Server para un contenedor (ver ConexionDePruebas).
public sealed class ApiFixture : IAsyncLifetime
{
    public const string AdminEmail = "admin.pruebas@test.local";
    public static readonly string AdminPassword = $"Pr-{Guid.NewGuid():N}"[..24];
    public const int TiempoMinimoFalloMs = 60;
    public const string AvisosTaller = "avisos.taller@test.local";

    // El prefijo identifica las bases temporales: es lo unico que este codigo tiene permitido eliminar.
    private readonly string nombreBase = $"{BasesDeDatos.PrefijoPruebas}{Guid.NewGuid():N}";

    // Carpetas propias de esta ejecucion para la bandeja de correo y las imagenes; se borran al terminar.
    private readonly string carpetaTemporal = Path.Combine(Path.GetTempPath(), $"synctime-pruebas-{Guid.NewGuid():N}");
    public string CarpetaBandeja => Path.Combine(carpetaTemporal, "bandeja");
    public string CarpetaImagenes => Path.Combine(carpetaTemporal, "imagenes");
    public string CarpetaAdjuntos => Path.Combine(carpetaTemporal, "adjuntos");
    public string CarpetaExportaciones => Path.Combine(carpetaTemporal, "exportaciones");

    // Fase 7: la regla contra solicitudes falsas mira la IP. El servidor de pruebas no tiene una, asi que
    // una prueba puede elegirla con esta cabecera (solo existe en las pruebas, nunca en la API real).
    public const string CabeceraIp = "X-Prueba-Ip";
    private WebApplicationFactory<Program> factory = null!;
    private string? tokenAdmin;
    private int secuencia;

    public IServiceProvider Services => factory.Services;

    // Fase 14A: el ID de cliente de Google de las pruebas es inventado, y los tokens los fabrica el verificador simulado.
    public const string GoogleClientId = "pruebas-synctime.apps.googleusercontent.com";
    public VerificadorGoogleSimulado Google { get; } = new();

    public async Task InitializeAsync()
    {
        var configuracion = new Dictionary<string, string>
        {
            // Fase 16B: Windows en el equipo de desarrollo; usuario y clave de SQL Server si el entorno define TEST_SQL_PASSWORD (pipeline).
            // Esperas amplias: un fallo por tiempo de espera no dice nada sobre el codigo que se prueba (ver ConexionDePruebas).
            ["ConnectionStrings__DefaultConnection"] = ConexionDePruebas.DesdeEntorno(Environment.GetEnvironmentVariable, nombreBase),
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
            ["RateLimiting__DesafioFacial"] = "100000",
            ["RateLimiting__Reautenticacion"] = "100000",
            ["RateLimiting__Publico"] = "100000",
            ["RateLimiting__ContactoPorHora"] = "100000",
            ["Privacidad__PurgaActiva"] = "false",
            ["RateLimiting__CookiesPorHora"] = "100000",
            ["RateLimiting__PorCuenta"] = "100000",
            ["RateLimiting__Salud"] = "100000",
            ["RateLimiting__Adjuntos"] = "100000",
            ["RostroFacial__ClaveCifrado"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["RostroFacial__TiempoMinimoFalloMs"] = TiempoMinimoFalloMs.ToString(),
            // Los vectores sinteticos de RostroFacialTests se arman alrededor de 0.5; el valor calibrado
            // (appsettings.json) puede cambiar sin tocar estas pruebas.
            ["RostroFacial__UmbralDistancia"] = "0.5",
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
            ["Imagenes__Carpeta"] = CarpetaImagenes,
            ["Adjuntos__Carpeta"] = CarpetaAdjuntos,
            // Fase 13A: las exportaciones van a una carpeta temporal y el proceso en segundo plano no corre solo: cada prueba lo ejecuta
            // cuando lo necesita (TrabajadorExportaciones), asi nada depende de cuando despierte.
            ["Reportes__Carpeta"] = CarpetaExportaciones,
            ["Reportes__ColaActiva"] = "false",
            ["RateLimiting__Exportaciones"] = "100000",
            ["RateLimiting__Reportes"] = "100000",
            // Fase 14A: un ID de cliente INVENTADO (no es el real): el verificador simulado nunca llama a Google. El limite de espera
            // baja a 1 segundo para que las pruebas de «Google lento» no tarden; el tope real es de 5 (OpcionesGoogle).
            ["Google__ClientId"] = GoogleClientId,
            ["RateLimiting__IngresoGoogle"] = "100000",
            ["Google__SegundosLimite"] = "1"
        };

        // Program lee la configuracion antes de que WebApplicationFactory pueda cambiarla,
        // y las variables de entorno tienen prioridad sobre appsettings y user-secrets.
        foreach (var (clave, valor) in configuracion)
            Environment.SetEnvironmentVariable(clave, valor);

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            host.ConfigureServices(services =>
            {
                services.AddTransient<IStartupFilter, IpDePrueba>();
                // Fase 14A: la API de las pruebas verifica los tokens de Google con este simulado, nunca con Google.
                services.AddSingleton<Negocio.IngresoGoogle.IVerificadorGoogle>(Google);
            }));
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

    // Fase 9: otra API sobre la misma base con el reloj adelantado (por ejemplo, para que venza un enlace).
    public HttpClient ClienteConReloj(TimeSpan adelanto, string? token = null)
    {
        var conReloj = factory.WithWebHostBuilder(host => host.ConfigureServices(services =>
            services.AddSingleton<TimeProvider>(new RelojAdelantado(adelanto))));
        var client = conReloj.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class RelojAdelantado(TimeSpan adelanto) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + adelanto;
    }

    public async Task<T> ConsultarAsync<T>(Func<LetrerosHerreradbContext, Task<T>> consulta)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await consulta(scope.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>());
    }

    public int Siguiente() => Interlocked.Increment(ref secuencia);
    public string Telefono() => $"9{Siguiente():D8}";
    // Fase 9: el alta de personal exige un RUT valido (con su digito verificador).
    public string Rut()
    {
        var cuerpo = (20_000_000 + Siguiente()).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"{cuerpo}-{Negocio.Rut.DigitoVerificador(cuerpo)}";
    }

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
            .PostAsJsonAsync("/api/v1/empleados", new { rut = Rut(), nombre = "Emp", apellido = "Prueba", telefono = Telefono(), email, password = AdminPassword, rol, passwordActual = AdminPassword })
            .EsperarAsync(HttpStatusCode.Created);
        return await LoginEmpleadoAsync(email, AdminPassword);
    }

    // Empleado nuevo con su email, para las pruebas que necesitan volver a iniciar sesion.
    public async Task<(int Id, string Email, string Token)> CrearEmpleadoAsync(string rol)
    {
        var email = $"empleado{Siguiente()}@test.local";
        var creado = await Cliente(await TokenAdminAsync())
            .PostAsJsonAsync("/api/v1/empleados", new { rut = Rut(), nombre = "Emp", apellido = "Prueba", telefono = Telefono(), email, password = AdminPassword, rol, passwordActual = AdminPassword })
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

    // Fase 16A: otra API sobre la misma base, en el entorno indicado (por ejemplo «Production») y con la IP elegida por la cabecera
    // X-Prueba-Ip, como la API principal de las pruebas. Es lo que permite probar lo que cambia segun el entorno: el CORS, la URL
    // publica y la lectura de las cabeceras de un proxy.
    public ApiTemporal ConEntorno(string entorno, IReadOnlyDictionary<string, string> variables)
    {
        var anteriores = variables.Keys.ToDictionary(v => v, Environment.GetEnvironmentVariable);
        foreach (var (variable, valor) in variables)
            Environment.SetEnvironmentVariable(variable, valor);

        var otra = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment(entorno);
            host.ConfigureServices(services =>
            {
                services.AddTransient<IStartupFilter, IpDePrueba>();
                services.AddSingleton<Negocio.IngresoGoogle.IVerificadorGoogle>(Google);
            });
        });
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

        public async Task<T> ConsultarAsync<T>(Func<LetrerosHerreradbContext, Task<T>> consulta)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await consulta(scope.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>());
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
        versionTerminos = "1.0", versionPrivacidad = "1.1", versionMarketing = marketing ? "1.0" : null
    };

    public async Task<(int Id, string Telefono, string Token)> RegistrarClienteAsync()
    {
        var telefono = Telefono();
        var creado = await Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", Registro(telefono)).EsperarAsync(HttpStatusCode.Created);
        var usuario = creado.GetProperty("usuario");
        return (usuario.GetProperty("idCliente").GetInt32(), telefono, creado.GetProperty("token").GetString()!);
    }

    // Fase 7: cliente con cuenta web y correo confirmado, creado directo en la base (asi no deja el correo
    // de verificacion en la cola). Las solicitudes en linea lo exigen.
    public async Task<(int Id, string Correo, string Token)> CrearClienteConfirmadoAsync(string? correo = null, string? rut = null)
    {
        var telefono = Telefono();
        correo ??= CorreoDe(telefono);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
        var auth = scope.ServiceProvider.GetRequiredService<Services.AuthService>();
        var cliente = new Cliente
        {
            Rut = rut,
            Nombre = "Cli",
            Apellido = "Web",
            Telefono = telefono,
            Email = correo,
            PasswordHash = auth.HashPassword(AdminPassword),
            Registrado = true,
            FechaVerificacionCorreo = DateTimeOffset.UtcNow
        };
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();
        return (cliente.IdCliente, correo, auth.CreateTokenCliente(cliente));
    }

    // Cuerpo valido de una solicitud (fase 7: descripcion de 10 caracteres o mas y al menos un item).
    public static object CuerpoSolicitud(int idProducto = 1, IEnumerable<int>? adjuntos = null, string descripcion = "Letrero para la vitrina del local") => new
    {
        descripcionGeneral = descripcion,
        detalles = new[] { new { idProducto, descripcion = "Letrero luminoso", anchoMm = 1000, altoMm = 2000, cantidad = 2 } },
        adjuntos = adjuntos?.ToArray() ?? []
    };

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
                descripcionGeneral = "Encargo registrado en el local",
                detalles = new[] { new { idProducto, descripcion = "Letrero de prueba", anchoMm = 1000, altoMm = 2000, cantidad = 2 } }
            })
            .EsperarAsync(HttpStatusCode.Created);
        return creada.GetProperty("idSolicitud").GetInt32();
    }

    // Fase 8: se emite sobre la solicitud (que pasa a «cotizado»). Cotizacion de 1000 de mano de obra mas
    // 2 x 500 en materiales: total 2000.
    public async Task<JsonElement> CrearCotizacionAsync(int idSolicitud, string? token = null) =>
        await Cliente(token ?? await TokenAdminAsync())
            .PostAsJsonAsync($"/api/v1/solicitudes/{idSolicitud}/cotizaciones", CuerpoCotizacion())
            .EsperarAsync(HttpStatusCode.Created);

    public static object CuerpoCotizacion(decimal manoObra = 1000m, decimal otros = 0m, string? observaciones = null) => new
    {
        costoManoObra = manoObra,
        otrosCostos = otros,
        observaciones,
        materiales = new[] { new { idMaterial = 1, medidaAplicada = "2 x 1", cantidad = 2m, costoUnitario = 500m } }
    };

    // Fase 8: la respuesta que el cliente dio en el local, registrada por el personal con la version vista.
    public async Task<JsonElement> ResponderCotizacionAsync(int idCotizacion, string decision = "aprobada", string? motivo = null)
    {
        var admin = Cliente(await TokenAdminAsync());
        var version = (await admin.GetAsync($"/api/v1/cotizaciones/{idCotizacion}").EsperarAsync(HttpStatusCode.OK)).GetProperty("rowVersion").GetString()!;
        return await admin.SendAsync(ConVersion(HttpMethod.Post, $"/api/v1/cotizaciones/{idCotizacion}/respuesta", version,
            new { decision, via = "presencial", motivo })).EsperarAsync(HttpStatusCode.OK);
    }

    public Task AprobarCotizacionAsync(int idCotizacion) => ResponderCotizacionAsync(idCotizacion);

    // «Hoy» en Chile, como lo usa la API.
    public static DateOnly HoyChile() => DateOnly.FromDateTime(Negocio.Local.HoraChile.Convertir(DateTimeOffset.UtcNow));

    // Datos minimos de un pedido (E08): retiro en el local, entrega en 5 dias.
    public static object DatosPedido(int idResponsable, object? abonoInicial = null) => new
    {
        idEmpleadoResponsable = idResponsable,
        fechaEntregaEstimada = HoyChile().AddDays(5),
        modalidadEntrega = "retiro_local",
        requiereInstalacion = false,
        costoInstalacionAdicional = 0,
        abonoInicial
    };

    // Fase 8: un pedido nace de una cotizacion aprobada. Sobre la solicitud indicada (o una presencial nueva)
    // se emite una cotizacion de `monto` en mano de obra, sin materiales; se aprueba en el local y se crea el
    // pedido «aceptado». Devuelve la ficha del pedido.
    public async Task<JsonElement> CrearPedidoAsync(int monto = 100_000, int? idSolicitud = null, object? abonoInicial = null)
    {
        var admin = Cliente(await TokenAdminAsync());
        var solicitud = idSolicitud ?? await CrearSolicitudAsync();
        var cotizacion = await admin.PostAsJsonAsync($"/api/v1/solicitudes/{solicitud}/cotizaciones",
            new { costoManoObra = monto, otrosCostos = 0, materiales = Array.Empty<object>() }).EsperarAsync(HttpStatusCode.Created);
        var idCotizacion = cotizacion.GetProperty("idCotizacion").GetInt32();
        await ResponderCotizacionAsync(idCotizacion);
        return await admin.PostAsJsonAsync($"/api/v1/cotizaciones/{idCotizacion}/pedido", DatosPedido(await IdAdminAsync(), abonoInicial))
            .EsperarAsync(HttpStatusCode.Created);
    }

    // Fase 7: version vigente de una solicitud (el rowVersion de su ficha), para If-Match.
    public async Task<string> VersionAsync(int idSolicitud) =>
        (await Cliente(await TokenAdminAsync()).GetAsync($"/api/v1/solicitudes/{idSolicitud}").EsperarAsync(HttpStatusCode.OK))
            .GetProperty("rowVersion").GetString()!;

    // Peticion con la cabecera If-Match (y un cuerpo JSON si se pasa).
    public static HttpRequestMessage ConVersion(HttpMethod metodo, string url, string version, object? cuerpo = null)
    {
        var peticion = new HttpRequestMessage(metodo, url);
        if (cuerpo is not null)
            peticion.Content = JsonContent.Create(cuerpo);
        peticion.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return peticion;
    }

    // Fase 7: «aceptado» ya no se pide por PATCH /estado; llega al aprobar la cotizacion (fase 8). Las
    // pruebas de pedidos y pagos solo necesitan una solicitud aceptada: se deja asi directo en la base.
    public Task AceptarSolicitudAsync(int idSolicitud) =>
        ConsultarAsync(db => db.Solicitudes.Where(s => s.IdSolicitud == idSolicitud)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, "aceptado")));
}

// Middleware de prueba: si la peticion trae X-Prueba-Ip, esa es su IP; si no, no tiene ninguna.
public sealed class IpDePrueba : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((contexto, siguiente) =>
        {
            contexto.Connection.RemoteIpAddress = contexto.Request.Headers.TryGetValue(ApiFixture.CabeceraIp, out var ip)
                && System.Net.IPAddress.TryParse(ip.ToString(), out var direccion) ? direccion : null;
            return siguiente(contexto);
        });
        next(app);
    };
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
