using System.Net;
using System.Net.Http.Json;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

[Collection(ApiCollection.Name)]
public sealed class AutorizacionTests(ApiFixture api)
{
    public static TheoryData<string> RutasDePersonal =>
    [
        "/api/v1/clientes/",
        "/api/v1/empleados",
        "/api/v1/materiales",
        "/api/v1/solicitudes/",
        "/api/v1/cotizaciones/",
        "/api/v1/pedidos/",
        "/api/v1/metricas/conversion-online"
    ];

    [Theory]
    [MemberData(nameof(RutasDePersonal))]
    public async Task StaffRoutes_RejectAnonymous(string ruta)
    {
        await api.Cliente().GetAsync(ruta).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(RutasDePersonal))]
    public async Task StaffRoutes_RejectClientToken(string ruta)
    {
        var (_, _, token) = await api.RegistrarClienteAsync();

        await api.Cliente(token).GetAsync(ruta).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Metrics_RequireManagementRole()
    {
        var operario = await api.TokenEmpleadoAsync(Roles.Operario);

        await api.Cliente(operario).GetAsync("/api/v1/metricas/conversion-online").EsperarAsync(HttpStatusCode.Forbidden);
        await api.Cliente(operario).GetAsync("/api/v1/solicitudes/").EsperarAsync(HttpStatusCode.OK);
    }

    // Antes de corregir la base, crear estos dos roles respondia 500 por las ñ corruptas del CHECK.
    [Theory]
    [InlineData(Roles.Dueno)]
    [InlineData(Roles.Disenador)]
    [InlineData(Roles.Operario)]
    [InlineData(Roles.Administrador)]
    public async Task EveryEmployeeRole_CanBeCreatedAndLogIn(string rol)
    {
        var token = await api.TokenEmpleadoAsync(rol);

        await api.Cliente(token).GetAsync("/api/v1/auth/me").EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EmployeeRoles_LimitWhatEachOneCanDo()
    {
        var producto = new { idCategoria = 1, nombre = $"Producto {api.Siguiente()}" };

        await api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario)).PostAsJsonAsync("/api/v1/productos/", producto).EsperarAsync(HttpStatusCode.Forbidden);
        await api.Cliente(await api.TokenEmpleadoAsync(Roles.Dueno)).PostAsJsonAsync("/api/v1/productos/", producto).EsperarAsync(HttpStatusCode.Created);
        await api.Cliente(await api.TokenEmpleadoAsync(Roles.Disenador)).DeleteAsync("/api/v1/solicitudes/1").EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateEmployee_RejectsBadInput()
    {
        var token = await api.TokenAdminAsync();
        object Empleado(string rol, string password) => new { rut = api.Rut(), nombre = "A", apellido = "B", telefono = api.Telefono(), email = $"e{api.Siguiente()}@test.local", password, rol, passwordActual = ApiFixture.AdminPassword };

        // Se comprueba el motivo: un 400 por falta de contraseña actual haria pasar estas lineas sin validar nada.
        var rolInvalido = await api.Cliente(token).PostAsJsonAsync("/api/v1/auth/empleados", Empleado("jefe", ApiFixture.AdminPassword)).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Contains("Rol", rolInvalido.GetProperty("error").GetString());
        var corta = await api.Cliente(token).PostAsJsonAsync("/api/v1/auth/empleados", Empleado(Roles.Operario, "corta")).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal(AuthService.MensajePasswordInvalida, corta.GetProperty("error").GetString());
        var vacios = await api.Cliente(token).PostAsJsonAsync("/api/v1/auth/empleados", new { rut = "", nombre = "", apellido = "", telefono = "", email = "", password = ApiFixture.AdminPassword, rol = Roles.Operario, passwordActual = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Contains("requeridos", vacios.GetProperty("error").GetString());
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/empleados", Empleado(Roles.Operario, ApiFixture.AdminPassword)).EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateEmployee_RequiresTheCurrentPassword()
    {
        var token = await api.TokenAdminAsync();
        object Empleado(string email, string? passwordActual) => new { rut = api.Rut(), nombre = "A", apellido = "B", telefono = api.Telefono(), email, password = ApiFixture.AdminPassword, rol = Roles.Operario, passwordActual };
        var sinPassword = $"e{api.Siguiente()}@test.local";
        var conErronea = $"e{api.Siguiente()}@test.local";

        await api.Cliente(token).PostAsJsonAsync("/api/v1/auth/empleados", Empleado(sinPassword, null)).EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente(token).PostAsJsonAsync("/api/v1/auth/empleados", Empleado(conErronea, "no-es-mi-password")).EsperarAsync(HttpStatusCode.BadRequest);

        Assert.False(await api.ConsultarAsync(db => db.Empleados.AnyAsync(e => e.Email == sinPassword || e.Email == conErronea)));
    }

    // ── Portal del cliente ──

    [Fact]
    public async Task Client_OnlySeesTheirOwnRequestsAndOrders()
    {
        var yo = await api.RegistrarClienteAsync();
        var otro = await api.RegistrarClienteAsync();
        var mia = (await api.Cliente(yo.Token).PostAsJsonAsync("/api/v1/mis/solicitudes", new { descripcionGeneral = "Mia", idCliente = otro.Id })
            .EsperarAsync(HttpStatusCode.Created)).GetProperty("idSolicitud").GetInt32();
        var ajena = (await api.Cliente(otro.Token).PostAsJsonAsync("/api/v1/mis/solicitudes", new { descripcionGeneral = "Ajena" })
            .EsperarAsync(HttpStatusCode.Created)).GetProperty("idSolicitud").GetInt32();

        await api.AceptarSolicitudAsync(mia);
        await api.AceptarSolicitudAsync(ajena);
        var admin = await api.TokenAdminAsync();
        var idAdmin = await api.IdAdminAsync();
        foreach (var idSolicitud in new[] { mia, ajena })
        {
            await api.Cliente(admin).PostAsJsonAsync("/api/v1/pedidos/", new { idSolicitud, idEmpleadoResponsable = idAdmin, fechaEntregaEstimada = DateTime.Now.AddDays(3) })
                .EsperarAsync(HttpStatusCode.Created);
        }

        var solicitudes = await api.Cliente(yo.Token).GetAsync("/api/v1/mis/solicitudes").EsperarAsync(HttpStatusCode.OK);
        var pedidos = await api.Cliente(yo.Token).GetAsync("/api/v1/mis/pedidos").EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(mia, Assert.Single(solicitudes.EnumerateArray()).GetProperty("idSolicitud").GetInt32());
        Assert.Equal(mia, Assert.Single(pedidos.EnumerateArray()).GetProperty("idSolicitud").GetInt32());
    }

    [Fact]
    public async Task ClientRequest_IsOnline()
    {
        var (id, _, token) = await api.RegistrarClienteAsync();

        var creada = await api.Cliente(token).PostAsJsonAsync("/api/v1/mis/solicitudes", new
        {
            detalles = new[] { new { idProducto = 1, descripcion = "Letrero", cantidad = 1 } }
        }).EsperarAsync(HttpStatusCode.Created);

        Assert.Equal("online", creada.GetProperty("origen").GetString());
        var idSolicitud = creada.GetProperty("idSolicitud").GetInt32();
        var fila = await api.ConsultarAsync(db => db.Solicitudes.AsNoTracking().SingleAsync(s => s.IdSolicitud == idSolicitud));
        Assert.Equal(id, fila.IdCliente);
    }

    [Fact]
    public async Task ClientToken_CannotCreateRequestsForOthers()
    {
        var (_, _, token) = await api.RegistrarClienteAsync();

        await api.Cliente(token).PostAsJsonAsync("/api/v1/solicitudes/", new { idCliente = 1, descripcionGeneral = "x" }).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EmployeeToken_CannotUseClientPortal()
    {
        await api.Cliente(await api.TokenAdminAsync()).GetAsync("/api/v1/mis/pedidos").EsperarAsync(HttpStatusCode.Forbidden);
    }

    // Conocer un telefono no basta para quedarse con la cuenta de ese cliente.
    [Fact]
    public async Task Registration_CannotClaimAnExistingPhone()
    {
        var telefono = api.Telefono();
        await api.Cliente(await api.TokenAdminAsync()).PostAsJsonAsync("/api/v1/clientes/", new { nombre = "Real", apellido = "Cliente", telefono }).EsperarAsync(HttpStatusCode.Created);

        await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", new { rut = api.Rut(), nombre = "Impostor", apellido = "X", telefono, password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Staff_CanActivateWebAccessForExistingClient()
    {
        var telefono = api.Telefono();
        var admin = await api.TokenAdminAsync();
        var creado = await api.Cliente(admin).PostAsJsonAsync("/api/v1/clientes/", new { nombre = "Real", apellido = "Cliente", telefono }).EsperarAsync(HttpStatusCode.Created);
        var id = creado.GetProperty("idCliente").GetInt32();
        Assert.False(creado.GetProperty("registrado").GetBoolean());

        var corta = await api.Cliente(admin).PutAsJsonAsync($"/api/v1/clientes/{id}/credenciales", new { password = "corta", passwordActual = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal(AuthService.MensajePasswordInvalida, corta.GetProperty("error").GetString());

        // Sin la contraseña de quien lo hace no se asignan credenciales.
        await api.Cliente(admin).PutAsJsonAsync($"/api/v1/clientes/{id}/credenciales", new { password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente(admin).PutAsJsonAsync($"/api/v1/clientes/{id}/credenciales", new { password = ApiFixture.AdminPassword, passwordActual = "no-es-mi-password" }).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.False(await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == id).Select(c => c.Registrado).SingleAsync()));

        await api.Cliente(admin).PutAsJsonAsync($"/api/v1/clientes/{id}/credenciales", new { password = ApiFixture.AdminPassword, passwordActual = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.NoContent);

        var token = await api.LoginClienteAsync(telefono, ApiFixture.AdminPassword);
        await api.Cliente(token).GetAsync("/api/v1/mis/solicitudes").EsperarAsync(HttpStatusCode.OK);
        var leido = await api.Cliente(admin).GetAsync($"/api/v1/clientes/{id}").EsperarAsync(HttpStatusCode.OK);
        Assert.True(leido.GetProperty("registrado").GetBoolean());
        Assert.False(leido.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Registration_RejectsMissingDataAndWeakPassword()
    {
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", new { nombre = "A", apellido = "B", telefono = api.Telefono(), password = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", new { rut = api.Rut(), nombre = "A", apellido = "B", telefono = api.Telefono(), password = "corta" })
            .EsperarAsync(HttpStatusCode.BadRequest);
    }

    // ── Primer administrador ──

    [Fact]
    public async Task Bootstrap_CreatedTheFirstAdminFromConfiguration()
    {
        var rol = await api.ConsultarAsync(db => db.Empleados.Where(e => e.Email == ApiFixture.AdminEmail).Select(e => e.Rol).SingleAsync());

        Assert.Equal(Roles.Administrador, rol);
    }

    [Fact]
    public async Task Bootstrap_IsIgnoredWhenEmployeesExist()
    {
        var antes = await api.ConsultarAsync(db => db.Empleados.CountAsync());

        await AdminBootstrapper.CrearPrimerAdministradorAsync(api.Services, ConfiguracionBootstrap(ApiFixture.AdminPassword), NullLogger.Instance);

        Assert.Equal(antes, await api.ConsultarAsync(db => db.Empleados.CountAsync()));
    }

    // El README manda quitar la password tras el primer arranque: la API debe seguir arrancando.
    [Fact]
    public async Task Bootstrap_WithoutPassword_IsIgnoredWhenEmployeesExist()
    {
        var antes = await api.ConsultarAsync(db => db.Empleados.CountAsync());
        var sinPassword = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bootstrap:Admin:Rut"] = api.Rut(),
            ["Bootstrap:Admin:Nombre"] = "Otro",
            ["Bootstrap:Admin:Apellido"] = "Admin",
            ["Bootstrap:Admin:Telefono"] = api.Telefono(),
            ["Bootstrap:Admin:Email"] = "otro.admin@test.local"
        }).Build();

        await AdminBootstrapper.CrearPrimerAdministradorAsync(api.Services, sinPassword, NullLogger.Instance);

        Assert.Equal(antes, await api.ConsultarAsync(db => db.Empleados.CountAsync()));
    }

    [Fact]
    public async Task Bootstrap_FailsLoudlyWithAWeakPassword()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AdminBootstrapper.CrearPrimerAdministradorAsync(api.Services, ConfiguracionBootstrap("corta"), NullLogger.Instance));
    }

    private IConfiguration ConfiguracionBootstrap(string password) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bootstrap:Admin:Rut"] = api.Rut(),
            ["Bootstrap:Admin:Nombre"] = "Otro",
            ["Bootstrap:Admin:Apellido"] = "Admin",
            ["Bootstrap:Admin:Telefono"] = api.Telefono(),
            ["Bootstrap:Admin:Email"] = "otro.admin@test.local",
            ["Bootstrap:Admin:Password"] = password
        }).Build();
}
