# Tablero Kanban de la construcción

Una tarjeta por fase del plan (`plan-de-construccion.md`). Solo hay **una fase en curso a la vez**. Una fase pasa a "Hecho" cuando el usuario da su visto bueno al informe de cierre, que queda en `informes/`.

Última actualización: 13-11-2026 (fase 4 aprobada).

## Hecho

- **Fase 0 — Preparación del repositorio y documentos** · rama `fase-00-preparacion` · informe: `informes/fase-00.md` · aprobada el 12-11-2026
- **Fase 1 — Cimientos del backend y base demo** · rama `fase-01-cimientos-backend` · informe: `informes/fase-01.md` · aprobada el 12-11-2026
- **Ajuste por la revisión de la profesora guía** · rama `ajuste-revision-profesora` · aprobado el 12-11-2026 · requisitos en macro-requerimientos (versión 2, 159 identificadores), pedido con estado de pago independiente e historial de abonos (migraciones `SepararEstadoPedidoYPago` y `HistorialDePagos`, aplicadas en la base demo, no en la real), base de datos documentada en `docs/base-de-datos/`, plan ajustado para la revisión del lunes
- **Fase 2 — Sistema de diseño** · rama `fase-02-sistema-diseno` · informe: `informes/fase-02.md` · aprobada el 13-11-2026
- **Fase 3 — Estructura de la aplicación web y pantallas de la revisión** · rama `fase-03-estructura-y-pantallas` · informe: `informes/fase-03.md` · aprobada el 13-11-2026
- **Fase 4 — Servicios comunes: correo, consentimientos e imágenes** · rama `fase-04-servicios-comunes` · informe: `informes/fase-04.md` · aprobada el 13-11-2026

## En revisión (esperando el visto bueno del usuario)

_(ninguna)_

## En curso

_(ninguna)_

## Por hacer

| # | Fase | Depende de | Necesita del usuario |
|---|---|---|---|
| 5 | Portada pública | 3, 4 | WhatsApp, correo y teléfono del taller; fotos y permiso de logos |
| 6 | Cuentas y sesión | 3, 4 | Decidir sobre cuentas sin correo, si existen |
| 7 | Solicitudes y clientes | 6 | Confirmar los diagramas de estados |
| 8 | Cotizaciones, pedidos, pagos y abonos | 7 | Autorizar las migraciones `SepararEstadoPedidoYPago` y `HistorialDePagos` en la base real |
| 9 | Mantenedores y estado del local | 4, 6 | Decidir si diseñador y operario dan vuelta el cartel |
| 10 | Rostro según requisitos | 6, 9 | — |
| 11 | Notificaciones, auditoría y privacidad | 4, 8, 9 | — (la cuenta SMTP se pidió en la fase 4) |
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
