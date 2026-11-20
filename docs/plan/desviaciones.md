# Desviaciones y pendientes externos

Registro de todo requisito que **no** se cumple al pie de la letra, o que no se puede verificar en el equipo de desarrollo. Se actualiza al cerrar cada fase. Fuente de los requisitos: `docs/requisitos/requerimientos_del_sistema.md` (versión 2, 12-11-2026). Los códigos antiguos (versión 1) se indican entre paréntesis; su equivalencia completa está en `trazabilidad.md`.

## Desviaciones decididas

| Requisito | Qué pide | Qué se hace | Quién lo decidió | Fecha |
|---|---|---|---|---|
| RFS03 (antes RFS25), RFU22 | Identificar al empleado solo con la cámara, comparando contra todos los rostros registrados | Se pide primero el email y se compara solo contra el rostro de esa persona | El usuario, por seguridad | 12-11-2026 |
| RFS03 (antes RFS24, RFS27), RFU21, RFU22, E17 | Ingreso facial para los empleados | Solo `dueño` y `administrador`. Diseñador y operario entran con contraseña | El usuario | 12-11-2026 |
| RNFO12 | Al menos 500 registros sintéticos y 12 meses de historia | Datos mínimos de prueba en una base aparte, `LetreriaLosHerreraDB_Demo` | El usuario | 12-11-2026 |
| Reglas de diseño (bordes) | Bordes sutiles de 1 px translúcidos (`border-slate-200/80`, `border-black/5`) | Se usan en tarjetas y separadores. Los **campos de formulario** y el botón secundario llevan un borde más oscuro (`#828DA0`), porque con el gris claro el campo no alcanza el contraste mínimo de 3:1 que exige WCAG 2.2 (1.4.11) | Claude, fase 2 (por WCAG, que el documento también exige) | 12-11-2026 |
| Reglas de diseño (barra) | Barra superior flotante de cristal | Flotante desde 768 px de ancho. En el teléfono y con poca altura (zoom), la barra es más alta y deja de ser fija, para no tapar el elemento con foco (WCAG 2.4.11) | Claude, fase 2, por la revisión de accesibilidad de ECC | 12-11-2026 |
| RFS02 (cartel) | El dueño o un trabajador da vuelta el cartel | **Resuelto en la fase 9** (decisión 1 del usuario, 14-11-2026): todo el personal (dueño, administrador, diseñador y operario) da vuelta el cartel, como piden RFU16 y RNFO03. Antes solo lo podían dueño y administrador | Usuario, decisión 1 del plan de la fase 9 | 14-11-2026 |
| Logotipo | Logotipo del taller (versiones gris y roja) | Se usa la versión de anillo y letras en tinta `#0F172A`, derivada del original sin redibujarlo, porque combina con el modo claro y no compite con el ámbar de las acciones. Originales en `docs/marca/originales/` | Claude, a pedido del usuario («el logo que mejor le quede») | 13-11-2026 |
| RFS01 (fotos de la vitrina) | Imagen en alta definición de cada trabajo | La semilla demo apuntaba a fotos que no existen. Desde el cierre de la fase 5 usa 22 fotos reales entregadas por el taller (ver «Cambios y pendientes de la fase 5») | Claude | 13-11-2026 |
| Reglas de diseño (foco) | Acento solo en llamadas a la acción y estados activos | El anillo de foco también es ámbar: el foco es un estado activo y así se ve sobre los tres fondos (≥4,5:1) | Claude, fase 2 | 12-11-2026 |
| RFU23, E19, S17, RNFE07 | Asistente con IA generativa (consulta en lenguaje natural y sugerencia estructurada) e integración HTTPS con un proveedor de IA | **No se construye.** Se elimina el asistente de IA; en la fase 14 queda solo el ingreso con Google | El usuario | 20-11-2026 |
| RNFO13 (parte de IA) | Pruebas del asistente de IA simulado | Sin asistente no hay pruebas de IA; el resto de RNFO13 (unitarias, integración y e2e) se mantiene | El usuario | 20-11-2026 |
| RNFP05 | Funcionar en las dos últimas versiones de Chrome, Edge, Firefox y Safari | Solo **Chrome, Edge y Brave** (motor Chromium). Firefox y Safari no se prueban ni se aseguran | El usuario | 20-11-2026 |
| RNFO14, RNFO15 (despliegue) | Contenedores y pipeline como único camino de ejecución | Se mantiene `docker compose up` local completo y **además** se despliega en Vercel (frontend), Render (backend con Docker) y Azure SQL Database, capa gratuita. La base gratuita se pausa sola con la inactividad | El usuario | 20-11-2026 |
| Reglas de diseño (acento) | Acento ámbar `#B45309` para la acción principal, el estado activo y el foco | Pasa al naranja `#C2410C` (≈5,2:1 con letras blancas, WCAG 2.2 AA). Falta confirmar al aprobar la 12C si reemplaza también al foco. `DESIGN.md` se actualiza en la 12C | El usuario (color) y Claude (alcance) | 20-11-2026 |
| Reglas de diseño (fondo) | Fondo liso | Imagen `fondo-web.jpg` con opacidad baja en el portal y la cuenta del cliente; no en el panel del taller. Fase 12C | El usuario | 20-11-2026 |
| Nombres y apellidos | Hasta 80 caracteres sin revisar los símbolos | Hasta 20 caracteres, solo letras (con tildes y ñ), sin espacios al inicio ni al final y hasta 5 espacios entre palabras. Solo se valida al crear y editar; no se agrega restricción en la base de datos, para no romper registros existentes. Rechaza «Pérez-Gil» y «O'Higgins». Fase 12D | El usuario | 20-11-2026 |

## Cambios de la revisión con la profesora guía (12-11-2026)

No son desviaciones: son cambios de los requisitos, ya aplicados al documento.

| Tema | Cambio | Dónde |
|---|---|---|
| Estructura | Los 47 RFS se agrupan en 12 macro-requerimientos, uno por pantalla o proceso. Las reglas técnicas pasan a RNFP30 a RNFP33 | Documento de requisitos; equivalencia en `trazabilidad.md` |
| Pedido y pago | Estado del pedido (operativo) y estado de pago (financiero) independientes, con monto abonado, saldo pendiente y número de factura. «Cancelado» pasa a «Anulado». El pedido ya no exige un pago previo. Ciclo cerrado = «Entregado» y «Pagado total» | RFS06, RNFO02; migración `SepararEstadoPedidoYPago` |
| Pedido presencial directo | Se puede registrar un pedido acordado en el taller sin solicitud web previa | RFS06, RFU35, E26 |
| Filtros de pedidos | Por estado del pedido, estado de pago, cobranza, año, mes y cliente | RFS06, RFU36, E27, S24 |
| Solicitudes falsas | El envío en línea exige correo verificado o Google; al superar un límite no se guarda nada y se sugiere contactar al local | RFS04, RNFP33, S25 |
| Baja de cuenta | La cuenta del cliente se inhabilita (borrado lógico) en vez de borrarse, para conservar el historial contable | RFS12, RFU34, E24, S23 |
| Revisión del lunes | Las pantallas de menú, vitrina con modal de solicitud, cartel electrónico con QR, pedidos con filtros y reportes/KPI se adelantan a la fase 3, navegables con datos de la base demo | Plan, fase 3 |

## Cambios pedidos por el usuario (13-11-2026, fase 4)

No son desviaciones: el usuario los agregó a los requisitos.

| Tema | Cambio | Dónde |
|---|---|---|
| Objetivo principal | El dueño recibe en su Gmail un aviso por cada solicitud nueva, presencial o en línea. RFS08 solo pedía correos al administrador por el formulario de contacto y al cliente por los cambios de estado | Fase 4: `AvisosDelTaller`, `AvisoSolicitudNueva` |
| Cuenta SMTP | Se necesita en la fase 4, no en la 11. Gmail por SMTP (587, STARTTLS) con contraseña de aplicación en User Secrets | Plan, fases 4 y 11 |
| Destinatarios | Uno o más correos en `Correo:AvisosTaller`, leídos solo por `IDestinatariosAvisos`. Desde la fase 9 el servicio lee primero los destinos activos que el dueño o el administrador guardan desde su perfil (tabla `destinatarios_avisos`) | Fase 4 y fase 9 |

Decisiones del usuario al revisar la fase 4 (13-11-2026):
- **Sin aviso para las solicitudes presenciales:** las registra el personal en el taller y serían ruido. El correo queda para las solicitudes en línea y el formulario de contacto (fase 5).
- **Cuentas de correo:** `letrerosherrera.avisos@gmail.com` solo envía y recibe los avisos automáticos; `contacto.letrerosherrera@gmail.com` es el correo público del taller. WhatsApp de prueba: el número del desarrollador, que se reemplaza antes de la entrega.
- **Base real:** no se le aplican migraciones hasta terminar todas las fases; se trabaja sobre la demo.
- **Tope de 20 avisos por hora** (se mantiene) (`Correo:TopeAvisosPorHora`). Pasado el tope, llega un solo aviso de «muchas solicitudes» y las demás quedan en el panel. Protege el cupo diario de Gmail (unos 500 envíos) ante un envío masivo de solicitudes falsas, que dejaría al dueño sin los avisos reales.
- **El aviso no es atómico con la solicitud:** se encola justo después de guardarla. Si ese paso fallara, la solicitud queda guardada, el error se registra y `/salud/correo` pasa a «degradado» por un día. El patrón *outbox* (todo en una transacción) se evaluará en la fase 7, cuando se rehaga el flujo de solicitudes.

## Decisiones que tomé al aplicar la revisión

**El usuario aprobó todas el 12-11-2026** y pidió integrar las mejoras propuestas. Por eso el historial de abonos ya es parte del modelo (migración `HistorialDePagos`).

| Tema | Lo que pedía la revisión | Lo que hice y por qué |
|---|---|---|
| Pedido directo sin solicitud | Que `SolicitudId` y `CotizacionId` del pedido sean opcionales **o** se generen internamente | **Se generan internamente** en la misma transacción: una solicitud presencial con sus ítems y una cotización aprobada. Así el tipo de letrero, el material base, las medidas y los costos quedan en las mismas tablas que los demás pedidos, y no se duplican columnas en `pedidos`. La columna nueva `pedidos.origen` (`cotizacion` o `directo`) los distingue |
| Estado de pago | Atributo `EstadoPago` | Es una **columna calculada** a partir del monto abonado y del monto final, en vez de un valor que se escribe a mano. Nunca puede contradecir a los montos (por ejemplo, «Pagado total» con saldo pendiente) |
| Montos | `MontoAbonado` y `SaldoPendiente` enteros | Las columnas nuevas son `int`. Las existentes siguen en `decimal(12,2)` con valores enteros, como decidió el plan (no se cambia el tipo de las columnas existentes); la API y la interfaz exigen pesos sin decimales |
| Fecha de cada abono | E25 pedía monto, fecha y factura | **Tabla `pagos`** (migración `HistorialDePagos`): cada abono guarda su monto, fecha, medio de pago, observación y quién lo registró. Un trigger mantiene el monto abonado del pedido igual a la suma de sus abonos, y la base rechaza un abono que haría superar el monto final. El número de factura sigue en el pedido |
| Filtro de cobranza | Pedidos «Aceptados, Terminados o Entregados» con pago «No pagado» o «Abonado» | Incluye también los «En proceso»: cualquier pedido **no anulado** con saldo pendiente. Un pedido en fabricación con saldo también se cobra. Si la profesora quiso excluirlos a propósito, es un cambio de una línea |
| Límite contra solicitudes falsas | No insertar si la cuenta, el RUT o la IP tiene solicitudes pendientes o supera el límite | Valores propuestos: **una** solicitud en línea pendiente por cuenta o RUT, y **3 envíos por hora** por IP, configurables. El mensaje es el mismo para todos los motivos, para no revelar si un RUT ajeno tiene solicitudes |
| Solicitud de un visitante sin cuenta | RFU06 decía «Visitante» | Con la regla nueva, el visitante debe crear una cuenta con correo verificado o entrar con Google para enviar en línea. Sin cuenta, la vitrina le ofrece WhatsApp, teléfono y el formulario de contacto |
| Baja de cuenta | Inhabilitar en vez de borrar | Además de inhabilitar, se borran los datos que no hacen falta para conservar el historial (por ejemplo, la contraseña). **Revisar con el asesor legal**: la Ley 21.719 da derecho a la supresión, y conservar los datos se justifica solo por obligaciones como la contable |

## Cambios y pendientes de la fase 5 (13-11-2026)

No son desviaciones de un requisito: son partes que la fase 5 deja pendientes porque dependen de datos del taller, o decisiones que se tomaron al construirla.

| Requisito | Qué pide | Qué se hace | Quién lo decidió | Fecha |
|---|---|---|---|---|
| RFU30, S01 | Mapa interactivo con la ubicación exacta de Villagrán 810 | La portada muestra una imagen estática del mapa con el punto exacto entregado por el taller (-37.46377531272541, -72.34832848822964). El mapa interactivo (Leaflet con OpenStreetMap) se carga solo al pulsar «Mapa interactivo», y la vista de la fachada (Google Street View, inserción entregada por el taller) solo al pulsar «Ver la fachada». «Abrir en Google Maps» lleva al enlace del taller. La CSP de `index.html` permite las teselas de OpenStreetMap en `img-src` y `https://www.google.com` en `frame-src`; `connect-src` queda en `'self'` (y el origen de la API en producción) | Claude; ubicación confirmada por el taller | 13-11-2026 |
| RFU31, S01 | Botón directo al WhatsApp oficial de la empresa | El botón existe, pero el número es el de prueba del desarrollador. Se cambia por el del taller antes de la entrega (`src/configuracion/taller.js`) | El usuario (fase 4); pendiente del taller | 13-11-2026 |
| S01, RFS01 | Fotos de los trabajos | El taller entregó 68 fotos en `docs/imagenes-trabajos/`. Se eligieron 22, sin repetidas, recortadas al formato 4:3 de la tarjeta (las piezas altas van completas sobre un fondo desenfocado de la misma foto) en `docs/imagenes-trabajos/vitrina/`. Quedaron fuera las de menos de unos 700 px (se verían borrosas), las repetidas, las que muestran caras reconocibles, las con marcas de terceros (Pokémon, Magic) y las tres `Gemini_Generated_Image_*`, porque son imágenes generadas por IA y mostrarlas como trabajos reales engañaría al visitante. La semilla deja registrada la autorización de logo de cada cliente a nombre del dueño demo, como pidió el usuario. Sin medidas ni precio: no se inventan para trabajos reales | Claude, con autorización de logos del usuario | 13-11-2026 |
| E23, RFS12, RNFE01, S22 | Política de privacidad y términos vigentes | Versión 1.0 vigente (`src/paginas/publicas/legales/textos.js`), sin revisión legal por decisión del usuario. Quedan «[Pendiente de completar]» la razón social, el RUT y el abono | Pendiente del usuario y del asesor legal | 13-11-2026 |
| RFS12, RNFP21 | Aviso de cookies sin trampas en la elección | Dos botones de igual tamaño y la casilla de analítica desmarcada. Hoy no hay analítica: la elección se guarda y su constancia se registra en la API, pero no activa nada | Claude | 13-11-2026 |

Decisiones de Claude en la fase 5 (objetables):
- **Sublímite del formulario:** 5 avisos por hora para las consultas, dentro del tope de 20 que comparten con las solicitudes (`Correo:TopeConsultasPorHora`). Si un robot llena el formulario, las solicitudes conservan su cupo.
- **Límite por IP:** 5 envíos por hora en `POST /contacto` y 5 en `POST /consentimientos/cookies` (`RateLimiting:ContactoPorHora` y `CookiesPorHora`).
- **Consulta repetida:** la misma consulta (mismo correo y mismo mensaje) dentro de la última hora no se guarda ni se avisa de nuevo.
- **Campo trampa:** `sitioWeb`. Quien lo llena recibe la misma respuesta de éxito, pero no se guarda nada.
- **Logotipos:** cambiar la imagen de una publicación o marcarla con logo borra la autorización, y hay que registrarla otra vez. Volver a autorizar no cambia quién ni cuándo la dio.
- **Zona horaria:** la API no arranca si el servidor no tiene la zona de Chile (America/Santiago), en vez de responder con error en cada petición.
- **Cartel:** la fecha de última modificación se guarda en hora de Chile, no en la del servidor, que en un contenedor sería UTC.
- **Correos para clientes** (cierre de la fase 5): salen de la casilla automática `letrerosherrera.avisos@gmail.com`, su pie dice que no se responda ahí y «Responder» va a `contacto.letrerosherrera@gmail.com` (`CorreoParaCliente`, `Correo:ContactoTaller`). En los avisos internos al taller, «Responder» sigue yendo al cliente.
- **Vista de la fachada:** la inserción de Street View que entregó el taller se usa como segunda opción del mapa, «Ver la fachada», con la misma regla: solo carga a Google si el visitante la pulsa.
- **Nombre comercial:** «Letreros Herrera» en todo lo visible, los correos y la documentación. Las bases (`LetreriaLosHerreraDB`, `_Demo`), la solución, los proyectos y los espacios de nombres no cambian.

Pendientes para la fase de despliegue y operación (fase 16):

| Requisito | Qué falta | Por qué queda para después | Responsable |
|---|---|---|---|
| RFS08, RNFP33 | Encabezados reenviados (`X-Forwarded-For`) | Los límites por IP usan la IP directa. Detrás de un proxy hay que configurar `ForwardedHeaders` con los proxies de confianza (ya figura en el README del backend) | Fase 16 |
| RFS12, RNFE01 | Purga de `consultas_contacto` y de los consentimientos | Resuelto al cierre de la fase 5 con los plazos del usuario: `PurgaDatosPersonales`, una vez al día, borra las consultas a los 12 meses y los consentimientos de visitantes a los 24 meses de darlos o retirarlos (los de clientes y personal, 24 meses después de retirarlos). La elección del aviso de cookies vence en el navegador a los 12 meses | Hecho |
| RNFE04 | `frame-ancestors` (protección contra clickjacking) | La etiqueta `meta` de la CSP de `index.html` no admite `frame-ancestors`: la cabecera la debe enviar el servidor | Fase 16 |
| RNFE09 | Condiciones de uso de las teselas de OpenStreetMap | Comprobar la licencia de las teselas y la política de uso de la OSMF (ver `docs/licencias.md`) | Antes de la entrega |

## Cambios y pendientes de la fase 6 (13-11-2026)

No son desviaciones de un requisito: son decisiones que se tomaron al construir la cuenta y la sesión, y partes que quedan para fases posteriores.

| Requisito | Qué pide | Qué se hace | Quién lo decidió | Fecha |
|---|---|---|---|---|
| RNFP08 | Tokens de sesión que caduquen a los 15 minutos (configurable) | El token de acceso dura 15 minutos, pero la sesión continúa con una cookie de renovación que rota en cada uso: 12 horas para el personal (`Sesion:HorasPersonal`) y 7 días para el cliente (`Sesion:DiasCliente`), contados desde el ingreso. Renovar no la alarga | Claude, fase 6 (pendiente de confirmación del usuario) | 13-11-2026 |
| RFS03, RFU05, E02 | Ingreso del cliente también con Google | En la fase 6 solo hay correo y contraseña. Google se agrega en la fase 14, que exige el ID de cliente OAuth | Plan de construcción (fase 14) | 13-11-2026 |

Decisiones de Claude en la fase 6 (objetables):
- **RUT opcional en la cuenta.** Como en la solicitud rápida (E01). Si se escribe, se valida y debe ser único; el cliente puede quitarlo.
- **Teléfono o RUT ya existentes dan un solo mensaje.** Al registrarse, un teléfono o un RUT que ya están en el taller dan el mismo mensaje y no dicen cuál de los dos coincide, para no revelar datos ajenos. El correo sí se revela («Ya existe una cuenta con ese correo»). Es un punto abierto: ocultarlo exigiría confirmar el correo antes de crear la cuenta.
- **El token de acceso sigue válido hasta vencer tras revocar.** Al cerrar la sesión, cambiar la contraseña o desactivar la cuenta, el token ya emitido sirve hasta 15 minutos más. La renovación se corta de inmediato.
- **Marketing antes de confirmar el correo.** Un cliente puede aceptar el marketing en el registro sin haber confirmado su correo. Por eso, desde la fase 11, las campañas solo irán a correos confirmados (`fecha_verificacion_correo`).
- **Cambio de correo desde Mi cuenta.** No cierra las otras sesiones, pero exige la contraseña y avisa al correo anterior. Cuando el personal cambia el correo, sí se cierran las sesiones. Es un punto para revisar con el usuario.
- **Cookies `SameSite=Strict`.** La web y la API deben servirse en el mismo sitio (el mismo dominio registrable); si no, el navegador no envía la cookie de renovación. Se resuelve en la fase 16, con el despliegue.

Pendientes que quedan para las fases siguientes:

| Requisito | Qué falta | Por qué queda para después | Responsable |
|---|---|---|---|
| RFU05, E02, RFS03 | Ingreso con Google | Requiere el ID de cliente OAuth de Google, que el usuario debe entregar | Fase 14 y el usuario |
| Solicitudes en línea | Exigir correo confirmado para enviarlas | Son parte de las solicitudes de la fase 7; la decisión es del usuario | Fase 7 |
| Campañas de marketing | Enviar solo a correos confirmados y con marketing vigente | Las campañas se construyen en la fase 11 | Fase 11 |
| RNFE01, RFS12 | Descarga de datos (portabilidad), eliminación de la cuenta y auditoría de quién cambia credenciales o correos | Son la baja, la descarga y la auditoría de la fase 11 | Fase 11 |
| RNFP29 | Orígenes de producción, web y API en el mismo sitio, y `Sitio:UrlPublica` con la dirección pública | Dependen del dominio y del despliegue | Fase 16 |
| RNFP07 | Los límites por cuenta viven en memoria: con varias instancias de la API se multiplican | Requiere un almacén compartido si se escala | Fase 16 |

Pendiente antes de cerrar la fase 6 (encontrado al documentarla):

| Requisito | Hallazgo | Qué hace falta | Responsable |
|---|---|---|---|
| RNFP08, RFS03 | `frontend/synctimefront/.env.example` trae `VITE_API_URL=https://localhost:7112`, que es otro origen que la página (`localhost:3000`). `src/api/cliente.js` usa `credentials: 'same-origin'`, así que el navegador no envía la cookie de renovación: con esa configuración la sesión dura solo el token de 15 minutos. Con `VITE_API_URL` vacía, la API se llama por el proxy de Vite (`/api`) en el mismo origen | Corregir la configuración de desarrollo (o el modo de `fetch`) y comprobarlo en el navegador antes de cerrar la fase | Claude, en la siguiente tarea de código; el usuario lo revisa |

## Cambios y pendientes de la fase 7 (14-11-2026)

Recoge lo que se decidió en el plan y en la especificación de la fase 7 (aprobados el 14-11-2026), los cambios pedidos después de la especificación, y lo que queda para fases siguientes. Solo la primera tabla son cambios respecto de un requisito; el resto son decisiones de construcción y pendientes.

| Requisito | Qué pide | Qué se hace | Quién lo decidió | Fecha |
|---|---|---|---|---|
| RFU11 | El trabajador consulta, modifica, rechaza y elimina solicitudes | Todo el personal rechaza, devuelve a «solicitado» y elimina de forma lógica (`PATCH`). `DELETE /solicitudes/{id}` queda solo para gestión (borrado lógico, no físico) | Cambio posterior a la especificación | 14-11-2026 |
| Solicitudes en línea (pendiente de la fase 6) | Exigir correo confirmado para enviarlas | Resuelto: el envío en línea exige correo confirmado (`403` con `correo_sin_confirmar`). Cumple RFS03, que pide verificar el correo antes de aceptar solicitudes en línea | Plan de la fase 7, aprobado el 14-11-2026 | 14-11-2026 |

Decisiones de Claude en la fase 7 (objetables):

- **Outbox en la misma transacción.** El aviso al taller y la confirmación al cliente se escriben en la cola junto con la solicitud. Si la cola falla, no se guarda la solicitud. Es más estricto que la fase 4, que encolaba después de guardar: así, si la solicitud queda guardada, su aviso no se pierde. El envío real sigue fuera de la petición, así que un fallo del servidor de correo no impide guardar.
- **Adjuntos privados.** Los archivos no van a la carpeta pública de las fotos de la vitrina ni tienen ruta pública. Se sirven solo con sesión (el cliente dueño o el personal), con nombres aleatorios y con `Cache-Control: private, no-store`.
- **SVG y AI no se aceptan.** Pueden llevar código. El mensaje pide exportarlos a PDF o PNG. El formato se decide por el contenido, no por el nombre: un SVG renombrado a `.png` no pasa.
- **PDF guardado tal cual.** Un PDF no se reescribe: conserva sus metadatos (por ejemplo, el autor) y cualquier contenido activo. Se sirve solo como descarga (`Content-Disposition: attachment`) y no se revisa su contenido. Si se quiere un análisis (JavaScript o enlaces dentro del PDF), habría que agregarlo antes de la fase 16. Pendiente de decisión del usuario.
- **Huella de la IP.** La IP nunca se guarda en claro: se guarda su HMAC-SHA256 con una clave derivada de `Jwt:Key` con HKDF (etiqueta `synctime:huella-ip`), o con `Solicitudes:ClaveHuella` si existe. En IPv6 se usa el prefijo /64, porque un mismo hogar puede recibir una dirección distinta en cada envío.
- **ForwardedHeaders para la fase 16.** Detrás de un proxy, todas las solicitudes llegan con la IP del proxy, y la regla contra solicitudes falsas frenaría a todos los clientes a la vez (3 envíos en 24 horas). Antes de publicar hay que configurar `ForwardedHeaders` con la lista de proxies de confianza. Hasta entonces, la regla por IP no es fiable en el despliegue.
- **Presenciales sin aviso al taller.** Decisión de la fase 4, confirmada por el usuario el 14-11-2026. **Cambió en la fase 11:** cada destino de avisos puede elegir recibirlas (viene apagado). La confirmación al cliente sale solo si su correo está confirmado, porque el personal pudo escribirlo mal.
- **El RUT se revisa en todas las solicitudes pendientes,** también en las presenciales, para que un RUT ajeno no sirva para llenar la base desde el local. Las presenciales del personal no pasan por la regla contra solicitudes falsas.
- **Adjuntos: límites y correo.** 5 archivos por solicitud y 5 borradores por cliente; 10 subidas por minuto por usuario (`RateLimiting:Adjuntos`). Subir exige correo confirmado, para que una cuenta recién creada no use el disco. Un borrador que no se envía en 24 horas se borra con su archivo.
- **Pedido de la vitrina.** «Solicitar cotización» lleva al formulario del cliente (`/cuenta/solicitud`), no a un modal. El modal `ModalSolicitud.jsx` se retiró.

Pendientes que quedan para las fases siguientes:

| Requisito | Qué falta | Por qué queda para después | Responsable |
|---|---|---|---|
| RNFP02 | Comprobar que un cliente registra una solicitud desde el teléfono en 5 minutos o menos | Es una prueba con una persona real. El flujo ya está probado de punta a punta (`e2e/pantallas.spec.js`) | Usuario, que lo cronometra |
| RNFO04 | Programar `sp_depurar_solicitudes_no_concretadas` (12 meses) | Hoy lo ejecuta la base a mano; nada lo ejecuta solo | Fase 16 |
| RFS09, RFU19, S05 | Gráficos y métricas del inicio (pedidos, ingresos y conversión) | Dependen de los pedidos y las cotizaciones | Fases 8 y 12 |
| RFS12 | Bitácora de auditoría general: quién creó, modificó o eliminó cada entidad | Es la auditoría de la fase 11. La fase 7 solo registra el historial de las solicitudes | Fase 11 |
| RFU06 | Enviar la solicitud con cuenta de Google | Requiere el ID de cliente OAuth de Google | Fase 14 y usuario |
| RNFP10 | Control de concurrencia en la ficha de clientes | La tabla `clientes` no tiene versión; agregarla exige una migración | Decisión del usuario |
| Adjuntos | Revisión del contenido de los PDF | No se hace hoy (ver «PDF guardado tal cual») | Decisión del usuario |
| RNFP07 | Proxies y `ForwardedHeaders` para la IP de la regla contra solicitudes falsas y de los límites por IP | Depende del despliegue | Fase 16 |

## Cambios y pendientes de la fase 8 (14-11-2026)

Recoge lo que se decidió en el plan y en la especificación de la fase 8 (aprobados el 14-11-2026, con cuatro decisiones del usuario), las decisiones de construcción y lo que queda para fases siguientes. Solo la primera tabla son cambios respecto de un requisito; el resto son decisiones y pendientes.

Resuelta en esta fase: la fila de la fase 7 sobre «RFS05, RFU08: una sola pendiente por solicitud». Desde la migración `CotizacionesYPedidos`, la base lo garantiza con `uq_cotizaciones_una_pendiente`.

| Requisito | Qué pide | Qué se hace | Quién lo decidió | Fecha |
|---|---|---|---|---|
| RFU13, E25 | Registrar los abonos del cliente, con monto, fecha y medio de pago | Un abono mal ingresado no se edita ni se borra. Se anula con motivo, queda tachado en el pedido con quién y cuándo, y deja de sumar. Después se registra el correcto | Usuario (decisión 2 del plan) | 14-11-2026 |
| RFU13, E25 | Registrar los pagos del cliente | Anular un pedido que tuvo abonos deja lo abonado como «a devolver o a favor del cliente». La devolución no se registra en el sistema, y la API no tiene una ruta para hacerla | Usuario (decisión 4 del plan) | 14-11-2026 |
| RFU36, E27 | Filtrar los pedidos por mes | Con `mes` y sin `anio`, la API compara el mes de cualquier año y recorre los pedidos, porque no cabe en un rango de fechas. La pantalla siempre envía el año | Claude, fase 8 | 14-11-2026 |
| E06, E08, E25 | (El requisito no lo menciona) Que una respuesta perdida no duplique una cotización, un pedido o un abono | La API no tiene clave de idempotencia. Si se pierde la respuesta, la pantalla revisa antes de dejar reintentar: si el registro ya quedó, lo abre en vez de crear otro | Claude, fase 8 | 14-11-2026 |

Decisiones de Claude en la fase 8 (objetables):
- **Revertir la migración se detiene.** `Down()` no borra nada: si hay abonos anulados (el esquema anterior los volvería a sumar) o textos que no caben en sus columnas, se detiene con `THROW 50081` o `50082` antes de tocar la base. Hay que resolverlo a mano y con respaldo (prueba `RevertirCotizacionesYPedidos_SeDetieneAntesDePerderDatos`).
- **Los datos se completan antes de cada regla.** Las cotizaciones ya aprobadas o rechazadas reciben fecha y vía «presencial» (no se sabe cuándo ni quién respondió), los pedidos anulados reciben el motivo «Anulado antes de la fase 8» y la fecha del pedido, y cada pedido recibe una fila de creación en el historial. Si la base real tuviera algo que no cumple, la migración falla entera y no cambia nada.
- **Cambiar los montos de un pedido es de gestión.** El plan no lo pedía; `docs/api/permisos.md` lo fija. El cambio queda en el historial del pedido, sin cambio de estado.
- **Un pedido eliminado sigue ocupando su cotización.** `uq_pedidos_cotizacion` no filtra por `eliminado_logico`. La pantalla lo dice y no ofrece crear otro pedido.
- **Reactivar un abono responde `409 abono_anulado`.** Es el error 50080 del trigger, traducido en `ApiExceptionHandler`.
- **El correo «Tu cotización #N está lista» sale solo si el cliente tiene correo confirmado**, en la misma transacción que la cotización (outbox, como en la fase 7). El pedido directo no envía ningún correo.

Pendientes que quedan para las fases siguientes:

| Requisito | Qué falta | Por qué queda para después | Responsable |
|---|---|---|---|
| RFU36, S24 | Reporte de cobranza exportable (Excel, PDF o CSV) con el listado de pedidos por cobrar | Es parte de los reportes | Fase 13 |
| RFU09 | Avisos al cliente cuando cambia el estado de su solicitud o de su pedido, con enlace de baja | La fase 8 solo envía «Tu cotización #N está lista» (decisión 3 del plan) | Fase 11 |
| RFS12, RNFP10 | Bitácora general de quién creó, modificó o anuló cada registro | Los pedidos guardan su historial y los abonos anulados guardan quién y cuándo, pero no hay una bitácora común | Fase 11 |
| Base real | Aplicar las migraciones pendientes (de `SepararEstadoPedidoYPago` a `CotizacionesYPedidos`), con respaldo | La base real recibe todas juntas al terminar las fases (decisión del usuario, plan de la fase 8) | Usuario |

## Cambios y pendientes de la fase 9 (15-11-2026)

Recoge lo que se decidió en el plan y en la especificación de la fase 9 (aprobados el 14-11-2026, con cuatro decisiones del usuario y su pedido «Avisos del taller»), las decisiones de construcción y lo que queda pendiente. Solo la primera tabla son cambios respecto de un requisito; el resto son decisiones y pendientes.

Resueltas en esta fase: la fila «RFS02 (cartel)» de la tabla de desviaciones decididas, y la fila de la fase 7 sobre RNFP10 (control de concurrencia en la ficha de clientes): `clientes` tiene `row_version` desde la migración `MantenedoresYAvisos`.

| Requisito | Qué pide | Qué se hace | Quién lo decidió | Fecha |
|---|---|---|---|---|
| RFS08 | Cambiar desde el perfil el correo que recibe los avisos de solicitudes nuevas («Avisos del taller») | Un destino nuevo se confirma con un enlace de un solo uso que vence en 24 horas. Hasta confirmarlo, la dirección anterior sigue recibiendo avisos, y al confirmarlo recibe el aviso del cambio. Solo dueño y administrador lo administran | Pedido del usuario | 14-11-2026 |
| RFS02, RNFO03, RFU16 | Dar vuelta el cartel | Lo da vuelta todo el personal. El horario sigue siendo de gestión | Usuario, decisión 1 del plan | 14-11-2026 |
| RFU20 | Disponibilidad del personal | Franjas semanales que se repiten, sin excepciones por fecha. La portada muestra la unión de todas, sin nombres | Usuario, decisión 2 del plan | 14-11-2026 |
| RFS08 | Quitar un destino de avisos | No se puede quitar el último activo: para cambiarlo se reemplaza. `Correo:AvisosTaller` solo se usa si nunca se configuró ninguno en la base | Usuario, decisión 3 del plan | 14-11-2026 |
| RFS08 | Agregar un destino adicional | Los destinos activos reciben «Se agregó un destino a los avisos del taller» | Usuario, decisión 4 del plan | 14-11-2026 |

Decisiones de Claude en la fase 9 (objetables):
- **Reautenticación.** Piden la contraseña de quien lo hace: el alta de personal, el cambio de correo o de rol de otra persona, y agregar o quitar un destino de avisos. Reenviar o cancelar un cambio pendiente no la piden (`AgregarOQuitarUnDestino_PideLaContrasenaDeQuienLoHace`, `CambiarElCorreoOElRolDeOtraPersona_PideLaContrasena_YAvisaAlCorreoAnterior`).
- **Avisos a las direcciones anteriores.** Al cambiar el correo de una persona, la anterior recibe un aviso con la nueva enmascarada. Al reemplazar, quitar o agregar un destino, las direcciones afectadas también reciben aviso.
- **Dueño y administrador siguen siendo lo mismo** (confirmado por el usuario el 15-11-2026: se mantiene). Un administrador puede dar y quitar el rol de dueño. Solo lo frenan la reautenticación, el aviso a la persona afectada y la regla del último dueño. **Pregunta para el usuario:** ¿deben distinguirse sus permisos?
- **Nombres de tipos de letrero únicos en todo el taller**, no por categoría, porque la base tiene `uq_productos_nombre` sobre el nombre. El plan y la especificación decían «único por categoría».
- **Respaldo mientras la base real no tenga la tabla.** Si falta `destinatarios_avisos` (error 208), `IDestinatariosAvisos` usa `Correo:AvisosTaller` y registra el aviso una vez cada 5 minutos (`TablaDestinosAusente`). Se quita al aplicar la migración.
- **Vencimiento sin proceso automático.** Un pendiente vencido pasa a `cancelado` solo cuando se vuelve a pedir ese correo o un reemplazo de ese destino. Mientras tanto su enlace ya no sirve y no cuenta para el tope de pendientes.
- **Límites de los avisos.** Tres cambios pendientes a la vez como máximo, y un envío de enlace por minuto y cinco por hora por dirección.
- **Tokens de acceso.** Siguen válidos hasta 15 minutos después de desactivar a alguien, y la renovación se corta de inmediato (como en la fase 6).
- **Ficha de clientes sin guardar sobre la versión actual** (resuelto en la fase 11). Ante un conflicto de versión, la ficha dice que «tus cambios siguen aquí», pero pide recargar la página, lo que obliga a escribirlos otra vez. Los mantenedores y las cotizaciones sí permiten guardar sobre la versión actual (`useConflictoDeVersion`). Pendiente de unificar.
- **Sin selector de orden en los mantenedores.** La API ordena (`orden`), la pantalla no.
- **Horario y cartel sin versión** (resuelto en la fase 11: tienen `row_version` e `If-Match`). No tienen `row_version`: si dos personas los editan a la vez, gana la última escritura. La especificación no lo pedía.

Pendientes que quedan para después de la fase 9:

| Requisito | Qué falta | Por qué queda para después | Responsable |
|---|---|---|---|
| RNFO06 | Validar el RUT en la API de clientes: `POST /clientes` y `PUT /clientes/{id}` lo guardan sin validar (la pantalla sí valida) | No estaba en la especificación de la fase 9, que valida el RUT del personal | Claude, en la siguiente tarea de código, si el usuario lo confirma |
| RFS08, E22 | Bandeja para revisar las consultas del formulario de contacto (`consultas_contacto.revisada`) | El plan de la fase 9 no la incluye; el diccionario decía que llegaba aquí. Hoy nadie marca una consulta como revisada | Usuario: decidir en qué fase va |
| Base real | Aplicar las migraciones pendientes, incluida `MantenedoresYAvisos`, con respaldo | La base real recibe todas juntas al terminar las fases (decisión del usuario, plan de la fase 8) | Usuario |

## Cambios y pendientes de la fase 10 (15-11-2026)

Recoge lo que se decidió en el plan de la fase 10 (aprobado el 15-11-2026, con tres decisiones del usuario y su pedido de prueba de vida), las decisiones de construcción y lo que queda pendiente.

Resuelta en esta fase: la fila «E17: cinco capturas» de la tabla de desviaciones decididas. El registro guarda ahora exactamente tres muestras (frente, izquierda y derecha).

| Requisito | Qué pide | Qué se hace | Quién lo decidió | Fecha |
|---|---|---|---|---|
| RNFP34 (añadido), E18, RFS03 | Ingreso facial con una captura | Prueba de vida: tres capturas (frente y dos giros de unos 30 a 45 grados en un orden al azar que elige el servidor), validadas en el servidor | Pedido del usuario | 15-11-2026 |
| RNFP12 | Resolver el ingreso facial en 3 s o menos | Se mide como la respuesta del servidor desde que llegan las capturas. El flujo guiado completo dura de 6 a 9 s por la prueba de vida | Usuario, decisión 1 del plan | 15-11-2026 |
| RFS03 | Equipo autorizado para dueño y administrador | Se mantiene, además de la prueba de vida | Usuario, decisión 2 del plan | 15-11-2026 |
| RNFP34 | Calibrar el umbral con rostros reales | Pantalla «Calibrar el rostro» en el panel, apagada por configuración, que guarda solo distancias. Con la calibración del usuario (7 intentos propios: 0.24 a 0.31; 10 de otra persona: 0.62 a 0.70) el umbral queda en **0.45** | Usuario, decisión 3 del plan y elección del umbral | 15-11-2026 |

**Límite honesto de la prueba de vida.** Los descriptores y los 68 puntos se calculan en el navegador, así que **un cliente modificado podría falsificarlos**. La prueba de vida frena una foto o una pantalla sostenida frente a la cámara y sube mucho el costo de suplantar a alguien, **pero no es una protección infalible**. Por eso se mantienen la contraseña siempre visible, el equipo autorizado y el bloqueo tras 5 fallos.

Aprobadas por el usuario al cerrar la fase (15-11-2026), ya hechas:
- **Límite propio para el desafío** (`DesafioFacial`, 10 por minuto por IP). El ingreso conserva `LoginFacial`; uno no gasta el otro. Una IP puede hacer ahora hasta 10 ingresos con rostro por minuto, acotados por el bloqueo y el equipo autorizado.
- **El lado de cada muestra va en los datos asociados del cifrado** (`modelo:idEmpleado:lado`), con `formato_muestras = 3` (migración `RostroMuestrasPorLado`). Los rostros de formato 2 deben registrarse de nuevo; el del usuario en la demo ya se registró otra vez.

Decisiones de Claude en la fase 10 (objetables):
- **Errores por muestra con clave plana:** `errores["muestras.frente"]`, igual que otros errores anidados de la API.
- **El desafío responde 400 sin email** o con uno de más de 254 caracteres. Depende solo del formato, no de que la cuenta exista.
- **Una sola fecha para registro y consentimiento:** `fechaRegistro` sale de la fecha del consentimiento, porque no hay otra columna.
- **El consentimiento cambió de texto** (tres muestras, prueba de vida, solo gestión) y su versión pasó a `2026-11-15`.
- **Calibración menos precisa a propósito:** distancias y giros con 2 decimales y un máximo de 30 mediciones por empleado y hora, para que no sirva de oráculo. No debe encenderse en producción.
- **Umbrales de la guía en vivo** (no son de seguridad): brillo mínimo 50 de 255, rostro entre el 22 % y el 60 % del ancho, centrado a menos del 20 %.
- **El desafío se pide con la cámara ya encendida** y los modelos cargados, para que sus 30 segundos no se gasten esperando el permiso.
- **Las pruebas de integración fijan el umbral en 0.5**, porque sus vectores sintéticos se arman alrededor de ese valor; la configuración real usa 0.45.

Pendientes que quedan para después de la fase 10:

| Requisito | Qué falta | Por qué queda para después | Responsable |
|---|---|---|---|
| Despliegue | Configurar `ForwardedHeaders` detrás de un proxy inverso, para que el límite por IP sirva | Corresponde a la fase 16 | Claude, fase 16 |
| RNFP34 | Volver a calibrar con más personas, otra luz u otra cámara | La calibración del 15-11-2026 tiene 7 intentos propios y una sola persona ajena | Usuario |
| Base real | Aplicar las migraciones pendientes (ahora 10, incluidas `RostroConPruebaDeVida` y `RostroMuestrasPorLado`) con respaldo. Los rostros de la base real deberán registrarse de nuevo con las tres muestras | La base real recibe todas juntas al terminar las fases (decisión del usuario) | Usuario |

## Cambios y pendientes de la fase 11 (16-11-2026)

Recoge lo que se decidió en el plan de la fase 11 (aprobado el 15-11-2026, con cinco decisiones del usuario y su pedido sobre los avisos del taller), las decisiones de construcción y lo que queda pendiente.

Resueltas en esta fase:
- **Clave de idempotencia:** la fila de la fase 8 sobre la clave para cotizaciones, pedidos y abonos. Ahora `Idempotency-Key` cubre las seis rutas que crean.
- **Versión del horario y el cartel:** la fila de la fase 9. Ahora tienen `row_version` e `If-Match`.
- **Ficha de clientes:** la decisión de la fase 9 sobre la ficha sin «guardar sobre la versión actual». Ahora lo ofrece.

| Requisito | Qué pide | Qué se hace | Quién lo decidió | Fecha |
|---|---|---|---|---|
| RFS08 | Avisos al taller por cada solicitud | Son correos operativos: ninguna baja de un cliente los toca. Cada destino elige solicitudes en línea, presenciales o ambas. Los que fallan aparecen en «Correos no enviados» y se reenvían a los destinos de hoy | Pedido del usuario, decisiones 1 a 3 | 15-11-2026 |
| RFU09 | Darse de baja de correos no esenciales | Los avisos de estado de la solicitud y del pedido son opcionales. Los de cuenta y seguridad (verificación, recuperación, cambio de correo y comprobante de baja) se envían siempre | Usuario, decisión 4 | 15-11-2026 |
| E06, E08, E25 | (El requisito no lo menciona) Que un doble envío no duplique registros | `Idempotency-Key` en crear solicitudes, cotizaciones, pedidos y abonos | Pedido del usuario, decisión 5 | 15-11-2026 |

Decisiones de Claude en la fase 11 (objetables):
- **El enlace de baja es un token firmado (HMAC), no un valor guardado.** La especificación pedía guardar el hash de un token aleatorio, pero así no se podría volver a poner en cada correo. El enlace vence solo si el cliente cambia de correo o da de baja su cuenta.
- **La cola de correo ya no borra filas.** Antes se borraban a los 30 días. Ahora se vacían el asunto, los cuerpos y el enlace, y el destino queda enmascarado: a los 30 días los enviados y a los 90 los fallidos, para poder reenviarlos. La fila queda como constancia con el tipo, la referencia, el estado y las fechas.
  - El plan decía, por error mío, que la cola «nunca se purga». Sí se purgaba, borrando la fila entera.
- **La bitácora no tiene purga en esta fase.** Un trigger impide modificarla o borrarla. Sí se pueden ejecutar `TRUNCATE` o `DISABLE TRIGGER` con permisos de dueño de la base, así que el usuario de la API no debería serlo (fase 16).
- **La baja desde el enlace queda a nombre del cliente,** aunque el navegador tenga abierta la sesión de otra persona. Se encontró y corrigió al probar en la app.
- **Consultas de contacto y aviso del tope:** siguen la preferencia «en línea» de cada destino.
- **Reenviar un aviso de estado** a un cliente que ya no lo recibe responde 409 `cliente_no_recibe`.
- **Respuesta repetida por idempotencia:** devuelve el estado y el cuerpo, sin `Location` ni `ETag`.

Pendientes que quedan para después de la fase 11:

| Requisito | Qué falta | Por qué queda para después | Responsable |
|---|---|---|---|
| RFS12, S20 | Registrar quién exportó cada reporte | Las exportaciones son de la fase 13 | Claude, fase 13 |
| RNFE01 | ~~Plazo de borrado de la IP guardada en la bitácora~~ **Decidido el 16-11-2026: 90 días; purga construida en la 12B** | Resuelto | — |
| RFS12 | Usuario de la base sin permisos de dueño, para que nadie pueda desactivar el trigger de la bitácora | Corresponde al despliegue | Claude, fase 16 |
| RFU09 | El trabajador de la cola no vuelve a revisar la baja al enviar un correo opcional que ya estaba en la cola | Riesgo bajo: el aviso ya estaba encolado antes de la baja | Claude, si el usuario lo pide |
| RFU34 | Tras la baja, las demás rutas del cliente siguen aceptando su token hasta que vence (como máximo 15 minutos) | Igual que la desactivación de empleados (fase 9) | — |
| Base real | Aplicar las migraciones pendientes (ahora **13**, incluidas `CatalogoRealDelTaller` e `IndicadoresDashboard`) con respaldo. La `CatalogoRealDelTaller` borra categorías y productos: en la real no hay solicitudes ni vitrina que los usen (consultado el 17-11-2026) | La base real recibe todas juntas al terminar las fases (decisión del usuario) | Usuario |

## Decisiones y pendientes de la fase 12A (16-11-2026)

| Requisito | Decisión | Detalle |
|---|---|---|
| RNFE01 | **Retención de la IP en la bitácora de auditoría: 90 días** (decisión del usuario, 16-11-2026) | Pasados 90 días se borra **solo la IP** (`Ip = NULL`); el registro de auditoría queda intacto. Mismo criterio que los correos fallidos. Se anota en la política de privacidad (`textos.js`). La tarea de purga se construye en la 12B |
| Fase 12 | Se divide en 12A (contrato y frontend con datos de demostración) y 12B (backend, vistas SQL, índices, purga de IP y conexión real) | Decisión del usuario, 16-11-2026 |
| RNFO16, RNFO17 | Gráficos en SVG propio, sin librerías | Sin dependencias nuevas ni licencias que revisar |
| — | ~~Aviso visible «Datos de demostración» mientras `VITE_INDICADORES_DEMO` esté activo~~ | **Resuelto en la 12B (17-11-2026):** el adaptador, sus datos y la variable se eliminaron; la pantalla llama a la API real |
| — | Sin la variable `VITE_INDICADORES_DEMO`, la demo se enciende **solo en desarrollo y en las pruebas**; un build de producción usa la API real salvo `VITE_INDICADORES_DEMO=true` | Hallazgo de la revisión de ECC: así un build de producción nunca muestra cifras de ejemplo por olvido |
| ContratoTests | Las seis rutas de `/api/v1/indicadores` quedan en `openapi.json` bajo `x-planificadas`, fuera de `paths` | La prueba compara `paths` con la API real: sigue en verde (3 de 3) y la 12B mueve las rutas a `paths` al implementarlas |
| S24 | El saldo del dashboard parte en **«Saldo del período»** y tiene a un clic **«Toda la deuda vigente»** (`alcanceSaldo=vigente`), que ignora las fechas | Precisión del usuario del 16-11-2026, para no ocultar cobros antiguos. El inicio del dueño muestra siempre toda la deuda vigente |
| RFS09 | Las «siete tarjetas» del plan son siete indicadores más el saldo por cobrar, destacado aparte (ocho bloques de cifra) | El plan nombraba ocho cifras; el saldo va separado por ser la alerta |
| — | El filtro de estado del pedido no ofrece «Anulado» | Ningún indicador cuenta los anulados (fórmulas del plan general) |
| RNFE01 | ~~La línea de los 90 días se agregó a la política sin subir su versión~~ | **Resuelto en la 12B (autorizado el 16-11-2026):** la política de privacidad pasa a la versión **1.1** (`DocumentosLegales.cs` y `textos.js`, con la fecha y el cambio en «Cambios a esta política») |
| — | ~~Mientras dure la demo, el inicio del dueño mezcla cifras de ejemplo con las reales~~ | **Resuelto en la 12B:** todo el inicio usa datos reales |
| — | Dos pruebas antiguas fallan a veces al correr toda la suite (`cotizaciones.test.jsx` y `pedidosFase8.test.jsx`, una distinta en cada corrida) y pasan al repetirla | Son de tiempo bajo carga, no de esta fase; se revisan en la fase 15 |

## Cambios y pendientes de la fase 12B (17-11-2026)

| Requisito | Decisión o desviación | Detalle |
|---|---|---|
| Catálogo | **Catálogo real del taller** (pedido del usuario el 16-11-2026): se borra el catálogo de prueba y se rehace con 4 categorías y 16 productos | Migración `CatalogoRealDelTaller`. Probada hacia adelante y hacia atrás sobre una copia de la demo y aplicada en la demo; **no en la real** (le faltan ahora 13 migraciones) |
| Catálogo | Equivalencias de lo que usaba un producto de prueba (publicaciones de la vitrina e ítems de solicitudes de la demo) | Cartel metálico, Letrero luminoso y Letrero de fachada → Letreros luminosos; Lienzo y Lona publicitaria → Impresión en tela PVC / Gigantografías; Palomita y Caballete → Caballete publicitario / Paloma; Rotulación de vehículo → Rotulación vehicular y general; Estampado textil y Polera → Estampados; Pendón → Pendones; Señalética → Señalizaciones; Volantes y calendarios → Flyers / Folletería; Mural pintado, Vinilo para vitrina, Calcomanía, Decoración en vinilo y Autoadhesivo troquelado → Adhesivos y vinilos. Lo desconocido va a «Adhesivos y vinilos». Resultado en la demo: 29 publicaciones y 25 ítems conservados |
| Catálogo | Medidas con límites y opciones estructuradas por producto (decisión A, 16-11-2026) | La API valida las medidas y las opciones; elegir una opción es **opcional** (lo no elegido lo define la oficina al cotizar). Los datos anteriores a la 12B no se revalidan |
| Catálogo | **Pendiente:** editar las medidas y las opciones de un producto desde el panel de mantenedores | Hoy solo cambian con una migración (`CatalogoDelTaller.cs`). Los mantenedores de categorías y productos siguen igual para el nombre, la descripción y si se ofrece |
| S12 | Las **solicitudes** en línea frente a las presenciales (la 12A contaba pedidos por origen) | Decisión B, 16-11-2026 |
| RFS09 | El **ticket promedio** es ingresos ÷ pedidos entregados (la 12A promediaba todos los no anulados) | Decisión B |
| S10 | La tarjeta «Pedidos» trae el desglose por estado (aceptado, en proceso, terminado y entregado) | Decisión B |
| S19 | La **demanda por categoría** se mide sobre los ingresos: los pedidos **entregados** del período, con el monto de cada uno repartido entre sus ítems según su cantidad (la suma coincide con los ingresos). Admite agrupar por tipo de letrero | Decisión D. En la 12A se medía sobre todos los pedidos no anulados creados en el período; un pedido que aún no se entrega no entra a este ranking |
| Filtros | Se mantienen `estadoPago=por_cobrar`, `categoria` y `cliente` (no `soloPorCobrar`, `categoriaId`, `clienteId`) | Decisión C. Equivalencia anotada en `docs/api/contrato.md` |
| RNFE01 | **IP de la bitácora a 90 días.** El trigger `trg_auditoria_inmutable` se reescribió para admitir un único cambio: `ip = NULL` en filas de más de 90 días | La purga diaria (`PurgaDatosPersonales`) lo hace en lotes de 5000. La bitácora sigue sin borrarse nunca. Al introducir fallos a propósito, bajar el umbral del trigger a 30 días **no fue detectado** por las primeras pruebas: se agregó `ElTrigger_RechazaBorrarLaIpDeUnaFilaDeMenosDe90Dias_AunqueTengaMasDeUnMes` y desde entonces se detecta |
| RNFE01 | **Política de privacidad 1.1** (autorizada el 16-11-2026) | Los registros y consultas nuevos aceptan la 1.1. La 12B no agrega un flujo para pedir de nuevo la aceptación a quienes ya aceptaron la 1.0: sus consentimientos quedan registrados con la versión que leyeron |
| RNFO11 | Las pruebas del backend crean la base con las migraciones, así que el catálogo real ocupa los ids 1 a 16 (como la semilla anterior ocupaba 1 a 4) | Las pruebas existentes que usan `idProducto = 1` siguen valiendo: ahora es «Impresión en tela PVC / Gigantografías», con medidas amplias |
| RNFO11 | **Pendiente:** `docs/base-de-datos/LetreriaLosHerreraDB.sql` (el script completo de la base) sigue en el estado de la fase 11 | El diccionario de datos y las migraciones están al día. El script se regenera con el generador de la fase 11, que ya no está en la carpeta temporal; se rehace en la fase 17 |
| RNFP26 | Los índices nuevos no se pueden medir con los pocos datos de la demo | La medición con volumen real queda para la fase 15 |
| RFU27 | La lista de clientes del filtro «Cliente» trae como máximo los primeros 100 | **Pendiente (fase 15):** buscarlos en el servidor. Un cliente de la dirección que no está en la lista se muestra igual («Cliente N.º 9») y se puede quitar |
| Revisión ECC | **Pendiente (aceptado):** editar una solicitud antigua cuyo producto fue remapeado por `CatalogoRealDelTaller` puede fallar la validación de medidas aunque no se toquen (por ejemplo, un «Mural pintado» pasado a «Adhesivos y vinilos») | Solo afecta a solicitudes de la demo; la base real no tiene solicitudes. Se resolverá validando solo lo que cambia, cuando se editen solicitudes con el catálogo real |
| Revisión ECC | Un producto con rangos o medidas fijas acepta una solicitud **sin medidas** | La oficina cotiza y revisa; el formulario completa solo las medidas únicas. Exigirlas queda como mejora |
| Revisión ECC | Criterio de los filtros: `categoria` filtra los **pedidos** por «algún ítem de la categoría» y las **solicitudes** por la categoría del primer ítem; el embudo y la conversión no dependen de `estadoPedido` ni de `estadoPago` | Documentado en el contrato; la pantalla lo avisa en el embudo. Con `categoria`, la tarjeta de ingresos suma el pedido completo y la demanda solo la parte de esa categoría |
| Revisión ECC | La demanda excluye los pedidos que no tienen ítems (no aparecen en la vista de ítems), aunque sí cuentan en los ingresos | No ocurre con datos creados por la API (una solicitud exige al menos un ítem) |
| Revisión ECC | Las fechas de las vistas son «días de Chile» solo si SQL Server corre en hora de Chile (`fecha_pedido` es `datetime2(0)` sin zona) | **Requisito de despliegue (fase 16):** el servidor de base de datos en hora de Chile |
| Revisión ECC | Los índices filtrados exigen `QUOTED_IDENTIFIER ON` en la sesión que modifica `pedidos` o `auditoria`: `sqlcmd` y los scripts manuales deben usar `-I` | Para la guía de operación (fase 16) |
| Revisión ECC | `ix_pedidos_indicadores_fecha` es ancho y deja redundante a `ix_pedidos_fecha_estado` | Se mide con volumen real en la fase 15 antes de quitar alguno |
| Revisión ECC | La purga de la IP corre una vez al día: si la aplicación está detenida, la IP puede quedar algo más de 90 días | La política lo dice («la depuración corre una vez al día») |
| Revisión ECC | La migración `CatalogoRealDelTaller` se detiene con el error 50130 si hay productos con publicaciones o solicitudes que no están en la lista de equivalencias, en vez de remapearlos en silencio | Agregar el producto a la migración y repetir |
| Demostración | ~~La verificación en la app la hace el usuario al iniciar sesión~~ | **Hecha el 17-11-2026** con la sesión de dueño que abrió el usuario (ver `informes/fase-12.md`) |

## Restricciones del equipo de desarrollo

| Tema | Situación | Consecuencia |
|---|---|---|
| Docker | El equipo usa OptiOS 11 Lite, sin Hyper-V: Docker Desktop no se puede instalar | Las fases 1 a 15 se desarrollan y prueban de forma nativa. Los contenedores (RNFO14, RNFO15) se construyen en la fase 16 y se verifican en GitHub Codespaces y GitHub Actions |
| Base de datos | Instancia `localhost\SQLEXPRESS` (SQL Server 2025 Express) con autenticación de Windows. No se usa LocalDB | Las pruebas crean bases temporales en esa instancia |
| SDK de .NET | **Resuelto el 12-11-2026:** SDK 10.0.404 instalado y fijado con `global.json` en la raíz | Las pruebas pasan con el SDK 10 |

## Requisitos pendientes de verificación externa

Quedan construidos y documentados, pero su comprobación no depende del código ni de este equipo.

| Requisito | Qué falta comprobar | Responsable |
|---|---|---|
| RNFP04 | Disponibilidad mensual del 99 % | Depende del hosting que se contrate |
| RNFE03 | HTTPS con certificado TLS vigente | Depende del hosting y del dominio |
| RNFP09 | Respaldo diario guardado fuera del servidor principal | Depende del hosting |
| RNFO14, RNFO15 | `docker compose up` y pipeline de CI/CD en verde | Usuario, en GitHub Codespaces y GitHub Actions (fase 16) |
| RNFP02 | Que un cliente sin capacitación registre una solicitud en 5 minutos | Prueba con una persona real |
| RNFO13 | Prueba manual con teclado y lector de pantalla (NVDA) | Usuario, con el guion de la fase 15 |
| RNFP05 | Funcionamiento en Safari real | Prueba en un dispositivo Apple |
| RNFE01, RNFE06, RFS12 | Revisión legal de la política de privacidad, los términos, el consentimiento facial y la baja por inhabilitación | **No se hará** (decisión del usuario, 13-11-2026: no hay presupuesto). Los textos quedan como versión vigente |

## Limitaciones y mejoras futuras (para la Sección 11 del informe técnico)

| Mejora | Origen |
|---|---|
| **Módulo de facturación mensual automatizada para clientes habituales con pedidos entregados por cobrar.** Hoy el sistema registra el número de factura y lista los pedidos por cobrar, pero la factura se emite fuera del sistema | Sugerencia de la profesora guía, 12-11-2026 |
