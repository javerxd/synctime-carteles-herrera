using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Mantenedores;
using LetrerosHerreraSYNCTIMEapi.Negocio.Portada;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 12D: regla de nombres y apellidos. Los casos viven en CasosNombre.json, el mismo archivo que lee
// el frontend (src/test/reglaNombreCasosCompartidos.test.js), para que los dos lados no se contradigan.
public class ReglaNombreTests
{
    private static readonly JsonElement Datos = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CasosNombre.json"))).RootElement;

    public static TheoryData<string, string?, string?> Casos()
    {
        var datos = new TheoryData<string, string?, string?>();
        foreach (var caso in Datos.GetProperty("casos").EnumerateArray())
        {
            var valor = caso.GetProperty("valor");
            var codigo = caso.GetProperty("codigo");
            datos.Add(
                caso.GetProperty("nombre").GetString()!,
                valor.ValueKind == JsonValueKind.Null ? null : valor.GetString(),
                codigo.ValueKind == JsonValueKind.Null ? null : codigo.GetString());
        }
        return datos;
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void ElCodigo_CoincideConLaTablaCompartida(string _, string? valor, string? codigo) =>
        Assert.Equal(codigo, ReglaNombre.Revisar(valor));

    [Fact]
    public void LosLimites_CoincidenConLaTablaCompartida()
    {
        Assert.Equal(ReglaNombre.MaximoCaracteres, Datos.GetProperty("maximoCaracteres").GetInt32());
        Assert.Equal(ReglaNombre.MaximoEspacios, Datos.GetProperty("maximoEspacios").GetInt32());
    }

    [Fact]
    public void LosMensajes_CoincidenConLaTablaCompartida()
    {
        foreach (var mensaje in Datos.GetProperty("mensajes").EnumerateObject())
            Assert.Equal(mensaje.Value.GetString(), ReglaNombre.Mensaje(mensaje.Name));
    }

    [Fact]
    public void ElError_UsaElMensajeDelFormulario_CuandoFaltaElDato()
    {
        Assert.Equal("Escribe tu nombre.", ReglaNombre.Error(null, "Escribe tu nombre."));
        Assert.Equal("Escribe tu nombre.", ReglaNombre.Error("   ", "Escribe tu nombre."));
        Assert.Null(ReglaNombre.Error("María José", "Escribe tu nombre."));
        Assert.Equal(ReglaNombre.Mensaje("caracteres"), ReglaNombre.Error("Ana2", "Escribe tu nombre."));
    }

    // ── La regla se aplica en cada formulario con nombre o apellido ──

    [Fact]
    public void LaCuenta_RechazaNombresYApellidosFueraDeLaRegla()
    {
        var errores = ValidacionCuenta.Validar(null, "Pérez-Gil", "Abcdefghijklmnopqrstu", "912345678", null);

        Assert.Equal(["apellido", "nombre"], errores.Keys.Order());
        Assert.Equal(ReglaNombre.Mensaje("caracteres"), errores["nombre"][0]);
        Assert.Equal(ReglaNombre.Mensaje("largo"), errores["apellido"][0]);
    }

    [Fact]
    public void LaCuenta_AceptaNombresConTildeYParticula() =>
        Assert.Empty(ValidacionCuenta.Validar(null, "María José", "De la Fuente", "912345678", null));

    [Fact]
    public void LaCuenta_NoCambiaLoEscrito_RechazaEspaciosEnLosBordes()
    {
        var errores = ValidacionCuenta.Validar(null, " Camila", "Rojas ", "912345678", null);

        Assert.Equal(ReglaNombre.Mensaje("bordes"), errores["nombre"][0]);
        Assert.Equal(ReglaNombre.Mensaje("bordes"), errores["apellido"][0]);
    }

    [Fact]
    public void LaCuenta_SigueExigiendoNombreYApellido()
    {
        var errores = ValidacionCuenta.Validar(null, "", null, "912345678", null);

        Assert.Equal("Escribe tu nombre.", errores["nombre"][0]);
        Assert.Equal("Escribe tu apellido.", errores["apellido"][0]);
    }

    [Fact]
    public void ElEmpleado_RechazaNombresFueraDeLaRegla()
    {
        var roles = new[] { "dueño", "administrador", "diseñador", "operario" };
        var valido = new EntradaEmpleado("12.345.678-5", "Camila", "Rojas", "912345678", "camila@letrerosherrera.cl", "diseñador");
        Assert.Empty(ValidacionMantenedores.Empleado(valido, roles));

        var errores = ValidacionMantenedores.Empleado(valido with { Nombre = "Ana2", Apellido = "O'Higgins" }, roles);
        Assert.Equal(["apellido", "nombre"], errores.Keys.Order());
    }

    [Fact]
    public void LaConsulta_RechazaUnNombreConSimbolos()
    {
        var consulta = new ConsultaContactoEntrada("Pedro Soto", "pedro@correo.cl", null, "Cotización", "Quiero un letrero para mi local.", true);
        Assert.Empty(ValidacionContacto.Validar(consulta));

        var errores = ValidacionContacto.Validar(consulta with { Nombre = "Pedro_Soto 2" });
        Assert.Equal(ReglaNombre.Mensaje("caracteres"), errores["nombre"][0]);
    }
}
