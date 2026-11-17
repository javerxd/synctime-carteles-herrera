# Matriz de permisos por rol

Qué puede hacer cada rol en la API. La columna "Hoy" no es una descripción: la comprueba una prueba automática (`ContratoTests.ElAccesoDeclarado_CoincideConLoQueLaApiPermite`), que llama a cada operación como visitante, cliente, operario, diseñador y administrador y compara el resultado con `docs/api/openapi.json`.

Roles (RNFO08): `dueño`, `administrador`, `diseñador` y `operario` para el personal, y `cliente`.

- **Gestión** = dueño y administrador.
- **Trabajador**, en los requisitos, es quien atiende y produce. Aquí se propone que sean `diseñador` y `operario`; dueño y administrador pueden hacer todo lo que hace un trabajador.

## Estado actual, verificado

| Área | Visitante | Cliente | Operario | Diseñador | Gestión |
|---|---|---|---|---|---|
| Ver catálogo, categorías, productos, horario y estado del local | Sí | Sí | Sí | Sí | Sí |
| Ver el estado del local ahora y los documentos legales (`GET /local/estado`, `GET /documentos-legales`) | Sí | Sí | Sí | Sí | Sí |
| Enviar una consulta de contacto (`POST /contacto`) | Sí | Sí | Sí | Sí | Sí |
| Registrar la aceptación o el retiro de cookies (`POST /consentimientos/cookies`) | Sí | Sí | Sí | Sí | Sí |
| Enviar una solicitud en línea (`POST /mis/solicitudes`). El cliente debe tener el correo confirmado (`403` si no) | No | Sí | No | No | No |
| Registrarse como cliente, recuperar la contraseña y confirmar el correo (`/auth/registro-cliente`, `/auth/recuperar`, `/auth/restablecer`, `/auth/verificar-correo`) | Sí | — | — | — | — |
| Ingresar, renovar la sesión y cerrarla (`/auth/login`, `/auth/login-cliente`, `/auth/renovar`, `/auth/salir`) | Sí | Sí | Sí | Sí | Sí |
| Reenviar el enlace para confirmar el correo (`/auth/reenviar-verificacion`) | No | Sí | No | No | No |
| Ver y corregir su cuenta, cambiar su correo, su contraseña y su marketing (`/mi-cuenta`) | No | Sí | No | No | No |
| Ver sus propias solicitudes y pedidos (`/mis/…`) | No | Sí | No | No | No |
| Ver y responder (aprobar o rechazar) la cotización de una solicitud propia (`/mis/cotizaciones/{id}`). Lo ajeno responde `404` | No | Sí | No | No | No |
| Subir y quitar sus borradores de adjuntos (`/mis/adjuntos`). Subir exige correo confirmado | No | Sí | No | No | No |
| Ver un adjunto (`GET /adjuntos/{id}/miniatura` y `/archivo`). El cliente solo ve los suyos; el personal, todos | No | Sí (solo los suyos) | Sí | Sí | Sí |
| Listar y ver clientes, solicitudes, cotizaciones, pedidos y materiales, y la lista corta del personal activo (`GET /empleados/activos`) | No | No | Sí | Sí | Sí |
| Registrar y modificar clientes; asignarles contraseña | No | No | Sí | No | Sí |
| Registrar en el local, editar y adjuntar archivos a una solicitud (`POST /solicitudes`, `PUT /solicitudes/{id}`, `POST /solicitudes/{id}/adjuntos`) | No | No | Sí | Sí | Sí |
| Rechazar, devolver a «solicitado» y eliminar de forma lógica una solicitud (`PATCH /solicitudes/{id}/estado` y `/eliminar-logico`) | No | No | Sí | Sí | Sí |
| Borrar una solicitud con `DELETE /solicitudes/{id}` (borrado lógico, con la versión vista) | No | No | No | No | Sí |
| Emitir y editar cotizaciones, y registrar la respuesta que el cliente dio por teléfono, WhatsApp o en el local (fase 8) | No | No | Sí | Sí | Sí |
| Crear pedidos (desde una cotización aprobada o directos en el taller), editar sus datos sin cambiar los montos, avanzarlos, anular los que nunca tuvieron abonos y registrar abonos (fase 8) | No | No | Sí | Sí | Sí |
| Anular un abono propio registrado en las últimas 24 horas (`POST /pedidos/{id}/pagos/{idPago}/anular`) | No | No | Sí | Sí | Sí |
| Cambiar los montos de un pedido ya creado (`montoBaseTrabajo`, `costoInstalacionAdicional` en `PUT /pedidos/{id}`; queda en el historial del pedido) | No | No | No | No | Sí |
| Anular cualquier abono, y anular un pedido que tenga o haya tenido abonos (vigentes o anulados) | No | No | No | No | Sí |
| Ver las alertas de entregas y cobranza (`GET /pedidos/alertas`) | No | No | No | No | Sí |
| Eliminar cotizaciones y pedidos (borrado lógico, con la versión vista). Desde la fase 9 no hay borrado de clientes, categorías, productos ni publicaciones: se desactivan | No | No | No | No | Sí |
| Mantener categorías, productos, publicaciones y materiales (crear, editar y activar o desactivar, con la versión vista) | No | No | No | No | Sí |
| Mantener el personal (`/empleados`): listar, dar de alta, editar, cambiar el rol y desactivar. Dar de alta y cambiar el correo o el rol piden además la contraseña de quien lo hace | No | No | No | No | Sí |
| Ver y cambiar los destinos de los avisos del taller y enviar la prueba (`/avisos-taller`). Agregar, reemplazar y quitar piden además la contraseña de quien lo hace | No | No | No | No | Sí |
| Confirmar un destino de avisos desde el enlace del correo (`POST /avisos-taller/confirmar`) | Sí | Sí | Sí | Sí | Sí |
| Cambiar el horario semanal (`PUT /horarios`) y ver la disponibilidad de todos (`GET /disponibilidad`) | No | No | No | No | Sí |
| Registrar su propia disponibilidad (`/mi-disponibilidad`) | No | No | Sí | Sí | Sí |
| Ver las franjas de atención de la portada (`GET /local/atencion`) | Sí | Sí | Sí | Sí | Sí |
| Autorizar el logotipo de un cliente en una publicación (`PUT /catalogo/{id}/autorizacion-logo`) | No | No | No | No | Sí |
| Subir imágenes (`POST /imagenes`) | No | No | No | No | Sí |
| Ver una imagen subida (`GET /imagenes/{archivo}`) | Sí | Sí | Sí | Sí | Sí |
| Cambiar el estado del local (dar vuelta el cartel, `PUT /cartel-digital`) | No | No | Sí | Sí | Sí |
| Ver la métrica de conversión | No | No | No | No | Sí |
| Ver el dashboard: ingresos, pedidos, cobranza, embudo, demanda y pedidos por cobrar (`GET /indicadores/*`, fase 12B). Cliente, operario y diseñador reciben `403`; sin sesión, `401` | No | No | No | No | Sí |
| Activar su propio acceso con rostro (`PUT /auth/rostro`, `POST /auth/rostro/equipo`; los demás reciben `403 solo_gestion`) | No | No | No | No | Sí |
| Desactivar su propio acceso con rostro (`DELETE /auth/rostro`) | No | No | Sí | Sí | Sí |
| Ver los rostros registrados y registrar o desactivar el de otro empleado de gestión (`/rostros`) | No | No | No | No | Sí |
| Calibrar el umbral del rostro (`/rostros/calibracion`, solo con `RostroFacial:Calibracion:Activa`) | No | No | No | No | Sí |
| Elegir qué avisos del taller recibe cada destino (`PUT /avisos-taller/destinos/{id}/preferencia`) | No | No | No | No | Sí |
| Ver y reenviar los correos no enviados (`/correos/no-enviados`) | No | No | No | No | Sí |
| Consultar la bitácora de auditoría y los consentimientos (`/auditoria`) | No | No | No | No | Sí |
| Darse de baja de los correos opcionales con el enlace (`/correos/baja`) | Sí (con el enlace) | Sí | No | No | No |
| Descargar sus datos y dar de baja su cuenta (`/mi-cuenta/datos`, `/mi-cuenta/baja`) | No | Sí | No | No | No |

## Diferencias con los requisitos

Lo que hoy permite la API y lo que piden los requisitos no coincide en estos puntos. Cada uno se corrige en la fase indicada.

| # | Requisito | Hoy | Debe quedar | Fase |
|---|---|---|---|---|
| 1 | RNFO03, RFU16: solo el personal cambia el estado del local | Resuelto en la fase 9 (decisión 1 del 14-11-2026): todo el personal da vuelta el cartel; el horario sigue siendo de gestión | Todo el personal | 9 |
| 2 | RFU11: el trabajador consulta, modifica, **rechaza y elimina** solicitudes | Resuelto en la fase 7: todo el personal rechaza, devuelve y elimina de forma lógica (`PATCH`). `DELETE /solicitudes/{id}` sigue siendo solo de gestión | Todo el personal (el `DELETE`, solo gestión) | 7 |
| 3 | RFU12: el trabajador cotiza | Resuelto en la fase 8: todo el personal emite, edita y registra la respuesta del cliente. Eliminar sigue siendo de gestión | Todo el personal | 8 |
| 4 | RFU13, RFU14: el trabajador confirma el pago, crea el pedido y registra su avance | Resuelto en la fase 8: todo el personal crea pedidos, los avanza y registra abonos. Cambiar los montos de un pedido, anular un pedido con abonos (aunque estén anulados), anular abonos ajenos o de más de 24 horas y eliminar son de gestión (decisiones 2 y 4 del 14-11-2026 y revisión de seguridad); el resto del personal recibe `403 solo_gestion` | Todo el personal | 8 |
| 5 | Decisión del 12-11-2026: el rostro es solo para dueño y administrador | Resuelto en la fase 10: solo gestión lo activa; los demás roles reciben el `401` genérico al ingresar y `403 solo_gestion` al activar | Solo gestión | 10 |
| 6 | `POST /auth/rostro/equipo` | Resuelto en la fase 10: responde `403 solo_gestion` a los roles que no son de gestión | Rechazo con 403 en la propia ruta | 10 |

## Tres decisiones confirmadas

**El usuario aceptó las tres propuestas el 12-11-2026.** Rigen desde las fases de negocio. Cambiarlas después es posible, pero afecta a los permisos de todas las fases siguientes.

1. **¿Dueño y administrador pueden lo mismo?**
   Propuesta: **sí, exactamente lo mismo**, como hoy. Los requisitos hablan del "dueño" para mantenedores, empleados, reportes y auditoría, y RFS26 trata a ambos roles como "dueño".
   Alternativa: reservar solo al `dueño` la gestión de empleados y roles, la auditoría y la desactivación de rostros ajenos.

2. **¿Diseñador y operario pueden lo mismo?**
   Propuesta: **sí**. Los requisitos nombran un único "trabajador" para solicitudes, cotizaciones, pagos, pedidos y entregas (RFU10 a RFU16). Hoy están repartidos: el diseñador cotiza y el operario maneja clientes y pedidos.
   Alternativa: mantener el reparto actual, lo que dejaría sin cumplir RFU12 y RFU13 para uno de los dos roles.

3. **¿Quién puede eliminar registros?**
   Propuesta: el trabajador puede eliminar **solicitudes** (lo pide RFU11), siempre como borrado lógico y con registro en la auditoría. Eliminar clientes, cotizaciones y pedidos sigue siendo solo de gestión, y también pasa a ser borrado lógico (fases 7 y 8).
