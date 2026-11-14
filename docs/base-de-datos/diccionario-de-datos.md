# Diccionario de datos — LetreriaLosHerreraDB

Estado del esquema: migración `CuentasYSesion` (13-11-2026), 21 tablas. Diagrama en [modelo-entidad-relacion.md](modelo-entidad-relacion.md); script de creación en [LetreriaLosHerreraDB.sql](LetreriaLosHerreraDB.sql).

**Convenciones**
- Motor SQL Server 2025 Express, intercalación `Modern_Spanish_CI_AS` (ordena y compara en español, sin distinguir mayúsculas).
- Nombres de tablas y columnas en español y en minúsculas con guion bajo. Prefijos de restricciones: `pk_` clave primaria, `fk_` clave foránea, `uq_` índice único, `ix_` índice, `ck_` restricción CHECK, `df_` valor por defecto.
- **PK** clave primaria (entera, autoincremental salvo que se diga otra cosa). **FK** clave foránea. **UQ** valor único.
- **Calculada**: columna que calcula SQL Server y guarda (`PERSISTED`); no se escribe a mano.
- **Montos en pesos chilenos sin decimales** (RNFO05). Las columnas nuevas usan `int`. Las anteriores son `decimal(12,2)` y siempre guardan valores enteros: la API y la interfaz validan que no tengan decimales.
- Fechas y horas en `datetime2(0)` (hora de Chile del servidor), salvo las del rostro, las de las tablas de la fase 4 (`correos_salientes`, `consentimientos`, `imagenes`) y las de la fase 5 (`consultas_contacto` y la autorización del logo en `catalogo`) y la fase 6 (`tokens_renovacion`, `codigos_cuenta` y `clientes.fecha_verificacion_correo`), que usan `datetimeoffset(0)`: deciden bloqueos, reintentos y constancias que no deben depender de la hora local.
- `row_version` es un `rowversion`: cambia en cada modificación y permite rechazar el cambio de un registro que otra persona modificó después de abrirlo (RNFP10).

Columnas marcadas **NUEVA** o **CAMBIA**: introducidas el 12-11-2026 por la revisión de la profesora guía. La tabla `pagos` (historial de abonos) también es de esa fecha. Las tablas `correos_salientes`, `consentimientos` e `imagenes` son de la fase 4 (13-11-2026). La tabla `consultas_contacto` y las columnas nuevas de `catalogo` e `imagenes` son de la fase 5 (13-11-2026). Las tablas `tokens_renovacion` y `codigos_cuenta` y la columna `clientes.fecha_verificacion_correo` son de la fase 6 (13-11-2026).

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
| `email` | nvarchar(100) | Sí | | Correo. UQ si existe. CHECK de formato. **CAMBIA (fase 6):** es obligatorio si hay contraseña, y es el usuario para ingresar y para recuperarla |
| `direccion` | nvarchar(255) | Sí | | Dirección |
| `password_hash` | varchar(100) | Sí | | Hash BCrypt de la contraseña. Nulo si no tiene cuenta. Solo puede haberlo con correo (`ck_clientes_cuenta_con_correo`, fase 6) |
| `registrado` | bit | — | Calculada: 1 si tiene `password_hash` | Indica si tiene cuenta |
| `fecha_registro` | datetime2(0) | No | `SYSDATETIME()` | Fecha de alta |
| `activo` | bit | No | 1 | **0 = cuenta inhabilitada** (borrado lógico). Cuando un cliente da de baja su cuenta no se borran sus pedidos: se conserva la integridad histórica y contable |
| `fecha_verificacion_correo` | datetimeoffset(0) | Sí | | **NUEVA (fase 6).** Cuándo confirmó el correo, con el enlace o con el código de recuperación. Nulo = sin confirmar. Cambiar el correo la vuelve a dejar nula |

Restricciones: `pk_clientes`, `uq_clientes_telefono`, `uq_clientes_rut` (filtrado: RUT no nulo), `uq_clientes_email` (filtrado: correo no nulo), `ck_clientes_email`, `ck_clientes_cuenta_con_correo` (fase 6).

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
| `rol` | nvarchar(20) | No | `administrador` | `administrador`, `dueño`, `diseñador` u `operario` (RNFO08). Dueño y administrador pueden lo mismo; diseñador y operario también (decisión del 12-11-2026) |
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
| `imagen_url` | nvarchar(500) | Sí | | **CAMBIA (fase 5).** Dirección de una foto anterior a la subida de imágenes. Ya no es obligatoria: la foto nueva va en `id_imagen` |
| `ancho_mm`, `alto_mm` | int | Sí | | Medidas en milímetros. CHECK: ambas nulas o ambas mayores que 0 |
| `precio_referencial` | decimal(12,2) | Sí | | Precio referencial en pesos. CHECK ≥ 0 |
| `fecha_publicacion` | datetime2(0) | No | `SYSDATETIME()` | Fecha de publicación |
| `visible` | bit | No | 1 | 1 = se muestra en la vitrina. Con logo de cliente, solo puede ser 1 si hay autorización (`ck_catalogo_logo`) |
| `id_imagen` | int | Sí | | **NUEVA (fase 5).** FK a `imagenes`: la foto subida con `POST /imagenes`. Sin foto, la vitrina muestra «Foto del trabajo pendiente» |
| `incluye_logo_cliente` | bit | No | 0 | **NUEVA (fase 5).** 1 = la foto muestra el logotipo de un cliente |
| `id_empleado_autoriza_logo` | int | Sí | | **NUEVA (fase 5).** FK a `empleados`: quién registró la autorización del cliente |
| `fecha_autorizacion_logo` | datetimeoffset(0) | Sí | | **NUEVA (fase 5).** Cuándo se registró la autorización. Volver a autorizarla no la cambia |

Restricciones: `pk_catalogo`, `fk_catalogo_producto`, `fk_catalogo_empleado`, `fk_catalogo_imagen`, `fk_catalogo_empleado_autoriza_logo` (sin borrado en cascada), `ck_catalogo_precio`, `ck_catalogo_medidas`, `ck_catalogo_logo` (un logo de cliente no se muestra sin autorización) y `ck_catalogo_autorizacion_logo` (quién y cuándo van juntos: ambos nulos o ambos con valor). Índices: `ix_catalogo_visible_producto` (`visible`, `id_producto`), la consulta pública de la vitrina; `ix_catalogo_imagen` (`id_imagen`) e `ix_catalogo_autoriza_logo` (`id_empleado_autoriza_logo`), para las claves foráneas de la fase 5. Razón del cambio de `imagen_url`: la vitrina ya no exige una dirección de archivo, porque las fotos se suben con `POST /imagenes`.

## solicitudes

Cabecera de cada pedido de trabajo de un cliente, en línea o en el local (RFS04).

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_solicitud` | int | No | autoincremental | PK |
| `id_cliente` | int | No | | FK a `clientes` |
| `id_empleado_registra` | int | Sí | | FK a `empleados`: quién la registró en el local. Nulo si la envió el cliente |
| `origen` | nvarchar(15) | — | Calculada: `online` si no la registró un empleado; si no, `presencial` | Origen |
| `descripcion_general` | nvarchar(2000) | Sí | | Especificaciones generales. **Fase 7:** hasta 2000 caracteres (antes 500). La API exige de 10 a 2000 |
| `fecha_solicitud` | datetime2(0) | No | `SYSDATETIME()` | Fecha y hora automáticas, en hora de Chile |
| `estado` | nvarchar(15) | No | `solicitado` | `solicitado`, `cotizado`, `aceptado` o `rechazado` (`ck_solicitudes_estado`). Las transiciones las valida la API; cada cambio queda en `solicitudes_historial` |
| `motivo_rechazo` | nvarchar(500) | Sí | | Obligatorio al rechazar (lo exige la API, de 10 a 500 caracteres). **Fase 7:** hasta 500 (antes 250) |
| `eliminado_logico` | bit | No | 0 | 1 = eliminada; se conserva para estadísticas (RNFO04) |
| `fecha_eliminacion_logica` | datetime2(0) | Sí | | Cuándo se eliminó |
| `row_version` | rowversion | No | automática | Control de concurrencia: la API lo recibe en `If-Match` y lo compara al editar o cambiar el estado |
| `huella_ip` | char(64) | Sí | | **Nueva (fase 7).** HMAC-SHA256 de la IP de envío con una clave del servidor (`Solicitudes:ClaveHuella` o una derivada de `Jwt:Key`). En IPv6 se usa el prefijo /64. Nunca se guarda la IP en claro. Solo la usa la regla contra solicitudes falsas; es nula en las presenciales |

Restricciones: `pk_solicitudes`, `fk_solicitudes_cliente`, `fk_solicitudes_empleado`, `ck_solicitudes_estado`. Índices: `ix_solicitudes_metricas` (`fecha_solicitud`, `origen`, `estado`, `eliminado_logico`) para indicadores y bandeja; `ix_solicitudes_cliente` (`id_cliente`, `eliminado_logico`) para el historial de un cliente; `ix_solicitudes_huella_ip` (`huella_ip`, `fecha_solicitud`, filtrado a las no nulas), que busca las solicitudes de la última hora (configurable) de una misma IP para la regla contra solicitudes falsas.

## solicitud_detalles

Ítems de una solicitud.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_solicitud_detalle` | int | No | autoincremental | PK |
| `id_solicitud` | int | No | | FK a `solicitudes` (se borra con ella) |
| `id_producto` | int | No | | FK a `productos`: tipo de letrero |
| `descripcion` | nvarchar(500) | No | | Glosa de especificaciones. **Fase 7:** hasta 500 caracteres (antes 300). Sigue siendo obligatoria: sin descripción se guarda vacía (`""`) |
| `ancho_mm`, `alto_mm` | int | Sí | | Medidas en milímetros. CHECK: ambas nulas o ambas mayores que 0 |
| `cantidad` | int | No | 1 | Cantidad. CHECK > 0 |
| `area_m2` | decimal(12,4) | — | Calculada: ancho × alto / 1.000.000 | Área en m² |

Restricciones: `pk_solicitud_detalles`, `fk_sol_detalles_solicitud` (en cascada), `fk_sol_detalles_producto`, `ck_sol_detalles_cantidad`, `ck_sol_detalles_medidas`. Índices: `ix_sol_detalles_solicitud`, `ix_sol_detalles_producto`.

## solicitudes_historial

**Tabla nueva (14-11-2026, fase 7).** Cada cambio de estado de una solicitud, y también su creación (`estado_anterior` nulo). Dice quién lo hizo: un empleado, el cliente (cuando envió la solicitud en línea) o nadie, cuando lo hace el sistema (por ejemplo, al cotizar). Quitar un adjunto también queda anotado aquí, sin cambio de estado (`estado_anterior` igual a `estado_nuevo`, con el motivo «Se quitó el adjunto …»).

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_historial` | int | No | autoincremental | PK |
| `id_solicitud` | int | No | | FK a `solicitudes`, en cascada: el historial se va con la solicitud |
| `estado_anterior` | nvarchar(15) | Sí | | Estado antes del cambio. Nulo al crearse la solicitud |
| `estado_nuevo` | nvarchar(15) | No | | Estado después del cambio |
| `motivo` | nvarchar(500) | Sí | | Motivo del rechazo, o la nota del cambio (por ejemplo, al editar una solicitud cotizada) |
| `id_empleado` | int | Sí | | FK a `empleados`: quién hizo el cambio, si fue el personal |
| `id_cliente` | int | Sí | | FK a `clientes`: quién hizo el cambio, si fue el cliente (envío en línea) |
| `fecha` | datetimeoffset(0) | No | | Cuándo ocurrió |

Si `id_empleado` e `id_cliente` están nulos, el cambio lo hizo el sistema.

Restricciones: `pk_solicitudes_historial`, `fk_solicitudes_historial_solicitud` (en cascada), `fk_solicitudes_historial_empleado` y `fk_solicitudes_historial_cliente` (sin acción en cascada), `ck_solicitudes_historial_estados` (los dos estados son de los cuatro válidos) y `ck_solicitudes_historial_actor` (nunca un cliente y un empleado a la vez).

Índices: `ix_solicitudes_historial_solicitud` (`id_solicitud`, `fecha`), `IX_solicitudes_historial_id_empleado` e `IX_solicitudes_historial_id_cliente`.

## adjuntos_solicitud

**Tabla nueva (14-11-2026, fase 7).** Una fila por archivo: el logo o diseño que el cliente adjunta a su solicitud, o el que el personal agrega desde la ficha. Mientras `id_solicitud` es nulo, es un **borrador** del cliente: se vincula a la solicitud al enviarla, en la misma transacción. Los archivos están en una carpeta privada del servidor (`Adjuntos:Carpeta`, `almacen/adjuntos`), fuera del repositorio. El archivo se nombra con `clave`, que nunca se muestra, y solo se sirve con sesión (`GET /api/v1/adjuntos/{id}/miniatura` y `/archivo`).

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_adjunto` | int | No | autoincremental | PK |
| `id_solicitud` | int | Sí | | FK a `solicitudes`, en cascada. Nulo mientras es un borrador |
| `id_cliente` | int | Sí | | FK a `clientes`: quién lo subió, si fue el cliente |
| `id_empleado` | int | Sí | | FK a `empleados`: quién lo subió, si fue el personal |
| `clave` | char(32) | No | aleatoria (128 bits) | Nombre de los archivos en disco. Único (`uq_adjuntos_solicitud_clave`) |
| `tipo` | varchar(10) | No | | `imagen` o `pdf` |
| `nombre_original` | nvarchar(120) | No | | Nombre saneado: sin rutas ni caracteres de control, de hasta 120 caracteres. Solo se muestra |
| `bytes` | int | No | | Tamaño del archivo subido, antes de convertirlo. Mayor que 0 |
| `ancho`, `alto` | int | Sí | | Medidas de la imagen en píxeles. Nulas en un PDF |
| `fecha_subida` | datetimeoffset(0) | No | | Cuándo se subió |
| `fecha_vinculo` | datetimeoffset(0) | Sí | | Cuándo se vinculó a la solicitud. Está presente si y solo si `id_solicitud` está presente |

Exactamente uno de `id_cliente` e `id_empleado` tiene valor.

Restricciones: `pk_adjuntos_solicitud`, `fk_adjuntos_solicitud_solicitud` (en cascada), `fk_adjuntos_solicitud_cliente` y `fk_adjuntos_solicitud_empleado` (sin acción en cascada); `ck_adjuntos_solicitud_autor`, `ck_adjuntos_solicitud_vinculo`, `ck_adjuntos_solicitud_tipo`, `ck_adjuntos_solicitud_bytes` y `ck_adjuntos_solicitud_medidas` (una imagen tiene medidas; un PDF, no).

Índices: `uq_adjuntos_solicitud_clave` (único), `ix_adjuntos_solicitud_solicitud` (`id_solicitud`, filtrado a las no nulas), `ix_adjuntos_solicitud_borrador` (`id_cliente`, `fecha_subida`, filtrado a los borradores) e `IX_adjuntos_solicitud_id_empleado`.

**Límites y retención:** 10 MB por archivo y 5 por solicitud; el tope se revisa al subir y otra vez al vincular. Un borrador que pasa 24 horas sin enviarse se borra junto con su archivo (`PurgaDatosPersonales`, una vez al día). Si se borra una solicitud, sus filas se van en cascada; sus archivos quedan en disco hasta la purga, que borra los que ya no tienen registro.

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

Trabajo acordado (RFS06). **Tabla modificada el 12-11-2026:** el estado operativo y el estado de pago pasan a ser independientes.

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
| `monto_abonado` | int | No | 0 | **NUEVA.** Total pagado hasta ahora, en pesos: la suma de los `pagos` del pedido. La mantiene el trigger `trg_pagos_recalcular_monto_abonado`; no se escribe a mano |
| `saldo_pendiente` | int | — | Calculada: monto final − monto abonado | **NUEVA.** Lo que falta por cobrar |
| `estado_pago` | nvarchar(12) | — | Calculada: `no_pagado` si el abono es 0; `pagado_total` si cubre el monto final; si no, `abonado` | **NUEVA.** Estado financiero, independiente del operativo |
| `numero_factura` | nvarchar(30) | Sí | | **NUEVA.** Número de factura cuando se paga. No es único: un cliente habitual puede pagar varios pedidos con una sola factura a fin de mes |
| `row_version` | rowversion | No | automática | Control de concurrencia |

**Regla de cierre:** el ciclo de un pedido termina solo cuando `estado_pedido = entregado` y `estado_pago = pagado_total`.

Restricciones: `pk_pedidos`, `uq_pedidos_solicitud`, `fk_pedidos_solicitud`, `fk_pedidos_cotizacion`, `fk_pedidos_empleado`, `ck_pedidos_modalidad`, `ck_pedidos_costos`, `ck_pedidos_estado_pedido` (**NUEVA**, reemplaza a `ck_pedidos_estado`), `ck_pedidos_origen` (**NUEVA**), `ck_pedidos_monto_abonado` (**NUEVA**: el abono va de 0 al monto final), `ck_pedidos_montos_enteros` (**NUEVA**: monto base e instalación en pesos sin decimales y con un total de hasta 2.000 millones; sin esta regla, el saldo, que es entero, y el estado de pago podrían contradecirse).

Índices y la consulta que resuelve cada uno:
- `ix_pedidos_fecha_estado` (`fecha_pedido`, `estado_pedido`): filtros por año, mes y estado; pedidos por mes.
- `ix_pedidos_calendario_entrega` (`fecha_entrega_estimada`, `estado_pedido`): calendario de entregas.
- `ix_pedidos_cobranza` (`estado_pago`, `estado_pedido`, incluye `saldo_pendiente`): **NUEVO**, listado de cobranza (pedidos no anulados con pago `no_pagado` o `abonado`).
- El filtro por cliente usa `ix_solicitudes_cliente`, a través de la solicitud del pedido.

## pagos

**Tabla nueva (12-11-2026).** Historial de abonos y pagos de cada pedido (RFS06, E25): un abono inicial, otros parciales y el pago final quedan cada uno con su fecha y su medio de pago.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_pago` | int | No | autoincremental | PK |
| `id_pedido` | int | No | | FK a `pedidos` |
| `monto` | int | No | | Pesos pagados en este abono. CHECK: mayor que 0 |
| `fecha_pago` | date | No | hoy | Día en que el cliente pagó; puede ser anterior al día del registro |
| `medio_pago` | nvarchar(15) | No | | `efectivo`, `transferencia`, `tarjeta_debito`, `tarjeta_credito`, `cheque` u `otro` |
| `observacion` | nvarchar(300) | Sí | | Nota libre, por ejemplo el número de la transferencia |
| `id_empleado_registra` | int | No | | FK a `empleados`: quién registró el abono |
| `fecha_registro` | datetime2(0) | No | `SYSDATETIME()` | Cuándo se registró |

**Regla:** después de insertar, modificar o borrar un pago, el trigger `trg_pagos_recalcular_monto_abonado` deja `pedidos.monto_abonado` igual a la suma de los pagos del pedido. Para eso suma al pedido la diferencia de cada operación (lo insertado menos lo borrado), sin volver a leer toda la tabla: dos abonos simultáneos al mismo pedido no se interbloquean, y el segundo suma sobre el total ya confirmado del primero, también si la base activa el aislamiento por instantáneas. Solo escribe el pedido si el total cambió: corregir la observación de un abono no invalida la versión del pedido que otra persona tiene abierta. Nadie más escribe `monto_abonado` (la API la ignora al guardar). Si esa suma supera el monto final, `ck_pedidos_monto_abonado` rechaza la operación completa y el pago no se guarda. Así `estado_pago` y `saldo_pendiente` siempre coinciden con el historial.

Restricciones: `pk_pagos`, `fk_pagos_pedido`, `fk_pagos_empleado`, `ck_pagos_monto`, `ck_pagos_medio_pago`.

Índices y la consulta que resuelve cada uno:
- `ix_pagos_pedido_fecha` (`id_pedido`, `fecha_pago`): historial de un pedido en orden de fecha; también cubre la clave foránea y la suma que hace el trigger.
- `IX_pagos_id_empleado_registra`: clave foránea al empleado (lo crea EF Core por convención, como en las demás tablas).

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

## correos_salientes

**Tabla nueva (13-11-2026, fase 4).** Cola de salida del correo (RFS08). Quien origina un correo (por ejemplo, una solicitud nueva que avisa al taller) solo inserta aquí una fila; un proceso en segundo plano la envía después, con reintentos. Así un servidor de correo lento o caído nunca hace esperar ni falla la petición.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_correo` | bigint | No | autoincremental | PK |
| `tipo` | varchar(40) | No | | `aviso_solicitud_nueva`, `prueba`, … Desde la fase 6, también `verificacion_correo`, `recuperacion_cuenta` y `aviso_cambio_correo` (correos de la cuenta) |
| `referencia` | varchar(64) | Sí | | A qué se refiere, sin abrir el cuerpo: por ejemplo `solicitud:123` |
| `destinatarios` | nvarchar(1000) | No | | Direcciones separadas por punto y coma, validadas al encolar |
| `responder_a` | nvarchar(254) | Sí | | A quién va la respuesta (en el aviso de solicitud, el correo del cliente) |
| `asunto` | nvarchar(200) | No | | Sin saltos de línea |
| `cuerpo_html` | nvarchar(max) | No | | Versión HTML (plantilla base) |
| `cuerpo_texto` | nvarchar(max) | No | | Versión de texto plano |
| `estado` | varchar(10) | No | `pendiente` | `pendiente`, `enviando`, `enviado` o `fallido` |
| `intentos` | int | No | | Intentos hechos. Se suma al tomar el correo, antes de enviarlo |
| `proximo_intento` | datetimeoffset(0) | No | | Desde cuándo puede intentarse. Tras un fallo: 1, 5, 15, 60 y 180 minutos |
| `bloqueado_hasta` | datetimeoffset(0) | Sí | | Mientras un proceso lo envía nadie más lo toma; si ese proceso se cae, al vencer se retoma |
| `ultimo_error` | nvarchar(200) | Sí | | Categoría y mensaje fijo en español (por ejemplo `tiempo_agotado: …`). **Nunca** el texto del servidor, que puede repetir el usuario de la cuenta o una dirección |
| `fecha_creacion` | datetimeoffset(0) | No | `SYSDATETIMEOFFSET()` | Cuándo se encoló |
| `fecha_envio` | datetimeoffset(0) | Sí | | Cuándo se entregó |

Restricciones: `pk_correos_salientes`, `ck_correos_salientes_estado`, `ck_correos_salientes_intentos` (`intentos >= 0`).

Índices:
- `ix_correos_salientes_pendientes` (`estado`, `proximo_intento`), filtrado a `pendiente` y `enviando`: lo que busca el proceso en cada vuelta. Solo indexa lo que falta enviar.
- `ix_correos_salientes_referencia` (`referencia`), filtrado a los no nulos: encontrar el correo de una solicitud.
- `ix_correos_salientes_fecha` (`fecha_creacion`): la purga de lo antiguo.

**Retención:** lo enviado o fallido se borra a los 30 días (`Correo:Cola:DiasRetencion`), porque el cuerpo lleva datos personales del cliente.

## consentimientos

**Tabla nueva (13-11-2026, fase 4).** Registro de consentimientos (RFS12): quién aceptó, qué documento, en qué versión y cuándo; y, si lo retiró, cuándo. Solo se inserta y se marca la revocación: nunca se borra ni se edita otra cosa. El consentimiento del rostro tiene su propio registro en `rostros_empleado`.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_consentimiento` | int | No | autoincremental | PK |
| `id_cliente` | int | Sí | | FK a `clientes`, si el titular es un cliente |
| `id_empleado` | int | Sí | | FK a `empleados`, si el titular es del personal |
| `referencia_envio` | varchar(64) | Sí | | Si es un visitante sin cuenta: el envío que hizo, por ejemplo `contacto:15` |
| `tipo` | varchar(20) | No | | `privacidad`, `terminos`, `marketing` o `cookies` |
| `version_documento` | varchar(20) | No | | Versión del documento que la persona leyó. Si el texto cambia, sube la versión y debe aceptarse de nuevo |
| `fecha_aceptacion` | datetimeoffset(0) | No | `SYSDATETIMEOFFSET()` | Cuándo aceptó |
| `fecha_revocacion` | datetimeoffset(0) | Sí | | Cuándo lo retiró |

Restricciones: `pk_consentimientos`, `fk_consentimientos_cliente`, `fk_consentimientos_empleado` (sin cascada: la constancia sobrevive a la inhabilitación de la cuenta), `ck_consentimientos_tipo`, `ck_consentimientos_titular` (exactamente uno de `id_cliente`, `id_empleado` o `referencia_envio`), `ck_consentimientos_revocacion` (la revocación no es anterior a la aceptación).

Índices: `ix_consentimientos_cliente` (`id_cliente`, `tipo`), `ix_consentimientos_empleado` (`id_empleado`, `tipo`) e `ix_consentimientos_envio` (`referencia_envio`, `tipo`), cada uno filtrado a sus filas no nulas: responden «¿qué tiene vigente este titular?» y cubren las claves foráneas.

## imagenes

**Tabla nueva (13-11-2026, fase 4).** Imágenes subidas por el taller (RNFP22). Los archivos se guardan fuera de la base, en la carpeta `Imagenes:Carpeta`, como `{clave}-grande.webp` (1600 px por el lado mayor) y `{clave}-miniatura.webp` (640 px). Aquí queda quién la subió, cuándo y sus medidas.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_imagen` | int | No | autoincremental | PK |
| `clave` | char(32) | No | | UQ. Aleatoria (128 bits): una imagen aún no publicada, como un logotipo sin la autorización del cliente, no se encuentra probando números |
| `ancho` | int | No | | Ancho de la original, ya enderezada |
| `alto` | int | No | | Alto de la original, ya enderezada |
| `bytes_grande` | int | No | | Peso de la versión grande |
| `bytes_miniatura` | int | No | | Peso de la miniatura |
| `es_ejemplo` | bit | No | 0 | **NUEVA (fase 5).** 1 = ilustración de ejemplo de la base demo. La vitrina la rotula «Imagen de ejemplo» |
| `id_empleado_sube` | int | No | | FK a `empleados` |
| `fecha_subida` | datetimeoffset(0) | No | `SYSDATETIMEOFFSET()` | |

Restricciones: `pk_imagenes`, `fk_imagenes_empleado`, `uq_imagenes_clave`, `ck_imagenes_medidas` (medidas y pesos mayores que cero). Índice `ix_imagenes_empleado` para la clave foránea.

---

## consultas_contacto

**Tabla nueva (13-11-2026, fase 5).** Consultas del formulario de contacto de la portada (RFS08, E22). Cada una deja un aviso por correo al taller, y la bandeja para revisarlas llega en la fase 9. Su consentimiento de privacidad queda en `consentimientos`, con la referencia `contacto:{id}`.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_consulta` | int | No | autoincremental | PK |
| `nombre` | nvarchar(100) | No | | Nombre de quien escribe, sin espacios sobrantes |
| `correo` | nvarchar(254) | No | | Correo para responder. Validado al recibir la consulta |
| `telefono` | nvarchar(20) | Sí | | Opcional. Se guarda como `+56` seguido de 9 dígitos (formato `+569XXXXXXXX` para celulares), aunque se escriba con espacios |
| `asunto` | nvarchar(150) | No | | Asunto de la consulta |
| `mensaje` | nvarchar(2000) | No | | Texto de la consulta, sin espacios sobrantes al inicio ni al final |
| `fecha_recepcion` | datetimeoffset(0) | No | `SYSDATETIMEOFFSET()` | Cuándo llegó |
| `revisada` | bit | No | 0 | 1 = el taller ya la revisó (lo marca la bandeja de la fase 9) |
| `fecha_revision` | datetimeoffset(0) | Sí | | Cuándo se marcó como revisada |

Restricciones: `pk_consultas_contacto`. No tiene `CHECK` ni claves foráneas: las reglas de largo y de formato están en la API (`ValidacionContacto`) y en el formulario.

Índice: `ix_consultas_contacto_bandeja` (`revisada`, `fecha_recepcion`): las consultas sin revisar, de la más reciente a la más antigua.

La misma consulta (mismo correo y mismo mensaje) enviada otra vez dentro de la hora no se guarda de nuevo.

**Retención:** todavía sin plazo de borrado. El taller lo fija con su asesor legal antes de la entrega (ver `docs/plan/desviaciones.md`).

---

## tokens_renovacion

**Tabla nueva (13-11-2026, fase 6).** Sesiones de la API (RNFP08, RFS03). Cada ingreso abre una **familia** (un `uniqueidentifier`). Cada renovación marca como usado el token que recibió y emite otro de la misma familia con la misma `fecha_expiracion`, así que renovar no alarga la sesión. Solo se guarda el hash SHA-256 del token: el valor viaja en la cookie `synctime_renovacion` y nunca se escribe en la base.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_token` | bigint | No | autoincremental | PK |
| `id_cliente` | int | Sí | | FK a `clientes`, si el titular es un cliente |
| `id_empleado` | int | Sí | | FK a `empleados`, si el titular es del personal |
| `hash_token` | char(64) | No | | SHA-256 del token en hexadecimal. UQ: cada renovación busca por este valor |
| `familia` | uniqueidentifier | No | | Un inicio de sesión. Cerrar una sesión revoca toda su familia |
| `fecha_creacion` | datetimeoffset(0) | No | | Cuándo se emitió |
| `fecha_expiracion` | datetimeoffset(0) | No | | Hasta cuándo dura la sesión, contada desde el ingreso: 12 horas para el personal (`Sesion:HorasPersonal`) y 7 días para el cliente (`Sesion:DiasCliente`) |
| `fecha_uso` | datetimeoffset(3) | Sí | | Cuándo se usó para renovar. Un token ya usado no sirve: si vuelve a presentarse, se revoca su familia |
| `fecha_revocacion` | datetimeoffset(0) | Sí | | Cuándo se cerró la sesión: al salir, al cambiar o restablecer la contraseña, al desactivar la cuenta o por reutilización |

Restricciones: `pk_tokens_renovacion`, `fk_tokens_renovacion_cliente` y `fk_tokens_renovacion_empleado` (con `ON DELETE CASCADE`), y `ck_tokens_renovacion_titular` (exactamente uno de `id_cliente` o `id_empleado`).

Índices: `uq_tokens_renovacion_hash` (único), `ix_tokens_renovacion_familia`, `ix_tokens_renovacion_expiracion`, y `ix_tokens_renovacion_cliente` e `ix_tokens_renovacion_empleado`, filtrados a los no nulos.

**Retención:** las filas vencidas hace más de un día se borran con `PurgaDatosPersonales`, una vez al día.

---

## codigos_cuenta

**Tabla nueva (13-11-2026, fase 6).** Códigos de un solo uso de las cuentas (RNFP13, E02). Hay dos propósitos: `verificacion_correo`, un enlace que confirma el correo del cliente (24 horas), y `recuperacion`, un código de 6 dígitos para cambiar la contraseña (15 minutos). Solo se guarda el hash. El del enlace es SHA-256, porque el enlace ya tiene 256 bits al azar. El del código es HMAC-SHA256 con una clave del servidor (`Jwt:Key`): con solo 6 dígitos, un hash sin clave se revierte probando todas las combinaciones.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_codigo` | bigint | No | autoincremental | PK |
| `id_cliente` | int | Sí | | FK a `clientes`, si el titular es un cliente |
| `id_empleado` | int | Sí | | FK a `empleados`, si es la recuperación del personal |
| `proposito` | varchar(20) | No | | `verificacion_correo` o `recuperacion`. CHECK |
| `hash_codigo` | char(64) | No | | Hash del enlace o del código (ver arriba). Índice no único |
| `correo` | nvarchar(254) | No | | Correo al que se envió. Solo vale mientras sea el correo actual de la cuenta |
| `fecha_creacion` | datetimeoffset(0) | No | | Cuándo se envió |
| `fecha_expiracion` | datetimeoffset(0) | No | | 24 horas para la verificación; 15 minutos para la recuperación |
| `fecha_uso` | datetimeoffset(0) | Sí | | Cuándo se usó o se anuló. Un código nuevo anula el anterior, y cambiar el correo anula los pendientes |
| `intentos` | int | No | 0 | Intentos de código hechos. Al llegar a 5 el código deja de servir, aunque después se escriba bien |

Restricciones: `pk_codigos_cuenta`, `fk_codigos_cuenta_cliente` y `fk_codigos_cuenta_empleado` (con `ON DELETE CASCADE`), `ck_codigos_cuenta_proposito`, `ck_codigos_cuenta_titular` (exactamente uno de `id_cliente` o `id_empleado`) e `ck_codigos_cuenta_intentos` (`intentos >= 0`).

Índices: `ix_codigos_cuenta_hash` (el enlace se busca por su hash), `ix_codigos_cuenta_expiracion`, y `ix_codigos_cuenta_cliente` e `ix_codigos_cuenta_empleado` (por titular y propósito, filtrados a los no nulos).

Límites de envío, en la API y no en la base: como máximo 1 código por minuto y 5 por hora por cuenta. Si se supera, no se envía nada y la respuesta es la misma.

**Retención:** las filas vencidas hace más de un día se borran con `PurgaDatosPersonales`, una vez al día.

---

## Vista, triggers y procedimiento

| Objeto | Tipo | Qué hace |
|---|---|---|
| `vw_metricas_conversion_online` | Vista | Por año y mes: solicitudes en línea, cuántas se concretaron en un pedido **no anulado** y el porcentaje de conversión. **Actualizada el 12-11-2026** (antes excluía los pedidos «cancelados») |
| `trg_cotizacion_materiales_recalcular_total` | Trigger | Tras insertar, modificar o borrar líneas de materiales, recalcula `costo_materiales_total` de las cotizaciones afectadas |
| `trg_pagos_recalcular_monto_abonado` | Trigger | **Nuevo (12-11-2026).** Tras insertar, modificar o borrar pagos, ajusta `monto_abonado` de los pedidos afectados con la diferencia, de modo que siempre sea la suma de sus pagos |
| `sp_depurar_solicitudes_no_concretadas` | Procedimiento | Borra solicitudes rechazadas o eliminadas, sin pedido, con más de `@meses_antiguedad` meses (12 por defecto, RNFO04) |

## Historial de cambios del esquema

| Migración | Fecha | Cambio |
|---|---|---|
| `InitialCreateV6` | 06-11-2026 | Tablas, restricciones, índices y datos iniciales |
| `ObjetosProgramablesV6` | 06-11-2026 | Trigger, vista, procedimiento, cartel único y domingo cerrado |
| `AgregarRostrosEmpleado` | 06-11-2026 | Tabla `rostros_empleado` |
| `SepararEstadoPedidoYPago` | 12-11-2026 | Pedido con estado operativo y de pago independientes, monto abonado, saldo, factura y origen; «cancelado» pasa a «anulado» |
| `HistorialDePagos` | 12-11-2026 | Tabla `pagos` (historial de abonos con fecha, medio y quién lo registró) y su trigger; restricción `ck_pedidos_montos_enteros`. Si algún pedido ya tenía un monto abonado, se conserva como un pago inicial. Si alguno tuviera centavos, la migración se detiene sin cambiar nada |
| `ServiciosComunes` | 13-11-2026 | Tablas nuevas `correos_salientes` (cola de correo), `consentimientos` e `imagenes`. Solo crea tablas: no toca datos ni tablas existentes |
| `PortadaPublica` | 13-11-2026 | Tabla nueva `consultas_contacto`. En `catalogo`, `imagen_url` deja de ser obligatoria y se agregan `id_imagen`, `incluye_logo_cliente`, `id_empleado_autoriza_logo` y `fecha_autorizacion_logo`, con sus claves foráneas, las restricciones `ck_catalogo_logo` y `ck_catalogo_autorizacion_logo` e índices. En `imagenes`, `es_ejemplo`. No borra tablas, columnas ni filas (prueba `PortadaPublica_SoloAgrega`) |
| `CuentasYSesion` | 13-11-2026 | Tablas nuevas `tokens_renovacion` (sesiones; solo se guarda el hash del token) y `codigos_cuenta` (enlaces de verificación y códigos de recuperación; solo el hash). En `clientes`, la columna `fecha_verificacion_correo` y la restricción `ck_clientes_cuenta_con_correo`, que impide una contraseña sin correo: si algún cliente tuviera contraseña sin correo, la migración se detiene. En la base real no hay clientes (consultado el 13-11-2026). Solo agrega (prueba `CuentasYSesion_SoloAgrega`). Aplicada en la base demo; no en la real |
| `SolicitudesYAdjuntos` | 14-11-2026 | Tablas nuevas `adjuntos_solicitud` (archivos de las solicitudes, privados) y `solicitudes_historial` (cambios de estado). En `solicitudes`, la columna nueva `huella_ip` con su índice `ix_solicitudes_huella_ip`, y se amplían `descripcion_general` (2000), `motivo_rechazo` (500) y `solicitud_detalles.descripcion` (500). No borra tablas, columnas ni filas (prueba `SolicitudesYAdjuntos_SoloAgrega`). Aplicada en la base demo; no en la real |
