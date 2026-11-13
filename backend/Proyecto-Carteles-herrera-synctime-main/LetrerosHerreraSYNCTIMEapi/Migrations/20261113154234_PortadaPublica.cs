using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <inheritdoc />
    public partial class PortadaPublica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "es_ejemplo",
                table: "imagenes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "imagen_url",
                table: "catalogo",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_autorizacion_logo",
                table: "catalogo",
                type: "datetimeoffset(0)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "id_empleado_autoriza_logo",
                table: "catalogo",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "id_imagen",
                table: "catalogo",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "incluye_logo_cliente",
                table: "catalogo",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "consultas_contacto",
                columns: table => new
                {
                    id_consulta = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    correo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    asunto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    mensaje = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    fecha_recepcion = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    revisada = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    fecha_revision = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consultas_contacto", x => x.id_consulta);
                });

            migrationBuilder.CreateIndex(
                name: "ix_catalogo_autoriza_logo",
                table: "catalogo",
                column: "id_empleado_autoriza_logo");

            migrationBuilder.CreateIndex(
                name: "ix_catalogo_imagen",
                table: "catalogo",
                column: "id_imagen");

            migrationBuilder.AddCheckConstraint(
                name: "ck_catalogo_autorizacion_logo",
                table: "catalogo",
                sql: "(id_empleado_autoriza_logo IS NULL AND fecha_autorizacion_logo IS NULL) OR (id_empleado_autoriza_logo IS NOT NULL AND fecha_autorizacion_logo IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_catalogo_logo",
                table: "catalogo",
                sql: "visible = 0 OR incluye_logo_cliente = 0 OR fecha_autorizacion_logo IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_consultas_contacto_bandeja",
                table: "consultas_contacto",
                columns: new[] { "revisada", "fecha_recepcion" });

            migrationBuilder.AddForeignKey(
                name: "fk_catalogo_empleado_autoriza_logo",
                table: "catalogo",
                column: "id_empleado_autoriza_logo",
                principalTable: "empleados",
                principalColumn: "id_empleado");

            migrationBuilder.AddForeignKey(
                name: "fk_catalogo_imagen",
                table: "catalogo",
                column: "id_imagen",
                principalTable: "imagenes",
                principalColumn: "id_imagen");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_catalogo_empleado_autoriza_logo",
                table: "catalogo");

            migrationBuilder.DropForeignKey(
                name: "fk_catalogo_imagen",
                table: "catalogo");

            migrationBuilder.DropTable(
                name: "consultas_contacto");

            migrationBuilder.DropIndex(
                name: "ix_catalogo_autoriza_logo",
                table: "catalogo");

            migrationBuilder.DropIndex(
                name: "ix_catalogo_imagen",
                table: "catalogo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_catalogo_autorizacion_logo",
                table: "catalogo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_catalogo_logo",
                table: "catalogo");

            migrationBuilder.DropColumn(
                name: "es_ejemplo",
                table: "imagenes");

            migrationBuilder.DropColumn(
                name: "fecha_autorizacion_logo",
                table: "catalogo");

            migrationBuilder.DropColumn(
                name: "id_empleado_autoriza_logo",
                table: "catalogo");

            migrationBuilder.DropColumn(
                name: "id_imagen",
                table: "catalogo");

            migrationBuilder.DropColumn(
                name: "incluye_logo_cliente",
                table: "catalogo");

            migrationBuilder.AlterColumn<string>(
                name: "imagen_url",
                table: "catalogo",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
