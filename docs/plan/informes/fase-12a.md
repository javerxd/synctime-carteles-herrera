# Informe de cierre — Fase 12A: dashboard (contrato y frontend)

Fecha: 16-11-2026 · Rama: `fase-12a-dashboard-frontend` (nace de `fase-11-notificaciones-auditoria-y-privacidad`) · Estado: **esperando tu visto bueno**

Plan aprobado: `fase-12a-plan.md` (16-11-2026), con los gráficos en SVG propio, el aviso «Datos de demostración» con `VITE_INDICADORES_DEMO` y el `openapi.json` sincronizado con `ContratoTests`. Incluye tus tres precisiones del mismo día. Contrato: `fase-12-especificacion.md`.

## Qué se hizo

### Contrato primero
- **`fase-12-especificacion.md`:** seis rutas `GET /api/v1/indicadores/*` (solo gestión), filtros comunes, JSON exacto de cada respuesta y las fórmulas literales del plan general.
- **`openapi.json`:** las seis rutas, con sus parámetros y esquemas, van bajo `x-planificadas`, fuera de `paths`. **`ContratoTests` sigue en verde (3 de 3)**; la 12B las pasa a `paths` en el mismo commit que las implementa.

### Adaptador de demostración (precisión 1)
- **`src/api/indicadoresDemo.js`** calcula en el navegador las respuestas exactas del contrato; **`src/api/indicadores.js`** elige entre la demo y la API real con `VITE_INDICADORES_DEMO`.
- **Datos de ejemplo** (`datosDemoIndicadores.js`, con semilla fija): de octubre de 2025 a hoy, **todos los meses de 2026, enero incluido**. Traen pedidos anulados, terminados y entregados sin pagar, abonados, los dos orígenes, 12 clientes y 5 categorías. Una prueba exige cada uno de estos casos.
- **La pantalla avisa «Datos de demostración»**, y el inicio lo indica con una insignia.

### Pantalla `/panel/indicadores`
- **Filtros combinables en la dirección:**
  - período (Semana, Mes o Año), rango propio, acceso por año y mes, y comparación (período anterior, mismo período del año anterior o ninguna);
  - estado del pedido y estado de pago (varios, con el atajo «Por cobrar»);
  - categoría, origen y cliente buscable.
- **Siete tarjetas** con su variación, flecha y signo (no solo color): ingresos, pedidos, en línea y presenciales, concretadas, conversión (en puntos), ticket promedio y entregas a tiempo (con la meta del 90 %).
- **Cuatro gráficos en SVG propio**, cada uno con título, una frase que resume la tendencia y el conmutador «Gráfico / Tabla de datos»:
  - evolución (línea con área y comparación punteada; se recorre con las flechas);
  - cobranza (anillo de tres estados con el saldo al centro);
  - embudo del canal web;
  - demanda por categoría (por monto o por unidades).
- **«Pedidos por cobrar»:** los estados, con «Listo y sin pagar» destacado; el monto, lo abonado, el saldo y el total al pie.
- **Inicio del dueño y del administrador:** el saldo de toda la deuda vigente y los ingresos, los pedidos y la conversión del mes, con un enlace al dashboard.
- **Política de privacidad:** dice que la IP de la auditoría se borra a los 90 días. La decisión quedó en `desviaciones.md` y en el commit del plan.

### Carga en paralelo con fallo aislado (precisión 2, RNFP23)
- **`useIndicadores.js`** dispara los bloques a la vez y espera con `Promise.allSettled`.
- **Cada bloque** se muestra apenas llega y tiene su propia cancelación, su esqueleto, su estado vacío y su error con **«Reintentar», que vuelve a pedir solo ese bloque**.
- **Un cambio de filtro** vuelve a pedir solo los bloques que dependen de él. La página de la tabla solo recarga la tabla, y el alcance del saldo no recarga la evolución, el embudo ni la demanda.
- **Si un bloque falla al dibujarse** (una respuesta con otra forma), solo ese bloque lo avisa.

### Saldo por cobrar claro (precisión 3, S24)
- **La tarjeta y la tabla** dicen siempre a qué corresponde el saldo:
  - **«Saldo del período»**, con el nombre del período;
  - **«Toda la deuda vigente»**, sin límite de fecha e incluidos los cobros antiguos.
- **Se cambia con un clic** desde las dos (`alcanceSaldo=vigente`).
- **El inicio del dueño** muestra siempre toda la deuda vigente.
- **Un clic en «No pagado» o «Abonado»** del anillo filtra la tabla, y se ve como un chip que se puede quitar.

## Pruebas
- **Vitest: 635 de 635.** Son 43 nuevas:
  - las cifras del adaptador contra cuentas hechas a mano: sin anulados, ingresos de los entregados, concretadas, conversión en puntos, entregas a tiempo y saldo del período o vigente;
  - cada filtro, los períodos y la comparación;
  - la carga en paralelo, el fallo aislado y «Reintentar»;
  - la respuesta tardía, StrictMode y el fallo al dibujar;
  - el clic en el anillo, el conmutador de tabla y los estados vacío y de error.
- **Playwright: 218 de 218.** `/panel/indicadores` y `/panel` pasan a 360, 768 y 1440 px, sin desborde ni fallas graves de axe. Hay una prueba nueva del flujo anillo → tabla, tabla de datos y alcance del saldo a 360 px.
- **Backend:** `ContratoTests` pasa 3 de 3 con el `openapi.json` nuevo.
- **`npm run lint`:** sin avisos en los archivos nuevos. **`npm run build`:** correcto; la pantalla pesa 27 kB (8 kB comprimida) y no hay dependencias nuevas.

## Revisiones de ECC
- **`react-reviewer`:** no encontró carreras ni problemas con StrictMode. Se corrigieron:
  - el límite de errores por bloque;
  - la demo, que ahora solo se enciende sola en desarrollo;
  - los filtros de la dirección que no están en el catálogo, que ahora se ven y se pueden quitar;
  - la barra de filtros, que toma el período de la dirección;
  - la recarga selectiva por bloque;
  - que «cobro» ya no pisa el estado de pago elegido;
  - guardas ante respuestas incompletas;
  - pruebas nuevas.
- **`a11y-architect`:** se corrigieron:
  - el filtro de cliente (un solo campo, se aplica al elegir de la lista y conserva el foco);
  - los nombres estables de la leyenda, con la instrucción aparte;
  - el anuncio del resultado de la tabla;
  - un solo anuncio de carga para toda la página;
  - el gráfico de evolución (nombre corto, instrucciones aparte y valor al recibir el foco);
  - la ayuda del rango propio;
  - las tablas desplazables como grupo y no como región.
  - **Quedan para la fase 15** dos puntos de prioridad baja: los botones «Tabla de datos» repetidos en cada bloque y las tarjetas sin `<dl>`.
- **`security-reviewer`** (lo hice yo): no hay HTML inyectado, almacenamiento local ni secretos. Los filtros se validan en la dirección antes de viajar, y no hay archivos `.env` rastreados (RNFO15).

## Desviaciones y pendientes (detalle en `desviaciones.md`)
- **Versión de la política de privacidad:** la línea de los 90 días se agregó sin subirla de la 1.0. **Te toca decidir** si pasa a 1.1 en la 12B, lo que pide de nuevo el consentimiento.
- **Mientras dure la demo**, el inicio mezcla los indicadores de ejemplo (con su insignia) con las cifras reales de «Entregas y cobranza».
- **Pruebas intermitentes:** dos pruebas antiguas (`cotizaciones` y `pedidosFase8`) fallaron a veces al correr la suite completa y pasaron al repetirla. No son de esta fase; quedan para la fase 15.
- **12B:** el backend con vistas SQL e índices, la purga de la IP a los 90 días, el paso de las rutas a `paths` y `VITE_INDICADORES_DEMO=false`.
- **Trazabilidad:** los requisitos de la fase quedan **«En curso»** hasta la 12B.
