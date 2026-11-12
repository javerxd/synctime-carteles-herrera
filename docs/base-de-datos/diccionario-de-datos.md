# Diccionario de datos — LetreriaLosHerreraDB

Estado del esquema: migración `SepararEstadoPedidoYPago` (14-11-2026). Diagrama en [modelo-entidad-relacion.md](modelo-entidad-relacion.md); script de creación en [LetreriaLosHerreraDB.sql](LetreriaLosHerreraDB.sql).

**Convenciones**
- Motor SQL Server 2025 Express, intercalación `Modern_Spanish_CI_AS` (ordena y compara en español, sin distinguir mayúsculas).
- Nombres de tablas y columnas en español y en minúsculas con guion bajo. Prefijos de restricciones: `pk_` clave primaria, `fk_` clave foránea, `uq_` índice único, `ix_` índice, `ck_` restricción CHECK, `df_` valor por defecto.
- **PK** clave primaria (entera, autoincremental salvo que se diga otra cosa). **FK** clave foránea. **UQ** valor único.
- **Calculada**: columna que calcula SQL Server y guarda (`PERSISTED`); no se escribe a mano.
- **Montos en pesos chilenos sin decimales** (RNFO05). Las columnas nuevas usan `int`. Las anteriores son `decimal(12,2)` y siempre guardan valores enteros: la API y la interfaz validan que no tengan decimales.
- Fechas y horas en `datetime2(0)` (hora de Chile del servidor), salvo las del rostro, que usan `datetimeoffset(0)` porque deciden bloqueos y caducidades.
- `row_version` es un `rowversion`: cambia en cada modificación y permite rechazar el cambio de un registro que otra persona modificó después de abrirlo (RNFP10).

Columnas marcadas **NUEVA** o **CAMBIA**: introducidas el 14-11-2026 por la revisión de la profesora guía.

---

## clientes

Personas que piden trabajos. Un cliente puede existir sin cuenta: lo registra el personal al recibir una solicitud presencial. Con cuenta, ingresa al portal.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_cliente` | int | No | autoincremental | PK |
| `rut` | varchar(12) | Sí | | RUT con dígito verificador (RNFO06). UQ si existe; es opcional en una solicitud rápida |
| `nombre` | nvarchar(80) | No | | Nombre |
| `apellido` | nvarchar(80) | No | | Apellido |
| `telefono` | varchar(20) | No | | Teléfono. UQ: sirve para buscar al cliente antes de crearlo |
| `email` | nvarchar(100) | Sí | | Correo. UQ si existe. CHECK de formato |
| `direccion` | nvarchar(255) | Sí | | Dirección |
| `password_hash` | varchar(100) | Sí | | Hash BCrypt de la contraseña. Nulo si no tiene cuenta |
| `registrado` | bit | — | Calculada: 1 si tiene `password_hash` | Indica si tiene cuenta |
| `fecha_registro` | datetime2(0) | No | `SYSDATETIME()` | Fecha de alta |
| `activo` | bit | No | 1 | **0 = cuenta inhabilitada** (borrado lógico). Cuando un cliente da de baja su cuenta no se borran sus pedidos: se conserva la integridad histórica y contable |

Restricciones: `pk_clientes`, `uq_clientes_telefono`, `uq_clientes_rut` (filtrado: RUT no nulo), `uq_clientes_email` (filtrado: correo no nulo), `ck_clientes_email`.

## empleados

Personal del taller con acceso al panel interno.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_empleado` | int | No | autoincremental | PK |
| `rut` | varchar(12) | No | | RUT. UQ |
| `nombre` | nvarchar(80) | No | | Nombre |
| `apellido` | nvarchar(80) | No | | Apellido |
| `telefono` | varchar(20) | No | | Teléfono |
| `email` | nvarchar(100) | No | | Correo de ingreso. UQ. CHECK de formato |
| `password_hash` | varchar(100) | No | | Hash BCrypt de la contraseña |
| `rol` | nvarchar(20) | No | `administrador` | `administrador`, `dueño`, `diseñador` u `operario` (RNFO08). Dueño y administrador pueden lo mismo; diseñador y operario también (decisión del 14-11-2026) |
| `activo` | bit | No | 1 | 0 = no puede ingresar |

Restricciones: `pk_empleados`, `uq_empleados_rut`, `uq_empleados_email`, `ck_empleados_rol`, `ck_empleados_email`.

## categorias

Grandes grupos de trabajos (letreros, vehículos, estampados, muros, personalizado). Los mantiene el dueño.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_categoria` | int | No | autoincremental | PK |
| `nombre` | nvarchar(80) | No | | Nombre. UQ |
| `descripcion` | nvarchar(250) | Sí | | Descripción |
| `activo` | bit | No | 1 | 0 = no se ofrece |

Datos iniciales: 5 categorías. Restricciones: `pk_categorias`, `uq_categorias_nombre`.

## productos

**Tipos de letrero** que define el dueño (palomita, cartel metálico, lienzo, de ventana, de candidatura…), cada uno dentro de una categoría.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_producto` | int | No | autoincremental | PK |
| `id_categoria` | int | No | | FK a `categorias` |
| `nombre` | nvarchar(100) | No | | Nombre del tipo de letrero. UQ |
| `descripcion` | nvarchar(250) | Sí | | Descripción |
| `precio_base_ref` | decimal(12,2) | Sí | | Precio referencial en pesos. CHECK ≥ 0 |
| `activo` | bit | No | 1 | 0 = no se ofrece |

Datos iniciales: 4 tipos. Restricciones: `pk_productos`, `fk_productos_categoria`, `uq_productos_nombre`, `ck_productos_precio`. Índice `ix_productos_categoria` (`id_categoria`, `activo`): lista los tipos activos de una categoría.

## catalogo

Vitrina pública de trabajos realizados o destacados (RFS01).

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_catalogo` | int | No | autoincremental | PK |
| `id_producto` | int | No | | FK a `productos`: tipo de letrero y, por él, la categoría |
| `id_empleado_creador` | int | Sí | | FK a `empleados`: quién publicó |
| `titulo` | nvarchar(120) | No | | Título |
| `descripcion` | nvarchar(500) | No | | Descripción |
| `imagen_url` | nvarchar(500) | No | | Ruta de la imagen |
| `ancho_mm`, `alto_mm` | int | Sí | | Medidas en milímetros. CHECK: ambas nulas o ambas mayores que 0 |
| `precio_referencial` | decimal(12,2) | Sí | | Precio referencial en pesos. CHECK ≥ 0 |
| `fecha_publicacion` | datetime2(0) | No | `SYSDATETIME()` | Fecha de publicación |
| `visible` | bit | No | 1 | 1 = se muestra en la portada |

Restricciones: `pk_catalogo`, `fk_catalogo_producto`, `fk_catalogo_empleado`, `ck_catalogo_precio`, `ck_catalogo_medidas`. Índice `ix_catalogo_visible_producto` (`visible`, `id_producto`): la consulta pública de la vitrina. La autorización de uso de logos (RFS01, RFS07) se agrega en la fase 5.

## solicitudes

Cabecera de cada pedido de trabajo de un cliente, en línea o en el local (RFS04).

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_solicitud` | int | No | autoincremental | PK |
| `id_cliente` | int | No | | FK a `clientes` |
| `id_empleado_registra` | int | Sí | | FK a `empleados`: quién la registró en el local. Nulo si la envió el cliente |
| `origen` | nvarchar(10) | — | Calculada: `online` si no la registró un empleado; si no, `presencial` | Origen |
| `descripcion_general` | nvarchar(500) | Sí | | Especificaciones generales |
| `fecha_solicitud` | datetime2(0) | No | `SYSDATETIME()` | Fecha y hora automáticas |
| `estado` | nvarchar(15) | No | `solicitado` | `solicitado`, `cotizado`, `aceptado` o `rechazado` |
| `motivo_rechazo` | nvarchar(250) | Sí | | Obligatorio al rechazar (lo exige la API) |
| `eliminado_logico` | bit | No | 0 | 1 = eliminada; se conserva para estadísticas (RNFO04) |
| `fecha_eliminacion_logica` | datetime2(0) | Sí | | Cuándo se eliminó |
| `row_version` | rowversion | No | automática | Control de concurrencia |

Restricciones: `pk_solicitudes`, `fk_solicitudes_cliente`, `fk_solicitudes_empleado`, `ck_solicitudes_estado`. Índices: `ix_solicitudes_metricas` (`fecha_solicitud`, `origen`, `estado`, `eliminado_logico`) para indicadores y bandeja; `ix_solicitudes_cliente` (`id_cliente`, `eliminado_logico`) para el historial de un cliente y la regla contra solicitudes falsas.

## solicitud_detalles

Ítems de una solicitud.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_solicitud_detalle` | int | No | autoincremental | PK |
| `id_solicitud` | int | No | | FK a `solicitudes` (se borra con ella) |
| `id_producto` | int | No | | FK a `productos`: tipo de letrero |
| `descripcion` | nvarchar(300) | No | | Glosa de especificaciones |
| `ancho_mm`, `alto_mm` | int | Sí | | Medidas en milímetros. CHECK: ambas nulas o ambas mayores que 0 |
| `cantidad` | int | No | 1 | Cantidad. CHECK > 0 |
| `area_m2` | decimal(12,4) | — | Calculada: ancho × alto / 1.000.000 | Área en m² |

Restricciones: `pk_solicitud_detalles`, `fk_sol_detalles_solicitud` (en cascada), `fk_sol_detalles_producto`, `ck_sol_detalles_cantidad`, `ck_sol_detalles_medidas`. Índices: `ix_sol_detalles_solicitud`, `ix_sol_detalles_producto`.

## materiales

Materiales base (madera, vidrio, cholguán, vinilo, plancha metálica…) con su costo unitario. Los mantiene el dueño.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_material` | int | No | autoincremental | PK |
| `nombre` | nvarchar(100) | No | | Nombre. UQ |
| `unidad_medida` | nvarchar(30) | No | | `m2`, `litro`, etc. |
| `costo_unitario_aprox` | decimal(12,2) | No | | Costo por unidad en pesos. CHECK ≥ 0 |
| `activo` | bit | No | 1 | 0 = no se ofrece |

Datos iniciales: 5 materiales. Restricciones: `pk_materiales`, `uq_materiales_nombre`, `ck_materiales_costo`.

## cotizaciones

Presupuesto de una solicitud (RFS05). Una solicitud puede tener varias; solo una aprobada da origen al pedido.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_cotizacion` | int | No | autoincremental | PK |
| `id_solicitud` | int | No | | FK a `solicitudes` |
| `id_empleado` | int | No | | FK a `empleados`: quién la elaboró |
| `fecha_cotizacion` | datetime2(0) | No | `SYSDATETIME()` | Fecha |
| `observaciones` | nvarchar(500) | Sí | | Observaciones |
| `costo_materiales_total` | decimal(12,2) | No | 0 | Suma de las líneas de materiales. La mantiene el trigger `trg_cotizacion_materiales_recalcular_total` |
| `costo_mano_obra` | decimal(12,2) | No | 0 | Mano de obra |
| `otros_costos` | decimal(12,2) | No | 0 | Otros gastos |
| `total_cotizacion` | decimal(14,2) | — | Calculada: materiales + mano de obra + otros | Total |
| `estado` | nvarchar(15) | No | `pendiente` | `pendiente`, `aprobada` o `rechazada`. La aprueba el cliente en la web o el personal cuando el cliente confirma por teléfono o WhatsApp |

Restricciones: `pk_cotizaciones`, `fk_cotizaciones_solicitud` (en cascada), `fk_cotizaciones_empleado`, `ck_cotizaciones_costos`, `ck_cotizaciones_estado`. Índice `ix_cotizaciones_solicitud` (`id_solicitud`, `estado`).

## cotizacion_materiales

Líneas de materiales de una cotización.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_cotizacion_material` | int | No | autoincremental | PK |
| `id_cotizacion` | int | No | | FK a `cotizaciones` (se borra con ella) |
| `id_material` | int | No | | FK a `materiales` |
| `medida_aplicada` | nvarchar(80) | Sí | | Medida a la que se aplica |
| `cantidad` | decimal(10,2) | No | | Cantidad en la unidad del material (admite decimales: 1,5 m²). CHECK > 0 |
| `costo_unitario` | decimal(12,2) | No | | Costo por unidad al cotizar. CHECK ≥ 0 |
| `subtotal` | decimal(14,2) | — | Calculada: cantidad × costo unitario | Subtotal |

Restricciones: `pk_cotizacion_materiales`, `fk_cot_mat_cotizacion` (en cascada), `fk_cot_mat_material`, `ck_cot_mat_cantidad`, `ck_cot_mat_costo`. Índice `ix_cot_mat_cotizacion`. Tiene el trigger `trg_cotizacion_materiales_recalcular_total`.

## pedidos

Trabajo acordado (RFS06). **Tabla modificada el 14-11-2026:** el estado operativo y el estado de pago pasan a ser independientes.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_pedido` | int | No | autoincremental | PK |
| `id_solicitud` | int | No | | FK a `solicitudes`. UQ: una solicitud da a lo más un pedido. En un pedido directo, la solicitud se genera internamente |
| `id_cotizacion` | int | Sí | | FK a `cotizaciones`: la cotización aprobada de origen |
| `origen` | nvarchar(15) | No | `cotizacion` | **NUEVA.** `cotizacion` (nace de una cotización aprobada) o `directo` (acordado en el taller sin solicitud previa) |
| `id_empleado_responsable` | int | No | | FK a `empleados`: responsable del trabajo |
| `fecha_pedido` | datetime2(0) | No | `SYSDATETIME()` | Fecha de creación. Sirve para filtrar por año y mes |
| `fecha_entrega_estimada` | datetime2(0) | No | | Fecha comprometida (calendario de entregas) |
| `fecha_entrega_real` | datetime2(0) | Sí | | Fecha en que se entregó |
| `modalidad_entrega` | nvarchar(25) | No | `retiro_local` | `retiro_local`, `entrega_domicilio` o `instalacion_terreno` |
| `requiere_instalacion` | bit | No | 0 | Si lleva instalación |
| `direccion_instalacion` | nvarchar(255) | Sí | | Dónde se instala |
| `costo_instalacion_adicional` | decimal(12,2) | No | 0 | Costo de instalación en pesos |
| `monto_base_trabajo` | decimal(12,2) | No | 0 | Monto del trabajo en pesos (de la cotización o acordado) |
| `monto_total_final` | decimal(14,2) | — | Calculada: monto base + instalación | Monto final |
| `estado_pedido` | nvarchar(15) | No | `aceptado` | **CAMBIA** (antes `estado`). Estado operativo: `aceptado`, `en_proceso`, `terminado`, `entregado` o `anulado`. «Anulado» reemplaza a «cancelado», que en Chile se entiende como «pagado» |
| `monto_abonado` | int | No | 0 | **NUEVA.** Total pagado hasta ahora, en pesos |
| `saldo_pendiente` | int | — | Calculada: monto final − monto abonado | **NUEVA.** Lo que falta por cobrar |
| `estado_pago` | nvarchar(12) | — | Calculada: `no_pagado` si el abono es 0; `pagado_total` si cubre el monto final; si no, `abonado` | **NUEVA.** Estado financiero, independiente del operativo |
| `numero_factura` | nvarchar(30) | Sí | | **NUEVA.** Número de factura cuando se paga. No es único: un cliente habitual puede pagar varios pedidos con una sola factura a fin de mes |
| `row_version` | rowversion | No | automática | Control de concurrencia |

**Regla de cierre:** el ciclo de un pedido termina solo cuando `estado_pedido = entregado` y `estado_pago = pagado_total`.

Restricciones: `pk_pedidos`, `uq_pedidos_solicitud`, `fk_pedidos_solicitud`, `fk_pedidos_cotizacion`, `fk_pedidos_empleado`, `ck_pedidos_modalidad`, `ck_pedidos_costos`, `ck_pedidos_estado_pedido` (**NUEVA**, reemplaza a `ck_pedidos_estado`), `ck_pedidos_origen` (**NUEVA**), `ck_pedidos_monto_abonado` (**NUEVA**: el abono va de 0 al monto final).

Índices y la consulta que resuelve cada uno:
- `ix_pedidos_fecha_estado` (`fecha_pedido`, `estado_pedido`): filtros por año, mes y estado; pedidos por mes.
- `ix_pedidos_calendario_entrega` (`fecha_entrega_estimada`, `estado_pedido`): calendario de entregas.
- `ix_pedidos_cobranza` (`estado_pago`, `estado_pedido`, incluye `saldo_pendiente`): **NUEVO**, listado de cobranza (pedidos no anulados con pago `no_pagado` o `abonado`).
- El filtro por cliente usa `ix_solicitudes_cliente`, a través de la solicitud del pedido.

## horarios_atencion

Horario semanal publicado (RFS02). Una fila por día.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_horario` | int | No | autoincremental | PK |
| `dia_semana` | tinyint | No | | 1 = lunes … 7 = domingo. UQ. CHECK entre 1 y 7 |
| `nombre_dia` | nvarchar(15) | No | | Nombre del día |
| `hora_inicio`, `hora_fin` | time(0) | Sí | | Franja de atención. CHECK: si abre, ambas existen y el fin es posterior al inicio |
| `abierto` | bit | No | 1 | Si se atiende ese día |

Datos iniciales: lunes a viernes de 9:00 a 18:30, sábado de 9:00 a 14:00, domingo cerrado. Restricciones: `pk_horarios_atencion`, `uq_horarios_dia`, `ck_horarios_dia`, `ck_horarios_rango`.

## cartel_digital

**Cartel electrónico** del local (RFS02): el dueño o un trabajador lo «da vuelta» al salir a terreno o a una entrega. Tiene una sola fila.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_cartel` | int | No | 1 | PK. CHECK: siempre 1 |
| `abierto_en_local` | bit | No | 1 | 1 = «Abierto / Disponible» (verde); 0 = «Cerrado / Ausente» (rojo). La interfaz muestra además el texto, no solo el color |
| `mensaje_estado` | nvarchar(250) | No | `Local abierto y atendiendo` | Mensaje personalizado |
| `tiempo_estimado_regreso` | nvarchar(80) | Sí | | Hora estimada de regreso |
| `fecha_ultima_modificacion` | datetime2(0) | No | `SYSDATETIME()` | Última actualización |
| `id_empleado_modifica` | int | Sí | | FK a `empleados`: quién lo cambió |

Restricciones: `pk_cartel_digital`, `fk_cartel_digital_empleado`, `ck_cartel_unico`, `df_cartel_id`.

## rostros_empleado

Datos del ingreso facial (RFS03). Guarda solo el descriptor numérico cifrado y la constancia del consentimiento, nunca la fotografía (RNFE06).

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_rostro` | int | No | autoincremental | PK |
| `id_empleado` | int | No | | FK a `empleados` |
| `descriptor_cifrado` | varbinary(1024) | Sí | | Descriptor cifrado con AES-GCM. Se borra al desactivar |
| `fecha_consentimiento` | datetimeoffset(0) | No | | Cuándo dio su consentimiento expreso |
| `version_consentimiento` | varchar(20) | No | | Versión del texto aceptado |
| `fecha_revocacion` | datetimeoffset(0) | Sí | | Cuándo se desactivó |
| `intentos_fallidos` | int | No | | Intentos fallidos seguidos |
| `bloqueado_hasta` | datetimeoffset(0) | Sí | | Bloqueo temporal tras varios fallos |
| `token_equipo_hash` | varbinary(32) | Sí | | Hash del equipo autorizado |
| `fecha_autorizacion_equipo` | datetimeoffset(0) | Sí | | Cuándo se autorizó el equipo |

Restricciones: `pk_rostros_empleado`, `fk_rostros_empleado_empleado`. Índice único filtrado `uq_rostros_empleado_vigente` (`id_empleado` con `fecha_revocacion` nula): a lo más un rostro vigente por empleado.

---

## Vista, trigger y procedimiento

| Objeto | Tipo | Qué hace |
|---|---|---|
| `vw_metricas_conversion_online` | Vista | Por año y mes: solicitudes en línea, cuántas se concretaron en un pedido **no anulado** y el porcentaje de conversión. **Actualizada el 14-11-2026** (antes excluía los pedidos «cancelados») |
| `trg_cotizacion_materiales_recalcular_total` | Trigger | Tras insertar, modificar o borrar líneas de materiales, recalcula `costo_materiales_total` de las cotizaciones afectadas |
| `sp_depurar_solicitudes_no_concretadas` | Procedimiento | Borra solicitudes rechazadas o eliminadas, sin pedido, con más de `@meses_antiguedad` meses (12 por defecto, RNFO04) |

## Historial de cambios del esquema

| Migración | Fecha | Cambio |
|---|---|---|
| `InitialCreateV6` | 06-11-2026 | Tablas, restricciones, índices y datos iniciales |
| `ObjetosProgramablesV6` | 06-11-2026 | Trigger, vista, procedimiento, cartel único y domingo cerrado |
| `AgregarRostrosEmpleado` | 06-11-2026 | Tabla `rostros_empleado` |
| `SepararEstadoPedidoYPago` | 14-11-2026 | Pedido con estado operativo y de pago independientes, monto abonado, saldo, factura y origen; «cancelado» pasa a «anulado» |
