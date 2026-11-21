# Tablero Kanban de la construcción

Una tarjeta por fase del plan (`plan-de-construccion.md`). Solo hay **una fase en curso a la vez**. Una fase pasa a "Hecho" cuando el usuario da su visto bueno al informe de cierre, que queda en `informes/`.

Última actualización: 21-11-2026 (fases 13A y 13B aprobadas).

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
- **Fase 11 — Notificaciones, auditoría y privacidad** · rama `fase-11-notificaciones-auditoria-y-privacidad` · informe: `informes/fase-11.md` · aprobada el 16-11-2026 · plazo de la IP en la bitácora decidido el 16-11-2026: 90 días (purga en la 12B)

- **Fase 12A — Dashboard: contrato y frontend** · rama `fase-12a-dashboard-frontend` · informe: `informes/fase-12a.md` · aprobada el 16-11-2026 · el usuario autorizó subir la política de privacidad a 1.1 en la 12B
- **Fase 12B — Backend de indicadores, vistas SQL, conexión real y catálogo real** · rama `fase-12b-indicadores-backend` · informe: `informes/fase-12.md` (cubre la 12A y la 12B) · aprobada el 20-11-2026 · catálogo real del taller, política de privacidad 1.1, migraciones `CatalogoRealDelTaller` e `IndicadoresDashboard` aplicadas en la demo (no en la real)

- **Fase 12C — Portal público: mapa, carteles, barra superior, vitrina e ingreso** · rama `fase-12c-portal-publico` · informe: `informes/fase-12c.md` · aprobada el 20-11-2026 · sin migración; solo frontend

- **Fase 12D — Paneles, cuentas, validaciones y catálogo editable** · rama `fase-12d-paneles-y-cuentas` · informe: `informes/fase-12d.md` · aprobada el 20-11-2026 · sin migración ni cambios de permisos; endpoint nuevo `PUT /productos/{id}/medidas-y-opciones`; incluye el ajuste del zoom de la vitrina

- **Fase 13A — Backend de reportes y exportaciones** · rama `fase-13a-reportes-backend` · informe: `informes/fase-13a.md` · aprobada el 21-11-2026 · margen = ingresos − materiales − otros costos · migración `ExportacionesDeReportes` aplicada solo en la demo
- **Fase 13B — Frontend de reportes y exportaciones** · rama `fase-13b-reportes-frontend` · informe: `informes/fase-13b.md` · aprobada el 21-11-2026 · margen = ingresos − materiales − otros costos, con la columna «Otros costos» en el detalle · impresión de reportes en A4 apaisado

## En revisión (esperando el visto bueno del usuario)

Nada por ahora.


## Plan aprobado, construcción sin iniciar


## En curso

Nada por ahora.

## Por hacer

| # | Fase | Depende de | Necesita del usuario |
|---|---|---|---|
| 13 | Reportes y exportaciones | 11, 12, 12D | — |
| 14 | Ingreso con Google (sin asistente de IA) | 6, 7, 9 | ID de cliente OAuth de Google |
| 15 | Calidad: rendimiento, accesibilidad y compatibilidad (Chrome, Edge y Brave) | 13, 14 | Prueba con NVDA |
| 16 | Contenedores, CI/CD, nube y operación (Docker local, Vercel, Render y Azure SQL) | 15 | Permiso para subir la rama; ejecutar la verificación en un Codespace; claves de cada plataforma |
| 17 | Cierre y verificación de trazabilidad | 16 | — |

## Reglas del tablero

- Cada fase se trabaja en su rama `fase-NN-nombre`, que nace de la rama aprobada de la fase anterior.
- Al cerrar una fase se hace commit en su rama. No se fusiona a `main` ni se sube a GitHub hasta que el usuario lo pida.
- Si una fase se divide, se agrega su tarjeta aquí y se anota el cambio con fecha en el plan.
