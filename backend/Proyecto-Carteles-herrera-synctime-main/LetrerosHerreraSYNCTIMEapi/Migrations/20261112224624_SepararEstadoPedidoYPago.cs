using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <summary>
    /// Revision de la profesora guia (12-11-2026): el pedido pasa a tener estado operativo y estado de
    /// pago independientes, con monto abonado, saldo pendiente, numero de factura y origen.
    /// "cancelado" pasa a llamarse "anulado". Escrita con EF y completada a mano con la conversion de
    /// datos y la vista de conversion, que dependen de la columna renombrada.
    /// </summary>
    public partial class SepararEstadoPedidoYPago : Migration
    {

        // La vista de conversion contaba los pedidos "no cancelados". Con el renombre de la columna
        // y del estado, se recrea contando los "no anulados".
        private const string VistaConversion = """
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
            LEFT JOIN dbo.pedidos p ON p.id_solicitud = s.id_solicitud AND p.estado_pedido <> N'anulado'
            WHERE s.origen = N'online'
            GROUP BY YEAR(s.fecha_solicitud), MONTH(s.fecha_solicitud);
            """;

        private const string VistaConversionAnterior = """
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
            """;
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_estado",
                table: "pedidos");

            migrationBuilder.RenameColumn(
                name: "estado",
                table: "pedidos",
                newName: "estado_pedido");

            // Antes del CHECK nuevo, que ya no admite "cancelado".
            migrationBuilder.Sql("UPDATE dbo.pedidos SET estado_pedido = N'anulado' WHERE estado_pedido = N'cancelado';");

            migrationBuilder.AddColumn<int>(
                name: "monto_abonado",
                table: "pedidos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "numero_factura",
                table: "pedidos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origen",
                table: "pedidos",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "cotizacion");

            migrationBuilder.AddColumn<string>(
                name: "estado_pago",
                table: "pedidos",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                computedColumnSql: "CONVERT(NVARCHAR(12), CASE WHEN monto_abonado = 0 THEN N'no_pagado' WHEN monto_abonado >= monto_base_trabajo + costo_instalacion_adicional THEN N'pagado_total' ELSE N'abonado' END)",
                stored: true);

            migrationBuilder.AddColumn<int>(
                name: "saldo_pendiente",
                table: "pedidos",
                type: "int",
                nullable: false,
                computedColumnSql: "CONVERT(INT, monto_base_trabajo + costo_instalacion_adicional) - monto_abonado",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_cobranza",
                table: "pedidos",
                columns: new[] { "estado_pago", "estado_pedido" })
                .Annotation("SqlServer:Include", new[] { "saldo_pendiente" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_estado_pedido",
                table: "pedidos",
                sql: "estado_pedido IN (N'aceptado', N'en_proceso', N'terminado', N'entregado', N'anulado')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_monto_abonado",
                table: "pedidos",
                sql: "monto_abonado >= 0 AND monto_abonado <= monto_base_trabajo + costo_instalacion_adicional");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_origen",
                table: "pedidos",
                sql: "origen IN (N'cotizacion', N'directo')");

            migrationBuilder.Sql(VistaConversion);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pedidos_cobranza",
                table: "pedidos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_estado_pedido",
                table: "pedidos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_monto_abonado",
                table: "pedidos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_origen",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "estado_pago",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "saldo_pendiente",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "monto_abonado",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "numero_factura",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "origen",
                table: "pedidos");

            migrationBuilder.RenameColumn(
                name: "estado_pedido",
                table: "pedidos",
                newName: "estado");

            migrationBuilder.Sql("UPDATE dbo.pedidos SET estado = N'cancelado' WHERE estado = N'anulado';");
            migrationBuilder.Sql(VistaConversionAnterior);

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_estado",
                table: "pedidos",
                sql: "estado IN (N'aceptado', N'en_proceso', N'terminado', N'entregado', N'cancelado')");
        }
    }
}
