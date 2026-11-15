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

    // Fase 4: la migracion solo agrega tablas nuevas. No toca datos ni tablas existentes, asi que no
    // puede fallar por lo que ya haya guardado en la base real cuando se aplique alli.
    [Fact]
    public async Task ServiciosComunes_SoloCreaSusTablas()
    {
        var script = await api.ConsultarAsync(db => Task.FromResult(
            db.GetService<IMigrator>().GenerateScript("20261113010201_HistorialDePagos", "20261113111453_ServiciosComunes")));

        Assert.Contains("CREATE TABLE [correos_salientes]", script);
        Assert.Contains("CREATE TABLE [consentimientos]", script);
        Assert.Contains("CREATE TABLE [imagenes]", script);
        foreach (var prohibido in new[] { "ALTER TABLE", "DROP ", "UPDATE [", "DELETE FROM" })
            Assert.DoesNotContain(prohibido, script);
    }

    // Fase 5: agrega columnas y una tabla. No borra tablas, columnas ni filas, asi que tampoco puede
    // perder datos de la base real al aplicarse alli.
    [Fact]
    public async Task PortadaPublica_SoloAgrega()
    {
        var script = await api.ConsultarAsync(db => Task.FromResult(
            db.GetService<IMigrator>().GenerateScript("20261113111453_ServiciosComunes", "20261113154234_PortadaPublica")));

        Assert.Contains("CREATE TABLE [consultas_contacto]", script);
        foreach (var prohibido in new[] { "DROP TABLE", "DROP COLUMN", "DELETE FROM", "UPDATE [" })
            Assert.DoesNotContain(prohibido, script);
    }

    // Fase 6: solo crea las tablas de sesiones y codigos, agrega una columna y una regla. En la base real
    // no hay cuentas de cliente sin correo (consultado el 13-11-2026), asi que la regla no choca con datos.
    [Fact]
    public async Task CuentasYSesion_SoloAgrega()
    {
        var script = await api.ConsultarAsync(db => Task.FromResult(
            db.GetService<IMigrator>().GenerateScript("20261113154234_PortadaPublica", "20261114002321_CuentasYSesion")));

        Assert.Contains("CREATE TABLE [tokens_renovacion]", script);
        Assert.Contains("CREATE TABLE [codigos_cuenta]", script);
        Assert.Contains("ck_clientes_cuenta_con_correo", script);
        foreach (var prohibido in new[] { "DROP TABLE", "DROP COLUMN", "DELETE FROM", "UPDATE [" })
            Assert.DoesNotContain(prohibido, script);
    }

    // Fase 7: crea adjuntos_solicitud y solicitudes_historial, agrega solicitudes.huella_ip y amplia tres
    // textos (descripcion general 500 -> 2000, motivo 250 -> 500, descripcion del item 300 -> 500). Ampliar
    // no pierde datos; en la base demo se consulto antes (14-11-2026) que nada choca con la migracion.
    [Fact]
    public async Task SolicitudesYAdjuntos_SoloAgrega()
    {
        var script = await api.ConsultarAsync(db => Task.FromResult(
            db.GetService<IMigrator>().GenerateScript("20261114002321_CuentasYSesion", "20261114035839_SolicitudesYAdjuntos")));

        Assert.Contains("CREATE TABLE [adjuntos_solicitud]", script);
        Assert.Contains("CREATE TABLE [solicitudes_historial]", script);
        Assert.Contains("ADD [huella_ip] char(64) NULL", script);
        Assert.Contains("ALTER COLUMN [descripcion_general] nvarchar(2000) NULL", script);
        Assert.Contains("ALTER COLUMN [motivo_rechazo] nvarchar(500) NULL", script);
        Assert.Contains("ALTER COLUMN [descripcion] nvarchar(500) NOT NULL", script);
        foreach (var prohibido in new[] { "DROP TABLE", "DROP COLUMN", "DELETE FROM", "UPDATE [" })
            Assert.DoesNotContain(prohibido, script);
    }

    // Fase 8: agrega columnas, la tabla pedidos_historial, indices unicos y reglas, y reescribe el trigger de
    // pagos (los abonos anulados no suman). Amplia tres textos. Antes de cada regla completa los datos que ya
    // existen (respuesta de las cotizaciones respondidas, motivo de los pedidos anulados, historial de cada
    // pedido); en la base demo se consulto antes (14-11-2026) que nada choca. No borra tablas, columnas ni filas.
    [Fact]
    public async Task CotizacionesYPedidos_SoloAgrega()
    {
        const string ultima = Fase8;
        Assert.Contains(ultima, await api.ConsultarAsync(db => Task.FromResult(db.Database.GetMigrations().ToList())));
        var script = await api.ConsultarAsync(db => Task.FromResult(
            db.GetService<IMigrator>().GenerateScript("20261114035839_SolicitudesYAdjuntos", ultima)));

        Assert.Contains("CREATE TABLE [pedidos_historial]", script);
        Assert.Contains("ADD [row_version] rowversion NOT NULL", script);
        Assert.Contains("CREATE UNIQUE INDEX [uq_cotizaciones_una_pendiente]", script);
        Assert.Contains("CREATE UNIQUE INDEX [uq_pedidos_cotizacion]", script);
        Assert.Contains("ALTER COLUMN [observaciones] nvarchar(2000) NULL", script);
        Assert.Contains("WHERE anulado = 0", script);
        // Cada regla nueva va despues del relleno que la hace cumplir.
        Assert.True(script.IndexOf("via_respuesta = N'presencial'", StringComparison.Ordinal) < script.IndexOf("ck_cotizaciones_respuesta", StringComparison.Ordinal));
        Assert.True(script.IndexOf("N'Anulado antes de la fase 8'", StringComparison.Ordinal) < script.IndexOf("ck_pedidos_anulacion", StringComparison.Ordinal));
        foreach (var prohibido in new[] { "DROP TABLE", "DROP COLUMN", "DELETE FROM" })
            Assert.DoesNotContain(prohibido, script, StringComparison.Ordinal);
    }

    // Revertir la fase 8 nunca destruye datos en silencio: si hay abonos anulados o textos que no caben en las
    // columnas anteriores, se detiene (THROW) antes de tocar nada. No borra filas.
    [Fact]
    public async Task RevertirCotizacionesYPedidos_SeDetieneAntesDePerderDatos()
    {
        const string ultima = Fase8;
        var abajo = await api.ConsultarAsync(db => Task.FromResult(
            db.GetService<IMigrator>().GenerateScript(ultima, "20261114035839_SolicitudesYAdjuntos")));

        Assert.Contains("THROW 50081", abajo, StringComparison.Ordinal);
        Assert.Contains("THROW 50082", abajo, StringComparison.Ordinal);
        // El unico DELETE es el del propio historial de migraciones de EF.
        Assert.Equal(["DELETE FROM [__EFMigrationsHistory]"],
            System.Text.RegularExpressions.Regex.Matches(abajo, @"DELETE FROM \S+").Select(m => m.Value));
        Assert.True(abajo.IndexOf("THROW 50082", StringComparison.Ordinal) < abajo.IndexOf("DROP COLUMN", StringComparison.Ordinal));
    }

    private const string Fase8 = "20261114220104_CotizacionesYPedidos";

    // Fase 9: la migracion MantenedoresYAvisos solo agrega (columnas de version, tablas nuevas, indices y
    // reglas) y amplia textos. No borra tablas, columnas ni filas.
    [Fact]
    public async Task MantenedoresYAvisos_SoloAgrega()
    {
        var ultima = await api.ConsultarAsync(db => Task.FromResult(db.Database.GetMigrations().Last()));
        Assert.EndsWith("_MantenedoresYAvisos", ultima);
        var script = await api.ConsultarAsync(db => Task.FromResult(db.GetService<IMigrator>().GenerateScript(Fase8, ultima)));

        Assert.Contains("CREATE TABLE [destinatarios_avisos]", script);
        Assert.Contains("CREATE TABLE [destinatarios_avisos_historial]", script);
        Assert.Contains("CREATE TABLE [disponibilidad_empleados]", script);
        Assert.Contains("CREATE UNIQUE INDEX [uq_destinatarios_avisos_vigente]", script);
        Assert.Contains("ck_horarios_cerrado", script);
        foreach (var tabla in new[] { "categorias", "productos", "catalogo", "materiales", "empleados", "clientes" })
            Assert.Contains($"ALTER TABLE [{tabla}] ADD [row_version] rowversion NOT NULL", script);
        foreach (var prohibido in new[] { "DROP TABLE", "DROP COLUMN", "DELETE FROM" })
            Assert.DoesNotContain(prohibido, script, StringComparison.Ordinal);
    }

    // Revertir la fase 9 se detiene (THROW) si ya hay destinos de avisos, historial o franjas guardados, o
    // textos que no caben en las columnas anteriores. No borra filas.
    [Fact]
    public async Task RevertirMantenedoresYAvisos_SeDetieneAntesDePerderDatos()
    {
        var ultima = await api.ConsultarAsync(db => Task.FromResult(db.Database.GetMigrations().Last()));
        var abajo = await api.ConsultarAsync(db => Task.FromResult(db.GetService<IMigrator>().GenerateScript(ultima, Fase8)));

        Assert.Contains("THROW 50090", abajo, StringComparison.Ordinal);
        Assert.Contains("THROW 50091", abajo, StringComparison.Ordinal);
        Assert.Equal(["DELETE FROM [__EFMigrationsHistory]"],
            System.Text.RegularExpressions.Regex.Matches(abajo, @"DELETE FROM \S+").Select(m => m.Value));
        Assert.True(abajo.IndexOf("THROW 50090", StringComparison.Ordinal) < abajo.IndexOf("DROP TABLE", StringComparison.Ordinal));
    }

    // Fase 8: un abono anulado deja de sumar, y la base no deja reactivarlo.
    [Fact]
    public async Task ElTrigger_NoSumaLosAbonosAnulados_YNoDejaReactivarlos()
    {
        var pedido = await api.CrearPedidoAsync(50_000);
        var id = pedido.GetProperty("idPedido").GetInt32();
        var idAdmin = await api.IdAdminAsync();
        await api.ConsultarAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.pagos (id_pedido, monto, medio_pago, id_empleado_registra) VALUES ({id}, 20000, N'efectivo', {idAdmin}), ({id}, 5000, N'efectivo', {idAdmin})"));
        Task<int> AbonadoAsync() => api.ConsultarAsync(db => db.Pedidos.AsNoTracking().Where(p => p.IdPedido == id).Select(p => p.MontoAbonado).SingleAsync());
        Assert.Equal(25_000, await AbonadoAsync());

        await api.ConsultarAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.pagos SET anulado = 1, motivo_anulacion = N'Mal ingresado', id_empleado_anula = {idAdmin}, fecha_anulacion = SYSDATETIMEOFFSET()
            WHERE id_pedido = {id} AND monto = 5000
            """));
        Assert.Equal(20_000, await AbonadoAsync());

        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => api.ConsultarAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.pagos SET anulado = 0, motivo_anulacion = NULL, id_empleado_anula = NULL, fecha_anulacion = NULL
            WHERE id_pedido = {id} AND monto = 5000
            """)));
        Assert.Equal(20_000, await AbonadoAsync());
    }

    [Fact]
    public async Task Migrations_CreateTriggerViewAndProcedure()
    {
        var objetos = await Valores("SELECT name AS Value FROM sys.objects WHERE type IN ('TR', 'V', 'P')");

        Assert.Contains("trg_cotizacion_materiales_recalcular_total", objetos);
        Assert.Contains("trg_pagos_recalcular_monto_abonado", objetos);
        Assert.Contains("vw_metricas_conversion_online", objetos);
        Assert.Contains("sp_depurar_solicitudes_no_concretadas", objetos);
    }

    [Fact]
    public async Task Migrations_CreateEveryCheckConstraint()
    {
        var restricciones = await Valores("SELECT name AS Value FROM sys.check_constraints");

        Assert.Equal(59, restricciones.Count);
        // Fase 9: un dia cerrado no guarda horas; franjas validas; destinos de avisos con estado, enlace y correo.
        foreach (var nombre in new[] { "ck_horarios_cerrado", "ck_disponibilidad_dia", "ck_disponibilidad_rango", "ck_destinatarios_avisos_estado",
                     "ck_destinatarios_avisos_enlace", "ck_destinatarios_avisos_correo", "ck_destinatarios_avisos_historial_accion" })
            Assert.Contains(nombre, restricciones);
        // Fase 8: montos enteros en la cotizacion, respuesta con fecha y via, anulacion de pedidos y abonos con
        // sus datos, y estados validos en el historial del pedido.
        foreach (var nombre in new[] { "ck_cotizaciones_montos_enteros", "ck_cotizaciones_respuesta", "ck_cotizaciones_via", "ck_cot_mat_costo_entero",
                     "ck_pedidos_anulacion", "ck_pagos_anulacion", "ck_pedidos_historial_estados" })
            Assert.Contains(nombre, restricciones);
        // Fase 7: un adjunto tiene un solo autor, solo esta vinculado con fecha, y es imagen (con medidas) o PDF.
        foreach (var nombre in new[] { "ck_adjuntos_solicitud_autor", "ck_adjuntos_solicitud_vinculo", "ck_adjuntos_solicitud_tipo",
                     "ck_adjuntos_solicitud_bytes", "ck_adjuntos_solicitud_medidas", "ck_solicitudes_historial_estados", "ck_solicitudes_historial_actor" })
            Assert.Contains(nombre, restricciones);
        // Fase 6: no hay cuenta web de cliente sin correo.
        Assert.Contains("ck_clientes_cuenta_con_correo", restricciones);
        // Fase 5: un logo de cliente no se muestra sin autorizacion, y la autorizacion dice quien y cuando.
        Assert.Contains("ck_catalogo_logo", restricciones);
        Assert.Contains("ck_catalogo_autorizacion_logo", restricciones);
        // Fase 4: cola de correo, consentimientos e imagenes.
        Assert.Contains("ck_correos_salientes_estado", restricciones);
        Assert.Contains("ck_correos_salientes_intentos", restricciones);
        Assert.Contains("ck_consentimientos_tipo", restricciones);
        Assert.Contains("ck_consentimientos_titular", restricciones);
        Assert.Contains("ck_consentimientos_revocacion", restricciones);
        Assert.Contains("ck_imagenes_medidas", restricciones);
        Assert.Contains("ck_pedidos_montos_enteros", restricciones);
        // Historial de abonos: cada pago es positivo y usa un medio conocido.
        Assert.Contains("ck_pagos_monto", restricciones);
        Assert.Contains("ck_pagos_medio_pago", restricciones);
        // Revision de la profesora guia: el pedido separa su estado operativo de su estado de pago.
        Assert.Contains("ck_pedidos_estado_pedido", restricciones);
        Assert.Contains("ck_pedidos_monto_abonado", restricciones);
        Assert.Contains("ck_pedidos_origen", restricciones);
        Assert.DoesNotContain("ck_pedidos_estado", restricciones);
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

        // Fase 6: las sesiones y los codigos de una cuenta desaparecen con ella.
        // Fase 7: los adjuntos y el historial de una solicitud se van con ella (sp_depurar_solicitudes_no_concretadas).
        Assert.Equal(
            ["fk_adjuntos_solicitud_solicitud", "fk_codigos_cuenta_cliente", "fk_codigos_cuenta_empleado", "fk_cot_mat_cotizacion", "fk_cotizaciones_solicitud",
             "fk_sol_detalles_solicitud", "fk_solicitudes_historial_solicitud", "fk_tokens_renovacion_cliente", "fk_tokens_renovacion_empleado"],
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
        var categorias = await api.Cliente().GetAsync("/api/v1/categorias/?tamano=100").EsperarAsync(HttpStatusCode.OK);
        var horarios = await api.Cliente().GetAsync("/api/v1/horarios").EsperarAsync(HttpStatusCode.OK);
        var idCartel = await api.ConsultarAsync(db => db.CartelDigital.AsNoTracking().Select(c => c.IdCartel).SingleAsync());

        Assert.Contains(categorias.GetProperty("elementos").EnumerateArray(), c => c.GetProperty("nombre").GetString() == "Vehículos");
        Assert.Equal(7, horarios.GetArrayLength());
        Assert.Equal("Miércoles", DiaSemana(horarios, 3).GetProperty("nombreDia").GetString());
        Assert.Equal("Sábado", DiaSemana(horarios, 6).GetProperty("nombreDia").GetString());
        Assert.False(DiaSemana(horarios, 7).GetProperty("abierto").GetBoolean());
        Assert.Equal(1, idCartel);
    }

    private static JsonElement DiaSemana(JsonElement horarios, int dia) =>
        horarios.EnumerateArray().Single(h => h.GetProperty("diaSemana").GetInt32() == dia);
}
