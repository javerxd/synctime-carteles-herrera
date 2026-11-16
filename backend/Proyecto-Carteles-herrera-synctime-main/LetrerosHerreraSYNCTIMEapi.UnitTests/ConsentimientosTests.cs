using LetrerosHerreraSYNCTIMEapi.Negocio.Consentimientos;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// RFS12: cada documento legal tiene una version, y un consentimiento pertenece a una sola persona o envio.
public class ConsentimientosTests
{
    [Fact]
    public void CadaDocumentoLegal_TieneVersion()
    {
        Assert.Equal(["avisos_estado", "cookies", "marketing", "privacidad", "terminos"], DocumentosLegales.VersionesVigentes.Keys.Order());
        Assert.All(DocumentosLegales.VersionesVigentes.Values, version => Assert.Matches(@"^\d+\.\d+$", version));
    }

    [Theory]
    [InlineData("privacidad", true)]
    [InlineData("terminos", true)]
    [InlineData("marketing", true)]
    [InlineData("cookies", true)]
    [InlineData("biometrico", false)]
    [InlineData("PRIVACIDAD", false)]
    [InlineData("", false)]
    public void SoloSeAceptanLosTiposConocidos(string tipo, bool valido)
    {
        Assert.Equal(valido, DocumentosLegales.EsTipo(tipo));
    }

    [Fact]
    public void UnTitular_EsUnaSolaPersonaOUnSoloEnvio()
    {
        Assert.Equal(5, TitularConsentimiento.Cliente(5).IdCliente);
        Assert.Equal(7, TitularConsentimiento.Empleado(7).IdEmpleado);
        Assert.Equal("contacto:12", TitularConsentimiento.Envio("contacto:12").ReferenciaEnvio);
        Assert.Throws<ArgumentException>(() => TitularConsentimiento.Envio(" "));
        Assert.Throws<ArgumentException>(() => TitularConsentimiento.Envio(new string('x', 65)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TitularConsentimiento.Cliente(0));
    }
}
