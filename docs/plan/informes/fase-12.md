# Informe de cierre — Fase 12: indicadores y dashboard (12A y 12B)

Fecha: 17-11-2026 · Ramas: `fase-12a-dashboard-frontend` (aprobada el 16-11-2026, informe `fase-12a.md`) y `fase-12b-indicadores-backend` (nace de la 12A) · Estado: **esperando tu visto bueno**

Plan aprobado: `fase-12b-plan.md`, con tus decisiones A a D del 16-11-2026. Contrato: `fase-12-especificacion.md`. La 12A construyó el frontend con datos de demostración; la **12B** construyó el backend, las vistas SQL, la purga de la IP, el catálogo real y conectó el dashboard a la API real.

## Qué se hizo

### 0. Catálogo real del taller (tu pedido del 16-11-2026)
- **Borrado y rehecho.** La migración `CatalogoRealDelTaller` borra las categorías y los productos de prueba y deja **4 categorías y 16 productos** (ids 1 a 4 y 1 a 16), con las medidas y opciones que entregaste: impresión y publicidad gráfica, letreros y estructuras, papelería e impresión pequeña, y estampados y personalización.
- **Nada se pierde en la demo.** Lo que usaba un producto de prueba (29 publicaciones de la vitrina y 25 ítems de solicitudes) pasó al producto nuevo equivalente, en dos pasos con productos temporales. La tabla de equivalencias está en `desviaciones.md`. La migración se detiene si encuentra productos con referencias que no conoce, en vez de remapearlos en silencio.
- **Medidas y opciones (decisión A).** Cada producto trae sus límites en milímetros o sus medidas fijas (X Banner 80 × 200, Demo Stand 300 × 300, caballete en tres tamaños, tarjetas, flyers) y sus opciones (material, acabado, terminaciones, variante, espesor de 2 a 10 mm, diseño…). La API rechaza con `400` una medida fuera de rango o una opción que el producto no tiene. El formulario muestra los límites, completa una medida única, deja elegir entre varias y ofrece cada opción como selector; lo elegido se ve en el comprobante.
- **Probada hacia adelante y hacia atrás** sobre una copia de la demo con sus datos y sobre una base vacía. Aplicada **solo en la demo**; la real sigue con 3 migraciones.

### 1. Base de datos (`IndicadoresDashboard`, RNFO11)
- **Tres vistas** (sin `SELECT *`): `vw_indicadores_pedidos`, `vw_indicadores_solicitudes` y `vw_indicadores_items` (esta reparte el monto de cada pedido entre sus ítems según su cantidad: decisión D).
- **Cuatro índices justificados**, cada uno con la consulta que sirve y por qué no bastaba lo existente (`docs/base-de-datos/diccionario-de-datos.md`, «Índices del dashboard»).
- **IP de la bitácora a los 90 días (RNFE01).** El trigger `trg_auditoria_inmutable` sigue rechazando todo `UPDATE` y `DELETE` **salvo uno**: poner `ip = NULL` en filas de más de 90 días, sin cambiar ninguna otra columna. La purga diaria lo hace en lotes de 5000. El registro del evento queda intacto.

### 2. Backend (`Negocio` / `Datos` / `Services` / `Endpoints`, RNFO10)
Seis rutas `GET /api/v1/indicadores/*`, solo dueño y administrador (otros roles `403`, sin sesión `401`), con una **consulta agregada por bloque** (RNFP26):
- **Resumen:** las siete tarjetas con su comparación y el saldo por cobrar. Fórmulas de tu lista:
  - **S10:** pedidos sin anulados, con el desglose por estado.
  - **S11:** monto final de los entregados.
  - **S12:** solicitudes en línea frente a presenciales.
  - **S13:** en línea con un pedido **no anulado**.
  - **S14:** S13 ÷ S12 × 100, con la variación en **puntos porcentuales**.
  - **Ticket promedio:** ingresos ÷ entregados.
  - **Entregas a tiempo:** real ≤ estimada.
  - **Saldo por cobrar:** del período o de toda la deuda vigente.
- **Evolución** por día, semana o mes, con el período de comparación alineado; **cobranza**, **embudo**, **demanda** por categoría o por tipo de letrero y **pedidos por cobrar** paginados en SQL.
- **Filtros combinables:** período, rango propio (con tope), año y mes, comparación, estado del pedido y del pago, categoría, origen y cliente. Un filtro inválido da `400` con el error por campo. Se mantienen los nombres de la 12A (decisión C).

### 3. Frontend conectado
La pantalla llama a la API real; se eliminaron el adaptador de demostración, sus datos, la variable `VITE_INDICADORES_DEMO` y el aviso. Los estados de carga, vacío y error con «Reintentar» siguen como en la 12A. Nuevo: pedidos por estado en su tarjeta y demanda agrupable por tipo de letrero. El inicio del dueño usa datos reales.

### 4. Privacidad
La política de privacidad pasa a la versión **1.1** (autorizada por ti): informa que la IP de la bitácora se borra a los 90 días.

## Pruebas
- **Cifras a mano contra SQL Server (34 casos, `IndicadoresFase12bTests`):** cada indicador, variación en puntos, gráfico y filtro combinado coincide con un cálculo hecho a mano sobre datos conocidos, con pedidos anulados excluidos, entregas atrasadas, abonos parciales, división por cero, un cliente sin pedidos y un período vacío. Las cuentas están comentadas en el propio archivo.
- **Purga de la IP (`PurgaIpAuditoriaTests`):** a 91 días se borra, a 89 no; el registro queda intacto; el trigger rechaza cualquier otro cambio, también a 31, 60 y 89 días, y la prueba avisa si la bitácora gana una columna que el trigger no compara.
- **Reglas puras y catálogo:** unitarias nuevas para períodos, filtros, variaciones, reparto del redondeo, medidas y opciones. **`ContratoTests`** comprueba que las seis rutas, su acceso por rol y el contrato coincidan.
- **Fallos a propósito.** Introduje 13 errores en las fórmulas y filtros (contar los anulados, `<` en vez de `<=`, la conversión en % y no en puntos, ticket sobre todos los pedidos, deuda vigente con fechas, purga a 30 días, filtro de categoría ignorado, divisor cero que da 0, demanda sin reparto, trigger a 30 días, enero contra diciembre del mismo año, medidas sin máximo, embudo con anulados) y **las pruebas detectaron los 13**. Uno no fue detectado al principio (bajar el umbral del trigger); agregué la prueba que faltaba.
- **Resultado final (corrida completa del 17-11-2026):** backend, **500 unitarias y 507 de integración, todas en verde**; **Vitest 627 de 627 y Playwright 218 de 218** con axe a 360, 768 y 1440 px; lint limpio y build correcto. En el camino, una prueba antigua falló por mis propios datos (estaban en 2031 y esperaba cero solicitudes futuras): los moví a 2021.

## Revisiones de ECC y qué se corrigió
- **Seguridad:** sin hallazgos críticos ni altos. Se corrigió el rango propio sin tope (daba un 500 y gastaba memoria: ahora tiene límites y responde `400`) y los mensajes que repetían lo enviado.
- **C#:** se corrigió el embudo (ahora acumulativo, sin pasos que suban), el redondeo de la demanda (suma exacta con el método del mayor resto) y el margen de reloj de la purga.
- **Base de datos:** se corrigió el contador `IDENTITY` (quedaba en 1016, lo que además inutilizaba el `Down`), la migración se detiene ante productos desconocidos con referencias y se agregó la prueba de columnas del trigger.
- **Integración con el frontend:** se corrigió un error de opciones de la API que quedaba invisible y bloqueaba el envío, el borrador que perdía las opciones al recargar, las fechas con basura en la dirección, las etiquetas del embudo y de la demanda y los filtros que el embudo no usa.
- Lo que se aceptó o queda para después está en `desviaciones.md`.

## Pendiente
- **Tu verificación en la app (la hago contigo):** la demo pide iniciar sesión como dueño y no tengo su contraseña. Con el frontend y la API levantados, revisamos juntos `/panel/indicadores` y `/panel` a 360, 768 y 1440 px y la consola.
- **Base real:** le faltan ahora 13 migraciones, entre ellas las dos de esta fase. Se aplican con respaldo y con tu permiso.
- **Pendientes anotados:** editar las medidas y opciones de un producto desde el panel; regenerar el script completo de la base; buscar clientes en el servidor cuando pasen de 100; validar solo lo modificado al editar una solicitud antigua remapeada.
- **Trazabilidad:** RFS09, RFU19, RFU27, E21, S10 a S14, S19 y RNFO11 quedan **Cumplido**; S24, RFU36 y E16 siguen «En curso» hasta los reportes exportables (fase 13).
