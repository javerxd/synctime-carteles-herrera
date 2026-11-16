-- =====================================================================================
-- SyncTime - Letreros Herrera (la base conserva el nombre LetreriaLosHerreraDB)
-- Script completo de la base de datos: tablas, restricciones, índices, datos iniciales,
-- triggers, vista y procedimiento almacenado. Estado: migración NotificacionesYAuditoria.
-- Generado el 15-11-2026 desde el modelo de EF Core, que es la fuente única del esquema.
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
-- Fase 4 (13-11-2026), tablas nuevas:
--   * correos_salientes: cola de salida del correo (avisos al taller), con reintentos.
--   * consentimientos:   quién aceptó qué documento, en qué versión y cuándo, y su revocación.
--   * imagenes:          imágenes subidas por el taller (los archivos WebP quedan fuera de la base).
--
-- Fases 5 a 7 (13 y 14-11-2026): catálogo con autorización de logo y consultas de contacto
--   (PortadaPublica); tokens de renovación y códigos de cuenta (CuentasYSesion); adjuntos e
--   historial de solicitudes, y huella de IP (SolicitudesYAdjuntos).
--
-- Fase 8 (14-11-2026), migración CotizacionesYPedidos:
--   * cotizaciones:      versión, respuesta (fecha, vía, quién, motivo), borrado lógico y una sola
--                        pendiente por solicitud (índice único filtrado). Montos en pesos enteros.
--   * pedidos:           motivo y fecha de anulación, borrado lógico y un pedido por cotización.
--   * pedidos_historial: (tabla nueva) cada cambio de estado del pedido, con quién y cuándo.
--   * pagos:             un abono mal ingresado se anula con motivo; el trigger suma solo los no
--                        anulados y no deja reactivar uno anulado.
--
-- Fase 9 (15-11-2026), migración MantenedoresYAvisos (sección 2b):
--   * row_version en categorias, productos, catalogo, materiales, empleados y clientes.
--   * horarios_atencion: quién y cuándo, y un día cerrado no guarda horas (ck_horarios_cerrado).
--   * disponibilidad_empleados: (tabla nueva) franjas semanales de atención del personal.
--   * destinatarios_avisos e historial: (tablas nuevas) a qué correos llegan los avisos del taller.
--   * Descripciones de categorías (300) y productos (500), y regreso estimado del cartel (100), más largos.
--
-- Fase 10 (15-11-2026), migración RostroConPruebaDeVida (sección 2c):
--   * rostros_empleado: tres muestras cifradas (descriptor_frente, descriptor_izquierda y
--                       descriptor_derecha), quién inició el registro y el formato de las muestras (2 =
--                       tres muestras sin el lado; 3 = cada una atada a su lado, sección 2d). descriptor_cifrado, el promedio antiguo, deja de usarse.
--   * desafios_rostro:  (tabla nueva) desafíos de la prueba de vida: solo hashes del token y del email,
--                       el orden de los giros y su vigencia. La purga diaria borra los de más de un día.
--   * calibracion_rostro: (tabla nueva) mediciones de la calibración del umbral: solo distancias y giros,
--                       sin datos biométricos.
--
-- Fase 10, seguimiento (15-11-2026), migración RostroMuestrasPorLado (sección 2d):
--   * ck_rostros_empleado_muestras admite formato_muestras 3: cada muestra cifrada con su lado (frente,
--                       izquierda o derecha) en los datos asociados. Las filas de formato 2 se cifraron sin el
--                       lado y la API las trata como «debe registrarse de nuevo».
--
-- Fase 11 (15-11-2026), migración NotificacionesYAuditoria (sección 2e):
--   * auditoria:          (tabla nueva) bitácora inmutable: el trigger trg_auditoria_inmutable rechaza UPDATE y DELETE.
--   * claves_idempotencia: (tabla nueva) Idempotency-Key de las rutas que crean; duran 24 horas.
--   * destinatarios_avisos: recibe_en_linea y recibe_presenciales (al menos uno) y row_version.
--   * clientes:           recibe_avisos_estado (avisos de estado opcionales) e inhabilitado_en (baja de la cuenta).
--   * correos_salientes:  enlace_baja (List-Unsubscribe) y el reenvío de los fallidos (revisado, reenviado_como,
--                         id_empleado_reenvia, fecha_reenvio); índice filtrado de los fallidos.
--   * row_version en horarios_atencion y cartel_digital; consentimientos admite avisos_estado.
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
    [fecha_verificacion_correo] datetimeoffset(0) NULL,
    CONSTRAINT [pk_clientes] PRIMARY KEY ([id_cliente]),
    CONSTRAINT [ck_clientes_cuenta_con_correo] CHECK (password_hash IS NULL OR email IS NOT NULL),
    CONSTRAINT [ck_clientes_email] CHECK (email IS NULL OR (email LIKE N'%_@_%._%' AND email NOT LIKE N'% %'))
);
GO


CREATE TABLE [consultas_contacto] (
    [id_consulta] int NOT NULL IDENTITY,
    [nombre] nvarchar(100) NOT NULL,
    [correo] nvarchar(254) NOT NULL,
    [telefono] nvarchar(20) NULL,
    [asunto] nvarchar(150) NOT NULL,
    [mensaje] nvarchar(2000) NOT NULL,
    [fecha_recepcion] datetimeoffset(0) NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
    [revisada] bit NOT NULL DEFAULT CAST(0 AS bit),
    [fecha_revision] datetimeoffset(0) NULL,
    CONSTRAINT [pk_consultas_contacto] PRIMARY KEY ([id_consulta])
);
GO


CREATE TABLE [correos_salientes] (
    [id_correo] bigint NOT NULL IDENTITY,
    [tipo] varchar(40) NOT NULL,
    [referencia] varchar(64) NULL,
    [destinatarios] nvarchar(1000) NOT NULL,
    [responder_a] nvarchar(254) NULL,
    [asunto] nvarchar(200) NOT NULL,
    [cuerpo_html] nvarchar(max) NOT NULL,
    [cuerpo_texto] nvarchar(max) NOT NULL,
    [estado] varchar(10) NOT NULL DEFAULT 'pendiente',
    [intentos] int NOT NULL,
    [proximo_intento] datetimeoffset(0) NOT NULL,
    [bloqueado_hasta] datetimeoffset(0) NULL,
    [ultimo_error] nvarchar(200) NULL,
    [fecha_creacion] datetimeoffset(0) NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
    [fecha_envio] datetimeoffset(0) NULL,
    CONSTRAINT [pk_correos_salientes] PRIMARY KEY ([id_correo]),
    CONSTRAINT [ck_correos_salientes_estado] CHECK (estado IN ('pendiente', 'enviando', 'enviado', 'fallido')),
    CONSTRAINT [ck_correos_salientes_intentos] CHECK (intentos >= 0)
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


CREATE TABLE [codigos_cuenta] (
    [id_codigo] bigint NOT NULL IDENTITY,
    [id_cliente] int NULL,
    [id_empleado] int NULL,
    [proposito] varchar(20) NOT NULL,
    [hash_codigo] char(64) NOT NULL,
    [correo] nvarchar(254) NOT NULL,
    [fecha_creacion] datetimeoffset(0) NOT NULL,
    [fecha_expiracion] datetimeoffset(0) NOT NULL,
    [fecha_uso] datetimeoffset(0) NULL,
    [intentos] int NOT NULL DEFAULT 0,
    CONSTRAINT [pk_codigos_cuenta] PRIMARY KEY ([id_codigo]),
    CONSTRAINT [ck_codigos_cuenta_intentos] CHECK (intentos >= 0),
    CONSTRAINT [ck_codigos_cuenta_proposito] CHECK (proposito IN ('verificacion_correo', 'recuperacion')),
    CONSTRAINT [ck_codigos_cuenta_titular] CHECK ((CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT [fk_codigos_cuenta_cliente] FOREIGN KEY ([id_cliente]) REFERENCES [clientes] ([id_cliente]) ON DELETE CASCADE,
    CONSTRAINT [fk_codigos_cuenta_empleado] FOREIGN KEY ([id_empleado]) REFERENCES [empleados] ([id_empleado]) ON DELETE CASCADE
);
GO


CREATE TABLE [consentimientos] (
    [id_consentimiento] int NOT NULL IDENTITY,
    [id_cliente] int NULL,
    [id_empleado] int NULL,
    [referencia_envio] varchar(64) NULL,
    [tipo] varchar(20) NOT NULL,
    [version_documento] varchar(20) NOT NULL,
    [fecha_aceptacion] datetimeoffset(0) NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
    [fecha_revocacion] datetimeoffset(0) NULL,
    CONSTRAINT [pk_consentimientos] PRIMARY KEY ([id_consentimiento]),
    CONSTRAINT [ck_consentimientos_revocacion] CHECK (fecha_revocacion IS NULL OR fecha_revocacion >= fecha_aceptacion),
    CONSTRAINT [ck_consentimientos_tipo] CHECK (tipo IN ('privacidad', 'terminos', 'marketing', 'cookies')),
    CONSTRAINT [ck_consentimientos_titular] CHECK ((CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) + (CASE WHEN referencia_envio IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT [fk_consentimientos_cliente] FOREIGN KEY ([id_cliente]) REFERENCES [clientes] ([id_cliente]),
    CONSTRAINT [fk_consentimientos_empleado] FOREIGN KEY ([id_empleado]) REFERENCES [empleados] ([id_empleado])
);
GO


CREATE TABLE [imagenes] (
    [id_imagen] int NOT NULL IDENTITY,
    [clave] char(32) NOT NULL,
    [ancho] int NOT NULL,
    [alto] int NOT NULL,
    [bytes_grande] int NOT NULL,
    [bytes_miniatura] int NOT NULL,
    [es_ejemplo] bit NOT NULL DEFAULT CAST(0 AS bit),
    [id_empleado_sube] int NOT NULL,
    [fecha_subida] datetimeoffset(0) NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
    CONSTRAINT [pk_imagenes] PRIMARY KEY ([id_imagen]),
    CONSTRAINT [ck_imagenes_medidas] CHECK (ancho > 0 AND alto > 0 AND bytes_grande > 0 AND bytes_miniatura > 0),
    CONSTRAINT [fk_imagenes_empleado] FOREIGN KEY ([id_empleado_sube]) REFERENCES [empleados] ([id_empleado])
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
    [descripcion_general] nvarchar(2000) NULL,
    [fecha_solicitud] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
    [estado] nvarchar(15) NOT NULL DEFAULT N'solicitado',
    [motivo_rechazo] nvarchar(500) NULL,
    [eliminado_logico] bit NOT NULL DEFAULT CAST(0 AS bit),
    [fecha_eliminacion_logica] datetime2(0) NULL,
    [row_version] rowversion NOT NULL,
    [huella_ip] char(64) NULL,
    CONSTRAINT [pk_solicitudes] PRIMARY KEY ([id_solicitud]),
    CONSTRAINT [ck_solicitudes_estado] CHECK (estado IN (N'solicitado', N'cotizado', N'aceptado', N'rechazado')),
    CONSTRAINT [fk_solicitudes_cliente] FOREIGN KEY ([id_cliente]) REFERENCES [clientes] ([id_cliente]) ON DELETE NO ACTION,
    CONSTRAINT [fk_solicitudes_empleado] FOREIGN KEY ([id_empleado_registra]) REFERENCES [empleados] ([id_empleado])
);
GO


CREATE TABLE [tokens_renovacion] (
    [id_token] bigint NOT NULL IDENTITY,
    [id_cliente] int NULL,
    [id_empleado] int NULL,
    [hash_token] char(64) NOT NULL,
    [familia] uniqueidentifier NOT NULL,
    [fecha_creacion] datetimeoffset(0) NOT NULL,
    [fecha_expiracion] datetimeoffset(0) NOT NULL,
    [fecha_uso] datetimeoffset(3) NULL,
    [fecha_revocacion] datetimeoffset(0) NULL,
    CONSTRAINT [pk_tokens_renovacion] PRIMARY KEY ([id_token]),
    CONSTRAINT [ck_tokens_renovacion_titular] CHECK ((CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT [fk_tokens_renovacion_cliente] FOREIGN KEY ([id_cliente]) REFERENCES [clientes] ([id_cliente]) ON DELETE CASCADE,
    CONSTRAINT [fk_tokens_renovacion_empleado] FOREIGN KEY ([id_empleado]) REFERENCES [empleados] ([id_empleado]) ON DELETE CASCADE
);
GO


CREATE TABLE [catalogo] (
    [id_catalogo] int NOT NULL IDENTITY,
    [id_producto] int NOT NULL,
    [id_empleado_creador] int NULL,
    [titulo] nvarchar(120) NOT NULL,
    [descripcion] nvarchar(500) NOT NULL,
    [imagen_url] nvarchar(500) NULL,
    [id_imagen] int NULL,
    [ancho_mm] int NULL,
    [alto_mm] int NULL,
    [precio_referencial] decimal(12,2) NULL,
    [fecha_publicacion] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
    [visible] bit NOT NULL DEFAULT CAST(1 AS bit),
    [incluye_logo_cliente] bit NOT NULL DEFAULT CAST(0 AS bit),
    [id_empleado_autoriza_logo] int NULL,
    [fecha_autorizacion_logo] datetimeoffset(0) NULL,
    CONSTRAINT [pk_catalogo] PRIMARY KEY ([id_catalogo]),
    CONSTRAINT [ck_catalogo_autorizacion_logo] CHECK ((id_empleado_autoriza_logo IS NULL AND fecha_autorizacion_logo IS NULL) OR (id_empleado_autoriza_logo IS NOT NULL AND fecha_autorizacion_logo IS NOT NULL)),
    CONSTRAINT [ck_catalogo_logo] CHECK (visible = 0 OR incluye_logo_cliente = 0 OR fecha_autorizacion_logo IS NOT NULL),
    CONSTRAINT [ck_catalogo_medidas] CHECK ((ancho_mm IS NULL AND alto_mm IS NULL) OR (ancho_mm > 0 AND alto_mm > 0)),
    CONSTRAINT [ck_catalogo_precio] CHECK (precio_referencial IS NULL OR precio_referencial >= 0),
    CONSTRAINT [fk_catalogo_empleado] FOREIGN KEY ([id_empleado_creador]) REFERENCES [empleados] ([id_empleado]),
    CONSTRAINT [fk_catalogo_empleado_autoriza_logo] FOREIGN KEY ([id_empleado_autoriza_logo]) REFERENCES [empleados] ([id_empleado]),
    CONSTRAINT [fk_catalogo_imagen] FOREIGN KEY ([id_imagen]) REFERENCES [imagenes] ([id_imagen]),
    CONSTRAINT [fk_catalogo_producto] FOREIGN KEY ([id_producto]) REFERENCES [productos] ([id_producto]) ON DELETE NO ACTION
);
GO


CREATE TABLE [adjuntos_solicitud] (
    [id_adjunto] int NOT NULL IDENTITY,
    [id_solicitud] int NULL,
    [id_cliente] int NULL,
    [id_empleado] int NULL,
    [clave] char(32) NOT NULL,
    [tipo] varchar(10) NOT NULL,
    [nombre_original] nvarchar(120) NOT NULL,
    [bytes] int NOT NULL,
    [ancho] int NULL,
    [alto] int NULL,
    [fecha_subida] datetimeoffset(0) NOT NULL,
    [fecha_vinculo] datetimeoffset(0) NULL,
    CONSTRAINT [pk_adjuntos_solicitud] PRIMARY KEY ([id_adjunto]),
    CONSTRAINT [ck_adjuntos_solicitud_autor] CHECK ((CASE WHEN id_cliente IS NULL THEN 0 ELSE 1 END) + (CASE WHEN id_empleado IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT [ck_adjuntos_solicitud_bytes] CHECK (bytes > 0),
    CONSTRAINT [ck_adjuntos_solicitud_medidas] CHECK ((tipo = 'imagen' AND ancho > 0 AND alto > 0) OR (tipo = 'pdf' AND ancho IS NULL AND alto IS NULL)),
    CONSTRAINT [ck_adjuntos_solicitud_tipo] CHECK (tipo IN ('imagen', 'pdf')),
    CONSTRAINT [ck_adjuntos_solicitud_vinculo] CHECK ((id_solicitud IS NULL AND fecha_vinculo IS NULL) OR (id_solicitud IS NOT NULL AND fecha_vinculo IS NOT NULL)),
    CONSTRAINT [fk_adjuntos_solicitud_cliente] FOREIGN KEY ([id_cliente]) REFERENCES [clientes] ([id_cliente]),
    CONSTRAINT [fk_adjuntos_solicitud_empleado] FOREIGN KEY ([id_empleado]) REFERENCES [empleados] ([id_empleado]),
    CONSTRAINT [fk_adjuntos_solicitud_solicitud] FOREIGN KEY ([id_solicitud]) REFERENCES [solicitudes] ([id_solicitud]) ON DELETE CASCADE
);
GO


CREATE TABLE [cotizaciones] (
    [id_cotizacion] int NOT NULL IDENTITY,
    [id_solicitud] int NOT NULL,
    [id_empleado] int NOT NULL,
    [fecha_cotizacion] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
    [observaciones] nvarchar(2000) NULL,
    [costo_materiales_total] decimal(12,2) NOT NULL DEFAULT 0.0,
    [costo_mano_obra] decimal(12,2) NOT NULL DEFAULT 0.0,
    [otros_costos] decimal(12,2) NOT NULL DEFAULT 0.0,
    [total_cotizacion] AS CONVERT(DECIMAL(14,2), costo_materiales_total + costo_mano_obra + otros_costos) PERSISTED,
    [estado] nvarchar(15) NOT NULL DEFAULT N'pendiente',
    [row_version] rowversion NOT NULL,
    [fecha_respuesta] datetimeoffset(0) NULL,
    [via_respuesta] nvarchar(15) NULL,
    [id_empleado_respuesta] int NULL,
    [motivo_rechazo] nvarchar(500) NULL,
    [eliminado_logico] bit NOT NULL DEFAULT CAST(0 AS bit),
    [fecha_eliminacion_logica] datetimeoffset(0) NULL,
    CONSTRAINT [pk_cotizaciones] PRIMARY KEY ([id_cotizacion]),
    CONSTRAINT [ck_cotizaciones_costos] CHECK (costo_materiales_total >= 0 AND costo_mano_obra >= 0 AND otros_costos >= 0),
    CONSTRAINT [ck_cotizaciones_estado] CHECK (estado IN (N'pendiente', N'aprobada', N'rechazada')),
    CONSTRAINT [ck_cotizaciones_montos_enteros] CHECK (costo_mano_obra = FLOOR(costo_mano_obra) AND otros_costos = FLOOR(otros_costos)),
    CONSTRAINT [ck_cotizaciones_respuesta] CHECK ((CASE WHEN estado = N'pendiente' THEN 1 ELSE 0 END) = (CASE WHEN fecha_respuesta IS NULL THEN 1 ELSE 0 END) AND (CASE WHEN via_respuesta IS NULL THEN 0 ELSE 1 END) = (CASE WHEN fecha_respuesta IS NULL THEN 0 ELSE 1 END)),
    CONSTRAINT [ck_cotizaciones_via] CHECK (via_respuesta IS NULL OR via_respuesta IN (N'cliente_web', N'telefono', N'whatsapp', N'presencial')),
    CONSTRAINT [fk_cotizaciones_empleado] FOREIGN KEY ([id_empleado]) REFERENCES [empleados] ([id_empleado]) ON DELETE NO ACTION,
    CONSTRAINT [fk_cotizaciones_empleado_respuesta] FOREIGN KEY ([id_empleado_respuesta]) REFERENCES [empleados] ([id_empleado]),
    CONSTRAINT [fk_cotizaciones_solicitud] FOREIGN KEY ([id_solicitud]) REFERENCES [solicitudes] ([id_solicitud]) ON DELETE CASCADE
);
GO


CREATE TABLE [solicitud_detalles] (
    [id_solicitud_detalle] int NOT NULL IDENTITY,
    [id_solicitud] int NOT NULL,
    [id_producto] int NOT NULL,
    [descripcion] nvarchar(500) NOT NULL,
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


CREATE TABLE [solicitudes_historial] (
    [id_historial] int NOT NULL IDENTITY,
    [id_solicitud] int NOT NULL,
    [estado_anterior] nvarchar(15) NULL,
    [estado_nuevo] nvarchar(15) NOT NULL,
    [motivo] nvarchar(500) NULL,
    [id_empleado] int NULL,
    [id_cliente] int NULL,
    [fecha] datetimeoffset(0) NOT NULL,
    CONSTRAINT [pk_solicitudes_historial] PRIMARY KEY ([id_historial]),
    CONSTRAINT [ck_solicitudes_historial_actor] CHECK (id_cliente IS NULL OR id_empleado IS NULL),
    CONSTRAINT [ck_solicitudes_historial_estados] CHECK (estado_nuevo IN (N'solicitado', N'cotizado', N'aceptado', N'rechazado') AND (estado_anterior IS NULL OR estado_anterior IN (N'solicitado', N'cotizado', N'aceptado', N'rechazado'))),
    CONSTRAINT [fk_solicitudes_historial_cliente] FOREIGN KEY ([id_cliente]) REFERENCES [clientes] ([id_cliente]),
    CONSTRAINT [fk_solicitudes_historial_empleado] FOREIGN KEY ([id_empleado]) REFERENCES [empleados] ([id_empleado]),
    CONSTRAINT [fk_solicitudes_historial_solicitud] FOREIGN KEY ([id_solicitud]) REFERENCES [solicitudes] ([id_solicitud]) ON DELETE CASCADE
);
GO


CREATE TABLE [cotizacion_materiales] (
    [id_cotizacion_material] int NOT NULL IDENTITY,
    [id_cotizacion] int NOT NULL,
    [id_material] int NOT NULL,
    [medida_aplicada] nvarchar(100) NULL,
    [cantidad] decimal(10,2) NOT NULL,
    [costo_unitario] decimal(12,2) NOT NULL,
    [subtotal] AS CONVERT(DECIMAL(14,2), cantidad * costo_unitario) PERSISTED,
    CONSTRAINT [pk_cotizacion_materiales] PRIMARY KEY ([id_cotizacion_material]),
    CONSTRAINT [ck_cot_mat_cantidad] CHECK (cantidad > 0),
    CONSTRAINT [ck_cot_mat_costo] CHECK (costo_unitario >= 0),
    CONSTRAINT [ck_cot_mat_costo_entero] CHECK (costo_unitario = FLOOR(costo_unitario)),
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
    [direccion_instalacion] nvarchar(300) NULL,
    [costo_instalacion_adicional] decimal(12,2) NOT NULL DEFAULT 0.0,
    [monto_base_trabajo] decimal(12,2) NOT NULL DEFAULT 0.0,
    [monto_total_final] AS CONVERT(DECIMAL(14,2), monto_base_trabajo + costo_instalacion_adicional) PERSISTED,
    [estado_pedido] nvarchar(15) NOT NULL DEFAULT N'aceptado',
    [monto_abonado] int NOT NULL DEFAULT 0,
    [saldo_pendiente] AS CONVERT(INT, monto_base_trabajo + costo_instalacion_adicional) - monto_abonado PERSISTED,
    [estado_pago] AS CONVERT(NVARCHAR(12), CASE WHEN monto_abonado = 0 THEN N'no_pagado' WHEN monto_abonado >= monto_base_trabajo + costo_instalacion_adicional THEN N'pagado_total' ELSE N'abonado' END) PERSISTED,
    [numero_factura] nvarchar(30) NULL,
    [row_version] rowversion NOT NULL,
    [motivo_anulacion] nvarchar(500) NULL,
    [fecha_anulacion] datetimeoffset(0) NULL,
    [eliminado_logico] bit NOT NULL DEFAULT CAST(0 AS bit),
    [fecha_eliminacion_logica] datetimeoffset(0) NULL,
    CONSTRAINT [pk_pedidos] PRIMARY KEY ([id_pedido]),
    CONSTRAINT [ck_pedidos_anulacion] CHECK ((estado_pedido = N'anulado' AND motivo_anulacion IS NOT NULL AND fecha_anulacion IS NOT NULL) OR (estado_pedido <> N'anulado' AND motivo_anulacion IS NULL AND fecha_anulacion IS NULL)),
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
    [anulado] bit NOT NULL DEFAULT CAST(0 AS bit),
    [motivo_anulacion] nvarchar(300) NULL,
    [id_empleado_anula] int NULL,
    [fecha_anulacion] datetimeoffset(0) NULL,
    CONSTRAINT [pk_pagos] PRIMARY KEY ([id_pago]),
    CONSTRAINT [ck_pagos_anulacion] CHECK ((anulado = 1 AND motivo_anulacion IS NOT NULL AND id_empleado_anula IS NOT NULL AND fecha_anulacion IS NOT NULL) OR (anulado = 0 AND motivo_anulacion IS NULL AND id_empleado_anula IS NULL AND fecha_anulacion IS NULL)),
    CONSTRAINT [ck_pagos_medio_pago] CHECK (medio_pago IN (N'efectivo', N'transferencia', N'tarjeta_debito', N'tarjeta_credito', N'cheque', N'otro')),
    CONSTRAINT [ck_pagos_monto] CHECK (monto > 0),
    CONSTRAINT [fk_pagos_empleado] FOREIGN KEY ([id_empleado_registra]) REFERENCES [empleados] ([id_empleado]) ON DELETE NO ACTION,
    CONSTRAINT [fk_pagos_empleado_anula] FOREIGN KEY ([id_empleado_anula]) REFERENCES [empleados] ([id_empleado]),
    CONSTRAINT [fk_pagos_pedido] FOREIGN KEY ([id_pedido]) REFERENCES [pedidos] ([id_pedido]) ON DELETE NO ACTION
);
GO


CREATE TABLE [pedidos_historial] (
    [id_historial] int NOT NULL IDENTITY,
    [id_pedido] int NOT NULL,
    [estado_anterior] nvarchar(15) NULL,
    [estado_nuevo] nvarchar(15) NOT NULL,
    [nota] nvarchar(500) NULL,
    [id_empleado] int NOT NULL,
    [fecha] datetimeoffset(0) NOT NULL,
    CONSTRAINT [pk_pedidos_historial] PRIMARY KEY ([id_historial]),
    CONSTRAINT [ck_pedidos_historial_estados] CHECK (estado_nuevo IN (N'aceptado', N'en_proceso', N'terminado', N'entregado', N'anulado') AND (estado_anterior IS NULL OR estado_anterior IN (N'aceptado', N'en_proceso', N'terminado', N'entregado', N'anulado'))),
    CONSTRAINT [fk_pedidos_historial_empleado] FOREIGN KEY ([id_empleado]) REFERENCES [empleados] ([id_empleado]),
    CONSTRAINT [fk_pedidos_historial_pedido] FOREIGN KEY ([id_pedido]) REFERENCES [pedidos] ([id_pedido])
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


CREATE INDEX [ix_adjuntos_solicitud_borrador] ON [adjuntos_solicitud] ([id_cliente], [fecha_subida]) WHERE [id_solicitud] IS NULL;
GO


CREATE INDEX [IX_adjuntos_solicitud_id_empleado] ON [adjuntos_solicitud] ([id_empleado]);
GO


CREATE INDEX [ix_adjuntos_solicitud_solicitud] ON [adjuntos_solicitud] ([id_solicitud]) WHERE [id_solicitud] IS NOT NULL;
GO


CREATE UNIQUE INDEX [uq_adjuntos_solicitud_clave] ON [adjuntos_solicitud] ([clave]);
GO


CREATE INDEX [IX_cartel_digital_id_empleado_modifica] ON [cartel_digital] ([id_empleado_modifica]);
GO


CREATE INDEX [ix_catalogo_autoriza_logo] ON [catalogo] ([id_empleado_autoriza_logo]);
GO


CREATE INDEX [IX_catalogo_id_empleado_creador] ON [catalogo] ([id_empleado_creador]);
GO


CREATE INDEX [IX_catalogo_id_producto] ON [catalogo] ([id_producto]);
GO


CREATE INDEX [ix_catalogo_imagen] ON [catalogo] ([id_imagen]);
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


CREATE INDEX [ix_codigos_cuenta_cliente] ON [codigos_cuenta] ([id_cliente], [proposito]) WHERE [id_cliente] IS NOT NULL;
GO


CREATE INDEX [ix_codigos_cuenta_empleado] ON [codigos_cuenta] ([id_empleado], [proposito]) WHERE [id_empleado] IS NOT NULL;
GO


CREATE INDEX [ix_codigos_cuenta_expiracion] ON [codigos_cuenta] ([fecha_expiracion]);
GO


CREATE INDEX [ix_codigos_cuenta_hash] ON [codigos_cuenta] ([hash_codigo]);
GO


CREATE INDEX [ix_consentimientos_cliente] ON [consentimientos] ([id_cliente], [tipo]) WHERE [id_cliente] IS NOT NULL;
GO


CREATE INDEX [ix_consentimientos_empleado] ON [consentimientos] ([id_empleado], [tipo]) WHERE [id_empleado] IS NOT NULL;
GO


CREATE INDEX [ix_consentimientos_envio] ON [consentimientos] ([referencia_envio], [tipo]) WHERE [referencia_envio] IS NOT NULL;
GO


CREATE UNIQUE INDEX [uq_consentimientos_cliente_vigente] ON [consentimientos] ([id_cliente], [tipo], [version_documento]) WHERE [id_cliente] IS NOT NULL AND [fecha_revocacion] IS NULL;
GO


CREATE UNIQUE INDEX [uq_consentimientos_empleado_vigente] ON [consentimientos] ([id_empleado], [tipo], [version_documento]) WHERE [id_empleado] IS NOT NULL AND [fecha_revocacion] IS NULL;
GO


CREATE UNIQUE INDEX [uq_consentimientos_envio_vigente] ON [consentimientos] ([referencia_envio], [tipo], [version_documento]) WHERE [referencia_envio] IS NOT NULL AND [fecha_revocacion] IS NULL;
GO


CREATE INDEX [ix_consultas_contacto_bandeja] ON [consultas_contacto] ([revisada], [fecha_recepcion]);
GO


CREATE INDEX [ix_correos_salientes_fecha] ON [correos_salientes] ([fecha_creacion]);
GO


CREATE INDEX [ix_correos_salientes_pendientes] ON [correos_salientes] ([estado], [proximo_intento]) WHERE [estado] IN ('pendiente', 'enviando');
GO


CREATE INDEX [ix_correos_salientes_referencia] ON [correos_salientes] ([referencia]) WHERE [referencia] IS NOT NULL;
GO


CREATE INDEX [ix_cot_mat_cotizacion] ON [cotizacion_materiales] ([id_cotizacion]);
GO


CREATE INDEX [IX_cotizacion_materiales_id_material] ON [cotizacion_materiales] ([id_material]);
GO


CREATE INDEX [ix_cotizaciones_empleado_respuesta] ON [cotizaciones] ([id_empleado_respuesta]);
GO


CREATE INDEX [IX_cotizaciones_id_empleado] ON [cotizaciones] ([id_empleado]);
GO


CREATE INDEX [ix_cotizaciones_solicitud] ON [cotizaciones] ([id_solicitud], [estado]);
GO


CREATE UNIQUE INDEX [uq_cotizaciones_una_pendiente] ON [cotizaciones] ([id_solicitud]) WHERE [estado] = N'pendiente' AND [eliminado_logico] = 0;
GO


CREATE UNIQUE INDEX [uq_empleados_email] ON [empleados] ([email]);
GO


CREATE UNIQUE INDEX [uq_empleados_rut] ON [empleados] ([rut]);
GO


CREATE UNIQUE INDEX [uq_horarios_dia] ON [horarios_atencion] ([dia_semana]);
GO


CREATE INDEX [ix_imagenes_empleado] ON [imagenes] ([id_empleado_sube]);
GO


CREATE UNIQUE INDEX [uq_imagenes_clave] ON [imagenes] ([clave]);
GO


CREATE UNIQUE INDEX [uq_materiales_nombre] ON [materiales] ([nombre]);
GO


CREATE INDEX [ix_pagos_empleado_anula] ON [pagos] ([id_empleado_anula]);
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


CREATE INDEX [IX_pedidos_id_empleado_responsable] ON [pedidos] ([id_empleado_responsable]);
GO


CREATE UNIQUE INDEX [uq_pedidos_cotizacion] ON [pedidos] ([id_cotizacion]) WHERE [id_cotizacion] IS NOT NULL;
GO


CREATE UNIQUE INDEX [uq_pedidos_solicitud] ON [pedidos] ([id_solicitud]);
GO


CREATE INDEX [ix_pedidos_historial_empleado] ON [pedidos_historial] ([id_empleado]);
GO


CREATE INDEX [ix_pedidos_historial_pedido] ON [pedidos_historial] ([id_pedido], [fecha]);
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


CREATE INDEX [ix_solicitudes_huella_ip] ON [solicitudes] ([huella_ip], [fecha_solicitud]) WHERE [huella_ip] IS NOT NULL;
GO


CREATE INDEX [IX_solicitudes_id_empleado_registra] ON [solicitudes] ([id_empleado_registra]);
GO


CREATE INDEX [ix_solicitudes_metricas] ON [solicitudes] ([fecha_solicitud], [origen], [estado], [eliminado_logico]);
GO


CREATE INDEX [IX_solicitudes_historial_id_cliente] ON [solicitudes_historial] ([id_cliente]);
GO


CREATE INDEX [IX_solicitudes_historial_id_empleado] ON [solicitudes_historial] ([id_empleado]);
GO


CREATE INDEX [ix_solicitudes_historial_solicitud] ON [solicitudes_historial] ([id_solicitud], [fecha]);
GO


CREATE INDEX [ix_tokens_renovacion_cliente] ON [tokens_renovacion] ([id_cliente]) WHERE [id_cliente] IS NOT NULL;
GO


CREATE INDEX [ix_tokens_renovacion_empleado] ON [tokens_renovacion] ([id_empleado]) WHERE [id_empleado] IS NOT NULL;
GO


CREATE INDEX [ix_tokens_renovacion_expiracion] ON [tokens_renovacion] ([fecha_expiracion]);
GO


CREATE INDEX [ix_tokens_renovacion_familia] ON [tokens_renovacion] ([familia]);
GO


CREATE UNIQUE INDEX [uq_tokens_renovacion_hash] ON [tokens_renovacion] ([hash_token]);
GO

-- -------------------------------------------------------------------------------------
-- 2. Objetos que el modelo no expresa (migraciones ObjetosProgramablesV6, SepararEstadoPedidoYPago, HistorialDePagos y CotizacionesYPedidos)
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

-- Mantiene pedidos.monto_abonado igual a la suma de los pagos no anulados del pedido.
GO
CREATE OR ALTER TRIGGER dbo.trg_pagos_recalcular_monto_abonado
ON dbo.pagos
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.id_pago = i.id_pago WHERE d.anulado = 1 AND i.anulado = 0)
        THROW 50080, N'Un abono anulado no se puede reactivar.', 1;

    ;WITH cambios AS (
        SELECT id_pedido, monto FROM inserted WHERE anulado = 0
        UNION ALL
        SELECT id_pedido, -monto FROM deleted WHERE anulado = 0
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
-- 2b. Fase 9: mantenedores y avisos del taller (migración MantenedoresYAvisos)
-- -------------------------------------------------------------------------------------
DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[cartel_digital]') AND [c].[name] = N'tiempo_estimado_regreso');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [cartel_digital] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [cartel_digital] ALTER COLUMN [tiempo_estimado_regreso] nvarchar(100) NULL;

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[productos]') AND [c].[name] = N'descripcion');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [productos] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [productos] ALTER COLUMN [descripcion] nvarchar(500) NULL;

ALTER TABLE [productos] ADD [row_version] rowversion NOT NULL;

ALTER TABLE [materiales] ADD [row_version] rowversion NOT NULL;

ALTER TABLE [horarios_atencion] ADD [fecha_modificacion] datetimeoffset(0) NULL;

ALTER TABLE [horarios_atencion] ADD [id_empleado_modifica] int NULL;

ALTER TABLE [empleados] ADD [row_version] rowversion NOT NULL;

ALTER TABLE [clientes] ADD [row_version] rowversion NOT NULL;

DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[categorias]') AND [c].[name] = N'descripcion');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [categorias] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [categorias] ALTER COLUMN [descripcion] nvarchar(300) NULL;

ALTER TABLE [categorias] ADD [row_version] rowversion NOT NULL;

ALTER TABLE [catalogo] ADD [row_version] rowversion NOT NULL;

CREATE TABLE [destinatarios_avisos] (
    [id_destinatario] int NOT NULL IDENTITY,
    [correo] nvarchar(254) NOT NULL,
    [estado] varchar(10) NOT NULL,
    [id_reemplaza] int NULL,
    [token_hash] char(64) NULL,
    [vence] datetimeoffset(0) NULL,
    [id_empleado_solicita] int NOT NULL,
    [fecha_solicitud] datetimeoffset(0) NOT NULL,
    [fecha_confirmacion] datetimeoffset(0) NULL,
    [fecha_retiro] datetimeoffset(0) NULL,
    CONSTRAINT [pk_destinatarios_avisos] PRIMARY KEY ([id_destinatario]),
    CONSTRAINT [ck_destinatarios_avisos_correo] CHECK (correo COLLATE Latin1_General_100_BIN2 = LOWER(correo) COLLATE Latin1_General_100_BIN2 AND correo LIKE N'%_@_%._%' AND correo NOT LIKE N'% %'),
    CONSTRAINT [ck_destinatarios_avisos_enlace] CHECK ((estado = 'pendiente' AND token_hash IS NOT NULL AND vence IS NOT NULL) OR (estado <> 'pendiente' AND token_hash IS NULL AND vence IS NULL)),
    CONSTRAINT [ck_destinatarios_avisos_estado] CHECK (estado IN ('pendiente', 'activo', 'retirado', 'cancelado')),
    CONSTRAINT [fk_destinatarios_avisos_empleado] FOREIGN KEY ([id_empleado_solicita]) REFERENCES [empleados] ([id_empleado]),
    CONSTRAINT [fk_destinatarios_avisos_reemplaza] FOREIGN KEY ([id_reemplaza]) REFERENCES [destinatarios_avisos] ([id_destinatario])
);

CREATE TABLE [disponibilidad_empleados] (
    [id_disponibilidad] int NOT NULL IDENTITY,
    [id_empleado] int NOT NULL,
    [dia_semana] tinyint NOT NULL,
    [hora_inicio] time(0) NOT NULL,
    [hora_fin] time(0) NOT NULL,
    CONSTRAINT [pk_disponibilidad_empleados] PRIMARY KEY ([id_disponibilidad]),
    CONSTRAINT [ck_disponibilidad_dia] CHECK (dia_semana BETWEEN 1 AND 7),
    CONSTRAINT [ck_disponibilidad_rango] CHECK (hora_inicio < hora_fin),
    CONSTRAINT [fk_disponibilidad_empleado] FOREIGN KEY ([id_empleado]) REFERENCES [empleados] ([id_empleado])
);

CREATE TABLE [destinatarios_avisos_historial] (
    [id_historial] int NOT NULL IDENTITY,
    [accion] varchar(12) NOT NULL,
    [correo_enmascarado] nvarchar(254) NOT NULL,
    [id_destinatario] int NULL,
    [id_empleado] int NULL,
    [fecha] datetimeoffset(0) NOT NULL,
    [detalle] nvarchar(300) NULL,
    CONSTRAINT [pk_destinatarios_avisos_historial] PRIMARY KEY ([id_historial]),
    CONSTRAINT [ck_destinatarios_avisos_historial_accion] CHECK (accion IN ('agregar', 'reemplazar', 'confirmar', 'cancelar', 'quitar', 'reenviar', 'probar')),
    CONSTRAINT [fk_destinatarios_avisos_historial_destino] FOREIGN KEY ([id_destinatario]) REFERENCES [destinatarios_avisos] ([id_destinatario]),
    CONSTRAINT [fk_destinatarios_avisos_historial_empleado] FOREIGN KEY ([id_empleado]) REFERENCES [empleados] ([id_empleado])
);

CREATE INDEX [ix_horarios_empleado_modifica] ON [horarios_atencion] ([id_empleado_modifica]);

ALTER TABLE [horarios_atencion] ADD CONSTRAINT [ck_horarios_cerrado] CHECK (abierto = 1 OR (hora_inicio IS NULL AND hora_fin IS NULL));

CREATE INDEX [ix_destinatarios_avisos_empleado] ON [destinatarios_avisos] ([id_empleado_solicita]);

CREATE INDEX [ix_destinatarios_avisos_reemplaza] ON [destinatarios_avisos] ([id_reemplaza]);

CREATE UNIQUE INDEX [uq_destinatarios_avisos_token] ON [destinatarios_avisos] ([token_hash]) WHERE [token_hash] IS NOT NULL;

CREATE UNIQUE INDEX [uq_destinatarios_avisos_vigente] ON [destinatarios_avisos] ([correo]) WHERE [estado] IN ('activo', 'pendiente');

CREATE INDEX [ix_destinatarios_avisos_historial_destino] ON [destinatarios_avisos_historial] ([id_destinatario], [accion], [fecha]);

CREATE INDEX [ix_destinatarios_avisos_historial_empleado] ON [destinatarios_avisos_historial] ([id_empleado]);

CREATE INDEX [ix_destinatarios_avisos_historial_fecha] ON [destinatarios_avisos_historial] ([fecha]);

CREATE INDEX [ix_disponibilidad_empleado_dia] ON [disponibilidad_empleados] ([id_empleado], [dia_semana]);

ALTER TABLE [horarios_atencion] ADD CONSTRAINT [fk_horarios_empleado_modifica] FOREIGN KEY ([id_empleado_modifica]) REFERENCES [empleados] ([id_empleado]);
GO

-- -------------------------------------------------------------------------------------
-- 2c. Fase 10: rostro con prueba de vida (migración RostroConPruebaDeVida)
-- -------------------------------------------------------------------------------------
ALTER TABLE [rostros_empleado] ADD [descriptor_derecha] varbinary(1024) NULL;

ALTER TABLE [rostros_empleado] ADD [descriptor_frente] varbinary(1024) NULL;

ALTER TABLE [rostros_empleado] ADD [descriptor_izquierda] varbinary(1024) NULL;

ALTER TABLE [rostros_empleado] ADD [formato_muestras] tinyint NULL;

ALTER TABLE [rostros_empleado] ADD [id_empleado_registra] int NULL;
GO

-- Las columnas nuevas ya existen: lo que sigue puede usarlas.
CREATE TABLE [calibracion_rostro] (
    [id_medicion] int NOT NULL IDENTITY,
    [id_empleado] int NOT NULL,
    [etiqueta] varchar(12) NOT NULL,
    [distancia_frente] float NOT NULL,
    [distancia_primer_giro] float NOT NULL,
    [distancia_segundo_giro] float NOT NULL,
    [distancia_max_entre_capturas] float NOT NULL,
    [giro_frente] float NOT NULL,
    [giro_primero] float NOT NULL,
    [giro_segundo] float NOT NULL,
    [umbral_vigente] float NOT NULL,
    [id_empleado_registra] int NOT NULL,
    [fecha] datetimeoffset(0) NOT NULL,
    CONSTRAINT [pk_calibracion_rostro] PRIMARY KEY ([id_medicion]),
    CONSTRAINT [ck_calibracion_rostro_etiqueta] CHECK (etiqueta IN ('propio', 'otra_persona')),
    CONSTRAINT [fk_calibracion_rostro_empleado] FOREIGN KEY ([id_empleado]) REFERENCES [empleados] ([id_empleado]),
    CONSTRAINT [fk_calibracion_rostro_registra] FOREIGN KEY ([id_empleado_registra]) REFERENCES [empleados] ([id_empleado])
);

CREATE TABLE [desafios_rostro] (
    [id_desafio] int NOT NULL IDENTITY,
    [token_hash] char(64) NOT NULL,
    [correo_hash] char(64) NOT NULL,
    [orden] varchar(17) NOT NULL,
    [emitido] datetimeoffset(0) NOT NULL,
    [vence] datetimeoffset(0) NOT NULL,
    [usado] datetimeoffset(0) NULL,
    CONSTRAINT [pk_desafios_rostro] PRIMARY KEY ([id_desafio]),
    CONSTRAINT [ck_desafios_rostro_orden] CHECK (orden IN ('izquierda_derecha', 'derecha_izquierda')),
    CONSTRAINT [ck_desafios_rostro_vigencia] CHECK (vence > emitido AND (usado IS NULL OR usado >= emitido))
);

CREATE INDEX [ix_rostros_empleado_registra] ON [rostros_empleado] ([id_empleado_registra]);

ALTER TABLE [rostros_empleado] ADD CONSTRAINT [ck_rostros_empleado_muestras] CHECK ((formato_muestras IS NULL AND descriptor_frente IS NULL AND descriptor_izquierda IS NULL AND descriptor_derecha IS NULL) OR (formato_muestras = 2 AND ((fecha_revocacion IS NULL AND descriptor_frente IS NOT NULL AND descriptor_izquierda IS NOT NULL AND descriptor_derecha IS NOT NULL) OR (fecha_revocacion IS NOT NULL AND descriptor_frente IS NULL AND descriptor_izquierda IS NULL AND descriptor_derecha IS NULL))));

CREATE INDEX [ix_calibracion_rostro_empleado] ON [calibracion_rostro] ([id_empleado]);

CREATE INDEX [ix_calibracion_rostro_registra] ON [calibracion_rostro] ([id_empleado_registra]);

CREATE INDEX [ix_desafios_rostro_emitido] ON [desafios_rostro] ([emitido]);

CREATE UNIQUE INDEX [uq_desafios_rostro_token] ON [desafios_rostro] ([token_hash]);

ALTER TABLE [rostros_empleado] ADD CONSTRAINT [fk_rostros_empleado_registra] FOREIGN KEY ([id_empleado_registra]) REFERENCES [empleados] ([id_empleado]);
GO

-- -------------------------------------------------------------------------------------
-- 2d. Fase 10, seguimiento: muestras atadas a su lado (migración RostroMuestrasPorLado)
-- -------------------------------------------------------------------------------------
ALTER TABLE [rostros_empleado] DROP CONSTRAINT [ck_rostros_empleado_muestras];

ALTER TABLE [rostros_empleado] ADD CONSTRAINT [ck_rostros_empleado_muestras] CHECK ((formato_muestras IS NULL AND descriptor_frente IS NULL AND descriptor_izquierda IS NULL AND descriptor_derecha IS NULL) OR (formato_muestras IN (2, 3) AND ((fecha_revocacion IS NULL AND descriptor_frente IS NOT NULL AND descriptor_izquierda IS NOT NULL AND descriptor_derecha IS NOT NULL) OR (fecha_revocacion IS NOT NULL AND descriptor_frente IS NULL AND descriptor_izquierda IS NULL AND descriptor_derecha IS NULL))));
GO

-- -------------------------------------------------------------------------------------
-- 2e. Fase 11: notificaciones, auditoría y privacidad (migración NotificacionesYAuditoria)
-- -------------------------------------------------------------------------------------
ALTER TABLE [destinatarios_avisos_historial] DROP CONSTRAINT [ck_destinatarios_avisos_historial_accion];

ALTER TABLE [consentimientos] DROP CONSTRAINT [ck_consentimientos_tipo];

ALTER TABLE [horarios_atencion] ADD [row_version] rowversion NOT NULL;

ALTER TABLE [destinatarios_avisos] ADD [recibe_en_linea] bit NOT NULL DEFAULT CAST(1 AS bit);

ALTER TABLE [destinatarios_avisos] ADD [recibe_presenciales] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [destinatarios_avisos] ADD [row_version] rowversion NOT NULL;

ALTER TABLE [correos_salientes] ADD [enlace_baja] varchar(400) NULL;

ALTER TABLE [correos_salientes] ADD [fecha_reenvio] datetimeoffset(0) NULL;

ALTER TABLE [correos_salientes] ADD [id_empleado_reenvia] int NULL;

ALTER TABLE [correos_salientes] ADD [reenviado_como] bigint NULL;

ALTER TABLE [correos_salientes] ADD [revisado] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [clientes] ADD [inhabilitado_en] datetimeoffset(0) NULL;

ALTER TABLE [clientes] ADD [recibe_avisos_estado] bit NOT NULL DEFAULT CAST(1 AS bit);

ALTER TABLE [cartel_digital] ADD [row_version] rowversion NOT NULL;
GO

-- Las columnas nuevas ya existen: lo que sigue puede usarlas.

CREATE TABLE [auditoria] (
    [id_auditoria] bigint NOT NULL IDENTITY,
    [fecha] datetimeoffset(0) NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
    [id_empleado] int NULL,
    [id_cliente] int NULL,
    [rol] nvarchar(20) NULL,
    [entidad] varchar(40) NOT NULL,
    [id_entidad] varchar(40) NULL,
    [accion] varchar(30) NOT NULL,
    [detalle] nvarchar(2000) NULL,
    [ip] varchar(45) NULL,
    CONSTRAINT [pk_auditoria] PRIMARY KEY ([id_auditoria]),
    CONSTRAINT [ck_auditoria_quien] CHECK (id_empleado IS NULL OR id_cliente IS NULL)
);

CREATE TABLE [claves_idempotencia] (
    [id_clave] bigint NOT NULL IDENTITY,
    [clave] char(36) NOT NULL,
    [ambito] varchar(100) NOT NULL,
    [id_usuario] int NOT NULL,
    [tipo_usuario] varchar(10) NOT NULL,
    [hash_cuerpo] char(64) NOT NULL,
    [estado_http] int NULL,
    [respuesta] nvarchar(max) NULL,
    [fecha] datetimeoffset(0) NOT NULL,
    [vence] datetimeoffset(0) NOT NULL,
    CONSTRAINT [pk_claves_idempotencia] PRIMARY KEY ([id_clave]),
    CONSTRAINT [ck_claves_idempotencia_tipo_usuario] CHECK (tipo_usuario IN ('cliente', 'empleado')),
    CONSTRAINT [ck_claves_idempotencia_vence] CHECK (vence > fecha)
);

ALTER TABLE [destinatarios_avisos_historial] ADD CONSTRAINT [ck_destinatarios_avisos_historial_accion] CHECK (accion IN ('agregar', 'reemplazar', 'confirmar', 'cancelar', 'quitar', 'reenviar', 'probar', 'preferencia'));

ALTER TABLE [destinatarios_avisos] ADD CONSTRAINT [ck_destinatarios_avisos_preferencia] CHECK (recibe_en_linea = 1 OR recibe_presenciales = 1);

CREATE INDEX [ix_correos_salientes_empleado_reenvia] ON [correos_salientes] ([id_empleado_reenvia]) WHERE [id_empleado_reenvia] IS NOT NULL;

CREATE INDEX [ix_correos_salientes_fallidos] ON [correos_salientes] ([proximo_intento], [revisado]) WHERE [estado] = 'fallido';

ALTER TABLE [consentimientos] ADD CONSTRAINT [ck_consentimientos_tipo] CHECK (tipo IN ('privacidad', 'terminos', 'marketing', 'cookies', 'avisos_estado'));

CREATE INDEX [ix_auditoria_cliente] ON [auditoria] ([id_cliente], [fecha] DESC) WHERE [id_cliente] IS NOT NULL;

CREATE INDEX [ix_auditoria_empleado] ON [auditoria] ([id_empleado], [fecha] DESC, [id_auditoria] DESC) WHERE [id_empleado] IS NOT NULL;

CREATE INDEX [ix_auditoria_entidad] ON [auditoria] ([entidad], [id_entidad]);

CREATE INDEX [ix_auditoria_fecha] ON [auditoria] ([fecha] DESC, [id_auditoria] DESC);

CREATE INDEX [ix_claves_idempotencia_vence] ON [claves_idempotencia] ([vence]);

CREATE UNIQUE INDEX [uq_claves_idempotencia] ON [claves_idempotencia] ([clave], [ambito], [tipo_usuario], [id_usuario]);

ALTER TABLE [correos_salientes] ADD CONSTRAINT [fk_correos_salientes_empleado_reenvia] FOREIGN KEY ([id_empleado_reenvia]) REFERENCES [empleados] ([id_empleado]);

GO
CREATE TRIGGER dbo.trg_auditoria_inmutable ON dbo.auditoria
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50125, N'La bitácora de auditoría no se modifica ni se borra.', 1;
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
    (N'20261113010201_HistorialDePagos', N'10.0.11'),
    (N'20261113111453_ServiciosComunes', N'10.0.11'),
    (N'20261113154234_PortadaPublica', N'10.0.11'),
    (N'20261114002321_CuentasYSesion', N'10.0.11'),
    (N'20261114035839_SolicitudesYAdjuntos', N'10.0.11'),
    (N'20261114220104_CotizacionesYPedidos', N'10.0.11'),
    (N'20261115021638_MantenedoresYAvisos', N'10.0.11'),
    (N'20261115131307_RostroConPruebaDeVida', N'10.0.11'),
    (N'20261115151730_RostroMuestrasPorLado', N'10.0.11'),
    (N'20261116002312_NotificacionesYAuditoria', N'10.0.11');
GO

SET NOEXEC OFF;
GO
