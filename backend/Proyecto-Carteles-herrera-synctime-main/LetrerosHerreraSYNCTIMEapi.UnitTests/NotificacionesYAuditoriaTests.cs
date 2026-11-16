using System.Security.Cryptography;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 11: el enlace de baja, las cabeceras List-Unsubscribe, los correos de estado y el detalle de la auditoria.
public class NotificacionesYAuditoriaTests
{
    private static readonly byte[] Clave = RandomNumberGenerator.GetBytes(32);

    // ── Enlace de baja ──

    [Fact]
    public void ElTokenDeBaja_SirveParaSuCliente_YSoloMientrasNoCambieElCorreoNiLaCuenta()
    {
        var token = EnlaceBaja.Token(Clave, 42, "cliente@correo.cl", null);

        Assert.True(EnlaceBaja.TryLeerId(token, out var id));
        Assert.Equal(42, id);
        Assert.True(EnlaceBaja.Valido(Clave, token, 42, "Cliente@Correo.cl", null));
        Assert.False(EnlaceBaja.Valido(Clave, token, 42, "otro@correo.cl", null));
        Assert.False(EnlaceBaja.Valido(Clave, token, 42, "cliente@correo.cl", DateTimeOffset.UtcNow));
        Assert.False(EnlaceBaja.Valido(Clave, token, 43, "cliente@correo.cl", null));
        Assert.False(EnlaceBaja.Valido(RandomNumberGenerator.GetBytes(32), token, 42, "cliente@correo.cl", null));
        Assert.DoesNotContain("cliente", token);
        Assert.DoesNotContain("@", token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corto")]
    [InlineData("con espacios y simbolos !!")]
    public void UnTokenIlegible_NoSeLee(string? token) => Assert.False(EnlaceBaja.TryLeerId(token, out _));

    [Fact]
    public void UnTokenAlterado_NoEsValido()
    {
        var token = EnlaceBaja.Token(Clave, 7, "a@b.cl", null);
        for (var i = 0; i < token.Length; i += 5)
        {
            var alterado = token[..i] + (token[i] == 'A' ? 'B' : 'A') + token[(i + 1)..];
            Assert.False(EnlaceBaja.TryLeerId(alterado, out var id) && EnlaceBaja.Valido(Clave, alterado, id, "a@b.cl", null));
        }
    }

    [Fact]
    public void LosEnlaces_UsanLaDireccionPublica()
    {
        Assert.Equal("https://sitio.cl/baja?t=abc", EnlaceBaja.UrlPagina("https://sitio.cl/", "abc"));
        Assert.Equal("https://sitio.cl/api/v1/correos/baja/un-clic?t=abc", EnlaceBaja.UrlUnClic("https://sitio.cl", "abc"));
        Assert.Null(EnlaceBaja.UrlPagina("no es una url", "abc"));
        Assert.Null(EnlaceBaja.UrlPagina("javascript:alert(1)", "abc"));
    }

    // ── Cabeceras (RFC 2369 y RFC 8058) ──

    private static CorreoSaliente Fila(string? enlaceBaja) => new()
    {
        IdCorreo = 5,
        Tipo = TiposCorreo.EstadoPedido,
        Destinatarios = "cliente@correo.cl",
        Asunto = "Tu pedido cambió",
        CuerpoHtml = "<p>Hola</p>",
        CuerpoTexto = "Hola",
        EnlaceBaja = enlaceBaja,
        FechaCreacion = DateTimeOffset.UtcNow
    };

    private static OpcionesCorreo Opciones() => new() { Modo = OpcionesCorreo.ModoBandeja };

    [Fact]
    public void UnCorreoOpcional_LlevaLasCabecerasDeBajaConUnClic()
    {
        var mensaje = ConstructorMime.Construir(Fila("https://sitio.cl/api/v1/correos/baja/un-clic?t=abc"), Opciones());

        Assert.Equal("<https://sitio.cl/api/v1/correos/baja/un-clic?t=abc>", mensaje.Headers["List-Unsubscribe"]);
        Assert.Equal("List-Unsubscribe=One-Click", mensaje.Headers["List-Unsubscribe-Post"]);
    }

    [Fact]
    public void UnCorreoEsencial_NoLlevaCabecerasDeBaja()
    {
        var mensaje = ConstructorMime.Construir(Fila(null), Opciones());

        Assert.Null(mensaje.Headers["List-Unsubscribe"]);
        Assert.Null(mensaje.Headers["List-Unsubscribe-Post"]);
    }

    // ── Categorias ──

    [Fact]
    public void LasCategorias_SeparanAvisosDelTallerOpcionalesYEsenciales()
    {
        foreach (var tipo in new[] { TiposCorreo.AvisoSolicitudNueva, TiposCorreo.AvisoTopeSolicitudes, TiposCorreo.ConsultaContacto })
        {
            Assert.True(CategoriasCorreo.EsAvisoDelTaller(tipo));
            Assert.False(CategoriasCorreo.EsAvisoDeEstado(tipo));
        }
        foreach (var tipo in new[] { TiposCorreo.EstadoSolicitud, TiposCorreo.EstadoPedido, TiposCorreo.CotizacionLista })
            Assert.True(CategoriasCorreo.EsAvisoDeEstado(tipo));
        foreach (var tipo in new[] { TiposCorreo.VerificacionCorreo, TiposCorreo.RecuperacionCuenta, TiposCorreo.AvisoCambioCorreo, TiposCorreo.BajaCuenta })
            Assert.False(CategoriasCorreo.EsAvisoDeEstado(tipo));
        Assert.Equal("Aviso de solicitud nueva", CategoriasCorreo.Texto(TiposCorreo.AvisoSolicitudNueva));
    }

    [Fact]
    public void LosDestinatariosEnmascarados_NoMuestranNingunaDireccionCompleta()
    {
        var enmascarados = CategoriasCorreo.Enmascarar("uno@taller.cl;dos.personas@gmail.com");

        Assert.Equal("u•••o@taller.cl, d•••s@gmail.com", enmascarados);
    }

    // ── Correos de estado al cliente (S09) ──

    [Fact]
    public void ElCorreoDeEstadoDelPedido_TieneNumeroEstadoMensajeFechaYEnlaceDeBaja()
    {
        var mensaje = CorreosDeEstado.Pedido("cliente@correo.cl", "Ana", 12, "anulado", "El cliente desistió", new DateTime(2026, 11, 15, 10, 30, 0),
            "contacto@taller.cl", "https://sitio.cl", "TOKEN123");

        Assert.Equal(TiposCorreo.EstadoPedido, mensaje.Tipo);
        Assert.Equal("estado:pedido:12", mensaje.Referencia);
        Assert.Contains("#12", mensaje.Texto);
        Assert.Contains("Anulado", mensaje.Texto);
        Assert.Contains("El cliente desistió", mensaje.Texto);
        Assert.Contains("15-11-2026", mensaje.Texto);
        Assert.Contains("https://sitio.cl/baja?t=TOKEN123", mensaje.Html);
        Assert.Equal("https://sitio.cl/api/v1/correos/baja/un-clic?t=TOKEN123", mensaje.EnlaceBaja);
    }

    [Fact]
    public void ElComprobanteDeBaja_EsEsencial_SinEnlaceDeBaja()
    {
        var mensaje = CorreosDeEstado.ComprobanteBaja("cliente@correo.cl", "Ana", "BAJA-3-20261115", new DateTime(2026, 11, 15, 10, 30, 0), "contacto@taller.cl");

        Assert.Equal(TiposCorreo.BajaCuenta, mensaje.Tipo);
        Assert.Null(mensaje.EnlaceBaja);
        Assert.Contains("BAJA-3-20261115", mensaje.Texto);
        Assert.DoesNotContain("/baja?t=", mensaje.Html);
    }

    // ── Detalle de la auditoria ──

    [Fact]
    public void ElDetalle_GuardaLosNombres_YSoloLosValoresPermitidos()
    {
        var reglas = ReglasAuditoria.Entidades[typeof(Cliente)];
        var cambios = new List<CambioDeCampo>
        {
            new("Activo", true, false),
            new("Email", "antes@correo.cl", "despues@correo.cl"),
            new("PasswordHash", "hash-viejo", "hash-nuevo")
        };

        var detalle = JsonDocument.Parse(DetalleAuditoria.DeCambios(reglas, cambios)!).RootElement;

        Assert.Equal(["activo", "email", "passwordHash"], detalle.GetProperty("campos").EnumerateArray().Select(c => c.GetString()));
        Assert.False(detalle.GetProperty("valores").GetProperty("activo").GetProperty("despues").GetBoolean());
        Assert.False(detalle.GetProperty("valores").TryGetProperty("email", out _));
        Assert.DoesNotContain("correo.cl", detalle.GetRawText());
        Assert.DoesNotContain("hash-", detalle.GetRawText());
    }

    [Fact]
    public void ElDetalle_NuncaPasaDe2000Caracteres()
    {
        var reglas = ReglasAuditoria.Entidades[typeof(Solicitud)];
        var cambios = Enumerable.Range(0, 400).Select(i => new CambioDeCampo($"CampoMuyLargoNumero{i}", null, null)).ToList();

        Assert.True(DetalleAuditoria.DeCambios(reglas, cambios)!.Length <= 2000);
        Assert.True(DetalleAuditoria.Libre(new Dictionary<string, object?> { ["x"] = new string('a', 5000) })!.Length <= 2000);
    }

    [Fact]
    public void NingunCampoSensible_PuedeMostrarSuValor()
    {
        foreach (var (tipo, reglas) in ReglasAuditoria.Entidades)
            Assert.Empty(reglas.Valores.Intersect(ReglasAuditoria.Sensibles, StringComparer.OrdinalIgnoreCase));
        foreach (var sensible in new[] { "PasswordHash", "TokenHash", "TokenEquipoHash", "DescriptorCifrado", "DescriptorFrente", "Email", "Correo", "CuerpoHtml" })
            Assert.Contains(sensible, ReglasAuditoria.Sensibles);
    }
}
