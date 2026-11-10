using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateV6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categorias",
                columns: table => new
                {
                    id_categoria = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categorias", x => x.id_categoria);
                });

            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    id_cliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    rut = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: true),
                    nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    apellido = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    telefono = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    direccion = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    password_hash = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    registrado = table.Column<bool>(type: "bit", nullable: false, computedColumnSql: "CONVERT(BIT, CASE WHEN password_hash IS NOT NULL THEN 1 ELSE 0 END)", stored: true),
                    fecha_registro = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clientes", x => x.id_cliente);
                    table.CheckConstraint("ck_clientes_email", "email IS NULL OR (email LIKE N'%_@_%._%' AND email NOT LIKE N'% %')");
                });

            migrationBuilder.CreateTable(
                name: "empleados",
                columns: table => new
                {
                    id_empleado = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    rut = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    apellido = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    telefono = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "administrador"),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_empleados", x => x.id_empleado);
                    table.CheckConstraint("ck_empleados_email", "email LIKE N'%_@_%._%' AND email NOT LIKE N'% %'");
                    table.CheckConstraint("ck_empleados_rol", "rol IN (N'administrador', N'dueño', N'diseñador', N'operario')");
                });

            migrationBuilder.CreateTable(
                name: "horarios_atencion",
                columns: table => new
                {
                    id_horario = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    dia_semana = table.Column<byte>(type: "tinyint", nullable: false),
                    nombre_dia = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    hora_fin = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    abierto = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_horarios_atencion", x => x.id_horario);
                    table.CheckConstraint("ck_horarios_dia", "dia_semana BETWEEN 1 AND 7");
                    table.CheckConstraint("ck_horarios_rango", "abierto = 0 OR (hora_inicio IS NOT NULL AND hora_fin IS NOT NULL AND hora_fin > hora_inicio)");
                });

            migrationBuilder.CreateTable(
                name: "materiales",
                columns: table => new
                {
                    id_material = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    unidad_medida = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    costo_unitario_aprox = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materiales", x => x.id_material);
                    table.CheckConstraint("ck_materiales_costo", "costo_unitario_aprox >= 0");
                });

            migrationBuilder.CreateTable(
                name: "productos",
                columns: table => new
                {
                    id_producto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_categoria = table.Column<int>(type: "int", nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    precio_base_ref = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_productos", x => x.id_producto);
                    table.CheckConstraint("ck_productos_precio", "precio_base_ref IS NULL OR precio_base_ref >= 0");
                    table.ForeignKey(
                        name: "fk_productos_categoria",
                        column: x => x.id_categoria,
                        principalTable: "categorias",
                        principalColumn: "id_categoria",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cartel_digital",
                columns: table => new
                {
                    id_cartel = table.Column<int>(type: "int", nullable: false),
                    abierto_en_local = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    mensaje_estado = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false, defaultValue: "Local abierto y atendiendo"),
                    tiempo_estimado_regreso = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    fecha_ultima_modificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    id_empleado_modifica = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cartel_digital", x => x.id_cartel);
                    table.CheckConstraint("ck_cartel_unico", "id_cartel = 1");
                    table.ForeignKey(
                        name: "fk_cartel_digital_empleado",
                        column: x => x.id_empleado_modifica,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                });

            migrationBuilder.CreateTable(
                name: "solicitudes",
                columns: table => new
                {
                    id_solicitud = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_cliente = table.Column<int>(type: "int", nullable: false),
                    id_empleado_registra = table.Column<int>(type: "int", nullable: true),
                    origen = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false, computedColumnSql: "CASE WHEN id_empleado_registra IS NULL THEN N'online' ELSE N'presencial' END", stored: true),
                    descripcion_general = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    fecha_solicitud = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    estado = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false, defaultValue: "solicitado"),
                    motivo_rechazo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    eliminado_logico = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    fecha_eliminacion_logica = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitudes", x => x.id_solicitud);
                    table.CheckConstraint("ck_solicitudes_estado", "estado IN (N'solicitado', N'cotizado', N'aceptado', N'rechazado')");
                    table.ForeignKey(
                        name: "fk_solicitudes_cliente",
                        column: x => x.id_cliente,
                        principalTable: "clientes",
                        principalColumn: "id_cliente",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_solicitudes_empleado",
                        column: x => x.id_empleado_registra,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                });

            migrationBuilder.CreateTable(
                name: "catalogo",
                columns: table => new
                {
                    id_catalogo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_producto = table.Column<int>(type: "int", nullable: false),
                    id_empleado_creador = table.Column<int>(type: "int", nullable: true),
                    titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    imagen_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ancho_mm = table.Column<int>(type: "int", nullable: true),
                    alto_mm = table.Column<int>(type: "int", nullable: true),
                    precio_referencial = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    fecha_publicacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    visible = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogo", x => x.id_catalogo);
                    table.CheckConstraint("ck_catalogo_medidas", "(ancho_mm IS NULL AND alto_mm IS NULL) OR (ancho_mm > 0 AND alto_mm > 0)");
                    table.CheckConstraint("ck_catalogo_precio", "precio_referencial IS NULL OR precio_referencial >= 0");
                    table.ForeignKey(
                        name: "fk_catalogo_empleado",
                        column: x => x.id_empleado_creador,
                        principalTable: "empleados",
                        principalColumn: "id_empleado");
                    table.ForeignKey(
                        name: "fk_catalogo_producto",
                        column: x => x.id_producto,
                        principalTable: "productos",
                        principalColumn: "id_producto",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cotizaciones",
                columns: table => new
                {
                    id_cotizacion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_solicitud = table.Column<int>(type: "int", nullable: false),
                    id_empleado = table.Column<int>(type: "int", nullable: false),
                    fecha_cotizacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    costo_materiales_total = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                    costo_mano_obra = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                    otros_costos = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                    total_cotizacion = table.Column<decimal>(type: "decimal(14,2)", nullable: false, computedColumnSql: "CONVERT(DECIMAL(14,2), costo_materiales_total + costo_mano_obra + otros_costos)", stored: true),
                    estado = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false, defaultValue: "pendiente")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotizaciones", x => x.id_cotizacion);
                    table.CheckConstraint("ck_cotizaciones_costos", "costo_materiales_total >= 0 AND costo_mano_obra >= 0 AND otros_costos >= 0");
                    table.CheckConstraint("ck_cotizaciones_estado", "estado IN (N'pendiente', N'aprobada', N'rechazada')");
                    table.ForeignKey(
                        name: "fk_cotizaciones_empleado",
                        column: x => x.id_empleado,
                        principalTable: "empleados",
                        principalColumn: "id_empleado",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cotizaciones_solicitud",
                        column: x => x.id_solicitud,
                        principalTable: "solicitudes",
                        principalColumn: "id_solicitud",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "solicitud_detalles",
                columns: table => new
                {
                    id_solicitud_detalle = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_solicitud = table.Column<int>(type: "int", nullable: false),
                    id_producto = table.Column<int>(type: "int", nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ancho_mm = table.Column<int>(type: "int", nullable: true),
                    alto_mm = table.Column<int>(type: "int", nullable: true),
                    cantidad = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    area_m2 = table.Column<decimal>(type: "decimal(12,4)", nullable: true, computedColumnSql: "CONVERT(DECIMAL(12,4), CONVERT(BIGINT, ancho_mm) * alto_mm / 1000000.0)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitud_detalles", x => x.id_solicitud_detalle);
                    table.CheckConstraint("ck_sol_detalles_cantidad", "cantidad > 0");
                    table.CheckConstraint("ck_sol_detalles_medidas", "(ancho_mm IS NULL AND alto_mm IS NULL) OR (ancho_mm > 0 AND alto_mm > 0)");
                    table.ForeignKey(
                        name: "fk_sol_detalles_producto",
                        column: x => x.id_producto,
                        principalTable: "productos",
                        principalColumn: "id_producto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sol_detalles_solicitud",
                        column: x => x.id_solicitud,
                        principalTable: "solicitudes",
                        principalColumn: "id_solicitud",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cotizacion_materiales",
                columns: table => new
                {
                    id_cotizacion_material = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_cotizacion = table.Column<int>(type: "int", nullable: false),
                    id_material = table.Column<int>(type: "int", nullable: false),
                    medida_aplicada = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    cantidad = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    costo_unitario = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(14,2)", nullable: false, computedColumnSql: "CONVERT(DECIMAL(14,2), cantidad * costo_unitario)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotizacion_materiales", x => x.id_cotizacion_material);
                    table.CheckConstraint("ck_cot_mat_cantidad", "cantidad > 0");
                    table.CheckConstraint("ck_cot_mat_costo", "costo_unitario >= 0");
                    table.ForeignKey(
                        name: "fk_cot_mat_cotizacion",
                        column: x => x.id_cotizacion,
                        principalTable: "cotizaciones",
                        principalColumn: "id_cotizacion",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cot_mat_material",
                        column: x => x.id_material,
                        principalTable: "materiales",
                        principalColumn: "id_material",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedidos",
                columns: table => new
                {
                    id_pedido = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_solicitud = table.Column<int>(type: "int", nullable: false),
                    id_cotizacion = table.Column<int>(type: "int", nullable: true),
                    id_empleado_responsable = table.Column<int>(type: "int", nullable: false),
                    fecha_pedido = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    fecha_entrega_estimada = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    fecha_entrega_real = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    modalidad_entrega = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false, defaultValue: "retiro_local"),
                    requiere_instalacion = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    direccion_instalacion = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    costo_instalacion_adicional = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                    monto_base_trabajo = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                    monto_total_final = table.Column<decimal>(type: "decimal(14,2)", nullable: false, computedColumnSql: "CONVERT(DECIMAL(14,2), monto_base_trabajo + costo_instalacion_adicional)", stored: true),
                    estado = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false, defaultValue: "aceptado"),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedidos", x => x.id_pedido);
                    table.CheckConstraint("ck_pedidos_costos", "costo_instalacion_adicional >= 0 AND monto_base_trabajo >= 0");
                    table.CheckConstraint("ck_pedidos_estado", "estado IN (N'aceptado', N'en_proceso', N'terminado', N'entregado', N'cancelado')");
                    table.CheckConstraint("ck_pedidos_modalidad", "modalidad_entrega IN (N'retiro_local', N'entrega_domicilio', N'instalacion_terreno')");
                    table.ForeignKey(
                        name: "fk_pedidos_cotizacion",
                        column: x => x.id_cotizacion,
                        principalTable: "cotizaciones",
                        principalColumn: "id_cotizacion");
                    table.ForeignKey(
                        name: "fk_pedidos_empleado",
                        column: x => x.id_empleado_responsable,
                        principalTable: "empleados",
                        principalColumn: "id_empleado",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedidos_solicitud",
                        column: x => x.id_solicitud,
                        principalTable: "solicitudes",
                        principalColumn: "id_solicitud",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "cartel_digital",
                columns: new[] { "id_cartel", "abierto_en_local", "id_empleado_modifica", "mensaje_estado", "tiempo_estimado_regreso" },
                values: new object[] { 1, true, null, "Local abierto y atendiendo", null });

            migrationBuilder.InsertData(
                table: "categorias",
                columns: new[] { "id_categoria", "activo", "descripcion", "nombre" },
                values: new object[,]
                {
                    { 1, true, "Letreros comerciales, luminosos, palomitas, metálicos o en madera", "Letreros" },
                    { 2, true, "Rotulación, ploteo y gráficas para vehículos", "Vehículos" },
                    { 3, true, "Estampado textil, promocional y serigrafía", "Estampados" },
                    { 4, true, "Pintura mural, fachadas y gráfica de gran formato", "Muros" },
                    { 5, true, "Trabajos especiales a medida del cliente", "Personalizado / Otro" }
                });

            migrationBuilder.InsertData(
                table: "horarios_atencion",
                columns: new[] { "id_horario", "abierto", "dia_semana", "hora_fin", "hora_inicio", "nombre_dia" },
                values: new object[,]
                {
                    { 1, true, (byte)1, new TimeOnly(18, 30, 0), new TimeOnly(9, 0, 0), "Lunes" },
                    { 2, true, (byte)2, new TimeOnly(18, 30, 0), new TimeOnly(9, 0, 0), "Martes" },
                    { 3, true, (byte)3, new TimeOnly(18, 30, 0), new TimeOnly(9, 0, 0), "Miércoles" },
                    { 4, true, (byte)4, new TimeOnly(18, 30, 0), new TimeOnly(9, 0, 0), "Jueves" },
                    { 5, true, (byte)5, new TimeOnly(18, 30, 0), new TimeOnly(9, 0, 0), "Viernes" },
                    { 6, true, (byte)6, new TimeOnly(14, 0, 0), new TimeOnly(9, 0, 0), "Sábado" }
                });

            migrationBuilder.InsertData(
                table: "materiales",
                columns: new[] { "id_material", "activo", "costo_unitario_aprox", "nombre", "unidad_medida" },
                values: new object[,]
                {
                    { 1, true, 12500m, "Madera", "m2" },
                    { 2, true, 8900m, "Pintura", "litro" },
                    { 3, true, 6500m, "Lienzo PVC", "m2" },
                    { 4, true, 7200m, "Vinilo Adhesivo", "m2" },
                    { 5, true, 18000m, "Plancha Metálica", "m2" }
                });

            migrationBuilder.InsertData(
                table: "productos",
                columns: new[] { "id_producto", "activo", "descripcion", "id_categoria", "nombre", "precio_base_ref" },
                values: new object[,]
                {
                    { 1, true, "Estructura metálica con gráfica impresa", 1, "Cartel metálico impreso", 45000m },
                    { 2, true, "Lienzo PVC con ojetillos perimetrales", 1, "Lienzo publicitario", 25000m },
                    { 3, true, "Letrero publicitario doble cara plegable", 1, "Letrero tipo Palomita", 35000m },
                    { 4, true, "Diseño y fabricación según medidas", 1, "Trabajo personalizado", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_cartel_digital_id_empleado_modifica",
                table: "cartel_digital",
                column: "id_empleado_modifica");

            migrationBuilder.CreateIndex(
                name: "IX_catalogo_id_empleado_creador",
                table: "catalogo",
                column: "id_empleado_creador");

            migrationBuilder.CreateIndex(
                name: "IX_catalogo_id_producto",
                table: "catalogo",
                column: "id_producto");

            migrationBuilder.CreateIndex(
                name: "ix_catalogo_visible_producto",
                table: "catalogo",
                columns: new[] { "visible", "id_producto" });

            migrationBuilder.CreateIndex(
                name: "uq_categorias_nombre",
                table: "categorias",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_clientes_email",
                table: "clientes",
                column: "email",
                unique: true,
                filter: "[email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_clientes_rut",
                table: "clientes",
                column: "rut",
                unique: true,
                filter: "[rut] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_clientes_telefono",
                table: "clientes",
                column: "telefono",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cot_mat_cotizacion",
                table: "cotizacion_materiales",
                column: "id_cotizacion");

            migrationBuilder.CreateIndex(
                name: "IX_cotizacion_materiales_id_material",
                table: "cotizacion_materiales",
                column: "id_material");

            migrationBuilder.CreateIndex(
                name: "IX_cotizaciones_id_empleado",
                table: "cotizaciones",
                column: "id_empleado");

            migrationBuilder.CreateIndex(
                name: "ix_cotizaciones_solicitud",
                table: "cotizaciones",
                columns: new[] { "id_solicitud", "estado" });

            migrationBuilder.CreateIndex(
                name: "uq_empleados_email",
                table: "empleados",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_empleados_rut",
                table: "empleados",
                column: "rut",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_horarios_dia",
                table: "horarios_atencion",
                column: "dia_semana",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_materiales_nombre",
                table: "materiales",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_calendario_entrega",
                table: "pedidos",
                columns: new[] { "fecha_entrega_estimada", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_fecha_estado",
                table: "pedidos",
                columns: new[] { "fecha_pedido", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_id_cotizacion",
                table: "pedidos",
                column: "id_cotizacion");

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_id_empleado_responsable",
                table: "pedidos",
                column: "id_empleado_responsable");

            migrationBuilder.CreateIndex(
                name: "uq_pedidos_solicitud",
                table: "pedidos",
                column: "id_solicitud",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_productos_categoria",
                table: "productos",
                columns: new[] { "id_categoria", "activo" });

            migrationBuilder.CreateIndex(
                name: "uq_productos_nombre",
                table: "productos",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sol_detalles_producto",
                table: "solicitud_detalles",
                column: "id_producto");

            migrationBuilder.CreateIndex(
                name: "ix_sol_detalles_solicitud",
                table: "solicitud_detalles",
                column: "id_solicitud");

            migrationBuilder.CreateIndex(
                name: "ix_solicitudes_cliente",
                table: "solicitudes",
                columns: new[] { "id_cliente", "eliminado_logico" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_id_empleado_registra",
                table: "solicitudes",
                column: "id_empleado_registra");

            migrationBuilder.CreateIndex(
                name: "ix_solicitudes_metricas",
                table: "solicitudes",
                columns: new[] { "fecha_solicitud", "origen", "estado", "eliminado_logico" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cartel_digital");

            migrationBuilder.DropTable(
                name: "catalogo");

            migrationBuilder.DropTable(
                name: "cotizacion_materiales");

            migrationBuilder.DropTable(
                name: "horarios_atencion");

            migrationBuilder.DropTable(
                name: "pedidos");

            migrationBuilder.DropTable(
                name: "solicitud_detalles");

            migrationBuilder.DropTable(
                name: "materiales");

            migrationBuilder.DropTable(
                name: "cotizaciones");

            migrationBuilder.DropTable(
                name: "productos");

            migrationBuilder.DropTable(
                name: "solicitudes");

            migrationBuilder.DropTable(
                name: "categorias");

            migrationBuilder.DropTable(
                name: "clientes");

            migrationBuilder.DropTable(
                name: "empleados");
        }
    }
}
