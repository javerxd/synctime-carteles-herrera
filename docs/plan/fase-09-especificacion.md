# Especificación técnica de la fase 9 (contrato entre backend y frontend)

Acompaña a `fase-09-plan.md`, aprobado el 14-11-2026 con cuatro decisiones del usuario. Si algo no está aquí, decide lo más simple y coherente con el plan y con las fases 7 y 8, y anótalo en tu respuesta.

## Decisiones fijas
- **Gestión:** son los roles `dueño` y `administrador`, con `Politicas.Gestion`. **Personal** es todo empleado (`Politicas.Empleado`).
- **Errores:** formato estándar `{ error, codigo, nivel, errores? }`, con los mismos códigos que en las fases 7 y 8:
  - 400 `sin_version`;
  - 409 `version_obsoleta`;
  - 409 `transicion_invalida` o un código propio;
  - 403 `solo_gestion`.
- **Versión:** toda edición o desactivación de un mantenedor exige `If-Match`, con el ETag del GET (o `rowVersion` en el cuerpo).
- **Listados:** usan `ParametrosListado` y `ResultadoPaginado`, con `q`, `activo` (`true`, `false` o todos), `orden`, `pagina` y `tamano`.
- **Borrado:** no hay borrado físico en los mantenedores. Se **desactiva** (`activo = false`), y un desactivado deja de ofrecerse en los formularios.
- **Fechas:** en hora de Chile (`HoraChile`).

## Base de datos (migración nueva `MantenedoresYAvisos`, solo agrega)

**`row_version` (rowversion)** en `categorias`, `productos`, `catalogo`, `materiales`, `empleados` y `clientes`.

**`horarios_atencion`:**
- Columnas nuevas: `id_empleado_modifica` (int null, FK a `empleados`) y `fecha_modificacion` (datetimeoffset null).
- Check: si abre, `hora_inicio < hora_fin`; si no abre, las dos horas son null.

**`disponibilidad_empleados`** (nueva):

| Columna | Tipo y reglas |
|---|---|
| `id_disponibilidad` | int, identity |
| `id_empleado` | FK a `empleados` |
| `dia_semana` | tinyint, de 1 (lunes) a 7 (domingo) |
| `hora_inicio`, `hora_fin` | time(0), con inicio < fin |

- Índice por empleado y día.
- La API impide que dos franjas del mismo empleado se crucen.

**`destinatarios_avisos`** (nueva):

| Columna | Tipo y reglas |
|---|---|
| `id_destinatario` | int, identity |
| `correo` | nvarchar(254), en minúsculas |
| `estado` | `pendiente`, `activo`, `retirado` o `cancelado` (check) |
| `id_reemplaza` | int null, FK a `destinatarios_avisos`: el activo que reemplaza |
| `token_hash` | char(64) null, SHA-256 del enlace |
| `vence` | datetimeoffset null |
| `id_empleado_solicita` | FK a `empleados` |
| `fecha_solicitud` | datetimeoffset |
| `fecha_confirmacion` | datetimeoffset null |
| `fecha_retiro` | datetimeoffset null |

- Índice único filtrado: un mismo correo no puede estar dos veces `activo` o `pendiente`.
- Check: `token_hash` y `vence` presentes si y solo si el estado es `pendiente`.

**`destinatarios_avisos_historial`** (nueva):

| Columna | Tipo y reglas |
|---|---|
| `id_historial` | int, identity |
| `accion` | `agregar`, `reemplazar`, `confirmar`, `cancelar`, `quitar`, `reenviar` o `probar` |
| `correo_enmascarado` | nvarchar(254) |
| `id_destinatario` | int null |
| `id_empleado` | int null; null si confirmó alguien desde el enlace, sin sesión |
| `fecha` | datetimeoffset |
| `detalle` | nvarchar(300) null |

**Antes de aplicar:** se comprueba que los datos actuales de la demo cumplan los checks nuevos (por ejemplo, horarios con inicio < fin).

**`Down()`:** se detiene con `THROW` si las tablas nuevas tienen datos, igual que en la fase 8.

**Script:** se regenera `docs/base-de-datos/LetreriaLosHerreraDB.sql`.

## Avisos del taller

**Lectura de los destinatarios** (`IDestinatariosAvisos`):
- Devuelve los correos `activo` de la base. Si no hay ninguno, devuelve los de `Correo:AvisosTaller`.
- La lectura de la configuración sigue en un solo lugar: lo comprueba `ArquitecturaTests`, que se actualiza para aceptar la nueva implementación.
- **Sin caché.** Si se agrega, se invalida al confirmar.

**Enmascarar un correo:** `contacto@gmail.com` se muestra como `c•••o@gmail.com`. El dominio va completo.

**Rutas** (todas gestión, salvo confirmar):
- **`GET /api/v1/avisos-taller`.** Responde:
  ```
  { destinos: [{ idDestinatario, correo, estado, reemplazaA: { idDestinatario, correo } | null,
               fechaSolicitudChile, fechaConfirmacionChile, venceChile }],
    usaRespaldo: bool,
    historial: [{ fechaChile, accion, correo, empleado, detalle }] }
  ```
  - `destinos` trae solo los activos y los pendientes.
  - `usaRespaldo` es true si no hay activos en la base y se usa la configuración.
  - El historial trae los últimos 20 registros, con los correos enmascarados.
  - **Gestión ve los correos completos**, porque los administra.
- **`POST /api/v1/avisos-taller/destinos`** con `{ correo, idReemplaza? }`.
  - **Sin `idReemplaza`:** agrega un destino.
  - **Con `idReemplaza`** (un activo): reemplaza ese destino.
  - **Valida el correo** con la regla de correos de la fase 6. Responde 409 `ya_existe` si ya está activo o pendiente, y 409 si el destino a reemplazar ya tiene un reemplazo pendiente.
  - **Crea el destino `pendiente`** con un enlace de un solo uso que vence en 24 horas y escribe en la cola, **en la misma transacción**, el correo «Confirma el destino de los avisos de Letreros Herrera».
    - Sale desde la casilla automática, con el pie de «no respondas».
    - Su botón «Confirmar este correo» lleva a `{Sitio:UrlPublica}/avisos/confirmar?token=…`.
  - Registra el historial y responde **201**.
- **`POST /api/v1/avisos-taller/destinos/{id}/reenviar`:** sobre un pendiente. Genera un enlace nuevo que invalida el anterior, lo reencola y registra el historial.
  - Límite: 1 por minuto y 5 por hora por destino.
- **`POST /api/v1/avisos-taller/destinos/{id}/cancelar`:** el pendiente pasa a `cancelado` y se registra el historial.
- **`DELETE /api/v1/avisos-taller/destinos/{id}`:** quita un activo, que pasa a `retirado`.
  - **Regla:** si es el último activo, responde 409 `ultimo_destino` con «Debe quedar al menos un correo activo. Para cambiarlo, reemplázalo» (decisión 3).
  - **Aviso:** se envía a la dirección quitada: «Este correo ya no recibe los avisos del taller».
- **`POST /api/v1/avisos-taller/confirmar`** con `{ token }`. **Es pública**, porque la abre quien recibe el correo, sin sesión.
  - **Límites:** usa el limitador de las rutas de cuenta, por IP.
  - **Errores:** con un enlace inválido, vencido o ya usado responde **400** con «El enlace no es válido o ya venció. Pide uno nuevo desde el perfil». La respuesta no dice qué correo era.
  - **Al confirmar,** en una transacción:
    - el pendiente pasa a `activo` y se borran su token y su vencimiento;
    - si reemplazaba a otro, ese pasa a `retirado`, y su dirección recibe «El destino de los avisos de Letreros Herrera cambió». El correo trae la dirección nueva enmascarada, quién lo pidió (su nombre de pila) y la fecha;
    - si fue un destino **agregado**, cada activo anterior recibe «Se agregó un destino a los avisos del taller», con la dirección enmascarada (decisión 4);
    - se registra el historial con `id_empleado` null.
  - Responde **200** con `{ correo: "c•••o@gmail.com" }`.
- **`POST /api/v1/avisos-taller/prueba`:** encola a los destinatarios actuales (de la base o del respaldo) un aviso de ejemplo, en la misma transacción que su registro en el historial.
  - **Contenido:** el asunto «Nueva solicitud #0 - Prueba del taller», con la misma plantilla de `AvisoSolicitudNueva`, una franja «Esto es una prueba» y los datos ficticios marcados.
  - **Tipo de correo:** `prueba_avisos`. No cuenta para el tope de avisos por hora.
  - **Límite:** 3 cada 10 minutos por taller; si se supera, 429.
  - **Respuesta:** **202** con `{ idsCorreos: [..], destinatarios: [correos], usaRespaldo }`. No espera el envío.
- **`GET /api/v1/avisos-taller/prueba/estado?ids=1,2`:** devuelve `[{ idCorreo, destinatario, estado: "pendiente"|"enviado"|"fallido", intentos, error }]`, con `error` como un texto corto y seguro. Solo responde por correos del tipo `prueba_avisos`.

## Estado del local y horario
- **`PUT /api/v1/cartel-digital`:** ahora lo puede usar **todo el personal**.
  - **Validaciones:** el mensaje es obligatorio (máx. 200), y el regreso estimado es opcional (máx. 100).
  - **Registro:** guarda quién y cuándo.
  - **Respuesta:** ya no devuelve la entidad de EF.
- **`GET /api/v1/horarios`:** sigue público.
- **`PUT /api/v1/horarios`** (gestión): `{ dias: [{ diaSemana, abierto, horaInicio?, horaFin? }] }` con los 7 días, en una transacción. Guarda quién y cuándo, y responde con los 7 días.

## Disponibilidad (RFU20)
- **`GET /api/v1/mi-disponibilidad`** (personal) y **`PUT /api/v1/mi-disponibilidad`** con `{ franjas: [{ diaSemana, horaInicio, horaFin }] }`:
  - reemplaza todas las franjas propias;
  - **validaciones:** inicio < fin, sin cruces entre franjas del mismo día, y cada franja dentro del horario de ese día; un día cerrado no admite franjas.
- **`GET /api/v1/disponibilidad`** (gestión): las franjas de todo el personal, con su nombre.
- **`GET /api/v1/local/atencion`** (pública): `[{ diaSemana, nombreDia, franjas: [{ desde, hasta }] }]`. Es la **unión** de las franjas de los empleados activos, sin nombres ni identificadores.

## Mantenedores

Todos son de **gestión**, salvo las lecturas que ya usa el resto del personal: el listado de materiales en el editor de cotizaciones y el de productos en las solicitudes.

- **`/api/v1/categorias`:**
  - `GET` paginado y `GET /{id}` con ETag;
  - `POST`, `PUT /{id}` y `PATCH /{id}/activo` con `{ activo }`;
  - datos: `{ nombre (2–80, único), descripcion? (≤300) }`.
- **`/api/v1/productos`** (tipos de letrero): igual que categorías.
  - Datos: `{ idCategoria (activa), nombre (2–100, único por categoría), descripcion? (≤500), precioBaseRef? (entero ≥ 0) }`.
- **`/api/v1/materiales`:**
  - `GET` paginado (todo el personal) y `GET /{id}`;
  - `POST`, `PUT /{id}` y `PATCH /{id}/activo`;
  - datos: `{ nombre (2–100, único), unidadMedida (1–20), costoUnitarioAprox (entero ≥ 0) }`.
- **`/api/v1/catalogo`** (publicaciones):
  - `GET` paginado: para gestión, todas; para el público sigue la regla de la fase 5;
  - `POST` y `PUT /{id}`: `{ idProducto, titulo, descripcion, idImagen?, anchoMm?, altoMm?, precioReferencial?, incluyeLogoCliente, visible }`;
  - `PUT /{id}/autorizacion-logo`, que ya existe;
  - `PATCH /{id}/visible`.
  - **Regla:** hacer visible una publicación con `incluyeLogoCliente` sin autorización responde **409 `logo_sin_autorizacion`**. La base ya lo impide con `ck_catalogo_logo`, y el error se traduce al mismo 409.
  - **Imagen:** se sube con la ruta de imágenes de la fase 4/5.
- **`/api/v1/empleados`:**
  - `GET` paginado (gestión), sin el hash de la contraseña;
  - `GET /{id}`;
  - `POST`: el alta pasa de `/auth/empleados` a aquí, con la misma reautenticación de quien da el alta;
  - `PUT /{id}`;
  - `PATCH /{id}/activo`;
  - datos: `{ rut (válido, único), nombre, apellido, telefono, email (único), rol, password (solo al crear) }`.
  - **Desactivar,** en una transacción: `activo = false`, se borran su rostro y sus descriptores (como en la fase 6/rostro) y se revocan todas sus sesiones.
  - **Reglas:**
    - nadie se desactiva ni se cambia el rol a sí mismo: 409 `propio`;
    - siempre queda al menos un `dueño` activo: 409 `ultimo_dueno`;
    - el rol debe ser uno de los 4 (RNFO08).
  - Para el editor de pedidos queda una lista corta de empleados activos `{ idEmpleado, nombre, rol }`, que puede ver todo el personal.
- **`/api/v1/clientes`:** `PUT /{id}` exige ahora `If-Match` (RNFP10).

**Rutas que se retiran:**
- los `DELETE` físicos de categorías, productos, catálogo y clientes;
- `/auth/empleados`;
- las respuestas que devolvían la entidad de EF.

Las pruebas que las usaban se adaptan.

## Frontend

**`/panel/perfil`, sección «Avisos del taller»** (solo para gestión; para los demás roles no se renderiza):
- **Lista de destinos:** muestra los activos y los pendientes, con la vigencia del enlace.
  - Acciones de cada uno: «Reemplazar», «Quitar» (desactivado con explicación si es el último), «Reenviar enlace» y «Cancelar cambio».
- **«Agregar correo».**
- **Respaldo:** si `usaRespaldo`, un aviso: «Hoy los avisos van al correo de la configuración del servidor. Agrega uno aquí para administrarlo desde el panel».
- **«Enviar correo de prueba»:** muestra el estado de cada destinatario, consultando cada 2 segundos hasta 60 segundos, con «Salió bien», «No salió: …» o «Sigue en cola».
- **Historial reciente.**
- **Encabezado** claro de configuración del taller, como dice el plan.

**`/avisos/confirmar?token=`:** página pública que confirma y muestra el resultado. No necesita sesión.

**`/panel/local`:**
- **Cartel:** todo el personal lo da vuelta.
- **Horario semanal:** editable por gestión, y de solo lectura para los demás.
- **«Mi disponibilidad»:** franjas por día, que se agregan o quitan.
- **Gestión** ve además la disponibilidad de todos.

**`/panel/mantenedores`:**
- Pestañas o subrutas: Categorías, Tipos de letrero, Vitrina, Materiales y Personal.
- Cada una con listado, búsqueda, filtro activo o inactivo y paginación en la dirección.
- Formulario con aviso de versión vieja sin perder lo escrito: se reusa `useConflictoDeVersion` de la fase 8.
- La interfaz dice «Administrar …», nunca «CRUD».
- **Vitrina:** subida de imagen, la casilla «Incluye el logo de un cliente» y la autorización. El interruptor de visible queda bloqueado, con explicación, si falta la autorización.
- **Personal:** alta con contraseña inicial escrita por quien la da, cambio de rol y desactivación con diálogo de confirmación: «Se borrará su acceso con rostro y se cerrarán sus sesiones».

**Portada:** muestra las franjas de atención de `GET /local/atencion` junto al horario.

**Navegación:** «Catálogo y mantenedores» deja de ser «Fase 9».
