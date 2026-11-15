using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <summary>
    /// Fase 9 (plan aprobado el 14-11-2026): mantenedores con version, horario con quien y cuando, franjas
    /// de disponibilidad del personal (RFU20) y los destinos de los avisos del taller con su historial.
    /// Solo agrega: columnas row_version (rowversion, la llena SQL Server al agregarla), columnas nulas,
    /// tres tablas nuevas, indices y reglas. Amplia dos descripciones y el regreso
    /// estimado del cartel (ampliar no pierde datos).
    /// La regla nueva de horarios (un dia cerrado no guarda horas) se comprobo contra la base demo el
    /// 14-11-2026: el domingo cerrado tiene sus dos horas en NULL. Si la base real no la cumpliera, la
    /// migracion falla entera sin cambiar nada, para revisarla antes a mano.
    /// </summary>
    public partial class MantenedoresYAvisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "tiempo_estimado_regreso",
                table: "cartel_digital",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "descripcion",
                table: "productos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "productos",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "materiales",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_modificacion",
                table: "horarios_atencion",
                type: "datetimeoffset(0)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "id_empleado_modifica",
                table: "horarios_atencion",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "empleados",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "clientes",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<string>(
                name: "descripcion",
                table: "categorias",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "categorias",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "catalogo",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "destinatarios_avisos",
                columns: table => new
                {
                    id_destinatario = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    correo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    estado = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    id_reemplaza = table.Column<int>(type: "int", nullable: true),
                    token_hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    vence = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    id_empleado_solicita = table.Column<int>(type: "int", nullable: false),
                    fecha_solicitud = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    fecha_confirmacion = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    fecha_retiro = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_destinatarios_avisos", x => x.id_destinatario);
                    table.CheckConstraint("ck_destinatarios_avisos_correo", "correo COLLATE Latin1_General_100_BIN2 = LOWER(correo) COLLATE Latin1_General_100_BIN2 AND correo LIKE N'%_@_%._%' AND correo NOT LIKE N'% %'");
                    table.CheckConstraint("ck_destinatarios_avisos_enlace", "(estado = 'pendiente' AND token_hash IS NOT NULL AND vence IS NOT NULL) OR (estado <> 'pendiente' AND token_hash IS NULL AND vence IS NULL)");
                    table.CheckConstraint("ck_destinatarios_avisos_estado", "estado IN ('pendiente', 'activo', 'retirado', 'cancelado')");
                    table.ForeignKey(
                        name: "fk_destinatarios_avisos_empleado",
                        column: x => x.id_empleado_solicita,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                    table.ForeignKey(
                        name: "fk_destinatarios_avisos_reemplaza",
                        column: x => x.id_reemplaza,
                        principalTable: "destinatarios_avisos",
                        principalColumn: "id_destinatario");
                });

            migrationBuilder.CreateTable(
                name: "disponibilidad_empleados",
                columns: table => new
                {
                    id_disponibilidad = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_empleado = table.Column<int>(type: "int", nullable: false),
                    dia_semana = table.Column<byte>(type: "tinyint", nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    hora_fin = table.Column<TimeOnly>(type: "time(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disponibilidad_empleados", x => x.id_disponibilidad);
                    table.CheckConstraint("ck_disponibilidad_dia", "dia_semana BETWEEN 1 AND 7");
                    table.CheckConstraint("ck_disponibilidad_rango", "hora_inicio < hora_fin");
                    table.ForeignKey(
                        name: "fk_disponibilidad_empleado",
                        column: x => x.id_empleado,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                });

            migrationBuilder.CreateTable(
                name: "destinatarios_avisos_historial",
                columns: table => new
                {
                    id_historial = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    accion = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    correo_enmascarado = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    id_destinatario = table.Column<int>(type: "int", nullable: true),
                    id_empleado = table.Column<int>(type: "int", nullable: true),
                    fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    detalle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_destinatarios_avisos_historial", x => x.id_historial);
                    table.CheckConstraint("ck_destinatarios_avisos_historial_accion", "accion IN ('agregar', 'reemplazar', 'confirmar', 'cancelar', 'quitar', 'reenviar', 'probar')");
                    table.ForeignKey(
                        name: "fk_destinatarios_avisos_historial_destino",
                        column: x => x.id_destinatario,
                        principalTable: "destinatarios_avisos",
                        principalColumn: "id_destinatario");
                    table.ForeignKey(
                        name: "fk_destinatarios_avisos_historial_empleado",
                        column: x => x.id_empleado,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                });







            migrationBuilder.CreateIndex(
                name: "ix_horarios_empleado_modifica",
                table: "horarios_atencion",
                column: "id_empleado_modifica");

            migrationBuilder.AddCheckConstraint(
                name: "ck_horarios_cerrado",
                table: "horarios_atencion",
                sql: "abierto = 1 OR (hora_inicio IS NULL AND hora_fin IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_destinatarios_avisos_empleado",
                table: "destinatarios_avisos",
                column: "id_empleado_solicita");

            migrationBuilder.CreateIndex(
                name: "ix_destinatarios_avisos_reemplaza",
                table: "destinatarios_avisos",
                column: "id_reemplaza");

            migrationBuilder.CreateIndex(
                name: "uq_destinatarios_avisos_token",
                table: "destinatarios_avisos",
                column: "token_hash",
                unique: true,
                filter: "[token_hash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_destinatarios_avisos_vigente",
                table: "destinatarios_avisos",
                column: "correo",
                unique: true,
                filter: "[estado] IN ('activo', 'pendiente')");

            migrationBuilder.CreateIndex(
                name: "ix_destinatarios_avisos_historial_destino",
                table: "destinatarios_avisos_historial",
                columns: new[] { "id_destinatario", "accion", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_destinatarios_avisos_historial_empleado",
                table: "destinatarios_avisos_historial",
                column: "id_empleado");

            migrationBuilder.CreateIndex(
                name: "ix_destinatarios_avisos_historial_fecha",
                table: "destinatarios_avisos_historial",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "ix_disponibilidad_empleado_dia",
                table: "disponibilidad_empleados",
                columns: new[] { "id_empleado", "dia_semana" });

            migrationBuilder.AddForeignKey(
                name: "fk_horarios_empleado_modifica",
                table: "horarios_atencion",
                column: "id_empleado_modifica",
                principalTable: "empleados",
                principalColumn: "id_empleado");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir nunca destruye datos en silencio: si alguien ya configuro destinos de avisos, registro
            // franjas de disponibilidad o el historial tiene filas, se detiene sin cambiar nada. Tampoco se
            // achican las descripciones si alguna ya no cabe en el largo anterior.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.destinatarios_avisos)
                   OR EXISTS (SELECT 1 FROM dbo.destinatarios_avisos_historial)
                   OR EXISTS (SELECT 1 FROM dbo.disponibilidad_empleados)
                    THROW 50090, N'No se puede revertir MantenedoresYAvisos: hay destinos de avisos, historial o franjas de disponibilidad guardados. Respáldalos y bórralos a mano antes de revertir.', 1;
                IF EXISTS (SELECT 1 FROM dbo.categorias WHERE LEN(descripcion) > 250)
                   OR EXISTS (SELECT 1 FROM dbo.productos WHERE LEN(descripcion) > 250)
                   OR EXISTS (SELECT 1 FROM dbo.cartel_digital WHERE LEN(tiempo_estimado_regreso) > 80)
                    THROW 50091, N'No se puede revertir MantenedoresYAvisos: hay descripciones (más de 250 caracteres) o un regreso estimado del cartel (más de 80) que no caben en las columnas anteriores. Acórtalos a mano antes de revertir.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_horarios_empleado_modifica",
                table: "horarios_atencion");

            migrationBuilder.DropTable(
                name: "destinatarios_avisos_historial");

            migrationBuilder.DropTable(
                name: "disponibilidad_empleados");

            migrationBuilder.DropTable(
                name: "destinatarios_avisos");

            migrationBuilder.DropIndex(
                name: "ix_horarios_empleado_modifica",
                table: "horarios_atencion");

            migrationBuilder.DropCheckConstraint(
                name: "ck_horarios_cerrado",
                table: "horarios_atencion");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "materiales");

            migrationBuilder.DropColumn(
                name: "fecha_modificacion",
                table: "horarios_atencion");

            migrationBuilder.DropColumn(
                name: "id_empleado_modifica",
                table: "horarios_atencion");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "catalogo");

            migrationBuilder.AlterColumn<string>(
                name: "descripcion",
                table: "productos",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "descripcion",
                table: "categorias",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "tiempo_estimado_regreso",
                table: "cartel_digital",
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
