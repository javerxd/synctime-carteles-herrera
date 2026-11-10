using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// La base de estas pruebas la crean solo las migraciones, igual que un entorno nuevo.
[Collection(ApiCollection.Name)]
public sealed class MigracionTests(ApiFixture api)
{
    private Task<List<string>> Valores(string sql) =>
        api.ConsultarAsync(db => db.Database.SqlQueryRaw<string>(sql).ToListAsync());

    [Fact]
    public async Task Model_HasNoPendingChanges()
    {
        var hayCambios = await api.ConsultarAsync(db => Task.FromResult(db.Database.HasPendingModelChanges()));

        Assert.False(hayCambios, "El modelo cambio y falta generar una migracion.");
    }

    [Fact]
    public async Task FaceMigration_OnlyCreatesItsOwnTable()
    {
        var script = await api.ConsultarAsync(db => Task.FromResult(
            db.GetService<IMigrator>().GenerateScript("20261106031126_ObjetosProgramablesV6", "20261106094459_AgregarRostrosEmpleado")));

        Assert.Contains("CREATE TABLE [rostros_empleado]", script);
        Assert.Contains("CREATE UNIQUE INDEX [uq_rostros_empleado_vigente]", script);
        foreach (var prohibido in new[] { "ALTER TABLE", "DROP ", "UPDATE [", "DELETE FROM" })
            Assert.DoesNotContain(prohibido, script);
    }

    [Fact]
    public async Task Migrations_CreateTriggerViewAndProcedure()
    {
        var objetos = await Valores("SELECT name AS Value FROM sys.objects WHERE type IN ('TR', 'V', 'P')");

        Assert.Contains("trg_cotizacion_materiales_recalcular_total", objetos);
        Assert.Contains("vw_metricas_conversion_online", objetos);
        Assert.Contains("sp_depurar_solicitudes_no_concretadas", objetos);
    }

    [Fact]
    public async Task Migrations_CreateEveryCheckConstraint()
    {
        var restricciones = await Valores("SELECT name AS Value FROM sys.check_constraints");

        Assert.Equal(20, restricciones.Count);
    }

    [Fact]
    public async Task RoleCheck_KeepsEnyeIntact()
    {
        var definicion = Assert.Single(await Valores("SELECT definition AS Value FROM sys.check_constraints WHERE name = 'ck_empleados_rol'"));

        Assert.Contains("dueño", definicion);
        Assert.Contains("diseñador", definicion);
    }

    [Fact]
    public async Task Migrations_CascadeOnlyWhereTheScriptDoes()
    {
        var enCascada = await Valores("SELECT name AS Value FROM sys.foreign_keys WHERE delete_referential_action_desc <> 'NO_ACTION'");

        Assert.Equal(
            ["fk_cot_mat_cotizacion", "fk_cotizaciones_solicitud", "fk_sol_detalles_solicitud"],
            enCascada.Order());
    }

    [Fact]
    public async Task Database_UsesSpanishCollation()
    {
        var collation = Assert.Single(await Valores("SELECT CONVERT(nvarchar(128), collation_name) AS Value FROM sys.databases WHERE name = DB_NAME()"));

        Assert.Equal("Modern_Spanish_CI_AS", collation);
    }

    [Fact]
    public async Task EmployeeEmail_IsRequired()
    {
        var nulable = Assert.Single(await Valores("SELECT CONVERT(nvarchar(5), is_nullable) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('dbo.empleados') AND name = 'email'"));

        Assert.Equal("0", nulable);
    }

    [Fact]
    public async Task SeedData_KeepsAccentsAndClosedSunday()
    {
        var categorias = await api.Cliente().GetAsync("/api/v1/categorias/").EsperarAsync(HttpStatusCode.OK);
        var horarios = await api.Cliente().GetAsync("/api/v1/horarios").EsperarAsync(HttpStatusCode.OK);
        var idCartel = await api.ConsultarAsync(db => db.CartelDigital.AsNoTracking().Select(c => c.IdCartel).SingleAsync());

        Assert.Contains(categorias.EnumerateArray(), c => c.GetProperty("nombre").GetString() == "Vehículos");
        Assert.Equal(7, horarios.GetArrayLength());
        Assert.Equal("Miércoles", DiaSemana(horarios, 3).GetProperty("nombreDia").GetString());
        Assert.Equal("Sábado", DiaSemana(horarios, 6).GetProperty("nombreDia").GetString());
        Assert.False(DiaSemana(horarios, 7).GetProperty("abierto").GetBoolean());
        Assert.Equal(1, idCartel);
    }

    private static JsonElement DiaSemana(JsonElement horarios, int dia) =>
        horarios.EnumerateArray().Single(h => h.GetProperty("diaSemana").GetInt32() == dia);
}
