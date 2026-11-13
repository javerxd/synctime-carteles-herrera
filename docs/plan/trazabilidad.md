# Matriz de trazabilidad

Cada requisito de `docs/requisitos/requerimientos_del_sistema.md`, la fase del plan donde se cumple y su evidencia.
Al cerrar cada fase se actualizan las columnas **Estado** y **Evidencia** de sus requisitos (prueba automática, pantalla o documento).
En la fase 17 no puede quedar ninguno en "Pendiente": cada uno termina como Cumplido, Desviación o Pendiente externo (ver `desviaciones.md`).

Total: **159 identificadores** (versión 2 del documento, 12-11-2026). La versión 1 tenía 183; la diferencia viene de agrupar 47 RFS en 12 macro-requerimientos y de los requisitos nuevos de la revisión con la profesora guía. **NUEVO** y **CAMBIA** marcan lo que introdujo esa revisión.

"3 (pantalla)" indica que la fase 3 deja la pantalla navegable con datos de la base demo para la revisión de avance, y la fase posterior la completa con su lógica.

## Requisitos funcionales del sistema (12 macro-requerimientos)

Cada macro-requerimiento agrupa las acciones de una pantalla o proceso. Su detalle está en viñetas en el documento de requisitos.

| Requisito | Macro-requerimiento | Fase | Estado | Evidencia |
|---|---|---|---|---|
| RFS01 | Publicitar el catálogo de trabajos realizados y enviar una solicitud. | 3 (pantalla), 5, 7, 9 | En curso | Fase 3: portada «Mostrador» con vitrina desde `GET /catalogo`, filtro por categoría y «Cotizar uno similar» (`src/paginas/publicas/Portada.jsx`); captura revisada a 390 y 1440 px. Fase 5: vitrina paginada y filtrable por categoría en la API (`ServicioVitrina.ListarAsync`) y en la portada («Ver más trabajos»), con fotos en WebP de dos tamaños, medidas y precio referencial (`PortadaApiTests.LaVitrina_SeFiltraPorCategoriaYSePagina_SinDatosDelPersonal`, `UnTrabajoConImagenSubida_LaMuestraEnDosTamanos`, `e2e/pantallas.spec.js`). Las fotos son ilustraciones de ejemplo, rotuladas. Fotos reales cargadas en la demo al cierre de la fase 5. Faltan el envío (fase 7) y la publicación desde el panel (fase 9) |
| RFS02 | Gestionar la disponibilidad horaria y el cartel electrónico. | 3 (pantalla), 5, 9 | En curso | Fase 3: cartel con texto, mensaje, regreso y última actualización en la portada y en `/panel/local`, que dueño y administrador dan vuelta (`PUT /cartel-digital`); horario semanal con «Hoy»; código QR descargable en PNG y SVG e imprimible (`EstadoLocal.jsx`, `CodigoQr.jsx`, `e2e/pantallas.spec.js`). Fase 5: «abierto ahora» calculado con la hora de Chile (`GET /local/estado`, `ReglaLocalAbierto`, `PortadaTests`, `PortadaApiTests.ElEstadoDelLocal_CombinaHorarioYCartel_SinDatosDelPersonal`); la portada lo actualiza cada 60 segundos, y el QR apunta a `TALLER.urlPortal`. Faltan editar el horario y registrar quién cambió el cartel (fase 9) |
| RFS03 | Autenticación, gestión de cuentas y acceso biométrico facial. | 6, 10, 14 | Pendiente | Incluye desviaciones del ingreso facial (ver `desviaciones.md`) |
| RFS04 | Gestión integral de solicitudes y prevención de solicitudes falsas. | 3 (modal), 6, 7 | En curso | Fase 3: modal de solicitud con tipo, medidas, cantidad, detalle y contacto; validación que enfoca el primer error; casilla de privacidad desmarcada (`ModalSolicitud.jsx`, `e2e/pantallas.spec.js`). No envía: muestra el aviso S25 hasta la fase 7 |
| RFS05 | Gestión de cotizaciones y aprobación por doble vía. | 8 | Pendiente |  |
| RFS06 | Gestión de pedidos, control independiente de pagos y abonos, y calendario. | 3 (pantalla), 8 | En curso | Ajuste del 12-11-2026: modelo de datos con estado de pedido y de pago independientes e historial de abonos (migraciones `SepararEstadoPedidoYPago` y `HistorialDePagos`, pruebas `PedidoPagoTests`). Fase 3: listado con ambos estados, monto final, abonado y saldo, filtros, ficha y formulario del pedido directo (`Pedidos.jsx`, `reglasPedidos.js`, `pedidos.test.js`, `e2e/pantallas.spec.js`); semilla demo con 12 pedidos (`SemillaDemo.cs`, `CimientosTests`). Falta la lógica de negocio (fase 8) |
| RFS07 | Mantenedores del sistema (catálogo, tipos de letrero, materiales y personal). | 9 | Pendiente |  |
| RFS08 | Notificaciones por correo y formulario de contacto. | 4, 5, 11 | En curso | Fase 4: cola de correo `correos_salientes` con envío en segundo plano, reintentos y tiempo máximo (MailKit, Gmail por SMTP con STARTTLS); bandeja local en desarrollo; plantilla base en HTML y texto; **aviso al taller por cada solicitud nueva** (pedido por el usuario el 13-11-2026), con tope por hora contra envíos masivos (`ColaDeCorreoTests`, `CorreoTests`). Fase 5: formulario de contacto (`POST /contacto`, `ServicioContacto`) que guarda la consulta y su consentimiento en una transacción y deja un aviso al taller con respuesta dirigida al visitante, con sublímite de 5 por hora dentro del tope de 20 (`PortadaApiTests`, `PortadaTests`). Faltan los avisos al cliente con enlace de baja (fase 11) |
| RFS09 | Análisis de datos, indicadores (KPIs) y dashboard interactivo. | 3 (pantalla), 12 | En curso | Fase 3: estructura del panel de indicadores con 6 KPI y su fórmula, 4 gráficos de barras con tabla de datos (`Indicadores.jsx`, `GraficoBarras.jsx`), calculados en el navegador con la base demo y rotulados como tales. Los cálculos en la API llegan en la fase 12 |
| RFS10 | Generación, exportación (Excel, PDF y CSV) e impresión de reportes. | 3 (pantalla), 13 | En curso | Fase 3: pestañas Operativo, Gerencial y Parametrizable con filtros por fechas, estado y pago y vista previa (`Reportes.jsx`). Exportación e impresión en la fase 13 |
| RFS11 | Asistente con inteligencia artificial externa. | 14 | Pendiente |  |
| RFS12 | Auditoría transversal, gestión de consentimientos e inhabilitación de cuentas. | 4, 5, 6, 11 | En curso | Fase 4: registro de consentimientos con titular (cliente, empleado o envío de un visitante), tipo, versión del documento, fecha y hora y revocación; un solo titular y una sola aceptación vigente por versión, exigidos por la base (tabla `consentimientos`, `ServicioConsentimientos`, `ConsentimientoServicioTests`). Fase 5: documentos legales con su versión vigente (`GET /documentos-legales`, `DocumentosLegales`), consentimiento de privacidad de cada consulta y consentimiento de cookies por visitante con su retiro (`PortadaApiTests.AceptarLasCookies_QuedaRegistradoPorVisitante`, `RechazarLasCookiesDespuesDeAceptarlas_RegistraElRetiro`), páginas `/privacidad` y `/terminos` en el pie del sitio público, y aviso de cookies (`BannerCookies.jsx`, `e2e/pantallas.spec.js`). Los textos son borradores. Faltan las casillas de la cuenta (fase 6), la auditoría y la baja (fase 11) |

## Requisitos funcionales del usuario (36)

| Requisito | Descripción | Fase | Estado | Evidencia |
|---|---|---|---|---|
| RFU01 | (Visitante) ver en la página principal de «Letreros Herrera» la vitrina de trabajos realizados con… | 5 | En curso | Fase 5: vitrina en la página principal con foto, tipo de letrero, medidas, precio «Desde» y «Cotizar uno similar» (`Portada.jsx`, `e2e/pantallas.spec.js`). Vitrina con 22 fotos reales del taller (cierre de la fase 5) |
| RFU02 | (Visitante) saber el horario, los datos de contacto de la empresa y si el local está abierto o el dueño… | 5 | En curso | Fase 5: horario semanal con «Hoy», hora de Chile y datos de contacto; el mostrador dice si el local está abierto, ausente o cerrado, y cuándo abre (`Portada.jsx`, `horario.js`, `GET /local/estado`) |
| RFU03 | (Visitante) entrar al portal escaneando un código QR. | 5 | En curso | Fase 3: código QR del portal descargable y para imprimir (`EstadoLocal.jsx`, `CodigoQr.jsx`). Fase 5: el QR apunta a `TALLER.urlPortal`, que se fija con `VITE_URL_PORTAL` (sin ella, la dirección del sitio que se ve). Falta fijar la dirección pública al desplegar (fase 16) |
| RFU04 | (Visitante) crear una cuenta de cliente marcando voluntariamente la aceptación de los términos y condiciones,… | 6 | Pendiente |  |
| RFU05 | (Cliente, trabajador, dueño) iniciar sesión; el cliente, también con Google. | 6,14 | Pendiente |  |
| RFU06 **CAMBIA** | (Visitante, cliente) enviar una solicitud de trabajo desde la vitrina a cualquier hora, con una cuenta de… | 3 (modal), 7 | Pendiente |  |
| RFU07 | (Cliente registrado) consultar desde un apartado en su vista principal el seguimiento y estado de sus… | 8 | Pendiente |  |
| RFU08 | (Cliente registrado) aceptar o rechazar una cotización contando que al enviar la solicitud el dueño… | 8 | Pendiente |  |
| RFU09 | (Cliente) recibir un aviso cuando su solicitud o pedido cambie de estado, con la opción de darse de baja de… | 11 | Pendiente |  |
| RFU10 | (Trabajador) registrar la solicitud de un cliente que llega al local. | 7 | Pendiente |  |
| RFU11 | (Trabajador) consultar, modificar, rechazar y eliminar solicitudes. | 7 | Pendiente |  |
| RFU12 | (Trabajador) cotizar separando materiales, mano de obra y otros gastos. (!) | 8 | Pendiente |  |
| RFU13 **CAMBIA** | (Trabajador) convertir la cotización aprobada en pedido y registrar los abonos y pagos del cliente, con el… | 8 | Pendiente |  |
| RFU14 | (Trabajador) asignar responsable, actualizar el avance y registrar la entrega o instalación. | 8 | Pendiente |  |
| RFU15 | (Trabajador) ver en un calendario las entregas comprometidas. | 8 | Pendiente |  |
| RFU16 **CAMBIA** | (Trabajador) dar vuelta el cartel electrónico para avisar si el local está abierto o cerrado, con un mensaje… | 3 (pantalla), 9 | En curso | Fase 3: formulario «Dar vuelta el cartel» en `/panel/local` (prueba `e2e/pantallas.spec.js`). Hoy solo dueño y administrador, por el permiso de la API: ver `desviaciones.md` (decisión en la fase 9) |
| RFU17 | (Dueño) mantener catálogo, productos, categorías y materiales, indicando si se cuenta con el permiso del… | 9 | Pendiente |  |
| RFU18 | (Dueño) mantener empleados y sus roles. | 9 | Pendiente |  |
| RFU19 | (Dueño) acceder al iniciar sesión a la vista de administración con las solicitudes pendientes y ver gráficos… | 7,12 | Pendiente |  |
| RFU20 | (Trabajador) registrar su disponibilidad horaria para que la agenda la muestre a los clientes. | 9 | Pendiente |  |
| RFU21 | (Dueño) registrar y desactivar el rostro de los empleados para el ingreso rápido. | 10 | Pendiente |  |
| RFU22 | (Dueño, trabajador) ingresar al panel mirando a la cámara y entrar con su rol; si no es reconocido, usar… | 10 | Pendiente |  |
| RFU23 | (Visitante, cliente, dueño) usar el asistente con inteligencia artificial para describir en lenguaje natural… | 14 | Pendiente |  |
| RFU24 | (Dueño, trabajador) generar, filtrar e imprimir desde el navegador el reporte operativo de pedidos y… | 13 | Pendiente |  |
| RFU25 | (Dueño) generar y consultar el reporte gerencial (con totales y gráficos) y el reporte parametrizable por… | 13 | Pendiente |  |
| RFU26 | (Dueño, trabajador) exportar los reportes autorizados para su rol a archivos Excel (.xlsx), PDF y CSV… | 13 | Pendiente |  |
| RFU27 | (Dueño) filtrar interactivamente el dashboard por fechas, categorías y estados, y consultar la tabla de datos… | 12 | Pendiente |  |
| RFU28 | (Dueño) consultar el registro de auditoría del sistema para revisar quién creó o modificó registros… | 11 | Pendiente |  |
| RFU29 | (Visitante, cliente, trabajador, dueño) navegar y operar todas las pantallas del sistema utilizando… | 2,3,15 | En curso | Fase 2: sistema de diseño y galería `/galeria.html`. Fase 3: todas las pantallas con enlace de salto, navegación por teclado, menú del panel en `<dialog>` en el teléfono, avisos anunciados; axe sin errores críticos ni serios a 360, 768 y 1440 px (`e2e/pantallas.spec.js`). Falta la prueba con NVDA (fase 15) |
| RFU30 | (Visitante, cliente registrado) visualizar en la vista principal (con o sin sesión iniciada) un mapa… | 5 | En curso | Fase 5: imagen estática del mapa en el propio sitio (`public/mapa/ubicacion-taller.webp`). Punto exacto entregado por el taller. El mapa interactivo (Leaflet con OpenStreetMap) se carga solo al pulsar «Mapa interactivo», y la fachada en Google Street View solo con «Ver la fachada»; «Abrir en Google Maps» lleva al enlace del taller (`MapaUbicacion.jsx`; `e2e/pantallas.spec.js`: «el mapa no contacta a OpenStreetMap ni a Google hasta que el visitante lo activa») |
| RFU31 | (Visitante, cliente) contactar directamente al taller mediante un botón en la vista principal que redirija al… | 5 | En curso | Fase 5: botón flotante «Escribir por WhatsApp» y enlaces en el mostrador y el pie (`EstructuraPublica.jsx`, `Portada.jsx`). El número es de prueba: se cambia por el del taller antes de la entrega (`src/configuracion/taller.js`) |
| RFU32 | (Visitante, cliente) enviar consultas generales a través del formulario de contacto de la vista principal… | 5 | En curso | Fase 5: formulario de contacto en la portada (`FormularioContacto.jsx`, `reglasContacto.js`) con validación en el navegador y en la API; cada consulta llega al taller por correo (`AvisoConsultaContacto`) y queda guardada (`consultas_contacto`). Pruebas: `PortadaApiTests`, `PortadaTests`, `portada.test.jsx`, `e2e/pantallas.spec.js`. Falta la bandeja para revisarlas (fase 9) |
| RFU33 | (Visitante, cliente) leer la Política de Privacidad y los Términos y Condiciones, y decidir en el banner de… | 5 | En curso | Fase 5: páginas `/privacidad` y `/terminos` con borradores (versión 1.0), y aviso de cookies con dos botones de igual tamaño y la analítica desmarcada, que guarda la elección y registra la aceptación o su retiro (`BannerCookies.jsx`, `e2e/pantallas.spec.js`). Falta la revisión legal de los textos |
| RFU34 **CAMBIA** | (Cliente registrado) descargar una copia de todos sus datos personales almacenados en el sistema y dar de… | 11 | Pendiente |  |
| RFU35 **NUEVO** | (Dueño, trabajador) registrar un pedido presencial directo cuando el cliente acuerda el trabajo en el taller,… | 3 (pantalla), 8 | En curso | Fase 3: formulario del pedido presencial directo con validación (RUT con dígito verificador, montos enteros, abono no mayor al monto final), solo de presentación (`PedidoDirecto.jsx`). El guardado en una transacción llega en la fase 8 |
| RFU36 **NUEVO** | (Dueño) filtrar los pedidos por estado del pedido, estado de pago, año, mes y cliente, y listar los pedidos… | 3 (pantalla), 8, 13 | En curso | Fase 3: filtros por estado del pedido, estado de pago, año, mes y cliente (nombre sin tildes, RUT o teléfono), y «Por cobrar», aplicados en la interfaz (`reglasPedidos.js`, `pedidos.test.js`, `e2e/pantallas.spec.js`). Pasan a la API en la fase 8 |

## Requisitos no funcionales del producto (33)

| Requisito | Descripción | Fase | Estado | Evidencia |
|---|---|---|---|---|
| RNFP01 | (Rendimiento) responder cada consulta o registro en 2 segundos o menos en el 95 % de las peticiones, con… | 15 | Pendiente |  |
| RNFP02 | (Facilidad de uso) que un cliente sin capacitación registre una solicitud desde el celular en 5 minutos o… | 7,15 | Pendiente |  |
| RNFP03 | (Facilidad de uso) adaptar todas las pantallas a anchos desde 360 px sin desplazamiento horizontal. | 2,15 | En curso | Fase 2: sistema de diseño (`src/estilos/`, `src/componentes/ui/`) y galería `/galeria.html`: sin desplazamiento horizontal a 320, 360, 640, 768 y 1440 px (Playwright). Faltan las pantallas |
| RNFP04 | (Disponibilidad) 99 % de disponibilidad mensual, 24 horas, aunque el local esté cerrado. | 16 (externo) | Pendiente |  |
| RNFP05 | (Compatibilidad) funcionar en las dos últimas versiones de Chrome, Edge, Firefox y Safari, sin complementos. | 15 | Pendiente |  |
| RNFP06 | (Seguridad) guardar contraseñas solo como hash con sal. | 6 | Pendiente |  |
| RNFP07 | (Seguridad) aplicar limitación de tasa (rate limiting) por dirección IP y por usuario en los endpoints de la… | 1,6 | En curso | Fase 1: límite por IP y por cuenta (`LimitadorPorCuenta`), 429 con cuerpo. Falta la recuperación de clave (fase 6) |
| RNFP08 | (Seguridad) tokens de sesión que caduquen a los 15 minutos (configurable). | 6 | Pendiente |  |
| RNFP09 | (Confiabilidad) respaldar la base de datos una vez al día y conservar cada respaldo 30 días, fuera del… | 16 | Pendiente |  |
| RNFP10 | (Integridad) rechazar, con aviso, el cambio de un registro que otro usuario modificó después de abrirlo. | 7,8,9 | Pendiente |  |
| RNFP11 | (Mantenibilidad) documentar todas las rutas de la API en OpenAPI, consultables con Scalar o Swagger en… | 1 | Cumplido | OpenAPI y Scalar en desarrollo; contrato en `docs/api/openapi.json` |
| RNFP12 | (Seguridad) resolver el ingreso facial en 3 s o menos, con el mismo límite de 10 intentos por minuto que el… | 10 | Pendiente |  |
| RNFP13 | (Seguridad) completar el flujo de recuperación de contraseña mediante un enlace o código temporal de un solo… | 6 | Pendiente |  |
| RNFP14 | (Facilidad de uso y Responsive) aplicar un diseño mobile first con sistema de diseño propio y consistente,… | 2,3 | En curso | Fase 2: tokens, tabla adaptable, esqueletos, estados vacíos, objetivos de 44 px. Fase 3: estructura pública (barra de una fila, pie con enlaces legales) y del panel (menú lateral filtrado por rol, cajón en el teléfono), páginas 403, 404 y de error (`src/estructuras/`, `PaginasDeError.jsx`); sin desborde a 360, 768 y 1440 px (`e2e/pantallas.spec.js`). Fase 5: la portada, `/privacidad` y `/terminos` pasan la misma prueba a 360, 768 y 1440 px. Falta la revisión de la fase 15 |
| RNFP15 | (Accesibilidad) cumplir el estándar WCAG 2.2 nivel AA en todas las pantallas, incluyendo HTML semántico,… | 2,15 | En curso | Fase 2: sistema de diseño (`src/estilos/`, `src/componentes/ui/`) y galería `/galeria.html`: axe sin violaciones a 360 y 1440 px; contrastes medidos en `tokens.test.js`; Esc y retorno del foco en diálogos; movimiento reducido y alto contraste comprobados. Fase 5: la portada, `/privacidad` y `/terminos`, sin fallas críticas ni graves de axe a 360, 768 y 1440 px (`e2e/pantallas.spec.js`). Faltan las pantallas restantes y la prueba manual |
| RNFP16 | (Seguridad) autenticar mediante JWT en la API con autorización basada en al menos tres roles diferenciados y… | 1,17 | Pendiente |  |
| RNFP17 | (Confiabilidad e IA) gestionar las claves de APIs externas (IA, OAuth, SMTP) exclusivamente mediante… | 4,14 | En curso | Fase 4: la cuenta SMTP solo se lee de User Secrets o variables de entorno; cada envío tiene tiempo máximo; un SMTP caído no bloquea ni rompe la API, y ninguna credencial aparece en registros, respuestas ni en la base (`ColaDeCorreoTests.UnSmtpCaido_NoBloqueaLaPeticion…`, `LasCredenciales_NoAparecenEnLogsRespuestasNiEnLaBase`). Faltan IA y OAuth (fase 14) |
| RNFP18 | (Rendimiento) ejecutar la generación y exportación de reportes grandes a Excel, PDF y CSV de forma asíncrona… | 13 | Pendiente |  |
| RNFP19 | (Mantenibilidad) mantener el contrato OpenAPI actualizado y accesible desde la API, habiéndolo definido antes… | 1 y cada fase | En curso | Fase 1: contrato con las 58 operaciones y prueba `ContratoTests`. Faltan los códigos de respuesta por operación |
| RNFP20 | (Confiabilidad) contar con una estrategia de respaldo y restauración de la base de datos documentada y… | 16 | Pendiente |  |
| RNFP21 | (Ética y usabilidad) diseñar todas las pantallas y flujos libres de patrones engañosos (dark patterns),… | 5 y cada fase | En curso | Fase 5: aviso de cookies con «Rechazar opcionales» y «Guardar mi elección» del mismo tamaño y la analítica desmarcada, así que rechazar es tan fácil como aceptar; la casilla de privacidad del formulario viene desmarcada (`BannerCookies.jsx`, `portada.test.jsx`, `e2e/pantallas.spec.js`: «el aviso de cookies ofrece dos opciones iguales»). Faltan los flujos de cuenta y de solicitud (fases 6 y 7) |
| RNFP22 | (Rendimiento de recursos y fuentes) servir las imágenes del catálogo y la interfaz optimizadas en formatos… | 2,4,5 | En curso | Fase 2: Plus Jakarta Sans alojada en el sitio, solo 4 grosores y el subconjunto latino. Fase 4: imágenes subidas convertidas a WebP en dos tamaños (1600 y 640 px), enderezadas y sin metadatos, servidas con caché de un año (`ImagenApiTests`, `ImagenesTests`). Fase 5: la vitrina sirve WebP en dos tamaños (`srcset` de 640 y 1600 px) con carga diferida y dimensiones fijas, y las imágenes de ejemplo se sirven igual (`Portada.jsx`, `e2e/pantallas.spec.js`). Falta la medición de la fase 15 |
| RNFP23 | (Rendimiento de red en cliente) evitar llamadas duplicadas a la API al enviar formularios o cargar vistas,… | 3,15 | En curso | Fase 3: cliente `fetch` único que comparte las lecturas iguales en curso (`src/api/cliente.js`, `clienteApi.test.js`); botones que no repiten la acción mientras cargan; peticiones independientes de cada pantalla en paralelo. Falta la revisión de la fase 15 (cada pantalla vuelve a pedir sus datos al abrirse) |
| RNFP24 | (Rendimiento de renderizado en cliente) dividir el código de React por ruta o página (`code splitting` con… | 3,15 | En curso | Fase 3: cada página se carga aparte con `lazy` y `Suspense` (`src/App.jsx`); el reconocimiento facial solo al usarlo. JavaScript inicial de 101,8 KB comprimido (presupuesto: 150 KB, medido en la fase 3). Fase 5: las páginas legales se cargan aparte, y Leaflet solo cuando el visitante pide el mapa interactivo (`MapaUbicacion.jsx`). Falta la medición de la fase 15 |
| RNFP25 | (Caché y compresión HTTP) activar la compresión estándar (Gzip/Brotli) en las respuestas de ASP.NET Core y… | 1,16 | En curso | Fase 1: compresión Brotli y Gzip, probada. Falta la caché de archivos estáticos (fase 16) |
| RNFP26 | (Eficiencia en base de datos) utilizar el pool de conexiones por defecto de ADO.NET / Entity Framework Core… | 1 y cada fase | En curso | Fase 1: proyecciones y paginación disponibles. Se aplican por módulo |
| RNFP27 | (Verificación en condiciones reales) comprobar el rendimiento y la usabilidad del sistema en dispositivos… | 15 | Pendiente |  |
| RNFP28 | (Seguridad en consultas y aislamiento de datos) utilizar exclusivamente consultas parametrizadas o LINQ en… | 1 | En curso | Fase 1: orden solo por campos permitidos, probado contra inyección. `/mis` filtra por el token |
| RNFP29 | (Seguridad de orígenes) configurar una política de CORS cerrada en la API que permita peticiones únicamente… | 6,16 | Pendiente |  |
| RNFP30 **NUEVO** | (Validación y errores; antes RFS31) validar todos los datos de entrada en tres niveles (formularios del front… | 1, 3 | En curso | Fase 1: error estándar y validadores (`Negocio/Validacion.cs`). Fase 3: nivel de formulario en el acceso, el modal de solicitud, el pedido directo y el cartel: mensajes en español que dicen cómo seguir y foco en el primer error; el cliente traduce el error estándar de la API (`ErrorApi`). Falta aplicarlos por módulo en la API (fases 6 a 9) |
| RNFP31 **NUEVO** | (Autorización; antes RFS23) permitir cada función solo a los roles autorizados, verificándolo siempre en el… | 1 y cada fase | En curso | Fase 1: `docs/api/permisos.md`, verificada por `ContratoTests`. Faltan los ajustes de las fases 7 a 10 |
| RNFP32 **NUEVO** | (Listados; antes RFS32) permitir buscar, filtrar, ordenar y paginar los listados principales del sistema… | 1, 5, 7, 8, 9 | En curso | Fase 1: `ParametrosListado`, `PaginarAsync` y `Ordenar`, probados. Fase 5: `GET /catalogo` pagina y filtra por categoría (`?categoria=`, `?pagina=`, `?tamano=`) en la API y en la vitrina (`ServicioVitrina.ListarAsync`, `VisibilidadPublicaTests`). Faltan los demás listados (solicitudes, cotizaciones, pedidos y clientes), en sus fases |
| RNFP33 **NUEVO** | (Protección contra solicitudes falsas; parte técnica de RFS04) limitar las solicitudes en línea por cuenta,… | 7 | Pendiente |  |

## Requisitos no funcionales organizacionales (17)

| Requisito | Descripción | Fase | Estado | Evidencia |
|---|---|---|---|---|
| RNFO01 | construir con React, ASP.NET Core Web API y SQL Server. | cumplido antes del plan | Cumplido | React, ASP.NET Core Web API y SQL Server: `frontend/` y `backend/` |
| RNFO02 **CAMBIA** | aplicar el flujo solicitud -> cotización -> aprobación -> pedido, o registrar un pedido presencial directo;… | 8 | En curso | Ajuste del 12-11-2026: el esquema ya separa el pago del pedido y admite el pedido directo. Falta la lógica de negocio (fase 8) |
| RNFO03 | publicar el horario que fije el dueño y permitir que solo el personal cambie el estado del local. | 9 | Pendiente |  |
| RNFO04 | conservar solicitudes rechazadas y eliminadas al menos 12 meses, para estadísticas. | 7,11 | Pendiente |  |
| RNFO05 | montos en pesos chilenos sin decimales y fechas en formato dd-mm-aaaa. | 1,3 | En curso | Fase 1: `Reglas.EsMontoEnPesos`. Fase 3: `formatearPesos` y `formatearFecha` (dd-mm-aaaa, sin corrimiento por zona horaria) en todas las pantallas (`src/utilidades/formato.js`, `utilidades.test.jsx`). Falta aplicarlo en los módulos de la API (fase 8) |
| RNFO06 | validar el RUT con su dígito verificador. | 1,3 | En curso | Fase 1: `Rut` con dígito verificador, 20 casos de prueba. Fase 3: `validarRut` y `formatearRut` en el frontend (`src/utilidades/rut.js`, `utilidades.test.jsx`), usados en el pedido directo. Falta aplicarlo en cuentas, clientes y empleados (fases 6 y 9) |
| RNFO07 | todas las pantallas y mensajes en español. | todas | Pendiente |  |
| RNFO08 | permitir solo los roles administrador, dueño, diseñador y operario para el personal. | 9 | Pendiente |  |
| RNFO09 | desarrollar con Kanban y mantener el código en un repositorio con control de versiones. | 0 | En curso | Repositorio único y tablero Kanban creados en la fase 0 (`tablero.md`) |
| RNFO10 | estructurar el backend en cuatro capas claramente separadas (presentación, API, servicios de negocio y acceso… | 1, cada fase y 17 | En curso | Fase 1: capas, repositorio base y pruebas de arquitectura (`docs/arquitectura.md`). Quedan 8 módulos por migrar |
| RNFO11 | normalizar el modelo de datos relacional con al menos seis entidades relacionadas, gestionando el esquema… | cada migración y 12 | En curso | Esquema normalizado de 15 tablas con migraciones versionadas, restricciones, índices justificados, vista, triggers y procedimiento; documentado en `docs/base-de-datos/`. Faltan las vistas del dashboard (fase 12) |
| RNFO12 | poblar la base de datos para pruebas y demostración con datos sintéticos verosímiles (nunca datos personales… | 1 (reducido) | En curso | Fase 1: base `LetreriaLosHerreraDB_Demo` y comando `sembrar-demo`. Reducido por decisión del usuario |
| RNFO13 | implementar pruebas unitarias automatizadas para la capa de negocio, pruebas de integración automatizadas… | 1,14,15 | En curso | Fase 1: 96 pruebas unitarias y 166 de integración (187 tras el ajuste). Fase 2: axe automático en `e2e/galeria.spec.js`. Faltan IA simulada y prueba manual |
| RNFO14 | contenerizar el sistema mediante `Dockerfile` multi-stage con imagen base ligera de versión fija y un archivo… | 16 | Pendiente |  |
| RNFO15 | automatizar un pipeline CI/CD que construya las imágenes, ejecute las suites de pruebas y publique únicamente… | 0,16 | Pendiente |  |
| RNFO16 | revisar las dependencias del proyecto para eliminar paquetes o librerías sin usar tanto en el front end como… | 3,16 | En curso | Fase 3: se eliminaron `axios`, `prop-types`, las páginas, estilos, recursos y pruebas del contrato antiguo; `npm audit --omit=dev` sin vulnerabilidades; `uqr` (MIT, sin dependencias) para el QR (`docs/licencias.md`). Falta la revisión del backend (fase 16) |
| RNFO17 | verificar y documentar el cumplimiento de las licencias de todas las imágenes, fuentes tipográficas, iconos y… | 2,5,16 | En curso | Fase 2: `docs/licencias.md` (fuente OFL, iconos ISC, paquetes del frontend). Fase 5: Leaflet (BSD-2-Clause) y OpenStreetMap, documentados en `docs/licencias.md`; ilustraciones de ejemplo propias; logotipos de clientes solo con autorización registrada, y una publicación con logo no se muestra sin ella (`ck_catalogo_logo`, `PUT /catalogo/{id}/autorizacion-logo`, `PortadaApiTests.UnaPublicacionConLogoDelCliente_NoSePuedeMostrarSinAutorizacion`, `LaBase_ImpideMostrarUnLogoSinAutorizacion`). Fotos reales con autorización de logo registrada (cierre de la fase 5). Faltan la verificación de las licencias de las teselas de OpenStreetMap y el backend (fase 16) |

## Requisitos no funcionales externos (9)

| Requisito | Descripción | Fase | Estado | Evidencia |
|---|---|---|---|---|
| RNFE01 | cumplir la Ley N.º 19.628 (modificada por la Ley N.º 21.719): informar el uso de los datos en la Política de… | 5,6,11 | En curso | Fase 5: borrador de la política de privacidad, con los datos que se recogen, para qué sirven y quién los recibe (`src/paginas/publicas/legales/textos.js`), y casilla de privacidad sin marcar con la versión registrada. Hoy no hay rastreadores que bloquear. Faltan la revisión legal y los derechos de consulta, corrección, descarga y eliminación (fases 6 y 11) |
| RNFE02 | integrarse con el inicio de sesión de Google mediante OAuth 2.0. | 14 | Pendiente |  |
| RNFE03 | comunicarse solo por HTTPS, con certificado TLS vigente. | 16 (externo) | Pendiente |  |
| RNFE04 | aplicar las recomendaciones del OWASP Top 10:2025 y OWASP API Security Top 10. | cada fase y 17 | Pendiente |  |
| RNFE05 | integrarse con un servicio de correo saliente con remitente autenticado. | 4,11 | En curso | Fase 4: SMTP con MailKit y remitente autenticado (Gmail, contraseña de aplicación), probado contra un servidor SMTP de pruebas. El envío real a Gmail se comprueba con `probar-correo` cuando el usuario escriba la contraseña de aplicación (ver informe de la fase 4) |
| RNFE06 | tratar el rostro como dato personal sensible: consentimiento expreso, guardar solo el descriptor y permitir… | 10 | Pendiente |  |
| RNFE07 | integrarse mediante HTTPS desde el backend con una API externa de inteligencia artificial generativa (como… | 14 | Pendiente |  |
| RNFE08 | cumplir con las pautas de accesibilidad de contenido web WCAG 2.2 en su nivel de conformidad AA del W3C. | 2,15 | En curso | Igual que RNFP15 |
| RNFE09 | cumplir con los términos de licencia de los componentes open source, activos gráficos y tipografías… | 5,16 | En curso | Fase 5: Leaflet (BSD-2-Clause) y OpenStreetMap, con su atribución visible junto al mapa, documentados en `docs/licencias.md`. Falta comprobar las condiciones de uso de las teselas de OpenStreetMap antes de la entrega |

## Entradas (27)

| Requisito | Descripción | Fase | Estado | Evidencia |
|---|---|---|---|---|
| E01 | Datos del cliente: RUT (opcional en solicitud rápida), nombre, apellido, teléfono, correo, dirección y, si… | 6 | Pendiente |  |
| E02 | Credenciales: correo y contraseña, o autorización de Google. Cliente, trabajador. | 6 | Pendiente |  |
| E03 **CAMBIA** | Solicitud: cliente y descripción general del trabajo. Cliente con correo verificado o trabajador. | 7 | Pendiente |  |
| E04 **CAMBIA** | Ítems de la solicitud: tipo de letrero, descripción, ancho, alto y cantidad. Cliente o trabajador. | 7 | Pendiente |  |
| E05 | Cambio en una solicitud: datos corregidos, motivo de rechazo o marca de eliminación. Trabajador. | 7 | Pendiente |  |
| E06 | Cotización: observaciones; por material: material, medida, cantidad y costo unitario; mano de obra; otros… | 8 | Pendiente |  |
| E07 | Respuesta a la cotización: aprobación o rechazo. Cliente registrado o trabajador. | 8 | Pendiente |  |
| E08 **CAMBIA** | Creación del pedido desde una cotización aprobada: responsable, fecha estimada, modalidad de entrega, si… | 8 | Pendiente |  |
| E09 **CAMBIA** | Avance del pedido: nuevo estado del pedido y, al entregar, fecha real. Trabajador. | 8 | Pendiente |  |
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
| E21 **CAMBIA** | Filtros del dashboard: rango de fechas, categoría de producto, estado del pedido, estado de pago u origen de… | 12 | Pendiente |  |
| E22 | Formulario de contacto: nombre del remitente, correo electrónico, teléfono (opcional), asunto y mensaje de… | 5 | En curso | Fase 5: formulario de contacto con nombre, correo, teléfono opcional (se guarda como `+56` y 9 dígitos), asunto y mensaje; `POST /contacto` valida cada campo en español (`ValidacionContacto`, `PortadaTests`, `portada.test.jsx`, `e2e/pantallas.spec.js`) |
| E23 | Registro de consentimiento legal y privacidad: aceptación de Términos y Condiciones y Política de Privacidad… | 5,6 | En curso | Fase 5: casilla de privacidad sin marcar en el formulario de contacto, con la versión que se leyó registrada en `consentimientos` (referencia `contacto:{id}`), y preferencias de cookies en el aviso. Faltan los Términos y el marketing en el registro de cuenta (fase 6) |
| E24 **CAMBIA** | Solicitud sobre datos personales o baja de correo: petición de descarga de datos personales, solicitud de… | 11 | Pendiente |  |
| E25 **NUEVO** | Abono o pago del pedido: monto, fecha de pago, medio de pago (efectivo, transferencia, tarjeta de débito o de… | 8 | En curso | Tabla `pagos` con su trigger y restricciones (migración `HistorialDePagos`, pruebas `PedidoPagoTests`). Falta el formulario y la ruta de la API (fase 8) |
| E26 **NUEVO** | Pedido presencial directo: cliente (búsqueda por RUT o teléfono, o sus datos si es nuevo), tipo de letrero,… | 8 | Pendiente |  |
| E27 **NUEVO** | Filtros de pedidos: estado del pedido, estado de pago, cobranza (no pagados o abonados), año, mes y cliente.… | 3 (pantalla), 8 | En curso | Fase 3: barra de filtros de pedidos con resumen anunciado de cantidad y saldo por cobrar (`Pedidos.jsx`, `e2e/pantallas.spec.js`). Pasa a la API en la fase 8 |

## Salidas (25)

| Requisito | Descripción | Fase | Estado | Evidencia |
|---|---|---|---|---|
| S01 | Catálogo público y vitrina principal («Letreros Herrera»): título y datos de contacto de la empresa,… | 5 | En curso | Fase 5: vitrina pública con título, descripción, tipo de letrero, categoría, medidas, precio referencial y foto en WebP (`Portada.jsx`), botón a WhatsApp y mapa con la ubicación del taller (`MapaUbicacion.jsx`), y datos de contacto en el mostrador y el pie. Punto exacto, «Abrir en Google Maps» y vista de la fachada al activarla (cierre de la fase 5). Falta el número de WhatsApp del taller |
| S02 **CAMBIA** | Horario y cartel electrónico: horario de lunes a domingo, cartel verde «Abierto / Disponible» o rojo «Cerrado… | 3 (pantalla), 5 | En curso | Fase 3: horario de lunes a domingo con «Hoy» y cartel verde «Abierto / Disponible» o rojo «Cerrado / Ausente» con texto e icono, en la portada y el panel (`Portada.jsx`, `EstadoLocal.jsx`). Fase 5: «abierto ahora» con la hora de Chile y el cartel (`ReglaLocalAbierto`, `PortadaTests`): abierto solo dentro del horario y con el cartel en abierto; la hora de cierre ya cuenta como cerrado. Falta editar el horario y registrar quién cambió el cartel (fase 9). |
| S03 | Comprobante de solicitud: número, fecha y hora, nombre del cliente, ítems (producto, medidas, área, cantidad)… | 7 | Pendiente |  |
| S04 **CAMBIA** | Seguimiento del cliente en vista principal: por solicitud, número, fecha, estado y motivo de rechazo; total y… | 8 | Pendiente |  |
| S05 | Bandeja de solicitudes y vista principal de administración: resumen de solicitudes pendientes, métricas clave… | 7 | Pendiente |  |
| S06 | Cotización: número, fecha, cliente, ítems, detalle de materiales, costo total de materiales, mano de obra,… | 8 | Pendiente |  |
| S07 **CAMBIA** | Ficha del pedido: número, origen (cotización o directo), solicitud y cotización de origen, cliente,… | 8 | En curso | Columnas de pago en el esquema y en `GET /pedidos/{id}`; historial de abonos en la tabla `pagos`. Falta la ficha en la interfaz (fases 3 y 8) |
| S08 | Calendario de entregas: pedidos por día con cliente, modalidad y estado, con colores e íconos o etiquetas… | 8 | Pendiente |  |
| S09 | Notificación al cliente y correo de contacto al administrador: al cliente, número de solicitud o pedido,… | 5,11 | En curso | Fase 5: al correo del taller llegan los datos de cada consulta del formulario de contacto, con «Responder» dirigido a quien escribió (`AvisoConsultaContacto`, `PortadaApiTests.UnaConsulta_SeGuardaConSuConsentimiento_YAvisaAlTaller`). Falta el correo al cliente con su enlace de baja (fase 11) |
| S10 **CAMBIA** | Pedidos por mes: cantidad de pedidos creados por estado, sin contar los anulados en el total. Dueño. | 12 | Pendiente |  |
| S11 | Ingresos por mes: suma del monto final de los pedidos entregados y total del período. Dueño. | 12 | Pendiente |  |
| S12 | Solicitudes en línea: cantidad de online y, para comparar, de presenciales. Dueño. | 12 | Pendiente |  |
| S13 **CAMBIA** | Solicitudes en línea concretadas: online con un pedido no anulado. Dueño. | 12 | Pendiente |  |
| S14 | Conversión en línea: porcentaje S13 sobre S12 y, al comparar dos períodos, la diferencia en puntos… | 12 | Pendiente |  |
| S15 | Resultado del ingreso facial: nombre y rol del empleado reconocido, o aviso de que no hubo coincidencia con… | 10 | Pendiente |  |
| S16 | Registro de rostros: por empleado, nombre, rol, fecha de registro, fecha del consentimiento y estado (activo… | 10 | Pendiente |  |
| S17 | Sugerencia del asistente de IA: respuesta estructurada con categoría sugerida, producto base,… | 14 | Pendiente |  |
| S18 | Reporte operativo, gerencial y parametrizable: documento descargable en Excel (`.xlsx` con encabezados,… | 13 | Pendiente |  |
| S19 | Dashboard e indicadores (KPIs): al menos cuatro gráficos interactivos actualizados con su título, descripción… | 12 | Pendiente |  |
| S20 | Bitácora de auditoría y consentimientos: historial de creación y modificación en entidades principales… | 11 | Pendiente |  |
| S21 | Respuesta de error estándar: estructura uniforme de error de la API con código de estado HTTP, mensaje… | 1 | Cumplido | Fase 1: `Api/ErrorApi.cs`; pruebas `CimientosTests` (401, 403, 404, 400, 409, 429 y 500) |
| S22 | Documentos legales y banner de cookies: vista de Términos y Condiciones, Política de Privacidad, inventario… | 5,16 | En curso | Fase 5: páginas `/privacidad` y `/terminos` con borradores en versión 1.0, aviso de cookies con sus preferencias y pie con enlaces (`DocumentoLegal.jsx`, `BannerCookies.jsx`, `e2e/pantallas.spec.js`). Faltan el inventario de licencias en el sitio (hoy está en `docs/licencias.md`) y la revisión legal de los textos |
| S23 **CAMBIA** | Archivo de portabilidad y comprobante de baja: archivo descargable (JSON o CSV) con todos los datos… | 11 | Pendiente |  |
| S24 **NUEVO** | Listado de pedidos por cobrar: pedidos no anulados con pago «No pagado» o «Abonado», con cliente, estado del… | 3 (pantalla), 8, 13 | En curso | Fase 3: «Por cobrar» deja los pedidos no anulados con pago «No pagado» o «Abonado» y suma el saldo (`reglasPedidos.js`, `pedidos.test.js`, `e2e/pantallas.spec.js`). Completa en las fases 8 y 13 |
| S25 **NUEVO** | Aviso de contacto directo: mensaje «Para esta solicitud es preferible contactarse directamente con el local»,… | 3 (pantalla), 7 | En curso | Fase 3: el modal muestra «Para esta solicitud es preferible contactarse directamente con el local» al enviar (`ModalSolicitud.jsx`, `e2e/pantallas.spec.js`). La regla que decide cuándo se muestra llega en la fase 7 |

## Equivalencia de los códigos antiguos (versión 1 del documento, 47 RFS)

Las pruebas, el código y los informes de las fases 0 y 1 citan los códigos antiguos. Esta tabla indica dónde quedó cada uno.

| Código antiguo | Qué pedía | Ahora en | Observación |
|---|---|---|---|
| RFS01 | mostrar las publicaciones visibles del catálogo o vitrina de trabajos realizados (imagen,… | RFS01 |  |
| RFS02 | mostrar el horario semanal y el estado actual del local. | RFS02 |  |
| RFS03 | generar un código QR que abra el portal público. | RFS02 |  |
| RFS04 | crear cuentas de cliente validando que teléfono, RUT y correo no estén registrados,… | RFS03 |  |
| RFS05 | autenticar clientes (correo y contraseña, o Google) y trabajadores (correo y contraseña),… | RFS03 |  |
| RFS06 | registrar cada solicitud con estado «solicitado», fecha automática y origen «online»… | RFS04 |  |
| RFS07 | registrar uno o más ítems por solicitud (producto, descripción, cantidad, medidas) y… | RFS04 |  |
| RFS08 | buscar al cliente por teléfono o RUT antes de crear uno nuevo, para no duplicarlo. | RFS04 |  |
| RFS09 | listar solicitudes por estado, origen y fecha, y permitir modificarlas solo en… | RFS04 |  |
| RFS11 | registrar el rechazo de una solicitud con su motivo y conservarla para las estadísticas. | RFS04 |  |
| RFS12 | crear cotizaciones asociadas a una solicitud (materiales, mano de obra, otros costos),… | RFS05 |  |
| RFS13 | registrar la aprobación o el rechazo de una cotización, hecho por el cliente en línea o… | RFS05 |  |
| RFS14 | al confirmar el pago de una cotización aprobada, pasar la solicitud a «aceptado» y crear… | RFS06 | Cambia: el pedido ya no exige un pago previo; el pago es un estado aparte |
| RFS15 | registrar en el pedido responsable, fechas estimada y real de entrega, modalidad de… | RFS06 |  |
| RFS16 | cambiar el estado del pedido entre en proceso, aceptado, terminado, entregado, cancelado. | RFS06 | Cambia: «cancelado» pasa a «anulado» y el estado de pago se separa del estado del pedido |
| RFS17 | mostrar un calendario de pedidos por fecha estimada de entrega, distinguiendo pendientes… | RFS06 |  |
| RFS18 | enviar un correo al cliente cuando su solicitud sea cotizada, rechazada o aceptada, y… | RFS08 |  |
| RFS19 | mostrar a cada cliente registrado solo sus propias solicitudes, cotizaciones y pedidos. | RFS04 |  |
| RFS20 | permitir mantener categorías, productos, publicaciones del catálogo, materiales y… | RFS07 |  |
| RFS21 | permitir actualizar el horario de cada día y el estado del local, registrando quién y… | RFS02 |  |
| RFS22 | calcular pedidos por mes, ingresos por mes, solicitudes en línea, solicitudes en línea… | RFS09 |  |
| RFS23 | permitir cada función solo a los roles autorizados. | RNFP31 | Pasa a no funcional |
| RFS24 | registrar el rostro de un empleado activo a partir de varias capturas, guardando solo los… | RFS03 |  |
| RFS25 | identificar al empleado comparando el descriptor con los registrados, aceptando solo una… | RFS03 | Sigue como desviación: email primero |
| RFS26 | ante una coincidencia, emitir el mismo token que el ingreso con contraseña y mostrar si… | RFS03 |  |
| RFS27 | ofrecer el ingreso facial solo en el acceso al panel interno, mantener siempre el ingreso… | RFS03 |  |
| RFS28 | método de recuperación de contraseña. | RFS03 |  |
| RFS29 | validar las transiciones de estado en solicitudes, cotizaciones y pedidos para impedir… | RFS04, RFS05, RFS06 |  |
| RFS30 | registrar automáticamente en una bitácora de auditoría quién creó, modificó o eliminó… | RFS12 |  |
| RFS31 | validar todos los datos de entrada en tres niveles (formularios del front end, endpoints… | RNFP30 | Pasa a no funcional |
| RFS32 | permitir buscar, filtrar, ordenar y paginar los listados principales del sistema… | RNFP32 (y los filtros de pedidos de RFS06) | Pasa a no funcional |
| RFS33 | calcular al menos seis indicadores clave (sumando a pedidos por mes, ingresos por mes,… | RFS09 |  |
| RFS34 | alimentar un dashboard interactivo desde el backend con al menos cuatro visualizaciones… | RFS09 |  |
| RFS35 | generar al menos tres reportes del negocio: uno operativo (detalle de pedidos, entregas y… | RFS10 |  |
| RFS36 | exportar cada uno de los reportes aplicando los filtros seleccionados a formatos Excel… | RFS10 |  |
| RFS37 | proveer una versión limpia de impresión desde el navegador para cada reporte, ocultando… | RFS10 |  |
| RFS38 | ofrecer un endpoint en la API que consuma un servicio de inteligencia artificial externo… | RFS11 |  |
| RFS39 | presentar como primera pantalla al ingresar al sistema la vista principal de «Letrería… | RFS01 |  |
| RFS40 | enviar automáticamente un correo electrónico al administrador con los datos ingresados… | RFS08 |  |
| RFS41 | habilitar en la vista principal, cuando un cliente inicie sesión, un apartado directo de… | RFS03 (destino al ingresar), RFS04 (seguimiento del cliente), RFS09 (vista de administración) |  |
| RFS42 | publicar de forma accesible en todo el sitio la Política de Privacidad y los Términos y… | RFS12 |  |
| RFS43 | exigir en el registro de cuenta y envío de formularios una casilla sin marcar por defecto… | RFS12 (y los formularios de RFS01 y RFS03) |  |
| RFS44 | registrar en la base de datos quién aceptó los Términos y Condiciones, la Política de… | RFS12 |  |
| RFS45 | mostrar un banner de consentimiento de cookies al ingresar al sitio y bloquear la… | RFS12 |  |
| RFS46 | permitir a cada cliente registrado descargar en un archivo estructurado todos sus datos… | RFS12 | Cambia: la baja inhabilita la cuenta (borrado lógico) en vez de borrarla |
| RFS47 | verificar y registrar que toda publicación en la vitrina o catálogo que contenga… | RFS01, RFS07 |  |
| RFS48 | procesar el enlace de baja (unsubscribe) incluido en los correos electrónicos para… | RFS08 |  |
