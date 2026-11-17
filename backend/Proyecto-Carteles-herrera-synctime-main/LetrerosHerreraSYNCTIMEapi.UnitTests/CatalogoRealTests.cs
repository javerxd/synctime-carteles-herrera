using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 12B: el catalogo real del taller y sus reglas de medidas y opciones, sin base de datos.
public class CatalogoRealTests
{
    private static LimitesProducto Limites(int id)
    {
        var p = CatalogoDelTaller.Productos.Single(x => x.Id == id);
        var opciones = (p.Opciones ?? []).ToDictionary(o => o.Grupo, o => (IReadOnlyList<string>)o.Valores);
        return new LimitesProducto(p.Nombre, p.AnchoMin, p.AnchoMax, p.AltoMin, p.AltoMax, p.Fijas ?? [], opciones);
    }

    [Fact]
    public void ElCatalogoReal_TieneCuatroCategoriasYDieciseisProductos_SinNombresRepetidos()
    {
        Assert.Equal(4, CatalogoDelTaller.Categorias.Length);
        Assert.Equal(16, CatalogoDelTaller.Productos.Length);
        Assert.Equal(16, CatalogoDelTaller.Productos.Select(p => p.Nombre).Distinct().Count());
        Assert.All(CatalogoDelTaller.Productos, p => Assert.Contains(CatalogoDelTaller.Categorias, c => c.Id == p.IdCategoria));
        // Cada categoria tiene al menos un producto.
        Assert.All(CatalogoDelTaller.Categorias, c => Assert.Contains(CatalogoDelTaller.Productos, p => p.IdCategoria == c.Id));
    }

    [Fact]
    public void LosLimitesDeCadaProducto_SonCoherentes()
    {
        foreach (var p in CatalogoDelTaller.Productos)
        {
            var rangos = new[] { p.AnchoMin, p.AnchoMax, p.AltoMin, p.AltoMax };
            // O los cuatro limites o ninguno (igual que ck_productos_medidas).
            Assert.True(rangos.All(r => r is null) || rangos.All(r => r is > 0), p.Nombre);
            if (p.AnchoMin is int min)
                Assert.True(p.AnchoMax >= min && p.AltoMax >= p.AltoMin, p.Nombre);
            // Una medida fija no convive con rangos.
            if (p.Fijas is { Length: > 0 })
                Assert.Null(p.AnchoMin);
        }
    }

    [Theory]
    [InlineData(6, 800, 2000, true)]          // X Banner: unica medida
    [InlineData(6, 800, 2100, false)]
    [InlineData(6, 2000, 800, false)]         // girada tampoco
    [InlineData(7, 3000, 3000, true)]         // Demo Stand
    [InlineData(14, 90, 55, true)]            // tarjeta de presentacion
    [InlineData(14, 91, 55, false)]
    [InlineData(12, 1200, 600, true)]         // caballete: una de tres
    [InlineData(12, 1100, 600, false)]
    [InlineData(15, 210, 297, true)]          // flyer A4
    public void UnProductoDeMedidasFijas_AceptaSoloEsasCombinaciones(int idProducto, int ancho, int alto, bool valida)
    {
        var errores = ReglasCatalogo.ValidarMedidas(ancho, alto, Limites(idProducto));

        Assert.Equal(valida, errores.Count == 0);
        if (!valida)
            Assert.Contains("cm", errores["anchoMm"][0]);
    }

    [Theory]
    [InlineData(5, 800, 2000, true)]          // Roll Up: justo en los minimos
    [InlineData(5, 1500, 3000, true)]         // y en los maximos
    [InlineData(5, 799, 2000, false)]
    [InlineData(5, 800, 3001, false)]
    [InlineData(9, 10, 2000, true)]           // Letrero luminoso: 1 a 200 cm
    [InlineData(9, 2001, 500, false)]
    [InlineData(3, 300, 2000, true)]          // Postera: 30 a 200 cm
    [InlineData(3, 299, 300, false)]
    [InlineData(1, 50_000, 50_000, true)]     // Gigantografia: hasta 5000 cm
    public void UnProductoConRangos_AceptaSoloLoQueEstaDentro(int idProducto, int ancho, int alto, bool valida)
    {
        Assert.Equal(valida, ReglasCatalogo.ValidarMedidas(ancho, alto, Limites(idProducto)).Count == 0);
    }

    [Fact]
    public void ElErrorDeUnRango_DiceCualEsElQueFalla_EnCentimetros()
    {
        var errores = ReglasCatalogo.ValidarMedidas(799, 3001, Limites(5));

        Assert.Contains("80 y 150 cm", errores["anchoMm"][0]);
        Assert.Contains("200 y 300 cm", errores["altoMm"][0]);
    }

    [Theory]
    [InlineData(11)]   // senalizaciones: tamano predeterminado
    [InlineData(16)]   // estampados: personalizable
    public void UnProductoSinMedidasQueValidar_AceptaCualquiera(int idProducto)
    {
        Assert.Empty(ReglasCatalogo.ValidarMedidas(1, 49_999, Limites(idProducto)));
    }

    [Fact]
    public void ElegirTodasLasOpcionesDeCualquierProducto_CabeEnElLimiteGuardado()
    {
        foreach (var p in CatalogoDelTaller.Productos)
        {
            // Lo peor: en cada grupo, el valor mas largo.
            var todas = (p.Opciones ?? []).ToDictionary(o => o.Grupo, o => o.Valores.OrderByDescending(v => v.Length).First());
            Assert.Null(ReglasCatalogo.ValidarOpciones(todas, Limites(p.Id)));
            Assert.True((ReglasCatalogo.Serializar(todas) ?? "").Length <= ReglasCatalogo.MaxLargoOpciones, p.Nombre);
        }
    }

    [Fact]
    public void UnaOpcionAjena_NoRepiteTodoLoQueEnvioElCliente()
    {
        var mensaje = ReglasCatalogo.ValidarOpciones(new Dictionary<string, string> { [new string('g', 5000)] = "x" }, Limites(2));

        Assert.True(mensaje!.Length < 200);
    }

    [Fact]
    public void SinMedidas_NoHayNadaQueValidar()
    {
        Assert.Empty(ReglasCatalogo.ValidarMedidas(null, null, Limites(6)));
    }

    [Fact]
    public void LasOpciones_DebenSerLasDelProducto()
    {
        var banner = Limites(2);

        Assert.Null(ReglasCatalogo.ValidarOpciones(new Dictionary<string, string> { ["Material"] = "Tela PVC de 13 onzas", ["Terminaciones"] = "Solo tela impresa" }, banner));
        Assert.Null(ReglasCatalogo.ValidarOpciones(null, banner));
        Assert.Null(ReglasCatalogo.ValidarOpciones(new Dictionary<string, string>(), banner));
        Assert.Contains("no es una opción válida", ReglasCatalogo.ValidarOpciones(new Dictionary<string, string> { ["Material"] = "Madera" }, banner));
        Assert.Contains("no es una opción de", ReglasCatalogo.ValidarOpciones(new Dictionary<string, string> { ["Bastidor"] = "Madera" }, banner));
        // El mismo valor en el grupo equivocado tampoco sirve.
        Assert.NotNull(ReglasCatalogo.ValidarOpciones(new Dictionary<string, string> { ["Terminaciones"] = "Tela PVC de 13 onzas" }, banner));
    }

    [Fact]
    public void LasOpcionesDeLetrasCorporeas_IncluyenElEspesorDe2a10Milimetros()
    {
        var letras = CatalogoDelTaller.Productos.Single(p => p.Nombre == "Letras corpóreas");

        var espesores = letras.Opciones!.Single(o => o.Grupo == "Espesor").Valores;

        Assert.Equal(Enumerable.Range(2, 9).Select(n => $"{n} mm"), espesores);
    }

    [Fact]
    public void LasOpcionesGuardadas_VanYVienenComoJsonOrdenado_YUnValorRotoEsSinOpciones()
    {
        var elegidas = new Dictionary<string, string> { ["Terminaciones"] = "Ojetillos", ["Diseño"] = "Del cliente" };

        var json = ReglasCatalogo.Serializar(elegidas);

        Assert.Equal("{\"Diseño\":\"Del cliente\",\"Terminaciones\":\"Ojetillos\"}", json);
        Assert.Equal(elegidas, ReglasCatalogo.Leer(json));
        Assert.Null(ReglasCatalogo.Serializar(null));
        Assert.Null(ReglasCatalogo.Leer("no es json"));
        Assert.Null(ReglasCatalogo.Leer(null));
        Assert.Null(ReglasCatalogo.Leer("{}"));
    }

    [Fact]
    public void LasOpcionesMuyLargas_Rechazan()
    {
        var largas = Enumerable.Range(0, 30).ToDictionary(i => $"Grupo{i}", _ => new string('x', 30));
        var limites = new LimitesProducto("P", null, null, null, null, [], largas.ToDictionary(g => g.Key, g => (IReadOnlyList<string>)new[] { g.Value }));

        Assert.Contains("demasiado largas", ReglasCatalogo.ValidarOpciones(largas, limites));
    }
}
