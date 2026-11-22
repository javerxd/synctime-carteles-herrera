# Informe de cierre — Fase 15A: rendimiento del backend

Fecha: 21-11-2026 · Rama: `fase-15-calidad` (nace del commit del plan, `8ac6b42`) · Estado: **construida, en revisión; parada para tu visto bueno antes de la 15B** · Commit y push los haces tú (los comandos van al final)

Plan: `fase-15-plan.md` (aprobado por ti el 21-11-2026). Tus tres decisiones: umbrales de Lighthouse (Rendimiento ≥ 85 y Accesibilidad ≥ 95, para la 15B), **un índice solo con migración aparte y tu aprobación** (no hizo falta ninguno) y **documentar que las cifras dependen de este equipo**. **Sin migración y sin dependencias nuevas** (el guion de carga es Node.js nativo).

## Resultado en una línea

**La meta se cumple con holgura: con 20 usuarios simultáneos, el 99,8 % de las respuestas llegó en 2 segundos o menos** (mediana 21 ms, p95 289 ms, ningún `429` ni `500`). No hizo falta corregir rendimiento; sí se corrigieron tres defectos de la búsqueda de clientes, una advertencia de EF Core y una prueba unitaria que no compilaba.

## Qué se hizo

- **Guion de carga propio** (`scripts/carga/`, Node.js nativo, sin paquetes; `README.md` ahí). Un solo comando, `node scripts/carga/carga.mjs`, hace todo: construye la API en Release en una carpeta temporal, la levanta contra una **base nueva y temporal** (`SyncTime_Pruebas_carga_xxxxxxxx`, creada por las migraciones), crea cuentas de prueba, **siembra datos sintéticos** con `sqlcmd`, lanza **20 usuarios** (8 visitantes, 6 clientes con cuenta y 6 personas del taller, cada una con su propia IP de origen salvo el personal, que comparte la del taller), mide, escribe el informe y **borra la base y la carpeta temporales**. **No usa la base real ni la demo** ni ningún dato personal; las claves de prueba nacen al azar en cada corrida y no se escriben en ningún archivo; la API de la prueba lleva sus variables de entorno, que pisan los *User Secrets* (ni el correo real ni el ID de Google llegan a ella). Cuenta como respuesta buena la que trae el código esperado **y** llega en 2 s o menos (un `500` o un `429` rápido no cuenta).
- **Datos sintéticos** (`datos-sinteticos.sql`): 2.000 clientes, 6.000 solicitudes (mitad en línea) con sus ítems e historial, cotizaciones, unos 2.400 pedidos con abonos e historial, 40.000 filas de bitácora y 60 publicaciones. Se niega a correr en una base que no sea temporal de pruebas.
- **Búsqueda de clientes en el servidor (RFU27).** El contrato ya lo admitía (`GET /clientes?q=&tamano=`); se **verificó con 1.500 clientes** y se corrigió lo que falló (abajo). `contrato.md` lo deja dicho. **La pantalla del filtro «Cliente» sigue trayendo los primeros 100: se cambia en la 15B.**
- **Correcciones.** (1) `ServicioClientes.ListarAsync`: un texto de solo puntos devolvía a todos los clientes con RUT, un teléfono con `+56` no se encontraba y un RUT escrito todo junto tampoco. (2) La advertencia de EF Core 10102 al arrancar (la purga de exportaciones vencidas hacía `Take(500)` sin orden). (3) `IngresoGoogleTests` **no compilaba en el commit de la 14C** (faltaba `correoEsDePersonal: false` en cuatro llamadas): las 749 unitarias vuelven a correr.

## Pruebas

- **Backend: 749 unitarias y 704 de integración, todas en verde** (18 de integración nuevas en `RendimientoFase15aTests`: con 1.500 clientes sintéticos, nunca más de 100 por página; un cliente fuera de los 100 primeros se encuentra por nombre y por teléfono; recorrer todas las páginas lista a cada cliente una sola vez; `%`, `_`, `[a-z]`, `Ana%`, `.` y `...` son texto; teléfono con y sin `+56`; RUT con puntos, con guion y todo junto; una red de seguridad de tiempo). Se insertan con una sola sentencia SQL y se borran al terminar (el interceptor de auditoría no se entera: no deja filas de bitácora que no se puedan borrar).
- **Guion de carga: 21 pruebas de Node** (`node --test "scripts/carga/*.test.mjs"`): percentiles, la meta del 95 %, el informe y las opciones.
- **Pruebas primero y fallos a propósito (detectados, código restaurado).** *Backend:* (1) quitar el tope de 100 por página → falla 1 prueba; (2) aplicar el filtro de búsqueda solo a los primeros 100 clientes (el defecto de RFU27) → fallan 4; (3) comodines de `LIKE` sin escapar → fallan 4. Las pruebas de `.`, `+56` y RUT todo junto se escribieron **antes** de corregir y fallaron primero (5 fallos). *Guion:* hacer que una respuesta equivocada cuente como buena → falla 1 prueba.
- **Revisiones de ECC.** `performance-optimizer`: sin N+1 ni consultas que hoy importen; ningún índice nuevo hace falta (la evidencia no lo pide); una advertencia de EF por corregir (hecho) y mejoras opcionales que no se hicieron (en `desviaciones.md`). `csharp-reviewer`: prueba aprobada con retoques menores (aplicados el nombre y los comentarios; dejé `ExecuteSqlRaw` porque los valores son constantes del archivo y ahí van dentro de literales) y dos defectos reales del servicio, ya corregidos con su prueba.

## Mediciones (en `docs/evidencias/`)

| Corrida | Qué es | Respuestas | Buenas (≤ 2 s) | p95 | Observaciones |
|---|---|---:|---:|---:|---|
| `carga-15a.md` | **La prueba pedida**: 20 usuarios, límites reales, pausa de 1 a 3 s, 120 s | 2.001 | **99,8 %** | 289 ms | 0 × `429`, 0 × 5xx |
| `carga-15a-estres.md` | 20 usuarios sin límites de tasa y pausas de 0,1 a 0,4 s (con consultas SQL más lentas) | 8.680 | 99,8 % | 65 ms | unas 96 peticiones por segundo |
| `carga-15a-60-usuarios.md` | **60 usuarios** sin límites, mismo ritmo | 23.559 | 100 % | 202 ms | unas 260 por segundo; la API usó 0,7 núcleos |
| `carga-15a-limites.md` | 20 usuarios con los **límites reales** al ritmo de 0,1 a 0,4 s | 10.265 | 36,4 % | 41 ms | el 64 % fueron `429`: el límite de 100 por minuto trabajando, no lentitud |

Otras dos corridas de la prueba pedida dieron 100 % y 99,7 % (la variación son unas pocas peticiones de 2 a 4,5 s a la vez, por falta de memoria libre; ver abajo).

## Lo que debes saber (límites documentados, no escondidos)

- **Este equipo estaba corto de memoria** (8 GB con menos de 1 GB libre: Discord, navegadores, el IDE). SQL Server Express trabajó con unos 280 MB y se vieron las esperas (`SLEEP_BPOOL_STEAL`). Pasa la meta igual, pero **conviene repetir la medición con el equipo desocupado** (cierra programas y corre el comando de arriba; son 5 minutos). **La prueba en la nube de la fase 16 dará otras cifras.**
- **La primera petición de cada ruta tras arrancar tarda hasta 1,5 s** (compilar EF Core, JIT, plan de SQL). Sin un servicio de calentamiento; se decide en la fase 16.
- **Para la fase 16:** los límites por IP funcionan y no dan `429` falsos con uso normal, pero detrás de una red que comparte IP los visitantes compartirían cupo: la API debe leer la IP real desde el proxy. Anotado en `desviaciones.md`.
- **Una base de pruebas antigua** (`SyncTime_Pruebas_dcfd40e1…`, del 17-11-2026) sigue en tu SQL Server: no es de esta fase y no la toqué; la puedes borrar.
- **Durante el trabajo** el SQL Server se atascó dos veces por mi propio SQL de siembra (un cruce con mal plan, ya simplificado) y se reinició y recuperó solo; **la demo y la real no se tocaron**, y todas las bases temporales de la 15A se borraron.

## Qué sigue y qué te toca

La **15B** (frontend): llamadas duplicadas, presupuesto del JavaScript, red lenta y móvil, Chrome/Edge/Brave, axe en todas las rutas, Lighthouse, guion de NVDA y **la pantalla del filtro «Cliente» con la búsqueda en el servidor**. Empieza cuando des el visto bueno a esta parte. De tu lado, opcional: repetir la carga con el equipo desocupado.

## Comandos para tu commit

Desde la carpeta del repositorio. Se agregan `backend`, `docs` y `scripts`, para **no arrastrar `.impeccable/` ni el archivo `python`**:

```powershell
git add backend docs scripts
git status --short
git commit -m "feat(fase-15a): prueba de carga con 20 usuarios (guion propio), busqueda de clientes verificada y corregida, evidencias y documentacion"
```
