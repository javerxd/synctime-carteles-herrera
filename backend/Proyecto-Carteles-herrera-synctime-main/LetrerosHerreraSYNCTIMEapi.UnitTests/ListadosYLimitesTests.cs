using LetrerosHerreraSYNCTIMEapi.Datos;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Security;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

public class ParametrosListadoTests
{
    [Fact]
    public void SinParametros_UsaLaPrimeraPaginaYElTamanoPredeterminado()
    {
        var p = new ParametrosListado();

        Assert.Equal(1, p.Pagina);
        Assert.Equal(ParametrosListado.TamanoPredeterminado, p.Tamano);
        Assert.Equal(0, p.Saltar);
        Assert.Null(p.Orden);
        Assert.Null(p.Buscar);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    [InlineData(int.MaxValue, ParametrosListado.PaginaMaxima)]
    public void LaPagina_SeAjustaAUnRangoSeguro(int pedida, int esperada) =>
        Assert.Equal(esperada, new ParametrosListado(pagina: pedida).Pagina);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(50, 50)]
    [InlineData(100000, ParametrosListado.TamanoMaximo)]
    public void ElTamano_NoSuperaElMaximo(int pedido, int esperado) =>
        Assert.Equal(esperado, new ParametrosListado(tamano: pedido).Tamano);

    [Fact]
    public void Saltar_NoDesbordaConLaPaginaMasAlta()
    {
        var p = new ParametrosListado(pagina: int.MaxValue, tamano: int.MaxValue);
        Assert.Equal((ParametrosListado.PaginaMaxima - 1) * ParametrosListado.TamanoMaximo, p.Saltar);
    }

    [Fact]
    public void LosTextos_SeRecortanYLosVaciosNoCuentan()
    {
        var p = new ParametrosListado(orden: "  -fecha ", buscar: "   ");
        Assert.Equal("-fecha", p.Orden);
        Assert.Null(p.Buscar);

        var largo = new ParametrosListado(buscar: new string('a', 500));
        Assert.Equal(ParametrosListado.LargoMaximoBusqueda, largo.Buscar!.Length);
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    public void ElResultado_CalculaElTotalDePaginas(int total, int tamano, int paginas) =>
        Assert.Equal(paginas, new ResultadoPaginado<int>([], total, 1, tamano).TotalPaginas);
}

public class BasesDeDatosTests
{
    private const string Conexion = @"Server=localhost\SQLEXPRESS;Database={0};Trusted_Connection=True;TrustServerCertificate=True;";

    [Theory]
    [InlineData("LetreriaLosHerreraDB_Demo", true)]
    [InlineData("SyncTime_Pruebas_0123abcd", true)]
    [InlineData("LetreriaLosHerreraDB", false)]              // la base real nunca se migra sola
    [InlineData("LetreriaLosHerreraDB_Demo_copia", false)]
    [InlineData("Otra_SyncTime_Pruebas_1", false)]
    [InlineData("master", false)]
    public void SoloLaBaseDemoYLasDePruebas_AdmitenMigracionAutomatica(string nombre, bool esperado)
    {
        Assert.Equal(nombre, BasesDeDatos.Nombre(string.Format(Conexion, nombre)));
        Assert.Equal(esperado, BasesDeDatos.AdmiteMigracionAutomatica(nombre));
    }

    [Fact]
    public void SoloLasBasesDePruebas_SePuedenEliminar()
    {
        Assert.True(BasesDeDatos.EsDePruebas("SyncTime_Pruebas_abc"));
        Assert.False(BasesDeDatos.EsDePruebas("LetreriaLosHerreraDB_Demo"));
        Assert.False(BasesDeDatos.EsDePruebas("LetreriaLosHerreraDB"));
    }
}

public class LimitadorPorCuentaTests
{
    private sealed class Reloj(DateTimeOffset ahora) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = ahora;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private static readonly DateTimeOffset Inicio = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    private const string Ana = "ana@letreros.cl";

    [Fact]
    public void SeBloqueaAlAgotarLosFallosPermitidos()
    {
        var limitador = new LimitadorPorCuenta(limite: 3, new Reloj(Inicio));

        limitador.RegistrarFallo(Ana);
        limitador.RegistrarFallo(Ana);
        Assert.False(limitador.EstaBloqueada(Ana));

        limitador.RegistrarFallo(Ana);
        Assert.True(limitador.EstaBloqueada(Ana));
    }

    [Fact]
    public void ConsultarSiEstaBloqueada_NoGastaIntentos()
    {
        var limitador = new LimitadorPorCuenta(limite: 1, new Reloj(Inicio));

        for (var i = 0; i < 20; i++)
            Assert.False(limitador.EstaBloqueada(Ana));
    }

    [Fact]
    public void UnAcierto_BorraLosFallosAnteriores()
    {
        var limitador = new LimitadorPorCuenta(limite: 2, new Reloj(Inicio));
        limitador.RegistrarFallo(Ana);

        limitador.Olvidar(Ana);
        limitador.RegistrarFallo(Ana);

        Assert.False(limitador.EstaBloqueada(Ana));
    }

    [Fact]
    public void CadaCuentaTieneSuPropioContador()
    {
        var limitador = new LimitadorPorCuenta(limite: 1, new Reloj(Inicio));

        limitador.RegistrarFallo(Ana);

        Assert.True(limitador.EstaBloqueada(Ana));
        Assert.False(limitador.EstaBloqueada("beto@letreros.cl"));
    }

    [Theory]
    [InlineData("Ana@Letreros.CL")]
    [InlineData("  ana@letreros.cl ")]
    [InlineData("ａｎａ@letreros.cl")]   // letras de ancho completo: SQL Server las compara como iguales
    public void LaMismaCuentaEscritaDeOtraForma_ComparteElContador(string variante)
    {
        var limitador = new LimitadorPorCuenta(limite: 1, new Reloj(Inicio));

        limitador.RegistrarFallo(variante);

        Assert.True(limitador.EstaBloqueada(Ana));
        Assert.Equal(1, limitador.CuentasEnMemoria);
    }

    [Fact]
    public void AlPasarElMinuto_ElContadorVuelveAEmpezar()
    {
        var reloj = new Reloj(Inicio);
        var limitador = new LimitadorPorCuenta(limite: 1, reloj);
        limitador.RegistrarFallo(Ana);

        reloj.Ahora = Inicio.AddSeconds(59);
        Assert.True(limitador.EstaBloqueada(Ana));

        reloj.Ahora = Inicio.AddSeconds(61);
        Assert.False(limitador.EstaBloqueada(Ana));
        limitador.RegistrarFallo(Ana);
        Assert.True(limitador.EstaBloqueada(Ana));
    }

    [Fact]
    public void LasCuentasVencidas_SeDescartan()
    {
        var reloj = new Reloj(Inicio);
        var limitador = new LimitadorPorCuenta(limite: 5, reloj, cuentasAntesDeLimpiar: 10);
        for (var i = 0; i < 30; i++)
            limitador.RegistrarFallo($"cuenta{i}@letreros.cl");

        reloj.Ahora = Inicio.AddMinutes(5);
        limitador.RegistrarFallo("otra@letreros.cl");

        Assert.Equal(1, limitador.CuentasEnMemoria);
    }

    [Fact]
    public void HayUnTopeDeCuentasEnMemoria_AunqueTodasEstenVigentes()
    {
        var limitador = new LimitadorPorCuenta(limite: 5, new Reloj(Inicio), cuentasAntesDeLimpiar: 10);

        for (var i = 0; i < 500; i++)
            limitador.RegistrarFallo($"cuenta{i}@letreros.cl");

        Assert.Equal(50, limitador.CuentasEnMemoria);
    }

    [Fact]
    public void LaClaveGuardada_NoCreceConElTextoRecibido()
    {
        var limitador = new LimitadorPorCuenta(limite: 1, new Reloj(Inicio));
        var enorme = new string('a', 1_000_000) + "@letreros.cl";

        limitador.RegistrarFallo(enorme);

        Assert.True(limitador.EstaBloqueada(enorme));
        Assert.False(limitador.EstaBloqueada(Ana));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnaCuentaVacia_NoSeCuentaNiSeBloquea(string? cuenta)
    {
        var limitador = new LimitadorPorCuenta(limite: 1, new Reloj(Inicio));

        limitador.RegistrarFallo(cuenta);
        limitador.RegistrarFallo(cuenta);

        Assert.False(limitador.EstaBloqueada(cuenta));
        Assert.Equal(0, limitador.CuentasEnMemoria);
    }
}
