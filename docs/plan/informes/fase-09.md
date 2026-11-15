# Informe de cierre — Fase 9: mantenedores y estado del local

Fecha: 15-11-2026 · Rama: `fase-09-mantenedores-y-local` (nace de `fase-08-cotizaciones-y-pedidos`) · Estado: **aprobada el 15-11-2026**

Plan aprobado: `fase-09-plan.md`, con tus cuatro decisiones del 14-11-2026, y su especificación en `fase-09-especificacion.md`. Incluye tu pedido: **cambiar desde el perfil el correo que recibe los avisos de solicitudes nuevas**.

## Qué se hizo

### Avisos del taller (tu pedido)
- **Dónde se ve:** una sección **«Avisos del taller»** en `/panel/perfil`, marcada como «Configuración del taller», con el texto «No cambia tu correo de ingreso».
  - Solo la ven el dueño y el administrador. A los demás roles no se les muestra, y la API les responde 403 si intentan leerla o cambiarla.
- **Destinos:** uno o más correos, guardados en la base.
  - El servicio de destinatarios de la fase 4 usa los activos de la base. `Correo:AvisosTaller` queda como respaldo cuando no hay ninguno.
- **Agregar o reemplazar un correo:**
  - La dirección nueva recibe un **enlace de un solo uso**: vence en 24 horas y se guarda solo como hash.
  - **La anterior sigue recibiendo los avisos hasta que la nueva se confirma.** Si nunca se confirma, no cambia nada.
  - Al confirmar se avisa a la dirección anterior. Al agregar un destino, se avisa a los que ya estaban activos (decisión 4), y también al correo de la configuración cuando se deja de usar.
- **Quitar:** no se puede quitar el último correo activo (decisión 3). La dirección quitada recibe un aviso.
- **«Enviar correo de prueba»:**
  - pasa por la cola y responde de inmediato;
  - la pantalla consulta el estado y muestra «Salió bien» o «No salió: …»;
  - tiene un límite propio de 3 cada 10 minutos y no gasta el tope de avisos por hora.
- **Historial:** registra cada cambio con quién y cuándo, y las direcciones aparecen enmascaradas.
- **Por la revisión de seguridad:**
  - agregar, reemplazar o quitar un correo pide tu contraseña;
  - como máximo puede haber 3 correos esperando confirmación;
  - la página pública de confirmación no puede trabar el resto.

### Estado del local, horario y disponibilidad
- **Cartel:** lo da vuelta **todo el personal** (decisión 1). Queda registrado quién y cuándo.
- **Horario semanal:** lo edita el dueño o el administrador, y queda registrado quién y cuándo.
- **«Mi disponibilidad»** (decisión 2):
  - cada persona registra sus franjas semanales;
  - no se cruzan y quedan dentro del horario.
  - La portada muestra las **franjas de atención** de todo el equipo, sin nombres.

### Mantenedores (`/panel/mantenedores`)
- **Pantallas:** categorías, tipos de letrero, vitrina, materiales y personal, con búsqueda, filtro de activos y paginación.
- **Edición:**
  - si otra persona cambió el registro mientras lo editabas, se avisa sin perder lo escrito;
  - no hay borrado físico: se desactiva.
- **Vitrina:**
  - una publicación con logo sin autorización **no se puede hacer visible**, ni por la API ni en la base;
  - la imagen se sube y queda enlazada.
- **Personal:**
  - en el alta, la contraseña inicial la escribe quien da el alta;
  - se puede editar y cambiar el rol;
  - **desactivar borra su rostro y cierra sus sesiones en un solo paso**;
  - nadie se desactiva a sí mismo y siempre queda al menos un dueño activo;
  - cambiar el correo o el rol de otra persona pide tu contraseña, cierra sus sesiones y avisa a su correo anterior.
- **Clientes:** al editar se exige la versión vista. La API valida el RUT y lo guarda normalizado.
- **Control de versión:** se agregó a los seis mantenedores (RNFP10).

### Migración `MantenedoresYAvisos`
- **Qué agrega:** versión en los mantenedores, quién y cuándo en el horario, y tres tablas nuevas: disponibilidad, destinos de avisos y su historial.
- **Comprobaciones:**
  - se probó subirla, revertirla y volver a subirla en una base temporal, que se borró después;
  - revertirla se detiene si eso borraría datos.
- **Dónde está aplicada:** en la demo. **No en la real**, que sigue con 3 migraciones (le faltan 8).
- **Script:** se regeneró `LetreriaLosHerreraDB.sql`.

## Verificación

| Comprobación | Resultado |
|---|---|
| Backend | 354 unitarias y 369 de integración, en verde; compila sin advertencias |
| Frontend | 403 de Vitest y 175 de Playwright con axe (las pantallas nuevas a 360, 768 y 1440 px), en verde; lint sin errores; build correcto |
| Pruebas no vacías | Quitar el permiso de gestión de los avisos, activar un correo sin confirmarlo o la regla del último dueño hace fallar su prueba |
| **Avisos del taller en la app (demo)** | Con tu sesión de dueño:<br>• correo de prueba al respaldo: «Salió bien»;<br>• agregaste `avisos.nuevo@letreria.test` con tu contraseña y quedó pendiente;<br>• una prueba antes de confirmar siguió yendo al respaldo;<br>• confirmé desde el enlace de la bandeja local («Correo confirmado») y el enlace se borró de la dirección;<br>• usarlo de nuevo dio «El enlace no es válido o ya venció»;<br>• el correo de la configuración recibió «Se agregó un destino…»;<br>• la siguiente prueba llegó a la dirección nueva;<br>• el historial muestra cada paso, y «Quitar» explica que debe quedar uno activo. |
| Disponibilidad | Guardé una franja el lunes de 9:00 a 13:00, y la portada muestra «Atención: 9:00 a 13:00», sin nombres |
| Corrección durante la prueba | La lista de cambios no se actualizaba tras enviar la prueba hasta recargar la página. Ya se actualiza sola |

## Revisión de ECC

**Seguridad, C# y base de datos del backend:** ninguno crítico. Se corrigieron, entre otros:
- **(alto)** un administrador podía cambiar el correo de un dueño y luego recuperar su contraseña. Ahora pide la contraseña de quien hace el cambio, cierra las sesiones de esa persona, anula sus códigos y avisa al correo anterior;
- cambiar los avisos sin confirmar la contraseña;
- dejar el correo de la configuración sin avisar;
- la confirmación pública que podía trabar las demás operaciones;
- el envío ilimitado de enlaces;
- quitar un destino con un reemplazo pendiente;
- desactivar un cliente en dos pasos;
- datos nulos que daban error 500;
- el RUT del cliente sin validar en la API.

**React y accesibilidad del frontend:** ninguno crítico. Se corrigieron, entre otros:
- se perdía lo escrito al activar u ocultar un registro;
- se podía guardar mientras subía la imagen;
- el nombre accesible del interruptor de la vitrina;
- el foco tras enviar un enlace o quitar una franja;
- el anuncio de la subida;
- el token de confirmación, que podía viajar en el «Referer».

## Pendiente (en `desviaciones.md`)
- **Base real:** las 8 migraciones pendientes esperan al final de las fases.
- **Ficha de clientes:** avisa del conflicto de versión, pero no ofrece «guardar sobre la versión actual» como los mantenedores.
- **Listas de los mantenedores:** sin selector de orden; ordenan por nombre.
- **Tokens de acceso:** un empleado desactivado conserva hasta 15 minutos el token que ya tenía. No puede renovarlo.

## Lo que necesito de ti

1. **Tu visto bueno a esta fase**, o lo que quieras cambiar.
2. **Dueño y administrador** siguen con los mismos permisos (tu decisión del 12-11). Por eso un administrador puede dar o quitar el rol de dueño. Ahora eso pide su contraseña y avisa por correo, pero puedes preferir que **solo un dueño** pueda dar, quitar o desactivar a otro dueño.
