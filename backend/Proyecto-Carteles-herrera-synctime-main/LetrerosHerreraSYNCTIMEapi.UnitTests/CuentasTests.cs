using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 6: reglas de los datos de la cuenta y correos de cuenta.
public class CuentasTests
{
    [Theory]
    [InlineData("912345678", "912345678")]
    [InlineData("9 1234 5678", "912345678")]
    [InlineData("+56 9 1234 5678", "912345678")]
    [InlineData("+56912345678", "912345678")]
    [InlineData("12345", null)]
    [InlineData("", null)]
    public void ElTelefono_SeNormalizaANueveDigitos(string escrito, string? esperado) =>
        Assert.Equal(esperado, ValidacionCuenta.NormalizarTelefono(escrito));

    [Fact]
    public void UnaCuentaValida_NoTieneErrores_YElRutEsOpcional()
    {
        Assert.Empty(ValidacionCuenta.Validar(null, "Camila", "Rojas", "912345678", null));
        Assert.Empty(ValidacionCuenta.Validar("12.345.678-5", "Camila", "Rojas", "912345678", "Villagrán 810"));
    }

    [Fact]
    public void LosErrores_VanPorCampo()
    {
        var errores = ValidacionCuenta.Validar("12.345.678-9", "", new string('a', 81), "123", new string('d', 256));

        Assert.Equal(["apellido", "direccion", "nombre", "rut", "telefono"], errores.Keys.Order());
    }

    [Theory]
    [InlineData("nombre@correo.cl", true)]
    [InlineData("sin-arroba", false)]
    [InlineData("a;b@correo.cl", false)]
    public void ElCorreo_DebeSerValido_YCaberEnLaColumna(string correo, bool valido)
    {
        var errores = new Dictionary<string, string[]>();
        ValidacionCuenta.ValidarCorreo(correo, errores);
        Assert.Equal(valido, errores.Count == 0);

        var largo = new Dictionary<string, string[]>();
        ValidacionCuenta.ValidarCorreo($"{new string('a', 95)}@correo.cl", largo);
        Assert.True(largo.ContainsKey("correo"));
    }

    [Fact]
    public void ElCorreoDeRecuperacion_LlevaElCodigo_YPideNoResponderALaCasillaAutomatica()
    {
        var mensaje = CorreosDeCuenta.Recuperacion("cliente@correo.cl", "Camila", "042137", 15, "contacto.letrerosherrera@gmail.com");

        Assert.Equal("recuperacion_cuenta", mensaje.Tipo);
        Assert.Contains("042137", mensaje.Texto);
        Assert.DoesNotContain("042137", mensaje.Asunto);
        Assert.Equal("contacto.letrerosherrera@gmail.com", mensaje.ResponderA);
        Assert.Contains("no respondas", mensaje.Texto);
    }

    [Fact]
    public void ElCorreoDeVerificacion_NoAceptaEnlacesQueEjecutenCodigo()
    {
        Assert.Throws<ArgumentException>(() => CorreosDeCuenta.Verificacion("c@correo.cl", "Camila", "javascript:alert(1)", "contacto.letrerosherrera@gmail.com"));
        var mensaje = CorreosDeCuenta.Verificacion("c@correo.cl", "Camila", "https://letrerosherrera.cl/cuenta/verificar?token=abc", "contacto.letrerosherrera@gmail.com");
        Assert.Contains("token=abc", mensaje.Html);
    }
}
