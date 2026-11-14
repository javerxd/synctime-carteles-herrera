using System.Text;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 7: reglas de las solicitudes, sin base de datos (RFS04, RNFP32, RNFP33).
public class SolicitudesTests
{
    // ── Transiciones de estado ──

    [Theory]
    [InlineData("solicitado", "cotizado", ActorEstado.Sistema)]
    [InlineData("cotizado", "solicitado", ActorEstado.Cliente)]
    [InlineData("cotizado", "solicitado", ActorEstado.Personal)]
    [InlineData("cotizado", "aceptado", ActorEstado.Sistema)]
    [InlineData("solicitado", "rechazado", ActorEstado.Personal)]
    [InlineData("cotizado", "rechazado", ActorEstado.Personal)]
    public void LasTransicionesDelDiagrama_SonValidas(string desde, string hacia, ActorEstado quien)
    {
        Assert.True(TransicionesSolicitud.EsValida(desde, hacia, quien));
        Assert.Null(TransicionesSolicitud.Explicar(desde, hacia, quien));
    }

    [Theory]
    [InlineData("solicitado", "aceptado", ActorEstado.Personal)]
    [InlineData("solicitado", "cotizado", ActorEstado.Personal)]
    [InlineData("solicitado", "solicitado", ActorEstado.Personal)]
    [InlineData("rechazado", "solicitado", ActorEstado.Personal)]
    [InlineData("aceptado", "rechazado", ActorEstado.Personal)]
    [InlineData("cotizado", "aceptado", ActorEstado.Personal)]
    [InlineData("solicitado", "rechazado", ActorEstado.Cliente)]
    public void LasDemasTransiciones_SeRechazanConUnaExplicacion(string desde, string hacia, ActorEstado quien)
    {
        Assert.False(TransicionesSolicitud.EsValida(desde, hacia, quien));
        Assert.False(string.IsNullOrWhiteSpace(TransicionesSolicitud.Explicar(desde, hacia, quien)));
    }

    [Theory]
    [InlineData("solicitado", true, true)]
    [InlineData("cotizado", true, false)]
    [InlineData("aceptado", false, false)]
    [InlineData("rechazado", false, true)]
    public void EditarYEliminar_DependenDelEstado(string estado, bool editable, bool eliminable)
    {
        Assert.Equal(editable, EstadosSolicitud.EsEditable(estado));
        Assert.Equal(eliminable, EstadosSolicitud.PuedeEliminarse(estado));
    }

    // ── Validacion de items y descripcion ──

    private static EntradaDetalle Item(int? ancho = 1000, int? alto = 500, int cantidad = 1, string? descripcion = null) =>
        new(1, descripcion, ancho, alto, cantidad);

    [Fact]
    public void UnaSolicitudValida_NoTieneErrores()
    {
        Assert.Empty(ValidacionSolicitud.Validar("Letrero luminoso para mi almacén", [Item(), Item(null, null)], [1, 2]));
    }

    [Fact]
    public void LosErrores_VanPorCampoConElIndiceDelItem()
    {
        var errores = ValidacionSolicitud.Validar("corta", [Item(alto: null), Item(ancho: 0, cantidad: 1000), Item(descripcion: new string('x', 501))], null);

        Assert.Equal(["descripcionGeneral", "detalles[0].altoMm", "detalles[1].anchoMm", "detalles[1].cantidad", "detalles[2].descripcion"], errores.Keys.Order());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void DebeHaberDe1A10Items(int cantidad)
    {
        var errores = ValidacionSolicitud.Validar("Descripción suficiente", Enumerable.Range(0, cantidad).Select(_ => Item()).ToList(), null);
        Assert.True(errores.ContainsKey("detalles"));
    }

    [Fact]
    public void MasDe5Adjuntos_EsUnError()
    {
        Assert.True(ValidacionSolicitud.Validar("Descripción suficiente", [Item()], [1, 2, 3, 4, 5, 6]).ContainsKey("adjuntos"));
        Assert.False(ValidacionSolicitud.Validar("Descripción suficiente", [Item()], [1, 1, 2, 3, 4, 5]).ContainsKey("adjuntos"));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("corto", false)]
    [InlineData("No trabajamos ese material", true)]
    public void ElMotivoDeRechazo_TieneDe10A500Caracteres(string? motivo, bool valido) =>
        Assert.Equal(valido, ValidacionSolicitud.ValidarMotivo(motivo) is null);

    // ── Area ──

    [Theory]
    [InlineData(1000, 2000, 2, 4.0)]
    [InlineData(333, 333, 1, 0.11)]
    [InlineData(1234, 567, 3, 2.1)]
    [InlineData(50000, 50000, 999, 2497500.0)]
    public void ElArea_SeCalculaEnElServidorConDosDecimales(int ancho, int alto, int cantidad, double esperada) =>
        Assert.Equal((decimal)esperada, MedidasSolicitud.AreaM2(ancho, alto, cantidad));

    [Fact]
    public void SinMedidas_NoHayArea_YElTotalSumaSoloLasConocidas()
    {
        Assert.Null(MedidasSolicitud.AreaM2(null, null, 3));
        Assert.Equal(1.5m, MedidasSolicitud.AreaTotal([1m, null, 0.5m]));
        Assert.Null(MedidasSolicitud.AreaTotal([null, null]));
        Assert.Equal("#123", MedidasSolicitud.Numero(123));
        Assert.Equal("2,5 m²", MedidasSolicitud.Area(2.5m));
    }

    // ── Regla contra solicitudes falsas ──

    [Theory]
    [InlineData(false, false, false, 0, false)]
    [InlineData(false, false, false, 2, false)]
    [InlineData(true, false, false, 0, true)]
    [InlineData(false, true, false, 0, true)]
    [InlineData(false, false, true, 0, true)]
    [InlineData(false, false, false, 3, true)]
    public void LaRegla_FrenaPorCualquierMotivo(bool cuenta, bool rut, bool ip, int envios, bool frena) =>
        Assert.Equal(frena, ReglaContraSolicitudesFalsas.Frena(new(cuenta, rut, ip, envios)));

    private static readonly byte[] ClaveUno = HuellaIp.DerivarClave("secreto-uno");
    private static readonly byte[] ClaveDos = HuellaIp.DerivarClave("secreto-dos");

    [Fact]
    public void ElMaximoPorIp_EsConfigurable_YElAvisoEsElDeS25()
    {
        Assert.False(ReglaContraSolicitudesFalsas.Frena(new(false, false, false, 3), maxEnviosPorIp: 4));
        Assert.True(ReglaContraSolicitudesFalsas.Frena(new(false, false, false, 1), maxEnviosPorIp: 1));
        Assert.Equal(3, ReglaContraSolicitudesFalsas.MaxEnLineaPorIpPredeterminado);
        Assert.Equal(60, ReglaContraSolicitudesFalsas.VentanaIpMinutosPredeterminada);
        Assert.Equal("Para esta solicitud es preferible contactarse directamente con el local", ReglaContraSolicitudesFalsas.Mensaje);
    }

    [Fact]
    public void LaHuellaDeLaIp_EsUnHmacQueDependeDeLaClave_YNoContieneLaIp()
    {
        var huella = HuellaIp.Calcular("203.0.113.7", ClaveUno);

        Assert.Matches("^[0-9a-f]{64}$", huella);
        Assert.Equal(huella, HuellaIp.Calcular(" 203.0.113.7 ", ClaveUno));
        Assert.Equal(huella, HuellaIp.Calcular("::ffff:203.0.113.7", ClaveUno));
        Assert.NotEqual(huella, HuellaIp.Calcular("203.0.113.7", ClaveDos));
        Assert.NotEqual(huella, HuellaIp.Calcular("203.0.113.8", ClaveUno));
        Assert.DoesNotContain("203", huella!);
        Assert.Null(HuellaIp.Calcular(null, ClaveUno));
    }

    [Fact]
    public void UnaIpv6_CuentaPorSuPrefijo64()
    {
        Assert.Equal("2001:db8:1:2::/64", HuellaIp.Normalizar("2001:db8:1:2:aaaa:bbbb:cccc:dddd"));
        Assert.Equal(HuellaIp.Calcular("2001:db8:1:2::1", ClaveUno), HuellaIp.Calcular("2001:db8:1:2:ffff::9", ClaveUno));
        Assert.NotEqual(HuellaIp.Calcular("2001:db8:1:2::1", ClaveUno), HuellaIp.Calcular("2001:db8:1:3::1", ClaveUno));
    }

    [Fact]
    public void LaClaveDeLaHuella_SeDerivaConSuEtiqueta_YNoEsElSecretoOriginal()
    {
        Assert.Equal(32, ClaveUno.Length);
        Assert.Equal(ClaveUno, HuellaIp.DerivarClave("secreto-uno"));
        Assert.NotEqual(Encoding.UTF8.GetBytes("secreto-uno"), ClaveUno);
        var esperada = System.Security.Cryptography.HKDF.DeriveKey(System.Security.Cryptography.HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes("secreto-uno"), 32, info: Encoding.UTF8.GetBytes("synctime:huella-ip"));
        Assert.Equal(esperada, ClaveUno);
    }

    [Theory]
    [InlineData(null, null, null, 0)]
    [InlineData("solicitado", "online", "fecha_asc", 0)]
    [InlineData("inventado", "telefono", "nombre", 3)]
    public void LosFiltrosDeLaBandeja_SeValidan(string? estado, string? origen, string? orden, int errores) =>
        Assert.Equal(errores, ValidacionSolicitud.ValidarFiltro(estado, origen, orden).Count);

    // ── Adjuntos ──

    [Fact]
    public void ElFormato_SeDecidePorElContenido()
    {
        Assert.Equal(FormatoAdjunto.Imagen, ReglasAdjunto.Detectar([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal(FormatoAdjunto.Imagen, ReglasAdjunto.Detectar([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
        Assert.Equal(FormatoAdjunto.Pdf, ReglasAdjunto.Detectar(Encoding.ASCII.GetBytes("%PDF-1.7\n")));
        Assert.Equal(FormatoAdjunto.Desconocido, ReglasAdjunto.Detectar(Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")));
        Assert.Equal(FormatoAdjunto.Desconocido, ReglasAdjunto.Detectar(Encoding.ASCII.GetBytes("%!PS-Adobe-3.0 (un .ai)")));
        Assert.Equal(FormatoAdjunto.Desconocido, ReglasAdjunto.Detectar([]));
    }

    [Theory]
    [InlineData("logo.png", "logo.png")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\Users\\Ana\\diseño final.pdf", "diseño final.pdf")]
    [InlineData("mi \"logo\"<script>.png", "mi _logo__script_.png")]
    [InlineData("nombre\u202Egnp.exe", "nombregnp.exe")]
    [InlineData("   ", "imagen")]
    public void ElNombre_SeSanea(string nombre, string esperado) =>
        Assert.Equal(esperado, ReglasAdjunto.SanearNombre(nombre, FormatoAdjunto.Imagen));

    [Fact]
    public void UnNombreLargo_SeRecortaConservandoLaExtension()
    {
        var nombre = ReglasAdjunto.SanearNombre(new string('a', 300) + ".pdf", FormatoAdjunto.Pdf);
        Assert.Equal(120, nombre.Length);
        Assert.EndsWith(".pdf", nombre);
        Assert.Equal("plano.pdf", ReglasAdjunto.NombreDescargaPdf("plano"));
    }

    // ── Confirmacion al cliente ──

    [Fact]
    public void LaConfirmacion_LlevaElComprobante_YPideNoResponder()
    {
        var comprobante = new ComprobanteSolicitud(45, "#45", new DateTime(2026, 11, 14, 10, 32, 0), "online", "solicitado", null,
            new ClienteComprobante("Ana", "Rojas", "912345678", "ana@correo.cl"),
            [new DetalleComprobante(1, "Letrero luminoso", null, 1000, 2000, 2, 4m)], 4m, "Para la vitrina",
            [new AdjuntoComprobante(3, "pdf", "plano.pdf", 100, null, null, null, "/api/v1/adjuntos/3/archivo")], [1]);

        var mensaje = ConfirmacionSolicitud.Componer(comprobante, "ana@correo.cl", "contacto@taller.cl", "https://synctime.cl");

        Assert.Equal(TiposCorreo.ConfirmacionSolicitud, mensaje.Tipo);
        Assert.Equal("Recibimos tu solicitud #45", mensaje.Asunto);
        Assert.Equal(["ana@correo.cl"], mensaje.Para);
        Assert.Equal("contacto@taller.cl", mensaje.ResponderA);
        foreach (var parte in new[] { mensaje.Texto, mensaje.Html })
        {
            Assert.Contains("14-11-2026", parte);
            Assert.Contains("10:32", parte);
            Assert.Contains("2 × Letrero luminoso", parte);
            Assert.Contains("100 × 200 cm", parte);
            Assert.Contains("4 m²", parte);
            Assert.Contains("plano.pdf", parte);
            Assert.Contains("En revisión", parte);
        }
        Assert.Contains("no respondas", mensaje.Texto);
        Assert.Contains("https://synctime.cl/cuenta/solicitudes", mensaje.Html);
    }
}
