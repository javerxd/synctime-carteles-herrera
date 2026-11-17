namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 12B: el catalogo REAL de Letreros Herrera (servicios y caracteristicas entregados por el taller el
// 16-11-2026). Es la unica fuente: el modelo lo siembra (HasData) y la migracion CatalogoRealDelTaller lo
// inserta tras borrar el catalogo de prueba. Medidas en milimetros (el taller las da en centimetros).
public static class CatalogoDelTaller
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

    public static IEnumerable<ProductoMedidaFija> MedidasFijas()
    {
        var id = 0;
        foreach (var p in Productos)
            foreach (var (ancho, alto) in p.Fijas ?? [])
                yield return new ProductoMedidaFija { IdMedida = ++id, IdProducto = p.Id, AnchoMm = ancho, AltoMm = alto };
    }

    public static IEnumerable<ProductoOpcion> Opciones()
    {
        var id = 0;
        foreach (var p in Productos)
            foreach (var (grupo, valores) in p.Opciones ?? [])
                for (var i = 0; i < valores.Length; i++)
                    yield return new ProductoOpcion { IdOpcion = ++id, IdProducto = p.Id, Grupo = grupo, Valor = valores[i], Orden = i + 1 };
    }
}
