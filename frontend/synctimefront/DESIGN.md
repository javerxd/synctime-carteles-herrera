---
name: SyncTime — Letreros Herrera
description: Modo claro de alta gama para un taller de letreros real; sobrio, preciso y legible de lejos.
colors:
  naranja-taller: "#C2410C"
  naranja-taller-hondo: "#9A3412"
  naranja-taller-presionado: "#7C2D12"
  naranja-velo: "#FFF7ED"
  tinta-profunda: "#0F172A"
  pizarra-secundaria: "#475569"
  pizarra-tenue: "#64748B"
  borde-de-campo: "#828DA0"
  divisor-pizarra: "#E2E8F0"
  blanco-pulcro: "#FFFFFF"
  gris-seda: "#F8FAFC"
  gris-hundido: "#F1F5F9"
  verde-abierto: "#15803D"
  rojo-cerrado: "#B91C1C"
typography:
  display:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "3rem"
    fontWeight: 700
    lineHeight: 1.05
    letterSpacing: "-0.03em"
  headline:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "2.25rem"
    fontWeight: 700
    lineHeight: 1.2
    letterSpacing: "-0.02em"
  title:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "1.5rem"
    fontWeight: 600
    lineHeight: 1.2
    letterSpacing: "-0.02em"
  body:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.6
  label:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 600
    lineHeight: 1.35
rounded:
  sm: "6px"
  md: "10px"
  lg: "14px"
  xl: "20px"
  pildora: "999px"
spacing:
  "1": "4px"
  "2": "8px"
  "3": "12px"
  "4": "16px"
  "6": "24px"
  "8": "32px"
  "12": "48px"
  "16": "64px"
components:
  boton-primario:
    backgroundColor: "{colors.naranja-taller}"
    textColor: "{colors.blanco-pulcro}"
    rounded: "{rounded.md}"
    padding: "0 20px"
    height: "44px"
  boton-primario-hover:
    backgroundColor: "{colors.naranja-taller-hondo}"
  boton-primario-presionado:
    backgroundColor: "{colors.naranja-taller-presionado}"
  boton-secundario:
    backgroundColor: "{colors.blanco-pulcro}"
    textColor: "{colors.tinta-profunda}"
    rounded: "{rounded.md}"
    height: "44px"
  campo:
    backgroundColor: "{colors.blanco-pulcro}"
    textColor: "{colors.tinta-profunda}"
    rounded: "{rounded.md}"
    padding: "8px 12px"
    height: "44px"
  tarjeta:
    backgroundColor: "{colors.blanco-pulcro}"
    rounded: "{rounded.lg}"
    padding: "24px"
  insignia:
    rounded: "{rounded.pildora}"
    typography: "{typography.label}"
    height: "26px"
  chip-activo:
    backgroundColor: "{colors.naranja-velo}"
    textColor: "{colors.naranja-taller-hondo}"
    rounded: "{rounded.pildora}"
---

# Design System: SyncTime — Letreros Herrera

Fuente normativa de los valores: `src/estilos/tokens.css` (tres capas: primitivos, semánticos y de componente). La prueba `src/test/ui/tokens.test.js` comprueba los valores fijados por el cliente, los contrastes y la regla de capas. Galería viva, solo en desarrollo: `npm run dev` y abrir `/galeria.html`. Producto y voz: [PRODUCT.md](PRODUCT.md). Reglas del cliente: `docs/requisitos/reglas-de-diseno.md`.

## Overview

**Estrella polar: «El taller de precisión».** Un letrero bien hecho se lee de lejos, con letras firmes, márgenes medidos y un solo color que llama. El sistema hace lo mismo: blanco pulcro, tinta profunda, un naranja de taller que aparece solo donde hay que actuar, y superficies que se separan con filos de 1 px y sombras difusas, nunca con decoración.

- **Modo:** el portal público persuade (vitrina, cartel, solicitud); el panel del taller opera (pedidos, cobranza, reportes). Las dos comparten el sistema; el panel es más denso y casi sin movimiento.
- **Prioridad de dispositivo:** portada pensada primero para el celular; panel pensado primero para el escritorio (PRODUCT.md).
- **Sin patrones engañosos:** cancelar, rechazar y darse de baja se ven igual que aceptar.
- **Anti-referencia:** el tema oscuro con efecto vidrio que tenía el proyecto (`src/index.css`) queda solo como evidencia; la fase 3 lo elimina.

## Colors

### Primary
- **Naranja de taller** (`#C2410C`, 5,18:1 sobre blanco; fase 12C, reemplaza al naranja `#B45309`): la acción principal de cada vista (un solo botón primario), el estado activo (pestaña, página actual, filtro activo), el hover de la barra pública y el anillo de foco. Nunca como fondo de sección ni decoración.
- **Naranja hondo** (`#9A3412`, 7,31:1): el mismo botón al pasar el cursor; texto sobre el velo naranja.
- **Velo naranja** (`#FFF7ED`): fondo de un filtro o una página activa.

### Neutral
- **Tinta profunda** (`#0F172A`, 17,85:1): todo el texto principal y los títulos.
- **Pizarra secundaria** (`#475569`, 7,58:1): ayudas, descripciones, encabezados de tabla.
- **Pizarra tenue** (`#64748B`, 4,76:1): marcadores de campo y metadatos, **solo** sobre blanco o gris seda (sobre gris hundido no llega a 4,5:1).
- **Borde de campo** (`#828DA0`, ≥3,06:1 sobre los tres fondos): el borde de todo control de formulario y del botón secundario. Más claro que esto, el campo deja de reconocerse (WCAG 1.4.11).
- **Divisor** (`#E2E8F0`) y **filo sutil** (tinta al 8 %): separan superficies; no delimitan controles.
- **Blanco pulcro** (`#FFFFFF`), **gris seda** (`#F8FAFC`) y **gris hundido** (`#F1F5F9`): fondo general, secciones alternadas y zonas hundidas (campos deshabilitados, esqueletos).

### Tonos de estado
Seis tonos con fondo, borde, texto (todos ≥6,6:1 sobre su fondo) y versión sólida: neutro, info (azul), listo (violeta), éxito (verde), aviso (amarillo) y peligro (rojo).
- Estado del pedido: aceptado → neutro, en proceso → info, terminado → listo, entregado → éxito, anulado → peligro.
- Estado de pago: no pagado → aviso, abonado → info, pagado total → éxito.
- Cartel electrónico: «Abierto / Disponible» → éxito sólido; «Cerrado / Ausente» → peligro sólido.

### Named Rules
- **La regla del único naranja:** en cada vista hay a lo más un elemento naranja relleno. Si aparecen dos, uno de ellos no es la acción principal.
- **Nunca solo color:** cada estado lleva texto e icono propio; dos estados no comparten icono.

## Typography

Una sola familia, **Plus Jakarta Sans**, alojada en el sitio, en 400, 500, 600 y 700 (subconjunto latino, que cubre el español). Escala fija en rem con razón cercana a 1,2; solo el título de portada usa `clamp()`.

### Hierarchy
- **Display** (700, 48 px en escritorio y 36 px en el teléfono, interlineado 1,05, −0,03 em): el título de la portada. Uno por página.
- **Headline** (700, 36 px, 1,2, −0,02 em): título de página (`h1`).
- **Title** (600, 30/24/20 px, 1,2, −0,02 em): `h2`, `h3` y `h4`, con `text-wrap: balance`.
- **Body** (400, 16 px, 1,6): texto de lectura, con medida máxima de 68 caracteres.
- **Label** (600, 14 px, 1,35): etiquetas de campo, botones compactos e insignias. Los botones usan 600 a 16 px.
- **Metadatos** (500, 12 px): solo encabezados de tabla y datos de apoyo, nunca párrafos.

### Named Rules
- **Cifras en columna:** montos, fechas y números de pedido usan `font-variant-numeric: tabular-nums` (clase `.cifras`, y toda tabla).
- **Formatos de Chile:** pesos sin decimales (`$12.990`), fechas `dd-mm-aaaa`.

## Layout

- Contenido de hasta 1200 px, con margen lateral `clamp(16px, 4vw, 32px)`.
- Puntos de quiebre: 480 px (30 rem, acciones del diálogo a ancho completo), 768 px (48 rem, la tabla pasa a fichas, la barra superior deja de ser fija) y 1024 px.
- Rejillas con `auto-fit` y `minmax(min(100%, X), 1fr)`: se reacomodan sin desplazamiento horizontal desde 320 px.
- Ritmo: grupos apretados (4 a 12 px), separación generosa (24 a 64 px), más aire sobre un título que debajo.
- **Objetivos táctiles:** 44 × 44 px en todo control. El tamaño compacto (36 px) solo se aplica con puntero fino (`@media (pointer: fine)`); en pantallas táctiles vuelve a 44 px.

## Elevation & Depth

Capas, no relieve: en reposo, un filo de 1 px y una sombra mínima; la elevación aparece con la interacción o cuando algo flota.

### Shadow Vocabulary
- **Reposo** (`0 1px 2px rgb(15 23 42 / 0.05), 0 1px 1px rgb(15 23 42 / 0.03)`): tarjetas y botones rellenos.
- **Elevada** (`0 1px 2px … / 0.04, 0 4px 8px -2px … / 0.06, 0 14px 28px -8px … / 0.12`): tarjeta de la vitrina al pasar el cursor o con el foco dentro; el filo se vuelve transparente.
- **Flotante** (`0 2px 4px … / 0.04, 0 12px 24px -6px … / 0.12, 0 32px 64px -16px … / 0.2`): diálogos.
- **Barra** (`0 1px 0 … / 0.06, 0 8px 24px -16px … / 0.16`): la barra superior de cristal (blanco al 80 % con `blur(12px) saturate(180%)`).

### Named Rules
- **Borde o sombra, no los dos a la vez:** al elevarse, la tarjeta pierde su filo.
- **El cristal es un efecto concreto:** solo la barra superior y el telón del diálogo (`blur(4px)`). Nunca como decoración de tarjetas.

## Shapes

- Radios: 6 px (insignias cuadradas, foco de pestaña), 10 px (botones y campos), 14 px (tarjetas, avisos, tablas), 20 px (diálogos), píldora (insignias, chips).
- Bordes siempre de 1 px. Sin franjas laterales gruesas en avisos ni tarjetas.
- Iconos de Lucide, trazo 1,75 (2 en insignias), 14 a 24 px, siempre `aria-hidden` y acompañados de texto.

## Components

Biblioteca en `src/componentes/ui/` (importar desde `src/componentes/ui`). Cada componente tiene sus estados: normal, hover (solo con puntero fino), foco visible, presionado, deshabilitado, cargando, error y vacío donde corresponde.

### Buttons (`Boton`)
- **Variantes:** primario (naranja), secundario (blanco con borde de campo), fantasma y peligro (rojo sólido, para anular).
- **Presión:** `scale(0.97)` con la curva `cubic-bezier(0.16, 1, 0.3, 1)` en 140 ms (fase 12C; token `--escala-presion-boton`).
- **Hover (fase 12C):** solo con puntero real (`hover: hover` y `pointer: fine`), cambio de color y elevación de 1 px con sombra, en 160 ms. Solo se animan `transform`, sombra y color. Con `prefers-reduced-motion` queda solo el cambio de color.
- **Zona animada:** el atributo `data-animar` de la estructura pública (portal y cuenta del cliente) activa hover y presión; el panel del taller no lo trae y los botones solo cambian de color.
- **Cargando:** gira el indicador, sigue enfocable (`aria-disabled`), no repite la acción y lo dice con texto (`textoCargando`, o «(cargando)» para el lector de pantalla).
- **Como enlace:** `como="a"`; deshabilitado pierde el `href` y sale del orden de tabulación.

### Inputs / Fields (`Campo`, `Casilla`)
- Etiqueta visible y vinculada; la ayuda va antes del control y el error después, en una región que se anuncia (`aria-live`), con icono.
- Foco: anillo naranja más borde naranja; error: borde y anillo rojos y `aria-invalid`.
- Texto a 16 px para que el teléfono no acerque la página. Casillas siempre desmarcadas por defecto (consentimientos).
- Los mensajes de error dicen qué pasó y cómo seguir («Escribe un correo completo, por ejemplo maria@correo.cl»).

### Chips (`Chip`)
- Filtros rápidos de activar y desactivar (`aria-pressed`). Activo: velo naranja, borde naranja y una marca de verificación.

### Cards / Containers (`Tarjeta`)
- Radio de 14 px, fondo blanco, filo sutil y sombra de reposo; relleno de 24 px.
- `interactiva`: se eleva 2 px y toma la sombra elevada al pasar el cursor o recibir el foco. El enlace del título cubre toda la tarjeta; cualquier otro control dentro necesita `position: relative; z-index: 1`.

### Badges (`Insignia`, `EstadoPedido`, `EstadoPago`, `EstadoCartel`)
- Píldora de 26 px con icono y texto en el tono del estado; `solido` y `grande` para el cartel electrónico.
- Un estado desconocido se muestra tal como llega, en neutro: nunca se inventa.

### Data (`TablaAdaptable`, `BarraFiltros`, `Paginacion`, `Pestanas`)
- Tabla real con `caption`, encabezados de columna y la columna principal como encabezado de fila. Cuando el espacio de la tabla (no la ventana: en el panel el menú lateral se lleva parte del ancho) baja de 768 px, cada fila es una ficha y cada dato muestra su columna; los roles ARIA explícitos conservan la semántica. Si en modo tabla las columnas no caben, la tabla se desplaza dentro de su marco y nunca ensancha la página.
- La barra de filtros es una zona `search` con nombre propio, su resumen se anuncia al cambiar y «Limpiar filtros» aparece solo con filtros aplicados.
- Paginación con la página actual en naranja (`aria-current`) y extremos inactivos pero enfocables.
- Pestañas con el patrón de WAI-ARIA (flechas, Inicio, Fin), indicador naranja de 2 px que no se desliza (se usa muy seguido).

### Feedback (`Aviso`, `EstadoVacio`, `Esqueleto`)
- Avisos en línea con borde de 1 px en su tono; el error usa `role="alert"`.
- Estados vacíos que enseñan: qué falta, por qué y cuál es el siguiente paso.
- Esqueletos con un brillo que cruza en 1,6 s; decorativos (`aria-hidden`), y la carga se anuncia con texto.

### Dialog (`Dialogo`)
- `<dialog>` nativo: atrapa el foco, Esc cierra, el foco vuelve a quien lo abrió, `enfoqueInicial` elige el primer control.
- Entra en 260 ms con la curva del documento (opacidad, `translateY(8px)` y `scale(0.98)`) y sale en 160 ms. Telón de tinta al 40 % con desenfoque de 4 px.
- Un clic en el telón cierra, salvo que el gesto haya empezado dentro (seleccionar texto).

### Navigation (barra superior)
- Barra flotante de cristal de una sola fila (64 px, igual a `--altura-barra`, que reserva `scroll-padding-top` para que el foco no quede tapado). En el teléfono el menú se despliega bajo la barra. Con poca altura (zoom) deja de ser fija. Una barra que crezca a dos filas no puede ser fija (caso de la galería).
- Enlace «Saltar al contenido» (`.enlace-salto`) como primer elemento enfocable.

### Portal público (fase 12C)
- **Barra:** Trabajos, Horario y Consultas; «Solicitar cotización» (naranja, el mismo estilo que en la portada), «Ingresar» (borde claro con el icono `user-round`) y el cartel pequeño del local en la esquina derecha. Hover y foco por teclado de los enlaces: fondo naranja con letras blancas. En el teléfono, «Ingresar» y el menú quedan en icono (el nombre accesible sigue en el texto) y, a 360 px, la marca queda en su logotipo.
- **Cartel:** el pequeño dice el estado («Abierto / Disponible», «Cerrado / Ausente»; en el teléfono solo «Abierto» o «Cerrado»). El grande usa `cartel-abierto.webp` o `cartel-cerrado.webp` como imagen decorativa (`alt` vacío) y los textos como texto real sobre una base sólida. Entra deslizándose una vez al cargar; al cambiar de estado las imágenes se cruzan con un fundido y la región `aria-live="polite"` lo anuncia.
- **Fondo:** `fondo-web.webp` en una capa fija con opacidad 0,14 detrás del portal, el ingreso y la cuenta del cliente; nunca en el panel del taller. Tarjetas y textos van sobre superficies sólidas.
- **Vitrina:** búsqueda por «contiene» (los títulos que empiezan con lo escrito, primero; sin tildes ni mayúsculas), categoría en lista desplegable nativa, contador anunciado en voz baja y zoom de 1,03 de las tarjetas con puntero real.
- **Ingreso:** `/ingresar` pregunta «¿Quién está iniciando sesión?» con dos tarjetas-enlace, cliente a la izquierda y personal del taller a la derecha (en el teléfono, cliente primero).
- **Imágenes:** WebP generado con `scripts/imagenes-portal.py` desde `docs/image/` (fondo ≤ 200 KB, carteles ≤ 150 KB, icono ≤ 20 KB).

## Do's and Don'ts

### Do:
- **Do** usar el naranja solo para la acción principal y lo activo (la regla del único naranja).
- **Do** acompañar cada estado con texto e icono propio.
- **Do** limitar el hover a `@media (hover: hover) and (pointer: fine)` y mantener 44 px en pantallas táctiles.
- **Do** animar solo `transform`, `opacity`, colores y sombras, en menos de 300 ms, con la curva del documento; la salida, más rápida que la entrada.
- **Do** respetar `prefers-reduced-motion`: sin desplazamientos ni escalas, sin brillo del esqueleto; el indicador de carga sigue girando, más lento.
- **Do** comprobar alto contraste de Windows (`forced-colors`): la pestaña activa, la página actual, los bordes del diálogo y los deshabilitados tienen reglas propias.
- **Do** rotular «Datos de ejemplo» todo lo que no sea real.

### Don't:
- **Don't** poner etiquetas pequeñas sobre los títulos ni numerar las secciones (01, 02…): el título habla solo.
- **Don't** usar texto degradado, cristal decorativo ni franjas de color de más de 1 px en tarjetas o avisos.
- **Don't** usar la pizarra tenue sobre el gris hundido (no llega a 4,5:1) ni bordes de campo más claros que `#828DA0`.
- **Don't** usar emojis o caracteres sueltos como iconos: solo Lucide.
- **Don't** animar acciones de teclado repetidas ni orquestar entradas de página en el panel.
- **Don't** pedir fuentes, mapas ni analítica a terceros antes de una acción o un consentimiento.
