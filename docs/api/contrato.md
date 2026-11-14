# Contrato de la API para el frontend

Este documento reune lo necesario para adaptar el frontend (`frontend/synctimefront`) cuando esten definidos los requisitos finales. **Estado al 13-11-2026:** la API esta lista y probada hasta la fase 6 (cuentas y sesion). En el frontend, el ingreso, el registro, la recuperacion y Mi cuenta usan este contrato desde la fase 6. El login facial de empleados se describe en la seccion 7.

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
| `POST /auth/login-facial` (personal) | `{ email, descriptor, tokenEquipo? }` | Lo mismo que `/auth/login`. Ver seccion 7 |

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
| `413` | El cuerpo supera el tope de su ruta: 8 KB en el login de rostro, 64 KB al activar el rostro, y 16 KB en `POST /contacto` y `POST /consentimientos/cookies` |
| `429` | Limite por IP por minuto: 10 logins con contraseña, 10 logins con rostro, 10 confirmaciones de contraseña, 10 envios publicos o 100 peticiones generales. Cada uno tiene su contador. `POST /contacto` y `POST /consentimientos/cookies` tienen además 5 envíos por hora por IP cada uno (`RateLimiting:ContactoPorHora` y `CookiesPorHora`). Ademas, 10 fallos por cuenta por minuto en el ingreso, la recuperacion y los cambios de correo o contraseña (`RateLimiting:PorCuenta`) |
| `503` | El login facial no esta disponible porque falta su clave de cifrado |

**Edicion simultanea.** Solicitudes y pedidos traen `rowVersion` en sus respuestas. Las rutas que los editan lo exigen:

1. Guardar el `rowVersion` que entrega el GET, junto con los datos del formulario.
2. Enviarlo sin cambios en el `PUT` o `PATCH`.
3. Si responde `409`, avisar que otra persona cambio el registro y volver a cargarlo. Si responde `200`, usar el `rowVersion` de esa respuesta para la siguiente edicion.

Si falta, responde `400`. Las eliminaciones no lo piden. Cotizaciones, clientes y productos no tienen version: ahi prevalece la ultima edicion.

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
| `POST /auth/empleados` | Gestion | `{ rut, nombre, apellido, telefono, email, password, rol, passwordActual }`. `password` es la del empleado nuevo; `passwordActual`, la de quien lo crea |
| `GET /auth/me` | Con sesion | Lista de claims del token |
| `POST /auth/login-facial` | Publico | `{ email, descriptor, tokenEquipo? }`. Todo fallo responde el mismo `401` vacio |
| `GET /auth/rostro` | Personal | `{ disponible, activo, fechaConsentimiento, requiereEquipo, equipoVence, equipoVigente, consentimiento: { version, texto } }` |
| `PUT /auth/rostro` | Personal | `{ password, consentimiento: true, versionConsentimiento, descriptores }`, con 3 a 10 capturas. Devuelve `{ activo, fechaConsentimiento, tokenEquipo, equipoVence }` |
| `POST /auth/rostro/equipo` | `dueño`, `administrador` | `{ password }`. Autoriza este equipo y anula el anterior. Devuelve `{ tokenEquipo, equipoVence }` |
| `DELETE /auth/rostro` | Personal | Borra los datos biometricos sin pedir contraseña. Responde `204`, tambien si no estaba activo |

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

**Correo y enlaces.** El correo es el usuario para ingresar y para recuperar la contraseña, así que una cuenta con contraseña siempre tiene correo. El cliente puede usar su cuenta sin confirmar el correo. Confirmarlo es necesario para las campañas (fase 11), y la fase 7 decidirá si las solicitudes en línea lo exigen (ver `docs/plan/desviaciones.md`). El enlace de confirmación dura 24 horas, sirve una sola vez y sale desde `Sitio:UrlPublica`.

### Clientes, personal y materiales

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /clientes`, `GET /clientes/{id}` | Personal | |
| `POST /clientes`, `PUT /clientes/{id}` | `dueño`, `administrador`, `operario` | `{ rut?, nombre, apellido, telefono, email?, direccion?, activo? }`. El correo se guarda en minusculas y sin espacios. Un cliente con contraseña necesita correo (`400`). Si el correo cambia, queda sin confirmar y se envia un enlace nuevo. Desactivarlo o cambiarle el correo cierra sus sesiones |
| `PUT /clientes/{id}/credenciales` | `dueño`, `administrador`, `operario` | `{ password, passwordActual, correo? }`. Activa el acceso web de un cliente que ya existe. Sin correo no hay cuenta: si el cliente no tiene uno guardado, hay que enviarlo en `correo`. `400` si el correo no sirve; `409` si el cliente esta desactivado o el correo ya es de otra cuenta. Cierra sus sesiones anteriores y envia el enlace para confirmar el correo si aun no esta confirmado. `password` es la del cliente; `passwordActual`, la de quien la asigna |
| `DELETE /clientes/{id}` | Gestion | `409` si tiene solicitudes |
| `GET /empleados`, `GET /materiales` | Personal | |

### Vitrina

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /categorias`, `GET /categorias/{id}` | Publico | `?incluirInactivos=true` para el personal |
| `POST /categorias`, `PUT /categorias/{id}` | Gestion | `{ nombre, descripcion?, activo? }` |
| `GET /productos`, `GET /productos/{id}` | Publico | `?incluirInactivos=true` para el personal. Incluye `categoria` |
| `POST /productos`, `PUT /productos/{id}` | Gestion | `{ idCategoria, nombre, descripcion?, precioBaseRef?, activo? }` |
| `GET /catalogo`, `GET /catalogo/{id}` | Publico | Paginado: `?categoria=`, `?pagina=` (1) y `?tamano=` (20, máximo 100). Responde `{ elementos, total, pagina, tamano, totalPaginas }`, ordenado por fecha de publicación, la más reciente primero. Cada trabajo trae `idCatalogo`, `titulo`, `descripcion`, `idProducto`, `tipoLetrero`, `categoria`, `anchoMm`, `altoMm`, `precioReferencial`, `imagen` (`urlGrande`, `urlMiniatura`, `ancho`, `alto`, `esEjemplo`, o `null`) y `fechaPublicacion`. Los visitantes no reciben `visible`, `incluyeLogoCliente` ni `logoAutorizado`. `?incluirOcultos=true` solo para el personal. `GET /catalogo/{id}` responde `404` si el trabajo no es visible para quien pide |
| `POST /catalogo`, `PUT /catalogo/{id}` | Gestion | `{ idProducto, titulo, descripcion, imagenUrl?, idImagen?, anchoMm?, altoMm?, precioReferencial?, visible?, incluyeLogoCliente? }`. `imagenUrl` es la dirección de una foto antigua; las fotos nuevas se suben con `POST /imagenes` y se referencian con `idImagen`. `400` con el motivo si el título no tiene entre 1 y 120 caracteres, la descripción pasa de 500, las medidas no van juntas o no son mayores que cero, el precio no es un monto entero en pesos no negativo, o el tipo de letrero o la imagen no existen. Una publicación con logo de cliente solo puede quedar visible con su autorización: al crearla, guárdala oculta y autorízala después. Al cambiar la imagen o marcar el logo en un `PUT`, la autorización anterior se borra. Responden la publicación completa (`visible`, `incluyeLogoCliente`, `logoAutorizado`); `POST` responde `201` |
| `PUT /catalogo/{id}/autorizacion-logo` | Gestion | `{ autorizado }`. `true` registra quién autorizó el logotipo del cliente y cuándo; si ya estaba registrada, conserva el registro original. `false` la retira y, si la publicación lleva logo, la oculta en el mismo paso. `400` si la publicación no lleva logo de cliente. `404` si no existe. Responde la publicación |
| `DELETE` de las tres | Gestion | `409` si tiene registros asociados |
| `GET /horarios` | Publico | Los 7 dias |
| `GET /cartel-digital` | Publico | `{ abiertoEnLocal, mensajeEstado, tiempoEstimadoRegreso, fechaUltimaModificacion }` |
| `PUT /cartel-digital` | Gestion | `{ abiertoEnLocal, mensajeEstado, tiempoEstimadoRegreso? }` |

### Imagenes (fase 4)

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `POST /imagenes` | Gestion | `multipart/form-data` con el campo `archivo`: JPG, PNG o WebP de hasta 10 MB (el tipo se reconoce por el contenido, no por el nombre). Responde `201` con `{ clave, ancho, alto, urlGrande, urlMiniatura }`. Se guarda en WebP: 1600 px y 640 px por el lado mayor, sin agrandar, enderezada y sin metadatos. `400` si no es una imagen valida, `413` si supera 10 MB |
| `GET /imagenes/{archivo}` | Publico | `{clave}-grande.webp` o `{clave}-miniatura.webp`. `Cache-Control: public, max-age=31536000, immutable` y `ETag`. `404` para cualquier otro nombre |

Al crear una solicitud (`POST /solicitudes`, `POST /solicitudes/online` y `POST /mis/solicitudes`) la API deja en la cola de correo un aviso para el taller. La respuesta no cambia ni espera al servidor de correo: si el aviso no se puede encolar, la solicitud igual queda guardada y el problema se registra.

### Portada publica (fase 5)

Todas son publicas (sin token). Los envios que guardan algo tienen sus propios limites, descritos en la seccion 3.

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /local/estado` | Publico | `{ abiertoAhora, motivo, dentroDeHorario, horaChile, hoy, proximaApertura, cartel, semana }`. `motivo`: `abierto`; `ausente` (en horario, pero el cartel dice cerrado: se muestra su mensaje); `fuera_de_horario`, o `cerrado_hoy` si ese dia no se atiende. Se calcula con la hora de Chile (America/Santiago), y la hora de cierre ya cuenta como cerrado. `cartel` es `null` si no hay cartel cargado |
| `GET /documentos-legales` | Publico | `[{ tipo, version }]` para `privacidad`, `terminos`, `marketing` y `cookies`. Hoy todas estan en `1.0`, que son borradores pendientes de revision legal |
| `POST /contacto` | Publico | `{ nombre, correo, telefono?, asunto, mensaje, aceptaPrivacidad, versionPrivacidad, sitioWeb? }`. Nombre hasta 100 caracteres, asunto hasta 150 y mensaje entre 10 y 2000. `telefono` se guarda como `+56` y 9 digitos. `versionPrivacidad` debe ser la vigente. Responde `202` con `{ recibida, mensaje }`. `400` con el motivo y los errores por campo; `409` si la version de la privacidad ya no es la vigente; `413` si supera 16 KB; `429` si supera 5 por hora por IP. `sitioWeb` es un campo trampa: si llega con texto, responde igual `202`, pero no guarda ni avisa nada. La misma consulta (mismo correo y mismo mensaje) dentro de la ultima hora no se guarda ni avisa de nuevo. La consulta y el consentimiento de privacidad se guardan en una transaccion, y el aviso al taller queda en la cola de correo (`consulta_contacto`, con respuesta dirigida a quien escribio) |
| `POST /consentimientos/cookies` | Publico | `{ visitante, version, acepta? }`. `visitante` es un GUID anonimo que genera el navegador. Sin `acepta`, o con `true`, registra la aceptacion de la version vigente de cookies (`204`). Con `acepta: false` registra el retiro (`204`), que no hace nada si nunca se acepto. `400` si falta `visitante`; `409` si la version no es la vigente; `429` si supera 5 por hora por IP |

### Solicitudes

Un detalle tiene la forma `{ idProducto, descripcion, anchoMm?, altoMm?, cantidad? }`. Ancho y alto van juntos, la cantidad por defecto es 1 y se admiten hasta 50 detalles. Toda solicitud nueva necesita una `descripcionGeneral` o al menos un detalle.

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /solicitudes`, `GET /solicitudes/{id}` | Personal | No incluye las eliminadas |
| `POST /solicitudes` | Personal | `{ idCliente, descripcionGeneral?, detalles? }`. Queda con origen `presencial` |
| `PUT /solicitudes/{id}` | Personal | `{ descripcionGeneral, rowVersion }`. No cambia el estado |
| `PATCH /solicitudes/{id}/estado` | Gestion | `{ estado, motivoRechazo?, rowVersion }`. Estados: `solicitado`, `cotizado`, `aceptado`, `rechazado` |
| `DELETE /solicitudes/{id}` | Gestion | Eliminacion logica, responde `204`. `409` si tiene pedido |
| `PATCH /solicitudes/{id}/eliminar-logico` | Gestion | Igual, pero devuelve la solicitud |
| `POST /solicitudes/online` | Publico | `{ cliente: { nombre, apellido, telefono, email?, direccion? }, descripcionGeneral?, detalles? }`. Solo devuelve `{ idSolicitud, origen, estado, fechaSolicitud }`. `409` si el telefono tiene cuenta: hay que iniciar sesion |
| `GET /metricas/conversion-online` | Gestion | Por año y mes: `totalSolicitudesOnline`, `solicitudesConcretadasEnPedido`, `porcentajeConversion` |

### Portal del cliente

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /mis/solicitudes` | `cliente` | Solo las suyas, con sus detalles |
| `GET /mis/pedidos` | `cliente` | Solo los suyos |
| `POST /mis/solicitudes` | `cliente` | `{ descripcionGeneral?, detalles? }` |

### Cotizaciones y pedidos

Flujo: se crea la cotizacion, se aprueba con `PUT`, se acepta la solicitud con `PATCH` y entonces se puede crear el pedido. Los estados de la solicitud no avanzan solos.

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /cotizaciones`, `GET /cotizaciones/{id}` | Personal | |
| `POST /cotizaciones` | `dueño`, `administrador`, `diseñador` | `{ idSolicitud, observaciones?, costoManoObra, otrosCostos, materiales? }`. Material: `{ idMaterial, medidaAplicada?, cantidad, costoUnitario }` |
| `PUT /cotizaciones/{id}` | `dueño`, `administrador`, `diseñador` | `{ observaciones?, costoManoObra, otrosCostos, estado, materiales? }`. Sin `materiales` se conservan; `[]` los quita. Estados: `pendiente`, `aprobada`, `rechazada`. Los totales los calcula la base |
| `DELETE /cotizaciones/{id}` | Gestion | `409` si la usa un pedido |
| `GET /pedidos`, `GET /pedidos/{id}` | Personal | Cada pedido trae `estadoPedido` y, calculados por la base, `estadoPago` (`no_pagado`, `abonado`, `pagado_total`), `montoAbonado` y `saldoPendiente`; además `origen` (`cotizacion` o `directo`) y `numeroFactura?` |
| `POST /pedidos` | `dueño`, `administrador`, `operario` | `{ idSolicitud, idCotizacion?, idEmpleadoResponsable, fechaEntregaEstimada, modalidadEntrega?, requiereInstalacion?, direccionInstalacion?, costoInstalacionAdicional?, montoBaseTrabajo? }`. La solicitud debe estar `aceptado` y la cotizacion, ser suya y estar `aprobada`. Sin monto, usa el total de la cotizacion. Los montos van en pesos, sin decimales (400 si no) |
| `PUT /pedidos/{id}` | `dueño`, `administrador`, `operario` | `{ estadoPedido, fechaEntregaEstimada, fechaEntregaReal?, modalidadEntrega, requiereInstalacion, direccionInstalacion?, costoInstalacionAdicional, montoBaseTrabajo, rowVersion }`. Estados del pedido: `aceptado`, `en_proceso`, `terminado`, `entregado`, `anulado` («cancelado» ya no existe: en Chile se entiende como «pagado»). El estado de pago no se envía: lo calcula la base. Modalidades: `retiro_local`, `entrega_domicilio`, `instalacion_terreno`. Montos en pesos sin decimales (400). 409 si el monto final quedaría bajo lo ya abonado; también 409 si un abono cambió el pedido después de abrirlo (`rowVersion` vencido) |
| `DELETE /pedidos/{id}` | Gestion | 409 si el pedido tiene abonos registrados: se anula en vez de borrarse |

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
| Pantallas de administracion de productos | Pedir `?incluirInactivos=true` para poder reactivar lo inactivo |

## 6. Cargar el catalogo real

Las categorias, productos, materiales y horarios actuales son datos de ejemplo del script de referencia.

1. Cambiar los `HasData` de `LetrerosHerreradbContext.cs`, o cargar los datos por la API si los va a mantener el propio cliente. Conviene decidirlo con los requisitos: lo que este en `HasData` queda fijado en el codigo.
2. Si se usa `HasData`, generar una migracion nueva con `dotnet ef migrations add`. Nunca editar una migracion ya aplicada.
3. Actualizar las pruebas que dependen de los datos de ejemplo. `ApiFixture` y otras usan `idProducto = 1` e `idMaterial = 1`, y `MigracionTests` comprueba valores concretos.

Si los productos reales necesitan campos nuevos, se agregan al modelo con su migracion.

## 7. Login con reconocimiento facial

Implementado solo para empleados. Complementa a email y contraseña, que siguen igual y siempre disponibles. Los clientes no tienen rutas ni boton.

### Lo que protege y lo que no

`face-api` corre en el navegador y produce un descriptor, una lista de 128 numeros. **El servidor no puede saber si salio de una persona presente.** Una foto puede suplantar a alguien: la prueba de navegador lo demuestra entrando con una imagen fija. Tampoco hay prueba de vida, porque tambien correria en el navegador y se salta enviando la peticion directamente.

| Rol | Que necesita para entrar con rostro | Riesgo |
| --- | --- | --- |
| `dueño`, `administrador` | Rostro y un **equipo autorizado** | Frena una foto o un descriptor copiado usados desde otro equipo. No frena a alguien con una foto en el equipo autorizado, ni a un programa malicioso o a un script inyectado en la pagina, que puede pedir un token nuevo con la cookie de renovacion (desde la fase 6, el token ya no esta en `localStorage`) |
| `diseñador`, `operario` | Solo el rostro | **Riesgo aceptado por decision del responsable del proyecto:** quien tenga su email y una foto, o un descriptor copiado, puede entrar desde cualquier lugar, y esa cuenta ve datos de clientes y de empleados |

Conviene autorizar solo equipos no compartidos. Cada persona tiene un solo equipo autorizado, que vence a los 30 dias (`RostroFacial:DiasEquipo`).

Una sesion iniciada con rostro es igual a una normal: dura lo que cualquier sesion del personal (12 horas, con renovacion, `Sesion:HorasPersonal`). Su token de acceso sigue valido hasta 15 minutos despues de desactivar al empleado, y la renovacion lo rechaza de inmediato. Lo unico que sigue exigiendo la contraseña es crear empleados, asignar credenciales, activar el rostro y autorizar un equipo.

### Flujos

**Activar**, desde Mi perfil: leer el consentimiento, aceptarlo, escribir la contraseña y mirar a la camara. Se toman 5 capturas y se guarda solo su promedio. Dueño y administrador reciben ademas `tokenEquipo`, que el navegador guarda como `synctime_equipo:<email>`. Volver a activar reemplaza el rostro.

**Entrar:** escribir el email, pulsar "Entrar con rostro" y mirar a la camara. La comparacion se hace en el servidor y contra el rostro de esa persona. Si falla, el formulario de contraseña sigue disponible.

**Desactivar**, desde Mi perfil, sin contraseña: se borran el descriptor y el token del equipo. Solo quedan las fechas y la version del consentimiento, como constancia.

### Reglas del servidor

- **Datos:** tabla `rostros_empleado`, una fila por activacion. El descriptor va cifrado con AES-256-GCM y atado al empleado, de modo que copiarlo a otra fila no sirve. La clave esta en `RostroFacial:ClaveCifrado`. No se guardan fotos, y el navegador nunca recibe descriptores guardados.
- **Fallos:** el mismo `401` vacio, con un tiempo minimo de 400 ms, sea que el email no exista, no tenga rostro, este inactivo, bloqueado, sin equipo, o el rostro no coincida.
- **Bloqueo:** 5 fallos bloquean el rostro de esa cuenta 15 minutos, sin avisarlo y sin afectar a la contraseña. Cualquiera que sepa el email de un operario o diseñador puede provocarlo. Sin un equipo valido el intento no se cuenta, asi que no se puede bloquear al dueño desde fuera.
- **Umbral:** distancia maxima 0,5 (`RostroFacial:UmbralDistancia`). Es **provisional:** no se ha medido con personas reales. Hay que calibrarlo con pruebas consentidas, registrando distancias.
- **Clave ausente o invalida:** la API arranca, el login con contraseña funciona y las rutas de rostro responden `503`. Si la clave cambia, cada empleado debe volver a activar su rostro; queda un error en el log.
- **Empleados inactivos:** su rostro se rechaza de inmediato. Como hoy solo se pueden desactivar por SQL, sus datos se borran en el siguiente intento de login facial de cualquier persona. Si se reactiva a alguien antes de eso, su rostro seguiria activo: quien desactive por SQL debe borrar tambien sus datos (el comando esta en el `README.md`). La futura ruta para desactivar empleados debe borrarlos en el mismo paso.

### Frontend

- La libreria es `@vladmandic/face-api` 1.7.15, en version exacta. **Su repositorio esta archivado** y su README indica que `@vladmandic/human` la reemplaza; `face-api.js` lleva mas tiempo sin cambios. Queda aislada en `src/rostro/motorFacial.js`. Cambiarla exige tocar ese modulo y `RostroFacialService`, y que cada empleado active su rostro de nuevo.
- La libreria (1,3 MB) y 3 modelos (6,7 MB, en `public/models`) se descargan solo al usar la funcion.
- La politica de seguridad de `index.html` no necesito cambios. La libreria intenta compilar un WebAssembly opcional dentro de un `try/catch`, y la politica lo bloquea: en la consola aparece esa violacion, y es esperada. Sin WebGL usa la CPU, mas lenta.
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
- Solo `dueño` y `administrador` cambian el estado de una solicitud. Un `diseñador` no puede marcarla como `cotizado`.
- Un pedido exige una solicitud `aceptado`, y su cotizacion, si se indica, debe estar `aprobada`.
- Una solicitud online con el telefono de una cuenta se rechaza con `409`.
- Una solicitud admite hasta 50 detalles.
- Solo se pueden pedir productos activos.
- El login facial admite un solo equipo autorizado por persona, y `diseñador` y `operario` no lo necesitan (seccion 7).
- Una cuenta web de cliente exige correo: el correo es el usuario para ingresar y para recuperar la contraseña. El RUT tambien es opcional en la cuenta (decision de la fase 6, en `docs/plan/desviaciones.md`).
- Registrarse con un telefono o un RUT que ya estan en el taller da el mismo mensaje, sin decir cual de los dos coincide. El correo si se revela («Ya existe una cuenta con ese correo»).
