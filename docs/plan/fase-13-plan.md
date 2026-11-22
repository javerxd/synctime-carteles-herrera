# Plan de la fase 13 — Reportes y exportaciones

Fecha: 20-11-2026 · Ramas: `fase-13a-reportes-backend` y `fase-13b-reportes-frontend` (la primera nace de `fase-12d-paneles-y-cuentas`; la segunda, de la 13A) · Estado: **esperando tu aprobación**; construcción sin iniciar

Origen: el plan general (`plan-de-construccion.md`, fase 13), los requisitos RFS10, RFS12 (la parte de «quién exportó»), RFU24 a RFU26, RFU28, RFU36, RNFP18, E16, E20, S18, S20 y S24, y tu indicación del 20-11-2026 de usar como referencia las tres imágenes de `docs/image/` para los botones de exportación.

Qué hay hoy: la pantalla `/panel/reportes` (fase 3) calcula en el navegador con los pedidos de la API, solo para dueño y administrador, y avisa «la exportación llega en la fase 13». El dashboard (12B) ya calcula ingresos, cobranza y demanda en el servidor sobre vistas SQL. **No hay** ninguna librería de Excel ni de PDF en el backend, ni trabajos en segundo plano para exportar. Sí existen una cola en segundo plano (correo), una carpeta de archivos (`almacen/`), la bitácora de auditoría (fase 11) y un limitador de peticiones.

Esta fase **sí necesita una migración** (tabla de trabajos de exportación) y **dos dependencias nuevas** (Excel y PDF): ambas están en las decisiones del final, para que las apruebes o cambies.

## 0. Cómo se divide (como la 12A y la 12B)

- **13A — Backend:** los tres reportes en la API, las exportaciones en segundo plano a Excel, PDF y CSV, la migración, la auditoría y el contrato. Termina con las pruebas pasando y con tu visto bueno.
- **13B — Frontend:** las pantallas conectadas a la API, los botones de exportación, el estado de cada descarga y la vista de impresión. Termina con la verificación en la app y con tu visto bueno.

Cada una con su informe y su parada. Si prefieres una sola entrega, dilo; el contenido es el mismo.

## 1. Los tres reportes

Todos en hora de Chile y con los **pedidos anulados fuera de los totales** (como el dashboard); en el listado operativo se muestran marcados, para que se vean.

| Reporte | Contenido | Quién lo ve |
|---|---|---|
| **Operativo** (RFU24, S18) | Tres tablas: **pedidos** (cliente, fechas, estado del pedido, estado de pago, monto, abonado y saldo), **entregas** (fecha estimada y real, y si fue a tiempo) y **solicitudes** (número, fecha, cliente, origen y estado) | Dueño, administrador, diseñador y operario |
| **Gerencial** (RFU25, S18) | Ingresos, costos y margen del período, totales y gráficos (ingresos y costos por tramo; ingresos por tipo de letrero). Los ingresos usan **la misma definición del dashboard** (pedidos entregados con fecha de entrega real en el período), para que ambos números coincidan | Dueño y administrador |
| **Parametrizable** (RFU25, RFU36, S24) | Pedidos filtrados por rango de fechas, estado del pedido, estado de pago, año, mes y cliente, con totales. Con «por cobrar» es el **listado de cobranza** (pedidos no anulados con pago «No pagado» o «Abonado», con saldo y total por cobrar) | Dueño y administrador |

- **Costos:** salen de la cotización aceptada del pedido (materiales, mano de obra y otros costos). Un pedido directo, sin cotización, **no tiene costo registrado**: aparece como «sin costo» y no se inventa; el margen solo se calcula sobre los pedidos con costo, y el reporte lo dice.
- **Filtros:** los mismos nombres y reglas que ya usa el dashboard (`desde`, `hasta`, `anio`, `mes`, `estadoPedido`, `estadoPago`, `cliente`), más `periodo` y `comparar` (E16). Un filtro inválido responde `400` con el error por campo; nada se ignora en silencio.
- **Tope de tamaño:** un reporte de más de 50 000 filas se rechaza con un mensaje claro («acota el rango»); no se genera a medias.
- **Consultas:** en el servidor, con las vistas que ya existen (RNFO11) y proyecciones sin cargar entidades completas. La pantalla deja de calcular en el navegador y pasa a leer la API.

## 2. Exportaciones en segundo plano (RNFP18)

Flujo, igual para los tres reportes y los tres formatos:

1. `POST /api/v1/reportes/{reporte}/exportaciones` con el formato y los filtros → responde **`202`** con el identificador del trabajo, sin esperar el archivo.
2. `GET /api/v1/reportes/exportaciones/{id}` → estado: `pendiente`, `procesando`, `listo` o `fallido` (con un mensaje claro).
3. `GET /api/v1/reportes/exportaciones/{id}/archivo` → la descarga, solo si está `listo`, solo para quien la pidió y mientras no haya vencido.

- **Cola:** un servicio en segundo plano, con el mismo patrón de la cola de correo: toma trabajos pendientes, un máximo de 2 a la vez, con tiempo límite por trabajo; si el servidor se reinicia a mitad, el trabajo vuelve a `pendiente`.
- **Almacenamiento:** el archivo se guarda en `almacen/exportaciones/` (ya ignorada por git) con un nombre generado por el servidor, **nunca** con texto del usuario. Vence a las **24 horas** y un proceso lo borra (el registro del trabajo queda, sin el contenido). Los reportes llevan datos de clientes: no se guardan más de lo necesario.
- **Seguridad:** el rol se valida **en el servidor** por tipo de reporte (un diseñador que pide el gerencial recibe `403`); quien no pidió el trabajo recibe `404`, no `403`, para no revelar que existe; un trabajo pendiente por persona a la vez; limitador de peticiones propio (10 por minuto). Los filtros se guardan validados y sin datos personales (el cliente va por su identificador).
- **Migración `ExportacionesDeReportes`:** tabla `trabajos_exportacion` (identificador no adivinable, reporte, formato, filtros en JSON, estado, quién lo pidió, fechas de pedido, inicio, término y vencimiento, nombre del archivo, tamaño, filas y error) con restricciones de estado y formato e índices por persona y por estado. Verifico si la bitácora tiene una restricción sobre el tipo de acción y, si la tiene, la amplío en la misma migración. **Solo la aplico en la base demo**; la real recibe todas las migraciones juntas al final, como acordamos.

## 3. Los formatos

- **Excel (.xlsx):** una hoja por tabla del reporte, con encabezados en negrita y fijos al desplazar, **fechas con formato de fecha** (no texto), **pesos con formato numérico** (miles con punto), anchos de columna razonables y una fila de **totales** con fórmula `SUM`. Una hoja «Filtros» con los filtros aplicados y la fecha de emisión. Las celdas de texto se escriben como texto, nunca como fórmula.
- **PDF:** título, fecha y hora de emisión, **filtros aplicados**, tabla con el encabezado repetido en cada página, **«Página X de Y»**, hoja A4 horizontal y **texto seleccionable** (fuente incrustada, no una imagen). En el gerencial, los gráficos van como imagen con la tabla de sus datos al lado. No es un PDF accesible etiquetado (PDF/UA): queda anotado como límite.
- **CSV:** UTF-8 **con marca de orden de bytes** (para que Excel respete las tildes), **separador `;`** (el que Excel en español de Chile espera), fechas `aaaa-mm-dd`, pesos enteros sin separador de miles y comillas según RFC 4180. **Contra la inyección de fórmulas:** un texto que empiece con `=`, `+`, `-` o `@` se escribe precedido de un apóstrofo. El separador y la codificación quedan documentados en `contrato.md` y en la propia pantalla.
- **Un reporte con varias tablas (el operativo) en CSV:** un CSV es una sola tabla, así que exporta la **tabla principal** (pedidos) y la pantalla lo dice. Las demás tablas van en Excel y PDF. Es la decisión 5.
- **Nombre del archivo:** `reporte-operativo-2026-11-20.xlsx` (tipo y fecha de emisión), sin datos personales.

## 4. Auditoría (RFS12, RFU28, S20)

- Cada exportación **pedida** registra en la bitácora: quién (empleado y rol), qué reporte, qué formato, con qué filtros y, al terminar, cuántas filas. Nueva acción `exportar_reporte`, con su texto «Exportó un reporte» en el visor `/panel/auditoria` y en su filtro por acción (fase 11).
- Un trabajo fallido también queda registrado como pedido; no se registra el contenido del archivo.

## 5. Frontend (13B)

- **`/panel/reportes`** pasa a leer la API: las tres pestañas actuales, con los filtros de pantalla y la **misma tabla que luego se exporta** (lo que ves es lo que sale). Carga, vacío y error por bloque. La entrada «Reportes» del menú la ven **todos los roles**, y cada persona ve solo las pestañas que su rol permite (el diseñador y el operario, solo la operativa).
- **Los tres botones de exportación** (Excel, CSV y PDF), tomando como referencia tus imágenes:
  - **Excel en verde, CSV en azul y PDF en rojo**, texto blanco «Exportar a Excel», «Exportar a CSV» y «Exportar a PDF», esquinas redondeadas y el icono de archivo con su insignia («X», «CSV», «PDF») y la flecha de descarga.
  - Las imágenes son fotos de un botón sobre un fondo texturizado, borrosas y con resplandor: **no se usan como archivo**. Los botones se construyen en código (componente `BotonExportar`, iconos en SVG propios), con los colores como **tokens** y el texto como texto real, para que escalen, se lean con lector de pantalla y respondan al teclado. No replico el resplandor ni el degradado: el panel del taller no lleva botones animados ni adornos (regla de la 12C); queda un color liso con borde fino.
  - **Colores:** parto de tus imágenes y los ajusto a **≥ 4,5:1 con texto blanco**, que el validador del proyecto comprueba (el verde y el azul de las imágenes quedan claros para eso). El rojo es el `#B91C1C` que ya usa «Cerrar sesión».
  - Van juntos, bajo el título del reporte, con el rótulo «Exportar» y la leyenda de qué llevan (los filtros de pantalla, el separador del CSV).
- **Estado de cada descarga, sin bloquear la pantalla:** al pulsar, el botón pasa a «Preparando…» y se desactiva solo ese botón; un aviso con `role="status"` anuncia «Preparando tu archivo» y luego «Tu archivo está listo» (la descarga empieza sola y queda un enlace «Descargar de nuevo»); si falla, un aviso con el motivo y «Reintentar». Se puede seguir usando la pantalla y pedir otro formato. Si cambias de página, el trabajo sigue en el servidor y se pierde solo el aviso.
- **Impresión (RFU24):** botón «Imprimir» y hoja limpia con `@media print`: sin menú, sin botones, con título, fecha de emisión, filtros aplicados y la tabla completa, con saltos de página que no parten las filas y los gráficos con su tabla.
- **Atajo de cobranza (RFU36):** en la tabla «Pedidos por cobrar» del dashboard, un enlace «Exportar cobranza» que abre el reporte parametrizable ya filtrado por cobrar.
- **Accesibilidad:** pestañas por teclado, botones con nombre accesible que contiene el texto visible, avisos anunciados, foco que no se pierde al cambiar de estado, objetivo táctil de 44 px en el teléfono.

## 6. Pruebas primero y revisiones

**13A (backend)**
- **Unitarias:** validación de filtros (rango invertido, mes sin año, año fuera de rango, estado desconocido); escape de CSV (comillas, `;`, saltos de línea, tildes, ñ, y los cuatro inicios de fórmula); formatos de fecha y número; cálculo de costos y margen con y sin cotización; el tope de filas.
- **Integración (xUnit contra SQL Server), con la matriz de roles completa:** operativo con `200` a dueño, administrador, diseñador y operario; gerencial y parametrizable con `200` a dueño y administrador y `403` a diseñador y operario; cliente `403`; sin sesión `401`. Ciclo del trabajo (`202`, estados, descarga, vencimiento y `410`); un tercero recibe `404`; un trabajo a la vez por persona; limitador; auditoría con quién, formato y filtros; reinicio con un trabajo a medias.
- **Los archivos se abren de verdad:** el `.xlsx` se vuelve a leer con el SDK de Open XML (hojas, formatos de fecha y número, totales); el CSV se lee con un lector estándar y se comprueba la marca, el separador y las tildes; el PDF se lee con una librería de pruebas (PdfPig, solo en el proyecto de pruebas) para comprobar **que el texto es seleccionable**, el título, los filtros y «Página X de Y».
- **Los filtros se respetan en los tres formatos:** misma consulta, mismas filas en pantalla (JSON), Excel, CSV y PDF.
- **Coherencia con el dashboard:** los ingresos y el saldo por cobrar del gerencial y de la cobranza **son iguales** a los de `GET /indicadores/resumen` para el mismo período, y la suma de las filas es igual al total.
- **Fallos a propósito:** quitar la validación del rol en un reporte, quitar el escape de fórmulas y mover un filtro, para comprobar que las pruebas lo detectan.
- **ECC:** `csharp-reviewer` y `security-reviewer` (permisos por reporte, inyección de fórmulas, descarga de archivos ajenos, recorrido de rutas, agotamiento de recursos, secretos).

**13B (frontend)**
- **Vitest:** botones por rol y pestañas por rol; la máquina de estados de la descarga (preparando, listo, fallido, reintento); los filtros viajan tal cual a la API; los avisos; las reglas de `@media print`; el contraste de los tres botones en `tokens.test.js`.
- **Playwright con axe a 360, 768 y 1440 px:** las tres pestañas, los botones, un trabajo en preparación y listo, un error, el cajón del teléfono, sin desplazamiento horizontal; la descarga se comprueba con el evento de descarga; la vista de impresión con `emulateMedia('print')`.
- **ECC:** `react-reviewer` y `a11y-architect`.
- **Verificación en la app real**, con tu sesión de dueño y, si quieres, la de un trabajador: genero los tres reportes en los tres formatos contra la base demo y **comparo las cifras con una consulta SQL independiente de solo lectura**. Esto **escribe** en la demo (los trabajos y la bitácora): te aviso antes. Abrir el `.xlsx` en Excel y el PDF en tu visor es una comprobación que solo puedes hacer tú; te dejo la lista. Sin contraseñas en el repositorio.

**Cierre de cada parte:** informe `informes/fase-13a.md` y `fase-13b.md`, tablero, trazabilidad (RFS10, RFS12, RFU24 a RFU26, RFU28, RFU36, RNFP18, E16, E20, S18, S20, S24), `contrato.md`, `permisos.md`, `openapi.json`, diccionario de datos y arquitectura (flujo de trabajos), `desviaciones.md` y commit en la rama. Los comandos de GitHub te los paso; yo no subo nada.

## Decisiones y supuestos que te dejo a la vista

1. **Dos dependencias nuevas, MIT las dos, a tu aprobación:** **DocumentFormat.OpenXml** (Microsoft) para escribir el Excel con formatos y totales, y **PDFsharp/MigraDoc** para el PDF (tablas, encabezado repetido y «Página X de Y»); los gráficos del PDF los dibujo con SkiaSharp, que ya está en el proyecto. Alternativas: ClosedXML (más cómoda, pero arrastra una dependencia con licencia dividida que prefiero no aceptar sin verificarla) y QuestPDF (muy buena, con una licencia comunitaria con condiciones). Verifico versión, licencia y vulnerabilidades antes de instalar.
2. **Quién ve qué (sección 1):** el reporte operativo para **todo el personal** (tus requisitos RFU24 y RFU26 dicen «dueño, trabajador»), e incluye montos y saldos; el gerencial y el parametrizable, solo para **dueño y administrador**. Si el diseñador y el operario no deben ver montos, lo oculto en su versión.
3. **Dividir en 13A y 13B** (sección 0), con parada en cada una.
4. **Archivos que vencen a las 24 horas** (sección 2). Si prefieres otro plazo, es un solo valor.
5. **CSV del operativo = solo la tabla de pedidos** (sección 3). Otra opción es un `.zip` con un CSV por tabla; lo evito para no complicar la descarga.
6. **CSV con separador `;` y marca UTF-8** (sección 3), pensado para abrirlo con doble clic en Excel de Chile. Si lo vas a leer con otras herramientas, dime y uso `,`.
7. **Botones:** tres botones separados, sin la flecha hacia abajo de tus imágenes, porque no abren ningún menú (la flecha sugiere uno). Si la flecha significa algo que quieres, por ejemplo elegir entre «con filtros» y «todo», dímelo.
8. **El PDF no es accesible etiquetado (PDF/UA)**; sí tiene texto seleccionable, como piden los requisitos. Queda anotado en `desviaciones.md`.
9. **La migración solo en la demo** hasta el final, igual que las anteriores.
