# Informe de cierre — Fase 11: notificaciones, auditoría y privacidad

Fecha: 16-11-2026 · Rama: `fase-11-notificaciones-auditoria-y-privacidad` (nace de `fase-10-rostro-segun-requisitos`) · Estado: **en revisión, esperando tu visto bueno**

Plan aprobado: `fase-11-plan.md`, con tus cinco decisiones del 15-11-2026, y su especificación en `fase-11-especificacion.md`. Incluye tus pedidos sobre los **avisos del taller como correos operativos** y la **revisión de concurrencia e idempotencia**.

## Qué se hizo

### Avisos del taller: correos operativos (tus pedidos)
- **La baja de un cliente no los toca.** Ni la baja de sus correos ni la de su cuenta frenan el aviso al taller de sus solicitudes. Lo exigen dos pruebas.
- **Preferencia por cada correo destino** (decisión 1), en «Avisos del taller» del perfil: solicitudes en línea, presenciales o ambas.
  - Debe quedar marcada al menos una.
  - Se guarda con control de versión y queda en el historial de la sección.
  - El aviso de las **presenciales** es nuevo y viene apagado.
- **«Correos no enviados»** (`/panel/correos-no-enviados`, solo gestión) muestra **todos** los correos que fallaron tras los 6 intentos (decisión 2).
  - Cada uno trae el tipo, a qué se refiere (con enlace), el destino enmascarado, los intentos y el motivo. Nunca el contenido ni la dirección completa.
  - Se puede filtrar por tipo, fechas y revisión.
  - El inicio del panel avisa cuando hay correos sin revisar.
- **«Reenviar»** confirma antes a quién irá.
  - Un aviso del taller va a los **destinos activos de hoy**, según su preferencia (decisión 3). Los demás correos van a su misma dirección.
  - Funciona una sola vez por fallo, aunque se pulse dos veces a la vez.
  - Queda registrado quién lo pidió.
- **Auditoría de los avisos:** cada envío y cada fallo definitivo quedan en la bitácora con el tipo, la referencia, el destino enmascarado, los intentos y el motivo. **Sin el asunto, el cuerpo ni la dirección completa.**

### Correos al cliente y baja
- **Avisos de estado (RFS08, RFU09, S09):** el cliente recibe un correo cuando su solicitud se cotiza, se rechaza o se acepta, y cuando su pedido pasa a en proceso, terminado, entregado o anulado.
  - Cada correo trae el número, el estado nuevo, un mensaje, la fecha y el **enlace de baja**.
  - Solo llegan a correos confirmados y salen en la misma transacción que el cambio.
- **Qué se puede dar de baja** (decisión 4): los avisos de estado y el marketing. Los correos de cuenta y seguridad se envían siempre.
- **Enlace de baja:**
  - Abre una página que **no hace nada al cargar**: hay que pulsar «Confirmar baja».
  - Funciona sin sesión, saca el token de la dirección y no lo deja viajar a otros sitios.
  - Gmail además ofrece la baja con un clic (`List-Unsubscribe-Post`).
  - Es un token firmado que vence si el cliente cambia de correo o da de baja su cuenta.
- **«Correos que recibo»** en Mi cuenta: cada cambio queda como consentimiento, con fecha y versión.

### Bitácora de auditoría (RFS12, RFU28, S20, E25)
- **Tabla `auditoria`:** un trigger impide modificarla o borrarla.
- **Qué registra:** la llena un interceptor de EF Core con quién creó, modificó o eliminó solicitudes, cotizaciones, pedidos, abonos, catálogo, clientes, empleados, destinos de avisos, rostros, horario y cartel.
  - Siempre guarda los nombres de los campos cambiados. El valor solo aparece para una lista de campos permitidos, como estados, montos y fechas.
  - **Nunca** guarda contraseñas, hashes, tokens, descriptores ni cuerpos de correo.
- **Prueba de arquitectura:** exige que toda entidad principal esté cubierta y que no haya escrituras que el interceptor no vea.
- **Visor `/panel/auditoria`:** con filtros, el detalle legible y la sección «Consentimientos» (quién aceptó qué versión y cuándo).

### Privacidad (RFU34, RNFE01, E24, S23)
- **«Mis datos» en Mi cuenta:**
  - **Descargar mis datos:** con la contraseña, entrega un JSON con la cuenta, los consentimientos, las solicitudes, las cotizaciones, los pedidos y los abonos. Solo trae lo del cliente.
  - **Dar de baja mi cuenta:** con la contraseña y escribiendo «DAR DE BAJA».
    - La cuenta queda inhabilitada: se borran la contraseña, los códigos y las sesiones, y se apagan los correos opcionales.
    - Se conservan sus pedidos.
    - Entrega un comprobante `BAJA-{id}-{fecha}` en pantalla y por correo.
- **Cola de correo:**
  - **Enviados:** a los 30 días se vacían el asunto, los cuerpos y el enlace, y el destino queda enmascarado.
  - **Fallidos:** lo mismo a los 90 días, para poder reenviarlos mientras tanto.
  - La fila ya no se borra: queda como constancia.

### Concurrencia e idempotencia (tu pedido)
- **Revisión:** solicitudes, cotizaciones, pedidos, catálogo, clientes, personal y los mantenedores ya tenían control de versión.
- **Lo que se agregó:**
  - **`Idempotency-Key`** en crear solicitudes (del personal y del cliente), cotizaciones, pedidos (directo y desde una cotización) y abonos. Cada formulario genera su clave al abrirse y la repite en los reintentos.
    - **Misma clave y mismo cuerpo:** la misma respuesta, sin crear otro registro.
    - **Otro cuerpo:** responde 409.
  - **Control de versión** en el horario, el cartel y la preferencia de avisos.
  - **«Guardar sobre la versión actual»** en la ficha de clientes.
- **Pendientes resueltos:** dos de las fases 8 y 9.

### Migración `NotificacionesYAuditoria`
- **Qué agrega:**
  - las tablas `auditoria` (con su trigger) y `claves_idempotencia`;
  - las preferencias de los destinos;
  - la preferencia, la inhabilitación y su versión en `clientes`;
  - el reenvío y el enlace de baja en `correos_salientes`;
  - la versión del horario y del cartel.
- **Comprobaciones:**
  - se probó subirla, revertirla y volver a subirla en bases temporales, que se borraron después;
  - revertirla se detiene si eso borraría datos.
- **Dónde está aplicada:** en la demo, que quedó con **14 migraciones**. **No en la real**, que sigue con 3 (le faltan 11).
- **Script:** se regeneró `LetreriaLosHerreraDB.sql` y se comprobó contra una base creada con las migraciones.

## Verificación

| Comprobación | Resultado |
|---|---|
| Backend | 420 unitarias y 454 de integración, en verde; compila sin advertencias |
| Frontend | 592 de Vitest y 217 de Playwright con axe (pantallas nuevas a 360, 768 y 1440 px), en verde; lint sin errores; build correcto |
| Pruebas no vacías | Hacen fallar su prueba:<br>• que la baja del cliente silencie el aviso al taller (2);<br>• ignorar la preferencia de presenciales (2);<br>• guardar el cuerpo en la auditoría (2);<br>• dar de baja con solo abrir el enlace (1);<br>• reenviar sin la condición atómica (1);<br>• la idempotencia sin comparar el cuerpo (2) |
| **En la app, con tu sesión de dueño (demo, bandeja local)** | Ver el detalle debajo de la tabla |

**Lo que se probó en la app con tu sesión de dueño:**
- **Preferencia de avisos:**
  - sin versión: 400;
  - con las dos casillas apagadas: 400 «Elige al menos un tipo…»;
  - correcta: 200;
  - con una versión vieja: 409.
- **Solicitud presencial #20:** se creó con una clave de idempotencia y se envió dos veces a la vez. Quedó **una sola** solicitud; la segunda respuesta trajo `Idempotent-Replayed`, y otro cuerpo con la misma clave dio 409 `clave_reutilizada`.
- **El aviso de esa presencial:** llegó al destino que la pide, y la bitácora registró «Aviso enviado» sin el contenido y con el destino enmascarado.
- **Correos no enviados:** marqué ese aviso como fallido en la demo, porque en modo bandeja ningún correo falla de verdad.
  - El inicio del panel mostró «1 correo no se pudo enviar».
  - La vista lo listó, y «Reenviar» explicó que iría a los destinos de hoy. Lo reenvié y salió.
  - Un segundo reenvío dio 409 `ya_reenviado`.
  - La bitácora registró «Reenvió un correo» a tu nombre.
- **Correo de estado al cliente:**
  - Al rechazar la solicitud #21 de un cliente demo, llegó a la bandeja «Tu solicitud #21: Rechazada», con las cabeceras de baja.
  - Abrir su enlace no cambió nada en la base; «Confirmar baja» apagó los avisos de estado y quedó como consentimiento y en la bitácora.
- **Corrección hecha durante la prueba:** el registro automático de esa baja quedó a **tu** nombre, porque el navegador tenía tu sesión abierta. Ahora la baja desde el enlace siempre queda a nombre del cliente: tiene una prueba nueva, que primero falló y ahora pasa.

## Revisión de ECC

Ningún hallazgo crítico. Se corrigieron, entre otros:
- **C#, altos:**
  - reenviar un aviso de estado a un cliente que ya se dio de baja. Ahora responde 409 `cliente_no_recibe`;
  - con el token aún vigente tras la baja de la cuenta, se podía editar la cuenta. Ahora esas rutas lo revisan.
- **Base de datos, alto:** los índices del visor de auditoría. Además, un detalle demasiado largo queda marcado como truncado en vez de perderse.
- **Seguridad y C#, medios:**
  - doble clic en la baja de la cuenta;
  - errores al guardar la clave de idempotencia;
  - un tope de 256 KB en las rutas con clave;
  - `no-store` en la descarga de datos y en la baja;
  - marcar como revisado y desactivar el rostro, ahora en un solo paso con su registro.
- **React y accesibilidad:**
  - un segundo clic al emitir una cotización podía crear otra;
  - el filtro de datos sensibles en el detalle de la auditoría;
  - el foco tras el comprobante, el reenvío y marcar como revisado;
  - un `status` dentro de un `alert`;
  - los títulos de las páginas nuevas.

## Lo que no se pudo probar
- **Un fallo real del servidor de correo:** en modo bandeja no ocurre. El fallo se simuló en la demo, y el trabajador de la cola con un fallo real está cubierto por pruebas automáticas.
- **La baja con un clic desde Gmail real:** la cabecera está probada en el mensaje, no en Gmail.
- **«Mis datos» y la baja de la cuenta en la app:** necesitan la contraseña de un cliente demo, que no tengo ni debo escribir. Están cubiertos por pruebas de integración y de pantalla.
- **Un lector de pantalla real:** no se probó con uno.

## Pendiente (en `desviaciones.md`)
- **Exportaciones:** registrar quién exportó cada reporte llega con la fase 13.
- **IP en la bitácora:** falta decidir un plazo para borrarla. Es tu decisión.
- **Usuario de la base:** no debe tener permisos de dueño, para que nadie pueda desactivar el trigger de la bitácora (fase 16).
- **Base real:** las 11 migraciones pendientes esperan al final de las fases.

## Lo que necesito de ti
1. **Tu visto bueno a esta fase**, o lo que quieras cambiar.
2. Cuando quieras: **cuánto tiempo se guarda la IP** en la bitácora (por ejemplo, 12 meses).
