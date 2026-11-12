# Tablero Kanban de la construcción

Una tarjeta por fase del plan (`plan-de-construccion.md`). Solo hay **una fase en curso a la vez**. Una fase pasa a "Hecho" cuando el usuario da su visto bueno al informe de cierre, que queda en `informes/`.

Última actualización: 14-11-2026.

## Hecho

- **Fase 0 — Preparación del repositorio y documentos** · rama `fase-00-preparacion` · informe: `informes/fase-00.md` · aprobada el 12-11-2026

## En revisión (esperando el visto bueno del usuario)

- **Fase 1 — Cimientos del backend y base demo** · rama `fase-01-cimientos-backend` · informe: `informes/fase-01.md`

## En curso

_(ninguna)_

## Por hacer

| # | Fase | Depende de | Necesita del usuario |
|---|---|---|---|
| 2 | Sistema de diseño | 0 | — |
| 3 | Estructura de la aplicación web | 1, 2 | Probar su ingreso con rostro |
| 4 | Servicios comunes: correo, consentimientos e imágenes | 1 | — |
| 5 | Portada pública | 3, 4 | WhatsApp, correo y teléfono del taller; fotos y permiso de logos |
| 6 | Cuentas y sesión | 3, 4 | Decidir sobre cuentas sin correo, si existen |
| 7 | Solicitudes y clientes | 6 | Confirmar los diagramas de estados |
| 8 | Cotizaciones, pago y pedidos | 7 | — |
| 9 | Mantenedores y estado del local | 4, 6 | — |
| 10 | Rostro según requisitos | 6, 9 | — |
| 11 | Notificaciones, auditoría y privacidad | 4, 8, 9 | Cuenta SMTP del taller |
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
