# Informe de cierre — Fase 15B: rendimiento, accesibilidad y compatibilidad del frontend

Fecha: 22-11-2026 · Rama: `fase-15-calidad` (sobre el commit de la 15A, `67049ba`) · Estado: **construida, en revisión; parada para tu visto bueno. Falta tu parte: la prueba con NVDA y el teléfono** · Commit y push los haces tú (comandos al final)

Plan: `fase-15-plan.md` (secciones 3, 4 y 5). **Sin migración y sin dependencias nuevas** en el proyecto (el paquete `lighthouse` se bajó con `npx`, con tu aprobación, y no queda en `package.json`).

## Resultado en una línea

**Las metas se cumplen con holgura en las mediciones automáticas:** JavaScript inicial de 117,5 KB (presupuesto 150), Lighthouse móvil de 93 a 100 en rendimiento y 100 en accesibilidad (umbrales 85 y 95), axe sin fallas graves ni desplazamiento horizontal en las 55 direcciones de la aplicación a 360, 768 y 1440 px, y las pruebas pasan en Chrome, Edge y Brave. **Lo que no se puede medir sin personas queda en tus manos:** la prueba con teclado y NVDA y el cronómetro de 5 minutos en un teléfono.

## Qué se hizo

1. **Filtro «Cliente» con búsqueda en el servidor (RFU27).** Indicadores y reportes dejan de traer 100 clientes: `GET /clientes?q=&tamano=10`, con espera de 300 ms desde la última tecla, desde la segunda letra y cancelando la búsqueda anterior. Dos clientes con el mismo nombre se distinguen por su número, la lista muestra teléfono y RUT, un texto de estado anuncia «N clientes encontrados / Sin coincidencias» y que el filtro se aplicó o se quitó, y el nombre del cliente que viene de la dirección se pide una vez. 17 pruebas (`fase15bFiltroCliente.test.jsx`).
2. **Llamadas duplicadas (RNFP23).** Prueba de Playwright que anota las peticiones a la API de 29 pantallas y 5 formularios (ingreso, registro, solicitud, cotización y abono) y falla si una idéntica sale dos veces (`llamadasDuplicadas.spec.js`, detector con 8 pruebas propias). **Hallazgo corregido:** las miniaturas de adjuntos se descargaban dos veces en desarrollo porque la descarga no se cancelaba; ahora sí. Evidencia: `docs/evidencias/llamadas-15b.md`.
3. **Presupuesto de peso (RNFP24).** Una prueba construye la aplicación y falla si el JavaScript inicial pasa de 150 KB comprimidos, si una ruta pasa de 40 KB o si una pieza pesada bajo demanda no tiene tope o lo supera. Hoy: inicial 117,5 KB; CSS inicial 12,8 KB (tope 20); ruta más pesada 35,2 KB; mapa ≈ 50 KB y reconocimiento facial ≈ 320 KB, solo cuando se usan. El aviso de Vite por el fragmento de 1,3 MB (la biblioteca del rostro, que no se puede partir) queda decidido y cubierto por su tope.
4. **Red lenta y teléfono (RNFP27).** 16 pruebas (Pixel 7 e iPhone 14, 3G rápida y 4G lenta, CPU 4 veces más lenta) contra el build de producción con gzip: portada, ingreso, solicitud en línea y Mi cuenta funcionan completas; usables en 3,9 a 5,3 s en 3G y en menos de 2 s en 4G; ningún servicio de terceros al abrirlas. **Tres defectos reales corregidos** (el diseño se movía hasta 0,85 de CLS; ahora ≈ 0): el esqueleto de carga no llenaba la ventana y el pie de página saltaba; la barra del teléfono se estiraba en pantallas cortas; las fuentes llegaban tarde y los botones de la portada pasaban de una fila a dos.
5. **Chrome, Edge y Brave (RNFP05).** Con los ejecutables instalados, sin descargar nada: Chrome 157 corre el recorrido completo (381 pruebas); Edge 158, Brave 158 y el teléfono (Pixel 7) corren el conjunto esencial etiquetado `@esencial` (69 pruebas cada uno). Brave, con su bloqueador, no bloqueó nada propio.
6. **axe en todas las rutas (RNFP15, RNFE08, RNFP03).** La lista del recorrido salió de `App.jsx` y una prueba falla si una ruta no está en él (55 direcciones, incluidas la página «no encontrada» del sitio y del panel y las direcciones antiguas que redirigen). A 360, 768 y 1440 px: sin violaciones críticas ni serias y sin desplazamiento horizontal. Además: estados (diálogo de abono con foco atrapado y devuelto, errores del ingreso, lista vacía), recorrido con Tab de 7 pantallas (foco visible y con nombre), reflujo a 320 × 256 px, alto contraste forzado y movimiento reducido. **Hallazgo corregido:** el cartel que se balancea ensanchaba la portada 6 px a 320 px.
7. **Lighthouse móvil y guion manual.** `docs/evidencias/lighthouse-15b.md` (cinco páginas: rendimiento 100, 100, 96, 93 y 95; accesibilidad 100 en todas) y `docs/evidencias/guion-nvda.md` (guion de teclado y NVDA, ≈ 1 hora, con la tabla para anotar lo que falle).

## Pruebas

- **Vitest: 54 archivos y 967 pruebas, todas en verde** (nuevas: filtro 17, archivo privado 4, detector de llamadas 8, presupuesto 10, rutas 10).
- **Playwright:** Chrome 381 de 381, red lenta 16 de 16, Edge + Brave + teléfono 207 de 207.
- **Pruebas primero y fallos a propósito, todos detectados y el código restaurado:** quitar la espera o pedir 100 resultados en el filtro (falla 1); una lectura duplicada al abrir una pantalla (falla con «2 × GET /api/v1/horarios»); inflar el JavaScript inicial importando el reconocimiento facial en la entrada (falla con 437 KB); quitar una ruta del recorrido (falla y nombra la ruta). Las pruebas de la descarga cancelada y del filtro nuevo se escribieron antes del código y fallaron primero (3 y 11 fallos).
- **Lint:** sin avisos en lo nuevo.

## Revisiones de ECC

- **react-reviewer:** sin problemas críticos (carreras, `AbortController`, StrictMode y el estado derivado están bien). Corregido: el filtro no buscaba por teléfono o RUT en la lista (ahora van en la etiqueta), «Quitar» no vaciaba lo escrito, el campo quedaba con «· N.º 31» al elegir entre homónimos y se veía un resultado viejo al repetir una búsqueda.
- **a11y-architect:** sin bloqueantes. Corregido: el filtro ahora **anuncia la cantidad de resultados y el cambio de filtro**; el guion de NVDA se completó con pasos que axe no ve (título y foco al cambiar de pantalla, «Cargando…», foco no tapado, calendario, visor de adjuntos, pegar contraseña). Anotado sin hacer: botón del cartel con `aria-pressed`, región «Cargando…» persistente, estados hover y foco en axe.
- **e2e-runner:** corregido: el servidor de vista previa siempre se reconstruye y solo se levanta para la red lenta, Brave es opcional, la evidencia no se pisa entre corridas, los topes de tiempo admiten un factor para equipos cargados y se endurecieron la lista vacía y las rutas del recorrido. Anotado: una prueba existente del rostro (cámara falsa) falló en dos corridas largas y pasa sola y en la final.
- **performance-optimizer:** nada grande que quitar de la carga inicial (React y el enrutador son la mayor parte); descartado partir más o precargar rutas. Anotado: precargar el peso 500 de la fuente, caché y brotli (fase 16), un `modulepreload` de la portada para 3G.
- Todo lo anotado y no hecho está en `desviaciones.md` (sección 15B).

## Lo que debes saber

- **Las cifras dependen de este equipo** y están medidas en emulación (CPU y red simuladas), con la API simulada: no son las de un teléfono real ni las del servidor en la nube. La red lenta no detecta por sí sola un exceso de ≈ 330 KB de JavaScript (llegaba a 5,8 s de un tope de 7): lo detecta el presupuesto de peso.
- **axe solo exige cero críticas y serias:** las de impacto moderado no hacen fallar la prueba. Lo que axe no puede ver (orden de foco, texto alternativo, anuncios) es justo lo que cubre tu prueba con NVDA.
- **El `<datalist>` del filtro no es un combobox completo.** Si en NVDA resulta incómodo, el reemplazo es un combobox ARIA propio (anotado).
- **Safari real** sigue sin probarse (decisión tuya del 20-11-2026) y el botón oficial de Google solo se prueba con el script simulado.
- **Servidores:** no quedó ninguno de los míos corriendo (4173, 4174 y 4175 detenidos). Tu frontend (3000) y tu API demo (7112) no estaban en marcha y no escribí nada en la base demo.
- Una prueba existente, `rostro.spec.js` (ingreso con rostro a 768 px), es intermitente en corridas largas; no se identificó relación con esta fase.

## Qué te toca

1. **Prueba con teclado y NVDA** con `docs/evidencias/guion-nvda.md` (≈ 1 hora; la hacemos juntos, te aviso antes de empezar). Los hallazgos se corrigen dentro de la fase 15.
2. **Cronómetro de 5 minutos en un teléfono real** (RNFP02), con el guion al final del mismo archivo (opcional, pendiente externo).
3. Visto bueno a la 15B para pasar a la 16 (**no la he empezado**).

## Comandos para tu commit

Desde la carpeta del repositorio. Se agregan `frontend` y `docs`, para **no arrastrar `.impeccable/` ni el archivo `python`**:

```powershell
git add frontend docs
git status --short
git commit -m "feat(fase-15b): filtro de clientes con busqueda en el servidor, presupuesto de peso, red lenta y movil, Chrome/Edge/Brave, axe en todas las rutas, Lighthouse y guion NVDA"
```
