# Plan de la fase 16 — Contenedores, CI/CD, nube y operación

Fecha: 22-11-2026 · Rama: `fase-16-contenedores-nube` (nace de `fase-15-calidad`, commit `e897622`) · Estado: **propuesto, esperando tu aprobación; construcción sin iniciar** (se hará en **otra sesión**, por decisión del usuario)

Origen: el plan general (`plan-de-construccion.md`, fase 16, con el ajuste del 20-11-2026) y los requisitos **RNFO14, RNFO15, RNFO16, RNFO17, RNFP04, RNFP09, RNFP20, RNFP25, RNFP29, RNFE03, RNFE09 y S22**, más lo que las fases anteriores dejaron anotado para ésta (`desviaciones.md`): la hora de Chile en la base, la lectura de la IP real detrás del proxy, la caché de `/assets/`, la repetición de la carga en la nube y la medición con servidor real.

**Destino:** frontend en **Vercel**, backend en **Render** (con Docker) y base de datos en **Azure SQL Database** (capa gratuita). Además se **mantiene** el entorno completo con `docker compose up` (decisión del 20-11-2026). Los conectores de las tres plataformas están instalados; al mirarlos hoy (solo lectura), **Render y Vercel no tienen ningún servicio ni proyecto creado** y **Azure no respondió a tiempo**: se verifica al empezar.

## 0. Cómo se divide (como la 13, la 14 y la 15)

- **16A — Archivos de contenedores y ajustes del código para la nube.** `Dockerfile` de la API y del frontend, `compose.yaml` (frontend, API, SQL Server, correo de desarrollo, migrador), `.env.example`, `.devcontainer/`, `render.yaml`, `vercel.json`, y los cambios pequeños que la nube exige en la API (proxy HTTPS, reintentos, hora de Chile). Pruebas primero. Parada con informe.
- **16B — Pipeline CI/CD y verificación en Codespaces.** GitHub Actions (construir, probar, escanear, publicar solo si todo pasa), guion `scripts/verificar-compose.sh` y su evidencia. Necesita que **subas la rama a GitHub**. Parada con informe.
- **16C — Despliegue y operación en la nube.** Azure SQL (con respaldo y migración), Render, Vercel, respaldo diario de 30 días con restauración probada, guía de despliegue, inventario de licencias, repetición de la carga y de Lighthouse contra la nube. Es la parte que **crea cuentas y servicios y puede costar dinero: cada creación va con tu permiso**. Parada con informe.

La 16A no depende de ninguna cuenta. La 16B depende de la 16A y de GitHub. La 16C depende de las dos y de tus decisiones de la sección 7.

## 1. Qué hay hoy (para no construir a ciegas)

- **Backend:** .NET 10 (`global.json` fija el SDK 10.0.404), una solución con la API y dos proyectos de pruebas (749 unitarias y 704 de integración, que crean sus bases temporales en SQL Server). Configuración por variables de entorno (`ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Cors__AllowedOrigins__0`, `Correo__…`, `Google__ClientId`, `RostroFacial__ClaveCifrado`, `Bootstrap__Admin__…`, `Sitio__UrlPublica`). Las rutas de salud ya existen: `/salud`, `/salud/lista`, `/salud/correo`. La compresión Brotli y Gzip ya está activa.
- **PDF en Linux:** la fuente (Plus Jakarta Sans) viaja incrustada en el programa y SkiaSharp usa los binarios «sin dependencias de Linux»: **no hace falta instalar fuentes** en la imagen.
- **Cookies de sesión:** `Secure` y `SameSite=Strict`, la de renovación solo en `/api/v1/auth`. Funcionan si el navegador ve **un solo origen**: por eso Vercel reenvía `/api` a Render (reescritura), como ya decidimos.
- **Frontend:** Vite; `npm run build` produce `dist/` (JavaScript inicial de 117,5 KB comprimidos, presupuesto de 150 verificado por una prueba). Hoy el proxy de `/api` solo existe en el servidor de desarrollo.
- **Docker:** no se puede instalar en este equipo (OptiOS 11 Lite, sin Hyper-V). Los archivos se escriben con todo el rigor y se **verifican en un Codespace y en GitHub Actions**.

## 2. Hallazgos que cambian el diseño (léelos: hay decisiones tuyas)

| # | Hallazgo | Por qué importa | Qué propongo |
|---|---|---|---|
| H1 | **La API no entiende al proxy de Render/Vercel.** No usa `UseForwardedHeaders`; detrás de un proxy que termina el HTTPS, `UseHttpsRedirection` puede entrar en bucle y todas las IP serán la del proxy (límites de tasa compartidos: riesgo ya anotado en la 15A). | Sin esto el despliegue no funciona o los visitantes comparten cupo. | `UseForwardedHeaders` con los proxies de confianza por configuración, antes de todo lo demás, con prueba de integración (`X-Forwarded-For` y `X-Forwarded-Proto`). |
| H2 | **Las migraciones no se aplican solas fuera de desarrollo** (a propósito, con respaldo). | La API desplegada arrancaría contra una base vacía. | Un **migrador** (`dotnet ef migrations bundle`, ejecutable único) que corre como servicio de un solo uso en `compose`, y como paso **manual, con tu permiso y con respaldo previo**, contra Azure SQL. La API sigue sin migrar sola. |
| H3 | **Hora de Chile.** Las columnas de fecha usan `SYSDATETIME()` y las vistas de indicadores cuentan «días de Chile» solo si el servidor está en hora de Chile. **Azure SQL corre en UTC.** | Los indicadores y los pedidos del día se desfasarían entre 3 y 4 horas. | Una **migración** que cambie esos valores por defecto a la hora de Chile (`AT TIME ZONE`) y las vistas que dependan de la hora del servidor. **Es una migración: va aparte, con tu aprobación y con respaldo** (regla de la fase 15). Se prueba contra SQL Server en un contenedor con el reloj en UTC. |
| H4 | **La API guarda archivos en disco** (`almacen/adjuntos`, `imagenes`, `exportaciones`). El disco de Render gratuito es **efímero**: cada reinicio o despliegue borra los adjuntos de las solicitudes. | Pérdida de datos de clientes. | **Decisión tuya (D1):** disco persistente de Render (de pago, cambio de código mínimo) o Azure Blob Storage (barato, requiere abstraer el almacén en el código). |
| H5 | **Render gratuito se duerme tras 15 minutos** y tarda cerca de un minuto en despertar; además se detienen los procesos internos (cola de correos, exportaciones, purgas). **RNFP04 pide 99 % de disponibilidad, 24 horas.** | El requisito no se cumple con un servicio que duerme. | **Decisión tuya (D2):** plan de pago de Render (no duerme) o plan gratuito con un monitor externo que lo despierte y la limitación **documentada**. |
| H6 | **Correo.** Hoy es SMTP de Gmail; hasta donde sé, **Render bloquea los puertos SMTP salientes en los servicios gratuitos** (a verificar al desplegar). | Los avisos al taller y los códigos de recuperación no saldrían. | Verificar primero. Si está bloqueado: plan de pago, o un proveedor de correo por HTTPS (cambio acotado, detrás de la interfaz del enviador que ya existe). **Decisión tuya (D3), después de verificar.** |
| H7 | **Azure SQL gratuita se pausa sola** y la primera consulta tras la pausa falla o tarda varios segundos. EF Core está sin reintentos. | Errores aleatorios tras ratos sin uso. | `EnableRetryOnFailure` con tiempo de espera suficiente, y `/salud/lista` como «despertador». Se documenta y se prueba con la pausa real. Cuota gratuita: 100.000 segundos de vCore por mes (se mira el consumo después de la carga). |
| H8 | **Respaldo diario con 30 días, fuera del servidor principal (RNFP09).** Azure guarda respaldos automáticos de pocos días. | Se queda corto. | Un trabajo programado de GitHub Actions que exporta la base (`.bacpac`) a un almacenamiento externo con retención de 30 días, y una **restauración probada en `localhost\SQLEXPRESS`**. Destino: **decisión tuya (D4).** |
| H9 | **Secretos.** Ninguna clave puede estar en el repositorio ni en los registros (RNFO15). | Regla de siempre. | Variables de Render, de Vercel y secretos de GitHub **que cargas tú**; los archivos llevan solo los nombres (`sync: false` en `render.yaml`, `.env.example` sin valores). Un análisis de secretos en el pipeline. |
| H10 | **Google (14B/14C).** El ID de cliente de Google debe autorizar el dominio de Vercel. La aplicación sigue en modo «Prueba». | El botón no funcionaría en la nube. | Pasos exactos para que lo hagas en la consola de Google; yo no toco el ID. |

## 3. 16A — Archivos de contenedores y ajustes del código

**Pruebas primero** (xUnit de integración y Vitest) para todo lo que sea código; los archivos de contenedores se validan de forma **estática** en este equipo y de forma **real** en la 16B.

- **`backend/Proyecto-Carteles-herrera-synctime-main/Dockerfile`** (varias etapas, versiones fijas): `sdk` para compilar y publicar (sin las pruebas) → `aspnet` de ejecución, usuario sin privilegios, hora de Chile disponible (`tzdata`), `curl` para la comprobación de salud, `PORT` configurable (Render lo pasa), `HEALTHCHECK` sobre `/salud`. Una etapa más que construye el **migrador** (`efbundle`). `.dockerignore` (sin `bin`, `obj`, `.vs`, `almacen`, secretos).
- **`frontend/synctimefront/Dockerfile`** (Node para compilar → `nginx` sin privilegios para servir): `nginx.conf` con compresión, `try_files` para las rutas de la aplicación, caché larga solo para `/assets/` (con huella) y `no-cache` para `index.html` (**cierra la caché de RNFP25**), cabeceras de seguridad y reescritura de `/api` hacia la API para el uso local. `.dockerignore`.
- **`compose.yaml`** (raíz): `web`, `api`, `sql` (SQL Server en Linux, versión fija, volumen, comprobación de salud), `migrador` (un solo uso, espera a `sql`), `correo` (buzón de desarrollo para ver los correos sin enviarlos). Red interna; solo el frontend y el buzón publican puertos; volúmenes nombrados para la base y para `almacen`; comprobaciones de salud y variables desde `.env`. **`.env.example`** sin valores secretos y **`.gitignore`** que excluye `.env`.
- **`.devcontainer/`** (Codespaces con Docker listo) y **`scripts/verificar-compose.sh`**: levanta todo, espera la salud de cada servicio, recorre `/salud`, la portada, el ingreso y un flujo básico (con la semilla de demostración, nunca la base real), y guarda la evidencia en `docs/evidencias/`.
- **`render.yaml`** (Blueprint): servicio web con Docker, ruta de salud, variables con `sync: false`, región y plan según tus decisiones. **`vercel.json`**: reescritura de `/api/(.*)` hacia el backend, cabeceras de seguridad (sin `Cross-Origin-Opener-Policy: same-origin`: bloquearía la ventana de Google), caché larga de `/assets/` y la política de contenido de producción.
- **Cambios en la API** (cada uno con su prueba antes): `UseForwardedHeaders` (H1); `EnableRetryOnFailure` para Azure SQL (H7); opciones por entorno para CORS y la URL pública; si D1 = Blob, la abstracción del almacén de archivos.
- **Migración de hora de Chile (H3):** se escribe, se prueba contra SQL Server en contenedor con el reloj en UTC y **queda sin aplicar** hasta la 16C y tu permiso.
- **Revisión sin Docker:** validación de la sintaxis de `compose.yaml` y de los `Dockerfile` con las herramientas disponibles, y revisión con ECC (`docker-patterns`, `security-reviewer`, `database-reviewer`, `csharp-reviewer`, `react-reviewer`, `performance-optimizer`).

## 4. 16B — CI/CD y verificación en Codespaces (RNFO15, RNFO14)

- **`.github/workflows/ci.yml`** en cada subida: backend (restaurar, compilar, 749 unitarias y 704 de integración contra un SQL Server en contenedor de servicio), frontend (lint, Vitest 967, construcción y presupuesto de peso), Playwright (Chrome) sobre el entorno `compose`, construcción de las dos imágenes, análisis de vulnerabilidades de las imágenes y del repositorio, y análisis de secretos. **Solo si todo pasa**, se publican las imágenes en el registro de GitHub (GHCR), con versión fija y sin secretos en los registros.
- **`.github/workflows/migrar.yml`** (manual, con un entorno protegido que pide tu aprobación): respaldo, migrador contra Azure SQL, comprobación posterior. **Nunca corre solo.**
- **`.github/workflows/respaldo.yml`** (programado, diario): exportación y retención de 30 días (H8). Los secretos de GitHub los cargas tú.
- **Verificación en un Codespace** (necesito de ti): abrir un Codespace de la rama y ejecutar `scripts/verificar-compose.sh`; te dejo los pasos exactos. La evidencia queda en `docs/evidencias/`.
- **Permiso para subir la rama a GitHub** al empezar la 16B (el remoto es `origin`); yo no subo nada: te paso los comandos.

## 5. 16C — Nube y operación (RNFP04, RNFP09, RNFE03, RNFP20, RNFE09, RNFO16, RNFO17, S22)

Orden, con parada y permiso en cada creación (cuentas, servicios y costos):

1. **Azure SQL Database (capa gratuita):** servidor y base, cortafuegos (solo Render y tu IP para migrar), cifrado en tránsito, usuario propio de la aplicación con permisos mínimos (no el administrador). **La base real se respalda antes de cualquier migración y la restauración se prueba.**
2. **Migraciones** con el flujo manual de la sección 4 (incluida la de hora de Chile) y datos iniciales: el primer administrador sale de `Bootstrap__Admin__…`, que cargas tú.
3. **Render:** servicio web con la imagen de la API, variables cargadas por ti, ruta de salud, disco (según D1) y plan (D2).
4. **Vercel:** proyecto del frontend con `vercel.json`, variables de construcción, dominio `*.vercel.app` con certificado válido (**RNFE03**) y, si lo quieres, tu dominio propio.
5. **Enlaces:** CORS de la API con el origen de Vercel, `Sitio__UrlPublica`, el ID de Google autorizado (H10), el enlace de los correos y del código QR.
6. **Pruebas de humo en la nube**, la **carga de 20 usuarios repetida contra la API en Render** (`scripts/carga/` apunta a una URL; pendiente de la 15A, RNFP01) y **Lighthouse sobre la URL de Vercel**.
7. **Respaldo y restauración probada** en `localhost\SQLEXPRESS` (RNFP09), y una nota de **disponibilidad** (RNFP04): qué se cumple y qué no según el plan elegido.
8. **Documentación:** guía de despliegue (HTTPS, dominio, CORS por entorno, rotación de claves), **inventario de licencias** de paquetes, fuentes, iconos e imágenes con su vista pública (RNFE09), y eliminación de dependencias sin uso.

## 6. Lo que tienes que hacer tú

1. **Aprobar este plan** y decidir D1 a D7 (sección 7).
2. **Cargar los secretos** en Render, Vercel y GitHub (te doy la lista exacta de nombres; los valores nunca pasan por mí ni por el repositorio): cadena de conexión de Azure, `Jwt__Key`, `RostroFacial__ClaveCifrado`, contraseña del primer administrador, credenciales de correo y, si corresponde, el ID de Google.
3. **Permisos puntuales:** subir la rama (16B), crear cada recurso en la nube (16C) y aplicar migraciones a Azure (con respaldo).
4. **Abrir un Codespace** y ejecutar el guion de verificación (16B).
5. **Configurar Google** para el dominio de Vercel (pasos exactos en la 16C).

## 7. Decisiones y supuestos a tu vista

| # | Decisión | Mi recomendación |
|---|---|---|
| D1 | **Dónde viven los archivos** (adjuntos, imágenes, reportes): disco persistente de Render o Azure Blob Storage. | Disco persistente de Render (cambio mínimo de código). Si el presupuesto es cero, Blob. |
| D2 | **Plan de Render:** de pago (no duerme, cumple RNFP04) o gratuito con monitor externo y la limitación documentada. | Pago, mientras el taller lo use de verdad; gratuito para la entrega académica si lo prefieres, con la nota de disponibilidad. |
| D3 | **Correo desde Render** (se decide tras verificar el bloqueo de SMTP). | Mantener Gmail por SMTP si el plan lo permite; si no, proveedor por HTTPS. |
| D4 | **Dónde se guardan los respaldos** de 30 días. | Almacenamiento externo de la cuenta de Azure o artefactos de GitHub con retención: el más barato que cumpla «fuera del servidor principal». |
| D5 | **Región:** Azure y Render lo más cerca posible entre sí (ninguna está en Chile). | Un par cercano de la costa este de Estados Unidos; se confirma con lo que ofrezca cada plataforma. |
| D6 | **División en 16A, 16B y 16C con parada en cada una**, y la **migración de hora de Chile** como cambio aparte y con tu aprobación. | Sí. |
| D7 | **Sin dependencias de producción nuevas** salvo que D1 elija Blob (un paquete de Azure) o D3 un proveedor de correo. | Sí. |

## 8. Pruebas y revisiones

- **Pruebas primero:** xUnit de integración (proxy y `X-Forwarded-*`, reintentos, hora de Chile con el reloj en UTC), Vitest (presupuesto de peso y política de contenido de producción, ya existentes) y el guion de verificación del entorno completo.
- **Fallos a propósito:** quitar `tzdata` de la imagen (la API debe negarse a arrancar y la comprobación de salud fallar); quitar `UseForwardedHeaders` (la prueba de IP real falla); un secreto de prueba en un archivo (el análisis de secretos del pipeline debe detenerlo); una prueba rota (el pipeline no debe publicar la imagen).
- **ECC:** `docker-patterns`, `security-reviewer`, `database-reviewer`, `csharp-reviewer`, `react-reviewer`, `performance-optimizer` y `e2e-runner`.
- **Cierre:** informes `informes/fase-16a.md`, `fase-16b.md` y `fase-16c.md`, tablero, trazabilidad (RNFO14, RNFO15, RNFO16, RNFO17, RNFP04, RNFP09, RNFP20, RNFP25, RNFP29, RNFE03, RNFE09, S22 y los pendientes de `desviaciones.md`) y la evidencia en `docs/evidencias/`. Los puntos que dependen de la nube quedan como «construido, pendiente de verificación» hasta que exista su evidencia (regla del plan general). Los comandos de git te los paso; yo no subo nada.
