# Informe de cierre — Fase 5: portada pública

Fecha: 13-11-2026 · Rama: `fase-05-portada-publica` (nace de `fase-04-servicios-comunes`) · Estado: **en revisión**, esperando tu aprobación

La portada ya funciona sin sesión. Muestra si el local está abierto ahora, en hora de Chile, combinando el horario con el cartel. También muestra la vitrina de trabajos con filtro por categoría, el horario de la semana, cómo llegar, el formulario de contacto, el aviso de cookies y los borradores de la política de privacidad y de los términos. Cada consulta del formulario queda guardada con su consentimiento y le llega al taller como aviso por correo. El navegador no contacta a ningún tercero hasta que el visitante lo pide.

## Cómo verlo

API sobre la base demo (ya tiene la migración nueva y la vitrina de ejemplo) y el frontend:

```
cd backend/Proyecto-Carteles-herrera-synctime-main
dotnet run --project .\LetrerosHerreraSYNCTIMEapi --launch-profile https -- --Correo:AvisosTaller=avisos.demo@letreria.test --Correo:Modo=Bandeja
```

`--Correo:Modo=Bandeja` es necesario porque en tus User Secrets quedó `Correo:Modo=Smtp`: sin él, la demo envía por Gmail. La configuración `api-demo-avisos` de `.claude/launch.json` ya lo incluye.

```
cd frontend/synctimefront
npm run dev
```

- **Portada:** `http://localhost:3000/`.
- **Correo de cada consulta:** en desarrollo no sale del equipo. Queda en la bandeja local, `https://localhost:7112/dev/bandeja`.
- **Volver a cargar la vitrina de ejemplo:** `dotnet run --project .\LetrerosHerreraSYNCTIMEapi --launch-profile https -- sembrar-demo`. Solo funciona sobre la base demo.

## Qué se hizo

### Backend

| Tema | Resultado | Dónde |
|---|---|---|
| «Abierto ahora» | Abierto solo si es horario de atención en hora de Chile (`America/Santiago`, con su horario de verano) **y** el cartel dice abierto. La hora de cierre ya cuenta como cerrado. Si está cerrado, dice cuándo abre. La API no arranca si el servidor no tiene la zona horaria de Chile, en vez de fallar en cada visita | `Negocio/Local/ReglaLocalAbierto.cs`, `GET /api/v1/local/estado` |
| Vitrina | Paginada (9 por página) y filtrable por categoría. Cada trabajo trae su tipo de letrero y una imagen WebP en dos tamaños. Los visitantes no ven datos de gestión: si está oculto, si lleva un logo ni quién lo publicó | `Services/ServiciosPortada.cs`, `Endpoints/CatalogoApi.cs` |
| Logo del cliente (RNFO17) | Una publicación con el logo de un cliente no se muestra sin una autorización registrada, con quién la dio y cuándo. Lo impiden la API y una restricción de la base. Si cambia la imagen, la autorización anterior se borra y hay que volver a darla. Volver a autorizar no cambia el registro original | `PUT /api/v1/catalogo/{id}/autorizacion-logo`, `ck_catalogo_logo` |
| Formulario de contacto | Guarda la consulta y su consentimiento de privacidad (con la versión leída) en una sola transacción y deja el aviso en la cola de correo. Si la persona no aceptó, no guarda nada; si la versión que leyó ya no es la vigente, pide volver a leerla | `POST /api/v1/contacto`, `ServicioContacto` |
| Defensas del formulario | Tiene:<br>• un campo trampa contra robots, que recibe la misma respuesta;<br>• un límite de 5 envíos por hora por IP;<br>• un tamaño máximo de 16 KB.<br><br>Una misma consulta repetida dentro de la hora no se guarda de nuevo. Las consultas tienen su propio tope de 5 avisos por hora dentro de los 20 del taller, así que un robot no deja sin avisos a las solicitudes | `PortadaApi.cs`, `OpcionesCorreo.TopeConsultasPorHora` |
| Aviso de consulta | El correo al taller lleva el nombre, el contacto y el mensaje citado, y «Responder» le escribe directo al visitante | `AvisoConsultaContacto` |
| Cookies | Hoy no hay analítica. Solo se registra quién la acepta, con versión, fecha y hora; si después la rechaza, se registra el retiro. Quien nunca la aceptó no deja datos | `POST /api/v1/consentimientos/cookies` |
| Documentos legales | La API publica la versión vigente de cada documento, la que se registra al aceptar | `GET /api/v1/documentos-legales` |
| Vitrina de ejemplo | 7 trabajos de demostración. Sus imágenes se dibujan en el equipo y van marcadas «Imagen de ejemplo», así que no se descargan fotos de terceros. Solo se cargan en la base demo | `Datos/SemillaDemo.cs`, `Infraestructura/Imagenes/ImagenesDeEjemplo.cs` |

**Migración `PortadaPublica`:**
- Agrega a `catalogo` la imagen subida, la marca de logo y su autorización, y a `imagenes` la marca «de ejemplo».
- Crea la tabla `consultas_contacto`.
- Solo agrega; no borra ni cambia datos.

Está aplicada en las bases de prueba y en la demo. **No en la base real.**

### Frontend

| Tema | Resultado |
|---|---|
| Encabezado y pie | Teléfono, correo y WhatsApp del taller. El pie tiene «Preferencias de cookies» y los enlaces legales |
| Mostrador | Estado del local en el acto, con el mensaje del cartel y el regreso estimado. Se actualiza solo cada minuto y al volver a la pestaña |
| Vitrina | Fotos WebP con carga diferida y tamaño según la pantalla. Tiene filtro por categoría y «Ver más trabajos», que deja el foco en el primer trabajo nuevo. Cuando falla la carga, ofrece reintentar |
| Horario y mapa | Semana completa con el día de hoy marcado y «Hora de Chile». El mapa es una imagen alojada en el sitio y solo carga Leaflet y OpenStreetMap si el visitante pulsa «Usar mapa interactivo». Si no carga, vuelve a la imagen |
| WhatsApp | Botón flotante de 56 px que sube sobre el aviso de cookies mientras se ve |
| Formulario | Valida al salir de cada campo y lleva el foco al primer error. La casilla de privacidad viene desmarcada. Al terminar, el resultado queda a la vista. No envía sin la versión vigente de la política |
| Aviso de cookies | No bloquea la página. Ofrece «Rechazar opcionales» y «Guardar mi elección», iguales en tamaño y estilo, con la casilla de analítica desmarcada. Reserva su alto abajo, así no tapa el elemento enfocado (WCAG 2.4.11) ni el final del pie |
| Privacidad y términos | Páginas `/privacidad` y `/terminos`, con versión, índice y aviso de «Borrador pendiente de revisión legal» |
| CSP | En producción, `connect-src` queda en `'self'` (más el origen de la API si va en otro dominio). Las teselas de OpenStreetMap solo se admiten como imágenes |

## Verificación

| Comprobación | Resultado |
|---|---|
| Pruebas del backend | 196 unitarias (antes 177) y 253 de integración (antes 227), todas en verde. El contrato `openapi.json` coincide con la API en rutas y permisos |
| Pruebas del frontend | 178 unitarias y 56 de Playwright con axe, todas en verde. Playwright recorre `/`, `/privacidad` y `/terminos` a 360, 768 y 1440 px, más 5 flujos: estado y vitrina, cookies, formulario, mapa sin terceros y documentos legales |
| Pruebas no vacías | Reintroducir a propósito dos errores hace fallar su prueba: conservar la autorización del logo al cambiar la imagen, y guardar dos veces la misma consulta |
| La app en marcha | API real sobre la base demo y frontend en el navegador. Una consulta enviada desde la portada respondió 202, quedó guardada con el teléfono normalizado y su consentimiento de privacidad 1.0, y su aviso llegó a la bandeja local. En toda la visita el navegador solo pidió recursos a `localhost`. Que OpenStreetMap se contacte solo al activar el mapa lo comprueba Playwright |
| Diseño | Portada revisada en el navegador a 360, 768 y 1440 px. Tras la revisión se quitó una franja blanca suelta sobre el pie y el aviso de cookies dejó de tapar el botón de WhatsApp |
| Script de la base | `LetreriaLosHerreraDB.sql` regenerado con la migración nueva |
| Tamaño | JavaScript inicial: unos 101 KB comprimido. El mapa interactivo (44 KB) se descarga solo al activarlo |

## Revisión de ECC

Dos revisores: `security-reviewer` (C# y seguridad) y `react-reviewer` (React y accesibilidad WCAG 2.2). Ninguno encontró hallazgos críticos. Todo lo alto quedó corregido; lo medio, corregido o anotado.

| Revisor | Hallazgo | Corrección |
|---|---|---|
| Seguridad (alto) | Un robot que llenara el formulario agotaba el tope de 20 avisos por hora, y las solicitudes reales dejaban de avisar | Límite propio de 5 envíos por hora por IP, sublímite de 5 avisos de consultas y consultas repetidas descartadas |
| Seguridad (alto) | El registro de cookies aceptaba filas sin tope | Límite de 5 por hora por IP; solo se guarda la aceptación o su retiro |
| Seguridad (alto) | Sin la zona horaria de Chile en el servidor, la portada daba 500 y el formulario fallaba después de guardar | Se prueba el nombre IANA y el de Windows; la API no arranca sin la zona, y la hora se calcula antes de guardar |
| Seguridad (alto) | Cambiar la imagen de una publicación conservaba la autorización del logo anterior | La autorización se borra y hay que volver a darla |
| Seguridad (alto) | Un teléfono «válido» con muchos espacios no cabía en la columna y daba 500 | Se guarda normalizado (`+569XXXXXXXX`) |
| Seguridad (medio) | La vitrina pública decía qué trabajos llevan el logo de un cliente | Los visitantes reciben un formato sin esos datos |
| Seguridad (medio) | Volver a autorizar borraba quién y cuándo, y se podía autorizar una publicación sin logo | Se conserva el registro original; sin logo responde 400 |
| Seguridad (medio) | Cuerpos de petición sin límite en las rutas anónimas | 16 KB en contacto y cookies |
| Seguridad (bajo) | Datos fuera de rango en el catálogo daban 500 en vez de 400; la fecha del cartel quedaba en la hora del servidor | Validación en la API; la fecha se guarda en hora de Chile |
| Accesibilidad (alto) | «Ver más trabajos» desaparecía al pulsarlo y el foco se perdía | El botón sigue ahí mientras carga y el foco pasa al primer trabajo nuevo |
| Accesibilidad (alto) | El aviso de cookies podía tapar el elemento enfocado y el botón de WhatsApp | El aviso publica su alto: la página reserva ese espacio y WhatsApp sube |
| Accesibilidad (medio) | El aviso de cookies era lo último al tabular y, al cerrarlo, el foco se perdía | Va al principio del documento; al reabrirlo recibe el foco y al cerrarlo lo devuelve |
| Accesibilidad (medio) | Retirar la analítica no dejaba constancia | Se registra el retiro |
| Accesibilidad (medio) | El formulario podía enviar sin la versión de la política, y el resultado quedaba fuera de la vista | No envía sin versión; el resultado recibe la vista y el foco, y un error de la API enfoca su campo |
| Accesibilidad (medio) | Si el mapa interactivo fallaba, caía toda la portada | Vuelve a la imagen y ofrece reintentar; anuncia cuándo se activa; con el dedo no atrapa el desplazamiento de la página |
| Accesibilidad (medio) | La portada volvía al ancla cada vez que llegaban datos; sin reintentos; el estado no se actualizaba | Un solo ajuste por navegación, «Reintentar» en los errores y estado cada minuto |
| Accesibilidad (medio) | La CSP de producción admitía `localhost` | Se reduce en el build |
| Accesibilidad (bajo) | Texto alternativo que repetía el título, campo trampa autocompletable, botón del pie pequeño, tres nombres para WhatsApp | `alt` vacío (el título ya nombra el trabajo), trampa `inert`, alto mínimo, «Escribir por WhatsApp» en todos |

Quedan anotados para la fase de despliegue (16), en `desviaciones.md`:
- **Proxy inverso:** detrás de uno, los límites por IP verían la IP del proxy. Se configura con la lista de proxies de confianza.
- **Protección contra clickjacking** (`frame-ancestors`): no funciona en una etiqueta `<meta>` y requiere una cabecera del servidor.
- **Plazo de conservación** de las consultas y de los registros de cookies: se purga cuando lo definas (ver «Lo que necesito de ti»).

## Un aviso enviado por error

En la primera prueba en marcha, la demo usó el envío real, porque tus User Secrets tienen `Correo:Modo=Smtp`. El aviso de una consulta de prueba («Prueba de la fase 5 · Visitante de Prueba») salió por Gmail a la dirección ficticia `avisos.demo@letreria.test`. Ese dominio no existe, así que lo más probable es que la cuenta de avisos reciba un rebote de «dirección no encontrada»; puedes borrarlo. No salió nada a ninguna persona real. Desde entonces, la configuración de la demo fuerza la bandeja local.

## Lo que no se verificó

- **Correo real de una consulta en Gmail:** el envío real ya quedó probado en la fase 4. Aquí el aviso se comprobó en la bandeja local.
- **Teléfono real:** el diseño se revisó con la vista de 360 px del navegador, no en un teléfono.
- **Base real:** no se tocó. Le faltan 4 migraciones (`SepararEstadoPedidoYPago`, `HistorialDePagos`, `ServiciosComunes` y `PortadaPublica`), que se aplicarán con tu orden al terminar las fases.

## Lo que necesito de ti

1. **La ubicación exacta del taller.** En los mapas abiertos no aparece el número 810 de Villagrán, así que el marcador es aproximado y la portada lo dice. Pásame un enlace de Google Maps o las coordenadas y regenero el mapa.
2. **Los datos de los textos legales.** Están marcados «[completar: …]»:
   - razón social y RUT;
   - dónde estará alojado el sitio;
   - cuánto tiempo guardar las consultas de contacto y los registros de cookies.

   Antes de publicar, los dos documentos necesitan revisión de un abogado.
3. **Fotos reales y permisos.** Las fotos reales de trabajos, y para cada una que muestre el logo de un cliente, su autorización. Hasta entonces la vitrina usa imágenes de ejemplo marcadas como tales.
4. **Tu visto bueno a esta fase**, o lo que quieras cambiar.
