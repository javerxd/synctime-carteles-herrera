using LetrerosHerreraSYNCTIMEapi.Negocio;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

public class RutTests
{
    [Theory]
    [InlineData("12.345.678-5", "12345678-5")]
    [InlineData("12345678-5", "12345678-5")]
    [InlineData("123456785", "12345678-5")]
    [InlineData("12.345.6785", "12345678-5")]
    [InlineData("11.111.111-1", "11111111-1")]
    [InlineData("6-k", "6-K")]
    [InlineData("  1-9  ", "1-9")]
    public void UnRutConDigitoVerificadorCorrecto_SeAceptaYSeNormaliza(string rut, string esperado)
    {
        Assert.True(Rut.TryNormalizar(rut, out var normalizado));
        Assert.Equal(esperado, normalizado);
    }

    [Theory]
    [InlineData("12.345.678-9")]   // digito verificador equivocado
    [InlineData("12345678-K")]
    [InlineData("0-0")]            // el cuerpo no puede empezar con cero
    [InlineData("1234567890-1")]   // demasiado largo
    [InlineData("12a45678-5")]
    [InlineData("1-2-3.4-5")]      // puntos y guiones fuera de lugar
    [InlineData("12..345.678-5")]
    [InlineData("-5")]
    [InlineData("5")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UnRutIncorrecto_SeRechaza(string? rut) => Assert.False(Rut.EsValido(rut));

    [Theory]
    [InlineData("12345678", '5')]
    [InlineData("6", 'K')]
    [InlineData("1", '9')]
    [InlineData("11111111", '1')]
    public void ElDigitoVerificador_SeCalculaConModulo11(string cuerpo, char esperado) =>
        Assert.Equal(esperado, Rut.DigitoVerificador(cuerpo));
}

public class ReglasTests
{
    [Theory]
    [InlineData("0", true)]
    [InlineData("45000", true)]
    [InlineData("45000.00", true)]
    [InlineData("45000.50", false)]   // los pesos chilenos no llevan decimales
    [InlineData("-1", false)]
    public void UnMontoEnPesos_EsEnteroYNoNegativo(string monto, bool esperado) =>
        Assert.Equal(esperado, Reglas.EsMontoEnPesos(decimal.Parse(monto, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("912345678", true)]
    [InlineData("+56912345678", true)]
    [InlineData("+56 9 1234 5678", true)]
    [InlineData("432123456", true)]
    [InlineData("12345678", false)]     // faltan digitos
    [InlineData("9123456789", false)]   // sobran digitos
    [InlineData("91234567a", false)]
    [InlineData("+57912345678", false)] // otro pais
    [InlineData("", false)]
    [InlineData(null, false)]
    public void UnTelefono_TieneNueveDigitosConPrefijoOpcional(string? telefono, bool esperado) =>
        Assert.Equal(esperado, Reglas.EsTelefono(telefono));

    [Theory]
    [InlineData("ana@letreros.cl", true)]
    [InlineData("ana.diaz+taller@sub.dominio.cl", true)]
    [InlineData("ana@localhost", false)]          // sin dominio de primer nivel
    [InlineData("Ana Diaz <ana@letreros.cl>", false)]
    [InlineData("ana@@letreros.cl", false)]
    [InlineData("ana..diaz@letreros.cl", false)]
    [InlineData("ana@[10.0.0.1]", false)]
    [InlineData("ana letreros.cl", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void UnCorreo_DebeSerUnaDireccionSimple(string? correo, bool esperado) =>
        Assert.Equal(esperado, Reglas.EsCorreo(correo));
}

public class ErroresDeCamposTests
{
    [Fact]
    public void SinErrores_NoHayNadaQueInformar()
    {
        var errores = new ErroresDeCampos()
            .Requerido("Ana", "nombre", "El nombre es obligatorio.")
            .Si(false, "rut", "El RUT no es válido.");

        Assert.False(errores.Hay);
        Assert.Empty(errores.PorCampo());
    }

    [Fact]
    public void LosErrores_SeAgrupanPorCampoYConservanElOrden()
    {
        var errores = new ErroresDeCampos()
            .Requerido("  ", "nombre", "El nombre es obligatorio.")
            .Si(true, "rut", "El RUT no es válido.")
            .Si(true, "rut", "El RUT ya está registrado.");

        Assert.True(errores.Hay);
        var porCampo = errores.PorCampo();
        Assert.Equal(["nombre", "rut"], porCampo.Keys);
        Assert.Equal(["El RUT no es válido.", "El RUT ya está registrado."], porCampo["rut"]);
        Assert.Equal("El nombre es obligatorio.", errores.Resumen);
    }
}
