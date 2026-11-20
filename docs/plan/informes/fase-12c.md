# Informe de cierre — Fase 12C: portal público

Fecha: 20-11-2026 · Rama: `fase-12c-portal-publico` · Estado: **esperando tu visto bueno**

Plan: `fase-12c-plan.md`. Es una fase solo de frontend: no hay migración.

## Qué se hizo

- **Mapa «Cómo llegar» (403):** diagnóstico, no a ciegas. La causa era `<meta name="referrer" content="no-referrer">` en `index.html`: OpenStreetMap bloquea las peticiones sin Referer. Ahora es `strict-origin-when-cross-origin` y la capa de teselas lo declara (`teselas.js`). **Verificado en la app:** 6 de 6 teselas cargadas, ninguna rota. La CSP no cambia. `VITE_MAPA_TESELAS` permite otro servidor si hiciera falta (no fue necesario).
- **Naranja `#C2410C`** como único acento, también en foco y estado activo (5,18:1 con blanco; hover `#9A3412`, 7,31:1). `DESIGN.md` y `PRODUCT.md` actualizados.
- **Fondo e imágenes:** `fondo-web` con opacidad 0,6 (subida desde 0,14 a pedido del usuario) en el portal, el ingreso y la cuenta del cliente (no en el panel). Las cuatro imágenes pasan a WebP con `scripts/imagenes-portal.py`: fondo 16 KB e icono 3 KB (topes 200 y 20 KB). Originales en `docs/image/`.
- **Barra:** Trabajos, Horario y Consultas; sin «Cómo llegar» ni «Contacto». Hover y foco naranja con letras blancas. «Solicitar cotización» igual en barra y portada. «Ingresar» con borde claro e icono de usuario, visible junto al menú en el teléfono. Cartel pequeño en la esquina derecha. A 360 px «Ingresar» y el menú quedan en icono y la marca en su logotipo, para que quepa todo sin desplazarse.
- **Botones:** una sola definición en `Boton`: hover con elevación de 1 px, presión 0,97, solo con puntero real, solo `transform`, sombra y color; sin movimiento con `prefers-reduced-motion`. Se activa con `data-animar` en la estructura pública; **el panel del taller no lo trae** (verificado con Playwright).
- **Cartel digital (rehecho a pedido del usuario):** ya no es una imagen: es un letrero dibujado a partir del cartel del taller (cuerda, clavo, ojales, doble filete y cinta), verde si el local está abierto y rojo si está cerrado, con los textos reales adentro: «En el taller ahora.», «Abierto» o «Cerrado», la frase del estado («Cerrado. Abrimos hoy a las 9:00.») y «Cartel actualizado el …». Se quitó la insignia «Abierto / Disponible» de esa sección y en toda la portada el estado dice solo «Abierto» o «Cerrado». Entra deslizándose de izquierda a derecha al cargar y luego se balancea apenas desde el clavo; sin movimiento con `prefers-reduced-motion`. Cambia de color con un fundido y `aria-live`. Una sola consulta del estado compartida con el cartel pequeño de la barra.
- **WhatsApp flotante:** usa la imagen del icono entregado por el taller en lugar del icono genérico.
- **Consultas:** `/consultas` con el formulario sin cambios; la portada conserva los canales directos y un botón; `/#contacto` redirige. Icono de WhatsApp (WebP) en la portada, Consultas y el contacto directo.
- **Vitrina:** búsqueda por «contiene» sin tildes (con varias palabras, todas deben aparecer), título que empieza primero, categoría en lista desplegable, contador anunciado, «Limpiar» y zoom 1,03.
- **Ingreso:** `/ingresar` con dos tarjetas (cliente a la izquierda, personal a la derecha; en el teléfono, cliente primero), con el retorno conservado. El formulario del cliente pasa a `/ingresar/cliente`; Recuperar, Registro y Verificar apuntan ahí.

## Decisión de diseño que conviene que sepas

La vitrina se pide **completa** (páginas de 100, tope 20) y se filtra en el navegador; la API no cambió. Con más de 2.000 trabajos habría que filtrar en el servidor con `q`.

## Pruebas

- Vitest: **668 de 668** (627 antes de la fase; 41 nuevas, y se ajustaron 2 de `cuenta.test.jsx` y la del acento en `tokens.test.js`).
- Playwright: recorrido completo con axe a 360, 768 y 1440 px, incluidas `/`, `/ingresar`, `/ingresar/cliente` y `/consultas`: **sin fallas graves ni desplazamiento horizontal**; 8 pruebas nuevas de la 12C. Se actualizaron 4 flujos que daban por hecho el formulario en `/ingresar`.
- Verificación en la app (sesión de dueño): barra, cartel, mapa con teselas, búsqueda real («senal» → «Señalética de estacionamiento») y barra a 360 px.
- Build de producción correcto.

## Revisiones de ECC

- **Seguridad:** sin hallazgos altos. Se corrigió el medio: `/cuenta/verificar` ahora no envía Referer mientras está abierta, como `/baja` y `/avisos/confirmar`. Con la política actual los tokens no salen a terceros.
- **Accesibilidad:** sin bloqueantes. Corregidos: espacio de foco bajo el botón de WhatsApp (2.4.11), región viva del cartel (`aria-atomic`, texto de carga), contador «Cargando trabajos…», nombre del buscador y alto contraste sin imagen de fondo.
- **React:** sin errores de hooks. Corregidos: `catalogoCompleto` cancelable y sin duplicados, búsqueda con varias palabras y un id de prueba.

## Pendientes y límites (no bloquean)

- **Orden de foco en el teléfono:** el DOM va marca, menú, «Ingresar»; visualmente «Ingresar» queda a la izquierda del menú. Desviación menor aceptada.
- La CSP es una `meta`; no protege contra clickjacking. Emitirla como encabezado HTTP (`frame-ancestors`, `Referrer-Policy`) y quitar `localhost` de `connect-src` es de la fase 16.
- Las cuatro imágenes entran al inventario de licencias de la fase 16 como material entregado por el taller.
- El contador de la vitrina se anuncia en cada tecla; aceptado.
- **Supuestos del plan sin objeción hasta ahora:** el naranja reemplaza al ámbar en el foco; «Contacto» sale de la barra; el fondo no va en el panel; imágenes en WebP.
