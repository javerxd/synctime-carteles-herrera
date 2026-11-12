using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Datos;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Cimientos de la fase 1: formato unico de error, salud, compresion, cabeceras, limites y paginacion.
[Collection(ApiCollection.Name)]
public class CimientosTests(ApiFixture api)
{
    private static void EsErrorEstandar(JsonElement cuerpo, int estado, string codigo, string nivel = "api")
    {
        Assert.Equal(estado, cuerpo.GetProperty("status").GetInt32());
        Assert.Equal(codigo, cuerpo.GetProperty("codigo").GetString());
        Assert.Equal(nivel, cuerpo.GetProperty("nivel").GetString());
        var error = cuerpo.GetProperty("error").GetString();
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Equal(error, cuerpo.GetProperty("title").GetString());
    }

    // ── Formato unico de error (RFS31, S21) ──

    [Fact]
    public async Task SinSesion_Responde401ConElFormatoEstandar()
    {
        var cuerpo = await api.Cliente().GetAsync("/api/v1/clientes/").EsperarAsync(HttpStatusCode.Unauthorized);
        EsErrorEstandar(cuerpo, 401, "no_autenticado");
    }

    [Fact]
    public async Task SinPermiso_Responde403ConElFormatoEstandar()
    {
        var operario = await api.TokenEmpleadoAsync(Roles.Operario);
        var cuerpo = await api.Cliente(operario).DeleteAsync("/api/v1/clientes/999999").EsperarAsync(HttpStatusCode.Forbidden);
        EsErrorEstandar(cuerpo, 403, "sin_permiso");
    }

    [Fact]
    public async Task LoQueNoExiste_Responde404ConElFormatoEstandar()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());

        EsErrorEstandar(await admin.GetAsync("/api/v1/clientes/999999").EsperarAsync(HttpStatusCode.NotFound), 404, "no_encontrado");
        EsErrorEstandar(await admin.GetAsync("/api/v1/ruta-que-no-existe").EsperarAsync(HttpStatusCode.NotFound), 404, "no_encontrado");
    }

    [Fact]
    public async Task UnDatoInvalido_Responde400ConElMotivo()
    {
        var cuerpo = await api.Cliente()
            .PostAsJsonAsync("/api/v1/auth/login", new { email = "", password = "" })
            .EsperarAsync(HttpStatusCode.BadRequest);

        EsErrorEstandar(cuerpo, 400, "datos_invalidos");
        Assert.Contains("requeridos", cuerpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task UnJsonMalFormado_Responde400ConElFormatoEstandar()
    {
        using var contenido = new StringContent("{ esto no es json", Encoding.UTF8, "application/json");
        var cuerpo = await api.Cliente().PostAsync("/api/v1/auth/login", contenido).EsperarAsync(HttpStatusCode.BadRequest);
        EsErrorEstandar(cuerpo, 400, "solicitud_invalida");
    }

    [Fact]
    public async Task UnaRestriccionDeLaBase_IndicaElNivelBaseDeDatos()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var rut = api.Rut();
        await admin.PostAsJsonAsync("/api/v1/clientes/", new { rut, nombre = "Uno", apellido = "Prueba", telefono = api.Telefono() })
            .EsperarAsync(HttpStatusCode.Created);

        // El endpoint solo revisa el telefono: el RUT repetido lo detecta el indice unico de la base.
        var cuerpo = await admin.PostAsJsonAsync("/api/v1/clientes/", new { rut, nombre = "Dos", apellido = "Prueba", telefono = api.Telefono() })
            .EsperarAsync(HttpStatusCode.Conflict);

        EsErrorEstandar(cuerpo, 409, "registro_duplicado", "base_de_datos");
    }

    [Fact]
    public async Task UnErrorInesperado_Responde500SinRevelarDetallesInternos()
    {
        var cliente = api.ClienteConInterceptor(new FallaAlGuardar(), await api.TokenAdminAsync());

        using var respuesta = await cliente.PostAsJsonAsync("/api/v1/clientes/", new { nombre = "Falla", apellido = "Prueba", telefono = api.Telefono() });
        var texto = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        EsErrorEstandar(JsonDocument.Parse(texto).RootElement, 500, "error_interno");
        Assert.DoesNotContain(FallaAlGuardar.Detalle, texto);
        Assert.DoesNotContain("Exception", texto);
    }

    private sealed class FallaAlGuardar : SaveChangesInterceptor
    {
        public const string Detalle = "detalle-interno-que-no-debe-salir";

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Detalle);
    }

    // ── Limites de intentos (RNFP07) ──

    [Fact]
    public async Task ElLimitePorCuenta_RechazaConCuerpoYNoAfectaAOtrasCuentas()
    {
        await using var otra = api.ConVariable("RateLimiting__PorCuenta", "2");
        var cliente = otra.Cliente();
        var cuenta = $"limitada{api.Siguiente()}@test.local";
        Task<HttpResponseMessage> Intentar(string email) =>
            cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "clave-equivocada" });

        await Intentar(cuenta).EsperarAsync(HttpStatusCode.Unauthorized);
        await Intentar(cuenta.ToUpperInvariant()).EsperarAsync(HttpStatusCode.Unauthorized);
        var rechazo = await Intentar(cuenta).EsperarAsync(HttpStatusCode.TooManyRequests);

        EsErrorEstandar(rechazo, 429, "demasiados_intentos");
        await Intentar($"otra{api.Siguiente()}@test.local").EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ElLimitePorCuenta_TambienCubreAlCliente()
    {
        await using var otra = api.ConVariable("RateLimiting__PorCuenta", "1");
        var cliente = otra.Cliente();
        var telefono = api.Telefono();

        await cliente.PostAsJsonAsync("/api/v1/auth/login-cliente", new { telefono, password = "clave-equivocada" }).EsperarAsync(HttpStatusCode.Unauthorized);
        await cliente.PostAsJsonAsync("/api/v1/auth/login-cliente", new { telefono, password = "clave-equivocada" }).EsperarAsync(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ElLimitePorCuenta_SoloCuentaLosFallos()
    {
        await using var otra = api.ConVariable("RateLimiting__PorCuenta", "2");
        var cliente = otra.Cliente();
        var empleado = await api.CrearEmpleadoAsync(Roles.Operario);
        Task<HttpResponseMessage> Entrar(string password) =>
            cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = empleado.Email, password });

        // Entrar bien muchas veces no gasta intentos.
        for (var i = 0; i < 4; i++)
            await Entrar(ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);

        // Un fallo seguido de un acierto deja el contador en cero: hacen falta dos fallos seguidos para bloquear.
        await Entrar("clave-equivocada").EsperarAsync(HttpStatusCode.Unauthorized);
        await Entrar(ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.OK);
        await Entrar("clave-equivocada").EsperarAsync(HttpStatusCode.Unauthorized);
        await Entrar("clave-equivocada").EsperarAsync(HttpStatusCode.Unauthorized);

        // Bloqueada: ni la contraseña correcta entra hasta que pase el minuto.
        await Entrar(ApiFixture.AdminPassword).EsperarAsync(HttpStatusCode.TooManyRequests);
    }

    // Regla del acceso con rostro: un anonimo, que no tiene equipo autorizado, no puede bloquear el rostro
    // de otra persona. Por eso el limite por cuenta no se aplica a ese ingreso.
    [Fact]
    public async Task ElLimitePorCuenta_NoPermiteBloquearElRostroDeOtraPersona()
    {
        await using var otra = api.ConVariable("RateLimiting__PorCuenta", "1");
        var cliente = otra.Cliente();
        var email = $"rostro{api.Siguiente()}@test.local";
        var descriptor = Enumerable.Range(0, 128).Select(i => i % 7 * 0.01f + 0.01f).ToArray();

        for (var i = 0; i < 4; i++)
            await cliente.PostAsJsonAsync("/api/v1/auth/login-facial", new { email, descriptor }).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    // El tope de tamaño del cuerpo lo aplica el servidor real (Kestrel), no el de pruebas. Lo que si se
    // puede comprobar aqui es que un correo o una contraseña desmesurados se rechazan sin consultar nada.
    [Fact]
    public async Task UnCorreoOUnaPasswordDesmesurados_SeRechazanEnElInicioDeSesion()
    {
        var enorme = new string('a', 5_000);

        var porCorreo = await api.Cliente().PostAsJsonAsync("/api/v1/auth/login", new { email = $"{enorme}@test.local", password = "x" }).EsperarAsync(HttpStatusCode.BadRequest);
        var porPassword = await api.Cliente().PostAsJsonAsync("/api/v1/auth/login", new { email = "a@test.local", password = enorme }).EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/login-cliente", new { telefono = enorme, password = "x" }).EsperarAsync(HttpStatusCode.BadRequest);

        EsErrorEstandar(porCorreo, 400, "datos_invalidos");
        EsErrorEstandar(porPassword, 400, "datos_invalidos");
    }

    [Fact]
    public async Task ElLimitePorIp_RechazaConElFormatoEstandar()
    {
        await using var otra = api.ConVariable("RateLimiting__Login", "1");
        var cliente = otra.Cliente();

        await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = $"a{api.Siguiente()}@test.local", password = "x" }).EsperarAsync(HttpStatusCode.Unauthorized);
        var rechazo = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = $"b{api.Siguiente()}@test.local", password = "x" })
            .EsperarAsync(HttpStatusCode.TooManyRequests);

        EsErrorEstandar(rechazo, 429, "demasiados_intentos");
    }

    // ── Salud, compresion y cabeceras (RNFP25) ──

    [Fact]
    public async Task Salud_RespondeSinSesion()
    {
        var viva = await api.Cliente().GetAsync("/salud").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("ok", viva.GetProperty("estado").GetString());

        var lista = await api.Cliente().GetAsync("/salud/lista").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("ok", lista.GetProperty("estado").GetString());
        Assert.Equal("ok", lista.GetProperty("comprobaciones").GetProperty("base_de_datos").GetString());
    }

    [Theory]
    [InlineData("br")]
    [InlineData("gzip")]
    public async Task LasRespuestas_SeComprimenSiElClienteLoAdmite(string codificacion)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, "/api/v1/productos/");
        peticion.Headers.AcceptEncoding.ParseAdd(codificacion);

        using var respuesta = await api.Cliente().SendAsync(peticion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains(codificacion, respuesta.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task TodaRespuesta_LlevaLasCabecerasDeSeguridad()
    {
        using var respuesta = await api.Cliente().GetAsync("/api/v1/productos/");

        Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", respuesta.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", respuesta.Headers.GetValues("Referrer-Policy").Single());
    }

    // El manejador de excepciones borra las cabeceras puestas antes de el: los errores deben conservarlas,
    // y sin las de CORS el navegador no dejaria que el frontend leyera el motivo del error.
    [Fact]
    public async Task LosErrores_ConservanLasCabecerasDeSeguridadYDeCors()
    {
        const string origen = "http://localhost:3000";
        var token = await api.TokenAdminAsync();
        var rut = api.Rut();
        var admin = api.Cliente(token);
        admin.DefaultRequestHeaders.Add("Origin", origen);
        var conFalla = api.ClienteConInterceptor(new FallaAlGuardar(), token);
        conFalla.DefaultRequestHeaders.Add("Origin", origen);

        await admin.PostAsJsonAsync("/api/v1/clientes/", new { rut, nombre = "Uno", apellido = "Prueba", telefono = api.Telefono() }).EsperarAsync(HttpStatusCode.Created);
        using var conflicto = await admin.PostAsJsonAsync("/api/v1/clientes/", new { rut, nombre = "Dos", apellido = "Prueba", telefono = api.Telefono() });
        using var interno = await conFalla.PostAsJsonAsync("/api/v1/clientes/", new { nombre = "Falla", apellido = "Prueba", telefono = api.Telefono() });
        using var sinSesion = await api.Cliente().SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/clientes/") { Headers = { { "Origin", origen } } });

        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, interno.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, sinSesion.StatusCode);
        Assert.All(new[] { conflicto, interno, sinSesion }, respuesta =>
        {
            Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", respuesta.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal(origen, respuesta.Headers.GetValues("Access-Control-Allow-Origin").Single());
        });
    }

    [Fact]
    public async Task LasRespuestasDeAutenticacion_NoSeGuardanEnCache()
    {
        using var respuesta = await api.Cliente().PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFixture.AdminEmail, password = ApiFixture.AdminPassword });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public void LosErroresPorCampo_SeEscribenComoUnObjetoDeListas()
    {
        var errores = new ErroresDeCampos()
            .Si(true, "rut", "El RUT no es válido.")
            .Si(true, "telefono", "El teléfono debe tener nueve dígitos.");

        var json = JsonSerializer.SerializeToElement(
            LetrerosHerreraSYNCTIMEapi.Api.ErrorApi.Detalle(400, "datos_invalidos", errores.Resumen, errores.PorCampo()),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal("El RUT no es válido.", json.GetProperty("errores").GetProperty("rut")[0].GetString());
        Assert.Equal(["rut", "telefono"], json.GetProperty("errores").EnumerateObject().Select(p => p.Name));
        Assert.Equal("El RUT no es válido.", json.GetProperty("error").GetString());
    }

    // ── Paginacion y orden (RFS32) ──

    private static readonly Dictionary<string, Expression<Func<Material, object?>>> CamposMaterial = new()
    {
        ["nombre"] = m => m.Nombre,
        ["costo"] = m => m.CostoUnitarioAprox,
        ["activo"] = m => m.Activo
    };

    private static readonly Expression<Func<Material, object?>> PorId = m => m.IdMaterial;

    [Fact]
    public async Task Paginar_CuentaElTotalYTraeSoloLaPaginaPedida()
    {
        var todos = await api.ConsultarAsync(db => db.Materiales.OrderBy(m => m.Nombre).Select(m => m.Nombre).ToListAsync());
        Assert.True(todos.Count >= 5, "Las migraciones siembran cinco materiales.");

        var segunda = await api.ConsultarAsync(db => db.Materiales.AsNoTracking()
            .Ordenar("nombre", CamposMaterial, "nombre", PorId)
            .Select(m => m.Nombre)
            .PaginarAsync(new ParametrosListado(pagina: 2, tamano: 2)));

        Assert.Equal(todos.Count, segunda.Total);
        Assert.Equal(todos.Skip(2).Take(2), segunda.Elementos);
        Assert.Equal((2, 2), (segunda.Pagina, segunda.Tamano));

        var fueraDeRango = await api.ConsultarAsync(db => db.Materiales.OrderBy(m => m.Nombre).PaginarAsync(new ParametrosListado(pagina: 500)));
        Assert.Empty(fueraDeRango.Elementos);
        Assert.Equal(todos.Count, fueraDeRango.Total);
    }

    [Fact]
    public async Task Ordenar_AceptaSoloCamposPermitidos()
    {
        Task<List<decimal>> Costos(string? orden) => api.ConsultarAsync(db => db.Materiales.AsNoTracking()
            .Ordenar(orden, CamposMaterial, "nombre", PorId).Select(m => m.CostoUnitarioAprox).ToListAsync());
        Task<List<string>> Nombres(string? orden) => api.ConsultarAsync(db => db.Materiales.AsNoTracking()
            .Ordenar(orden, CamposMaterial, "nombre", PorId).Select(m => m.Nombre).ToListAsync());

        var ascendente = await Costos("costo");
        Assert.Equal(ascendente.OrderBy(c => c), ascendente);
        Assert.True(ascendente.Distinct().Count() > 1, "Los costos de la semilla deben diferir para que el orden se note.");

        var descendente = await Costos("-costo");
        Assert.Equal(descendente.OrderByDescending(c => c), descendente);
        Assert.NotEqual(ascendente, descendente);

        // Un campo desconocido, o un intento de inyeccion, usa el orden predeterminado, que es por nombre.
        var esperado = await api.ConsultarAsync(db => db.Materiales.OrderBy(m => m.Nombre).Select(m => m.Nombre).ToListAsync());
        Assert.Equal(esperado, await Nombres(null));
        Assert.Equal(esperado, await Nombres("password_hash"));
        Assert.Equal(esperado, await Nombres("nombre; DROP TABLE materiales"));
        Assert.NotEqual(esperado, await Nombres("-nombre"));
    }

    [Fact]
    public async Task Ordenar_DesempataParaQueLasPaginasNoSeSolapen()
    {
        // En memoria, con filas que llegan en un orden distinto al de su id y empatan en el campo pedido:
        // sin desempate conservarian el orden de llegada (3, 1, 2).
        var empatados = new[] { 3, 1, 2 }.Select(id => new Material { IdMaterial = id, Nombre = "x", Activo = true }).AsQueryable();
        Assert.Equal([1, 2, 3], empatados.Ordenar("activo", CamposMaterial, "nombre", PorId).Select(m => m.IdMaterial));
        Assert.Equal([1, 2, 3], empatados.Ordenar("-activo", CamposMaterial, "nombre", PorId).Select(m => m.IdMaterial));

        // Y contra SQL Server: dos paginas seguidas no repiten ni omiten filas.
        var ids = await api.ConsultarAsync(db => db.Materiales.AsNoTracking()
            .Ordenar("activo", CamposMaterial, "nombre", PorId).Select(m => m.IdMaterial).ToListAsync());
        Assert.Equal(ids.Order(), ids);

        var primera = await api.ConsultarAsync(db => db.Materiales.AsNoTracking()
            .Ordenar("activo", CamposMaterial, "nombre", PorId).Select(m => m.IdMaterial).PaginarAsync(new ParametrosListado(pagina: 1, tamano: 2)));
        var segunda = await api.ConsultarAsync(db => db.Materiales.AsNoTracking()
            .Ordenar("activo", CamposMaterial, "nombre", PorId).Select(m => m.IdMaterial).PaginarAsync(new ParametrosListado(pagina: 2, tamano: 2)));
        Assert.Equal(ids.Take(4), primera.Elementos.Concat(segunda.Elementos));
    }

    [Fact]
    public void Ordenar_ConUnPredeterminadoQueNoExiste_FallaConUnMensajeClaro()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            Array.Empty<Material>().AsQueryable().Ordenar(null, CamposMaterial, "inexistente", PorId));
        Assert.Contains("inexistente", error.Message);
    }

    // ── Bases de datos: pruebas aisladas y semilla de demostracion ──

    [Fact]
    public async Task LasPruebas_UsanUnaBaseTemporalConElPrefijoAcordado()
    {
        var conexion = await api.ConsultarAsync(db => Task.FromResult(db.Database.GetConnectionString()!));
        Assert.True(BasesDeDatos.EsDePruebas(BasesDeDatos.Nombre(conexion)));
    }

    [Fact]
    public async Task LaSemilla_SeNiegaAEscribirFueraDeUnaBaseDemo()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Demo:Password"] = ApiFixture.AdminPassword })
            .Build();
        var empleadosAntes = await api.ConsultarAsync(db => db.Empleados.CountAsync());

        var codigo = await SemillaDemo.EjecutarAsync(api.Services, configuracion, "LetreriaLosHerreraDB", NullLogger.Instance);

        Assert.Equal(1, codigo);
        Assert.Equal(empleadosAntes, await api.ConsultarAsync(db => db.Empleados.CountAsync()));
    }

    [Fact]
    public async Task LaSemilla_CreaUnUsuarioPorRolYNoSeDuplicaAlRepetirla()
    {
        var conexion = await api.ConsultarAsync(db => Task.FromResult(db.Database.GetConnectionString()!));
        var nombre = $"{BasesDeDatos.PrefijoPruebas}semilla_{Guid.NewGuid():N}";
        var opciones = new DbContextOptionsBuilder<LetrerosHerreradbContext>()
            .UseSqlServer(conexion.Replace(BasesDeDatos.Nombre(conexion), nombre))
            .Options;

        await using var db = new LetrerosHerreradbContext(opciones);
        try
        {
            await db.Database.MigrateAsync();
            await using var scope = api.Services.CreateAsyncScope();
            var auth = scope.ServiceProvider.GetRequiredService<AuthService>();

            Assert.True(await SemillaDemo.SembrarAsync(db, auth, ApiFixture.AdminPassword));
            Assert.False(await SemillaDemo.SembrarAsync(db, auth, ApiFixture.AdminPassword));

            var empleados = await db.Empleados.AsNoTracking().ToListAsync();
            Assert.Equal(Roles.Empleados.Order(), empleados.Select(e => e.Rol).Order());
            Assert.All(empleados, e =>
            {
                Assert.True(Rut.EsValido(e.Rut), $"RUT de demostracion invalido: {e.Rut}");
                Assert.True(auth.VerifyPassword(ApiFixture.AdminPassword, e.PasswordHash));
                Assert.EndsWith(SemillaDemo.DominioCorreo, e.Email);
            });

            Assert.True(await db.Clientes.CountAsync() >= 3);
            Assert.Contains(await db.Clientes.AsNoTracking().ToListAsync(), c => c.PasswordHash is not null);
            Assert.True(await db.Catalogos.CountAsync() >= 3);
            Assert.True(await db.Solicitudes.CountAsync() >= 3);
            Assert.Contains(await db.Solicitudes.Select(s => s.Origen).ToListAsync(), origen => origen == "online");
        }
        finally
        {
            // Sin Assert aqui: ocultaria el fallo original de la prueba.
            if (BasesDeDatos.EsDePruebas(db.Database.GetDbConnection().Database))
                await db.Database.EnsureDeletedAsync();
        }
    }
}
