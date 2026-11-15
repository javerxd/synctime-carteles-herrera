using System.Globalization;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Negocio.Rostro;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 10: medida del giro y calibracion, sin base ni HTTP. Los casos del giro viven en CasosGiro.json, que
// el frontend copia tal cual para probar src/rostro/giro.js con los mismos numeros.
public class RostroTests
{
    private const double Tolerancia = 1e-12;

    private static readonly JsonElement Casos = Leer();

    private static JsonElement Leer()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "CasosGiro.json");
        using var documento = JsonDocument.Parse(File.ReadAllText(ruta));
        return documento.RootElement.Clone();
    }

    public static TheoryData<string> NombresDeCasos()
    {
        var datos = new TheoryData<string>();
        foreach (var caso in Casos.GetProperty("casos").EnumerateArray())
            datos.Add(caso.GetProperty("nombre").GetString()!);
        return datos;
    }

    private static double Numero(JsonElement valor) => valor.ValueKind == JsonValueKind.String
        ? double.Parse(valor.GetString()!, CultureInfo.InvariantCulture)
        : valor.GetDouble();

    // Arma los puntos como lo explica la descripcion del archivo.
    private static (double[][] Puntos, double Ancho, double Alto) Armar(JsonElement caso)
    {
        var ancho = caso.GetProperty("ancho").GetDouble();
        var alto = caso.GetProperty("alto").GetDouble();
        var cantidad = caso.TryGetProperty("cantidad", out var c) ? c.GetInt32() : Giro.CantidadPuntos;
        var puntos = Enumerable.Range(0, cantidad).Select(_ => new[] { ancho / 2, alto / 2 }).ToArray();
        puntos[Giro.MandibulaInicio][0] = caso.GetProperty("x0").GetDouble();
        puntos[Giro.MandibulaFin][0] = caso.GetProperty("x16").GetDouble();
        puntos[Giro.PuntaNariz][0] = caso.GetProperty("x30").GetDouble();
        if (caso.TryGetProperty("cambios", out var cambios))
        {
            foreach (var cambio in cambios.EnumerateArray())
                puntos[cambio.GetProperty("i").GetInt32()] = [Numero(cambio.GetProperty("x")), Numero(cambio.GetProperty("y"))];
        }
        return (puntos, ancho, alto);
    }

    [Theory]
    [MemberData(nameof(NombresDeCasos))]
    public void ElIndiceDeGiro_DaLoMismoQueLosCasosCompartidos(string nombre)
    {
        var caso = Casos.GetProperty("casos").EnumerateArray().Single(c => c.GetProperty("nombre").GetString() == nombre);
        var (puntos, ancho, alto) = Armar(caso);

        var g = Giro.Indice(puntos, ancho, alto);

        var esperado = caso.GetProperty("g");
        if (esperado.ValueKind == JsonValueKind.Null)
            Assert.Null(g);
        else
        {
            Assert.NotNull(g);
            Assert.InRange(g!.Value, esperado.GetDouble() - Tolerancia, esperado.GetDouble() + Tolerancia);
        }
    }

    [Fact]
    public void LosCasosCompartidos_CubrenLoValidoYLoInvalido()
    {
        var casos = Casos.GetProperty("casos").EnumerateArray().ToList();
        Assert.True(casos.Count >= 20);
        Assert.Contains(casos, c => c.GetProperty("g").ValueKind == JsonValueKind.Null);
        Assert.Contains(casos, c => c.GetProperty("g").ValueKind == JsonValueKind.Number && c.GetProperty("g").GetDouble() > 0);
        Assert.Contains(casos, c => c.GetProperty("g").ValueKind == JsonValueKind.Number && c.GetProperty("g").GetDouble() < 0);
    }

    [Fact]
    public void SinPuntosOConUnPuntoIncompleto_NoHayIndice()
    {
        Assert.Null(Giro.Indice(null, 640, 480));
        var puntos = Enumerable.Range(0, Giro.CantidadPuntos).Select(_ => new[] { 320d, 240d }).ToArray<double[]?>();
        puntos[0] = [200d];
        Assert.Null(Giro.Indice(puntos, 640, 480));
        puntos[0] = null;
        Assert.Null(Giro.Indice(puntos, 640, 480));
        Assert.Null(Giro.Indice(Enumerable.Range(0, Giro.CantidadPuntos).Select(_ => new[] { 320d, 240d }).ToArray(), double.NaN, 480));
        Assert.Null(Giro.Indice(Enumerable.Range(0, Giro.CantidadPuntos).Select(_ => new[] { 320d, 240d }).ToArray(), 20_000, 480));
    }

    [Fact]
    public void LosRangosCompartidos_ClasificanCadaGiro()
    {
        var porDefecto = Casos.GetProperty("rangosPorDefecto");
        var rangos = new RangosGiro(porDefecto.GetProperty("frenteMaximo").GetDouble(), porDefecto.GetProperty("giroMinimo").GetDouble(),
            porDefecto.GetProperty("giroMaximo").GetDouble());
        Assert.Equal(RangosGiro.PorDefecto, rangos);

        foreach (var caso in Casos.GetProperty("rangos").EnumerateArray())
        {
            var g = caso.GetProperty("g").GetDouble();
            Assert.True(caso.GetProperty("frente").GetBoolean() == rangos.EsFrente(g), $"frente con g = {g}");
            Assert.True(caso.GetProperty("izquierda").GetBoolean() == rangos.EsGiro(LadoGiro.Izquierda, g), $"izquierda con g = {g}");
            Assert.True(caso.GetProperty("derecha").GetBoolean() == rangos.EsGiro(LadoGiro.Derecha, g), $"derecha con g = {g}");
        }
    }

    [Theory]
    [InlineData(null, 0.2, ProblemaGiro.NoEstaDeFrente)]
    [InlineData(LadoGiro.Izquierda, -0.5, ProblemaGiro.LadoContrario)]
    [InlineData(LadoGiro.Izquierda, 0, ProblemaGiro.LadoContrario)]
    [InlineData(LadoGiro.Izquierda, 0.2, ProblemaGiro.GiroInsuficiente)]
    [InlineData(LadoGiro.Izquierda, 0.9, ProblemaGiro.GiroExcesivo)]
    [InlineData(LadoGiro.Derecha, 0.5, ProblemaGiro.LadoContrario)]
    [InlineData(LadoGiro.Derecha, -0.2, ProblemaGiro.GiroInsuficiente)]
    [InlineData(LadoGiro.Derecha, -0.9, ProblemaGiro.GiroExcesivo)]
    [InlineData(LadoGiro.Derecha, -0.5, ProblemaGiro.Ninguno)]
    public void ElProblemaDeCadaCaptura_DiceComoRepetirla(LadoGiro? lado, double g, ProblemaGiro esperado) =>
        Assert.Equal(esperado, RangosGiro.PorDefecto.Revisar(lado, g));

    [Theory]
    [InlineData(0.15, 0.30, 0.75, true)]
    [InlineData(0.30, 0.30, 0.75, false)]
    [InlineData(0.15, 0.80, 0.75, false)]
    [InlineData(0.15, 0.30, 1.2, false)]
    [InlineData(0, 0.30, 0.75, false)]
    [InlineData(double.NaN, 0.30, 0.75, false)]
    public void LosRangos_DebenTenerSentido(double frente, double minimo, double maximo, bool validos) =>
        Assert.Equal(validos, new RangosGiro(frente, minimo, maximo).SonValidos);

    [Fact]
    public void ElOrdenDeLosGiros_SeTraduceASusLados()
    {
        Assert.Equal((LadoGiro.Izquierda, LadoGiro.Derecha), OrdenGiros.Lados(OrdenGiros.IzquierdaDerecha));
        Assert.Equal((LadoGiro.Derecha, LadoGiro.Izquierda), OrdenGiros.Lados(OrdenGiros.DerechaIzquierda));
        Assert.Null(OrdenGiros.Lados("cualquiera"));
        Assert.Equal(["izquierda", "derecha"], OrdenGiros.ParaElNavegador(OrdenGiros.IzquierdaDerecha));
        Assert.Equal(["derecha", "izquierda"], OrdenGiros.ParaElNavegador(OrdenGiros.DerechaIzquierda));
    }

    // ── Calibracion ──

    [Fact]
    public void SeriesSeparadas_SugierenElPuntoMedio_ConMargenPositivo()
    {
        var resumen = Calibracion.Resumir([0.30, 0.35, 0.40], [0.60, 0.70]);

        Assert.Equal(new SerieCalibracion(3, 0.30, 0.40, 0.35), resumen.Propio);
        Assert.Equal(new SerieCalibracion(2, 0.60, 0.70, 0.65), resumen.Otra);
        Assert.Equal(0.50, resumen.UmbralSugerido);
        Assert.Equal(0.10, resumen.Margen);
    }

    [Fact]
    public void SeriesSolapadas_SugierenElUmbralConMenosErrores_ConMargenNegativo()
    {
        double[] propias = [0.30, 0.40, 0.55];
        double[] ajenas = [0.50, 0.70, 0.80];

        var resumen = Calibracion.Resumir(propias, ajenas);

        // Con 0.40 se rechaza un propio (0.55) y no entra ningun ajeno; con 0.55 entra un ajeno (0.50).
        // Los dos cometen un error: se prefiere el menor, que no acepta a nadie ajeno.
        Assert.Equal(0.40, resumen.UmbralSugerido);
        Assert.Equal(1, Calibracion.Errores(propias, ajenas, 0.40));
        Assert.Equal(-0.025, resumen.Margen);
    }

    [Fact]
    public void SeriesQueSeTocan_NoCuentanComoSeparadas()
    {
        // 0.5 es a la vez el mayor propio y el menor ajeno: no hay hueco. Con 0.3 o con 0.5 hay un error; se
        // sugiere 0.3, que no deja entrar a nadie ajeno, y el margen es cero.
        var resumen = Calibracion.Resumir([0.2, 0.3, 0.5], [0.5, 0.9, 1.0, 1.1]);

        Assert.Equal(0.3, resumen.UmbralSugerido);
        Assert.Equal(0, resumen.Margen);
    }

    [Fact]
    public void SinUnaDeLasSeries_NoHayUmbralSugerido()
    {
        var resumen = Calibracion.Resumir([0.3], []);

        Assert.Equal(1, resumen.Propio.N);
        Assert.Equal(SerieCalibracion.Vacia, resumen.Otra);
        Assert.Null(resumen.UmbralSugerido);
        Assert.Null(resumen.Margen);
    }

    [Fact]
    public void LaPeorDistancia_EsLaMayorDeLasTres() =>
        Assert.Equal(0.6, Calibracion.PeorDistancia(0.2, 0.6, 0.4));
}
