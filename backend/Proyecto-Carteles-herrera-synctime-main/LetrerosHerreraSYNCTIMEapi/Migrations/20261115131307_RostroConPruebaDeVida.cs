using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <summary>
    /// Fase 10 (plan aprobado el 15-11-2026): rostro con prueba de vida. Solo agrega: tres columnas cifradas
    /// para las muestras de frente, izquierda y derecha, quien inicio el registro y el formato de las muestras
    /// en rostros_empleado (todas nulas, asi que las filas existentes no cambian), y dos tablas nuevas:
    /// desafios_rostro (hash del desafio, hash del email, orden de los giros y su vigencia) y
    /// calibracion_rostro (solo distancias y giros, sin datos biometricos). El descriptor promedio antiguo
    /// (descriptor_cifrado) se conserva y deja de usarse: un rostro vigente sin las tres muestras debe
    /// registrarse de nuevo.
    /// </summary>
    public partial class RostroConPruebaDeVida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "descriptor_derecha",
                table: "rostros_empleado",
                type: "varbinary(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "descriptor_frente",
                table: "rostros_empleado",
                type: "varbinary(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "descriptor_izquierda",
                table: "rostros_empleado",
                type: "varbinary(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "formato_muestras",
                table: "rostros_empleado",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "id_empleado_registra",
                table: "rostros_empleado",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "calibracion_rostro",
                columns: table => new
                {
                    id_medicion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_empleado = table.Column<int>(type: "int", nullable: false),
                    etiqueta = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    distancia_frente = table.Column<double>(type: "float", nullable: false),
                    distancia_primer_giro = table.Column<double>(type: "float", nullable: false),
                    distancia_segundo_giro = table.Column<double>(type: "float", nullable: false),
                    distancia_max_entre_capturas = table.Column<double>(type: "float", nullable: false),
                    giro_frente = table.Column<double>(type: "float", nullable: false),
                    giro_primero = table.Column<double>(type: "float", nullable: false),
                    giro_segundo = table.Column<double>(type: "float", nullable: false),
                    umbral_vigente = table.Column<double>(type: "float", nullable: false),
                    id_empleado_registra = table.Column<int>(type: "int", nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calibracion_rostro", x => x.id_medicion);
                    table.CheckConstraint("ck_calibracion_rostro_etiqueta", "etiqueta IN ('propio', 'otra_persona')");
                    table.ForeignKey(
                        name: "fk_calibracion_rostro_empleado",
                        column: x => x.id_empleado,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                    table.ForeignKey(
                        name: "fk_calibracion_rostro_registra",
                        column: x => x.id_empleado_registra,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                });

            migrationBuilder.CreateTable(
                name: "desafios_rostro",
                columns: table => new
                {
                    id_desafio = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    token_hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    correo_hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    orden = table.Column<string>(type: "varchar(17)", unicode: false, maxLength: 17, nullable: false),
                    emitido = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    vence = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    usado = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_desafios_rostro", x => x.id_desafio);
                    table.CheckConstraint("ck_desafios_rostro_orden", "orden IN ('izquierda_derecha', 'derecha_izquierda')");
                    table.CheckConstraint("ck_desafios_rostro_vigencia", "vence > emitido AND (usado IS NULL OR usado >= emitido)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_rostros_empleado_registra",
                table: "rostros_empleado",
                column: "id_empleado_registra");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rostros_empleado_muestras",
                table: "rostros_empleado",
                sql: "(formato_muestras IS NULL AND descriptor_frente IS NULL AND descriptor_izquierda IS NULL AND descriptor_derecha IS NULL) OR (formato_muestras = 2 AND ((fecha_revocacion IS NULL AND descriptor_frente IS NOT NULL AND descriptor_izquierda IS NOT NULL AND descriptor_derecha IS NOT NULL) OR (fecha_revocacion IS NOT NULL AND descriptor_frente IS NULL AND descriptor_izquierda IS NULL AND descriptor_derecha IS NULL)))");

            migrationBuilder.CreateIndex(
                name: "ix_calibracion_rostro_empleado",
                table: "calibracion_rostro",
                column: "id_empleado");

            migrationBuilder.CreateIndex(
                name: "ix_calibracion_rostro_registra",
                table: "calibracion_rostro",
                column: "id_empleado_registra");

            migrationBuilder.CreateIndex(
                name: "ix_desafios_rostro_emitido",
                table: "desafios_rostro",
                column: "emitido");

            migrationBuilder.CreateIndex(
                name: "uq_desafios_rostro_token",
                table: "desafios_rostro",
                column: "token_hash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_rostros_empleado_registra",
                table: "rostros_empleado",
                column: "id_empleado_registra",
                principalTable: "empleados",
                principalColumn: "id_empleado");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir nunca destruye datos en silencio: si ya hay desafios, mediciones de calibracion o algun
            // rostro con las columnas nuevas llenas, se detiene sin cambiar nada.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.desafios_rostro)
                   OR EXISTS (SELECT 1 FROM dbo.calibracion_rostro)
                    THROW 50100, N'No se puede revertir RostroConPruebaDeVida: hay desafíos de rostro o mediciones de calibración guardados. Respáldalos y bórralos a mano antes de revertir.', 1;
                IF EXISTS (SELECT 1 FROM dbo.rostros_empleado
                           WHERE descriptor_frente IS NOT NULL OR descriptor_izquierda IS NOT NULL OR descriptor_derecha IS NOT NULL
                              OR id_empleado_registra IS NOT NULL OR formato_muestras IS NOT NULL)
                    THROW 50101, N'No se puede revertir RostroConPruebaDeVida: hay rostros registrados con las tres muestras o con quién los registró. Respáldalos y bórralos a mano antes de revertir.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_rostros_empleado_registra",
                table: "rostros_empleado");

            migrationBuilder.DropTable(
                name: "calibracion_rostro");

            migrationBuilder.DropTable(
                name: "desafios_rostro");

            migrationBuilder.DropIndex(
                name: "ix_rostros_empleado_registra",
                table: "rostros_empleado");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rostros_empleado_muestras",
                table: "rostros_empleado");

            migrationBuilder.DropColumn(
                name: "descriptor_derecha",
                table: "rostros_empleado");

            migrationBuilder.DropColumn(
                name: "descriptor_frente",
                table: "rostros_empleado");

            migrationBuilder.DropColumn(
                name: "descriptor_izquierda",
                table: "rostros_empleado");

            migrationBuilder.DropColumn(
                name: "formato_muestras",
                table: "rostros_empleado");

            migrationBuilder.DropColumn(
                name: "id_empleado_registra",
                table: "rostros_empleado");
        }
    }
}
