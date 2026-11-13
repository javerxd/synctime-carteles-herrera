# Informe de cierre — Fase 2: sistema de diseño

Fecha: 12-11-2026 · Rama: `fase-02-sistema-diseno` (nace de `ajuste-revision-profesora`) · Estado: **en revisión, esperando el visto bueno del usuario**

Esta fase no cambia el backend ni las pantallas que se usan hoy: deja los tokens, los componentes base y una galería interna para revisarlos. Las pantallas de la revisión del lunes se arman con estas piezas en la fase 3.

## Cómo verlo

```
cd frontend/synctimefront
npm run dev
```

Abrir `http://localhost:3000/galeria.html`. La galería solo existe en desarrollo: `vite build` compila únicamente `index.html`.

## Qué se hizo

| Tema | Resultado | Dónde |
|---|---|---|
| Producto y voz (skill `impeccable`) | `PRODUCT.md` con tus tres respuestas: prioridad igual para el cliente en el celular y el personal en el escritorio; voz «tú, cercano»; el logotipo lo entregas después y mientras tanto va el nombre en texto | `frontend/synctimefront/PRODUCT.md` |
| Dirección de arte (skill `impeccable`) | `DESIGN.md` con tokens legibles por máquina y las reglas: estrella polar «El taller de precisión», la regla del único ámbar, nunca solo color, borde o sombra pero no ambos | `frontend/synctimefront/DESIGN.md` |
| Tokens en tres capas (skill `ui-ux-pro-max:design-system`) | Primitivos, semánticos y de componente. Fondos `#FFFFFF`, `#F8FAFC` y `#F1F5F9`; tinta `#0F172A`; acento `#B45309`; títulos a −0,02 em; seis tonos de estado; sombras difusas multicapa; cristal en la barra; curva `cubic-bezier(0.16, 1, 0.3, 1)` y compresión 0,98 | `src/estilos/tokens.css` |
| Fuente (RNFP22, RNFO17) | Plus Jakarta Sans alojada en el sitio, solo 400, 500, 600 y 700 y el subconjunto latino. Nada se pide a Google Fonts | `src/estilos/fuentes.css` |
| Estilos base | Foco ámbar visible, selección, cursor de texto, barra de desplazamiento, cifras tabulares, texto solo para lectores, enlace para saltar al contenido y movimiento reducido | `src/estilos/base.css` |
| Componentes con su matriz de estados | Botón, enlace, campo (texto, lista y área), casilla, chip de filtro, tarjeta, insignias de estado del pedido, del pago y del cartel, aviso, estado vacío, esqueleto, diálogo, pestañas, paginación, barra de filtros y tabla adaptable | `src/componentes/ui/` |
| Movimiento (skill `emil-design-eng`) | Presión de 140 ms, elevación de tarjetas, entrada del diálogo en 260 ms y salida en 160 ms, brillo del esqueleto. Hover solo con puntero fino. Con movimiento reducido no hay desplazamientos ni escalas | CSS Modules de cada componente |
| Galería interna | Color con contrastes medidos en vivo, tipografía, todos los estados de cada componente, filtros de pedidos con «Por cobrar», tabla que pasa a fichas, diálogo de solicitud con la casilla de privacidad desmarcada | `galeria.html`, `src/galeria/` |
| Licencias (RNFO17) | Inventario de la fuente (OFL), los iconos (ISC) y los paquetes del frontend | `docs/licencias.md` |

**Prioridad del lunes:** ya existen las piezas que usan las pantallas de la fase 3: botón, campos, tarjeta, insignias de estado (con texto e icono, verde y rojo del cartel incluidos), diálogo, tabla adaptable, filtros, esqueleto y estado vacío.

## Verificación

| Comprobación | Resultado |
|---|---|
| Pruebas unitarias del sistema de diseño (Vitest) | 56 de 56: 8 de tokens y contrastes, 48 de componentes |
| Pruebas del frontend en total | 135 de 135 (las 79 anteriores siguen en verde) |
| Pruebas en navegador real (Playwright, Chromium) | 14 de 14 |
| Sin desplazamiento horizontal | A 320, 360, 640 (1280 px con zoom al 200 %), 768 y 1440 px |
| axe (WCAG 2.0, 2.1 y 2.2, A y AA) | 0 violaciones de cualquier gravedad a 360 y a 1440 px. A 360 px, axe deja «por revisar» el contraste de algunos elementos (no puede medirlo bajo el cristal); esos colores los mide la prueba de tokens |
| Contrastes medidos | Tinta 17,85:1; texto secundario 7,58:1; texto tenue 4,76:1 (solo sobre blanco y seda); ámbar 5,02:1 y texto blanco sobre ámbar 5,02:1; borde de campos y foco ≥3:1 sobre los tres fondos; cada tono de estado ≥6,6:1 sobre su fondo |
| Objetivos táctiles | Todos los controles visibles miden al menos 44 × 44 px en un teléfono emulado |
| Diálogo | Retiene el foco con Tab, se cierra con Esc, devuelve el foco y enfoca el primer campo al abrir |
| Movimiento reducido y alto contraste | Esqueleto quieto y sin compresión con movimiento reducido; pestaña activa, página actual y borde del diálogo visibles con `forced-colors` |
| Detector de `impeccable` | Sin hallazgos |
| oxlint | Sin advertencias en el código nuevo |
| Compilación | Correcta; la galería no entra en la compilación |
| Pruebas no vacuas | Al quitar la regla de alto contraste de las pestañas, su prueba falla; la prueba de movimiento reducido comprueba primero que el brillo existe |

## Revisión de ECC

Dos revisores: `react-reviewer` y `a11y-architect`. No hubo hallazgos críticos. Lo corregido, con pruebas nuevas:

| Hallazgo | Corrección |
|---|---|
| Alto contraste de Windows: la pestaña activa, el borde del diálogo y la página actual desaparecían | Reglas `forced-colors` en pestañas, paginación, diálogo y botones deshabilitados |
| En el teléfono la barra fija es más alta que el espacio reservado y podía tapar el foco (WCAG 2.4.11) | Bajo 768 px o con poca altura, la barra deja de ser fija. Prueba que recorre 25 elementos con Tab a 360 px |
| La tabla en fichas podía leer dos veces el nombre de la columna | Texto alternativo vacío en la etiqueta visual; la columna principal pasa a encabezado de fila |
| «Cargando…» aparecía junto con su región y el lector podía no anunciarlo | La región de estado existe siempre y solo cambia su texto |
| El botón cargando no lo decía con palabras; como enlace deshabilitado seguía abriéndose con Ctrl+clic | `textoCargando` o «(cargando)» para el lector; el enlace inactivo pierde su `href` y sale del orden de tabulación |
| El diálogo se cerraba al seleccionar texto y soltar fuera; su contenido quedaba montado cerrado | El gesto debe empezar en el telón; el contenido se monta al abrir. Nuevo `enfoqueInicial` |
| Pestañas: `aria-controls` hacia paneles inexistentes; sin pestaña alcanzable si la activa no existe | Todos los paneles montados (los inactivos ocultos) y la primera como respaldo |
| Paginación: el foco se perdía al llegar al extremo | «Anterior» y «Siguiente» quedan inactivos pero enfocables |
| Props externos que se pisaban (Chip, Campo, Casilla) | Se combinan |

Queda documentado en `DESIGN.md`, sin cambio de código:
- el diálogo exige que `alCerrar` cierre de verdad;
- otro control dentro de una tarjeta pulsable necesita `z-index`;
- los mensajes de error deben decir cómo seguir.

## Decisiones que conviene que revises

Están en `desviaciones.md`:
- **Borde de los campos:** es más oscuro (`#828DA0`) que el gris claro de tu documento. Con el gris claro, el campo no llega al contraste de 3:1 que exige WCAG. Las tarjetas sí usan el borde translúcido.
- **Barra superior en el teléfono:** deja de ser fija para no tapar el foco. Desde 768 px es la barra flotante de cristal.
- **Anillo de foco:** es ámbar. El foco es un estado activo.
- **Dirección visual:** `impeccable` propone normalmente varias direcciones visuales para que elijas una. No lo hice, porque tu documento de reglas ya fija el mundo visual (colores, fuente, materiales y movimiento). La composición de la portada se decide en la fase 3, y ahí te mostraré la propuesta.

## Lo que no se verificó

- **Navegadores:** solo Chromium (Chrome y Edge). Firefox y Safari quedan para la fase 15. Playwright solo tiene Chromium instalado.
- **Alto contraste:** se emuló en Chromium; no se probó en Windows con un tema de alto contraste real.
- **Lector de pantalla:** NVDA no se probó; es tu prueba manual de la fase 15. Lo que dicen los revisores sobre NVDA son predicciones.
- **Teléfonos reales:** no se probaron.

## Cambios en archivos

- **Frontend:** `PRODUCT.md`, `DESIGN.md`, `galeria.html`, `src/estilos/`, `src/componentes/ui/`, `src/galeria/`, `src/test/ui/`, `e2e/galeria.spec.js`. En `src/test/setup.js` se agregó un reemplazo mínimo de `<dialog>` para jsdom. `package.json` suma `@fontsource/plus-jakarta-sans`, `lucide-react` y `@axe-core/playwright` (este solo en desarrollo).
- **Documentos:** `docs/licencias.md`, `trazabilidad.md`, `tablero.md`, `desviaciones.md` y este informe.
- **Herramientas:** `.claude/launch.json`, para abrir la galería en el navegador de la app.

## Lo que necesito de ti

1. Tu visto bueno a esta fase, o lo que quieras cambiar del sistema (puedes recorrer la galería).
2. Cuando lo tengas, el logotipo del taller, para la barra y la portada.
