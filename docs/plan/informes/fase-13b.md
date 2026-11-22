# Informe de cierre — Fase 13B: frontend de reportes y exportaciones

Fecha: 21-11-2026 · Rama: `fase-13b-reportes-frontend` (nace de `fase-13a-reportes-backend`) · Estado: **aprobada el 21-11-2026**; commit en la rama, sin push

Plan: `fase-13-plan.md` (secciones 5 y 6) con los ajustes que aprobaste al cerrar la 13A: los tres botones inactivos mientras se prepara un archivo, y el enlace del dashboard a la cobranza con `alcanceSaldo`. Sin migración ni cambios en la API.

## Qué se hizo

- **`/panel/reportes` lee la API** (antes era un reporte provisional que armaba cifras en el navegador). Tres pestañas: **Operativo** (todo el personal, también diseñador y operario), **Gerencial** y **Parametrizable** (solo dueño y administrador: el resto ni ve la pestaña ni pide el reporte). Cada una muestra **la misma tabla que luego se exporta**, con carga, vacío y error por bloque. Los filtros viven en la dirección (período, rango propio, año, mes, estado de pedido y de pago, cliente, alcance del saldo y, en el gerencial, «Comparar con»), así que un enlace reproduce la pantalla.
- **Gerencial:** resumen con su variación, el desglose de los tres costos y el margen (ingresos con costo − materiales − otros costos, con el ajuste del 21-11-2026), tramos, tipos de letrero y detalle de pedidos entregados (con las columnas «Materiales» y «Otros costos» a la vista, para verificar el margen), con dos gráficos y su tabla debajo. Los pedidos «Sin costo» se ven como tales.
- **Tres botones de exportar** (Excel verde, CSV azul, PDF rojo, texto blanco real, íconos SVG, colores como tokens): piden el archivo con los filtros de pantalla, **quedan inactivos los tres mientras se prepara uno** (siguen enfocables), anuncian «Preparando tu archivo» y «Tu archivo está listo» en una región de estado, con «Descargar de nuevo»; un fallo es una alerta con el motivo y «Reintentar» (sin él cuando no se arregla reintentando: reporte muy grande o sin permiso). La pantalla sigue usable mientras tanto; si cambias de página, la consulta se corta y el trabajo sigue en el servidor.
- **Imprimir** (`@media print`): hoja limpia con título, emisión en hora de Chile, filtros aplicados y las tablas; sin menú, pestañas, filtros ni botones.
- **Dashboard:** «Pedidos por cobrar» tiene el enlace «Exportar cobranza» al parametrizable por cobrar, con el período, el alcance del saldo y el cliente del dashboard.
- **Menú «Reportes»** para todos los roles, con las pestañas que cada uno puede ver.
- **Código retirado:** `panel.pedidos()`, `reglasPedidos.js` (con su prueba) y `GraficoBarras`, que solo usaba el reporte provisional.

## Pruebas

- **Vitest: 835 de 835** (nuevas: `fase13Reglas`, `fase13Exportacion` —estado de la descarga, errores, reintento, foco, tiempo máximo— y `fase13Pantalla` —pestañas por rol, tres reportes, filtros, carga/vacío/error, tope de filas, exportar—; más el contraste de los botones en `tokens.test.js`). Con el equipo ocupado, `pedidosFase8.test.jsx` llegó a fallar una vez por tiempo y pasó al repetirla (ya está en `desviaciones.md`).
- **Playwright + axe:** **suite completa 262 de 262** (7,8 min), sin romper ninguna otra pantalla.
  Lo de reportes: 18 pruebas (la 18.ª, de la impresión: papel apaisado, título y tabla como tabla); reportes a 360, 768 y 1440 px (sin desborde horizontal ni fallas graves de accesibilidad) en los tres reportes; pedir un Excel simulado (preparando → listo → evento de descarga con el nombre del servidor, y los filtros que viajaron); fallo con «Reintentar»; el gerencial con sus gráficos; el diseñador solo con el operativo y la entrada en el cajón del teléfono; el enlace del dashboard; y la vista de impresión (con `emulateMedia`).
- **Lint** (`oxlint`): sin errores nuevos (solo los avisos de siempre) · **`vite build`**: compila.

## Revisiones de ECC y qué se hizo con ellas

- **React:** sin hallazgos críticos. **Corregido:** una petición colgada dejaba los botones inactivos para siempre (ahora cada llamada vence a los 30 s); un fallo pasajero de red al consultar el estado perdía el trabajo y «Reintentar» pedía otro (el servidor respondería 409): ahora se toleran tres fallos y «Reintentar» retoma ese trabajo; «Reintentar» usaba los filtros de la pantalla de ahora y no los del pedido; doble descarga con dos clics rápidos; una tabla de 50.000 filas congelaría el navegador (tope de 1.000 en pantalla, con aviso); claves de lista por posición y un porcentaje no numérico que mostraba `NaN`.
- **Accesibilidad (WCAG 2.2):** **corregido:** el foco se perdía al cerrar el aviso o pulsar «Reintentar» (vuelve al botón del formato, sin robarlo al abrir la pantalla); el aviso de «cargando» nacía con su texto y los lectores no lo anunciaban (ahora la región vive siempre montada); los gráficos solo se distinguían por color (la segunda serie lleva rayas), con etiquetas del eje que no cabían en el teléfono (abreviadas) y un marco que se desplaza sin ser enfocable (ahora lo es); las listas sin viñetas pierden su rol en algunos lectores (`role="list"`); el vacío de una tabla sin su nivel de encabezado; y el aviso de fallo no decía de qué archivo hablaba.

## Pendientes y límites

- **Tu comprobación en la aplicación real** (no la hice yo: necesita tu sesión de dueño y escribe en la base demo; no toqué la real). Pedir los tres archivos crea filas en `trabajos_exportacion` y en la bitácora de la demo, y los archivos viven 24 horas en la carpeta de exportaciones de la API. Para comprobarla:
  1. Levantar API y frontend contra la demo, entrar como dueño y abrir **Reportes**.
  2. En el **Operativo**, con el mes de datos que tengas, pulsar **Exportar a Excel**: debe decir «Preparando Excel…», dejar los tres inactivos y bajar el archivo. Repetir con **CSV** y **PDF**.
  3. **Abrir el `.xlsx` en Excel** (hojas, encabezado fijo, totales, fechas y pesos como números) y **el `.pdf` en tu visor** (texto seleccionable, encabezado repetido, «Página X de Y»; en el gerencial, los gráficos). Abrir el `.csv` en Excel: debe quedar en columnas, con tildes y ñ bien.
  4. **Comparar cifras:** los totales de la pantalla con una consulta de solo lectura a la base demo (o con el dashboard del mismo período: ingresos y por cobrar deben coincidir).
  5. En el dashboard, **«Exportar cobranza»** debe abrir el parametrizable por cobrar con los mismos filtros.
  6. Entrar como diseñador u operario: solo debe aparecer la pestaña Operativo.
  7. Probar **Imprimir** (Ctrl+P) en cada reporte: sin menú ni botones.
- **El PDF no es accesible etiquetado** (decisión 8 del plan, ya anotada en `desviaciones.md`): los mismos datos están en Excel y CSV.
- **Impresión (ajustada tras tu prueba del 21-11-2026):** sale en A4 apaisado, con el título del reporte y sin el enlace de salto; si se fuerza el papel vertical, las tablas salen como fichas. Sale con las primeras 1.000 filas por tabla, igual que la pantalla; para imprimir todo, el PDF.
- **Commit:** nada está commiteado en la rama; lo haré cuando des el visto bueno.

## Documentos actualizados

`trazabilidad.md` (RFS10, RFU24, RFU25, RFU26, RFU36, RNFP18, E16, E20, S18 y S24: Cumplido), `tablero.md` (13B en revisión) y `desviaciones.md` (decisiones y pendientes de la 13B).
