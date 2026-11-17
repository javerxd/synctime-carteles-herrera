using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    // Fase 12B (RNFO11, RNFP26, RNFE01): las vistas del dashboard, sus indices y la purga de la IP de la bitacora.
    // - Vistas: columnas explicitas, sin SELECT *; las agregaciones por periodo las hace la API sobre ellas.
    // - Indices: justificados en docs/base-de-datos/ (cada uno, con la consulta que sirve).
    // - Trigger de la bitacora: sigue rechazando todo UPDATE y DELETE salvo UNO: poner en NULL la IP de filas de mas de 90
    //   dias, sin cambiar ninguna otra columna. El registro del evento queda intacto.
    // No borra tablas, columnas ni filas. Down: borra las vistas, restaura el trigger inmutable y quita los indices.
    /// <inheritdoc />
    public partial class IndicadoresDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sol_detalles_solicitud",
                table: "solicitud_detalles");

            migrationBuilder.CreateIndex(
                name: "ix_sol_detalles_solicitud",
                table: "solicitud_detalles",
                column: "id_solicitud")
                .Annotation("SqlServer:Include", new[] { "id_producto", "cantidad" });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_entrega_real",
                table: "pedidos",
                column: "fecha_entrega_real",
                filter: "[estado_pedido] = N'entregado' AND [eliminado_logico] = 0")
                .Annotation("SqlServer:Include", new[] { "fecha_entrega_estimada", "monto_total_final", "id_solicitud" });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_indicadores_fecha",
                table: "pedidos",
                column: "fecha_pedido",
                filter: "[eliminado_logico] = 0")
                .Annotation("SqlServer:Include", new[] { "estado_pedido", "estado_pago", "monto_total_final", "monto_abonado", "saldo_pendiente", "id_solicitud" });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_ip_vigente",
                table: "auditoria",
                column: "fecha",
                filter: "[ip] IS NOT NULL");

            migrationBuilder.Sql("""
                CREATE OR ALTER VIEW dbo.vw_indicadores_pedidos
                AS
                SELECT
                    p.id_pedido,
                    p.id_solicitud,
                    s.id_cliente,
                    s.origen                                  AS origen_solicitud,
                    CONVERT(date, p.fecha_pedido)             AS fecha_pedido,
                    CONVERT(date, p.fecha_entrega_estimada)   AS fecha_entrega_estimada,
                    CONVERT(date, p.fecha_entrega_real)       AS fecha_entrega_real,
                    p.estado_pedido,
                    p.estado_pago,
                    CONVERT(int, p.monto_total_final)         AS monto_total_final,
                    p.monto_abonado,
                    p.saldo_pendiente
                FROM dbo.pedidos p
                JOIN dbo.solicitudes s ON s.id_solicitud = p.id_solicitud
                WHERE p.eliminado_logico = 0;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER VIEW dbo.vw_indicadores_solicitudes
                AS
                SELECT
                    s.id_solicitud,
                    s.id_cliente,
                    s.origen,
                    CONVERT(date, s.fecha_solicitud) AS fecha_solicitud,
                    (SELECT TOP (1) pr.id_categoria
                       FROM dbo.solicitud_detalles d
                       JOIN dbo.productos pr ON pr.id_producto = d.id_producto
                      WHERE d.id_solicitud = s.id_solicitud
                      ORDER BY d.id_solicitud_detalle)         AS id_categoria_principal,
                    CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM dbo.cotizaciones c
                                                    WHERE c.id_solicitud = s.id_solicitud AND c.eliminado_logico = 0)
                                      THEN 1 ELSE 0 END)       AS cotizada,
                    CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM dbo.cotizaciones c
                                                    WHERE c.id_solicitud = s.id_solicitud AND c.eliminado_logico = 0
                                                      AND c.estado = N'aprobada')
                                      THEN 1 ELSE 0 END)       AS aprobada,
                    CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM dbo.pedidos p
                                                    WHERE p.id_solicitud = s.id_solicitud AND p.eliminado_logico = 0
                                                      AND p.estado_pedido <> N'anulado')
                                      THEN 1 ELSE 0 END)       AS concretada
                FROM dbo.solicitudes s
                WHERE s.eliminado_logico = 0;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER VIEW dbo.vw_indicadores_items
                AS
                SELECT
                    d.id_solicitud_detalle,
                    p.id_pedido,
                    d.id_producto,
                    pr.id_categoria,
                    d.cantidad,
                    CONVERT(decimal(19, 4), p.monto_total_final) * d.cantidad
                        / NULLIF(SUM(d.cantidad) OVER (PARTITION BY p.id_pedido), 0) AS monto_asignado
                FROM dbo.pedidos p
                JOIN dbo.solicitud_detalles d ON d.id_solicitud = p.id_solicitud
                JOIN dbo.productos pr ON pr.id_producto = d.id_producto
                WHERE p.eliminado_logico = 0;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER dbo.trg_auditoria_inmutable ON dbo.auditoria
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    -- Una sentencia que no alcanza ninguna fila no cambia nada: no hay nada que rechazar.
                    IF NOT EXISTS (SELECT 1 FROM deleted) RETURN;
                    -- Unica excepcion (RNFE01, fase 12B): poner en NULL la IP de filas de mas de 90 dias. Todo lo demas, rechazado.
                    IF EXISTS (SELECT 1 FROM inserted)
                       AND (SELECT COUNT(*) FROM inserted) = (SELECT COUNT(*) FROM deleted)
                       AND NOT EXISTS (SELECT 1 FROM inserted WHERE ip IS NOT NULL)
                       AND NOT EXISTS (SELECT 1 FROM deleted WHERE ip IS NULL OR fecha > DATEADD(DAY, -90, SYSDATETIMEOFFSET()))
                       AND NOT EXISTS (SELECT i.id_auditoria, i.fecha, i.id_empleado, i.id_cliente, i.rol, i.entidad, i.id_entidad, i.accion, i.detalle
                                         FROM inserted i
                                       EXCEPT
                                       SELECT d.id_auditoria, d.fecha, d.id_empleado, d.id_cliente, d.rol, d.entidad, d.id_entidad, d.accion, d.detalle
                                         FROM deleted d)
                    BEGIN
                        UPDATE a SET ip = NULL FROM dbo.auditoria a JOIN inserted i ON i.id_auditoria = a.id_auditoria;
                        RETURN;
                    END;
                    THROW 50125, N'La bitácora de auditoría no se modifica ni se borra.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_indicadores_items;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_indicadores_solicitudes;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_indicadores_pedidos;");
            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER dbo.trg_auditoria_inmutable ON dbo.auditoria
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 50125, N'La bitácora de auditoría no se modifica ni se borra.', 1;
                END
                """);

            migrationBuilder.DropIndex(
                name: "ix_sol_detalles_solicitud",
                table: "solicitud_detalles");

            migrationBuilder.DropIndex(
                name: "ix_pedidos_entrega_real",
                table: "pedidos");

            migrationBuilder.DropIndex(
                name: "ix_pedidos_indicadores_fecha",
                table: "pedidos");

            migrationBuilder.DropIndex(
                name: "ix_auditoria_ip_vigente",
                table: "auditoria");

            migrationBuilder.CreateIndex(
                name: "ix_sol_detalles_solicitud",
                table: "solicitud_detalles",
                column: "id_solicitud");
        }
    }
}
