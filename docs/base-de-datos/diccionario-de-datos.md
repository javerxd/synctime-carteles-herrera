# Diccionario de datos — LetreriaLosHerreraDB

Estado del esquema: migración `MantenedoresYAvisos` (14-11-2026), 27 tablas. Aplicada en la base demo; la base real todavía no tiene las migraciones nuevas (se aplican todas juntas al terminar las fases, con respaldo). Diagrama en [modelo-entidad-relacion.md](modelo-entidad-relacion.md); script de creación en [LetreriaLosHerreraDB.sql](LetreriaLosHerreraDB.sql).

**Convenciones**
- Motor SQL Server 2025 Express, intercalación `Modern_Spanish_CI_AS` (ordena y compara en español, sin distinguir mayúsculas).
- Nombres de tablas y columnas en español y en minúsculas con guion bajo. Prefijos de restricciones: `pk_` clave primaria, `fk_` clave foránea, `uq_` índice único, `ix_` índice, `ck_` restricción CHECK, `df_` valor por defecto.
- **PK** clave primaria (entera, autoincremental salvo que se diga otra cosa). **FK** clave foránea. **UQ** valor único.
- **Calculada**: columna que calcula SQL Server y guarda (`PERSISTED`); no se escribe a mano.
- **Montos en pesos chilenos sin decimales** (RNFO05). Las columnas nuevas usan `int`. Las anteriores son `decimal(12,2)` y siempre guardan valores enteros: la API y la interfaz validan que no tengan decimales.
- Fechas y horas en `datetime2(0)` (hora de Chile del servidor), salvo las del rostro, las de las tablas de la fase 4 (`correos_salientes`, `consentimientos`, `imagenes`) y las de la fase 5 (`consultas_contacto` y la autorización del logo en `catalogo`) y la fase 6 (`tokens_renovacion`, `codigos_cuenta` y `clientes.fecha_verificacion_correo`), que usan `datetimeoffset(0)`: deciden bloqueos, reintentos y constancias que no deben depender de la hora local. Desde la fase 9 también lo usan `destinatarios_avisos`, `destinatarios_avisos_historial` y `horarios_atencion.fecha_modificacion`; `disponibilidad_empleados` usa `time(0)`.
- `row_version` es un `rowversion`: cambia en cada modificación y permite rechazar el cambio de un registro que otra persona modificó después de abrirlo (RNFP10).

Columnas marcadas **NUEVA** o **CAMBIA**: introducidas el 12-11-2026 por la revisión de la profesora guía. La tabla `pagos` (historial de abonos) también es de esa fecha. Las tablas `correos_salientes`, `consentimientos` e `imagenes` son de la fase 4 (13-11-2026). La tabla `consultas_contacto` y las columnas nuevas de `catalogo` e `imagenes` son de la fase 5 (13-11-2026). Las tablas `tokens_renovacion` y `codigos_cuenta` y la columna `clientes.fecha_verificacion_correo` son de la fase 6 (13-11-2026). Las de la fase 8 (14-11-2026) se marcan **NUEVA (fase 8)** o **CAMBIA (fase 8)**, y la tabla `pedidos_historial` también es de esa fecha. Las de la fase 9 (14-11-2026) se marcan **NUEVA (fase 9)** o **CAMBIA (fase 9)**: `row_version` en los seis mantenedores, las columnas nuevas de `horarios_atencion` y las tablas `disponibilidad_empleados`, `destinatarios_avisos` y `destinatarios_avisos_historial`. La tabla `trabajos_exportacion` es de la fase 13A (20-11-2026). La tabla `identidades_externas` es de la fase 14A (21-11-2026).

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
| `password_hash` | varchar(100) | Sí | | Hash BCrypt de la contraseña. Nulo si no tiene cuenta, **y también si la cuenta se creó con Google y todavía no creó una contraseña (fase 14A, ver `identidades_externas`)**. Solo puede haberlo con correo (`ck_clientes_cuenta_con_correo`, fase 6) |
| `registrado` | bit | — | Calculada: 1 si tiene `password_hash` | Indica si tiene contraseña. Una cuenta de Google sin contraseña vale 0: para saber si hay cuenta web, la API mira también `identidades_externas` |
| `fecha_registro` | datetime2(0) | No | `SYSDATETIME()` | Fecha de alta |
| `activo` | bit | No | 1 | **0 = cuenta inhabilitada** (borrado lógico). Cuando un cliente da de baja su cuenta no se borran sus pedidos: se conserva la integridad histórica y contable |
| `fecha_verificacion_correo` | datetimeoffset(0) | Sí | | **NUEVA (fase 6).** Cuándo confirmó el correo, con el enlace o con el código de recuperación. Nulo = sin confirmar. Cambiar el correo la vuelve a dejar nula |
| `row_version` | rowversion | No | automática | **NUEVA (fase 9).** Control de concurrencia (RNFP10): la API la recibe en `If-Match` al editar o desactivar y la compara. Cambia en cada modificación |

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
| `row_version` | rowversion | No | automática | **NUEVA (fase 9).** Control de concurrencia (RNFP10): la API la recibe en `If-Match` al editar o desactivar y la compara. Cambia en cada modificación |

Restricciones: `pk_empleados`, `uq_empleados_rut`, `uq_empleados_email`, `ck_empleados_rol`, `ck_empleados_email`.

## categorias

Grandes grupos de trabajos. **Desde la fase 12B** son las cuatro del catálogo real del taller: Impresión y publicidad gráfica, Letreros y estructuras, Papelería e impresión pequeña, y Estampados y personalización. Los mantiene el dueño.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_categoria` | int | No | autoincremental | PK |
| `nombre` | nvarchar(80) | No | | Nombre. UQ |
| `descripcion` | nvarchar(300) | Sí | | **CAMBIA (fase 9).** Ampliada de 250 a 300 caracteres |
| `activo` | bit | No | 1 | 0 = no se ofrece |
| `row_version` | rowversion | No | automática | **NUEVA (fase 9).** Control de concurrencia (RNFP10): la API la recibe en `If-Match` al editar o desactivar y la compara. Cambia en cada modificación |

Datos iniciales: **4 categorías** (ids 1 a 4; la migración `CatalogoRealDelTaller` borra las 5 de prueba y las rehace). Restricciones: `pk_categorias`, `uq_categorias_nombre`.

## productos

**Tipos de letrero** (los productos del taller), cada uno dentro de una categoría. **Desde la fase 12B** son los 16 del catálogo real (gigantografías, banners, posteras, pendones, roll up, X banner, demo stand, adhesivos y vinilos, letreros luminosos, letras corpóreas, señalizaciones, caballete publicitario, rotulación vehicular y general, tarjetas de presentación, flyers y estampados), con sus medidas y opciones.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_producto` | int | No | autoincremental | PK |
| `id_categoria` | int | No | | FK a `categorias` |
| `nombre` | nvarchar(100) | No | | Nombre del tipo de letrero. UQ en todo el taller, no por categoría (`uq_productos_nombre`; decisión de Claude, fase 9) |
| `descripcion` | nvarchar(500) | Sí | | **CAMBIA (fase 9).** Ampliada de 250 a 500 caracteres |
| `precio_base_ref` | decimal(12,2) | Sí | | Precio referencial en pesos. CHECK ≥ 0 |
| `activo` | bit | No | 1 | 0 = no se ofrece |
| `ancho_min_mm`, `ancho_max_mm`, `alto_min_mm`, `alto_max_mm` | int | Sí | | **NUEVAS (fase 12B).** Límites de las medidas en milímetros (el taller las da en centímetros). Los cuatro o ninguno (`ck_productos_medidas`: mínimos > 0 y máximos ≥ mínimos). Nulos = sin medidas que validar (señalizaciones, estampados) o medidas fijas (`producto_medidas_fijas`) |
| `row_version` | rowversion | No | automática | **NUEVA (fase 9).** Control de concurrencia (RNFP10): la API la recibe en `If-Match` al editar o desactivar y la compara. Cambia en cada modificación |

Datos iniciales: **16 tipos de letrero** (ids 1 a 16, fase 12B). Restricciones: `pk_productos`, `fk_productos_categoria`, `uq_productos_nombre`, `ck_productos_precio`, `ck_productos_medidas`. Índice `ix_productos_categoria` (`id_categoria`, `activo`): lista los tipos activos de una categoría.

## producto_medidas_fijas

**Nueva (fase 12B).** Las medidas únicas o disponibles de un producto (X Banner 80 × 200 cm, Demo Stand 300 × 300 cm, caballete en tres tamaños, tarjeta 9 × 5,5 cm, flyers en tres tamaños). Un producto con filas aquí solo acepta esas combinaciones.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_medida` | int | No | autoincremental | PK |
| `id_producto` | int | No | | FK a `productos` (se borra con él) |
| `ancho_mm`, `alto_mm` | int | No | | Medida en milímetros. CHECK > 0 (`ck_producto_medidas_fijas`) |

Restricciones: `pk_producto_medidas_fijas`, `fk_producto_medidas_fijas_producto` (en cascada), `uq_producto_medidas_fijas` (`id_producto`, `ancho_mm`, `alto_mm`). Datos iniciales: 9 filas.

## producto_opciones

**Nueva (fase 12B).** Lo que el cliente puede elegir de un producto: grupo «Material» con valores «Tela PVC de 13 onzas» y «Papel vinílico de una micra», grupo «Acabado», «Terminaciones», «Variante», «Espesor» (2 a 10 mm), «Diseño» (del cliente o de la oficina), etc. Elegir es opcional: lo que el cliente deja sin elegir lo define la oficina al cotizar.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_opcion` | int | No | autoincremental | PK |
| `id_producto` | int | No | | FK a `productos` (se borra con él) |
| `grupo` | nvarchar(60) | No | | Nombre del grupo (el selector) |
| `valor` | nvarchar(80) | No | | Una opción del grupo |
| `orden` | int | No | | Orden dentro del grupo |

Restricciones: `pk_producto_opciones`, `fk_producto_opciones_producto` (en cascada), `uq_producto_opciones` (`id_producto`, `grupo`, `valor`). Datos iniciales: 72 filas. La API valida lo que llega contra estas filas y rechaza un grupo o un valor que el producto no tiene.

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
| `row_version` | rowversion | No | automática | **NUEVA (fase 9).** Control de concurrencia (RNFP10): la API la recibe en `If-Match` al editar o desactivar y la compara. Cambia en cada modificación |

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
| `opciones` | nvarchar(500) | Sí | | **NUEVA (fase 12B).** Lo que el cliente eligió entre las opciones del producto, como objeto JSON ordenado por grupo: `{"Material":"Tela PVC de 13 onzas"}`. Nulo = no eligió |
| `area_m2` | decimal(12,4) | — | Calculada: ancho × alto / 1.000.000 | Área en m² |

Restricciones: `pk_solicitud_detalles`, `fk_sol_detalles_solicitud` (en cascada), `fk_sol_detalles_producto`, `ck_sol_detalles_cantidad`, `ck_sol_detalles_medidas`. Índices: `ix_sol_detalles_solicitud` (`id_solicitud`, **ahora con `id_producto` y `cantidad` incluidas**, fase 12B), `ix_sol_detalles_producto`. Las medidas de cada letrero se validan en la API contra los límites de su producto; no hay una restricción en la tabla porque los datos anteriores a la fase 12B quedarían fuera de ella.

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
| `row_version` | rowversion | No | automática | **NUEVA (fase 9).** Control de concurrencia (RNFP10): la API la recibe en `If-Match` al editar o desactivar y la compara. Cambia en cada modificación |

Datos iniciales: 5 materiales. Restricciones: `pk_materiales`, `uq_materiales_nombre`, `ck_materiales_costo`.

## cotizaciones

Presupuesto de una solicitud (RFS05). Una solicitud puede tener varias; solo una aprobada da origen al pedido. Desde la fase 8, la base no admite dos cotizaciones «pendiente» de una misma solicitud.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_cotizacion` | int | No | autoincremental | PK |
| `id_solicitud` | int | No | | FK a `solicitudes` |
| `id_empleado` | int | No | | FK a `empleados`: quién la elaboró |
| `fecha_cotizacion` | datetime2(0) | No | `SYSDATETIME()` | Fecha |
| `observaciones` | nvarchar(2000) | Sí | | Observaciones. **CAMBIA (fase 8):** hasta 2000 caracteres (antes 500). Las ve el cliente |
| `costo_materiales_total` | decimal(12,2) | No | 0 | Suma de las líneas de materiales. La mantiene el trigger `trg_cotizacion_materiales_recalcular_total` |
| `costo_mano_obra` | decimal(12,2) | No | 0 | Mano de obra. **Fase 8:** sin decimales (`ck_cotizaciones_montos_enteros`) |
| `otros_costos` | decimal(12,2) | No | 0 | Otros gastos. **Fase 8:** sin decimales (`ck_cotizaciones_montos_enteros`) |
| `total_cotizacion` | decimal(14,2) | — | Calculada: materiales + mano de obra + otros | Total |
| `estado` | nvarchar(15) | No | `pendiente` | `pendiente`, `aprobada` o `rechazada`. La aprueba el cliente en la web o el personal cuando el cliente confirma por teléfono o WhatsApp |
| `fecha_respuesta` | datetimeoffset(0) | Sí | | **NUEVA (fase 8).** Cuándo se respondió. Presente si y solo si el estado no es `pendiente` (`ck_cotizaciones_respuesta`) |
| `via_respuesta` | nvarchar(15) | Sí | | **NUEVA (fase 8).** `cliente_web`, `telefono`, `whatsapp` o `presencial` (`ck_cotizaciones_via`). Presente si y solo si hay `fecha_respuesta` |
| `id_empleado_respuesta` | int | Sí | | **NUEVA (fase 8).** FK a `empleados`: quién registró la respuesta. Nulo si la respondió el cliente en la web |
| `motivo_rechazo` | nvarchar(500) | Sí | | **NUEVA (fase 8).** Motivo, si se rechazó. La API lo pide opcional y solo lo guarda al rechazar |
| `row_version` | rowversion | No | automática | **NUEVA (fase 8).** Control de concurrencia: editar, responder o eliminar exige la versión vista (`If-Match`) |
| `eliminado_logico` | bit | No | 0 | **NUEVA (fase 8).** 1 = eliminada; la fila se conserva. Solo se eliminan pendientes o rechazadas sin pedido |
| `fecha_eliminacion_logica` | datetimeoffset(0) | Sí | | **NUEVA (fase 8).** Cuándo se eliminó |

Restricciones: `pk_cotizaciones`, `fk_cotizaciones_solicitud` (en cascada), `fk_cotizaciones_empleado`, `ck_cotizaciones_costos`, `ck_cotizaciones_estado`. Desde la fase 8 también `fk_cotizaciones_empleado_respuesta`, `ck_cotizaciones_montos_enteros` (mano de obra y otros costos sin decimales), `ck_cotizaciones_respuesta` (pendiente si y solo si no hay fecha de respuesta) y `ck_cotizaciones_via`.

Índices: `ix_cotizaciones_solicitud` (`id_solicitud`, `estado`), para la bandeja de cotizaciones de una solicitud. **`uq_cotizaciones_una_pendiente` (NUEVO, fase 8):** único, sobre `id_solicitud` y filtrado a `estado = 'pendiente'` y `eliminado_logico = 0`: una solicitud tiene a lo más una cotización pendiente, aunque dos personas cotizen a la vez. `ix_cotizaciones_empleado_respuesta` (NUEVO), para la clave foránea de la respuesta.

## cotizacion_materiales

Líneas de materiales de una cotización.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_cotizacion_material` | int | No | autoincremental | PK |
| `id_cotizacion` | int | No | | FK a `cotizaciones` (se borra con ella) |
| `id_material` | int | No | | FK a `materiales` |
| `medida_aplicada` | nvarchar(100) | Sí | | Medida a la que se aplica. **CAMBIA (fase 8):** hasta 100 caracteres (antes 80) |
| `cantidad` | decimal(10,2) | No | | Cantidad en la unidad del material (admite decimales: 1,5 m²). CHECK > 0 |
| `costo_unitario` | decimal(12,2) | No | | Costo por unidad al cotizar. CHECK ≥ 0. **Fase 8:** sin decimales (`ck_cot_mat_costo_entero`) |
| `subtotal` | decimal(14,2) | — | Calculada: cantidad × costo unitario | Subtotal. Puede tener centavos por la cantidad |

Restricciones: `pk_cotizacion_materiales`, `fk_cot_mat_cotizacion` (en cascada), `fk_cot_mat_material`, `ck_cot_mat_cantidad`, `ck_cot_mat_costo`, `ck_cot_mat_costo_entero` (**NUEVA, fase 8**). Índice `ix_cot_mat_cotizacion`. Tiene el trigger `trg_cotizacion_materiales_recalcular_total`.

## pedidos

Trabajo acordado (RFS06). **Tabla modificada el 12-11-2026:** el estado operativo y el estado de pago pasan a ser independientes. **Modificada el 14-11-2026 (fase 8):** anulación con motivo, borrado lógico, un pedido por cotización e historial de estados en `pedidos_historial`.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_pedido` | int | No | autoincremental | PK |
| `id_solicitud` | int | No | | FK a `solicitudes`. UQ: una solicitud da a lo más un pedido. En un pedido directo, la solicitud se genera internamente |
| `id_cotizacion` | int | Sí | | FK a `cotizaciones`: la cotización aprobada de origen. **UQ filtrado desde la fase 8** (`uq_pedidos_cotizacion`): una cotización da a lo más un pedido, aunque el pedido se elimine |
| `origen` | nvarchar(15) | No | `cotizacion` | **NUEVA.** `cotizacion` (nace de una cotización aprobada) o `directo` (acordado en el taller sin solicitud previa) |
| `id_empleado_responsable` | int | No | | FK a `empleados`: responsable del trabajo |
| `fecha_pedido` | datetime2(0) | No | `SYSDATETIME()` | Fecha de creación. Sirve para filtrar por año y mes |
| `fecha_entrega_estimada` | datetime2(0) | No | | Fecha comprometida (calendario de entregas) |
| `fecha_entrega_real` | datetime2(0) | Sí | | Fecha en que se entregó |
| `modalidad_entrega` | nvarchar(25) | No | `retiro_local` | `retiro_local`, `entrega_domicilio` o `instalacion_terreno` |
| `requiere_instalacion` | bit | No | 0 | Si lleva instalación |
| `direccion_instalacion` | nvarchar(300) | Sí | | Dónde se instala. **CAMBIA (fase 8):** hasta 300 caracteres (antes 255). La API la exige salvo con `retiro_local`, donde se guarda nula |
| `costo_instalacion_adicional` | decimal(12,2) | No | 0 | Costo de instalación en pesos |
| `monto_base_trabajo` | decimal(12,2) | No | 0 | Monto del trabajo en pesos (de la cotización o acordado) |
| `monto_total_final` | decimal(14,2) | — | Calculada: monto base + instalación | Monto final |
| `estado_pedido` | nvarchar(15) | No | `aceptado` | **CAMBIA** (antes `estado`). Estado operativo: `aceptado`, `en_proceso`, `terminado`, `entregado` o `anulado`. «Anulado» reemplaza a «cancelado», que en Chile se entiende como «pagado». Desde la fase 8, cada cambio queda en `pedidos_historial` y las transiciones las valida la API (`TransicionesPedido`) |
| `monto_abonado` | int | No | 0 | **NUEVA.** Total pagado hasta ahora, en pesos: la suma de los `pagos` del pedido. La mantiene el trigger `trg_pagos_recalcular_monto_abonado`; no se escribe a mano |
| `saldo_pendiente` | int | — | Calculada: monto final − monto abonado | **NUEVA.** Lo que falta por cobrar |
| `estado_pago` | nvarchar(12) | — | Calculada: `no_pagado` si el abono es 0; `pagado_total` si cubre el monto final; si no, `abonado` | **NUEVA.** Estado financiero, independiente del operativo |
| `numero_factura` | nvarchar(30) | Sí | | **NUEVA.** Número de factura cuando se paga. No es único: un cliente habitual puede pagar varios pedidos con una sola factura a fin de mes |
| `motivo_anulacion` | nvarchar(500) | Sí | | **NUEVA (fase 8).** Motivo de 10 a 500 caracteres. Presente si y solo si el estado es `anulado` (`ck_pedidos_anulacion`) |
| `fecha_anulacion` | datetimeoffset(0) | Sí | | **NUEVA (fase 8).** Cuándo se anuló. Va junto con `motivo_anulacion` |
| `eliminado_logico` | bit | No | 0 | **NUEVA (fase 8).** 1 = eliminado; la fila se conserva para estadísticas. Solo se elimina un pedido anulado sin abonos vigentes |
| `fecha_eliminacion_logica` | datetimeoffset(0) | Sí | | **NUEVA (fase 8).** Cuándo se eliminó |
| `row_version` | rowversion | No | automática | Control de concurrencia: editar y cambiar el estado exigen la versión vista (`If-Match`). Registrar o anular un abono no la exige, pero también cambia la versión del pedido |

**Regla de cierre:** el ciclo de un pedido termina solo cuando `estado_pedido = entregado` y `estado_pago = pagado_total`.

Restricciones: `pk_pedidos`, `uq_pedidos_solicitud`, `fk_pedidos_solicitud`, `fk_pedidos_cotizacion`, `fk_pedidos_empleado`, `ck_pedidos_modalidad`, `ck_pedidos_costos`, `ck_pedidos_estado_pedido` (**NUEVA**, reemplaza a `ck_pedidos_estado`), `ck_pedidos_origen` (**NUEVA**), `ck_pedidos_monto_abonado` (**NUEVA**: el abono va de 0 al monto final), `ck_pedidos_montos_enteros` (**NUEVA**: monto base e instalación en pesos sin decimales y con un total de hasta 2.000 millones; sin esta regla, el saldo, que es entero, y el estado de pago podrían contradecirse) y `ck_pedidos_anulacion` (**NUEVA, fase 8**: `anulado` si y solo si hay motivo y fecha de anulación).

Índices y la consulta que resuelve cada uno:
- `ix_pedidos_fecha_estado` (`fecha_pedido`, `estado_pedido`): filtros por año, mes y estado; pedidos por mes.
- `ix_pedidos_calendario_entrega` (`fecha_entrega_estimada`, `estado_pedido`): calendario de entregas.
- `ix_pedidos_cobranza` (`estado_pago`, `estado_pedido`, incluye `saldo_pendiente`): **NUEVO**, listado de cobranza (pedidos no anulados con pago `no_pagado` o `abonado`).
- `uq_pedidos_cotizacion` (**NUEVO, fase 8**): único, sobre `id_cotizacion` y filtrado a las cotizaciones no nulas. Una cotización da a lo más un pedido. Reemplaza al índice `IX_pedidos_id_cotizacion`, y cubre la clave foránea `fk_pedidos_cotizacion`. No filtra por `eliminado_logico`: un pedido eliminado sigue ocupando su cotización.
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
| `anulado` | bit | No | 0 | **NUEVA (fase 8).** 1 = el abono se anuló. Un abono no se edita ni se borra: se anula con motivo y deja de sumar (decisión del 14-11-2026) |
| `motivo_anulacion` | nvarchar(300) | Sí | | **NUEVA (fase 8).** Motivo de 10 a 300 caracteres. Presente si y solo si `anulado` = 1 |
| `id_empleado_anula` | int | Sí | | **NUEVA (fase 8).** FK a `empleados`: quién anuló el abono. Presente si y solo si `anulado` = 1 |
| `fecha_anulacion` | datetimeoffset(0) | Sí | | **NUEVA (fase 8).** Cuándo se anuló. Presente si y solo si `anulado` = 1 |

**Regla:** después de insertar, modificar o borrar un pago, el trigger `trg_pagos_recalcular_monto_abonado` deja `pedidos.monto_abonado` igual a la suma de los pagos del pedido. Para eso suma al pedido la diferencia de cada operación (lo insertado vigente menos lo borrado o anulado vigente), sin volver a leer toda la tabla: dos abonos simultáneos al mismo pedido no se interbloquean, y el segundo suma sobre el total ya confirmado del primero, también si la base activa el aislamiento por instantáneas. Solo escribe el pedido si el total cambió: corregir la observación de un abono no invalida la versión del pedido que otra persona tiene abierta. Nadie más escribe `monto_abonado` (la API la ignora al guardar). Si esa suma supera el monto final, `ck_pedidos_monto_abonado` rechaza la operación completa y el pago no se guarda. Así `estado_pago` y `saldo_pendiente` siempre coinciden con el historial.

**Anulación (fase 8).** Solo cuentan los abonos vigentes (`anulado = 0`). Anular un abono resta su monto del pedido, y el saldo vuelve a lo que era antes de ese abono. Un abono anulado no se puede reactivar: el trigger lo rechaza con el error 50080, que la API responde como `409 abono_anulado`. Para corregir un abono mal ingresado se anula con motivo y se registra el correcto. Quedan a la vista en el pedido: el abono anulado aparece tachado, con su motivo, quién y cuándo.

Restricciones: `pk_pagos`, `fk_pagos_pedido`, `fk_pagos_empleado`, `ck_pagos_monto`, `ck_pagos_medio_pago`. Desde la fase 8, también `fk_pagos_empleado_anula` y `ck_pagos_anulacion` (un abono anulado tiene motivo, quién y cuándo; uno vigente no tiene ninguno de los tres).

Índices y la consulta que resuelve cada uno:
- `ix_pagos_pedido_fecha` (`id_pedido`, `fecha_pago`): historial de un pedido en orden de fecha; también cubre la clave foránea y la suma que hace el trigger.
- `IX_pagos_id_empleado_registra`: clave foránea al empleado (lo crea EF Core por convención, como en las demás tablas).
- `ix_pagos_empleado_anula` (**NUEVO, fase 8**): clave foránea a quien anuló el abono.

## pedidos_historial

**Tabla nueva (14-11-2026, fase 8).** Cada cambio de estado de un pedido, y también su creación (`estado_anterior` nulo). Siempre lo hace un empleado. Un cambio de montos (solo gestión) queda aquí también, sin cambio de estado: `estado_anterior` igual a `estado_nuevo`, y la nota dice los montos antes y después. Los pedidos que ya existían al aplicar la migración recibieron una fila de creación («Registrado antes del historial de pedidos»), a nombre de su responsable y con la fecha del pedido. La anulación de un abono no pasa por aquí: queda tachada en la lista de abonos del pedido (`pagos`).

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_historial` | int | No | autoincremental | PK |
| `id_pedido` | int | No | | FK a `pedidos`. Sin acción en cascada |
| `estado_anterior` | nvarchar(15) | Sí | | Estado antes del cambio. Nulo al crearse el pedido |
| `estado_nuevo` | nvarchar(15) | No | | Estado después del cambio: `aceptado`, `en_proceso`, `terminado`, `entregado` o `anulado` |
| `nota` | nvarchar(500) | Sí | | Nota del avance, motivo de la anulación o los montos que cambiaron |
| `id_empleado` | int | No | | FK a `empleados`: quién hizo el cambio. Sin acción en cascada |
| `fecha` | datetimeoffset(0) | No | | Cuándo ocurrió |

Restricciones: `pk_pedidos_historial`, `fk_pedidos_historial_pedido`, `fk_pedidos_historial_empleado` y `ck_pedidos_historial_estados` (los dos estados son de los cinco válidos del pedido).

Índices: `ix_pedidos_historial_pedido` (`id_pedido`, `fecha`), que da el historial de un pedido en orden de fecha, y `ix_pedidos_historial_empleado` (`id_empleado`), para la clave foránea.

---

## horarios_atencion

Horario semanal publicado (RFS02). Una fila por día. Lo cambian el dueño y el administrador (`PUT /api/v1/horarios`), los siete días en una sola transacción. No tiene `row_version`: si dos personas lo editan a la vez, gana la última escritura (decisión de Claude, fase 9; ver `desviaciones.md`).

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_horario` | int | No | autoincremental | PK |
| `dia_semana` | tinyint | No | | 1 = lunes … 7 = domingo. UQ. CHECK entre 1 y 7 |
| `nombre_dia` | nvarchar(15) | No | | Nombre del día |
| `hora_inicio`, `hora_fin` | time(0) | Sí | | Franja de atención. CHECK: si abre, ambas existen y el fin es posterior al inicio |
| `abierto` | bit | No | 1 | Si se atiende ese día |
| `id_empleado_modifica` | int | Sí | | **NUEVA (fase 9).** FK a `empleados`: quién cambió el día por última vez |
| `fecha_modificacion` | datetimeoffset(0) | Sí | | **NUEVA (fase 9).** Cuándo se cambió por última vez |

Un día cerrado no guarda horas (`ck_horarios_cerrado`, fase 9): si `abierto` es 0, `hora_inicio` y `hora_fin` son nulas.

Datos iniciales: lunes a viernes de 9:00 a 18:30, sábado de 9:00 a 14:00, domingo cerrado. Restricciones: `pk_horarios_atencion`, `uq_horarios_dia`, `ck_horarios_dia`, `ck_horarios_rango`, `ck_horarios_cerrado` (fase 9), `fk_horarios_empleado_modifica` e `ix_horarios_empleado_modifica` (fase 9).

## cartel_digital

**Cartel electrónico** del local (RFS02): todo el personal (dueño, administrador, diseñador y operario, decisión 1 del 14-11-2026) lo «da vuelta» al salir a terreno o a una entrega. Tiene una sola fila y guarda quién y cuándo lo cambió por última vez. No tiene `row_version`: gana la última escritura (decisión de Claude, fase 9).

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_cartel` | int | No | 1 | PK. CHECK: siempre 1 |
| `abierto_en_local` | bit | No | 1 | 1 = «Abierto / Disponible» (verde); 0 = «Cerrado / Ausente» (rojo). La interfaz muestra además el texto, no solo el color |
| `mensaje_estado` | nvarchar(250) | No | `Local abierto y atendiendo` | Mensaje personalizado |
| `tiempo_estimado_regreso` | nvarchar(100) | Sí | | Hora estimada de regreso. **CAMBIA (fase 9).** Ampliada de 80 a 100 caracteres |
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

## trabajos_exportacion

**Tabla nueva (20-11-2026, fase 13A, migración `ExportacionesDeReportes`).** Los archivos de reportes (Excel, PDF y CSV) que se preparan en segundo plano (RNFP18). Pedir un reporte solo inserta una fila; un proceso en segundo plano toma los pendientes (hasta 2 a la vez), genera el archivo y lo guarda en `almacen/exportaciones/` con el nombre `{id_trabajo en 32 hex}.{formato}`. El archivo vence a las 24 horas; el registro queda sin el contenido. No se audita con el interceptor: el pedido y el término quedan en la bitácora con la acción `exportar_reporte`.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_trabajo` | uniqueidentifier | No | lo genera la aplicación (aleatorio) | PK. No adivinable: quien no lo conoce no puede pedir el archivo de otra persona |
| `reporte` | varchar(20) | No | | `operativo`, `gerencial` o `parametrizable` |
| `formato` | varchar(5) | No | | `xlsx`, `csv` o `pdf` |
| `filtros` | varchar(1000) | No | | Los filtros ya validados, en JSON: solo lo que se pidió, sin datos personales (el cliente va por su número) |
| `estado` | varchar(12) | No | `pendiente` | `pendiente`, `procesando`, `listo` o `fallido` |
| `id_empleado` | int | No | | FK a `empleados`: quién lo pidió |
| `rol_solicitante` | nvarchar(20) | No | | El rol con el que lo pidió (la bitácora lo registra también al terminar) |
| `fecha_pedido` | datetimeoffset(0) | No | | Cuándo se pidió. También fija el «hoy» del reporte (un período relativo no cambia si se genera después de medianoche) |
| `fecha_inicio` | datetimeoffset(0) | Sí | | Cuándo lo tomó el proceso |
| `fecha_termino` | datetimeoffset(0) | Sí | | Cuándo terminó, o falló |
| `fecha_vencimiento` | datetimeoffset(0) | Sí | | `fecha_termino` + 24 horas |
| `bloqueado_hasta` | datetimeoffset(0) | Sí | | Mientras está `procesando`, hasta cuándo es de quien lo tomó; pasado ese momento se considera abandonado |
| `intentos` | tinyint | No | 0 | Veces que se tomó. Un trabajo interrumpido 3 veces queda `fallido` |
| `nombre_archivo` | varchar(80) | Sí | | Lo que ve quien descarga: `reporte-operativo-2026-11-20.xlsx` |
| `tamano_bytes` | int | Sí | | Tamaño del archivo |
| `filas` | int | Sí | | Filas del reporte (todas sus tablas) |
| `error` | nvarchar(300) | Sí | | El motivo, en español y sin datos internos, si falló |
| `archivo_borrado_en` | datetimeoffset(0) | Sí | | Cuándo se borró el contenido al vencer |

Restricciones: `pk_trabajos_exportacion`, `fk_trabajos_exportacion_empleado`, `ck_trabajos_exportacion_reporte`, `ck_trabajos_exportacion_formato`, `ck_trabajos_exportacion_estado`, `ck_trabajos_exportacion_intentos`, `ck_trabajos_exportacion_listo` (un trabajo `listo` tiene fecha de término y de vencimiento, nombre y filas) y `ck_trabajos_exportacion_fallido` (uno `fallido` tiene su motivo). Índices: `ux_trabajos_exportacion_activo` (único filtrado: `id_empleado` con estado `pendiente` o `procesando`; **una persona no puede tener más de un trabajo activo**), `ix_trabajos_exportacion_estado` (la cola), `ix_trabajos_exportacion_vencimiento` (filtrado: la purga) e `ix_trabajos_exportacion_empleado`.

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

**Retención (fase 11):** la fila no se borra; se vacían el asunto, los cuerpos, `enlace_baja` y `responder_a`, y el destinatario queda enmascarado: a los 30 días los enviados (`Correo:Cola:DiasCuerpoEnviados`) y a los 90 los fallidos (`Correo:Cola:DiasCuerpoFallidos`), para poder reenviarlos mientras tanto. Columnas nuevas: `revisado`, `reenviado_como`, `id_empleado_reenvia`, `fecha_reenvio` y `enlace_baja`. Ver también `auditoria` y `claves_idempotencia` en el script de la base.

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

**Tabla nueva (13-11-2026, fase 5).** Consultas del formulario de contacto de la portada (RFS08, E22). Cada una deja un aviso por correo al taller, y la bandeja para revisarlas no está en el plan de la fase 9: queda pendiente (ver `desviaciones.md`). Su consentimiento de privacidad queda en `consentimientos`, con la referencia `contacto:{id}`.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_consulta` | int | No | autoincremental | PK |
| `nombre` | nvarchar(100) | No | | Nombre de quien escribe, sin espacios sobrantes |
| `correo` | nvarchar(254) | No | | Correo para responder. Validado al recibir la consulta |
| `telefono` | nvarchar(20) | Sí | | Opcional. Se guarda como `+56` seguido de 9 dígitos (formato `+569XXXXXXXX` para celulares), aunque se escriba con espacios |
| `asunto` | nvarchar(150) | No | | Asunto de la consulta |
| `mensaje` | nvarchar(2000) | No | | Texto de la consulta, sin espacios sobrantes al inicio ni al final |
| `fecha_recepcion` | datetimeoffset(0) | No | `SYSDATETIMEOFFSET()` | Cuándo llegó |
| `revisada` | bit | No | 0 | 1 = el taller ya la revisó. Hoy nadie lo marca: la bandeja está pendiente (ver `desviaciones.md`) |
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

## identidades_externas

**Tabla nueva (21-11-2026, fase 14A, migración `IngresoConGoogle`); ampliada el 21-11-2026 por la fase 14C (migración `GoogleParaPersonal`): la identidad es de un cliente O de un empleado.** La cuenta de otro proveedor (por ahora, Google) vinculada a un cliente o, desde la 14C, a un empleado (RFS03, RFU05, E02). **La identidad es el `sub` del proveedor, que no cambia, y no el correo** (la persona puede cambiarlo). Nunca se guarda el token de Google: solo quién es, el correo que verificó al vincular y cuándo entró. Una cuenta creada con Google no tiene contraseña: `clientes.password_hash` es nulo y, como `clientes.registrado` se calcula a partir de él, vale 0; la API reconoce la cuenta por su identidad (la ficha del personal usa `registrado` o la existencia de la identidad). El personal no tiene identidades externas: no entra por Google (RFU05).

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_identidad` | int | No | autoincremental | PK |
| `id_cliente` | int | Sí | | FK a `clientes`. **CAMBIA (fase 14C):** admite nulo (nulo si la identidad es de un empleado) |
| `id_empleado` | int | Sí | | **NUEVA (fase 14C).** FK a `empleados` (nulo si la identidad es de un cliente). El empleado vincula su Google desde Mi perfil con su contraseña actual; el correo verificado de Google debe ser el de su ficha |
| `proveedor` | varchar(20) | No | `google` (lo pone la aplicación) | CHECK `proveedor IN ('google')`; la lista se amplía con una migración si se suma otro proveedor |
| `sub` | varchar(255) COLLATE Latin1_General_BIN2 | No | | Identificador estable de la persona en el proveedor (ASCII alfanumérico). Columna **binaria**: `a` y `A` son identidades distintas, y ninguna conversión de caracteres puede hacer chocar a dos. Es dato personal: se audita sin mostrar su valor |
| `correo` | nvarchar(254) | No | | El correo que verificó el proveedor al vincular (al vincular desde Mi cuenta, el de la cuenta) |
| `fecha_vinculacion` | datetimeoffset(0) | No | | Cuándo se vinculó o se creó la cuenta con Google |
| `fecha_ultimo_ingreso` | datetimeoffset(0) | Sí | | Último ingreso con esta identidad. No genera registro de «modificar» en la bitácora (cada ingreso queda como `ingreso_google`) |

Fase 14C: `ck_identidades_externas_destino` (exactamente uno de `id_cliente` e `id_empleado`), `fk_identidades_externas_empleado` (`ON DELETE CASCADE`: la identidad se va con su empleado) y el índice único filtrado `uq_identidades_externas_empleado` (`id_empleado`, `proveedor`: a lo más un Google por empleado). **Los índices por cuenta pasan a ser filtrados** (`uq_identidades_externas_cliente` solo para `id_cliente IS NOT NULL`), porque con nulos un índice normal solo admitiría una fila. `uq_identidades_externas_sub` no cambia: un mismo Google no puede estar en dos cuentas, sea de cliente o de empleado. Cambiar el correo de un empleado borra su identidad (se ligó a un correo que ya no es el suyo).

Restricciones de la 14A: `pk_identidades_externas`, `fk_identidades_externas_cliente` (`ON DELETE CASCADE`: la identidad se va con su cuenta) y `ck_identidades_externas_proveedor`. Índices únicos: `uq_identidades_externas_sub` (`proveedor`, `sub`: **un mismo Google no puede estar en dos cuentas**, aunque dos peticiones lleguen a la vez) y `uq_identidades_externas_cliente` (`id_cliente`, `proveedor`: **una cuenta no puede tener dos Google**).

**Auditoría:** la tabla se audita con el interceptor (entidad `identidad_externa`, «Cuenta de Google vinculada»; el `sub` y el correo no se muestran). Además, tres acciones de la bitácora sobre la entidad `cuenta`: `ingreso_google`, `vincular_google` y `desvincular_google`, con quién y cuándo, sin token ni `sub`. `auditoria.accion` no tiene restricción de valores, así que no hizo falta cambiarla.

**Baja de la cuenta:** `POST /mi-cuenta/baja` borra también su identidad (el Google queda libre y la cuenta ya no entra por ahí).


## disponibilidad_empleados

**Tabla nueva (14-11-2026, fase 9).** Franjas de atención de cada empleado por día de la semana (RFU20). Al guardar, la API reemplaza todas las franjas propias. La portada muestra la unión de las franjas de los empleados activos, sin nombres.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_disponibilidad` | int | No | autoincremental | PK |
| `id_empleado` | int | No | | FK a `empleados` |
| `dia_semana` | tinyint | No | | 1 = lunes … 7 = domingo. CHECK entre 1 y 7 |
| `hora_inicio`, `hora_fin` | time(0) | No | | Franja. CHECK: el inicio es anterior al fin |

Restricciones: `pk_disponibilidad_empleados`, `fk_disponibilidad_empleado`, `ck_disponibilidad_dia`, `ck_disponibilidad_rango`. Índice `ix_disponibilidad_empleado_dia` (`id_empleado`, `dia_semana`). La base no impide que dos franjas del mismo empleado se crucen, ni que una franja salga del horario del taller: lo valida la API. Las franjas se repiten cada semana y no hay excepciones por fecha (decisión 2 del 14-11-2026).

## destinatarios_avisos

**Tabla nueva (14-11-2026, fase 9).** Correos que reciben los avisos de solicitudes nuevas. Los administran el dueño y el administrador desde su perfil («Avisos del taller», pedido del usuario el 14-11-2026). Estados: `pendiente`, `activo`, `retirado` y `cancelado`; el diagrama está en [modelo-entidad-relacion.md](modelo-entidad-relacion.md#destinos-de-los-avisos-del-taller-fase-9).

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_destinatario` | int | No | autoincremental | PK |
| `correo` | nvarchar(254) | No | | En minúsculas (`ck_destinatarios_avisos_correo`). Un mismo correo no puede estar dos veces `activo` o `pendiente` (índice único filtrado `uq_destinatarios_avisos_vigente`) |
| `estado` | varchar(10) | No | | `pendiente`, `activo`, `retirado` o `cancelado` (`ck_destinatarios_avisos_estado`) |
| `id_reemplaza` | int | Sí | | FK a `destinatarios_avisos`: el destino que este reemplaza. Nulo si se agregó |
| `token_hash` | char(64) | Sí | | SHA-256 del enlace de confirmación, en hexadecimal. Nunca se guarda el enlace. Solo en `pendiente` (índice único filtrado `uq_destinatarios_avisos_token`) |
| `vence` | datetimeoffset(0) | Sí | | Vencimiento del enlace: 24 horas desde que se pidió o se reenvió. Solo en `pendiente` |
| `id_empleado_solicita` | int | No | | FK a `empleados`: quién lo pidió |
| `fecha_solicitud` | datetimeoffset(0) | No | | Cuándo se pidió |
| `fecha_confirmacion` | datetimeoffset(0) | Sí | | Cuándo se confirmó (pasó a `activo`) |
| `fecha_retiro` | datetimeoffset(0) | Sí | | Cuándo pasó a `retirado` o a `cancelado` |

Restricciones: `pk_destinatarios_avisos`, `fk_destinatarios_avisos_empleado`, `fk_destinatarios_avisos_reemplaza`, `ck_destinatarios_avisos_correo`, `ck_destinatarios_avisos_estado` y `ck_destinatarios_avisos_enlace` (`token_hash` y `vence` están si y solo si el estado es `pendiente`). Índices: `ix_destinatarios_avisos_empleado` y `ix_destinatarios_avisos_reemplaza`.

Un pendiente vencido sigue en `pendiente` hasta que alguien vuelve a pedir ese correo o un reemplazo de ese destino: entonces pasa a `cancelado` (`CancelarVencidosAsync`, con historial «Venció sin confirmarse.»). No hay un proceso que lo haga solo.

## destinatarios_avisos_historial

**Tabla nueva (14-11-2026, fase 9).** Cada acción sobre los destinos de avisos, con quién, cuándo y la dirección enmascarada. Es la bitácora de los avisos del taller; la bitácora general de la fase 11 podrá leer esta tabla.

| Columna | Tipo | Nulo | Por defecto o cálculo | Descripción |
|---|---|---|---|---|
| `id_historial` | int | No | autoincremental | PK |
| `accion` | varchar(12) | No | | `agregar`, `reemplazar`, `confirmar`, `cancelar`, `quitar`, `reenviar` o `probar` (`ck_destinatarios_avisos_historial_accion`) |
| `correo_enmascarado` | nvarchar(254) | No | | Por ejemplo `c•••o@gmail.com`. En `probar` puede ser una lista de direcciones enmascaradas |
| `id_destinatario` | int | Sí | | FK a `destinatarios_avisos`. Nulo en `probar` |
| `id_empleado` | int | Sí | | FK a `empleados`: quién hizo la acción. Nulo si confirmó alguien desde el enlace, sin sesión |
| `fecha` | datetimeoffset(0) | No | | Cuándo ocurrió |
| `detalle` | nvarchar(300) | Sí | | Texto corto, por ejemplo «Reemplaza a …» |

Restricciones: `pk_destinatarios_avisos_historial`, `fk_destinatarios_avisos_historial_destino`, `fk_destinatarios_avisos_historial_empleado` y `ck_destinatarios_avisos_historial_accion`. Índices: `ix_destinatarios_avisos_historial_destino` (`id_destinatario`, `accion`, `fecha`), `ix_destinatarios_avisos_historial_empleado` e `ix_destinatarios_avisos_historial_fecha`. El límite de correos de prueba (3 cada 10 minutos) cuenta las filas `probar` de esta tabla.

---

## Vista, triggers y procedimiento

| Objeto | Tipo | Qué hace |
|---|---|---|
| `vw_indicadores_pedidos` | Vista | **Nueva (fase 12B).** Un pedido vigente (`eliminado_logico = 0`) con el origen y el cliente de su solicitud. Fechas como día de Chile (`date`) y montos en pesos enteros. Alimenta los ingresos, los pedidos por estado, la cobranza, el saldo por cobrar y el listado «Pedidos por cobrar» del dashboard |
| `vw_indicadores_solicitudes` | Vista | **Nueva (fase 12B).** Una solicitud vigente con su categoría principal (la del primer ítem) y su avance en el embudo: `cotizada` (tiene una cotización no eliminada), `aprobada` (una cotización aprobada) y `concretada` (tiene un pedido no anulado). Alimenta las solicitudes en línea y presenciales, las concretadas, la conversión y el embudo |
| `vw_indicadores_items` | Vista | **Nueva (fase 12B).** Los ítems de cada pedido con su producto y categoría. `monto_asignado` reparte el monto final del pedido entre sus ítems en proporción a su cantidad, de modo que la suma de los ítems es el monto del pedido (decisión D del plan). Alimenta la demanda por categoría o por tipo de letrero |
| `vw_metricas_conversion_online` | Vista | Por año y mes: solicitudes en línea, cuántas se concretaron en un pedido **no anulado** y el porcentaje de conversión. **Actualizada el 12-11-2026** (antes excluía los pedidos «cancelados») |
| `trg_cotizacion_materiales_recalcular_total` | Trigger | Tras insertar, modificar o borrar líneas de materiales, recalcula `costo_materiales_total` de las cotizaciones afectadas |
| `trg_pagos_recalcular_monto_abonado` | Trigger | **Nuevo (12-11-2026).** Tras insertar, modificar o borrar pagos, ajusta `monto_abonado` de los pedidos afectados con la diferencia, de modo que siempre sea la suma de sus pagos. **Reescrito el 14-11-2026 (fase 8):** solo cuenta los abonos vigentes (`anulado = 0`); anular un abono resta su monto, y reactivar uno anulado se rechaza con el error 50080 |
| `trg_auditoria_inmutable` | Trigger | **Reescrito (fase 12B, RNFE01).** Sigue rechazando todo `UPDATE` y `DELETE` de la bitácora (error 50125) **salvo uno**: poner `ip = NULL` en filas de más de 90 días, sin cambiar ninguna otra columna. Una sentencia que no alcanza ninguna fila no cambia nada y pasa. El registro del evento queda intacto; solo se borra la dirección IP |
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
| `CotizacionesYPedidos` | 14-11-2026 | Cotizaciones con `row_version`, respuesta (fecha, vía, quién y motivo de rechazo), borrado lógico y el índice único filtrado `uq_cotizaciones_una_pendiente`. Pedidos con motivo y fecha de anulación, borrado lógico y `uq_pedidos_cotizacion`. Tabla nueva `pedidos_historial`. En `pagos`, la anulación de abonos y el trigger `trg_pagos_recalcular_monto_abonado` reescrito (solo suma los vigentes). Amplía `cotizaciones.observaciones` (2000), `cotizacion_materiales.medida_aplicada` (100) y `pedidos.direccion_instalacion` (300). Antes de cada regla nueva completa los datos existentes: respuesta «presencial» de las cotizaciones ya respondidas, motivo «Anulado antes de la fase 8» de los pedidos anulados y una fila de creación en el historial de cada pedido. Revertirla se detiene (`THROW` 50081 o 50082) si hay abonos anulados o textos que no caben en las columnas anteriores, sin tocar nada. Solo agrega y amplía (prueba `CotizacionesYPedidos_SoloAgrega`). La API la aplica en la base demo al arrancar; la base real espera la autorización del usuario |
| `MantenedoresYAvisos` | 14-11-2026 | Solo agrega. `row_version` en `categorias`, `productos`, `catalogo`, `materiales`, `empleados` y `clientes`. En `horarios_atencion`, `id_empleado_modifica` (FK), `fecha_modificacion` y la restricción `ck_horarios_cerrado`. Tablas nuevas `disponibilidad_empleados`, `destinatarios_avisos` y `destinatarios_avisos_historial`. Amplía `categorias.descripcion` (300), `productos.descripcion` (500) y `cartel_digital.tiempo_estimado_regreso` (100). `Down()` se detiene con `THROW 50090` o `50091` si hay datos en las tablas nuevas o textos que no caben en las columnas anteriores. Aplicada en la base demo; no en la real. Pruebas: `MigracionTests.MantenedoresYAvisos_SoloAgrega` y `RevertirMantenedoresYAvisos_SeDetieneAntesDePerderDatos` |
| `CatalogoRealDelTaller` | 17-11-2026 | **Fase 12B.** Reemplaza el catálogo de prueba por el catálogo real del taller: borra las 5 categorías y los 4 a 19 productos anteriores y rehace 4 categorías (ids 1 a 4) y 16 productos (ids 1 a 16). En `productos`, los límites de medidas (`ancho_min_mm`…) y `ck_productos_medidas`; tablas nuevas `producto_medidas_fijas` y `producto_opciones`; en `solicitud_detalles`, `opciones`. Lo que usaba un producto viejo (publicaciones de la vitrina e ítems de solicitudes) se reasigna **antes** al producto nuevo más parecido, en dos pasos con productos temporales (ids 1001 en adelante); en una base sin referencias (la real) solo borra e inserta. Probada hacia adelante y hacia atrás sobre una copia de la demo. `Down()` restaura las 5 categorías y 4 productos originales y apunta todo al producto 1: no es reversible sin perder la equivalencia fina. Aplicada en la demo; no en la real. Prueba: `MigracionTests.CatalogoRealDelTaller_SoloBorraElCatalogoViejo` |
| `IndicadoresDashboard` | 17-11-2026 | **Fase 12B (RNFO11, RNFP26, RNFE01).** Tres vistas (`vw_indicadores_pedidos`, `_solicitudes` e `_items`), cuatro índices (ver «Índices del dashboard») y el trigger de la bitácora reescrito para admitir solo el borrado de la IP a los 90 días. No borra tablas, columnas ni filas; reemplaza `ix_sol_detalles_solicitud` por una versión que lo cubre. `Down()` borra las vistas, restaura el trigger inmutable y quita los índices. Aplicada en la demo; no en la real. Pruebas: `MigracionTests.IndicadoresDashboard_SoloAgregaVistasEIndices_YElTriggerSoloAdmiteBorrarLaIp` y `PurgaIpAuditoriaTests` |

## Índices del dashboard (fase 12B, RNFO11)

Cada índice nuevo sirve a una consulta concreta del dashboard, que es siempre **una consulta agregada** (`GROUP BY`/`SUM`/`COUNT`) sobre las vistas; la API nunca trae los pedidos a memoria para sumarlos (RNFP26). Antes de crear cada uno se revisó que ninguno existente la sirviera:

| Índice | Tabla y columnas | Consulta que sirve | Por qué no bastaba lo que ya había |
|---|---|---|---|
| `ix_pedidos_indicadores_fecha` | `pedidos (fecha_pedido)` + `estado_pedido`, `estado_pago`, `monto_total_final`, `monto_abonado`, `saldo_pendiente`, `id_solicitud`; filtrado `eliminado_logico = 0` | Pedidos creados en el período (S10), saldo por cobrar del período (S24), cobranza y listado «por cobrar» | `ix_pedidos_fecha_estado` solo tiene `fecha_pedido` y `estado_pedido`: cada suma de montos volvía a la tabla. Este lo cubre y solo guarda pedidos vigentes |
| `ix_pedidos_entrega_real` | `pedidos (fecha_entrega_real)` + `fecha_entrega_estimada`, `monto_total_final`, `id_solicitud`; filtrado `estado_pedido = 'entregado' AND eliminado_logico = 0` | Ingresos (S11), ticket promedio y entregas a tiempo: la suma de los entregados en el período | No existía ningún índice por la fecha de entrega real. Al ser filtrado, solo guarda los entregados |
| `ix_sol_detalles_solicitud` (reemplazado) | `solicitud_detalles (id_solicitud)` + `id_producto`, `cantidad` | La vista de ítems (demanda) y la categoría principal de cada solicitud | Mismo índice con las columnas que lee la vista, para no volver a la tabla |
| `ix_auditoria_ip_vigente` | `auditoria (fecha)`; filtrado `ip IS NOT NULL` | La purga diaria de la IP a los 90 días: solo recorre las filas que aún la guardan | `ix_auditoria_fecha` incluye todas las filas, también las ya purgadas |

Los que ya existían y el dashboard reutiliza: `ix_solicitudes_metricas` (`fecha_solicitud`, `origen`, `estado`, `eliminado_logico`) para las solicitudes por período y origen, `ix_pedidos_cobranza` (`estado_pago`, `estado_pedido` + `saldo_pendiente`) para la deuda vigente y `ix_cotizaciones_solicitud` (`id_solicitud`, `estado`) para el embudo. Con los pocos datos de la base demo no se puede medir la mejora: la medición con volumen real queda para la fase 15 (rendimiento).
