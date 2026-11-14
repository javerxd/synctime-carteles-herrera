# Especificación técnica de la fase 7 (contrato entre backend y frontend)

Acompaña a `fase-07-plan.md` (aprobado el 14-11-2026). Si algo no está aquí, decide lo más simple y coherente con el plan y anótalo en tu respuesta.

## Decisiones fijas
- **Solicitudes en línea:** exigen sesión de **cliente con correo confirmado**. La ruta pública `POST /api/v1/solicitudes/online` **se elimina**.
- **Botón «Solicitar cotización» de la portada:** lleva al formulario de solicitud del cliente, `/cuenta/solicitud`. Sin sesión, pasa por `/ingresar` (con regreso); sin correo confirmado, muestra el aviso con «Reenviar enlace».
- **Solicitudes presenciales:** **no** envían aviso al taller (decisión de la fase 4, confirmada el 14-11-2026). Sí envían confirmación al cliente si tiene correo.
- **Fechas:** todas se muestran en hora de Chile (`HoraChile`).
- **Errores:** usan el formato estándar `{ error, codigo, nivel, errores? }`.

## Base de datos (migración nueva `SolicitudesYAdjuntos`, solo agrega)

**`adjuntos_solicitud`**, una fila por archivo adjunto:

| Columna | Tipo y reglas |
|---|---|
| `id_adjunto` | int, identity |
| `id_solicitud` | int null, FK a `solicitudes`; null mientras es borrador |
| `id_cliente` | int null, quién lo subió si fue el cliente |
| `id_empleado` | int null, quién lo subió si fue el personal |
| `clave` | char(32), aleatoria; nombra el archivo en disco |
| `tipo` | `'imagen'` o `'pdf'` |
| `nombre_original` | nvarchar(120), saneado |
| `bytes` | int |
| `ancho`, `alto` | int null, solo imágenes |
| `fecha_subida` | datetimeoffset |
| `fecha_vinculo` | datetimeoffset null, cuándo se vinculó a la solicitud |

- **Reglas de la tabla:**
  - exactamente uno de `id_cliente` e `id_empleado` (check);
  - `fecha_vinculo` está presente si y solo si `id_solicitud` está presente;
  - índices por solicitud y por cliente borrador.
- **Archivos en disco:** en una carpeta **privada** (`Adjuntos:Carpeta`, por defecto `almacen/adjuntos`), fuera de la carpeta pública de imágenes. Se quedan fuera del repositorio.

**`solicitudes_historial`**, cada cambio de estado de una solicitud:

| Columna | Tipo y reglas |
|---|---|
| `id_historial` | int, identity |
| `id_solicitud` | FK a `solicitudes` |
| `estado_anterior` | null |
| `estado_nuevo` | |
| `motivo` | nvarchar(500) null |
| `id_empleado` | int null |
| `id_cliente` | int null |
| `fecha` | datetimeoffset |

**`solicitudes`**, columna nueva:
- `huella_ip` (char(64) null): el HMAC-SHA256 de la IP con `Jwt:Key`. Nunca se guarda la IP en claro. Sirve para la regla contra solicitudes falsas.

## Reglas de negocio
- **Transiciones de la solicitud:**

| Desde | Hacia | Quién |
|---|---|---|
| solicitado | cotizado | sistema, al emitir una cotización (fase 8) |
| cotizado | solicitado | el cliente rechaza la cotización, o se edita en «cotizado» |
| cotizado | aceptado | se aprueba la cotización (fase 8) |
| solicitado o cotizado | rechazado | el personal, con motivo obligatorio (10 a 500 caracteres) |

  - Cualquier otra transición responde **409** con la explicación.
  - Cada transición escribe una fila en `solicitudes_historial`.
- **Edición:** solo en «solicitado» o «cotizado».
  - Usa control de concurrencia con `RowVersion`: el cliente envía `If-Match` con la versión que vio y, si otra persona cambió el registro, recibe 409 con «Otra persona modificó esta solicitud…».
  - Editar en «cotizado» deja la cotización pendiente como «rechazada» y la solicitud vuelve a «solicitado».
- **Borrado lógico:** solo en «solicitado» o «rechazado».
- **Regla contra solicitudes falsas (RFS04, RNFP33), al enviar en línea:**
  - **Se rechaza si:**
    - la cuenta tiene una solicitud en línea pendiente («solicitado» o «cotizado», no eliminada);
    - otra solicitud pendiente tiene el mismo RUT del cliente;
    - la huella de la IP tiene una solicitud en línea pendiente;
    - la IP ya envió 3 solicitudes en línea en las últimas 24 horas.
  - **Cómo se rechaza:** **no se escribe nada** (ni solicitud, ni adjuntos vinculados, ni correos) y se responde **409** con `codigo: "contacto_directo"`.
  - **Mensaje:** el mismo para todos los motivos, y no revela de quién es la otra solicitud: «Ya tenemos una solicitud tuya en revisión. Para agregar algo o pedir otra cosa, escríbenos por WhatsApp o llámanos.».
  - **Contacto:** el frontend lo arma con el horario, el estado del cartel (`GET /api/v1/local/estado`), el teléfono y el WhatsApp de `src/configuracion/taller.js`.
- **Ítems:**
  - **Datos:** de 1 a 10 ítems; cada uno con `idProducto` (activo), `descripcion` opcional (máximo 500), `anchoMm` y `altoMm` (ambos o ninguno; de 1 a 50 000) y `cantidad` (de 1 a 999).
  - **Área:** `areaM2` = ancho × alto / 1 000 000 × cantidad, redondeada a 2 decimales y calculada en el servidor.
  - **Descripción general:** obligatoria, de 10 a 2000 caracteres.

## Adjuntos
- **Formatos:**
  - JPG, PNG y WebP: se reconocen por su contenido y se procesan con el `ProcesadorImagenes` existente. Quedan como WebP grande (1600 px) y miniatura (640 px), sin metadatos.
  - PDF: se reconoce porque empieza con `%PDF-`. Se guarda tal cual.
  - Lo demás se rechaza con 400 «Formato no aceptado. Sube JPG, PNG, WebP o PDF; si tu diseño es SVG o AI, expórtalo a PDF o PNG.».
- **Límites:** 10 MB por archivo y **5 por solicitud**. El tope se aplica al subir (borradores del cliente sin vincular) y al vincular.
- **Rutas:**
  - `POST /api/v1/mis/adjuntos` (cliente)
    - Cuerpo: multipart con el campo `archivo`.
    - Responde **201** con `{ idAdjunto, tipo, nombre, bytes, ancho, alto, urlMiniatura }`. `urlMiniatura` es `/api/v1/adjuntos/{id}/miniatura` si es imagen y `null` si es PDF.
    - Errores: 400 si el formato o el tamaño no sirven; 409 si ya hay 5 borradores.
  - `DELETE /api/v1/mis/adjuntos/{id}` (cliente): **204**, solo si es un borrador suyo; si no, 404.
  - `GET /api/v1/adjuntos/{id}/miniatura` y `GET /api/v1/adjuntos/{id}/archivo` (sesión)
    - Pueden verlos el cliente dueño (subido por él o de una solicitud suya) y cualquier empleado; a todos los demás se responde 404.
    - Cabeceras: `Cache-Control: private, no-store`, `X-Content-Type-Options: nosniff`, y para el PDF `Content-Disposition: attachment` con un nombre seguro.
    - El frontend las pide con el token: las descarga con `fetch` y las muestra con `URL.createObjectURL`, que libera al quitarlas.
  - `POST /api/v1/solicitudes/{id}/adjuntos` (personal): sube y vincula de inmediato, con el mismo tope de 5. `DELETE /api/v1/solicitudes/{id}/adjuntos/{idAdjunto}` (personal).
- **Purga:** `PurgaDatosPersonales` borra los borradores con más de 24 horas, con sus archivos.

## Rutas de solicitudes

**Cliente:**
- `POST /api/v1/mis/solicitudes` (cliente)
  - Cuerpo: `{ descripcionGeneral, detalles: [{ idProducto, descripcion?, anchoMm?, altoMm?, cantidad }], adjuntos: [idAdjunto] }`.
  - Respuestas:
    - **201** con el **comprobante** (forma abajo);
    - **403** con `codigo: "correo_sin_confirmar"` si el correo no está confirmado;
    - **409** con `codigo: "contacto_directo"` si lo frena la regla contra solicitudes falsas;
    - **400** con errores por campo (`descripcionGeneral`, `detalles[0].anchoMm`…, `adjuntos`).
  - Vincula los adjuntos (borradores del mismo cliente) y encola el aviso al taller y la confirmación al cliente, **todo en una transacción**.
- `GET /api/v1/mis/solicitudes` (cliente): sus solicitudes, paginadas, con el resumen.
- `GET /api/v1/mis/solicitudes/{id}` (cliente): su comprobante. Si no es suya, 404.

**Comprobante:**
```
{ idSolicitud, numero: "#123", fechaChile: "2026-11-14T10:32:00", origen: "online"|"presencial", estado,
  cliente: { nombre, apellido, telefono, correo },
  detalles: [{ producto, descripcion, anchoMm, altoMm, cantidad, areaM2 }], areaTotalM2,
  descripcionGeneral, adjuntos: [{ idAdjunto, tipo, nombre, bytes, urlMiniatura }], rowVersion }
```

**Personal:**
- `GET /api/v1/solicitudes` (personal)
  - Parámetros: `estado`, `origen`, `desde`, `hasta`, `q` (busca por número, nombre, teléfono o RUT), `orden` (`fecha_desc` por defecto, `fecha_asc`), `pagina` y `tamano`. Usa `ParametrosListado` y `ResultadoPaginado`.
  - Cada fila trae el número, la fecha de Chile, el cliente, el origen, el estado, la cantidad de ítems y de adjuntos, y quién la registró.
- `GET /api/v1/solicitudes/{id}` (personal): el comprobante, el historial de estados y `registradaPor`.
- `POST /api/v1/solicitudes` (personal): registro presencial, con `{ idCliente, descripcionGeneral, detalles }`. Responde 201 con el comprobante.
- `PUT /api/v1/solicitudes/{id}` (personal): edición con `If-Match`.
- `PATCH /api/v1/solicitudes/{id}/estado` (gestión): el cuerpo es `{ estado: "rechazado", motivo }` o la vuelta a «solicitado». Responde 409 si la transición no es válida.
- `PATCH /api/v1/solicitudes/{id}/eliminar-logico` (gestión): se mantiene.

**Clientes:**
- `GET /api/v1/clientes` (personal): paginado, con `q` que busca por teléfono, RUT o nombre.
- `GET /api/v1/clientes/buscar?telefono=&rut=` (personal): para el registro presencial; devuelve 0 o 1 cliente.

## Correos (cola de la fase 4)
- **Aviso al taller:** se adapta `AvisoSolicitudNueva`.
  - Asunto «Nueva solicitud #123 - Nombre Apellido».
  - Contenido: fecha de Chile, origen, cliente con nombre y teléfono (con el contacto de un toque ya existente), ítems con medidas, área y cantidad, descripción y «N archivos adjuntos (se ven en el panel)».
  - Botón «Ver solicitud»: `{Sitio:UrlPublica}/panel/solicitudes/{id}`.
  - Los archivos no se adjuntan.
- **Confirmación al cliente:** `CorreoParaCliente` con el comprobante. El asunto es «Recibimos tu solicitud #123».
- **Cuándo se escriben:** los dos en la cola dentro de la misma transacción que la solicitud, respetando el tope por hora de los avisos del taller. Si se supera el tope, la solicitud se guarda igual y el aviso se resume, como en la fase 4.
- **Solicitudes frenadas:** las que frena la regla contra solicitudes falsas no generan correos.

## Frontend (rutas)
- `/cuenta/solicitud`: formulario del cliente. Lleva ítems repetibles, la descripción y la **zona de adjuntos**, y termina en el comprobante.
- `/cuenta/solicitudes`: «Mis solicitudes», con la lista y el comprobante de cada una.
- `/panel/solicitudes`: bandeja con filtros, búsqueda y paginación. `/panel/solicitudes/:id`: ficha con adjuntos, historial y acciones de estado.
- `/panel/solicitudes/nueva`: registro presencial (busca al cliente antes de crearlo).
- `/panel/clientes` y `/panel/clientes/:id`.
- `/panel` (inicio): las solicitudes pendientes. Ahí llegan el diseñador y el operario al ingresar.
- **Zona de adjuntos:**
  - Miniaturas cuadradas uniformes de 96 px. Un PDF se ve como una tarjeta del mismo tamaño, con ícono y nombre recortado.
  - Botón «×» de al menos 24 px con `aria-label="Quitar {nombre}"`, progreso por archivo y contador «N de 5».
  - Errores por archivo, arrastrar y soltar, `accept="image/jpeg,image/png,image/webp,application/pdf"`.
  - La entrada y la salida de cada archivo animan opacidad y escala de 0.96 a 1 en 160 ms ease-out, y nada con `prefers-reduced-motion`.
