using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetrerosHerreraSYNCTIMEapi.Migrations
{
    // Fase 12B: reemplaza el catalogo de prueba por el catalogo REAL del taller (4 categorias y 16 productos, con
    // sus medidas y opciones). Borra las categorias y los productos anteriores y los rehace con ids 1..4 y 1..16.
    // Lo que apuntaba a un producto viejo (publicaciones de la vitrina y items de solicitudes) se reasigna ANTES
    // al producto nuevo mas parecido, en dos pasos con productos temporales (ids 1001 en adelante) porque los ids
    // finales chocan con los viejos. En una base sin referencias (la real) solo se borra e inserta.
    // Down: restaura las 5 categorias y 4 productos de la semilla original y apunta TODO al producto 1; la
    // equivalencia fina del Up no se puede deshacer (no es reversible sin perdida de esa informacion).
    /// <inheritdoc />
    public partial class CatalogoRealDelTaller : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "opciones",
                table: "solicitud_detalles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "alto_max_mm",
                table: "productos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "alto_min_mm",
                table: "productos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ancho_max_mm",
                table: "productos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ancho_min_mm",
                table: "productos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "producto_medidas_fijas",
                columns: table => new
                {
                    id_medida = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_producto = table.Column<int>(type: "int", nullable: false),
                    ancho_mm = table.Column<int>(type: "int", nullable: false),
                    alto_mm = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_producto_medidas_fijas", x => x.id_medida);
                    table.CheckConstraint("ck_producto_medidas_fijas", "ancho_mm > 0 AND alto_mm > 0");
                    table.ForeignKey(
                        name: "fk_producto_medidas_fijas_producto",
                        column: x => x.id_producto,
                        principalTable: "productos",
                        principalColumn: "id_producto",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "producto_opciones",
                columns: table => new
                {
                    id_opcion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_producto = table.Column<int>(type: "int", nullable: false),
                    grupo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    valor = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_producto_opciones", x => x.id_opcion);
                    table.ForeignKey(
                        name: "fk_producto_opciones_producto",
                        column: x => x.id_producto,
                        principalTable: "productos",
                        principalColumn: "id_producto",
                        onDelete: ReferentialAction.Cascade);
                });

            // 1. Productos temporales (categoria temporal 1001): reciben lo que usa el catalogo viejo.
            migrationBuilder.Sql("""
                SET IDENTITY_INSERT dbo.categorias ON;
                INSERT INTO dbo.categorias (id_categoria, nombre, activo) VALUES (1001, N'tmp-12b', 1);
                SET IDENTITY_INSERT dbo.categorias OFF;
                SET IDENTITY_INSERT dbo.productos ON;
                INSERT INTO dbo.productos (id_producto, id_categoria, nombre, activo)
                SELECT 1000 + n, 1001, N'tmp-12b-' + CONVERT(nvarchar(4), n), 1
                FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14),(15),(16)) AS v(n);
                SET IDENTITY_INSERT dbo.productos OFF;
                """);

            // Seguridad: si algun producto con referencias no es de la semilla conocida, no se remapea en silencio: se detiene.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.productos p
                           WHERE p.id_producto < 1000 AND p.nombre NOT IN (N'Autoadhesivo troquelado', N'Caballete', N'Calcomanía de vinilo de corte', N'Cartel metálico impreso', N'Decoración en vinilo', N'Estampado textil', N'Letrero de fachada', N'Letrero luminoso', N'Letrero tipo Palomita', N'Lienzo publicitario', N'Lona publicitaria', N'Mural pintado', N'Pendón', N'Polera estampada', N'Rotulación de vehículo', N'Señalética', N'Trabajo personalizado', N'Vinilo para vitrina', N'Volantes y calendarios')
                             AND (EXISTS (SELECT 1 FROM dbo.catalogo k WHERE k.id_producto = p.id_producto)
                               OR EXISTS (SELECT 1 FROM dbo.solicitud_detalles d WHERE d.id_producto = p.id_producto)))
                    THROW 50130, N'CatalogoRealDelTaller: hay productos con publicaciones o solicitudes que no estan en la lista de equivalencias. Agregalos a la migracion antes de aplicarla.', 1;
                """);

            // 2. Equivalencias: lo que apuntaba a un producto viejo apunta al temporal del producto nuevo.
            migrationBuilder.Sql("""
                UPDATE dbo.catalogo SET id_producto = CASE (SELECT p.nombre FROM dbo.productos p WHERE p.id_producto = catalogo.id_producto)
                    WHEN N'Cartel metálico impreso' THEN 1009
                    WHEN N'Lienzo publicitario' THEN 1001
                    WHEN N'Letrero tipo Palomita' THEN 1012
                    WHEN N'Trabajo personalizado' THEN 1008
                    WHEN N'Rotulación de vehículo' THEN 1013
                    WHEN N'Polera estampada' THEN 1016
                    WHEN N'Mural pintado' THEN 1008
                    WHEN N'Letrero luminoso' THEN 1009
                    WHEN N'Letrero de fachada' THEN 1009
                    WHEN N'Estampado textil' THEN 1016
                    WHEN N'Vinilo para vitrina' THEN 1008
                    WHEN N'Lona publicitaria' THEN 1001
                    WHEN N'Calcomanía de vinilo de corte' THEN 1008
                    WHEN N'Pendón' THEN 1004
                    WHEN N'Decoración en vinilo' THEN 1008
                    WHEN N'Autoadhesivo troquelado' THEN 1008
                    WHEN N'Volantes y calendarios' THEN 1015
                    WHEN N'Señalética' THEN 1011
                    WHEN N'Caballete' THEN 1012
                    ELSE 1008 END
                WHERE id_producto < 1000;
                UPDATE dbo.solicitud_detalles SET id_producto = CASE (SELECT p.nombre FROM dbo.productos p WHERE p.id_producto = solicitud_detalles.id_producto)
                    WHEN N'Cartel metálico impreso' THEN 1009
                    WHEN N'Lienzo publicitario' THEN 1001
                    WHEN N'Letrero tipo Palomita' THEN 1012
                    WHEN N'Trabajo personalizado' THEN 1008
                    WHEN N'Rotulación de vehículo' THEN 1013
                    WHEN N'Polera estampada' THEN 1016
                    WHEN N'Mural pintado' THEN 1008
                    WHEN N'Letrero luminoso' THEN 1009
                    WHEN N'Letrero de fachada' THEN 1009
                    WHEN N'Estampado textil' THEN 1016
                    WHEN N'Vinilo para vitrina' THEN 1008
                    WHEN N'Lona publicitaria' THEN 1001
                    WHEN N'Calcomanía de vinilo de corte' THEN 1008
                    WHEN N'Pendón' THEN 1004
                    WHEN N'Decoración en vinilo' THEN 1008
                    WHEN N'Autoadhesivo troquelado' THEN 1008
                    WHEN N'Volantes y calendarios' THEN 1015
                    WHEN N'Señalética' THEN 1011
                    WHEN N'Caballete' THEN 1012
                    ELSE 1008 END
                WHERE id_producto < 1000;
                """);

            // 3. Se borra todo el catalogo anterior.
            migrationBuilder.Sql("DELETE FROM dbo.productos WHERE id_producto < 1000;");
            migrationBuilder.Sql("DELETE FROM dbo.categorias WHERE id_categoria < 1000;");

            // 4. El catalogo real, con sus ids definitivos.
            migrationBuilder.Sql("SET IDENTITY_INSERT dbo.categorias ON;");
            foreach (var c in CatalogoV1.Categorias)
            {
                migrationBuilder.Sql($"INSERT INTO dbo.categorias (id_categoria, nombre, descripcion, activo) VALUES ({c.Id}, N'{Q(c.Nombre)}', N'{Q(c.Descripcion)}', 1);");
            }
            migrationBuilder.Sql("SET IDENTITY_INSERT dbo.categorias OFF;");
            migrationBuilder.Sql("SET IDENTITY_INSERT dbo.productos ON;");
            foreach (var p in CatalogoV1.Productos)
            {
                migrationBuilder.Sql(
                    "INSERT INTO dbo.productos (id_producto, id_categoria, nombre, descripcion, activo, ancho_min_mm, ancho_max_mm, alto_min_mm, alto_max_mm) VALUES " +
                    $"({p.Id}, {p.IdCategoria}, N'{Q(p.Nombre)}', N'{Q(p.Descripcion)}', 1, {N(p.AnchoMin)}, {N(p.AnchoMax)}, {N(p.AltoMin)}, {N(p.AltoMax)});");
            }
            migrationBuilder.Sql("SET IDENTITY_INSERT dbo.productos OFF;");
            foreach (var p in CatalogoV1.Productos)
            {
                foreach (var (ancho, alto) in p.Fijas ?? [])
                {
                    migrationBuilder.Sql($"INSERT INTO dbo.producto_medidas_fijas (id_producto, ancho_mm, alto_mm) VALUES ({p.Id}, {ancho}, {alto});");
                }
                foreach (var (grupo, valores) in p.Opciones ?? [])
                {
                    for (var i = 0; i < valores.Length; i++)
                    {
                        migrationBuilder.Sql($"INSERT INTO dbo.producto_opciones (id_producto, grupo, valor, orden) VALUES ({p.Id}, N'{Q(grupo)}', N'{Q(valores[i])}', {i + 1});");
                    }
                }
            }

            // 5. Lo reasignado pasa de su producto temporal al definitivo, y los temporales se borran.
            migrationBuilder.Sql("""
                UPDATE dbo.catalogo SET id_producto = id_producto - 1000 WHERE id_producto > 1000;
                UPDATE dbo.solicitud_detalles SET id_producto = id_producto - 1000 WHERE id_producto > 1000;
                DELETE FROM dbo.productos WHERE id_producto > 1000;
                DELETE FROM dbo.categorias WHERE id_categoria = 1001;
                """);

            // Los temporales (1001+) subieron el contador: se deja en el ultimo id real, para que el proximo producto sea el 17.
            migrationBuilder.Sql("DBCC CHECKIDENT ('dbo.productos', RESEED, 16); DBCC CHECKIDENT ('dbo.categorias', RESEED, 4);");

            migrationBuilder.AddCheckConstraint(
                name: "ck_productos_medidas",
                table: "productos",
                sql: "(ancho_min_mm IS NULL AND ancho_max_mm IS NULL AND alto_min_mm IS NULL AND alto_max_mm IS NULL) OR (ancho_min_mm > 0 AND ancho_max_mm >= ancho_min_mm AND alto_min_mm > 0 AND alto_max_mm >= alto_min_mm)");

            migrationBuilder.CreateIndex(
                name: "uq_producto_medidas_fijas",
                table: "producto_medidas_fijas",
                columns: new[] { "id_producto", "ancho_mm", "alto_mm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_producto_opciones",
                table: "producto_opciones",
                columns: new[] { "id_producto", "grupo", "valor" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Todo lo que usa el catalogo real pasa a un producto temporal, que luego pasa al producto 1 original.
            migrationBuilder.Sql("""
                SET IDENTITY_INSERT dbo.categorias ON;
                INSERT INTO dbo.categorias (id_categoria, nombre, activo) VALUES (1001, N'tmp-12b', 1);
                SET IDENTITY_INSERT dbo.categorias OFF;
                SET IDENTITY_INSERT dbo.productos ON;
                INSERT INTO dbo.productos (id_producto, id_categoria, nombre, activo) VALUES (1001, 1001, N'tmp-12b', 1);
                SET IDENTITY_INSERT dbo.productos OFF;
                UPDATE dbo.catalogo SET id_producto = 1001 WHERE id_producto < 1000;
                UPDATE dbo.solicitud_detalles SET id_producto = 1001 WHERE id_producto < 1000;
                DELETE FROM dbo.productos WHERE id_producto < 1000;
                DELETE FROM dbo.categorias WHERE id_categoria < 1000;
                """);

            migrationBuilder.DropCheckConstraint(name: "ck_productos_medidas", table: "productos");
            migrationBuilder.DropTable(name: "producto_medidas_fijas");
            migrationBuilder.DropTable(name: "producto_opciones");
            migrationBuilder.DropColumn(name: "opciones", table: "solicitud_detalles");
            migrationBuilder.DropColumn(name: "alto_max_mm", table: "productos");
            migrationBuilder.DropColumn(name: "alto_min_mm", table: "productos");
            migrationBuilder.DropColumn(name: "ancho_max_mm", table: "productos");
            migrationBuilder.DropColumn(name: "ancho_min_mm", table: "productos");

            // La semilla original: 5 categorias y 4 productos.
            migrationBuilder.Sql("""
                SET IDENTITY_INSERT dbo.categorias ON;
                INSERT INTO dbo.categorias (id_categoria, nombre, descripcion, activo) VALUES
                    (1, N'Letreros', N'Letreros comerciales, luminosos, palomitas, metálicos o en madera', 1),
                    (2, N'Vehículos', N'Rotulación, ploteo y gráficas para vehículos', 1),
                    (3, N'Estampados', N'Estampado textil, promocional y serigrafía', 1),
                    (4, N'Muros', N'Pintura mural, fachadas y gráfica de gran formato', 1),
                    (5, N'Personalizado / Otro', N'Trabajos especiales a medida del cliente', 1);
                SET IDENTITY_INSERT dbo.categorias OFF;
                SET IDENTITY_INSERT dbo.productos ON;
                INSERT INTO dbo.productos (id_producto, id_categoria, nombre, descripcion, precio_base_ref, activo) VALUES
                    (1, 1, N'Cartel metálico impreso', N'Estructura metálica con gráfica impresa', 45000, 1),
                    (2, 1, N'Lienzo publicitario', N'Lienzo PVC con ojetillos perimetrales', 25000, 1),
                    (3, 1, N'Letrero tipo Palomita', N'Letrero publicitario doble cara plegable', 35000, 1),
                    (4, 1, N'Trabajo personalizado', N'Diseño y fabricación según medidas', NULL, 1);
                SET IDENTITY_INSERT dbo.productos OFF;
                UPDATE dbo.catalogo SET id_producto = 1 WHERE id_producto = 1001;
                UPDATE dbo.solicitud_detalles SET id_producto = 1 WHERE id_producto = 1001;
                DELETE FROM dbo.productos WHERE id_producto = 1001;
                DELETE FROM dbo.categorias WHERE id_categoria = 1001;
                """);
            migrationBuilder.Sql("DBCC CHECKIDENT ('dbo.productos', RESEED, 4); DBCC CHECKIDENT ('dbo.categorias', RESEED, 5);");
        }

        private static string Q(string texto) => texto.Replace("'", "''");

        private static string N(int? valor) => valor is int v ? v.ToString() : "NULL";

        // Copia congelada del catalogo real del 16-11-2026 (medidas en milimetros). La migracion no depende del
        // codigo vivo: si el catalogo cambia despues, se hace otra migracion.
        private static class CatalogoV1
        {
            public sealed record CategoriaReal(int Id, string Nombre, string Descripcion);

            public sealed record ProductoReal(
                int Id, int IdCategoria, string Nombre, string Descripcion,
                int? AnchoMin = null, int? AnchoMax = null, int? AltoMin = null, int? AltoMax = null,
                (int Ancho, int Alto)[]? Fijas = null, (string Grupo, string[] Valores)[]? Opciones = null);

            private static readonly (string, string[]) DisenoClienteOficina = ("Diseño", ["Del cliente", "Desarrollado por la oficina"]);

            public static readonly CategoriaReal[] Categorias =
            [
                new(1, "Impresión y publicidad gráfica", "Gigantografías, banners, posteras, pendones, roll up, X banner, demo stand, adhesivos y vinilos"),
                new(2, "Letreros y estructuras", "Letreros luminosos, letras corpóreas, señalizaciones, caballetes y rotulación vehicular"),
                new(3, "Papelería e impresión pequeña", "Tarjetas de presentación y folletería"),
                new(4, "Estampados y personalización", "Tazas, poleras, bufandas y llaveros")
            ];

            public static readonly ProductoReal[] Productos =
            [
                new(1, 1, "Impresión en tela PVC / Gigantografías", "Impresión en tela PVC, brillante o mate. Ancho y alto de 1 a 5000 cm",
                    10, 50_000, 10, 50_000, Opciones: [("Acabado", ["Brillante", "Mate"]), DisenoClienteOficina]),
                new(2, 1, "Banners", "Tela PVC de 13 onzas o papel vinílico de una micra. Ancho y alto de 1 a 5000 cm",
                    10, 50_000, 10, 50_000, Opciones: [("Material", ["Tela PVC de 13 onzas", "Papel vinílico de una micra"]),
                        ("Terminaciones", ["Ojetillos con refuerzo perimetral", "Solo tela impresa"]), DisenoClienteOficina]),
                new(3, 1, "Posteras", "Impresión en tela PVC con bastidor. Ancho y alto de 30 a 200 cm",
                    300, 2000, 300, 2000, Opciones: [("Bastidor", ["Madera", "Metálico"]), DisenoClienteOficina]),
                new(4, 1, "Pendones", "Tela PVC. Ancho y alto de 1 a 5000 cm",
                    10, 50_000, 10, 50_000, Opciones: [("Terminaciones", ["Ojetillos", "Bolsillos"]), DisenoClienteOficina]),
                new(5, 1, "Roll Up / Pendones Roller", "Estructura de aluminio con bolso de transporte. Ancho de 80 a 150 cm y alto de 200 a 300 cm",
                    800, 1500, 2000, 3000, Opciones: [DisenoClienteOficina]),
                new(6, 1, "X Banner / banner araña", "Estructura de aluminio con bolso de transporte. Medida única: 80 x 200 cm",
                    Fijas: [(800, 2000)], Opciones: [DisenoClienteOficina]),
                new(7, 1, "Demo Stand", "Marco de aluminio con gráfica en tela PVC. Medida única: 300 x 300 cm",
                    Fijas: [(3000, 3000)], Opciones: [DisenoClienteOficina]),
                new(8, 1, "Adhesivos y vinilos", "Material según la variante elegida. Ancho y alto de 1 a 5000 cm",
                    10, 50_000, 10, 50_000, Opciones: [("Variante", ["Stickers", "Vinilo transparente", "Vinilo adhesivo", "Vinilo de recorte",
                        "Vinilo microperforado", "Floor Graphic"]), DisenoClienteOficina]),
                new(9, 2, "Letreros luminosos", "Plancha galvanizada con iluminación LED y estructura metálica. Ancho y alto de 1 a 200 cm",
                    10, 2000, 10, 2000, Opciones: [DisenoClienteOficina]),
                new(10, 2, "Letras corpóreas", "Con iluminación LED. Ancho y alto de 1 a 200 cm",
                    10, 2000, 10, 2000, Opciones: [("Material", ["Acrílico", "Aluminio", "Madera"]),
                        ("Espesor", ["2 mm", "3 mm", "4 mm", "5 mm", "6 mm", "7 mm", "8 mm", "9 mm", "10 mm"]),
                        ("Diseño", ["Personalizado", "Tipografía del cliente"])]),
                new(11, 2, "Señalizaciones", "Material y soporte en zincalum, diseño predeterminado",
                    Opciones: [("Tipo", ["Vial", "Industrial"])]),
                new(12, 2, "Caballete publicitario / Paloma", "Gráfica en tela PVC sobre estructura de madera. Medidas disponibles: 100 x 50, 120 x 60 y 150 x 80 cm",
                    Fijas: [(1000, 500), (1200, 600), (1500, 800)], Opciones: [DisenoClienteOficina]),
                new(13, 2, "Rotulación vehicular y general", "Diseño del cliente. Ancho y alto de 1 a 5000 cm",
                    10, 50_000, 10, 50_000, Opciones: [("Material", ["Vinilo adhesivo", "Vinilo de corte", "Vinilo microperforado", "Vinilo reflectante",
                        "Otro (especificar en la descripción)"])]),
                new(14, 3, "Tarjetas de presentación", "Opalina lisa, una cara. Medida única: 9 x 5,5 cm",
                    Fijas: [(90, 55)], Opciones: [("Acabado", ["Mate", "Brillante"]), DisenoClienteOficina]),
                new(15, 3, "Flyers / Folletería", "Couché de 130 gramos, una cara. Tamaños: 10 x 15, 21 x 15 y 21 x 29,7 cm",
                    Fijas: [(100, 150), (210, 150), (210, 297)], Opciones: [DisenoClienteOficina]),
                new(16, 4, "Estampados", "Material según el producto. Indica la ubicación del estampado en la descripción",
                    Opciones: [("Producto", ["Tazas", "Poleras", "Bufandas", "Llaveros"]),
                        ("Técnica de estampado", ["Sublimación", "Termotransferencia", "DTF"]), DisenoClienteOficina])
            ];
        }
    }
}
