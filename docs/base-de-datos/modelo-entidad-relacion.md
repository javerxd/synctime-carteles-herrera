# Modelo entidad-relación — LetreriaLosHerreraDB

Estado del esquema: migración `HistorialDePagos` (12-11-2026). Fuente única del esquema: el modelo de EF Core (`LetrerosHerreradbContext.cs`) y sus migraciones. El script completo para crear la base está en [`LetreriaLosHerreraDB.sql`](LetreriaLosHerreraDB.sql) y el detalle de cada columna en el [diccionario de datos](diccionario-de-datos.md).

15 tablas, 1 vista, 2 triggers y 1 procedimiento almacenado.

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

    solicitudes ||--o{ cotizaciones : "se cotiza en"
    empleados ||--o{ cotizaciones : "elabora"
    cotizaciones ||--o{ cotizacion_materiales : "detalla"
    materiales ||--o{ cotizacion_materiales : "se usa en"

    solicitudes ||--o| pedidos : "se concreta en"
    cotizaciones |o--o{ pedidos : "origina"
    empleados ||--o{ pedidos : "es responsable de"
    pedidos ||--o{ pagos : "se paga con"
    empleados ||--o{ pagos : "registra"

    empleados |o--o{ cartel_digital : "actualiza"
    empleados ||--o{ rostros_empleado : "registra su rostro"

    categorias {
        int id_categoria PK
        nvarchar nombre UK
        nvarchar descripcion
        bit activo
    }
    productos {
        int id_producto PK
        int id_categoria FK
        nvarchar nombre UK "tipo de letrero"
        nvarchar descripcion
        decimal precio_base_ref
        bit activo
    }
    catalogo {
        int id_catalogo PK
        int id_producto FK
        int id_empleado_creador FK
        nvarchar titulo
        nvarchar descripcion
        nvarchar imagen_url
        int ancho_mm
        int alto_mm
        decimal precio_referencial
        datetime2 fecha_publicacion
        bit visible
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
    }
    solicitudes {
        int id_solicitud PK
        int id_cliente FK
        int id_empleado_registra FK
        nvarchar origen "calculada: online o presencial"
        nvarchar descripcion_general
        datetime2 fecha_solicitud
        nvarchar estado "solicitado, cotizado, aceptado, rechazado"
        nvarchar motivo_rechazo
        bit eliminado_logico
        datetime2 fecha_eliminacion_logica
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
    materiales {
        int id_material PK
        nvarchar nombre UK "material base"
        nvarchar unidad_medida
        decimal costo_unitario_aprox
        bit activo
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
        int id_cotizacion FK
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
    }
    horarios_atencion {
        int id_horario PK
        tinyint dia_semana UK
        nvarchar nombre_dia
        time hora_inicio
        time hora_fin
        bit abierto
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
```

`horarios_atencion` no se relaciona con otras tablas: guarda el horario semanal publicado. La vista `vw_metricas_conversion_online` se calcula a partir de `solicitudes` y `pedidos`.

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

```mermaid
stateDiagram-v2
    direction LR
    state "Estado del pedido (operativo)" as op {
        [*] --> aceptado
        aceptado --> en_proceso
        en_proceso --> terminado
        terminado --> entregado
        aceptado --> anulado
        en_proceso --> anulado
        terminado --> anulado
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
    }
```

- **«Anulado» reemplaza a «cancelado»**, porque en Chile «cancelado» se entiende como «pagado».
- **Cada abono queda en `pagos`** con su monto, fecha, medio de pago y quién lo registró. El trigger `trg_pagos_recalcular_monto_abonado` mantiene `monto_abonado` igual a la suma de los pagos del pedido.
- **El estado de pago no se escribe a mano.** La base lo calcula a partir de `monto_abonado` y del monto final: sin abono es `no_pagado`, con el total cubierto es `pagado_total`, y entre ambos es `abonado`. Así nunca contradice a los montos.
- **Saldo pendiente** = monto final − monto abonado. La base impide abonar más que el monto final (`ck_pedidos_monto_abonado`): un pago que haría superarlo no se guarda.
- **Cierre del ciclo:** un pedido está cerrado solo cuando su estado es `entregado` y su pago es `pagado_total`.
- **Cobranza:** el índice `ix_pedidos_cobranza` resuelve el listado de pedidos no anulados con pago `no_pagado` o `abonado` y su saldo.
- Las transiciones del estado operativo (qué paso puede seguir a cuál) las valida la capa de negocio en la fase 8. El diagrama de arriba es la propuesta que se confirmará en la fase 7.
