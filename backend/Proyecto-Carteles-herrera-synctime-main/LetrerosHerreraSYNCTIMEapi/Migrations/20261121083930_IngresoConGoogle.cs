using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    // Fase 14A (RFS03, RNFE02, RNFP17): la tabla de cuentas de Google vinculadas a un cliente (identidades_externas).
    // - La identidad es el «sub» del proveedor (columna binaria: distingue mayusculas y minusculas). Indice unico (proveedor, sub): un mismo Google no puede estar en dos cuentas.
    // - Indice unico (id_cliente, proveedor): una cuenta no puede tener dos Google. El proveedor tiene su CHECK para poder sumar otros.
    // - Nunca se guarda el token de Google: solo quien es, su correo verificado y cuando entro.
    // - clientes.password_hash ya admite nulo: una cuenta creada con Google no tiene contraseña. La bitacora no necesita cambios:
    //   auditoria.accion no tiene restriccion de valores (las nuevas son ingreso_google, vincular_google y desvincular_google).
    // No toca tablas, columnas ni filas existentes. Se aplica solo a la base de demostracion y a las de pruebas (Program.cs);
    // la base real la recibe a mano y con respaldo previo, al final del proyecto. Down: borra la tabla nueva.
    /// <inheritdoc />
    public partial class IngresoConGoogle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "identidades_externas",
                columns: table => new
                {
                    id_identidad = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_cliente = table.Column<int>(type: "int", nullable: false),
                    proveedor = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    sub = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false, collation: "Latin1_General_BIN2"),
                    correo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    fecha_vinculacion = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    fecha_ultimo_ingreso = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identidades_externas", x => x.id_identidad);
                    table.CheckConstraint("ck_identidades_externas_proveedor", "proveedor IN ('google')");
                    table.ForeignKey(
                        name: "fk_identidades_externas_cliente",
                        column: x => x.id_cliente,
                        principalTable: "clientes",
                        principalColumn: "id_cliente",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_identidades_externas_cliente",
                table: "identidades_externas",
                columns: new[] { "id_cliente", "proveedor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_identidades_externas_sub",
                table: "identidades_externas",
                columns: new[] { "proveedor", "sub" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "identidades_externas");
        }
    }
}
