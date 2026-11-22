# Prompts de la fase 16C (para pegar en sesiones nuevas)

Plan: `docs/plan/fase-16c-plan.md`. **Pega cada prompt en una sesión nueva, de a uno y en orden.** El segundo y el tercero solo cuando hayas dado tu visto bueno a la parte anterior. Antes del primero, haz el **paso 0** de la sección 3 del plan (fusionar `fase-16-contenedores-nube` a `main` y proteger `main`).

---

## Prompt 1 — 16C-1: código y archivos para la nube gratuita (sin tocar la nube)

```
Proyecto SyncTime. La fase 16A y la 16B están APROBADAS por mí (16B el 22-11-2026) y la rama fase-16-contenedores-nube ya está fusionada a main en GitHub (repositorio público javerxd/synctime-carteles-herrera). Quiero la fase 16C, PARTE 16C-1, siguiendo docs/plan/fase-16c-plan.md (secciones 1, 2, 4, 7 y 8), docs/plan/informes/fase-16b.md, la sección "fase 16B" de docs/plan/desviaciones.md, docs/despliegue.md y docs/plan/plan-de-construccion.md (fase 16).

PRIMERO, sin construir nada: (1) comprueba con `gh` que main contiene .github/workflows/{ci,migrar,respaldo}.yml y que la última CI de main quedó en verde y publicó las imágenes (si no, dímelo y detente); (2) crea la rama fase-16c-nube-1 desde main y dime el comando si no puedes crearla tú; (3) preséntame un resumen de una página del plan y PREGÚNTAME una por una las decisiones D8 a D17 con AskUserQuestion (tu recomendación primero, con su motivo). No empieces a construir hasta que yo las responda: aprobar el plan no es construir.

Contexto que debes tener presente:
- Decisiones definitivas para costo $0 (D1 a D7 del 22-11-2026): Azure Blob Storage para los archivos, Render plan gratuito, Gmail por SMTP con cambio a un proveedor por HTTPS si Render lo bloquea, respaldos diarios en Blob con retención de 30 días, región US East, y ninguna dependencia nueva salvo Azure.Storage.Blobs.
- Hallazgos nuevos (plan, sección 1): Render gratuito BLOQUEA SMTP (H11), los archivos van hoy a disco y Render gratuito no tiene disco (H12), Render se duerme y la API se conecta a la base al arrancar (Program.cs:357), con Azure SQL en pausa puede no levantar (H13 y H14).
- En este equipo NO hay Docker y no lo instales: lo real se verifica en GitHub Actions (ya hay un job que levanta compose). Las pruebas de Blob usan Azurite en un contenedor de la CI (versión fija), no un servicio de la nube.

Alcance de ESTA sesión: solo la 16C-1. Orden, con pruebas primero y fallos a propósito: (1) IAlmacenArchivos con disco y Blob, las rutas que devuelven Results.File(ruta…) pasan a flujos, purgas por antigüedad, Almacen__Modo validado al arrancar; (2) enviador de correo por HTTPS detrás de IEnviadorCorreo (el proveedor lo decido yo en D8; no hagas llamadas reales al proveedor); (3) el arranque en producción tolera una base en pausa; (4) pantalla "el sistema está despertando" con reintento en el frontend; (5) render.yaml gratuito sin disco y ajuste de scripts/validar-contenedores.py y sus pruebas; (6) docs/despliegue.md y desviaciones.md. La CI debe quedar en verde (sube los pisos de pruebas de ci-resumen.py cuando agregues pruebas). Al terminar: revisiones de ECC (csharp-reviewer, security-reviewer, database-reviewer, react-reviewer, docker-patterns), informe docs/plan/informes/fase-16c-1.md, tablero y trazabilidad, y PARADA para mi visto bueno.

Reglas:
- No hagas commit ni push: pásame los comandos de PowerShell, cada uno en su propio bloque, y agrega solo las carpetas de la fase (no toques .impeccable/ ni el archivo "python"). Yo subo la rama y tú lees el resultado de la CI con `gh run view` cuando te diga "mira tú".
- No crees ninguna cuenta, servicio, proyecto ni base en Azure, Render, Vercel ni en ninguna otra plataforma, y no uses los conectores de esas plataformas para escribir. No apliques migraciones a ninguna base real. No escribas credenciales, claves ni el ID de cliente de Google en ningún archivo ni las pidas en el chat: solo nombres de variables y de secretos.
- Si arranco mi API demo contra LetreriaLosHerreraDB_Demo se aplican las migraciones a esa base: avísame antes de cualquier prueba local que escriba en una base y, si hay una API demo (7112) o el frontend (3000) en marcha que bloquee la compilación, pídeme que la detenga.
- El plugin ECC tiene un control (GateGuard) que rechaza la primera escritura de cada archivo y los comandos destructivos: expón los hechos y reintenta; no lo desactives.
- Los .sh se ejecutan con `bash` (git los guarda sin bit de ejecución). Para mis comandos en PowerShell, nada de printf, openssl ni sed.
- Cuando necesites que haga algo en GitHub o en un panel, dame los pasos EXACTOS (en qué pestaña, qué botón, qué escribir) y espera mi resultado. No empieces la 16C-2 ni la 16C-3.
```

---

## Prompt 2 — 16C-2: Azure (cada creación con mi permiso)

```
Proyecto SyncTime. La 16C-1 está APROBADA por mí y fusionada a main. Quiero la fase 16C, PARTE 16C-2 (Azure), siguiendo docs/plan/fase-16c-plan.md (secciones 2, 5, 7 y 8), docs/plan/informes/fase-16c-1.md, docs/despliegue.md (secciones 4 a 6: migrador, workflows, entornos y secretos de GitHub) y las decisiones D8 a D17 que ya respondí (están en el informe de la 16C-1).

PRIMERO, sin crear nada: comprueba con el conector de Azure (solo lectura) o con mis capturas el TIPO DE SUSCRIPCIÓN (la oferta gratuita de SQL es incompatible con "Azure for Students Starter") y las regiones donde existe la oferta gratuita de SQL; resúmeme el consumo esperado y el costo de cada recurso con la herramienta de precios; y pregúntame si apruebo empezar.

Regla central: ANTES DE CADA CREACIÓN te detienes y me preguntas con este molde: "PARADA DE CREACIÓN: qué se crea · dónde y con qué nombre · plan o nivel · costo esperado · cómo se borra · qué necesito de ti (clics exactos y los datos que YO escribo en el panel). ¿Apruebo crear esto?". Solo creas, o me das los pasos, después de mi "sí" a esa parada concreta. Un "sí" no vale para la siguiente.

Orden (plan, sección 5): presupuesto de 1 USD con alertas (lo hago yo en el portal, con tus pasos), grupo de recursos synctime-rg, servidor SQL y base gratuita con auto-pausa hasta el mes siguiente, usuarios de la base (aplicación, migración y respaldo; las contraseñas las escribo yo), cuenta de almacenamiento y contenedores privados archivos y respaldos con ciclo de vida de 30 días, identidad para GitHub por OIDC (una por entorno, permisos mínimos), entornos y secretos de GitHub (azure-sql-produccion con mi aprobación y ramas solo main; azure-respaldo), primera migración con migrar.yml (la más delicada: respaldo previo verificado, migrador, comprobación; incluye la migración de la hora de Chile), primer respaldo con respaldo.yml y restauración probada de principio a fin en localhost\SQLEXPRESS con OTRO nombre de base (nunca sobre LetreriaLosHerreraDB). Los workflows migrar.yml y respaldo.yml nunca se han ejecutado: léelos con `gh run view` y corrige lo que falle, como en la 16B.

Reglas: las de la 16C-1 (sin commit ni push, sin credenciales en el chat ni en archivos, GateGuard, comandos de PowerShell en bloques separados). Ninguna contraseña ni clave pasa por ti: yo las escribo en el portal o en los secretos de GitHub y tú solo verificas que existan por su NOMBRE. No abras el cortafuegos de Azure SQL a todo internet. Al terminar: revisiones de ECC (security-reviewer y database-reviewer en particular), informe docs/plan/informes/fase-16c-2.md con el consumo de Azure, evidencia en docs/evidencias/, tablero y trazabilidad, y PARADA para mi visto bueno. No empieces la 16C-3.
```

---

## Prompt 3 — 16C-3: Render, Vercel y cierre (cada creación con mi permiso)

```
Proyecto SyncTime. La 16C-2 está APROBADA por mí (Azure SQL, Blob, respaldo y restauración probados). Quiero la fase 16C, PARTE 16C-3 (Render, Vercel y cierre), siguiendo docs/plan/fase-16c-plan.md (secciones 2, 6, 7 y 8), docs/plan/informes/fase-16c-2.md, docs/despliegue.md y render.yaml / frontend/synctimefront/vercel.json, con las decisiones D8 a D17 ya respondidas.

Misma regla central que en la 16C-2: ANTES DE CADA CREACIÓN (servicio de Render, proyecto de Vercel, monitor externo si elegí D9, cualquier cuenta) te detienes con el molde "PARADA DE CREACIÓN" y esperas mi "sí" a esa creación concreta. Las variables y secretos los escribo yo en cada panel; tú verificas por NOMBRE.

Orden (plan, sección 6): servicio de Render desde el Blueprint (plan gratuito, virginia, construcción desde el repositorio, autoDeployTrigger off); copiar la URL REAL que Render asigne (si el nombre ya existe, Render agrega un sufijo: no uses una URL que no sea mía); medición de memoria con carga y, si 512 MB no alcanza, PARA y pregúntame (plan de pago pequeño o Azure Container Apps); proyecto de Vercel con vercel.json apuntando a la URL real y dominio *.vercel.app con certificado válido; CORS, Sitio__UrlPublica y el origen de Vercel en la consola de Google Cloud (pasos exactos para mí; el ID de Google no se escribe en ningún archivo); medir la IP real del visitante con Vercel → Render y ajustar ForwardedHeaders al número exacto de saltos; pruebas de humo (salud, portada, ingreso con correo y con Google, solicitud con adjunto que queda en Blob, exportación, correo real, salida); carga de 20 usuarios contra la API en Render y Lighthouse contra Vercel; tiempo de despertar con Render dormido y con la base en pausa (con y sin monitor); nota de disponibilidad de RNFP04 (qué se cumple y qué no, con la capa gratuita); guía de despliegue completa, inventario de licencias con su vista pública (RNFO17, RNFE09, S22) y eliminación de dependencias sin uso (RNFO16). Cierra con revisiones de ECC, informe docs/plan/informes/fase-16c-3.md y un informe global de la 16C, tablero y trazabilidad con evidencia real, y PARADA para mi visto bueno.

Reglas: las de la 16C-1 y la 16C-2. No dejes nada abierto sin decírmelo: al final, la lista de TODO lo que existe en la nube, su costo y cómo se borra.
```
