using LetrerosHerreraSYNCTIMEapi.Negocio.Mantenedores;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 12D (decisión 6): reglas de los límites de medidas, las medidas fijas y las opciones de un producto,
// que el taller ahora edita desde el mantenedor. Medidas en milímetros.
public class MedidasYOpcionesTests
{
    private static GrupoOpcionesEntrada Grupo(string nombre, params string?[] valores) => new(nombre, valores);

    private static EntradaMedidasYOpciones ConRangos(int anchoMin = 100, int anchoMax = 2000, int altoMin = 100, int altoMax = 3000) =>
        new(anchoMin, anchoMax, altoMin, altoMax, [], [Grupo("Acabado", "Mate", "Brillante")]);

    [Fact]
    public void UnProductoConRangos_YOpciones_EsValido() =>
        Assert.Empty(ValidacionMedidasYOpciones.Validar(ConRangos()));

    [Fact]
    public void UnProductoConMedidasFijas_SinRangos_EsValido() =>
        Assert.Empty(ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [new(800, 2000), new(1000, 500)], [])));

    [Fact]
    public void UnProductoSinMedidasNiOpciones_EsValido() =>
        Assert.Empty(ValidacionMedidasYOpciones.Validar(new(null, null, null, null, null, null)));

    [Fact]
    public void ElMinimoMayorQueElMaximo_SeRechazaEnElCampoDelMaximo()
    {
        var errores = ValidacionMedidasYOpciones.Validar(ConRangos(anchoMin: 500, anchoMax: 400, altoMin: 900, altoMax: 100));

        Assert.Equal(["altoMaxMm", "anchoMaxMm"], errores.Keys.Order());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(50_001)]
    public void LosLimites_DebenSerPositivos_YDentroDelTopeDelCatalogo(int valor)
    {
        var errores = ValidacionMedidasYOpciones.Validar(ConRangos(anchoMin: valor, anchoMax: 50_000));

        Assert.True(errores.ContainsKey("anchoMinMm"));
    }

    [Fact]
    public void ElTopeExacto_EsValido() =>
        Assert.Empty(ValidacionMedidasYOpciones.Validar(ConRangos(anchoMin: 10, anchoMax: 50_000, altoMin: 10, altoMax: 50_000)));

    [Fact]
    public void LosCuatroLimites_VanJuntos_ONingunoSeUsa()
    {
        var errores = ValidacionMedidasYOpciones.Validar(new(100, 2000, null, null, [], []));

        Assert.Equal(["altoMaxMm", "altoMinMm"], errores.Keys.Order());
    }

    [Fact]
    public void UnProducto_NoMezclaMedidasFijasYRangos()
    {
        var errores = ValidacionMedidasYOpciones.Validar(new(100, 2000, 100, 3000, [new(800, 2000)], []));

        Assert.Contains("medidasFijas", errores.Keys);
    }

    [Fact]
    public void LasMedidasFijas_NoSeRepiten_NiSonCeroNiNulas()
    {
        Assert.Contains("medidasFijas", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [new(800, 2000), new(800, 2000)], [])).Keys);
        Assert.Contains("medidasFijas", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [new(0, 2000)], [])).Keys);
        Assert.Contains("medidasFijas", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [new(800, null)], [])).Keys);
        Assert.Contains("medidasFijas", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [new(50_001, 100)], [])).Keys);
    }

    [Fact]
    public void LasMedidasFijas_TienenUnTope() =>
        Assert.Contains("medidasFijas", ValidacionMedidasYOpciones.Validar(new(null, null, null, null,
            Enumerable.Range(1, ValidacionMedidasYOpciones.MaxMedidasFijas + 1).Select(i => new MedidaFijaEntrada(i * 10, 100)).ToList(), [])).Keys);

    [Fact]
    public void UnGrupo_NecesitaAlMenosUnValor()
    {
        Assert.Contains("opciones", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [], [Grupo("Acabado")])).Keys);
        Assert.Contains("opciones", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [], [Grupo("Acabado", "  ", "")])).Keys);
    }

    [Fact]
    public void LosValores_NoSeRepiten_SinImportarMayusculasNiEspaciosAlRededor()
    {
        var errores = ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [], [Grupo("Acabado", "Mate", " mate ")]));

        Assert.Contains("opciones", errores.Keys);
    }

    [Fact]
    public void LosGrupos_NoSeRepiten_NiSinNombre()
    {
        Assert.Contains("opciones", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [], [Grupo("Diseño", "A"), Grupo("diseño", "B")])).Keys);
        Assert.Contains("opciones", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [], [Grupo("", "A")])).Keys);
    }

    [Fact]
    public void LosTextos_TienenElLargoDeLaBase()
    {
        Assert.Contains("opciones", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [],
            [Grupo(new string('g', ValidacionMedidasYOpciones.LargoGrupo + 1), "A")])).Keys);
        Assert.Contains("opciones", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [],
            [Grupo("Acabado", new string('v', ValidacionMedidasYOpciones.LargoValor + 1))])).Keys);
        Assert.Empty(ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [],
            [Grupo(new string('g', ValidacionMedidasYOpciones.LargoGrupo), new string('v', ValidacionMedidasYOpciones.LargoValor))])));
    }

    [Fact]
    public void LaCantidadDeGruposYValores_TieneTope()
    {
        var demasiadosGrupos = Enumerable.Range(1, ValidacionMedidasYOpciones.MaxGrupos + 1).Select(i => Grupo($"G{i}", "A")).ToList();
        Assert.Contains("opciones", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [], demasiadosGrupos)).Keys);

        var demasiadosValores = Grupo("Espesor", Enumerable.Range(1, ValidacionMedidasYOpciones.MaxValoresPorGrupo + 1).Select(i => $"{i} mm").ToArray());
        Assert.Contains("opciones", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [], [demasiadosValores])).Keys);
    }

    [Fact]
    public void UnElementoNulo_EnLasListas_EsUnErrorDeCampo_NoUnaExcepcion()
    {
        Assert.Contains("medidasFijas", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [null!], [])).Keys);
        Assert.Contains("opciones", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [], [null!])).Keys);
        Assert.Contains("opciones", ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [], [new GrupoOpcionesEntrada("Acabado", null)])).Keys);
    }

    [Fact]
    public void ElMensaje_NombraElGrupoConProblema()
    {
        var errores = ValidacionMedidasYOpciones.Validar(new(null, null, null, null, [], [Grupo("Acabado", "Mate"), Grupo("Material")]));

        Assert.Contains("Material", errores["opciones"][0]);
    }
}
