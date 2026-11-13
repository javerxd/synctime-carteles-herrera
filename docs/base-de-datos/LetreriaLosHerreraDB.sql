-- =====================================================================================
-- SyncTime - Letrería Los Herrera
-- Script completo de la base de datos: tablas, restricciones, índices, datos iniciales,
-- triggers, vista y procedimiento almacenado. Estado: migración HistorialDePagos.
-- Generado el 12-11-2026 desde el modelo de EF Core, que es la fuente única del esquema.
--
-- Cambios de la revisión del 12-11-2026 (tabla pedidos):
--   * estado        -> estado_pedido: aceptado, en_proceso, terminado, entregado, anulado.
--                      "anulado" reemplaza a "cancelado", que en Chile se entiende como "pagado".
--   * estado_pago   (nuevo, calculado): no_pagado, abonado, pagado_total.
--   * monto_abonado (nuevo, pesos), saldo_pendiente (nuevo, calculado) y numero_factura (nuevo, opcional).
--   * origen        (nuevo): cotizacion o directo (pedido presencial acordado en el taller).
--   * pagos         (tabla nueva): historial de abonos con monto, fecha, medio de pago y quién lo
--                      registró. Un trigger mantiene monto_abonado igual a la suma de los pagos.
--   * ck_pedidos_montos_enteros (nueva): los montos del pedido van en pesos, sin decimales.
--
-- Cómo ejecutarlo. Crea una base NUEVA y se detiene sin hacer nada si ya existe:
--   sqlcmd -S localhost\SQLEXPRESS -E -C -I -f 65001 -i LetreriaLosHerreraDB.sql
--   -f 65001 conserva tildes y eñes; -I activa QUOTED_IDENTIFIER, que exigen los índices filtrados.
--   En SQL Server Management Studio basta con abrirlo y ejecutarlo (F5).
-- =====================================================================================

IF DB_ID(N'LetreriaLosHerreraDB') IS NOT NULL
BEGIN
    RAISERROR(N'La base LetreriaLosHerreraDB ya existe. Este script solo crea una base nueva: no se ejecutó nada.', 16, 1);
    SET NOEXEC ON;
END
GO

CREATE DATABASE [LetreriaLosHerreraDB] COLLATE Modern_Spanish_CI_AS;
GO

USE [LetreriaLosHerreraDB];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- -------------------------------------------------------------------------------------
-- 1. Tablas, restricciones, índices y datos iniciales
-- -------------------------------------------------------------------------------------
CREATE TABLE [categorias] (
    [id_categoria] int NOT NULL IDENTITY,
    [nombre] nvarchar(80) NOT NULL,
    [descripcion] nvarchar(250) NULL,
    [activo] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [pk_categorias] PRIMARY KEY ([id_categoria])
);
GO


CREATE TABLE [clientes] (
    [id_cliente] int NOT NULL IDENTITY,
    [rut] varchar(12) NULL,
    [nombre] nvarchar(80) NOT NULL,
    [apellido] nvarchar(80) NOT NULL,
    [telefono] varchar(20) NOT NULL,
    [email] nvarchar(100) NULL,
    [direccion] nvarchar(255) NULL,
    [password_hash] varchar(100) NULL,
    [registrado] AS CONVERT(BIT, CASE WHEN password_hash IS NOT NULL THEN 1 ELSE 0 END) PERSISTED,
    [fecha_registro] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
    [activo] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [pk_clientes] PRIMARY KEY ([id_cliente]),
    CONSTRAINT [ck_clientes_email] CHECK (email IS NULL OR (email LIKE N'%_@_%._%' AND email NOT LIKE N'% %'))
);
GO


CREATE TABLE [empleados] (
    [id_empleado] int NOT NULL IDENTITY,
    [rut] varchar(12) NOT NULL,
    [nombre] nvarchar(80) NOT NULL,
    [apellido] nvarchar(80) NOT NULL,
    [telefono] varchar(20) NOT NULL,
    [email] nvarchar(100) NOT NULL,
    [password_hash] varchar(100) NOT NULL,
    [rol] nvarchar(20) NOT NULL DEFAULT N'administrador',
    [activo] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [pk_empleados] PRIMARY KEY ([id_empleado]),
    CONSTRAINT [ck_empleados_email] CHECK (email LIKE N'%_@_%._%' AND email NOT LIKE N'% %'),
    CONSTRAINT [ck_empleados_rol] CHECK (rol IN (N'administrador', N'dueño', N'diseñador', N'operario'))
);
GO


CREATE TABLE [horarios_atencion] (
    [id_horario] int NOT NULL IDENTITY,
    [dia_semana] tinyint NOT NULL,
    [nombre_dia] nvarchar(15) NOT NULL,
    [hora_inicio] time(0) NULL,
    [hora_fin] time(0) NULL,
    [abierto] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [pk_horarios_atencion] PRIMARY KEY ([id_horario]),
    CONSTRAINT [ck_horarios_dia] CHECK (dia_semana BETWEEN 1 AND 7),
    CONSTRAINT [ck_horarios_rango] CHECK (abierto = 0 OR (hora_inicio IS NOT NULL AND hora_fin IS NOT NULL AND hora_fin > hora_inicio))
);
GO


CREATE TABLE [materiales] (
    [id_material] int NOT NULL IDENTITY,
    [nombre] nvarchar(100) NOT NULL,
    [unidad_medida] nvarchar(30) NOT NULL,
    [costo_unitario_aprox] decimal(12,2) NOT NULL,
    [activo] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [pk_materiales] PRIMARY KEY ([id_material]),
    CONSTRAINT [ck_materiales_costo] CHECK (costo_unitario_aprox >= 0)
);
GO


CREATE TABLE [productos] (
    [id_producto] int NOT NULL IDENTITY,
    [id_categoria] int NOT NULL,
    [nombre] nvarchar(100) NOT NULL,
    [descripcion] nvarchar(250) NULL,
    [precio_base_ref] decimal(12,2) NULL,
    [activo] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [pk_productos] PRIMARY KEY ([id_producto]),
    CONSTRAINT [ck_productos_precio] CHECK (precio_base_ref IS NULL OR precio_base_ref >= 0),
    CONSTRAINT [fk_productos_categoria] FOREIGN KEY ([id_categoria]) REFERENCES [categorias] ([id_categoria]) ON DELETE NO ACTION
);
GO


CREATE TABLE [cartel_digital] (
    [id_cartel] int NOT NULL,
    [abierto_en_local] bit NOT NULL DEFAULT CAST(1 AS bit),
    [mensaje_estado] nvarchar(250) NOT NULL DEFAULT N'Local abierto y atendiendo',
    [tiempo_estimado_regreso] nvarchar(80) NULL,
    [fecha_ultima_modificacion] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
    [id_empleado_modifica] int NULL,
    CONSTRAINT [pk_cartel_digital] PRIMARY KEY ([id_cartel]),
    CONSTRAINT [ck_cartel_unico] CHECK (id_cartel = 1),
    CONSTRAINT [fk_cartel_digital_empleado] FOREIGN KEY ([id_empleado_modifica]) REFERENCES [empleados] ([id_empleado])
);
GO


CREATE TABLE [rostros_empleado] (
    [id_rostro] int NOT NULL IDENTITY,
    [id_empleado] int NOT NULL,
    [descriptor_cifrado] varbinary(1024) NULL,
    [fecha_consentimiento] datetimeoffset(0) NOT NULL,
    [version_consentimiento] varchar(20) NOT NULL,
    [fecha_revocacion] datetimeoffset(0) NULL,
    [intentos_fallidos] int NOT NULL,
    [bloqueado_hasta] datetimeoffset(0) NULL,
    [token_equipo_hash] varbinary(32) NULL,
    [fecha_autorizacion_equipo] datetimeoffset(0) NULL,
    CONSTRAINT [pk_rostros_empleado] PRIMARY KEY ([id_rostro]),
    CONSTRAINT [fk_rostros_empleado_empleado] FOREIGN KEY ([id_empleado]) REFERENCES [empleados] ([id_empleado])
);
GO


CREATE TABLE [solicitudes] (
    [id_solicitud] int NOT NULL IDENTITY,
    [id_cliente] int NOT NULL,
    [id_empleado_registra] int NULL,
    [origen] AS CASE WHEN id_empleado_registra IS NULL THEN N'online' ELSE N'presencial' END PERSISTED,
    [descripcion_general] nvarchar(500) NULL,
    [fecha_solicitud] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
    [estado] nvarchar(15) NOT NULL DEFAULT N'solicitado',
    [motivo_rechazo] nvarchar(250) NULL,
    [eliminado_logico] bit NOT NULL DEFAULT CAST(0 AS bit),
    [fecha_eliminacion_logica] datetime2(0) NULL,
    [row_version] rowversion NOT NULL,
    CONSTRAINT [pk_solicitudes] PRIMARY KEY ([id_solicitud]),
    CONSTRAINT [ck_solicitudes_estado] CHECK (estado IN (N'solicitado', N'cotizado', N'aceptado', N'rechazado')),
    CONSTRAINT [fk_solicitudes_cliente] FOREIGN KEY ([id_cliente]) REFERENCES [clientes] ([id_cliente]) ON DELETE NO ACTION,
    CONSTRAINT [fk_solicitudes_empleado] FOREIGN KEY ([id_empleado_registra]) REFERENCES [empleados] ([id_empleado])
);
GO


CREATE TABLE [catalogo] (
    [id_catalogo] int NOT NULL IDENTITY,
    [id_producto] int NOT NULL,
    [id_empleado_creador] int NULL,
    [titulo] nvarchar(120) NOT NULL,
    [descripcion] nvarchar(500) NOT NULL,
    [imagen_url] nvarchar(500) NOT NULL,
    [ancho_mm] int NULL,
    [alto_mm] int NULL,
    [precio_referencial] decimal(12,2) NULL,
    [fecha_publicacion] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
    [visible] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [pk_catalogo] PRIMARY KEY ([id_catalogo]),
    CONSTRAINT [ck_catalogo_medidas] CHECK ((ancho_mm IS NULL AND alto_mm IS NULL) OR (ancho_mm > 0 AND alto_mm > 0)),
    CONSTRAINT [ck_catalogo_precio] CHECK (precio_referencial IS NULL OR precio_referencial >= 0),
    CONSTRAINT [fk_catalogo_empleado] FOREIGN KEY ([id_empleado_creador]) REFERENCES [empleados] ([id_empleado]),
    CONSTRAINT [fk_catalogo_producto] FOREIGN KEY ([id_producto]) REFERENCES [productos] ([id_producto]) ON DELETE NO ACTION
);
GO


CREATE TABLE [cotizaciones] (
    [id_cotizacion] int NOT NULL IDENTITY,
    [id_solicitud] int NOT NULL,
    [id_empleado] int NOT NULL,
    [fecha_cotizacion] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
    [observaciones] nvarchar(500) NULL,
    [costo_materiales_total] decimal(12,2) NOT NULL DEFAULT 0.0,
    [costo_mano_obra] decimal(12,2) NOT NULL DEFAULT 0.0,
    [otros_costos] decimal(12,2) NOT NULL DEFAULT 0.0,
    [total_cotizacion] AS CONVERT(DECIMAL(14,2), costo_materiales_total + costo_mano_obra + otros_costos) PERSISTED,
    [estado] nvarchar(15) NOT NULL DEFAULT N'pendiente',
    CONSTRAINT [pk_cotizaciones] PRIMARY KEY ([id_cotizacion]),
    CONSTRAINT [ck_cotizaciones_costos] CHECK (costo_materiales_total >= 0 AND costo_mano_obra >= 0 AND otros_costos >= 0),
    CONSTRAINT [ck_cotizaciones_estado] CHECK (estado IN (N'pendiente', N'aprobada', N'rechazada')),
    CONSTRAINT [fk_cotizaciones_empleado] FOREIGN KEY ([id_empleado]) REFERENCES [empleados] ([id_empleado]) ON DELETE NO ACTION,
    CONSTRAINT [fk_cotizaciones_solicitud] FOREIGN KEY ([id_solicitud]) REFERENCES [solicitudes] ([id_solicitud]) ON DELETE CASCADE
);
GO


CREATE TABLE [solicitud_detalles] (
    [id_solicitud_detalle] int NOT NULL IDENTITY,
    [id_solicitud] int NOT NULL,
    [id_producto] int NOT NULL,
    [descripcion] nvarchar(300) NOT NULL,
    [ancho_mm] int NULL,
    [alto_mm] int NULL,
    [cantidad] int NOT NULL DEFAULT 1,
    [area_m2] AS CONVERT(DECIMAL(12,4), CONVERT(BIGINT, ancho_mm) * alto_mm / 1000000.0) PERSISTED,
    CONSTRAINT [pk_solicitud_detalles] PRIMARY KEY ([id_solicitud_detalle]),
    CONSTRAINT [ck_sol_detalles_cantidad] CHECK (cantidad > 0),
    CONSTRAINT [ck_sol_detalles_medidas] CHECK ((ancho_mm IS NULL AND alto_mm IS NULL) OR (ancho_mm > 0 AND alto_mm > 0)),
    CONSTRAINT [fk_sol_detalles_producto] FOREIGN KEY ([id_producto]) REFERENCES [productos] ([id_producto]) ON DELETE NO ACTION,
    CONSTRAINT [fk_sol_detalles_solicitud] FOREIGN KEY ([id_solicitud]) REFERENCES [solicitudes] ([id_solicitud]) ON DELETE CASCADE
);
GO


CREATE TABLE [cotizacion_materiales] (
    [id_cotizacion_material] int NOT NULL IDENTITY,
    [id_cotizacion] int NOT NULL,
    [id_material] int NOT NULL,
    [medida_aplicada] nvarchar(80) NULL,
    [cantidad] decimal(10,2) NOT NULL,
    [costo_unitario] decimal(12,2) NOT NULL,
    [subtotal] AS CONVERT(DECIMAL(14,2), cantidad * costo_unitario) PERSISTED,
    CONSTRAINT [pk_cotizacion_materiales] PRIMARY KEY ([id_cotizacion_material]),
    CONSTRAINT [ck_cot_mat_cantidad] CHECK (cantidad > 0),
    CONSTRAINT [ck_cot_mat_costo] CHECK (costo_unitario >= 0),
    CONSTRAINT [fk_cot_mat_cotizacion] FOREIGN KEY ([id_cotizacion]) REFERENCES [cotizaciones] ([id_cotizacion]) ON DELETE CASCADE,
    CONSTRAINT [fk_cot_mat_material] FOREIGN KEY ([id_material]) REFERENCES [materiales] ([id_material]) ON DELETE NO ACTION
);
GO


CREATE TABLE [pedidos] (
    [id_pedido] int NOT NULL IDENTITY,
    [id_solicitud] int NOT NULL,
    [id_cotizacion] int NULL,
    [origen] nvarchar(15) NOT NULL DEFAULT N'cotizacion',
    [id_empleado_responsable] int NOT NULL,
    [fecha_pedido] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
    [fecha_entrega_estimada] datetime2(0) NOT NULL,
    [fecha_entrega_real] datetime2(0) NULL,
    [modalidad_entrega] nvarchar(25) NOT NULL DEFAULT N'retiro_local',
    [requiere_instalacion] bit NOT NULL DEFAULT CAST(0 AS bit),
    [direccion_instalacion] nvarchar(255) NULL,
    [costo_instalacion_adicional] decimal(12,2) NOT NULL DEFAULT 0.0,
    [monto_base_trabajo] decimal(12,2) NOT NULL DEFAULT 0.0,
    [monto_total_final] AS CONVERT(DECIMAL(14,2), monto_base_trabajo + costo_instalacion_adicional) PERSISTED,
    [estado_pedido] nvarchar(15) NOT NULL DEFAULT N'aceptado',
    [monto_abonado] int NOT NULL DEFAULT 0,
    [saldo_pendiente] AS CONVERT(INT, monto_base_trabajo + costo_instalacion_adicional) - monto_abonado PERSISTED,
    [estado_pago] AS CONVERT(NVARCHAR(12), CASE WHEN monto_abonado = 0 THEN N'no_pagado' WHEN monto_abonado >= monto_base_trabajo + costo_instalacion_adicional THEN N'pagado_total' ELSE N'abonado' END) PERSISTED,
    [numero_factura] nvarchar(30) NULL,
    [row_version] rowversion NOT NULL,
    CONSTRAINT [pk_pedidos] PRIMARY KEY ([id_pedido]),
    CONSTRAINT [ck_pedidos_costos] CHECK (costo_instalacion_adicional >= 0 AND monto_base_trabajo >= 0),
    CONSTRAINT [ck_pedidos_estado_pedido] CHECK (estado_pedido IN (N'aceptado', N'en_proceso', N'terminado', N'entregado', N'anulado')),
    CONSTRAINT [ck_pedidos_modalidad] CHECK (modalidad_entrega IN (N'retiro_local', N'entrega_domicilio', N'instalacion_terreno')),
    CONSTRAINT [ck_pedidos_monto_abonado] CHECK (monto_abonado >= 0 AND monto_abonado <= monto_base_trabajo + costo_instalacion_adicional),
    CONSTRAINT [ck_pedidos_montos_enteros] CHECK (monto_base_trabajo = FLOOR(monto_base_trabajo) AND costo_instalacion_adicional = FLOOR(costo_instalacion_adicional) AND monto_base_trabajo + costo_instalacion_adicional <= 2000000000),
    CONSTRAINT [ck_pedidos_origen] CHECK (origen IN (N'cotizacion', N'directo')),
    CONSTRAINT [fk_pedidos_cotizacion] FOREIGN KEY ([id_cotizacion]) REFERENCES [cotizaciones] ([id_cotizacion]),
    CONSTRAINT [fk_pedidos_empleado] FOREIGN KEY ([id_empleado_responsable]) REFERENCES [empleados] ([id_empleado]) ON DELETE NO ACTION,
    CONSTRAINT [fk_pedidos_solicitud] FOREIGN KEY ([id_solicitud]) REFERENCES [solicitudes] ([id_solicitud]) ON DELETE NO ACTION
);
GO


CREATE TABLE [pagos] (
    [id_pago] int NOT NULL IDENTITY,
    [id_pedido] int NOT NULL,
    [monto] int NOT NULL,
    [fecha_pago] date NOT NULL DEFAULT (CONVERT(DATE, SYSDATETIME())),
    [medio_pago] nvarchar(15) NOT NULL,
    [observacion] nvarchar(300) NULL,
    [id_empleado_registra] int NOT NULL,
    [fecha_registro] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
    CONSTRAINT [pk_pagos] PRIMARY KEY ([id_pago]),
    CONSTRAINT [ck_pagos_medio_pago] CHECK (medio_pago IN (N'efectivo', N'transferencia', N'tarjeta_debito', N'tarjeta_credito', N'cheque', N'otro')),
    CONSTRAINT [ck_pagos_monto] CHECK (monto > 0),
    CONSTRAINT [fk_pagos_empleado] FOREIGN KEY ([id_empleado_registra]) REFERENCES [empleados] ([id_empleado]) ON DELETE NO ACTION,
    CONSTRAINT [fk_pagos_pedido] FOREIGN KEY ([id_pedido]) REFERENCES [pedidos] ([id_pedido]) ON DELETE NO ACTION
);
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'id_cartel', N'abierto_en_local', N'id_empleado_modifica', N'mensaje_estado', N'tiempo_estimado_regreso') AND [object_id] = OBJECT_ID(N'[cartel_digital]'))
    SET IDENTITY_INSERT [cartel_digital] ON;
INSERT INTO [cartel_digital] ([id_cartel], [abierto_en_local], [id_empleado_modifica], [mensaje_estado], [tiempo_estimado_regreso])
VALUES (1, CAST(1 AS bit), NULL, N'Local abierto y atendiendo', NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'id_cartel', N'abierto_en_local', N'id_empleado_modifica', N'mensaje_estado', N'tiempo_estimado_regreso') AND [object_id] = OBJECT_ID(N'[cartel_digital]'))
    SET IDENTITY_INSERT [cartel_digital] OFF;
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'id_categoria', N'activo', N'descripcion', N'nombre') AND [object_id] = OBJECT_ID(N'[categorias]'))
    SET IDENTITY_INSERT [categorias] ON;
INSERT INTO [categorias] ([id_categoria], [activo], [descripcion], [nombre])
VALUES (1, CAST(1 AS bit), N'Letreros comerciales, luminosos, palomitas, metálicos o en madera', N'Letreros'),
(2, CAST(1 AS bit), N'Rotulación, ploteo y gráficas para vehículos', N'Vehículos'),
(3, CAST(1 AS bit), N'Estampado textil, promocional y serigrafía', N'Estampados'),
(4, CAST(1 AS bit), N'Pintura mural, fachadas y gráfica de gran formato', N'Muros'),
(5, CAST(1 AS bit), N'Trabajos especiales a medida del cliente', N'Personalizado / Otro');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'id_categoria', N'activo', N'descripcion', N'nombre') AND [object_id] = OBJECT_ID(N'[categorias]'))
    SET IDENTITY_INSERT [categorias] OFF;
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'id_horario', N'abierto', N'dia_semana', N'hora_fin', N'hora_inicio', N'nombre_dia') AND [object_id] = OBJECT_ID(N'[horarios_atencion]'))
    SET IDENTITY_INSERT [horarios_atencion] ON;
INSERT INTO [horarios_atencion] ([id_horario], [abierto], [dia_semana], [hora_fin], [hora_inicio], [nombre_dia])
VALUES (1, CAST(1 AS bit), CAST(1 AS tinyint), '18:30:00', '09:00:00', N'Lunes'),
(2, CAST(1 AS bit), CAST(2 AS tinyint), '18:30:00', '09:00:00', N'Martes'),
(3, CAST(1 AS bit), CAST(3 AS tinyint), '18:30:00', '09:00:00', N'Miércoles'),
(4, CAST(1 AS bit), CAST(4 AS tinyint), '18:30:00', '09:00:00', N'Jueves'),
(5, CAST(1 AS bit), CAST(5 AS tinyint), '18:30:00', '09:00:00', N'Viernes'),
(6, CAST(1 AS bit), CAST(6 AS tinyint), '14:00:00', '09:00:00', N'Sábado');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'id_horario', N'abierto', N'dia_semana', N'hora_fin', N'hora_inicio', N'nombre_dia') AND [object_id] = OBJECT_ID(N'[horarios_atencion]'))
    SET IDENTITY_INSERT [horarios_atencion] OFF;
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'id_material', N'activo', N'costo_unitario_aprox', N'nombre', N'unidad_medida') AND [object_id] = OBJECT_ID(N'[materiales]'))
    SET IDENTITY_INSERT [materiales] ON;
INSERT INTO [materiales] ([id_material], [activo], [costo_unitario_aprox], [nombre], [unidad_medida])
VALUES (1, CAST(1 AS bit), 12500.0, N'Madera', N'm2'),
(2, CAST(1 AS bit), 8900.0, N'Pintura', N'litro'),
(3, CAST(1 AS bit), 6500.0, N'Lienzo PVC', N'm2'),
(4, CAST(1 AS bit), 7200.0, N'Vinilo Adhesivo', N'm2'),
(5, CAST(1 AS bit), 18000.0, N'Plancha Metálica', N'm2');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'id_material', N'activo', N'costo_unitario_aprox', N'nombre', N'unidad_medida') AND [object_id] = OBJECT_ID(N'[materiales]'))
    SET IDENTITY_INSERT [materiales] OFF;
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'id_producto', N'activo', N'descripcion', N'id_categoria', N'nombre', N'precio_base_ref') AND [object_id] = OBJECT_ID(N'[productos]'))
    SET IDENTITY_INSERT [productos] ON;
INSERT INTO [productos] ([id_producto], [activo], [descripcion], [id_categoria], [nombre], [precio_base_ref])
VALUES (1, CAST(1 AS bit), N'Estructura metálica con gráfica impresa', 1, N'Cartel metálico impreso', 45000.0),
(2, CAST(1 AS bit), N'Lienzo PVC con ojetillos perimetrales', 1, N'Lienzo publicitario', 25000.0),
(3, CAST(1 AS bit), N'Letrero publicitario doble cara plegable', 1, N'Letrero tipo Palomita', 35000.0),
(4, CAST(1 AS bit), N'Diseño y fabricación según medidas', 1, N'Trabajo personalizado', NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'id_producto', N'activo', N'descripcion', N'id_categoria', N'nombre', N'precio_base_ref') AND [object_id] = OBJECT_ID(N'[productos]'))
    SET IDENTITY_INSERT [productos] OFF;
GO


CREATE INDEX [IX_cartel_digital_id_empleado_modifica] ON [cartel_digital] ([id_empleado_modifica]);
GO


CREATE INDEX [IX_catalogo_id_empleado_creador] ON [catalogo] ([id_empleado_creador]);
GO


CREATE INDEX [IX_catalogo_id_producto] ON [catalogo] ([id_producto]);
GO


CREATE INDEX [ix_catalogo_visible_producto] ON [catalogo] ([visible], [id_producto]);
GO


CREATE UNIQUE INDEX [uq_categorias_nombre] ON [categorias] ([nombre]);
GO


CREATE UNIQUE INDEX [uq_clientes_email] ON [clientes] ([email]) WHERE [email] IS NOT NULL;
GO


CREATE UNIQUE INDEX [uq_clientes_rut] ON [clientes] ([rut]) WHERE [rut] IS NOT NULL;
GO


CREATE UNIQUE INDEX [uq_clientes_telefono] ON [clientes] ([telefono]);
GO


CREATE INDEX [ix_cot_mat_cotizacion] ON [cotizacion_materiales] ([id_cotizacion]);
GO


CREATE INDEX [IX_cotizacion_materiales_id_material] ON [cotizacion_materiales] ([id_material]);
GO


CREATE INDEX [IX_cotizaciones_id_empleado] ON [cotizaciones] ([id_empleado]);
GO


CREATE INDEX [ix_cotizaciones_solicitud] ON [cotizaciones] ([id_solicitud], [estado]);
GO


CREATE UNIQUE INDEX [uq_empleados_email] ON [empleados] ([email]);
GO


CREATE UNIQUE INDEX [uq_empleados_rut] ON [empleados] ([rut]);
GO


CREATE UNIQUE INDEX [uq_horarios_dia] ON [horarios_atencion] ([dia_semana]);
GO


CREATE UNIQUE INDEX [uq_materiales_nombre] ON [materiales] ([nombre]);
GO


CREATE INDEX [IX_pagos_id_empleado_registra] ON [pagos] ([id_empleado_registra]);
GO


CREATE INDEX [ix_pagos_pedido_fecha] ON [pagos] ([id_pedido], [fecha_pago]);
GO


CREATE INDEX [ix_pedidos_calendario_entrega] ON [pedidos] ([fecha_entrega_estimada], [estado_pedido]);
GO


CREATE INDEX [ix_pedidos_cobranza] ON [pedidos] ([estado_pago], [estado_pedido]) INCLUDE ([saldo_pendiente]);
GO


CREATE INDEX [ix_pedidos_fecha_estado] ON [pedidos] ([fecha_pedido], [estado_pedido]);
GO


CREATE INDEX [IX_pedidos_id_cotizacion] ON [pedidos] ([id_cotizacion]);
GO


CREATE INDEX [IX_pedidos_id_empleado_responsable] ON [pedidos] ([id_empleado_responsable]);
GO


CREATE UNIQUE INDEX [uq_pedidos_solicitud] ON [pedidos] ([id_solicitud]);
GO


CREATE INDEX [ix_productos_categoria] ON [productos] ([id_categoria], [activo]);
GO


CREATE UNIQUE INDEX [uq_productos_nombre] ON [productos] ([nombre]);
GO


CREATE UNIQUE INDEX [uq_rostros_empleado_vigente] ON [rostros_empleado] ([id_empleado]) WHERE [fecha_revocacion] IS NULL;
GO


CREATE INDEX [ix_sol_detalles_producto] ON [solicitud_detalles] ([id_producto]);
GO


CREATE INDEX [ix_sol_detalles_solicitud] ON [solicitud_detalles] ([id_solicitud]);
GO


CREATE INDEX [ix_solicitudes_cliente] ON [solicitudes] ([id_cliente], [eliminado_logico]);
GO


CREATE INDEX [IX_solicitudes_id_empleado_registra] ON [solicitudes] ([id_empleado_registra]);
GO


CREATE INDEX [ix_solicitudes_metricas] ON [solicitudes] ([fecha_solicitud], [origen], [estado], [eliminado_logico]);
GO

-- -------------------------------------------------------------------------------------
-- 2. Objetos que el modelo no expresa (migraciones ObjetosProgramablesV6, SepararEstadoPedidoYPago y HistorialDePagos)
-- -------------------------------------------------------------------------------------

-- El cartel digital es una sola fila: su id vale 1 por defecto.
ALTER TABLE dbo.cartel_digital ADD CONSTRAINT df_cartel_id DEFAULT (1) FOR id_cartel;
GO

-- El domingo, cerrado.
INSERT INTO dbo.horarios_atencion (dia_semana, nombre_dia, hora_inicio, hora_fin, abierto)
VALUES (7, N'Domingo', NULL, NULL, 0);
GO

-- Recalcula el costo de materiales de una cotizacion cuando cambian sus lineas.
GO
CREATE OR ALTER TRIGGER dbo.trg_cotizacion_materiales_recalcular_total
ON dbo.cotizacion_materiales
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH cotizaciones_afectadas AS (
        SELECT id_cotizacion FROM inserted
        UNION
        SELECT id_cotizacion FROM deleted
    )
    UPDATE c
       SET costo_materiales_total = ISNULL((
               SELECT SUM(cm.subtotal)
               FROM dbo.cotizacion_materiales cm
               WHERE cm.id_cotizacion = c.id_cotizacion
           ), 0)
      FROM dbo.cotizaciones c
      JOIN cotizaciones_afectadas ca ON ca.id_cotizacion = c.id_cotizacion;
END
GO

-- Mantiene pedidos.monto_abonado igual a la suma de los pagos del pedido.
GO
CREATE OR ALTER TRIGGER dbo.trg_pagos_recalcular_monto_abonado
ON dbo.pagos
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH cambios AS (
        SELECT id_pedido, monto FROM inserted
        UNION ALL
        SELECT id_pedido, -monto FROM deleted
    ),
    diferencias AS (
        SELECT id_pedido, SUM(monto) AS diferencia
        FROM cambios
        GROUP BY id_pedido
    )
    UPDATE p
       SET monto_abonado = p.monto_abonado + d.diferencia
      FROM dbo.pedidos p
      JOIN diferencias d ON d.id_pedido = p.id_pedido
     WHERE d.diferencia <> 0;
END
GO

-- Conversion de solicitudes en linea en pedidos no anulados, por anio y mes.
GO
CREATE OR ALTER VIEW dbo.vw_metricas_conversion_online
AS
SELECT
    YEAR(s.fecha_solicitud)  AS anio,
    MONTH(s.fecha_solicitud) AS mes,
    COUNT(s.id_solicitud)    AS total_solicitudes_online,
    COUNT(p.id_pedido)       AS solicitudes_concretadas_en_pedido,
    CAST(
        CASE WHEN COUNT(s.id_solicitud) = 0 THEN 0
             ELSE (COUNT(p.id_pedido) * 100.0) / COUNT(s.id_solicitud)
        END AS DECIMAL(5,2)
    ) AS porcentaje_conversion
FROM dbo.solicitudes s
LEFT JOIN dbo.pedidos p ON p.id_solicitud = s.id_solicitud AND p.estado_pedido <> N'anulado'
WHERE s.origen = N'online'
GROUP BY YEAR(s.fecha_solicitud), MONTH(s.fecha_solicitud);
GO

-- Depura solicitudes rechazadas o eliminadas sin pedido, con mas de N meses (RNFO04).
GO
CREATE OR ALTER PROCEDURE dbo.sp_depurar_solicitudes_no_concretadas
    @meses_antiguedad INT = 12
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DELETE s
    FROM dbo.solicitudes s
    LEFT JOIN dbo.pedidos p ON p.id_solicitud = s.id_solicitud
    WHERE p.id_pedido IS NULL
      AND (s.eliminado_logico = 1 OR s.estado = N'rechazado')
      AND s.fecha_solicitud < DATEADD(MONTH, -@meses_antiguedad, SYSDATETIME());
END
GO

-- -------------------------------------------------------------------------------------
-- 3. Historial de migraciones: la API reconoce la base como actualizada.
-- -------------------------------------------------------------------------------------
CREATE TABLE [__EFMigrationsHistory] (
    [MigrationId] nvarchar(150) NOT NULL,
    [ProductVersion] nvarchar(32) NOT NULL,
    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES
    (N'20261106031123_InitialCreateV6', N'10.0.11'),
    (N'20261106031126_ObjetosProgramablesV6', N'10.0.11'),
    (N'20261106094459_AgregarRostrosEmpleado', N'10.0.11'),
    (N'20261112224624_SepararEstadoPedidoYPago', N'10.0.11'),
    (N'20261113010201_HistorialDePagos', N'10.0.11');
GO

SET NOEXEC OFF;
GO
