# Informe de cierre — Fase 13A: backend de reportes y exportaciones

Fecha: 20-11-2026 · Rama: `fase-13a-reportes-backend` (nace de `fase-12d-paneles-y-cuentas`) · Estado: **aprobada el 21-11-2026** (con la definición del margen de abajo); commit en la rama, sin push

Plan: `fase-13-plan.md` (las nueve decisiones, tal como las aprobaste). Migración: **`ExportacionesDeReportes`** (tabla `trabajos_exportacion`), aplicada **solo en la base demo**; la real la recibe al final con todas. Cambios de permisos: reportes y exportaciones nuevos (ver `permisos.md`).

## Qué se hizo

- **Tres reportes en la API**, todos con los pedidos anulados fuera de los totales y la misma definición del dashboard: `GET /api/v1/reportes/operativo` (todo el personal: pedidos —con los anulados marcados—, entregas y solicitudes), `…/gerencial` (dueño y administrador: resumen de ingresos, costos y margen con su comparación, ingresos y costos por tramo, ingresos por tipo de letrero y detalle de pedidos entregados, con dos gráficos) y `…/parametrizable` (dueño y administrador: pedidos filtrados con totales; con `estadoPago=por_cobrar` es la cobranza). Cada reporte responde **un documento único** que luego se exporta tal cual: lo que se ve es lo que sale.
- **Exportación en segundo plano (RNFP18):** `POST /reportes/{reporte}/exportaciones` responde `202`; `GET /reportes/exportaciones/{id}` da el estado (`pendiente`, `procesando`, `listo`, `fallido`) y `…/archivo` la descarga, solo para quien lo pidió (de un tercero, `404`), mientras no venza (24 horas; después `410`). Un servicio en segundo plano con el patrón de la cola de correo: hasta 2 trabajos a la vez, 120 s cada uno, un trabajo por persona (lo impone un índice único filtrado), 10 pedidos por minuto, reintento si el servidor se reinicia a mitad y purga de archivos vencidos y huérfanos.
- **Tres formatos:** Excel (hojas, encabezado en negrita y fijo, fechas y pesos como números con formato, totales con fórmula `SUM`/`SUMIF`, hoja «Filtros»), CSV (UTF-8 con marca, `;`, CRLF, fechas `aaaa-mm-dd`, defensa contra fórmulas) y PDF (A4 horizontal, texto seleccionable con Plus Jakarta Sans incrustada, encabezado repetido, «Página X de Y», gráficos del gerencial como imagen).
- **Bitácora:** acción nueva `exportar_reporte` («Exportó un reporte»): al pedirla (quién, rol, reporte, formato, filtros) y al terminar (filas) o fallar (motivo). Nunca el contenido.
- **Contrato y documentos:** `openapi.json` (8 rutas), `contrato.md`, `permisos.md`, `arquitectura.md` (flujo de trabajos), diccionario de datos, `licencias.md`, `desviaciones.md`, `trazabilidad.md` y `tablero.md`.

## Dependencias nuevas

| Paquete | Versión | Licencia | Dónde |
|---|---|---|---|
| DocumentFormat.OpenXml | 3.5.1 | MIT | API |
| PDFsharp-MigraDoc | **6.2.4** | MIT | API |
| PdfPig | 0.1.16 | Apache-2.0 | solo pruebas |

**La última de PDFsharp (6.5.0) está publicada como AGPL-3.0**, que obligaría a publicar el código del sistema; por eso quedó fijada la 6.2.4 (la última MIT). `licencias.md` avisa que no se actualice sin revisar la licencia. Sin paquetes vulnerables (`dotnet list package --vulnerable`).

## Pruebas

- **Unitarias: 674 de 674** (nuevas: `ReportesTests` —filtros, permisos por rol, nombres, tope, costos y margen, totales, formatos y CSV con sus escapes y los cuatro inicios de fórmula— y `ArchivosDeReportesTests`, que **reabre los archivos**: el `.xlsx` con Open XML y el PDF con PdfPig).
- **Integración (SQL Server) de `ReportesFase13aTests`: 52 de 52**, con datos conocidos de 2022 y cada cifra calculada a mano: matriz de roles completa en los tres reportes y en pedir la exportación (dueño, administrador, diseñador, operario, cliente y sin sesión); filtros inválidos con su error por campo; contenido de las tres tablas del operativo, la cobranza (por período y vigente), el gerencial (con el pedido «sin costo» y el redondeo de materiales); **coincidencia con el dashboard** (ingresos y saldo por cobrar iguales a los de `GET /indicadores/*`); ciclo completo (202, estado, descarga, vencimiento, 410, purga); un tercero recibe `404`; un trabajo por persona (también con 4 pedidos simultáneos); límites de 10 por minuto y de lectura; reinicio a mitad y trabajos interrumpidos o pendientes eternos; bitácora con quién, formato, filtros y filas; los tres formatos con los mismos pedidos; un nombre que empieza con `=` sale defendido en CSV y como texto en Excel.
- **Batería completa después de las correcciones de las revisiones: unitarias 674 de 674 e integración 580 de 580** (incluye el contrato contra la API, la arquitectura en capas y las migraciones).
- **Fallos a propósito (detectados):** (1) quitar la política de gestión del reporte gerencial → falla la matriz de roles; (2) quitar el escape de fórmulas del CSV → fallan 7 pruebas unitarias y la de integración del nombre con `=`; (3) cambiar el filtro de entregas por el de creación → fallan 6 pruebas de integración. Código restaurado.
- **Mediciones** (equipo de desarrollo): 10.000 filas: CSV 0,0 s, Excel 0,6 s (48 MB), PDF 9,5 s (273 MB). 50.000 filas: CSV 0,1 s, Excel 1,4 s (207 MB), **PDF 232 s y 1,3 GB**. De ahí el tope propio del PDF (abajo).

## Revisiones de ECC y qué se hizo con ellas

- **Seguridad:** 0 críticas, 0 altas. Permisos por reporte, archivos ajenos, rutas, fórmulas, filtros guardados y bitácora verificados sin hallazgo. **Corregido:** sin tope de archivos vivos por persona (llenaban el disco: ahora 5), cuerpo del POST sin límite (ahora 4 KB), los GET de reportes solo bajo el límite general (ahora 30 por minuto), el filtro guardado conservaba el texto crudo (ahora el valor normalizado), respuestas con datos sin `no-store` y un log con la excepción completa.
- **C#:** una alta, **corregida**: «Emitido el… (hora de Chile)» mostraba hora UTC (un reporte pedido a las 21:30 salía con la fecha del día siguiente). También corregidos: los generadores no podían cancelarse (ahora reciben el token), el PDF esperaba su turno sin token, un archivo podía borrarse si dos procesos se solapaban, un pedido «pendiente» eterno bloqueaba a la persona (ahora se cierra a la hora), la carpeta con barra final daba 410, sustitutos XML sueltos hacían fallar el Excel, fugas de `SKData`, y la nota de las solicitudes (solo las filtra el cliente). Con la medición se agregó el **tope de 10.000 filas para el PDF**. Todo con su prueba.
- **Sin hacer (dejado a propósito):** repartir los cupos al terminar cada trabajo en vez de por lote (con 2 cupos el costo es bajo) y escribir el Excel con `OpenXmlWriter` (hoy 207 MB con 50.000 filas, aceptable).

## Pendientes y límites (no bloquean)

- **Margen (decidido por ti el 21-11-2026):** ingresos − costo de materiales, sobre los pedidos con costo; el resumen sigue mostrando los tres costos. «Ingresos» son los del dashboard (pedidos entregados con su monto final); si «cobrados» debía significar lo abonado, es un cambio en `ConsultasReportes`.
- **El PDF no es accesible etiquetado (PDF/UA)** (decisión 8); sus gráficos van sobre la tabla de sus datos, no al lado.
- **Para la 13B:** como la base impone un trabajo activo por persona, los tres botones deben desactivarse mientras se prepara uno (el plan decía que solo el pulsado). Los filtros del dashboard enlazan a la cobranza con `alcanceSaldo`.
- **Lo que solo puedes comprobar tú:** abrir un `.xlsx` en Excel y un PDF en tu visor (te dejo ejemplos antes de la 13B).
- La migración está aplicada en la demo; la real no se tocó.

## Cierre

Documentos actualizados: `tablero.md`, `trazabilidad.md`, `desviaciones.md`, `contrato.md`, `permisos.md`, `openapi.json`, diccionario de datos, modelo entidad-relación, `arquitectura.md` y `licencias.md`. Los comandos de GitHub te los paso; yo no subo nada.
