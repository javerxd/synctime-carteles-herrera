using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    // Fase 13A (RNFP18, RFS10): la tabla de trabajos de exportacion de reportes (Excel, PDF y CSV en segundo plano).
    // - Restricciones de reporte, formato y estado, y de coherencia (un trabajo «listo» tiene su archivo y vencimiento; uno «fallido», su motivo).
    // - Indice unico filtrado: una persona no puede tener mas de un trabajo pendiente o en proceso a la vez.
    // - La bitacora no necesita cambios: auditoria.accion no tiene restriccion de valores (la nueva accion es exportar_reporte).
    // No toca tablas, columnas ni filas existentes. Down: borra la tabla nueva.
    /// <inheritdoc />
    public partial class ExportacionesDeReportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trabajos_exportacion",
                columns: table => new
                {
                    id_trabajo = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reporte = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    formato = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    filtros = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: false),
                    estado = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false, defaultValue: "pendiente"),
                    id_empleado = table.Column<int>(type: "int", nullable: false),
                    rol_solicitante = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    fecha_pedido = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    fecha_inicio = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    fecha_termino = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    fecha_vencimiento = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    bloqueado_hasta = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    intentos = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    nombre_archivo = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    tamano_bytes = table.Column<int>(type: "int", nullable: true),
                    filas = table.Column<int>(type: "int", nullable: true),
                    error = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    archivo_borrado_en = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trabajos_exportacion", x => x.id_trabajo);
                    table.CheckConstraint("ck_trabajos_exportacion_estado", "estado IN ('pendiente', 'procesando', 'listo', 'fallido')");
                    table.CheckConstraint("ck_trabajos_exportacion_fallido", "estado <> 'fallido' OR error IS NOT NULL");
                    table.CheckConstraint("ck_trabajos_exportacion_formato", "formato IN ('xlsx', 'csv', 'pdf')");
                    table.CheckConstraint("ck_trabajos_exportacion_intentos", "intentos >= 0");
                    table.CheckConstraint("ck_trabajos_exportacion_listo", "estado <> 'listo' OR (fecha_termino IS NOT NULL AND fecha_vencimiento IS NOT NULL AND nombre_archivo IS NOT NULL AND filas IS NOT NULL)");
                    table.CheckConstraint("ck_trabajos_exportacion_reporte", "reporte IN ('operativo', 'gerencial', 'parametrizable')");
                    table.ForeignKey(
                        name: "fk_trabajos_exportacion_empleado",
                        column: x => x.id_empleado,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                });

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_exportacion_empleado",
                table: "trabajos_exportacion",
                columns: new[] { "id_empleado", "fecha_pedido" });

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_exportacion_estado",
                table: "trabajos_exportacion",
                columns: new[] { "estado", "fecha_pedido" });

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_exportacion_vencimiento",
                table: "trabajos_exportacion",
                column: "fecha_vencimiento",
                filter: "[estado] = 'listo' AND [archivo_borrado_en] IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_trabajos_exportacion_activo",
                table: "trabajos_exportacion",
                column: "id_empleado",
                unique: true,
                filter: "[estado] IN ('pendiente', 'procesando')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trabajos_exportacion");
        }
    }
}
