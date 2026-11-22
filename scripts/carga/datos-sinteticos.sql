-- Datos SINTETICOS para la prueba de carga (fase 15A, RNFP01). Nada de esto es real ni personal: nombres, telefonos (empiezan con 7),
-- correos (@carga.test) y montos son inventados. Solo corre contra una base TEMPORAL que crea el propio guion (prefijo SyncTime_Pruebas_);
-- carga.mjs la borra al terminar. Se ejecuta con: sqlcmd -I -b -v Clientes=2000 Solicitudes=6000 Auditoria=40000 Publicaciones=60 -i datos-sinteticos.sql
-- (-I es obligatorio: pedidos y auditoria tienen indices filtrados y vistas indexadas que exigen QUOTED_IDENTIFIER ON.)
--
-- Volumen de referencia (un taller con unos dos años de historia, bastante mas de lo que tendra al abrir):
--   2.000 clientes, 6.000 solicitudes (la mitad en linea) con 1 a 3 items cada una, 40 % aceptadas con su cotizacion y su pedido (unos 2.400),
--   abonos, historiales y 40.000 filas de bitacora.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @nClientes int = $(Clientes), @nSolicitudes int = $(Solicitudes), @nAuditoria int = $(Auditoria), @nCatalogo int = $(Publicaciones);
DECLARE @marca nvarchar(20) = N'sintetico-carga';

IF DB_NAME() NOT LIKE N'SyncTime[_]Pruebas[_]%'
    THROW 50990, N'Los datos sinteticos solo se cargan en una base temporal de pruebas (prefijo SyncTime_Pruebas_).', 1;
IF EXISTS (SELECT 1 FROM clientes WHERE direccion = @marca)
    THROW 50991, N'Esta base ya tiene los datos sinteticos.', 1;

BEGIN TRAN;

CREATE TABLE #emp (n int NOT NULL PRIMARY KEY, id int NOT NULL);
INSERT #emp SELECT ROW_NUMBER() OVER (ORDER BY id_empleado), id_empleado FROM empleados WHERE activo = 1;
DECLARE @nEmp int = (SELECT COUNT(*) FROM #emp);
IF @nEmp = 0 THROW 50992, N'No hay empleados: crea al menos uno antes de sembrar.', 1;

-- ── Clientes ──
INSERT INTO clientes (nombre, apellido, telefono, email, direccion)
SELECT CHOOSE((i % 20) + 1, N'Ana', N'Luis', N'María', N'Pedro', N'Carla', N'Jorge', N'Paula', N'Diego', N'Rosa', N'Mario', N'Elena', N'Hugo',
                            N'Camila', N'Felipe', N'Valentina', N'Andrés', N'Daniela', N'Matías', N'Francisca', N'Tomás'),
       CHOOSE((i % 30) + 1, N'González', N'Muñoz', N'Rojas', N'Díaz', N'Pérez', N'Soto', N'Contreras', N'Silva', N'Martínez', N'Sepúlveda',
                            N'Morales', N'Rodríguez', N'López', N'Fuentes', N'Hernández', N'Torres', N'Araya', N'Flores', N'Espinoza', N'Valenzuela',
                            N'Castillo', N'Tapia', N'Reyes', N'Gutiérrez', N'Castro', N'Pizarro', N'Álvarez', N'Vásquez', N'Sánchez', N'Fernández'),
       '7' + RIGHT('00000000' + CAST(i AS varchar(8)), 8),
       CASE WHEN i % 5 < 3 THEN N'cliente' + CAST(i AS nvarchar(8)) + N'@carga.test' END,
       @marca
FROM (SELECT value AS i FROM GENERATE_SERIES(1, @nClientes)) n
ORDER BY i;

-- Los clientes sinteticos tienen ids seguidos (se insertaron de una vez, en orden): el cliente k de una solicitud es @primerCliente + k, sin cruzar tablas.
DECLARE @primerCliente int, @ultimoCliente int;
SELECT @primerCliente = MIN(id_cliente), @ultimoCliente = MAX(id_cliente) FROM clientes WHERE direccion = @marca;
IF @ultimoCliente - @primerCliente + 1 <> @nClientes THROW 50993, N'Los clientes sinteticos no quedaron con ids seguidos.', 1;

-- ── Solicitudes (par = en linea, impar = registrada en el local) ──
-- Estados por k % 10: 0,1,9 solicitado · 2,3 cotizado · 4,5,6,7 aceptado · 8 rechazado.
INSERT INTO solicitudes (id_cliente, id_empleado_registra, descripcion_general, fecha_solicitud, estado, motivo_rechazo)
SELECT @primerCliente + (s.i % @nClientes),
       CASE WHEN s.i % 2 = 0 THEN NULL ELSE e.id END,
       N'Letrero sintético de carga número ' + CAST(s.i AS nvarchar(10)) + N', pedido de ejemplo para las pruebas de rendimiento',
       CAST(DATEADD(MINUTE, -(7200 + CAST((s.i * 7919) % 1044000 AS int)), SYSDATETIME()) AS datetime2(0)),
       CASE s.i % 10 WHEN 2 THEN N'cotizado' WHEN 3 THEN N'cotizado' WHEN 4 THEN N'aceptado' WHEN 5 THEN N'aceptado'
                     WHEN 6 THEN N'aceptado' WHEN 7 THEN N'aceptado' WHEN 8 THEN N'rechazado' ELSE N'solicitado' END,
       CASE WHEN s.i % 10 = 8 THEN N'Rechazada por datos sintéticos de carga' END
FROM (SELECT value AS i FROM GENERATE_SERIES(1, @nSolicitudes)) s
JOIN #emp e ON e.n = (s.i % @nEmp) + 1
ORDER BY s.i;

CREATE TABLE #sol (k int NOT NULL PRIMARY KEY, id int NOT NULL, estado nvarchar(30) NOT NULL, fecha datetime2(0) NOT NULL, id_cliente int NOT NULL, id_emp int NULL);
INSERT #sol SELECT ROW_NUMBER() OVER (ORDER BY id_solicitud), id_solicitud, estado, fecha_solicitud, id_cliente, id_empleado_registra
FROM solicitudes WHERE descripcion_general LIKE N'Letrero sintético de carga número %';

-- Items: de 1 a 3 por solicitud.
INSERT INTO solicitud_detalles (id_solicitud, id_producto, descripcion, ancho_mm, alto_mm, cantidad)
SELECT s.id, 1 + ((s.k + j.value) % 16), N'Ítem sintético ' + CAST(j.value AS nvarchar(2)),
       500 + ((s.k * 31 + j.value * 17) % 2000), 400 + ((s.k * 13 + j.value * 7) % 1600), 1 + ((s.k + j.value) % 5)
FROM #sol s CROSS APPLY GENERATE_SERIES(1, 1 + (s.k % 3)) j;

-- Historial: la creacion (por el cliente si fue en linea, por un empleado si fue en el local) y el cambio de estado actual.
INSERT INTO solicitudes_historial (id_solicitud, estado_anterior, estado_nuevo, motivo, id_empleado, id_cliente, fecha)
SELECT s.id, NULL, N'solicitado', NULL, s.id_emp, CASE WHEN s.id_emp IS NULL THEN s.id_cliente END, CAST(s.fecha AS datetimeoffset(0)) FROM #sol s;
INSERT INTO solicitudes_historial (id_solicitud, estado_anterior, estado_nuevo, motivo, id_empleado, id_cliente, fecha)
SELECT s.id, N'solicitado', s.estado, CASE WHEN s.estado = N'rechazado' THEN N'Rechazada por datos sintéticos de carga' END, e.id, NULL,
       CAST(DATEADD(HOUR, 6 + (s.k % 48), s.fecha) AS datetimeoffset(0))
FROM #sol s JOIN #emp e ON e.n = (s.k % @nEmp) + 1 WHERE s.estado <> N'solicitado';

-- ── Cotizaciones: pendiente (solicitud cotizada) o aprobada (solicitud aceptada) ──
INSERT INTO cotizaciones (id_solicitud, id_empleado, fecha_cotizacion, observaciones, costo_mano_obra, otros_costos, estado, fecha_respuesta, via_respuesta, id_empleado_respuesta)
SELECT s.id, e.id, CAST(DATEADD(HOUR, 6 + (s.k % 48), s.fecha) AS datetime2(0)), N'Cotización sintética de carga',
       (40 + (s.k * 137) % 400) * 1000, (s.k % 5) * 1000,
       CASE WHEN s.estado = N'aceptado' THEN N'aprobada' ELSE N'pendiente' END,
       CASE WHEN s.estado = N'aceptado' THEN CAST(DATEADD(HOUR, 30 + (s.k % 48), s.fecha) AS datetimeoffset(0)) END,
       CASE WHEN s.estado = N'aceptado' THEN CHOOSE((s.k % 4) + 1, N'cliente_web', N'telefono', N'whatsapp', N'presencial') END,
       CASE WHEN s.estado = N'aceptado' AND s.k % 4 <> 0 THEN e.id END
FROM #sol s JOIN #emp e ON e.n = (s.k % @nEmp) + 1
WHERE s.estado IN (N'cotizado', N'aceptado');

-- ── Pedidos: uno por cotizacion aprobada. Por k % 7: 0 aceptado · 1 en proceso · 2 terminado · 3,4,5 entregado · 6 anulado ──
INSERT INTO pedidos (id_solicitud, id_cotizacion, origen, id_empleado_responsable, fecha_pedido, fecha_entrega_estimada, fecha_entrega_real,
                     modalidad_entrega, monto_base_trabajo, estado_pedido, motivo_anulacion, fecha_anulacion)
SELECT s.id, c.id_cotizacion, N'cotizacion', e.id, p.fecha_pedido, p.estimada,
       CASE WHEN s.k % 7 BETWEEN 3 AND 5 THEN CASE WHEN DATEADD(DAY, (s.k % 5) - 1, p.estimada) > SYSDATETIME() THEN CAST(SYSDATETIME() AS datetime2(0))
                                                    ELSE DATEADD(DAY, (s.k % 5) - 1, p.estimada) END END,
       N'retiro_local', c.costo_mano_obra + c.otros_costos,
       CASE s.k % 7 WHEN 0 THEN N'aceptado' WHEN 1 THEN N'en_proceso' WHEN 2 THEN N'terminado' WHEN 6 THEN N'anulado' ELSE N'entregado' END,
       CASE WHEN s.k % 7 = 6 THEN N'Anulado por datos sintéticos de carga' END,
       CASE WHEN s.k % 7 = 6 THEN CAST(DATEADD(DAY, 1, p.fecha_pedido) AS datetimeoffset(0)) END
FROM #sol s
JOIN cotizaciones c ON c.id_solicitud = s.id AND c.estado = N'aprobada'
JOIN #emp e ON e.n = (s.k % @nEmp) + 1
CROSS APPLY (SELECT CAST(DATEADD(HOUR, 40 + (s.k % 24), s.fecha) AS datetime2(0)) AS fecha_pedido,
                    CAST(DATEADD(DAY, 7 + (s.k % 14), DATEADD(HOUR, 40 + (s.k % 24), s.fecha)) AS datetime2(0)) AS estimada) p
WHERE s.estado = N'aceptado'
ORDER BY s.k;

CREATE TABLE #ped (id int NOT NULL PRIMARY KEY, k int NOT NULL, estado nvarchar(30) NOT NULL, fecha datetime2(0) NOT NULL, base int NOT NULL, id_emp int NOT NULL);
INSERT #ped SELECT p.id_pedido, s.k, p.estado_pedido, p.fecha_pedido, CAST(p.monto_base_trabajo AS int), p.id_empleado_responsable
FROM pedidos p JOIN #sol s ON s.id = p.id_solicitud;

INSERT INTO pedidos_historial (id_pedido, estado_anterior, estado_nuevo, nota, id_empleado, fecha)
SELECT id, NULL, N'aceptado', N'Pedido sintético de carga', id_emp, CAST(fecha AS datetimeoffset(0)) FROM #ped;
INSERT INTO pedidos_historial (id_pedido, estado_anterior, estado_nuevo, nota, id_empleado, fecha)
SELECT id, N'aceptado', estado, NULL, id_emp, CAST(DATEADD(DAY, 2, fecha) AS datetimeoffset(0)) FROM #ped WHERE estado <> N'aceptado';

-- ── Abonos: 80 % de los pedidos no anulados pagan la mitad; los entregados pagan el resto (el trigger mantiene monto_abonado) ──
INSERT INTO pagos (id_pedido, monto, fecha_pago, medio_pago, id_empleado_registra, fecha_registro)
SELECT p.id, p.base / 2, CAST(p.fecha AS date), CHOOSE((p.k % 5) + 1, N'efectivo', N'transferencia', N'tarjeta_debito', N'tarjeta_credito', N'otro'), p.id_emp, p.fecha
FROM #ped p WHERE p.estado <> N'anulado' AND p.k % 5 <> 0 AND p.base / 2 > 0;
INSERT INTO pagos (id_pedido, monto, fecha_pago, medio_pago, id_empleado_registra, fecha_registro)
SELECT p.id, p.base - p.base / 2, CAST(DATEADD(DAY, 7, p.fecha) AS date), N'transferencia', p.id_emp, DATEADD(DAY, 7, p.fecha)
FROM #ped p WHERE p.estado = N'entregado' AND p.k % 5 <> 0 AND p.base - p.base / 2 > 0
  AND DATEADD(DAY, 7, p.fecha) <= SYSDATETIME();

-- ── Bitacora: la IP solo se conserva 90 dias (RNFE01) ──
INSERT INTO auditoria (fecha, id_empleado, rol, entidad, id_entidad, accion, detalle, ip)
SELECT f.fecha, e.id, CHOOSE((a.i % 3) + 1, N'administrador', N'operario', N'dueño'),
       CHOOSE((a.i % 4) + 1, N'solicitud', N'pedido', N'cliente', N'cotizacion'), CAST((a.i % @nSolicitudes) + 1 AS varchar(10)),
       CASE WHEN a.i % 4 = 0 THEN N'crear' ELSE N'modificar' END,
       N'{"campos":["estado"],"valores":{"estado":{"antes":"solicitado","despues":"cotizado"}}}',
       CASE WHEN f.fecha > DATEADD(DAY, -90, SYSDATETIMEOFFSET()) THEN '127.0.0.1' END
FROM (SELECT value AS i FROM GENERATE_SERIES(1, @nAuditoria)) a
JOIN #emp e ON e.n = (a.i % @nEmp) + 1
CROSS APPLY (SELECT DATEADD(MINUTE, -CAST((CAST(a.i AS bigint) * 104729) % 1051200 AS int), SYSDATETIMEOFFSET()) AS fecha) f;

-- ── Vitrina: publicaciones visibles, sin foto (las imagenes viven en disco, no en la base) ──
INSERT INTO catalogo (id_producto, id_empleado_creador, titulo, descripcion, ancho_mm, alto_mm, precio_referencial, visible)
SELECT 1 + (v.i % 16), e.id, N'Trabajo sintético de carga ' + CAST(v.i AS nvarchar(6)), N'Letrero de ejemplo para medir la vitrina con muchas publicaciones.',
       1200, 600, 50000 + (v.i % 20) * 5000, 1
FROM (SELECT value AS i FROM GENERATE_SERIES(1, @nCatalogo)) v JOIN #emp e ON e.n = (v.i % @nEmp) + 1;

COMMIT;

SELECT 'clientes' AS tabla, COUNT(*) AS filas FROM clientes
UNION ALL SELECT 'solicitudes', COUNT(*) FROM solicitudes
UNION ALL SELECT 'solicitud_detalles', COUNT(*) FROM solicitud_detalles
UNION ALL SELECT 'cotizaciones', COUNT(*) FROM cotizaciones
UNION ALL SELECT 'pedidos', COUNT(*) FROM pedidos
UNION ALL SELECT 'pagos', COUNT(*) FROM pagos
UNION ALL SELECT 'auditoria', COUNT(*) FROM auditoria
UNION ALL SELECT 'catalogo', COUNT(*) FROM catalogo;
