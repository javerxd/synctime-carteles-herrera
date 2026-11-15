# Plan de la fase 11 — Notificaciones, auditoría y privacidad

Fecha: 15-11-2026 · Rama: `fase-11-notificaciones-auditoria-y-privacidad` (nace de `fase-10-rostro-segun-requisitos`) · Estado: **propuesto con tus decisiones, esperando tu aprobación**

Requisitos del plan general: RFS08 (avisos al cliente y baja de correos), RFS12 (auditoría, portabilidad e inhabilitación), RNFO04, RNFE01, RFU09, RFU28, RFU34, E24, E25 (auditoría de abonos), S09, S20, S23. Se suman tus pedidos del 15-11-2026 sobre los **avisos del taller como correos operativos**.

## Antes de empezar (comprobado)

- **Fases de las que depende:** la 4 (cola de correo y consentimientos), la 7 (solicitudes y avisos al taller), la 8 (cotizaciones, pedidos y abonos) y la 9 (destinos de los avisos) están aprobadas. La rama sale de la 10, aprobada.
- **Lo que ya existe:**
  - la cola `correos_salientes` con estados `pendiente`, `enviando`, `enviado` y `fallido`, hasta 6 intentos y el último error clasificado (sin el texto del servidor);
  - el aviso al taller por cada solicitud **en línea**; las presenciales no avisan (decisión de la fase 4);
  - los destinos de los avisos en la base (fase 9) y el correo «Tu cotización #N está lista» (fase 8);
  - el consentimiento de marketing, que se acepta y se retira desde Mi cuenta (fase 6);
  - historiales propios de solicitudes, pedidos y abonos (fases 7 y 8), pero **no una bitácora general**.
- **Un hallazgo:** la cola **nunca se purga**: guarda para siempre el cuerpo de cada correo, que incluye datos de clientes. Se corrige en esta fase (sección 4).

## 1. Avisos del taller: correos operativos (tus pedidos)

Los avisos del taller (solicitud nueva, tope de solicitudes, consulta de contacto) son **correos operativos del negocio**, no comunicaciones al cliente:

- **La baja de un cliente no los toca.** La baja solo afecta a los correos opcionales de ese cliente (sección 2). Una prueba lo exige: un cliente dado de baja envía una solicitud y el taller recibe el aviso igual.
- **Preferencia de avisos, por cada correo destino** (decisión 1): cada destino elige si recibe las **solicitudes en línea, las presenciales o ambas**.
  - Agrega por primera vez el aviso de las solicitudes **presenciales**, que hoy no existe; viene apagado hasta que se elija.
  - Se edita en «Avisos del taller», en el perfil, solo por dueño y administrador, y queda en el historial de esa sección (quién y cuándo).
  - Un aviso que nadie quiere recibir no se encola.
- **Correos que no salieron:** una vista en el panel, «Correos no enviados», con los que quedaron `fallido` después de todos los reintentos.
  - Muestra el tipo («Aviso de solicitud nueva»), a qué se refiere (enlace a la solicitud #N), el destino enmascarado, la fecha, los intentos y el motivo en español.
  - Muestra **todos** los correos que fallaron (decisión 2): avisos del taller y correos a clientes, con un filtro por tipo.
  - **«Reenviar»** vuelve a encolar el correo y queda registrado quién lo pidió. Un aviso del taller va a los destinos activos de hoy, según su preferencia; los demás, a su misma dirección (decisión 3). Un correo se puede reenviar una vez por fallo; si vuelve a fallar, aparece de nuevo.
  - Un aviso en el inicio del panel cuando hay correos no enviados sin revisar, para que el dueño se entere sin buscar.
- **Auditoría de los avisos:** cada envío y cada fallo definitivo de un aviso queda en la bitácora (sección 3), con tipo, referencia (`solicitud:123`), destino enmascarado, intentos, motivo y fecha. **Nunca el asunto, el cuerpo ni la dirección completa.**

## 2. Correos al cliente y baja

- **Avisos de estado (RFS08, RFU09, S09):** el cliente recibe un correo cuando su solicitud se cotiza, se rechaza o se acepta y cuando su pedido cambia de estado (en proceso, terminado, entregado, anulado), con el número, el estado nuevo, un mensaje, la fecha y el **enlace de baja**.
  - Solo a un correo confirmado, como hoy. Se encolan en la misma transacción que el cambio.
  - Los presenciales sin correo no reciben nada.
- **Qué se puede dar de baja:**
  - **Nunca:** los correos de cuenta y seguridad (verificar el correo, recuperar la contraseña, aviso de cambio de correo). Sin ellos no se puede usar la cuenta.
  - **Siempre:** el marketing.
  - **Los avisos de estado:** también, con el enlace de baja (decisión 4).
- **Baja con enlace firmado:** abre una página de confirmación (no se ejecuta con solo abrir el enlace, porque algunos correos lo abren solos), funciona sin sesión y revoca de inmediato.
  - Cabeceras `List-Unsubscribe` y `List-Unsubscribe-Post` (baja con un clic desde Gmail).
  - El enlace no lleva el correo en claro ni sirve para otra cosa; vence si el cliente cambia de correo.
- **Preferencias en Mi cuenta:** el cliente activa o desactiva cada tipo de correo opcional. Cada cambio queda como consentimiento con fecha, hora y versión.

## 3. Bitácora de auditoría (RFS12, RFU28, S20, E25)

- **Inmutable:** tabla `auditoria` en la que la aplicación solo inserta; un trigger en la base rechaza `UPDATE` y `DELETE`.
- **Automática:** un interceptor de EF Core registra quién creó, modificó o eliminó solicitudes, cotizaciones, pedidos, abonos, catálogo, clientes, empleados, destinos de avisos y rostros: usuario, rol, entidad, identificador, acción, fecha y hora, y los **nombres** de los campos cambiados.
  - **Sin datos sensibles:** ni contraseñas, ni hashes, ni descriptores, ni tokens, ni cuerpos de correo. Una lista explícita de campos permitidos por entidad decide qué valores se muestran (por ejemplo el estado anterior y el nuevo); el resto se anota solo como «cambió».
  - Una prueba exige que toda entidad principal quede cubierta y prohíbe las escrituras que el interceptor no ve (`ExecuteUpdate`, `ExecuteDelete` y SQL directo sobre esas tablas), salvo las justificadas, que registran a mano.
- **Abonos (E25):** cada abono y cada anulación queda con quién, cuándo y cuánto.
- **Además:** los avisos del taller (sección 1), los reenvíos, las bajas de correo y de cuenta y las descargas de datos.
- **Visor para el dueño** (`/panel/auditoria`, gestión): filtros por fecha, usuario, entidad y acción; paginado; sin exportar todavía (las exportaciones son de la fase 13, que también registrará quién exportó).
- **Consentimientos:** consulta de quién aceptó los términos, la privacidad y el marketing, qué versión y cuándo (RFU28).
- **Conservación:** la bitácora se conserva al menos 24 meses (configurable). Las solicitudes rechazadas y eliminadas se conservan al menos 12 meses (RNFO04, ya cumplido por el borrado lógico; se agrega la prueba).

## 4. Privacidad: Mis datos, baja de cuenta y purga de la cola (RFS12, RFU34, RNFE01, E24, S23)

- **«Mis datos» en Mi cuenta:**
  - **Descargar** un archivo JSON con todos los datos personales e historial del cliente: cuenta, consentimientos, solicitudes con sus ítems y adjuntos (los nombres, no los archivos), cotizaciones, pedidos y abonos. Pide la contraseña.
  - **Dar de baja la cuenta** (inhabilitación, borrado lógico): pide la contraseña y confirmación.
    - Se conservan las solicitudes, pedidos y abonos (integridad histórica y contable).
    - El cliente deja de ingresar (se cierran sus sesiones) y de recibir correos.
    - Se borran la contraseña, los códigos y los tokens; se retira el marketing.
    - Se entrega un **comprobante** en pantalla y por correo, con la fecha.
    - Queda en la auditoría.
  - El texto final sigue pendiente de revisión legal (desviación ya registrada).
- **Purga de la cola de correo:** el cuerpo de los correos enviados se borra después de unos días (por ejemplo 30, configurable), y la fila queda con el tipo, la referencia, el estado y las fechas. Los `fallido` conservan el cuerpo hasta reenviarlos o hasta un plazo mayor (por ejemplo 90 días), para poder reenviarlos.

## 5. Migración `NotificacionesYAuditoria` (solo agrega; demo, no la real)

- `auditoria` (con el trigger que impide cambiarla o borrarla), las claves de idempotencia, la versión del horario y del cartel, la preferencia de avisos de cada destino, las preferencias de correo del cliente, la inhabilitación de la cuenta (fecha y comprobante) y los datos del reenvío en `correos_salientes` (quién y cuándo).
- Revertirla se detiene si borraría datos. Se regenera el script de la base.

## 6. Pruebas y cierre

**Pruebas primero:**
- un cliente dado de baja **sigue generando el aviso al taller**;
- la preferencia «solo en línea», «solo presenciales» y «ambas» encola exactamente los avisos que corresponden;
- un aviso que agota los reintentos aparece en «Correos no enviados», queda en la auditoría sin cuerpo ni dirección completa, y «Reenviar» lo vuelve a encolar una vez;
- cada cambio de estado encola su correo al cliente, con el enlace de baja y las cabeceras;
- el enlace de baja no actúa con solo abrirlo, funciona sin sesión, es de un solo propósito y no sirve alterado;
- la auditoría cubre toda entidad principal y no guarda campos sensibles; la tabla no se puede modificar;
- la descarga trae todos los datos del cliente y solo los suyos; la baja impide ingresar y recibir correos y conserva los pedidos;
- la purga borra los cuerpos enviados y conserva los fallidos.

**Pruebas no vacías:** se reintroducen fallos en una copia temporal (la baja del cliente silencia el aviso al taller, ignorar la preferencia, guardar el cuerpo en la auditoría, baja con solo abrir el enlace) y se comprueba que su prueba falla.

**Frontend:** Vitest de las pantallas nuevas (preferencia de avisos, correos no enviados con reenviar, baja, preferencias del cliente, mis datos, visor de auditoría) y Playwright con axe a 360, 768 y 1440 px.

**Revisiones:** `ecc:csharp-reviewer`, `ecc:security-reviewer`, `ecc:database-reviewer` y `ecc:react-reviewer`.

**Verificación en la app (demo, con la bandeja local):** un aviso de solicitud presencial según la preferencia; un aviso forzado a fallar que aparece en la vista y se reenvía; un cambio de estado que llega al cliente; la baja desde el enlace; la descarga y la baja de una cuenta de prueba.

**Al cerrar:** se actualizan la trazabilidad, el tablero, las desviaciones, `docs/api` y el informe `fase-11.md`.

## 7. Revisión de concurrencia e idempotencia (tu pedido)

Revisé las rutas que escriben (fases 1 a 10):

- **Ya con control de versión** (`If-Match` y `409 version_obsoleta`): solicitudes, cotizaciones, pedidos, catálogo, clientes, personal y los mantenedores. Los abonos no se editan: se anulan, con bloqueo en la base.
- **Sin control de versión:**
  - el horario y el cartel del local (pendiente de la fase 9: gana la última escritura);
  - los destinos de avisos (se protegen con bloqueos y reglas, pero no con versión);
  - Mi cuenta del cliente (la edita una sola persona; riesgo bajo).
- **Sin idempotencia:** crear una solicitud, una cotización, un pedido o un abono. Hoy la pantalla revisa antes de reintentar, pero un doble envío desde otra pestaña, o una respuesta perdida en la red, puede duplicar un registro (pendiente de la fase 8).

Lo que haría en esta fase:
- **Clave de idempotencia** (`Idempotency-Key`, generada por la pantalla al abrir el formulario) en `POST` de solicitudes, cotizaciones, pedidos y abonos. La misma clave con el mismo cuerpo devuelve la respuesta original sin crear otro; con otro cuerpo, `409`. Las claves se guardan 24 horas, por usuario, en una tabla nueva.
- **Versión** (`row_version` con `If-Match`) en el horario y el cartel, y en la preferencia de avisos nueva.
- **Las acciones nuevas de esta fase son idempotentes por diseño:** reenviar un correo solo actúa si sigue `fallido`; la baja y la confirmación de baja dan el mismo resultado si se repiten.
- **La ficha de clientes** ofrecerá «guardar sobre la versión actual», como los mantenedores (pendiente de la fase 9).
- **Pruebas:** dos envíos con la misma clave crean un solo registro, también en paralelo; dos ediciones del horario con la misma versión: la segunda recibe `409`.

## Decisiones del usuario (15-11-2026)

1. **Preferencia de avisos por cada correo destino:** cada destino de «Avisos del taller» elige si recibe las solicitudes en línea, las presenciales o ambas.
2. **«Correos no enviados» muestra todos los correos que fallaron:** los avisos del taller y los correos a clientes, con el destino enmascarado y un filtro por tipo.
3. **Reenviar un aviso del taller va a los destinos activos de hoy**, según su preferencia. Los demás correos se reenvían a su misma dirección.
4. **El cliente puede darse de baja de los avisos de estado** (no esenciales, RFU09). Los de cuenta y seguridad llegan siempre.
5. **Además, revisar la concurrencia y la idempotencia:** sección 7.
