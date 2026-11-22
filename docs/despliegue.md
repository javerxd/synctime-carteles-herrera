# Despliegue de SyncTime

Estado: **fase 16B** (contenedores y ajustes del código de la 16A, más el pipeline de CI/CD de la 16B). La guía completa de la nube (Azure SQL, Render, Vercel, respaldos, rotación de claves, licencias) se escribe en la **16C**, cuando existan los servicios; lo que sigue es lo que ya está construido y lo que cada pieza necesita.

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
| `Correo__*`, `Google__ClientId`, `RostroFacial__ClaveCifrado`, `Bootstrap__Admin__*` | Como hasta ahora (README del backend) | según el caso |
| `PORT` | Puerto de escucha (Render lo entrega solo; por omisión 8080) | no |

Fuera de desarrollo, la API **no arranca** si falta `Cors__AllowedOrigins__0` o `Sitio__UrlPublica`, o si están mal escritos: es preferible a un sitio que parece funcionar y falla en el navegador.

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

## 7. Pendiente de la 16C

Azure SQL, Render, Vercel, primera ejecución de `migrar.yml` y `respaldo.yml`, restauración probada en `localhost\SQLEXPRESS`, carga y Lighthouse contra la nube, IP real del visitante con Vercel → Render, guía completa de despliegue e inventario de licencias.
