using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    // Fase 16A (H3). NO SE APLICA A NINGUNA BASE REAL NI A LA DEMO hasta la fase 16C, con permiso del usuario y respaldo previo.
    //
    // Las fechas «sin zona» (datetime2 y date) de clientes, solicitudes, cotizaciones, pedidos, pagos, catalogo y cartel se llenaban con
    // SYSDATETIME(), que es la hora LOCAL del servidor. Las vistas de indicadores y la API las tratan como «hora de Chile»: era cierto
    // solo mientras SQL Server corria en hora de Chile. Azure SQL y los contenedores corren en UTC: una solicitud hecha a las 21:30 en
    // Chile quedaria con la fecha del dia siguiente y los indicadores se correrian entre 3 y 4 horas.
    //
    // Que hace: cambia el valor por defecto de esas 8 columnas por la hora de Chile calculada desde el instante UTC
    // (SYSDATETIMEOFFSET() AT TIME ZONE 'Pacific SA Standard Time'), que no depende de la zona del servidor, y la depuracion de solicitudes
    // (sp_depurar_solicitudes_no_concretadas) para que compare con esa misma hora.
    // Que NO hace: no borra ni modifica ninguna fila (las ya guardadas siguen valiendo: se escribieron en hora de Chile), no cambia
    // tipos ni columnas y no toca las vistas, que leen las columnas ya guardadas. Las columnas datetimeoffset (SYSDATETIMEOFFSET)
    // guardan un instante y estaban bien.
    // Se escribe con SQL explicito y no con AlterColumn porque EF Core emitiria ALTER COLUMN, mas de lo que hace falta sobre tablas con datos.
    // Se puede repetir sin fallar (busca la restriccion por su columna) y Down devuelve los valores anteriores.
    // Las expresiones deben coincidir con Datos/HoraChileSql.cs (el modelo) y con el snapshot: lo comprueba Model_HasNoPendingChanges.
    /// <inheritdoc />
    public partial class HoraDeChileEnLaBase : Migration
    {
        private const string Ahora = "CONVERT(datetime2(0), SYSDATETIMEOFFSET() AT TIME ZONE 'Pacific SA Standard Time')";
        private const string Hoy = "CONVERT(date, SYSDATETIMEOFFSET() AT TIME ZONE 'Pacific SA Standard Time')";

        // tabla, columna, valor por defecto anterior, valor por defecto nuevo
        private static readonly (string Tabla, string Columna, string Anterior, string Nuevo)[] Columnas =
        [
            ("clientes", "fecha_registro", "SYSDATETIME()", Ahora),
            ("catalogo", "fecha_publicacion", "SYSDATETIME()", Ahora),
            ("cartel_digital", "fecha_ultima_modificacion", "SYSDATETIME()", Ahora),
            ("solicitudes", "fecha_solicitud", "SYSDATETIME()", Ahora),
            ("cotizaciones", "fecha_cotizacion", "SYSDATETIME()", Ahora),
            ("pedidos", "fecha_pedido", "SYSDATETIME()", Ahora),
            ("pagos", "fecha_pago", "CONVERT(DATE, SYSDATETIME())", Hoy),
            ("pagos", "fecha_registro", "SYSDATETIME()", Ahora),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADD DEFAULT no valida el nombre de la zona: sin esta comprobacion, un servidor que no la conozca aceptaria la migracion y
            // despues fallaria cada INSERT de clientes, solicitudes, cotizaciones, pedidos y pagos.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.time_zone_info WHERE name = N'Pacific SA Standard Time')
                    THROW 50160, N'Este servidor no conoce la zona horaria de Chile (Pacific SA Standard Time): la migracion no se aplica.', 1;
                """);

            foreach (var (tabla, columna, _, nuevo) in Columnas)
                CambiarValorPorDefecto(migrationBuilder, tabla, columna, nuevo);

            migrationBuilder.Sql(Depuracion(Ahora));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (tabla, columna, anterior, _) in Columnas)
                CambiarValorPorDefecto(migrationBuilder, tabla, columna, anterior);

            migrationBuilder.Sql(Depuracion("SYSDATETIME()"));
        }

        // Quita la restriccion de valor por defecto de la columna (tenga el nombre que tenga) y pone la nueva.
        private static void CambiarValorPorDefecto(MigrationBuilder migrationBuilder, string tabla, string columna, string expresion) =>
            migrationBuilder.Sql($"""
                DECLARE @restriccion sysname = (
                    SELECT dc.name
                    FROM sys.default_constraints dc
                    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[{tabla}]') AND c.name = N'{columna}');
                IF @restriccion IS NOT NULL
                BEGIN
                    DECLARE @quitar nvarchar(max) = N'ALTER TABLE [dbo].[{tabla}] DROP CONSTRAINT ' + QUOTENAME(@restriccion);
                    EXEC sys.sp_executesql @quitar;
                END
                ALTER TABLE [dbo].[{tabla}] ADD DEFAULT ({expresion}) FOR [{columna}];
                """);

        // El mismo procedimiento de la migracion ObjetosProgramablesV6, cambiando solo de donde sale «ahora».
        private static string Depuracion(string ahora) => $"""
            CREATE OR ALTER PROCEDURE dbo.sp_depurar_solicitudes_no_concretadas
                @meses_antiguedad INT = 12
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                DELETE s
                FROM dbo.solicitudes s
                LEFT JOIN dbo.pedidos p ON p.id_solicitud = s.id_solicitud
                WHERE p.id_pedido IS NULL
                  AND (s.eliminado_logico = 1 OR s.estado = N'rechazado')
                  AND s.fecha_solicitud < DATEADD(MONTH, -@meses_antiguedad, {ahora});
            END
            """;
    }
}
