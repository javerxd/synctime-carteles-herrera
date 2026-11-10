using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Lo inactivo u oculto no se muestra ni se puede pedir desde fuera; el personal lo sigue administrando.
[Collection(ApiCollection.Name)]
public sealed class VisibilidadPublicaTests(ApiFixture api)
{
    [Fact]
    public async Task Producto_Inactive_IsHiddenExceptWhenStaffAsksForIt()
    {
        var idCategoria = await CrearCategoriaAsync();
        var id = await CrearProductoAsync(idCategoria, activo: false);

        await VerificarOcultoAsync("/api/v1/productos", "idProducto", id, "incluirInactivos");
    }

    [Fact]
    public async Task Producto_OfInactiveCategory_IsHiddenExceptWhenStaffAsksForIt()
    {
        var idCategoria = await CrearCategoriaAsync(activa: false);
        var id = await CrearProductoAsync(idCategoria);

        await VerificarOcultoAsync("/api/v1/productos", "idProducto", id, "incluirInactivos");
    }

    [Fact]
    public async Task Categoria_Inactive_IsHiddenExceptWhenStaffAsksForIt()
    {
        var id = await CrearCategoriaAsync(activa: false);

        await VerificarOcultoAsync("/api/v1/categorias", "idCategoria", id, "incluirInactivos");
    }

    [Fact]
    public async Task Catalogo_Hidden_IsHiddenExceptWhenStaffAsksForIt()
    {
        var idProducto = await CrearProductoAsync(await CrearCategoriaAsync());
        var id = await CrearCatalogoAsync(idProducto, visible: false);

        await VerificarOcultoAsync("/api/v1/catalogo", "idCatalogo", id, "incluirOcultos");
    }

    [Fact]
    public async Task Catalogo_OfInactiveProduct_IsHiddenExceptWhenStaffAsksForIt()
    {
        var idProducto = await CrearProductoAsync(await CrearCategoriaAsync(), activo: false);
        var id = await CrearCatalogoAsync(idProducto);

        await VerificarOcultoAsync("/api/v1/catalogo", "idCatalogo", id, "incluirOcultos");
    }

    [Fact]
    public async Task ActiveItems_RemainPublic()
    {
        var idCategoria = await CrearCategoriaAsync();
        var idProducto = await CrearProductoAsync(idCategoria);
        var idCatalogo = await CrearCatalogoAsync(idProducto);

        Assert.Contains(await IdsAsync(api.Cliente(), "/api/v1/categorias/", "idCategoria"), id => id == idCategoria);
        Assert.Contains(await IdsAsync(api.Cliente(), "/api/v1/productos/", "idProducto"), id => id == idProducto);
        Assert.Contains(await IdsAsync(api.Cliente(), "/api/v1/catalogo/", "idCatalogo"), id => id == idCatalogo);
        await api.Cliente().GetAsync($"/api/v1/productos/{idProducto}").EsperarAsync(HttpStatusCode.OK);
        await api.Cliente().GetAsync($"/api/v1/catalogo/{idCatalogo}").EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Solicitud_WithInactiveProduct_IsRejectedInEveryFlow()
    {
        var idCategoria = await CrearCategoriaAsync();
        var disponible = await CrearProductoAsync(idCategoria);
        var inactivo = await CrearProductoAsync(idCategoria, activo: false);
        var enCategoriaInactiva = await CrearProductoAsync(await CrearCategoriaAsync(activa: false));

        foreach (var idProducto in new[] { inactivo, enCategoriaInactiva })
        {
            foreach (var rechazo in await EnviarEnCadaFlujoAsync(idProducto, HttpStatusCode.BadRequest))
                Assert.Contains("productos", rechazo.GetProperty("error").GetString());
        }

        // La misma peticion con un producto disponible se acepta: el rechazo se debe solo al producto.
        await EnviarEnCadaFlujoAsync(disponible, HttpStatusCode.Created);
    }

    private async Task<JsonElement[]> EnviarEnCadaFlujoAsync(int idProducto, HttpStatusCode esperado)
    {
        var (_, _, tokenCliente) = await api.RegistrarClienteAsync();
        var idCliente = await api.CrearClienteAsync();
        var detalles = new[] { new { idProducto, descripcion = "d", cantidad = 1 } };

        return
        [
            await api.Cliente(await api.TokenAdminAsync()).PostAsJsonAsync("/api/v1/solicitudes/", new { idCliente, detalles }).EsperarAsync(esperado),
            await api.Cliente(tokenCliente).PostAsJsonAsync("/api/v1/mis/solicitudes", new { detalles }).EsperarAsync(esperado),
            await api.Cliente().PostAsJsonAsync("/api/v1/solicitudes/online", new
            {
                cliente = new { nombre = "Ana", apellido = "Web", telefono = api.Telefono() },
                detalles
            }).EsperarAsync(esperado)
        ];
    }

    // El parametro solo tiene efecto para el personal, y por defecto todos reciben lo mismo.
    private async Task VerificarOcultoAsync(string ruta, string campoId, int id, string parametro)
    {
        var (_, _, tokenCliente) = await api.RegistrarClienteAsync();
        var anonimo = api.Cliente();
        var cliente = api.Cliente(tokenCliente);
        var personal = api.Cliente(await api.TokenEmpleadoAsync("operario"));

        foreach (var sinPermiso in new[] { anonimo, cliente })
        {
            Assert.DoesNotContain(id, await IdsAsync(sinPermiso, $"{ruta}/", campoId));
            Assert.DoesNotContain(id, await IdsAsync(sinPermiso, $"{ruta}/?{parametro}=true", campoId));
            await sinPermiso.GetAsync($"{ruta}/{id}").EsperarAsync(HttpStatusCode.NotFound);
        }

        Assert.DoesNotContain(id, await IdsAsync(personal, $"{ruta}/", campoId));
        Assert.Contains(id, await IdsAsync(personal, $"{ruta}/?{parametro}=true", campoId));
        await personal.GetAsync($"{ruta}/{id}").EsperarAsync(HttpStatusCode.OK);
    }

    private static async Task<List<int>> IdsAsync(HttpClient cliente, string url, string campoId)
    {
        var lista = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);
        return lista.EnumerateArray().Select(e => e.GetProperty(campoId).GetInt32()).ToList();
    }

    private async Task<int> CrearCategoriaAsync(bool activa = true)
    {
        var creada = await api.Cliente(await api.TokenAdminAsync())
            .PostAsJsonAsync("/api/v1/categorias/", new { nombre = $"Categoria {api.Siguiente()}", activo = activa })
            .EsperarAsync(HttpStatusCode.Created);
        return creada.GetProperty("idCategoria").GetInt32();
    }

    private async Task<int> CrearProductoAsync(int idCategoria, bool activo = true)
    {
        var creado = await api.Cliente(await api.TokenAdminAsync())
            .PostAsJsonAsync("/api/v1/productos/", new { idCategoria, nombre = $"Producto {api.Siguiente()}", activo })
            .EsperarAsync(HttpStatusCode.Created);
        return creado.GetProperty("idProducto").GetInt32();
    }

    private async Task<int> CrearCatalogoAsync(int idProducto, bool visible = true)
    {
        JsonElement creado = await api.Cliente(await api.TokenAdminAsync())
            .PostAsJsonAsync("/api/v1/catalogo/", new { idProducto, titulo = $"Trabajo {api.Siguiente()}", descripcion = "d", imagenUrl = "u", visible })
            .EsperarAsync(HttpStatusCode.Created);
        return creado.GetProperty("idCatalogo").GetInt32();
    }
}
