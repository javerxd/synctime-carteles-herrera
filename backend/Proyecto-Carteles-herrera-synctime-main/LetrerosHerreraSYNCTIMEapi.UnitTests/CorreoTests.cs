using System.Net.Sockets;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Repositories;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Reglas del correo que no necesitan la base ni un servidor: reintentos, destinatarios, plantilla y fallos.
public class CorreoTests
{
    // ── Reintentos ──

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 60)]
    [InlineData(5, 180)]
    public void LaCola_EsperaCadaVezMasEntreIntentos(int intentosRealizados, int minutos)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutos), PoliticaReintentos.Espera(intentosRealizados, maximoIntentos: 6));
    }

    [Fact]
    public void UnProblemaDelServidor_NuncaAgotaLosIntentos()
    {
        Assert.Equal(TimeSpan.FromMinutes(180), PoliticaReintentos.Espera(6, maximoIntentos: 6, problemaDelServidor: true));
        Assert.Equal(TimeSpan.FromMinutes(180), PoliticaReintentos.Espera(40, maximoIntentos: 6, problemaDelServidor: true));
    }

    [Fact]
    public void LaCola_NoReintentaAlAgotarLosIntentos()
    {
        Assert.Null(PoliticaReintentos.Espera(6, maximoIntentos: 6));
        Assert.Null(PoliticaReintentos.Espera(9, maximoIntentos: 6));
        Assert.Equal(TimeSpan.FromMinutes(180), PoliticaReintentos.Espera(8, maximoIntentos: 10));
    }

    // ── Destinatarios de los avisos del taller ──

    [Fact]
    public void LosDestinatarios_AdmitenVariosCorreosSeparadosPorComaOPuntoYComa()
    {
        var lista = ListaDeCorreos.Separar(" dueno@gmail.com; Taller@Letreria.cl ,otro@correo.cl ", out var invalidos);

        Assert.Equal(["dueno@gmail.com", "Taller@Letreria.cl", "otro@correo.cl"], lista);
        Assert.Equal(0, invalidos);
    }

    [Fact]
    public void LosDestinatarios_DescartanRepetidosYDireccionesInvalidas()
    {
        var lista = ListaDeCorreos.Separar("dueno@gmail.com;DUENO@gmail.com;no-es-correo;;a@b", out var invalidos);

        Assert.Equal(["dueno@gmail.com"], lista);
        Assert.Equal(2, invalidos);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ; , ")]
    public void SinDestinatarios_LaListaQuedaVacia(string? texto)
    {
        Assert.Empty(ListaDeCorreos.Separar(texto, out var invalidos));
        Assert.Equal(0, invalidos);
    }

    [Fact]
    public void LosDestinatarios_NoAceptanSaltosDeLinea()
    {
        // Un salto de linea en una direccion podria inyectar cabeceras en el mensaje.
        Assert.Empty(ListaDeCorreos.Separar("dueno@gmail.com\r\nBcc: espia@x.cl", out _));
    }

    // ── Plantilla base ──

    private static (string Html, string Texto) PlantillaDeEjemplo(string parrafo) =>
        PlantillaCorreo.Componer("Título de prueba", "Resumen breve", new CuerpoCorreo().Parrafo(parrafo), "Pie de prueba");

    [Fact]
    public void LaPlantilla_EscapaElTextoQueEscribeElUsuario()
    {
        var (html, texto) = PlantillaDeEjemplo("<script>alert('x')</script> & <b>negrita</b>");

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<b>negrita</b>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("<script>alert('x')</script> & <b>negrita</b>", texto);
    }

    [Fact]
    public void LaPlantilla_EsLegibleEnElTelefonoYEnEspanol()
    {
        var (html, _) = PlantillaDeEjemplo("Hola");

        Assert.Contains("<html lang=\"es\"", html);
        Assert.Contains("name=\"viewport\"", html);
        Assert.Contains("max-width:600px", html);
        Assert.Contains("name=\"color-scheme\" content=\"light\"", html);
        // Sin recursos externos: el logotipo va dentro del mensaje.
        Assert.Contains($"cid:{PlantillaCorreo.IdLogo}", html);
        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("fonts.googleapis", html);
    }

    [Fact]
    public void LaPlantilla_UsaLaIdentidadVisualDelSistema()
    {
        var cuerpo = new CuerpoCorreo().Parrafo("Hola").Boton("Abrir el panel", "https://synctime.cl/panel");
        var (html, texto) = PlantillaCorreo.Componer("Título", "Resumen", cuerpo, "Pie");

        Assert.Contains("#0F172A", html);  // tinta
        Assert.Contains("#B45309", html);  // ámbar, solo en la acción
        Assert.Contains("Plus Jakarta Sans", html);
        Assert.Contains("Letrería Los Herrera", html);
        Assert.Contains("Abrir el panel: https://synctime.cl/panel", texto);
        Assert.Contains("Resumen", html);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hola")]
    [InlineData("ftp://servidor/archivo")]
    public void LaPlantilla_NoAceptaEnlacesPeligrosos(string url)
    {
        Assert.Throws<ArgumentException>(() => new CuerpoCorreo().Boton("Abrir", url));
    }

    [Fact]
    public void ElAsunto_NoPuedeLlevarSaltosDeLinea()
    {
        var asunto = PlantillaCorreo.AsuntoSeguro("Nueva solicitud\r\nBcc: espia@x.cl\t" + new string('a', 300));

        Assert.DoesNotContain('\r', asunto);
        Assert.DoesNotContain('\n', asunto);
        Assert.DoesNotContain('\t', asunto);
        Assert.True(asunto.Length <= 150);
    }

    // ── Aviso de solicitud nueva ──

    private static SolicitudParaAviso Solicitud(string origen = "online", string? email = "juan.perez@correo.cl", string? registradaPor = null) => new(
        IdSolicitud: 123,
        FechaSolicitud: new DateTime(2026, 11, 13, 15, 42, 0),
        Origen: origen,
        RegistradaPor: registradaPor,
        ClienteNombre: "Juan Pérez",
        ClienteTelefono: "912345678",
        ClienteEmail: email,
        Descripcion: "Letrero para la vitrina <urgente>",
        Detalles: [new DetalleParaAviso("Letrero luminoso", "Con logo", 1000, 2000, 2), new DetalleParaAviso("Adhesivo", "Sin medidas", null, null, 1)]);

    [Fact]
    public void ElAviso_TieneLoQueElDuenoNecesitaParaResponder()
    {
        var mensaje = AvisoSolicitudNueva.Componer(Solicitud(), ["dueno@gmail.com"], "https://localhost:3000");

        Assert.Equal(TiposCorreo.AvisoSolicitudNueva, mensaje.Tipo);
        Assert.Equal(["dueno@gmail.com"], mensaje.Para);
        Assert.Equal("Nueva solicitud N.º 123 · Juan Pérez", mensaje.Asunto);
        Assert.Equal("juan.perez@correo.cl", mensaje.ResponderA);
        foreach (var parte in new[] { mensaje.Html, mensaje.Texto })
        {
            Assert.Contains("en línea", parte);
            Assert.Contains("13-11-2026", parte);
            Assert.Contains("15:42", parte);
            Assert.Contains("+56 9 1234 5678", parte);
            Assert.Contains("juan.perez@correo.cl", parte);
            Assert.Contains("2 × Letrero luminoso", parte);
            Assert.Contains("100 × 200 cm", parte);
            Assert.Contains("1 × Adhesivo", parte);
        }
        Assert.Contains("Letrero para la vitrina &lt;urgente&gt;", mensaje.Html);
        Assert.Contains("tel:+56912345678", mensaje.Html);
        Assert.Contains("https://wa.me/56912345678", mensaje.Html);
        Assert.Contains("https://localhost:3000/panel", mensaje.Html);
    }

    [Theory]
    [InlineData("localhost:5173")]
    [InlineData("ftp://servidor")]
    [InlineData("no es una url")]
    public void UnaUrlDelSitioMalEscrita_NoImpideElAviso(string url)
    {
        var mensaje = AvisoSolicitudNueva.Componer(Solicitud(), ["dueno@gmail.com"], url);

        Assert.DoesNotContain("Abrir el panel", mensaje.Html);
        Assert.Contains("Juan Pérez", mensaje.Html);
    }

    [Fact]
    public void LoQueEscribeElVisitante_NoPuedeFabricarLineasFalsas()
    {
        var solicitud = Solicitud() with
        {
            ClienteNombre = "Juan\r\nTeléfono: +56 9 0000 0000\u202E",
            Descripcion = "Hola\nSolicitud: N.º 999"
        };

        var mensaje = AvisoSolicitudNueva.Componer(solicitud, ["dueno@gmail.com"], null);

        Assert.DoesNotContain("\nTeléfono: +56 9 0000 0000", mensaje.Texto);
        Assert.DoesNotContain("\nSolicitud: N.º 999", mensaje.Texto);
        Assert.Contains("Hola Solicitud: N.º 999", mensaje.Texto);
        // Comparacion exacta: para la cultura, un caracter invisible "aparece" en cualquier texto.
        Assert.DoesNotContain('\u202E', mensaje.Texto);
        Assert.DoesNotContain('\u202E', mensaje.Asunto);
        Assert.DoesNotContain('\u202E', mensaje.Html);
    }

    [Fact]
    public void ElEnlaceParaEscribirle_NoAceptaParametrosEscondidosEnLaDireccion()
    {
        var mensaje = AvisoSolicitudNueva.Componer(Solicitud(email: "x?bcc=espia@otro.cl&subject=Urgente@dominio.cl"), ["dueno@gmail.com"], null);

        Assert.DoesNotContain("mailto:x?bcc", mensaje.Html);
    }

    [Fact]
    public void ElAvisoDeMuchasSolicitudes_LlevaAlPanel()
    {
        var mensaje = AvisoTopeSolicitudes.Componer(["dueno@gmail.com"], 20, "https://synctime.cl");

        Assert.Equal(TiposCorreo.AvisoTopeSolicitudes, mensaje.Tipo);
        Assert.Contains("20 solicitudes o más", mensaje.Texto);
        Assert.Contains("https://synctime.cl/panel", mensaje.Html);
    }

    [Fact]
    public void ElAviso_DeUnaSolicitudPresencial_DiceQuienLaRegistro()
    {
        var mensaje = AvisoSolicitudNueva.Componer(Solicitud("presencial", registradaPor: "María González"), ["dueno@gmail.com"], null);

        Assert.Contains("presencial", mensaje.Html);
        Assert.Contains("María González", mensaje.Texto);
        // Sin la dirección del sitio no hay botón al panel, pero el aviso igual sale.
        Assert.DoesNotContain("/panel", mensaje.Html);
    }

    [Fact]
    public void ElAviso_SinCorreoDelCliente_NoTieneResponderA()
    {
        var mensaje = AvisoSolicitudNueva.Componer(Solicitud(email: null), ["dueno@gmail.com"], null);

        Assert.Null(mensaje.ResponderA);
        Assert.Contains("Sin correo", mensaje.Texto);
    }

    [Fact]
    public void ElAviso_ConUnCorreoDelClienteInvalido_NoLoUsaComoResponderA()
    {
        var mensaje = AvisoSolicitudNueva.Componer(Solicitud(email: "juan\r\nBcc: espia@x.cl"), ["dueno@gmail.com"], null);

        Assert.Null(mensaje.ResponderA);
    }

    [Theory]
    [InlineData("912345678", "+56 9 1234 5678", "+56912345678", true)]
    [InlineData("+56 9 1234 5678", "+56 9 1234 5678", "+56912345678", true)]
    [InlineData("222345678", "+56 2 2234 5678", "+56222345678", false)]
    public void ElTelefono_SeMuestraConPrefijoYSoloLosCelularesTienenWhatsApp(string telefono, string legible, string internacional, bool whatsapp)
    {
        var resultado = TelefonoChile.Interpretar(telefono);

        Assert.NotNull(resultado);
        Assert.Equal(legible, resultado.Legible);
        Assert.Equal(internacional, resultado.Internacional);
        Assert.Equal(whatsapp, resultado.EsCelular);
    }

    [Fact]
    public void UnTelefonoQueNoEsChileno_SeMuestraTalCualSinEnlaces()
    {
        Assert.Null(TelefonoChile.Interpretar("123"));
    }

    // ── Remitente ──

    [Fact]
    public void LaBandejaLocal_TieneRemitenteAunqueNoHayaCuentaConfigurada()
    {
        var bandeja = new OpcionesCorreo { Modo = OpcionesCorreo.ModoBandeja };
        var smtpSinCuenta = new OpcionesCorreo { Modo = OpcionesCorreo.ModoSmtp };
        var smtpConCuenta = new OpcionesCorreo { Modo = OpcionesCorreo.ModoSmtp, Smtp = new OpcionesSmtp { Usuario = "cuenta@gmail.com" } };

        Assert.Equal("synctime@bandeja.local", bandeja.DireccionRemitente);
        Assert.Null(smtpSinCuenta.DireccionRemitente);
        Assert.Equal("cuenta@gmail.com", smtpConCuenta.DireccionRemitente);
    }

    // ── Fallos del servidor de correo ──

    public static TheoryData<Exception, string, bool, bool> Fallos() => new()
    {
        { new AuthenticationException("535: 5.7.8 Usuario usuario-secreto@gmail.com rechazado"), "autenticacion", false, true },
        { new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, new MailboxAddress("", "destino-secreto@gmail.com"), "550 destino-secreto@gmail.com no existe"), "destinatario", true, false },
        { new SmtpCommandException(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.ServiceNotAvailable, "421 usuario-secreto@gmail.com intente luego"), "servidor", false, true },
        { new SmtpCommandException(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.AuthenticationRequired, "530 autenticacion requerida para usuario-secreto@gmail.com"), "autenticacion", false, true },
        { new SocketException((int)SocketError.ConnectionRefused), "conexion", false, true },
        { new IOException("usuario-secreto@gmail.com"), "conexion", false, true },
        { new TimeoutException("usuario-secreto@gmail.com"), "tiempo_agotado", false, true },
        { new OperationCanceledException("usuario-secreto@gmail.com"), "tiempo_agotado", false, true },
        { new SmtpProtocolException("usuario-secreto@gmail.com"), "servidor", false, true },
        { new InvalidOperationException("usuario-secreto@gmail.com"), "desconocido", false, false },
        // Un 4xx del destinatario (limite de envios, buzon lleno por ahora) se reintenta.
        { new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxBusy, new MailboxAddress("", "destino-secreto@gmail.com"), "450 destino-secreto@gmail.com ocupado"), "servidor", false, true },
        // Una direccion guardada que no se puede leer: reintentar no la arregla.
        { new ParseException("direccion-secreto@ invalida", 0, 0), "mensaje", true, false }
    };

    [Theory]
    [MemberData(nameof(Fallos))]
    public void CadaFallo_SeClasificaSinGuardarElTextoDelServidor(Exception error, string categoria, bool permanente, bool detieneLote)
    {
        var fallo = ClasificadorFallos.Clasificar(error);

        Assert.Equal(categoria, fallo.Categoria);
        Assert.Equal(permanente, fallo.Permanente);
        Assert.Equal(detieneLote, fallo.DetieneLote);
        // El texto del servidor puede repetir el usuario o la direccion: nunca se guarda ni se registra.
        Assert.DoesNotContain("secreto", fallo.Texto);
        Assert.False(string.IsNullOrWhiteSpace(fallo.Texto));
    }
}
