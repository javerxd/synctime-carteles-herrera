using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    // Fase 14C (RFS03, RFU05, RNFE02): el personal tambien entra por Google. La identidad externa pasa a ser de un cliente O de un empleado.
    // - Nueva columna id_empleado (FK a empleados, borrado en cascada) y id_cliente pasa a admitir nulo.
    // - CHECK ck_identidades_externas_destino: exactamente uno de los dos. Las filas que ya existen (todas de clientes) lo cumplen.
    // - Los indices unicos por cuenta pasan a ser FILTRADOS (con nulos, un indice normal solo admitiria una fila): uno por cliente y uno por empleado.
    //   El indice unico (proveedor, sub) no cambia: un mismo Google no puede estar en dos cuentas, sea de cliente o de empleado.
    // No toca otras tablas ni filas. Se aplica solo a la base de demostracion y a las de pruebas (Program.cs); la base real la recibe al final.
    // Down: borra las identidades de empleados (no caben en el esquema anterior) y vuelve a lo de la 14A.
    /// <inheritdoc />
    public partial class GoogleParaPersonal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_identidades_externas_cliente",
                table: "identidades_externas");

            migrationBuilder.AlterColumn<int>(
                name: "id_cliente",
                table: "identidades_externas",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "id_empleado",
                table: "identidades_externas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_identidades_externas_cliente",
                table: "identidades_externas",
                columns: new[] { "id_cliente", "proveedor" },
                unique: true,
                filter: "[id_cliente] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_identidades_externas_empleado",
                table: "identidades_externas",
                columns: new[] { "id_empleado", "proveedor" },
                unique: true,
                filter: "[id_empleado] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_identidades_externas_destino",
                table: "identidades_externas",
                sql: "(CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) = 1");

            migrationBuilder.AddForeignKey(
                name: "fk_identidades_externas_empleado",
                table: "identidades_externas",
                column: "id_empleado",
                principalTable: "empleados",
                principalColumn: "id_empleado",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM identidades_externas WHERE id_cliente IS NULL");

            migrationBuilder.DropForeignKey(
                name: "fk_identidades_externas_empleado",
                table: "identidades_externas");

            migrationBuilder.DropIndex(
                name: "uq_identidades_externas_cliente",
                table: "identidades_externas");

            migrationBuilder.DropIndex(
                name: "uq_identidades_externas_empleado",
                table: "identidades_externas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_identidades_externas_destino",
                table: "identidades_externas");

            migrationBuilder.DropColumn(
                name: "id_empleado",
                table: "identidades_externas");

            migrationBuilder.AlterColumn<int>(
                name: "id_cliente",
                table: "identidades_externas",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_identidades_externas_cliente",
                table: "identidades_externas",
                columns: new[] { "id_cliente", "proveedor" },
                unique: true);
        }
    }
}
