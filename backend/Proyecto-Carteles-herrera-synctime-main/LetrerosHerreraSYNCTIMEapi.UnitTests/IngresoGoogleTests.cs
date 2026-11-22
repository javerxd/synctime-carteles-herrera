using LetrerosHerreraSYNCTIMEapi.Negocio.IngresoGoogle;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 14A (RFS03, RNFE02, RNFP17): las condiciones que debe cumplir el token de Google, el nonce de un solo uso, el pase
// del registro pendiente y la regla de no fusionar cuentas. Todo con datos inventados: ninguna prueba toca a Google.
public class IngresoGoogleTests
{
    private const string ClientId = "pruebas-synctime.apps.googleusercontent.com";
    private const string Nonce = "nonce-de-pruebas-0123456789";
    private static readonly DateTimeOffset Ahora = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private sealed class RelojManual(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset ahora = inicio;
        public override DateTimeOffset GetUtcNow() => ahora;
        public void Avanzar(TimeSpan tiempo) => ahora += tiempo;
    }

    private static ClaimsGoogle Valido(Func<ClaimsGoogle, ClaimsGoogle>? cambio = null)
    {
        var claims = new ClaimsGoogle("https://accounts.google.com", [ClientId], "109876543210987654321", " Ana.Perez@Correo.TEST ", true,
            "Ana", "Pérez", "Ana Pérez", Nonce, Ahora.AddMinutes(30));
        return cambio?.Invoke(claims) ?? claims;
    }

    private static ValidacionGoogle Validar(ClaimsGoogle claims, string? nonceEsperado = Nonce) =>
        ReglasGoogle.Validar(claims, ClientId, nonceEsperado, Ahora);

    // ── Las condiciones del token ──

    [Fact]
    public void UnTokenValido_EntregaLaIdentidad_ConElCorreoNormalizado()
    {
        var resultado = Validar(Valido());

        Assert.Null(resultado.Motivo);
        var identidad = Assert.IsType<IdentidadGoogle>(resultado.Identidad);
        Assert.Equal("109876543210987654321", identidad.Sub);
        Assert.Equal("ana.perez@correo.test", identidad.Correo);
        Assert.Equal("Ana", identidad.Nombre);
        Assert.Equal("Pérez", identidad.Apellido);
    }

    [Theory]
    [InlineData("accounts.google.com")]
    [InlineData("https://accounts.google.com")]
    public void LosDosEmisoresDeGoogle_SeAceptan(string emisor) =>
        Assert.Null(Validar(Valido(c => c with { Emisor = emisor })).Motivo);

    [Theory]
    [InlineData("https://accounts.google.com.falso.test")]
    [InlineData("https://evil.test")]
    [InlineData("")]
    [InlineData(null)]
    public void UnEmisorAjeno_SeRechaza(string? emisor)
    {
        var resultado = Validar(Valido(c => c with { Emisor = emisor }));
        Assert.Null(resultado.Identidad);
        Assert.Equal(MotivoRechazoGoogle.Emisor, resultado.Motivo);
    }

    [Fact]
    public void UnTokenParaOtraAplicacion_SeRechaza_PorLaAudiencia()
    {
        var resultado = Validar(Valido(c => c with { Audiencias = ["otra-aplicacion.apps.googleusercontent.com"] }));
        Assert.Null(resultado.Identidad);
        Assert.Equal(MotivoRechazoGoogle.Audiencia, resultado.Motivo);
    }

    [Fact]
    public void SinAudiencia_SeRechaza() =>
        Assert.Equal(MotivoRechazoGoogle.Audiencia, Validar(Valido(c => c with { Audiencias = [] })).Motivo);

    [Fact]
    public void LaAudienciaSeCompara_ExactaYNoPorParecido() =>
        Assert.Equal(MotivoRechazoGoogle.Audiencia, Validar(Valido(c => c with { Audiencias = [ClientId.ToUpperInvariant(), ClientId + ".falso", "x" + ClientId] })).Motivo);

    [Fact]
    public void UnTokenVencido_SeRechaza()
    {
        Assert.Equal(MotivoRechazoGoogle.Vencido, Validar(Valido(c => c with { Expira = Ahora.AddSeconds(-1) })).Motivo);
        Assert.Equal(MotivoRechazoGoogle.Vencido, Validar(Valido(c => c with { Expira = Ahora })).Motivo);
        Assert.Equal(MotivoRechazoGoogle.Vencido, Validar(Valido(c => c with { Expira = null })).Motivo);
    }

    [Fact]
    public void UnCorreoSinVerificar_SeRechaza_AunqueTodoLoDemasSeaValido()
    {
        var resultado = Validar(Valido(c => c with { CorreoVerificado = false }));
        Assert.Null(resultado.Identidad);
        Assert.Equal(MotivoRechazoGoogle.CorreoSinVerificar, resultado.Motivo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SinSub_SeRechaza(string? sub) =>
        Assert.Equal(MotivoRechazoGoogle.SinIdentidad, Validar(Valido(c => c with { Sub = sub })).Motivo);

    [Fact]
    public void UnSubDemasiadoLargo_SeRechaza() =>
        Assert.Equal(MotivoRechazoGoogle.SinIdentidad, Validar(Valido(c => c with { Sub = new string('1', 256) })).Motivo);

    [Theory]
    [InlineData("12.34")]
    [InlineData("ab cd")]
    [InlineData("ñandú")]
    [InlineData("1234'; DROP TABLE clientes;--")]
    public void UnSubConCaracteresRaros_SeRechaza(string sub) =>
        Assert.Equal(MotivoRechazoGoogle.SinIdentidad, Validar(Valido(c => c with { Sub = sub })).Motivo);

    [Fact]
    public void UnCorreoMasLargoQueElDeLaCuenta_SeRechaza()
    {
        var largo = $"{new string('a', 95)}@correo.test";
        Assert.True(largo.Length > 100 && largo.Length <= 254);
        Assert.Equal(MotivoRechazoGoogle.SinCorreo, Validar(Valido(c => c with { Correo = largo })).Motivo);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    [InlineData(5, 5)]
    [InlineData(99, 5)]
    public void LaEsperaAGoogle_SiempreEstaEntre1Y5Segundos(int configurado, int esperado) =>
        Assert.Equal(TimeSpan.FromSeconds(esperado), new LetrerosHerreraSYNCTIMEapi.Infraestructura.Google.OpcionesGoogle { SegundosLimite = configurado }.Limite);

    [Fact]
    public void LaEsperaAGoogle_PorDefecto_Es5Segundos_YSinIdNoEstaConfigurado()
    {
        var opciones = new LetrerosHerreraSYNCTIMEapi.Infraestructura.Google.OpcionesGoogle();
        Assert.Equal(TimeSpan.FromSeconds(5), opciones.Limite);
        Assert.False(opciones.Configurado);
        Assert.False(new LetrerosHerreraSYNCTIMEapi.Infraestructura.Google.OpcionesGoogle { ClientId = "   " }.Configurado);
        Assert.True(new LetrerosHerreraSYNCTIMEapi.Infraestructura.Google.OpcionesGoogle { ClientId = "x.apps.googleusercontent.com" }.Configurado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-correo")]
    [InlineData("a@b")]
    public void SinCorreoValido_SeRechaza(string? correo) =>
        Assert.Equal(MotivoRechazoGoogle.SinCorreo, Validar(Valido(c => c with { Correo = correo })).Motivo);

    [Theory]
    [InlineData("otro-nonce")]
    [InlineData("")]
    [InlineData(null)]
    public void UnNonceDistinto_OAusente_SeRechaza(string? delToken) =>
        Assert.Equal(MotivoRechazoGoogle.Nonce, Validar(Valido(c => c with { Nonce = delToken })).Motivo);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SiNoSeEsperabaNingunNonce_NadaSeAcepta(string? esperado) =>
        Assert.Equal(MotivoRechazoGoogle.Nonce, Validar(Valido(c => c with { Nonce = esperado }), esperado).Motivo);

    [Fact]
    public void SinNombre_ElNombreSaleDelNombreCompleto_YElApellidoQuedaVacio()
    {
        var resultado = Validar(Valido(c => c with { Nombre = null, Apellido = null, NombreCompleto = "Ana María Pérez Soto" }));
        var identidad = Assert.IsType<IdentidadGoogle>(resultado.Identidad);
        Assert.Equal("Ana María Pérez Soto", identidad.Nombre);
        Assert.Equal("", identidad.Apellido);
    }

    [Fact]
    public void SinNingunNombre_ElNombreQuedaVacio_ParaQueLaPersonaLoEscriba()
    {
        var resultado = Validar(Valido(c => c with { Nombre = null, Apellido = null, NombreCompleto = null }));
        var identidad = Assert.IsType<IdentidadGoogle>(resultado.Identidad);
        Assert.Equal("", identidad.Nombre);
        Assert.Equal("", identidad.Apellido);
    }

    // ── Nonce de un solo uso ──

    [Fact]
    public void ElNonce_SeEmiteAleatorio_YSoloSirveUnaVez()
    {
        var nonces = new NoncesGoogle(TimeProvider.System);
        var primero = nonces.Emitir()!;
        var segundo = nonces.Emitir()!;

        Assert.NotEqual(primero.Nonce, segundo.Nonce);
        Assert.True(primero.Nonce.Length >= 40);
        Assert.True(nonces.Consumir(primero.Nonce));
        Assert.False(nonces.Consumir(primero.Nonce));
        Assert.True(nonces.Consumir(segundo.Nonce));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("inventado")]
    public void UnNonceQueNoSeEmitio_NoSirve(string? nonce) =>
        Assert.False(new NoncesGoogle(TimeProvider.System).Consumir(nonce));

    [Fact]
    public void ElNonce_VenceALos5Minutos()
    {
        var reloj = new RelojManual(Ahora);
        var nonces = new NoncesGoogle(reloj);
        var emitido = nonces.Emitir()!;
        Assert.Equal(Ahora.AddMinutes(5), emitido.Vence);

        reloj.Avanzar(TimeSpan.FromMinutes(5));
        Assert.False(nonces.Consumir(emitido.Nonce));
    }

    [Fact]
    public void ElNonce_AunNoVencido_Sirve()
    {
        var reloj = new RelojManual(Ahora);
        var nonces = new NoncesGoogle(reloj);
        var emitido = nonces.Emitir()!;
        reloj.Avanzar(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));
        Assert.True(nonces.Consumir(emitido.Nonce));
    }

    [Fact]
    public void ElAlmacenDeNonces_TieneTope_YLoLiberaAlVencer()
    {
        var reloj = new RelojManual(Ahora);
        var nonces = new NoncesGoogle(reloj, maximo: 3);
        Assert.NotNull(nonces.Emitir());
        Assert.NotNull(nonces.Emitir());
        Assert.NotNull(nonces.Emitir());
        Assert.Null(nonces.Emitir());

        reloj.Avanzar(TimeSpan.FromMinutes(6));
        Assert.NotNull(nonces.Emitir());
    }

    // ── Pase del registro pendiente ──

    private static readonly byte[] Clave = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private static readonly IdentidadGoogle Identidad = new("109876543210987654321", "ana.perez@correo.test", "Ana", "Pérez");

    [Fact]
    public void ElPase_LlevaLaIdentidad_YVenceALos10Minutos()
    {
        var reloj = new RelojManual(Ahora);
        var pases = new PaseGoogle(Clave, reloj);
        var emitido = pases.Emitir(Identidad);

        Assert.Equal(Ahora.AddMinutes(10), emitido.Vence);
        var leido = Assert.IsType<ContenidoPase>(pases.Leer(emitido.Pase));
        Assert.Equal(Identidad.Sub, leido.Identidad.Sub);
        Assert.Equal(Identidad.Correo, leido.Identidad.Correo);
        Assert.Equal("Ana", leido.Identidad.Nombre);
        Assert.Equal("Pérez", leido.Identidad.Apellido);
    }

    [Fact]
    public void ElPase_Vencido_NoSirve()
    {
        var reloj = new RelojManual(Ahora);
        var pases = new PaseGoogle(Clave, reloj);
        var emitido = pases.Emitir(Identidad);

        reloj.Avanzar(TimeSpan.FromMinutes(9) + TimeSpan.FromSeconds(59));
        Assert.NotNull(pases.Leer(emitido.Pase));
        reloj.Avanzar(TimeSpan.FromSeconds(1));
        Assert.Null(pases.Leer(emitido.Pase));
    }

    [Fact]
    public void ElPase_Manipulado_NoSirve()
    {
        var pases = new PaseGoogle(Clave, new RelojManual(Ahora));
        var emitido = pases.Emitir(Identidad).Pase;
        var (carga, firma) = (emitido.Split('.')[0], emitido.Split('.')[1]);

        // Cambiar un solo caracter de la carga o de la firma, quitarle la firma o agregarle otra parte.
        static string Cambiar(string texto) => texto[..5] + (texto[5] == 'A' ? 'B' : 'A') + texto[6..];
        Assert.Null(pases.Leer($"{Cambiar(carga)}.{firma}"));
        Assert.Null(pases.Leer($"{carga}.{Cambiar(firma)}"));
        Assert.Null(pases.Leer(carga));
        Assert.Null(pases.Leer($"{carga}."));
        Assert.Null(pases.Leer($".{firma}"));
        Assert.Null(pases.Leer($"{carga}.{firma}.{firma}"));
    }

    [Fact]
    public void ElPase_FirmadoConOtraClave_NoSirve()
    {
        var emitido = new PaseGoogle(Enumerable.Repeat((byte)7, 32).ToArray(), new RelojManual(Ahora)).Emitir(Identidad);
        Assert.Null(new PaseGoogle(Clave, new RelojManual(Ahora)).Leer(emitido.Pase));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a.b")]
    [InlineData("%%%.%%%")]
    public void UnTextoCualquiera_NoEsUnPase(string? texto) =>
        Assert.Null(new PaseGoogle(Clave, new RelojManual(Ahora)).Leer(texto));

    [Fact]
    public void UnPaseDemasiadoLargo_NoSeProcesa() =>
        Assert.Null(new PaseGoogle(Clave, new RelojManual(Ahora)).Leer(new string('A', 5000)));

    [Fact]
    public void ElPase_UsadoUnaVez_NoSePuedeUsarOtra()
    {
        var pases = new PaseGoogle(Clave, new RelojManual(Ahora));
        var leido = pases.Leer(pases.Emitir(Identidad).Pase)!;

        Assert.False(pases.EstaUsado(leido));
        pases.MarcarUsado(leido);
        Assert.True(pases.EstaUsado(leido));
    }

    [Fact]
    public void DosPases_DeLaMismaPersona_SonDistintos_YSeUsanPorSeparado()
    {
        var pases = new PaseGoogle(Clave, new RelojManual(Ahora));
        var a = pases.Emitir(Identidad);
        var b = pases.Emitir(Identidad);

        Assert.NotEqual(a.Pase, b.Pase);
        pases.MarcarUsado(pases.Leer(a.Pase)!);
        Assert.False(pases.EstaUsado(pases.Leer(b.Pase)!));
    }

    [Fact]
    public void ElPaseUsado_SeOlvida_CuandoYaVencio()
    {
        var reloj = new RelojManual(Ahora);
        var pases = new PaseGoogle(Clave, reloj);
        var leido = pases.Leer(pases.Emitir(Identidad).Pase)!;
        pases.MarcarUsado(leido);
        Assert.Equal(1, pases.UsadosRecordados);

        reloj.Avanzar(TimeSpan.FromMinutes(11));
        // Un pase vencido ya no sirve por si solo: no hace falta recordarlo.
        pases.Emitir(Identidad);
        Assert.Equal(0, pases.UsadosRecordados);
    }

    // ── Los tres caminos y la regla de no fusionar ──

    [Fact]
    public void ConLaIdentidadYaVinculada_ALaCuentaHabilitada_SeAbreLaSesion() =>
        Assert.Equal(CaminoGoogle.IniciarSesion, ReglasGoogle.Decidir(identidadVinculada: true, cuentaHabilitada: true, correoTieneCuenta: true, correoEsDePersonal: false));

    [Fact]
    public void ConLaIdentidadVinculada_PeroLaCuentaDeBaja_NoEntra() =>
        Assert.Equal(CaminoGoogle.Rechazar, ReglasGoogle.Decidir(identidadVinculada: true, cuentaHabilitada: false, correoTieneCuenta: true, correoEsDePersonal: false));

    [Fact]
    public void ConUnCorreoQueYaTieneCuenta_NuncaSeFusionaSola() =>
        Assert.Equal(CaminoGoogle.CuentaExistente, ReglasGoogle.Decidir(identidadVinculada: false, cuentaHabilitada: false, correoTieneCuenta: true, correoEsDePersonal: false));

    [Fact]
    public void ConUnCorreoNuevo_SePideCompletarElRegistro() =>
        Assert.Equal(CaminoGoogle.RegistroPendiente, ReglasGoogle.Decidir(identidadVinculada: false, cuentaHabilitada: false, correoTieneCuenta: false, correoEsDePersonal: false));

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void SinContrasena_NoSePuedeDesvincular(bool tieneContrasena, bool esperado) =>
        Assert.Equal(esperado, ReglasGoogle.PuedeDesvincular(tieneContrasena));

    [Theory]
    [InlineData("ana@correo.test", "Ana@Correo.TEST ", true)]
    [InlineData("ana@correo.test", "otra@correo.test", false)]
    [InlineData("ana@correo.test", null, false)]
    [InlineData(null, "ana@correo.test", false)]
    public void VincularExige_QueElCorreoDeGoogleSeaElDeLaCuenta(string? deLaCuenta, string? deGoogle, bool esperado) =>
        Assert.Equal(esperado, ReglasGoogle.MismoCorreo(deLaCuenta, deGoogle));
}
