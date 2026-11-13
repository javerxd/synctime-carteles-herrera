Requerimientos del sistema — SyncTime, Letrería Los Herrera

Versión 2 (12-11-2026), reestructurada tras la revisión con la profesora guía: los 47 requerimientos funcionales del sistema se agrupan en 12 macro-requerimientos, uno por pantalla o proceso, con el detalle de sus acciones en viñetas. Las reglas puramente técnicas (validación en tres niveles, formato de errores, autorización por rol, paginación y límites de envío) pasan a los no funcionales del producto (RNFP30 a RNFP33). La equivalencia entre los códigos antiguos (RFS01 a RFS48) y los nuevos está en `docs/plan/trazabilidad.md`.

1. Requerimientos funcionales del sistema (12)

RFS01: Publicitar el catálogo de trabajos realizados y enviar una solicitud.
- Presentar como primera pantalla, con o sin sesión iniciada, la portada de «Letrería Los Herrera»: propuesta de valor, datos de contacto, botón directo a WhatsApp para consultas, formulario de contacto y mapa interactivo del local en Villagrán 810, 4440000 Los Ángeles, Bío Bío.
- Mostrar la vitrina o catálogo de trabajos realizados o destacados (imagen, título, descripción, tipo de letrero, categoría, medidas y precio referencial), filtrable por categoría.
- No mostrar ninguna publicación que contenga logotipos o marcas de clientes sin la autorización expresa del cliente registrada en el sistema.
- Desde la misma pantalla de la vitrina, abrir una ventana (modal) para enviar una solicitud de trabajo con sus especificaciones y medidas, con la casilla de aceptación de la Política de Privacidad y los Términos y Condiciones desmarcada por defecto. El envío sigue las reglas de RFS04.

RFS02: Gestionar la disponibilidad horaria y el cartel electrónico.
- Mostrar el horario semanal de atención y las franjas disponibles. El dueño actualiza el horario de cada día, y queda registrado quién y cuándo lo modificó.
- Cartel electrónico digital: indicador verde «Abierto / Disponible» o rojo «Cerrado / Ausente», siempre acompañado de su texto, con mensaje personalizado, hora estimada de regreso y hora de la última actualización. El dueño o un trabajador «da vuelta» el cartel cuando sale a terreno o a una entrega, y queda registrado quién lo cambió. Primero se construye el indicador funcional con la paleta del sistema; las animaciones elaboradas quedan para el final.
- Generar un código QR descargable e imprimible, para pegarlo en la ventana del local, que abra directamente el portal público.

RFS03: Autenticación, gestión de cuentas y acceso biométrico facial.
- Crear cuentas de cliente validando el RUT con su dígito verificador y que el teléfono, el RUT y el correo no estén registrados, y guardar la contraseña solo como hash. Verificar el correo de la cuenta antes de aceptar solicitudes en línea (RFS04).
- Iniciar sesión: clientes con correo y contraseña o con Google (OAuth 2.0); personal con correo y contraseña. Entregar un token de sesión (JWT) con el rol.
- Recuperar la contraseña.
- Al iniciar sesión, llevar al cliente a su vista principal con el seguimiento de sus solicitudes y pedidos, y al dueño o administrador a la vista de administración (RFS09).
- Ingreso facial, solo en el acceso al panel interno: registrar el rostro de un empleado activo a partir de varias capturas, guardando solo los descriptores numéricos y la fecha del consentimiento expreso, nunca la fotografía; identificar al empleado comparando su descriptor con el registrado y aceptar solo una coincidencia bajo el umbral de distancia; ante una coincidencia, emitir el mismo token que el ingreso con contraseña e indicar si ingresó como dueño o como trabajador; mantener siempre el ingreso con contraseña, y permitir al dueño desactivar el rostro de cualquier empleado.

RFS04: Gestión integral de solicitudes y prevención de solicitudes falsas.
- Buscar al cliente por RUT o teléfono antes de crearlo, para no duplicarlo.
- Registrar la cabecera de la solicitud con fecha automática, estado «solicitado» y origen «online» (cliente) o «presencial» (trabajador).
- Registrar uno o más ítems por solicitud (tipo de letrero, descripción, medidas y cantidad) y calcular el área en $\text{m}^2$ de cada uno.
- Listar y filtrar las solicitudes por estado, origen y fecha; modificarlas solo en «solicitado» o «cotizado»; rechazarlas con su motivo, conservándolas para las estadísticas, y eliminarlas de forma lógica.
- Validar las transiciones de estado de la solicitud, para impedir saltos o retrocesos inválidos.
- Mostrar a cada cliente registrado, en su vista principal, solo sus propias solicitudes, cotizaciones y pedidos, con su estado.
- Regla contra solicitudes falsas: para evitar que un tercero llene la base con solicitudes falsas usando RUT válidos de otras personas, el envío en línea exige una cuenta con correo verificado o el ingreso con Google. Si la cuenta, el RUT o la dirección IP ya tiene una solicitud en línea pendiente sin concretar, o superó el límite de envíos (RNFP33), el sistema no guarda la solicitud en la base de datos. En su lugar muestra un mensaje amable, sin la palabra «rechazado»: «Para esta solicitud es preferible contactarse directamente con el local», junto con el horario disponible, el cartel electrónico, el teléfono y el botón de WhatsApp. El mensaje es el mismo cualquiera sea el motivo, para no revelar datos de otras personas.

RFS05: Gestión de cotizaciones y aprobación por doble vía.
- Crear cotizaciones de una solicitud desglosando el costo de materiales (según el material base y las dimensiones), la mano de obra y otros gastos; calcular subtotales y total; pasar la solicitud a «cotizado» y registrar quién la elaboró.
- Aprobar o rechazar la cotización por dos vías: el cliente desde su sesión en la web, o el dueño o un trabajador en el panel interno cuando el cliente confirma por teléfono o WhatsApp.
- Validar las transiciones de estado de la cotización.

RFS06: Gestión de pedidos, control independiente de pagos y abonos, y calendario.
- Crear el pedido por dos vías. (1) Desde una cotización: al aprobarse, el pedido nace en estado «Aceptado» con los montos de la cotización y la solicitud pasa a «aceptado», sin exigir un pago previo. (2) Pedido presencial directo: cuando el cliente acuerda el trabajo en el taller sin solicitud web previa, el dueño o un trabajador busca al cliente por RUT o teléfono (o lo crea) y registra el tipo de letrero, el material base, las medidas, la cantidad, la glosa de especificaciones, el costo de materiales, la mano de obra, la instalación y el abono o pago, si lo hubo. La solicitud y la cotización de ese pedido se generan internamente.
- Registrar en el pedido el responsable, las fechas estimada y real de entrega, la modalidad de entrega, la instalación y su costo, y calcular el monto final.
- Llevar dos estados independientes. Estado del pedido (operativo): «Aceptado», «En proceso», «Terminado», «Entregado» o «Anulado»; no se usa «Cancelado», que en Chile se entiende como «pagado». Estado de pago (financiero): «No pagado», «Abonado» o «Pagado total».
- Registrar abonos y pagos: cada abono queda en un historial con su monto, fecha, medio de pago y quién lo registró; el pedido muestra el monto abonado (la suma de sus abonos), el saldo pendiente (monto final menos monto abonado) y el número de factura opcional, para los clientes habituales que pagan a fin de mes.
- Considerar cerrado el ciclo de un pedido solo cuando está «Entregado» y «Pagado total».
- Validar las transiciones del estado del pedido.
- Listados y filtros para el dueño: por estado del pedido; por estado de pago; filtro de cobranza, que lista los pedidos no anulados con pago «No pagado» o «Abonado» y su saldo pendiente; por año y mes; y por cliente, para ver todo su historial de trabajos.
- Mostrar un calendario de pedidos por fecha estimada de entrega, distinguiendo pendientes y entregados.

RFS07: Mantenedores del sistema (catálogo, tipos de letrero, materiales y personal).
- Administrar las categorías y los tipos de letrero que define el dueño (por ejemplo palomita, de ventana o de candidatura), los materiales base (por ejemplo vidrio, madera o cholguán) con su costo unitario, las publicaciones de la vitrina (indicando si se cuenta con el permiso del cliente cuando una imagen incluye su logotipo) y los empleados con su rol.
- De cara al usuario se habla de «administrar» o «mantener», nunca de «CRUD».

RFS08: Notificaciones por correo y formulario de contacto.
- Enviar al administrador un correo con los datos del formulario de contacto de la portada, registrando la consulta para su revisión.
- Avisar por correo al cliente cuando su solicitud se cotiza, rechaza o acepta y cuando su pedido cambia de estado, incluyendo siempre un enlace funcional para darse de baja de los correos opcionales o comerciales.
- Procesar el enlace de baja revocando de inmediato el consentimiento correspondiente.

RFS09: Análisis de datos, indicadores (KPIs) y dashboard interactivo.
- Calcular al menos seis indicadores clave con su fórmula: pedidos por mes, ingresos por mes, solicitudes en línea, solicitudes en línea concretadas y porcentaje de conversión, más un sexto como ticket promedio por pedido, porcentaje de entregas a tiempo o saldo total por cobrar. Por rango semanal, mensual o anual, comparando dos períodos.
- Alimentar desde el backend un dashboard con al menos cuatro gráficos interactivos, filtrables por rango de fechas, categoría, estado del pedido, estado de pago u origen de la solicitud, cada uno con su título, descripción textual y tabla de datos accesible.
- Al iniciar sesión el dueño o el administrador, mostrar la vista de administración con las solicitudes pendientes y las métricas principales del negocio.

RFS10: Generación, exportación (Excel, PDF y CSV) e impresión de reportes.
- Generar al menos tres reportes: operativo (detalle de pedidos, entregas y solicitudes, con estado de pago y saldo), gerencial (ingresos, costos, totales y gráficos) y parametrizable (rango de fechas y filtros, incluidos estado del pedido, estado de pago, año, mes y cliente).
- Exportar cada reporte con los filtros aplicados a Excel (.xlsx con encabezados, formatos de fecha y número, y totales), PDF (título, fecha de emisión, filtros aplicados, numeración de páginas y texto seleccionable) y CSV (UTF-8 con separador documentado), registrando en la auditoría quién exportó y validando su rol.
- Proveer una versión limpia de impresión desde el navegador (`@media print`), sin menús, botones ni navegación.

RFS11: Asistente con inteligencia artificial externa.
- Ofrecer un endpoint que consuma un servicio externo de inteligencia artificial para sugerir, a partir de una descripción en lenguaje natural, la categoría, las medidas y un borrador de solicitud o cotización, o para redactar descripciones comerciales del catálogo; validar la entrada y devolver una respuesta JSON estructurada o el error estándar.

RFS12: Auditoría transversal, gestión de consentimientos e inhabilitación de cuentas.
- Registrar automáticamente en una bitácora de auditoría quién creó, modificó o eliminó registros en las entidades principales (solicitudes, cotizaciones, pedidos, catálogo, clientes y empleados) y quién exportó reportes, con fecha, hora y detalle del cambio.
- Gestionar de forma unificada la Política de Privacidad, los Términos y Condiciones y las cookies: publicarlos en todo el sitio; exigir en el registro y en los formularios una casilla desmarcada por defecto para aceptarlos y otra independiente, también desmarcada, para autorizar el marketing; registrar quién aceptó, la versión del documento, la fecha y la hora; y mostrar un banner de cookies que bloquea la analítica y los rastreadores hasta el consentimiento expreso.
- Permitir al cliente descargar en un archivo estructurado sus datos personales e historial (portabilidad) y dar de baja su cuenta. La baja no borra el historial: la cuenta queda inhabilitada (borrado lógico) para conservar la integridad histórica y contable de los pedidos de la empresa; el cliente deja de poder ingresar y de recibir comunicaciones, y se borran los datos que no hacen falta para esa conservación, como la contraseña.

2. Requerimientos funcionales del usuario (36)

RFU01 (Visitante): ver en la página principal de «Letrería Los Herrera» la vitrina de trabajos realizados con imágenes, tipo de letrero, medidas y costo estimado antes de pedir.

RFU02 (Visitante): saber el horario, los datos de contacto de la empresa y si el local está abierto o el dueño ausente.

RFU03 (Visitante): entrar al portal escaneando un código QR.

RFU04 (Visitante): crear una cuenta de cliente marcando voluntariamente la aceptación de los términos y condiciones, la política de privacidad y, de forma separada, si desea recibir marketing.

RFU05 (Cliente, trabajador, dueño): iniciar sesión; el cliente, también con Google.

RFU06 (Visitante, cliente): enviar una solicitud de trabajo desde la vitrina a cualquier hora, con una cuenta de correo verificado o con Google; si el sistema sugiere contactarse con el local, ver ahí mismo el horario, el cartel electrónico, el teléfono y el botón de WhatsApp.

RFU07 (Cliente registrado): consultar desde un apartado en su vista principal el seguimiento y estado de sus solicitudes, cotizaciones y pedidos.

RFU08 (Cliente registrado): aceptar o rechazar una cotización contando que al enviar la solicitud el dueño probablemente se contactará con el cliente por WhatsApp y por ese medio le daría una cotización.

RFU09 (Cliente): recibir un aviso cuando su solicitud o pedido cambie de estado, con la opción de darse de baja de correos no esenciales mediante un enlace directo.

RFU10 (Trabajador): registrar la solicitud de un cliente que llega al local.

RFU11 (Trabajador): consultar, modificar, rechazar y eliminar solicitudes.

RFU12 (Trabajador): cotizar separando materiales, mano de obra y otros gastos. (!)

RFU13 (Trabajador): convertir la cotización aprobada en pedido y registrar los abonos y pagos del cliente, con el número de factura cuando corresponda.

RFU14 (Trabajador): asignar responsable, actualizar el avance y registrar la entrega o instalación.

RFU15 (Trabajador): ver en un calendario las entregas comprometidas.

RFU16 (Trabajador): dar vuelta el cartel electrónico para avisar si el local está abierto o cerrado, con un mensaje y la hora de regreso; el dueño, además, cambiar el horario. (!)

RFU17 (Dueño): mantener catálogo, productos, categorías y materiales, indicando si se cuenta con el permiso del cliente cuando una imagen incluya su logotipo.

RFU18 (Dueño): mantener empleados y sus roles.

RFU19 (Dueño): acceder al iniciar sesión a la vista de administración con las solicitudes pendientes y ver gráficos de pedidos, ingresos y conversión por semana, mes o año, comparando períodos.

RFU20 (Trabajador): registrar su disponibilidad horaria para que la agenda la muestre a los clientes.

RFU21 (Dueño): registrar y desactivar el rostro de los empleados para el ingreso rápido.

RFU22 (Dueño, trabajador): ingresar al panel mirando a la cámara y entrar con su rol; si no es reconocido, usar contraseña.

RFU23 (Visitante, cliente, dueño): usar el asistente con inteligencia artificial para describir en lenguaje natural el letrero o trabajo que necesita y recibir una sugerencia estructurada de categoría, medidas y borrador de solicitud, o permitir al dueño generar descripciones comerciales para el catálogo.

RFU24 (Dueño, trabajador): generar, filtrar e imprimir desde el navegador el reporte operativo de pedidos y solicitudes con un diseño limpio de hoja.

RFU25 (Dueño): generar y consultar el reporte gerencial (con totales y gráficos) y el reporte parametrizable por rango de fechas.

RFU26 (Dueño, trabajador): exportar los reportes autorizados para su rol a archivos Excel (.xlsx), PDF y CSV conservando los filtros aplicados en pantalla.

RFU27 (Dueño): filtrar interactivamente el dashboard por fechas, categorías y estados, y consultar la tabla de datos y descripción asociada a cada gráfico.

RFU28 (Dueño): consultar el registro de auditoría del sistema para revisar quién creó o modificó registros principales, quién aceptó los términos y condiciones y cuándo, y quién exportó reportes.

RFU29 (Visitante, cliente, trabajador, dueño): navegar y operar todas las pantallas del sistema utilizando únicamente el teclado o un lector de pantalla, recibiendo avisos claros de validación, carga o error.

RFU30 (Visitante, cliente registrado): visualizar en la vista principal (con o sin sesión iniciada) un mapa interactivo con la ubicación exacta del local comercial en Villagrán 810, 4440000 Los Ángeles, Bío Bío.

RFU31 (Visitante, cliente): contactar directamente al taller mediante un botón en la vista principal que redirija al WhatsApp oficial de la empresa.

RFU32 (Visitante, cliente): enviar consultas generales a través del formulario de contacto de la vista principal para que el administrador las reciba por correo y las revise.

RFU33 (Visitante, cliente): leer la Política de Privacidad y los Términos y Condiciones, y decidir en el banner de cookies si acepta o rechaza las cookies no esenciales y rastreadores.

RFU34 (Cliente registrado): descargar una copia de todos sus datos personales almacenados en el sistema y dar de baja su cuenta, que queda inhabilitada conservando el historial de sus pedidos.

RFU35 (Dueño, trabajador): registrar un pedido presencial directo cuando el cliente acuerda el trabajo en el taller, sin solicitud web previa, incluyendo el abono o pago si lo deja.

RFU36 (Dueño): filtrar los pedidos por estado del pedido, estado de pago, año, mes y cliente, y listar los pedidos por cobrar con su saldo pendiente.

3. Requerimientos no funcionales del producto (33)

RNFP01 (Rendimiento): responder cada consulta o registro en 2 segundos o menos en el 95 % de las peticiones, con hasta 20 usuarios simultáneos.

RNFP02 (Facilidad de uso): que un cliente sin capacitación registre una solicitud desde el celular en 5 minutos o menos.

RNFP03 (Facilidad de uso): adaptar todas las pantallas a anchos desde 360 px sin desplazamiento horizontal.

RNFP04 (Disponibilidad): 99 % de disponibilidad mensual, 24 horas, aunque el local esté cerrado.

RNFP05 (Compatibilidad): funcionar en las dos últimas versiones de Chrome, Edge, Firefox y Safari, sin complementos.

RNFP06 (Seguridad): guardar contraseñas solo como hash con sal.

RNFP07 (Seguridad): aplicar limitación de tasa (rate limiting) por dirección IP y por usuario en los endpoints de la API (con un límite estricto de 10 intentos por minuto en inicio de sesión y recuperación de clave), respondiendo con código HTTP 429 al superarlos.

RNFP08 (Seguridad): tokens de sesión que caduquen a los 15 minutos (configurable).

RNFP09 (Confiabilidad): respaldar la base de datos una vez al día y conservar cada respaldo 30 días, fuera del servidor principal.

RNFP10 (Integridad): rechazar, con aviso, el cambio de un registro que otro usuario modificó después de abrirlo.

RNFP11 (Mantenibilidad): documentar todas las rutas de la API en OpenAPI, consultables con Scalar o Swagger en desarrollo.

RNFP12 (Seguridad): resolver el ingreso facial en 3 s o menos, con el mismo límite de 10 intentos por minuto que el ingreso con contraseña.

RNFP13 (Seguridad): completar el flujo de recuperación de contraseña mediante un enlace o código temporal de un solo uso con expiración máxima de 15 minutos enviado al correo registrado.

RNFP14 (Facilidad de uso y Responsive): aplicar un diseño mobile first con sistema de diseño propio y consistente, adaptable sin desplazamiento horizontal entre 360 px y 1440 px (celular, tablet y escritorio), con tablas adaptadas, navegación colapsable en móvil, controles táctiles de al menos $24 \times 24\text{ px}$ (recomendado $44 \times 44\text{ px}$), skeletons de carga (esqueletos animados) mientras se obtienen datos, estados vacíos y vistas de error (404 y 403 sin permisos).

RNFP15 (Accesibilidad): cumplir el estándar WCAG 2.2 nivel AA en todas las pantallas, incluyendo HTML semántico, navegación completa por teclado con foco visible y enlace para saltar al contenido, cierre de modales con Esc, etiquetas vinculadas a campos de formulario con errores anunciados a lectores de pantalla, contraste mínimo de 4,5:1 en texto normal y 3:1 en texto grande y componentes, no transmitir información solo con color, texto alternativo descriptivo en todas las imágenes, soporte de zoom hasta 200 % y respeto de `prefers-reduced-motion`.

RNFP16 (Seguridad): autenticar mediante JWT en la API con autorización basada en al menos tres roles diferenciados y mitigar al menos cinco vulnerabilidades del OWASP API Security Top 10.

RNFP17 (Confiabilidad e IA): gestionar las claves de APIs externas (IA, OAuth, SMTP) exclusivamente mediante variables de entorno (en `.env` local y variables de entorno de servidor en producción, nunca en el código), definir un tiempo máximo de espera (timeout) en todas las llamadas a servicios externos y capturar fallos devolviendo el formato de error estándar de la API sin bloquear el sistema.

RNFP18 (Rendimiento): ejecutar la generación y exportación de reportes grandes a Excel, PDF y CSV de forma asíncrona sin bloquear la interfaz de usuario.

RNFP19 (Mantenibilidad): mantener el contrato OpenAPI actualizado y accesible desde la API, habiéndolo definido antes de la implementación de los endpoints.

RNFP20 (Confiabilidad): contar con una estrategia de respaldo y restauración de la base de datos documentada y probada de principio a fin al menos una vez.

RNFP21 (Ética y usabilidad): diseñar todas las pantallas y flujos libres de patrones engañosos (dark patterns), garantizando que las opciones de rechazar cookies, cancelar acciones o darse de baja sean igual de visibles, claras y sencillas que las de aceptar.

RNFP22 (Rendimiento de recursos y fuentes): servir las imágenes del catálogo y la interfaz optimizadas en formatos modernos (WebP o AVIF), aplicar carga diferida (`lazy loading`) a las imágenes fuera de pantalla y limitar los grosores de las fuentes tipográficas estrictamente a los utilizados.

RNFP23 (Rendimiento de red en cliente): evitar llamadas duplicadas a la API al enviar formularios o cargar vistas, aplicar `debounce` (por ejemplo, 300 ms) en los campos de búsqueda y filtrado, y lanzar en paralelo (`Promise.all`) las peticiones independientes al cargar una vista.

RNFP24 (Rendimiento de renderizado en cliente): dividir el código de React por ruta o página (`code splitting` con carga diferida de vistas), evitar re-renderizados innecesarios de componentes y cargar cualquier script externo tras la carga de la página principal.

RNFP25 (Caché y compresión HTTP): activar la compresión estándar (Gzip/Brotli) en las respuestas de ASP.NET Core y configurar cabeceras de caché en el navegador (`Cache-Control`) para los archivos estáticos.

RNFP26 (Eficiencia en base de datos): utilizar el pool de conexiones por defecto de ADO.NET / Entity Framework Core hacia SQL Server, evitar las consultas $N+1$ mediante carga anticipada (`.Include()`) o proyecciones (`.Select()`) y pedir en las consultas únicamente las columnas necesarias (evitando `SELECT *`).

RNFP27 (Verificación en condiciones reales): comprobar el rendimiento y la usabilidad del sistema en dispositivos móviles y bajo simulación de redes móviles lentas (throttling en navegador) sin pérdida de funcionalidad.

RNFP28 (Seguridad en consultas y aislamiento de datos): utilizar exclusivamente consultas parametrizadas o LINQ en Entity Framework Core sin concatenar jamás entradas de usuario en sentencias SQL, y filtrar siempre en el backend por el identificador del usuario autenticado (proveniente del token JWT) para asegurar que ningún cliente acceda a registros de otro.

RNFP29 (Seguridad de orígenes): configurar una política de CORS cerrada en la API que permita peticiones únicamente desde los orígenes autorizados del front end según el entorno.

RNFP30 (Validación y errores; antes RFS31): validar todos los datos de entrada en tres niveles (formularios del front end, endpoints de la API en el servidor y restricciones de la base de datos) y devolver los errores de la API bajo un formato estándar y centralizado.

RNFP31 (Autorización; antes RFS23): permitir cada función solo a los roles autorizados, verificándolo siempre en el servidor.

RNFP32 (Listados; antes RFS32): permitir buscar, filtrar, ordenar y paginar los listados principales del sistema (solicitudes, cotizaciones, pedidos, catálogo y clientes) tanto en la API como en la interfaz.

RNFP33 (Protección contra solicitudes falsas; parte técnica de RFS04): limitar las solicitudes en línea por cuenta, por RUT y por dirección IP: como máximo una solicitud en línea pendiente sin concretar por cuenta o RUT, y 3 envíos por hora por IP (valores propuestos y configurables). Al superar un límite no se escribe nada en la base de datos.

4. Requerimientos no funcionales organizacionales (17)

RNFO01: construir con React, ASP.NET Core Web API y SQL Server.

RNFO02: aplicar el flujo solicitud -> cotización -> aprobación -> pedido, o registrar un pedido presencial directo; el pago se controla aparte con el estado de pago (no pagado, abonado o pagado total) y no es requisito para crear el pedido.

RNFO03: publicar el horario que fije el dueño y permitir que solo el personal cambie el estado del local.

RNFO04: conservar solicitudes rechazadas y eliminadas al menos 12 meses, para estadísticas.

RNFO05: montos en pesos chilenos sin decimales y fechas en formato dd-mm-aaaa.

RNFO06: validar el RUT con su dígito verificador.

RNFO07: todas las pantallas y mensajes en español.

RNFO08: permitir solo los roles administrador, dueño, diseñador y operario para el personal.

RNFO09: desarrollar con Kanban y mantener el código en un repositorio con control de versiones.

RNFO10: estructurar el backend en cuatro capas claramente separadas (presentación, API, servicios de negocio y acceso a datos), aplicando principios SOLID, inyección de dependencias y al menos dos patrones de diseño justificados (incluyendo obligatoriamente el patrón Repository).

RNFO11: normalizar el modelo de datos relacional con al menos seis entidades relacionadas, gestionando el esquema mediante migraciones versionadas, restricciones de integridad (claves, unicidad, nulos y chequeos), índices justificados para las consultas más comunes y vistas o consultas preparadas para los reportes y el dashboard.

RNFO12: poblar la base de datos para pruebas y demostración con datos sintéticos verosímiles (nunca datos personales reales) que incluyan al menos 500 registros en la entidad principal y 12 meses de historia.

RNFO13: implementar pruebas unitarias automatizadas para la capa de negocio, pruebas de integración automatizadas para los endpoints principales de la API (incluyendo el servicio de IA simulado), auditoría automática de accesibilidad (Lighthouse o axe sin errores críticos) y prueba manual documentada con teclado y lector de pantalla (NVDA o VoiceOver).

RNFO14: contenerizar el sistema mediante `Dockerfile` multi-stage con imagen base ligera de versión fija y un archivo `compose.yaml` (con front end, API, base de datos y servicios auxiliares, volúmenes, redes internas, health checks y variables en `.env` excluido del repositorio entregando `.env.example`), de modo que todo el entorno arranque con un único comando (`docker compose up`).

RNFO15: automatizar un pipeline CI/CD que construya las imágenes, ejecute las suites de pruebas y publique únicamente si todas las pruebas pasan, sin exponer credenciales ni secretos en el historial de Git ni en los logs.

RNFO16: revisar las dependencias del proyecto para eliminar paquetes o librerías sin usar tanto en el front end como en el backend, y verificar cualquier librería externa antes de su inclusión.

RNFO17: verificar y documentar el cumplimiento de las licencias de todas las imágenes, fuentes tipográficas, iconos y paquetes de código open source utilizados en el proyecto, y prohibir el uso de logotipos de clientes en la vitrina sin su autorización previa.

5. Requerimientos no funcionales externos (9)

RNFE01: cumplir la Ley N.º 19.628 (modificada por la Ley N.º 21.719): informar el uso de los datos en la Política de Privacidad, pedir solo los necesarios, exigir consentimiento separado para marketing, bloquear rastreadores hasta su aceptación, permitir consultarlos, corregirlos, descargarlos (portabilidad) y eliminarlos, y aplicar medidas de seguridad proporcionales al riesgo.

RNFE02: integrarse con el inicio de sesión de Google mediante OAuth 2.0.

RNFE03: comunicarse solo por HTTPS, con certificado TLS vigente.

RNFE04: aplicar las recomendaciones del OWASP Top 10:2025 y OWASP API Security Top 10.

RNFE05: integrarse con un servicio de correo saliente con remitente autenticado.

RNFE06: tratar el rostro como dato personal sensible: consentimiento expreso, guardar solo el descriptor y permitir su eliminación.

RNFE07: integrarse mediante HTTPS desde el backend con una API externa de inteligencia artificial generativa (como Google Gemini API u OpenAI), enviando la llave de API de forma segura desde variables de entorno del servidor.

RNFE08: cumplir con las pautas de accesibilidad de contenido web WCAG 2.2 en su nivel de conformidad AA del W3C.

RNFE09: cumplir con los términos de licencia de los componentes open source, activos gráficos y tipografías empleadas, así como con las políticas del proveedor de mapas embebidos.

6. Requerimientos de información: entradas (27)

E01 Datos del cliente: RUT (opcional en solicitud rápida), nombre, apellido, teléfono, correo, dirección y, si crea cuenta, contraseña. Visitante o trabajador.

E02 Credenciales: correo y contraseña, o autorización de Google. Cliente, trabajador.

E03 Solicitud: cliente y descripción general del trabajo. Cliente con correo verificado o trabajador.

E04 Ítems de la solicitud: tipo de letrero, descripción, ancho, alto y cantidad. Cliente o trabajador.

E05 Cambio en una solicitud: datos corregidos, motivo de rechazo o marca de eliminación. Trabajador.

E06 Cotización: observaciones; por material: material, medida, cantidad y costo unitario; mano de obra; otros costos. Trabajador.

E07 Respuesta a la cotización: aprobación o rechazo. Cliente registrado o trabajador.

E08 Creación del pedido desde una cotización aprobada: responsable, fecha estimada, modalidad de entrega, si requiere instalación, dirección y costo de instalación, y abono inicial si lo hay. Trabajador.

E09 Avance del pedido: nuevo estado del pedido y, al entregar, fecha real. Trabajador.

E10 Horario de atención: por día, si abre, hora de apertura y de cierre. Dueño.

E11 Estado del local: abierto o ausente, mensaje y tiempo estimado de regreso. Trabajador.

E12 Publicación del catálogo / vitrina: producto, título, descripción, imagen, tipo de letrero, ancho y alto (medidas), costo estimado o precio referencial, confirmación de permiso de uso de logo y visibilidad. Dueño.

E13 Categoría y producto: nombre y descripción de la categoría; nombre, descripción, categoría y precio base referencial del producto. Dueño.

E14 Material: nombre, unidad de medida y costo unitario aproximado. Dueño.

E15 Empleado: RUT, nombre, apellido, teléfono, correo, contraseña inicial y rol. Dueño.

E16 Parámetros de reporte: reporte, rango (semana, mes o año), fechas desde y hasta y, si compara, el segundo período. Dueño.

E17 Rostro del empleado: empleado elegido, tres capturas de cámara y consentimiento expreso. Dueño.

E18 Captura de ingreso: una captura del rostro al ingresar. Dueño o trabajador.

E19 Consulta al asistente de IA: descripción en lenguaje natural del letrero requerido o datos base del producto para redactar su publicación. Visitante, cliente o dueño.

E20 Configuración de exportación de reporte: tipo de reporte (operativo, gerencial o parametrizable), filtros activos (rango de fechas, estado, categoría u origen) y formato de salida elegido (Excel `.xlsx`, PDF, CSV o vista de impresión). Dueño o trabajador.

E21 Filtros del dashboard: rango de fechas, categoría de producto, estado del pedido, estado de pago u origen de solicitud. Dueño.

E22 Formulario de contacto: nombre del remitente, correo electrónico, teléfono (opcional), asunto y mensaje de consulta. Visitante o cliente.

E23 Registro de consentimiento legal y privacidad: aceptación de Términos y Condiciones y Política de Privacidad (casilla obligatoria desmarcada por defecto), consentimiento opcional de marketing y preferencias del banner de cookies. Visitante o cliente.

E24 Solicitud sobre datos personales o baja de correo: petición de descarga de datos personales, solicitud de baja (inhabilitación) de la cuenta o confirmación de baja de lista de correo. Cliente registrado.

E25 Abono o pago del pedido: monto, fecha de pago, medio de pago (efectivo, transferencia, tarjeta de débito o de crédito, cheque u otro), observación opcional y número de factura opcional del pedido; queda registrado quién lo ingresó y cuándo. Trabajador o dueño.

E26 Pedido presencial directo: cliente (búsqueda por RUT o teléfono, o sus datos si es nuevo), tipo de letrero, material base, ancho, alto, cantidad, glosa de especificaciones, costo de materiales, mano de obra, instalación, responsable, fecha estimada y abono o pago si lo hay. Trabajador o dueño.

E27 Filtros de pedidos: estado del pedido, estado de pago, cobranza (no pagados o abonados), año, mes y cliente. Dueño.

7. Requerimientos de información: salidas (25)

S01 Catálogo público y vitrina principal («Letrería Los Herrera»): título y datos de contacto de la empresa, trabajos realizados (imagen en WebP/AVIF, título, descripción, tipo de letrero, categoría, medidas y costo estimado), botón directo a WhatsApp y mapa interactivo con la ubicación del local en Villagrán 810, 4440000 Los Ángeles, Bío Bío. Visitante, cliente registrado.

S02 Horario y cartel electrónico: horario de lunes a domingo, cartel verde «Abierto / Disponible» o rojo «Cerrado / Ausente» con su texto, mensaje, hora estimada de regreso y hora de última actualización, y código QR del portal. Visitante.

S03 Comprobante de solicitud: número, fecha y hora, nombre del cliente, ítems (producto, medidas, área, cantidad) y estado «solicitado». Visitante, trabajador.

S04 Seguimiento del cliente en vista principal: por solicitud, número, fecha, estado y motivo de rechazo; total y estado de la cotización; estado del pedido, estado de pago, saldo pendiente, fecha estimada y modalidad. Cliente registrado.

S05 Bandeja de solicitudes y vista principal de administración: resumen de solicitudes pendientes, métricas clave del negocio, y listado con número, fecha, cliente y teléfono, origen, trabajador que la registró, estado y cantidad de ítems. Dueño, trabajador.

S06 Cotización: número, fecha, cliente, ítems, detalle de materiales, costo total de materiales, mano de obra, otros costos, total, estado y trabajador que la elaboró. Trabajador, cliente registrado.

S07 Ficha del pedido: número, origen (cotización o directo), solicitud y cotización de origen, cliente, responsable, fechas, modalidad, instalación y dirección, monto base, costo de instalación, monto final, estado del pedido, estado de pago, monto abonado, saldo pendiente, número de factura e historial de abonos (fecha, monto y medio de pago). Trabajador.

S08 Calendario de entregas: pedidos por día con cliente, modalidad y estado, con colores e íconos o etiquetas distintivas para pendientes y entregados. Trabajador.

S09 Notificación al cliente y correo de contacto al administrador: al cliente, número de solicitud o pedido, estado nuevo, mensaje, fecha y enlace para darse de baja; al administrador, correo con los datos del formulario de contacto enviado desde la página principal. Cliente, dueño.

S10 Pedidos por mes: cantidad de pedidos creados por estado, sin contar los anulados en el total. Dueño.

S11 Ingresos por mes: suma del monto final de los pedidos entregados y total del período. Dueño.

S12 Solicitudes en línea: cantidad de online y, para comparar, de presenciales. Dueño.

S13 Solicitudes en línea concretadas: online con un pedido no anulado. Dueño.

S14 Conversión en línea: porcentaje S13 sobre S12 y, al comparar dos períodos, la diferencia en puntos porcentuales. Dueño.

S15 Resultado del ingreso facial: nombre y rol del empleado reconocido, o aviso de que no hubo coincidencia con la opción de contraseña. Dueño, trabajador.

S16 Registro de rostros: por empleado, nombre, rol, fecha de registro, fecha del consentimiento y estado (activo o desactivado). Dueño.

S17 Sugerencia del asistente de IA: respuesta estructurada con categoría sugerida, producto base, medidas/materiales estimados o texto comercial redactado para el catálogo. Visitante, cliente o dueño.

S18 Reporte operativo, gerencial y parametrizable: documento descargable en Excel (`.xlsx` con encabezados, formatos y totales), PDF (con título, fecha de emisión, filtros usados, paginación y texto seleccionable), CSV (UTF-8) y vista limpia de impresión en navegador. Dueño, trabajador.

S19 Dashboard e indicadores (KPIs): al menos cuatro gráficos interactivos actualizados con su título, descripción textual y tabla de datos accesible, junto con al menos seis indicadores calculados (sumando a S10-S14 un sexto KPI como ticket promedio por pedido o porcentaje de entregas a tiempo). Dueño.

S20 Bitácora de auditoría y consentimientos: historial de creación y modificación en entidades principales (usuario, rol, entidad, acción, fecha y hora), registro de quién aceptó los términos y condiciones y cuándo, y registro de quién solicitó cada exportación de reportes. Dueño.

S21 Respuesta de error estándar: estructura uniforme de error de la API con código de estado HTTP, mensaje comprensible para el usuario y detalle de campos inválidos por nivel de validación. Visitante, cliente, trabajador, dueño.

S22 Documentos legales y banner de cookies: vista de Términos y Condiciones, Política de Privacidad, inventario de licencias open source y aviso de configuración de cookies. Visitante, cliente.

S23 Archivo de portabilidad y comprobante de baja: archivo descargable (JSON o CSV) con todos los datos personales e historial del cliente, o confirmación de que su cuenta quedó inhabilitada. Cliente registrado.

S24 Listado de pedidos por cobrar: pedidos no anulados con pago «No pagado» o «Abonado», con cliente, estado del pedido, monto final, monto abonado y saldo pendiente, y el total por cobrar. Dueño.

S25 Aviso de contacto directo: mensaje «Para esta solicitud es preferible contactarse directamente con el local», con el horario, el cartel electrónico, el teléfono y el botón de WhatsApp. Visitante, cliente.

8. Requerimientos tecnológicos: desarrollo (15)

* Computador por integrante (3 a 4): 4 núcleos, 8 GB de RAM (16 GB recomendado), 20 GB libres en SSD.

* Celular Android o iOS: navegador actualizado y cámara para pruebas en dispositivos reales y redes móviles.

* Conexión a Internet: banda ancha.

* Sistema operativo: Windows 10 u 11, macOS o Linux.

* SDK de .NET: versión 10.

* Node.js: versión LTS vigente.

* Motor de base de datos: SQL Server 2025 Express o Developer.

* Administrador de base de datos: SQL Server Management Studio.

* Editor: Visual Studio o Visual Studio Code.

* Control de versiones: Git y cuenta de GitHub.

* Credenciales de Google: proyecto en Google Cloud con ID de cliente OAuth.

* Contenedores: Docker Desktop / Docker Engine y Docker Compose.

* Automatización CI/CD: GitHub Actions configurado en el repositorio.

* Herramientas de auditoría de accesibilidad y rendimiento: Lighthouse, axe DevTools, simulación de red lenta en DevTools y lector de pantalla (NVDA o VoiceOver).

* Credenciales de IA externa: clave de API (por ejemplo, Google Gemini API) gestionada en archivo `.env` local (con plantilla `.env.example` en el repositorio).

9. Requerimientos tecnológicos: implementación (8)

* Servidor de aplicación: hosting o servidor virtual compatible con contenedores Docker y ASP.NET Core en .NET 10, con HTTPS y compresión estándar (Gzip/Brotli).

* Servidor de base de datos: SQL Server (Express admite hasta 50 GB) con respaldos automáticos y estrategia de restauración probada.

* Alojamiento de la interfaz: contenedor web o servicio de sitios estáticos que sirva la aplicación en React e imágenes en WebP/AVIF.

* Orquestación por contenedores: entorno ejecutable mediante `compose.yaml` con redes internas, volúmenes persistentes y gestión segura de secretos y variables de entorno fuera del código fuente.

* Dominio y certificado TLS: dominio propio y certificado vigente.

* Servicio de correo: cuenta SMTP con remitente autenticado del negocio.

* Servicio de mapas e integración con WhatsApp: mapa interactivo embebido apuntando a Villagrán 810, Los Ángeles, Bío Bío, y enlace directo al WhatsApp del taller.

* Equipo del taller y letrero QR: el computador actual y el celular del dueño con navegador actualizado y cámara, junto con el letrero o adhesivo con el código QR impreso por el propio taller.