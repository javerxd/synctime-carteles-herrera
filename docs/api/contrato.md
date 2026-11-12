# Contrato de la API para el frontend

Este documento reune lo necesario para adaptar el frontend (`frontend/synctimefront`) cuando esten definidos los requisitos finales. **Estado al 06-11-2026:** la API esta lista y probada. En el frontend solo esta hecho el login facial de empleados (seccion 7); el resto sigue usando el contrato anterior.

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

El token va en la cabecera `Authorization: Bearer <token>` y dura 480 minutos, salvo que se configure `Jwt:ExpirationMinutes`.

| Login | Cuerpo | Respuesta |
| --- | --- | --- |
| `POST /auth/login` (personal) | `{ email, password }` | `{ token, usuario: { idEmpleado, nombre, apellido, email, rol } }` |
| `POST /auth/login-cliente` | `{ telefono, password }` | `{ token, cliente: { idCliente, nombre, apellido, telefono, email } }` |
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
| `413` | El cuerpo supera el tope de las rutas de rostro: 8 KB en el login, 64 KB al activar |
| `429` | Limite por IP por minuto: 10 logins con contraseña, 10 logins con rostro, 10 confirmaciones de contraseña, 10 envios publicos o 100 peticiones generales. Cada uno tiene su contador |
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
| `POST /auth/registro-cliente` | Publico | `{ rut, nombre, apellido, telefono, email?, direccion?, password }`. Password de 8 a 72 caracteres. `409` si el telefono existe |
| `POST /auth/empleados` | Gestion | `{ rut, nombre, apellido, telefono, email, password, rol, passwordActual }`. `password` es la del empleado nuevo; `passwordActual`, la de quien lo crea |
| `GET /auth/me` | Con sesion | Lista de claims del token |
| `POST /auth/login-facial` | Publico | `{ email, descriptor, tokenEquipo? }`. Todo fallo responde el mismo `401` vacio |
| `GET /auth/rostro` | Personal | `{ disponible, activo, fechaConsentimiento, requiereEquipo, equipoVence, equipoVigente, consentimiento: { version, texto } }` |
| `PUT /auth/rostro` | Personal | `{ password, consentimiento: true, versionConsentimiento, descriptores }`, con 3 a 10 capturas. Devuelve `{ activo, fechaConsentimiento, tokenEquipo, equipoVence }` |
| `POST /auth/rostro/equipo` | `dueño`, `administrador` | `{ password }`. Autoriza este equipo y anula el anterior. Devuelve `{ tokenEquipo, equipoVence }` |
| `DELETE /auth/rostro` | Personal | Borra los datos biometricos sin pedir contraseña. Responde `204`, tambien si no estaba activo |

### Clientes, personal y materiales

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /clientes`, `GET /clientes/{id}` | Personal | |
| `POST /clientes`, `PUT /clientes/{id}` | `dueño`, `administrador`, `operario` | `{ rut?, nombre, apellido, telefono, email?, direccion?, activo? }` |
| `PUT /clientes/{id}/credenciales` | `dueño`, `administrador`, `operario` | `{ password, passwordActual }`. Activa el acceso web de un cliente que ya existe. `password` es la del cliente; `passwordActual`, la de quien la asigna |
| `DELETE /clientes/{id}` | Gestion | `409` si tiene solicitudes |
| `GET /empleados`, `GET /materiales` | Personal | |

### Vitrina

| Ruta | Quien | Cuerpo y notas |
| --- | --- | --- |
| `GET /categorias`, `GET /categorias/{id}` | Publico | `?incluirInactivos=true` para el personal |
| `POST /categorias`, `PUT /categorias/{id}` | Gestion | `{ nombre, descripcion?, activo? }` |
| `GET /productos`, `GET /productos/{id}` | Publico | `?incluirInactivos=true` para el personal. Incluye `categoria` |
| `POST /productos`, `PUT /productos/{id}` | Gestion | `{ idCategoria, nombre, descripcion?, precioBaseRef?, activo? }` |
| `GET /catalogo`, `GET /catalogo/{id}` | Publico | `?incluirOcultos=true` para el personal. Incluye `producto` |
| `POST /catalogo`, `PUT /catalogo/{id}` | Gestion | `{ idProducto, titulo, descripcion, imagenUrl, anchoMm?, altoMm?, precioReferencial?, visible? }` |
| `DELETE` de las tres | Gestion | `409` si tiene registros asociados |
| `GET /horarios` | Publico | Los 7 dias |
| `GET /cartel-digital` | Publico | `{ abiertoEnLocal, mensajeEstado, tiempoEstimadoRegreso, fechaUltimaModificacion }` |
| `PUT /cartel-digital` | Gestion | `{ abiertoEnLocal, mensajeEstado, tiempoEstimadoRegreso? }` |

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
| `GET /pedidos`, `GET /pedidos/{id}` | Personal | |
| `POST /pedidos` | `dueño`, `administrador`, `operario` | `{ idSolicitud, idCotizacion?, idEmpleadoResponsable, fechaEntregaEstimada, modalidadEntrega?, requiereInstalacion?, direccionInstalacion?, costoInstalacionAdicional?, montoBaseTrabajo? }`. La solicitud debe estar `aceptado` y la cotizacion, ser suya y estar `aprobada`. Sin monto, usa el total de la cotizacion |
| `PUT /pedidos/{id}` | `dueño`, `administrador`, `operario` | `{ estado, fechaEntregaEstimada, fechaEntregaReal?, modalidadEntrega, requiereInstalacion, direccionInstalacion?, costoInstalacionAdicional, montoBaseTrabajo, rowVersion }`. Estados: `aceptado`, `en_proceso`, `terminado`, `entregado`, `cancelado`. Modalidades: `retiro_local`, `entrega_domicilio`, `instalacion_terreno` |
| `DELETE /pedidos/{id}` | Gestion | |

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
| `dueño`, `administrador` | Rostro y un **equipo autorizado** | Frena una foto o un descriptor copiado usados desde otro equipo. No frena a alguien con una foto en el equipo autorizado, ni a un programa malicioso o un script inyectado que robe el token de `localStorage` |
| `diseñador`, `operario` | Solo el rostro | **Riesgo aceptado por decision del responsable del proyecto:** quien tenga su email y una foto, o un descriptor copiado, puede entrar desde cualquier lugar, y esa cuenta ve datos de clientes y de empleados |

Conviene autorizar solo equipos no compartidos. Cada persona tiene un solo equipo autorizado, que vence a los 30 dias (`RostroFacial:DiasEquipo`).

Una sesion iniciada con rostro es igual a una normal: dura 8 horas y sigue valida tras desactivar al empleado. Lo unico que sigue exigiendo la contraseña es crear empleados, asignar credenciales, activar el rostro y autorizar un equipo.

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
