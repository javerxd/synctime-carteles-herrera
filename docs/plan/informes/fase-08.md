# Informe de cierre — Fase 8: cotizaciones, pedidos, pagos y abonos

Fecha: 14-11-2026 · Rama: `fase-08-cotizaciones-y-pedidos` (nace de `fase-07-solicitudes-y-clientes`) · Estado: **aprobada el 14-11-2026**

Plan aprobado: `fase-08-plan.md`, con tus cuatro decisiones del 14-11-2026, y su especificación en `fase-08-especificacion.md`.

## Qué se hizo

### Cotizaciones (RFS05, RFU08, RFU12, E06, E07, S06)
- **Editor:**
  - Lleva materiales del catálogo, con medida, cantidad y costo unitario. El costo se sugiere desde el material y se puede cambiar.
  - También mano de obra, otros costos y observaciones para el cliente.
  - El total se ve en vivo, junto a una vista previa de lo que verá el cliente.
- **Montos con IVA incluido** (decisión 1): todo va en pesos enteros. Al pasar al pedido, el total se redondea al peso.
- **Al emitir:**
  - La solicitud pasa a «cotizado» y queda guardado quién la elaboró.
  - Si el cliente tiene el correo confirmado, recibe **«Tu cotización #N está lista»** (decisión 3), con el botón «Ver cotización». El correo se escribe en la misma transacción.
- **Una sola cotización pendiente por solicitud:** ahora la base también lo garantiza.
- **Respuesta por doble vía:**
  - el cliente aprueba o rechaza desde «Mis solicitudes», y aprobar pide confirmar;
  - el personal registra la respuesta cuando el cliente confirma por teléfono, WhatsApp o en el local, y queda quién la marcó y por qué vía.
  - Si la rechaza, la solicitud vuelve a «solicitado» y se puede cotizar de nuevo.
- **Quién cotiza:** todo el personal. Antes el operario no podía.

### Pedidos (RFS06, RFU13, RFU14, RFU35, E08, E09, E26, S07)
- **Desde la cotización aprobada:**
  - con responsable, fecha estimada, modalidad, instalación y dirección, y abono inicial si lo hay;
  - nace «aceptado», sin exigir pago.
- **Pedido directo en el taller:**
  - es una página propia, que reemplaza el diálogo de la fase 3;
  - busca o crea al cliente y registra en una sola transacción la solicitud, la cotización aprobada, el pedido y el abono.
- **Estados:**
  - aceptado → en proceso → terminado → entregado, con la fecha real de entrega;
  - se puede volver un paso atrás, y anular con motivo;
  - cada cambio queda en el historial del pedido.
- **El pago va aparte:** un pedido se puede entregar sin pagar. Su ciclo se cierra cuando está entregado **y** pagado total.
- **Ficha:**
  - montos, datos del pedido, historial de estados e historial de abonos;
  - si otra persona lo modificó mientras lo veías, avisa sin perder lo escrito y deja guardar sobre la versión actual.
- **Quién:** todo el personal crea pedidos, los avanza y registra abonos. Antes el diseñador no podía.
- **Montos:** solo el dueño o el administrador pueden cambiar el monto de un pedido ya creado, y el cambio queda en el historial.

### Abonos (E25)
- **Datos:** monto, fecha de pago, medio, observación y número de factura. Queda quién lo registró y cuándo.
- **Tope:** ningún abono puede superar el saldo, ni siquiera con dos abonos al mismo tiempo, porque la ruta bloquea el pedido antes de registrar.
- **Corrección** (decisión 2): un abono mal ingresado se **anula con motivo**. Queda tachado en el historial, con quién y cuándo, y deja de sumar; después se registra el correcto.
  - El personal puede anular solo los abonos que registró en las últimas 24 horas. El dueño y el administrador pueden anular cualquiera.
- **Pedido anulado con abonos** (decisión 4):
  - solo el dueño o el administrador lo anulan, y basta con que haya tenido abonos, aunque estén anulados;
  - la ficha muestra lo abonado como «a devolver o a favor del cliente».
- **Respuesta perdida:** si se corta la conexión al registrar un abono, un pedido o una cotización, la pantalla revisa si quedó registrado antes de dejar reintentar. Así no se duplica.

### Listados, cobranza y calendario (RFU15, RFU36, E27, S08, S24)
- **Pedidos:** filtros por estado del pedido, estado de pago, cobranza, año, mes y cliente, con búsqueda, orden y paginación en la API. Los filtros quedan en la dirección.
- **Por cobrar:** muestra el total por cobrar de **todo el filtro**, no solo de la página.
- **Cotizaciones:** listado con filtros y su ficha.
- **Calendario de entregas:**
  - en el computador, una cuadrícula de mes; en el teléfono, una lista por día;
  - pendientes, entregados y atrasados se distinguen con color, ícono y texto.
- **Inicio del panel** (dueño y administrador): entregas atrasadas, de los próximos 3 días y total por cobrar.

### Seguimiento del cliente (RFU07, S04)
- Cada solicitud muestra sus pasos: enviada, cotización, fabricación y entrega.
- Muestra la cotización con su detalle (S06), con aprobar o rechazar mientras está pendiente.
- Muestra el pedido con su estado, pago, saldo, fecha estimada y modalidad.
- El cliente solo ve lo suyo.

### Migración `CotizacionesYPedidos`
- **Qué agrega:**
  - **Cotizaciones:** versión, respuesta (fecha, vía, quién y motivo), borrado lógico, una sola pendiente por solicitud y montos enteros.
  - **Pedidos:** motivo de anulación, borrado lógico y un pedido por cotización.
  - **Tabla `pedidos_historial`** (nueva).
  - **Abonos:** anulación. El trigger suma solo los vigentes y no deja reactivar uno anulado.
- **Datos existentes:** antes de cada regla se completan los que ya había.
- **Revertirla:** se detiene si eso borraría datos.
- **Comprobaciones:**
  - se probó subirla, revertirla y volver a subirla en una base temporal, que se borró después;
  - el script `LetreriaLosHerreraDB.sql` se regeneró y se comprobó creando una base temporal idéntica a la demo, que también se borró.
- **Dónde está aplicada:** en la demo. **No en la real**, que sigue con 3 migraciones.
- **Consulta de solo lectura en la base real:** tiene 0 cotizaciones y 0 pedidos, así que la migración no va a fallar cuando se aplique.

## Verificación

| Comprobación | Resultado |
|---|---|
| Backend | 335 unitarias y 329 de integración, en verde; compila sin advertencias |
| Frontend | 348 de Vitest y 141 de Playwright con axe (las pantallas nuevas a 360, 768 y 1440 px), en verde; lint sin errores; build correcto |
| Pruebas no vacías | Quitar el tope de abonos, la regla de una cotización pendiente o el bloqueo del pedido hace fallar su prueba |
| **La app en marcha (demo)** | Con tu sesión de dueño, sobre la solicitud #19:<br>• cotización #13 emitida ($81.800);<br>• el correo «Tu cotización #13 está lista» salió a la bandeja local;<br>• respuesta del cliente por WhatsApp registrada;<br>• pedido #13 creado con un abono de $30.000;<br>• avance hasta «entregado» con saldo pendiente;<br>• un abono mayor que el saldo, rechazado;<br>• pago del resto con factura, y «ciclo cerrado»;<br>• ese abono anulado (quedó tachado y el pedido volvió a deber) y registrado de nuevo.<br>En la base, lo abonado coincide con los abonos vigentes en todos los pedidos. |
| Cobranza | «Por cobrar» mostró $420.000 en 6 pedidos. Coincide con la suma a mano de las filas y con una consulta directa a la base |
| Calendario e inicio | El calendario de noviembre, en el computador y a 375 px, muestra el pedido #13 entregado hoy. El inicio muestra 4 atrasadas y 1 próxima. La consola, sin errores |

## Revisión de ECC

**Seguridad, C# y base de datos del backend:** ninguno crítico. Se corrigieron, entre otros:
- un pedido eliminado dejaba su cotización sin salida (alto);
- cualquier empleado podía cambiar el monto de un pedido sin dejar rastro;
- se podía saltar la regla de anular un pedido con abonos, registrando y anulando un abono antes;
- montos que, al faltar en la petición, quedaban en $0;
- crear un pedido sin revisar el estado de la solicitud;
- revertir la migración borraba los abonos anulados;
- una factura que se podía reemplazar sin aviso al registrar un abono;
- fechas extremas que daban error 500.

**React y accesibilidad del frontend:** ninguno crítico. Se corrigieron, entre otros:
- un conflicto de versión que obligaba a perder lo escrito (alto);
- un abono que se podía duplicar si se cortaba la red (alto);
- listados que volvían al esqueleto en cada filtro;
- el diálogo de abono, que quedaba abierto sin saldo;
- detalles de foco y de anuncios para lectores de pantalla en el calendario, en los radios y en el seguimiento del cliente.

## Pendiente para otras fases (en `desviaciones.md`)
- **Fase 11:**
  - los demás avisos de cambio de estado al cliente (RFU09);
  - la bitácora general de quién cambió qué.
- **Fase 13:** el reporte exportable de cobranza.
- **Clave de idempotencia en la API** (para decidir): hoy la pantalla revisa antes de reintentar, pero no evita un doble envío desde otra pestaña.
- **Indicadores y reportes:** siguen calculando en el navegador, ahora con los pedidos de la API. Pasan a la API en las fases 12 y 13.

## Lo que no se verificó
- **El lado del cliente en la app con la cotización nueva:** tu ingreso reemplazó la sesión del cliente de prueba. La aprobación desde la cuenta del cliente queda cubierta por las pruebas de integración y de Playwright, y en la app se vio el seguimiento hasta «cotización en curso».
- **Un teléfono real:** la revisión se hizo con la vista de 375 px del navegador.

## Lo que necesito de ti

1. **Tu visto bueno a esta fase**, o lo que quieras cambiar.
2. Si quieres que la API tenga **clave de idempotencia** para cotizaciones, pedidos y abonos, o si basta la revisión que hace la pantalla.
