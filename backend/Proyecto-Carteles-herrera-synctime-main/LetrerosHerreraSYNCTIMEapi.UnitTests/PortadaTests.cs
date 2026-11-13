using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using LetrerosHerreraSYNCTIMEapi.Negocio.Portada;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 5: reglas de la portada que no necesitan la base.
public class PortadaTests
{
    // ── «Abierto ahora»: horario semanal (hora de Chile) y cartel manual ──

    private static readonly IReadOnlyList<FranjaHorario> Semana =
    [
        new(1, "Lunes", true, new TimeOnly(9, 0), new TimeOnly(18, 30)),
        new(2, "Martes", true, new TimeOnly(9, 0), new TimeOnly(18, 30)),
        new(3, "Miércoles", true, new TimeOnly(9, 0), new TimeOnly(18, 30)),
        new(4, "Jueves", true, new TimeOnly(9, 0), new TimeOnly(18, 30)),
        new(5, "Viernes", true, new TimeOnly(9, 0), new TimeOnly(18, 30)),
        new(6, "Sábado", true, new TimeOnly(9, 0), new TimeOnly(14, 0)),
        new(7, "Domingo", false, null, null)
    ];

    private static EstadoCartelLocal Cartel(bool abierto) =>
        new(abierto, abierto ? "Local abierto y atendiendo" : "Salí a instalar", abierto ? null : "16:00", new DateTime(2026, 11, 16, 8, 0, 0));

    // El 16-11-2026 es lunes.
    private static DateTime Lunes(int hora, int minuto = 0) => new(2026, 11, 16, hora, minuto, 0);

    [Fact]
    public void DentroDelHorarioYConElCartelAbierto_EstaAbierto()
    {
        var estado = ReglaLocalAbierto.Calcular(Semana, Cartel(true), Lunes(10));

        Assert.True(estado.AbiertoAhora);
        Assert.True(estado.DentroDeHorario);
        Assert.Equal(ReglaLocalAbierto.Abierto, estado.Motivo);
        Assert.Equal("Lunes", estado.Hoy!.NombreDia);
    }

    [Fact]
    public void ConElCartelCerrado_EstaCerradoAunqueSeaHorario()
    {
        var estado = ReglaLocalAbierto.Calcular(Semana, Cartel(false), Lunes(10));

        Assert.False(estado.AbiertoAhora);
        Assert.True(estado.DentroDeHorario);
        Assert.Equal(ReglaLocalAbierto.Ausente, estado.Motivo);
    }

    [Fact]
    public void FueraDelHorario_EstaCerradoAunqueElCartelDigaAbierto_YDiceCuandoAbre()
    {
        var estado = ReglaLocalAbierto.Calcular(Semana, Cartel(true), Lunes(19));

        Assert.False(estado.AbiertoAhora);
        Assert.Equal(ReglaLocalAbierto.FueraDeHorario, estado.Motivo);
        Assert.Equal(("Martes", new TimeOnly(9, 0), false), (estado.ProximaApertura!.NombreDia, estado.ProximaApertura.Hora, estado.ProximaApertura.EsHoy));
    }

    [Fact]
    public void AntesDeAbrir_LaProximaAperturaEsHoy()
    {
        var estado = ReglaLocalAbierto.Calcular(Semana, Cartel(true), Lunes(8, 59));

        Assert.Equal(ReglaLocalAbierto.FueraDeHorario, estado.Motivo);
        Assert.True(estado.ProximaApertura!.EsHoy);
        Assert.Equal(new TimeOnly(9, 0), estado.ProximaApertura.Hora);
    }

    [Fact]
    public void UnDiaSinAtencion_DiceCerradoHoy_YAbreElLunes()
    {
        var domingo = new DateTime(2026, 11, 15, 12, 0, 0);

        var estado = ReglaLocalAbierto.Calcular(Semana, Cartel(true), domingo);

        Assert.Equal(ReglaLocalAbierto.CerradoHoy, estado.Motivo);
        Assert.Equal("Lunes", estado.ProximaApertura!.NombreDia);
    }

    [Theory]
    [InlineData(13, 59, true)]
    [InlineData(14, 0, false)]
    public void LaHoraDeCierre_YaEsCerrado(int hora, int minuto, bool abierto)
    {
        var sabado = new DateTime(2026, 11, 21, hora, minuto, 0);

        Assert.Equal(abierto, ReglaLocalAbierto.Calcular(Semana, Cartel(true), sabado).AbiertoAhora);
    }

    [Fact]
    public void SinCartel_MandaElHorario()
    {
        Assert.True(ReglaLocalAbierto.Calcular(Semana, null, Lunes(10)).AbiertoAhora);
    }

    [Fact]
    public void SinNingunDiaDeAtencion_NoHayProximaApertura()
    {
        var cerrado = Semana.Select(f => f with { Abierto = false }).ToList();

        var estado = ReglaLocalAbierto.Calcular(cerrado, Cartel(true), Lunes(10));

        Assert.False(estado.AbiertoAhora);
        Assert.Null(estado.ProximaApertura);
    }

    [Theory]
    [InlineData("2026-11-13T13:00:00Z", 10)] // horario de verano de Chile: UTC-3
    [InlineData("2026-07-13T13:00:00Z", 9)]  // horario de invierno: UTC-4
    public void LaHoraEsLaDeChile(string utc, int horaChile)
    {
        Assert.Equal(horaChile, HoraChile.Convertir(DateTimeOffset.Parse(utc)).Hour);
    }

    // ── Formulario de contacto (E22) ──

    private static ConsultaContactoEntrada Consulta() => new(
        Nombre: "Pedro Soto",
        Correo: "pedro.soto@correo.cl",
        Telefono: "+56 9 1234 5678",
        Asunto: "Letrero para mi local",
        Mensaje: "Hola, quisiera saber cuánto cuesta un letrero luminoso de dos metros.",
        AceptaPrivacidad: true);

    [Fact]
    public void UnaConsultaCompleta_EsValida()
    {
        Assert.Empty(ValidacionContacto.Validar(Consulta()));
        Assert.Empty(ValidacionContacto.Validar(Consulta() with { Telefono = null }));
    }

    [Fact]
    public void SinAceptarLaPrivacidad_NoSeRecibe()
    {
        var errores = ValidacionContacto.Validar(Consulta() with { AceptaPrivacidad = false });

        Assert.Contains("aceptaPrivacidad", errores.Keys);
    }

    [Theory]
    [InlineData("nombre", "")]
    [InlineData("correo", "no-es-correo")]
    [InlineData("telefono", "123")]
    [InlineData("asunto", " ")]
    [InlineData("mensaje", "Hola")]
    public void CadaCampo_TieneSuMensajeEnEspanol(string campo, string valor)
    {
        var consulta = campo switch
        {
            "nombre" => Consulta() with { Nombre = valor },
            "correo" => Consulta() with { Correo = valor },
            "telefono" => Consulta() with { Telefono = valor },
            "asunto" => Consulta() with { Asunto = valor },
            _ => Consulta() with { Mensaje = valor }
        };

        var errores = ValidacionContacto.Validar(consulta);

        var mensaje = Assert.Single(Assert.Contains(campo, errores));
        Assert.False(string.IsNullOrWhiteSpace(mensaje));
    }

    [Fact]
    public void LosTextosTienenLargoMaximo()
    {
        var errores = ValidacionContacto.Validar(Consulta() with
        {
            Nombre = new string('a', 101),
            Asunto = new string('a', 151),
            Mensaje = new string('a', 2001)
        });

        Assert.Equal(["asunto", "mensaje", "nombre"], errores.Keys.Order());
    }
}
