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
    public DbSet<VistaIndicadorPedido> IndicadoresPedidos { get; set; } = null!;
    public DbSet<VistaIndicadorSolicitud> IndicadoresSolicitudes { get; set; } = null!;
    public DbSet<VistaIndicadorItem> IndicadoresItems { get; set; } = null!;
    public DbSet<RostroEmpleado> RostrosEmpleado { get; set; } = null!;
    public DbSet<CorreoSaliente> CorreosSalientes { get; set; } = null!;
    public DbSet<Consentimiento> Consentimientos { get; set; } = null!;
    public DbSet<Imagen> Imagenes { get; set; } = null!;
    public DbSet<ConsultaContacto> ConsultasContacto { get; set; } = null!;
    public DbSet<TokenRenovacion> TokensRenovacion { get; set; } = null!;
    public DbSet<CodigoCuenta> CodigosCuenta { get; set; } = null!;
    public DbSet<IdentidadExterna> IdentidadesExternas { get; set; } = null!;
    public DbSet<AdjuntoSolicitud> AdjuntosSolicitud { get; set; } = null!;
    public DbSet<SolicitudHistorial> SolicitudesHistorial { get; set; } = null!;
    public DbSet<PedidoHistorial> PedidosHistorial { get; set; } = null!;
    public DbSet<DisponibilidadEmpleado> DisponibilidadEmpleados { get; set; } = null!;
    public DbSet<DestinatarioAviso> DestinatariosAvisos { get; set; } = null!;
    public DbSet<DestinatarioAvisoHistorial> DestinatariosAvisosHistorial { get; set; } = null!;
    public DbSet<DesafioRostro> DesafiosRostro { get; set; } = null!;
    public DbSet<MedicionCalibracionRostro> CalibracionRostro { get; set; } = null!;
    public DbSet<RegistroAuditoria> Auditoria { get; set; } = null!;
    public DbSet<ClaveIdempotencia> ClavesIdempotencia { get; set; } = null!;
    public DbSet<TrabajoExportacion> TrabajosExportacion { get; set; } = null!;

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
            // Fase 9 (RNFP10): version para el control de concurrencia de los mantenedores.
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();
            // Fase 11: preferencia de los avisos de estado y baja de la cuenta.
            entity.Property(e => e.RecibeAvisosEstado).HasColumnName("recibe_avisos_estado").HasDefaultValue(true).HasSentinel(true);
            entity.Property(e => e.InhabilitadoEn).HasColumnName("inhabilitado_en").HasColumnType("datetimeoffset(0)");

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
            // Fase 9 (RNFP10): version para el control de concurrencia de los mantenedores.
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();

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
            // Fase 9: hasta 300 caracteres (antes 250); ampliar una columna no pierde datos.
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasMaxLength(300);
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true).HasSentinel(true);
            // Fase 9 (RNFP10): version para el control de concurrencia de los mantenedores.
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();

            entity.HasIndex(e => e.Nombre).IsUnique().HasDatabaseName("uq_categorias_nombre");

            // Fase 12B: el catalogo real del taller (CatalogoDelTaller); la migracion borra el de prueba.
            entity.HasData(CatalogoDelTaller.Categorias.Select(c => new Categoria { IdCategoria = c.Id, Nombre = c.Nombre, Descripcion = c.Descripcion }));
        });

        // ── productos ──
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("productos", t =>
            {
                t.HasCheckConstraint("ck_productos_precio", "precio_base_ref IS NULL OR precio_base_ref >= 0");
                // Fase 12B: o no hay limites, o los cuatro estan y son coherentes.
                t.HasCheckConstraint("ck_productos_medidas",
                    "(ancho_min_mm IS NULL AND ancho_max_mm IS NULL AND alto_min_mm IS NULL AND alto_max_mm IS NULL) OR " +
                    "(ancho_min_mm > 0 AND ancho_max_mm >= ancho_min_mm AND alto_min_mm > 0 AND alto_max_mm >= alto_min_mm)");
            });
            entity.HasKey(e => e.IdProducto).HasName("pk_productos");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            entity.Property(e => e.IdCategoria).HasColumnName("id_categoria");
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(100);
            // Fase 9: hasta 500 caracteres (antes 250).
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasMaxLength(500);
            entity.Property(e => e.PrecioBaseRef).HasColumnName("precio_base_ref").HasColumnType("decimal(12,2)");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true).HasSentinel(true);
            entity.Property(e => e.AnchoMinMm).HasColumnName("ancho_min_mm");
            entity.Property(e => e.AnchoMaxMm).HasColumnName("ancho_max_mm");
            entity.Property(e => e.AltoMinMm).HasColumnName("alto_min_mm");
            entity.Property(e => e.AltoMaxMm).HasColumnName("alto_max_mm");
            // Fase 9 (RNFP10): version para el control de concurrencia de los mantenedores.
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();

            entity.HasIndex(e => e.Nombre).IsUnique().HasDatabaseName("uq_productos_nombre");
            entity.HasIndex(e => new { e.IdCategoria, e.Activo }).HasDatabaseName("ix_productos_categoria");

            entity.HasOne(e => e.Categoria)
                .WithMany(c => c.Productos)
                .HasForeignKey(e => e.IdCategoria)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_productos_categoria");

            entity.HasData(CatalogoDelTaller.Productos.Select(p => new Producto
            {
                IdProducto = p.Id, IdCategoria = p.IdCategoria, Nombre = p.Nombre, Descripcion = p.Descripcion,
                AnchoMinMm = p.AnchoMin, AnchoMaxMm = p.AnchoMax, AltoMinMm = p.AltoMin, AltoMaxMm = p.AltoMax
            }));
        });

        // ── producto_medidas_fijas (fase 12B) ──
        modelBuilder.Entity<ProductoMedidaFija>(entity =>
        {
            entity.ToTable("producto_medidas_fijas", t => t.HasCheckConstraint("ck_producto_medidas_fijas", "ancho_mm > 0 AND alto_mm > 0"));
            entity.HasKey(e => e.IdMedida).HasName("pk_producto_medidas_fijas");
            entity.Property(e => e.IdMedida).HasColumnName("id_medida");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            entity.Property(e => e.AnchoMm).HasColumnName("ancho_mm");
            entity.Property(e => e.AltoMm).HasColumnName("alto_mm");
            entity.HasIndex(e => new { e.IdProducto, e.AnchoMm, e.AltoMm }).IsUnique().HasDatabaseName("uq_producto_medidas_fijas");
            entity.HasOne(e => e.Producto).WithMany(p => p.MedidasFijas).HasForeignKey(e => e.IdProducto)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_producto_medidas_fijas_producto");
            entity.HasData(CatalogoDelTaller.MedidasFijas());
        });

        // ── producto_opciones (fase 12B) ──
        modelBuilder.Entity<ProductoOpcion>(entity =>
        {
            entity.ToTable("producto_opciones");
            entity.HasKey(e => e.IdOpcion).HasName("pk_producto_opciones");
            entity.Property(e => e.IdOpcion).HasColumnName("id_opcion");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            entity.Property(e => e.Grupo).HasColumnName("grupo").HasMaxLength(60);
            entity.Property(e => e.Valor).HasColumnName("valor").HasMaxLength(80);
            entity.Property(e => e.Orden).HasColumnName("orden");
            entity.HasIndex(e => new { e.IdProducto, e.Grupo, e.Valor }).IsUnique().HasDatabaseName("uq_producto_opciones");
            entity.HasOne(e => e.Producto).WithMany(p => p.Opciones).HasForeignKey(e => e.IdProducto)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_producto_opciones_producto");
            entity.HasData(CatalogoDelTaller.Opciones());
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
            // Fase 9 (RNFP10): version para el control de concurrencia de los mantenedores.
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();

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
            // Fase 12B: lo elegido entre las opciones del producto (objeto JSON).
            entity.Property(e => e.Opciones).HasColumnName("opciones").HasMaxLength(500);
            entity.Property(e => e.AreaM2).HasColumnName("area_m2").HasColumnType("decimal(12,4)")
                .HasComputedColumnSql("CONVERT(DECIMAL(12,4), CONVERT(BIGINT, ancho_mm) * alto_mm / 1000000.0)", stored: true);

            // Fase 12B: ahora cubre producto y cantidad, que lee la vista de items del dashboard.
            entity.HasIndex(e => e.IdSolicitud).HasDatabaseName("ix_sol_detalles_solicitud")
                .IncludeProperties(e => new { e.IdProducto, e.Cantidad });
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
            // Fase 9 (RNFP10): version para el control de concurrencia de los mantenedores.
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();

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
            // Fase 12B (RNFO11): el dashboard agrega por fecha del pedido y por fecha de entrega real; ambos indices cubren las
            // columnas que suma (sin volver a la tabla) y solo guardan pedidos vigentes.
            entity.HasIndex(e => e.FechaPedido).HasDatabaseName("ix_pedidos_indicadores_fecha")
                .IncludeProperties(e => new { e.EstadoPedido, e.EstadoPago, e.MontoTotalFinal, e.MontoAbonado, e.SaldoPendiente, e.IdSolicitud })
                .HasFilter("[eliminado_logico] = 0");
            entity.HasIndex(e => e.FechaEntregaReal).HasDatabaseName("ix_pedidos_entrega_real")
                .IncludeProperties(e => new { e.FechaEntregaEstimada, e.MontoTotalFinal, e.IdSolicitud })
                .HasFilter("[estado_pedido] = N'entregado' AND [eliminado_logico] = 0");
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
                // Fase 9: un dia cerrado no guarda horas.
                t.HasCheckConstraint("ck_horarios_cerrado", "abierto = 1 OR (hora_inicio IS NULL AND hora_fin IS NULL)");
            });
            entity.HasKey(e => e.IdHorario).HasName("pk_horarios_atencion");
            entity.Property(e => e.IdHorario).HasColumnName("id_horario");
            entity.Property(e => e.DiaSemana).HasColumnName("dia_semana");
            entity.Property(e => e.NombreDia).HasColumnName("nombre_dia").HasMaxLength(15);
            entity.Property(e => e.HoraInicio).HasColumnName("hora_inicio").HasColumnType("time(0)");
            entity.Property(e => e.HoraFin).HasColumnName("hora_fin").HasColumnType("time(0)");
            entity.Property(e => e.Abierto).HasColumnName("abierto").HasDefaultValue(true).HasSentinel(true);
            // Fase 9: quien hizo el ultimo cambio y cuando.
            entity.Property(e => e.IdEmpleadoModifica).HasColumnName("id_empleado_modifica");
            entity.Property(e => e.FechaModificacion).HasColumnName("fecha_modificacion").HasColumnType("datetimeoffset(0)");
            // Fase 11: version para guardar el horario con If-Match.
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();

            entity.HasIndex(e => e.DiaSemana).IsUnique().HasDatabaseName("uq_horarios_dia");
            entity.HasIndex(e => e.IdEmpleadoModifica).HasDatabaseName("ix_horarios_empleado_modifica");
            entity.HasOne<Empleado>().WithMany()
                .HasForeignKey(e => e.IdEmpleadoModifica)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_horarios_empleado_modifica");

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
            // Fase 9: hasta 100 caracteres (antes 80).
            entity.Property(e => e.TiempoEstimadoRegreso).HasColumnName("tiempo_estimado_regreso").HasMaxLength(100);
            entity.Property(e => e.FechaUltimaModificacion).HasColumnName("fecha_ultima_modificacion").HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.IdEmpleadoModifica).HasColumnName("id_empleado_modifica");
            // Fase 11: version para guardar el cartel con If-Match.
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();

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
            // Fase 10: sin formato no hay muestras; con formato 2 o 3, la fila vigente tiene las tres y la revocada ninguna.
            entity.ToTable("rostros_empleado", t => t.HasCheckConstraint("ck_rostros_empleado_muestras",
                "(formato_muestras IS NULL AND descriptor_frente IS NULL AND descriptor_izquierda IS NULL AND descriptor_derecha IS NULL) OR "
                + "(formato_muestras IN (2, 3) AND ((fecha_revocacion IS NULL AND descriptor_frente IS NOT NULL AND descriptor_izquierda IS NOT NULL AND descriptor_derecha IS NOT NULL) OR "
                + "(fecha_revocacion IS NOT NULL AND descriptor_frente IS NULL AND descriptor_izquierda IS NULL AND descriptor_derecha IS NULL)))"));
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
            entity.Property(e => e.DescriptorFrente).HasColumnName("descriptor_frente").HasMaxLength(1024);
            entity.Property(e => e.DescriptorIzquierda).HasColumnName("descriptor_izquierda").HasMaxLength(1024);
            entity.Property(e => e.DescriptorDerecha).HasColumnName("descriptor_derecha").HasMaxLength(1024);
            entity.Property(e => e.IdEmpleadoRegistra).HasColumnName("id_empleado_registra");
            entity.Property(e => e.FormatoMuestras).HasColumnName("formato_muestras");
            entity.Ignore(e => e.TieneTresMuestras);

            entity.HasOne(e => e.Empleado)
                .WithMany()
                .HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_rostros_empleado_empleado");
            entity.HasOne(e => e.EmpleadoRegistra)
                .WithMany()
                .HasForeignKey(e => e.IdEmpleadoRegistra)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_rostros_empleado_registra");
            entity.HasIndex(e => e.IdEmpleadoRegistra).HasDatabaseName("ix_rostros_empleado_registra");

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
            // Fase 11: baja con un clic, «Correos no enviados» y su reenvio.
            entity.Property(e => e.EnlaceBaja).HasColumnName("enlace_baja").HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.Revisado).HasColumnName("revisado").HasDefaultValue(false);
            entity.Property(e => e.ReenviadoComo).HasColumnName("reenviado_como");
            entity.Property(e => e.IdEmpleadoReenvia).HasColumnName("id_empleado_reenvia");
            entity.Property(e => e.FechaReenvio).HasColumnName("fecha_reenvio").HasColumnType("datetimeoffset(0)");
            entity.HasOne<Empleado>().WithMany()
                .HasForeignKey(e => e.IdEmpleadoReenvia)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_correos_salientes_empleado_reenvia");
            entity.HasIndex(e => e.IdEmpleadoReenvia).HasFilter("[id_empleado_reenvia] IS NOT NULL").HasDatabaseName("ix_correos_salientes_empleado_reenvia");
            // «Correos no enviados»: solo los fallidos, del mas reciente al mas antiguo (proximo_intento guarda cuando fallo).
            entity.HasIndex(e => new { e.ProximoIntento, e.Revisado })
                .HasFilter("[estado] = 'fallido'")
                .HasDatabaseName("ix_correos_salientes_fallidos");

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
                // Fase 11: avisos_estado, la preferencia de los avisos de estado del cliente.
                t.HasCheckConstraint("ck_consentimientos_tipo", "tipo IN ('privacidad', 'terminos', 'marketing', 'cookies', 'avisos_estado')");
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

        // ── identidades_externas (fase 14A) ──
        modelBuilder.Entity<IdentidadExterna>(entity =>
        {
            entity.ToTable("identidades_externas", t =>
            {
                t.HasCheckConstraint("ck_identidades_externas_proveedor", "proveedor IN ('google')");
                // Fase 14C: la identidad es de un cliente o de un empleado, nunca de ninguno ni de ambos.
                t.HasCheckConstraint("ck_identidades_externas_destino",
                    "(CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) = 1");
            });
            entity.HasKey(e => e.IdIdentidad).HasName("pk_identidades_externas");
            entity.Property(e => e.IdIdentidad).HasColumnName("id_identidad");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.Proveedor).HasColumnName("proveedor").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Sub).HasColumnName("sub").HasMaxLength(255).IsUnicode(false).UseCollation("Latin1_General_BIN2");
            entity.Property(e => e.Correo).HasColumnName("correo").HasMaxLength(254);
            entity.Property(e => e.FechaVinculacion).HasColumnName("fecha_vinculacion").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.FechaUltimoIngreso).HasColumnName("fecha_ultimo_ingreso").HasColumnType("datetimeoffset(0)");

            // Un mismo Google no puede estar en dos cuentas, ni una cuenta tener dos Google (por proveedor).
            entity.HasIndex(e => new { e.Proveedor, e.Sub }).IsUnique().HasDatabaseName("uq_identidades_externas_sub");
            // Fase 14C: los indices por cuenta son FILTRADOS (los clientes tienen id_empleado nulo y los empleados, id_cliente nulo).
            entity.HasIndex(e => new { e.IdCliente, e.Proveedor }).IsUnique().HasDatabaseName("uq_identidades_externas_cliente").HasFilter("[id_cliente] IS NOT NULL");
            entity.HasIndex(e => new { e.IdEmpleado, e.Proveedor }).IsUnique().HasDatabaseName("uq_identidades_externas_empleado").HasFilter("[id_empleado] IS NOT NULL");

            entity.HasOne(e => e.Cliente).WithMany(c => c.IdentidadesExternas).HasForeignKey(e => e.IdCliente)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_identidades_externas_cliente");
            entity.HasOne(e => e.Empleado).WithMany(c => c.IdentidadesExternas).HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_identidades_externas_empleado");
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

        // ── disponibilidad_empleados (fase 9, RFU20) ──
        // Franjas semanales de atencion de cada empleado. Que no se crucen lo revisa la API.
        modelBuilder.Entity<DisponibilidadEmpleado>(entity =>
        {
            entity.ToTable("disponibilidad_empleados", t =>
            {
                t.HasCheckConstraint("ck_disponibilidad_dia", "dia_semana BETWEEN 1 AND 7");
                t.HasCheckConstraint("ck_disponibilidad_rango", "hora_inicio < hora_fin");
            });
            entity.HasKey(e => e.IdDisponibilidad).HasName("pk_disponibilidad_empleados");
            entity.Property(e => e.IdDisponibilidad).HasColumnName("id_disponibilidad");
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.DiaSemana).HasColumnName("dia_semana");
            entity.Property(e => e.HoraInicio).HasColumnName("hora_inicio").HasColumnType("time(0)");
            entity.Property(e => e.HoraFin).HasColumnName("hora_fin").HasColumnType("time(0)");

            entity.HasIndex(e => new { e.IdEmpleado, e.DiaSemana }).HasDatabaseName("ix_disponibilidad_empleado_dia");

            entity.HasOne(e => e.Empleado).WithMany()
                .HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_disponibilidad_empleado");
        });

        // ── destinatarios_avisos (fase 9) ──
        // Los correos que reciben los avisos del taller. Un mismo correo no puede estar dos veces activo o
        // pendiente, y el enlace de confirmacion (su hash y su vencimiento) existe solo mientras esta pendiente.
        modelBuilder.Entity<DestinatarioAviso>(entity =>
        {
            entity.ToTable("destinatarios_avisos", t =>
            {
                t.HasCheckConstraint("ck_destinatarios_avisos_estado", "estado IN ('pendiente', 'activo', 'retirado', 'cancelado')");
                t.HasCheckConstraint("ck_destinatarios_avisos_enlace",
                    "(estado = 'pendiente' AND token_hash IS NOT NULL AND vence IS NOT NULL) OR (estado <> 'pendiente' AND token_hash IS NULL AND vence IS NULL)");
                t.HasCheckConstraint("ck_destinatarios_avisos_correo", "correo COLLATE Latin1_General_100_BIN2 = LOWER(correo) COLLATE Latin1_General_100_BIN2 AND correo LIKE N'%_@_%._%' AND correo NOT LIKE N'% %'");
                // Fase 11: un destino recibe al menos un origen de solicitudes.
                t.HasCheckConstraint("ck_destinatarios_avisos_preferencia", "recibe_en_linea = 1 OR recibe_presenciales = 1");
            });
            entity.HasKey(e => e.IdDestinatario).HasName("pk_destinatarios_avisos");
            entity.Property(e => e.IdDestinatario).HasColumnName("id_destinatario");
            entity.Property(e => e.Correo).HasColumnName("correo").HasMaxLength(254);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(10).IsUnicode(false);
            entity.Property(e => e.IdReemplaza).HasColumnName("id_reemplaza");
            entity.Property(e => e.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsUnicode(false).IsFixedLength();
            entity.Property(e => e.Vence).HasColumnName("vence").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.IdEmpleadoSolicita).HasColumnName("id_empleado_solicita");
            entity.Property(e => e.FechaSolicitud).HasColumnName("fecha_solicitud").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.FechaConfirmacion).HasColumnName("fecha_confirmacion").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.FechaRetiro).HasColumnName("fecha_retiro").HasColumnType("datetimeoffset(0)");
            // Fase 11 (decision 1): que solicitudes avisa cada destino, y su version.
            entity.Property(e => e.RecibeEnLinea).HasColumnName("recibe_en_linea").HasDefaultValue(true).HasSentinel(true);
            entity.Property(e => e.RecibePresenciales).HasColumnName("recibe_presenciales").HasDefaultValue(false);
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();

            entity.HasIndex(e => e.Correo).IsUnique().HasFilter("[estado] IN ('activo', 'pendiente')")
                .HasDatabaseName("uq_destinatarios_avisos_vigente");
            entity.HasIndex(e => e.TokenHash).IsUnique().HasFilter("[token_hash] IS NOT NULL")
                .HasDatabaseName("uq_destinatarios_avisos_token");
            entity.HasIndex(e => e.IdReemplaza).HasDatabaseName("ix_destinatarios_avisos_reemplaza");
            entity.HasIndex(e => e.IdEmpleadoSolicita).HasDatabaseName("ix_destinatarios_avisos_empleado");

            entity.HasOne(e => e.Reemplaza).WithMany()
                .HasForeignKey(e => e.IdReemplaza)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_destinatarios_avisos_reemplaza");
            entity.HasOne(e => e.EmpleadoSolicita).WithMany()
                .HasForeignKey(e => e.IdEmpleadoSolicita)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_destinatarios_avisos_empleado");
        });

        // ── destinatarios_avisos_historial (fase 9) ──
        modelBuilder.Entity<DestinatarioAvisoHistorial>(entity =>
        {
            entity.ToTable("destinatarios_avisos_historial", t =>
                t.HasCheckConstraint("ck_destinatarios_avisos_historial_accion",
                    "accion IN ('agregar', 'reemplazar', 'confirmar', 'cancelar', 'quitar', 'reenviar', 'probar', 'preferencia')"));
            entity.HasKey(e => e.IdHistorial).HasName("pk_destinatarios_avisos_historial");
            entity.Property(e => e.IdHistorial).HasColumnName("id_historial");
            entity.Property(e => e.Accion).HasColumnName("accion").HasMaxLength(12).IsUnicode(false);
            entity.Property(e => e.CorreoEnmascarado).HasColumnName("correo_enmascarado").HasMaxLength(254);
            entity.Property(e => e.IdDestinatario).HasColumnName("id_destinatario");
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.Fecha).HasColumnName("fecha").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.Detalle).HasColumnName("detalle").HasMaxLength(300);

            // La seccion del perfil muestra los ultimos; los limites de reenvio y de prueba cuentan por accion.
            entity.HasIndex(e => e.Fecha).HasDatabaseName("ix_destinatarios_avisos_historial_fecha");
            entity.HasIndex(e => new { e.IdDestinatario, e.Accion, e.Fecha }).HasDatabaseName("ix_destinatarios_avisos_historial_destino");
            entity.HasIndex(e => e.IdEmpleado).HasDatabaseName("ix_destinatarios_avisos_historial_empleado");

            entity.HasOne<DestinatarioAviso>().WithMany()
                .HasForeignKey(e => e.IdDestinatario)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_destinatarios_avisos_historial_destino");
            entity.HasOne(e => e.Empleado).WithMany()
                .HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_destinatarios_avisos_historial_empleado");
        });

        // ── desafios_rostro (fase 10) ──
        // Desafios de la prueba de vida: solo hashes, un solo uso y vida corta. El indice de emitido sirve a la purga.
        modelBuilder.Entity<DesafioRostro>(entity =>
        {
            entity.ToTable("desafios_rostro", t =>
            {
                t.HasCheckConstraint("ck_desafios_rostro_orden", "orden IN ('izquierda_derecha', 'derecha_izquierda')");
                t.HasCheckConstraint("ck_desafios_rostro_vigencia", "vence > emitido AND (usado IS NULL OR usado >= emitido)");
            });
            entity.HasKey(e => e.IdDesafio).HasName("pk_desafios_rostro");
            entity.Property(e => e.IdDesafio).HasColumnName("id_desafio");
            entity.Property(e => e.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsUnicode(false).IsFixedLength();
            entity.Property(e => e.CorreoHash).HasColumnName("correo_hash").HasMaxLength(64).IsUnicode(false).IsFixedLength();
            entity.Property(e => e.Orden).HasColumnName("orden").HasMaxLength(17).IsUnicode(false);
            entity.Property(e => e.Emitido).HasColumnName("emitido").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.Vence).HasColumnName("vence").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.Usado).HasColumnName("usado").HasColumnType("datetimeoffset(0)");

            entity.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("uq_desafios_rostro_token");
            entity.HasIndex(e => e.Emitido).HasDatabaseName("ix_desafios_rostro_emitido");
        });

        // ── calibracion_rostro (fase 10) ──
        // Mediciones de la calibracion: solo numeros, sin datos biometricos.
        modelBuilder.Entity<MedicionCalibracionRostro>(entity =>
        {
            entity.ToTable("calibracion_rostro", t =>
                t.HasCheckConstraint("ck_calibracion_rostro_etiqueta", "etiqueta IN ('propio', 'otra_persona')"));
            entity.HasKey(e => e.IdMedicion).HasName("pk_calibracion_rostro");
            entity.Property(e => e.IdMedicion).HasColumnName("id_medicion");
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.Etiqueta).HasColumnName("etiqueta").HasMaxLength(12).IsUnicode(false);
            entity.Property(e => e.DistanciaFrente).HasColumnName("distancia_frente");
            entity.Property(e => e.DistanciaPrimerGiro).HasColumnName("distancia_primer_giro");
            entity.Property(e => e.DistanciaSegundoGiro).HasColumnName("distancia_segundo_giro");
            entity.Property(e => e.DistanciaMaxEntreCapturas).HasColumnName("distancia_max_entre_capturas");
            entity.Property(e => e.GiroFrente).HasColumnName("giro_frente");
            entity.Property(e => e.GiroPrimero).HasColumnName("giro_primero");
            entity.Property(e => e.GiroSegundo).HasColumnName("giro_segundo");
            entity.Property(e => e.UmbralVigente).HasColumnName("umbral_vigente");
            entity.Property(e => e.IdEmpleadoRegistra).HasColumnName("id_empleado_registra");
            entity.Property(e => e.Fecha).HasColumnName("fecha").HasColumnType("datetimeoffset(0)");

            entity.HasIndex(e => e.IdEmpleado).HasDatabaseName("ix_calibracion_rostro_empleado");
            entity.HasIndex(e => e.IdEmpleadoRegistra).HasDatabaseName("ix_calibracion_rostro_registra");

            entity.HasOne(e => e.Empleado).WithMany()
                .HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_calibracion_rostro_empleado");
            entity.HasOne(e => e.EmpleadoRegistra).WithMany()
                .HasForeignKey(e => e.IdEmpleadoRegistra)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_calibracion_rostro_registra");
        });

        // ── auditoria (fase 11) ──
        // Inmutable: el trigger trg_auditoria_inmutable (migracion NotificacionesYAuditoria) rechaza UPDATE y DELETE.
        // Sin claves foraneas a proposito: la bitacora sobrevive a lo que registra y no frena ninguna otra operacion.
        modelBuilder.Entity<RegistroAuditoria>(entity =>
        {
            entity.ToTable("auditoria", t =>
                t.HasCheckConstraint("ck_auditoria_quien", "id_empleado IS NULL OR id_cliente IS NULL"));
            entity.HasKey(e => e.IdAuditoria).HasName("pk_auditoria");
            entity.Property(e => e.IdAuditoria).HasColumnName("id_auditoria");
            entity.Property(e => e.Fecha).HasColumnName("fecha").HasColumnType("datetimeoffset(0)").HasDefaultValueSql("SYSDATETIMEOFFSET()");
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.Rol).HasColumnName("rol").HasMaxLength(20);
            entity.Property(e => e.Entidad).HasColumnName("entidad").HasMaxLength(40).IsUnicode(false);
            entity.Property(e => e.IdEntidad).HasColumnName("id_entidad").HasMaxLength(40).IsUnicode(false);
            entity.Property(e => e.Accion).HasColumnName("accion").HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Detalle).HasColumnName("detalle").HasMaxLength(2000);
            entity.Property(e => e.Ip).HasColumnName("ip").HasMaxLength(45).IsUnicode(false);

            // El visor ordena por fecha e id, del mas reciente al mas antiguo: los indices tienen esa misma clave.
            entity.HasIndex(e => new { e.Fecha, e.IdAuditoria }).IsDescending(true, true).HasDatabaseName("ix_auditoria_fecha");
            // Fase 12B (RNFE01): la purga diaria de la IP a los 90 dias solo mira las filas que aun la guardan.
            entity.HasIndex(e => e.Fecha).HasDatabaseName("ix_auditoria_ip_vigente").HasFilter("[ip] IS NOT NULL");
            entity.HasIndex(e => new { e.Entidad, e.IdEntidad }).HasDatabaseName("ix_auditoria_entidad");
            entity.HasIndex(e => new { e.IdEmpleado, e.Fecha, e.IdAuditoria }).IsDescending(false, true, true)
                .HasFilter("[id_empleado] IS NOT NULL").HasDatabaseName("ix_auditoria_empleado");
            entity.HasIndex(e => new { e.IdCliente, e.Fecha }).IsDescending(false, true)
                .HasFilter("[id_cliente] IS NOT NULL").HasDatabaseName("ix_auditoria_cliente");
        });

        // ── claves_idempotencia (fase 11) ──
        modelBuilder.Entity<ClaveIdempotencia>(entity =>
        {
            entity.ToTable("claves_idempotencia", t =>
            {
                t.HasCheckConstraint("ck_claves_idempotencia_tipo_usuario", "tipo_usuario IN ('cliente', 'empleado')");
                t.HasCheckConstraint("ck_claves_idempotencia_vence", "vence > fecha");
            });
            entity.HasKey(e => e.IdClave).HasName("pk_claves_idempotencia");
            entity.Property(e => e.IdClave).HasColumnName("id_clave");
            entity.Property(e => e.Clave).HasColumnName("clave").HasMaxLength(36).IsUnicode(false).IsFixedLength();
            entity.Property(e => e.Ambito).HasColumnName("ambito").HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.TipoUsuario).HasColumnName("tipo_usuario").HasMaxLength(10).IsUnicode(false);
            entity.Property(e => e.HashCuerpo).HasColumnName("hash_cuerpo").HasMaxLength(64).IsUnicode(false).IsFixedLength();
            entity.Property(e => e.EstadoHttp).HasColumnName("estado_http");
            entity.Property(e => e.Respuesta).HasColumnName("respuesta");
            entity.Property(e => e.Fecha).HasColumnName("fecha").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.Vence).HasColumnName("vence").HasColumnType("datetimeoffset(0)");

            entity.HasIndex(e => new { e.Clave, e.Ambito, e.TipoUsuario, e.IdUsuario }).IsUnique().HasDatabaseName("uq_claves_idempotencia");
            entity.HasIndex(e => e.Vence).HasDatabaseName("ix_claves_idempotencia_vence");
        });

        // ── trabajos_exportacion (fase 13A, migracion ExportacionesDeReportes) ──
        // Las exportaciones de reportes en segundo plano. Un solo trabajo activo por persona lo garantiza la base (indice unico filtrado).
        modelBuilder.Entity<TrabajoExportacion>(entity =>
        {
            entity.ToTable("trabajos_exportacion", t =>
            {
                t.HasCheckConstraint("ck_trabajos_exportacion_reporte", "reporte IN ('operativo', 'gerencial', 'parametrizable')");
                t.HasCheckConstraint("ck_trabajos_exportacion_formato", "formato IN ('xlsx', 'csv', 'pdf')");
                t.HasCheckConstraint("ck_trabajos_exportacion_estado", "estado IN ('pendiente', 'procesando', 'listo', 'fallido')");
                t.HasCheckConstraint("ck_trabajos_exportacion_intentos", "intentos >= 0");
                t.HasCheckConstraint("ck_trabajos_exportacion_listo", "estado <> 'listo' OR (fecha_termino IS NOT NULL AND fecha_vencimiento IS NOT NULL AND nombre_archivo IS NOT NULL AND filas IS NOT NULL)");
                t.HasCheckConstraint("ck_trabajos_exportacion_fallido", "estado <> 'fallido' OR error IS NOT NULL");
            });
            entity.HasKey(e => e.IdTrabajo).HasName("pk_trabajos_exportacion");
            entity.Property(e => e.IdTrabajo).HasColumnName("id_trabajo").ValueGeneratedNever();
            entity.Property(e => e.Reporte).HasColumnName("reporte").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Formato).HasColumnName("formato").HasMaxLength(5).IsUnicode(false);
            entity.Property(e => e.Filtros).HasColumnName("filtros").HasMaxLength(1000).IsUnicode(false);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(12).IsUnicode(false).HasDefaultValue(EstadosExportacion.Pendiente);
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.RolSolicitante).HasColumnName("rol_solicitante").HasMaxLength(20);
            entity.Property(e => e.FechaPedido).HasColumnName("fecha_pedido").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.FechaInicio).HasColumnName("fecha_inicio").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.FechaTermino).HasColumnName("fecha_termino").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.FechaVencimiento).HasColumnName("fecha_vencimiento").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.BloqueadoHasta).HasColumnName("bloqueado_hasta").HasColumnType("datetimeoffset(0)");
            entity.Property(e => e.Intentos).HasColumnName("intentos").HasDefaultValue((byte)0);
            entity.Property(e => e.NombreArchivo).HasColumnName("nombre_archivo").HasMaxLength(80).IsUnicode(false);
            entity.Property(e => e.TamanoBytes).HasColumnName("tamano_bytes");
            entity.Property(e => e.Filas).HasColumnName("filas");
            entity.Property(e => e.Error).HasColumnName("error").HasMaxLength(300);
            entity.Property(e => e.ArchivoBorradoEn).HasColumnName("archivo_borrado_en").HasColumnType("datetimeoffset(0)");
            entity.HasOne(e => e.Empleado).WithMany()
                .HasForeignKey(e => e.IdEmpleado)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_trabajos_exportacion_empleado");
            // Un solo trabajo pendiente o en proceso por persona (RNFP18): la base lo impone, no solo la API.
            entity.HasIndex(e => e.IdEmpleado).IsUnique().HasFilter("[estado] IN ('pendiente', 'procesando')").HasDatabaseName("ux_trabajos_exportacion_activo");
            // La cola: lo pendiente en orden de llegada, y lo que quedo «procesando» de un proceso que se cayo.
            entity.HasIndex(e => new { e.Estado, e.FechaPedido }).HasDatabaseName("ix_trabajos_exportacion_estado");
            // La purga: los archivos que vencieron y todavia no se borraron.
            entity.HasIndex(e => e.FechaVencimiento).HasFilter("[estado] = 'listo' AND [archivo_borrado_en] IS NULL").HasDatabaseName("ix_trabajos_exportacion_vencimiento");
            // La persona consulta sus trabajos.
            entity.HasIndex(e => new { e.IdEmpleado, e.FechaPedido }).HasDatabaseName("ix_trabajos_exportacion_empleado");
        });

        // ── vistas del dashboard (las crea la migracion IndicadoresDashboard) ──
        modelBuilder.Entity<VistaIndicadorPedido>(entity =>
        {
            entity.ToView("vw_indicadores_pedidos");
            entity.HasNoKey();
            entity.Property(e => e.IdPedido).HasColumnName("id_pedido");
            entity.Property(e => e.IdSolicitud).HasColumnName("id_solicitud");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.OrigenSolicitud).HasColumnName("origen_solicitud");
            entity.Property(e => e.FechaPedido).HasColumnName("fecha_pedido");
            entity.Property(e => e.FechaEntregaEstimada).HasColumnName("fecha_entrega_estimada");
            entity.Property(e => e.FechaEntregaReal).HasColumnName("fecha_entrega_real");
            entity.Property(e => e.EstadoPedido).HasColumnName("estado_pedido");
            entity.Property(e => e.EstadoPago).HasColumnName("estado_pago");
            entity.Property(e => e.MontoTotalFinal).HasColumnName("monto_total_final");
            entity.Property(e => e.MontoAbonado).HasColumnName("monto_abonado");
            entity.Property(e => e.SaldoPendiente).HasColumnName("saldo_pendiente");
        });
        modelBuilder.Entity<VistaIndicadorSolicitud>(entity =>
        {
            entity.ToView("vw_indicadores_solicitudes");
            entity.HasNoKey();
            entity.Property(e => e.IdSolicitud).HasColumnName("id_solicitud");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.Origen).HasColumnName("origen");
            entity.Property(e => e.FechaSolicitud).HasColumnName("fecha_solicitud");
            entity.Property(e => e.IdCategoriaPrincipal).HasColumnName("id_categoria_principal");
            entity.Property(e => e.Cotizada).HasColumnName("cotizada");
            entity.Property(e => e.Aprobada).HasColumnName("aprobada");
            entity.Property(e => e.Concretada).HasColumnName("concretada");
        });
        modelBuilder.Entity<VistaIndicadorItem>(entity =>
        {
            entity.ToView("vw_indicadores_items");
            entity.HasNoKey();
            entity.Property(e => e.IdSolicitudDetalle).HasColumnName("id_solicitud_detalle");
            entity.Property(e => e.IdPedido).HasColumnName("id_pedido");
            entity.Property(e => e.IdProducto).HasColumnName("id_producto");
            entity.Property(e => e.IdCategoria).HasColumnName("id_categoria");
            entity.Property(e => e.Cantidad).HasColumnName("cantidad");
            entity.Property(e => e.MontoAsignado).HasColumnName("monto_asignado").HasColumnType("decimal(19,4)");
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
