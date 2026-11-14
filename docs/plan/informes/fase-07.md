# Informe de cierre — Fase 7: solicitudes y clientes

Fecha: 14-11-2026 · Rama: `fase-07-solicitudes-y-clientes` (nace de `fase-06-cuentas-y-sesion`) · Estado: **aprobada el 14-11-2026**

Plan aprobado: `fase-07-plan.md`, con su especificación en `fase-07-especificacion.md`. Además de lo previsto, incluye dos pedidos tuyos del 14-11-2026: **diseños adjuntos** en la solicitud y **avisos por correo** de cada solicitud nueva.

## Qué se hizo

### Solicitud del cliente
- **Quién la envía:** se envía desde la cuenta con el **correo confirmado**. La ruta pública anterior, sin cuenta, se eliminó. El botón «Solicitar cotización» de la portada lleva al formulario, pasando por el ingreso si hace falta.
- **Formulario:** hasta 10 letreros, cada uno con tipo, ancho y alto en cm, cantidad y detalle opcional. El área estimada se ve en vivo y el servidor la recalcula. La descripción es obligatoria.
- **Se conserva lo escrito:** lo que va escrito se guarda en la pestaña, y si la sesión vence o el cliente sale, se recupera al volver.
- **Comprobante:** número, fecha y hora de Chile, cliente, letreros con medidas, área y cantidad, archivos y estado. Es el mismo en la pantalla, en «Mis solicitudes» y en el correo.
- **Regla contra solicitudes falsas (RFS04, RNFP33):** se frena el envío en cuatro casos:
  - la cuenta ya tiene una solicitud en línea pendiente;
  - otra solicitud pendiente tiene el mismo RUT;
  - la conexión ya tiene una solicitud en línea pendiente;
  - la conexión ya envió 3 en la última hora (RNFP33, configurable).

  En esos casos **no se escribe nada** y se muestra el aviso de contacto directo con el texto de S25, (horario, cartel, teléfono y WhatsApp), igual para todos los motivos. La IP no se guarda: solo una huella cifrada.

### Diseños adjuntos (tu pedido)
- **Cuánto se puede adjuntar:** hasta **5 archivos de 10 MB** por solicitud.
- **Formatos:**
  - JPG, PNG y WebP se convierten a WebP sin metadatos.
  - Los PDF se reconocen por su contenido.
  - SVG, AI y similares no se aceptan, porque pueden llevar código; el mensaje pide exportarlos a PDF o PNG.
- **Cómo se ve:**
  - Se puede arrastrar y soltar o elegir archivos; en el teléfono se abre la cámara o la galería.
  - Cada archivo cargado se ve como una **miniatura cuadrada del mismo tamaño**. Un PDF se ve como una tarjeta del mismo tamaño, con su ícono y su nombre.
  - Cada uno tiene una **«×» para quitarlo**, más grande en pantallas táctiles, y hay progreso por archivo y un contador «N de 5». Los errores salen por archivo.
- **Si el cliente sale del formulario:** los archivos que ya había subido se recuperan al volver.
- **Privacidad:**
  - Los adjuntos **no son públicos**.
  - Solo los ve el cliente dueño (y deja de verlos si la solicitud se elimina) y el personal, siempre con sesión.
  - No se guardan en la caché del navegador.
  - Un PDF se descarga en vez de abrirse en la página.
- **Borradores:** los archivos que nunca se envían se borran solos a las 24 horas.
- **Personal:** el personal ve los adjuntos en la ficha y los abre en grande; también puede adjuntar en solicitudes abiertas. Quitar un adjunto queda en el historial.

### Avisos por correo (tu pedido)
- **Aviso al taller:**
  - Asunto «Nueva solicitud #123 - Nombre Apellido».
  - Contenido: fecha y hora de Chile, origen, cliente con nombre y teléfono, letreros con medidas, área y cantidad, descripción y cuántos archivos trae.
  - El botón **«Ver solicitud»** abre la ficha en el panel y exige ingresar como personal.
  - Los archivos no viajan en el correo.
- **Confirmación al cliente:** su comprobante, desde la casilla automática, con el pie de «no respondas».
- **Cuándo se escriben:** los dos correos se escriben en la cola **en la misma transacción que la solicitud**. No puede salir un aviso de algo que no se guardó, ni quedar una solicitud guardada sin su aviso. El envío lo hace la cola de la fase 4, fuera de la petición, así que un fallo del servidor de correo no impide guardar la solicitud.
- **Solicitudes frenadas:** las que frena la regla contra solicitudes falsas no generan correos.
- **Presenciales:** no avisan al taller (tu decisión de la fase 4). Confirman al cliente solo si su correo está confirmado.

### Personal
- **Bandeja:**
  - Filtros por estado, origen y fecha, búsqueda por número, nombre, teléfono o RUT, orden y paginación.
  - Los filtros quedan en la dirección, así que se pueden compartir.
- **Ficha:**
  - Comprobante, adjuntos, historial de estados y quién la registró.
  - Si otra persona la modificó mientras la veías, avisa sin perder lo escrito.
- **Registro presencial:** busca al cliente por teléfono o RUT antes de crearlo.
- **Clientes:** listado y ficha.
- **Inicio del panel:** muestra las solicitudes por atender. Ahí llegan el diseñador y el operario.
- **Estados** (diagramas aprobados):
  - todo el personal puede rechazar (con motivo de 10 a 500 caracteres), devolver a «solicitado» y eliminar, según RFU11;
  - el borrado definitivo es solo de gestión;
  - una transición no válida responde 409 con explicación.

### Migración `SolicitudesYAdjuntos`
- **Qué agrega:**
  - las tablas `adjuntos_solicitud` y `solicitudes_historial`;
  - la columna `solicitudes.huella_ip`;
  - amplía tres textos (sin perder datos).
- **Comprobaciones:** se probó subirla, revertirla y volver a subirla en una base temporal, que se borró después.
- **Dónde está aplicada:** en la demo. **No en la real**, que sigue en `AgregarRostrosEmpleado`.

## Verificación

| Comprobación | Resultado |
|---|---|
| Backend | 263 unitarias y 296 de integración, en verde |
| Frontend | 288 de Vitest y 104 de Playwright con axe (las pantallas nuevas a 360, 768 y 1440 px), en verde; lint sin errores; build correcto |
| Pruebas no vacías | Quitar la regla contra solicitudes falsas, permitir ver un adjunto ajeno o encolar fuera de la transacción hace fallar su prueba |
| **De punta a punta, con Gmail real** | En la app sobre la base demo, el cliente de prueba envió la solicitud #19 con un logo adjunto:<br>• vio su comprobante;<br>• **el aviso «Nueva solicitud #19 - Prueba Fase Seis» salió por smtp.gmail.com al primer intento hacia la casilla configurada en tus User Secrets**;<br>• la confirmación al cliente también salió;<br>• el enlace «Ver solicitud» lleva al acceso del personal |

**Revisa tu Gmail:** debería estar el aviso de la solicitud #19. La confirmación se envió a la dirección ficticia de la cuenta de prueba (`@letreria.test`), así que probablemente llegue un rebote a la casilla de avisos; se puede borrar.

## Revisión de ECC

**Seguridad y C# del backend:** ninguno crítico. Se corrigieron:
- subir archivos ahora exige correo confirmado y tiene límite propio;
- quitar un borrador ya no puede borrar un adjunto ya enviado;
- la huella de IPv6 cuenta por /64 y usa una clave propia;
- la confirmación presencial se manda solo a correos confirmados;
- rechazar y eliminar exigen la versión vista;
- los permisos siguen RFU11;
- si se satura la regla contra solicitudes falsas, se responde 503 en vez de 500;
- filtros inválidos, y adjuntos de solicitudes eliminadas.

**React y accesibilidad del frontend:** ninguno crítico. Se corrigieron:
- los borradores que se perdían al salir (ahora se recuperan) y las subidas que no se cancelaban;
- enviar mientras se quita un archivo;
- la búsqueda que no seguía a la dirección;
- conflictos en los diálogos;
- y detalles de foco, anuncios, movimiento reducido y tamaño táctil.

## Pendiente para otras fases (en `desviaciones.md`)
- **Fase 16:**
  - detrás de un proxy, configurar las direcciones de confianza; si no, la regla por conexión vería la IP del proxy;
  - la cabecera contra que otro sitio incruste la página.
- **Ficha de clientes:** no avisa si otra persona la modificó, porque la tabla de clientes no tiene control de versión. Se puede agregar cuando lo necesites.
- **Fase 8:** el editor de cotizaciones y el paso a pedido.

## Lo que necesito de ti

1. **Tu visto bueno a esta fase**, o lo que quieras cambiar.
2. Confirmar que te llegó a Gmail el aviso de la solicitud #19.
