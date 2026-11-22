# Plan de la fase 15 — Calidad: rendimiento, accesibilidad y compatibilidad

Fecha: 21-11-2026 · Rama: `fase-15-calidad` (nace de `fase-14c-google-personal`, una vez commiteada) · Estado: **propuesto, esperando tu aprobación**; construcción sin iniciar

Origen: el plan general (`plan-de-construccion.md`, fase 15, con el ajuste del 20-11-2026: Chrome, Edge y Brave) y los requisitos RNFP01, RNFP02, RNFP03, RNFP05, RNFP15, RNFP23, RNFP24, RNFP27, RNFO13, RNFE08 y RFU29, más lo que las fases anteriores dejaron anotado para ésta (`desviaciones.md`: la lista de clientes del filtro «Cliente», RFU27; la revisión de llamadas duplicadas, RNFP23; la medición del código dividido, RNFP24; la prueba con NVDA, RFU29).

Esta fase **no agrega funciones**: mide, corrige y deja evidencia. Casi todo el código nuevo son pruebas y guiones; las correcciones dependen de lo que se mida. **Sin migración y sin dependencias de producción nuevas** (Playwright, axe y Vitest ya están; el guion de carga es de Node.js sin paquetes).

## 0. Cómo se divide (como la 13 y la 14)

- **15A — Medir y corregir el backend.** Guion de carga con 20 usuarios, medición de tiempos por ruta, la búsqueda de clientes en el servidor y las correcciones de rendimiento que salgan. Parada con informe.
- **15B — Medir y corregir el frontend.** Llamadas duplicadas, código dividido y peso, red lenta y móvil, Playwright en Chrome, Edge y Brave, axe en **todas** las rutas, Lighthouse, y el guion de prueba con teclado y NVDA. Parada con informe.

Las dos partes pueden construirse una tras otra en la misma rama; la 15B no depende del código de la 15A, salvo la búsqueda de clientes (la API de la 15A y su pantalla de la 15B).

## 1. Qué hay hoy (para no medir a ciegas)

- **Backend:** compresión de respuestas activa (`AddResponseCompression`, RNFP25 en parte), consultas parametrizadas, límites de tasa por política, vistas e índices de indicadores (12B), exportaciones en segundo plano (13A). **Nadie ha medido** el 95 % de las respuestas en 2 segundos con 20 usuarios simultáneos.
- **Frontend:** cada página con `lazy` y `Suspense` (JavaScript inicial de 101,8 KB comprimido en la fase 3, presupuesto de 150 KB; la 14B sumó el botón de Google, que no carga nada de Google hasta que se pulsa), cliente HTTP que comparte lecturas iguales, `debounce` en los filtros, imágenes WebP con carga diferida, Leaflet solo bajo demanda. Playwright corre **solo en Chromium de escritorio**; el recorrido de `e2e/pantallas.spec.js` ya pasa axe a 360, 768 y 1440 px en la mayoría de las rutas, y las pruebas de Google (14B y 14C) añaden las suyas.
- **Pendientes anotados:** RFU27 (la lista de clientes del filtro «Cliente» trae solo los primeros 100), la revisión de «cada pantalla vuelve a pedir sus datos al abrirse» (RNFP23) y la medición real del código dividido (RNFP24).

## 2. Rendimiento del backend (15A) — RNFP01, RNFO13

- **Guion de carga propio** en `scripts/carga/` (Node.js nativo, sin paquetes): 20 «usuarios» simultáneos con una mezcla realista (portada, vitrina, horario, ingreso, Mi cuenta, solicitud en línea, panel del personal con sus listados, indicadores y un reporte), tiempos por ruta (mediana, p95 y máximo), tasa de errores y un resumen en `docs/evidencias/carga-15a.md`. Corre contra la API demo con la base `LetreriaLosHerreraDB_Demo`; **no usa la base real ni credenciales en el repositorio** (las toma de variables de entorno que tú pones al lanzarlo).
- **Meta de cierre:** el **95 % de las respuestas en 2 segundos o menos** con 20 usuarios simultáneos. Si una ruta no lo logra, se anota la **causa medida** (plan de ejecución, índice faltante, consulta N+1, serialización) y se corrige; lo que no se pueda corregir aquí (por ejemplo, el hardware de este equipo) queda como límite documentado, no escondido.
- **Qué se mira:** las rutas con listados y filtros (pedidos, solicitudes, clientes, auditoría), los indicadores y reportes (12B y 13A), el ingreso (BCrypt es lento a propósito: se mide qué parte del tiempo es esa), y el efecto de los límites de tasa con 20 usuarios desde una IP (que **no** deben dar `429` falsos al uso normal: si lo hacen, se ajusta el límite o se explica).
- **Búsqueda de clientes en el servidor (RFU27).** `GET /clientes` ya busca por `?q=` y pagina; el filtro «Cliente» de pedidos y solicitudes hoy trae los primeros 100 y los filtra en el navegador. Se cambia a **búsqueda en el servidor con `debounce`** (el contrato ya lo admite: se verifica y, si falta algo, se declara en `openapi.json`), con su prueba de carga con más de 1.000 clientes sintéticos.
- **Datos de prueba de carga:** un guion que **inserta datos sintéticos verosímiles** (nunca personales) en una base temporal de pruebas, no en la demo del usuario, y que los borra al terminar.
- **Pruebas primero:** las correcciones de rendimiento llevan una prueba de integración que falla antes (por ejemplo, «el listado con 1.500 clientes no trae más de una página»).

## 3. Rendimiento del cliente (15B) — RNFP23, RNFP24, RNFP27

- **Llamadas duplicadas (RNFP23).** Una prueba de Playwright que **cuenta las peticiones a la API** al abrir cada pantalla principal y al enviar cada formulario (ingreso, registro, solicitud, cotización, abono), y falla si una misma petición idéntica sale dos veces sin motivo (React StrictMode se descuenta en desarrollo). Se corrige lo que aparezca; lo que sea a propósito (por ejemplo, el reintento tras `503`) se anota.
- **Código dividido (RNFP24).** Medición del **JavaScript inicial** y de cada ruta (`vite build` con informe), con **presupuesto**: 150 KB comprimidos iniciales (el de la fase 3) y un tope por ruta; una prueba que falla si el build lo supera. Se revisa el aviso de fragmentos grandes de Vite (el del panel y el del reconocimiento facial) y se decide si se parte más. Ningún script de terceros carga al abrir la portada (Google solo al pulsar; lo comprueba la política de contenido de la 14B).
- **Red lenta y móvil (RNFP27).** Playwright con **perfil móvil** (Pixel y iPhone como tamaños, motor Chromium) y **throttling** de red (3G rápida y 4G lenta) y de CPU: las rutas clave (portada, vitrina, ingreso, solicitud en línea, Mi cuenta) cargan y funcionan sin pérdida de funcionalidad; se mide el tiempo hasta que se puede usar (LCP e INP aproximados) y se anota.
- **Lighthouse** sobre las rutas públicas y dos del panel (rendimiento, accesibilidad, buenas prácticas): se guardan los informes en `docs/evidencias/` y se corrige lo que baje de los umbrales que fijamos al construir (propuesta: rendimiento ≥ 85 en móvil simulado, accesibilidad ≥ 95, sin errores en consola).
- **Imágenes y fuentes (RNFP22, ya cumplido):** solo se revisa que no haya regresiones (peso de las imágenes de la portada, fuentes limitadas a los grosores usados).

## 4. Compatibilidad — RNFP05 (con la desviación del 20-11-2026)

- **Chrome, Edge y Brave** (los tres con motor Chromium), con Playwright usando los **ejecutables ya instalados** en el equipo (`channel: 'chrome'`, `channel: 'msedge'` y la ruta de Brave en `C:\Program Files\BraveSoftware\`), **sin descargar navegadores**. Se agregan tres proyectos a `playwright.config.js` y un perfil móvil; para no triplicar el tiempo, el recorrido completo de pantallas corre en Chrome y un **conjunto esencial** (portada, ingreso de cliente y de personal, registro, solicitud en línea, Mi cuenta, panel y Google con el script simulado) corre en los tres y en móvil.
- **Firefox, WebKit y Safari quedan fuera** por tu decisión (en `desviaciones.md`): no se prueban ni se aseguran. Si alguno de esos navegadores falla, no se corrige en esta fase.
- Se comprueba lo que depende del motor: el botón oficial de Google se prueba **con el script simulado** (el real solo lo prueba una persona), el `<dialog>`, `BroadcastChannel`, Web Locks y la cámara del ingreso con rostro (con una cámara falsa de Chromium).

## 5. Accesibilidad — RNFP15, RNFE08, RFU29, RNFP03

- **axe en todas las rutas.** Hoy `PANTALLAS` en `e2e/pantallas.spec.js` cubre la mayoría; se construye la **lista completa de rutas desde `App.jsx`** y una prueba que **falla si una ruta de la aplicación no está en el recorrido**, y se recorren todas a **360, 768 y 1440 px** con las reglas WCAG 2.2 A/AA: sin violaciones críticas ni serias y **sin desplazamiento horizontal**. Se agregan los estados que hoy no se miran (diálogos abiertos, errores de formulario, listas vacías, la cámara del rostro, los avisos de Google).
- **Más allá de axe:** pruebas de teclado automatizadas de los flujos clave (orden de foco, atrapar y devolver el foco en diálogos, Esc, que ningún control quede inalcanzable), zoom al 200 % y 400 % (reflow a 320 px) y `prefers-reduced-motion` y alto contraste forzado.
- **Guion de prueba manual con teclado y NVDA** (`docs/evidencias/guion-nvda.md`): pasos exactos por pantalla (ingreso, registro, solicitud, Mi cuenta, panel, formularios con errores, diálogos, el botón de Google y su zona, la cámara), qué debe anunciar el lector en cada paso y una tabla para anotar lo que no pase. **Esa prueba la ejecuta una persona** (tú): te dejo el guion y la hacemos juntos; los hallazgos se corrigen en la misma fase.
- **RNFP02 (5 minutos desde el celular):** se prepara el guion con cronómetro para que tú lo midas en un teléfono real (pendiente externo ya anotado); aquí solo se mide el flujo automatizado.

## 6. Lo que tienes que hacer tú

1. **Aprobar este plan** y decidir los umbrales propuestos (sección 7).
2. **Prueba con NVDA** y con teclado, guiada por el guion que te dejo (aprox. 1 hora).
3. **Medir los 5 minutos de RNFP02 en un teléfono** (opcional, pendiente externo, con su guion).
4. **Lanzar el guion de carga** con la API demo levantada (te digo cuándo; escribe datos sintéticos temporales).
Sin esto la fase queda «construida, pendiente de la prueba manual», como la 14.

## 7. Pruebas y revisiones

- **Pruebas primero** para lo que se corrija: Vitest (reglas puras, presupuestos de peso, deduplicación de lecturas), xUnit de integración (listados paginados, búsqueda en el servidor) y Playwright (conteo de peticiones, axe, teclado, red lenta, tres navegadores).
- **Fallos a propósito:** duplicar una petición al abrir una pantalla, inflar el JavaScript inicial por encima del presupuesto, quitar la etiqueta de un campo, y devolver 1.000 clientes sin paginar; las pruebas deben fallar.
- **ECC:** `performance-optimizer` (cuellos de botella, re-renderizados, consultas), `a11y-architect` (lo de la sección 5), `react-reviewer` y `csharp-reviewer` para el código que se toque, y `e2e-runner` para ordenar la estabilidad de las pruebas en los tres navegadores. Correcciones o anotación en `desviaciones.md`.
- **Cierre:** informes `informes/fase-15a.md` y `fase-15b.md`, tablero, trazabilidad (RNFP01, RNFP02, RNFP03, RNFP05, RNFP15, RNFP23, RNFP24, RNFP27, RNFO13, RNFE08, RFU29, RFU27), `desviaciones.md` y la evidencia en `docs/evidencias/`. Los comandos de git te los paso; yo no subo nada.

## Decisiones y supuestos a tu vista

1. **Metas de cierre:** 95 % de respuestas ≤ 2 s con 20 usuarios (es el requisito); **JavaScript inicial ≤ 150 KB comprimidos**; **Lighthouse móvil: rendimiento ≥ 85 y accesibilidad ≥ 95** (propuesta mía: ¿las suben o bajan?); axe sin críticas ni serias; sin desplazamiento horizontal de 360 a 1440 px.
2. **El guion de carga corre contra la API demo y una base temporal**, nunca contra la base real ni contra tus datos; los datos sintéticos se borran al terminar. Los resultados dependen de este equipo (Windows, SQL Server Express, sin Docker): se documenta como tal; la prueba en la nube de la fase 16 puede dar otras cifras.
3. **Navegadores:** Chrome, Edge y Brave con los ejecutables instalados, sin descargas; el recorrido completo en Chrome y el conjunto esencial en los tres y en móvil. Brave puede traer su bloqueador de rastreadores activo: se prueba tal cual viene (si bloquea algo propio, es un hallazgo).
4. **Búsqueda de clientes en el servidor** se entrega en la 15A (API y prueba de carga) y la 15B (pantalla con `debounce`).
5. **No se agrega ninguna dependencia de producción** y no hay migración; si una corrección de rendimiento necesitara un índice, sería una migración aparte **a tu aprobación**.
6. **La fase 16 (contenedores y nube) queda como está**: la compresión y la caché HTTP de producción (RNFP25) se cierran allí; aquí solo se mide con lo que ya hay.
