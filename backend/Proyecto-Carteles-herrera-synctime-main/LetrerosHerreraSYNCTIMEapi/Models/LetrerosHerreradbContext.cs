using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LetrerosHerreraSYNCTIMEapi.Models;

// Este modelo y sus migraciones son la unica fuente de verdad del esquema.
public class LetrerosHerreradbContext : DbContext
{
    public LetrerosHerreradbContext(DbContextOptions<LetrerosHerreradbContext> options)
        : base(options) { }

    public DbSet<Cliente> Clientes { get; set; } = null!;
    public DbSet<Empleado> Empleados { get; set; } = null!;
    public DbSet<Categoria> Categorias { get; set; } = null!;
    public DbSet<Producto> Productos { get; set; } = null!;
    public DbSet<Catalogo> Catalogos { get; set; } = null!;
    public DbSet<Solicitud> Solicitudes { get; set; } = null!;
    public DbSet<SolicitudDetalle> SolicitudDetalles { get; set; } = null!;
    public DbSet<Material> Materiales { get; set; } = null!;
    public DbSet<Cotizacion> Cotizaciones { get; set; } = null!;
    public DbSet<CotizacionMaterial> CotizacionMateriales { get; set; } = null!;
    public DbSet<Pedido> Pedidos { get; set; } = null!;
    public DbSet<Pago> Pagos { get; set; } = null!;
    public DbSet<HorarioAtencion> HorariosAtencion { get; set; } = null!;
    public DbSet<CartelDigital> CartelDigital { get; set; } = null!;
    public DbSet<MetricaConversionOnline> MetricasConversionOnline { get; set; } = null!;
    public DbSet<RostroEmpleado> RostrosEmpleado { get; set; } = null!;
    public DbSet<CorreoSaliente> CorreosSalientes { get; set; } = null!;
    public DbSet<Consentimiento> Consentimientos { get; set; } = null!;
    public DbSet<Imagen> Imagenes { get; set; } = null!;
    public DbSet<ConsultaContacto> ConsultasContacto { get; set; } = null!;
    public DbSet<TokenRenovacion> TokensRenovacion { get; set; } = null!;
    public DbSet<CodigoCuenta> CodigosCuenta { get; set; } = null!;
    public DbSet<AdjuntoSolicitud> AdjuntosSolicitud { get; set; } = null!;
    public DbSet<SolicitudHistorial> SolicitudesHistorial { get; set; } = null!;
    public DbSet<PedidoHistorial> PedidosHistorial { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Modern_Spanish_CI_AS");

        // HasSentinel indica que valor deja actuar al DEFAULT de la columna. Por defecto seria el del tipo
        // (false, 0), y un activo=false o una cantidad=0 enviados a proposito se confundirian con "sin asignar".

        // ── clientes ──
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("clientes", t =>
            {
                t.HasCheckConstraint("ck_clientes_email", "email IS NULL OR (email LIKE N'%_@_%._%' AND email NOT LIKE N'% %')");
                // Fase 6 (RFS03): no hay cuenta web sin correo; el correo es el usuario y el canal de recuperacion.
                t.HasCheckConstraint("ck_clientes_cuenta_con_correo", "password_hash IS NULL OR email IS NOT NULL");
            });
            entity.HasKey(e => e.IdCliente).HasName("pk_clientes");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.Rut).HasColumnName("rut").HasMaxLength(12).IsUnicode(false);
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(80);
            entity.Property(e => e.Apellido).HasColumnName("apellido").HasMaxLength(80);
            entity.Property(e => e.Telefono).HasColumnName("telefono").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100);
            entity.Property(e => e.Direccion).HasColumnName("direccion").HasMaxLength(255);
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Registrado).HasColumnName("registrado")
                .HasComputedColumnSql("CONVERT(BIT, CASE WHEN password_hash IS NOT NULL THEN 1 ELSE 0 END)", stored: true);
            entity.Property(e => e.FechaRegistro).HasColumnName("fecha_registro").HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true).HasSentinel(true);
            entity.Property(e => e.FechaVerificacionCorreo).HasColumnName("fecha_verificacion_correo").HasColumnType("datetimeoffset(0)");

            entity.HasIndex(e => e.Telefono).IsUnique().HasDatabaseName("uq_clientes_telefono");
            entity.HasIndex(e => e.Rut).IsUnique().HasFilter("[rut] IS NOT NULL").HasDatabaseName("uq_clientes_rut");
            entity.HasIndex(e => e.Email).IsUnique().HasFilter("[email] IS NOT NULL").HasDatabaseName("uq_clientes_email");
        });

        // ── empleados ──
        modelBuilder.Entity<Empleado>(entity =>
        {
            entity.ToTable("empleados", t =>
            {
                t.HasCheckConstraint("ck_empleados_rol", "rol IN (N'administrador', N'dueño', N'diseñador', N'operario')");
                t.HasCheckConstraint("ck_empleados_email", "email LIKE N'%_@_%._%' AND email NOT LIKE N'% %'");
            });
            entity.HasKey(e => e.IdEmpleado).HasName("pk_empleados");
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.Rut).HasColumnName("rut").HasMaxLength(12).IsUnicode(false);
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(80);
            entity.Property(e => e.Apellido).HasColumnName("apellido").HasMaxLength(80);
            entity.Property(e => e.Telefono).HasColumnName("telefono").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Rol).HasColumnName("rol").HasMaxLength(20).HasDefaultValue("administrador");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true).HasSentinel(true);

            entity.HasIndex(e => e.Rut).IsUnique().HasDatabaseName("uq_empleados_rut");
            entity.HasIndex(e => e.Email).IsUnique().HasDatabaseName("uq_empleados_email");
        });

        // ── categorias ──
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.ToTable("categorias");
            entity.HasKey(e => e.IdCategoria).HasName("pk_categorias");
            entity.Property(e => e.IdCategoria).HasColumnName("id_categoria");
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(80);
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasMaxLength(250);
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true).HasSentinel(true);

            entity.HasIndex(e => e.Nombre).IsUnique().HasDatabaseName("uq_categorias_nombre");

            entity.HasData(
                new Categoria { IdCategoria = 1, Nombre = "Letreros", Descripcion = "Letreros comerciales, luminosos, palomitas, metálicos o en madera" },
                new Categoria { IdCategoria = 2, Nombre = "Vehículos", Descripcion = "Rotulación, ploteo y gráficas para vehículos" },
                new Categoria { IdCategoria = 3, Nombre = "Estampados", Descripcion = "Estampado textil, promocional y serigrafía" },
                new Categoria { IdCategoria = 4, Nombre = "Muros", Descripcion = "Pintura mural, fachadas y gráfica de gran formato" },
                new Categoria { IdCategoria = 5, Nombre = "Personalizado / Otro", Descripcion = "Trabajos especiales a medida del cliente" });
        });

        // ── productos ──
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("productos", t =>
                t.HasCheckConstraint("ck_productos_precio", "precio_base_ref IS NULL OR precio_base_ref >= 0"));
            entity.HasKey(e => e.IdProducto).HasName("pk_productos");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            entity.Property(e => e.IdCategoria).HasColumnName("id_categoria");
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(100);
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasMaxLength(250);
            entity.Property(e => e.PrecioBaseRef).HasColumnName("precio_base_ref").HasColumnType("decimal(12,2)");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true).HasSentinel(true);

            entity.HasIndex(e => e.Nombre).IsUnique().HasDatabaseName("uq_productos_nombre");
            entity.HasIndex(e => new { e.IdCategoria, e.Activo }).HasDatabaseName("ix_productos_categoria");

            entity.HasOne(e => e.Categoria)
                .WithMany(c => c.Productos)
                .HasForeignKey(e => e.IdCategoria)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_productos_categoria");

            entity.HasData(
                new Producto { IdProducto = 1, IdCategoria = 1, Nombre = "Cartel metálico impreso", Descripcion = "Estructura metálica con gráfica impresa", PrecioBaseRef = 45000m },
                new Producto { IdProducto = 2, IdCategoria = 1, Nombre = "Lienzo publicitario", Descripcion = "Lienzo PVC con ojetillos perimetrales", PrecioBaseRef = 25000m },
                new Producto { IdProducto = 3, IdCategoria = 1, Nombre = "Letrero tipo Palomita", Descripcion = "Letrero publicitario doble cara plegable", PrecioBaseRef = 35000m },
                new Producto { IdProducto = 4, IdCategoria = 1, Nombre = "Trabajo personalizado", Descripcion = "Diseño y fabricación según medidas" });
        });

        // ── catalogo ──
        modelBuilder.Entity<Catalogo>(entity =>
        {
            entity.ToTable("catalogo", t =>
            {
                t.HasCheckConstraint("ck_catalogo_precio", "precio_referencial IS NULL OR precio_referencial >= 0");
                t.HasCheckConstraint("ck_catalogo_medidas", "(ancho_mm IS NULL AND alto_mm IS NULL) OR (ancho_mm > 0 AND alto_mm > 0)");
                // Fase 5 (RNFO17): un trabajo con el logotipo de un cliente no se muestra sin su autorizacion.
                t.HasCheckConstraint("ck_catalogo_logo", "visible = 0 OR incluye_logo_cliente = 0 OR fecha_autorizacion_logo IS NOT NULL");
                t.HasCheckConstraint("ck_catalogo_autorizacion_logo",
                    "(id_empleado_autoriza_logo IS NULL AND fecha_autorizacion_logo IS NULL) OR (id_empleado_autoriza_logo IS NOT NULL AND fecha_autorizacion_logo IS NOT NULL)");
            });
            entity.HasKey(e => e.IdCatalogo).HasName("pk_catalogo");
            entity.Property(e => e.IdCatalogo).HasColumnName("id_catalogo");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            entity.Property(e => e.IdEmpleadoCreador).HasColumnName("id_empleado_creador");
            entity.Property(e => e.Titulo).HasColumnName("titulo").HasMaxLength(120);
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasMaxLength(500);
            entity.Property(e => e.ImagenUrl).HasColumnName("imagen_url").HasMaxLength(500);
            entity.Property(e => e.AnchoMm).HasColumnName("ancho_mm");
            entity.Property(e => e.AltoMm).HasColumnName("alto_mm");
            entity.Property(e => e.PrecioReferencial).HasColumnName("precio_referencial").HasColumnType("decimal(12,2)");
            entity.Property(e => e.FechaPublicacion).HasColumnName("fecha_publicacion").HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.Visible).HasColumnName("visible").HasDefaultValue(true).HasSentinel(true);
            entity.Property(e => e.IdImagen).HasColumnName("id_imagen");
            entity.Property(e => e.IncluyeLogoCliente).HasColumnName("incluye_logo_cliente").HasDefaultValue(false);
            entity.Property(e => e.IdEmpleadoAutorizaLogo).HasColumnName("id_empleado_autoriza_logo");
            entity.Property(e => e.FechaAutorizacionLogo).HasColumnName("fecha_autorizacion_logo").HasColumnType("datetimeoffset(0)");

            entity.HasIndex(e => new { e.Visible, e.IdProducto }).HasDatabaseName("ix_catalogo_visible_producto");
            entity.HasIndex(e => e.IdImagen).HasDatabaseName("ix_catalogo_imagen");
            entity.HasIndex(e => e.IdEmpleadoAutorizaLogo).HasDatabaseName("ix_catalogo_autoriza_logo");

            entity.HasOne(e => e.Imagen).WithMany()
                .HasForeignKey(e => e.IdImagen)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_catalogo_imagen");

            entity.HasOne(e => e.EmpleadoAutorizaLogo).WithMany()
                .HasForeignKey(e => e.IdEmpleadoAutorizaLogo)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_catalogo_empleado_autoriza_logo");

            entity.HasOne(e => e.Producto)
                .WithMany(p => p.Catalogos)
                .HasForeignKey(e => e.IdProducto)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_catalogo_producto");

            entity.HasOne(e => e.EmpleadoCreador)
                .WithMany(emp => emp.CatalogosCreados)
                .HasForeignKey(e => e.IdEmpleadoCreador)
                .HasConstraintName("fk_catalogo_empleado");
        });

        // ── solicitudes ──
        modelBuilder.Entity<Solicitud>(entity =>
        {
            entity.ToTable("solicitudes", t =>
                t.HasCheckConstraint("ck_solicitudes_estado", "estado IN (N'solicitado', N'cotizado', N'aceptado', N'rechazado')"));
            entity.HasKey(e => e.IdSolicitud).HasName("pk_solicitudes");
            entity.Property(e => e.IdSolicitud).HasColumnName("id_solicitud");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.IdEmpleadoRegistra).HasColumnName("id_empleado_registra");
            entity.Property(e => e.Origen).HasColumnName("origen").HasMaxLength(15)
                .HasComputedColumnSql("CASE WHEN id_empleado_registra IS NULL THEN N'online' ELSE N'presencial' END", stored: true);
            // Fase 7: hasta 2000 caracteres (antes 500); ampliar una columna no pierde datos.
            entity.Property(e => e.DescripcionGeneral).HasColumnName("descripcion_general").HasMaxLength(2000);
            entity.Property(e => e.FechaSolicitud).HasColumnName("fecha_solicitud").HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(15).HasDefaultValue("solicitado");
            // Fase 7: el motivo admite hasta 500 caracteres, igual que en el historial.
            entity.Property(e => e.MotivoRechazo).HasColumnName("motivo_rechazo").HasMaxLength(500);
            entity.Property(e => e.EliminadoLogico).HasColumnName("eliminado_logico").HasDefaultValue(false);
            entity.Property(e => e.FechaEliminacionLogica).HasColumnName("fecha_eliminacion_logica").HasColumnType("datetime2(0)");
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();
            entity.Property(e => e.HuellaIp).HasColumnName("huella_ip").HasMaxLength(64).IsUnicode(false).IsFixedLength();

            // La regla contra solicitudes falsas busca por huella las de la ultima hora (configurable).
            entity.HasIndex(e => new { e.HuellaIp, e.FechaSolicitud }).HasFilter("[huella_ip] IS NOT NULL")
                .HasDatabaseName("ix_solicitudes_huella_ip");
            entity.HasIndex(e => new { e.FechaSolicitud, e.Origen, e.Estado, e.EliminadoLogico })
                .HasDatabaseName("ix_solicitudes_metricas");
            entity.HasIndex(e => new { e.IdCliente, e.EliminadoLogico })
                .HasDatabaseName("ix_solicitudes_cliente");

            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Solicitudes)
                .HasForeignKey(e => e.IdCliente)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_solicitudes_cliente");

            entity.HasOne(e => e.EmpleadoRegistra)
                .WithMany(emp => emp.SolicitudesRegistradas)
                .HasForeignKey(e => e.IdEmpleadoRegistra)
                .HasConstraintName("fk_solicitudes_empleado");
        });

        // ── solicitud_detalles ──
        modelBuilder.Entity<SolicitudDetalle>(entity =>
        {
            entity.ToTable("solicitud_detalles", t =>
            {
                t.HasCheckConstraint("ck_sol_detalles_cantidad", "cantidad > 0");
                t.HasCheckConstraint("ck_sol_detalles_medidas", "(ancho_mm IS NULL AND alto_mm IS NULL) OR (ancho_mm > 0 AND alto_mm > 0)");
            });
            entity.HasKey(e => e.IdSolicitudDetalle).HasName("pk_solicitud_detalles");
            entity.Property(e => e.IdSolicitudDetalle).HasColumnName("id_solicitud_detalle");
            entity.Property(e => e.IdSolicitud).HasColumnName("id_solicitud");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            // Fase 7: hasta 500 caracteres (antes 300). Sigue siendo NOT NULL: sin descripcion se guarda "".
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasMaxLength(500);
            entity.Property(e => e.AnchoMm).HasColumnName("ancho_mm");
            entity.Property(e => e.AltoMm).HasColumnName("alto_mm");
            entity.Property(e => e.Cantidad).HasColumnName("cantidad").HasDefaultValue(1).HasSentinel(1);
            entity.Property(e => e.AreaM2).HasColumnName("area_m2").HasColumnType("decimal(12,4)")
                .HasComputedColumnSql("CONVERT(DECIMAL(12,4), CONVERT(BIGINT, ancho_mm) * alto_mm / 1000000.0)", stored: true);

            entity.HasIndex(e => e.IdSolicitud).HasDatabaseName("ix_sol_detalles_solicitud");
            entity.HasIndex(e => e.IdProducto).HasDatabaseName("ix_sol_detalles_producto");

            entity.HasOne(e => e.Solicitud)
                .WithMany(s => s.Detalles)
                .HasForeignKey(e => e.IdSolicitud)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_sol_detalles_solicitud");

            entity.HasOne(e => e.Producto)
                .WithMany(p => p.SolicitudDetalles)
                .HasForeignKey(e => e.IdProducto)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_sol_detalles_producto");
        });

        // ── materiales ──
        modelBuilder.Entity<Material>(entity =>
        {
            entity.ToTable("materiales", t =>
                t.HasCheckConstraint("ck_materiales_costo", "costo_unitario_aprox >= 0"));
            entity.HasKey(e => e.IdMaterial).HasName("pk_materiales");
            entity.Property(e => e.IdMaterial).HasColumnName("id_material");
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(100);
            entity.Property(e => e.UnidadMedida).HasColumnName("unidad_medida").HasMaxLength(30);
            entity.Property(e => e.CostoUnitarioAprox).HasColumnName("costo_unitario_aprox").HasColumnType("decimal(12,2)");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true).HasSentinel(true);

            entity.HasIndex(e => e.Nombre).IsUnique().HasDatabaseName("uq_materiales_nombre");

            entity.HasData(
                new Material { IdMaterial = 1, Nombre = "Madera", UnidadMedida = "m2", CostoUnitarioAprox = 12500m },
                new Material { IdMaterial = 2, Nombre = "Pintura", UnidadMedida = "litro", CostoUnitarioAprox = 8900m },
                new Material { IdMaterial = 3, Nombre = "Lienzo PVC", UnidadMedida = "m2", CostoUnitarioAprox = 6500m },
                new Material { IdMaterial = 4, Nombre = "Vinilo Adhesivo", UnidadMedida = "m2", CostoUnitarioAprox = 7200m },
                new Material { IdMaterial = 5, Nombre = "Plancha Metálica", UnidadMedida = "m2", CostoUnitarioAprox = 18000m });
        });

        // ── cotizaciones ──
        modelBuilder.Entity<Cotizacion>(entity =>
        {
            entity.ToTable("cotizaciones", t =>
            {
                t.HasCheckConstraint("ck_cotizaciones_costos", "costo_materiales_total >= 0 AND costo_mano_obra >= 0 AND otros_costos >= 0");
                t.HasCheckConstraint("ck_cotizaciones_estado", "estado IN (N'pendiente', N'aprobada', N'rechazada')");
                // Fase 8: pesos enteros, IVA incluido (decision 1 del 14-11-2026).
                t.HasCheckConstraint("ck_cotizaciones_montos_enteros",
                    "costo_mano_obra = FLOOR(costo_mano_obra) AND otros_costos = FLOOR(otros_costos)");
                // Solo una «pendiente» no tiene respuesta, y toda respuesta dice por que via llego.
                t.HasCheckConstraint("ck_cotizaciones_respuesta",
                    "(CASE WHEN estado = N'pendiente' THEN 1 ELSE 0 END) = (CASE WHEN fecha_respuesta IS NULL THEN 1 ELSE 0 END) " +
                    "AND (CASE WHEN via_respuesta IS NULL THEN 0 ELSE 1 END) = (CASE WHEN fecha_respuesta IS NULL THEN 0 ELSE 1 END)");
                t.HasCheckConstraint("ck_cotizaciones_via",
                    "via_respuesta IS NULL OR via_respuesta IN (N'cliente_web', N'telefono', N'whatsapp', N'presencial')");
            });
            entity.HasKey(e => e.IdCotizacion).HasName("pk_cotizaciones");
            entity.Property(e => e.IdCotizacion).HasColumnName("id_cotizacion");
            entity.Property(e => e.IdSolicitud).HasColumnName("id_solicitud");
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.FechaCotizacion).HasColumnName("fecha_cotizacion").HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSDATETIME()");
            // Fase 8: hasta 2000 caracteres (antes 500); ampliar una columna no pierde datos. Las ve el cliente.
            entity.Property(e => e.Observaciones).HasColumnName("observaciones").HasMaxLength(2000);
            entity.Property(e => e.CostoMaterialesTotal).HasColumnName("costo_materiales_total").HasColumnType("decimal(12,2)")
                .HasDefaultValue(0m);
            entity.Property(e => e.CostoManoObra).HasColumnName("costo_mano_obra").HasColumnType("decimal(12,2)")
                .HasDefaultValue(0m);
            entity.Property(e => e.OtrosCostos).HasColumnName("otros_costos").HasColumnType("decimal(12,2)")
                .HasDefaultValue(0m);
            entity.Property(e => e.TotalCotizacion).HasColumnName("total_cotizacion").HasColumnType("decimal(14,2)")
                .HasComputedColumnSql("CONVERT(DECIMAL(14,2), costo_materiales_total + costo_mano_obra + otros_costos)", stored: true);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(15).HasDefaultValue("pendiente");
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();
            entity.Property(e => e.FechaRespuesta).HasColumnName("fecha_respuesta").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.ViaRespuesta).HasColumnName("via_respuesta").HasMaxLength(15);
            entity.Property(e => e.IdEmpleadoRespuesta).HasColumnName("id_empleado_respuesta");
            entity.Property(e => e.MotivoRechazo).HasColumnName("motivo_rechazo").HasMaxLength(500);
            entity.Property(e => e.EliminadoLogico).HasColumnName("eliminado_logico").HasDefaultValue(false);
            entity.Property(e => e.FechaEliminacionLogica).HasColumnName("fecha_eliminacion_logica").HasColumnType("datetimeoffset(0)");

            entity.HasIndex(e => new { e.IdSolicitud, e.Estado }).HasDatabaseName("ix_cotizaciones_solicitud");
            // Fase 8: una sola cotizacion «pendiente» (no eliminada) por solicitud. Antes era una desviacion
            // anotada; ahora la garantiza la base aunque dos personas coticen a la vez.
            entity.HasIndex(e => e.IdSolicitud).IsUnique()
                .HasFilter("[estado] = N'pendiente' AND [eliminado_logico] = 0")
                .HasDatabaseName("uq_cotizaciones_una_pendiente");
            entity.HasIndex(e => e.IdEmpleadoRespuesta).HasDatabaseName("ix_cotizaciones_empleado_respuesta");

            entity.HasOne(e => e.Solicitud)
                .WithMany(s => s.Cotizaciones)
                .HasForeignKey(e => e.IdSolicitud)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_cotizaciones_solicitud");

            entity.HasOne(e => e.Empleado)
                .WithMany(emp => emp.Cotizaciones)
                .HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_cotizaciones_empleado");

            entity.HasOne(e => e.EmpleadoRespuesta)
                .WithMany()
                .HasForeignKey(e => e.IdEmpleadoRespuesta)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_cotizaciones_empleado_respuesta");
        });

        // ── cotizacion_materiales ──
        modelBuilder.Entity<CotizacionMaterial>(entity =>
        {
            // HasTrigger evita que EF use OUTPUT sin INTO, que SQL Server rechaza en tablas con trigger.
            entity.ToTable("cotizacion_materiales", t =>
            {
                t.HasTrigger("trg_cotizacion_materiales_recalcular_total");
                t.HasCheckConstraint("ck_cot_mat_cantidad", "cantidad > 0");
                t.HasCheckConstraint("ck_cot_mat_costo", "costo_unitario >= 0");
                // Fase 8: el costo unitario va en pesos enteros; la cantidad admite 2 decimales.
                t.HasCheckConstraint("ck_cot_mat_costo_entero", "costo_unitario = FLOOR(costo_unitario)");
            });
            entity.HasKey(e => e.IdCotizacionMaterial).HasName("pk_cotizacion_materiales");
            entity.Property(e => e.IdCotizacionMaterial).HasColumnName("id_cotizacion_material");
            entity.Property(e => e.IdCotizacion).HasColumnName("id_cotizacion");
            entity.Property(e => e.IdMaterial).HasColumnName("id_material");
            // Fase 8: hasta 100 caracteres (antes 80).
            entity.Property(e => e.MedidaAplicada).HasColumnName("medida_aplicada").HasMaxLength(100);
            entity.Property(e => e.Cantidad).HasColumnName("cantidad").HasColumnType("decimal(10,2)");
            entity.Property(e => e.CostoUnitario).HasColumnName("costo_unitario").HasColumnType("decimal(12,2)");
            entity.Property(e => e.Subtotal).HasColumnName("subtotal").HasColumnType("decimal(14,2)")
                .HasComputedColumnSql("CONVERT(DECIMAL(14,2), cantidad * costo_unitario)", stored: true);

            entity.HasIndex(e => e.IdCotizacion).HasDatabaseName("ix_cot_mat_cotizacion");

            entity.HasOne(e => e.Cotizacion)
                .WithMany(c => c.Materiales)
                .HasForeignKey(e => e.IdCotizacion)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_cot_mat_cotizacion");

            entity.HasOne(e => e.Material)
                .WithMany(m => m.CotizacionMateriales)
                .HasForeignKey(e => e.IdMaterial)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_cot_mat_material");
        });

        // ── pedidos ──
        // Estado operativo y estado de pago son independientes: un pedido puede entregarse sin estar
        // pagado (cliente habitual que paga a fin de mes) o pagarse por adelantado o con un abono.
        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.ToTable("pedidos", t =>
            {
                t.HasCheckConstraint("ck_pedidos_modalidad", "modalidad_entrega IN (N'retiro_local', N'entrega_domicilio', N'instalacion_terreno')");
                t.HasCheckConstraint("ck_pedidos_costos", "costo_instalacion_adicional >= 0 AND monto_base_trabajo >= 0");
                t.HasCheckConstraint("ck_pedidos_estado_pedido", "estado_pedido IN (N'aceptado', N'en_proceso', N'terminado', N'entregado', N'anulado')");
                t.HasCheckConstraint("ck_pedidos_origen", "origen IN (N'cotizacion', N'directo')");
                // Lo abonado nunca supera el monto final. Usa las columnas base porque un CHECK no
                // puede apoyarse en otra columna calculada.
                t.HasCheckConstraint("ck_pedidos_monto_abonado", "monto_abonado >= 0 AND monto_abonado <= monto_base_trabajo + costo_instalacion_adicional");
                // Pesos sin decimales y dentro del rango de int: si no, el saldo (entero) y el estado de
                // pago (que compara con el total decimal) podrian contradecirse.
                t.HasCheckConstraint("ck_pedidos_montos_enteros",
                    "monto_base_trabajo = FLOOR(monto_base_trabajo) AND costo_instalacion_adicional = FLOOR(costo_instalacion_adicional) " +
                    "AND monto_base_trabajo + costo_instalacion_adicional <= 2000000000");
                // Fase 8: un pedido anulado dice por que y cuando; uno que no esta anulado no trae esos datos.
                t.HasCheckConstraint("ck_pedidos_anulacion",
                    "(estado_pedido = N'anulado' AND motivo_anulacion IS NOT NULL AND fecha_anulacion IS NOT NULL) " +
                    "OR (estado_pedido <> N'anulado' AND motivo_anulacion IS NULL AND fecha_anulacion IS NULL)");
            });
            entity.HasKey(e => e.IdPedido).HasName("pk_pedidos");
            entity.Property(e => e.IdPedido).HasColumnName("id_pedido");
            entity.Property(e => e.IdSolicitud).HasColumnName("id_solicitud");
            entity.Property(e => e.IdCotizacion).HasColumnName("id_cotizacion");
            entity.Property(e => e.Origen).HasColumnName("origen").HasMaxLength(15).HasDefaultValue("cotizacion");
            entity.Property(e => e.IdEmpleadoResponsable).HasColumnName("id_empleado_responsable");
            entity.Property(e => e.FechaPedido).HasColumnName("fecha_pedido").HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.FechaEntregaEstimada).HasColumnName("fecha_entrega_estimada").HasColumnType("datetime2(0)");
            entity.Property(e => e.FechaEntregaReal).HasColumnName("fecha_entrega_real").HasColumnType("datetime2(0)");
            entity.Property(e => e.ModalidadEntrega).HasColumnName("modalidad_entrega").HasMaxLength(25)
                .HasDefaultValue("retiro_local");
            entity.Property(e => e.RequiereInstalacion).HasColumnName("requiere_instalacion").HasDefaultValue(false);
            // Fase 8: hasta 300 caracteres (antes 255).
            entity.Property(e => e.DireccionInstalacion).HasColumnName("direccion_instalacion").HasMaxLength(300);
            entity.Property(e => e.CostoInstalacionAdicional).HasColumnName("costo_instalacion_adicional").HasColumnType("decimal(12,2)")
                .HasDefaultValue(0m);
            entity.Property(e => e.MontoBaseTrabajo).HasColumnName("monto_base_trabajo").HasColumnType("decimal(12,2)")
                .HasDefaultValue(0m);
            entity.Property(e => e.MontoTotalFinal).HasColumnName("monto_total_final").HasColumnType("decimal(14,2)")
                .HasComputedColumnSql("CONVERT(DECIMAL(14,2), monto_base_trabajo + costo_instalacion_adicional)", stored: true);
            entity.Property(e => e.EstadoPedido).HasColumnName("estado_pedido").HasMaxLength(15).HasDefaultValue("aceptado");
            // La escribe solo el trigger de pagos; EF la lee de vuelta tras cada INSERT o UPDATE y nunca
            // la envia (ni al crear ni al editar), asi ningun codigo puede pisar lo abonado.
            entity.Property(e => e.MontoAbonado).HasColumnName("monto_abonado").HasDefaultValue(0)
                .ValueGeneratedOnAddOrUpdate();
            entity.Property(e => e.MontoAbonado).Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            entity.Property(e => e.MontoAbonado).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            entity.Property(e => e.SaldoPendiente).HasColumnName("saldo_pendiente")
                .HasComputedColumnSql("CONVERT(INT, monto_base_trabajo + costo_instalacion_adicional) - monto_abonado", stored: true);
            // Se deriva de los montos para que nunca los contradiga: sin abono, "no_pagado"; con el
            // total cubierto, "pagado_total"; entre ambos, "abonado".
            entity.Property(e => e.EstadoPago).HasColumnName("estado_pago").HasMaxLength(12)
                .HasComputedColumnSql(
                    "CONVERT(NVARCHAR(12), CASE WHEN monto_abonado = 0 THEN N'no_pagado' " +
                    "WHEN monto_abonado >= monto_base_trabajo + costo_instalacion_adicional THEN N'pagado_total' " +
                    "ELSE N'abonado' END)", stored: true);
            entity.Property(e => e.NumeroFactura).HasColumnName("numero_factura").HasMaxLength(30);
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();
            entity.Property(e => e.MotivoAnulacion).HasColumnName("motivo_anulacion").HasMaxLength(500);
            entity.Property(e => e.FechaAnulacion).HasColumnName("fecha_anulacion").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.EliminadoLogico).HasColumnName("eliminado_logico").HasDefaultValue(false);
            entity.Property(e => e.FechaEliminacionLogica).HasColumnName("fecha_eliminacion_logica").HasColumnType("datetimeoffset(0)");

            entity.HasIndex(e => e.IdSolicitud).IsUnique().HasDatabaseName("uq_pedidos_solicitud");
            // Fase 8: una cotizacion da a lo mas un pedido.
            entity.HasIndex(e => e.IdCotizacion).IsUnique().HasFilter("[id_cotizacion] IS NOT NULL")
                .HasDatabaseName("uq_pedidos_cotizacion");
            entity.HasIndex(e => new { e.FechaEntregaEstimada, e.EstadoPedido }).HasDatabaseName("ix_pedidos_calendario_entrega");
            entity.HasIndex(e => new { e.FechaPedido, e.EstadoPedido }).HasDatabaseName("ix_pedidos_fecha_estado");
            // Cobranza: pedidos no pagados o con abono, por estado operativo, con su saldo.
            entity.HasIndex(e => new { e.EstadoPago, e.EstadoPedido }).IncludeProperties(e => e.SaldoPendiente)
                .HasDatabaseName("ix_pedidos_cobranza");

            entity.HasOne(e => e.Solicitud)
                .WithOne(s => s.Pedido)
                .HasForeignKey<Pedido>(e => e.IdSolicitud)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pedidos_solicitud");

            entity.HasOne(e => e.Cotizacion)
                .WithMany()
                .HasForeignKey(e => e.IdCotizacion)
                .HasConstraintName("fk_pedidos_cotizacion");

            entity.HasOne(e => e.EmpleadoResponsable)
                .WithMany(emp => emp.PedidosResponsable)
                .HasForeignKey(e => e.IdEmpleadoResponsable)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pedidos_empleado");
        });

        // ── pagos ──
        // Historial de abonos y pagos de cada pedido. El trigger recalcula pedidos.monto_abonado con la
        // suma de sus pagos vigentes (fase 8: los anulados no suman); si la suma supera el monto final,
        // ck_pedidos_monto_abonado rechaza la operacion completa y el pago no se guarda.
        modelBuilder.Entity<Pago>(entity =>
        {
            entity.ToTable("pagos", t =>
            {
                t.HasTrigger("trg_pagos_recalcular_monto_abonado");
                t.HasCheckConstraint("ck_pagos_monto", "monto > 0");
                t.HasCheckConstraint("ck_pagos_medio_pago",
                    "medio_pago IN (N'efectivo', N'transferencia', N'tarjeta_debito', N'tarjeta_credito', N'cheque', N'otro')");
                // Fase 8: un abono anulado dice por que, quien y cuando; uno vigente no trae esos datos.
                t.HasCheckConstraint("ck_pagos_anulacion",
                    "(anulado = 1 AND motivo_anulacion IS NOT NULL AND id_empleado_anula IS NOT NULL AND fecha_anulacion IS NOT NULL) " +
                    "OR (anulado = 0 AND motivo_anulacion IS NULL AND id_empleado_anula IS NULL AND fecha_anulacion IS NULL)");
            });
            entity.HasKey(e => e.IdPago).HasName("pk_pagos");
            entity.Property(e => e.IdPago).HasColumnName("id_pago");
            entity.Property(e => e.IdPedido).HasColumnName("id_pedido");
            entity.Property(e => e.Monto).HasColumnName("monto");
            entity.Property(e => e.FechaPago).HasColumnName("fecha_pago").HasColumnType("date")
                .HasDefaultValueSql("CONVERT(DATE, SYSDATETIME())");
            entity.Property(e => e.MedioPago).HasColumnName("medio_pago").HasMaxLength(15);
            entity.Property(e => e.Observacion).HasColumnName("observacion").HasMaxLength(300);
            entity.Property(e => e.IdEmpleadoRegistra).HasColumnName("id_empleado_registra");
            entity.Property(e => e.FechaRegistro).HasColumnName("fecha_registro").HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.Anulado).HasColumnName("anulado").HasDefaultValue(false);
            entity.Property(e => e.MotivoAnulacion).HasColumnName("motivo_anulacion").HasMaxLength(300);
            entity.Property(e => e.IdEmpleadoAnula).HasColumnName("id_empleado_anula");
            entity.Property(e => e.FechaAnulacion).HasColumnName("fecha_anulacion").HasColumnType("datetimeoffset(0)");

            // Historial de un pedido en orden de fecha; tambien cubre la clave foranea id_pedido.
            entity.HasIndex(e => new { e.IdPedido, e.FechaPago }).HasDatabaseName("ix_pagos_pedido_fecha");
            entity.HasIndex(e => e.IdEmpleadoAnula).HasDatabaseName("ix_pagos_empleado_anula");

            entity.HasOne(e => e.Pedido)
                .WithMany(p => p.Pagos)
                .HasForeignKey(e => e.IdPedido)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pagos_pedido");

            entity.HasOne(e => e.EmpleadoRegistra)
                .WithMany(emp => emp.PagosRegistrados)
                .HasForeignKey(e => e.IdEmpleadoRegistra)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pagos_empleado");

            entity.HasOne(e => e.EmpleadoAnula)
                .WithMany()
                .HasForeignKey(e => e.IdEmpleadoAnula)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_pagos_empleado_anula");
        });

        // ── pedidos_historial (fase 8) ──
        // Sin cascada: los pedidos no se borran (el borrado es logico) y su historial tampoco.
        modelBuilder.Entity<PedidoHistorial>(entity =>
        {
            entity.ToTable("pedidos_historial", t =>
                t.HasCheckConstraint("ck_pedidos_historial_estados",
                    "estado_nuevo IN (N'aceptado', N'en_proceso', N'terminado', N'entregado', N'anulado') " +
                    "AND (estado_anterior IS NULL OR estado_anterior IN (N'aceptado', N'en_proceso', N'terminado', N'entregado', N'anulado'))"));
            entity.HasKey(e => e.IdHistorial).HasName("pk_pedidos_historial");
            entity.Property(e => e.IdHistorial).HasColumnName("id_historial");
            entity.Property(e => e.IdPedido).HasColumnName("id_pedido");
            entity.Property(e => e.EstadoAnterior).HasColumnName("estado_anterior").HasMaxLength(15);
            entity.Property(e => e.EstadoNuevo).HasColumnName("estado_nuevo").HasMaxLength(15);
            entity.Property(e => e.Nota).HasColumnName("nota").HasMaxLength(500);
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.Fecha).HasColumnName("fecha").HasColumnType("datetimeoffset(0)");

            entity.HasIndex(e => new { e.IdPedido, e.Fecha }).HasDatabaseName("ix_pedidos_historial_pedido");
            entity.HasIndex(e => e.IdEmpleado).HasDatabaseName("ix_pedidos_historial_empleado");

            entity.HasOne(e => e.Pedido).WithMany(p => p.Historial).HasForeignKey(e => e.IdPedido)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_pedidos_historial_pedido");
            entity.HasOne(e => e.Empleado).WithMany().HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_pedidos_historial_empleado");
        });

        // ── horarios_atencion ──
        modelBuilder.Entity<HorarioAtencion>(entity =>
        {
            entity.ToTable("horarios_atencion", t =>
            {
                t.HasCheckConstraint("ck_horarios_dia", "dia_semana BETWEEN 1 AND 7");
                t.HasCheckConstraint("ck_horarios_rango", "abierto = 0 OR (hora_inicio IS NOT NULL AND hora_fin IS NOT NULL AND hora_fin > hora_inicio)");
            });
            entity.HasKey(e => e.IdHorario).HasName("pk_horarios_atencion");
            entity.Property(e => e.IdHorario).HasColumnName("id_horario");
            entity.Property(e => e.DiaSemana).HasColumnName("dia_semana");
            entity.Property(e => e.NombreDia).HasColumnName("nombre_dia").HasMaxLength(15);
            entity.Property(e => e.HoraInicio).HasColumnName("hora_inicio").HasColumnType("time(0)");
            entity.Property(e => e.HoraFin).HasColumnName("hora_fin").HasColumnType("time(0)");
            entity.Property(e => e.Abierto).HasColumnName("abierto").HasDefaultValue(true).HasSentinel(true);

            entity.HasIndex(e => e.DiaSemana).IsUnique().HasDatabaseName("uq_horarios_dia");

            var apertura = new TimeOnly(9, 0);
            var cierre = new TimeOnly(18, 30);
            // El domingo (cerrado) lo inserta ObjetosProgramablesV6: HasData omite los false y el DEFAULT lo dejaria abierto.
            entity.HasData(
                new HorarioAtencion { IdHorario = 1, DiaSemana = 1, NombreDia = "Lunes", HoraInicio = apertura, HoraFin = cierre, Abierto = true },
                new HorarioAtencion { IdHorario = 2, DiaSemana = 2, NombreDia = "Martes", HoraInicio = apertura, HoraFin = cierre, Abierto = true },
                new HorarioAtencion { IdHorario = 3, DiaSemana = 3, NombreDia = "Miércoles", HoraInicio = apertura, HoraFin = cierre, Abierto = true },
                new HorarioAtencion { IdHorario = 4, DiaSemana = 4, NombreDia = "Jueves", HoraInicio = apertura, HoraFin = cierre, Abierto = true },
                new HorarioAtencion { IdHorario = 5, DiaSemana = 5, NombreDia = "Viernes", HoraInicio = apertura, HoraFin = cierre, Abierto = true },
                new HorarioAtencion { IdHorario = 6, DiaSemana = 6, NombreDia = "Sábado", HoraInicio = apertura, HoraFin = new TimeOnly(14, 0), Abierto = true });
        });

        // ── cartel_digital ──
        modelBuilder.Entity<CartelDigital>(entity =>
        {
            entity.ToTable("cartel_digital", t =>
                t.HasCheckConstraint("ck_cartel_unico", "id_cartel = 1"));
            entity.HasKey(e => e.IdCartel).HasName("pk_cartel_digital");
            entity.Property(e => e.IdCartel).HasColumnName("id_cartel").ValueGeneratedNever();
            entity.Property(e => e.AbiertoEnLocal).HasColumnName("abierto_en_local").HasDefaultValue(true).HasSentinel(true);
            entity.Property(e => e.MensajeEstado).HasColumnName("mensaje_estado").HasMaxLength(250)
                .HasDefaultValue("Local abierto y atendiendo");
            entity.Property(e => e.TiempoEstimadoRegreso).HasColumnName("tiempo_estimado_regreso").HasMaxLength(80);
            entity.Property(e => e.FechaUltimaModificacion).HasColumnName("fecha_ultima_modificacion").HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.IdEmpleadoModifica).HasColumnName("id_empleado_modifica");

            entity.HasOne(e => e.EmpleadoModifica)
                .WithMany()
                .HasForeignKey(e => e.IdEmpleadoModifica)
                .HasConstraintName("fk_cartel_digital_empleado");

            entity.HasData(new CartelDigital { IdCartel = 1, AbiertoEnLocal = true, MensajeEstado = "Local abierto y atendiendo" });
        });

        // ── rostros_empleado ──
        // Sin cascada: ninguna ruta borra empleados y la constancia del consentimiento debe conservarse.
        // datetimeoffset porque estas fechas deciden bloqueos y caducidades, que no deben depender de la hora local.
        modelBuilder.Entity<RostroEmpleado>(entity =>
        {
            entity.ToTable("rostros_empleado");
            entity.HasKey(e => e.IdRostro).HasName("pk_rostros_empleado");
            entity.Property(e => e.IdRostro).HasColumnName("id_rostro");
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.DescriptorCifrado).HasColumnName("descriptor_cifrado").HasMaxLength(1024);
            entity.Property(e => e.FechaConsentimiento).HasColumnName("fecha_consentimiento").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.VersionConsentimiento).HasColumnName("version_consentimiento").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.FechaRevocacion).HasColumnName("fecha_revocacion").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.IntentosFallidos).HasColumnName("intentos_fallidos");
            entity.Property(e => e.BloqueadoHasta).HasColumnName("bloqueado_hasta").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.TokenEquipoHash).HasColumnName("token_equipo_hash").HasMaxLength(32);
            entity.Property(e => e.FechaAutorizacionEquipo).HasColumnName("fecha_autorizacion_equipo").HasColumnType("datetimeoffset(0)");

            entity.HasOne(e => e.Empleado)
                .WithMany()
                .HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_rostros_empleado_empleado");

            entity.HasIndex(e => e.IdEmpleado).IsUnique().HasFilter("[fecha_revocacion] IS NULL").HasDatabaseName("uq_rostros_empleado_vigente");
        });

        // ── correos_salientes (fase 4) ──
        // Las fechas son datetimeoffset: deciden cuando reintentar y no deben depender de la hora local.
        modelBuilder.Entity<CorreoSaliente>(entity =>
        {
            entity.ToTable("correos_salientes", t =>
            {
                t.HasCheckConstraint("ck_correos_salientes_estado", "estado IN ('pendiente', 'enviando', 'enviado', 'fallido')");
                t.HasCheckConstraint("ck_correos_salientes_intentos", "intentos >= 0");
            });
            entity.HasKey(e => e.IdCorreo).HasName("pk_correos_salientes");
            entity.Property(e => e.IdCorreo).HasColumnName("id_correo");
            entity.Property(e => e.Tipo).HasColumnName("tipo").HasMaxLength(40).IsUnicode(false);
            entity.Property(e => e.Referencia).HasColumnName("referencia").HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.Destinatarios).HasColumnName("destinatarios").HasMaxLength(1000);
            entity.Property(e => e.ResponderA).HasColumnName("responder_a").HasMaxLength(254);
            entity.Property(e => e.Asunto).HasColumnName("asunto").HasMaxLength(200);
            entity.Property(e => e.CuerpoHtml).HasColumnName("cuerpo_html");
            entity.Property(e => e.CuerpoTexto).HasColumnName("cuerpo_texto");
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(10).IsUnicode(false)
                .HasDefaultValue(EstadosCorreo.Pendiente);
            entity.Property(e => e.Intentos).HasColumnName("intentos");
            entity.Property(e => e.ProximoIntento).HasColumnName("proximo_intento").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.BloqueadoHasta).HasColumnName("bloqueado_hasta").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.UltimoError).HasColumnName("ultimo_error").HasMaxLength(200);
            entity.Property(e => e.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("datetimeoffset(0)")
                .HasDefaultValueSql("SYSDATETIMEOFFSET()");
            entity.Property(e => e.FechaEnvio).HasColumnName("fecha_envio").HasColumnType("datetimeoffset(0)");

            // Lo que el trabajador busca en cada vuelta: lo pendiente cuya hora llego. Solo indexa lo que
            // falta enviar, que es poco; lo enviado se acumula hasta la purga sin agrandar este indice.
            entity.HasIndex(e => new { e.Estado, e.ProximoIntento })
                .HasFilter("[estado] IN ('pendiente', 'enviando')")
                .HasDatabaseName("ix_correos_salientes_pendientes");
            entity.HasIndex(e => e.Referencia).HasFilter("[referencia] IS NOT NULL").HasDatabaseName("ix_correos_salientes_referencia");
            // Purga de lo ya resuelto por antiguedad.
            entity.HasIndex(e => e.FechaCreacion).HasDatabaseName("ix_correos_salientes_fecha");
        });

        // ── consentimientos (fase 4) ──
        modelBuilder.Entity<Consentimiento>(entity =>
        {
            entity.ToTable("consentimientos", t =>
            {
                t.HasCheckConstraint("ck_consentimientos_tipo", "tipo IN ('privacidad', 'terminos', 'marketing', 'cookies')");
                t.HasCheckConstraint("ck_consentimientos_titular",
                    "(CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) + (CASE WHEN referencia_envio IS NULL THEN 0 ELSE 1 END) = 1");
                t.HasCheckConstraint("ck_consentimientos_revocacion", "fecha_revocacion IS NULL OR fecha_revocacion >= fecha_aceptacion");
            });
            entity.HasKey(e => e.IdConsentimiento).HasName("pk_consentimientos");
            entity.Property(e => e.IdConsentimiento).HasColumnName("id_consentimiento");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.ReferenciaEnvio).HasColumnName("referencia_envio").HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.Tipo).HasColumnName("tipo").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.VersionDocumento).HasColumnName("version_documento").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.FechaAceptacion).HasColumnName("fecha_aceptacion").HasColumnType("datetimeoffset(0)")
                .HasDefaultValueSql("SYSDATETIMEOFFSET()");
            entity.Property(e => e.FechaRevocacion).HasColumnName("fecha_revocacion").HasColumnType("datetimeoffset(0)");

            // Consultas de "¿que tiene vigente este titular?"; tambien cubren las claves foraneas.
            entity.HasIndex(e => new { e.IdCliente, e.Tipo }).HasFilter("[id_cliente] IS NOT NULL").HasDatabaseName("ix_consentimientos_cliente");
            entity.HasIndex(e => new { e.IdEmpleado, e.Tipo }).HasFilter("[id_empleado] IS NOT NULL").HasDatabaseName("ix_consentimientos_empleado");
            entity.HasIndex(e => new { e.ReferenciaEnvio, e.Tipo }).HasFilter("[referencia_envio] IS NOT NULL").HasDatabaseName("ix_consentimientos_envio");

            // Una sola aceptacion vigente por titular, documento y version: dos peticiones simultaneas
            // (un doble clic) no pueden dejar el mismo consentimiento dos veces.
            entity.HasIndex(e => new { e.IdCliente, e.Tipo, e.VersionDocumento }).IsUnique()
                .HasFilter("[id_cliente] IS NOT NULL AND [fecha_revocacion] IS NULL").HasDatabaseName("uq_consentimientos_cliente_vigente");
            entity.HasIndex(e => new { e.IdEmpleado, e.Tipo, e.VersionDocumento }).IsUnique()
                .HasFilter("[id_empleado] IS NOT NULL AND [fecha_revocacion] IS NULL").HasDatabaseName("uq_consentimientos_empleado_vigente");
            entity.HasIndex(e => new { e.ReferenciaEnvio, e.Tipo, e.VersionDocumento }).IsUnique()
                .HasFilter("[referencia_envio] IS NOT NULL AND [fecha_revocacion] IS NULL").HasDatabaseName("uq_consentimientos_envio_vigente");

            // Sin cascada: la constancia debe sobrevivir aunque la cuenta se inhabilite.
            entity.HasOne(e => e.Cliente).WithMany()
                .HasForeignKey(e => e.IdCliente)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_consentimientos_cliente");
            entity.HasOne(e => e.Empleado).WithMany()
                .HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_consentimientos_empleado");
        });

        // ── tokens_renovacion (fase 6) ──
        modelBuilder.Entity<TokenRenovacion>(entity =>
        {
            entity.ToTable("tokens_renovacion", t =>
                t.HasCheckConstraint("ck_tokens_renovacion_titular",
                    "(CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) = 1"));
            entity.HasKey(e => e.IdToken).HasName("pk_tokens_renovacion");
            entity.Property(e => e.IdToken).HasColumnName("id_token");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.Hash).HasColumnName("hash_token").HasMaxLength(64).IsUnicode(false).IsFixedLength();
            entity.Property(e => e.Familia).HasColumnName("familia");
            entity.Property(e => e.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.FechaExpiracion).HasColumnName("fecha_expiracion").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.FechaUso).HasColumnName("fecha_uso").HasColumnType("datetimeoffset(3)");
            entity.Property(e => e.FechaRevocacion).HasColumnName("fecha_revocacion").HasColumnType("datetimeoffset(0)");

            // Cada renovacion busca por hash; revocar una sesion, por familia; cerrar todas, por titular.
            entity.HasIndex(e => e.Hash).IsUnique().HasDatabaseName("uq_tokens_renovacion_hash");
            entity.HasIndex(e => e.Familia).HasDatabaseName("ix_tokens_renovacion_familia");
            entity.HasIndex(e => e.IdCliente).HasFilter("[id_cliente] IS NOT NULL").HasDatabaseName("ix_tokens_renovacion_cliente");
            entity.HasIndex(e => e.IdEmpleado).HasFilter("[id_empleado] IS NOT NULL").HasDatabaseName("ix_tokens_renovacion_empleado");
            entity.HasIndex(e => e.FechaExpiracion).HasDatabaseName("ix_tokens_renovacion_expiracion");

            // Si se borra la cuenta, sus sesiones dejan de existir con ella.
            entity.HasOne(e => e.Cliente).WithMany().HasForeignKey(e => e.IdCliente)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_tokens_renovacion_cliente");
            entity.HasOne(e => e.Empleado).WithMany().HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_tokens_renovacion_empleado");
        });

        // ── codigos_cuenta (fase 6) ──
        modelBuilder.Entity<CodigoCuenta>(entity =>
        {
            entity.ToTable("codigos_cuenta", t =>
            {
                t.HasCheckConstraint("ck_codigos_cuenta_titular",
                    "(CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) = 1");
                t.HasCheckConstraint("ck_codigos_cuenta_proposito", "proposito IN ('verificacion_correo', 'recuperacion')");
                t.HasCheckConstraint("ck_codigos_cuenta_intentos", "intentos >= 0");
            });
            entity.HasKey(e => e.IdCodigo).HasName("pk_codigos_cuenta");
            entity.Property(e => e.IdCodigo).HasColumnName("id_codigo");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.Proposito).HasColumnName("proposito").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Hash).HasColumnName("hash_codigo").HasMaxLength(64).IsUnicode(false).IsFixedLength();
            entity.Property(e => e.Correo).HasColumnName("correo").HasMaxLength(254);
            entity.Property(e => e.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.FechaExpiracion).HasColumnName("fecha_expiracion").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.FechaUso).HasColumnName("fecha_uso").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.Intentos).HasColumnName("intentos").HasDefaultValue(0);

            // El enlace de verificacion se busca por su hash; la recuperacion, por titular y proposito.
            entity.HasIndex(e => e.Hash).HasDatabaseName("ix_codigos_cuenta_hash");
            entity.HasIndex(e => new { e.IdCliente, e.Proposito }).HasFilter("[id_cliente] IS NOT NULL").HasDatabaseName("ix_codigos_cuenta_cliente");
            entity.HasIndex(e => new { e.IdEmpleado, e.Proposito }).HasFilter("[id_empleado] IS NOT NULL").HasDatabaseName("ix_codigos_cuenta_empleado");
            entity.HasIndex(e => e.FechaExpiracion).HasDatabaseName("ix_codigos_cuenta_expiracion");

            entity.HasOne(e => e.Cliente).WithMany().HasForeignKey(e => e.IdCliente)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_codigos_cuenta_cliente");
            entity.HasOne(e => e.Empleado).WithMany().HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_codigos_cuenta_empleado");
        });

        // ── adjuntos_solicitud (fase 7) ──
        modelBuilder.Entity<AdjuntoSolicitud>(entity =>
        {
            entity.ToTable("adjuntos_solicitud", t =>
            {
                t.HasCheckConstraint("ck_adjuntos_solicitud_autor",
                    "(CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) = 1");
                t.HasCheckConstraint("ck_adjuntos_solicitud_vinculo",
                    "(id_solicitud IS NULL AND fecha_vinculo IS NULL) OR (id_solicitud IS NOT NULL AND fecha_vinculo IS NOT NULL)");
                t.HasCheckConstraint("ck_adjuntos_solicitud_tipo", "tipo IN ('imagen', 'pdf')");
                t.HasCheckConstraint("ck_adjuntos_solicitud_bytes", "bytes > 0");
                t.HasCheckConstraint("ck_adjuntos_solicitud_medidas",
                    "(tipo = 'imagen' AND ancho > 0 AND alto > 0) OR (tipo = 'pdf' AND ancho IS NULL AND alto IS NULL)");
            });
            entity.HasKey(e => e.IdAdjunto).HasName("pk_adjuntos_solicitud");
            entity.Property(e => e.IdAdjunto).HasColumnName("id_adjunto");
            entity.Property(e => e.IdSolicitud).HasColumnName("id_solicitud");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.Clave).HasColumnName("clave").HasMaxLength(32).IsUnicode(false).IsFixedLength();
            entity.Property(e => e.Tipo).HasColumnName("tipo").HasMaxLength(10).IsUnicode(false);
            entity.Property(e => e.NombreOriginal).HasColumnName("nombre_original").HasMaxLength(120);
            entity.Property(e => e.Bytes).HasColumnName("bytes");
            entity.Property(e => e.Ancho).HasColumnName("ancho");
            entity.Property(e => e.Alto).HasColumnName("alto");
            entity.Property(e => e.FechaSubida).HasColumnName("fecha_subida").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.FechaVinculo).HasColumnName("fecha_vinculo").HasColumnType("datetimeoffset(0)");

            // La ficha lista los de una solicitud; el formulario y la purga, los borradores de cada cliente.
            entity.HasIndex(e => e.Clave).IsUnique().HasDatabaseName("uq_adjuntos_solicitud_clave");
            entity.HasIndex(e => e.IdSolicitud).HasFilter("[id_solicitud] IS NOT NULL").HasDatabaseName("ix_adjuntos_solicitud_solicitud");
            entity.HasIndex(e => new { e.IdCliente, e.FechaSubida }).HasFilter("[id_solicitud] IS NULL").HasDatabaseName("ix_adjuntos_solicitud_borrador");

            // Si sp_depurar_solicitudes_no_concretadas borra una solicitud, sus adjuntos se van con ella (la
            // purga diaria borra despues los archivos que quedan sin registro).
            entity.HasOne(e => e.Solicitud).WithMany(s => s.Adjuntos).HasForeignKey(e => e.IdSolicitud)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_adjuntos_solicitud_solicitud");
            entity.HasOne(e => e.Cliente).WithMany().HasForeignKey(e => e.IdCliente)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_adjuntos_solicitud_cliente");
            entity.HasOne(e => e.Empleado).WithMany().HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_adjuntos_solicitud_empleado");
        });

        // ── solicitudes_historial (fase 7) ──
        modelBuilder.Entity<SolicitudHistorial>(entity =>
        {
            entity.ToTable("solicitudes_historial", t =>
            {
                t.HasCheckConstraint("ck_solicitudes_historial_estados",
                    "estado_nuevo IN (N'solicitado', N'cotizado', N'aceptado', N'rechazado') AND (estado_anterior IS NULL OR estado_anterior IN (N'solicitado', N'cotizado', N'aceptado', N'rechazado'))");
                t.HasCheckConstraint("ck_solicitudes_historial_actor", "id_cliente IS NULL OR id_empleado IS NULL");
            });
            entity.HasKey(e => e.IdHistorial).HasName("pk_solicitudes_historial");
            entity.Property(e => e.IdHistorial).HasColumnName("id_historial");
            entity.Property(e => e.IdSolicitud).HasColumnName("id_solicitud");
            entity.Property(e => e.EstadoAnterior).HasColumnName("estado_anterior").HasMaxLength(15);
            entity.Property(e => e.EstadoNuevo).HasColumnName("estado_nuevo").HasMaxLength(15);
            entity.Property(e => e.Motivo).HasColumnName("motivo").HasMaxLength(500);
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.Fecha).HasColumnName("fecha").HasColumnType("datetimeoffset(0)");

            entity.HasIndex(e => new { e.IdSolicitud, e.Fecha }).HasDatabaseName("ix_solicitudes_historial_solicitud");

            // El historial vive y muere con su solicitud (igual que sus detalles y cotizaciones).
            entity.HasOne(e => e.Solicitud).WithMany(s => s.Historial).HasForeignKey(e => e.IdSolicitud)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_solicitudes_historial_solicitud");
            entity.HasOne(e => e.Empleado).WithMany().HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_solicitudes_historial_empleado");
            entity.HasOne(e => e.Cliente).WithMany().HasForeignKey(e => e.IdCliente)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_solicitudes_historial_cliente");
        });

        // ── imagenes (fase 4) ──
        modelBuilder.Entity<Imagen>(entity =>
        {
            entity.ToTable("imagenes", t =>
                t.HasCheckConstraint("ck_imagenes_medidas", "ancho > 0 AND alto > 0 AND bytes_grande > 0 AND bytes_miniatura > 0"));
            entity.HasKey(e => e.IdImagen).HasName("pk_imagenes");
            entity.Property(e => e.IdImagen).HasColumnName("id_imagen");
            entity.Property(e => e.Clave).HasColumnName("clave").HasMaxLength(32).IsUnicode(false).IsFixedLength();
            entity.Property(e => e.Ancho).HasColumnName("ancho");
            entity.Property(e => e.Alto).HasColumnName("alto");
            entity.Property(e => e.BytesGrande).HasColumnName("bytes_grande");
            entity.Property(e => e.BytesMiniatura).HasColumnName("bytes_miniatura");
            entity.Property(e => e.EsEjemplo).HasColumnName("es_ejemplo").HasDefaultValue(false);
            entity.Property(e => e.IdEmpleadoSube).HasColumnName("id_empleado_sube");
            entity.Property(e => e.FechaSubida).HasColumnName("fecha_subida").HasColumnType("datetimeoffset(0)")
                .HasDefaultValueSql("SYSDATETIMEOFFSET()");

            entity.HasIndex(e => e.Clave).IsUnique().HasDatabaseName("uq_imagenes_clave");
            entity.HasIndex(e => e.IdEmpleadoSube).HasDatabaseName("ix_imagenes_empleado");

            entity.HasOne(e => e.EmpleadoSube).WithMany()
                .HasForeignKey(e => e.IdEmpleadoSube)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_imagenes_empleado");
        });

        // ── consultas_contacto (fase 5) ──
        modelBuilder.Entity<ConsultaContacto>(entity =>
        {
            entity.ToTable("consultas_contacto");
            entity.HasKey(e => e.IdConsulta).HasName("pk_consultas_contacto");
            entity.Property(e => e.IdConsulta).HasColumnName("id_consulta");
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(100);
            entity.Property(e => e.Correo).HasColumnName("correo").HasMaxLength(254);
            entity.Property(e => e.Telefono).HasColumnName("telefono").HasMaxLength(20);
            entity.Property(e => e.Asunto).HasColumnName("asunto").HasMaxLength(150);
            entity.Property(e => e.Mensaje).HasColumnName("mensaje").HasMaxLength(2000);
            entity.Property(e => e.FechaRecepcion).HasColumnName("fecha_recepcion").HasColumnType("datetimeoffset(0)")
                .HasDefaultValueSql("SYSDATETIMEOFFSET()");
            entity.Property(e => e.Revisada).HasColumnName("revisada").HasDefaultValue(false);
            entity.Property(e => e.FechaRevision).HasColumnName("fecha_revision").HasColumnType("datetimeoffset(0)");

            // La bandeja de consultas del panel (fase 9): las no revisadas, de la mas reciente a la mas antigua.
            entity.HasIndex(e => new { e.Revisada, e.FechaRecepcion }).HasDatabaseName("ix_consultas_contacto_bandeja");
        });

        // ── vw_metricas_conversion_online (la crea la migracion ObjetosProgramablesV6) ──
        modelBuilder.Entity<MetricaConversionOnline>(entity =>
        {
            entity.ToView("vw_metricas_conversion_online");
            entity.HasNoKey();
            entity.Property(e => e.Anio).HasColumnName("anio");
            entity.Property(e => e.Mes).HasColumnName("mes");
            entity.Property(e => e.TotalSolicitudesOnline).HasColumnName("total_solicitudes_online");
            entity.Property(e => e.SolicitudesConcretadasEnPedido).HasColumnName("solicitudes_concretadas_en_pedido");
            entity.Property(e => e.PorcentajeConversion).HasColumnName("porcentaje_conversion").HasColumnType("decimal(5,2)");
        });
    }
}
