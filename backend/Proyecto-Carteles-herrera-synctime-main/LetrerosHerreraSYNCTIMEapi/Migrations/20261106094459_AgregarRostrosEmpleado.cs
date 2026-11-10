using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRostrosEmpleado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rostros_empleado",
                columns: table => new
                {
                    id_rostro = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_empleado = table.Column<int>(type: "int", nullable: false),
                    descriptor_cifrado = table.Column<byte[]>(type: "varbinary(1024)", maxLength: 1024, nullable: true),
                    fecha_consentimiento = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    version_consentimiento = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    fecha_revocacion = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    intentos_fallidos = table.Column<int>(type: "int", nullable: false),
                    bloqueado_hasta = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    token_equipo_hash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: true),
                    fecha_autorizacion_equipo = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rostros_empleado", x => x.id_rostro);
                    table.ForeignKey(
                        name: "fk_rostros_empleado_empleado",
                        column: x => x.id_empleado,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                });

            migrationBuilder.CreateIndex(
                name: "uq_rostros_empleado_vigente",
                table: "rostros_empleado",
                column: "id_empleado",
                unique: true,
                filter: "[fecha_revocacion] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rostros_empleado");
        }
    }
}
