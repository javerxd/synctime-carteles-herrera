using Microsoft.Data.SqlClient;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 16B (RNFO15): el pipeline de GitHub Actions corre estas pruebas contra un SQL Server en contenedor, donde NO existe la
// autenticacion de Windows. La cadena de conexion de las pruebas se arma aqui: sin clave en el entorno, igual que siempre
// (Windows, localhost\SQLEXPRESS); con TEST_SQL_PASSWORD, usuario y clave de SQL Server. Estas pruebas no usan base de datos.
public class ConexionDePruebasTests
{
    private static Func<string, string?> Entorno(params (string Variable, string Valor)[] valores)
    {
        var diccionario = valores.ToDictionary(v => v.Variable, v => v.Valor);
        return variable => diccionario.GetValueOrDefault(variable);
    }

    [Fact]
    public void SinVariables_UsaLaInstanciaLocalYLaAutenticacionDeWindows_ComoSiempre()
    {
        var cadena = ConexionDePruebas.DesdeEntorno(Entorno(), "SyncTime_Pruebas_a");

        var partes = new SqlConnectionStringBuilder(cadena);
        Assert.Equal(@"localhost\SQLEXPRESS", partes.DataSource);
        Assert.True(partes.IntegratedSecurity);
        Assert.Equal("SyncTime_Pruebas_a", partes.InitialCatalog);
        Assert.True(partes.TrustServerCertificate);
        Assert.Equal(60, partes.ConnectTimeout);
        Assert.Equal(180, partes.CommandTimeout);
    }

    [Fact]
    public void ConSoloElServidor_SigueConAutenticacionDeWindows()
    {
        var cadena = ConexionDePruebas.DesdeEntorno(Entorno(("TEST_SQL_SERVER", "otro,1433")), "SyncTime_Pruebas_a");

        var partes = new SqlConnectionStringBuilder(cadena);
        Assert.Equal("otro,1433", partes.DataSource);
        Assert.True(partes.IntegratedSecurity);
        Assert.Equal(string.Empty, partes.UserID);
    }

    [Fact]
    public void ConClave_UsaElUsuarioSaYSinAutenticacionDeWindows()
    {
        var cadena = ConexionDePruebas.DesdeEntorno(
            Entorno(("TEST_SQL_SERVER", "localhost,1433"), ("TEST_SQL_PASSWORD", "Clave-De-Prueba-123")), "SyncTime_Pruebas_a");

        var partes = new SqlConnectionStringBuilder(cadena);
        Assert.Equal("sa", partes.UserID);
        Assert.Equal("Clave-De-Prueba-123", partes.Password);
        Assert.False(partes.IntegratedSecurity);
        Assert.Equal("localhost,1433", partes.DataSource);
        Assert.Equal("SyncTime_Pruebas_a", partes.InitialCatalog);
        Assert.True(partes.TrustServerCertificate);
        // El constructor escribe «Integrated Security»; con SQL Server no debe aparecer en verdadero.
        Assert.DoesNotContain("Integrated Security=True", cadena, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ElServidorYElUsuario_SeRecortan_PeroLaClaveNo()
    {
        var cadena = ConexionDePruebas.DesdeEntorno(
            Entorno(("TEST_SQL_SERVER", " localhost,1433\n"), ("TEST_SQL_USER", "sa \n"), ("TEST_SQL_PASSWORD", " con espacios ")), "SyncTime_Pruebas_a");

        var partes = new SqlConnectionStringBuilder(cadena);
        Assert.Equal("localhost,1433", partes.DataSource);
        Assert.Equal("sa", partes.UserID);
        Assert.Equal(" con espacios ", partes.Password);
    }

    [Fact]
    public void ConUsuarioYClave_UsaEseUsuario()
    {
        var cadena = ConexionDePruebas.DesdeEntorno(
            Entorno(("TEST_SQL_USER", "tester"), ("TEST_SQL_PASSWORD", "x1y2z3w4v5")), "SyncTime_Pruebas_a");

        Assert.Equal("tester", new SqlConnectionStringBuilder(cadena).UserID);
    }

    // Una clave con «;», comillas o espacios no puede cortar la cadena ni agregar otras opciones (inyeccion en la cadena de conexion).
    [Theory]
    [InlineData("abc;Database=master;x=1")]
    [InlineData("abc'def")]
    [InlineData("abc\"def")]
    [InlineData("a'b\"c;d=e")]
    [InlineData(" con espacios ")]
    [InlineData("a=b")]
    public void LaClaveConCaracteresEspeciales_LlegaIntactaYSinAgregarOpciones(string clave)
    {
        var cadena = ConexionDePruebas.DesdeEntorno(Entorno(("TEST_SQL_PASSWORD", clave)), "SyncTime_Pruebas_a");

        var partes = new SqlConnectionStringBuilder(cadena);
        Assert.Equal(clave, partes.Password);
        Assert.Equal("SyncTime_Pruebas_a", partes.InitialCatalog);
        Assert.Equal("sa", partes.UserID);
    }

    [Fact]
    public void UnUsuarioSinClave_FallaEnVozAlta_EnVezDeCaerEnWindowsEnSilencio()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ConexionDePruebas.DesdeEntorno(Entorno(("TEST_SQL_USER", "sa")), "SyncTime_Pruebas_a"));

        Assert.Contains("TEST_SQL_PASSWORD", error.Message);
    }

    // El mensaje de error jamas incluye la clave: este texto termina en el registro del pipeline.
    [Fact]
    public void ElMensajeDeError_NoRepiteLaClave()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ConexionDePruebas.DesdeEntorno(Entorno(("TEST_SQL_PASSWORD", "secreta-1234"), ("TEST_SQL_USER", " ")), "SyncTime_Pruebas_a"));

        Assert.DoesNotContain("secreta-1234", error.Message);
    }

    [Fact]
    public void ElNombreDeLaBase_SeLeeIgualConLosDosTiposDeAutenticacion()
    {
        var windows = ConexionDePruebas.DesdeEntorno(Entorno(), "SyncTime_Pruebas_b");
        var sql = ConexionDePruebas.DesdeEntorno(Entorno(("TEST_SQL_PASSWORD", "k3y-0123456789")), "SyncTime_Pruebas_b");

        Assert.Equal("SyncTime_Pruebas_b", Datos.BasesDeDatos.Nombre(windows));
        Assert.Equal("SyncTime_Pruebas_b", Datos.BasesDeDatos.Nombre(sql));
        // Algunas pruebas cambian el nombre de la base reemplazando el texto en la cadena: tiene que seguir siendo posible.
        Assert.Equal("SyncTime_Pruebas_c", Datos.BasesDeDatos.Nombre(sql.Replace("SyncTime_Pruebas_b", "SyncTime_Pruebas_c")));
    }
}
