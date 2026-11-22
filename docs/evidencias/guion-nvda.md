# Guion de prueba manual con teclado y NVDA — SyncTime (fase 15B)

Requisitos: **RNFO13** (prueba con lector de pantalla), **RFU29**, **RNFP15** y **RNFE08** (accesibilidad). **Esta prueba la hace una persona**: las pruebas automáticas
(axe, orden de foco, reflujo, alto contraste) no reemplazan escuchar la pantalla. Dura cerca de **una hora**. No hace falta ninguna cuenta real: usa la base demo
con las cuentas de prueba, o la API simulada (`npm run dev` con la API de demostración).

## Antes de empezar

| Qué | Cómo |
|---|---|
| Lector | NVDA (versión reciente) con voz en español. «Tecla NVDA» = Insert o Bloq Mayús (según cómo esté configurado NVDA). Para escribir en un campo, NVDA pasa solo al «modo foco»; si no lo hace, `NVDA+Espacio`. |
| Navegador | **Chrome** (es el que se prueba a fondo). Si hay tiempo, repite «Ingreso» y «Solicitud» en Edge. |
| Teclado | Solo teclado; **no uses el mouse**. |
| Ventana | Pantalla completa (1280 px o más). La segunda vuelta, a 400 % de zoom (`Ctrl` + `+`). |
| Voz | Si NVDA habla muy rápido, baja la velocidad en `NVDA + N` → Preferencias → Configuración de voz. |
| Dirección | La aplicación corre en `http://localhost:3000`. |

Teclas que se usan: `Tab` / `Mayús+Tab` (controles), `H` (siguiente encabezado), `D` (siguiente región o punto de referencia), `F` (siguiente campo de formulario),
`B` (siguiente botón), `K` (siguiente enlace), `Enter`, `Espacio`, `Esc`, `NVDA+F7` (lista de elementos), `NVDA+Flecha abajo` (leer todo).
**Marca cada fila con ✅ si NVDA dijo lo que se espera, o ❌ y anota lo que dijo en la tabla del final.**

## Qué debe cumplirse en TODAS las pantallas

1. Al abrir la pantalla, NVDA dice el **título de la página** y, al empezar a leer, el **encabezado principal (nivel 1)**.
2. `Tab` recorre los controles **en el orden en que se leen**, y siempre se **ve** dónde está el foco (contorno claro).
3. Cada control dice **su nombre y su tipo** («Correo electrónico, campo de edición», «Ingresar, botón»). Ningún «botón» ni «enlace» sin nombre.
4. Lo obligatorio se anuncia como obligatorio; los errores se anuncian **al aparecer** y quedan **asociados a su campo**.
5. Nada se mueve, parpadea ni suena solo. Todo se puede hacer **sin mouse**.

## 1. Portada (`/`)

| # | Qué hacer | Qué debe pasar |
|---|---|---|
| 1.1 | Abre la portada, espera que cargue. `NVDA+Flecha abajo` (leer todo). | Dice «Letreros que se ven de lejos.» y el estado del taller («En el taller ahora… Abierto/Cerrado»). El cartel decorativo no se repite dos veces. |
| 1.2 | `H` varias veces. | Encabezados en orden lógico (nivel 1, luego 2). Ninguno vacío. |
| 1.3 | `D` varias veces. | Anuncia las regiones: navegación principal, contenido principal, pie de página. |
| 1.4 | `Tab` hasta el botón del cartel («Balancear el cartel» / «Pausar el balanceo») y `Enter`. | Anota **qué dice NVDA al activarlo** (a veces no repite el nombre nuevo de un botón que ya tenía el foco; si no dice nada, es un hallazgo menor). Con el balanceo activo no se pierde el foco. |
| 1.5 | Vitrina: `F` hasta «Buscar trabajos»; escribe «vitrina». | Tras una pausa dice «1 trabajo» (o «N trabajos») sin mover el foco del campo. |
| 1.6 | Horario: lee la tabla del horario semanal. | Cada día se lee con sus horas («Lunes, 09:00 a 18:30»). El día de hoy se identifica con texto, no solo con color. |
| 1.7 | Mapa: llega con `Tab` al botón que lo activa (**no pulses Enter**: contactaría a OpenStreetMap). | Dice qué hace antes de activarse (cargar el mapa, que se conecta a un tercero). |
| 1.8 | Banner de cookies (si aparece): `Tab`. | Dice las dos opciones con el mismo peso. Se resuelve con teclado. |

## 2. Ingreso del cliente (`/ingresar/cliente`)

| # | Qué hacer | Qué debe pasar |
|---|---|---|
| 2.1 | Abre la pantalla. | «Ingresar a tu cuenta, encabezado nivel 1». |
| 2.2 | `Tab` hasta el **botón de Google** («Continuar con Google»). | Se anuncia como botón; dice que **abre una ventana de Google** (el aviso junto al botón se lee). **No lo actives** (a menos que quieras probar con tu cuenta). |
| 2.3 | `Tab` hasta «Correo electrónico» y «Contraseña». | Cada uno con su nombre y «obligatorio» si lo es. |
| 2.4 | Sin escribir nada, `Enter` en «Ingresar». | NVDA anuncia el **error**; el foco pasa **al primer campo con error**; el campo se anuncia como inválido (según la versión de NVDA: «no válido» o «inválido») y lee su mensaje. |
| 2.5 | Escribe un correo y una clave **incorrectos**; `Enter`. | Se anuncia el error de credenciales **sin recargar** ni perder lo escrito. No revela cuál de los dos falló. |
| 2.6 | `Mayús+Tab` hacia atrás hasta salir. | El orden inverso es el mismo recorrido. Sin «trampas» de foco. |
| 2.7 | «Crear cuenta», `Enter`. | Va a «Crear cuenta», encabezado nivel 1, **con el foco al inicio de la página**. |

## 3. Registro (`/cuenta/registro`)

| # | Qué hacer | Qué debe pasar |
|---|---|---|
| 3.1 | Recorre con `F`. | Nombre, Apellido, Teléfono, Correo electrónico, Contraseña, las dos casillas. Las dos casillas **empiezan desmarcadas** y NVDA lo dice («casilla de verificación, no marcada»). |
| 3.2 | Escribe un nombre con números («Rosa2»). | El error aparece junto al campo y se anuncia; **no** cambia lo que escribiste. |
| 3.3 | Llena todo, **sin** marcar «Leí y acepto los términos…»; «Crear cuenta». | El foco va a la casilla obligatoria, dice «no válida / obligatoria». No se envía nada. |
| 3.4 | Marca la casilla (`Espacio`) y «Crear cuenta». | Va a «Mi cuenta». Aparece «Tu cuenta está lista» y «Confirma tu correo»; NVDA lo anuncia. |

## 4. Solicitud de cotización (`/cuenta/solicitud`, con sesión)

| # | Qué hacer | Qué debe pasar |
|---|---|---|
| 4.1 | `F` por los campos: Tipo de letrero, Ancho, Alto, Cantidad, Descripción. | Cada uno con su nombre y su unidad; el tipo de letrero es una lista que se maneja con flechas. |
| 4.2 | Escribe ancho 120, alto 60, cantidad 2. | Un texto de estado dice el **área** calculada («1,44 m²») sin quitar el foco. |
| 4.3 | Adjuntos: `Tab` al botón de agregar archivos y elige un PNG con el teclado. | El archivo aparece en una lista con su nombre, y un botón «Quitar <nombre>» alcanzable con `Tab`. «1 de 5» se anuncia. |
| 4.4 | Sube un `.svg` (no permitido). | Se explica en palabras que no se admite y por qué («expórtalo a PDF o PNG»). |
| 4.5 | «Enviar solicitud». | El foco pasa al encabezado «Recibimos tu solicitud #N» y NVDA lo lee. El comprobante muestra el detalle y los adjuntos con nombre. |

## 5. Mi cuenta (`/cuenta`)

| # | Qué hacer | Qué debe pasar |
|---|---|---|
| 5.1 | `D` y `H`. | Región de navegación «Mi cuenta» con las tres opciones; la página actual dice «página actual». |
| 5.2 | Edita un dato, guarda. | El resultado («Guardamos tus datos») se anuncia sin mover el foco. |
| 5.3 | Si hay Google: «Vincular con Google» → pide la contraseña. | Es un **diálogo**: NVDA dice «diálogo» + su título; el foco entra; `Tab` no sale; `Esc` lo cierra y el foco **vuelve al botón** que lo abrió. |

## 6. Acceso del personal y panel (`/personal/ingresar`, `/panel`)

| # | Qué hacer | Qué debe pasar |
|---|---|---|
| 6.1 | Acceso del personal: igual que 2.2 a 2.5. Además, el **ingreso con rostro**: `Tab` hasta el botón y actívalo. | Antes de pedir la cámara, explica en texto qué va a pasar. Si niegas la cámara, lo dice con palabras y **deja la contraseña** disponible. Mientras mira la cámara, un texto de estado dice qué hacer («Mira al frente», «Gira la cabeza…») y no depende solo del dibujo. |
| 6.2 | Panel: «Hola, <nombre>». `D`. | Regiones: menú lateral, contenido. El enlace de salto al contenido es el primer `Tab` y funciona. |
| 6.3 | Menú lateral con `Tab` y flechas. | La opción actual se anuncia («página actual»). |
| 6.4 | Pedidos → un pedido → «Registrar abono». | Diálogo «Registrar abono del pedido #N»; el foco entra al campo «Monto»; `Tab` se queda dentro; `Esc` cierra y el foco vuelve a «Registrar abono». |
| 6.5 | Monto mayor al saldo; «Registrar abono». | El error se anuncia y queda **asociado al campo Monto**; el valor escrito no se pierde. |
| 6.6 | Tablas (pedidos, solicitudes): `Ctrl+Alt+flechas`. | Las celdas se leen con su **encabezado de columna**. En pantalla angosta, las tarjetas se leen con «etiqueta: valor». |
| 6.7 | Indicadores: recorre los gráficos. | **Cada gráfico tiene su tabla** con las mismas cifras; el color nunca es lo único que dice el estado. |
| 6.8 | Filtro «Cliente» (Indicadores o Reportes): escribe dos letras del nombre de un cliente (o un teléfono). | NVDA anuncia «N clientes encontrados» (o «Sin coincidencias»); con `Flecha abajo` lee cada sugerencia (nombre y teléfono/RUT) y `Enter` la elige; anuncia «Filtro por cliente: <nombre>». El botón «Quitar el filtro por cliente: <nombre>» devuelve el foco al campo y anuncia «Se quitó el filtro por cliente». Si no hay red, dice «No pudimos buscar clientes…». Borrar el texto con `Esc` o la «x» del campo también anuncia que se quitó. |

## 6 bis. Pasos que axe no puede ver

| # | Qué hacer | Qué debe pasar |
|---|---|---|
| 6b.1 | Cambia de pantalla con un enlace del menú (por ejemplo Pedidos → Calendario). | NVDA lee el **título nuevo** y el foco queda en un lugar lógico (el inicio del contenido o el encabezado), no se pierde en la nada. |
| 6b.2 | Abre una pantalla lenta (con la red lenta de Chrome: DevTools → Network → Slow 3G) y escucha la carga. | Se anuncia «Cargando…» y después el contenido; no queda silencio. |
| 6b.3 | Con el zoom al 400 %, recorre con `Tab` bajo la barra fija y el aviso de cookies. | El control enfocado **nunca queda tapado** por una barra o aviso fijo (WCAG 2.4.11). |
| 6b.4 | Pantallas sin paso propio todavía: Calendario de entregas, «Ver … en grande» de un adjunto y el editor de cotización. | Mismas reglas que las demás (títulos, nombres, diálogos con Esc, tablas con encabezados). Anota lo que no cumpla. |
| 6b.5 | Ingreso: pega la contraseña con `Ctrl+V` y prueba el gestor de contraseñas del navegador. | Se puede pegar y autocompletar (WCAG 3.3.8). |

## 7. Segunda vuelta: ampliado y sin color

1. **Zoom al 400 %** (`Ctrl` + `+` hasta 400 %): repite 2, 4 y 6.2. Nada se corta ni exige desplazarse a los lados; el menú pasa a un botón «Menú».
2. **Alto contraste de Windows** (`Alt izquierdo + Mayús izquierdo + Impr Pant`): el foco, las casillas, la pestaña y la página actual **se siguen viendo**.
3. **Movimiento reducido** (Configuración de Windows → Accesibilidad → Efectos visuales → desactivar animaciones): el cartel de la portada **no se balancea solo**.

## Cómo anotar lo que no pase

| Pantalla y paso | Qué dijo NVDA / qué se vio | Qué debía pasar | Gravedad (bloquea / molesta / detalle) | Corregido en |
|---|---|---|---|---|
| | | | | |
| | | | | |
| | | | | |

Al terminar: guarda este archivo con tus notas como `docs/evidencias/resultado-nvda.md` y avísame; los hallazgos se corrigen dentro de la fase 15.

## Aparte: 5 minutos desde un celular (RNFP02)

Pendiente externo. Con cronómetro y **un teléfono real**, sin ayuda: abrir `/` → «Solicitar cotización» → crear cuenta (o ingresar) → llenar la solicitud con un adjunto → «Enviar». Anota el tiempo y dónde dudó la persona. Meta: **5 minutos o menos** para una persona sin capacitación.
