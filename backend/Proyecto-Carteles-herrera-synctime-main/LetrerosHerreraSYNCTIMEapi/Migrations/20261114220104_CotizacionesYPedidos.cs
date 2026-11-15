using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <summary>
    /// Fase 8 (plan aprobado el 14-11-2026): cotizaciones con respuesta del cliente o del personal y
    /// version (row_version), una sola «pendiente» por solicitud, pedidos con motivo de anulacion e
    /// historial de estados, abonos que se anulan con motivo, y borrado logico de cotizaciones y pedidos.
    /// Solo agrega: columnas, una tabla, indices y reglas. Amplia tres textos (ampliar no pierde datos).
    /// Antes de cada regla nueva completa los datos que ya existen, asi la base demo (consultada el
    /// 14-11-2026) la cumple: las cotizaciones ya respondidas reciben fecha y via «presencial», los
    /// pedidos anulados un motivo y una fecha, y cada pedido su fila de creacion en el historial.
    /// Si la base real tuviera algo que no cumple (por ejemplo, dos cotizaciones pendientes de una misma
    /// solicitud), la migracion falla entera sin cambiar nada, para revisarlo antes a mano.
    /// </summary>
    public partial class CotizacionesYPedidos : Migration
    {
        // Los abonos anulados dejan de sumar: el trigger cuenta solo las filas con anulado = 0. Anular un
        // abono es un UPDATE en que la fila vieja (deleted) suma y la nueva (inserted) no, asi que su monto
        // se resta. Un abono anulado no vuelve a estar vigente: el trigger lo impide aunque se intente a mano.
        private const string TriggerPagos = """
            CREATE OR ALTER TRIGGER dbo.trg_pagos_recalcular_monto_abonado
            ON dbo.pagos
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.id_pago = i.id_pago WHERE d.anulado = 1 AND i.anulado = 0)
                    THROW 50080, N'Un abono anulado no se puede reactivar.', 1;

                ;WITH cambios AS (
                    SELECT id_pedido, monto FROM inserted WHERE anulado = 0
                    UNION ALL
                    SELECT id_pedido, -monto FROM deleted WHERE anulado = 0
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

        // El de la migracion HistorialDePagos, que suma todos los pagos.
        private const string TriggerPagosAnterior = """
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

        // Las fechas guardadas en datetime2 son hora de Chile: AT TIME ZONE les agrega su desfase.
        private const string ZonaChile = "Pacific SA Standard Time";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Textos mas largos ──

            migrationBuilder.AlterColumn<string>(
                name: "direccion_instalacion",
                table: "pedidos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "observaciones",
                table: "cotizaciones",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "medida_aplicada",
                table: "cotizacion_materiales",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);

            // ── cotizaciones: respuesta, version y borrado logico ──

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "cotizaciones",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_respuesta",
                table: "cotizaciones",
                type: "datetimeoffset(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "via_respuesta",
                table: "cotizaciones",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "id_empleado_respuesta",
                table: "cotizaciones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_rechazo",
                table: "cotizaciones",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "eliminado_logico",
                table: "cotizaciones",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_eliminacion_logica",
                table: "cotizaciones",
                type: "datetimeoffset(0)",
                nullable: true);

            // Antes de ck_cotizaciones_respuesta: las ya aprobadas o rechazadas se respondieron en el local,
            // el dia en que se cotizaron (no se sabe cuando ni quien).
            migrationBuilder.Sql($"""
                UPDATE dbo.cotizaciones
                   SET fecha_respuesta = fecha_cotizacion AT TIME ZONE '{ZonaChile}',
                       via_respuesta = N'presencial'
                 WHERE estado <> N'pendiente';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_cotizaciones_montos_enteros",
                table: "cotizaciones",
                sql: "costo_mano_obra = FLOOR(costo_mano_obra) AND otros_costos = FLOOR(otros_costos)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cotizaciones_respuesta",
                table: "cotizaciones",
                sql: "(CASE WHEN estado = N'pendiente' THEN 1 ELSE 0 END) = (CASE WHEN fecha_respuesta IS NULL THEN 1 ELSE 0 END) AND (CASE WHEN via_respuesta IS NULL THEN 0 ELSE 1 END) = (CASE WHEN fecha_respuesta IS NULL THEN 0 ELSE 1 END)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cotizaciones_via",
                table: "cotizaciones",
                sql: "via_respuesta IS NULL OR via_respuesta IN (N'cliente_web', N'telefono', N'whatsapp', N'presencial')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cot_mat_costo_entero",
                table: "cotizacion_materiales",
                sql: "costo_unitario = FLOOR(costo_unitario)");

            migrationBuilder.CreateIndex(
                name: "uq_cotizaciones_una_pendiente",
                table: "cotizaciones",
                column: "id_solicitud",
                unique: true,
                filter: "[estado] = N'pendiente' AND [eliminado_logico] = 0");

            migrationBuilder.CreateIndex(
                name: "ix_cotizaciones_empleado_respuesta",
                table: "cotizaciones",
                column: "id_empleado_respuesta");

            migrationBuilder.AddForeignKey(
                name: "fk_cotizaciones_empleado_respuesta",
                table: "cotizaciones",
                column: "id_empleado_respuesta",
                principalTable: "empleados",
                principalColumn: "id_empleado");

            // ── pedidos: anulacion, borrado logico y un pedido por cotizacion ──

            migrationBuilder.AddColumn<string>(
                name: "motivo_anulacion",
                table: "pedidos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_anulacion",
                table: "pedidos",
                type: "datetimeoffset(0)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "eliminado_logico",
                table: "pedidos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_eliminacion_logica",
                table: "pedidos",
                type: "datetimeoffset(0)",
                nullable: true);

            // Antes de ck_pedidos_anulacion. No se sabe cuando se anularon: se toma la fecha del pedido, la
            // misma que recibe su fila en el historial.
            migrationBuilder.Sql($"""
                UPDATE dbo.pedidos
                   SET motivo_anulacion = N'Anulado antes de la fase 8',
                       fecha_anulacion = fecha_pedido AT TIME ZONE '{ZonaChile}'
                 WHERE estado_pedido = N'anulado';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_anulacion",
                table: "pedidos",
                sql: "(estado_pedido = N'anulado' AND motivo_anulacion IS NOT NULL AND fecha_anulacion IS NOT NULL) OR (estado_pedido <> N'anulado' AND motivo_anulacion IS NULL AND fecha_anulacion IS NULL)");

            // El indice de la clave foranea se reemplaza por uno unico (que tambien la cubre).
            migrationBuilder.DropIndex(
                name: "IX_pedidos_id_cotizacion",
                table: "pedidos");

            migrationBuilder.CreateIndex(
                name: "uq_pedidos_cotizacion",
                table: "pedidos",
                column: "id_cotizacion",
                unique: true,
                filter: "[id_cotizacion] IS NOT NULL");

            // ── pedidos_historial ──

            migrationBuilder.CreateTable(
                name: "pedidos_historial",
                columns: table => new
                {
                    id_historial = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_pedido = table.Column<int>(type: "int", nullable: false),
                    estado_anterior = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    estado_nuevo = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    nota = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    id_empleado = table.Column<int>(type: "int", nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedidos_historial", x => x.id_historial);
                    table.CheckConstraint("ck_pedidos_historial_estados", "estado_nuevo IN (N'aceptado', N'en_proceso', N'terminado', N'entregado', N'anulado') AND (estado_anterior IS NULL OR estado_anterior IN (N'aceptado', N'en_proceso', N'terminado', N'entregado', N'anulado'))");
                    table.ForeignKey(
                        name: "fk_pedidos_historial_empleado",
                        column: x => x.id_empleado,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                    table.ForeignKey(
                        name: "fk_pedidos_historial_pedido",
                        column: x => x.id_pedido,
                        principalTable: "pedidos",
                        principalColumn: "id_pedido");
                });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_historial_pedido",
                table: "pedidos_historial",
                columns: new[] { "id_pedido", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_historial_empleado",
                table: "pedidos_historial",
                column: "id_empleado");

            // Cada pedido que ya existe recibe su fila de creacion: de nada a su estado actual, a nombre de
            // su responsable y con la fecha del pedido.
            migrationBuilder.Sql($"""
                INSERT INTO dbo.pedidos_historial (id_pedido, estado_anterior, estado_nuevo, nota, id_empleado, fecha)
                SELECT id_pedido, NULL, estado_pedido, N'Registrado antes del historial de pedidos', id_empleado_responsable,
                       fecha_pedido AT TIME ZONE '{ZonaChile}'
                  FROM dbo.pedidos;
                """);

            // ── pagos: anulacion de abonos ──

            migrationBuilder.AddColumn<bool>(
                name: "anulado",
                table: "pagos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "motivo_anulacion",
                table: "pagos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "id_empleado_anula",
                table: "pagos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_anulacion",
                table: "pagos",
                type: "datetimeoffset(0)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_pagos_anulacion",
                table: "pagos",
                sql: "(anulado = 1 AND motivo_anulacion IS NOT NULL AND id_empleado_anula IS NOT NULL AND fecha_anulacion IS NOT NULL) OR (anulado = 0 AND motivo_anulacion IS NULL AND id_empleado_anula IS NULL AND fecha_anulacion IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_pagos_empleado_anula",
                table: "pagos",
                column: "id_empleado_anula");

            migrationBuilder.AddForeignKey(
                name: "fk_pagos_empleado_anula",
                table: "pagos",
                column: "id_empleado_anula",
                principalTable: "empleados",
                principalColumn: "id_empleado");

            // Todos los pagos existentes quedan vigentes (anulado = 0): monto_abonado ya es su suma.
            migrationBuilder.Sql(TriggerPagos);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir nunca destruye datos en silencio. El esquema anterior no sabe representar un abono
            // anulado (volveria a sumar) ni textos mas largos que sus columnas: si hay alguno, se detiene sin
            // cambiar nada, para resolverlo a mano y con respaldo.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.pagos WHERE anulado = 1)
                    THROW 50081, N'No se puede revertir CotizacionesYPedidos: hay abonos anulados, que el esquema anterior volvería a sumar. Respáldalos y resuélvelos a mano antes de revertir.', 1;
                IF EXISTS (SELECT 1 FROM dbo.cotizaciones WHERE DATALENGTH(observaciones) > 1000)
                   OR EXISTS (SELECT 1 FROM dbo.cotizacion_materiales WHERE DATALENGTH(medida_aplicada) > 160)
                   OR EXISTS (SELECT 1 FROM dbo.pedidos WHERE DATALENGTH(direccion_instalacion) > 510)
                    THROW 50082, N'No se puede revertir CotizacionesYPedidos: hay observaciones (más de 500), medidas (más de 80) o direcciones (más de 255) que no caben en las columnas anteriores. Acórtalas a mano antes de revertir.', 1;
                """);
            // Sin abonos anulados, el trigger anterior (que suma todos los pagos) da el mismo monto abonado.
            migrationBuilder.Sql(TriggerPagosAnterior);

            migrationBuilder.DropForeignKey(
                name: "fk_pagos_empleado_anula",
                table: "pagos");

            migrationBuilder.DropIndex(
                name: "ix_pagos_empleado_anula",
                table: "pagos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pagos_anulacion",
                table: "pagos");

            migrationBuilder.DropColumn(
                name: "anulado",
                table: "pagos");

            migrationBuilder.DropColumn(
                name: "motivo_anulacion",
                table: "pagos");

            migrationBuilder.DropColumn(
                name: "id_empleado_anula",
                table: "pagos");

            migrationBuilder.DropColumn(
                name: "fecha_anulacion",
                table: "pagos");

            migrationBuilder.DropTable(
                name: "pedidos_historial");

            migrationBuilder.DropIndex(
                name: "uq_pedidos_cotizacion",
                table: "pedidos");

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_id_cotizacion",
                table: "pedidos",
                column: "id_cotizacion");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_anulacion",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "motivo_anulacion",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "fecha_anulacion",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "eliminado_logico",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "fecha_eliminacion_logica",
                table: "pedidos");

            migrationBuilder.DropForeignKey(
                name: "fk_cotizaciones_empleado_respuesta",
                table: "cotizaciones");

            migrationBuilder.DropIndex(
                name: "ix_cotizaciones_empleado_respuesta",
                table: "cotizaciones");

            migrationBuilder.DropIndex(
                name: "uq_cotizaciones_una_pendiente",
                table: "cotizaciones");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cotizaciones_montos_enteros",
                table: "cotizaciones");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cotizaciones_respuesta",
                table: "cotizaciones");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cotizaciones_via",
                table: "cotizaciones");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cot_mat_costo_entero",
                table: "cotizacion_materiales");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "cotizaciones");

            migrationBuilder.DropColumn(
                name: "fecha_respuesta",
                table: "cotizaciones");

            migrationBuilder.DropColumn(
                name: "via_respuesta",
                table: "cotizaciones");

            migrationBuilder.DropColumn(
                name: "id_empleado_respuesta",
                table: "cotizaciones");

            migrationBuilder.DropColumn(
                name: "motivo_rechazo",
                table: "cotizaciones");

            migrationBuilder.DropColumn(
                name: "eliminado_logico",
                table: "cotizaciones");

            migrationBuilder.DropColumn(
                name: "fecha_eliminacion_logica",
                table: "cotizaciones");

            migrationBuilder.AlterColumn<string>(
                name: "direccion_instalacion",
                table: "pedidos",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "observaciones",
                table: "cotizaciones",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "medida_aplicada",
                table: "cotizacion_materiales",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
