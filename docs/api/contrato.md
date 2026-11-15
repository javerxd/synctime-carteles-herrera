# Contrato de la API para el frontend

Este documento reune lo necesario para adaptar el frontend (`frontend/synctimefront`) cuando esten definidos los requisitos finales. **Estado al 14-11-2026:** la API esta lista y probada hasta la fase 7 (solicitudes, adjuntos y clientes). En el frontend, el ingreso, el registro, la recuperacion y Mi cuenta usan este contrato desde la fase 6, y las solicitudes, los adjuntos y los clientes, desde la fase 7. El login facial de empleados se describe en la seccion 7.

La referencia viva es Scalar (`/scalar`) y OpenAPI (`/openapi/v1.json`), disponibles en Development.

## 1. Conexion

| Dato | Valor |
| --- | --- |
| API por HTTPS | `https://localhost:7112/api/v1`. Requiere arrancar con `dotnet run --project LetrerosHerreraSYNCTIMEapi --launch-profile https` |
| API por HTTP | `http://localhost:5194/api/v1` (perfil por defecto de `dotnet run`) |
| Origenes permitidos en desarrollo | `http://localhost:3000`, `https://localhost:3000`, `http://localhost:5173`, `https://localhost:5173` |
| Otros ambientes | Configurar `Cors:AllowedOrigins` |

`VITE_API_URL` del frontend apunta por defecto a `https://localhost:7112`, por lo que necesita el perfil `https`.

## 2. Sesion y roles

El token de acceso va en la cabecera `Authorization: Bearer <token>` y dura 15 minutos (`Jwt:ExpirationMinutes`). La sesion continua con una cookie de renovacion que la API deja al ingresar: ver «Cuenta y sesion (fase 6)» en la seccion 4.

| Login | Cuerpo | Respuesta |
| --- | --- | --- |
| `POST /auth/login` (personal) | `{ email, password }` | `{ token, tipo: "empleado", usuario: { idEmpleado, nombre, apellido, email, rol } }` |
| `POST /auth/login-cliente` | `{ correo, password }` | `{ token, tipo: "cliente", usuario: { idCliente, nombre, apellido, telefono, email, correoVerificado } }`. El cliente entra con su correo, no con su telefono |
| `POST /auth/login-facial` (personal) | `{ email, desafio, tokenEquipo, capturas }`, despues de pedir el desafio | Lo mismo que `/auth/login`, mas `nombre` y `rol`. Ver seccion 7 |

Los roles son exactamente estos, con `ñ` y en minusculas: `dueño`, `administrador`, `diseñador`, `operario` y `cliente`. En la tabla de rutas:

- **Publico:** sin token.
- **Personal:** los cuatro roles de empleado.
- **Gestion:** `dueño` y `administrador`.

## 3. Reglas generales

**Errores.** En `400` y `409`, el motivo viene en el campo `error` del JSON. Hay que prever un mensaje por defecto: fuera de Development, un JSON mal formado responde `400` sin cuerpo. Las respuestas `401`, `403`, `404` y `429` tampoco traen motivo y se distinguen por el codigo.

| Codigo | Significado |
| --- | --- |
| `400` | Datos invalidos |
| `401` | Sin token, token vencido o credenciales incorrectas |
| `403` | El rol no tiene permiso |
| `404` | No existe, o no es visible para quien lo pide |
| `409` | Duplicado, tiene registros asociados, o lo modifico otra persona |
| `413` | El cuerpo supera el tope de su ruta: 1 KB al pedir el desafio de rostro, 32 KB en el login de rostro, 64 KB al registrar un rostro, y 16 KB en `POST /contacto` y `POST /consentimientos/cookies`. Una subida de adjunto que pasa de 10 MB responde `400` con `archivo_muy_grande`, no `413` |
| `429` | Limite por IP por minuto: 10 logins con contraseña, 10 desafios de rostro, 10 logins con rostro, 10 confirmaciones de contraseña, 10 envios publicos o 100 peticiones generales. Cada uno tiene su contador. `POST /contacto` y `POST /consentimientos/cookies` tienen además 5 envíos por hora por IP cada uno (`RateLimiting:ContactoPorHora` y `CookiesPorHora`). Ademas, 10 fallos por cuenta por minuto en el ingreso, la recuperacion y los cambios de correo o contraseña (`RateLimiting:PorCuenta`). Las subidas de adjuntos tienen 10 por minuto por usuario (`RateLimiting:Adjuntos`) |
| `503` | El login facial no esta disponible porque falta su clave de cifrado. Tambien `503` con `servicio_ocupado` y la cabecera `Retry-After: 5` si el servidor no consigue el turno de la regla contra solicitudes falsas o de una subida de adjunto en el plazo de `Solicitudes:EsperaBloqueoMs` (15 segundos por defecto) |

**Edicion simultanea.** Solicitudes, cotizaciones (fase 8) y pedidos traen `rowVersion` en sus respuestas y en la cabecera `ETag` (la version en base64, entre comillas). Las rutas que los editan, responden o cambian su estado la exigen, en la cabecera `If-Match` (preferida) o en `rowVersion` del cuerpo:

1. Guardar la version que entrega el GET, junto con los datos del formulario.
2. Enviarla sin cambios en el `PUT`, el `PATCH` o el `DELETE`.
3. Si responde `409` con `version_obsoleta`, avisar que otra persona cambio el registro y volver a cargarlo. Si responde `200`, usar la version de esa respuesta para la siguiente edicion.

Si falta, responde `400` con `sin_version`. Eliminar tambien la pide. Subir o quitar un adjunto de una solicitud tambien cambia su version. Registrar o anular un abono **no** la pide (solo suma o resta, y la base protege el tope), pero cambia la version del pedido: la respuesta trae el `ETag` nuevo. Clientes y productos no tienen version: ahi prevalece la ultima edicion.

**Inactivos y ocultos.**

- Por defecto, las listas devuelven solo lo que se vende, tambien al personal con sesion. Asi, quien mira la pagina publica ve lo mismo que un visitante.
- Las pantallas de administracion deben pedir `?incluirInactivos=true` en productos y categorias, y `?incluirOcultos=true` en catalogo. Solo tiene efecto para el personal.
- `GET` por id devuelve `404` a quien no sea personal si el registro esta inactivo u oculto.
- Un producto de una categoria inactiva cuenta como inactivo, y un trabajo de un producto inactivo no se muestra en la vitrina.
- Las solicitudes nuevas rechazan con `400` los productos no disponibles.

**Datos que salen del token.** No se aceptan en el cuerpo: el empleado que registra una solicitud, cotizacion o trabajo del catalogo, el que modifica el cartel, y el cliente de las rutas `/mis`.

**Confirmar la contraseña.** Las acciones delicadas piden la contraseña de quien las hace, en todas las sesiones, porque el token no indica si se entro con rostro. Si es incorrecta responde `400` y no `401`, porque el frontend cierra la sesion ante cualquier `401`.

**Fechas.** Formato ISO 8601. **Claves JSON.** `camelCase`.

## 4. Rutas

Todas llevan el prefijo `/api/v1`. Un `?` indica un campo opcional.

### Autenticacion

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `POST /auth/registro-cliente` | Publico | `{ rut?, nombre, apellido, telefono, correo, direccion?, password, aceptaTerminosYPrivacidad, aceptaMarketing, versionTerminos, versionPrivacidad, versionMarketing? }`. `201` con la sesion iniciada (`{ token, tipo, usuario }`), la cookie de renovacion y el envio del enlace para confirmar el correo. Terminos y privacidad son obligatorios; el marketing es otra casilla, sin marcar por defecto. Password de 8 a 72 caracteres. `400` con los errores por campo; `409` si el correo ya tiene cuenta, si el telefono o el RUT ya estan en el taller, o si cambiaron las versiones de los textos; `413` si supera 16 KB |
| `GET /auth/me` | Con sesion | Lista de claims del token |
| `POST /auth/login-facial/desafio` | Publico | `{ email }`. Responde `200` exista o no el email: `{ desafio, orden: ["izquierda","derecha"] o ["derecha","izquierda"], venceEnSegundos }`. `400` solo si falta el email o es demasiado largo. De un solo uso; vence a los 30 s |
| `POST /auth/login-facial` | Publico | `{ email, desafio, tokenEquipo, capturas }`, con tres capturas `{ descriptor: float[128], puntos: [[x,y] x 68], ancho, alto }` (frente, primer giro, segundo giro). Todo fallo responde el mismo `401` vacio. Ver seccion 7 |
| `GET /auth/rostro` | Personal | `{ disponible, activo, debeRegistrarseDeNuevo, puedeUsarRostro, fechaConsentimiento, requiereEquipo, equipoVence, equipoVigente, consentimiento: { version, texto } }` |
| `PUT /auth/rostro` | Gestion (los demas, `403 solo_gestion`) | `{ password, consentimiento: true, versionConsentimiento, muestras: { frente, izquierda, derecha } }`. Devuelve `{ idEmpleado, activo, fechaConsentimiento, tokenEquipo, equipoVence }`. `400` con `errores["muestras.frente"]`, `["muestras.izquierda"]` o `["muestras.derecha"]` si una muestra no sirve |
| `POST /auth/rostro/equipo` | Gestion (los demas, `403 solo_gestion`) | `{ password }`. Autoriza este equipo y anula el anterior. Devuelve `{ tokenEquipo, equipoVence }` |
| `DELETE /auth/rostro` | Personal | Borra las muestras sin pedir contraseña. Responde `204`, tambien si no estaba activo |
| `GET /rostros` | Gestion | Rostros registrados (S16): empleados activos de gestion con `{ idEmpleado, nombre, rol, estado (activo, debe_registrarse, sin_registro o desactivado), fechaRegistro, fechaConsentimiento, registradoPor, fechaDesactivacion, historial }` |
| `PUT /rostros/{idEmpleado}` | Gestion | Registra el rostro de **otro** empleado de gestion, presente: `{ passwordTitular, consentimiento, versionConsentimiento, muestras }`. La contraseña es la del titular (`400 datos_invalidos` si no corresponde). No entrega token de equipo |
| `DELETE /rostros/{idEmpleado}` | Gestion; el resto del personal solo el propio | Desactiva y borra las muestras. `204` |
| `POST /rostros/calibracion` | Gestion, con `RostroFacial:Calibracion:Activa` (si no, `404`) | `{ idEmpleado, etiqueta: propio u otra_persona, desafio, capturas }`. `201` con las distancias (2 decimales), los giros, `peorDistancia` y `habriaEntrado`. Maximo 30 por empleado y hora |
| `GET /rostros/calibracion`, `DELETE /rostros/calibracion` | Gestion, con la calibracion activa | `{ activa, umbralVigente, rangos, mediciones, resumen: { propio, otra, umbralSugerido, margen } }`; el `DELETE` borra todas las mediciones (`204`) |

### Cuenta y sesion (fase 6)

La sesion tiene dos piezas. El **token de acceso** (15 minutos) va en `Authorization`, y la pagina lo guarda solo en memoria. El **token de renovacion** va en la cookie `synctime_renovacion`: `httpOnly`, `Secure`, `SameSite=Strict` y solo para `/api/v1/auth`, asi que no viaja en las demas peticiones. La cookie `synctime_csrf` sí la puede leer la pagina, y debe repetirse en la cabecera `X-CSRF-Token` al renovar y al salir: un sitio ajeno no puede leerla, asi que no puede imitar esas peticiones.

- **Duracion:** 12 horas para el personal (`Sesion:HorasPersonal`) y 7 dias para el cliente (`Sesion:DiasCliente`), contados desde el ingreso. Renovar el token no alarga la sesion.
- **Rotacion:** cada renovacion cambia la cookie. Si vuelve a aparecer un token ya usado, se cierra toda su sesion. Dos pestañas que renuevan a la vez tienen 10 segundos de gracia: la segunda recibe `409` y reintenta con la cookie nueva.
- **Cierre:** `POST /auth/salir` cierra la sesion de ese equipo. Cambiar o restablecer la contraseña y desactivar la cuenta cierran todas las sesiones de la cuenta. Cambiar el correo desde Mi cuenta no las cierra (exige la contraseña y avisa al correo anterior); cuando lo cambia el personal con `PUT /clientes/{id}`, sí se cierran. El token de acceso ya emitido sigue valido hasta 15 minutos.
- **Cuenta inactiva:** si la cuenta ya no esta activa, la renovacion la rechaza y cierra la sesion.

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `POST /auth/renovar` | Publico, con cookie | Sin cuerpo. Exige `X-CSRF-Token` igual a la cookie `synctime_csrf`; si falta o no coincide, `400`. Responde `200` con `{ token, tipo, usuario }` y rota la cookie. `401` si la sesion no sirve (borra las cookies); `409` si otra pestaña acaba de renovarla: reintentar |
| `POST /auth/salir` | Publico, con cookie | Sin cuerpo. Exige `X-CSRF-Token`. Revoca la sesion de este equipo y borra las cookies. `204` |
| `POST /auth/verificar-correo` | Publico | `{ token }`, el del enlace del correo. `200` con `{ verificado: true }`. `400` si el enlace ya se uso, vencio (24 horas) o el correo cambio despues de enviarlo |
| `POST /auth/reenviar-verificacion` | Cliente | Sin cuerpo. `202`. Envia un enlace nuevo solo si el correo aun no esta confirmado; el anterior deja de servir. `404` si la cuenta ya no esta activa |
| `POST /auth/recuperar` | Publico | `{ tipo, correo }`, con `tipo` `cliente` o `personal`. `202` con el mismo mensaje exista o no la cuenta, y tarda lo mismo. Envia un codigo de 6 digitos que vence en 15 minutos. Como maximo 1 codigo por minuto y 5 por hora por cuenta: si se supera, no se envia nada y la respuesta sigue igual. `400` si el tipo no es valido |
| `POST /auth/restablecer` | Publico | `{ tipo, correo, codigo, password }`. `204`: cambia la contraseña, consume el codigo y cierra todas las sesiones de la cuenta. Solo sirve el codigo enviado al correo actual de la cuenta, de un solo uso y con 5 intentos. `400` con `errores.codigo` (incorrecto, vencido o agotado) o `errores.password` |
| `GET /mi-cuenta` | Cliente | `{ idCliente, nombre, apellido, rut, telefono, correo, direccion, correoVerificado, marketing }`. Solo los datos del propio cliente: el id sale del token |
| `PUT /mi-cuenta` | Cliente | `{ rut?, nombre, apellido, telefono, direccion? }`. Responde la cuenta. `400` por campo. `409` si el telefono o el RUT ya son de otra persona. El RUT es opcional y puede quitarse |
| `PUT /mi-cuenta/correo` | Cliente | `{ correo, passwordActual }`. Responde la cuenta con `correoVerificado: false`, avisa al correo anterior y envia el enlace al nuevo. `400` si la contraseña no es correcta (`errores.passwordActual`) o el correo no sirve; `409` si ya esta en otra cuenta. Limite: 10 fallos por minuto |
| `PUT /mi-cuenta/password` | Cliente | `{ passwordActual, passwordNueva }`. Responde `{ token }`: cierra todas las sesiones de la cuenta, incluida la actual, y abre una nueva en este equipo. `400` con `errores.passwordActual` o `errores.passwordNueva`. Limite: 10 fallos por minuto |
| `PUT /mi-cuenta/marketing` | Cliente | `{ acepta, version? }`. `204`. Aceptar exige la version vigente del marketing (`409` si cambio). Retirarlo no pide version y no afecta a los terminos |

**Correo y enlaces.** El correo es el usuario para ingresar y para recuperar la contraseña, así que una cuenta con contraseña siempre tiene correo. El cliente puede usar su cuenta sin confirmar el correo. Confirmarlo es necesario para las campañas (fase 11) y, desde la fase 7, para enviar solicitudes en línea y adjuntar archivos. El enlace de confirmación dura 24 horas, sirve una sola vez y sale desde `Sitio:UrlPublica`.

### Clientes, personal y materiales

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /clientes` | Personal | Paginado, ordenado por apellido y nombre: `?q=` busca por nombre, telefono o RUT; `?pagina=` (1) y `?tamano=` (20, maximo 100). Responde `{ elementos, total, pagina, tamano, totalPaginas }`. Cada fila trae `idCliente`, `rut`, `nombre`, `apellido`, `telefono`, `email`, `direccion`, `registrado`, `correoConfirmado`, `activo` y `fechaRegistro` |
| `GET /clientes/buscar?telefono=&rut=` | Personal | Para el registro presencial: busca antes de crear, por telefono o por RUT. Responde una lista de 0 o 1 cliente. `400` si no se envia ninguno de los dos |
| `GET /clientes/{id}` | Personal | Ficha con `rowVersion` y `ETag` (fase 9) |
| `POST /clientes`, `PUT /clientes/{id}` | `dueño`, `administrador`, `operario` | `{ rut?, nombre, apellido, telefono, email?, direccion?, activo? }`. El correo se guarda en minusculas y sin espacios. Un cliente con contraseña necesita correo (`400`). Si el correo cambia, queda sin confirmar y se envia un enlace nuevo. Desactivarlo o cambiarle el correo cierra sus sesiones. Fase 9: en `PUT`, si no viene `activo` se conserva el actual; nombre, apellido y teléfono se validan por campo (`400`); desactivar y cerrar sus sesiones van en una transacción. (RNFP10) `PUT` exige `If-Match` (`400 sin_version`, `409 version_obsoleta`) y responde la ficha con su `ETag`, igual que los mantenedores |
| `PUT /clientes/{id}/credenciales` | `dueño`, `administrador`, `operario` | `{ password, passwordActual, correo? }`. Activa el acceso web de un cliente que ya existe. Sin correo no hay cuenta: si el cliente no tiene uno guardado, hay que enviarlo en `correo`. `400` si el correo no sirve; `409` si el cliente esta desactivado o el correo ya es de otra cuenta. Cierra sus sesiones anteriores y envia el enlace para confirmar el correo si aun no esta confirmado. Si la cuenta ya tenia contraseña, el correo anterior recibe «El acceso a tu cuenta de Letreros Herrera cambió». `password` es la del cliente; `passwordActual`, la de quien la asigna |
| `GET /empleados/activos` | Personal | Lista corta `[{ idEmpleado, nombre, rol }]` del personal activo (`nombre` = nombre y apellido), para elegir al responsable de un pedido |
| `GET /empleados`, `GET /empleados/{id}` | Gestion | Paginado con `q` (nombre, correo o RUT), `activo` (`true`, `false` o `todos`; sin filtro, todos), `orden` (`nombre`, `apellido`, `rol`), `pagina`, `tamano`. Cada fila: `{ idEmpleado, rut, nombre, apellido, telefono, email, rol, activo, rowVersion }`, nunca la contraseña ni el rostro |
| `POST /empleados` | Gestion | Fase 9 (antes `/auth/empleados`): `{ rut, nombre, apellido, telefono, email, rol, password, passwordActual }`. RUT válido, teléfono de 9 dígitos, correo en minúsculas, rol uno de los 4. `400` por campo (o «La contraseña actual no es correcta.»), `409 ya_existe` (RUT o correo). `201` |
| `PUT /empleados/{id}` | Gestion | Los mismos datos, sin `password`. Cambiar el correo o el rol exige además `passwordActual` de quien edita (`400 datos_invalidos` con `errores.passwordActual` si falta o no es correcta; límite de las acciones delicadas). Cambiar el correo cierra sus sesiones, anula sus códigos de recuperación pendientes y avisa al correo anterior («Tu correo de ingreso a SyncTime cambió», con la dirección nueva enmascarada). Editar o desactivar exige la versión vista: cabecera `If-Match` con el `ETag` del `GET` (o `rowVersion` en el cuerpo); sin ella `400 sin_version`, con una vieja `409 version_obsoleta`. Las respuestas traen `rowVersion` y el `ETag`. Cambiar el rol cierra sus sesiones. `409 propio` (cambiarse el rol a sí mismo) o `409 ultimo_dueno` |
| `PATCH /empleados/{id}/activo` | Gestion | `{ activo }`. Editar o desactivar exige la versión vista: cabecera `If-Match` con el `ETag` del `GET` (o `rowVersion` en el cuerpo); sin ella `400 sin_version`, con una vieja `409 version_obsoleta`. Las respuestas traen `rowVersion` y el `ETag`. Desactivar, en una transacción: `activo = false`, se borran su rostro y su descriptor, y se cierran todas sus sesiones. `409 propio` o `ultimo_dueno` |
| `GET /materiales`, `GET /materiales/{id}` | Personal | Paginado (`q`, `activo`, `orden`: `nombre`, `costo`). El personal que no es de gestión ve solo los activos; gestión, todos por defecto |
| `POST /materiales`, `PUT /materiales/{id}`, `PATCH /materiales/{id}/activo` | Gestion | `{ nombre (2–100, único), unidadMedida (1–20), costoUnitarioAprox (entero ≥ 0) }`. Editar o desactivar exige la versión vista: cabecera `If-Match` con el `ETag` del `GET` (o `rowVersion` en el cuerpo); sin ella `400 sin_version`, con una vieja `409 version_obsoleta`. Las respuestas traen `rowVersion` y el `ETag`. `409 ya_existe` |

### Vitrina

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /categorias`, `GET /categorias/{id}` | Publico | Fase 9: paginado (`q`, `activo`, `orden`: `nombre`, `id`; `pagina`, `tamano`). El público y el personal que no es de gestión ven solo las activas; gestión, todas por defecto (o `?activo=true|false|todos`). Cada una: `{ idCategoria, nombre, descripcion, activo, rowVersion }`; el `GET /{id}` trae `ETag` |
| `POST /categorias`, `PUT /categorias/{id}`, `PATCH /categorias/{id}/activo` | Gestion | `{ nombre (2–80, único), descripcion? (≤300) }` (`activo` opcional solo al crear). Editar o desactivar exige la versión vista: cabecera `If-Match` con el `ETag` del `GET` (o `rowVersion` en el cuerpo); sin ella `400 sin_version`, con una vieja `409 version_obsoleta`. Las respuestas traen `rowVersion` y el `ETag`. `409 ya_existe` |
| `GET /productos`, `GET /productos/{id}` | Publico | Paginado como categorías, con `?categoria=` y `orden` `nombre`, `categoria`, `precio`. Quien no es de gestión ve solo los que se ofrecen (activos y de categoría activa). Cada uno: `{ idProducto, idCategoria, categoria: { idCategoria, nombre, activo }, nombre, descripcion, precioBaseRef, activo, rowVersion }` |
| `POST /productos`, `PUT /productos/{id}`, `PATCH /productos/{id}/activo` | Gestion | `{ idCategoria (activa; al editar puede conservar la suya), nombre (2–100), descripcion? (≤500), precioBaseRef? (entero ≥ 0) }`. Editar o desactivar exige la versión vista: cabecera `If-Match` con el `ETag` del `GET` (o `rowVersion` en el cuerpo); sin ella `400 sin_version`, con una vieja `409 version_obsoleta`. Las respuestas traen `rowVersion` y el `ETag`. `409 ya_existe`: el nombre es único en todo el catálogo (lo exige un índice de la base) |
| `GET /catalogo`, `GET /catalogo/{id}` | Publico | Paginado: `?categoria=`, `?pagina=` (1) y `?tamano=` (20, máximo 100). Responde `{ elementos, total, pagina, tamano, totalPaginas }`, ordenado por fecha de publicación, la más reciente primero. Cada trabajo trae `idCatalogo`, `titulo`, `descripcion`, `idProducto`, `tipoLetrero`, `categoria`, `anchoMm`, `altoMm`, `precioReferencial`, `imagen` (`urlGrande`, `urlMiniatura`, `ancho`, `alto`, `esEjemplo`, o `null`) y `fechaPublicacion`. Los visitantes y el personal que no es de gestión no reciben `visible`, `incluyeLogoCliente`, `logoAutorizado`, `idImagen` ni `rowVersion`. Gestión: `?incluirOcultos=true`, `?q=` (título o tipo de letrero) y `?visible=true|false`; `GET /{id}` con `ETag`. `GET /catalogo/{id}` responde `404` si el trabajo no es visible para quien pide |
| `POST /catalogo`, `PUT /catalogo/{id}` | Gestion | `{ idProducto, titulo, descripcion, imagenUrl?, idImagen?, anchoMm?, altoMm?, precioReferencial?, visible?, incluyeLogoCliente? }`. `imagenUrl` es la dirección de una foto antigua; las fotos nuevas se suben con `POST /imagenes` y se referencian con `idImagen`. `400` con el motivo si el título no tiene entre 1 y 120 caracteres, la descripción pasa de 500, las medidas no van juntas o no son mayores que cero, el precio no es un monto entero en pesos no negativo, o el tipo de letrero o la imagen no existen. Una publicación con logo de cliente solo puede quedar visible con su autorización: al crearla, guárdala oculta y autorízala después; si no, `409 logo_sin_autorizacion` (también cuando lo rechaza `ck_catalogo_logo` en la base, con `nivel: base_de_datos`). Al cambiar la imagen o marcar el logo en un `PUT`, la autorización anterior se borra. Editar o desactivar exige la versión vista: cabecera `If-Match` con el `ETag` del `GET` (o `rowVersion` en el cuerpo); sin ella `400 sin_version`, con una vieja `409 version_obsoleta`. Las respuestas traen `rowVersion` y el `ETag`. Responden la publicación completa (`visible`, `incluyeLogoCliente`, `logoAutorizado`); `POST` responde `201` |
| `PUT /catalogo/{id}/autorizacion-logo` | Gestion | `{ autorizado }`. `true` registra quién autorizó el logotipo del cliente y cuándo; si ya estaba registrada, conserva el registro original. `false` la retira y, si la publicación lleva logo, la oculta en el mismo paso. `400` si la publicación no lleva logo de cliente. `404` si no existe. Responde la publicación con su `ETag`. Si viene `If-Match`, se comprueba (`409 version_obsoleta`) |
| `PATCH /catalogo/{id}/visible` | Gestion | `{ visible }`. Editar o desactivar exige la versión vista: cabecera `If-Match` con el `ETag` del `GET` (o `rowVersion` en el cuerpo); sin ella `400 sin_version`, con una vieja `409 version_obsoleta`. Las respuestas traen `rowVersion` y el `ETag`. `409 logo_sin_autorizacion` |

Fase 9: no hay borrado físico. Los `DELETE` de categorías, productos, publicaciones y clientes se retiraron (`405`): se desactiva u oculta.

### Local, horario y disponibilidad (fase 9)

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /horarios` | Publico | Los 7 dias: `[{ diaSemana, nombreDia, abierto, horaInicio, horaFin }]`, horas `"HH:mm"` (null si cierra). No dice quién lo cambió |
| `PUT /horarios` | Gestion | `{ dias: [{ diaSemana, abierto, horaInicio?, horaFin? }] }` con los 7 días, en una transacción; guarda quién y cuándo. El cierre debe ser posterior a la apertura: `400` con errores por `"{dia}.horaInicio"` / `"{dia}.horaFin"` (o `dias` si faltan días). Responde los 7 días |
| `GET /mi-disponibilidad` | Personal | `{ franjas: [{ diaSemana, horaInicio, horaFin }] }` |
| `PUT /mi-disponibilidad` | Personal | `{ franjas: [...] }` reemplaza todas las propias (lista vacía = ninguna). Inicio antes que fin, sin cruces en el mismo día (pueden tocarse), dentro del horario de ese día; un día cerrado no admite franjas; hasta 28. `400` con errores por `"franjas[i].diaSemana|horaInicio|horaFin"`. Responde como el `GET` |
| `GET /disponibilidad` | Gestion | `[{ idEmpleado, nombre, rol, franjas: [...] }]` del personal activo que registró franjas |
| `GET /local/atencion` | Publico | `[{ diaSemana, nombreDia, franjas: [{ desde, hasta }] }]`, siempre 7 días: la unión de las franjas del personal activo, recortada al horario, sin nombres ni identificadores |
| `GET /cartel-digital` | Publico | `{ abiertoEnLocal, mensajeEstado, tiempoEstimadoRegreso, fechaUltimaModificacion }` |
| `PUT /cartel-digital` | Personal | Fase 9 (decisión 1): todo el personal. `{ abiertoEnLocal, mensajeEstado (1–200), tiempoEstimadoRegreso? (≤100) }`; guarda quién y cuándo. Responde `{ abiertoEnLocal, mensajeEstado, tiempoEstimadoRegreso, fechaUltimaModificacion, idEmpleadoModifica, modificadoPor }` |

### Avisos del taller (fase 9)

A qué correos llegan los avisos de solicitudes nuevas y consultas. Solo dueño y administrador (el resto del personal y los clientes reciben `403`), salvo confirmar. `IDestinatariosAvisos` usa los destinos `activo` de la base; si no hay ninguno, `Correo:AvisosTaller` (respaldo). Correos de la casilla automática, escritos en la cola en la misma transacción que el cambio.

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /avisos-taller` | Gestion | `{ destinos: [{ idDestinatario, correo, estado: activo|pendiente, reemplazaA: { idDestinatario, correo } | null, fechaSolicitudChile, fechaConfirmacionChile, venceChile }], usaRespaldo, historial: [{ fechaChile, accion, correo (enmascarado), empleado (nombre o null), detalle }] }` (últimos 20) |
| `POST /avisos-taller/destinos` | Gestion | `{ correo, idReemplaza?, passwordActual }` (`400 datos_invalidos` con `errores.passwordActual` si falta o no es correcta). `201` con el destino `pendiente`; el correo nuevo recibe «Confirma el destino de los avisos de Letreros Herrera» con un enlace de un solo uso a `{Sitio:UrlPublica}/avisos/confirmar?token=…` que vence en 24 horas. Hasta confirmar no recibe avisos, y el que reemplaza sigue recibiéndolos. `400` (`errores.correo`), `404` si `idReemplaza` no existe, `409 ya_existe`, `409 reemplazo_pendiente`, `409 transicion_invalida` (reemplazar uno no activo), `409 demasiados_pendientes` (ya hay 3 esperando), `429` si a ese correo ya se envió un enlace hace menos de un minuto o 5 veces en la hora (aunque se haya cancelado), `503` sin `Sitio:UrlPublica`. Un pendiente vencido no bloquea: se cancela solo |
| `POST /avisos-taller/destinos/{id}/reenviar` | Gestion | Enlace nuevo (el anterior deja de servir). `429` si se envió hace menos de un minuto o ya hubo 5 reenvíos en la hora. `409 transicion_invalida` si no está pendiente |
| `POST /avisos-taller/destinos/{id}/cancelar` | Gestion | El pendiente pasa a `cancelado`. Responde el destino |
| `DELETE /avisos-taller/destinos/{id}` | Gestion | Cuerpo `{ passwordActual }` (mismo `400`). Quita un activo (pasa a `retirado`) y le avisa «Este correo ya no recibe los avisos del taller». `204`. `409 ultimo_destino` («Debe quedar al menos un correo activo. Para cambiarlo, reemplázalo»); `409 reemplazo_pendiente` si otro destino espera confirmación para reemplazarlo (cancélalo primero) |
| `POST /avisos-taller/confirmar` | Publico | `{ token }`. `200 { correo: "c•••o@gmail.com" }`. `400` («El enlace no es válido o ya venció. Pide uno nuevo desde el perfil») si es inválido, vencido o ya usado. Al confirmar, el reemplazado pasa a `retirado` y recibe «El destino de los avisos de Letreros Herrera cambió»; si era un agregado, cada activo anterior recibe «Se agregó un destino a los avisos del taller» (si no había activos, lo reciben las direcciones del respaldo `Correo:AvisosTaller`). Límite por IP de las rutas de cuenta |
| `POST /avisos-taller/prueba` | Gestion | Sin cuerpo. `202 { idsCorreos, destinatarios, usaRespaldo }`: un aviso de ejemplo («Nueva solicitud #0 - Prueba del taller», tipo `prueba_avisos`, no cuenta para el tope por hora) por destinatario. `429` después de 3 en 10 minutos; `409 sin_destinos` si no hay a quién |
| `GET /avisos-taller/prueba/estado?ids=1,2` | Gestion | `[{ idCorreo, destinatario, estado: pendiente|enviado|fallido, intentos, error }]`, solo de correos `prueba_avisos` (hasta 20 ids) |

### Imagenes (fase 4)

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `POST /imagenes` | Gestion | `multipart/form-data` con el campo `archivo`: JPG, PNG o WebP de hasta 10 MB (el tipo se reconoce por el contenido, no por el nombre). Responde `201` con `{ idImagen, clave, ancho, alto, urlGrande, urlMiniatura }` (fase 9: `idImagen` es el que se envía al crear o editar una publicación). Se guarda en WebP: 1600 px y 640 px por el lado mayor, sin agrandar, enderezada y sin metadatos. `400` si no es una imagen valida, `413` si supera 10 MB |
| `GET /imagenes/{archivo}` | Publico | `{clave}-grande.webp` o `{clave}-miniatura.webp`. `Cache-Control: public, max-age=31536000, immutable` y `ETag`. `404` para cualquier otro nombre |

Al crear una solicitud, la API escribe en la cola de correo, **en la misma transaccion** que la solicitud (fase 7):

- En linea (`POST /mis/solicitudes`): el aviso al taller y la confirmacion al cliente, con su comprobante.
- Presencial (`POST /solicitudes`): solo la confirmacion al cliente, si su correo esta confirmado. **No avisa al taller** (decision de la fase 4, confirmada el 14-11-2026).

Si la cola no puede guardar alguno de esos correos, no se guarda la solicitud y la respuesta es un error, no `201`. Las solicitudes que frena la regla contra solicitudes falsas no generan correos. El envio real lo hace la cola en segundo plano, asi que un fallo del servidor de correo no impide guardar la solicitud. Tope: 20 avisos por hora entre solicitudes y consultas de contacto (`Correo:TopeAvisosPorHora`); superado el tope, la solicitud se guarda igual y su aviso se resume en uno solo.

### Portada publica (fase 5)

Todas son publicas (sin token). Los envios que guardan algo tienen sus propios limites, descritos en la seccion 3.

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /local/estado` | Publico | `{ abiertoAhora, motivo, dentroDeHorario, horaChile, hoy, proximaApertura, cartel, semana }`. `motivo`: `abierto`; `ausente` (en horario, pero el cartel dice cerrado: se muestra su mensaje); `fuera_de_horario`, o `cerrado_hoy` si ese dia no se atiende. Se calcula con la hora de Chile (America/Santiago), y la hora de cierre ya cuenta como cerrado. `cartel` es `null` si no hay cartel cargado |
| `GET /documentos-legales` | Publico | `[{ tipo, version }]` para `privacidad`, `terminos`, `marketing` y `cookies`. Hoy todas estan en `1.0`, que son borradores pendientes de revision legal |
| `POST /contacto` | Publico | `{ nombre, correo, telefono?, asunto, mensaje, aceptaPrivacidad, versionPrivacidad, sitioWeb? }`. Nombre hasta 100 caracteres, asunto hasta 150 y mensaje entre 10 y 2000. `telefono` se guarda como `+56` y 9 digitos. `versionPrivacidad` debe ser la vigente. Responde `202` con `{ recibida, mensaje }`. `400` con el motivo y los errores por campo; `409` si la version de la privacidad ya no es la vigente; `413` si supera 16 KB; `429` si supera 5 por hora por IP. `sitioWeb` es un campo trampa: si llega con texto, responde igual `202`, pero no guarda ni avisa nada. La misma consulta (mismo correo y mismo mensaje) dentro de la ultima hora no se guarda ni avisa de nuevo. La consulta y el consentimiento de privacidad se guardan en una transaccion, y el aviso al taller queda en la cola de correo (`consulta_contacto`, con respuesta dirigida a quien escribio) |
| `POST /consentimientos/cookies` | Publico | `{ visitante, version, acepta? }`. `visitante` es un GUID anonimo que genera el navegador. Sin `acepta`, o con `true`, registra la aceptacion de la version vigente de cookies (`204`). Con `acepta: false` registra el retiro (`204`), que no hace nada si nunca se acepto. `400` si falta `visitante`; `409` si la version no es la vigente; `429` si supera 5 por hora por IP |

### Solicitudes (fase 7)

Una solicitud tiene una cabecera (`descripcionGeneral`, obligatoria, de 10 a 2000 caracteres) y de 1 a 10 ítems (`detalles`). Cada ítem es `{ idProducto, descripcion?, anchoMm?, altoMm?, cantidad }`: el ancho y el alto van juntos o no van (de 1 a 50 000 mm), la cantidad va de 1 a 999 y la descripción del ítem admite hasta 500 caracteres. El producto debe estar activo. El área de cada ítem (`areaM2` = ancho × alto / 1 000 000 × cantidad, redondeada a 2 decimales) y el total (`areaTotalM2`) los calcula el servidor. Una solicitud admite hasta 5 adjuntos (ver «Adjuntos (fase 7)»).

La ruta pública `POST /solicitudes/online` **se eliminó** en la fase 7. Una solicitud en línea solo la envía un cliente con sesión y correo confirmado (`POST /mis/solicitudes`).

Estados: `solicitado`, `cotizado`, `aceptado` y `rechazado`. Quién cambia cada uno y cómo:

| Desde | Hacia | Quién |
| --- | --- | --- |
| `solicitado` | `cotizado` | El sistema, al emitir una cotización (fase 8) |
| `cotizado` | `solicitado` | El cliente, al rechazar la cotización; el personal, al devolverla («Volver a solicitado»); el sistema, al editar la solicitud |
| `cotizado` | `aceptado` | El sistema, al aprobar la cotización (fase 8) |
| `solicitado` o `cotizado` | `rechazado` | El personal, con motivo de 10 a 500 caracteres |

Cualquier otro cambio responde `409` con `transicion_invalida` y una explicación. Cada cambio queda en el historial de la solicitud, con quién lo hizo y cuándo.

**Comprobante.** Es el mismo en la pantalla del cliente, en su correo de confirmación y en el registro presencial. Las fechas van en hora de Chile (`fechaChile`, sin zona):

```
{ idSolicitud, numero: "#123", fechaChile: "2026-11-14T10:32:00", origen: "online" | "presencial", estado,
  motivoRechazo, cliente: { nombre, apellido, telefono, correo },
  detalles: [{ idProducto, producto, descripcion, anchoMm, altoMm, cantidad, areaM2 }], areaTotalM2,
  descripcionGeneral,
  adjuntos: [{ idAdjunto, tipo, nombre, bytes, ancho, alto, urlMiniatura, urlArchivo }],
  rowVersion }
```

La ficha del personal (`GET /solicitudes/{id}`) añade `idCliente`, `registradaPor` (quién la registró, o `null` si la envió el cliente) y `historial`: `[{ estadoAnterior, estadoNuevo, motivo, fechaChile, actor: "personal" | "cliente" | "sistema", por }]`. Fase 8: también `cotizaciones`: `[{ idCotizacion, numero, fechaChile, total, estado, idPedido, pedidoEliminado }]`, sin las eliminadas y de la más nueva a la más antigua (`idPedido` es el pedido que dio, aunque después se haya eliminado).

**Regla contra solicitudes falsas (RFS04, RNFP33).** Antes de guardar una solicitud en línea, la API la frena si se cumple cualquiera de estas condiciones:

- la cuenta tiene otra solicitud en línea pendiente (`solicitado` o `cotizado`, no eliminada);
- otra solicitud pendiente tiene el mismo RUT del cliente;
- la IP tiene una solicitud en línea pendiente;
- la IP ya envió 3 solicitudes en línea en la última hora (configurable).

Cuando frena, **no se escribe nada** (ni la solicitud, ni los adjuntos vinculados, ni los correos) y responde `409` con `codigo: "contacto_directo"` y el mismo mensaje para todos los motivos, el texto de S25: «Para esta solicitud es preferible contactarse directamente con el local». Ese mensaje no revela de quién es la otra solicitud. La IP nunca se guarda en claro: se guarda su HMAC-SHA256 con una clave del servidor (`solicitudes.huella_ip`). En IPv6 se usa el prefijo /64. Las solicitudes presenciales no pasan por esta regla.

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /solicitudes` | Personal | Paginada. Filtros: `?estado=` (`solicitado`, `cotizado`, `aceptado`, `rechazado`), `?origen=` (`online` o `presencial`), `?desde=` y `?hasta=` (fechas de Chile, incluidas), `?q=` (busca por número, nombre, teléfono o RUT), `?orden=` (`fecha_desc`, por defecto, o `fecha_asc`), `?pagina=` (1) y `?tamano=` (20, máximo 100). Responde `{ elementos, total, pagina, tamano, totalPaginas }`. Cada fila trae `numero`, `fechaChile`, `cliente` (`idCliente`, `nombre`, `apellido`, `telefono`), `origen`, `estado`, `cantidadItems`, `cantidadAdjuntos` y `registradaPor`. Un filtro con un valor desconocido responde `400`. No incluye las eliminadas |
| `GET /solicitudes/{id}` | Personal | La ficha. Trae la cabecera `ETag` con la version. `404` si no existe o esta eliminada |
| `POST /solicitudes` | Personal | Registro presencial: `{ idCliente, descripcionGeneral, detalles }`. El origen es `presencial` y lo registra quien tiene sesión (nunca viene en el cuerpo). Responde `201` con la ficha. `400` si el cliente no existe o esta desactivado. No pasa por la regla contra solicitudes falsas ni avisa al taller |
| `PUT /solicitudes/{id}` | Personal | `{ descripcionGeneral, detalles? }`. Solo en `solicitado` o `cotizado`; `409` con `transicion_invalida` en otro caso. Con `detalles`, reemplaza los ítems. Editar una solicitud `cotizado` deja su cotización pendiente como `rechazada` y la solicitud vuelve a `solicitado`. La version va en `If-Match` (o en `rowVersion`). Si otra persona la cambio, `409` con `version_obsoleta`. Responde la ficha |
| `PATCH /solicitudes/{id}/estado` | Todo el personal | `{ estado, motivo? }` con `If-Match`. `rechazado` exige `motivo` (10 a 500 caracteres). `solicitado` solo sirve para devolver una `cotizado`. `cotizado` y `aceptado` los pone el sistema (fase 8): otro intento responde `409`. Responde la ficha |
| `PATCH /solicitudes/{id}/eliminar-logico` | Todo el personal | Sin cuerpo, con `If-Match`. Solo en `solicitado` o `rechazado`, y sin pedido. La solicitud no se borra de la base. Responde `200` con `{ idSolicitud, eliminadoLogico: true }` |
| `DELETE /solicitudes/{id}` | Gestión | El mismo borrado lógico y las mismas reglas, con `If-Match`. Responde `204`. La API no tiene borrado físico: solo lo hace el procedimiento `sp_depurar_solicitudes_no_concretadas` (rechazadas o eliminadas, sin pedido, de más de 12 meses) |
| `POST /solicitudes/{id}/adjuntos` | Personal | Multipart con el campo `archivo`. Sube y vincula de inmediato, con el mismo tope de 5. Responde `201` con el adjunto. `409` con `tope_adjuntos` si ya hay 5, o `409` si la solicitud no esta en `solicitado` o `cotizado`. Ver «Adjuntos (fase 7)» |
| `DELETE /solicitudes/{id}/adjuntos/{idAdjunto}` | Personal | `204`. Solo en `solicitado` o `cotizado`. Queda anotado en el historial, con la version nueva. `404` si el adjunto no es de esa solicitud |
| `GET /metricas/conversion-online` | Gestión | Por año y mes: `totalSolicitudesOnline`, `solicitudesConcretadasEnPedido`, `porcentajeConversion` |

### Portal del cliente (fase 7)

Todas las rutas `/mis` salen del token: el cliente nunca viene en la ruta ni en el cuerpo. Las de envió exigen **correo confirmado**.

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `POST /mis/solicitudes` | `cliente` con correo confirmado | `{ descripcionGeneral, detalles, adjuntos? }`, donde `adjuntos` son los ids de sus borradores. Responde `201` con el comprobante y la cabecera `Location`. `403` con `correo_sin_confirmar` si el correo no esta confirmado. `409` con `contacto_directo` si la regla contra solicitudes falsas la frena (no se escribe nada). `400` con errores por campo (`descripcionGeneral`, `detalles[0].anchoMm`, `adjuntos`...). `503` con `servicio_ocupado` si no consigue el turno de la regla. Guarda la solicitud, vincula los adjuntos y deja en la cola los dos correos, todo en una transacción |
| `GET /mis/solicitudes` | `cliente` | Paginada (`?pagina=`, `?tamano=`). Solo las suyas, sin las eliminadas, de la más reciente a la más antigua. Cada fila trae `numero`, `fechaChile`, `origen`, `estado`, `motivoRechazo`, `resumen` (los primeros 160 caracteres de la descripción), `cantidadItems` y `cantidadAdjuntos`. Fase 8 (S04): ademas `cotizacion` (`null` o `{ idCotizacion, numero, total, estado }`, la ultima no eliminada) y `pedido` (`null` o `{ idPedido, numero, estadoPedido, estadoPago, saldoPendiente, fechaEntregaEstimada, modalidadEntrega, cicloCerrado, eliminado }`; un pedido anulado y eliminado por el taller se sigue mostrando, con `eliminado: true`) |
| `GET /mis/solicitudes/{id}` | `cliente` | El comprobante, con `cotizacion` y `pedido` como en la lista. `404` si no es suya, si no existe o si esta eliminada |
| `GET /mis/cotizaciones/{id}` | `cliente` | La cotizacion (S06, ver «Cotizaciones y pedidos (fase 8)») de una solicitud suya, con `ETag`. `elaboradaPor` trae solo `{ nombre }` con el nombre de pila (sin `idEmpleado`), igual `respuesta.registradaPor`. `404` si es ajena o esta eliminada |
| `POST /mis/cotizaciones/{id}/respuesta` | `cliente` | `{ decision: "aprobada" \| "rechazada", motivo? }` con `If-Match`. La via queda `cliente_web`. Responde `200` con la cotizacion. `409 transicion_invalida` si ya no esta pendiente; `409 version_obsoleta`; `400 sin_version`; `404` si es ajena |
| `GET /mis/pedidos` | `cliente` | Lista (no paginada) de sus pedidos, del mas reciente al mas antiguo: `[{ idPedido, idSolicitud, numero, estadoPedido, estadoPago, saldoPendiente, fechaEntregaEstimada, modalidadEntrega, cicloCerrado, eliminado }]`; los eliminados por el taller van con `eliminado: true`. Sin historial interno ni quien registro cada abono |
| `GET /mis/adjuntos` | `cliente` | Sus borradores, aún sin enviar, del más antiguo al más nuevo. Cada uno tiene la forma de la respuesta de la subida. Los usa el formulario para recuperar lo que el cliente subió antes y no envió |
| `POST /mis/adjuntos` | `cliente` con correo confirmado | Multipart con el campo `archivo`. Responde `201` con `{ idAdjunto, tipo, nombre, bytes, ancho, alto, urlMiniatura }`. `400` con `sin_archivo`, `formato_no_aceptado`, `archivo_muy_grande`, `imagen_invalida`. `409` con `tope_adjuntos` si ya tiene 5 borradores. `403` con `correo_sin_confirmar`. `429` si supera 10 subidas por minuto |
| `DELETE /mis/adjuntos/{id}` | `cliente` | `204`, solo para un borrador suyo. Si no, `404` |

### Adjuntos (fase 7)

Los diseños y logos que el cliente adjunta a su solicitud son **privados**: no forman parte de la vitrina ni tienen una ruta pública.

- **Formatos:** se reconocen por su contenido, no por el nombre ni por el tipo que declara el navegador. JPG, PNG y WebP se vuelven a codificar en WebP (1600 px, y una miniatura de 640 px), sin metadatos: se borra, por ejemplo, la ubicación GPS. Un PDF (empieza con `%PDF-`) se guarda tal cual. Lo demas, incluidos SVG y AI, responde `400` con `formato_no_aceptado` y el mensaje «Formato no aceptado. Sube JPG, PNG, WebP o PDF; si tu diseño es SVG o AI, expórtalo a PDF o PNG.».
- **Limites:** 10 MB por archivo y 5 por solicitud. Un borrador cuenta hasta que se envía la solicitud. Un borrador que pasa 24 horas sin enviarse se borra junto con su archivo (`PurgaDatosPersonales`, una vez al día).
- **Subida:** `multipart/form-data` con el campo `archivo`. Un archivo de más de 10 MB responde `400` con `archivo_muy_grande`, no `413`.
- **Ver un adjunto:** `GET /api/v1/adjuntos/{id}/miniatura` (la miniatura en WebP; para un PDF responde `404`) y `GET /api/v1/adjuntos/{id}/archivo` (la imagen grande en WebP, o el PDF tal cual). Requieren sesión. Los ve el cliente dueño (lo subió el, o es de una solicitud suya que no este eliminada) y cualquier empleado; a los demas se les responde `404`. Las respuestas traen `Cache-Control: private, no-store` y `X-Content-Type-Options: nosniff`. El PDF se descarga como adjunto (`Content-Disposition: attachment`) con un nombre seguro. El frontend las pide con el token y las muestra con una URL local (`URL.createObjectURL`).
- **Nombres:** el nombre original se guarda saneado (sin rutas ni caracteres de control, de hasta 120 caracteres) y solo se muestra. En disco, cada archivo tiene un nombre aleatorio de 128 bits, que nunca se muestra. Se guardan en `Adjuntos:Carpeta` (`almacen/adjuntos`, fuera del repositorio).
- **Correos:** los archivos no viajan en los correos. El aviso al taller dice cuantos hay, y se ven en el panel.

### Cotizaciones y pedidos (fase 8)

Todo el personal cotiza, registra la respuesta del cliente, crea pedidos, los avanza y registra abonos (RFU12-RFU14). Las rutas antiguas `POST /cotizaciones` y `POST /pedidos`, el `DELETE` fisico y las respuestas con la entidad de EF **se retiraron**.

**Montos.** Pesos chilenos con IVA incluido. Mano de obra, otros costos, costo unitario, costo de instalacion, monto base y abonos van **sin decimales** (`400` si no). La cantidad de un material admite 2 decimales, asi que su `subtotal` y el `total` de la cotizacion pueden tener centavos; `totalRedondeado` es el total al peso (mitad hacia arriba) y es el monto base del pedido. **Fechas** sin hora (`fechaEntregaEstimada`, `fechaEntregaReal`, `fechaPago`, `fecha` del calendario) van como `"aaaa-mm-dd"`; `fechaChile`, `fechaPedido`, `fechaRegistroChile` y `fechaAnulacionChile` van en hora de Chile, sin zona. «Hoy» es la fecha de Chile. Un monto obligatorio que **no viene** (`costoManoObra`, `otrosCostos`, `costoUnitario`, `cantidad`, `costoInstalacionAdicional`, `montoBaseTrabajo`) responde `400` con el error en su campo; nunca se toma como 0. Los ids de las rutas son numeros (`{id:int}`): otro texto responde `404`.

**Estados del pedido** (independiente del pago, que calcula la base: `no_pagado`, `abonado`, `pagado_total`):

| Desde | Hacia | Notas |
| --- | --- | --- |
| `aceptado` | `en_proceso` | |
| `en_proceso` | `terminado` | |
| `terminado` | `entregado` | `fechaEntregaReal` (por defecto hoy): no futura ni anterior a la fecha del pedido |
| `en_proceso` | `aceptado` | vuelta atras |
| `terminado` | `en_proceso` | vuelta atras |
| `aceptado`, `en_proceso` o `terminado` | `anulado` | `motivo` de 10 a 500. Si el pedido tiene o tuvo abonos (vigentes **o anulados**), solo gestion (`403 solo_gestion`) |

`entregado` y `anulado` no cambian mas. Otro cambio responde `409 transicion_invalida` con la explicacion. Cada cambio queda en el historial del pedido. `cicloCerrado` = `entregado` y `pagado_total` (se calcula).

**Codigos de los 409:** `transicion_invalida`, `version_obsoleta`, `cotizacion_pendiente` (la solicitud ya tiene una pendiente), `cotizacion_con_pedido` (la cotizacion ya tiene pedido), `abono_excede_saldo` («El abono supera el saldo pendiente ($X).»), `pedido_anulado` («Un pedido anulado no recibe abonos.»), `monto_bajo_abonado` («El monto final no puede quedar bajo lo ya abonado ($X).»), `abono_anulado` (ya estaba anulado; tambien si se intenta reactivar uno en la base), `tiene_abonos` (eliminar un pedido con abonos vigentes), `factura_distinta` («El pedido ya tiene la factura N; cámbiala editando el pedido.»). Si la regla la hizo cumplir la base (indice unico o `ck_pedidos_monto_abonado`), el mismo codigo llega con `nivel: "base_de_datos"`. `403 solo_gestion` cuando la accion es de gestion por las reglas del negocio (no por la ruta).

**Cotizacion (S06):**

```
{ idCotizacion, numero: "#45", idSolicitud, numeroSolicitud: "#123", fechaChile, estado: "pendiente" | "aprobada" | "rechazada",
  cliente: { idCliente, nombre, apellido, telefono, correo },
  items: [{ producto, descripcion, anchoMm, altoMm, cantidad, areaM2 }],
  materiales: [{ idMaterial, material, unidad, medidaAplicada, cantidad, costoUnitario, subtotal }],
  costoMaterialesTotal, costoManoObra, otrosCostos, total, totalRedondeado,
  observaciones, elaboradaPor: { idEmpleado, nombre },
  respuesta: null | { fechaChile, via: "cliente_web" | "telefono" | "whatsapp" | "presencial", motivo, registradaPor: { nombre } | null },
  idPedido: null | number, pedidoEliminado, rowVersion }
```

`idPedido` es el pedido que dio la cotizacion, aunque despues se haya eliminado (`pedidoEliminado: true`): una cotizacion da a lo mas un pedido, asi que con `idPedido` la pantalla no ofrece «Crear pedido».

**Ficha del pedido (S07):**

```
{ idPedido, numero, origen: "cotizacion" | "directo", solicitud: { idSolicitud, numero }, cotizacion: { idCotizacion, numero } | null,
  cliente: { idCliente, nombre, apellido, telefono, correo }, responsable: { idEmpleado, nombre },
  fechaPedido, fechaEntregaEstimada, fechaEntregaReal, modalidadEntrega, requiereInstalacion, direccionInstalacion,
  montoBaseTrabajo, costoInstalacionAdicional, montoTotalFinal, estadoPedido, estadoPago, montoAbonado, saldoPendiente,
  numeroFactura, cicloCerrado, motivoAnulacion, aFavorDelCliente, transicionesPosibles: ["en_proceso", "anulado", …],
  historial: [{ fechaChile, estadoAnterior, estadoNuevo, nota, empleado }],
  pagos: [{ idPago, monto, fechaPago, medioPago, observacion, registradoPor, fechaRegistroChile,
            anulado, motivoAnulacion, anuladoPor, fechaAnulacionChile, puedeAnular }], rowVersion }
```

`transicionesPosibles` y `puedeAnular` dependen de quien mira: sin ser de gestion, un pedido que tiene o tuvo abonos no ofrece `anulado`, y solo se anulan los abonos propios de las ultimas 24 horas. `aFavorDelCliente` es lo abonado vigente de un pedido `anulado` (0 en los demas): se muestra como «A devolver o a favor del cliente: $X».

**Datos del pedido (E08):** `idEmpleadoResponsable` (activo); `fechaEntregaEstimada` (desde hoy y hasta 2 años; al editar se acepta la que ya tenia); `modalidadEntrega` (`retiro_local`, `entrega_domicilio`, `instalacion_terreno`); `requiereInstalacion` (debe ser `true` con `instalacion_terreno`); `direccionInstalacion` (hasta 300, obligatoria salvo `retiro_local`, donde se guarda `null`); `costoInstalacionAdicional` (entero, 0 sin instalacion); `numeroFactura?` (hasta 30). **Abono (E25):** `{ monto, fechaPago?, medioPago, observacion?, numeroFactura? }`: monto entero mayor que 0; fecha no futura ni de hace mas de 365 dias (por defecto hoy); medio `efectivo`, `transferencia`, `tarjeta_debito`, `tarjeta_credito`, `cheque` u `otro`; observacion hasta 300; si trae `numeroFactura` y el pedido no tiene factura, se la asigna; si tiene la misma, nada; si tiene otra, `409 factura_distinta` (se cambia editando el pedido). Los errores del abono inicial van con el prefijo `abonoInicial.` (`abonoInicial.monto`...).

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `POST /solicitudes/{id}/cotizaciones` | Personal | `{ materiales: [{ idMaterial, medidaAplicada?, cantidad, costoUnitario }], costoManoObra, otrosCostos, observaciones? }`. De 0 a 30 materiales activos; medida hasta 100; cantidad mayor que 0 y hasta 100 000 con 2 decimales; costos de 0 a 100 000 000; observaciones hasta 2000 (las ve el cliente); total mayor que 0 y hasta 2 000 000 000. Solo sobre una solicitud `solicitado`: `409 cotizacion_pendiente` si ya tiene una pendiente, `409 transicion_invalida` en otro estado, `404` si no existe o esta eliminada. En una transaccion: la cotizacion, la solicitud a `cotizado` con su historial y, si el cliente tiene el **correo confirmado**, el correo «Tu cotización #N está lista» en la cola (boton «Ver cotización» a `{Sitio:UrlPublica}/cuenta/solicitudes/{idSolicitud}`). Responde `201` con la cotizacion y su `ETag` |
| `GET /cotizaciones` | Personal | Paginada. `?estado=`, `?desde=`, `?hasta=` (fechas de Chile, incluidas), `?idCliente=`, `?q=` (numero de cotizacion o de solicitud, nombre, telefono o RUT), `?orden=` (`fecha_desc` por defecto, `fecha_asc`, `total_desc`), `?pagina=`, `?tamano=`. Filas: `{ idCotizacion, numero, idSolicitud, fechaChile, cliente: { idCliente, nombre, apellido, telefono }, total, estado, elaboradaPor: { idEmpleado, nombre } }`. Sin las eliminadas. Un filtro desconocido responde `400` |
| `GET /cotizaciones/{id}` | Personal | La cotizacion con su `ETag` |
| `PUT /cotizaciones/{id}` | Personal | El mismo cuerpo que al emitir, con `If-Match`. Solo `pendiente` (`409 transicion_invalida` si no). Reemplaza las lineas: sin `materiales` (o con `[]`) quedan sin materiales |
| `POST /cotizaciones/{id}/respuesta` | Personal | `{ decision: "aprobada" \| "rechazada", via: "telefono" \| "whatsapp" \| "presencial", motivo? }` con `If-Match`. Queda quien la marco. Aprobar deja la solicitud `aceptado`; rechazar la devuelve a `solicitado` con la nota «Cotización #N rechazada: motivo». Responde `200` con la cotizacion |
| `DELETE /cotizaciones/{id}` | Gestion | Con `If-Match`. Borrado logico de una `pendiente` o `rechazada` sin pedido (`409` si no). Si estaba pendiente, la solicitud vuelve a `solicitado`. `204` |
| `POST /cotizaciones/{id}/pedido` | Personal | Datos del pedido y `abonoInicial?`. Solo de una cotizacion `aprobada` cuya solicitud siga `aceptado` y no este eliminada (`409 transicion_invalida`), y sin pedido (`409 cotizacion_con_pedido`; si el que tuvo se elimino: «Esta cotización ya tuvo el pedido #N (eliminado).»). Nace `aceptado` con el total redondeado como monto base. El abono inicial no puede superar el monto final (`409 abono_excede_saldo`). Todo en una transaccion. `201` con la ficha |
| `POST /pedidos/directo` | Personal | `{ idCliente, descripcionGeneral, detalles: [como en la fase 7], materiales, costoManoObra, otrosCostos, observaciones?, …datos del pedido…, abonoInicial? }`. El cliente se busca o se crea antes (`GET /clientes/buscar`, `POST /clientes`). En una transaccion: la solicitud presencial ya `aceptado` (nota «Pedido directo en el taller»), la cotizacion `aprobada` en el local por quien la registra, el pedido con origen `directo` y el abono. Si algo falla no se escribe nada. **No envia correos**. `201` con la ficha |
| `GET /pedidos` | Personal | Paginada. `?estadoPedido=`, `?estadoPago=`, `?cobranza=true` (no anulado y con pago `no_pagado` o `abonado`), `?anio=` y `?mes=` (por la fecha del pedido, como rango de fechas; `mes` sin `anio` filtra ese mes de cualquier año, sin rango posible), `?idCliente=`, `?idResponsable=`, `?q=` (numero de pedido, nombre, telefono o RUT), `?orden=` (`fecha_desc` por defecto, `fecha_asc`, `entrega_asc`, `saldo_desc`), `?pagina=`, `?tamano=`. Responde `{ elementos, total, pagina, tamano, totalPaginas, resumen: { totalPorCobrar, cantidadPorCobrar } }`; el resumen suma el saldo de los pedidos **por cobrar de todo el filtro** (no solo de la pagina; los anulados nunca cuentan). Filas: `{ idPedido, numero, origen, fechaPedido, cliente: { idCliente, nombre, apellido, telefono }, responsable: { idEmpleado, nombre }, fechaEntregaEstimada, fechaEntregaReal, modalidadEntrega, estadoPedido, estadoPago, montoTotalFinal, montoAbonado, saldoPendiente, cicloCerrado }`. Sin los eliminados |
| `GET /pedidos/{id}` | Personal | La ficha con su `ETag`. `404` si no existe o esta eliminado |
| `PUT /pedidos/{id}` | Personal | El cuerpo completo: datos del pedido y `montoBaseTrabajo` (entero), con `If-Match`. Un pedido `anulado` no se edita (`409`); en uno `entregado` solo cuenta `numeroFactura` (lo demas se ignora, aunque no sea valido). La `fechaEntregaEstimada` que ya tenia se acepta aunque haya pasado. **Cambiar `montoBaseTrabajo` o `costoInstalacionAdicional` es de gestion** (`403 solo_gestion` si otro valor llega de alguien mas) y deja una fila en el historial con el mismo estado y la nota «Monto final: $100.000 → $90.000 (monto base $100.000 → $90.000).». `409 monto_bajo_abonado` si el monto final queda bajo lo abonado. `200` con la ficha |
| `PATCH /pedidos/{id}/estado` | Personal | `{ estado, fechaEntregaReal?, motivo?, nota? }` con `If-Match` (nota hasta 500). `200` con la ficha |
| `POST /pedidos/{id}/pagos` | Personal | El abono, sin version: bloquea la fila del pedido, asi dos abonos a la vez se registran uno tras otro. `409 abono_excede_saldo` o `409 pedido_anulado`. `201` con la ficha (y el `ETag` nuevo) |
| `POST /pedidos/{id}/pagos/{idPago}/anular` | Personal | `{ motivo }` de 10 a 300. Gestion anula cualquiera; el resto, solo los que registro en las ultimas 24 horas (`403 solo_gestion`). `409 abono_anulado` si ya estaba anulado (no se reactiva). El abono deja de sumar y queda en el historial con quien y cuando. `200` con la ficha |
| `DELETE /pedidos/{id}` | Gestion | Con `If-Match`. Borrado logico de un pedido `anulado` sin abonos vigentes (`409 transicion_invalida` o `409 tiene_abonos`). `204` |
| `GET /pedidos/calendario` | Personal | `?desde=aaaa-mm-dd&hasta=aaaa-mm-dd`, como mucho 62 dias contando ambos (`400` si no, tambien con `hasta` = 9999-12-31). Pedidos no anulados ni eliminados: los pendientes en su fecha estimada y los entregados en su fecha real, ordenados por fecha. Cada uno: `{ idPedido, numero, fecha, cliente: { idCliente, nombre, apellido, telefono }, modalidadEntrega, estadoPedido, estadoPago, atrasado }` (`atrasado`: sin entregar y con la fecha estimada ya pasada) |
| `GET /pedidos/alertas` | Gestion | `{ proximas: [de hoy a hoy + 3], atrasadas: [estimada antes de hoy, sin entregar ni anular], totalPorCobrar, cantidadPorCobrar }`. Las listas tienen la forma del calendario, como mucho 20 cada una; el total por cobrar es de todos los pedidos |

## 5. Cambios que necesita el frontend actual

Rutas relativas a `frontend/synctimefront`. Los tests de `src/test` y `e2e` simulan el contrato antiguo, asi que pueden pasar sin que la pantalla funcione con la API real: hay que actualizarlos junto con cada cambio y probar contra la API.

| Archivo | Cambio |
| --- | --- |
| `src/pages/SolicitudesPage.jsx` y demas usos de `hasRole` o `RoleGuard` | Usar `dueño`, `diseñador` y `administrador`, en vez de `dueno`, `disenador` y `admin`. Hoy los botones de gestion quedan ocultos para los roles reales |
| `src/api/httpClient.js` | Actualizar los tipos, que describen campos que ya no existen, como `idUsuario`, `tamano`, `canalPreferido` y `unidadMedida`. Leer `data.error` ya esta hecho, porque el login facial lo necesitaba |
| `src/pages/SolicitudForm.jsx` | Enviar `{ idCliente, descripcionGeneral?, detalles: [...] }`. `idProducto`, `cantidad`, `tamano`, `materialSolicitado`, `telefonoContacto` y `notasWhatsapp` ya no existen. Hoy responde `400` |
| `src/pages/SolicitudForm.jsx` | `precioBase` pasa a ser `precioBaseRef` |
| `src/pages/ProductosPage.jsx` | Ya adaptada: `categoria` llega como objeto (se muestra `categoria.nombre`) y el precio es `precioBaseRef`. Antes pintaba el objeto y la pantalla quedaba en blanco. Cubierta por `src/test/ProductosPage.test.jsx`, con la forma real de la API |
| `src/App.jsx` | No hay un error boundary: si cualquier pantalla falla al dibujarse, React desmonta toda la aplicacion y queda en blanco, tambien el menu. Conviene agregar uno antes de adaptar el resto |
| `src/api/solicitudService.js` | `update` envia `{ descripcionGeneral, rowVersion }`; `cambiarEstado` envia `{ estado, motivoRechazo?, rowVersion }` |
| `src/pages/SolicitudesPage.jsx` | Conservar `rowVersion`, avisar del `409` y volver a cargar. Quitar `fechaDecision`, que ya no existe |
| Pantallas de administracion de productos | Fase 9: `?incluirInactivos=` ya no existe; gestión recibe todo por defecto y filtra con `?activo=` |
| `src/api/solicitudes.js` (`clientes.actualizar`) | Fase 9: `PUT /clientes/{id}` exige `If-Match` con el `rowVersion` de la ficha |

## 6. Cargar el catalogo real

Las categorias, productos, materiales y horarios actuales son datos de ejemplo del script de referencia.

1. Cambiar los `HasData` de `LetrerosHerreradbContext.cs`, o cargar los datos por la API si los va a mantener el propio cliente. Conviene decidirlo con los requisitos: lo que este en `HasData` queda fijado en el codigo.
2. Si se usa `HasData`, generar una migracion nueva con `dotnet ef migrations add`. Nunca editar una migracion ya aplicada.
3. Actualizar las pruebas que dependen de los datos de ejemplo. `ApiFixture` y otras usan `idProducto = 1` e `idMaterial = 1`, y `MigracionTests` comprueba valores concretos.

Si los productos reales necesitan campos nuevos, se agregan al modelo con su migracion.

## 7. Login con reconocimiento facial

Fase 10 (15-11-2026). Solo para `dueño` y `administrador`; los demas roles entran con contraseña. Email y contraseña siguen igual y siempre visibles. Los clientes no tienen rutas ni boton.

### Lo que protege y lo que no

`face-api` corre en el navegador y produce, por cada captura, un descriptor (128 numeros) y 68 puntos del rostro. Desde la fase 10 hay **prueba de vida con giros de cabeza**: el servidor elige al azar el orden de los giros, y el navegador debe enviar tres capturas (de frente, girando hacia un lado y hacia el otro) en ese orden. Esto frena una foto o una pantalla sostenida frente a la camara y sube mucho el costo de suplantar a alguien.

**No es infalible:** los descriptores y los puntos se calculan en el navegador, asi que un cliente modificado podria falsificarlos. Por eso se mantienen el **equipo autorizado** (dueño y administrador entran con rostro solo desde su equipo, 30 dias, `RostroFacial:DiasEquipo`), el bloqueo y la contraseña. Conviene autorizar solo equipos no compartidos.

Una sesion iniciada con rostro es igual a una normal (12 horas, con renovacion, `Sesion:HorasPersonal`).

### Flujos

**Registrar**, desde Mi perfil: leer y aceptar el consentimiento, escribir la contraseña y seguir el flujo guiado. Se toman tres muestras (frente, izquierda y derecha), con vista previa local de cada una y opcion de repetirla; la imagen no sale del navegador. Devuelve `tokenEquipo`, que el navegador guarda como `synctime_equipo:<email>`. Volver a registrar reemplaza el rostro. Un rostro anterior a la fase 10 (un solo promedio) queda como «debe registrarse de nuevo» y no sirve para entrar.

**Registrar a otro** («Rostros registrados», `/panel/rostros`): gestion inicia el registro de otro empleado de gestion; el titular, presente, acepta el consentimiento y escribe **su propia** contraseña. No se entrega token de equipo: el titular autoriza su equipo desde su perfil.

**Entrar:** escribir el email y pulsar «Entrar con rostro». La camara se enciende recien ahi; el navegador pide el desafio y guia el encuadre y los giros (flecha, progreso, avance automatico, unos 4 s por giro con un reintento). El flujo completo dura de 6 a 9 s. Al entrar se muestra «Entraste como {nombre} ({rol})». Si el servidor rechaza, el mensaje es siempre el mismo; tras 3 rechazos seguidos el foco pasa a la contraseña.

**Desactivar**, desde Mi perfil o desde «Rostros registrados»: se borran las muestras y el token del equipo. Quedan las fechas y la version del consentimiento, como constancia.

### Reglas del servidor

- **Desafio:** 32 bytes al azar; se guarda solo su hash (`desafios_rostro`), atado al hash del email. Vence a los `RostroFacial:SegundosDesafio` (30) y se marca usado en una sola sentencia antes de comparar, asi que no se puede usar dos veces, ni con envios simultaneos. La purga diaria borra los de mas de un dia.
- **Giro:** se calcula en el servidor con los puntos, en coordenadas sin espejo: `r = (x[30] - x[0]) / (x[16] - x[0])`, `g = (r - 0.5) x 2`, limitado a [-1, 1]; `g > 0` es un giro hacia la izquierda de la persona. La captura de frente debe tener `|g| <= FrenteMaximo` (0.15), y cada giro, el signo que pide el orden y `|g|` entre `GiroMinimo` (0.30) y `GiroMaximo` (0.75). El mismo calculo esta en `Negocio/Rostro/Giro.cs` y en `src/rostro/giro.js`, probados con los mismos casos (`CasosGiro.json`).
- **Comparacion:** cada captura contra la muestra de su lado, con distancia maxima `RostroFacial:UmbralDistancia`, y las tres entre si, con `UmbralEntreCapturas` (0.6). Las comparaciones se hacen siempre, tambien contra un señuelo, para que todo fallo cueste lo mismo.
- **Umbral calibrado: 0.45**, fijado por el usuario el 15-11-2026 tras calibrar con su camara. En 7 intentos propios, la peor distancia fue de 0.24 a 0.31; en 10 de otra persona, de 0.62 a 0.70. Se puede recalibrar en `/panel/rostros/calibrar`, encendiendo `RostroFacial:Calibracion:Activa` solo mientras se mide (no en produccion). La pantalla sugiere un umbral, que se copia a mano a la configuracion.
- **Datos:** `rostros_empleado` guarda las tres muestras en `descriptor_frente`, `descriptor_izquierda` y `descriptor_derecha`, cifradas con AES-256-GCM y atadas al empleado **y a su lado** (datos asociados `modelo:idEmpleado:lado`), con `formato_muestras = 3` y quien inicio el registro. Intercambiar las muestras entre columnas o copiarlas a otro empleado impide descifrarlas. Un rostro de formato 2 (fase 10, antes de atar el lado) cuenta como «debe registrarse de nuevo». La clave esta en `RostroFacial:ClaveCifrado`. No se guardan fotos ni puntos, y no se registran descriptores, puntos, tokens ni desafios en los logs. La calibracion (`calibracion_rostro`) guarda solo distancias y giros.
- **Fallos:** el mismo `401` vacio, con un tiempo minimo de 400 ms, sea por el formato, el desafio, el rol, un empleado inactivo, un rostro sin las tres muestras, el equipo, el bloqueo, el giro o la coincidencia.
- **Bloqueo y limites:** 5 fallos bloquean el rostro de esa cuenta 15 minutos, sin afectar a la contraseña; sin un equipo valido el intento no se cuenta. El desafio tiene su propio limite, `DesafioFacial` (`RateLimiting:DesafioFacial`, 10 por minuto por IP), y el ingreso el suyo, `LoginFacial` (10 por minuto por IP): agotar uno no gasta el otro ni la contraseña. La contraseña del titular, al registrar a otro, tiene su propio contador.
- **Clave ausente o invalida:** la API arranca, el login con contraseña funciona y las rutas de rostro responden `503`. Si la clave cambia, cada empleado debe registrar su rostro de nuevo.
- **Empleados desactivados:** desactivar a un empleado (fase 9) borra su rostro y cierra sus sesiones en el mismo paso.

### Frontend

- La libreria es `@vladmandic/face-api` 1.7.15, en version exacta. **Su repositorio esta archivado** y su README indica que `@vladmandic/human` la reemplaza. Queda aislada en `src/rostro/motorFacial.js`; cambiarla exige tocar ese modulo y `RostroFacialService`, y que cada empleado registre su rostro de nuevo.
- La libreria (1,3 MB) y 3 modelos (6,7 MB, en `public/models`) se descargan solo al usar la funcion.
- El flujo guiado esta en `src/rostro/FlujoFacial.jsx`, con la logica sin camara en `src/rostro/guiaFacial.js`. El video se ve espejado solo con CSS. Con `prefers-reduced-motion` no hay animaciones.
- La politica de seguridad de `index.html` incluye `worker-src 'self' blob:` (fase 10), que el motor necesita para su *worker*. Sin WebGL usa la CPU, mas lenta.
- En produccion, la camara exige HTTPS, y la API debe agregarse a `connect-src`, que hoy solo admite `localhost`.

### Datos personales

Estado al 06-11-2026, segun el [texto vigente de la Ley 19.628 con las modificaciones de la Ley 21.719](https://www.bcn.cl/leychile/consulta/obtxml?opt=7&idNorma=141599&idVersion=2026-12-01):

- **Vigencia:** la [Ley 21.719](https://www.bcn.cl/leychile/consulta/obtxml?opt=7&idNorma=1209272) entra en vigencia el 1 de diciembre de 2026. El [Boletin 18.623-07](https://tramitacion.senado.cl/appsenado/templates/tramitacion/index.php?boletin_ini=18623-07) figura "En tramitacion" en el Senado. Segun [Garrigues](https://www.garrigues.com/es_ES/noticia/senado-chileno-aprueba-proyecto-ley-busca-postergar-15-octubre-2027-entrada-vigencia-nueva), una fuente secundaria, el Senado lo aprobo el 28 de octubre con fecha 15 de octubre de 2027 y paso a la Camara. Mientras no se publique, se trabaja con la fecha vigente.
- **Art. 16 ter:** los rasgos faciales son datos biometricos. Exigen consentimiento expreso e informar el sistema, la finalidad especifica, el periodo de uso y como ejercer los derechos. La pantalla de activacion lo hace.
- **Art. 12:** el consentimiento se puede revocar en cualquier momento y sin dar motivo, por medios gratuitos y siempre disponibles. Probar que existio le corresponde al responsable, y por eso se conserva la constancia.
- **Arts. 14 quinquies y 14 sexies:** medidas de seguridad, entre ellas el cifrado, y deber de reportar vulneraciones a la Agencia y, si son datos sensibles, a las personas afectadas.

**A confirmar con un asesor legal.** Esto no es asesoria legal, y el texto de `Services/ConsentimientoFacial.cs` es un borrador:

1. El art. 12 presume que el consentimiento no es libre si se pide dentro de un contrato en que esa recoleccion no es necesaria. ¿Basta con que sea opcional para los trabajadores, sin desventaja para quien use contraseña?
2. ¿Hace falta la evaluacion de impacto del art. 15 ter? Su lista obligatoria menciona datos sensibles solo sin consentimiento, pero la regla general alcanza a todo tratamiento de alto riesgo.
3. El texto final del consentimiento: responsable, contacto y periodo de uso.
4. ¿Cuanto tiempo conservar la constancia despues de revocar?
5. ¿Debe reflejarse en el contrato o en el reglamento interno?

## 8. Decisiones de negocio para confirmar con los requisitos

Son reglas que la API aplica hoy por decision tecnica, sin que las haya definido el cliente:

- Un `administrador` puede crear empleados con rol `dueño`. Hoy ambos tienen los mismos permisos.
- Todo el personal rechaza, devuelve a «solicitado» y elimina de forma lógica una solicitud (RFU11). `cotizado` y `aceptado` solo los pone el sistema, al cotizar y aprobar (fase 8). Solo `dueño` y `administrador` borran con `DELETE`.
- Un pedido exige una solicitud `aceptado`, y su cotizacion, si se indica, debe estar `aprobada`.
- Una solicitud en línea pasa por la regla contra solicitudes falsas (`409` con `contacto_directo`, en la sección 4). Solo la envía un cliente con correo confirmado.
- Una solicitud admite de 1 a 10 ítems, y hasta 5 adjuntos. Los adjuntos son privados y no se aceptan SVG ni AI (`docs/plan/desviaciones.md`).
- La regla contra solicitudes falsas cuenta como máximo 3 envíos en línea por IP en una hora (RNFP33), configurable con `Solicitudes:MaxEnLineaPorIp` y `Solicitudes:VentanaIpMinutos`. El 409 `contacto_directo` usa el texto de S25.
- Solo se pueden pedir productos activos.
- El login facial es solo para `dueño` y `administrador`, con un solo equipo autorizado por persona (seccion 7).
- Una cuenta web de cliente exige correo: el correo es el usuario para ingresar y para recuperar la contraseña. El RUT tambien es opcional en la cuenta (decision de la fase 6, en `docs/plan/desviaciones.md`).
- Registrarse con un telefono o un RUT que ya estan en el taller da el mismo mensaje, sin decir cual de los dos coincide. El correo si se revela («Ya existe una cuenta con ese correo»).
