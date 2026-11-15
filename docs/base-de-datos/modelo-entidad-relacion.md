# Modelo entidad-relación — LetreriaLosHerreraDB

Estado del esquema: migración `MantenedoresYAvisos` (14-11-2026). Fuente única del esquema: el modelo de EF Core (`LetrerosHerreradbContext.cs`) y sus migraciones. El script completo para crear la base está en [`LetreriaLosHerreraDB.sql`](LetreriaLosHerreraDB.sql) y el detalle de cada columna en el [diccionario de datos](diccionario-de-datos.md).

27 tablas, 1 vista, 2 triggers y 1 procedimiento almacenado.

## Diagrama

GitHub y Visual Studio Code dibujan este bloque como imagen. Para exportarlo a PNG o SVG se puede pegar en <https://mermaid.live>.

```mermaid
erDiagram
    categorias ||--o{ productos : "agrupa"
    productos ||--o{ catalogo : "se publica en"
    productos ||--o{ solicitud_detalles : "tipo de letrero de"
    empleados |o--o{ catalogo : "crea"

    clientes ||--o{ solicitudes : "pide"
    empleados |o--o{ solicitudes : "registra (presencial)"
    solicitudes ||--o{ solicitud_detalles : "contiene"
    solicitudes |o--o{ adjuntos_solicitud : "tiene (vacío mientras es borrador)"
    clientes |o--o{ adjuntos_solicitud : "sube (borrador o archivo de su solicitud)"
    empleados |o--o{ adjuntos_solicitud : "sube desde la ficha"
    solicitudes ||--o{ solicitudes_historial : "registra cada cambio de estado"
    clientes |o--o{ solicitudes_historial : "cambia (envío en línea)"
    empleados |o--o{ solicitudes_historial : "cambia"

    solicitudes ||--o{ cotizaciones : "se cotiza en"
    empleados ||--o{ cotizaciones : "elabora"
    empleados |o--o{ cotizaciones : "registra la respuesta del cliente"
    cotizaciones ||--o{ cotizacion_materiales : "detalla"
    materiales ||--o{ cotizacion_materiales : "se usa en"

    solicitudes ||--o| pedidos : "se concreta en"
    cotizaciones |o--o| pedidos : "da a lo más un pedido"
    empleados ||--o{ pedidos : "es responsable de"
    pedidos ||--o{ pagos : "se paga con"
    empleados ||--o{ pagos : "registra"
    empleados |o--o{ pagos : "anula"
    pedidos ||--o{ pedidos_historial : "registra cada cambio de estado"
    empleados ||--o{ pedidos_historial : "cambia"

    empleados |o--o{ cartel_digital : "actualiza"
    empleados ||--o{ rostros_empleado : "registra su rostro"

    clientes |o--o{ consentimientos : "acepta"
    empleados |o--o{ consentimientos : "acepta"
    empleados ||--o{ imagenes : "sube"
    imagenes |o--o{ catalogo : "ilustra"
    empleados |o--o{ catalogo : "autoriza el logo"

    clientes |o--o{ tokens_renovacion : "tiene sesiones"
    empleados |o--o{ tokens_renovacion : "tiene sesiones"
    clientes |o--o{ codigos_cuenta : "recibe enlace o código"
    empleados |o--o{ codigos_cuenta : "recibe código de recuperación"

    empleados |o--o{ horarios_atencion : "modifica (fase 9)"
    empleados ||--o{ disponibilidad_empleados : "tiene sus franjas"
    empleados ||--o{ destinatarios_avisos : "pide el destino"
    destinatarios_avisos |o--o{ destinatarios_avisos : "reemplazado por"
    destinatarios_avisos ||--o{ destinatarios_avisos_historial : "registra cada acción"
    empleados |o--o{ destinatarios_avisos_historial : "hace la acción (nulo si confirma el enlace)"

    categorias {
        int id_categoria PK
        nvarchar nombre UK
        nvarchar descripcion
        bit activo
        rowversion row_version "fase 9"
    }
    productos {
        int id_producto PK
        int id_categoria FK
        nvarchar nombre UK "tipo de letrero"
        nvarchar descripcion
        decimal precio_base_ref
        bit activo
        rowversion row_version "fase 9"
    }
    catalogo {
        int id_catalogo PK
        int id_producto FK
        int id_empleado_creador FK
        nvarchar titulo
        nvarchar descripcion
        nvarchar imagen_url "antigua, ya opcional"
        int ancho_mm
        int alto_mm
        decimal precio_referencial
        datetime2 fecha_publicacion
        bit visible
        int id_imagen FK "fase 5"
        bit incluye_logo_cliente "fase 5"
        int id_empleado_autoriza_logo FK "fase 5"
        datetimeoffset fecha_autorizacion_logo "fase 5"
        rowversion row_version "fase 9"
    }
    clientes {
        int id_cliente PK
        varchar rut UK
        nvarchar nombre
        nvarchar apellido
        varchar telefono UK
        nvarchar email UK
        nvarchar direccion
        varchar password_hash
        bit registrado "calculada"
        datetime2 fecha_registro
        bit activo "0 = cuenta inhabilitada"
        datetimeoffset fecha_verificacion_correo "nulo = sin confirmar"
        rowversion row_version "fase 9"
    }
    empleados {
        int id_empleado PK
        varchar rut UK
        nvarchar nombre
        nvarchar apellido
        varchar telefono
        nvarchar email UK
        varchar password_hash
        nvarchar rol "administrador, dueño, diseñador, operario"
        bit activo
        rowversion row_version "fase 9"
    }
    solicitudes {
        int id_solicitud PK
        int id_cliente FK
        int id_empleado_registra FK
        nvarchar origen "calculada: online o presencial"
        nvarchar descripcion_general "hasta 2000"
        datetime2 fecha_solicitud
        nvarchar estado "solicitado, cotizado, aceptado, rechazado"
        nvarchar motivo_rechazo
        bit eliminado_logico
        datetime2 fecha_eliminacion_logica
        char huella_ip "HMAC de la IP; nunca la IP"
        rowversion row_version
    }
    solicitud_detalles {
        int id_solicitud_detalle PK
        int id_solicitud FK
        int id_producto FK
        nvarchar descripcion
        int ancho_mm
        int alto_mm
        int cantidad
        decimal area_m2 "calculada"
    }
    solicitudes_historial {
        int id_historial PK
        int id_solicitud FK
        nvarchar estado_anterior "nulo al crearse"
        nvarchar estado_nuevo
        nvarchar motivo "hasta 500"
        int id_empleado FK "o"
        int id_cliente FK "o"
        datetimeoffset fecha
    }
    adjuntos_solicitud {
        int id_adjunto PK
        int id_solicitud FK "nulo mientras es borrador"
        int id_cliente FK "o"
        int id_empleado FK "o"
        char clave UK "aleatoria, nombre del archivo en disco"
        varchar tipo "imagen o pdf"
        nvarchar nombre_original "saneado, hasta 120"
        int bytes
        int ancho "solo imagenes"
        int alto "solo imagenes"
        datetimeoffset fecha_subida
        datetimeoffset fecha_vinculo "presente si hay solicitud"
    }
    materiales {
        int id_material PK
        nvarchar nombre UK "material base"
        nvarchar unidad_medida
        decimal costo_unitario_aprox
        bit activo
        rowversion row_version "fase 9"
    }
    cotizaciones {
        int id_cotizacion PK
        int id_solicitud FK
        int id_empleado FK
        datetime2 fecha_cotizacion
        nvarchar observaciones
        decimal costo_materiales_total "lo mantiene el trigger"
        decimal costo_mano_obra
        decimal otros_costos
        decimal total_cotizacion "calculada"
        nvarchar estado "pendiente, aprobada, rechazada"
        datetimeoffset fecha_respuesta "fase 8, nula si pendiente"
        nvarchar via_respuesta "fase 8: cliente_web, telefono, whatsapp, presencial"
        int id_empleado_respuesta FK "fase 8, nulo si lo respondio el cliente"
        nvarchar motivo_rechazo "fase 8"
        rowversion row_version "fase 8"
        bit eliminado_logico "fase 8"
    }
    cotizacion_materiales {
        int id_cotizacion_material PK
        int id_cotizacion FK
        int id_material FK
        nvarchar medida_aplicada
        decimal cantidad
        decimal costo_unitario
        decimal subtotal "calculada"
    }
    pedidos {
        int id_pedido PK
        int id_solicitud FK "UK"
        int id_cotizacion FK "UK filtrado, fase 8"
        nvarchar origen "NUEVO: cotizacion o directo"
        int id_empleado_responsable FK
        datetime2 fecha_pedido
        datetime2 fecha_entrega_estimada
        datetime2 fecha_entrega_real
        nvarchar modalidad_entrega
        bit requiere_instalacion
        nvarchar direccion_instalacion
        decimal costo_instalacion_adicional
        decimal monto_base_trabajo
        decimal monto_total_final "calculada"
        nvarchar estado_pedido "RENOMBRADA: aceptado, en_proceso, terminado, entregado, anulado"
        int monto_abonado "NUEVO, suma de sus pagos"
        int saldo_pendiente "NUEVO, calculada"
        nvarchar estado_pago "NUEVO, calculada: no_pagado, abonado, pagado_total"
        nvarchar numero_factura "NUEVO, opcional"
        nvarchar motivo_anulacion "fase 8, solo si anulado"
        bit eliminado_logico "fase 8"
        rowversion row_version
    }
    pagos {
        int id_pago PK "NUEVA: historial de abonos"
        int id_pedido FK
        int monto "mayor que 0"
        date fecha_pago
        nvarchar medio_pago "efectivo, transferencia, tarjeta, cheque u otro"
        nvarchar observacion
        int id_empleado_registra FK
        datetime2 fecha_registro
        bit anulado "fase 8: el abono deja de sumar"
        nvarchar motivo_anulacion "fase 8"
        int id_empleado_anula FK "fase 8"
        datetimeoffset fecha_anulacion "fase 8"
    }
    pedidos_historial {
        int id_historial PK
        int id_pedido FK
        nvarchar estado_anterior "nulo al crearse"
        nvarchar estado_nuevo
        nvarchar nota "hasta 500"
        int id_empleado FK
        datetimeoffset fecha
    }
    horarios_atencion {
        int id_horario PK
        tinyint dia_semana UK
        nvarchar nombre_dia
        time hora_inicio
        time hora_fin
        bit abierto
        int id_empleado_modifica FK "fase 9"
        datetimeoffset fecha_modificacion "fase 9"
    }
    cartel_digital {
        int id_cartel PK "siempre 1"
        bit abierto_en_local "verde abierto, rojo cerrado"
        nvarchar mensaje_estado
        nvarchar tiempo_estimado_regreso
        datetime2 fecha_ultima_modificacion
        int id_empleado_modifica FK
    }
    rostros_empleado {
        int id_rostro PK
        int id_empleado FK
        varbinary descriptor_cifrado
        datetimeoffset fecha_consentimiento
        varchar version_consentimiento
        datetimeoffset fecha_revocacion
        int intentos_fallidos
        datetimeoffset bloqueado_hasta
        varbinary token_equipo_hash
        datetimeoffset fecha_autorizacion_equipo
    }
    consentimientos {
        int id_consentimiento PK
        int id_cliente FK "o"
        int id_empleado FK "o"
        varchar referencia_envio "o, visitante sin cuenta"
        varchar tipo "privacidad, terminos, marketing, cookies"
        varchar version_documento
        datetimeoffset fecha_aceptacion
        datetimeoffset fecha_revocacion
    }
    imagenes {
        int id_imagen PK
        char clave UK "aleatoria, nombre de los archivos"
        int ancho
        int alto
        int bytes_grande
        int bytes_miniatura
        bit es_ejemplo "fase 5"
        int id_empleado_sube FK
        datetimeoffset fecha_subida
    }
    consultas_contacto {
        int id_consulta PK
        nvarchar nombre "hasta 100"
        nvarchar correo "hasta 254"
        nvarchar telefono "opcional, +56"
        nvarchar asunto "hasta 150"
        nvarchar mensaje "hasta 2000"
        datetimeoffset fecha_recepcion
        bit revisada
        datetimeoffset fecha_revision
    }
    tokens_renovacion {
        bigint id_token PK
        int id_cliente FK "o"
        int id_empleado FK "o"
        char hash_token UK "SHA-256 del token"
        uniqueidentifier familia "un inicio de sesión"
        datetimeoffset fecha_creacion
        datetimeoffset fecha_expiracion
        datetimeoffset fecha_uso
        datetimeoffset fecha_revocacion
    }
    codigos_cuenta {
        bigint id_codigo PK
        int id_cliente FK "o"
        int id_empleado FK "o"
        varchar proposito "verificacion_correo, recuperacion"
        char hash_codigo "HMAC o SHA-256"
        nvarchar correo
        datetimeoffset fecha_expiracion
        datetimeoffset fecha_uso
        int intentos
    }
    correos_salientes {
        bigint id_correo PK
        varchar tipo
        varchar referencia "por ejemplo solicitud:123"
        nvarchar destinatarios
        nvarchar asunto
        varchar estado "pendiente, enviando, enviado, fallido"
        int intentos
        datetimeoffset proximo_intento
        nvarchar ultimo_error
    }
    disponibilidad_empleados {
        int id_disponibilidad PK
        int id_empleado FK
        tinyint dia_semana "1 lunes a 7 domingo"
        time hora_inicio
        time hora_fin
    }
    destinatarios_avisos {
        int id_destinatario PK
        nvarchar correo "UK filtrado: activo o pendiente"
        varchar estado "pendiente, activo, retirado, cancelado"
        int id_reemplaza FK "nulo si se agregó"
        char token_hash UK "SHA-256 del enlace; solo pendiente"
        datetimeoffset vence "solo pendiente"
        int id_empleado_solicita FK
        datetimeoffset fecha_solicitud
        datetimeoffset fecha_confirmacion
        datetimeoffset fecha_retiro
    }
    destinatarios_avisos_historial {
        int id_historial PK
        varchar accion "agregar, reemplazar, confirmar, cancelar, quitar, reenviar, probar"
        nvarchar correo_enmascarado
        int id_destinatario FK "nulo en las pruebas"
        int id_empleado FK "nulo si confirma el enlace"
        datetimeoffset fecha
        nvarchar detalle
    }
```

`horarios_atencion` se relaciona solo con `empleados`, por quién lo cambió por última vez. `correos_salientes` tampoco: es la cola de salida del correo, y cada fila dice a qué se refiere en `referencia`. `consultas_contacto` tampoco: es la bandeja del formulario de contacto de la portada, y su consentimiento de privacidad queda en `consentimientos` con la referencia `contacto:{id}`. La vista `vw_metricas_conversion_online` se calcula a partir de `solicitudes` y `pedidos`.

`adjuntos_solicitud` guarda los logos y diseños que acompañan a una solicitud. Mientras no tiene solicitud es un borrador del cliente, y se vincula al enviarla. Sus archivos son privados (carpeta `Adjuntos:Carpeta`, fuera del repositorio). `solicitudes_historial` guarda cada cambio de estado de una solicitud, con quién lo hizo: un empleado, el cliente o, si ninguno, el sistema.

`tokens_renovacion` y `codigos_cuenta` son de la cuenta: cada fila pertenece a un cliente o a un empleado, nunca a los dos, y se borran con su titular. `clientes.fecha_verificacion_correo` dice si el correo del cliente está confirmado.

`pedidos_historial` guarda cada cambio de estado de un pedido, con quién lo hizo (fase 8). Una cotización o un pedido eliminados no se borran: se marcan con `eliminado_logico`, y el pedido eliminado sigue ocupando su cotización (`uq_pedidos_cotizacion`). Un abono anulado tampoco se borra: queda en `pagos` con `anulado = 1`.

`disponibilidad_empleados` guarda las franjas semanales de cada empleado (RFU20). La API impide que se crucen, y la base solo exige que cada franja empiece antes de terminar. `destinatarios_avisos` guarda los correos que reciben los avisos de solicitudes nuevas, con su estado y el hash de su enlace de confirmación; `destinatarios_avisos_historial` registra cada acción sobre ellos, con la dirección enmascarada. El diagrama de sus estados está al final de este documento.

## Flujo de negocio que expresa el modelo

```mermaid
flowchart LR
    S["Solicitud<br/>online o presencial"] --> C["Cotización<br/>materiales, mano de obra, otros"]
    C -- "aprobada por el cliente<br/>o por el personal" --> P["Pedido<br/>origen = cotizacion"]
    T["Acuerdo en el taller<br/>sin solicitud previa"] -- "se generan solicitud<br/>y cotización internas" --> P2["Pedido<br/>origen = directo"]
```

**Pedido presencial directo.** Cuando un cliente acuerda el trabajo en el taller, el personal registra el pedido de una vez. En la misma transacción se crean una solicitud presencial (con sus ítems: tipo de letrero, medidas, cantidad y glosa) y una cotización aprobada (con el material base, la mano de obra y la instalación). Así todo pedido conserva su detalle en las mismas tablas, y los reportes y las búsquedas por cliente funcionan igual para los dos orígenes. La columna `origen` del pedido los distingue.

## Estados del pedido: dos atributos independientes

Un pedido puede fabricarse y entregarse sin estar pagado, por ejemplo cuando un cliente habitual paga a fin de mes. También puede pagarse por adelantado o con un abono parcial. Por eso el estado operativo y el estado de pago son columnas distintas.

Lo que sigue es la propuesta de la fase 7, **implementada en la fase 8**: la API valida las transiciones del pedido (`TransicionesPedido`, en `Negocio/Pedidos/ReglasPedido.cs`), y la base calcula el estado de pago.

```mermaid
stateDiagram-v2
    direction LR
    state "Estado del pedido (operativo)" as op {
        [*] --> aceptado
        aceptado --> en_proceso
        en_proceso --> terminado
        terminado --> entregado : se registra la fecha real
        en_proceso --> aceptado : vuelta atrás
        terminado --> en_proceso : vuelta atrás
        aceptado --> anulado : con motivo
        en_proceso --> anulado : con motivo
        terminado --> anulado : con motivo
        entregado --> [*]
        anulado --> [*]
    }
```

```mermaid
stateDiagram-v2
    direction LR
    state "Estado de pago (financiero, calculado)" as pago {
        [*] --> no_pagado
        no_pagado --> abonado : abono parcial
        no_pagado --> pagado_total : pago completo
        abonado --> pagado_total : se completa el saldo
        abonado --> no_pagado : se anula el único abono
        pagado_total --> abonado : se anula un abono, o sube el monto final
        pagado_total --> no_pagado : se anula el único abono
    }
```

- **«Anulado» reemplaza a «cancelado»**, porque en Chile «cancelado» se entiende como «pagado».
- **Cada abono queda en `pagos`** con su monto, fecha, medio de pago y quién lo registró. El trigger `trg_pagos_recalcular_monto_abonado` mantiene `monto_abonado` igual a la suma de los abonos vigentes del pedido. Desde la fase 8, un abono se **anula con motivo** en vez de editarse o borrarse: deja de sumar, queda tachado en el pedido con quién y cuándo, y no se puede reactivar.
- **El estado de pago no se escribe a mano.** La base lo calcula a partir de `monto_abonado` y del monto final: sin abono es `no_pagado`, con el total cubierto es `pagado_total`, y entre ambos es `abonado`. Así nunca contradice a los montos.
- **Saldo pendiente** = monto final − monto abonado. La base impide abonar más que el monto final (`ck_pedidos_monto_abonado`): un pago que haría superarlo no se guarda.
- **Cierre del ciclo:** un pedido está cerrado solo cuando su estado es `entregado` y su pago es `pagado_total`.
- **Cobranza:** el índice `ix_pedidos_cobranza` resuelve el listado de pedidos no anulados con pago `no_pagado` o `abonado` y su saldo.
- **Transiciones (fase 8).** Además del avance, se puede volver un paso atrás (de «en proceso» a «aceptado», o de «terminado» a «en proceso») por si alguien se equivocó; queda en el historial. «Entregado» pide la fecha real, que no puede ser futura ni anterior a la del pedido. «Anulado» pide un motivo de 10 a 500 caracteres. «Entregado» y «anulado» no cambian más. Cualquier otro cambio responde `409 transicion_invalida` con la explicación. Cada cambio queda en `pedidos_historial`, con quién y cuándo.
- **Montos.** Solo gestión cambia el monto base o el costo de instalación de un pedido ya creado, y el cambio queda en el historial. El monto final no puede quedar bajo lo abonado.
- **Anular con abonos (decisión 4, 14-11-2026).** Solo gestión puede anular un pedido que tiene o tuvo abonos. Lo abonado queda como «a devolver o a favor del cliente», y la devolución no se registra en el sistema.
- **Pago previo.** No se exige para crear el pedido (RNFO02). Un pedido puede entregarse sin pagar y cerrarse cuando se completa el pago.


## Estados de la solicitud y de la cotización (fase 7)

Diagramas aprobados en el plan de la fase 7 (`docs/plan/fase-07-plan.md`, 14-11-2026). La API aplica las transiciones de la solicitud en `Negocio/Solicitudes/ReglasSolicitud.cs` (`TransicionesSolicitud`). Las de la cotización las aplica `ServicioCotizaciones` desde la fase 8.

### Solicitud

```mermaid
stateDiagram-v2
    direction LR
    [*] --> solicitado : el cliente la envía o el personal la registra
    solicitado --> cotizado : el personal emite una cotización
    cotizado --> solicitado : el cliente rechaza la cotización (se puede cotizar de nuevo)
    cotizado --> aceptado : se aprueba la cotización (nace el pedido)
    solicitado --> rechazado : el personal la rechaza, con motivo
    cotizado --> rechazado : el personal la rechaza, con motivo
    aceptado --> [*]
    rechazado --> [*]
```

- **Quién cambia cada estado.** El personal rechaza una solicitud con motivo, de 10 a 500 caracteres, y puede devolver una «cotizado» a «solicitado». El sistema pasa a «cotizado» y a «aceptado» al emitir y aprobar una cotización (fase 8). El diagrama aprobado muestra la vuelta a «solicitado» solo para el cliente; la API también la permite al personal (`PATCH /solicitudes/{id}/estado`), que además la hace sola al editar una cotizada.
- **Edición:** los datos y los ítems solo cambian en «solicitado» o «cotizado». Editar en «cotizado» deja la cotización vigente como «rechazada» y la solicitud vuelve a «solicitado».
- **Borrado:** es lógico (la fila se conserva), solo en «solicitado» o «rechazado», y sin pedido.
- **Transición inválida:** responde `409` con `transicion_invalida` y una explicación (por ejemplo, «Una solicitud «rechazado» ya no cambia de estado.»).
- **Historial:** cada transición escribe una fila en `solicitudes_historial`, con quién la hizo y cuándo.

### Cotización

```mermaid
stateDiagram-v2
    direction LR
    [*] --> pendiente
    pendiente --> aprobada : el cliente o el personal la aprueba
    pendiente --> rechazada : el cliente la rechaza, o la solicitud se edita
    aprobada --> [*]
    rechazada --> [*]
```

- **Varias por solicitud, una pendiente a la vez.** Las anteriores quedan como historial. Desde la fase 8, la base lo garantiza con el índice único filtrado `uq_cotizaciones_una_pendiente`, así que dos personas que cotizan a la vez no pueden dejar dos pendientes. Emitir sobre una solicitud que ya tiene una pendiente responde `409`.
- **Efecto en la solicitud (fase 8).** Al emitir, la solicitud pasa de «solicitado» a «cotizado». Al aprobar, pasa a «aceptado» y se abre el paso a pedido. Al rechazar, vuelve a «solicitado» y puede cotizarse de nuevo. Cada paso queda en `solicitudes_historial`.
- **Eliminar (gestión).** Es un borrado lógico, solo de una pendiente o rechazada sin pedido. Si la pendiente se elimina, la solicitud vuelve a «solicitado».
- **Cuándo queda rechazada una pendiente:** también al rechazar o devolver la solicitud, además de cuando el cliente la rechaza o la solicitud se edita. La API ya lo hace en esos casos.
- **Edición:** una cotización aprobada o rechazada ya no se edita.
- **Pedido y pago:** implementados en la fase 8, como se describe en la sección «Estados del pedido». Un pedido nace de una cotización aprobada, y `uq_pedidos_cotizacion` da a lo más un pedido por cotización.

## Destinos de los avisos del taller (fase 9)

Diagrama del plan de la fase 9 (`docs/plan/fase-09-plan.md`, sección 1). La API aplica estas transiciones en `Services/ServicioDestinosAvisos.cs`. La base impide dos destinos vigentes con el mismo correo (índice filtrado `uq_destinatarios_avisos_vigente`) y un enlace que no corresponda a un pendiente (`ck_destinatarios_avisos_enlace`).

```mermaid
stateDiagram-v2
    direction LR
    [*] --> pendiente : se pide un correo (agregar o reemplazar)
    pendiente --> activo : confirma desde el enlace (un solo uso, 24 horas)
    pendiente --> cancelado : el personal lo cancela
    pendiente --> cancelado : vence sin confirmarse
    activo --> retirado : se quita (queda al menos otro activo)
    activo --> retirado : el destino nuevo que lo reemplaza se confirma
    retirado --> [*]
    cancelado --> [*]
```

- **Quién cambia cada estado.** El dueño o el administrador pide un destino, reenvía su enlace, cancela un cambio pendiente o quita un destino activo. Quien recibe el correo confirma sin sesión, desde `/avisos/confirmar`.
- **Reenviar no cambia el estado.** Un enlace nuevo deja sin efecto al anterior, y el destino sigue `pendiente`. Mientras esté `pendiente`, la dirección no recibe avisos.
- **Reemplazar no corta el aviso.** La dirección anterior sigue `activo` hasta que la nueva se confirma. Si nunca se confirma, no cambia nada, y un error de tipeo no deja sin avisos.
- **Vencimiento.** Un pendiente vencido ya no sirve su enlace y no cuenta para el tope de pendientes. Pasa a `cancelado` cuando alguien vuelve a pedir ese mismo correo o un reemplazo de ese destino. No hay un proceso en segundo plano que lo haga solo.
- **Último activo.** Quitar el último destino activo responde `409 ultimo_destino`. Para cambiarlo se reemplaza (decisión 3 del 14-11-2026).
- **Quién recibe los avisos.** `IDestinatariosAvisos` (`Datos/Correo/DestinatariosAvisos.cs`) devuelve los activos de esta tabla. Si no hay ninguno, usa `Correo:AvisosTaller`. No tiene caché.
- **Enlace.** Se genera al pedir o reenviar, se guarda solo su SHA-256 (`token_hash`) y vence en 24 horas. Al confirmar, `token_hash` y `vence` quedan nulos, así que el enlace sirve una sola vez.
- **Límites.** Tres pendientes a la vez como máximo, y un envío de enlace por minuto y cinco por hora por dirección (agregar, reemplazar o reenviar).
