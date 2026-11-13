# Desviaciones y pendientes externos

Registro de todo requisito que **no** se cumple al pie de la letra, o que no se puede verificar en el equipo de desarrollo. Se actualiza al cerrar cada fase. Fuente de los requisitos: `docs/requisitos/requerimientos_del_sistema.md` (versión 2, 12-11-2026). Los códigos antiguos (versión 1) se indican entre paréntesis; su equivalencia completa está en `trazabilidad.md`.

## Desviaciones decididas

| Requisito | Qué pide | Qué se hace | Quién lo decidió | Fecha |
|---|---|---|---|---|
| RFS03 (antes RFS25), RFU22 | Identificar al empleado solo con la cámara, comparando contra todos los rostros registrados | Se pide primero el email y se compara solo contra el rostro de esa persona | El usuario, por seguridad | 12-11-2026 |
| RFS03 (antes RFS24, RFS27), RFU21, RFU22, E17 | Ingreso facial para los empleados | Solo `dueño` y `administrador`. Diseñador y operario entran con contraseña | El usuario | 12-11-2026 |
| RNFO12 | Al menos 500 registros sintéticos y 12 meses de historia | Datos mínimos de prueba en una base aparte, `LetreriaLosHerreraDB_Demo` | El usuario | 12-11-2026 |
| E17 | Tres capturas de cámara | Cinco capturas; el servidor exige un mínimo de tres | Propuesta de Claude, aceptada al aprobar el plan | 12-11-2026 |
| Reglas de diseño (bordes) | Bordes sutiles de 1 px translúcidos (`border-slate-200/80`, `border-black/5`) | Se usan en tarjetas y separadores. Los **campos de formulario** y el botón secundario llevan un borde más oscuro (`#828DA0`), porque con el gris claro el campo no alcanza el contraste mínimo de 3:1 que exige WCAG 2.2 (1.4.11) | Claude, fase 2 (por WCAG, que el documento también exige) | 12-11-2026 |
| Reglas de diseño (barra) | Barra superior flotante de cristal | Flotante desde 768 px de ancho. En el teléfono y con poca altura (zoom), la barra es más alta y deja de ser fija, para no tapar el elemento con foco (WCAG 2.4.11) | Claude, fase 2, por la revisión de accesibilidad de ECC | 12-11-2026 |
| RFS02 (cartel) | El dueño o un trabajador da vuelta el cartel | Hoy la API (`PUT /cartel-digital`) solo lo permite a dueño y administrador, y la pantalla respeta ese permiso. **Decidir en la fase 9** si se amplía a diseñador y operario | Pendiente de tu decisión | 13-11-2026 |
| Logotipo | Logotipo del taller (versiones gris y roja) | Se usa la versión de anillo y letras en tinta `#0F172A`, derivada del original sin redibujarlo, porque combina con el modo claro y no compite con el ámbar de las acciones. Originales en `docs/marca/originales/` | Claude, a pedido del usuario («el logo que mejor le quede») | 13-11-2026 |
| RFS01 (fotos de la vitrina) | Imagen en alta definición de cada trabajo | La semilla demo apuntaba a fotos que no existen. Desde el cierre de la fase 5 usa 22 fotos reales entregadas por el taller (ver «Cambios y pendientes de la fase 5») | Claude | 13-11-2026 |
| Reglas de diseño (foco) | Acento solo en llamadas a la acción y estados activos | El anillo de foco también es ámbar: el foco es un estado activo y así se ve sobre los tres fondos (≥4,5:1) | Claude, fase 2 | 12-11-2026 |

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
| Destinatarios | Uno o más correos en `Correo:AvisosTaller`, leídos solo por `IDestinatariosAvisos`. En la fase 9 el servicio leerá primero el valor que guarde el dueño en la base | Fase 4 y fase 9 |

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
| E23, RFS12, RNFE01, S22 | Política de privacidad y términos vigentes | Borradores en versión 1.0 (`src/paginas/publicas/legales/textos.js`); cada página dice que es un borrador. Faltan la razón social, el RUT, el proveedor de alojamiento y su país, el plazo de conservación de las consultas, el abono y el momento del pago del saldo, y la revisión legal | Pendiente del usuario y del asesor legal | 13-11-2026 |
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
| RNFE01, RNFE06, RFS12 | Revisión legal de la política de privacidad, los términos, el consentimiento facial y la baja por inhabilitación | Asesor legal |

## Limitaciones y mejoras futuras (para la Sección 11 del informe técnico)

| Mejora | Origen |
|---|---|
| **Módulo de facturación mensual automatizada para clientes habituales con pedidos entregados por cobrar.** Hoy el sistema registra el número de factura y lista los pedidos por cobrar, pero la factura se emite fuera del sistema | Sugerencia de la profesora guía, 12-11-2026 |
