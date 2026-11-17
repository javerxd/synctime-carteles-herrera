using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 12B: el catalogo real del taller llega por migracion, y sus medidas y opciones se validan en la API.
[Collection(ApiCollection.Name)]
public sealed class CatalogoRealApiTests(ApiFixture api)
{
    // Devuelve la tarea de la peticion (no la respuesta) para poder encadenar EsperarAsync.
    private async Task<JsonElement> CrearAsync(object[] detalles, HttpStatusCode esperado)
    {
        var cliente = await api.CrearClienteAsync();
        return await api.Cliente(await api.TokenAdminAsync()).PostAsJsonAsync("/api/v1/solicitudes/",
            new { idCliente = cliente, descripcionGeneral = "Encargo para probar el catálogo real", detalles }).EsperarAsync(esperado);
    }

    private static object Detalle(int idProducto, int? ancho, int? alto, Dictionary<string, string>? opciones = null) =>
        new { idProducto, descripcion = "Prueba", anchoMm = ancho, altoMm = alto, cantidad = 1, opciones };

    [Fact]
    public async Task LaMigracion_DejaElCatalogoReal_Y_NingunProductoDePrueba()
    {
        var (categorias, productos, viejos) = await api.ConsultarAsync(async db => (
            await db.Categorias.AsNoTracking().Where(c => c.IdCategoria <= 4).OrderBy(c => c.IdCategoria).Select(c => c.Nombre).ToListAsync(),
            await db.Productos.AsNoTracking().Where(p => p.IdProducto <= 16).OrderBy(p => p.IdProducto).Select(p => p.Nombre).ToListAsync(),
            await db.Productos.CountAsync(p => new[] { "Cartel metálico impreso", "Lienzo publicitario", "Letrero tipo Palomita", "Trabajo personalizado", "Pendón", "Señalética" }.Contains(p.Nombre))
            + await db.Categorias.CountAsync(c => new[] { "Letreros", "Vehículos", "Muros", "Personalizado / Otro" }.Contains(c.Nombre))));

        Assert.Equal(CatalogoDelTaller.Categorias.Select(c => c.Nombre), categorias);
        Assert.Equal(CatalogoDelTaller.Productos.Select(p => p.Nombre), productos);
        Assert.Equal(0, viejos);
    }

    [Fact]
    public async Task LaBase_TieneLasMedidasFijasYLasOpcionesDelCatalogo()
    {
        var (fijas, opciones, xBanner) = await api.ConsultarAsync(async db => (
            await db.Set<ProductoMedidaFija>().CountAsync(m => m.IdProducto <= 16),
            await db.Set<ProductoOpcion>().CountAsync(o => o.IdProducto <= 16),
            await db.Productos.AsNoTracking().Include(p => p.MedidasFijas).SingleAsync(p => p.IdProducto == 6)));

        Assert.Equal(CatalogoDelTaller.MedidasFijas().Count(), fijas);
        Assert.Equal(CatalogoDelTaller.Opciones().Count(), opciones);
        Assert.Equal([(800, 2000)], xBanner.MedidasFijas.Select(m => (m.AnchoMm, m.AltoMm)));
    }

    [Fact]
    public async Task LaLista_DeProductos_TraeLimitesMedidasYOpciones_ParaElFormulario()
    {
        var respuesta = await api.Cliente().GetAsync("/api/v1/productos?tamano=100").EsperarAsync(HttpStatusCode.OK);
        var elementos = respuesta.ValueKind == JsonValueKind.Array ? respuesta : respuesta.GetProperty("elementos");
        var rollUp = elementos.EnumerateArray().Single(p => p.GetProperty("idProducto").GetInt32() == 5);
        var caballete = elementos.EnumerateArray().Single(p => p.GetProperty("idProducto").GetInt32() == 12);
        var banners = elementos.EnumerateArray().Single(p => p.GetProperty("idProducto").GetInt32() == 2);

        Assert.Equal(800, rollUp.GetProperty("anchoMinMm").GetInt32());
        Assert.Equal(3000, rollUp.GetProperty("altoMaxMm").GetInt32());
        Assert.Equal(3, caballete.GetProperty("medidasFijas").GetArrayLength());
        Assert.Contains(banners.GetProperty("opciones").EnumerateArray(),
            o => o.GetProperty("grupo").GetString() == "Material" && o.GetProperty("valor").GetString() == "Tela PVC de 13 onzas");
    }

    [Fact]
    public async Task UnaMedidaFueraDelRango_SeRechazaPorCampo_ConElLimiteEnCentimetros()
    {
        var rechazo = await CrearAsync([Detalle(5, 700, 2500)], HttpStatusCode.BadRequest);

        var errores = rechazo.GetProperty("errores");
        Assert.Contains("80 y 150 cm", errores.GetProperty("detalles[0].anchoMm")[0].GetString());
        Assert.False(errores.TryGetProperty("detalles[0].altoMm", out _));
    }

    [Fact]
    public async Task UnProductoDeMedidaUnica_SoloAceptaEsaMedida()
    {
        await CrearAsync([Detalle(6, 800, 2000)], HttpStatusCode.Created);

        var rechazo = await CrearAsync([Detalle(6, 900, 2000)], HttpStatusCode.BadRequest);

        Assert.Contains("80 x 200 cm", rechazo.GetProperty("errores").GetProperty("detalles[0].anchoMm")[0].GetString());
    }

    [Fact]
    public async Task LasOpcionesElegidas_SeGuardan_Y_VuelvenEnElComprobante()
    {
        var creada = await CrearAsync([Detalle(2, 3000, 1000, new Dictionary<string, string> { ["Material"] = "Tela PVC de 13 onzas", ["Terminaciones"] = "Solo tela impresa" })],
            HttpStatusCode.Created);

        var opciones = creada.GetProperty("detalles")[0].GetProperty("opciones");
        Assert.Equal("Tela PVC de 13 onzas", opciones.GetProperty("Material").GetString());
        Assert.Equal("Solo tela impresa", opciones.GetProperty("Terminaciones").GetString());
        var guardado = await api.ConsultarAsync(db => db.SolicitudDetalles.AsNoTracking()
            .Where(d => d.IdSolicitud == creada.GetProperty("idSolicitud").GetInt32()).Select(d => d.Opciones).SingleAsync());
        Assert.Equal("{\"Material\":\"Tela PVC de 13 onzas\",\"Terminaciones\":\"Solo tela impresa\"}", guardado);
    }

    [Fact]
    public async Task UnaOpcionQueElProductoNoTiene_OConUnValorAjeno_SeRechaza()
    {
        var ajena = await CrearAsync([Detalle(2, 3000, 1000, new Dictionary<string, string> { ["Bastidor"] = "Madera" })], HttpStatusCode.BadRequest);
        var valor = await CrearAsync([Detalle(2, 3000, 1000, new Dictionary<string, string> { ["Material"] = "Acrílico" })], HttpStatusCode.BadRequest);

        Assert.Contains("no es una opción", ajena.GetProperty("errores").GetProperty("detalles[0].opciones")[0].GetString());
        Assert.Contains("no es una opción válida", valor.GetProperty("errores").GetProperty("detalles[0].opciones")[0].GetString());
    }

    [Fact]
    public async Task SinMedidasNiOpciones_ElLetreroSeAceptaComoAntes()
    {
        await CrearAsync([Detalle(11, null, null)], HttpStatusCode.Created);
        await CrearAsync([Detalle(16, null, null, new Dictionary<string, string> { ["Producto"] = "Tazas", ["Técnica de estampado"] = "x" })], HttpStatusCode.BadRequest);
    }
}
