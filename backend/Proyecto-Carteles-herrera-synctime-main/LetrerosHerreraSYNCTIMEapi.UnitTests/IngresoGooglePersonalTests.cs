using LetrerosHerreraSYNCTIMEapi.Negocio.IngresoGoogle;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 14C (RFS03, RFU05, RNFE02): el ingreso con Google ahora también es del personal. Aquí, la regla pura que decide a qué
// camino lleva una identidad de Google cuando el correo puede ser el de un empleado. Sin base ni Google.
public class IngresoGooglePersonalTests
{
    [Fact]
    public void UnCorreoDeEmpleado_SinVincular_NoSeFusionaNiSeOfreceRegistroDeCliente()
    {
        var camino = ReglasGoogle.Decidir(identidadVinculada: false, cuentaHabilitada: false, correoTieneCuenta: false, correoEsDePersonal: true);

        Assert.Equal(CaminoGoogle.CuentaPersonalExistente, camino);
        Assert.NotEqual(CaminoGoogle.RegistroPendiente, camino);
        Assert.NotEqual(CaminoGoogle.IniciarSesion, camino);
    }

    [Fact]
    public void UnaIdentidadVinculada_IniciaSesion_AunqueSuCorreoSeaDeUnEmpleado()
    {
        Assert.Equal(CaminoGoogle.IniciarSesion,
            ReglasGoogle.Decidir(identidadVinculada: true, cuentaHabilitada: true, correoTieneCuenta: false, correoEsDePersonal: true));
    }

    [Fact]
    public void UnaIdentidadVinculadaAUnaCuentaNoHabilitada_SeRechaza_AunqueElCorreoSeaDeUnEmpleado()
    {
        Assert.Equal(CaminoGoogle.Rechazar,
            ReglasGoogle.Decidir(identidadVinculada: true, cuentaHabilitada: false, correoTieneCuenta: false, correoEsDePersonal: true));
    }

    [Fact]
    public void SiElCorreoEsDeUnClienteYDeUnEmpleado_MandaLaCuentaDelCliente()
    {
        // La persona puede vincular Google a su cuenta de cliente desde Mi cuenta; el personal lo hace desde Mi perfil.
        Assert.Equal(CaminoGoogle.CuentaExistente,
            ReglasGoogle.Decidir(identidadVinculada: false, cuentaHabilitada: false, correoTieneCuenta: true, correoEsDePersonal: true));
    }

    [Fact]
    public void UnCorreoQueNoEsDeNadie_SigueSiendoUnRegistroPendiente()
    {
        Assert.Equal(CaminoGoogle.RegistroPendiente,
            ReglasGoogle.Decidir(identidadVinculada: false, cuentaHabilitada: false, correoTieneCuenta: false, correoEsDePersonal: false));
    }

    [Fact]
    public void LasReglasDeAntes_NoCambian_ConUnCorreoQueNoEsDeNingunEmpleado()
    {
        Assert.Equal(CaminoGoogle.IniciarSesion, ReglasGoogle.Decidir(true, true, false, false));
        Assert.Equal(CaminoGoogle.Rechazar, ReglasGoogle.Decidir(true, false, false, false));
        Assert.Equal(CaminoGoogle.CuentaExistente, ReglasGoogle.Decidir(false, false, true, false));
        Assert.Equal(CaminoGoogle.RegistroPendiente, ReglasGoogle.Decidir(false, false, false, false));
    }

    [Fact]
    public void ElPersonal_SiempreSePuedeDesvincular_PorqueTodoEmpleadoTieneContrasena()
    {
        Assert.True(ReglasGoogle.PuedeDesvincular(tieneContrasena: true));
    }
}
