# Plan de construcción de SyncTime — Letrería Los Herrera

## Contexto

El 12-11-2026 entregaste los requisitos reales (`requerimientos_del_sistema.md`: 47 RFS, 34 RFU, 29 RNFP, 17 RNFO, 9 RNFE, entradas E01–E24, salidas S01–S23) y las reglas de diseño (`reglas de diseño.md`), y autorizaste modificar backend y frontend. Pediste construir por partes, con el plugin ECC y las skills de diseño, para que no se omita nada.

**Dónde estamos.** El backend (.NET 10, EF Core, SQL Server) tiene 14 tablas, 3 migraciones aplicadas y 136 pruebas; cubre catálogo, solicitudes, cotizaciones, pedidos, portal del cliente y login facial. El frontend (React 19, Vite) tiene un tema oscuro y varias pantallas con el contrato antiguo. La exploración encontró:

- De los 47 RFS, **7 se cumplen, 17 están a medias y 19 faltan**; los otros 4 son solo de interfaz. Faltan por completo: correos, recuperación de contraseña, auditoría, consentimientos, contacto, reportes, dashboard, IA, Google, portabilidad, Docker y CI/CD.
- **Lo construido contradice requisitos**: los estados cambian libremente (RFS29), se puede crear un pedido sin cotización aprobada ni pago (RFS14, RNFO02), el token dura 8 horas (RNFP08 pide 15 minutos), la lógica vive en los endpoints (RNFO10 pide capas y Repository), hay borrados físicos sin auditoría, y dos formatos de error.
- Del frontend solo se conservan el motor facial, el cliente HTTP (con cambios), el `ErrorBoundary` y la lógica de sesión, modales y avisos. Páginas y CSS se rehacen.

**Resultado esperado:** un sistema donde cada requisito tenga evidencia (prueba o pantalla), una desviación decidida por ti, o una verificación externa pendiente con responsable. Se construye en 18 fases que se cierran una por una.

Este plan pasó una revisión adversarial de ECC que contrastó los 183 identificadores; sus hallazgos ya están incorporados.

## Decisiones que tomaste (12-11-2026)

| Tema | Decisión |
|---|---|
| Ingreso facial | Email primero y comparación 1 a 1 (lo ya construido) |
| Quién usa el rostro | Solo `dueño` y `administrador`. Diseñador y operario entran con contraseña |
| Registro del rostro | Ambos caminos: el dueño lo inicia desde el panel con el titular presente, que confirma con su propia contraseña; o cada titular desde su perfil. El dueño puede desactivar cualquiera |
| Color de acento | Ámbar de taller `#B45309` |
| Datos de demostración | Base aparte `LetreriaLosHerreraDB_Demo`, solo los datos necesarios para probar |
| Ritmo | Parar al cerrar cada fase y esperar tu visto bueno |
| Git | Commit al cerrar cada fase en su propia rama; sin fusionar ni subir hasta que lo pidas |
| Docker | No se puede instalar en este equipo (OptiOS 11 Lite, sin Hyper-V). Las fases 1 a 15 se desarrollan y prueban de forma nativa (.NET 10, Node.js, SQL Server Express). Los contenedores se construyen en la fase 16 y se verifican en GitHub Codespaces y GitHub Actions |
| Base de datos local | Instancia `localhost\SQLEXPRESS` con autenticación de Windows (`Trusted_Connection=True; TrustServerCertificate=True;`). No se usa LocalDB. Base de desarrollo y demo: `LetreriaLosHerreraDB_Demo` |

## Desviaciones y pendientes externos (se registran en `docs/plan/desviaciones.md`)

**Desviaciones decididas:**
- **RFS25, RFU22:** no se identifica comparando contra todos los rostros; se pide el email primero. Decisión tuya, por seguridad.
- **RFS24, RFS27, RFU21, RFU22, E17:** el rostro no está disponible para diseñador ni operario. Decisión tuya.
- **RNFO12:** sin 500 registros ni 12 meses de historia; datos mínimos de prueba. Decisión tuya.
- **E17:** 5 capturas en vez de 3 (el servidor exige un mínimo de 3). **Decisión mía**, porque es más robusto; dime si prefieres 3.
- **RFS10** no existe en el documento: la numeración salta de RFS09 a RFS11.

**Requisitos que no puedo verificar por mi cuenta** (quedan preparados y documentados, con responsable):
- RNFP04 (99 % de disponibilidad), RNFE03 (certificado TLS) y la parte "fuera del servidor" de RNFP09: dependen del hosting que contrates.
- RNFO14 y RNFO15 (`docker compose up` y el pipeline): se verifican en GitHub Codespaces y GitHub Actions, porque este equipo no admite Docker.
- RNFP02 (un cliente sin capacitación en 5 minutos) y la prueba manual con lector de pantalla de RNFO13: necesitan a una persona.
- Revisión legal de privacidad, términos y consentimiento facial (RNFE01, RNFE06).

## Decisiones técnicas que tomé (objétalas al revisar el plan)

- **Estilos:** CSS propio con tokens (variables CSS) y CSS Modules, sin Tailwind. Las clases tipo Tailwind de tu documento se traducen a tokens. Así el sistema de diseño es propio (RNFP14) y no se agrega una dependencia.
- **Frontend en la misma carpeta** `frontend/synctimefront`, en JavaScript. Se eliminan páginas y CSS antiguos; se conservan herramientas, motor facial y pruebas válidas.
- **Tipografía:** Plus Jakarta Sans alojada en el propio sitio (licencia OFL), con los grosores 400, 500, 600 y 700. Sin Google Fonts, para no llamar a terceros antes del consentimiento.
- **Animación:** CSS y Web Animations API. Sin librería de animación salvo que una interacción lo exija.
- **Sesión:** token de acceso de 15 minutos más token de renovación rotatorio en cookie `httpOnly`. Se reemplaza `axios` por `fetch`.
- **Cliente inicia sesión con correo** (RFS05), no con teléfono. El teléfono sigue siendo único y sirve para buscar.
- **Pago:** es una confirmación que registra el personal (quién, cuándo, medio, monto). No hay pasarela de pago.
- **Montos:** enteros en pesos, validados en API e interfaz. No cambio el tipo de las columnas existentes.
- **Capas del backend:** carpetas `Api`, `Negocio` y `Datos` dentro del mismo proyecto, con pruebas de arquitectura. No separo en proyectos para no arriesgar las migraciones aplicadas.
- **Mapa:** imagen estática de la ubicación, alojada en el sitio, que pasa a mapa interactivo (Leaflet con OpenStreetMap) cuando el visitante lo activa. Así nada se pide a terceros sin una acción suya.
- **Correo:** MailKit con cola de salida. Como no hay Docker local, en desarrollo los correos se guardan en una bandeja local (archivos `.eml` en una carpeta, visibles en una ruta que solo existe en desarrollo). El envío SMTP real se prueba con el contenedor de correo en Codespaces o con la cuenta del taller.
- **Contra el spam en formularios públicos:** campo trampa, tiempo mínimo y límite por IP. Sin captcha de terceros.
- **Gráficos, PDF, QR y proveedor de IA:** se eligen en su fase, verificando peso y licencia (RNFO16, RNFO17). El proveedor de IA queda detrás de una interfaz; tu documento nombra Gemini y la clave la pones tú.
- **"Claude Design":** no hay una skill instalada con ese nombre. Sus criterios (componentes limpios, HTML semántico, separación modular, cero dependencias pesadas) los aplico con `ecc:react-patterns`, `ecc:frontend-patterns` y el agente `ecc:react-reviewer`. Si te refieres a un plugin concreto, dímelo.

## Protocolo de cada fase (igual para todas)

1. **Contrato primero (RNFP19):** las rutas nuevas se escriben primero en `docs/api/openapi.yaml`. Una prueba compara rutas, verbos y códigos de respuesta con lo que genera la API, y falla hasta que la implementación coincide.
2. **Pruebas primero** (`ecc:tdd-workflow`): backend con xUnit (unitarias de negocio e integración sobre una base temporal); frontend con Vitest, Testing Library y Playwright.
3. **Implementación** en la rama `fase-NN-nombre`, que nace de la rama aprobada de la fase anterior.
4. **Pases de diseño en toda interfaz nueva:** `ui-ux-pro-max` (matriz de estados y accesibilidad), `impeccable` (dirección de arte, crítica y pulido), `emil-design-eng` / `apple-design` / `animate` (movimiento).
5. **Revisión ECC** según lo tocado: `csharp-reviewer`, `react-reviewer`, `security-reviewer`, `a11y-architect`, `silent-failure-hunter`, y la skill `database-migrations` si hay migración. Cada hallazgo se corrige o se documenta.
6. **Pruebas no vacuas:** en las reglas críticas reintroduzco el error y la prueba debe fallar.
7. **Verificación en la app en marcha**, sobre la base demo, a 360, 768 y 1440 px, con la consola sin errores.
8. **Documentos al día:** contrato, matriz de permisos, justificación de índices, trazabilidad y tablero.
9. **Cierre:** commit en la rama, informe (qué se hizo, pruebas, qué no se verificó) y **me detengo a esperar tu visto bueno**.

**Reglas fijas:**
- **Bases de datos.** Todo corre en `localhost\SQLEXPRESS` con autenticación de Windows. Las pruebas automáticas crean bases temporales con un prefijo propio (`SyncTime_Pruebas_…`) y las eliminan al terminar; el código de pruebas se niega a ejecutarse sobre una base que no lleve ese prefijo. Las pruebas de extremo a extremo y toda verificación manual usan `LetreriaLosHerreraDB_Demo`. La base real `LetreriaLosHerreraDB` no recibe datos de prueba y nunca se borra ni se recrea.
- **Migraciones.** El esquema solo cambia con migraciones nuevas de EF Core. Antes de escribir una, consulto si los datos existentes la harían fallar. Pruebo también su reversión en la base temporal. A la base real se aplican **solo después de tu visto bueno de la fase**, con un respaldo previo verificado (`BACKUP ... COPY_ONLY` y `RESTORE VERIFYONLY`) y mostrándote antes el SQL.
- No escribo contraseñas ni claves por ti: si falta una credencial, te pido que la crees.
- Todo en español, también los mensajes de validación (RNFO07). Sin patrones engañosos (RNFP21).
- Si una fase crece demasiado, la divido y anoto el cambio con fecha en el plan.

## Mapa de fases

| # | Fase | Depende de | ¿Migración? |
|---|---|---|---|
| 0 | Preparación del repositorio y documentos | — | No |
| 1 | Cimientos del backend y base demo | 0 | No |
| 2 | Sistema de diseño | 0 | No |
| 3 | Estructura de la aplicación web | 1, 2 | No |
| 4 | Servicios comunes: correo, consentimientos e imágenes | 1 | Sí |
| 5 | Portada pública | 3, 4 | Sí |
| 6 | Cuentas y sesión | 3, 4 | Sí |
| 7 | Solicitudes y clientes | 6 | Sí |
| 8 | Cotizaciones, pago y pedidos | 7 | Sí |
| 9 | Mantenedores y estado del local | 4, 6 | Sí |
| 10 | Rostro según requisitos | 6, 9 | Posible |
| 11 | Notificaciones, auditoría y privacidad | 4, 8, 9 | Sí |
| 12 | Indicadores y dashboard | 8 | Sí (vistas) |
| 13 | Reportes y exportaciones | 11, 12 | Sí |
| 14 | Google y asistente de IA | 6, 7, 9 | Posible |
| 15 | Calidad: rendimiento, accesibilidad y compatibilidad | 13, 14 | No |
| 16 | Contenedores, CI/CD y operación | 15 | No |
| 17 | Cierre y verificación de trazabilidad | 16 | No |

Las fases se ejecutan en este orden, una a la vez.

## Fases en detalle

Rutas base: `B` = `backend/Proyecto-Carteles-herrera-synctime-main/LetrerosHerreraSYNCTIMEapi`, `F` = `frontend/synctimefront`.

### Fase 0 — Preparación del repositorio y documentos
**Objetivo:** un solo repositorio ordenado y los documentos fuente dentro de él. No cambia código de producto. **Requisitos:** RNFO09, RNFO15 (historial sin secretos).
- **Inventario antes de tocar nada:** comparar la carpeta local con `main` de GitHub (`aa7514a`) y listar las diferencias. Confirmar que el backend está subido como archivos normales y no como submódulo.
- **Respaldo:** `git bundle` del git anidado del backend (ramas `feat/login-facial` y `fix/migracion-fuente-unica`) en `C:\Users\Admin\Desktop\synctime-respaldo\`, verificado con `git bundle verify`. Luego se **mueve** su carpeta `.git` a ese respaldo. No se borra nada.
- **Unificación:** `git init` en `C:\Users\Admin\Desktop\synctime`, remoto, `fetch` y `reset` mixto, que no modifica archivos. Si hay diferencias con `aa7514a`, van en un commit aparte "importa trabajo local" que te muestro antes.
- **Higiene:** revisar el `.gitignore` (bin, obj, node_modules, `.env`, archivos de base de datos); fijar los finales de línea con `.gitattributes` sin reescribir todos los archivos; buscar secretos en el historial completo de GitHub. Hay un commit antiguo titulado "documentar credenciales admin" que revisaré en particular.
- **Documentos:** copiar los dos documentos a `docs/requisitos/`; guardar este plan en `docs/plan/plan-de-construccion.md` junto con `desviaciones.md`, `trazabilidad.md` y `tablero.md` (tablero Kanban).
- **Herramientas y línea base:** comprobar .NET 10, Node LTS, la instancia `localhost\SQLEXPRESS` y Playwright. No se comprueba Docker, que no existe en este equipo. Ejecutar las 136 pruebas del backend, apuntadas a `localhost\SQLEXPRESS` con la variable `TEST_SQL_SERVER` que el proyecto ya admite, y las 79 del frontend; anotar el resultado.

**Cierre:** respaldo verificado; `git status` sin cambios inesperados en la rama `fase-00-preparacion`; resultado del escaneo de secretos informado; línea base anotada.

### Fase 1 — Cimientos del backend y base demo
**Requisitos:** RNFO10, RFS31, S21, RFS32 (convención), RFS23 (matriz), RNFP07 (infraestructura), RNFP11, RNFP19, RNFP25, RNFP26, RNFP28, RNFO05, RNFO06, RNFO12 (reducido), RNFO13 (unitarias).
- **Capas** `Api` / `Negocio` / `Datos`, con repositorio base y unidad de trabajo. Molde: `B/Repositories/ISolicitudRepository.cs`. Prueba de arquitectura con una lista de módulos pendientes de migrar, que debe quedar vacía en la fase 17.
- **Error único** (extiende `B/Middleware/ApiExceptionHandler.cs`): `ProblemDetails` con `error`, `codigo`, `nivel` (formulario, API o base de datos) y `errores` por campo, también en 401, 403, 404 y 429. Los errores de restricciones de la base se traducen a este formato.
- **Validación en tres niveles:** validación centralizada de entrada con mensajes en español; validadores compartidos (RUT con dígito verificador, montos enteros, teléfono, correo); revisión de las restricciones CHECK del esquema actual.
- **Listados:** `pagina`, `tamano`, `orden`, `buscar` y `ResultadoPaginado<T>`, con proyecciones en vez de entidades completas.
- **Matriz de permisos por rol** en `docs/api/permisos.md`: qué puede cada rol, qué es "trabajador" y en qué se diferencian dueño y administrador. **Te la presento para confirmar.**
- Compresión Gzip/Brotli, ruta `/salud`, cabeceras de seguridad y HSTS fuera de Development. Límite de tasa por IP **y** por cuenta (hoy es solo por IP, en `B/Program.cs`).
- OpenAPI con resúmenes y respuestas, y la prueba de contrato del protocolo. `ContratoFrontend.md` pasa a `docs/api/contrato.md`.
- Proyecto nuevo de pruebas unitarias de negocio.
- **Pruebas sobre SQL Server Express:** `localhost\SQLEXPRESS` pasa a ser el servidor por defecto de las pruebas (hoy es LocalDB, en `ApiFixture.cs`), con el prefijo y la guarda descritos en las reglas fijas.
- **Base demo:** comando que crea `LetreriaLosHerreraDB_Demo` en `localhost\SQLEXPRESS` con las migraciones y una semilla mínima sintética (un usuario por rol con contraseñas que defines tú, clientes, publicaciones, solicitudes). Cada fase amplía la semilla con lo suyo. La API de desarrollo apunta a esta base.

**Cierre:** las 136 pruebas en verde (ajustadas al formato de error nuevo); unitarias nuevas; la API arranca contra la base demo; el frontend actual sigue funcionando porque `error` se conserva.

### Fase 2 — Sistema de diseño
**Requisitos:** reglas de diseño (los cuatro estándares e identidad visual), RNFP03, RNFP14, RNFP15, RNFP22 (fuentes), RNFE08, RNFO17 (fuentes), RFU29.
- `DESIGN.md`: dirección de arte (`impeccable`) y tokens en tres capas (`ui-ux-pro-max:design-system`): primitivos → semánticos → de componente.
- `F/src/estilos/tokens.css` con los valores de tu documento:
  - Fondos `#FFFFFF`, y secciones alternadas `#F8FAFC` y `#F1F5F9`.
  - Tinta `#0F172A`, con contraste verificado superior a 12:1.
  - Acento `#B45309`, **solo** en llamadas a la acción y estados activos.
  - Títulos en 600/700 con `letter-spacing: -0.02em`; cuerpo en 400/500.
  - Bordes de 1 px translúcidos, sombras difusas multicapa, cristal (`backdrop-filter`) para la barra superior.
  - Movimiento: curva `cubic-bezier(0.16, 1, 0.3, 1)`, compresión `scale(0.98)` al presionar, elevación suave de tarjetas, entrada de diálogos, brillo de esqueletos; todo se desactiva con `prefers-reduced-motion`.
- Componentes con su matriz completa de estados (normal, hover, foco visible, activo, deshabilitado, cargando, vacío): botón, enlace, campos de formulario con error anunciado al lector de pantalla, tarjeta, insignia, diálogo nativo (cierra con Esc y devuelve el foco), aviso, tabla adaptable, pestañas, esqueleto, estado vacío, paginación.
- Accesibilidad de base: controles táctiles de 44 × 44 px (nunca menos de 24), contraste 4,5:1 en texto y 3:1 en texto grande y componentes, foco que no queda tapado por la barra ni por botones flotantes, zoom al 200 %, reflujo desde 320 px, información que no depende solo del color.
- Galería interna de componentes, solo en desarrollo, para revisar el sistema.

**Cierre:** galería revisada a 320, 360, 768 y 1440 px y con zoom al 200 %; axe sin errores críticos ni serios; contrastes medidos y anotados.

### Fase 3 — Estructura de la aplicación web
**Requisitos:** RFS31 (formularios), RNFP14 (navegación, 403/404), RNFP23, RNFP24, RNFO05, RNFO06, RNFO16, RFU29.
- Dos estructuras de página: pública (barra flotante de cristal, pie con enlaces legales) y panel interno (navegación colapsable en móvil, filtrada por rol, también con enlaces legales). Páginas 403, 404 y de error. Enlace de salto al contenido en ambas.
- Cliente HTTP con `fetch`, errores estándar y manejo del 401 según el contexto (hoy redirige siempre a `/login`). Sesión con dos tipos de usuario. Rutas con carga diferida y guardas con los roles reales (`dueño`, `administrador`, `diseñador`, `operario`). Peticiones independientes en paralelo y sin llamadas duplicadas.
- Utilidades: `formatearPesos`, `formatearFecha` (dd-mm-aaaa), `validarRut`, `useDebounce` (300 ms).
- Se conservan y restilizan `F/src/rostro/*`, `CapturaFacial`, `ErrorBoundary` y el acceso actual con contraseña y rostro, que sirve de prueba de integración del sistema nuevo.
- Se eliminan: `index.css` oscuro, `App.css`, las páginas con el contrato antiguo, `prop-types`, `axios`, recursos sin uso y las pruebas que simulan el contrato antiguo. Las secciones aún no construidas muestran un estado vacío que indica su fase. Esas páginas ya fallan hoy contra la API real, así que no se pierde nada en uso.
- CSP actualizada (fuentes propias, imágenes, mapa).

**Cierre:** entrar con contraseña funciona sobre la base demo; **tu ingreso con rostro lo pruebas tú** contra la base real, cuyo esquema no cambia en esta fase; presupuesto de peso cumplido (JavaScript inicial de 150 KB comprimido o menos); axe sin errores críticos ni serios.

### Fase 4 — Servicios comunes: correo, consentimientos e imágenes
**Requisitos:** RFS40 (infraestructura), RFS44, RNFP17, RNFP22 (imágenes), RNFE05.
- **Correo:** `IServicioCorreo`, cola `correos_salientes`, envío en segundo plano con reintentos y tiempo máximo de espera; bandeja local en desarrollo. Un fallo del servidor de correo nunca bloquea la petición.
- **Consentimientos:** tabla `consentimientos` con el identificador del usuario (o del envío, si es un visitante sin cuenta), tipo, versión del documento, fecha y hora, y su revocación. Los documentos legales llevan número de versión.
- **Imágenes:** subida con validación de tipo y tamaño, conversión a WebP en dos tamaños y servicio con `Cache-Control`.

**Cierre:** un correo de prueba llega a la bandeja local y un servidor de correo caído no rompe la API; una imagen subida se sirve en WebP; un consentimiento queda registrado con versión y hora.

### Fase 5 — Portada pública
**Requisitos:** RFS01, RFS02 (lectura), RFS03, RFS39, RFS40, RFS42, RFS43 (contacto), RFS45, RFS47 (regla), RFS32 (catálogo), RFU01–03, RFU30–33, RNFP21, RNFE01, RNFE09, RNFO17 (logos), E22, E23, S01, S02, S22.
- **Backend:** catálogo filtrable por categoría y paginado, con `tipo_letrero` y la autorización de uso de logo (quién la registró y cuándo; sin ella la publicación no puede ser visible); horario con "abierto ahora" calculado con la hora de Chile, y regla clara de cómo convive con el estado manual del local; QR del portal con dirección configurable; `POST /contacto` que guarda la consulta y envía el correo al administrador.
- **Frontend:**
  - Encabezado "Letrería Los Herrera" con la propuesta de valor y los datos de contacto.
  - Vitrina de tarjetas: imagen WebP con carga diferida y texto alternativo, título, descripción, tipo de letrero, categoría, medidas y costo referencial; filtro por categoría.
  - Horario semanal y estado del local, con mensaje, tiempo estimado de regreso y hora de última actualización.
  - Mapa de Villagrán 810 (imagen estática que se vuelve interactiva al activarla) y botón flotante de WhatsApp.
  - Formulario de contacto con validación inmediata y casilla de privacidad desmarcada.
  - Banner de cookies con opciones de igual tamaño y casillas desmarcadas; ningún script de analítica se ejecuta sin consentimiento.
  - Política de Privacidad y Términos y Condiciones (borradores marcados como tales).
- **Necesito de ti:** número de WhatsApp, correo y teléfono del taller, fotos reales y permiso de uso de logos. Mientras tanto uso imágenes de ejemplo con licencia libre, marcadas como tales.

**Cierre:** la portada funciona sin sesión; el formulario registra la consulta, su consentimiento y el correo llega a la bandeja local; no hay peticiones a terceros sin una acción del visitante.

### Fase 6 — Cuentas y sesión
**Requisitos:** RFS04, RFS05 (correo), RFS27 (acceso del personal), RFS28, RFS43, RFS44, RNFE01 (consultar y corregir), RNFP06, RNFP07, RNFP08, RNFP13, RNFP29, RFU04, RFU05, E01, E02, E23.
- **Antes de migrar:** consultar en la base real si hay cuentas de cliente sin correo y decidir contigo qué hacer con ellas.
- **Backend:** token de 15 minutos configurable y renovación rotatoria (`tokens_renovacion`), también al entrar con rostro; cookie `SameSite` con protección CSRF; CORS con credenciales y orígenes explícitos; cierre de sesión; revocación al desactivar una cuenta; correo obligatorio para tener cuenta; registro que valida teléfono, RUT y correo duplicados con mensajes claros; recuperación de contraseña con código de un solo uso de 15 minutos guardado como hash; 10 intentos por minuto por IP y por cuenta en inicio de sesión y recuperación.
- **Frontend:** registro de cliente con dos casillas separadas y desmarcadas (términos y privacidad; marketing); ingreso de clientes; acceso del personal aparte, el único con "Entrar con rostro"; recuperación de contraseña; perfil del cliente donde puede ver y corregir sus datos; renovación silenciosa de la sesión.

**Cierre:** flujos probados de extremo a extremo; un token vencido se renueva sin expulsar al usuario; el intento 11 en un minuto recibe 429 con mensaje.

### Fase 7 — Solicitudes y clientes
**Requisitos:** RFS06–09, RFS11, RFS19, RFS29, RFS32, RFS41 (pendientes), RFS43, RNFO04, RNFP02, RNFP10, RFU06, RFU10, RFU11, RFU19, E03–E05, S03, S05.
- **Primero, los diagramas de estados** de solicitud, cotización y pedido, escritos en el contrato. **Te los presento para confirmar** antes de programar, porque los requisitos dejan dudas: orden de los estados del pedido, qué pasa con la solicitud si se rechaza una cotización y si puede haber varias cotizaciones.
- **Backend:** servicio y repositorio de solicitudes; transiciones válidas; edición (también de ítems) solo en «solicitado» o «cotizado»; rechazo con motivo obligatorio; borrado lógico; filtros por estado, origen y fecha, con orden y paginación; quién registró cada solicitud; clientes con búsqueda por teléfono o RUT, listado paginado y control de concurrencia; permisos del trabajador según RFU11.
- **Frontend:** solicitud en línea del visitante, con las casillas de consentimiento y un comprobante (número, fecha y hora, cliente, ítems con medidas, área y cantidad, estado); el mismo comprobante en el registro presencial, que busca al cliente antes de crearlo; bandeja del personal con filtros y búsqueda; listado y ficha de clientes; edición con aviso si otro usuario modificó el registro; vista principal del panel con las solicitudes pendientes, que es adonde llegan diseñador y operario al entrar.

**Cierre:** una transición inválida responde 409 con explicación; el flujo en línea se completa en menos de 5 minutos en un móvil (lo cronometro yo; la validación con una persona sin capacitación queda como pendiente externo); el cliente ve solo lo suyo.

### Fase 8 — Cotizaciones, pago y pedidos
**Requisitos:** RFS12–17, RFS19, RFS29, RFS32, RFS41 (cliente), RNFO02, RNFP10, RFU07, RFU08, RFU12–15, E06–E09, S04, S06–S08.
- **Antes de migrar:** consultar si hay pedidos sin cotización o sin pago en la base real, que harían fallar las reglas nuevas.
- **Backend:** cotización con materiales (ya existen cinco de ejemplo), mano de obra y otros costos, que pasa la solicitud a «cotizado» y guarda quién la elaboró; aprobación o rechazo por el cliente (`/mis/cotizaciones`) o por un trabajador; **confirmar pago** crea un único pedido con los montos de la cotización y pasa la solicitud a «aceptado»; se elimina la creación de pedidos sin cotización aprobada y pagada; pedido con responsable, fechas, modalidad, instalación y monto final; transiciones de estado; listados de cotizaciones y pedidos con búsqueda, filtros, orden y paginación; calendario por rango de fechas; control de concurrencia en ambos.
- **Frontend:** listados y editor de cotización; seguimiento del cliente con progreso visual y cotizaciones por aprobar, como apartado destacado de su vista principal; ficha del pedido; calendario de entregas que distingue pendientes y entregados con color y además icono o etiqueta; alertas de entregas próximas o atrasadas en la vista del administrador.

**Cierre:** prueba de extremo a extremo del flujo completo solicitud → cotización → aprobación → pago → pedido → entrega; ningún atajo salta pasos.

### Fase 9 — Mantenedores y estado del local
**Requisitos:** RFS20, RFS21, RFS02 (edición), RFS32, RFS47, RNFO03, RNFO08, RNFP10, RFU16–18, RFU20, E10–E15.
- **Backend:** mantenimiento de materiales y de empleados (editar, cambiar rol, desactivar; al desactivar se borra su rostro y se revoca su sesión en el mismo paso); horario editable y estado del local (abierto, ausente, mensaje, regreso estimado) modificable por todo el personal, ambos con registro de quién y cuándo; disponibilidad horaria de los empleados y su consulta pública.
- **Frontend:** mantenedores de categorías, productos, publicaciones (con subida de imagen y marca de permiso de logo), materiales y empleados, con búsqueda y paginación; horario y estado del local; registro de disponibilidad, y su vista para los clientes en la portada, mostrando franjas de atención sin datos personales del empleado.

**Cierre:** cada mantenedor probado con sus roles; una publicación con logo sin autorización no se puede hacer visible.

### Fase 10 — Rostro según requisitos
**Requisitos:** RFS24, RFS26, RFS27, RFU21, RFU22 (parcial), RNFP12, RNFE06, E17, E18, S15, S16.
- **Backend** (`B/Endpoints/RostroApi.cs`, `B/Services/RostroFacialService.cs`): solo `dueño` y `administrador` (los demás roles reciben el mismo 401 genérico); registro iniciado por el dueño con la contraseña del titular como consentimiento; listado de rostros (nombre, rol, fecha de registro, fecha de consentimiento, estado); desactivación por el dueño o por el propio titular, que **borra el descriptor** y conserva solo la constancia del consentimiento; respuesta que indica que ingresó como dueño; ingreso resuelto en 3 segundos o menos y límite de 10 intentos por minuto, ambos medidos.
- **Frontend:** pantalla del dueño "Rostros registrados"; sección de rostro en el perfil; aviso claro cuando no hay coincidencia, con la contraseña siempre disponible.
- Antes de restringir, verifico que no existan rostros de otros roles (hoy hay uno, el tuyo).

**Cierre:** un diseñador u operario no puede activar ni usar el rostro; el dueño lista y desactiva; tu ingreso con rostro sigue funcionando.

### Fase 11 — Notificaciones, auditoría y privacidad
**Requisitos:** RFS18, RFS30, RFS46, RFS48, RNFO04, RNFE01, RFU09, RFU28, RFU34, E24, S09, S20, S23.
- **Backend:**
  - Correos al cliente cuando su solicitud se cotiza, rechaza o acepta y cuando su pedido cambia de estado, con número, estado nuevo, mensaje, fecha y enlace de baja.
  - Baja con enlace firmado que lleva a una confirmación (no se ejecuta con solo abrir el enlace) y cabecera `List-Unsubscribe-Post`; preferencias de notificación.
  - Bitácora de auditoría inmutable mediante un interceptor de EF Core (usuario, rol, entidad, acción, fecha y hora, detalle sin datos sensibles). Una prueba exige que toda entidad principal quede cubierta y prohíbe las escrituras que el interceptor no ve.
  - Consulta de auditoría y de consentimientos para el dueño.
  - Descarga de los datos personales e historial (JSON) y eliminación o anonimización de la cuenta, conservando las estadísticas y entregando un comprobante.
- **Frontend:** preferencias y página de baja; "Mis datos" (descargar, eliminar); visor de auditoría con filtros.
- **Necesito de ti:** una cuenta SMTP del taller para el envío real. Sin ella queda probado contra la bandeja local, y el envío SMTP solo en Codespaces (fase 16).

**Cierre:** cada cambio de estado genera su correo; la baja funciona sin sesión; toda escritura en entidades principales deja registro.

### Fase 12 — Indicadores y dashboard
**Requisitos:** RFS22, RFS33, RFS34, RFS41 (administración), RNFO11 (vistas), RFU19, RFU27, E16, E21, S10–S14, S19.
- **Backend:** indicadores con las fórmulas literales del documento: pedidos por mes por estado sin contar cancelados; ingresos como suma del monto final de los entregados; solicitudes en línea frente a presenciales; en línea concretadas; conversión con su diferencia en puntos porcentuales al comparar; más ticket promedio y entregas a tiempo. Por semana, mes o año, comparando dos períodos. Vistas SQL e índices justificados. Datos filtrables por fecha, categoría, estado y origen.
- **Frontend** (skill `dataviz`): dashboard con al menos cuatro gráficos interactivos, cada uno con título, descripción textual y tabla de datos accesible; filtros; métricas en la vista principal del dueño.

**Cierre:** cada indicador coincide con un cálculo hecho a mano sobre datos conocidos.

### Fase 13 — Reportes y exportaciones
**Requisitos:** RFS35–37, RNFP18, RFU24–26, E16, E20, S18.
- **Backend:** tres reportes (operativo; gerencial con totales y gráficos; parametrizable) con los roles de cada uno; exportación en segundo plano con una tabla de trabajos, validando el rol y registrando en auditoría quién exportó:
  - **Excel:** encabezados, formatos de fecha y número, totales.
  - **PDF:** título, fecha de emisión, filtros aplicados, numeración de páginas y texto seleccionable.
  - **CSV:** UTF-8 con separador documentado.
- **Frontend:** reportes con los filtros de pantalla, vista limpia de impresión (`@media print`) y descargas que no bloquean la interfaz.

**Cierre:** los tres formatos se abren correctamente y respetan los filtros; un rol sin permiso recibe 403.

### Fase 14 — Google y asistente de IA
**Requisitos:** RFS05 (Google), RFS38, RFS43, RNFE02, RNFE07, RNFP17, RNFO13 (IA simulada), RFU05, RFU23, E19, S17.
- **Google:** validación del token de identidad exigiendo correo verificado; si el correo ya tiene cuenta con contraseña, no se fusiona sola: se pide iniciar sesión para vincularla; una cuenta nueva pasa por una pantalla que completa el registro con las casillas de consentimiento.
- **Asistente:** ruta con validación de entrada, tiempo máximo de espera, límite por IP y tope de uso, defensa contra instrucciones inyectadas, validación del JSON que devuelve el proveedor y error estándar; implementación simulada para las pruebas.
- **Frontend:** botón de Google, cuyo script se carga solo cuando el usuario lo pide; asistente en la solicitud (sugiere categoría, medidas y borrador) y en el catálogo (redacta descripciones para el dueño).
- **Necesito de ti:** el ID de cliente OAuth de Google Cloud y la clave del proveedor de IA que elijas. Sin ellas queda construido y probado con el simulador.

**Cierre:** si el proveedor falla o tarda, la API responde el error estándar sin bloquearse; ninguna clave está en el código.

### Fase 15 — Calidad: rendimiento, accesibilidad y compatibilidad
**Requisitos:** RNFP01, RNFP02, RNFP03, RNFP05, RNFP15, RNFP23, RNFP24, RNFP27, RNFO13, RNFE08, RFU29.
- Prueba de carga con 20 usuarios simultáneos, con un guion propio en Node.js que corre de forma nativa; simulación de red lenta; revisión de llamadas duplicadas y de la división del código.
- Playwright en Chromium, Edge, Firefox y WebKit, y con perfil móvil. Instalar Firefox y WebKit exige descargar sus navegadores: te pediré permiso en ese momento. WebKit se aproxima a Safari pero no lo reemplaza: Safari real queda para una prueba en un dispositivo.
- axe en todas las rutas y Lighthouse.
- Guion de prueba manual con teclado y lector de pantalla (NVDA). **Esa prueba la ejecuta una persona:** te dejo el guion y la hacemos juntos.

**Cierre:** el 95 % de las respuestas en 2 segundos o menos, o las causas y correcciones anotadas si no se alcanza; axe sin errores críticos ni serios; sin desplazamiento horizontal entre 360 y 1440 px.

### Fase 16 — Contenedores, CI/CD y operación
**Requisitos:** RNFO14–17, RNFP04, RNFP09, RNFP20, RNFP25, RNFP29, RNFE03, RNFE09, S22.
**Enfoque:** en este equipo no hay Docker. Los archivos de contenedores se escriben aquí con todo el rigor de la rúbrica y se **verifican en la nube**: en un GitHub Codespace (Docker nativo en Linux) y en el pipeline de GitHub Actions.
- `Dockerfile` en varias etapas, con imágenes base ligeras de versión fija, para API y frontend; `.dockerignore`.
- `compose.yaml` con frontend, API, SQL Server y correo de desarrollo, con volúmenes, redes internas, comprobaciones de salud, caché de archivos estáticos y variables en `.env` (excluido del repositorio, con `.env.example`).
- `.devcontainer/` para que el Codespace arranque con Docker listo, y un guion `scripts/verificar-compose.sh` que ejecuta `docker compose up`, espera a que todos los servicios estén sanos, recorre las rutas de salud y un flujo básico, y guarda la evidencia (salida de `docker compose ps`, registros y resultado) en `docs/evidencias/`.
- GitHub Actions: construir las imágenes, levantar el entorno, ejecutar todas las pruebas (las del backend contra un contenedor de SQL Server) y publicar las imágenes solo si pasan, sin secretos en el historial ni en los registros.
- Revisión estática aquí, sin Docker: validación de la sintaxis de `compose.yaml` y de los `Dockerfile`, y revisión con ECC (`docker-patterns`, `security-reviewer`).
- Respaldo diario con retención de 30 días y una restauración probada de principio a fin en `localhost\SQLEXPRESS`, documentada.
- Guía de despliegue (HTTPS, dominio, CORS por entorno); inventario de licencias de paquetes, fuentes, iconos e imágenes, con su vista pública; eliminación de dependencias sin uso.
- **Necesito de ti:** permiso para subir la rama a GitHub en ese momento, y que abras un Codespace del repositorio y ejecutes el guion de verificación. Te dejo los pasos exactos.

**Cierre:**
1. Archivos de contenedores escritos, revisados y validados de forma estática en este equipo.
2. En el Codespace, `docker compose up` levanta todo el entorno con un solo comando y el guion deja la evidencia en `docs/evidencias/`.
3. El pipeline de GitHub Actions termina en verde y solo publica imágenes con las pruebas aprobadas.
4. Una restauración de respaldo comprobada.

Los puntos 2 y 3 ocurren fuera de este equipo: hasta que existan esas evidencias, la fase queda como "construida, pendiente de verificación en la nube".

### Fase 17 — Cierre y verificación de trazabilidad
- Recorrer la matriz completa, identificador por identificador.
- Revisión final de seguridad contra OWASP Top 10 y OWASP API Security Top 10 (RNFE04, RNFP16), con `OWASP.md` actualizado.
- Revisión completa con ECC (`orch-review`). Informe técnico, README y manual breve de uso al día.

**Cierre:** cada requisito tiene evidencia, desviación decidida o pendiente externo con responsable; la lista de módulos sin migrar a capas está vacía; todas las pruebas pasan.

## Trazabilidad: dónde se cumple cada requisito

| Grupo | Requisito → fase |
|---|---|
| RFS | 01→5 · 02→5,9 · 03→5 · 04→6 · 05→6,14 · 06→7 · 07→7 · 08→7 · 09→7 · 11→7 · 12→8 · 13→8 · 14→8 · 15→8 · 16→8 · 17→8 · 18→11 · 19→7,8 · 20→9 · 21→9 · 22→12 · 23→1 y cada fase · 24→10 · 25→desviación · 26→10 · 27→6,10 · 28→6 · 29→7,8 · 30→11 · 31→1,3 · 32→1,5,7,8,9 · 33→12 · 34→12 · 35→13 · 36→13 · 37→13 · 38→14 · 39→5 · 40→4,5 · 41→7,8,12 · 42→5 · 43→5,6,7,14 · 44→4,6 · 45→5 · 46→11 · 47→5,9 · 48→11 |
| RFU | 01–03→5 · 04→6 · 05→6,14 · 06→7 · 07→8 · 08→8 · 09→11 · 10→7 · 11→7 · 12–15→8 · 16–18→9 · 19→7,12 · 20→9 · 21→10 · 22→10 · 23→14 · 24–26→13 · 27→12 · 28→11 · 29→2,3,15 · 30–33→5 · 34→11 |
| RNFP | 01→15 · 02→7,15 · 03→2,15 · 04→16 (externo) · 05→15 · 06→6 · 07→1,6 · 08→6 · 09→16 · 10→7,8,9 · 11→1 · 12→10 · 13→6 · 14→2,3 · 15→2,15 · 16→1,17 · 17→4,14 · 18→13 · 19→1 y cada fase · 20→16 · 21→5 y cada fase · 22→2,4,5 · 23→3,15 · 24→3,15 · 25→1,16 · 26→1 y cada fase · 27→15 · 28→1 · 29→6,16 |
| RNFO | 01→cumplido · 02→8 · 03→9 · 04→7,11 · 05→1,3 · 06→1,3 · 07→todas · 08→9 · 09→0 · 10→1, cada fase y 17 · 11→cada migración y 12 · 12→1 (reducido) · 13→1,14,15 · 14→16 · 15→0,16 · 16→3,16 · 17→2,5,16 |
| RNFE | 01→5,6,11 · 02→14 · 03→16 (externo) · 04→cada fase y 17 · 05→4,11 · 06→10 · 07→14 · 08→2,15 · 09→5,16 |
| Entradas | E01–02→6 · E03–05→7 · E06–09→8 · E10–15→9 · E16→12,13 · E17–18→10 · E19→14 · E20→13 · E21→12 · E22→5 · E23→5,6 · E24→11 |
| Salidas | S01–02→5 · S03→7 · S04→8 · S05→7 · S06–08→8 · S09→5,11 · S10–14→12 · S15–16→10 · S17→14 · S18→13 · S19→12 · S20→11 · S21→1 · S22→5,16 · S23→11 |

## Lo que necesito de ti, y cuándo

| Insumo | Fase | Si no llega |
|---|---|---|
| Confirmar la matriz de permisos por rol | 1 | No avanzo a las fases de negocio |
| Contraseñas de los usuarios de la base demo | 1 | No puedo crear la semilla |
| WhatsApp, correo y teléfono del taller | 5 | Uso marcadores visibles |
| Fotos de trabajos y permiso de logos | 5, 9 | Imágenes de ejemplo con licencia libre |
| Revisión legal de privacidad, términos y consentimiento facial | 5, 6, 10 | Quedan como borradores marcados |
| Confirmar los diagramas de estados | 7 | No programo el flujo |
| Cuenta SMTP del taller | 11 | Probado solo contra el receptor local |
| ID de cliente OAuth de Google y clave de IA | 14 | Probado con simulador |
| Prueba manual con NVDA y prueba en Safari real | 15 | Queda el guion sin ejecutar |
| Permiso para subir la rama, y ejecutar el guion de verificación en un Codespace | 16 | Contenedores construidos pero sin evidencia de `docker compose up` |
| Hosting, dominio y certificado | 16 | Solo guía de despliegue |

## Verificación

En cada fase, desde la raíz del repositorio:
- Backend: `dotnet test .\backend\Proyecto-Carteles-herrera-synctime-main\LetrerosHerreraSYNCTIMEapi.slnx -c Release`, sobre `localhost\SQLEXPRESS`
- Frontend, en `frontend/synctimefront`: `npm test`, `npm run lint`, `npm run build` y `npm run test:e2e`
- App en marcha sobre la base demo, recorrida en el navegador integrado a 360, 768 y 1440 px, con la consola sin errores.
- Al final (fase 17): la matriz de trazabilidad completa.

## Primer paso tras tu aprobación

Ejecuto **solo la Fase 0** (repositorio y documentos, sin tocar código de producto) y me detengo con su informe. Antes de mover el `.git` del backend te muestro el inventario de diferencias y el respaldo verificado.
