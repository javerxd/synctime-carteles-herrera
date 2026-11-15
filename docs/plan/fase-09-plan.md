# Plan de la fase 9 — Mantenedores y estado del local

Fecha: 14-11-2026 · Rama: `fase-09-mantenedores-y-local` (nace de `fase-08-cotizaciones-y-pedidos`) · Estado: **aprobado el 14-11-2026** · especificación: `fase-09-especificacion.md`

Requisitos del plan general: RFS07, RFS02 (edición del horario y del cartel), RFS01 (autorización de logos), RNFP32, RNFO03, RNFO08, RNFP10, RFU16–RFU18, RFU20, E10–E15. Se suma tu pedido del 14-11-2026: **cambiar el correo que recibe los avisos de solicitudes nuevas**, desde el perfil.

## Lo que ya existe

- **Rutas antiguas:** categorías, productos (tipos de letrero), publicaciones y clientes tienen rutas que leen y escriben la base directo, sin control de versión y con borrado físico. Materiales y empleados solo tienen un listado, y el alta de empleado vive en `/auth/empleados`. Se reescriben como en las fases 7 y 8: servicio, reglas, contrato y pruebas.
- **Publicaciones:** desde la fase 5 tienen imagen y la regla de la base que impide mostrar un logo sin autorización (`ck_catalogo_logo`).
- **Horario y cartel:** el horario solo se lee. El cartel solo lo pueden dar vuelta el dueño y el administrador.
- **Avisos del taller:** solo se leen de `Correo:AvisosTaller`, en `DestinatariosAvisosDesdeConfiguracion`. Una prueba de arquitectura comprueba que es el único lugar que lo lee.
- **Base real:** sigue sin migraciones. Las recibe todas al terminar las fases.

## 1. Avisos del taller (tu pedido del 14-11-2026)

**Dónde se ve:**
- **Ubicación:** una sección **«Avisos del taller»** en el perfil (`/panel/perfil`).
- **Quién la ve y la cambia:** solo el dueño y el administrador. Los demás roles no la ven, y la API les responde **403** tanto al leerla como al cambiarla.
- **Configuración del taller, no del usuario:** queda claro con un título aparte, otro fondo y el texto «Configuración del taller: estos correos reciben los avisos de solicitudes nuevas para todo el equipo. No cambia tu correo de ingreso».

**Destinos:**
- **Cuántos:** uno o más correos, guardados en la base (tabla nueva `destinatarios_avisos`).
- **Estados de cada destino:**
  - «pendiente de confirmar»;
  - «activo»;
  - «retirado»: se conserva para el historial.
- **Precedencia:** el servicio de destinatarios de la fase 4 usa primero los **activos** de la base. Solo si no hay ninguno usa `Correo:AvisosTaller`, que queda como respaldo. Sigue siendo el único lugar que conoce esa clave.

**Agregar o cambiar un correo:**
- **Confirmación:** se envía a la dirección nueva un correo con un **enlace de un solo uso**. El enlace se guarda solo como hash, vence en 24 horas y sale desde la casilla automática. Hasta confirmar, la dirección nueva no recibe avisos.
- **Cambiar** es reemplazar una dirección por otra: **la anterior sigue recibiendo los avisos hasta que la nueva se confirma**. Si nunca se confirma, no cambia nada, así que un error de tipeo no deja al dueño sin avisos.
- **Al confirmar:** la nueva pasa a «activo» y, si reemplazaba a otra, esa pasa a «retirado».
- **Aviso del cambio:** se envía a la dirección anterior: «El destino de los avisos de Letreros Herrera cambió». El correo no muestra la dirección nueva completa, solo enmascarada, por ejemplo «c•••@gmail.com».
- **Otras acciones:** se puede reenviar el enlace o cancelar un cambio pendiente.
- **Quitar un destino activo:** solo si queda al menos otro activo (decisión 3). La dirección quitada recibe el aviso del cambio.

**«Enviar correo de prueba»:**
- **Qué hace:** encola un aviso de ejemplo («Nueva solicitud #0 - Prueba del taller», marcado como prueba) a las direcciones activas, o al respaldo si no hay ninguna.
- **Sin bloquear la petición:** responde de inmediato. La pantalla muestra «Enviando…» y consulta el estado hasta que dice **«Salió bien a …»** o **«No salió: …»**, con el motivo que entrega el servidor de correo.
- **Límite:** propio, de 3 pruebas cada 10 minutos. No gasta el tope de 20 avisos por hora.

**Registro:**
- **Qué se registra:** cada acción (agregar, reemplazar, confirmar, cancelar, quitar, reenviar y probar) queda en una tabla de historial con quién, cuándo y qué dirección (enmascarada).
- **Dónde se ve:** la sección muestra los últimos cambios.
- **Relación con la fase 11:** la bitácora general de esa fase podrá leer esta tabla.

**Pruebas:**
- permisos por rol: dueño y administrador sí, diseñador, operario y cliente reciben 403;
- la confirmación de la nueva dirección y el enlace de un solo uso;
- la anterior sigue activa hasta que la nueva se confirme;
- el valor guardado reemplaza a `Correo:AvisosTaller`, que vuelve a usarse si no hay activos;
- el correo de prueba usa la cola y la petición responde sin esperar el envío;
- el aviso a la dirección anterior.

## 2. Estado del local y horario (RFS02, RNFO03, RFU16, E10, E11)

- **Cartel** (abierto o ausente, mensaje y regreso estimado): **todo el personal** lo da vuelta, como piden RFU16 («Trabajador») y RNFO03 («solo el personal»). Era la decisión pendiente del tablero (decisión 1).
- **Horario** (por día: si abre, apertura y cierre): solo el dueño y el administrador (E10). Valida que el cierre sea posterior a la apertura.
- **Registro:** los dos guardan quién hizo el último cambio y cuándo, y la portada sigue mostrando el estado sin datos del empleado.

## 3. Disponibilidad del personal (RFU20)

- **Qué registra cada empleado:** sus franjas de atención por día de la semana, por ejemplo «lunes 9:00–13:00».
- **Para los clientes:** la portada muestra las **franjas de atención** de cada día, la unión de las franjas de todo el personal, **sin nombres ni datos personales**.
- **Límites:** las franjas deben caer dentro del horario del taller y no pueden repetirse ni cruzarse para la misma persona.

## 4. Mantenedores (RFS07, RFU17, RFU18, E12–E15, RNFP32, RNFP10)

**Pantallas:** en `/panel/mantenedores` (la interfaz dice «Administrar», nunca «CRUD»). Cada mantenedor tiene búsqueda, filtros, orden y paginación en la API, y aviso si otra persona cambió el registro mientras lo editabas.

- **Categorías y tipos de letrero (E13):** nombre, descripción, categoría y precio base referencial. Si ya se usan, no se borran: se desactivan.
- **Publicaciones de la vitrina (E12):**
  - producto, título, descripción, imagen (con el procesamiento de la fase 4), medidas, precio referencial, autorización del logo y visibilidad;
  - **una publicación con logo sin autorización no se puede hacer visible**: la API responde 409 y la base lo impide.
- **Materiales (E14):** nombre, unidad de medida y costo unitario aproximado. El editor de cotizaciones de la fase 8 sugiere ese costo. Si ya se usan, se desactivan.
- **Personal (E15, RFU18, RNFO08):**
  - **Datos:** RUT, nombre, apellido, teléfono, correo, rol (dueño, administrador, diseñador u operario) y contraseña inicial. La contraseña la escribe quien da el alta, no el sistema.
  - **Acciones:** editar, cambiar el rol y desactivar.
  - **Al desactivar:** en un solo paso se borra su rostro y se cierran sus sesiones.
  - **Reglas:** nadie se desactiva ni se quita el rol de gestión a sí mismo, y siempre queda al menos un dueño activo.
- **Quién administra:** solo el dueño y el administrador.
- **Control de versión:** se agrega `row_version` a categorías, productos, publicaciones, materiales, empleados y clientes. Completa RNFP10; la ficha de clientes de la fase 7 deja de ser la excepción.

## 5. Migración `MantenedoresYAvisos` (solo agrega; demo, no la real)

- **Tablas nuevas:**
  - `destinatarios_avisos`: correo, estado, a quién reemplaza, hash del enlace, vencimiento, quién lo pidió y cuándo se confirmó;
  - `destinatarios_avisos_historial`;
  - `disponibilidad_empleados`.
- **Columnas nuevas:**
  - `row_version` en los seis mantenedores;
  - quién y cuándo en el horario.
- **Comprobaciones:** subir, revertir y volver a subir en una base temporal. Revertir se detiene si eso borraría datos. Se regenera el script de la base.

## 6. Pruebas y cierre

- **Mantenedores:** cada uno probado con sus roles (dueño y administrador sí; diseñador, operario y cliente no).
- **Personal:** desactivar a un empleado borra su rostro y cierra su sesión en el mismo paso. No se puede dejar el taller sin dueño.
- **Vitrina:** una publicación con logo sin autorización no se puede hacer visible, ni por la API ni en la base.
- **Cartel:** el operario y el diseñador lo dan vuelta, y la portada lo refleja.
- **Disponibilidad:** la portada muestra las franjas sin nombres.
- **Avisos del taller:** las pruebas de la sección 1. Además, de punta a punta en la app: cambiar el destino, confirmar desde la bandeja local y ver que la siguiente solicitud avisa a la dirección nueva.
- **Pruebas no vacías:** quitar el permiso de gestión, la confirmación antes de activar o la regla del último dueño hace fallar su prueba.
- **Diseño:** ui-ux-pro-max e impeccable en los mantenedores y en la sección de avisos; Playwright con axe a 360, 768 y 1440 px.
- **Revisiones de ECC:** seguridad y C#, React y accesibilidad, y base de datos.
- **Al cerrar:** contrato y permisos (`docs/api`), diccionario, modelo ER, trazabilidad, tablero e informe al día.

## Decisiones del usuario (14-11-2026)

1. **Cartel:** lo da vuelta todo el personal (dueño, administrador, diseñador y operario), como piden RFU16 y RNFO03.
2. **Disponibilidad:** franjas semanales que se repiten cada semana, sin excepciones por fecha.
3. **Último destino de avisos:** no se puede quitar; siempre queda al menos uno activo. Para cambiarlo se reemplaza. `Correo:AvisosTaller` solo se usa si nunca se configuró ninguno en la base.
4. **Agregar un destino adicional:** los destinos activos reciben «Se agregó un destino a los avisos del taller».
