using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using LetrerosHerreraSYNCTIMEapi.Negocio.Mantenedores;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 9: reglas de los mantenedores, del horario, de la disponibilidad y de los avisos del taller, sin base.
public class MantenedoresYAvisosTests
{
    // ── Avisos del taller ──

    [Theory]
    [InlineData("contacto@gmail.com", "c•••o@gmail.com")]
    [InlineData("ab@taller.cl", "a•••@taller.cl")]
    [InlineData("a@taller.cl", "a•••@taller.cl")]
    public void Enmascarar_DejaLaPrimeraYLaUltimaLetra_YElDominioCompleto(string correo, string esperado) =>
        Assert.Equal(esperado, CorreosDestinosAvisos.Enmascarar(correo));

    [Fact]
    public void LaPrueba_UsaLaPlantillaDelAviso_ConSuPropioTipo_YSinBotonAlPanel()
    {
        var mensaje = AvisoSolicitudNueva.ComponerPrueba("taller@gmail.com", new DateTime(2026, 11, 14, 10, 0, 0), "https://synctime.test");

        Assert.Equal(TiposCorreo.PruebaAvisos, mensaje.Tipo);
        Assert.Equal("Nueva solicitud #0 - Prueba del taller", mensaje.Asunto);
        Assert.Contains("Esto es una prueba", mensaje.Texto);
        Assert.Contains("ficticio", mensaje.Texto);
        Assert.DoesNotContain("/panel/solicitudes/0", mensaje.Html);
        Assert.NotEqual(TiposCorreo.AvisoSolicitudNueva, mensaje.Tipo);
    }

    [Fact]
    public void ElCorreoDeConfirmacion_SaleDeLaCasillaAutomatica_ConElBotonYSinOtraDireccion()
    {
        var mensaje = CorreosDestinosAvisos.Confirmar("nuevo@gmail.com", "Marta", "https://synctime.test/avisos/confirmar?token=abc", "contacto@taller.cl");

        Assert.Equal("Confirma el destino de los avisos de Letreros Herrera", mensaje.Asunto);
        Assert.Contains("Confirmar este correo", mensaje.Html);
        Assert.Contains("no respondas", mensaje.Texto);
        Assert.Equal("contacto@taller.cl", mensaje.ResponderA);
        Assert.Throws<ArgumentException>(() => CorreosDestinosAvisos.Confirmar("nuevo@gmail.com", "Marta", "javascript:alert(1)", "contacto@taller.cl"));
    }

    // ── Personal ──

    [Fact]
    public void NadieSeDesactivaNiCambiaSuRol_ASiMismo()
    {
        Assert.Equal(CodigosMantenedores.Propio, ReglasPersonal.RevisarCambio(1, 1, "administrador", true, "administrador", false, 3, "dueño"));
        Assert.Equal(CodigosMantenedores.Propio, ReglasPersonal.RevisarCambio(1, 1, "administrador", true, "operario", true, 3, "dueño"));
        Assert.Null(ReglasPersonal.RevisarCambio(1, 1, "administrador", true, "administrador", true, 3, "dueño"));
    }

    [Fact]
    public void SiempreQuedaUnDuenoActivo()
    {
        Assert.Equal(CodigosMantenedores.UltimoDueno, ReglasPersonal.RevisarCambio(1, 2, "dueño", true, "dueño", false, 1, "dueño"));
        Assert.Equal(CodigosMantenedores.UltimoDueno, ReglasPersonal.RevisarCambio(1, 2, "dueño", true, "administrador", true, 1, "dueño"));
        Assert.Null(ReglasPersonal.RevisarCambio(1, 2, "dueño", true, "dueño", false, 2, "dueño"));
        // Un dueño ya inactivo no cuenta: reactivarlo o editarlo no choca con la regla.
        Assert.Null(ReglasPersonal.RevisarCambio(1, 2, "dueño", false, "dueño", true, 1, "dueño"));
    }

    [Fact]
    public void ElEmpleado_ValidaRutCorreoTelefonoYRol()
    {
        string[] roles = ["administrador", "dueño", "diseñador", "operario"];
        var errores = ValidacionMantenedores.Empleado(new EntradaEmpleado("12.345.678-9", "", "Pérez", "123", "no-es-correo", "jefe"), roles);
        Assert.Equal(["email", "nombre", "rol", "rut", "telefono"], errores.Keys.Order());
        Assert.Empty(ValidacionMantenedores.Empleado(new EntradaEmpleado("12.345.678-5", "Ana", "Pérez", "9 1234 5678", "ana@taller.cl", "operario"), roles));
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("true", true, true)]
    [InlineData("false", false, true)]
    [InlineData("TODOS", null, true)]
    [InlineData("quizas", null, false)]
    public void ElFiltroActivo_AdmiteTrueFalseYTodos(string? texto, bool? esperado, bool valido)
    {
        Assert.Equal(valido, FiltroActivo.TryLeer(texto, out var activo));
        if (valido)
            Assert.Equal(esperado, activo);
    }

    [Fact]
    public void ElPrecio_DeUnTipoDeLetrero_EsUnMontoEnteroNoNegativo()
    {
        Assert.Contains("precioBaseRef", ValidacionMantenedores.Producto(new EntradaProducto(1, "Letrero", null, 10.5m)).Keys);
        Assert.Contains("precioBaseRef", ValidacionMantenedores.Producto(new EntradaProducto(1, "Letrero", null, -1)).Keys);
        Assert.Empty(ValidacionMantenedores.Producto(new EntradaProducto(1, "Letrero", null, null)));
    }

    // ── Horario y disponibilidad ──

    private static List<EntradaDiaHorario> Semana(string cierreLunes = "18:30") =>
    [
        new(1, true, "09:00", cierreLunes), new(2, true, "09:00", "18:30"), new(3, true, "9:00", "18:30"), new(4, true, "09:00", "18:30"),
        new(5, true, "09:00", "18:30"), new(6, true, "09:00", "14:00"), new(7, false, "10:00", "12:00")
    ];

    [Fact]
    public void ElHorario_ExigeLosSieteDias_YCierreDespuesDeLaApertura()
    {
        var (dias, errores) = ReglasHorario.Validar(Semana());
        Assert.Empty(errores);
        Assert.Null(dias[6].HoraInicio); // un dia cerrado no guarda horas
        Assert.Equal(new TimeOnly(9, 0), dias[2].HoraInicio);

        Assert.Contains("1.horaFin", ReglasHorario.Validar(Semana(cierreLunes: "09:00")).Errores.Keys);
        Assert.Contains("dias", ReglasHorario.Validar(Semana().Take(6).ToList()).Errores.Keys);
    }

    [Fact]
    public void LasFranjas_DentroDelHorario_SinCruces_YNoEnUnDiaCerrado()
    {
        var (horario, _) = ReglasHorario.Validar(Semana());
        Assert.Empty(ReglasHorario.ValidarFranjas([new(1, "09:00", "13:00"), new(1, "13:00", "18:30")], horario).Errores);
        Assert.NotEmpty(ReglasHorario.ValidarFranjas([new(1, "09:00", "13:00"), new(1, "12:00", "14:00")], horario).Errores);
        Assert.NotEmpty(ReglasHorario.ValidarFranjas([new(1, "08:00", "10:00")], horario).Errores);
        Assert.NotEmpty(ReglasHorario.ValidarFranjas([new(7, "10:00", "12:00")], horario).Errores);
        Assert.NotEmpty(ReglasHorario.ValidarFranjas([new(1, "12:00", "10:00")], horario).Errores);
        Assert.NotEmpty(ReglasHorario.ValidarFranjas([new(8, "10:00", "12:00")], horario).Errores);
    }

    [Fact]
    public void UnDiaAbiertoSinHoras_SeTrataComoCerrado_YUnaFranjaNulaEsUnErrorDeSuIndice()
    {
        IReadOnlyList<DiaHorario> horario = [new DiaHorario(1, "Lunes", true, null, null)];
        Assert.Contains("franjas[0].diaSemana", ReglasHorario.ValidarFranjas([new(1, "09:00", "10:00")], horario).Errores.Keys);
        Assert.Contains("franjas[1]", ReglasHorario.ValidarFranjas([new(1, "09:00", "10:00"), null], horario).Errores.Keys);
        Assert.Contains("dias[0]", ReglasHorario.Validar([null]).Errores.Keys);
    }

    [Fact]
    public void LaUnion_JuntaLoQueSeCruzaOSeToca_YDevuelveLosSieteDias()
    {
        var union = ReglasHorario.Unir(
        [
            new(1, new(9, 0), new(12, 0)), new(1, new(11, 0), new(13, 0)), new(1, new(13, 0), new(14, 0)), new(1, new(16, 0), new(18, 0)),
            new(2, new(10, 0), new(11, 0))
        ]);
        Assert.Equal(7, union.Count);
        Assert.Equal([new FranjaAtencion("09:00", "14:00"), new FranjaAtencion("16:00", "18:00")], union[0].Franjas);
        Assert.Single(union[1].Franjas);
        Assert.Empty(union[6].Franjas);
        Assert.Equal("Domingo", union[6].NombreDia);
    }

    // ── La base tambien impide mostrar un logo sin autorizacion ──

    [Fact]
    public void LaReglaDelLogoEnLaBase_SeTraduceA409()
    {
        var problema = LetrerosHerreraSYNCTIMEapi.Middleware.ApiExceptionHandler.TraducirSql(547,
            "The UPDATE statement conflicted with the CHECK constraint \"ck_catalogo_logo\".");
        Assert.Equal(409, problema!.Status);
        Assert.Equal("logo_sin_autorizacion", problema.Extensions["codigo"]);
        Assert.Equal("base_de_datos", problema.Extensions["nivel"]);
    }
}
