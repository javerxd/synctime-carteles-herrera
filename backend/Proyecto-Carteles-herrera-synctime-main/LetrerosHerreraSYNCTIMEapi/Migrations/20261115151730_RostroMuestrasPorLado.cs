using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <summary>
    /// Seguimiento de la fase 10 (decision del usuario del 15-11-2026): cada muestra se cifra con su lado
    /// (frente, izquierda o derecha) en los datos asociados, y esas filas llevan formato_muestras = 3. Solo cambia
    /// la regla ck_rostros_empleado_muestras para admitir el formato 3 con las mismas condiciones; no toca filas.
    /// Las de formato 2 (cifradas sin el lado) quedan como estan y la API las trata como «debe registrarse de nuevo».
    /// </summary>
    public partial class RostroMuestrasPorLado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rostros_empleado_muestras",
                table: "rostros_empleado");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rostros_empleado_muestras",
                table: "rostros_empleado",
                sql: "(formato_muestras IS NULL AND descriptor_frente IS NULL AND descriptor_izquierda IS NULL AND descriptor_derecha IS NULL) OR (formato_muestras IN (2, 3) AND ((fecha_revocacion IS NULL AND descriptor_frente IS NOT NULL AND descriptor_izquierda IS NOT NULL AND descriptor_derecha IS NOT NULL) OR (fecha_revocacion IS NOT NULL AND descriptor_frente IS NULL AND descriptor_izquierda IS NULL AND descriptor_derecha IS NULL)))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir no puede dejar filas que la regla anterior rechaza: si hay rostros de formato 3, se detiene
            // sin cambiar nada.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.rostros_empleado WHERE formato_muestras = 3)
                    THROW 50110, N'No se puede revertir RostroMuestrasPorLado: hay rostros registrados con las muestras atadas a su lado (formato 3). Respáldalos y bórralos a mano antes de revertir.', 1;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_rostros_empleado_muestras",
                table: "rostros_empleado");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rostros_empleado_muestras",
                table: "rostros_empleado",
                sql: "(formato_muestras IS NULL AND descriptor_frente IS NULL AND descriptor_izquierda IS NULL AND descriptor_derecha IS NULL) OR (formato_muestras = 2 AND ((fecha_revocacion IS NULL AND descriptor_frente IS NOT NULL AND descriptor_izquierda IS NOT NULL AND descriptor_derecha IS NOT NULL) OR (fecha_revocacion IS NOT NULL AND descriptor_frente IS NULL AND descriptor_izquierda IS NULL AND descriptor_derecha IS NULL)))");
        }
    }
}
