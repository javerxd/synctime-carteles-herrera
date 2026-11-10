using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <summary>
    /// Migracion escrita a mano con lo que el modelo de EF no puede expresar: el trigger, la vista,
    /// el procedimiento, el DEFAULT de cartel_digital.id_cartel y el domingo (HasData omite los false).
    /// </summary>
    public partial class ObjetosProgramablesV6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE dbo.cartel_digital ADD CONSTRAINT df_cartel_id DEFAULT (1) FOR id_cartel;");

            migrationBuilder.Sql("""
                INSERT INTO dbo.horarios_atencion (dia_semana, nombre_dia, hora_inicio, hora_fin, abierto)
                VALUES (7, N'Domingo', NULL, NULL, 0);
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER dbo.trg_cotizacion_materiales_recalcular_total
                ON dbo.cotizacion_materiales
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    ;WITH cotizaciones_afectadas AS (
                        SELECT id_cotizacion FROM inserted
                        UNION
                        SELECT id_cotizacion FROM deleted
                    )
                    UPDATE c
                       SET costo_materiales_total = ISNULL((
                               SELECT SUM(cm.subtotal)
                               FROM dbo.cotizacion_materiales cm
                               WHERE cm.id_cotizacion = c.id_cotizacion
                           ), 0)
                      FROM dbo.cotizaciones c
                      JOIN cotizaciones_afectadas ca ON ca.id_cotizacion = c.id_cotizacion;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER VIEW dbo.vw_metricas_conversion_online
                AS
                SELECT
                    YEAR(s.fecha_solicitud)  AS anio,
                    MONTH(s.fecha_solicitud) AS mes,
                    COUNT(s.id_solicitud)    AS total_solicitudes_online,
                    COUNT(p.id_pedido)       AS solicitudes_concretadas_en_pedido,
                    CAST(
                        CASE WHEN COUNT(s.id_solicitud) = 0 THEN 0
                             ELSE (COUNT(p.id_pedido) * 100.0) / COUNT(s.id_solicitud)
                        END AS DECIMAL(5,2)
                    ) AS porcentaje_conversion
                FROM dbo.solicitudes s
                LEFT JOIN dbo.pedidos p ON p.id_solicitud = s.id_solicitud AND p.estado <> N'cancelado'
                WHERE s.origen = N'online'
                GROUP BY YEAR(s.fecha_solicitud), MONTH(s.fecha_solicitud);
                """);

            migrationBuilder.Sql("""
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
                      AND s.fecha_solicitud < DATEADD(MONTH, -@meses_antiguedad, SYSDATETIME());
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_depurar_solicitudes_no_concretadas;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_metricas_conversion_online;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.trg_cotizacion_materiales_recalcular_total;");
            migrationBuilder.Sql("DELETE FROM dbo.horarios_atencion WHERE dia_semana = 7;");
            migrationBuilder.Sql("ALTER TABLE dbo.cartel_digital DROP CONSTRAINT df_cartel_id;");
        }
    }
}
