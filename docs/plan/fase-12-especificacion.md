# Especificación técnica de la fase 12 (contrato del dashboard)

Acompaña a `fase-12a-plan.md` y `fase-12b-plan.md`. La **12A** construyó el frontend con un adaptador de demostración; la **12B** (17-11-2026) implementó estas rutas en el backend sobre vistas SQL y quitó la demostración. Las seis rutas están en `paths` de `docs/api/openapi.json` y `ContratoTests` comprueba que coincidan con la API y con el acceso por rol. Las cifras se prueban contra SQL Server, con datos conocidos y cuentas hechas a mano (`IndicadoresFase12bTests`).

**Cambios de la 12B respecto de la 12A** (decisiones B, C y D del plan, aprobadas el 16-11-2026): S12 cuenta **solicitudes** (no pedidos); el ticket promedio es **ingresos ÷ pedidos entregados**; S10 trae el **desglose por estado**; la demanda se mide sobre los **ingresos** (pedidos entregados) y admite agrupar por **tipo de letrero**; los nombres de los filtros se mantienen (`estadoPago=por_cobrar`, `categoria`, `cliente`).

## Decisiones fijas
- **Acceso:** todas las rutas son `GET /api/v1/indicadores/*` y solo para gestión (dueño y administrador). Un operario o un diseñador recibe 403; sin sesión, 401.
- **Fórmulas literales del plan general.** Los pedidos **anulados nunca cuentan** en ningún indicador.
  - **Pedidos (S10):** pedidos no anulados con `fechaPedido` en el período, con el **desglose por estado** (aceptado, en_proceso, terminado y entregado).
  - **Ingresos (S11):** suma de `montoTotalFinal` de los pedidos `entregado` con `fechaEntregaReal` en el período.
  - **Solicitudes en línea y presenciales (S12):** las **solicitudes** del período (por `fecha_solicitud`) según su origen, `online` frente a `presencial`. Los porcentajes se calculan sobre la suma de las dos.
  - **Concretadas (S13):** solicitudes en línea del período que tienen un pedido **no anulado**.
  - **Conversión en línea (S14):** concretadas ÷ solicitudes en línea × 100. Sin solicitudes, `null`.
  - **Ticket promedio:** ingresos ÷ pedidos entregados en el período, redondeado a peso. Sin entregas, `null`.
  - **Entregas a tiempo:** entregados del período con `fechaEntregaReal ≤ fechaEntregaEstimada`, sobre los entregados del período × 100. La meta del 90 % es solo visual.
  - **Saldo por cobrar (S24):** suma de `montoTotalFinal − montoAbonado` de los pedidos no anulados en `no_pagado` o `abonado`.
- **Variación:** para cantidades y pesos, `(actual − anterior) ÷ anterior × 100`, con un decimal; si el anterior es 0 o no existe, `null`. Para los porcentajes (conversión y entregas a tiempo), la diferencia en **puntos porcentuales** (`tipoVariacion: "puntos"`).
- **Fechas** en hora de Chile, `aaaa-mm-dd`. «Hoy» es la fecha de Chile del servidor.
- **Montos** en pesos enteros, sin decimales.

## Filtros comunes (query string)

| Parámetro | Valores | Notas |
|---|---|---|
| `periodo` | `semana`, `mes` (predeterminado), `anio` | Relativo a hoy: semana de lunes a domingo, mes calendario o año calendario |
| `desde`, `hasta` | `aaaa-mm-dd` | Rango propio. Gana a todo lo demás; exige las dos fechas y `desde ≤ hasta` |
| `anio`, `mes` | `2020`–`2100`, `1`–`12` | Acceso directo. `mes` exige `anio`. Gana a `periodo` |
| `comparar` | `anterior` (predeterminado), `anio_anterior`, `ninguno` | `anterior`: el período inmediatamente anterior de igual forma (mes → mes anterior; rango de N días → los N días previos) |
| `estadoPedido` | lista separada por comas de `aceptado`, `en_proceso`, `terminado`, `entregado` | `anulado` no se acepta: ningún indicador lo cuenta |
| `estadoPago` | lista de `no_pagado`, `abonado`, `pagado_total`, o el atajo `por_cobrar` | `por_cobrar` = `no_pagado,abonado`; no se combina con la lista |
| `categoria` | id de categoría | Un pedido entra si **alguno** de sus ítems es de esa categoría |
| `origen` | `online`, `presencial` | `presencial` deja sin solicitudes en línea: conversión `null` |
| `cliente` | id de cliente | |
| `agrupar` | `categoria` (predeterminado), `producto` | Solo en `/demanda`: ranking por categoría o por tipo de letrero |
| `alcanceSaldo` | `periodo` (predeterminado), `vigente` | `vigente`: el saldo, la cobranza y el listado por cobrar **ignoran las fechas** (toda la deuda del taller); los demás filtros siguen aplicando |

- **Solicitudes en línea** (S13, S14 y embudo): las filtran `categoria` (la del producto principal de la solicitud), `cliente` y `origen`; `estadoPedido` y `estadoPago` no las filtran.
- **Valor inválido:** `400 datos_invalidos` con `errores` por campo (el frontend quita ese parámetro de la dirección). Se validan todos los parámetros a la vez y se informan juntos.
- **Acceso:** solo gestión (dueño y administrador); sin sesión `401`, cualquier otro rol `403`.
- **Rendimiento (RNFP26):** cada bloque es una consulta agregada sobre `vw_indicadores_pedidos`, `vw_indicadores_solicitudes` o `vw_indicadores_items`; el listado se pagina en SQL.

## Rutas y respuestas

Todas las respuestas traen `periodo: { tipo, desde, hasta, etiqueta }` (`tipo`: `semana`, `mes`, `anio` o `rango`; `etiqueta` en español, por ejemplo «noviembre de 2026»).

### `GET /indicadores/resumen`
```json
{
  "periodo": { "tipo": "mes", "desde": "2026-11-01", "hasta": "2026-11-30", "etiqueta": "noviembre de 2026" },
  "comparacion": { "tipo": "mes", "desde": "2026-10-01", "hasta": "2026-10-31", "etiqueta": "octubre de 2026" },
  "tarjetas": [
    { "clave": "ingresos", "unidad": "pesos", "valor": 180000, "anterior": 120000, "variacion": 50, "tipoVariacion": "porcentaje" },
    { "clave": "pedidos", "unidad": "cantidad", "valor": 3, "anterior": 1, "variacion": 200, "tipoVariacion": "porcentaje",
      "detalle": { "porEstado": [ { "estado": "aceptado", "cantidad": 0 }, { "estado": "en_proceso", "cantidad": 1 },
                                  { "estado": "terminado", "cantidad": 0 }, { "estado": "entregado", "cantidad": 2 } ] } },
    { "clave": "origen", "unidad": "cantidad", "valor": 4, "anterior": 2, "variacion": 100, "tipoVariacion": "porcentaje",
      "detalle": { "presenciales": 2, "porcentajeEnLinea": 66.7 } },
    { "clave": "concretadas", "unidad": "cantidad", "valor": 1, "anterior": 1, "variacion": 0, "tipoVariacion": "porcentaje",
      "detalle": { "solicitudesEnLinea": 4 } },
    { "clave": "conversion", "unidad": "porcentaje", "valor": 25, "anterior": 50, "variacion": -25, "tipoVariacion": "puntos" },
    { "clave": "ticketPromedio", "unidad": "pesos", "valor": 90000, "anterior": 120000, "variacion": -25, "tipoVariacion": "porcentaje" },
    { "clave": "entregasATiempo", "unidad": "porcentaje", "valor": 50, "anterior": 100, "variacion": -50, "tipoVariacion": "puntos" }
  ],
  "saldo": { "alcance": "periodo", "valor": 120000, "cantidad": 2, "anterior": 80000, "variacion": 50 }
}
```
- Las tarjetas van **siempre en este orden** y con estas claves. `origen.valor` son las **solicitudes** en línea.
- Un periodo sin movimiento da `0` (cantidades e ingresos) o `null` (conversión, ticket y entregas a tiempo: nunca una división por cero), y la variación es `null` cuando el valor anterior es `0` o no existe.
- Con `comparar=ninguno`, `comparacion` es `null` y en cada tarjeta `anterior` y `variacion` son `null`.
- Con `alcanceSaldo=vigente`, `saldo` es `{ "alcance": "vigente", "valor", "cantidad", "anterior": null, "variacion": null }`.

### `GET /indicadores/evolucion`
```json
{
  "periodo": { "...": "..." }, "comparacion": { "...": "..." },
  "granularidad": "semana",
  "serie": [
    { "desde": "2026-11-01", "hasta": "2026-11-07", "etiqueta": "01-11", "ingresos": 0, "pedidos": 2, "ingresosComparacion": 0, "pedidosComparacion": 0 }
  ]
}
```
- **Granularidad:** `semana` → `dia` (7 tramos); `mes` → `semana` (tramos de 7 días desde el día 1; el último puede ser más corto); `anio` → `mes`. En un rango propio: hasta 14 días, `dia`; hasta 92, `semana`; más, `mes`.
- **Etiqueta:** `dd-mm` del inicio del tramo, o el mes abreviado (`ene`) en la granularidad `mes`.
- **Comparación alineada por posición:** el tramo *i* del período de comparación, con la misma granularidad; si no existe, `null`. Sin comparación, los dos campos son `null`.
- `ingresos` usa la fecha de entrega real; `pedidos`, la fecha del pedido.

### `GET /indicadores/cobranza`
```json
{
  "periodo": { "...": "..." }, "alcance": "periodo",
  "segmentos": [
    { "estadoPago": "no_pagado", "cantidad": 1, "saldo": 80000 },
    { "estadoPago": "abonado", "cantidad": 1, "saldo": 40000 },
    { "estadoPago": "pagado_total", "cantidad": 1, "saldo": 0 }
  ],
  "saldoTotal": 120000, "cantidadPorCobrar": 2
}
```
- Siempre los tres segmentos, en este orden, aunque tengan 0. `cantidad` = pedidos no anulados (del período por `fechaPedido`, o sin límite de fecha con `vigente`).

### `GET /indicadores/embudo`
```json
{
  "periodo": { "...": "..." },
  "pasos": [
    { "clave": "solicitudes", "cantidad": 4, "porcentaje": 100, "porcentajePaso": null },
    { "clave": "cotizadas", "cantidad": 3, "porcentaje": 75, "porcentajePaso": 75 },
    { "clave": "aprobadas", "cantidad": 2, "porcentaje": 50, "porcentajePaso": 66.7 },
    { "clave": "concretadas", "cantidad": 1, "porcentaje": 25, "porcentajePaso": 50 }
  ]
}
```
- Solicitudes **en línea** con `fecha_solicitud` en el período. `cotizadas`: con al menos una cotización enviada. `aprobadas`: con una cotización aprobada. `concretadas`: con un pedido no anulado.
- `porcentaje` es sobre el primer paso; `porcentajePaso`, sobre el paso anterior. Con divisor 0, `null`.

### `GET /indicadores/demanda`
```json
{ "periodo": { "...": "..." }, "agrupar": "categoria",
  "categorias": [ { "idCategoria": 1, "nombre": "Impresión y publicidad gráfica", "monto": 120000, "cantidad": 2, "pedidos": 2 } ] }
```
- Base: los pedidos **entregados** en el período (los mismos de los ingresos). El `monto` de cada pedido se **reparte entre sus ítems en proporción a su cantidad**, así que la suma de los grupos es igual a los ingresos. `cantidad` = unidades; `pedidos` = pedidos distintos.
- Con `agrupar=producto` cada fila trae `idProducto` en vez de `idCategoria`. Solo los grupos con movimiento, de mayor a menor `monto` (a igual monto, por nombre). Con `categoria`, solo esa (y, con `agrupar=producto`, sus productos).

### `GET /indicadores/por-cobrar`
Filtros comunes, más `pagina` (desde 1) y `tamano` (1 a 100; el frontend pide 10). `estadoPago` funciona como en los demás bloques; el frontend manda aquí el segmento elegido en el anillo (`no_pagado` o `abonado`). Un estado sin saldo (`pagado_total`) da una lista vacía.
```json
{
  "periodo": { "...": "..." }, "alcance": "vigente",
  "elementos": [
    { "idPedido": 6, "cliente": { "idCliente": 3, "nombre": "Camila Reyes" }, "fechaPedido": "2026-01-15", "fechaEntregaEstimada": "2026-01-30",
      "diasDesdePedido": 305, "estadoPedido": "terminado", "estadoPago": "no_pagado",
      "montoTotalFinal": 90000, "montoAbonado": 0, "saldoPendiente": 90000, "listoSinPagar": true }
  ],
  "total": 4, "pagina": 1, "tamano": 10, "totalPaginas": 1, "totalSaldo": 290000
}
```
- Pedidos no anulados en `no_pagado` o `abonado`, de **mayor a menor saldo**; a igual saldo, el más antiguo primero; después, el id.
- `listoSinPagar`: `terminado` o `entregado` y en `no_pagado` (se destaca en la tabla).
- `totalSaldo` y `total` cubren **todas** las páginas. Una página fuera de rango devuelve la última.

## Implementación (12B)
- **Base de datos:** migración `IndicadoresDashboard` con las tres vistas, cuatro índices y el trigger de la bitácora; justificaciones en `docs/base-de-datos/diccionario-de-datos.md`.
- **Capas:** `Negocio/Indicadores/ReglasIndicadores.cs` (períodos, filtros y fórmulas puras), `Datos/Indicadores/ConsultasIndicadores.cs` (consultas agregadas), `Services/ServicioIndicadores.cs` y `Endpoints/IndicadoresApi.cs`.
- **IP de la bitácora a 90 días:** `PurgaDatosPersonales` pone `ip = NULL` en lotes de 5000 filas con más de 90 días; el trigger admite solo ese cambio.
- **Pruebas:** `IndicadoresTests` (reglas puras), `IndicadoresFase12bTests` (SQL Server, cifras a mano) y `PurgaIpAuditoriaTests`.
- **Frontend:** llama a la API real; el adaptador de demostración y `VITE_INDICADORES_DEMO` se eliminaron.
