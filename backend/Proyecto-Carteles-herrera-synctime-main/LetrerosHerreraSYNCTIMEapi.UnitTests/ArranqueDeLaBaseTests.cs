using System.Net.Sockets;
using System.Runtime.CompilerServices;
using LetrerosHerreraSYNCTIMEapi.Api;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 16C-1 (H14): Azure SQL gratuita se pausa sola y Render se duerme. La API se conectaba a la base AL ARRANCAR (migraciones pendientes,
// destinatarios de los avisos, primer administrador): con la base en pausa, ese primer contacto fallaba y la API no levantaba, justo cuando
// alguien llegaba a despertarla. Ahora, fuera de desarrollo, esos pasos corren en segundo plano: el servidor abre el puerto de inmediato y los
// pasos reintentan (con espera creciente) hasta que la base despierta o se agota un plazo largo; nunca tumban el proceso por una base dormida.
public sealed class ArranqueDeLaBaseTests
{
    // Un reloj que avanza solo cuando el codigo «espera»: las pruebas no duermen de verdad.
    private sealed class RelojDePrueba : TimeProvider
    {
        private DateTimeOffset ahora = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
        public List<TimeSpan> Esperas { get; } = [];
        public override DateTimeOffset GetUtcNow() => ahora;

        public Task Pausa(TimeSpan espera, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Esperas.Add(espera);
            ahora += espera;
            return Task.CompletedTask;
        }
    }

    private sealed class RegistroDePrueba : ILogger
    {
        public List<(LogLevel Nivel, string Mensaje, Exception? Error)> Entradas { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entradas.Add((logLevel, formatter(state, exception), exception));
    }

    private static Exception BaseDormida() => (SqlException)RuntimeHelpers.GetUninitializedObject(typeof(SqlException));

    private static OpcionesArranqueBase Opciones(int minutos = 30) => new() { MinutosMaximos = minutos, EsperaInicialSegundos = 5, EsperaMaximaSegundos = 60 };

    private static Task<bool> Ejecutar(IReadOnlyList<PasoDeArranque> pasos, RelojDePrueba reloj, RegistroDePrueba registro, OpcionesArranqueBase? opciones = null, CancellationToken ct = default) =>
        ArranqueTolerante.EjecutarAsync(pasos, opciones ?? Opciones(), registro, reloj.Pausa, reloj, ct);

    [Fact]
    public async Task ConLaBaseDespierta_CadaPasoCorreUnaVez_SinEsperar()
    {
        var reloj = new RelojDePrueba();
        var corridos = new List<string>();
        PasoDeArranque[] pasos = [new("migraciones", _ => { corridos.Add("a"); return Task.CompletedTask; }), new("destinatarios", _ => { corridos.Add("b"); return Task.CompletedTask; })];

        Assert.True(await Ejecutar(pasos, reloj, new RegistroDePrueba()));

        Assert.Equal(["a", "b"], corridos);
        Assert.Empty(reloj.Esperas);
    }

    // Fallo a proposito: la base en pausa. El paso reintenta con espera creciente y termina cuando despierta.
    [Fact]
    public async Task ConLaBaseEnPausa_ReintentaConEsperaCreciente_YTerminaCuandoDespierta()
    {
        var reloj = new RelojDePrueba();
        var registro = new RegistroDePrueba();
        var intentos = 0;
        PasoDeArranque[] pasos = [new("migraciones", _ => ++intentos < 4 ? throw BaseDormida() : Task.CompletedTask)];

        Assert.True(await Ejecutar(pasos, reloj, registro));

        Assert.Equal(4, intentos);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)], reloj.Esperas);
        Assert.Equal(3, registro.Entradas.Count(e => e.Nivel == LogLevel.Warning));
        Assert.DoesNotContain(registro.Entradas, e => e.Nivel >= LogLevel.Error);
    }

    [Fact]
    public async Task LaEsperaCreceSoloHastaElMaximo()
    {
        var reloj = new RelojDePrueba();
        var intentos = 0;
        PasoDeArranque[] pasos = [new("migraciones", _ => ++intentos < 8 ? throw BaseDormida() : Task.CompletedTask)];

        Assert.True(await Ejecutar(pasos, reloj, new RegistroDePrueba()));

        Assert.Equal(60, reloj.Esperas.Max(e => e.TotalSeconds));
        Assert.Equal([5, 10, 20, 40, 60, 60, 60], reloj.Esperas.Select(e => (int)e.TotalSeconds));
    }

    // Fallo a proposito: la base no despierta nunca. Tras el plazo se rinde SIN lanzar (la API sigue en pie) y deja un error claro.
    [Fact]
    public async Task SiLaBaseNoDespiertaAntesDelPlazo_SeRinde_SinLanzar_YLoRegistraSinElTextoDelError()
    {
        var reloj = new RelojDePrueba();
        var registro = new RegistroDePrueba();
        var llamadas = 0;
        var segundoPaso = false;
        PasoDeArranque[] pasos =
        [
            new("migraciones", _ => { llamadas++; throw new TimeoutException("Server=secreto.database.windows.net;Password=hunter2"); }),
            new("destinatarios", _ => { segundoPaso = true; return Task.CompletedTask; })
        ];

        var terminado = await Ejecutar(pasos, reloj, registro, Opciones(minutos: 2));

        Assert.False(terminado);
        Assert.True(llamadas > 2);
        Assert.False(segundoPaso);
        Assert.True(reloj.Esperas.Sum(e => e.TotalSeconds) <= 120 + 60);
        var error = Assert.Single(registro.Entradas, e => e.Nivel == LogLevel.Error);
        Assert.Contains("migraciones", error.Mensaje);
        // Ni el texto de la excepcion ni la cadena de conexion llegan al registro: solo el tipo.
        Assert.All(registro.Entradas, e =>
        {
            Assert.DoesNotContain("hunter2", e.Mensaje);
            Assert.DoesNotContain("secreto.database", e.Mensaje);
            Assert.Null(e.Error);
        });
    }

    // Un error que NO es de la base dormida (una configuracion invalida) no se reintenta ni se esconde: sigue tumbando el arranque.
    [Fact]
    public async Task UnErrorQueNoEsDeLaBase_NoSeReintenta_SePropaga()
    {
        var reloj = new RelojDePrueba();
        PasoDeArranque[] pasos = [new("administrador", _ => throw new InvalidOperationException("Bootstrap:Admin:Rol invalido."))];

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Ejecutar(pasos, reloj, new RegistroDePrueba()));

        Assert.Contains("Bootstrap", error.Message);
        Assert.Empty(reloj.Esperas);
    }

    [Fact]
    public async Task AlCerrarseElServidor_LaEsperaSeCancela_YSePropagaLaCancelacion()
    {
        var reloj = new RelojDePrueba();
        using var cierre = new CancellationTokenSource();
        PasoDeArranque[] pasos = [new("migraciones", _ => { cierre.Cancel(); throw BaseDormida(); })];

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Ejecutar(pasos, reloj, new RegistroDePrueba(), ct: cierre.Token));
    }

    [Fact]
    public async Task UnPasoQueYaTermino_NoSeRepiteSiUnoPosteriorReintenta()
    {
        var reloj = new RelojDePrueba();
        var primero = 0;
        var segundo = 0;
        PasoDeArranque[] pasos =
        [
            new("migraciones", _ => { primero++; return Task.CompletedTask; }),
            new("destinatarios", _ => ++segundo < 3 ? throw BaseDormida() : Task.CompletedTask)
        ];

        Assert.True(await Ejecutar(pasos, reloj, new RegistroDePrueba()));

        Assert.Equal(1, primero);
        Assert.Equal(3, segundo);
    }

    public static TheoryData<Exception, bool> Clasificacion => new()
    {
        { BaseDormida(), true },
        { new TimeoutException(), true },
        { new IOException("conexion cerrada"), true },
        { new SocketException(), true },
        { new RetryLimitExceededException("agotado", BaseDormida()), true },
        { new InvalidOperationException("envuelve", BaseDormida()), true },
        { new InvalidOperationException("Falta Bootstrap:Admin:Rut."), false },
        { new ArgumentException("x"), false },
        { new NullReferenceException(), false },
    };

    [Theory]
    [MemberData(nameof(Clasificacion))]
    public void ElErrorDeUnaBaseDormida_SeDistingueDeUnErrorDeConfiguracion(Exception error, bool transitorio)
    {
        Assert.Equal(transitorio, ErroresDeBase.EsTransitorio(error));
    }

    // Un error de configuracion NO es una base dormida: con la clave rotada o mal pegada, esperar 30 minutos lo esconderia.
    [Theory]
    [InlineData(18456, true)]
    [InlineData(18452, true)]
    [InlineData(4060, true)]
    [InlineData(4064, true)]
    [InlineData(40613, false)]   // Azure SQL: la base no esta disponible ahora (pausada)
    [InlineData(40197, false)]
    [InlineData(40501, false)]
    [InlineData(49918, false)]
    [InlineData(-2, false)]      // tiempo de espera agotado
    [InlineData(53, false)]      // no se encontro el servidor
    public void LasCredencialesRechazadasYLaBaseInexistente_SonPermanentes_LaPausaNo(int numero, bool permanente)
    {
        Assert.Equal(permanente, ErroresDeBase.EsFalloPermanente(numero));
    }

    [Fact]
    public void LasOpciones_SeLeenDeLaConfiguracion_ConValoresPorOmision()
    {
        var porOmision = OpcionesArranqueBase.Desde(new ConfigurationBuilder().Build());
        Assert.Equal(30, porOmision.MinutosMaximos);
        Assert.Equal(5, porOmision.EsperaInicialSegundos);
        Assert.Equal(60, porOmision.EsperaMaximaSegundos);

        var propias = OpcionesArranqueBase.Desde(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BaseDeDatos:ArranqueMinutosMaximos"] = "10",
            ["BaseDeDatos:ArranqueEsperaInicialSegundos"] = "2",
            ["BaseDeDatos:ArranqueEsperaMaximaSegundos"] = "30"
        }).Build());
        Assert.Equal((10, 2, 30), (propias.MinutosMaximos, propias.EsperaInicialSegundos, propias.EsperaMaximaSegundos));
    }

    [Theory]
    [InlineData("BaseDeDatos:ArranqueMinutosMaximos", "0")]
    [InlineData("BaseDeDatos:ArranqueMinutosMaximos", "500")]
    [InlineData("BaseDeDatos:ArranqueEsperaInicialSegundos", "0")]
    [InlineData("BaseDeDatos:ArranqueEsperaMaximaSegundos", "1000")]
    public void UnValorFueraDeRango_ImpideArrancar(string clave, string valor)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [clave] = valor }).Build();

        var error = Assert.Throws<InvalidOperationException>(() => OpcionesArranqueBase.Desde(configuracion));

        Assert.Contains(clave, error.Message);
    }

    [Fact]
    public void LaEsperaMaximaNoPuedeSerMenorQueLaInicial()
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BaseDeDatos:ArranqueEsperaInicialSegundos"] = "30",
            ["BaseDeDatos:ArranqueEsperaMaximaSegundos"] = "10"
        }).Build();

        Assert.Throws<InvalidOperationException>(() => OpcionesArranqueBase.Desde(configuracion));
    }
}
