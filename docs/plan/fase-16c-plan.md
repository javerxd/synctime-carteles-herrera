# Plan de la fase 16C — Despliegue y operación en la nube (Azure SQL, Azure Blob Storage, Render y Vercel)

Fecha: 22-11-2026 · Estado: **propuesto, esperando tu aprobación; construcción sin iniciar** (se hará en una **sesión nueva**, con el prompt de `fase-16c-prompt.md`) · Base: la 16A y la 16B, **aprobadas**; la 16B dejó el pipeline en verde con evidencia real (ejecuciones #3 a #10, `docs/evidencias/`).

Origen: `fase-16-plan.md` (sección 5), tus decisiones D1 a D7 para costo $0 del 22-11-2026, `informes/fase-16b.md`, `desviaciones.md` (secciones 16A y 16B) y `docs/despliegue.md`. Requisitos: **RNFP04, RNFP09, RNFP20, RNFE03, RNFE09, RNFO16, RNFO17, S22**, más los pendientes que arrastran RNFO15 (primera ejecución de `migrar.yml` y `respaldo.yml`), RNFP25, RNFP29 y RNFP01 (carga contra la nube).

**Reglas de la fase, todas con parada tuya:** no se crea **ninguna** cuenta, servicio, base, proyecto ni recurso sin que me digas que sí **en ese momento** y por cada uno; **no escribo ni veo credenciales** (las cargas tú en cada panel; yo solo sé sus nombres); no se aplica ninguna migración a una base real sin respaldo previo y tu permiso; no hago commit ni push (te paso los comandos); cada parada dice qué se crea, dónde, con qué plan, cuánto cuesta y cómo se borra.

## 0. Cómo se divide (D14, recomendada)

Tres partes, cada una con informe y parada. La primera **no toca la nube**:

| Parte | Qué es | Toca la nube | Necesita de ti |
|---|---|---|---|
| **16C-1** | **Código y archivos para la nube gratuita**: almacén de archivos en Blob, correo por HTTPS, tolerancia a la pausa de la base, pantalla de «despertando», `render.yaml` gratuito. Pruebas primero, CI en verde. | No | Decisiones D8 a D17 |
| **16C-2** | **Azure**: presupuesto y alertas, SQL Database gratuita, Blob Storage, identidad para GitHub (OIDC), entornos de GitHub, primera migración, primer respaldo y **restauración probada**. | Sí (cada creación con tu permiso) | Cuenta de Azure, paneles, secretos |
| **16C-3** | **Render y Vercel**, enlaces (CORS, Google, correo), pruebas de humo, carga de 20 usuarios, Lighthouse, medición del despertar, guía, licencias e informe final. | Sí (cada creación con tu permiso) | Cuentas de Render y Vercel, paneles, Google |

## 1. Qué cambió desde el plan de la 16 (léelo: hay decisiones tuyas)

| # | Hallazgo | Por qué importa | Qué propongo |
|---|---|---|---|
| **H11** | **Render gratuito bloquea el correo por SMTP** (puertos 25, 465 y 587; [aviso oficial de Render](https://render.com/changelog/free-web-services-will-no-longer-allow-outbound-traffic-to-smtp-ports), septiembre de 2025). | **Gmail por SMTP no funciona en Render gratuito.** Sin correo no hay avisos al taller ni códigos de recuperación. Tu D3 ya preveía este caso: «cambiar a un proveedor por HTTPS». | Enviador por HTTPS detrás de `IEnviadorCorreo` (el SMTP queda para compose). **Decisión D8**: qué proveedor (sección 2). |
| **H12** | **Los archivos viven en disco** (adjuntos privados, imágenes de la vitrina, reportes) y Render gratuito **no tiene disco persistente**. Hoy hay 3 almacenes (`AlmacenAdjuntos`, `ProcesadorImagenes`, `AlmacenExportaciones`) y al menos 4 rutas que devuelven `Results.File(ruta, …)`. | Sin esto, cada reinicio borra los adjuntos de los clientes. Tu D1 ya eligió Blob. | Interfaz `IAlmacenArchivos` con dos implementaciones (disco para desarrollo y compose; Blob para la nube), rutas que devuelven flujos, purgas por antigüedad adaptadas, un solo paquete nuevo (`Azure.Storage.Blobs`, tu D7) y pruebas contra **Azurite** (emulador oficial de Microsoft, en un contenedor de la CI). |
| **H13** | **Render gratuito se duerme a los ~15 min sin tráfico y despertar tarda alrededor de un minuto**; además detiene los procesos internos (cola de correo, exportaciones, purgas) y tiene poca memoria (512 MB y fracción de CPU: la API con SkiaSharp y PDF puede quedarse corta). A verificar en la documentación de Render al crear el servicio. | RNFP04 (99 % de disponibilidad) no se cumple con un servicio que duerme, y los avisos al taller se retrasarían hasta que alguien visite el sitio. Tu D2 ya acepta esto como límite **documentado**. | Documentarlo en `despliegue.md` y la nota de disponibilidad. **D9**: ¿monitor externo gratuito que lo despierte cada 5 a 10 minutos? **D16**: qué hacer si 512 MB no alcanza. |
| **H14** | **La API se conecta a la base al arrancar** (`Program.cs:357` revisa las migraciones pendientes). Con Azure SQL gratuita en pausa, ese primer contacto puede fallar y la API no levantar. Y hay **doble arranque en frío**: Render dormido + base en pausa = la primera visita del día puede tardar más de un minuto (y la reescritura de Vercel podría cortarla). | Errores 502/504 aleatorios para quien llega primero. | (a) Que el arranque **tolere** una base en pausa (reintenta y sigue, no cae), con prueba. (b) Pantalla «el sistema está despertando» con reintento automático en el frontend. (c) Medir el tiempo real y documentarlo. |
| **H15** | **Azure SQL gratuita**: 100.000 vCore-segundos y 32 GB al mes por base; al agotarse hay que elegir **«pausar hasta el mes siguiente»** (cuesta $0 pero la base queda inaccesible) o **«seguir con cargos»** (según [Microsoft](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer)). Es **incompatible con la oferta «Azure for Students Starter»**. | Si tu cuenta es de estudiante institucional, puede no servir. Y hay que elegir qué pasa al agotar la cuota. | **D10**: recomiendo pausar hasta el mes siguiente. Al crear la cuenta se comprueba el tipo de suscripción **antes** de crear nada. |
| **H16** | **Azure siempre pide tarjeta**, aunque uses capas gratuitas; todo uso fuera de esas capas se cobra ahí. **Blob Storage**: los 5 GB gratuitos son de la cuenta gratuita de Azure **por 12 meses**; después cuesta centavos por GB (a verificar con la herramienta de precios al crear). | Tu objetivo es $0: hay que acotar el riesgo. | **D13**: presupuesto de **1 USD con alertas** antes de crear cualquier recurso, un solo grupo de recursos, nada fuera de la lista, y revisión del consumo al final. Lo del año de Blob se te informa con cifras. |
| **H17** | **Vercel «Hobby»** es para uso **no comercial**; las reescrituras a un origen externo están permitidas. | El taller es un negocio real, aunque el proyecto sea académico. | **D11**: lo tienes que saber y aceptar; yo no puedo decidir por ti. Alternativa si no: pasar el frontend a otro hosting estático gratuito. |
| **H18** | **Render puede construir desde el repositorio** (como ya dice `render.yaml`) **o tomar la imagen de GHCR** (privada: exige un token de lectura que tendrías que crear). | Menos credenciales = menos riesgo, pero la imagen de Render sería otra construcción (mismo Dockerfile y commit) y no el binario exacto que probó la CI. | **D12**: recomiendo que Render construya desde el repositorio. |
| **H19** | **La base de datos de Azure empieza vacía.** La local `LetreriaLosHerreraDB` solo tiene tu administrador y su rostro (3 migraciones). El rostro está cifrado con una clave: la de producción será otra. | Hay que decidir si se traen datos. | **D17**: recomiendo **empezar vacía**: las migraciones ya incluyen el catálogo real (16 productos), el primer administrador sale de `Bootstrap__Admin__…` y registras tu rostro de nuevo. |
| **H20** | **Pendientes que ya conocías**: IP real del visitante con Vercel → Render (`ForwardedHeaders`, valores provisionales), nombre real de la URL de Render en `vercel.json`, región, y el **ID de Google** autorizado para el dominio de Vercel (H10). | Sin medirlos, los límites de tasa o el ingreso con Google fallan. | Se miden y ajustan en la 16C-3, con pasos exactos para Google. |

## 2. Decisiones que necesito de ti (te las pregunto una a una al empezar la 16C-1)

| # | Decisión | Mi recomendación |
|---|---|---|
| **D8** | **Proveedor de correo por HTTPS** (Render gratuito no deja SMTP). Opciones: **(a) Brevo**, API gratuita con unos 300 envíos al día y remitente verificado por correo, sin dominio propio; **(b) Resend**, 100 envíos al día pero **solo entrega a tu propio correo hasta que verifiques un dominio** (un dominio cuesta unos USD 10 al año: rompe el $0); **(c) Azure Communication Services Email**, dominio gratuito de Azure, cobro por envío de centavos (a medir); **(d) subir Render a plan de pago** (SMTP permitido, ~USD 7/mes). | **(a) Brevo**, y la 16C-1 **verifica con su documentación** si aguanta enviar desde una dirección de Gmail (la entregabilidad es el riesgo: puede caer en spam). Si no, (c). |
| **D9** | **Monitor externo** gratuito (por ejemplo UptimeRobot, cada 5 a 10 min a `/salud`) para que Render no se duerma. Mantener un servicio despierto 24/7 cuesta unas 744 de las ~750 horas gratuitas del mes. No despierta la base (`/salud` no la consulta). | **Sí**: mejora mucho RNFP04 y deja correr la cola de correo. Es un servicio externo más con tu correo. |
| **D10** | Qué hace la base de Azure al **agotar la cuota gratuita**. | **Pausar hasta el mes siguiente** (garantiza $0). |
| **D11** | Aceptar los términos de **Vercel Hobby** (no comercial) para este proyecto. | Informado: tu decisión. |
| **D12** | **Render construye desde el repositorio** o usa la imagen privada de GHCR. | **Desde el repositorio** (sin tokens). |
| **D13** | Aceptar **tarjeta en Azure** con presupuesto de 1 USD, alertas y revisión del consumo. | Sí. |
| **D14** | Dividir en **16C-1, 16C-2 y 16C-3** con parada en cada una. | Sí. |
| **D15** | **Nombres y regiones**: prefijo `synctime-`, grupo de recursos único, Render `virginia`, Azure en una región de la costa este de EE. UU. donde exista la oferta gratuita de SQL (se verifica al crear). | Se confirman en cada parada. |
| **D16** | **Plan B si 512 MB no alcanza** en Render gratuito: subir a un plan de pago pequeño, o mover la API a **Azure Container Apps** (tiene cuota gratuita mensual de cómputo; sería un cambio de despliegue, no de código). | Decidir con la **medición** de memoria, no antes. |
| **D17** | Base de Azure **vacía** (recomendado) o traer datos de la local. | Vacía. |

## 3. Lo que hay que hacer **antes** de la sesión nueva (paso 0, tuyo)

1. **Fusionar la rama a `main`**: en GitHub, **Compare & pull request** de `fase-16-contenedores-nube` → `main`, esperar la CI en verde y **Merge pull request** (con commit de fusión, para conservar el historial). **Por qué es obligatorio:** `migrar.yml` y `respaldo.yml` solo corren desde `main` (revisión de seguridad de la 16B), el respaldo programado solo corre desde la rama por omisión, y la imagen del migrador la publica la CI al subir a `main`. La fusión dispara la **primera publicación real en GHCR** (imágenes privadas): me dices cuando termine.
2. **Proteger `main`** (Settings → Branches → Add rule: exigir pull request y que pase el check de la CI) y, si quieres, una regla de protección de etiquetas `v*` (Settings → Tags). Es lo que la revisión de seguridad recomendó y que ningún archivo puede garantizar.
3. Abrir la **sesión nueva** con el prompt de `fase-16c-prompt.md` y la rama `fase-16c-nube-1` nacida de `main` (la sesión te da el comando).

## 4. 16C-1 — Código y archivos para la nube gratuita (sin nube)

Pruebas primero (xUnit y Vitest), fallos a propósito, CI en verde, revisiones de ECC (`csharp-reviewer`, `security-reviewer`, `database-reviewer`, `react-reviewer`, `docker-patterns`), informe `informes/fase-16c-1.md` y parada.

1. **Almacén de archivos** (H12): `IAlmacenArchivos` (guardar, abrir como flujo, borrar, listar por antigüedad); implementación en disco (la de hoy, para desarrollo y compose) y en Blob (`Azure.Storage.Blobs`, contenedor privado `archivos`, sin acceso público, nombres sin datos del cliente); las 4 rutas que hoy devuelven `Results.File(ruta…)` pasan a flujos con los mismos permisos y cabeceras; las purgas (exportaciones vencidas, adjuntos) usan la fecha del objeto; `Almacen__Modo` = `Disco` o `Blob` (con validación al arrancar, como `ConfiguracionDespliegue`). Pruebas de integración contra **Azurite** en un contenedor de la CI (versión fija) y las mismas pruebas contra el disco. La suite local sigue pasando sin Azurite: **las pruebas de Blob se omiten solo con una variable explícita y la CI las exige** (el piso de pruebas de `ci-resumen.py` sube).
2. **Correo por HTTPS** (H11, D8): `EnviadorHttp<Proveedor>` detrás de `IEnviadorCorreo`, `Correo__Modo` con el valor nuevo, sin dependencias (`HttpClient`), la clave en una variable (`Correo__Api__Clave`), reintentos acotados, y `/salud/correo` que lo refleje. Pruebas con un servidor simulado (nunca el proveedor real).
3. **Tolerancia a la base en pausa** (H14): el arranque en producción reintenta la revisión de migraciones pendientes con espera y **no cae** si la base despierta tarde; `/salud` sigue sin tocar la base y `/salud/lista` la despierta. Prueba con una base inalcanzable.
4. **Frontend**: pantalla/aviso «el sistema está despertando…» con reintento automático ante 502/503/504 de la primera petición, accesible (región `aria-live`), con Vitest y axe.
5. **`render.yaml` gratuito** (D2, D1, D12): `plan: free`, sin disco, construcción desde el repositorio, variables nuevas (`Almacen__Modo`, `Correo__Api__Clave`, cuenta de Blob) con `sync: false`; `validar-contenedores.py` y su prueba se ajustan (la regla del disco desaparece; la de secretos y `autoDeployTrigger: off` se mantienen).
6. **Documentación**: `despliegue.md` (almacén, correo, pausa, despertar) y `desviaciones.md`.

**Parada de la 16C-1:** informe y tu visto bueno. Aún no existe ningún recurso en la nube.

## 5. 16C-2 — Azure (cada creación, con tu permiso)

Antes de empezar, la sesión **comprueba** (solo lectura, con el conector de Azure si está disponible, o con tus capturas) el **tipo de suscripción** y las **regiones con la oferta gratuita de SQL**. Cada paso de creación sigue este molde, y **yo me detengo y te pregunto**:

> **PARADA DE CREACIÓN** — Qué se crea · dónde y con qué nombre · plan o nivel · costo esperado ($0 o cuánto) · cómo se borra · qué necesito de ti (clics exactos o datos que **tú** escribes en el panel). ¿Apruebo crear esto?

Orden:

1. **Cuenta y presupuesto** (tuyo): iniciar sesión en el portal de Azure, comprobar la suscripción, crear un **presupuesto de 1 USD con alertas a tu correo** (D13). Nada más se crea antes de esto.
2. **Grupo de recursos** `synctime-rg` (parada). Un solo grupo: borrarlo borra todo.
3. **Servidor SQL y base gratuita** (parada, D10, D15): oferta gratuita, serverless, **auto-pausa hasta el mes siguiente al agotar**, cifrado en tránsito, autenticación de SQL con un administrador cuya clave **escribes tú** (yo no la veo). **Cortafuegos**: sin «permitir servicios de Azure» abierto de par en par; solo reglas temporales (las abren y cierran los workflows) y la tuya para migrar si hiciera falta.
4. **Usuarios de la base** (parada): uno **de la aplicación** (permisos mínimos: leer y escribir datos, sin DDL), uno **de migración** (DDL) y uno **de respaldo** (solo lectura). Las contraseñas las generas tú y las cargas tú.
5. **Cuenta de almacenamiento y contenedores** (parada, D1, H16): cuenta de uso general v2, redundancia local, **sin acceso público**; contenedores privados `archivos` y `respaldos`; **regla de ciclo de vida que borra a los 30 días** en `respaldos/diario/` (defensa extra de RNFP09) y, si Azure lo ofrece, **eliminación lógica** para que un workflow comprometido no pueda borrar los respaldos de golpe.
6. **Identidad para GitHub, sin contraseñas** (parada): registro de aplicación con **credencial federada (OIDC)** para los entornos `azure-sql-produccion` y `azure-respaldo` de GitHub, con permisos mínimos (abrir y cerrar reglas del cortafuegos del servidor SQL; **Storage Blob Data Contributor** solo en la cuenta de almacenamiento). Una identidad por entorno.
7. **Entornos y secretos de GitHub** (tuyo, con mis pasos exactos): `azure-sql-produccion` (revisor: tú; ramas: solo `main`) y `azure-respaldo` (ramas: solo `main`), con los secretos y variables de `docs/despliegue.md` sección 6. Los valores los escribes tú.
8. **Primera migración** (parada, **la más delicada**): `migrar.yml` desde `main` con el commit exacto, aprobación tuya en el entorno, **respaldo previo a Blob y verificación**, migrador (la imagen que la CI probó), comprobación. Incluye la **migración de la hora de Chile** (16A) y la carga del catálogo real. Primera ejecución real de un workflow que nunca corrió: puede pedir ajustes (sintaxis de `sqlpackage` y `az`, variables que el migrador exija).
9. **Primer respaldo y rotación** (parada): `respaldo.yml` a mano; se comprueba el archivo en el contenedor.
10. **Restauración probada de principio a fin** (RNFP09, RNFP20): se baja el `.bacpac`, se importa en **`localhost\SQLEXPRESS` con otro nombre** (nunca sobre `LetreriaLosHerreraDB`), se compara contra Azure (tablas, conteos y la última migración) y se documenta el procedimiento paso a paso, con tiempos.

**Parada de la 16C-2:** informe `informes/fase-16c-2.md`, consumo de Azure visto en el portal y tu visto bueno.

## 6. 16C-3 — Render, Vercel y cierre (cada creación, con tu permiso)

1. **Render** (parada, D2, D12, D15, D16): crear el servicio desde el Blueprint de `render.yaml` (plan gratuito, `virginia`, construcción desde el repositorio, `autoDeployTrigger: off`); **tú escribes** en el panel las variables con `sync: false` (cadena de conexión del usuario **de la aplicación**, `Jwt__Key` nueva y larga, `RostroFacial__ClaveCifrado` nueva, `Bootstrap__Admin__…`, clave del proveedor de correo, cuenta de Blob con su método de acceso). Primer despliegue manual; se **copia la URL real** que Render asigne (si el nombre `synctime-api` ya existe, Render agrega un sufijo: **no se usa una URL que no sea la tuya**).
2. **Medición de memoria** con la carga (D16): si 512 MB no alcanza, **me detengo y te pregunto** (plan de pago pequeño o Azure Container Apps).
3. **Vercel** (parada, D11): proyecto del frontend desde el repositorio, variables de construcción (`VITE_GOOGLE_CLIENT_ID`; el ID de Google **no se escribe en el repositorio**), `vercel.json` con la URL **real** de Render (cambio de un valor, con tu commit) y dominio `*.vercel.app` con certificado válido (**RNFE03**).
4. **Enlaces** (tuyo, con mis pasos exactos): `Cors__AllowedOrigins__0` y `Sitio__UrlPublica` con el origen real de Vercel; en la **consola de Google Cloud**, el origen de Vercel en «Orígenes de JavaScript autorizados» del ID de cliente (H10); remitente del correo.
5. **IP real del visitante** (H20): entrar al sitio de Vercel, mirar la IP que guardó Auditoría y ajustar `ForwardedHeaders__RedesDeConfianza` y `LimiteDeSaltos` al número **exacto** de saltos medidos (más saltos de los reales = confiar en cabeceras que escribe un atacante).
6. **Pruebas de humo en la nube**: `/salud`, `/salud/lista`, portada, ingreso (correo y Google), crear una solicitud con adjunto (queda en **Blob**), exportar un reporte, un correo real de aviso, cerrar sesión.
7. **Carga de 20 usuarios contra Render** (RNFP01, `scripts/carga/` apuntando a la URL; ver el límite de la capa gratuita) y **Lighthouse contra la URL de Vercel**.
8. **Despertar medido** (H13/H14): tiempo de la primera visita con Render dormido y con la base en pausa; con y sin monitor (D9). Es la base de la **nota de disponibilidad (RNFP04)**: qué se cumple, qué no y por qué (capa gratuita).
9. **Documentación**: guía de despliegue completa (HTTPS, dominio, CORS por entorno, rotación de claves, cómo restaurar, cómo apagar y borrar todo), **inventario de licencias** de paquetes, fuentes, iconos e imágenes con su **vista pública** (RNFO17, RNFE09, S22) y eliminación de dependencias sin uso (RNFO16).
10. **Cierre**: revisiones de ECC, informe `informes/fase-16c-3.md` e informe global de la 16C, tablero y trazabilidad (RNFP04, RNFP09, RNFP20, RNFE03, RNFE09, S22, RNFO15, RNFO16, RNFO17, RNFP25, RNFP29, RNFP01) con evidencia en `docs/evidencias/`, y parada.

## 7. Pruebas, fallos a propósito y revisiones

- **Pruebas primero** en todo el código (16C-1): almacén (disco y Azurite), correo HTTPS con servidor simulado, arranque con base inalcanzable, pantalla de despertar, validadores.
- **Fallos a propósito**: un contenedor de Blob que fuera público (debe fallar la regla), una variable de Blob faltante en producción (la API no arranca, como CORS), un adjunto que se pierde al reiniciar el contenedor (debe sobrevivir con Blob), el proveedor de correo caído (la cola reintenta y no pierde el aviso), un secreto en `render.yaml` (el validador lo detiene).
- **Revisiones de ECC**: `csharp-reviewer`, `security-reviewer` (la migración, el respaldo y la identidad son lo más sensible), `database-reviewer`, `react-reviewer`, `docker-patterns` y `performance-optimizer` (carga y Lighthouse).
- **Cada ejecución real** de `migrar.yml`, `respaldo.yml` y la CI se lee con `gh run view` y queda como evidencia.

## 8. Riesgos y planes B

| Riesgo | Plan B |
|---|---|
| La oferta gratuita de SQL no está disponible en tu suscripción o región | Parada y decisión: otra región, otra suscripción o pagar la base más chica (se te informa el costo) |
| El correo desde Gmail por HTTPS cae en spam | Probar el remitente verificado del proveedor; si no, Azure Communication Services Email (D8 c) |
| 512 MB de Render no alcanzan | D16: plan de pago pequeño o Azure Container Apps |
| La reescritura de Vercel corta la primera petición (>30 s) | Reintento automático en el frontend (16C-1) y monitor anti-sueño (D9) |
| `sqlpackage` o `az` rechazan algo de los workflows nunca ejecutados | Se corrige en la 16C-2 con la evidencia del registro (como en la 16B) |
| Se agota la cuota gratuita de SQL antes de fin de mes | D10: la base se pausa hasta el mes siguiente; se avisa; revisar el consumo con la carga |
| Hay que deshacer todo | Borrar el grupo `synctime-rg`, el servicio de Render y el proyecto de Vercel; los respaldos están en la cuenta de almacenamiento (se descargan antes) |

## 9. Lo que tienes que hacer tú (resumen)

1. Aprobar este plan y las decisiones D8 a D17.
2. Paso 0 de la sección 3 (fusionar a `main`, proteger `main`).
3. Abrir la sesión nueva con el prompt.
4. En cada parada de creación: decir sí o no, y **escribir tú** las contraseñas y claves en los paneles (nunca en el chat).
5. Entrar a Azure, Render, Vercel y Google Cloud cuando te lo pida con los pasos exactos de ese momento.
6. Los commits y los push: yo te paso los comandos.
