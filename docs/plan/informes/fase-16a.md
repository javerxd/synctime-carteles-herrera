# Informe de cierre — Fase 16A: archivos de contenedores y ajustes del código para la nube

Fecha: 22-11-2026 · Rama: `fase-16-contenedores-nube` (sobre el commit del plan, `af82b12`) · Estado: **construida, en revisión; parada para tu visto bueno** · Commit y push los haces tú (comandos al final)

Plan: `fase-16-plan.md` (secciones 1, 2, 3, 7 y 8) con tus decisiones D1 a D7. **No se creó ningún servicio, proyecto ni base en Render, Vercel ni Azure. No se aplicó ninguna migración a ninguna base real ni a la demo. No se escribió ninguna clave, credencial ni el ID de cliente de Google en ningún archivo (solo nombres de variables).** No empecé la 16B ni la 16C.

## Resultado en una línea

**Todo lo que se puede comprobar sin Docker está comprobado y en verde; lo que exige Docker (construir las imágenes y levantar `compose`) NO se pudo comprobar en este equipo y es la primera tarea de la 16B.** Con eso hecho, la API ya entiende a un proxy, reintenta ante la pausa de Azure SQL, exige su configuración de producción y tiene escrita (sin aplicar) la migración de la hora de Chile.

## Qué se construyó

**1. Contenedores (todo en la raíz o junto a cada proyecto).**

| Archivo | Qué es |
|---|---|
| `backend/…/Dockerfile`, `.dockerignore`, `entrada.sh` | API en varias etapas: SDK `10.0.404-noble` → runtime `aspnet:10.0.15-noble`; `tzdata` y `curl`; `PORT` configurable; `HEALTHCHECK` a `/salud`; etapa **`migrador`** con `efbundle` (dotnet-ef 10.0.14). El punto de entrada baja a un usuario sin privilegios (UID 1654) antes de ejecutar `dotnet`. |
| `frontend/synctimefront/Dockerfile`, `.dockerignore`, `nginx/` | Node 24 compila → nginx **sin privilegios** sirve: compresión, `try_files`, caché de un año solo en `/assets/`, `no-cache` en `index.html`, cabeceras de seguridad y `/api` hacia la API. |
| `compose.yaml`, `.env.example`, `scripts/crear-env.sh` / `.ps1` | `sql` (SQL Server 2022, versión fija) → `migrador` (un solo uso) → `semilla` (datos inventados) → `api` → `web`; `correo` (Mailpit). Red interna, volúmenes, chequeos de salud, `cap_drop`. **Solo el sitio y el buzón de correo se publican, y solo en `127.0.0.1`.** `.env` con claves al azar (los scripts las generan; `.env` ya estaba en `.gitignore`). |
| `.devcontainer/devcontainer.json` | Codespaces con Docker listo. |
| `scripts/verificar-compose.sh` | Levanta todo, espera la salud, recorre salud, portada, caché, cabeceras, ingreso, API con y sin sesión y buzón de correo; deja `docs/evidencias/compose-<fecha>.md` y borra la evidencia si aparece algún secreto. **Para la 16B.** |
| `render.yaml` | Servicio Docker de la API, plan de pago, disco persistente en `/app/almacen`, `autoDeployTrigger: off`, chequeo `/salud`, secretos con `sync: false`. |
| `frontend/synctimefront/vercel.json` | Reescritura de `/api` hacia Render, cabeceras (**sin** `Cross-Origin-Opener-Policy`), caché de `/assets/`, política de contenido de producción. |
| `docs/despliegue.md` | Cómo levantar con compose, variables de la API, proxies, migrador y salud. La guía completa es de la 16C. |

**2. Cambios en la API (cada uno con su prueba, escrita antes).**

- **IP y protocolo reales detrás de un proxy** (`Api/ConfiguracionDespliegue.cs`, `UseForwardedHeaders` al principio del pipeline). Solo se leen de proxies declarados (`ForwardedHeaders__*`); sin declarar se ignoran; una lista vacía, una red demasiado ancha o un valor mal escrito impiden arrancar. Cierra el riesgo anotado en la 15A y en la 7.
- **Reintentos para Azure SQL** (`Datos/EstrategiaDeReintentos.cs`). **Hallazgo:** `EnableRetryOnFailure` a secas rompe esta API (lanza en cada una de sus ~40 transacciones manuales: con él fallaron las 36 pruebas probadas), así que se escribió una estrategia propia que reintenta fuera de transacciones y no lo hace dentro de ellas.
- **CORS y URL pública por entorno**: fuera de desarrollo son obligatorios y se validan al arrancar.

**3. Migración de hora de Chile** (`20261122073024_HoraDeChileEnLaBase`): 8 columnas de fecha pasan de `SYSDATETIME()` a la hora de Chile calculada desde UTC, y la depuración de solicitudes. No modifica filas ni tipos, lleva un guardia (se niega a aplicarse si el servidor no conoce la zona) y se deshace. **No aplicada.** Detalle en `desviaciones.md` (sección 16A).

## Pruebas

| Conjunto | Resultado |
|---|---|
| Backend, unitarias | **749 de 749** |
| Backend, integración | **754 de 754** (las 704 anteriores + 50 nuevas: proxy 13, entorno 14, reintentos 7, hora de Chile y migración 16) |
| Frontend (Vitest) | **981 de 981**, 55 archivos (967 + 14 nuevas: política de contenido, caché, cabeceras, reescritura). Lint: solo los 5 avisos que ya había |
| Validación estática de contenedores | `validar-contenedores.py` en verde; **27 de 27** pruebas de «fallos a propósito» |

**Pruebas primero y fallos a propósito, todos detectados y el código restaurado:**

- Antes del código, 30 de las 41 pruebas nuevas del backend fallaban.
- Sin `UseForwardedHeaders`: fallan 4 pruebas de proxy (cupo compartido y bucle de HTTPS).
- Con `EnableRetryOnFailure` a secas: fallan las 36 pruebas del conjunto de reintentos y de cotizaciones («no admite transacciones iniciadas por el usuario»).
- En contenedores: etiqueta `latest`, sin `HEALTHCHECK`, API como root, sin `tzdata`, secreto en un Dockerfile, en `.env.example` o en `render.yaml`, base de datos publicada, socket de Docker, `.env` sin ignorar, COOP, caché mal puesta y políticas de contenido distintas: cada uno es una prueba que falla.
- En el frontend: con la política de nginx alterada y la caché de Vercel acortada, fallan 2 pruebas.
- La migración tenía un error de T-SQL que **solo apareció al aplicarla a la base temporal de las pruebas** (corregido). La validación estática también encontró un error real de `compose.yaml`.

**La hora de Chile se probó así:** este equipo está en hora de Chile y no se puede cambiar el reloj de SQL Server, así que a la expresión real de cada valor por defecto se le dio un reloj en UTC inventado (verano, invierno, cruce de medianoche y los límites exactos de los cambios de horario de abril y septiembre de 2026). La prueba con un SQL Server que corre de verdad en UTC es la de la 16B, en el contenedor.

## Revisiones de ECC

- **security-reviewer:** sin críticos ni altos; sin secretos en archivos. **Corregido:** el guion de verificación podía dar verde en falso (cabeceras ausentes, sitio caído) y ejecutaba el `.env` como shell; ahora lee el archivo, falla de verdad, no deja datos del servidor en comandos y apaga con `--limpiar` aun si falla; la clave de SQL Server ya no va en los argumentos del chequeo; `__pycache__` ignorado. **Anotado:** ver abajo.
- **database-reviewer:** T-SQL correcto, `Down` exacto, sin otras dependencias de la hora local en vistas, triggers ni columnas calculadas. **Corregido:** guardia de zona horaria (si no, `ADD DEFAULT` se aplicaría igual y fallarían los INSERT); prueba que escanea vistas, triggers y columnas calculadas; límites exactos de los cambios de horario; `PedidoPagoTests` comparaba con la hora local del servidor; `SemillaDemo` usaba `DateTime.Today`.
- **csharp-reviewer:** la estrategia de reintentos es correcta y usa API pública. **Corregido:** protección ante un contexto liberado; las redes públicas de confianza ahora exigen `/24` o más chico (las privadas siguen hasta `/8`). **Hallazgo confirmado con una prueba nueva:** una escritura auditada **no se reintenta** (el interceptor abre su transacción antes que la estrategia): responde 500 sin dejar filas a medias y la siguiente petición funciona. Es un límite aceptado y documentado.
- **react-reviewer:** la política de contenido no rompe nada real (no hay scripts en línea, ni `eval`, ni WebAssembly; el reconocimiento facial usa WebGL o CPU), `Permissions-Policy` deja la cámara al propio sitio. **Corregido:** una prueba nunca comprobaba nada (el manifiesto de Vite no se emite), la regla de reescritura se evalúa de verdad, se compara `Permissions-Policy`, se exige el `include` de cabeceras en cada `location`, la reescritura SPA excluye `models/`, `marca/`, `mapa/` e `imagenes/` (un archivo que falta daba HTML con 200) y nginx ya no escucha en IPv6 fijo.
- **e2e-runner:** no se invocó: esta fase no toca pruebas de Playwright ni el comportamiento del navegador.

## Lo que debes saber (leer antes de aprobar)

1. **Tu API demo migrará sola.** La base `…_Demo` y las de pruebas migran al arrancar la API en desarrollo: **si arrancas tu API local contra `LetreriaLosHerreraDB_Demo`, la migración de hora de Chile se aplicará a esa base** (no toca datos; se deshace con `dotnet ef database update 20261121161200_GoogleParaPersonal`). La base real nunca migra sola. Si no la quieres en la demo todavía, no arranques la API demo hasta la 16C.
2. **Los contenedores no se han levantado nunca.** Se validaron de forma estática y sus errores de sintaxis ya se corrigieron, pero que las imágenes se construyan, que nginx acepte la plantilla, que `efbundle` arranque y que `setpriv` exista en la imagen noble **se sabe recién en la 16B**. Es probable que aparezca algún ajuste.
3. **Riesgo principal para la 16C: la IP del visitante con Vercel → Render.** Es probable que la API vea la salida de Vercel y todos compartan cupo de peticiones (y que Auditoría guarde esa IP). Los valores de `render.yaml` son provisionales y hay un procedimiento de medición escrito (entrar al sitio y mirar la IP guardada en Auditoría).
4. **Valores provisionales** que se confirman en la 16C: región de Render (`virginia`), plan (`0.5c-512mb`; se mide la memoria con la carga) y el nombre `synctime-api.onrender.com` de `vercel.json` (hay que copiar la URL real que Render asigne).
5. **La API arranca como root solo para preparar la carpeta de archivos y baja a un usuario sin privilegios** antes de ejecutar nada (así un disco persistente de Render, que llega a nombre de root, es escribible). Un escáner puede señalar «root»: es decisión documentada.
6. **SQL Server 2022 y no 2025** en compose: la imagen de 2025 ya no trae `sqlcmd` y el chequeo de salud lo necesita. Solo existe para amd64 (en Apple Silicon corre emulada) y pide unos 2 GB de memoria.
7. **Ninguna dependencia nueva del proyecto:** el validador usa PyYAML (ya instalado aquí; el pipeline de la 16B lo instalará con `pip`). `package.json` y los paquetes NuGet no cambiaron.
8. El archivo es **`compose.yaml`**, no `docker-compose.yml`: `docker compose up` lo encuentra igual. Tenerlos con los dos nombres haría que Compose avise de un conflicto.
9. Anotado y no hecho: ver la sección 16A de `desviaciones.md` (calentamiento de la base, digests de imagen, `proxy_hide_header`, etc.).

## Trazabilidad

RNFO14 (En curso: **construido, pendiente de verificación en la 16B**), RNFP25 (En curso: caché larga solo en `/assets/` y `no-cache` en `index.html` con prueba; falta medir en la nube), RNFP29 (En curso: CORS y URL pública por entorno con 14 pruebas; falta el origen real de Vercel), RNFE03 (Pendiente: HSTS, proxy y `https` listos; el certificado se ve en la 16C), RNFO15 (Pendiente: el pipeline es la 16B). RNFP04, RNFP09, RNFP20, RNFO16, RNFO17, RNFE09 y S22 siguen para la 16C. `desviaciones.md` registra que se **mantiene `docker compose up` local y se agrega la nube**.

## Qué te toca

1. **Visto bueno a la 16A** (o los cambios que pidas).
2. Decidir si arrancas la API demo antes de la 16C (ver el punto 1 de arriba).
3. Para empezar la 16B: **subir la rama a GitHub** (yo no subo nada) y abrir un Codespace; te dejaré los pasos exactos.

## Comandos para tu commit

Desde la carpeta del repositorio. Se agregan solo las carpetas y archivos de esta fase, para **no arrastrar `.impeccable/` ni el archivo `python`**:

```powershell
git add .gitignore .devcontainer .env.example compose.yaml render.yaml backend frontend docs scripts
git status --short
git commit -m "feat(fase-16a): Dockerfiles, compose, render.yaml y vercel.json, IP real tras proxy, reintentos para Azure SQL, CORS y URL publica por entorno y migracion de hora de Chile sin aplicar"
```
