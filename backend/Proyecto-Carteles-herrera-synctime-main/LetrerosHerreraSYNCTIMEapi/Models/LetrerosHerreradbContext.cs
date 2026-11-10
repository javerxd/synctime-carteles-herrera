using Microsoft.EntityFrameworkCore;

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
    public DbSet<HorarioAtencion> HorariosAtencion { get; set; } = null!;
    public DbSet<CartelDigital> CartelDigital { get; set; } = null!;
    public DbSet<MetricaConversionOnline> MetricasConversionOnline { get; set; } = null!;
    public DbSet<RostroEmpleado> RostrosEmpleado { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Modern_Spanish_CI_AS");

        // HasSentinel indica que valor deja actuar al DEFAULT de la columna. Por defecto seria el del tipo
        // (false, 0), y un activo=false o una cantidad=0 enviados a proposito se confundirian con "sin asignar".

        // ── clientes ──
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("clientes", t =>
                t.HasCheckConstraint("ck_clientes_email", "email IS NULL OR (email LIKE N'%_@_%._%' AND email NOT LIKE N'% %')"));
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

            entity.HasIndex(e => new { e.Visible, e.IdProducto }).HasDatabaseName("ix_catalogo_visible_producto");

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
            entity.Property(e => e.DescripcionGeneral).HasColumnName("descripcion_general").HasMaxLength(500);
            entity.Property(e => e.FechaSolicitud).HasColumnName("fecha_solicitud").HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(15).HasDefaultValue("solicitado");
            entity.Property(e => e.MotivoRechazo).HasColumnName("motivo_rechazo").HasMaxLength(250);
            entity.Property(e => e.EliminadoLogico).HasColumnName("eliminado_logico").HasDefaultValue(false);
            entity.Property(e => e.FechaEliminacionLogica).HasColumnName("fecha_eliminacion_logica").HasColumnType("datetime2(0)");
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();

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
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasMaxLength(300);
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
            });
            entity.HasKey(e => e.IdCotizacion).HasName("pk_cotizaciones");
            entity.Property(e => e.IdCotizacion).HasColumnName("id_cotizacion");
            entity.Property(e => e.IdSolicitud).HasColumnName("id_solicitud");
            entity.Property(e => e.IdEmpleado).HasColumnName("id_empleado");
            entity.Property(e => e.FechaCotizacion).HasColumnName("fecha_cotizacion").HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.Observaciones).HasColumnName("observaciones").HasMaxLength(500);
            entity.Property(e => e.CostoMaterialesTotal).HasColumnName("costo_materiales_total").HasColumnType("decimal(12,2)")
                .HasDefaultValue(0m);
            entity.Property(e => e.CostoManoObra).HasColumnName("costo_mano_obra").HasColumnType("decimal(12,2)")
                .HasDefaultValue(0m);
            entity.Property(e => e.OtrosCostos).HasColumnName("otros_costos").HasColumnType("decimal(12,2)")
                .HasDefaultValue(0m);
            entity.Property(e => e.TotalCotizacion).HasColumnName("total_cotizacion").HasColumnType("decimal(14,2)")
                .HasComputedColumnSql("CONVERT(DECIMAL(14,2), costo_materiales_total + costo_mano_obra + otros_costos)", stored: true);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(15).HasDefaultValue("pendiente");

            entity.HasIndex(e => new { e.IdSolicitud, e.Estado }).HasDatabaseName("ix_cotizaciones_solicitud");

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
            });
            entity.HasKey(e => e.IdCotizacionMaterial).HasName("pk_cotizacion_materiales");
            entity.Property(e => e.IdCotizacionMaterial).HasColumnName("id_cotizacion_material");
            entity.Property(e => e.IdCotizacion).HasColumnName("id_cotizacion");
            entity.Property(e => e.IdMaterial).HasColumnName("id_material");
            entity.Property(e => e.MedidaAplicada).HasColumnName("medida_aplicada").HasMaxLength(80);
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
        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.ToTable("pedidos", t =>
            {
                t.HasCheckConstraint("ck_pedidos_modalidad", "modalidad_entrega IN (N'retiro_local', N'entrega_domicilio', N'instalacion_terreno')");
                t.HasCheckConstraint("ck_pedidos_costos", "costo_instalacion_adicional >= 0 AND monto_base_trabajo >= 0");
                t.HasCheckConstraint("ck_pedidos_estado", "estado IN (N'aceptado', N'en_proceso', N'terminado', N'entregado', N'cancelado')");
            });
            entity.HasKey(e => e.IdPedido).HasName("pk_pedidos");
            entity.Property(e => e.IdPedido).HasColumnName("id_pedido");
            entity.Property(e => e.IdSolicitud).HasColumnName("id_solicitud");
            entity.Property(e => e.IdCotizacion).HasColumnName("id_cotizacion");
            entity.Property(e => e.IdEmpleadoResponsable).HasColumnName("id_empleado_responsable");
            entity.Property(e => e.FechaPedido).HasColumnName("fecha_pedido").HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.FechaEntregaEstimada).HasColumnName("fecha_entrega_estimada").HasColumnType("datetime2(0)");
            entity.Property(e => e.FechaEntregaReal).HasColumnName("fecha_entrega_real").HasColumnType("datetime2(0)");
            entity.Property(e => e.ModalidadEntrega).HasColumnName("modalidad_entrega").HasMaxLength(25)
                .HasDefaultValue("retiro_local");
            entity.Property(e => e.RequiereInstalacion).HasColumnName("requiere_instalacion").HasDefaultValue(false);
            entity.Property(e => e.DireccionInstalacion).HasColumnName("direccion_instalacion").HasMaxLength(255);
            entity.Property(e => e.CostoInstalacionAdicional).HasColumnName("costo_instalacion_adicional").HasColumnType("decimal(12,2)")
                .HasDefaultValue(0m);
            entity.Property(e => e.MontoBaseTrabajo).HasColumnName("monto_base_trabajo").HasColumnType("decimal(12,2)")
                .HasDefaultValue(0m);
            entity.Property(e => e.MontoTotalFinal).HasColumnName("monto_total_final").HasColumnType("decimal(14,2)")
                .HasComputedColumnSql("CONVERT(DECIMAL(14,2), monto_base_trabajo + costo_instalacion_adicional)", stored: true);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(15).HasDefaultValue("aceptado");
            entity.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();

            entity.HasIndex(e => e.IdSolicitud).IsUnique().HasDatabaseName("uq_pedidos_solicitud");
            entity.HasIndex(e => new { e.FechaEntregaEstimada, e.Estado }).HasDatabaseName("ix_pedidos_calendario_entrega");
            entity.HasIndex(e => new { e.FechaPedido, e.Estado }).HasDatabaseName("ix_pedidos_fecha_estado");

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
