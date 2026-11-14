# Especificación técnica de la fase 8 (contrato entre backend y frontend)

Acompaña a `fase-08-plan.md`, aprobado el 14-11-2026 con cuatro decisiones del usuario. Si algo no está aquí, decide lo más simple y coherente con el plan y con la fase 7, y anótalo en tu respuesta.

## Decisiones fijas
- **Montos con IVA incluido:** son pesos chilenos, enteros.
  - Mano de obra, otros costos, costo unitario de un material, costo de instalación, monto base y abonos: siempre enteros.
  - La cantidad de un material admite hasta 2 decimales, así que su subtotal puede tener centavos.
  - El monto base del pedido es el total de la cotización **redondeado al peso** (mitad hacia arriba).
  - La pantalla muestra todos los montos en pesos enteros.
- **Fechas:** se muestran en hora de Chile (`HoraChile`). «Hoy» es la fecha de Chile.
- **Errores:** formato estándar `{ error, codigo, nivel, errores? }`.
  - Transición inválida: **409** `transicion_invalida`.
  - Versión vieja: **409** `version_obsoleta`.
  - Sin versión: **400** `sin_version`.
  - Igual que en la fase 7.
- **Versión:** toda edición, respuesta y cambio de estado de una cotización o un pedido exige `If-Match`, con el ETag del GET (o `rowVersion` en el cuerpo).
  - Registrar o anular un abono **no** la exige, porque solo suma o resta y la base protege el tope.
  - Ambos cambian la versión del pedido, así que la respuesta trae el ETag nuevo.
- **Permisos (RFU12–RFU14):** todo el personal cotiza, responde por el cliente, crea pedidos, los avanza y registra abonos. Los casos reservados a gestión (`dueño`, `administrador`) están marcados en cada ruta.

## Base de datos (migración nueva `CotizacionesYPedidos`, solo agrega)

**`cotizaciones`**, columnas nuevas:

| Columna | Tipo y reglas |
|---|---|
| `row_version` | rowversion |
| `fecha_respuesta` | datetimeoffset null |
| `via_respuesta` | nvarchar(15) null: `cliente_web`, `telefono`, `whatsapp` o `presencial` |
| `id_empleado_respuesta` | int null, FK a `empleados`; null si respondió el cliente en la web |
| `motivo_rechazo` | nvarchar(500) null |
| `eliminado_logico` | bit, por defecto 0 |
| `fecha_eliminacion_logica` | datetimeoffset null |

- **Índice único filtrado** `uq_cotizaciones_una_pendiente`, sobre `id_solicitud`, con `WHERE estado = N'pendiente' AND eliminado_logico = 0`.
- **Checks:**
  - `ck_cotizaciones_montos_enteros`: mano de obra y otros costos sin decimales;
  - `ck_cotizaciones_respuesta`: `estado = 'pendiente'` si y solo si `fecha_respuesta` es null, y `via_respuesta` presente si y solo si `fecha_respuesta` presente.
- **`cotizacion_materiales`:** `ck_cot_mat_costo_entero` (costo unitario sin decimales). La cantidad sigue siendo `> 0`.

**`pedidos`**, columnas nuevas:

| Columna | Tipo y reglas |
|---|---|
| `motivo_anulacion` | nvarchar(500) null |
| `fecha_anulacion` | datetimeoffset null |
| `eliminado_logico` | bit, por defecto 0 |
| `fecha_eliminacion_logica` | datetimeoffset null |

- **Índice único filtrado** `uq_pedidos_cotizacion` sobre `id_cotizacion` `WHERE id_cotizacion IS NOT NULL`.
- **Check `ck_pedidos_anulacion`:** `estado_pedido = 'anulado'` si y solo si hay `motivo_anulacion` y `fecha_anulacion`.

**`pedidos_historial`** (nueva):

| Columna | Tipo y reglas |
|---|---|
| `id_historial` | int, identity |
| `id_pedido` | FK a `pedidos` |
| `estado_anterior` | null en la creación |
| `estado_nuevo` | |
| `nota` | nvarchar(500) null |
| `id_empleado` | FK a `empleados` |
| `fecha` | datetimeoffset |

**`pagos`**, columnas nuevas:

| Columna | Tipo y reglas |
|---|---|
| `anulado` | bit, por defecto 0 |
| `motivo_anulacion` | nvarchar(300) null |
| `id_empleado_anula` | int null, FK a `empleados` |
| `fecha_anulacion` | datetimeoffset null |

- **Check `ck_pagos_anulacion`:** `anulado = 1` si y solo si las tres columnas de anulación están presentes.
- **El trigger `trg_pagos_recalcular_monto_abonado` se reescribe:** suma solo las filas con `anulado = 0`. En `inserted` cuenta `monto` si `anulado = 0`, y en `deleted` resta `monto` si `anulado = 0`. Así anular un abono resta su monto.
- **Comprobaciones antes de aplicar:** que los datos actuales cumplan los checks nuevos.
  - En la demo: mano de obra, otros costos y costos unitarios enteros, y una sola cotización pendiente por solicitud.
  - Si algo no cumple, se corrige en la semilla de la demo, nunca relajando el check.
- **Script:** se regenera `docs/base-de-datos/LetreriaLosHerreraDB.sql`.

## Reglas de negocio (en `Negocio/Cotizaciones` y `Negocio/Pedidos`, sin HTTP ni base, con pruebas unitarias)

### Cotización
- **Materiales:** de 0 a 30 líneas `{ idMaterial (activo), medidaAplicada? (máx. 100), cantidad (> 0, ≤ 100 000, máx. 2 decimales), costoUnitario (entero, 0 a 100 000 000) }`.
- **Mano de obra y otros costos:** enteros, de 0 a 100 000 000.
- **Observaciones:** opcionales, máx. 2000. **Las ve el cliente.**
- **Total:** debe ser mayor que 0 y como máximo 2 000 000 000.
- **Emitir:** solo sobre una solicitud «solicitado» y no eliminada.
  - Si ya hay una pendiente, responde 409: «Esta solicitud ya tiene una cotización pendiente. Edítala o espera la respuesta del cliente.».
  - La base lo garantiza también: el error del índice único se traduce al mismo 409.
- **Editar:** solo «pendiente»; reemplaza las líneas.
- **Responder:**
  - `decision` es `aprobada` o `rechazada`;
  - el motivo es opcional (máx. 500), y solo cuenta si se rechaza;
  - solo sobre una cotización «pendiente».
- **Efecto en la solicitud** (con `TransicionesSolicitud` de la fase 7 y su historial):
  - al emitir: solicitado → cotizado (actor Sistema);
  - al aprobar: cotizado → aceptado (Sistema);
  - al rechazar: cotizado → solicitado (Cliente o Personal), con nota «Cotización #N rechazada: motivo».
- **Eliminar (gestión):** borrado lógico, solo «pendiente» o «rechazada» y sin pedido. Si estaba pendiente, la solicitud vuelve a «solicitado», con historial.

### Pedido
- **Transiciones** (`TransicionesPedido`):

| Desde | Hacia | Notas |
|---|---|---|
| aceptado | en_proceso | |
| en_proceso | terminado | |
| terminado | entregado | pide `fechaEntregaReal`: no futura, ni anterior a la fecha del pedido; por defecto, hoy |
| en_proceso | aceptado | vuelta atrás |
| terminado | en_proceso | vuelta atrás |
| aceptado, en_proceso o terminado | anulado | motivo obligatorio (10 a 500); **con abonos vigentes, solo gestión** (403 `solo_gestion`) |

  - «Entregado» y «anulado» no cambian más. Cualquier otra transición responde 409 con la explicación.
  - Cada cambio escribe una fila en `pedidos_historial`, con la nota opcional (máx. 500) o el motivo.
- **Ciclo cerrado:** `cicloCerrado = estadoPedido == entregado && estadoPago == pagado_total`. Se calcula y no se guarda.
- **Datos del pedido (E08):**
  - `idEmpleadoResponsable`: empleado activo.
  - `fechaEntregaEstimada`: fecha, desde hoy, como mucho 2 años adelante. Al editar, se acepta la que ya tenía.
  - `modalidadEntrega`: `retiro_local`, `entrega_domicilio` o `instalacion_terreno`.
  - `requiereInstalacion`: es obligatorio si la modalidad es `instalacion_terreno`.
  - `direccionInstalacion`: máx. 300. Obligatoria si la modalidad no es `retiro_local`; si no, se guarda null.
  - `costoInstalacionAdicional`: entero ≥ 0, y 0 si no requiere instalación.
  - `numeroFactura`: opcional, máx. 30.
- **Editar (PUT):**
  - en un pedido «anulado», nada;
  - en uno «entregado», solo `numeroFactura`;
  - en los demás, todo lo anterior y `montoBaseTrabajo` (entero ≥ 0). El monto final no puede quedar bajo lo abonado: 409 «El monto final no puede quedar bajo lo ya abonado ($X).».
- **Abonos (E25):**
  - **Datos:**
    - `monto`: entero > 0;
    - `fechaPago`: no futura y no más de 365 días atrás; por defecto, hoy;
    - `medioPago`: `efectivo`, `transferencia`, `tarjeta_debito`, `tarjeta_credito`, `cheque` u `otro`;
    - `observacion`: máx. 300;
    - `numeroFactura`: opcional; si viene, actualiza la del pedido.
  - **Cómo se registra:** en una transacción que primero bloquea la fila del pedido (`SELECT … WITH (UPDLOCK, ROWLOCK)`).
  - **Pedido anulado:** 409 «Un pedido anulado no recibe abonos.».
  - **Si supera el saldo:** 409 `abono_excede_saldo` «El abono supera el saldo pendiente ($X).». Si de todos modos llega a la base, el check del pedido lo traduce al mismo 409.
- **Anular un abono (decisión 2):**
  - `motivo` de 10 a 300.
  - Quién puede:
    - gestión, cualquiera;
    - el resto del personal, solo los que registró él mismo en las últimas 24 horas (si no, 403 `solo_gestion`).
  - Un abono ya anulado responde 409. Un abono anulado no se puede reactivar.
- **Pedido anulado con abonos (decisión 4):** la ficha trae `aFavorDelCliente` = monto abonado vigente. La pantalla lo muestra como «A devolver o a favor del cliente: $X». No hay ruta de devolución.
- **Eliminar (gestión):** borrado lógico, solo pedidos «anulado» sin abonos vigentes.

## Rutas

### Cotizaciones (personal)
- `POST /api/v1/solicitudes/{id}/cotizaciones`
  - Cuerpo: `{ materiales: [{ idMaterial, medidaAplicada?, cantidad, costoUnitario }], costoManoObra, otrosCostos, observaciones? }`.
  - Responde **201** con la cotización (S06) y su ETag.
  - Hace en **una transacción**:
    - la cotización y sus líneas;
    - el cambio de la solicitud a «cotizado» y su historial;
    - el correo «Tu cotización #N está lista» en la cola, solo si el cliente tiene correo **confirmado**.
- `PUT /api/v1/cotizaciones/{id}` (`If-Match`): el mismo cuerpo.
- `POST /api/v1/cotizaciones/{id}/respuesta` (`If-Match`): `{ decision, via: "telefono"|"whatsapp"|"presencial", motivo? }`. Responde **200** con la cotización.
- `GET /api/v1/cotizaciones`:
  - **Parámetros:** `estado`, `desde`, `hasta`, `idCliente`, `q` (número de cotización o de solicitud, nombre, teléfono o RUT), `orden` (`fecha_desc` por defecto, `fecha_asc`, `total_desc`), `pagina` y `tamano`.
  - **Cada fila:** `{ idCotizacion, numero, idSolicitud, fechaChile, cliente, total, estado, elaboradaPor }`.
- `GET /api/v1/cotizaciones/{id}`: la cotización, con ETag.
- `DELETE /api/v1/cotizaciones/{id}` (gestión, `If-Match`): borrado lógico, **204**.

### Cotización (S06)
```
{ idCotizacion, numero: "#45", idSolicitud, numeroSolicitud: "#123", fechaChile, estado,
  cliente: { idCliente, nombre, apellido, telefono, correo },
  items: [{ producto, descripcion, anchoMm, altoMm, cantidad, areaM2 }],
  materiales: [{ idMaterial, material, unidad, medidaAplicada, cantidad, costoUnitario, subtotal }],
  costoMaterialesTotal, costoManoObra, otrosCostos, total, totalRedondeado,
  observaciones, elaboradaPor: { idEmpleado, nombre },
  respuesta: null | { fechaChile, via, motivo, registradaPor: { nombre } | null },
  idPedido: null | number, rowVersion }
```
- **Para el cliente:** la misma forma, sin `idEmpleado` y con `elaboradaPor.nombre` reducido al nombre de pila.

### Cliente
- `GET /api/v1/mis/cotizaciones/{id}`: solo una suya y no eliminada; si no, 404. Va con ETag.
- `POST /api/v1/mis/cotizaciones/{id}/respuesta` (`If-Match`): `{ decision, motivo? }`, con vía `cliente_web`.
- **`GET /api/v1/mis/solicitudes`** y **`/{id}`** se amplían con el seguimiento (S04):
  - `cotizacion`: `null` o `{ idCotizacion, numero, total, estado }`, la última no eliminada;
  - `pedido`: `null` o `{ idPedido, numero, estadoPedido, estadoPago, saldoPendiente, fechaEntregaEstimada, modalidadEntrega, cicloCerrado }`.
  - El motivo de rechazo de la solicitud ya viene desde la fase 7.
- **`GET /api/v1/mis/pedidos`:** sigue existiendo, con la forma de `pedido` de arriba más `idSolicitud`.

### Pedidos (personal)
- `POST /api/v1/cotizaciones/{id}/pedido`: la cotización debe estar «aprobada» y sin pedido.
  - **Cuerpo:** `{ idEmpleadoResponsable, fechaEntregaEstimada, modalidadEntrega, requiereInstalacion, direccionInstalacion?, costoInstalacionAdicional, numeroFactura?, abonoInicial?: { monto, fechaPago?, medioPago, observacion? } }`.
  - **Responde:** **201** con la ficha.
  - **En una transacción:** el pedido «aceptado», su historial (null → aceptado) y el abono inicial. El abono inicial tampoco puede superar el monto final.
- `POST /api/v1/pedidos/directo` (RFU35, E26):
  - **Cuerpo:** `{ idCliente, descripcionGeneral, detalles: [como en la fase 7], materiales, costoManoObra, otrosCostos, observaciones?, …los datos del pedido…, abonoInicial? }`.
  - **El cliente:** la pantalla lo busca o lo crea antes, con las rutas de la fase 7 (`GET /clientes/buscar`, `POST /clientes`).
  - **En una transacción:**
    - la solicitud presencial con sus ítems, ya «aceptado», con su historial y la nota «Pedido directo en el taller»;
    - la cotización «aprobada», con vía `presencial` y respondida por quien la registra;
    - el pedido con origen `directo`;
    - el abono.
  - **No envía correos.**
  - **Responde:** **201** con la ficha.
- `GET /api/v1/pedidos`:
  - **Parámetros:** `estadoPedido`, `estadoPago`, `cobranza=true`, `anio`, `mes`, `idCliente`, `idResponsable`, `q` (número de pedido, nombre, teléfono o RUT), `orden` (`fecha_desc` por defecto, `fecha_asc`, `entrega_asc`, `saldo_desc`), `pagina` y `tamano`.
    - `cobranza` = no anulado y con pago `no_pagado` o `abonado`.
    - `anio` y `mes` van por fecha del pedido.
  - **Excluye** los eliminados.
  - **Responde:** `{ elementos, total, pagina, tamano, totalPaginas, resumen: { totalPorCobrar, cantidadPorCobrar } }`. El resumen cubre **todo el filtro**, no solo la página.
  - **Cada fila:** `{ idPedido, numero, origen, fechaPedido, cliente, responsable, fechaEntregaEstimada, modalidadEntrega, estadoPedido, estadoPago, montoTotalFinal, montoAbonado, saldoPendiente, cicloCerrado }`.
- `GET /api/v1/pedidos/{id}`: la ficha (S07), con ETag:
```
{ idPedido, numero, origen, solicitud: { idSolicitud, numero }, cotizacion: { idCotizacion, numero } | null,
  cliente, responsable: { idEmpleado, nombre }, fechaPedido, fechaEntregaEstimada, fechaEntregaReal,
  modalidadEntrega, requiereInstalacion, direccionInstalacion, montoBaseTrabajo, costoInstalacionAdicional,
  montoTotalFinal, estadoPedido, estadoPago, montoAbonado, saldoPendiente, numeroFactura, cicloCerrado,
  motivoAnulacion, aFavorDelCliente, transicionesPosibles: ["en_proceso", "anulado", …],
  historial: [{ fechaChile, estadoAnterior, estadoNuevo, nota, empleado }],
  pagos: [{ idPago, monto, fechaPago, medioPago, observacion, registradoPor, fechaRegistroChile,
            anulado, motivoAnulacion, anuladoPor, fechaAnulacionChile, puedeAnular }], rowVersion }
```
- `PUT /api/v1/pedidos/{id}` (`If-Match`): los datos editables.
- `PATCH /api/v1/pedidos/{id}/estado` (`If-Match`): `{ estado, fechaEntregaReal?, motivo?, nota? }`.
- `POST /api/v1/pedidos/{id}/pagos`: el abono, **201** con la ficha.
- `POST /api/v1/pedidos/{id}/pagos/{idPago}/anular`: `{ motivo }`, **200** con la ficha.
- `DELETE /api/v1/pedidos/{id}` (gestión, `If-Match`): borrado lógico, **204**.
- `GET /api/v1/pedidos/calendario?desde=&hasta=`:
  - **Rango:** como máximo 62 días; si no, 400.
  - **Qué trae:** pedidos no anulados ni eliminados. Los pendientes se ubican en su fecha estimada y los entregados en su fecha real.
  - **Cada uno:** `{ idPedido, numero, fecha, cliente, modalidadEntrega, estadoPedido, estadoPago, atrasado }`.
- `GET /api/v1/pedidos/alertas` (gestión): `{ proximas: [hoy a hoy+3], atrasadas: [estimada < hoy, sin entregar ni anular], totalPorCobrar, cantidadPorCobrar }`. Las listas traen la misma forma que el calendario, como máximo 20 cada una.

### Rutas que se retiran
- Las antiguas `POST /api/v1/cotizaciones` y `POST /api/v1/pedidos`.
- El `DELETE` físico.
- Las respuestas que devolvían la entidad de EF.

Las pruebas que las usaban se adaptan.

## Correo «cotización lista» (cola de la fase 4)
- **Asunto:** «Tu cotización #45 está lista».
- **Cuerpo:** saludo, solicitud #123, total (redondeado), un resumen de los ítems, «Puedes aprobarla o rechazarla desde tu cuenta» y el botón **«Ver cotización»**, que lleva a `{Sitio:UrlPublica}/cuenta/solicitudes/{idSolicitud}`.
- **Cómo sale:** con `CorreoParaCliente`, desde la casilla automática y con el pie de «no respondas». Se escribe en la misma transacción que la cotización.
- **Cuándo no sale:** si el cliente no tiene correo confirmado (solicitudes presenciales), y en el pedido directo.

## Frontend (rutas)

**Panel:**
- **`/panel/solicitudes/:id`** (ficha de la fase 7), sección «Cotización»:
  - la vigente y el historial;
  - «Cotizar» si está «solicitado»;
  - «Registrar respuesta del cliente» si hay una pendiente, con un diálogo de decisión, vía y motivo;
  - «Crear pedido» si está aprobada y sin pedido;
  - enlace al pedido si ya existe.
- **`/panel/solicitudes/:id/cotizar`** y **`/panel/cotizaciones/:id/editar`**: editor de cotización.
  - Líneas de material: selector del catálogo `GET /materiales`, que sugiere el costo unitario; medida, cantidad y costo.
  - Mano de obra, otros costos y observaciones.
  - Subtotales y total en vivo, con la vista previa de lo que verá el cliente.
- **`/panel/cotizaciones`:** listado con filtros en la dirección. **`/panel/cotizaciones/:id`:** vista S06.
- **`/panel/cotizaciones/:id/pedido`:** formulario E08, con abono inicial opcional.
- **`/panel/pedidos`:** listado conectado a la API.
  - Los filtros quedan en la dirección: estado del pedido, estado de pago, cobranza, año, mes, cliente y búsqueda.
  - Un acceso «Por cobrar» aplica `cobranza=true` y muestra el total por cobrar (S24).
- **`/panel/pedidos/:id`:** ficha S07, con:
  - avance, con botones de las transiciones posibles; anular pide motivo;
  - edición, con aviso de versión vieja sin perder lo escrito;
  - registro de abono, en un diálogo;
  - historial de abonos, con los anulados tachados y con su motivo, y «Anular» en los que se puede;
  - historial de estados.
- **`/panel/pedidos/directo`:** reemplaza al diálogo `PedidoDirecto` de la fase 3. Busca o crea al cliente, luego van los ítems, los materiales y costos, los datos del pedido y el abono.
- **`/panel/calendario`:**
  - **Vista de mes:** en ≥ 768 px, cuadrícula de mes. En el teléfono, lista por día.
  - **Distinción:** pendientes y entregados con color **y** con ícono y etiqueta («Pendiente», «Entregado», «Atrasado»).
  - **Navegación:** botones de mes anterior y siguiente y «Hoy».
  - **Cada pedido** es un enlace a su ficha.
- **`/panel` (inicio):** para gestión, la tarjeta de alertas con próximas, atrasadas y total por cobrar, cada una con su enlace.
- **Navegación del panel:** suma «Cotizaciones» y «Calendario».

**Cliente:**
- **`/cuenta/solicitudes`** y **`/cuenta/solicitudes/:id`:** seguimiento S04.
  - La solicitud.
  - Su cotización (S06) con «Aprobar» y «Rechazar» (motivo opcional) mientras está pendiente. Confirmar la aprobación pide un diálogo: «Al aprobar, el taller comienza tu trabajo».
  - El pedido, con estado, pago, saldo, fecha estimada y modalidad.
  - Estados con texto **y** con ícono, nunca solo con color.
- **Formato:** montos con `formatearPesos` y fechas con los formateadores existentes.

**Sin cambios en la lógica de sesión ni en el cliente HTTP:** `If-Match` y ETag ya existen desde la fase 7 (`useSolicitudEditable`).
