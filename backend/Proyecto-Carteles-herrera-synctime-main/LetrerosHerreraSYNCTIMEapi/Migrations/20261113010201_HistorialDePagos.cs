using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <summary>
    /// Historial de abonos (aprobado el 12-11-2026): cada abono o pago de un pedido queda en la tabla
    /// pagos con su monto, fecha, medio de pago y quien lo registro. El trigger mantiene
    /// pedidos.monto_abonado igual a la suma de los pagos del pedido (nadie mas escribe esa columna:
    /// EF la ignora y la migracion copia los abonos previos como pago inicial); si esa suma supera el monto final,
    /// ck_pedidos_monto_abonado rechaza la operacion completa. Ademas, los montos del pedido quedan en
    /// pesos enteros (ck_pedidos_montos_enteros): si un pedido existente tuviera centavos, la migracion
    /// falla entera y no cambia nada, para revisarlo antes a mano.
    /// </summary>
    public partial class HistorialDePagos : Migration
    {
        // Suma al pedido la diferencia que trae la operacion (lo insertado menos lo borrado) en vez de
        // volver a sumar toda la tabla. Asi no lee filas de pagos de otras transacciones: dos abonos
        // simultaneos al mismo pedido no se interbloquean, sino que el segundo espera el bloqueo de la
        // fila del pedido y suma sobre el valor ya confirmado (tambien con aislamiento por instantaneas,
        // porque un UPDATE siempre trabaja sobre la ultima version confirmada de la fila que bloquea).
        // El WHERE evita escribir el pedido cuando el total no cambia (por ejemplo, al corregir solo la
        // observacion), para no invalidar la version que otra persona tiene abierta.
        private const string TriggerPagos = """
            CREATE OR ALTER TRIGGER dbo.trg_pagos_recalcular_monto_abonado
            ON dbo.pagos
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                ;WITH cambios AS (
                    SELECT id_pedido, monto FROM inserted
                    UNION ALL
                    SELECT id_pedido, -monto FROM deleted
                ),
                diferencias AS (
                    SELECT id_pedido, SUM(monto) AS diferencia
                    FROM cambios
                    GROUP BY id_pedido
                )
                UPDATE p
                   SET monto_abonado = p.monto_abonado + d.diferencia
                  FROM dbo.pedidos p
                  JOIN diferencias d ON d.id_pedido = p.id_pedido
                 WHERE d.diferencia <> 0;
            END
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pagos",
                columns: table => new
                {
                    id_pago = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_pedido = table.Column<int>(type: "int", nullable: false),
                    monto = table.Column<int>(type: "int", nullable: false),
                    fecha_pago = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "CONVERT(DATE, SYSDATETIME())"),
                    medio_pago = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    observacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    id_empleado_registra = table.Column<int>(type: "int", nullable: false),
                    fecha_registro = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pagos", x => x.id_pago);
                    table.CheckConstraint("ck_pagos_medio_pago", "medio_pago IN (N'efectivo', N'transferencia', N'tarjeta_debito', N'tarjeta_credito', N'cheque', N'otro')");
                    table.CheckConstraint("ck_pagos_monto", "monto > 0");
                    table.ForeignKey(
                        name: "fk_pagos_empleado",
                        column: x => x.id_empleado_registra,
                        principalTable: "empleados",
                        principalColumn: "id_empleado",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pagos_pedido",
                        column: x => x.id_pedido,
                        principalTable: "pedidos",
                        principalColumn: "id_pedido",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_montos_enteros",
                table: "pedidos",
                sql: "monto_base_trabajo = FLOOR(monto_base_trabajo) AND costo_instalacion_adicional = FLOOR(costo_instalacion_adicional) AND monto_base_trabajo + costo_instalacion_adicional <= 2000000000");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_id_empleado_registra",
                table: "pagos",
                column: "id_empleado_registra");

            migrationBuilder.CreateIndex(
                name: "ix_pagos_pedido_fecha",
                table: "pagos",
                columns: new[] { "id_pedido", "fecha_pago" });

            // Si algun pedido ya tenia un monto abonado (registrado antes de existir el historial), se
            // conserva como un pago inicial, para que la suma de pagos siga igual a monto_abonado. Va antes
            // del trigger: estos valores ya son coherentes y no hay nada que recalcular.
            migrationBuilder.Sql("""
                INSERT INTO dbo.pagos (id_pedido, monto, fecha_pago, medio_pago, observacion, id_empleado_registra)
                SELECT id_pedido, monto_abonado, CONVERT(DATE, fecha_pedido), N'otro',
                       N'Abono registrado antes del historial de pagos', id_empleado_responsable
                FROM dbo.pedidos
                WHERE monto_abonado > 0;
                """);

            migrationBuilder.Sql(TriggerPagos);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // monto_abonado conserva la suma que tenia; solo se pierde el detalle de cada abono.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.trg_pagos_recalcular_monto_abonado;");

            migrationBuilder.DropTable(
                name: "pagos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_montos_enteros",
                table: "pedidos");
        }
    }
}
