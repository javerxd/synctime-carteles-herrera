# Despliegue de SyncTime

Estado: **fase 16C-1** (el código y los archivos para la nube gratuita, sin crear ningún servicio), sobre los contenedores de la 16A y el pipeline de la 16B. La guía completa de la nube (Azure SQL, Render, Vercel, respaldos, rotación de claves, licencias) se escribe en la **16C-3**, cuando existan los servicios; lo que sigue es lo que ya está construido y lo que cada pieza necesita.

## 1. Todo el proyecto en cualquier equipo con Docker (compose)

Pensado para que cualquier persona o evaluador clone el repositorio y lo levante sin instalar .NET, Node ni SQL Server.

```bash
./scripts/crear-env.sh          # Windows (PowerShell): .\scripts\crear-env.ps1
docker compose up --build
```

- Sitio: <http://localhost:8080> · Correos de prueba (nada sale a internet): <http://localhost:8025>.
- El script crea `.env` con claves **al azar** (no se sube a git) y muestra una vez la clave de los usuarios de demostración. Usuarios: `dueno`, `admin`, `disenador`, `operario` y `cliente`, todos `@letreria.test`.
- Orden de arranque: `sql` → `migrador` (aplica las migraciones y termina) → `semilla` (datos inventados y termina) → `api` → `web` (nginx). `docker compose down -v` borra los datos.
- Solo se publican el sitio y el buzón de correo, y solo en `127.0.0.1`. La base de datos y la API no son accesibles desde fuera.
- La imagen de SQL Server es solo para amd64 (en Apple Silicon corre emulada) y pide unos 2 GB de memoria libre.
- **Verificación**: `./scripts/verificar-compose.sh` (en un Codespace; el equipo de desarrollo no tiene Docker). Deja la evidencia en `docs/evidencias/compose-<fecha>.md`.
- **Validación estática** (sin Docker): `python scripts/validar-contenedores.py` y `python scripts/test_validar_contenedores.py` (necesitan PyYAML).

## 2. Variables de entorno de la API

Los nombres son los de .NET (`__` separa niveles). Los valores **nunca** se escriben en archivos del repositorio.

| Variable | Qué es | Obligatoria |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Cadena de conexión. En Azure SQL: `Encrypt=True;Connect Timeout=60` (la base gratuita se pausa sola) | sí |
| `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience` | Firma de las sesiones | sí |
| `Cors__AllowedOrigins__0` | Origen del sitio, sin barra final, en minúsculas (`https://….vercel.app`) | sí fuera de desarrollo |
| `Sitio__UrlPublica` | Dirección del sitio para los enlaces de los correos y el QR | sí fuera de desarrollo |
| `ForwardedHeaders__Habilitado`, `…__RedesDeConfianza__0`, `…__ProxiesDeConfianza__0`, `…__LimiteDeSaltos` | Qué proxies pueden decir la IP y el protocolo reales (ver 3) | sí detrás de un proxy |
| `BaseDeDatos__ReintentosMaximos` (5), `…__RetardoMaximoSegundos` (30) | Reintentos ante fallos transitorios de Azure SQL; `0` los apaga | no |
| `BaseDeDatos__ArranqueMinutosMaximos` (30), `…__ArranqueEsperaInicialSegundos` (5), `…__ArranqueEsperaMaximaSegundos` (60) | Cuánto insiste, y con qué espera, el arranque en segundo plano mientras la base despierta (ver 7) | no |
| `Almacen__Modo` | `Disco` (compose y desarrollo) o `Blob` (la nube: Azure Blob Storage). **Se declara siempre fuera de desarrollo**: sin él la API no arranca (ver 7) | sí fuera de desarrollo |
| `Almacen__Blob__CadenaDeConexion` | Cadena de conexión de la cuenta de almacenamiento (**secreto**: lleva la clave de la cuenta) | sí con `Blob` |
| `Almacen__Blob__Contenedor` (`archivos`), `…__TiempoEsperaSegundos` (30), `…__ReintentosMaximos` (3) | Contenedor **privado** y tiempos del SDK | no |
| `Correo__Modo` | `Smtp` (compose, desarrollo), `Api` (la nube: HTTPS) o `Bandeja` (solo desarrollo) | no |
| `Correo__Api__Clave` | Clave de la API de Brevo (**secreto**) | sí con `Api` |
| `Correo__Api__Proveedor` (`Brevo`), `…__Url` (`https://api.brevo.com/v3/smtp/email`), `…__UrlLogo`, `…__TiempoEsperaSegundos` (20) | Proveedor, dirección (solo https), logotipo y plazo de cada envío | no |
| `Correo__*`, `Google__ClientId`, `RostroFacial__ClaveCifrado`, `Bootstrap__Admin__*` | Como hasta ahora (README del backend) | según el caso |
| `PORT` | Puerto de escucha (Render lo entrega solo; por omisión 8080) | no |

Fuera de desarrollo, la API **no arranca** si falta `Cors__AllowedOrigins__0`, `Sitio__UrlPublica` o `Almacen__Modo`, si están mal escritos, o si `Almacen__Modo=Blob` no trae su cadena de conexión: es preferible a un sitio que parece funcionar y falla en el navegador (o que guarda los adjuntos de los clientes en un disco que se pierde).

## 3. Detrás de un proxy: IP y protocolo reales

Render, Vercel y el nginx de compose terminan el HTTPS y hablan HTTP con la API. Sin leer `X-Forwarded-For` y `X-Forwarded-Proto`, todos los visitantes comparten la IP del proxy (y su cupo de peticiones) y la redirección a HTTPS entra en bucle. Esas cabeceras las escribe cualquiera, así que solo se leen si vienen de un proxy **declarado**:

- `ForwardedHeaders__Habilitado=true` exige al menos un proxy o red de confianza; una red más ancha que `/8` o una dirección mal escrita impide arrancar.
- `LimiteDeSaltos` es cuántos proxies hay en la cadena (1 por omisión).
- **Pendiente de la 16C (medir, no suponer):** lo que llega a la API cuando se entra por Vercel → Render. Se comprueba entrando al sitio de Vercel y mirando la IP que guardó Auditoría: debe ser la del visitante, no la de Vercel ni la de Render. Si no lo es, se ajustan las redes y los saltos (los valores de `render.yaml` son provisionales).

## 4. Migraciones

La API **nunca** migra sola fuera de desarrollo (a propósito: con respaldo previo). Se aplican con el **migrador** (`efbundle`, imagen `--target migrador`), una vez, a mano:

```bash
docker build --target migrador -t synctime-migrador ./backend/Proyecto-Carteles-herrera-synctime-main
docker run --rm --env-file <variables> synctime-migrador
```

El migrador arma la aplicación para conocer el contexto, así que necesita las mismas variables que la API (cadena de conexión, `Jwt__*`, `Cors__*`, `Sitio__UrlPublica`). **Contra una base real: respaldo antes, con tu permiso.**

### Migración de hora de Chile (`20261122073024_HoraDeChileEnLaBase`)

Cambia el valor por defecto de 8 columnas de fecha (clientes, catálogo, cartel, solicitudes, cotizaciones, pedidos y pagos ×2) de `SYSDATETIME()` (hora local del servidor) a la hora de Chile calculada desde UTC, porque Azure SQL y los contenedores corren en UTC. No modifica ninguna fila. **Está escrita y probada, pero NO aplicada a ninguna base real ni a la demo:** queda para la 16C con respaldo y permiso.

> Aviso: la base de demostración de desarrollo (`…_Demo`) y las bases de pruebas **migran solas** al arrancar la API en desarrollo. Arrancar la API local contra `LetreriaLosHerreraDB_Demo` aplicará esta migración a esa base (no toca datos; se deshace con `dotnet ef database update 20261121161200_GoogleParaPersonal`). La base real (`LetreriaLosHerreraDB`) nunca migra sola.

## 5. Salud

- `/salud`: el proceso responde (no consulta la base). La usa el `HEALTHCHECK` de la imagen y Render.
- `/salud/lista`: además consulta la base. **No** se usa como chequeo periódico de Render: gastaría la cuota gratuita de Azure SQL y la mantendría despierta.
- `/salud/correo`: estado de los avisos por correo (degradado, nunca «fallo»).

## 6. Pipeline de CI/CD (fase 16B, RNFO15)

Tres workflows en `.github/workflows/`. Las reglas que cumplen (acciones fijadas por SHA, permisos mínimos, sin secretos, publicar solo si todo pasa) las comprueba `python scripts/validar-workflows.py` (57 pruebas de «fallo a propósito» en `scripts/test_validar_workflows.py`).

| Workflow | Cuándo corre | Qué hace |
|---|---|---|
| `ci.yml` | Cada subida de cualquier rama, etiquetas `v*` y a mano | `estatica` (validadores en Python), `backend` (compila, 749 unitarias y 768 de integración contra un SQL Server 2022 en contenedor con el reloj en UTC, vulnerabilidades de NuGet), `frontend` (lint, Vitest 981, build, presupuesto de peso, `npm audit`), `secretos` (gitleaks sobre todo el historial, con una autoprueba que planta un secreto), `imagenes` (construye API, migrador y frontend una sola vez, prueba lo básico y las analiza con Trivy), `compose` (`docker compose up` real con `verificar-compose.sh`) y `publicar` |
| `migrar.yml` | **Solo a mano** y con aprobación de una persona | Respaldo exportado a Blob Storage y verificado, migrador contra Azure SQL, comprobación de que la última migración del código es la de la base; el cortafuegos se cierra siempre |
| `respaldo.yml` | Todos los días 07:17 UTC y a mano | Exporta la base (`.bacpac`), la verifica, la sube al contenedor privado `respaldos` y borra lo de más de 30 días (nunca baja de 7 copias) |

**`publicar` solo corre si los otros siete pasaron** y solo en `main`, en etiquetas `v*` o en una ejecución manual con `publicar` marcado. Sube las **mismas** imágenes que se analizaron, a `ghcr.io/<usuario>/synctime-{api,migrador,web}`, con la etiqueta `sha-<12 primeros del commit>` (y la versión si es una etiqueta `vX.Y.Z`). **Nunca `latest`.** Es el único job con permiso de escribir paquetes.

### Pruebas de integración contra SQL Server con usuario y clave
`TEST_SQL_SERVER` indica el servidor. Sin más variables, todo sigue como siempre (Windows, `localhost\SQLEXPRESS`). Con `TEST_SQL_PASSWORD` (y `TEST_SQL_USER`, por omisión `sa`) se usa la autenticación de SQL Server, como en el pipeline. Un usuario sin clave es un error, no cae en Windows en silencio. La clave del pipeline se genera al azar en cada ejecución y se enmascara en el registro.

### Análisis y excepciones
- Vulnerabilidades: falla cualquier **alta o crítica con corrección publicada** (imágenes y dependencias; NuGet y npm con sus propias herramientas). Una excepción va en `.trivyignore` con motivo y **fecha de vencimiento** (`exp:AAAA-MM-DD`), y se anota en `docs/plan/desviaciones.md`.
- Secretos: `.gitleaks.toml` extiende las reglas por omisión; las excepciones se documentan ahí.
- Binarios descargados (gitleaks, Trivy): versión fija y suma SHA-256 verificada. Se eligió bajar los binarios y no usar `aquasecurity/trivy-action` por el incidente de marzo de 2026 con sus etiquetas.
- `.github/dependabot.yml` propone cada mes los SHA nuevos de las acciones y las imágenes base de los Dockerfile (no toca NuGet ni npm).

### Qué hay que configurar en GitHub (lo haces tú, en la 16C)
En **Settings → Environments** crear dos entornos:
- `azure-sql-produccion`: **Required reviewers** (tú) y **Deployment branches** solo `main`. Aquí viven los secretos de la migración.
- `azure-respaldo`: sin aprobación (el respaldo es automático), con su propia identidad de Azure y **Deployment branches solo `main`** (si no, cualquiera con escritura podría crear una rama que pida ese entorno y leer la base).

Secretos y variables (solo **nombres**; los valores los cargas tú y nunca pasan por el repositorio):

| Entorno | Secretos | Variables |
|---|---|---|
| `azure-sql-produccion` | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_SQL_MIGRACION_USUARIO`, `AZURE_SQL_MIGRACION_PASSWORD` | `AZURE_RESOURCE_GROUP`, `AZURE_SQL_SERVIDOR`, `AZURE_SQL_BASE`, `AZURE_STORAGE_ACCOUNT`, `SITIO_ORIGEN`, `SITIO_URL_PUBLICA` |
| `azure-respaldo` | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_SQL_RESPALDO_USUARIO`, `AZURE_SQL_RESPALDO_PASSWORD` | `AZURE_RESOURCE_GROUP`, `AZURE_SQL_SERVIDOR`, `AZURE_SQL_BASE`, `AZURE_STORAGE_ACCOUNT` |

Azure se inicia con **OIDC** (credencial federada, sin contraseña guardada) y Blob Storage con ese mismo inicio de sesión (`--auth-mode login`, sin llaves de cuenta). Los workflows de migración y respaldo **están escritos y validados de forma estática, pero no se han ejecutado**: su primera ejecución es de la 16C.

### Cómo se usa la migración (16C)
1. **El commit debe estar en `main`** (los workflows de migración y respaldo no corren desde otra rama: ni siquiera piden la aprobación) y la CI en verde sobre él, de modo que la imagen del migrador de ese commit ya esté en GHCR. El migrador comprueba que la imagen descargada esté construida con ese mismo commit (etiqueta `org.opencontainers.image.revision`).
2. **Actions → Migrar la base de datos → Run workflow**, desde `main`; escribir el sha completo y `MIGRAR`.
3. Aprobar el entorno cuando GitHub lo pida. Revisar en el resumen el respaldo guardado y la migración comprobada.
4. Si algo sale mal: el respaldo previo está en `respaldos/premigracion/` y se restaura con `sqlpackage /Action:Import`.

### Verificación real de compose en un Codespace
```bash
bash scripts/verificar-compose.sh --limpiar      # levanta todo, recorre el sitio, deja docs/evidencias/compose-<fecha>.md y apaga
bash scripts/fallos-a-proposito.sh               # imagen de la API sin tzdata: debe negarse a arrancar
```
Los guiones se llaman con `bash` porque git guarda los `.sh` sin bit de ejecución.

## 7. La nube gratuita (fase 16C-1)

Render gratuito cambia tres cosas del diseño original; las tres están resueltas **en el código** (sin crear ningún servicio todavía):

### Archivos de los clientes: disco o Azure Blob Storage (H12)

Adjuntos privados, imágenes de la vitrina y reportes exportados pasan por una interfaz (`IAlmacenArchivos`) con dos implementaciones que cumplen **las mismas pruebas de contrato**:

- `Almacen__Modo=Disco`: las carpetas de siempre (`Adjuntos__Carpeta`, `Imagenes__Carpeta`, `Reportes__Carpeta`), escribiendo en un temporal y renombrando. Es lo que usan compose y el desarrollo.
- `Almacen__Modo=Blob`: **un único contenedor privado** (`archivos`) con tres prefijos (`adjuntos/`, `imagenes/`, `exportaciones/`). El nombre del archivo es el mismo `{32 hex}.ext` aleatorio de siempre: ningún dato del cliente va en la dirección. Si el contenedor existe con acceso público (anónimo), la API **se niega a usarlo**; si no existe, lo crea privado. No hay direcciones firmadas ni enlaces directos: los archivos solo salen por la API, que sigue exigiendo sesión y permiso.
- Las descargas son **flujos** (`Results.Stream`): la API no entrega rutas de disco. Las purgas (adjuntos sin registro, exportaciones vencidas) usan la fecha del objeto.
- Render gratuito **no tiene disco persistente**: con `Disco` los archivos se perderían en cada reinicio. Por eso `render.yaml` declara `Almacen__Modo=Blob` y el validador exige que así sea.
- Las pruebas de Blob corren contra **Azurite** (el emulador oficial de Microsoft, `3.38.0`, en un contenedor de la CI), nunca contra la nube. En un equipo sin Azurite se omiten **solo** con `SYNCTIME_OMITIR_BLOB=1`; la CI no lo define y las exige (`validar-workflows.py` lo vigila).
- Paquete nuevo: solo `Azure.Storage.Blobs` (decisión D7).

### Correo por HTTPS (H11, D8)

Render gratuito **bloquea los puertos SMTP 25, 465 y 587**. Con `Correo__Modo=Api` los correos salen por la API transaccional de **Brevo** (`POST https://api.brevo.com/v3/smtp/email`, clave en la cabecera `api-key`), detrás de la misma interfaz (`IEnviadorCorreo`) y de la misma cola con reintentos que el SMTP. Lo que se sabe de Brevo (documentación oficial):

- **No admite imágenes incrustadas (`cid:`)**: el logotipo se pide por una dirección pública (`Correo__Api__UrlLogo`, o por omisión `Sitio__UrlPublica` + `/marca/icono-192.png`); sin dirección, se quita la imagen y el correo sale igual.
- **No se puede autenticar (DKIM/DMARC) un remitente `@gmail.com`**: Brevo cambia esa dirección por una propia para cumplir las reglas de Gmail. Es el riesgo de entregabilidad previsto en D8 (puede caer en spam). Lo ideal es un remitente de un dominio propio; si no se tiene, queda como **límite documentado** y el plan B es Azure Communication Services Email. El remitente (`Correo__Remitente__Direccion`) debe estar **verificado en Brevo** y ser distinto del contacto público del taller.
- **Sin clave de idempotencia**: si la respuesta se pierde después de aceptado el envío, el reintento podría entregar el aviso dos veces. Se prefiere a perderlo.
- Los fallos se clasifican solo por su código HTTP (nunca se guarda el cuerpo del error, que repite el correo del destinatario): 401/403 y 429/5xx detienen el lote y se reintentan sin agotarse; 400 cuenta intentos. `/salud/correo` queda «degradado» si falta la clave.
- El cliente HTTP **no sigue redirecciones** (la clave viaja en una cabecera propia que .NET no retira al redirigir) y `Correo__Api__Url` solo admite `https` (o `http` de localhost, para el servidor simulado de las pruebas). No se hace ninguna llamada real al proveedor en las pruebas.

### Base en pausa y servicio dormido (H13, H14, D10)

- Azure SQL gratuita se **pausa sola** (y, al agotar la cuota, hasta el mes siguiente: D10). Render gratuito **se duerme tras ~15 minutos sin tráfico y despertar tarda cerca de un minuto**, y mientras duerme no corren la cola de correo, las exportaciones ni las purgas. **Límite documentado de RNFP04** (99 % de disponibilidad): no se cumple con un servicio que duerme; D9 propone un monitor externo gratuito (se crea en la 16C-3, con permiso) que lo despierte cada 5 a 10 minutos y que **no** despierta la base (`/salud` no la consulta).
- **Arranque tolerante.** Antes, la API se conectaba a la base al arrancar y con la base dormida no levantaba. Ahora, fuera de desarrollo, la revisión de migraciones pendientes, la lectura de los destinatarios de los avisos y la creación del primer administrador corren **en segundo plano**: el puerto se abre de inmediato, `/salud` responde y `/salud/lista` (que sí consulta la base) la despierta. Los pasos reintentan con espera creciente (5, 10, 20, 40 y 60 s) hasta `BaseDeDatos__ArranqueMinutosMaximos` (30); si no despierta, lo registra (solo el tipo del error) y la API **sigue en pie**. Una configuración inválida (por ejemplo la clave del primer administrador) sigue impidiendo arrancar, porque no necesita la base para saberlo.
- **Sitio.** Si el sitio recibe un 502, 503 o 504 **sin el formato de error de la API** (lo que responden Vercel o Render dormido), repite las **lecturas** solo (3, 4, 5, 6, 8 y 10 s…, unos 66 s en total) y muestra «El sistema está despertando» (región viva para lectores de pantalla). Los **envíos** (POST, PUT, PATCH, DELETE) no se repiten solos: no se sabe si el servidor alcanzó a procesarlos y podrían duplicarse. Un error de la API con su código (por ejemplo `google_no_disponible`) no se confunde con un servidor dormido.
- **Pendiente de medir en la 16C-3**: el tiempo real del despertar (Render dormido + base en pausa) y si 512 MB de memoria alcanzan (D16).

## 8. Pendiente de la 16C-2 y la 16C-3

**16C-2 (Azure):** presupuesto y alertas, SQL Database gratuita, Blob Storage (contenedores `archivos` y `respaldos`, ciclo de vida de 30 días), identidad de GitHub (OIDC), entornos de GitHub, primera ejecución de `migrar.yml` y `respaldo.yml` y restauración probada en `localhost\SQLEXPRESS`. **16C-3 (Render y Vercel):** crear los servicios desde `render.yaml`, enlaces (CORS, Google, correo), IP real del visitante con Vercel → Render, pruebas de humo, carga de 20 usuarios y Lighthouse contra la nube, tiempo de despertar medido, guía completa de despliegue e inventario de licencias.
