# Plan de la fase 12C — Portal público: mapa, carteles, barra superior, vitrina e ingreso

Fecha: 20-11-2026 · Rama: `fase-12c-portal-publico` (nace de `fase-12b-indicadores-backend`) · Estado: **esperando tu aprobación**; construcción sin iniciar

Origen: tu lista de cambios del 20-11-2026 y tus respuestas a las cinco decisiones (contraste del naranja, búsqueda por «contiene», despliegue). Es una fase **casi solo de frontend**: no hay migración. Toca la portada (fase 5), la estructura pública (fase 3), el ingreso (fase 6) y la vitrina. La **12D** cubre los paneles y las cuentas.

Requisitos que toca: RFS01 (vitrina), RFS02 (cartel del local), RFS03 (pantalla de ingreso), RFS08 (contacto) y las reglas transversales de accesibilidad y diseño (contraste AA, teclado, reducción de movimiento). Ninguno deja de cumplirse; lo que cambia de las reglas de diseño va en `desviaciones.md`.

## 0. Antes de todo: el mapa «Cómo llegar» da 403

El mapa de la portada pide las teselas a `tile.openstreetmap.org` y OpenStreetMap las bloquea («no sigue la política de uso de teselas»). La política exige que cada petición lleve un `Referer` que identifique a la aplicación y bloquea las que no lo traen.

- **Diagnóstico primero:** abro el mapa en la app, miro en la red qué `Referer` viaja y qué responde el servidor. No arreglo a ciegas.
- **Corrección probable:** las teselas se piden con `referrerPolicy: 'strict-origin-when-cross-origin'` (Leaflet lo admite en `tileLayer`), con una etiqueta `<meta name="referrer">` equivalente en `index.html`. La CSP sigue permitiendo solo `tile.openstreetmap.org` en `img-src`.
- **Si OpenStreetMap sigue bloqueando desde `localhost`:** su servidor puede rechazar ese origen en desarrollo. En ese caso la dirección de las teselas pasa a una variable de entorno (`VITE_MAPA_TESELAS`, con OpenStreetMap por defecto y una alternativa que permita desarrollo) y lo dejo anotado. Lo sabré con la prueba real: **no se puede comprobar con pruebas automáticas**, solo mirando el mapa.
- **Si no hay red:** el mapa ya tiene su alternativa estática; compruebo que se vea con claridad y sin error.
- **Prueba:** unitaria de que la capa se crea con la política de referencia y la atribución; verificación en la app con el mapa cargado y la consola sin errores.

## 1. Color, fondo e imágenes

- **Naranja accesible `#C2410C`** (tu decisión 2): ≈5,2:1 con letras blancas (lo mido con el validador del proyecto antes de usarlo). Nuevo token `--color-naranja` y su estado de hover más oscuro. **Supuesto mío, objétalo si no:** el naranja **reemplaza** al ámbar `#B45309` como único acento (botón principal, estado activo y anillo de foco). Dos acentos casi iguales en pantalla se verían como un error. `DESIGN.md` se actualiza.
- **Fondo `fondo-web.jpg`** con opacidad baja en el portal público, el ingreso y la cuenta del cliente. **No** en el panel del taller. Va en una capa fija detrás del contenido con opacidad ≈ 0,10 a 0,15; las tarjetas y los textos siguen sobre superficies sólidas, así que el contraste no depende de la foto. Lo compruebo con axe en las tres anchuras.
- **Peso de las imágenes.** Las cuatro pesan entre 1,2 y 1,9 MB. Así tardarían en cargar y pondrían en riesgo la meta de rendimiento (RNFP01). Las convierto a **WebP** (el fondo ≤ 200 KB, los carteles ≤ 150 KB, el icono de WhatsApp ≤ 20 KB) con un guion que queda en el repositorio; el original queda en `docs/image/`. El fondo se carga sin bloquear la primera pantalla.
- **Licencias:** las cuatro imágenes entran al inventario de la fase 16 como material entregado por el taller.

## 2. Barra superior

- **Se elimina «Cómo llegar»** de la barra. La sección «Cómo llegar» sigue en la portada.
- **Nuevo botón «Consultas»** (sección 5). Reemplaza a «Contacto» en la barra; las secciones quedan: Trabajos, Horario y Consultas.
- **Hover:** los botones de la barra pasan a fondo naranja con letras blancas, y el foco por teclado se ve igual. Contraste ≥ 4,5:1 en reposo y en hover.
- **«Solicitar cotización»:** los dos botones (barra y portada) con el mismo estilo: naranja, letras blancas.
- **«Ingresar»:** toma el estilo anterior de «Solicitar cotización» (borde y fondo claro) más el icono `user-round` de lucide que enviaste. En el teléfono se mantiene visible junto al menú.
- **Cartel pequeño en la esquina derecha** (sección 4). En el teléfono muestra solo el icono y el estado («Abierto» o «Cerrado»), sin romper la barra a 360 px.

## 3. Botones: animación profesional

Con la skill `apple-design`. Una sola definición en el componente `Boton`, no una por pantalla.

- **Hover** (solo con puntero real: `@media (hover: hover)`): cambio de color y leve elevación en 150 a 200 ms con una curva suave.
- **Pulsación:** escala a 0,97 con retorno rápido; se puede interrumpir.
- **Foco por teclado:** el anillo aparece sin movimiento.
- **Solo propiedades baratas** (`transform`, `opacity`, color): nada que cause reflujo.
- **`prefers-reduced-motion`:** sin escala ni desplazamiento, solo el cambio de color.
- **Excepción que pediste:** el **panel del taller** no lleva estas animaciones. Se aplica con un atributo de zona en la estructura pública y en la cuenta del cliente; el panel no lo trae.

## 4. Cartel digital

- **Cartel pequeño** (el actual) sube a la esquina derecha de la barra, con sus textos.
- **Cartel grande**, en el lugar del anterior: usa `cartel-cerrado.png` (rojo) o `cartel-abierto.png` (verde), según el estado real del local. Conserva la información que hoy muestra: «En el taller ahora.», «Cerrado / Ausente», «Cerrado. Abrimos hoy a las 9:00.» y «Cartel actualizado el …».
- **Los textos son texto real**, no parte de la imagen, y la imagen es decorativa (`alt=""`). Así el lector de pantalla lee el estado y no hay texto sin contraste dentro de una foto. El texto sobre la imagen lleva una base sólida que asegura el contraste.
- **Animación de entrada:** el cartel se desliza de izquierda a derecha al cargar la página (≈ 500 ms, una sola vez, sin repetir al actualizarse el estado). Con `prefers-reduced-motion`, aparece sin movimiento.
- **Cambio de estado en vivo:** cuando el local abre o cierra, el cartel cambia de imagen con un fundido breve y el anuncio sigue usando `aria-live="polite"` como hoy.

## 5. Consultas: el formulario sale de la portada

- **Nueva ruta `/consultas`** (pantalla independiente) con el formulario «Escríbenos» tal como existe (`FormularioContacto`, con sus validaciones, consentimiento y anti-spam). Su correo y su API no cambian.
- **La portada** conserva solo los canales directos (teléfono, WhatsApp, correo) y un botón «Escribir una consulta» que lleva a `/consultas`.
- **Icono de WhatsApp:** el botón usa `icono-whatsapp.png` (en WebP), en la portada, en «Consultas» y en el contacto directo de las solicitudes. Con `aria-label` «Escribir por WhatsApp» y sin depender del color.
- **Los enlaces antiguos** `/#contacto` redirigen a `/consultas`, para no dejar enlaces rotos.

## 6. Vitrina: búsqueda, categorías y zoom

- **Búsqueda interactiva por «contiene»** (tu decisión 5): al escribir, la lista se filtra al instante. **Primero** aparecen los trabajos cuyo título empieza con lo escrito, luego los que lo contienen. Sin distinguir mayúsculas ni tildes («señal» encuentra «Señalización»). Busca en el título, el tipo de letrero y la categoría.
- **Categorías en lista desplegable** (selector nativo, accesible y cómodo en el teléfono), combinable con la búsqueda, con la opción «Todas».
- **Cuántos resultados:** un contador anunciado en voz baja («12 trabajos»). Sin resultados: mensaje claro y botón «Limpiar».
- **Dónde se filtra:** si la vitrina llega completa a la pantalla (hoy son decenas de trabajos), se filtra en el navegador, sin consultas nuevas. Si resulta paginada, agrego el parámetro `q` a la API de la vitrina y el filtro va en el servidor. Lo reviso antes de programar.
- **Zoom ligero al pasar el cursor** sobre cada tarjeta (escala ≈ 1,03, solo con puntero real, sin movimiento con `prefers-reduced-motion`); no tapa las tarjetas vecinas ni genera desplazamiento horizontal.

## 7. Pantalla de ingreso: «¿Quién está iniciando sesión?»

- **`/ingresar`** muestra dos tarjetas grandes: **cliente a la izquierda**, **personal del taller a la derecha** (en el teléfono, una bajo la otra, con cliente primero). Son enlaces, así que funcionan con teclado y lector.
- **El formulario de ingreso de cada uno no cambia.** El del cliente pasa a `/ingresar/cliente` y el del personal sigue en `/personal/ingresar`.
- **No se pierde el retorno:** las guardas que mandan a `/ingresar` con la dirección a la que volver la conservan al elegir la tarjeta. Lo cubro con pruebas.
- **Los enlaces antiguos a `/ingresar`** (correos, marcadores) siguen funcionando: muestran el selector.

## 8. Pruebas primero y revisiones

- **Vitest:** cada regla nueva antes de su código: filtro de la vitrina (contiene, prefijo primero, sin tildes, combinación con categoría, sin resultados), capa del mapa, estado del cartel por hora y por estado manual, selector de ingreso con retorno, «Consultas» y redirección de `/#contacto`, ausencia de «Cómo llegar» en la barra, botones con el estilo correcto.
- **Playwright con axe a 360, 768 y 1440 px:** portada, `/consultas`, `/ingresar`, ingreso del cliente y del personal; sin desplazamiento horizontal; con `prefers-reduced-motion`.
- **Contraste:** el validador del proyecto comprueba cada pareja nueva (naranja con blanco, texto del cartel, hover).
- **ECC:** `react-reviewer`, `a11y-architect` y `security-reviewer` (CSP y política de referencia del mapa). Revisión del peso de las imágenes.
- **Verificación en la app** con tu sesión de dueño y de cliente, a las tres anchuras, con la consola sin errores y mirando el mapa real.
- **Cierre:** informe `informes/fase-12c.md`, tablero, trazabilidad, `desviaciones.md` y commit en la rama. Los comandos de GitHub te los paso; yo no subo nada.

## Decisiones y supuestos que te dejo a la vista

1. **El naranja reemplaza al ámbar como único acento** (sección 1). Si prefieres conservar el ámbar para el foco y el estado activo, lo mantengo y el naranja queda solo en los botones.
2. **«Contacto» sale de la barra** y entra «Consultas» (sección 2). La barra queda con tres secciones.
3. **El fondo no va en el panel del taller** (sección 1): entiendo «panel del taller» como el panel del personal. La cuenta del cliente sí lleva fondo.
4. **Imágenes en WebP** (sección 1): sin esto, el fondo de 1,7 MB afectaría la carga. Si prefieres otro formato, lo cambio.
