# Matriz de trazabilidad

Cada requisito de `docs/requisitos/requerimientos_del_sistema.md`, la fase del plan donde se cumple y su evidencia.
Al cerrar cada fase se actualizan las columnas **Estado** y **Evidencia** de sus requisitos (prueba automática, pantalla o documento).
En la fase 17 no puede quedar ninguno en "Pendiente": cada uno termina como Cumplido, Desviación o Pendiente externo (ver `desviaciones.md`).

Total: **183 identificadores**. El documento no tiene RFS10: la numeración salta de RFS09 a RFS11.

## Requisitos funcionales del sistema (47)

| Requisito | Descripción | Fase | Estado | Evidencia |
|---|---|---|---|---|
| RFS01 | mostrar las publicaciones visibles del catálogo o vitrina de trabajos realizados (imagen, título,… | 5 | Pendiente |  |
| RFS02 | mostrar el horario semanal y el estado actual del local. | 5,9 | Pendiente |  |
| RFS03 | generar un código QR que abra el portal público. | 5 | Pendiente |  |
| RFS04 | crear cuentas de cliente validando que teléfono, RUT y correo no estén registrados, guardar y registrar la… | 6 | Pendiente |  |
| RFS05 | autenticar clientes (correo y contraseña, o Google) y trabajadores (correo y contraseña), y entregar un token… | 6,14 | Pendiente |  |
| RFS06 | registrar cada solicitud con estado «solicitado», fecha automática y origen «online» (cliente) o «presencial»… | 7 | Pendiente |  |
| RFS07 | registrar uno o más ítems por solicitud (producto, descripción, cantidad, medidas) y calcular el área en… | 7 | Pendiente |  |
| RFS08 | buscar al cliente por teléfono o RUT antes de crear uno nuevo, para no duplicarlo. | 7 | Pendiente |  |
| RFS09 | listar solicitudes por estado, origen y fecha, y permitir modificarlas solo en «solicitado» o «cotizado». (!) | 7 | Pendiente |  |
| RFS11 | registrar el rechazo de una solicitud con su motivo y conservarla para las estadísticas. | 7 | Pendiente |  |
| RFS12 | crear cotizaciones asociadas a una solicitud (materiales, mano de obra, otros costos), calcular subtotales y… | 8 | Pendiente |  |
| RFS13 | registrar la aprobación o el rechazo de una cotización, hecho por el cliente en línea o por un trabajador. (!) | 8 | Pendiente |  |
| RFS14 | al confirmar el pago de una cotización aprobada, pasar la solicitud a «aceptado» y crear un único pedido con… | 8 | Pendiente |  |
| RFS15 | registrar en el pedido responsable, fechas estimada y real de entrega, modalidad de entrega y costo de… | 8 | Pendiente |  |
| RFS16 | cambiar el estado del pedido entre en proceso, aceptado, terminado, entregado, cancelado. | 8 | Pendiente |  |
| RFS17 | mostrar un calendario de pedidos por fecha estimada de entrega, distinguiendo pendientes y entregados. | 8 | Pendiente |  |
| RFS18 | enviar un correo al cliente cuando su solicitud sea cotizada, rechazada o aceptada, y cuando su pedido cambie… | 11 | Pendiente |  |
| RFS19 | mostrar a cada cliente registrado solo sus propias solicitudes, cotizaciones y pedidos. | 7,8 | Pendiente |  |
| RFS20 | permitir mantener categorías, productos, publicaciones del catálogo, materiales y empleados con su rol. | 9 | Pendiente |  |
| RFS21 | permitir actualizar el horario de cada día y el estado del local, registrando quién y cuándo lo modificó. | 9 | Pendiente |  |
| RFS22 | calcular pedidos por mes, ingresos por mes, solicitudes en línea, solicitudes en línea concretadas y… | 12 | Pendiente |  |
| RFS23 | permitir cada función solo a los roles autorizados. | 1 y cada fase | Pendiente |  |
| RFS24 | registrar el rostro de un empleado activo a partir de varias capturas, guardando solo los descriptores… | 10 | Pendiente |  |
| RFS25 | identificar al empleado comparando el descriptor con los registrados, aceptando solo una coincidencia bajo el… | desviación | Desviación | Ver `desviaciones.md` |
| RFS26 | ante una coincidencia, emitir el mismo token que el ingreso con contraseña y mostrar si ingresó como dueño… | 10 | Pendiente |  |
| RFS27 | ofrecer el ingreso facial solo en el acceso al panel interno, mantener siempre el ingreso con contraseña y… | 6,10 | Pendiente |  |
| RFS28 | método de recuperación de contraseña. | 6 | Pendiente |  |
| RFS29 | validar las transiciones de estado en solicitudes, cotizaciones y pedidos para impedir saltos o retrocesos… | 7,8 | Pendiente |  |
| RFS30 | registrar automáticamente en una bitácora de auditoría quién creó, modificó o eliminó registros en las… | 11 | Pendiente |  |
| RFS31 | validar todos los datos de entrada en tres niveles (formularios del front end, endpoints de la API en el… | 1,3 | Pendiente |  |
| RFS32 | permitir buscar, filtrar, ordenar y paginar los listados principales del sistema (solicitudes, cotizaciones,… | 1,5,7,8,9 | Pendiente |  |
| RFS33 | calcular al menos seis indicadores clave (sumando a pedidos por mes, ingresos por mes, solicitudes en línea,… | 12 | Pendiente |  |
| RFS34 | alimentar un dashboard interactivo desde el backend con al menos cuatro visualizaciones actualizadas con los… | 12 | Pendiente |  |
| RFS35 | generar al menos tres reportes del negocio: uno operativo (detalle de pedidos, entregas y solicitudes), uno… | 13 | Pendiente |  |
| RFS36 | exportar cada uno de los reportes aplicando los filtros seleccionados a formatos Excel (.xlsx con… | 13 | Pendiente |  |
| RFS37 | proveer una versión limpia de impresión desde el navegador para cada reporte, ocultando menús, botones y… | 13 | Pendiente |  |
| RFS38 | ofrecer un endpoint en la API que consuma un servicio de inteligencia artificial externo (como asistente de… | 14 | Pendiente |  |
| RFS39 | presentar como primera pantalla al ingresar al sistema la vista principal de «Letrería Los Herrera», que… | 5 | Pendiente |  |
| RFS40 | enviar automáticamente un correo electrónico al administrador con los datos ingresados cuando un visitante o… | 4,5 | Pendiente |  |
| RFS41 | habilitar en la vista principal, cuando un cliente inicie sesión, un apartado directo de seguimiento de sus… | 7,8,12 | Pendiente |  |
| RFS42 | publicar de forma accesible en todo el sitio la Política de Privacidad y los Términos y Condiciones del… | 5 | Pendiente |  |
| RFS43 | exigir en el registro de cuenta y envío de formularios una casilla sin marcar por defecto (opt-in) para… | 5,6,7,14 | Pendiente |  |
| RFS44 | registrar en la base de datos quién aceptó los Términos y Condiciones, la Política de Privacidad y el permiso… | 4,6 | Pendiente |  |
| RFS45 | mostrar un banner de consentimiento de cookies al ingresar al sitio y bloquear la ejecución de cualquier… | 5 | Pendiente |  |
| RFS46 | permitir a cada cliente registrado descargar en un archivo estructurado todos sus datos personales e… | 11 | Pendiente |  |
| RFS47 | verificar y registrar que toda publicación en la vitrina o catálogo que contenga logotipos o marcas de… | 5,9 | Pendiente |  |
| RFS48 | procesar el enlace de baja (unsubscribe) incluido en los correos electrónicos para revocar de inmediato el… | 11 | Pendiente |  |

## Requisitos funcionales del usuario (34)

| Requisito | Descripción | Fase | Estado | Evidencia |
|---|---|---|---|---|
| RFU01 | (Visitante) ver en la página principal de «Letrería Los Herrera» la vitrina de trabajos realizados con imágenes, tipo de… | 5 | Pendiente |  |
| RFU02 | (Visitante) saber el horario, los datos de contacto de la empresa y si el local está abierto o el dueño ausente. | 5 | Pendiente |  |
| RFU03 | (Visitante) entrar al portal escaneando un código QR. | 5 | Pendiente |  |
| RFU04 | (Visitante) crear una cuenta de cliente marcando voluntariamente la aceptación de los términos y condiciones, la política… | 6 | Pendiente |  |
| RFU05 | (Cliente, trabajador, dueño) iniciar sesión; el cliente, también con Google. | 6,14 | Pendiente |  |
| RFU06 | (Visitante) enviar una solicitud de trabajo desde la web a cualquier hora. | 7 | Pendiente |  |
| RFU07 | (Cliente registrado) consultar desde un apartado en su vista principal el seguimiento y estado de sus solicitudes, cotizaciones y… | 8 | Pendiente |  |
| RFU08 | (Cliente registrado) aceptar o rechazar una cotización contando que al enviar la solicitud el dueño probablemente se contactará… | 8 | Pendiente |  |
| RFU09 | (Cliente) recibir un aviso cuando su solicitud o pedido cambie de estado, con la opción de darse de baja de correos no… | 11 | Pendiente |  |
| RFU10 | (Trabajador) registrar la solicitud de un cliente que llega al local. | 7 | Pendiente |  |
| RFU11 | (Trabajador) consultar, modificar, rechazar y eliminar solicitudes. | 7 | Pendiente |  |
| RFU12 | (Trabajador) cotizar separando materiales, mano de obra y otros gastos. (!) | 8 | Pendiente |  |
| RFU13 | (Trabajador) confirmar el pago y convertir la cotización en pedido. | 8 | Pendiente |  |
| RFU14 | (Trabajador) asignar responsable, actualizar el avance y registrar la entrega o instalación. | 8 | Pendiente |  |
| RFU15 | (Trabajador) ver en un calendario las entregas comprometidas. | 8 | Pendiente |  |
| RFU16 | (Trabajador) avisar si el local está abierto, cerrado o en pausa y cuándo vuelve; el dueño, además, cambiar el horario. (!) | 9 | Pendiente |  |
| RFU17 | (Dueño) mantener catálogo, productos, categorías y materiales, indicando si se cuenta con el permiso del cliente… | 9 | Pendiente |  |
| RFU18 | (Dueño) mantener empleados y sus roles. | 9 | Pendiente |  |
| RFU19 | (Dueño) acceder al iniciar sesión a la vista de administración con las solicitudes pendientes y ver gráficos de… | 7,12 | Pendiente |  |
| RFU20 | (Trabajador) registrar su disponibilidad horaria para que la agenda la muestre a los clientes. | 9 | Pendiente |  |
| RFU21 | (Dueño) registrar y desactivar el rostro de los empleados para el ingreso rápido. | 10 | Pendiente |  |
| RFU22 | (Dueño, trabajador) ingresar al panel mirando a la cámara y entrar con su rol; si no es reconocido, usar contraseña. | 10 | Pendiente |  |
| RFU23 | (Visitante, cliente, dueño) usar el asistente con inteligencia artificial para describir en lenguaje natural el letrero o trabajo que… | 14 | Pendiente |  |
| RFU24 | (Dueño, trabajador) generar, filtrar e imprimir desde el navegador el reporte operativo de pedidos y solicitudes con un diseño… | 13 | Pendiente |  |
| RFU25 | (Dueño) generar y consultar el reporte gerencial (con totales y gráficos) y el reporte parametrizable por rango de… | 13 | Pendiente |  |
| RFU26 | (Dueño, trabajador) exportar los reportes autorizados para su rol a archivos Excel (.xlsx), PDF y CSV conservando los filtros… | 13 | Pendiente |  |
| RFU27 | (Dueño) filtrar interactivamente el dashboard por fechas, categorías y estados, y consultar la tabla de datos y… | 12 | Pendiente |  |
| RFU28 | (Dueño) consultar el registro de auditoría del sistema para revisar quién creó o modificó registros principales,… | 11 | Pendiente |  |
| RFU29 | (Visitante, cliente, trabajador, dueño) navegar y operar todas las pantallas del sistema utilizando únicamente el teclado o un lector de pantalla,… | 2,3,15 | Pendiente |  |
| RFU30 | (Visitante, cliente registrado) visualizar en la vista principal (con o sin sesión iniciada) un mapa interactivo con la ubicación exacta del… | 5 | Pendiente |  |
| RFU31 | (Visitante, cliente) contactar directamente al taller mediante un botón en la vista principal que redirija al WhatsApp oficial de… | 5 | Pendiente |  |
| RFU32 | (Visitante, cliente) enviar consultas generales a través del formulario de contacto de la vista principal para que el… | 5 | Pendiente |  |
| RFU33 | (Visitante, cliente) leer la Política de Privacidad y los Términos y Condiciones, y decidir en el banner de cookies si acepta o… | 5 | Pendiente |  |
| RFU34 | (Cliente registrado) descargar una copia de todos sus datos personales almacenados en el sistema o solicitar el borrado definitivo… | 11 | Pendiente |  |

## Requisitos no funcionales del producto (29)

| Requisito | Descripción | Fase | Estado | Evidencia |
|---|---|---|---|---|
| RNFP01 | (Rendimiento) responder cada consulta o registro en 2 segundos o menos en el 95 % de las peticiones, con hasta 20 usuarios… | 15 | Pendiente |  |
| RNFP02 | (Facilidad de uso) que un cliente sin capacitación registre una solicitud desde el celular en 5 minutos o menos. | 7,15 | Pendiente |  |
| RNFP03 | (Facilidad de uso) adaptar todas las pantallas a anchos desde 360 px sin desplazamiento horizontal. | 2,15 | Pendiente |  |
| RNFP04 | (Disponibilidad) 99 % de disponibilidad mensual, 24 horas, aunque el local esté cerrado. | 16 (externo) | Pendiente |  |
| RNFP05 | (Compatibilidad) funcionar en las dos últimas versiones de Chrome, Edge, Firefox y Safari, sin complementos. | 15 | Pendiente |  |
| RNFP06 | (Seguridad) guardar contraseñas solo como hash con sal. | 6 | Pendiente |  |
| RNFP07 | (Seguridad) aplicar limitación de tasa (rate limiting) por dirección IP y por usuario en los endpoints de la API (con un… | 1,6 | Pendiente |  |
| RNFP08 | (Seguridad) tokens de sesión que caduquen a los 15 minutos (configurable). | 6 | Pendiente |  |
| RNFP09 | (Confiabilidad) respaldar la base de datos una vez al día y conservar cada respaldo 30 días, fuera del servidor principal. | 16 | Pendiente |  |
| RNFP10 | (Integridad) rechazar, con aviso, el cambio de un registro que otro usuario modificó después de abrirlo. | 7,8,9 | Pendiente |  |
| RNFP11 | (Mantenibilidad) documentar todas las rutas de la API en OpenAPI, consultables con Scalar o Swagger en desarrollo. | 1 | Pendiente |  |
| RNFP12 | (Seguridad) resolver el ingreso facial en 3 s o menos, con el mismo límite de 10 intentos por minuto que el ingreso con… | 10 | Pendiente |  |
| RNFP13 | (Seguridad) completar el flujo de recuperación de contraseña mediante un enlace o código temporal de un solo uso con… | 6 | Pendiente |  |
| RNFP14 | (Facilidad de uso y Responsive) aplicar un diseño mobile first con sistema de diseño propio y consistente, adaptable sin desplazamiento… | 2,3 | Pendiente |  |
| RNFP15 | (Accesibilidad) cumplir el estándar WCAG 2.2 nivel AA en todas las pantallas, incluyendo HTML semántico, navegación completa… | 2,15 | Pendiente |  |
| RNFP16 | (Seguridad) autenticar mediante JWT en la API con autorización basada en al menos tres roles diferenciados y mitigar al… | 1,17 | Pendiente |  |
| RNFP17 | (Confiabilidad e IA) gestionar las claves de APIs externas (IA, OAuth, SMTP) exclusivamente mediante variables de entorno (en… | 4,14 | Pendiente |  |
| RNFP18 | (Rendimiento) ejecutar la generación y exportación de reportes grandes a Excel, PDF y CSV de forma asíncrona sin bloquear… | 13 | Pendiente |  |
| RNFP19 | (Mantenibilidad) mantener el contrato OpenAPI actualizado y accesible desde la API, habiéndolo definido antes de la… | 1 y cada fase | Pendiente |  |
| RNFP20 | (Confiabilidad) contar con una estrategia de respaldo y restauración de la base de datos documentada y probada de principio a… | 16 | Pendiente |  |
| RNFP21 | (Ética y usabilidad) diseñar todas las pantallas y flujos libres de patrones engañosos (dark patterns), garantizando que las… | 5 y cada fase | Pendiente |  |
| RNFP22 | (Rendimiento de recursos y fuentes) servir las imágenes del catálogo y la interfaz optimizadas en formatos modernos (WebP o AVIF), aplicar carga… | 2,4,5 | Pendiente |  |
| RNFP23 | (Rendimiento de red en cliente) evitar llamadas duplicadas a la API al enviar formularios o cargar vistas, aplicar `debounce` (por ejemplo,… | 3,15 | Pendiente |  |
| RNFP24 | (Rendimiento de renderizado en cliente) dividir el código de React por ruta o página (`code splitting` con carga diferida de vistas), evitar… | 3,15 | Pendiente |  |
| RNFP25 | (Caché y compresión HTTP) activar la compresión estándar (Gzip/Brotli) en las respuestas de ASP.NET Core y configurar cabeceras de… | 1,16 | Pendiente |  |
| RNFP26 | (Eficiencia en base de datos) utilizar el pool de conexiones por defecto de ADO.NET / Entity Framework Core hacia SQL Server, evitar las… | 1 y cada fase | Pendiente |  |
| RNFP27 | (Verificación en condiciones reales) comprobar el rendimiento y la usabilidad del sistema en dispositivos móviles y bajo simulación de redes… | 15 | Pendiente |  |
| RNFP28 | (Seguridad en consultas y aislamiento de datos) utilizar exclusivamente consultas parametrizadas o LINQ en Entity Framework Core sin concatenar jamás… | 1 | Pendiente |  |
| RNFP29 | (Seguridad de orígenes) configurar una política de CORS cerrada en la API que permita peticiones únicamente desde los orígenes… | 6,16 | Pendiente |  |

## Requisitos no funcionales organizacionales (17)

| Requisito | Descripción | Fase | Estado | Evidencia |
|---|---|---|---|---|
| RNFO01 | construir con React, ASP.NET Core Web API y SQL Server. | cumplido antes del plan | Cumplido | React, ASP.NET Core Web API y SQL Server: `frontend/` y `backend/` |
| RNFO02 | aplicar el flujo solicitud -> cotización -> aceptación y pago -> pedido; ningún pedido sin cotización… | 8 | Pendiente |  |
| RNFO03 | publicar el horario que fije el dueño y permitir que solo el personal cambie el estado del local. | 9 | Pendiente |  |
| RNFO04 | conservar solicitudes rechazadas y eliminadas al menos 12 meses, para estadísticas. | 7,11 | Pendiente |  |
| RNFO05 | montos en pesos chilenos sin decimales y fechas en formato dd-mm-aaaa. | 1,3 | Pendiente |  |
| RNFO06 | validar el RUT con su dígito verificador. | 1,3 | Pendiente |  |
| RNFO07 | todas las pantallas y mensajes en español. | todas | Pendiente |  |
| RNFO08 | permitir solo los roles administrador, dueño, diseñador y operario para el personal. | 9 | Pendiente |  |
| RNFO09 | desarrollar con Kanban y mantener el código en un repositorio con control de versiones. | 0 | En curso | Repositorio único y tablero Kanban creados en la fase 0 (`tablero.md`) |
| RNFO10 | estructurar el backend en cuatro capas claramente separadas (presentación, API, servicios de negocio y acceso… | 1, cada fase y 17 | Pendiente |  |
| RNFO11 | normalizar el modelo de datos relacional con al menos seis entidades relacionadas, gestionando el esquema… | cada migración y 12 | Pendiente |  |
| RNFO12 | poblar la base de datos para pruebas y demostración con datos sintéticos verosímiles (nunca datos personales… | 1 (reducido) | Pendiente |  |
| RNFO13 | implementar pruebas unitarias automatizadas para la capa de negocio, pruebas de integración automatizadas… | 1,14,15 | Pendiente |  |
| RNFO14 | contenerizar el sistema mediante `Dockerfile` multi-stage con imagen base ligera de versión fija y un archivo… | 16 | Pendiente |  |
| RNFO15 | automatizar un pipeline CI/CD que construya las imágenes, ejecute las suites de pruebas y publique únicamente… | 0,16 | Pendiente |  |
| RNFO16 | revisar las dependencias del proyecto para eliminar paquetes o librerías sin usar tanto en el front end como… | 3,16 | Pendiente |  |
| RNFO17 | verificar y documentar el cumplimiento de las licencias de todas las imágenes, fuentes tipográficas, iconos y… | 2,5,16 | Pendiente |  |

## Requisitos no funcionales externos (9)

| Requisito | Descripción | Fase | Estado | Evidencia |
|---|---|---|---|---|
| RNFE01 | cumplir la Ley N.º 19.628 (modificada por la Ley N.º 21.719): informar el uso de los datos en la Política de… | 5,6,11 | Pendiente |  |
| RNFE02 | integrarse con el inicio de sesión de Google mediante OAuth 2.0. | 14 | Pendiente |  |
| RNFE03 | comunicarse solo por HTTPS, con certificado TLS vigente. | 16 (externo) | Pendiente |  |
| RNFE04 | aplicar las recomendaciones del OWASP Top 10:2025 y OWASP API Security Top 10. | cada fase y 17 | Pendiente |  |
| RNFE05 | integrarse con un servicio de correo saliente con remitente autenticado. | 4,11 | Pendiente |  |
| RNFE06 | tratar el rostro como dato personal sensible: consentimiento expreso, guardar solo el descriptor y permitir… | 10 | Pendiente |  |
| RNFE07 | integrarse mediante HTTPS desde el backend con una API externa de inteligencia artificial generativa (como… | 14 | Pendiente |  |
| RNFE08 | cumplir con las pautas de accesibilidad de contenido web WCAG 2.2 en su nivel de conformidad AA del W3C. | 2,15 | Pendiente |  |
| RNFE09 | cumplir con los términos de licencia de los componentes open source, activos gráficos y tipografías… | 5,16 | Pendiente |  |

## Entradas (24)

| Requisito | Descripción | Fase | Estado | Evidencia |
|---|---|---|---|---|
| E01 | Datos del cliente: RUT (opcional en solicitud rápida), nombre, apellido, teléfono, correo, dirección y, si… | 6 | Pendiente |  |
| E02 | Credenciales: correo y contraseña, o autorización de Google. Cliente, trabajador. | 6 | Pendiente |  |
| E03 | Solicitud: cliente y descripción general del trabajo. Visitante o trabajador. | 7 | Pendiente |  |
| E04 | Ítems de la solicitud: producto, descripción, ancho, alto y cantidad. Visitante o trabajador. | 7 | Pendiente |  |
| E05 | Cambio en una solicitud: datos corregidos, motivo de rechazo o marca de eliminación. Trabajador. | 7 | Pendiente |  |
| E06 | Cotización: observaciones; por material: material, medida, cantidad y costo unitario; mano de obra; otros… | 8 | Pendiente |  |
| E07 | Respuesta a la cotización: aprobación o rechazo. Cliente registrado o trabajador. | 8 | Pendiente |  |
| E08 | Confirmación de pago y pedido: responsable, fecha estimada, modalidad de entrega, si requiere instalación,… | 8 | Pendiente |  |
| E09 | Avance del pedido: nuevo estado y, al entregar, fecha real. Trabajador. | 8 | Pendiente |  |
| E10 | Horario de atención: por día, si abre, hora de apertura y de cierre. Dueño. | 9 | Pendiente |  |
| E11 | Estado del local: abierto o ausente, mensaje y tiempo estimado de regreso. Trabajador. | 9 | Pendiente |  |
| E12 | Publicación del catálogo / vitrina: producto, título, descripción, imagen, tipo de letrero, ancho y alto… | 9 | Pendiente |  |
| E13 | Categoría y producto: nombre y descripción de la categoría; nombre, descripción, categoría y precio base… | 9 | Pendiente |  |
| E14 | Material: nombre, unidad de medida y costo unitario aproximado. Dueño. | 9 | Pendiente |  |
| E15 | Empleado: RUT, nombre, apellido, teléfono, correo, contraseña inicial y rol. Dueño. | 9 | Pendiente |  |
| E16 | Parámetros de reporte: reporte, rango (semana, mes o año), fechas desde y hasta y, si compara, el segundo… | 12,13 | Pendiente |  |
| E17 | Rostro del empleado: empleado elegido, tres capturas de cámara y consentimiento expreso. Dueño. | 10 | Pendiente |  |
| E18 | Captura de ingreso: una captura del rostro al ingresar. Dueño o trabajador. | 10 | Pendiente |  |
| E19 | Consulta al asistente de IA: descripción en lenguaje natural del letrero requerido o datos base del producto… | 14 | Pendiente |  |
| E20 | Configuración de exportación de reporte: tipo de reporte (operativo, gerencial o parametrizable), filtros… | 13 | Pendiente |  |
| E21 | Filtros del dashboard: rango de fechas, categoría de producto, estado de pedido u origen de solicitud. Dueño. | 12 | Pendiente |  |
| E22 | Formulario de contacto: nombre del remitente, correo electrónico, teléfono (opcional), asunto y mensaje de… | 5 | Pendiente |  |
| E23 | Registro de consentimiento legal y privacidad: aceptación de Términos y Condiciones y Política de Privacidad… | 5,6 | Pendiente |  |
| E24 | Solicitud sobre datos personales o baja de correo: petición de descarga de datos personales, solicitud de… | 11 | Pendiente |  |

## Salidas (23)

| Requisito | Descripción | Fase | Estado | Evidencia |
|---|---|---|---|---|
| S01 | Catálogo público y vitrina principal («Letrería Los Herrera»): título y datos de contacto de la empresa,… | 5 | Pendiente |  |
| S02 | Horario y estado del local: horario de lunes a domingo, estado actual, mensaje, tiempo estimado de regreso y… | 5 | Pendiente |  |
| S03 | Comprobante de solicitud: número, fecha y hora, nombre del cliente, ítems (producto, medidas, área, cantidad)… | 7 | Pendiente |  |
| S04 | Seguimiento del cliente en vista principal: por solicitud, número, fecha, estado y motivo de rechazo; total y… | 8 | Pendiente |  |
| S05 | Bandeja de solicitudes y vista principal de administración: resumen de solicitudes pendientes, métricas clave… | 7 | Pendiente |  |
| S06 | Cotización: número, fecha, cliente, ítems, detalle de materiales, costo total de materiales, mano de obra,… | 8 | Pendiente |  |
| S07 | Ficha del pedido: número, solicitud y cotización de origen, cliente, responsable, fechas, modalidad,… | 8 | Pendiente |  |
| S08 | Calendario de entregas: pedidos por día con cliente, modalidad y estado, con colores e íconos o etiquetas… | 8 | Pendiente |  |
| S09 | Notificación al cliente y correo de contacto al administrador: al cliente, número de solicitud o pedido,… | 5,11 | Pendiente |  |
| S10 | Pedidos por mes: cantidad de pedidos creados por estado, sin contar los cancelados en el total. Dueño. | 12 | Pendiente |  |
| S11 | Ingresos por mes: suma del monto final de los pedidos entregados y total del período. Dueño. | 12 | Pendiente |  |
| S12 | Solicitudes en línea: cantidad de online y, para comparar, de presenciales. Dueño. | 12 | Pendiente |  |
| S13 | Solicitudes en línea concretadas: online con un pedido no cancelado. Dueño. | 12 | Pendiente |  |
| S14 | Conversión en línea: porcentaje S13 sobre S12 y, al comparar dos períodos, la diferencia en puntos… | 12 | Pendiente |  |
| S15 | Resultado del ingreso facial: nombre y rol del empleado reconocido, o aviso de que no hubo coincidencia con… | 10 | Pendiente |  |
| S16 | Registro de rostros: por empleado, nombre, rol, fecha de registro, fecha del consentimiento y estado (activo… | 10 | Pendiente |  |
| S17 | Sugerencia del asistente de IA: respuesta estructurada con categoría sugerida, producto base,… | 14 | Pendiente |  |
| S18 | Reporte operativo, gerencial y parametrizable: documento descargable en Excel (`.xlsx` con encabezados,… | 13 | Pendiente |  |
| S19 | Dashboard e indicadores (KPIs): al menos cuatro gráficos interactivos actualizados con su título, descripción… | 12 | Pendiente |  |
| S20 | Bitácora de auditoría y consentimientos: historial de creación y modificación en entidades principales… | 11 | Pendiente |  |
| S21 | Respuesta de error estándar: estructura uniforme de error de la API con código de estado HTTP, mensaje… | 1 | Pendiente |  |
| S22 | Documentos legales y banner de cookies: vista de Términos y Condiciones, Política de Privacidad, inventario… | 5,16 | Pendiente |  |
| S23 | Archivo de portabilidad y comprobante de borrado: archivo descargable (JSON o CSV) con todos los datos… | 11 | Pendiente |  |
