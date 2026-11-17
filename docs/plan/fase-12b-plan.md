# Plan de la fase 12B — Backend de indicadores, vistas SQL, conexión real y catálogo real

Fecha: 16-11-2026 · Rama: `fase-12b-indicadores-backend` (nace de `fase-12a-dashboard-frontend`, aprobada) · Estado: **aprobado el 16-11-2026**, con las decisiones A, B, C y D tal como se recomendaron; construcción sin iniciar

Requisitos: RFS09, RNFO11, RFU19, RFU27, RFU36, E16, E21, E27, S10 a S14, S19, S24, RNFP23, RNFP26, RNFE01 (IP a 90 días). Contrato de partida: `fase-12-especificacion.md`.

## 0. Antes de todo: el catálogo real del taller

Hoy la base demo tiene 9 categorías y 19 productos; la real, 5 y 4, sin ninguna solicitud que los use.

- **Nuevo catálogo:**
  - **Cuatro categorías:**
    - Impresión y publicidad gráfica;
    - Letreros y estructuras;
    - Papelería e impresión pequeña;
    - Estampados y personalización.
  - **16 productos:**
    - Gigantografías, Banners, Posteras, Pendones, Roll Up, X Banner y Demo Stand;
    - Adhesivos y vinilos, Letreros luminosos, Letras corpóreas, Señalizaciones y Caballete publicitario;
    - Rotulación vehicular y general, Tarjetas de presentación, Flyers y Estampados.
- **Cómo se carga:** con una **migración de datos** (`CatalogoRealDelTaller`). Así es idéntico en la demo, en las pruebas y, cuando lo autorices, en la base real.
  - **Borra** todas las categorías y productos anteriores.
  - **Inserta** los nuevos desde cero, con ids nuevos.
- **Lo que hoy apunta a productos viejos** (solo en la demo: 29 publicaciones de la vitrina y 25 ítems de solicitudes):
  - Antes de borrar, la migración los **reasigna al producto nuevo equivalente** (tabla abajo). Así no se pierden las fotos reales de la vitrina ni el historial de prueba.
  - Luego borra lo viejo.
  - La base real no tiene referencias: ahí solo borra e inserta.

| Producto viejo (demo) | Producto nuevo |
|---|---|
| Letrero luminoso | Letreros luminosos |
| Letrero de fachada | Letreros luminosos |
| Señalética | Señalizaciones |
| Rotulación de vehículo | Rotulación vehicular y general |
| Caballete | Caballete publicitario |
| Pendón | Pendones |
| Lona publicitaria | Impresión en tela PVC / Gigantografías |
| Vinilo para vitrina, calcomanía, decoración en vinilo, autoadhesivo troquelado | Adhesivos y vinilos |
| Estampado textil | Estampados |
| Volantes y calendarios | Flyers / Folletería |
| Productos de prueba que no calzan | Lo más cercano; te muestro la lista exacta antes de aplicar |

- **Características de cada producto** (tamaños, materiales, acabados y variantes). Te propongo dos niveles; ver la **decisión A**:
  - **Medidas con límites** (ancho y alto mínimo y máximo, o medidas fijas como 80 × 200 o 9 × 5,5 cm). Se guardan en milímetros, como el resto del sistema.
    - El formulario de solicitud las ofrece y la API las valida.
    - Un pedido fuera de rango recibe 400 con el motivo.
  - **Opciones** (material, acabado, terminaciones, variantes, estructura, iluminación, técnica, diseño). Se guardan como lista estructurada por producto (`producto_opciones`).
    - La solicitud muestra cada opción como selector, y el cliente elige.
    - Lo elegido viaja en el detalle de la solicitud y lo ve quien cotiza.
- **Semilla demo y vitrina:** se rehacen con el catálogo nuevo, y la demo del dashboard pasa a datos reales de la base demo (sección 4).

## 1. Base de datos (una migración más, `IndicadoresDashboard`)

Se prueba hacia adelante y hacia atrás en una base temporal. Se aplica **solo en `LetreriaLosHerreraDB_Demo`**; la real no se toca.

- **Vistas SQL** (sin `SELECT *`, columnas explícitas):
  - `vw_indicadores_pedidos`: una fila por pedido no eliminado, con su fecha, la fecha estimada y la real, el estado, el pago, el monto final, lo abonado, el saldo, el origen de su solicitud, el cliente y su categoría principal.
  - `vw_indicadores_solicitudes`: una fila por solicitud, con el origen, la fecha, el cliente, la categoría principal, si fue cotizada, si fue aprobada y si tiene un pedido no anulado.
  - `vw_indicadores_items`: los ítems de los pedidos con su producto y su categoría, para la demanda.
  - **Las agregaciones por período** las hace la API sobre las vistas, con parámetros: una vista no recibe fechas.
- **Índices justificados** (en `docs/base-de-datos/`, con la consulta que sirve cada uno):
  - `pedidos(fecha_pedido) INCLUDE (estado_pedido, estado_pago, monto_total_final, saldo_pendiente, id_solicitud)` filtrado sin eliminados;
  - `pedidos(fecha_entrega_real) WHERE estado_pedido = 'entregado'`;
  - `pedidos(estado_pago, estado_pedido) INCLUDE (saldo_pendiente)` para la cobranza;
  - `solicitudes(origen, fecha_solicitud) INCLUDE (id_cliente, estado)`;
  - `auditoria(fecha) WHERE ip IS NOT NULL`, para la purga.
  - Antes de crear cada uno reviso el plan de ejecución; si un índice existente ya sirve, no se duplica.
- **IP de la auditoría a los 90 días:**
  - **Hoy el trigger `trg_auditoria_inmutable` rechaza todo UPDATE.** Se reescribe para permitir **un único cambio**: `ip` a `NULL` en filas de más de 90 días, sin tocar ninguna otra columna. Cualquier otro UPDATE o DELETE se sigue rechazando.
  - **La purga se suma al proceso diario existente** (`PurgaDatosPersonales`).
  - **Política de privacidad a 1.1** (tu autorización): `DocumentosLegales.cs` y `textos.js` pasan a la 1.1, con la fecha del cambio en «Cambios y versiones». Los registros nuevos aceptan la 1.1; los consentimientos 1.0 ya dados quedan registrados como estaban.

## 2. Backend (`Api` / `Negocio` / `Datos`, RNFO10)

- **Las seis rutas `GET /api/v1/indicadores/*`**, solo gestión. Pasan de `x-planificadas` a `paths` en el `openapi.json`, y `ContratoTests` comprueba también el acceso por rol.
- **Capas:**
  - `Negocio/Indicadores`: períodos, comparación, fórmulas y variaciones, con funciones puras probadas con cuentas a mano.
  - `Datos/Indicadores`: consultas sobre las vistas, sumadas en SQL.
  - `Endpoints/IndicadoresApi`: validación de filtros y 400 por campo.
- **Fórmulas** (tu lista; las diferencias con la 12A van en la **decisión B**):
  - **S10:** pedidos del período sin anulados, con el **desglose por estado** (aceptado, en proceso, terminado y entregado).
  - **S11:** monto final de los entregados, en pesos enteros.
  - **S12:** **solicitudes** en línea frente a presenciales.
  - **S13:** solicitudes en línea con un pedido no anulado.
  - **S14:** S13 ÷ S12 en línea × 100, con la variación en pp. Con divisor 0 la respuesta es `null`, nunca un error.
  - **Ticket promedio:** ingresos de los entregados ÷ cantidad de entregados.
  - **Entregas a tiempo:** entregados con fecha real ≤ fecha estimada, en porcentaje.
  - **Saldo por cobrar:** suma de `saldo_pendiente` de los no anulados en «no pagado» o «abonado», del período o de toda la deuda vigente.
- **Gráficos:** la evolución, la cobranza, el embudo y la demanda, con el ranking **por categoría o por tipo de letrero** (`agrupar=categoria|producto`, nuevo).
- **Filtros combinables:** los de la 12A. Los nombres que propones van en la **decisión C**.
- **Rendimiento (RNFP26):** cada bloque es una sola consulta agregada, sin traer filas a memoria. El listado se pagina en SQL.

## 3. Pruebas primero (TDD)

- **Integración contra SQL Server:** una base temporal con pedidos y solicitudes conocidos, con las **mismas cifras hechas a mano de la 12A** más tus casos:
  - anulados excluidos, entregas atrasadas y abonos parciales;
  - división por cero, un cliente sin pedidos y la variación en pp;
  - cada filtro y su combinación;
  - la IP de una fila de 91 días borrada y la de 89 días intacta, con el trigger rechazando cualquier otro cambio;
  - el catálogo: 4 categorías y 16 productos, ninguno viejo, la vitrina reasignada y las medidas fuera de rango rechazadas.
- **Fallos a propósito:** cambio temporalmente fórmulas y filtros (por ejemplo, contar los anulados o usar `<` en lugar de `<=`) y confirmo que las pruebas fallan. Luego lo revierto.
- **Frontend:** Vitest y Playwright con axe a 360, 768 y 1440 px, ahora contra la API simulada con las respuestas del contrato.

## 4. Conexión real del frontend

- **`VITE_INDICADORES_DEMO` se retira:** la pantalla llama a la API real.
  - El adaptador de demostración y sus datos salen del bundle.
  - Sus cifras a mano pasan a ser casos de las pruebas de integración del backend.
- **Sin cambios visuales:** los estados de carga, vacío, error y «Reintentar» de la 12A se mantienen. Desaparece el aviso «Datos de demostración».
- **Solicitudes:** el formulario usa el catálogo nuevo, con las medidas y opciones de la decisión A.

## 5. Revisiones, verificación y cierre
- **ECC:** `csharp-reviewer`, `security-reviewer`, `database-reviewer` (con `database-migrations`), `react-reviewer` y una revisión de la integración entre frontend y backend.
- **En la app en marcha** sobre la demo, con tu sesión de dueño: 360, 768 y 1440 px y la consola sin errores. También `dotnet test`, Vitest y Playwright con axe.
- **Documentación:**
  - `trazabilidad.md` (los requisitos de la fase pasan a «Cumplido»), `tablero.md` y `desviaciones.md`;
  - `contrato.md`, `permisos.md`, el diccionario de datos, la justificación de índices y el script SQL;
  - el informe `informes/fase-12.md`, que cubre la 12A y la 12B;
  - el commit en la rama.
- **GitHub:** te entrego los comandos; yo no subo nada.

## Decisiones que necesito de ti

- **A. Características del catálogo.** Recomiendo **medidas con límites más opciones estructuradas**: el cliente elige y la API valida. La alternativa es guardarlas solo como texto descriptivo, sin validar, que es más rápido pero no evita pedidos imposibles.
- **B. Fórmulas que cambian respecto de la 12A.** Tu lista difiere en tres puntos:
  - **S12:** pasa a contar **solicitudes** en línea frente a presenciales (la 12A contaba pedidos por origen);
  - **ticket promedio:** pasa a ser ingresos ÷ **entregados** (la 12A promediaba todos los no anulados del período);
  - **S10:** suma el desglose por estado.

  Recomiendo **adoptar tu lista**; actualizo el contrato y las pruebas a mano.
- **C. Nombres de los filtros.** Tu lista usa `soloPorCobrar`, `categoriaId` y `clienteId`; el contrato aprobado en la 12A usa `estadoPago=por_cobrar`, `categoria` y `cliente`, que ya viven en las direcciones del frontend. Recomiendo **mantener los de la 12A** y anotar la equivalencia en `contrato.md`.
- **D. Monto por categoría** en el ranking, cuando un pedido tiene ítems de varias categorías. La solicitud no guarda el precio de cada ítem, solo el pedido tiene monto. Recomiendo **repartir el monto final entre sus categorías en proporción a la cantidad de cada ítem**; así la suma del ranking es igual a los ingresos. La alternativa es asignar todo a la categoría del primer ítem.
