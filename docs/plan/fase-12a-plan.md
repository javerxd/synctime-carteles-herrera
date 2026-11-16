# Plan de la fase 12A — Dashboard (solo frontend y contrato)

Fecha: 16-11-2026 · Rama: `fase-12a-dashboard-frontend` (nace de `fase-11-notificaciones-auditoria-y-privacidad`) · Estado: **propuesto, esperando tu aprobación**

La fase 12 del plan general se divide en dos partes, como pediste el 16-11-2026:
- **12A (esta):** el contrato de la API y todo el frontend del dashboard, con datos de demostración que cumplen ese contrato. No toca el backend ni crea migraciones.
- **12B (después):** el backend, las vistas SQL y los índices, la purga de la IP de la bitácora y la conexión real del frontend.

Requisitos: RFS09, RFU19, RFU27, RFU36, E16, E21, E27, S10 a S14, S19, S24, RNFO05, RNFO07, RNFO16, RNFO17, RNFP15, RNFE08.

## 0. Decisión ya tomada: IP de la bitácora (90 días)
- **Plazo:** pasados 90 días se borra **solo la IP**; el registro de auditoría queda intacto. Es el mismo criterio que los correos fallidos.
- **Dónde queda escrito:** en `desviaciones.md` y en la política de privacidad (`textos.js`).
- **Purga:** la tarea que la borra se construye en la 12B.

## 1. Contrato primero
**`docs/plan/fase-12-especificacion.md` y `docs/api/openapi.json`** definen rutas, filtros y JSON exactos. Así el frontend no supone campos y el backend de la 12B encaja directo.
- **Rutas** (todas solo gestión, en `/api/v1/indicadores`):
  - `GET /resumen`: las 7 tarjetas, cada una con su valor actual, el del período anterior y la variación (en puntos porcentuales para los porcentajes).
  - `GET /evolucion`: serie temporal de ingresos y pedidos, actual y de comparación.
  - `GET /cobranza`: los tres segmentos de pago y el saldo total por cobrar.
  - `GET /embudo`: solicitudes en línea, cotizadas, aprobadas y pedidos concretados.
  - `GET /demanda`: monto y cantidad por categoría.
  - `GET /por-cobrar`: listado paginado de pedidos con saldo, con su total.
- **Filtros comunes:** `periodo` (semana, mes o año), `desde` y `hasta`, `anio` y `mes`, `comparar`, `estadoPedido` (varios), `estadoPago` (varios o `por_cobrar`), `categoria`, `origen` y `cliente`.
- **Fórmulas literales del plan general:**
  - pedidos sin anulados;
  - ingresos = monto final de los entregados;
  - concretadas = en línea con un pedido no anulado;
  - saldo por cobrar = pedidos no anulados en «no pagado» o «abonado».
- **Fuera de OpenAPI hasta la 12B:** las rutas no se agregan todavía a la lista que exige `ContratoTests`. Esa prueba compara el documento con la API que existe, y estas rutas aún no existen. Las agrego bajo una marca de «planificadas» si la prueba lo permite; si no, quedan solo en la especificación y entran en el `openapi.json` con la 12B.

## 2. Capa de datos de demostración
- **Mientras no exista el backend:** `src/api/indicadores.js` usa un adaptador que **genera las respuestas exactas del contrato** a partir de un conjunto fijo de pedidos y solicitudes de ejemplo.
- **Filtros reales:** filtra, agrupa y compara de verdad en el navegador, así que cambiar un filtro cambia los números.
- **Cambio a la API real en la 12B:** se hace con una sola configuración (`VITE_INDICADORES_DEMO`) sin tocar las pantallas.
- **Aviso visible:** la pantalla muestra «Datos de demostración» mientras use el adaptador.

## 3. Pantalla de indicadores (`/panel/indicadores`) y el inicio del dueño
- **A. Barra de filtros combinables:**
  - período (Semana, Mes o Año) con rango personalizado, acceso por año y mes, y comparación con otro período;
  - estado del pedido;
  - estado de pago, con el acceso rápido «Por cobrar»;
  - categoría, origen y cliente (buscable).
  - Los filtros viven en la dirección, para que se puedan compartir y recargar.
- **B. Siete tarjetas** con su comparación:
  - ingresos (S11), pedidos (S10), en línea frente a presenciales (S12) y concretadas (S13);
  - conversión en línea en pp (S14), ticket promedio y entregas a tiempo;
  - **saldo por cobrar (S24)**, destacado como alerta.
- **C. Cuatro gráficos:**
  1. **Evolución comercial:** línea con área para el período actual y línea punteada para el de comparación.
  2. **Cobranza:** anillo de tres segmentos con el saldo en el centro. Un clic en un segmento filtra la tabla de abajo.
  3. **Embudo del canal web**, con el porcentaje de cada paso.
  4. **Demanda por categoría:** barras horizontales, de mayor a menor, con un conmutador entre monto y cantidad.
  - Opcional, solo si no recarga la vista a 360 px: una barra de meta del 90 % para las entregas a tiempo.
- **D. «Pedidos por cobrar»:** tabla adaptable y filtrable con el cliente, el estado (destacando los terminados y entregados sin pagar), el monto final, lo abonado y el saldo, con el total al pie.
- **Inicio del dueño y del administrador:** las métricas principales, que son el saldo, los ingresos, los pedidos y la conversión, con un enlace al dashboard.

## 4. Gráficos sin librería
- **SVG propio**, como el `GraficoBarras.jsx` de la fase 3: sin dependencias nuevas, así que no suma peso ni licencias (RNFO16, RNFO17).
- **Si una librería resultara necesaria**, la elegiría ligera y de código abierto, te diría su peso y su licencia, y la agregaría solo con tu visto bueno.

## 5. Narrativa, accesibilidad y formato
- **Cada gráfico:** título y una frase que resume la tendencia (por ejemplo, «Los ingresos subieron 12 % respecto del mes anterior»), más un conmutador **«Gráfico / Tabla de datos»** con una tabla semántica.
- **No depende solo del color:** etiquetas directas, patrones en el período de comparación e iconos en las alertas. La paleta se valida con el script de `dataviz`.
- **Contraste:** 4,5:1 en el texto y 3:1 en los componentes. Todo se opera con teclado.
- **Sin desplazamiento horizontal** a 360, 768 y 1440 px.
- **Formato chileno:** montos sin decimales (`$120.000`) y fechas `dd-mm-aaaa`.
- **Estados:** cargando (con *skeletons*), vacío y error con «Reintentar», en cada bloque.
- **Movimiento:** transiciones sutiles en el selector de período, en el conmutador y en los tooltips; ninguna con `prefers-reduced-motion`.

## 6. Orden de diseño
1. **`dataviz` y `ui-ux-pro-max`:** la arquitectura de la información, los cuatro gráficos, los tokens, los estados y la accesibilidad.
2. **`impeccable`:** crítica y pulido de la densidad, la jerarquía y el uso disciplinado de la paleta (blancos y grises, tinta `#0F172A`, ámbar `#B45309` solo en activos y acciones).
3. **`apple-design` y `emil-design-eng`:** solo las micro-interacciones puntuales.

## 7. Pruebas y cierre
- **Vitest primero:**
  - los cálculos del adaptador contra cuentas hechas a mano: sin anulados, ingresos de entregados, conversión en pp y saldo por cobrar;
  - cada filtro y el clic en el anillo;
  - el conmutador de tabla y los estados de carga, vacío y error.
- **Playwright con axe** a 360, 768 y 1440 px. También `npm run lint` y `npm run build`.
- **Revisiones de ECC:** `react-reviewer`, `security-reviewer` y `a11y-architect`.
- **Al cerrar:** se actualizan la especificación, el tablero, la trazabilidad (con estado «En curso» hasta la 12B), las desviaciones y el informe `fase-12a.md`, y se hace el commit en la rama.

## 8. Subir a GitHub (entrega al cerrar)
- **Revisión previa:** compruebo que no haya credenciales, secretos ni archivos `.env` rastreados ni pendientes (RNFO15), y reviso las ramas locales y el remoto.
- **Comandos:** te entrego los comandos exactos de PowerShell y Git para subir todo a `https://github.com/javerxd/synctime-carteles-herrera.git`, con mi recomendación sobre subir la rama actual o también sincronizar `main`.
  - **Los ejecutas tú;** yo no hago push.
