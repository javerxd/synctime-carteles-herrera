# Informe de cierre — Fase 16B: CI/CD con GitHub Actions y verificación de compose

Fecha: 22-11-2026 · Rama: `fase-16-contenedores-nube` (sobre el commit de la 16A, `98c7258`) · Estado: **construida, en revisión; parada para tu visto bueno** · Commit y push los haces tú (comandos al final)

Plan: `fase-16-plan.md` (secciones 4 y 8) con tus decisiones definitivas para costo $0 (D1 a D7). **No se creó ningún servicio, proyecto ni base en Render, Vercel ni Azure. No se aplicó ninguna migración a ninguna base. No se escribió ninguna clave, credencial ni el ID de cliente de Google en ningún archivo (solo nombres de secretos y variables de GitHub, que cargas tú). No se arrancó la API demo ni se escribió en `LetreriaLosHerreraDB_Demo`. No empecé la 16C.**

## Resultado en una línea

**El pipeline está escrito y validado de forma estática, y todo lo que se puede probar en este equipo está en verde; lo que exige GitHub o Docker NO se ha ejecutado nunca y es lo primero que hay que hacer** (pasos exactos más abajo). Es probable que la primera ejecución en GitHub pida algún ajuste.

## Qué se construyó

| Archivo | Qué es |
|---|---|
| `.github/workflows/ci.yml` | En cada subida: `estatica`, `backend` (compila, **749 unitarias** y **768 de integración** contra un SQL Server 2022 en contenedor con el reloj en UTC, vulnerabilidades de NuGet), `frontend` (lint, **981 de Vitest**, build, presupuesto de peso, `npm audit`), `secretos` (gitleaks sobre todo el historial, con una **autoprueba** que planta un secreto), `imagenes` (API, migrador y frontend: se construyen una vez, se prueba lo básico y se analizan con Trivy), `compose` (`docker compose up` real con `verificar-compose.sh`) y `publicar` |
| `.github/workflows/migrar.yml` | **Solo a mano**, solo desde `main`, con entorno protegido que pide tu aprobación, y hay que escribir `MIGRAR` y el commit exacto: respaldo a Blob Storage verificado → migrador (la imagen que la CI probó, comprobada contra el commit) → comprobación de la última migración. **Solo escrito; no se ejecutó** |
| `.github/workflows/respaldo.yml` | Diario (07:17 UTC): exporta `.bacpac`, lo verifica, lo sube al contenedor privado `respaldos`, borra lo de más de 30 días (nunca baja de 7). **Solo escrito; no se ejecutó** |
| `scripts/validar-workflows.py` + `test_validar_workflows.py` | Validador estático del pipeline: 55 «fallos a propósito» |
| `scripts/ci-resumen.py` + `test_ci_resumen.py` | Decide si un resultado deja pasar: pruebas falladas, omitidas o **menos que el piso** (749 / 768 / 981), y vulnerabilidades de NuGet. 21 pruebas |
| `scripts/fallos-a-proposito.sh` | Imagen de la API sin `tzdata`: debe negarse a arrancar y su comprobación de salud fallar (con control sano). Para el Codespace |
| `.gitleaks.toml`, `.trivyignore`, `.github/dependabot.yml` | Reglas del análisis de secretos y de vulnerabilidades (las excepciones exigen fecha de vencimiento) y actualización mensual de las acciones fijadas |
| `ConexionDePruebas.cs` (+ pruebas) | Cadena de conexión de las pruebas: **Windows como siempre**; con `TEST_SQL_PASSWORD` (y `TEST_SQL_USER`, por omisión `sa`) usuario y clave de SQL Server. 14 pruebas |

Seguridad del pipeline: **todas las acciones de terceros fijadas por SHA completo** (verificado contra GitHub: cada SHA corresponde a la versión que dice su comentario), permisos `contents: read` (solo `publicar` escribe paquetes), `persist-credentials: false`, ningún valor externo dentro de `run:`, clave de SQL de las pruebas aleatoria y enmascarada, binarios de gitleaks y Trivy con versión fija y suma SHA-256 verificada. **No se usa `aquasecurity/trivy-action`**: en marzo de 2026 sus etiquetas fueron reescritas con código malicioso.

## Pruebas (todas en este equipo)

| Conjunto | Resultado |
|---|---|
| Backend, unitarias | **749 de 749** |
| Backend, integración (autenticación de Windows, `localhost\SQLEXPRESS`) | **768 de 768** (754 + 14 nuevas) |
| Frontend (Vitest, con el reporter JSON que usa la CI) | **981 de 981** |
| Validador de contenedores (16A) | OK, **27 de 27** |
| Validador del pipeline | OK, **55 de 55** «fallos a propósito» |
| Resumen de resultados | **21 de 21** |

**Pruebas primero y fallos a propósito.** Antes del código las pruebas de conexión no compilaban y las de los validadores fallaban por no existir. Cada regla del pipeline tiene su prueba que estropea el archivo real y comprueba que el validador lo detecta y nombra la regla: acción sin SHA o con SHA corto, permisos de más, migración que se dispara sola, sin entorno, sin respaldo previo o sin cerrar el cortafuegos, publicación que no espera a un job o que corre aunque otro falle, `latest`, secreto escrito, clave sin enmascarar, inyección por `${{ }}`, descarga sin suma, retención distinta de 30 días, contenedor público, `DateTime.Now` en las pruebas, etc.

**Las cuatro pruebas con `DateTime.Now`** (`FlujoNegocioTests`, `PedidoPagoTests`, `SolicitudesFase7Tests` ×2) usan ahora la hora de Chile que usa la API: ya no dependen de la zona del equipo ni del servidor SQL. **No pude comprobarlas contra un SQL Server que corra de verdad en UTC** (este equipo está en hora de Chile): eso lo hace el job `backend` en su primera ejecución, que además verifica que el servidor esté en `+00:00` antes de probar. Lo mismo vale para `HoraChileFase16aTests` en el contenedor.

## Revisiones de ECC

- **security-reviewer:** sin críticos; 2 altos, 6 medios y observaciones. **Corregido:** `migrar.yml` y `respaldo.yml` solo corren desde `main` (antes dependían de una casilla de GitHub); si el cortafuegos de Azure SQL no se cierra, el job queda en rojo; la autoprueba de gitleaks exige el código exacto 42; el informe de NuGet no puede dar verde sin haber consultado la fuente; los secretos van solo en los pasos que los usan y `sqlpackage` se instala antes de iniciar sesión en Azure; el migrador se comprueba contra el commit; `publicar` no se cancela a la mitad. **Aceptado, anotado en `desviaciones.md`:** `publicar` se puede lanzar a mano desde cualquier rama (es lo que permite ver que una prueba rota no publica; `migrar` solo usa `main`), artefactos legibles en un repositorio público (sin claves), validador no es frontera de confianza (hace falta proteger `main` y un `CODEOWNERS`).
- **csharp-reviewer:** sin fallos reales. Corregido: recorte de servidor y usuario (nunca de la clave), una aserción vacía y un alias sin uso. Confirmado: una clave con `;` o comillas no puede agregar opciones y los mensajes de error nunca repiten la clave; ya no queda `DateTime.Now`, `DateTime.Today` ni `DateTimeOffset.Now` en las pruebas. Por confirmar en la primera corrida: una prueba abre su propia conexión con la cadena y necesita que EF la devuelva con la clave (debería).
- **docker-patterns:** etiquetas fijas, puertos solo en `127.0.0.1`, sin `--privileged`, y los contenedores de `fallos-a-proposito.sh` con las mismas restricciones de compose.
- **database-reviewer, react-reviewer y e2e-runner:** no se invocaron: esta fase no toca migraciones, frontend ni Playwright.

## Lo que debes saber (leer antes de aprobar)

1. **Nada del pipeline se ha ejecutado en GitHub ni en Docker.** Los workflows están validados de forma estática; la sintaxis de `sqlpackage` y de `az` ni siquiera se puede probar sin Azure. Espera ajustes tras la primera ejecución.
2. **El primer riesgo concreto es el análisis de secretos sobre el historial** (gitleaks puede marcar algún falso positivo antiguo; se resuelve en `.gitleaks.toml` con su motivo, nunca bajando el nivel) y el de vulnerabilidades (si una imagen base trae una alta con corrección publicada, la CI falla hasta actualizar la imagen o documentar la excepción con fecha).
3. **Tus decisiones de costo $0 chocan con lo que dejó la 16A, y es de la 16C:** `render.yaml` aún pide plan de pago y disco persistente, y la API aún guarda archivos en disco. Falta abstraer el almacén hacia Azure Blob Storage (paquete `Azure.Storage.Blobs`), actualizar `render.yaml` a plan gratuito sin disco y ajustar esa regla del validador. No lo toqué. El respaldo y la migración de la 16B ya usan Blob Storage.
4. **`migrar.yml` y `respaldo.yml` solo corren desde `main`.** Para migrar en la 16C hay que hacer merge de la rama (y que la CI quede verde sobre `main`, para que exista la imagen del migrador de ese commit).
5. **La CI también levanta compose** (job `compose`), además del Codespace: los ejecutores de GitHub traen Docker. Playwright no corre en la CI (no estaba en el pedido).
6. **Se publican tres imágenes** (`synctime-api`, `synctime-web`, `synctime-migrador`) en `ghcr.io/<tu-usuario>/…`, con la etiqueta `sha-<12>`; nunca `latest`. Son privadas por omisión.
7. Los `.sh` se llaman con `bash` (git los guarda sin bit de ejecución).
8. El archivo vacío `python` de la raíz (0 bytes, sin seguimiento) sigue ahí: no lo toqué, pero conviene borrarlo.
9. Pendientes conocidos de la 16C que **no** resolví: IP real del visitante con Vercel → Render, región y plan de Render, nombre de la URL de Render en `vercel.json`.

## Trazabilidad

RNFO15 (En curso: pipeline construido, pendiente de verificación), RNFO14 (En curso: ahora lo levanta la CI en cada subida y el Codespace; falta la evidencia real), RNFP09 (En curso: `respaldo.yml` escrito; sin ejecutar hasta la 16C). RNFO16, RNFO17, RNFP04, RNFP20, RNFE03, RNFE09 y S22 siguen para la 16C. `desviaciones.md` tiene la sección «16B» completa.

## Qué te toca (pasos exactos)

### 1. Commit y push (los haces tú)

Desde la carpeta del repositorio (en PowerShell). Se agregan solo las carpetas y archivos de esta fase, para **no arrastrar `.impeccable/` ni el archivo `python`**:

```powershell
git add .github .gitleaks.toml .trivyignore scripts docs backend
git status --short
git commit -m "feat(fase-16b): pipeline de CI/CD (ci, migrar y respaldo), conexion de pruebas con usuario sa, pruebas de hora de Chile sin DateTime.Now y guiones de verificacion"
git push origin fase-16-contenedores-nube
```

Al hacer push, GitHub corre `CI` solo. Si GitHub rechaza el push por «secret scanning» o «push protection», pásame el mensaje.

### 2. Mirar la CI (GitHub Actions)

1. En el repositorio: pestaña **Actions → CI → la ejecución de la rama `fase-16-contenedores-nube`**.
2. Hay siete jobs: `estatica`, `backend`, `frontend`, `secretos`, `imagenes`, `compose` y `publicar`. **`publicar` aparecerá «skipped» en esta rama a propósito** (solo corre en `main`, en etiquetas `v*` o a mano).
3. Si alguno falla: abre el job, copia el **paso que falló y sus últimas ~40 líneas** y pásamelos (los registros ya vienen con las claves enmascaradas). En `backend` y `frontend` el paso «Resumen…» muestra cuántas pruebas pasaron.

### 3. Verificación real de compose en un Codespace

1. En el repositorio: botón verde **Code → pestaña Codespaces → «⋯» → New with options…**, rama `fase-16-contenedores-nube`, máquina de **4 núcleos** (la definición pide 4 núcleos y 8 GB) → **Create codespace**.
2. Espera a que cargue el editor. Se crea `.env` con claves al azar (no se sube a git).
3. En la terminal del Codespace:

```bash
bash scripts/verificar-compose.sh --limpiar
```

   La primera vez tarda 10 a 20 minutos (construye las imágenes y descarga SQL Server). Si algo falla, el propio guion imprime los registros de los servicios **antes** de apagar el entorno.
4. Después:

```bash
bash scripts/fallos-a-proposito.sh
```

5. **Qué copiar de vuelta:** las **últimas ~40 líneas** de cada terminal y el contenido de las dos evidencias:

```bash
cat docs/evidencias/compose-*.md docs/evidencias/fallos-a-proposito-*.md
```

   (No contienen claves.) **No me pases el archivo `.env`.** Yo dejo la evidencia definitiva en `docs/evidencias/compose-<fecha>.md` y corrijo lo que falle (probables: `setpriv` en la imagen noble, la plantilla de nginx, `efbundle`, los chequeos de salud).
6. Cuando termines, **borra el Codespace** (github.com/codespaces → «⋯» → Delete) para no gastar tu cuota gratuita.

### 4. Fallos a propósito que necesitan GitHub (desde Git Bash o la terminal del Codespace, porque usan `openssl` y `sed`)

**a) Un secreto en un archivo: el análisis de secretos debe detener el pipeline.**

```bash
git switch fase-16-contenedores-nube
git switch -c prueba/secreto
printf 'token = "ghp_%s"\n' "$(openssl rand -hex 18)" > prueba-secreto.txt
git add prueba-secreto.txt
git commit -m "prueba: secreto plantado a proposito"
git push -u origin prueba/secreto
```

Esperado: el job `secretos` queda **en rojo** (o GitHub rechaza el push por su propia protección: avísame, también cuenta como detección). Limpieza:

```bash
git push origin --delete prueba/secreto
git switch fase-16-contenedores-nube
git branch -D prueba/secreto
```

**b) Una prueba rota: no debe publicarse ninguna imagen.**

```bash
git switch fase-16-contenedores-nube
git switch -c prueba/rota
sed -i '0,/Assert.Equal("sa", partes.UserID);/s//Assert.Equal("no-sa", partes.UserID);/' backend/Proyecto-Carteles-herrera-synctime-main/LetrerosHerreraSYNCTIMEapi.Tests/ConexionDePruebasTests.cs
git commit -am "prueba: prueba rota a proposito"
git push -u origin prueba/rota
```

Luego, en GitHub: **Actions → CI → Run workflow**, rama `prueba/rota`, marca **publicar**. Esperado: `backend` en rojo y **`publicar` «skipped»**; en tu perfil, **Packages**, no aparece ninguna imagen nueva. Limpieza:

```bash
git push origin --delete prueba/rota
git switch fase-16-contenedores-nube
git branch -D prueba/rota
```

**c) Sin `tzdata`:** ya está en el paso 3 (`fallos-a-proposito.sh`).

### 5. Opcional

Que el bit de ejecución de los `.sh` viaje en el repositorio: `git update-index --chmod=+x scripts/crear-env.sh scripts/verificar-compose.sh scripts/fallos-a-proposito.sh backend/Proyecto-Carteles-herrera-synctime-main/entrada.sh`.

## Parada

Cuando me pases el resultado de la CI y del Codespace corrijo lo que falle y cierro la evidencia de la 16B. **No empiezo la 16C** (Azure SQL, Blob Storage, Render, Vercel, los entornos y secretos de GitHub y la primera ejecución de `migrar.yml` y `respaldo.yml`) hasta tu visto bueno.
