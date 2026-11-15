# Tablero Kanban de la construcción

Una tarjeta por fase del plan (`plan-de-construccion.md`). Solo hay **una fase en curso a la vez**. Una fase pasa a "Hecho" cuando el usuario da su visto bueno al informe de cierre, que queda en `informes/`.

Última actualización: 15-11-2026 (fase 10 aprobada; la fase 11 empieza por su plan).

## Hecho

- **Fase 0 — Preparación del repositorio y documentos** · rama `fase-00-preparacion` · informe: `informes/fase-00.md` · aprobada el 12-11-2026
- **Fase 1 — Cimientos del backend y base demo** · rama `fase-01-cimientos-backend` · informe: `informes/fase-01.md` · aprobada el 12-11-2026
- **Ajuste por la revisión de la profesora guía** · rama `ajuste-revision-profesora` · aprobado el 12-11-2026 · requisitos en macro-requerimientos (versión 2, 159 identificadores), pedido con estado de pago independiente e historial de abonos (migraciones `SepararEstadoPedidoYPago` y `HistorialDePagos`, aplicadas en la base demo, no en la real), base de datos documentada en `docs/base-de-datos/`, plan ajustado para la revisión del lunes
- **Fase 2 — Sistema de diseño** · rama `fase-02-sistema-diseno` · informe: `informes/fase-02.md` · aprobada el 13-11-2026
- **Fase 3 — Estructura de la aplicación web y pantallas de la revisión** · rama `fase-03-estructura-y-pantallas` · informe: `informes/fase-03.md` · aprobada el 13-11-2026
- **Fase 4 — Servicios comunes: correo, consentimientos e imágenes** · rama `fase-04-servicios-comunes` · informe: `informes/fase-04.md` · aprobada el 13-11-2026
- **Fase 5 — Portada pública** · rama `fase-05-portada-publica` · informe: `informes/fase-05.md` · aprobada el 13-11-2026 · pendiente del usuario, sin bloquear: WhatsApp y teléfono definitivos, razón social, RUT y condiciones del abono
- **Fase 6 — Cuentas y sesión** · rama `fase-06-cuentas-y-sesion` · informe: `informes/fase-06.md` · aprobada el 14-11-2026
- **Fase 7 — Solicitudes y clientes** · rama `fase-07-solicitudes-y-clientes` · informe: `informes/fase-07.md` · aprobada el 14-11-2026 · incluye los diseños adjuntos y los avisos por correo de cada solicitud nueva
- **Fase 8 — Cotizaciones, pedidos, pagos y abonos** · rama `fase-08-cotizaciones-y-pedidos` · informe: `informes/fase-08.md` · aprobada el 14-11-2026 · pendiente del usuario, sin bloquear: decidir si la API lleva clave de idempotencia (`desviaciones.md`)
- **Fase 9 — Mantenedores y estado del local** · rama `fase-09-mantenedores-y-local` · informe: `informes/fase-09.md` · aprobada el 15-11-2026 · incluye los avisos del taller · el usuario confirmó (15-11-2026) que el administrador puede dar y quitar el rol de dueño
- **Fase 10 — Rostro según requisitos** · rama `fase-10-rostro-segun-requisitos` · informe: `informes/fase-10.md` · aprobada el 15-11-2026 · incluye la prueba de vida con giros, la calibración (umbral 0.45, calibrado por el usuario), el límite propio del desafío y el lado de cada muestra en el cifrado

## En revisión (esperando el visto bueno del usuario)

_(ninguna)_

## En curso

- **Fase 11 — Notificaciones, auditoría y privacidad** · rama `fase-11-notificaciones-auditoria-y-privacidad` (nace de `fase-10-rostro-segun-requisitos`) · incluye, por pedido del usuario (15-11-2026), los avisos del taller como correos operativos (preferencia por destino, «Correos no enviados» con reenviar, auditoría de los avisos) y la revisión de concurrencia e idempotencia · plan propuesto: `fase-11-plan.md`, esperando aprobación

## Por hacer

| # | Fase | Depende de | Necesita del usuario |
|---|---|---|---|
| 12 | Indicadores y dashboard | 8 | — |
| 13 | Reportes y exportaciones | 11, 12 | — |
| 14 | Google y asistente de IA | 6, 7, 9 | ID de cliente OAuth de Google; clave de IA |
| 15 | Calidad: rendimiento, accesibilidad y compatibilidad | 13, 14 | Prueba con NVDA; prueba en Safari real |
| 16 | Contenedores, CI/CD y operación | 15 | Permiso para subir la rama; ejecutar la verificación en un Codespace |
| 17 | Cierre y verificación de trazabilidad | 16 | — |

## Reglas del tablero

- Cada fase se trabaja en su rama `fase-NN-nombre`, que nace de la rama aprobada de la fase anterior.
- Al cerrar una fase se hace commit en su rama. No se fusiona a `main` ni se sube a GitHub hasta que el usuario lo pida.
- Si una fase se divide, se agrega su tarjeta aquí y se anota el cambio con fecha en el plan.
