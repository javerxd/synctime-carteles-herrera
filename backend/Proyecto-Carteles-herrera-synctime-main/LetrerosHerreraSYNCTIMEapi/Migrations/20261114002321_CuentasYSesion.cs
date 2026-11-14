using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <inheritdoc />
    public partial class CuentasYSesion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_verificacion_correo",
                table: "clientes",
                type: "datetimeoffset(0)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "codigos_cuenta",
                columns: table => new
                {
                    id_codigo = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_cliente = table.Column<int>(type: "int", nullable: true),
                    id_empleado = table.Column<int>(type: "int", nullable: true),
                    proposito = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    hash_codigo = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    correo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    fecha_expiracion = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    fecha_uso = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    intentos = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codigos_cuenta", x => x.id_codigo);
                    table.CheckConstraint("ck_codigos_cuenta_intentos", "intentos >= 0");
                    table.CheckConstraint("ck_codigos_cuenta_proposito", "proposito IN ('verificacion_correo', 'recuperacion')");
                    table.CheckConstraint("ck_codigos_cuenta_titular", "(CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "fk_codigos_cuenta_cliente",
                        column: x => x.id_cliente,
                        principalTable: "clientes",
                        principalColumn: "id_cliente",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_codigos_cuenta_empleado",
                        column: x => x.id_empleado,
                        principalTable: "empleados",
                        principalColumn: "id_empleado",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tokens_renovacion",
                columns: table => new
                {
                    id_token = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_cliente = table.Column<int>(type: "int", nullable: true),
                    id_empleado = table.Column<int>(type: "int", nullable: true),
                    hash_token = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    familia = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    fecha_expiracion = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    fecha_uso = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: true),
                    fecha_revocacion = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tokens_renovacion", x => x.id_token);
                    table.CheckConstraint("ck_tokens_renovacion_titular", "(CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "fk_tokens_renovacion_cliente",
                        column: x => x.id_cliente,
                        principalTable: "clientes",
                        principalColumn: "id_cliente",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tokens_renovacion_empleado",
                        column: x => x.id_empleado,
                        principalTable: "empleados",
                        principalColumn: "id_empleado",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_clientes_cuenta_con_correo",
                table: "clientes",
                sql: "password_hash IS NULL OR email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_codigos_cuenta_cliente",
                table: "codigos_cuenta",
                columns: new[] { "id_cliente", "proposito" },
                filter: "[id_cliente] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_codigos_cuenta_empleado",
                table: "codigos_cuenta",
                columns: new[] { "id_empleado", "proposito" },
                filter: "[id_empleado] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_codigos_cuenta_expiracion",
                table: "codigos_cuenta",
                column: "fecha_expiracion");

            migrationBuilder.CreateIndex(
                name: "ix_codigos_cuenta_hash",
                table: "codigos_cuenta",
                column: "hash_codigo");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_renovacion_cliente",
                table: "tokens_renovacion",
                column: "id_cliente",
                filter: "[id_cliente] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_renovacion_empleado",
                table: "tokens_renovacion",
                column: "id_empleado",
                filter: "[id_empleado] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_renovacion_expiracion",
                table: "tokens_renovacion",
                column: "fecha_expiracion");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_renovacion_familia",
                table: "tokens_renovacion",
                column: "familia");

            migrationBuilder.CreateIndex(
                name: "uq_tokens_renovacion_hash",
                table: "tokens_renovacion",
                column: "hash_token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "codigos_cuenta");

            migrationBuilder.DropTable(
                name: "tokens_renovacion");

            migrationBuilder.DropCheckConstraint(
                name: "ck_clientes_cuenta_con_correo",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "fecha_verificacion_correo",
                table: "clientes");
        }
    }
}
