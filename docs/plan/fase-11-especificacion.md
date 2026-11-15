# Especificación técnica de la fase 11 (contrato entre backend y frontend)

Acompaña a `fase-11-plan.md`, aprobado el 15-11-2026 con cinco decisiones del usuario. Si algo no está aquí, decide lo más simple y seguro, coherente con el código actual, y anótalo en tu respuesta. Todas las rutas van bajo `/api/v1`.

## Decisiones fijas
- **Avisos del taller = correos operativos.** Tipos `aviso_solicitud_nueva`, `aviso_tope_solicitudes` y `consulta_contacto`. Ninguna baja ni preferencia de un cliente los afecta.
- **Preferencia de avisos por destino** (decisión 1), en `destinatarios_avisos`: `recibe_en_linea` (bit, por defecto 1) y `recibe_presenciales` (bit, por defecto 0).
  - Al menos uno de los dos debe ser 1: CHECK en la base y 400 en la API.
  - El aviso de una solicitud presencial es nuevo, con el mismo tipo `aviso_solicitud_nueva` y la referencia `solicitud:{id}`. Lo envía `ServicioSolicitudes` al registrar una presencial, en la misma transacción.
  - Si ningún destino activo recibe ese origen, no se encola nada. Si la tabla no tiene destinos, el respaldo `Correo:AvisosTaller` recibe solo las solicitudes en línea, como hoy.
- **«Correos no enviados» muestra todos los fallidos** (decisión 2).
- **Reenviar un aviso del taller** va a los destinos activos de hoy, según su preferencia y el origen de la solicitud. Los demás correos se reenvían a su misma dirección (decisión 3).
- **El cliente puede darse de baja de los avisos de estado** (decisión 4). Los de cuenta y seguridad nunca se dan de baja.
- **Nunca en logs ni en la auditoría:** cuerpos o asuntos de correo, direcciones completas, contraseñas, hashes, tokens ni descriptores.

## Correos al cliente

- **Tipos nuevos** (`TiposCorreo`):
  - `estado_solicitud`: la solicitud pasa a cotizada, rechazada o aceptada. «Tu cotización está lista» (`cotizacion_lista`) ya existe para cotizada: se mantiene y pasa a ser opcional con la misma preferencia.
  - `estado_pedido`: el pedido pasa a `en_proceso`, `terminado`, `entregado` o `anulado`.
  - `baja_cuenta`: el comprobante de baja.
- **Contenido (S09):** número, estado nuevo en palabras, un mensaje breve (el motivo, si lo hay), la fecha y el **enlace de baja**. Se encolan en la misma transacción que el cambio, solo si el cliente tiene cuenta con el correo confirmado y la preferencia activa.
- **Categorías:**
  - **`avisos_estado`** (opcional): `estado_solicitud`, `estado_pedido` y `cotizacion_lista`.
  - **`marketing`** (opcional): ya existe como consentimiento; esta fase no agrega correos de marketing.
  - **Esenciales** (sin enlace de baja, siempre se envían): `verificacion_correo`, `recuperacion_cuenta`, `aviso_cambio_correo` y `baja_cuenta`.
- **Preferencia `avisos_estado`:**
  - **Dónde se guarda:** en `clientes.recibe_avisos_estado` (bit, por defecto 1).
  - **Registro:** cada cambio queda además en `consentimientos` con el tipo `avisos_estado`, la versión `1.0`, la aceptación o la revocación y la fecha.
  - **Al darse de baja:** el marketing también se revoca, con su consentimiento existente.

### Enlace de baja
- **Token:** sale de `clientes.token_baja`, 32 bytes aleatorios en base64url, y se guarda solo su hash (char(64)). Va en cada correo opcional: `{Sitio:UrlPublica}/baja?t={token}`.
  - **Se regenera** al cambiar el correo del cliente y al dar de baja la cuenta. Así un enlace viejo deja de servir.
- **Rutas:**
  - **`GET /correos/baja?t=`** (anónimo): `200 { correoEnmascarado, avisosEstado: bool, marketing: bool }`. **No cambia nada.**
  - **`POST /correos/baja`** (anónimo): cuerpo `{ t, avisosEstado?: false, marketing?: false }`; sin campos, apaga los dos. Responde `200 { avisosEstado, marketing }`. Es idempotente y deja auditoría.
  - **`POST /correos/baja/un-clic`** (anónimo, `application/x-www-form-urlencoded` con `List-Unsubscribe=One-Click`, RFC 8058): apaga los dos y responde 200 sin cuerpo.
  - **Token inválido o alterado:** 404 genérico.
  - **Limitador:** `ApiGeneral`.
- **Cabeceras en cada correo opcional:** `List-Unsubscribe: <{UrlPublica}/api/v1/correos/baja/un-clic?t={token}>` y `List-Unsubscribe-Post: List-Unsubscribe=One-Click`. El token va en la URL y la ruta lo acepta también por consulta.
- **`GET /mi-cuenta` suma `{ avisosEstado, marketing }`**, y **`PUT /mi-cuenta/preferencias`** recibe `{ avisosEstado, marketing }`. Esta ruta reemplaza a `PUT /mi-cuenta/marketing`, que se mantiene como alias.

## Avisos del taller: preferencia, no enviados y reenvío

- **`GET /avisos-taller`:** cada destino suma `recibeEnLinea`, `recibePresenciales` y `rowVersion`.
- **`PUT /avisos-taller/destinos/{id}/preferencia`** (gestión): cuerpo `{ recibeEnLinea, recibePresenciales }`, con la cabecera `If-Match`.
  - **Responde:** 200 con el destino. Si los dos vienen apagados, 400 `datos_invalidos` (`errores.preferencia`); sin la versión, 400 `sin_version`; si cambió, 409 `version_obsoleta`.
  - **Registro:** queda en `destinatarios_avisos_historial` con la acción `preferencia`. No pide contraseña.
- **`GET /correos/no-enviados`** (gestión): parámetros `pagina`, `tamano`, `tipo`, `desde`, `hasta` y `revisado`.
  - **Respuesta:** `ResultadoPaginado` de `{ idCorreo, tipo, tipoTexto, referencia, enlace, destinoEnmascarado, intentos, motivo, fechaCreacion, fechaFallo, revisado, reenviable, reenviadoComo }`.
  - **`enlace`:** la ruta del panel, por ejemplo `/panel/solicitudes/123`, o null.
  - **`reenviable`:** false si el cuerpo ya se purgó o si ya se reenvió.
- **`GET /correos/no-enviados/resumen`** (gestión): `{ sinRevisar: n }`, para el aviso del inicio del panel.
- **`POST /correos/no-enviados/{id}/reenviar`** (gestión):
  - **Si el correo sigue `fallido` y no se reenvió:** crea una fila nueva en la cola con el mismo tipo, referencia, asunto y cuerpo, y marca la original (`reenviado_como`, `id_empleado_reenvia`, `fecha_reenvio`, `revisado = 1`).
  - **Avisos del taller:** los destinatarios se recalculan con los destinos activos de hoy y su preferencia, según el origen de la solicitud (decisión 3).
  - **Respuestas:** 202 `{ idCorreoNuevo }`. Si ya se reenvió, 409 `ya_reenviado` con su id. Si se purgó el cuerpo, 409 `sin_contenido`. Si no existe, 404.
  - **Concurrencia:** se marca con una sola sentencia condicionada (`WHERE reenviado_como IS NULL`), para que dos clics no creen dos correos.
- **`POST /correos/no-enviados/{id}/revisado`** (gestión): marca `revisado = 1` sin reenviar. Responde 204.
- **El trabajador de la cola** deja en la auditoría:
  - **Envío de un aviso:** acción `aviso_enviado`.
  - **Fallo definitivo de cualquier correo:** acción `correo_fallido`.
  - **Detalle, siempre sin cuerpo, asunto ni dirección completa:** tipo, referencia, destino enmascarado, intentos y motivo clasificado.

## Bitácora de auditoría

**Tabla `auditoria`:**

| Columna | Tipo |
|---|---|
| `id_auditoria` | bigint identity |
| `fecha` | datetimeoffset |
| `id_empleado` | int null |
| `id_cliente` | int null |
| `rol` | varchar(20) null: `sistema` si no hay usuario |
| `entidad` | varchar(40) |
| `id_entidad` | varchar(40) null |
| `accion` | varchar(30): `crear`, `modificar`, `eliminar`, `aviso_enviado`, `correo_fallido`, `correo_reenviado`, `baja_correo`, `baja_cuenta`, `descarga_datos` y otras |
| `detalle` | nvarchar(2000) null, JSON |
| `ip` | varchar(45) null |

- **Inmutable:** un trigger `INSTEAD OF UPDATE, DELETE` lanza `THROW`. La purga usa un procedimiento o un camino documentado que desactiva el trigger en la misma transacción y solo borra filas de más de `Auditoria:MesesConservacion` (24), o bien la purga no existe en esta fase. Elige y anótalo.
- **Interceptor** (`SaveChangesInterceptor`): cubre `Solicitud`, `SolicitudDetalle`, `Cotizacion`, `Pedido`, `Pago`, `Catalogo`, `Producto`, `Categoria`, `Material`, `Cliente`, `Empleado`, `DestinatarioAviso`, `RostroEmpleado`, `HorarioAtencion` y `CartelDigital`.
  - **Qué guarda:** en el `detalle`, los nombres de los campos cambiados. Los valores solo van para una lista explícita por entidad: estados, montos, fechas, booleanos de activo y visible.
  - **Lo que nunca guarda:** `PasswordHash`, `TokenHash`, descriptores, `TokenEquipoHash`, cuerpos ni correos.
  - **Quién:** lo saca del `HttpContext` actual (claims). Si no hay contexto, el rol es `sistema`.
- **Prueba de arquitectura:**
  - **Cobertura:** toda entidad principal está en la lista del interceptor.
  - **Escrituras invisibles:** ningún `ExecuteUpdate`, `ExecuteDelete` ni `ExecuteSql…` sobre esas entidades fuera de una lista de excepciones justificadas, y cada excepción registra a mano con `IAuditoria.RegistrarAsync`.
- **`GET /auditoria`** (gestión): parámetros `pagina`, `tamano`, `desde`, `hasta`, `entidad`, `accion`, `idEmpleado` y `q` (sobre `id_entidad`).
  - **Respuesta:** `ResultadoPaginado` de `{ idAuditoria, fecha, usuario: { tipo: "empleado"|"cliente"|"sistema", id, nombre }, rol, entidad, entidadTexto, idEntidad, accion, accionTexto, detalle (objeto), ip }`. Va del más reciente al más antiguo.
- **`GET /auditoria/consentimientos`** (gestión): `ResultadoPaginado` de `{ titular: { tipo, id, nombre }, tipo, version, acepta, fecha }`, con filtros `tipo`, `desde` y `hasta`.

## Mis datos y baja de la cuenta (cliente)

- **`POST /mi-cuenta/datos`:** cuerpo `{ password }`, con el limitador `Reautenticacion` y la contraseña incorrecta como 400 `datos_invalidos`.
  - **Responde:** 200 con `application/json` y `Content-Disposition: attachment; filename="mis-datos-letreros-herrera-AAAAMMDD.json"`.
  - **El archivo:** `{ generado, cuenta, consentimientos, solicitudes: [{ …, detalles, adjuntos: [{ nombre, tipo, tamano }] }], cotizaciones, pedidos: [{ …, pagos }], correosEnviados: [{ tipo, fecha, estado }] }`. Solo trae lo del cliente.
  - **Auditoría:** `descarga_datos`.
- **`POST /mi-cuenta/baja`:** cuerpo `{ password, confirmacion: "DAR DE BAJA" }`, con el mismo limitador.
  - **Una transacción:**
    - `clientes.inhabilitado_en` = ahora;
    - `password_hash` = null;
    - se borran sus códigos y tokens de renovación, y se cierran las sesiones;
    - `recibe_avisos_estado` = 0, y se revoca el marketing;
    - se regenera `token_baja`;
    - se encola `baja_cuenta` al correo, si estaba confirmado;
    - auditoría `baja_cuenta`.
  - **Lo que se conserva:** solicitudes, cotizaciones, pedidos y pagos.
  - **Responde:** 200 `{ comprobante: { numero, fecha } }`. El número es `BAJA-{idCliente}-{AAAAMMDD}`.
  - **Después:** el login del cliente inhabilitado responde el 401 genérico; el registro con ese correo responde el 409 actual.
  - **Personal:** puede seguir viendo los pedidos, con el cliente marcado «Cuenta dada de baja».

## Purga de la cola

- **Trabajo:** se suma a `PurgaDatosPersonales`.
  - **Enviados de más de `Correo:Cola:DiasCuerpoEnviados` (30):** `asunto`, `cuerpo_html` y `cuerpo_texto` quedan vacíos, y `destinatarios` queda enmascarado.
  - **Fallidos de más de `Correo:Cola:DiasCuerpoFallidos` (90):** lo mismo, y quedan con `reenviable = false`.
- **Las columnas:** las de texto pasan a admitir null, o se guarda la cadena vacía. Elige y anótalo.

## Concurrencia e idempotencia

- **`Idempotency-Key`** (cabecera; UUID de la pantalla, uno por formulario abierto). Va en:
  - `POST /solicitudes` (personal), `POST /mis/solicitudes` y `POST /solicitudes/{id}/cotizaciones`;
  - `POST /pedidos/directo` y `POST /cotizaciones/{id}/pedido`;
  - `POST /pedidos/{id}/pagos`.
- **Tabla `claves_idempotencia`:** `clave` (char 36), `ambito` (ruta), `id_usuario` y `tipo_usuario`, `hash_cuerpo` (SHA-256), `estado_http`, `respuesta` (nvarchar max), `fecha` y `vence`. Es única por (`clave`, `ambito`, `tipo_usuario`, `id_usuario`).
- **Comportamiento:**
  - **Primera vez:** reserva la clave con un insert que no se puede repetir (si choca, espera o responde 409 `en_proceso`) y guarda la respuesta cuando es 2xx.
  - **Repetida con el mismo cuerpo:** devuelve la respuesta guardada, con la cabecera `Idempotent-Replayed: true`.
  - **Repetida con otro cuerpo:** 409 `clave_reutilizada`.
  - **Sin la cabecera:** se comporta como hoy, así que es retrocompatible.
  - **Clave inválida:** 400.
  - **Vencimiento:** las claves duran 24 horas, y la purga diaria las borra.
  - **Errores:** las respuestas 4xx no se guardan; liberan la clave para reintentar.
- **Versión en el horario y el cartel:** `horario_atencion` y `cartel_digital` reciben `row_version`.
  - **Respuesta del GET:** `rowVersion` y `ETag`.
  - **Al guardar:** el PUT exige `If-Match`; sin él, 400 `sin_version`; si cambió, 409 `version_obsoleta`. Esto aplica a `PUT /horarios` y a `PUT /cartel-digital`.
- **Ficha de clientes:** el frontend ofrece «Guardar sobre la versión actual» con `useConflictoDeVersion`, como los mantenedores.

## Migración `NotificacionesYAuditoria` (solo agrega)

- **Columnas nuevas:**
  - en `destinatarios_avisos`: las dos preferencias, con su CHECK, y `row_version`;
  - en `clientes`: `recibe_avisos_estado`, `token_baja_hash` e `inhabilitado_en`;
  - en `correos_salientes`: `revisado`, `reenviado_como`, `id_empleado_reenvia` y `fecha_reenvio`, y los textos que pasan a admitir null si se elige así;
  - `row_version` en `horario_atencion` y `cartel_digital`.
- **Tablas nuevas:** `auditoria` con su trigger, y `claves_idempotencia`.
- **Índices:** `auditoria(fecha desc)`, `auditoria(entidad, id_entidad)`, y en `correos_salientes`, un índice filtrado por `estado = 'fallido'`.
- **`Down()`:** `THROW` si las tablas nuevas tienen filas o si hay datos en las columnas nuevas que se perderían.
- **Dónde se aplica:** se prueba en una base temporal y se aplica solo a la demo. Se regenera el script.

## Frontend

- **Perfil, «Avisos del taller»:**
  - **Preferencia por destino:** dos casillas, «Solicitudes en línea» y «Solicitudes presenciales», con al menos una marcada y el error junto al campo. Se guarda con `If-Match`, y un conflicto se maneja como en los mantenedores.
- **`/panel/correos-no-enviados`** (gestión; se agrega al menú):
  - una tabla con filtros (tipo, fechas, «sin revisar»);
  - **«Reenviar»**, con confirmación que explica a quién irá: «a los destinos activos de hoy» o «a la misma dirección»;
  - **«Marcar como revisado»**;
  - los estados vacío, cargando y error;
  - el enlace a la solicitud.
- **Inicio del panel:** si `sinRevisar > 0`, un aviso con un enlace: «N correos no se pudieron enviar».
- **`/panel/auditoria`** (gestión; se agrega al menú):
  - visor con filtros, paginación y el detalle legible (los campos cambiados, con antes y después solo si vienen);
  - una pestaña o sección «Consentimientos».
- **Mi cuenta:**
  - **«Correos que recibo»:** casillas para los avisos de estado y el marketing, más el texto «Los correos de seguridad de tu cuenta siempre se envían».
  - **«Mis datos»:** «Descargar mis datos», con la contraseña, y «Dar de baja mi cuenta», con la contraseña, escribir «DAR DE BAJA» y una explicación clara de qué se conserva. Al terminar se muestra el comprobante, se cierra la sesión y se lleva a la portada.
- **`/baja?t=`** (pública): muestra el correo enmascarado y lo que está activo, con «Confirmar baja» y la opción de elegir.
  - **No actúa al cargar.** Muestra el resultado y su error con un enlace inválido.
  - **El token no viaja en el `Referer`:** `<meta name="referrer" content="no-referrer">` en la ruta, o el mismo método que se usó para el enlace de confirmación de la fase 9.
- **Idempotencia:** los formularios de crear solicitud (personal y cliente), cotización, pedido directo, pedido desde cotización y abono generan un `crypto.randomUUID()` al abrirse y lo mandan en `Idempotency-Key`.
  - El mismo valor se usa en cada reintento del mismo formulario, y uno nuevo solo tras un éxito o al reabrirlo.
  - Si la respuesta trae `Idempotent-Replayed`, se trata igual que un éxito.
- **Horario y cartel:** mandan `If-Match` y manejan el 409 con «Recargar» o «Guardar sobre la versión actual».
- **Ficha de clientes:** suma «Guardar sobre la versión actual».
- **Diseño:** sigue `DESIGN.md` de la fase 2, con accesibilidad AA.
