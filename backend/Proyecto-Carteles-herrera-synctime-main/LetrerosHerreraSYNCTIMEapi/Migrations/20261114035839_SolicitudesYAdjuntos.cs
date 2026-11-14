using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <inheritdoc />
    public partial class SolicitudesYAdjuntos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "motivo_rechazo",
                table: "solicitudes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "descripcion_general",
                table: "solicitudes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "huella_ip",
                table: "solicitudes",
                type: "char(64)",
                unicode: false,
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "descripcion",
                table: "solicitud_detalles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.CreateTable(
                name: "adjuntos_solicitud",
                columns: table => new
                {
                    id_adjunto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_solicitud = table.Column<int>(type: "int", nullable: true),
                    id_cliente = table.Column<int>(type: "int", nullable: true),
                    id_empleado = table.Column<int>(type: "int", nullable: true),
                    clave = table.Column<string>(type: "char(32)", unicode: false, fixedLength: true, maxLength: 32, nullable: false),
                    tipo = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    nombre_original = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    bytes = table.Column<int>(type: "int", nullable: false),
                    ancho = table.Column<int>(type: "int", nullable: true),
                    alto = table.Column<int>(type: "int", nullable: true),
                    fecha_subida = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    fecha_vinculo = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adjuntos_solicitud", x => x.id_adjunto);
                    table.CheckConstraint("ck_adjuntos_solicitud_autor", "(CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) = 1");
                    table.CheckConstraint("ck_adjuntos_solicitud_bytes", "bytes > 0");
                    table.CheckConstraint("ck_adjuntos_solicitud_medidas", "(tipo = 'imagen' AND ancho > 0 AND alto > 0) OR (tipo = 'pdf' AND ancho IS NULL AND alto IS NULL)");
                    table.CheckConstraint("ck_adjuntos_solicitud_tipo", "tipo IN ('imagen', 'pdf')");
                    table.CheckConstraint("ck_adjuntos_solicitud_vinculo", "(id_solicitud IS NULL AND fecha_vinculo IS NULL) OR (id_solicitud IS NOT NULL AND fecha_vinculo IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_adjuntos_solicitud_cliente",
                        column: x => x.id_cliente,
                        principalTable: "clientes",
                        principalColumn: "id_cliente");
                    table.ForeignKey(
                        name: "fk_adjuntos_solicitud_empleado",
                        column: x => x.id_empleado,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                    table.ForeignKey(
                        name: "fk_adjuntos_solicitud_solicitud",
                        column: x => x.id_solicitud,
                        principalTable: "solicitudes",
                        principalColumn: "id_solicitud",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "solicitudes_historial",
                columns: table => new
                {
                    id_historial = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_solicitud = table.Column<int>(type: "int", nullable: false),
                    estado_anterior = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    estado_nuevo = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    id_empleado = table.Column<int>(type: "int", nullable: true),
                    id_cliente = table.Column<int>(type: "int", nullable: true),
                    fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitudes_historial", x => x.id_historial);
                    table.CheckConstraint("ck_solicitudes_historial_actor", "id_cliente IS NULL OR id_empleado IS NULL");
                    table.CheckConstraint("ck_solicitudes_historial_estados", "estado_nuevo IN (N'solicitado', N'cotizado', N'aceptado', N'rechazado') AND (estado_anterior IS NULL OR estado_anterior IN (N'solicitado', N'cotizado', N'aceptado', N'rechazado'))");
                    table.ForeignKey(
                        name: "fk_solicitudes_historial_cliente",
                        column: x => x.id_cliente,
                        principalTable: "clientes",
                        principalColumn: "id_cliente");
                    table.ForeignKey(
                        name: "fk_solicitudes_historial_empleado",
                        column: x => x.id_empleado,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                    table.ForeignKey(
                        name: "fk_solicitudes_historial_solicitud",
                        column: x => x.id_solicitud,
                        principalTable: "solicitudes",
                        principalColumn: "id_solicitud",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_solicitudes_huella_ip",
                table: "solicitudes",
                columns: new[] { "huella_ip", "fecha_solicitud" },
                filter: "[huella_ip] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_adjuntos_solicitud_borrador",
                table: "adjuntos_solicitud",
                columns: new[] { "id_cliente", "fecha_subida" },
                filter: "[id_solicitud] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_adjuntos_solicitud_id_empleado",
                table: "adjuntos_solicitud",
                column: "id_empleado");

            migrationBuilder.CreateIndex(
                name: "ix_adjuntos_solicitud_solicitud",
                table: "adjuntos_solicitud",
                column: "id_solicitud",
                filter: "[id_solicitud] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_adjuntos_solicitud_clave",
                table: "adjuntos_solicitud",
                column: "clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_historial_id_cliente",
                table: "solicitudes_historial",
                column: "id_cliente");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_historial_id_empleado",
                table: "solicitudes_historial",
                column: "id_empleado");

            migrationBuilder.CreateIndex(
                name: "ix_solicitudes_historial_solicitud",
                table: "solicitudes_historial",
                columns: new[] { "id_solicitud", "fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adjuntos_solicitud");

            migrationBuilder.DropTable(
                name: "solicitudes_historial");

            migrationBuilder.DropIndex(
                name: "ix_solicitudes_huella_ip",
                table: "solicitudes");

            migrationBuilder.DropColumn(
                name: "huella_ip",
                table: "solicitudes");

            migrationBuilder.AlterColumn<string>(
                name: "motivo_rechazo",
                table: "solicitudes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "descripcion_general",
                table: "solicitudes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "descripcion",
                table: "solicitud_detalles",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);
        }
    }
}
