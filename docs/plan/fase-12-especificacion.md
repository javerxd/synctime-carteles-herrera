# Especificación técnica de la fase 12 (contrato del dashboard)

Acompaña a `fase-12a-plan.md`, aprobado el 16-11-2026. La **12A** construyó el frontend con un adaptador de demostración que responde exactamente este contrato (`frontend/synctimefront/src/api/indicadoresDemo.js`). La **12B** implementa estas rutas en el backend y apaga la demo con `VITE_INDICADORES_DEMO=false`. Las rutas están declaradas en `docs/api/openapi.json` bajo `x-planificadas`: `ContratoTests` solo lee `paths`, así que la prueba sigue en verde hasta que la 12B las pase a `paths` junto con su implementación.

## Decisiones fijas
- **Acceso:** todas las rutas son `GET /api/v1/indicadores/*` y solo para gestión (dueño y administrador). Un operario o un diseñador recibe 403; sin sesión, 401.
- **Fórmulas literales del plan general.** Los pedidos **anulados nunca cuentan** en ningún indicador.
  - **Pedidos (S10):** pedidos no anulados con `fechaPedido` en el período.
  - **Ingresos (S11):** suma de `montoTotalFinal` de los pedidos `entregado` con `fechaEntregaReal` en el período.
  - **En línea y presenciales (S12):** los pedidos del período según el origen de su solicitud (`online`) o directos y presenciales (`presencial`).
  - **Concretadas (S13):** solicitudes en línea del período que tienen un pedido **no anulado**.
  - **Conversión en línea (S14):** concretadas ÷ solicitudes en línea × 100. Sin solicitudes, `null`.
  - **Ticket promedio:** promedio de `montoTotalFinal` de los pedidos del período, redondeado a peso.
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
| `alcanceSaldo` | `periodo` (predeterminado), `vigente` | `vigente`: el saldo, la cobranza y el listado por cobrar **ignoran las fechas** (toda la deuda del taller); los demás filtros siguen aplicando |

- **Solicitudes en línea** (S13, S14 y embudo): las filtran `categoria` (la del producto principal de la solicitud), `cliente` y `origen`; `estadoPedido` y `estadoPago` no las filtran.
- **Valor inválido:** `400 datos_invalidos` con `errores` por campo (el frontend quita ese parámetro de la dirección).

## Rutas y respuestas

Todas las respuestas traen `periodo: { tipo, desde, hasta, etiqueta }` (`tipo`: `semana`, `mes`, `anio` o `rango`; `etiqueta` en español, por ejemplo «noviembre de 2026»).

### `GET /indicadores/resumen`
```json
{
  "periodo": { "tipo": "mes", "desde": "2026-11-01", "hasta": "2026-11-30", "etiqueta": "noviembre de 2026" },
  "comparacion": { "tipo": "mes", "desde": "2026-10-01", "hasta": "2026-10-31", "etiqueta": "octubre de 2026" },
  "tarjetas": [
    { "clave": "ingresos", "unidad": "pesos", "valor": 180000, "anterior": 120000, "variacion": 50, "tipoVariacion": "porcentaje" },
    { "clave": "pedidos", "unidad": "cantidad", "valor": 3, "anterior": 1, "variacion": 200, "tipoVariacion": "porcentaje" },
    { "clave": "origen", "unidad": "cantidad", "valor": 1, "anterior": 1, "variacion": 0, "tipoVariacion": "porcentaje",
      "detalle": { "presenciales": 2, "porcentajeEnLinea": 33.3 } },
    { "clave": "concretadas", "unidad": "cantidad", "valor": 1, "anterior": 1, "variacion": 0, "tipoVariacion": "porcentaje",
      "detalle": { "solicitudesEnLinea": 4 } },
    { "clave": "conversion", "unidad": "porcentaje", "valor": 25, "anterior": 50, "variacion": -25, "tipoVariacion": "puntos" },
    { "clave": "ticketPromedio", "unidad": "pesos", "valor": 80000, "anterior": 120000, "variacion": -33.3, "tipoVariacion": "porcentaje" },
    { "clave": "entregasATiempo", "unidad": "porcentaje", "valor": 50, "anterior": 100, "variacion": -50, "tipoVariacion": "puntos" }
  ],
  "saldo": { "alcance": "periodo", "valor": 120000, "cantidad": 2, "anterior": 80000, "variacion": 50 }
}
```
- Las tarjetas van **siempre en este orden** y con estas claves. `origen.valor` son los pedidos en línea.
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
{ "periodo": { "...": "..." }, "categorias": [ { "idCategoria": 1, "nombre": "Luminosos", "monto": 130000, "cantidad": 2, "pedidos": 2 } ] }
```
- Sobre los **ítems** de los pedidos no anulados del período: `monto` = suma de subtotales; `cantidad` = unidades; `pedidos` = pedidos distintos. Solo las categorías con movimiento, de mayor a menor `monto` (a igual monto, por nombre). Con `categoria`, solo esa.

### `GET /indicadores/por-cobrar`
Filtros comunes, más `pagina` (desde 1) y `tamano` (1 a 100; el frontend pide 10). Aquí `estadoPago` solo acepta `no_pagado` o `abonado` (el segmento elegido en el anillo).
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

## Para la 12B (backend)
- **Vistas SQL e índices** que calculen estas cifras en la base (no en memoria), con las mismas fórmulas. Índices sugeridos: `pedidos(fecha_pedido) INCLUDE (estado_pedido, estado_pago, monto_total_final, monto_abonado, id_cliente)`, `pedidos(fecha_entrega_real) WHERE estado_pedido = 'entregado'` y `solicitudes(origen, fecha_solicitud)`.
- **Pruebas:** reutilizar como casos de integración las cifras hechas a mano de `src/test/fase12Indicadores.test.js`.
- **Contrato:** mover las seis rutas de `x-planificadas` a `paths` de `openapi.json` en el mismo commit que las implementa (`ContratoTests` lo exige).
- **Purga de la IP de la bitácora:** a los 90 días, `Ip = NULL` en la auditoría, sin borrar el registro (decisión del 16-11-2026). Ya está escrito en la política de privacidad.
- **Frontend:** `VITE_INDICADORES_DEMO=false`; el aviso «Datos de demostración» desaparece solo.
