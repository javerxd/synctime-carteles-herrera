# Informe de cierre — Fase 4: servicios comunes (correo, consentimientos e imágenes)

Fecha: 13-11-2026 · Rama: `fase-04-servicios-comunes` (nace de `fase-03-estructura-y-pantallas`) · Estado: **en revisión, esperando el visto bueno del usuario**

Esta fase deja listos tres servicios que usan las fases siguientes: el correo con cola de salida, el registro de consentimientos y la subida de imágenes. Por pedido del usuario se agregó el **objetivo principal del sistema**: que el dueño reciba en su Gmail un aviso por cada solicitud nueva. Ese aviso ya funciona con las solicitudes que existen hoy (en línea, desde la cuenta del cliente y presenciales).

## Cómo verlo

La API sobre la base demo (el perfil `https` usa `LetreriaLosHerreraDB_Demo`, que ya recibió la migración nueva):

```
cd backend/Proyecto-Carteles-herrera-synctime-main
dotnet run --project .\LetrerosHerreraSYNCTIMEapi --launch-profile https -- --Correo:AvisosTaller=avisos.demo@letreria.test
```

El último argumento le da a la demo un destinatario de prueba. Sin él la API funciona igual, pero no hay a quién avisar.

- **Bandeja local:** en desarrollo los correos no salen del equipo. Quedan como archivos `.eml` y se ven en `https://localhost:7112/dev/bandeja`. Al crear una solicitud (desde Scalar en `https://localhost:7112/scalar` o desde la portada cuando llegue el envío de la fase 7), su aviso aparece ahí en uno o dos segundos.
- **Estado del correo:** `https://localhost:7112/salud/correo` responde `ok` o `degradado`.
- **Envío real a Gmail:** ver «Lo que necesito de ti».

## Qué se hizo

### Correo

| Tema | Resultado | Dónde |
|---|---|---|
| Cola de salida | La petición solo inserta el correo en `correos_salientes`. Un proceso en segundo plano lo envía en uno o dos segundos, porque el encolado lo despierta al instante | `Datos/Correo/ColaDeCorreo.cs`, `Infraestructura/Correo/ColaCorreoTrabajador.cs` |
| SMTP con MailKit | Valores listos para Gmail (`smtp.gmail.com`, 587, STARTTLS). Usuario y contraseña de aplicación solo desde User Secrets o variables de entorno. Cada envío tiene un tiempo máximo de 20 s. Sin cifrado solo se admite un servidor del mismo equipo | `Infraestructura/Correo/Enviadores.cs`, `OpcionesCorreo.cs`, `appsettings.json` |
| Reintentos | A los 1, 5, 15, 60 y 180 minutos. Si falla el servidor o la cuenta (caído, sin conexión, contraseña cambiada), el aviso sigue intentándose cada 3 horas sin darse por perdido. Solo un fallo del propio mensaje lo deja «fallido» | `Negocio/Correo/ReglasCorreo.cs`, `ClasificadorFallos.cs` |
| Credenciales | Nunca aparecen en registros, respuestas ni en la base. El error guardado es una categoría con un mensaje fijo en español, nunca el texto del servidor, que puede repetir el usuario | `ClasificadorFallos.cs` |
| Destinatarios | `IDestinatariosAvisos`, el único servicio que lee `Correo:AvisosTaller` (uno o más correos separados por `;`). Una prueba de arquitectura impide que otro archivo lea esa clave. En la fase 9 leerá primero el correo que guarde el dueño | `Datos/Correo/DestinatariosAvisos.cs`, `ArquitecturaTests.cs` |
| Plantilla base | HTML y texto plano, en español, en una columna de hasta 600 px que se adapta al teléfono. Tinta `#0F172A`, ámbar `#B45309` solo en la acción principal, botones secundarios con el borde de los controles del sistema y Plus Jakarta Sans con letras del sistema como respaldo. El logotipo viaja dentro del mensaje, así que no se pide nada a terceros. Todo lo que escribe un visitante se escapa | `Negocio/Correo/PlantillaCorreo.cs` |
| Aviso de solicitud nueva | En este orden: número y origen; cliente; teléfono y correo; botones para llamar, abrir WhatsApp (solo celulares) y escribir; la descripción citada; lo que pidió, con medidas; el botón al panel. «Responder» le escribe directo al cliente. Asunto: «Nueva solicitud N.º 123 · Nombre» | `Negocio/Correo/AvisoSolicitudNueva.cs`, `Services/ServiciosComunes.cs` |
| Tope contra envíos masivos | Pasadas 20 solicitudes en una hora, llega un solo aviso de «muchas solicitudes» y las demás quedan en el panel. Protege el cupo diario de Gmail (unos 500 envíos), para que un ataque no deje al dueño sin los avisos reales | `AvisosDelTaller` |
| Estado para el operador | `/salud/correo` dice `degradado` en cualquiera de estos casos: hay avisos fallidos, atrasados o que no llegaron a la cola; falta configuración; o no hay destinatarios. Mientras dure el problema, la API lo repite cada hora en el registro de errores. Una configuración mal escrita impide arrancar | `Api/Infraestructura.cs`, `Program.cs` |
| Bandeja local | Solo en desarrollo, solo en modo `Bandeja` y solo desde el mismo equipo. Muestra el correo aislado, sin scripts y con el logotipo incrustado | `Endpoints/BandejaLocalApi.cs` |
| Prueba real | Comando `probar-correo`: envía un correo de prueba por SMTP a los destinatarios y dice si funcionó, sin mostrar credenciales | `Infraestructura/Correo/PruebaDeCorreo.cs` |

### Consentimientos

La tabla `consentimientos` guarda, por cada aceptación:
- **Titular:** un cliente, un empleado o el envío de un visitante sin cuenta.
- **Tipo:** `privacidad`, `terminos`, `marketing` o `cookies`.
- **Versión del documento**, con la fecha y hora de aceptación y, si se retiró, la de revocación.

La base exige un solo titular por registro y una sola aceptación vigente por versión, también ante dos clics simultáneos. Si la versión que la persona leyó ya no es la vigente, no se registra. El servicio `ServicioConsentimientos` queda listo para los formularios de las fases 5 y 6.

### Imágenes

`POST /api/v1/imagenes` (dueño y administrador) recibe JPG, PNG o WebP de hasta 10 MB. `GET /api/v1/imagenes/{archivo}` sirve el resultado con caché de un año.
- **Tamaños:** dos WebP, de 1600 y 640 px por el lado mayor, sin agrandar.
- **Tipo:** se decide por el contenido. Un SVG o un archivo renombrado a `.png` no pasa.
- **Tamaño:** antes de decodificar se revisan las medidas, así que un archivo pequeño que declara 20.000 × 20.000 se rechaza sin agotar la memoria.
- **Orientación:** la foto se endereza según la cámara y pierde sus metadatos, incluida la ubicación GPS.
- **Nombres:** son aleatorios (128 bits), para que una imagen no publicada, como un logotipo sin autorización, no se encuentre probando.

### Migración

`ServiciosComunes`: solo crea `correos_salientes`, `consentimientos` e `imagenes`. No toca datos ni tablas existentes; lo comprueba `MigracionTests.ServiciosComunes_SoloCreaSusTablas`. Ya está aplicada en las bases de prueba y en la demo. **No se aplicó a la base real**, que sigue con 3 migraciones (lo comprobé con una consulta de solo lectura).

## Verificación

| Comprobación | Resultado |
|---|---|
| Pruebas del backend | 177 unitarias (antes 96) y 227 de integración (antes 187), todas en verde. El contrato `openapi.json` coincide con la API en rutas y permisos |
| Migración | En una base temporal: al subir crea las 3 tablas; al revertir las quita y deja la base en `HistorialDePagos` con sus 15 tablas. Después se borró la base temporal |
| Pruebas no vacías | 11 de 11 errores reintroducidos a propósito hacen fallar su prueba: guardar el texto del servidor, enviar dentro de la petición, no respetar la espera entre reintentos, reintentar un destinatario rechazado, servir cualquier nombre de archivo, ignorar la orientación, aceptar el tipo sin mirar el contenido, no escapar la plantilla, dar por perdido el aviso con el servidor caído, retomar sin fin un envío cortado y quitar el tope por hora |
| SMTP real con MailKit | Contra un servidor SMTP de pruebas local: entrega, servidor no disponible, servidor colgado (la petición respondió antes de que el SMTP agotara su espera), usuario rechazado (con el usuario repetido en la respuesta, que no se guarda ni se registra) y destinatario rechazado |
| La app en marcha | API real sobre la base demo. Una solicitud en línea respondió 201 y su aviso llegó a la bandeja local en menos de 3 segundos. Con la API ya iniciada, la petición completa, aviso incluido, tardó 35 ms. `/salud/correo`: `ok` |
| Diseño del correo | HTML revisado en el navegador a 360, 768 y 1440 px: sin desplazamiento horizontal y sin scripts. Tras la primera revisión se cambiaron tres cosas: las acciones de contacto subieron, ahora como botones secundarios con área de toque cómoda; la descripción pasó a cita; y la aclaración de seguridad pasó a nota |
| Script de la base | `LetreriaLosHerreraDB.sql` regenerado. Se creó una base temporal desde el script y se comparó con la demo: tablas, columnas, restricciones, índices, claves, objetos y migraciones coinciden (286 elementos). Después se borró |
| Vulnerabilidades de paquetes | `dotnet list package --vulnerable --include-transitive`: ninguna |
| Frontend | Sin cambios en esta fase |

## Revisión de ECC

Tres revisores. Ninguno encontró hallazgos críticos. Todo lo alto y lo medio quedó corregido y con su prueba.

| Revisor | Hallazgo | Corrección |
|---|---|---|
| `silent-failure-hunter` | Un corte de Gmail de más de unas 4 horas (por ejemplo, una contraseña cambiada) dejaba el aviso «fallido» para siempre | Los problemas del servidor o de la cuenta ya no agotan los intentos: se reintenta cada 3 horas |
| `silent-failure-hunter`, `csharp-reviewer` | Un correo que hace caer el proceso podía reintentarse sin fin | Se retoma mientras le queden intentos; después queda «fallido» |
| `silent-failure-hunter` | Nadie se enteraría de avisos fallidos, de una base sin la migración, de destinatarios vacíos o de un `Correo:Modo` mal puesto | `/salud/correo`, registro de errores cada hora, aviso crítico al arrancar si faltan migraciones, validación al arrancar y modo bandeja anulado fuera de desarrollo |
| `silent-failure-hunter` | La purga contaba desde la creación: un aviso que falló tarde perdía su rastro enseguida | Cuenta desde el envío o desde el fallo |
| `csharp-reviewer` | Un rechazo temporal (4xx) de Gmail se tomaba como definitivo | Solo un 5xx del destinatario es definitivo |
| `csharp-reviewer` | Un error al despedirse del servidor, después de entregar, haría reenviar el aviso | La despedida va aparte y no cuenta como fallo |
| `csharp-reviewer` | Una `Sitio:UrlPublica` mal escrita anulaba todos los avisos | El aviso sale igual, sin el botón al panel |
| `csharp-reviewer` | Un correo del cliente aceptado por la validación pero no por MimeKit bloqueaba el aviso | Solo se omite «Responder a» |
| `csharp-reviewer` | Al cerrar un envío podía pisarse el estado de otro proceso | La actualización exige que el correo siga reclamado por este proceso |
| `csharp-reviewer` | Dos aceptaciones simultáneas del mismo consentimiento | Índices únicos filtrados en la base |
| `csharp-reviewer` | Archivos de imagen huérfanos si fallaba la segunda escritura | Se limpian, también los temporales |
| `security-reviewer` | Un envío masivo de solicitudes anónimas podía agotar el cupo de Gmail y bloquear la cuenta | Tope de 20 avisos por hora y un solo aviso de «muchas solicitudes» |
| `security-reviewer` | `/dev/bandeja` quedaría abierta si un servidor corriera por error en modo desarrollo | Solo responde desde el mismo equipo y en modo `Bandeja`, y no muestra rutas del disco |
| `security-reviewer` | Una dirección como `x?bcc=…` agregaría destinatarios al hacer clic en «Escribirle» | Se escapa en el enlace `mailto:` |
| `security-reviewer` | Un nombre con saltos de línea o caracteres invisibles podía fabricar líneas falsas en el texto del correo | Se limpian, y el aviso indica que los datos de contacto los escribió el visitante |
| `security-reviewer` | Correo sin cifrar hacia un servidor remoto (sin credenciales) | Sin cifrado solo se admite un servidor del mismo equipo |
| Varios (bajos) | `$` en las expresiones regulares, direcciones con `;` o comillas, copia innecesaria de la imagen | Se usa `\z`; la validación de correos rechaza `;`, `,` y comillas; la imagen solo se copia si hay que girarla |

Al hacer la prueba en la app apareció un caso que las pruebas no cubrían. En la bandeja local, mientras no hay cuenta de Gmail configurada, no había dirección de remitente y la cola esperaba sin guardar nada. Ahora la bandeja usa `synctime@bandeja.local`, una dirección que no sale del equipo, y tiene su prueba.

Quedan anotados, sin cambio de código:
- **Detrás de un proxy**, los límites por IP verían la IP del proxy. Ya estaba en el README («Antes de producción»): se configura en la fase 16.
- **El aviso no es atómico con la solicitud:** se encola justo después de guardarla. Si ese paso fallara, la solicitud queda guardada y `/salud/correo` lo muestra durante un día. El patrón *outbox* (las dos cosas en una transacción) se evalúa en la fase 7, al rehacer las solicitudes.
- **Imágenes:** una imagen retirada sigue en la caché de los navegadores hasta un año, y borrarla debe borrar también sus archivos (fase 9). No hay cuota de subidas, que solo puede hacer el personal de gestión.
- **Consentimientos sin rutas todavía:** cuando se expongan (fases 5 y 6), el titular debe salir del token y no del cuerpo de la petición.

## Decisiones que conviene que revises

Están en `desviaciones.md`:

- **Aviso también para las solicitudes presenciales**, porque pediste «por cada solicitud nueva». Si avisar las que registras tú mismo resulta ruido, se quitan con una línea.
- **Tope de 20 avisos por hora** (`Correo:TopeAvisosPorHora`).
- **Contacto de un toque en el aviso:** llamar, WhatsApp (solo celulares), escribir y «Responder» directo al cliente.
- **SkiaSharp** (MIT) para las imágenes, en vez de ImageSharp, cuya licencia actual no es libre para todos los usos.

## Lo que no se verificó

- **Envío real a Gmail:** depende de la contraseña de aplicación que escribes tú (ver abajo). Hasta entonces, el SMTP queda probado con MailKit contra un servidor de pruebas local, con todos los casos de la tabla de verificación.
- **Cómo se ve el correo en Gmail, Outlook y el teléfono:** revisé el HTML en el navegador. Cada programa de correo lo dibuja distinto; cuando llegue el correo real, revísalo en tu teléfono.
- **Base real:** no se tocó. Le faltan 3 migraciones (`SepararEstadoPedidoYPago`, `HistorialDePagos` y `ServiciosComunes`). Contra ella la API funciona, pero las solicitudes no avisarán hasta aplicarlas con tu orden; `/salud/correo` lo dirá y la API lo registra al arrancar.
- **Linux (contenedor):** SkiaSharp trae sus bibliotecas para Linux, pero eso se prueba en la fase 16.

## Cambios en archivos

- **Backend, nuevos:**
  - `Infraestructura/Correo/` y `Infraestructura/Imagenes/` (adaptadores a SMTP y al disco).
  - `Datos/Correo/`, `Negocio/Correo/`, `Negocio/Imagenes/` y `Negocio/Consentimientos/`.
  - `Services/ServiciosComunes.cs`, `Endpoints/ImagenApi.cs` y `Endpoints/BandejaLocalApi.cs`.
  - `Models/CorreoSaliente.cs`, `Consentimiento.cs` e `Imagen.cs`, la migración `ServiciosComunes` y `Recursos/logo-lh-correo.png`.
- **Backend, modificados:**
  - `Program.cs`, `Api/Infraestructura.cs` (salud del correo) y la configuración (`appsettings*.json`, `.env.example`).
  - Los tres puntos que crean solicitudes (`SolicitudApi.cs` y `PortalClienteApi.cs`) y `SolicitudRepository.cs`.
  - `Validacion.cs`, que ahora rechaza `;`, `,` y comillas en los correos.
  - `.gitignore`, que excluye `bandeja-correo/` y `almacen/`.
  - README: secciones Correo e Imágenes.
- **Pruebas:**
  - Unitarias: `CorreoTests`, `ImagenesTests` y `ConsentimientosTests`.
  - Integración: `ColaDeCorreoTests`, `ImagenApiTests` y `ConsentimientoServicioTests`.
  - El servidor SMTP de pruebas `ServidorSmtpDePrueba`.
  - Cambios en `ApiFixture`, `MigracionTests` y `ArquitecturaTests`.
- **Documentos:**
  - Contrato (`openapi.json`, `contrato.md`) y `permisos.md`.
  - Diccionario, modelo y script de la base.
  - Plan (fases 4, 9 y 11, y la tabla de insumos), `desviaciones.md`, `trazabilidad.md`, `tablero.md` y `licencias.md`.

## Lo que necesito de ti

1. **La contraseña de aplicación de Gmail, escrita por ti en User Secrets.** Los comandos están en el README del backend, sección «Correo»; la contraseña se escribe sin que se vea en pantalla. Son tres valores:
   - `Correo:Smtp:Usuario`: la cuenta de Gmail que enviará los avisos.
   - `Correo:Smtp:Password`: su contraseña de aplicación. Se crea en <https://myaccount.google.com/apppasswords> y exige tener activa la verificación en dos pasos.
   - `Correo:AvisosTaller`: tu correo, para las pruebas. Después lo cambias por el del dueño.

   Avísame cuando estén y ejecuto `probar-correo`. Si quieres, también una solicitud de prueba sobre la base demo con el envío real activado. Si la cuenta no admite contraseñas de aplicación, la alternativa es OAuth2, explicada en el README: exige un proyecto en Google Cloud y publicar la aplicación, porque si no, el permiso vence a los 7 días.
2. Tu visto bueno a esta fase, o lo que quieras cambiar.
3. Pendiente de antes: el WhatsApp, el teléfono y el correo del taller para la portada (fase 5), y cuándo aplicar a la base real las migraciones pendientes, que ahora son 3.
