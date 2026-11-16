using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <inheritdoc />
    public partial class NotificacionesYAuditoria : Migration
    {
        /// <inheritdoc />
        // Fase 11: solo agrega. Columnas nuevas con valor por defecto (o nulas), dos tablas, indices, reglas y el trigger
        // que hace inmutable la bitacora. Las dos reglas que se reemplazan (tipos de consentimiento y acciones del
        // historial de destinos) solo se amplian: todo lo que ya cumplia la anterior cumple la nueva.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_destinatarios_avisos_historial_accion",
                table: "destinatarios_avisos_historial");

            migrationBuilder.DropCheckConstraint(
                name: "ck_consentimientos_tipo",
                table: "consentimientos");

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "horarios_atencion",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<bool>(
                name: "recibe_en_linea",
                table: "destinatarios_avisos",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "recibe_presenciales",
                table: "destinatarios_avisos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "destinatarios_avisos",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "enlace_baja",
                table: "correos_salientes",
                type: "varchar(400)",
                unicode: false,
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_reenvio",
                table: "correos_salientes",
                type: "datetimeoffset(0)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "id_empleado_reenvia",
                table: "correos_salientes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "reenviado_como",
                table: "correos_salientes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "revisado",
                table: "correos_salientes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "inhabilitado_en",
                table: "clientes",
                type: "datetimeoffset(0)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "recibe_avisos_estado",
                table: "clientes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "cartel_digital",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "auditoria",
                columns: table => new
                {
                    id_auditoria = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    id_empleado = table.Column<int>(type: "int", nullable: true),
                    id_cliente = table.Column<int>(type: "int", nullable: true),
                    rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    entidad = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    id_entidad = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    accion = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    detalle = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ip = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria", x => x.id_auditoria);
                    table.CheckConstraint("ck_auditoria_quien", "id_empleado IS NULL OR id_cliente IS NULL");
                });

            migrationBuilder.CreateTable(
                name: "claves_idempotencia",
                columns: table => new
                {
                    id_clave = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    clave = table.Column<string>(type: "char(36)", unicode: false, fixedLength: true, maxLength: 36, nullable: false),
                    ambito = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    tipo_usuario = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    hash_cuerpo = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    estado_http = table.Column<int>(type: "int", nullable: true),
                    respuesta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    vence = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claves_idempotencia", x => x.id_clave);
                    table.CheckConstraint("ck_claves_idempotencia_tipo_usuario", "tipo_usuario IN ('cliente', 'empleado')");
                    table.CheckConstraint("ck_claves_idempotencia_vence", "vence > fecha");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_destinatarios_avisos_historial_accion",
                table: "destinatarios_avisos_historial",
                sql: "accion IN ('agregar', 'reemplazar', 'confirmar', 'cancelar', 'quitar', 'reenviar', 'probar', 'preferencia')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_destinatarios_avisos_preferencia",
                table: "destinatarios_avisos",
                sql: "recibe_en_linea = 1 OR recibe_presenciales = 1");

            migrationBuilder.CreateIndex(
                name: "ix_correos_salientes_empleado_reenvia",
                table: "correos_salientes",
                column: "id_empleado_reenvia",
                filter: "[id_empleado_reenvia] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_correos_salientes_fallidos",
                table: "correos_salientes",
                columns: new[] { "proximo_intento", "revisado" },
                filter: "[estado] = 'fallido'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_consentimientos_tipo",
                table: "consentimientos",
                sql: "tipo IN ('privacidad', 'terminos', 'marketing', 'cookies', 'avisos_estado')");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_cliente",
                table: "auditoria",
                columns: new[] { "id_cliente", "fecha" },
                descending: new[] { false, true },
                filter: "[id_cliente] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_empleado",
                table: "auditoria",
                columns: new[] { "id_empleado", "fecha", "id_auditoria" },
                descending: new[] { false, true, true },
                filter: "[id_empleado] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_entidad",
                table: "auditoria",
                columns: new[] { "entidad", "id_entidad" });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_fecha",
                table: "auditoria",
                columns: new[] { "fecha", "id_auditoria" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_claves_idempotencia_vence",
                table: "claves_idempotencia",
                column: "vence");

            migrationBuilder.CreateIndex(
                name: "uq_claves_idempotencia",
                table: "claves_idempotencia",
                columns: new[] { "clave", "ambito", "tipo_usuario", "id_usuario" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_correos_salientes_empleado_reenvia",
                table: "correos_salientes",
                column: "id_empleado_reenvia",
                principalTable: "empleados",
                principalColumn: "id_empleado");

            // La aplicacion solo inserta en la bitacora: cualquier UPDATE o DELETE, venga de donde venga, se rechaza.
            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.trg_auditoria_inmutable ON dbo.auditoria
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 50125, N'La bitácora de auditoría no se modifica ni se borra.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir nunca destruye datos en silencio: si la bitacora, las claves de idempotencia o alguna columna
            // nueva tienen datos, o si hay filas que la regla anterior no admitiria, se detiene sin cambiar nada.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.auditoria) OR EXISTS (SELECT 1 FROM dbo.claves_idempotencia)
                    THROW 50120, N'No se puede revertir NotificacionesYAuditoria: la bitácora o las claves de idempotencia tienen registros. Respáldalos antes de revertir.', 1;
                IF EXISTS (SELECT 1 FROM dbo.correos_salientes
                           WHERE enlace_baja IS NOT NULL OR revisado = 1 OR reenviado_como IS NOT NULL OR id_empleado_reenvia IS NOT NULL OR fecha_reenvio IS NOT NULL)
                    THROW 50121, N'No se puede revertir NotificacionesYAuditoria: hay correos con enlace de baja, revisados o reenviados. Respáldalos antes de revertir.', 1;
                IF EXISTS (SELECT 1 FROM dbo.clientes WHERE inhabilitado_en IS NOT NULL OR recibe_avisos_estado = 0)
                   OR EXISTS (SELECT 1 FROM dbo.destinatarios_avisos WHERE recibe_en_linea = 0 OR recibe_presenciales = 1)
                    THROW 50122, N'No se puede revertir NotificacionesYAuditoria: hay cuentas dadas de baja, clientes sin avisos de estado o destinos con preferencia propia. Respáldalos antes de revertir.', 1;
                IF EXISTS (SELECT 1 FROM dbo.consentimientos WHERE tipo = 'avisos_estado')
                   OR EXISTS (SELECT 1 FROM dbo.destinatarios_avisos_historial WHERE accion = 'preferencia')
                    THROW 50123, N'No se puede revertir NotificacionesYAuditoria: hay consentimientos de avisos de estado o cambios de preferencia en el historial. Respáldalos antes de revertir.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_correos_salientes_empleado_reenvia",
                table: "correos_salientes");

            migrationBuilder.DropTable(
                name: "auditoria");

            migrationBuilder.DropTable(
                name: "claves_idempotencia");

            migrationBuilder.DropCheckConstraint(
                name: "ck_destinatarios_avisos_historial_accion",
                table: "destinatarios_avisos_historial");

            migrationBuilder.DropCheckConstraint(
                name: "ck_destinatarios_avisos_preferencia",
                table: "destinatarios_avisos");

            migrationBuilder.DropIndex(
                name: "ix_correos_salientes_empleado_reenvia",
                table: "correos_salientes");

            migrationBuilder.DropIndex(
                name: "ix_correos_salientes_fallidos",
                table: "correos_salientes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_consentimientos_tipo",
                table: "consentimientos");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "horarios_atencion");

            migrationBuilder.DropColumn(
                name: "recibe_en_linea",
                table: "destinatarios_avisos");

            migrationBuilder.DropColumn(
                name: "recibe_presenciales",
                table: "destinatarios_avisos");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "destinatarios_avisos");

            migrationBuilder.DropColumn(
                name: "enlace_baja",
                table: "correos_salientes");

            migrationBuilder.DropColumn(
                name: "fecha_reenvio",
                table: "correos_salientes");

            migrationBuilder.DropColumn(
                name: "id_empleado_reenvia",
                table: "correos_salientes");

            migrationBuilder.DropColumn(
                name: "reenviado_como",
                table: "correos_salientes");

            migrationBuilder.DropColumn(
                name: "revisado",
                table: "correos_salientes");

            migrationBuilder.DropColumn(
                name: "inhabilitado_en",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "recibe_avisos_estado",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "cartel_digital");

            migrationBuilder.AddCheckConstraint(
                name: "ck_destinatarios_avisos_historial_accion",
                table: "destinatarios_avisos_historial",
                sql: "accion IN ('agregar', 'reemplazar', 'confirmar', 'cancelar', 'quitar', 'reenviar', 'probar')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_consentimientos_tipo",
                table: "consentimientos",
                sql: "tipo IN ('privacidad', 'terminos', 'marketing', 'cookies')");
        }
    }
}
