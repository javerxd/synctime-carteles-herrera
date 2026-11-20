using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 12D: catalogo editable (medidas y opciones de un producto), validacion de nombres y apellidos en la API
// y permisos de la administracion de empleados. Medidas en milimetros.
[Collection(ApiCollection.Name)]
public sealed class Fase12dTests(ApiFixture api)
{
    private async Task<HttpClient> DuenoAsync() => api.Cliente(await api.TokenEmpleadoAsync(Roles.Dueno));

    private static string Version(JsonElement registro) => registro.GetProperty("rowVersion").GetString()!;

    // Un producto nuevo por prueba: el catalogo real (ids 1 a 16) no se toca.
    private async Task<JsonElement> ProductoNuevoAsync(HttpClient gestion) =>
        await gestion.PostAsJsonAsync("/api/v1/productos", new { idCategoria = 1, nombre = $"Producto 12D {api.Siguiente()}", descripcion = "x" })
            .EsperarAsync(HttpStatusCode.Created);

    private static Task<HttpResponseMessage> Editar(HttpClient cliente, int id, string version, object cuerpo) =>
        cliente.SendAsync(ApiFixture.ConVersion(HttpMethod.Put, $"/api/v1/productos/{id}/medidas-y-opciones", version, cuerpo));

    private static object ConRangos(object? opciones = null) => new
    {
        anchoMinMm = 100, anchoMaxMm = 2000, altoMinMm = 100, altoMaxMm = 3000, medidasFijas = Array.Empty<object>(),
        opciones = opciones ?? new[] { new { grupo = "Acabado", valores = new[] { "Mate", "Brillante" } } }
    };

    // ── Catalogo editable ──

    [Fact]
    public async Task Gestion_EditaLosLimitesYLasOpciones_YLaFichaLosDevuelve()
    {
        var gestion = await DuenoAsync();
        var producto = await ProductoNuevoAsync(gestion);
        var id = producto.GetProperty("idProducto").GetInt32();

        var ficha = await Editar(gestion, id, Version(producto), ConRangos()).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(100, ficha.GetProperty("anchoMinMm").GetInt32());
        Assert.Equal(3000, ficha.GetProperty("altoMaxMm").GetInt32());
        Assert.Equal(0, ficha.GetProperty("medidasFijas").GetArrayLength());
        Assert.Equal(["Mate", "Brillante"], ficha.GetProperty("opciones").EnumerateArray().Select(o => o.GetProperty("valor").GetString()));
        Assert.NotEqual(Version(producto), Version(ficha));
    }

    [Fact]
    public async Task UnProductoPasaDeRangosAMedidasFijas_YSeQuitanYAgreganLasMedidas()
    {
        var gestion = await DuenoAsync();
        var producto = await ProductoNuevoAsync(gestion);
        var id = producto.GetProperty("idProducto").GetInt32();
        var conRangos = await Editar(gestion, id, Version(producto), ConRangos()).EsperarAsync(HttpStatusCode.OK);

        var fijas = await Editar(gestion, id, Version(conRangos), new
        {
            anchoMinMm = (int?)null, anchoMaxMm = (int?)null, altoMinMm = (int?)null, altoMaxMm = (int?)null,
            medidasFijas = new[] { new { anchoMm = 800, altoMm = 2000 }, new { anchoMm = 1000, altoMm = 500 } },
            opciones = Array.Empty<object>()
        }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, fijas.GetProperty("anchoMinMm").ValueKind);
        Assert.Equal(2, fijas.GetProperty("medidasFijas").GetArrayLength());
        Assert.Equal(0, fijas.GetProperty("opciones").GetArrayLength());

        var una = await Editar(gestion, id, Version(fijas), new
        {
            medidasFijas = new[] { new { anchoMm = 1000, altoMm = 500 } }, opciones = Array.Empty<object>()
        }).EsperarAsync(HttpStatusCode.OK);
        var restante = una.GetProperty("medidasFijas").EnumerateArray().Single();
        Assert.Equal((1000, 500), (restante.GetProperty("anchoMm").GetInt32(), restante.GetProperty("altoMm").GetInt32()));
    }

    [Fact]
    public async Task CadaCambioDelCatalogo_QuedaEnLaAuditoria_ConQuienLoHizo()
    {
        var gestion = await DuenoAsync();
        var producto = await ProductoNuevoAsync(gestion);
        var id = producto.GetProperty("idProducto").GetInt32();
        var primera = await Editar(gestion, id, Version(producto), ConRangos()).EsperarAsync(HttpStatusCode.OK);

        await Editar(gestion, id, Version(primera), ConRangos(new[] { new { grupo = "Acabado", valores = new[] { "Mate" } } })).EsperarAsync(HttpStatusCode.OK);

        var creadas = await api.AuditoriaAsync("producto_opcion", accion: "crear");
        Assert.True(creadas.Count(a => a.Detalle!.Contains("Brillante")) >= 1);
        var quitadas = await api.ConsultarAsync(db => db.Auditoria.AsNoTracking()
            .Where(a => a.Entidad == "producto_opcion" && a.Accion == "eliminar" && a.Rol == Roles.Dueno).CountAsync());
        Assert.True(quitadas >= 1);
        Assert.NotEmpty(await api.AuditoriaAsync("producto", id.ToString(), "modificar"));
    }

    [Fact]
    public async Task SoloGestion_PuedeEditarElCatalogo()
    {
        var gestion = await DuenoAsync();
        var producto = await ProductoNuevoAsync(gestion);
        var id = producto.GetProperty("idProducto").GetInt32();

        foreach (var rol in new[] { Roles.Disenador, Roles.Operario })
            await Editar(api.Cliente(await api.TokenEmpleadoAsync(rol)), id, Version(producto), ConRangos()).EsperarAsync(HttpStatusCode.Forbidden);
        await Editar(api.Cliente(), id, Version(producto), ConRangos()).EsperarAsync(HttpStatusCode.Unauthorized);
        await Editar(api.Cliente(await api.TokenAdminAsync()), id, Version(producto), ConRangos()).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LosErroresVanPorCampo_Y_NadaSeGuardaSiAlgoFalla()
    {
        var gestion = await DuenoAsync();
        var producto = await ProductoNuevoAsync(gestion);
        var id = producto.GetProperty("idProducto").GetInt32();

        var rechazo = await Editar(gestion, id, Version(producto), new
        {
            anchoMinMm = 500, anchoMaxMm = 400, altoMinMm = 100, altoMaxMm = 50_001,
            medidasFijas = new[] { new { anchoMm = 100, altoMm = 100 } },
            opciones = new[] { new { grupo = "Acabado", valores = new[] { "Mate", "mate" } } }
        }).EsperarAsync(HttpStatusCode.BadRequest);

        var errores = rechazo.GetProperty("errores");
        foreach (var campo in new[] { "anchoMaxMm", "altoMaxMm", "medidasFijas", "opciones" })
            Assert.True(errores.TryGetProperty(campo, out _), campo);
        var intacto = await gestion.GetAsync($"/api/v1/productos/{id}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, intacto.GetProperty("anchoMinMm").ValueKind);
        Assert.Equal(0, intacto.GetProperty("opciones").GetArrayLength());
        Assert.Equal(Version(producto), Version(intacto));
    }

    [Fact]
    public async Task UnaVersionVieja_DaConflicto_YNoPisaElCambioDeOtraPersona()
    {
        var gestion = await DuenoAsync();
        var producto = await ProductoNuevoAsync(gestion);
        var id = producto.GetProperty("idProducto").GetInt32();
        await Editar(gestion, id, Version(producto), ConRangos()).EsperarAsync(HttpStatusCode.OK);

        var conflicto = await Editar(gestion, id, Version(producto), ConRangos(new[] { new { grupo = "Otro", valores = new[] { "X" } } }))
            .EsperarAsync(HttpStatusCode.Conflict);

        Assert.Equal("version_obsoleta", conflicto.GetProperty("codigo").GetString());
        var vigente = await gestion.GetAsync($"/api/v1/productos/{id}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("Acabado", vigente.GetProperty("opciones")[0].GetProperty("grupo").GetString());
    }

    [Fact]
    public async Task ElFormulario_OfreceSoloLoVigente_YLasSolicitudesViejasConservanLoElegido()
    {
        var gestion = await DuenoAsync();
        var producto = await ProductoNuevoAsync(gestion);
        var id = producto.GetProperty("idProducto").GetInt32();
        var ficha = await Editar(gestion, id, Version(producto), ConRangos()).EsperarAsync(HttpStatusCode.OK);
        var cliente = await api.CrearClienteAsync();
        object Cuerpo(string acabado) => new
        {
            idCliente = cliente, descripcionGeneral = "Encargo para probar el catálogo editable",
            detalles = new[] { new { idProducto = id, descripcion = "Prueba", anchoMm = 500, altoMm = 500, cantidad = 1, opciones = new Dictionary<string, string> { ["Acabado"] = acabado } } }
        };
        var antigua = await gestion.PostAsJsonAsync("/api/v1/solicitudes/", Cuerpo("Brillante")).EsperarAsync(HttpStatusCode.Created);

        await Editar(gestion, id, Version(ficha), ConRangos(new[] { new { grupo = "Acabado", valores = new[] { "Mate" } } })).EsperarAsync(HttpStatusCode.OK);

        var rechazo = await gestion.PostAsJsonAsync("/api/v1/solicitudes/", Cuerpo("Brillante")).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Contains("no es una opción válida", rechazo.GetProperty("errores").GetProperty("detalles[0].opciones")[0].GetString());
        var guardado = await api.ConsultarAsync(db => db.SolicitudDetalles.AsNoTracking()
            .Where(d => d.IdSolicitud == antigua.GetProperty("idSolicitud").GetInt32()).Select(d => d.Opciones).SingleAsync());
        Assert.Contains("Brillante", guardado);
    }

    [Fact]
    public async Task ElOrdenDeLosGrupos_SeConserva_YUnCambioSoloDeMayusculasActualizaLaFilaEnElLugar()
    {
        var gestion = await DuenoAsync();
        var producto = await ProductoNuevoAsync(gestion);
        var id = producto.GetProperty("idProducto").GetInt32();
        var primera = await Editar(gestion, id, Version(producto), ConRangos(new[]
        {
            new { grupo = "Material", valores = new[] { "Acrílico", "Aluminio" } }, new { grupo = "Acabado", valores = new[] { "Mate" } }
        })).EsperarAsync(HttpStatusCode.OK);
        var idMate = await api.ConsultarAsync(db => db.Set<LetrerosHerreraSYNCTIMEapi.Models.ProductoOpcion>()
            .Where(o => o.IdProducto == id && o.Valor == "Mate").Select(o => o.IdOpcion).SingleAsync());

        // Se invierte el orden de los grupos y de los valores, y «Mate» pasa a «mate».
        var segunda = await Editar(gestion, id, Version(primera), ConRangos(new[]
        {
            new { grupo = "Acabado", valores = new[] { "mate" } }, new { grupo = "Material", valores = new[] { "Aluminio", "Acrílico" } }
        })).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(["mate", "Aluminio", "Acrílico"], segunda.GetProperty("opciones").EnumerateArray().Select(o => o.GetProperty("valor").GetString()));
        Assert.Equal(["Acabado", "Material", "Material"], segunda.GetProperty("opciones").EnumerateArray().Select(o => o.GetProperty("grupo").GetString()));
        var mismaFila = await api.ConsultarAsync(db => db.Set<LetrerosHerreraSYNCTIMEapi.Models.ProductoOpcion>().AnyAsync(o => o.IdOpcion == idMate && o.Valor == "mate"));
        Assert.True(mismaFila);
    }

    [Fact]
    public async Task UnaEdicionSinCambios_RenuevaLaVersion_PeroNoDejaAuditoriaFalsaDelProducto()
    {
        var gestion = await DuenoAsync();
        var producto = await ProductoNuevoAsync(gestion);
        var id = producto.GetProperty("idProducto").GetInt32();
        var primera = await Editar(gestion, id, Version(producto), ConRangos()).EsperarAsync(HttpStatusCode.OK);
        var antes = (await api.AuditoriaAsync("producto", id.ToString(), "modificar")).Count;

        var segunda = await Editar(gestion, id, Version(primera), ConRangos()).EsperarAsync(HttpStatusCode.OK);

        Assert.NotEqual(Version(primera), Version(segunda));
        Assert.Equal(antes, (await api.AuditoriaAsync("producto", id.ToString(), "modificar")).Count);
    }

    [Fact]
    public async Task UnProductoInexistente_Da404_UnaPeticionSinVersion_Da400_YUnElementoNulo_Da400()
    {
        var gestion = await DuenoAsync();
        await Editar(gestion, 987654, "AAAAAAAAB9E=", ConRangos()).EsperarAsync(HttpStatusCode.NotFound);

        var producto = await ProductoNuevoAsync(gestion);
        var id = producto.GetProperty("idProducto").GetInt32();
        await gestion.PutAsJsonAsync($"/api/v1/productos/{id}/medidas-y-opciones", ConRangos()).EsperarAsync(HttpStatusCode.BadRequest);
        await Editar(gestion, id, Version(producto), new { medidasFijas = new object?[] { null }, opciones = new object?[] { null } }).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ElPrimerProducto_DeLaBandeja_EsElPrimerLetreroDeLaSolicitud()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var cliente = await api.CrearClienteAsync();
        var creada = await admin.PostAsJsonAsync("/api/v1/solicitudes/", new
        {
            idCliente = cliente, descripcionGeneral = "Encargo con dos letreros para la bandeja",
            detalles = new[]
            {
                new { idProducto = 6, descripcion = "X Banner", anchoMm = 800, altoMm = 2000, cantidad = 1 },
                new { idProducto = 7, descripcion = "Demo Stand", anchoMm = 3000, altoMm = 3000, cantidad = 1 }
            }
        }).EsperarAsync(HttpStatusCode.Created);
        var idSolicitud = creada.GetProperty("idSolicitud").GetInt32();

        var lista = await admin.GetAsync($"/api/v1/solicitudes?q={idSolicitud}&pagina=1&tamano=5").EsperarAsync(HttpStatusCode.OK);

        var fila = lista.GetProperty("elementos").EnumerateArray().Single(f => f.GetProperty("idSolicitud").GetInt32() == idSolicitud);
        Assert.Equal("X Banner / banner araña", fila.GetProperty("primerProducto").GetString());
        Assert.Equal(2, fila.GetProperty("cantidadItems").GetInt32());
    }

    // ── Nombres y apellidos ──

    [Theory]
    [InlineData("Ana2", "Rojas", "nombre")]
    [InlineData("Camila", "Pérez-Gil", "apellido")]
    [InlineData("Abcdefghijklmnopqrstu", "Rojas", "nombre")]
    [InlineData(" Camila", "Rojas", "nombre")]
    [InlineData("Ana  María", "Rojas", "nombre")]
    public async Task ElRegistroDelCliente_RechazaNombresFueraDeLaRegla(string nombre, string apellido, string campo)
    {
        var telefono = api.Telefono();
        var cuerpo = new
        {
            rut = (string?)null, nombre, apellido, telefono, correo = ApiFixture.CorreoDe(telefono), password = ApiFixture.AdminPassword,
            aceptaTerminosYPrivacidad = true, aceptaMarketing = false, versionTerminos = "1.0", versionPrivacidad = "1.1", versionMarketing = (string?)null
        };

        var rechazo = await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", cuerpo).EsperarAsync(HttpStatusCode.BadRequest);

        Assert.True(rechazo.GetProperty("errores").TryGetProperty(campo, out _), campo);
    }

    [Fact]
    public async Task ElRegistroDelCliente_AceptaTildesYParticulas()
    {
        var telefono = api.Telefono();
        var cuerpo = new
        {
            rut = (string?)null, nombre = "María José", apellido = "De la Fuente", telefono, correo = ApiFixture.CorreoDe(telefono), password = ApiFixture.AdminPassword,
            aceptaTerminosYPrivacidad = true, aceptaMarketing = false, versionTerminos = "1.0", versionPrivacidad = "1.1", versionMarketing = (string?)null
        };

        await api.Cliente().PostAsJsonAsync("/api/v1/auth/registro-cliente", cuerpo).EsperarAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ElAltaDeEmpleado_RechazaNombresConSimbolosONumeros()
    {
        var gestion = await DuenoAsync();

        var rechazo = await gestion.PostAsJsonAsync("/api/v1/empleados", new
        {
            rut = api.Rut(), nombre = "Emp3", apellido = "O'Higgins", telefono = api.Telefono(), email = $"emp{api.Siguiente()}@test.local",
            password = ApiFixture.AdminPassword, rol = Roles.Operario, passwordActual = ApiFixture.AdminPassword
        }).EsperarAsync(HttpStatusCode.BadRequest);

        var errores = rechazo.GetProperty("errores");
        Assert.True(errores.TryGetProperty("nombre", out _));
        Assert.True(errores.TryGetProperty("apellido", out _));
    }

    [Fact]
    public async Task LaConsultaDeContacto_RechazaUnNombreConNumeros()
    {
        var rechazo = await api.Cliente().PostAsJsonAsync("/api/v1/contacto", new
        {
            nombre = "Pedro 2", correo = "pedro@correo.cl", telefono = (string?)null, asunto = "Cotización", mensaje = "Quiero un letrero para mi local.",
            aceptaPrivacidad = true, sitioWeb = ""
        }).EsperarAsync(HttpStatusCode.BadRequest);

        Assert.True(rechazo.GetProperty("errores").TryGetProperty("nombre", out _));
    }

    // ── Empleados: permisos (decisión del 20-11-2026: gestión = dueño y administrador, sin cambios) ──

    [Fact]
    public async Task LaListaDeEmpleados_LaVenElDuenoYElAdministrador_PeroNoElDisenadorNiElOperario()
    {
        await api.Cliente(await api.TokenEmpleadoAsync(Roles.Dueno)).GetAsync("/api/v1/empleados").EsperarAsync(HttpStatusCode.OK);
        await api.Cliente(await api.TokenAdminAsync()).GetAsync("/api/v1/empleados").EsperarAsync(HttpStatusCode.OK);
        foreach (var rol in new[] { Roles.Disenador, Roles.Operario })
        {
            var cliente = api.Cliente(await api.TokenEmpleadoAsync(rol));
            await cliente.GetAsync("/api/v1/empleados").EsperarAsync(HttpStatusCode.Forbidden);
            await cliente.PostAsJsonAsync("/api/v1/empleados", new { }).EsperarAsync(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task DesactivarUnEmpleado_ConservaSuHistorial_YNoPermiteDesactivarseASiMismo()
    {
        var gestion = await DuenoAsync();
        var (id, _, _) = await api.CrearEmpleadoAsync(Roles.Operario);
        var ficha = await gestion.GetAsync($"/api/v1/empleados/{id}").EsperarAsync(HttpStatusCode.OK);

        var desactivado = await gestion.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/empleados/{id}/activo", Version(ficha), new { activo = false }))
            .EsperarAsync(HttpStatusCode.OK);

        Assert.False(desactivado.GetProperty("activo").GetBoolean());
        Assert.True(await api.ConsultarAsync(db => db.Empleados.AnyAsync(e => e.IdEmpleado == id)));
        var idAdmin = await api.IdAdminAsync();
        var admin = api.Cliente(await api.TokenAdminAsync());
        var yo = await admin.GetAsync($"/api/v1/empleados/{idAdmin}").EsperarAsync(HttpStatusCode.OK);
        var propio = await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/empleados/{idAdmin}/activo", Version(yo), new { activo = false }))
            .EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("propio", propio.GetProperty("codigo").GetString());
    }
}
