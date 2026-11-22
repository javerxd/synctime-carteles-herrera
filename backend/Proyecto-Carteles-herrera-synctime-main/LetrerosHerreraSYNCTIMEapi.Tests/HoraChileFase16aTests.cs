using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 16A (H3): las fechas de pedidos, solicitudes, cotizaciones y pagos se guardan SIN zona (datetime2) y las vistas de
// indicadores las cuentan como «dias de Chile». Hasta ahora eso era cierto solo porque SQL Server corria en hora de Chile
// (SYSDATETIME()). Azure SQL y un contenedor corren en UTC: sin esta migracion, una solicitud hecha a las 21:30 en Chile
// quedaria con la fecha del dia siguiente y los indicadores se correrian entre 3 y 4 horas.
//
// El servidor de pruebas es el de este equipo (hora de Chile), asi que no se puede cambiar su reloj. Se prueba de otra manera,
// que no depende de la zona del servidor: se toma la expresion REAL de cada valor por defecto y se le da un reloj en UTC
// inventado (se reemplaza SYSDATETIMEOFFSET() por una fecha fija con +00:00), como lo haria un servidor en UTC.
[Collection(ApiCollection.Name)]
public sealed class HoraChileFase16aTests(ApiFixture api)
{
    private const string MigracionAnterior = "20261121161200_GoogleParaPersonal";
    private const string MigracionHoraDeChile = "20261122073024_HoraDeChileEnLaBase";

    public sealed class ValorPorDefecto
    {
        public string Tabla { get; set; } = "";
        public string Columna { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Definicion { get; set; } = "";
    }

    private Task<List<ValorPorDefecto>> ValoresPorDefectoAsync() =>
        api.ConsultarAsync(db => db.Database.SqlQueryRaw<ValorPorDefecto>("""
            SELECT OBJECT_NAME(dc.parent_object_id) AS Tabla, c.name AS Columna, TYPE_NAME(c.user_type_id) AS Tipo, dc.definition AS Definicion
            FROM sys.default_constraints dc
            JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE OBJECTPROPERTY(dc.parent_object_id, 'SchemaId') = SCHEMA_ID('dbo')
            """).ToListAsync());

    // Las columnas «sin zona»: son las que dependen del reloj del servidor. Las de tipo datetimeoffset guardan el instante y no.
    private async Task<List<ValorPorDefecto>> ColumnasSinZonaAsync() =>
        (await ValoresPorDefectoAsync()).Where(v => v.Tipo is "datetime2" or "date" or "datetime" or "smalldatetime").ToList();

    [Fact]
    public async Task HayColumnasSinZonaConValorPorDefecto_Y_ElTestLasEncuentra()
    {
        // Guarda contra una prueba que no mire nada: las fechas de las tablas del negocio estan en esta lista.
        var columnas = (await ColumnasSinZonaAsync()).Select(v => $"{v.Tabla}.{v.Columna}").ToList();

        Assert.Contains("pedidos.fecha_pedido", columnas);
        Assert.Contains("solicitudes.fecha_solicitud", columnas);
        Assert.Contains("cotizaciones.fecha_cotizacion", columnas);
        Assert.Contains("pagos.fecha_pago", columnas);
        Assert.True(columnas.Count >= 8, $"Se esperaban al menos 8 columnas con la hora del servidor y hay {columnas.Count}.");
    }

    [Fact]
    public async Task NingunaColumnaSinZona_UsaLaHoraLocalDelServidor()
    {
        var culpables = (await ColumnasSinZonaAsync())
            .Where(v => EsRelojLocal(v.Definicion))
            .Select(v => $"{v.Tabla}.{v.Columna} = {v.Definicion}")
            .ToList();

        Assert.True(culpables.Count == 0,
            "Estas columnas guardan la hora local del servidor (UTC en Azure SQL y en un contenedor): " + string.Join("; ", culpables));
    }

    // SQL Server guarda las funciones en minusculas; «sysdatetimeoffset()» no contiene «sysdatetime()».
    private static bool EsRelojLocal(string definicion)
    {
        var d = definicion.ToLowerInvariant();
        return d.Contains("sysdatetime()") || d.Contains("getdate()") || d.Contains("current_timestamp");
    }

    // Instantes en UTC que cruzan la medianoche de Chile o cambian con el horario de verano.
    [Theory]
    [InlineData("2026-01-15T15:00:00", "2026-01-15T12:00:00")] // verano en Chile: UTC-3
    [InlineData("2026-07-15T15:00:00", "2026-07-15T11:00:00")] // invierno en Chile: UTC-4
    [InlineData("2026-01-15T02:30:00", "2026-01-14T23:30:00")] // en UTC ya es el dia siguiente; en Chile todavia no
    [InlineData("2026-07-15T03:30:00", "2026-07-14T23:30:00")]
    // Cambios de horario de Chile (la noche del primer sabado de abril y de septiembre): se recorre un limite exacto de cada uno.
    [InlineData("2026-04-05T02:59:59", "2026-04-04T23:59:59")] // aun verano (UTC-3)
    [InlineData("2026-04-05T03:00:00", "2026-04-04T23:00:00")] // vuelve a UTC-4: la hora 23 se repite
    [InlineData("2026-09-06T03:59:59", "2026-09-05T23:59:59")] // aun invierno (UTC-4)
    [InlineData("2026-09-06T04:00:00", "2026-09-06T01:00:00")] // salta a UTC-3: la hora 00 no existe
    public async Task ConElRelojDelServidorEnUtc_CadaColumnaSinZonaGuardaLaHoraDeChile(string utc, string chileEsperado)
    {
        var esperado = DateTime.Parse(chileEsperado, CultureInfo.InvariantCulture);
        var fallos = new List<string>();

        foreach (var v in await ColumnasSinZonaAsync())
        {
            // El servidor «en UTC»: el reloj se reemplaza por un instante fijo con desfase +00:00.
            var conRelojUtc = v.Definicion.Replace("sysdatetimeoffset()", $"CAST(N'{utc}+00:00' AS datetimeoffset(7))", StringComparison.OrdinalIgnoreCase);
#pragma warning disable EF1002 // La expresion sale de sys.default_constraints de la base de pruebas, no de una persona.
            var valor = await api.ConsultarAsync(db => db.Database.SqlQueryRaw<DateTime>($"SELECT {conRelojUtc} AS Value").SingleAsync());
#pragma warning restore EF1002

            var correcto = v.Tipo == "date" ? valor.Date == esperado.Date : valor == esperado;
            if (!correcto)
                fallos.Add($"{v.Tabla}.{v.Columna}: con el reloj en {utc} UTC guarda {valor:s} y debia guardar {(v.Tipo == "date" ? esperado.ToString("yyyy-MM-dd") : chileEsperado)}");
        }

        Assert.True(fallos.Count == 0, string.Join(Environment.NewLine, fallos));
    }

    [Fact]
    public async Task VistasTriggersProcedimientosYColumnasCalculadas_NoUsanLaHoraLocalDelServidor()
    {
        // Lo que no es un valor por defecto tambien podria depender de la zona del servidor. Hoy nada lo hace: esta prueba lo cuida.
        var usos = await api.ConsultarAsync(db => db.Database.SqlQueryRaw<string>("""
            SELECT OBJECT_NAME(m.object_id) AS Value
            FROM sys.sql_modules m
            WHERE OBJECTPROPERTY(m.object_id, 'SchemaId') = SCHEMA_ID('dbo')
              AND (LOWER(m.definition) LIKE '%sysdatetime()%' OR LOWER(m.definition) LIKE '%getdate()%' OR LOWER(m.definition) LIKE '%current_timestamp%')
            UNION ALL
            SELECT OBJECT_NAME(cc.object_id) + '.' + cc.name
            FROM sys.computed_columns cc
            WHERE LOWER(cc.definition) LIKE '%sysdatetime()%' OR LOWER(cc.definition) LIKE '%getdate()%'
            """).ToListAsync());

        Assert.True(usos.Count == 0, "Dependen de la hora local del servidor: " + string.Join(", ", usos));
    }

    [Fact]
    public async Task LaMigracion_SeNiegaAAplicarse_SiElServidorNoConoceLaZonaDeChile()
    {
        var script = await api.ConsultarAsync(db => Task.FromResult(
            db.GetService<IMigrator>().GenerateScript(MigracionAnterior, MigracionHoraDeChile)));

        // El guardia va ANTES de cambiar el primer valor por defecto.
        var guardia = script.IndexOf("sys.time_zone_info", StringComparison.Ordinal);
        Assert.True(guardia >= 0, "Falta la comprobacion de la zona horaria.");
        Assert.True(guardia < script.IndexOf("ADD DEFAULT", StringComparison.Ordinal), "La comprobacion debe ir antes de cambiar los valores por defecto.");
        Assert.Contains("THROW", script);
        // Y la zona existe en el servidor de estas pruebas (en el contenedor de la 16B y en Azure SQL se comprueba igual).
        var existe = await api.ConsultarAsync(db => db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sys.time_zone_info WHERE name = N'Pacific SA Standard Time'").SingleAsync());
        Assert.Equal(1, existe);
    }

    [Fact]
    public async Task LaDepuracionDeSolicitudes_ComparaConLaHoraDeChile()
    {
        var definicion = (await api.ConsultarAsync(db => db.Database
            .SqlQueryRaw<string>("SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_depurar_solicitudes_no_concretadas')) AS Value").SingleAsync()))
            .ToLowerInvariant();

        Assert.DoesNotContain("sysdatetime()", definicion);
        Assert.Contains("at time zone 'pacific sa standard time'", definicion);
    }

    [Fact]
    public async Task LaMigracion_NoToca_NiBorra_Datos_SoloCambiaValoresPorDefecto()
    {
        var script = await api.ConsultarAsync(db => Task.FromResult(
            db.GetService<IMigrator>().GenerateScript(MigracionAnterior, MigracionHoraDeChile)));

        Assert.Contains("AT TIME ZONE 'Pacific SA Standard Time'", script);
        foreach (var prohibido in new[] { "DROP TABLE", "DROP COLUMN", "DELETE FROM", "UPDATE [", "UPDATE dbo.", "TRUNCATE", "ALTER COLUMN" })
            Assert.DoesNotContain(prohibido, script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LaMigracion_SeDeshace_YDevuelveLosValoresAnteriores()
    {
        var script = await api.ConsultarAsync(db => Task.FromResult(
            db.GetService<IMigrator>().GenerateScript(MigracionHoraDeChile, MigracionAnterior)));

        Assert.Contains("SYSDATETIME()", script);
        Assert.DoesNotContain("AT TIME ZONE", script);
        // EF borra su propia fila del historial al deshacer: es lo unico que puede borrar.
        foreach (var prohibido in new[] { "DROP TABLE", "DROP COLUMN", "UPDATE [" })
            Assert.DoesNotContain(prohibido, script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ElScriptIdempotente_IncluyeLaMigracion_ParaAplicarlaALaNubeConUnScript()
    {
        // La 16C puede aplicar la migracion con un script idempotente (sqlcmd) en vez del migrador: debe generarse sin error.
        var script = await api.ConsultarAsync(db => Task.FromResult(
            db.GetService<IMigrator>().GenerateScript(MigracionAnterior, MigracionHoraDeChile, MigrationsSqlGenerationOptions.Idempotent)));

        Assert.Contains(MigracionHoraDeChile, script);
    }
}
