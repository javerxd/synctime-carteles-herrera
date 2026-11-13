using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <inheritdoc />
    public partial class ServiciosComunes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consentimientos",
                columns: table => new
                {
                    id_consentimiento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_cliente = table.Column<int>(type: "int", nullable: true),
                    id_empleado = table.Column<int>(type: "int", nullable: true),
                    referencia_envio = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    tipo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    version_documento = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    fecha_aceptacion = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    fecha_revocacion = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consentimientos", x => x.id_consentimiento);
                    table.CheckConstraint("ck_consentimientos_revocacion", "fecha_revocacion IS NULL OR fecha_revocacion >= fecha_aceptacion");
                    table.CheckConstraint("ck_consentimientos_tipo", "tipo IN ('privacidad', 'terminos', 'marketing', 'cookies')");
                    table.CheckConstraint("ck_consentimientos_titular", "(CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) + (CASE WHEN referencia_envio IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "fk_consentimientos_cliente",
                        column: x => x.id_cliente,
                        principalTable: "clientes",
                        principalColumn: "id_cliente");
                    table.ForeignKey(
                        name: "fk_consentimientos_empleado",
                        column: x => x.id_empleado,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                });

            migrationBuilder.CreateTable(
                name: "correos_salientes",
                columns: table => new
                {
                    id_correo = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tipo = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    referencia = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    destinatarios = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    responder_a = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    asunto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    cuerpo_html = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    cuerpo_texto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    estado = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "pendiente"),
                    intentos = table.Column<int>(type: "int", nullable: false),
                    proximo_intento = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    bloqueado_hasta = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    ultimo_error = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    fecha_envio = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_correos_salientes", x => x.id_correo);
                    table.CheckConstraint("ck_correos_salientes_estado", "estado IN ('pendiente', 'enviando', 'enviado', 'fallido')");
                    table.CheckConstraint("ck_correos_salientes_intentos", "intentos >= 0");
                });

            migrationBuilder.CreateTable(
                name: "imagenes",
                columns: table => new
                {
                    id_imagen = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    clave = table.Column<string>(type: "char(32)", unicode: false, fixedLength: true, maxLength: 32, nullable: false),
                    ancho = table.Column<int>(type: "int", nullable: false),
                    alto = table.Column<int>(type: "int", nullable: false),
                    bytes_grande = table.Column<int>(type: "int", nullable: false),
                    bytes_miniatura = table.Column<int>(type: "int", nullable: false),
                    id_empleado_sube = table.Column<int>(type: "int", nullable: false),
                    fecha_subida = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imagenes", x => x.id_imagen);
                    table.CheckConstraint("ck_imagenes_medidas", "ancho > 0 AND alto > 0 AND bytes_grande > 0 AND bytes_miniatura > 0");
                    table.ForeignKey(
                        name: "fk_imagenes_empleado",
                        column: x => x.id_empleado_sube,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                });

            migrationBuilder.CreateIndex(
                name: "ix_consentimientos_cliente",
                table: "consentimientos",
                columns: new[] { "id_cliente", "tipo" },
                filter: "[id_cliente] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_consentimientos_empleado",
                table: "consentimientos",
                columns: new[] { "id_empleado", "tipo" },
                filter: "[id_empleado] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_consentimientos_envio",
                table: "consentimientos",
                columns: new[] { "referencia_envio", "tipo" },
                filter: "[referencia_envio] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_consentimientos_cliente_vigente",
                table: "consentimientos",
                columns: new[] { "id_cliente", "tipo", "version_documento" },
                unique: true,
                filter: "[id_cliente] IS NOT NULL AND [fecha_revocacion] IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_consentimientos_empleado_vigente",
                table: "consentimientos",
                columns: new[] { "id_empleado", "tipo", "version_documento" },
                unique: true,
                filter: "[id_empleado] IS NOT NULL AND [fecha_revocacion] IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_consentimientos_envio_vigente",
                table: "consentimientos",
                columns: new[] { "referencia_envio", "tipo", "version_documento" },
                unique: true,
                filter: "[referencia_envio] IS NOT NULL AND [fecha_revocacion] IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_correos_salientes_fecha",
                table: "correos_salientes",
                column: "fecha_creacion");

            migrationBuilder.CreateIndex(
                name: "ix_correos_salientes_pendientes",
                table: "correos_salientes",
                columns: new[] { "estado", "proximo_intento" },
                filter: "[estado] IN ('pendiente', 'enviando')");

            migrationBuilder.CreateIndex(
                name: "ix_correos_salientes_referencia",
                table: "correos_salientes",
                column: "referencia",
                filter: "[referencia] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_imagenes_empleado",
                table: "imagenes",
                column: "id_empleado_sube");

            migrationBuilder.CreateIndex(
                name: "uq_imagenes_clave",
                table: "imagenes",
                column: "clave",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consentimientos");

            migrationBuilder.DropTable(
                name: "correos_salientes");

            migrationBuilder.DropTable(
                name: "imagenes");
        }
    }
}
