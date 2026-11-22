# Despliegue de SyncTime

Estado: **fase 16A** (archivos de contenedores y ajustes del código). La guía completa de la nube (Azure SQL, Render, Vercel, respaldos, rotación de claves, licencias) se escribe en la **16C**, cuando existan los servicios; lo que sigue es lo que ya está construido y lo que cada pieza necesita.

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

## 6. Pendiente de la 16B y la 16C

16B: pipeline de CI/CD y verificación de `docker compose up` en un Codespace. 16C: Azure SQL, Render, Vercel, respaldo diario de 30 días con restauración probada, carga y Lighthouse contra la nube, guía completa e inventario de licencias.
