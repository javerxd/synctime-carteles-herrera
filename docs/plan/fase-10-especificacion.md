# Especificación técnica de la fase 10 (contrato entre backend y frontend)

Acompaña a `fase-10-plan.md`, aprobado el 15-11-2026 con tres decisiones del usuario. Si algo no está aquí, decide lo más simple y seguro, coherente con el código actual del rostro, y anótalo en tu respuesta.

## Decisiones fijas
- **Quién usa el rostro:** solo los roles `dueño` y `administrador`.
  - **Al ingresar**, los demás roles reciben el mismo **401 genérico** que cualquier fallo.
  - **Al registrar**, reciben **403** `solo_gestion`.
- **Se mantiene:**
  - el email primero, con comparación 1 a 1;
  - el señuelo cuando no hay rostro;
  - el tiempo mínimo de cada fallo;
  - el bloqueo tras 5 fallos durante 15 minutos;
  - el limitador `LoginFacial` (10 por minuto por IP);
  - el **equipo autorizado** para dueño y administrador (decisión 2);
  - el consentimiento con versión, que se actualiza si cambia el texto;
  - el cifrado AES-GCM con datos asociados `modelo:idEmpleado`.
- **Nunca se registran en los logs** descriptores, puntos, tokens ni desafíos. Nunca se guardan imágenes.
- **RNFP12** (decisión 1): el servidor responde el `POST /auth/login-facial` en **3 segundos o menos**, desde que llegan las capturas. Se mide en una prueba. El flujo guiado completo dura de 6 a 9 segundos.

## Medida del giro (la misma en el navegador y en el servidor)

Los **68 puntos** se envían en coordenadas del **cuadro de video sin espejo** (la imagen tal como la entrega la cámara), junto con `ancho` y `alto`.

- **Índice de giro:**
  - **Cálculo:** `r = (x[30] − x[0]) / (x[16] − x[0])`, es decir, la punta de la nariz entre los extremos de la mandíbula; luego `g = (r − 0.5) × 2`, limitado a [−1, 1].
  - **Convención:** **`g > 0` es un giro hacia la izquierda de la persona** (en la imagen sin espejo, su lado izquierdo queda a la derecha). Con `g < 0`, hacia su derecha.
  - **Datos inválidos:** si `x[16] − x[0]` es menor que el 10 % del ancho, o algún punto no es finito o queda fuera del cuadro, la captura no es válida.
- **Rangos configurables**, en `RostroFacial:*`:
  - **`FrenteMaximo`:** `|g|` máximo de la captura de frente. Por defecto 0.15.
  - **`GiroMinimo`:** por defecto 0.30.
  - **`GiroMaximo`:** por defecto 0.75.
  - Estos valores corresponden, aproximadamente, a los 30–45 grados que se piden; la calibración los ajusta.
- **Umbrales configurables:**
  - `UmbralDistancia` (por defecto 0.5): distancia máxima entre una captura y la muestra de su lado.
  - `UmbralEntreCapturas` (por defecto 0.6): distancia máxima entre dos capturas del mismo intento.
- **Sin repetir código:** el cálculo vive en una sola función de cada lado (`Negocio/Rostro/Giro.cs` y `src/rostro/giro.js`), con pruebas que usan los mismos casos.

## Base de datos (migración nueva `RostroConPruebaDeVida`, solo agrega)

**`rostros_empleado`:**
- **Columnas nuevas:** `descriptor_frente`, `descriptor_izquierda` y `descriptor_derecha` (varbinary null, cifradas igual que hoy), `id_empleado_registra` (int null, FK a `empleados`: quien inició el registro) y `formato_muestras` (tinyint null; 2 = tres muestras).
- **`descriptor_cifrado`, el antiguo:** deja de usarse.
  - **Rostro activo sin las tres muestras:** cuenta como **«debe registrarse de nuevo»**. El ingreso falla con el 401 genérico y la pantalla lo indica al titular.
  - **Al revocar:** se borran las cuatro columnas cifradas.

**`desafios_rostro`** (nueva):

| Columna | Tipo y reglas |
|---|---|
| `id_desafio` | int, identity |
| `token_hash` | char(64), único: SHA-256 del token |
| `correo_hash` | char(64): SHA-256 del email normalizado |
| `orden` | `izquierda_derecha` o `derecha_izquierda` |
| `emitido` | datetimeoffset |
| `vence` | datetimeoffset: emitido + `RostroFacial:SegundosDesafio`, por defecto 30 |
| `usado` | datetimeoffset null |

- La purga diaria (`PurgaDatosPersonales`) borra los que tengan más de un día.

**`calibracion_rostro`** (nueva, **sin datos biométricos**):

| Columna | Tipo y reglas |
|---|---|
| `id_medicion` | int, identity |
| `id_empleado` | FK: el rostro contra el que se comparó |
| `etiqueta` | `propio` u `otra_persona` |
| `distancia_frente`, `distancia_primer_giro`, `distancia_segundo_giro` | float |
| `distancia_max_entre_capturas` | float |
| `giro_frente`, `giro_primero`, `giro_segundo` | float: los valores de `g` |
| `umbral_vigente` | float |
| `id_empleado_registra` | FK |
| `fecha` | datetimeoffset |

**`Down()`:** se detiene con `THROW` si las tablas nuevas o las columnas nuevas tienen datos.

**Script:** se regenera `docs/base-de-datos/LetreriaLosHerreraDB.sql`.

## Rutas

**Ingreso:**
- **`POST /api/v1/auth/login-facial/desafio`**
  - **Cuerpo:** `{ email }`. Es anónimo, con el limitador `LoginFacial`.
  - **Respuesta:** **siempre 200**, exista o no el email: `{ desafio, orden: ["izquierda","derecha"] | ["derecha","izquierda"], venceEnSegundos }`.
  - **El token:** 32 bytes aleatorios en base64url. El servidor guarda solo su hash.
  - **El orden:** lo elige el servidor con `RandomNumberGenerator`.
  - Si el rostro no está configurado (`RostroFacial:ClaveCifrado` falta), responde 503, como hoy.
- **`POST /api/v1/auth/login-facial`**
  - **Cuerpo:** `{ email, desafio, tokenEquipo, capturas: [c1, c2, c3] }`, donde cada `c` es `{ descriptor: float[128], puntos: [[x,y] × 68], ancho, alto }`.
  - **Orden de las capturas:** frente, primer giro y segundo giro.
  - **Tamaño máximo:** sube a 32 KB.
  - **Rechazo:** el mismo 401 genérico, con el tiempo mínimo, si falla cualquier cosa:
    - el formato;
    - el desafío: inexistente, vencido, ya usado o de otro email;
    - el rol;
    - el empleado inactivo;
    - un rostro sin las tres muestras;
    - el equipo no autorizado;
    - el bloqueo;
    - el giro: la de frente con `|g| > FrenteMaximo`, o un giro con el signo contrario al orden o fuera de `[GiroMinimo, GiroMaximo]`;
    - una captura que no coincide con su muestra: frente con frente, y cada giro con la muestra de su lado según el orden;
    - dos capturas entre sí a más de `UmbralEntreCapturas`.
  - **Cuándo se marca usado el desafío:** al principio, en una sola sentencia `UPDATE … WHERE usado IS NULL AND vence > ahora AND token_hash = … AND correo_hash = …`, que debe afectar 1 fila. Así no puede usarse dos veces, aunque lleguen dos envíos a la vez.
  - **Las comparaciones se hacen siempre**, también contra el señuelo, para que todo fallo cueste lo mismo.
  - **Intentos:** se cuentan como hoy, solo con un equipo válido.
  - **Éxito:** la sesión, como hoy, y además `{ nombre, rol }` (S15).

**Registro:**
- **`PUT /api/v1/auth/rostro`** (el titular, solo gestión)
  - **Cuerpo:** `{ password, consentimiento, versionConsentimiento, muestras: { frente: c, izquierda: c, derecha: c } }`.
  - **Validaciones:**
    - el giro de cada muestra: la de frente con `|g| ≤ FrenteMaximo`, la izquierda con `g ∈ [GiroMinimo, GiroMaximo]` y la derecha con `g ∈ [−GiroMaximo, −GiroMinimo]`;
    - que las tres sean de la misma persona (`UmbralEntreCapturas`).
  - **Si algo no cumple:** 400, con `errores.muestras.frente`, `errores.muestras.izquierda` o `errores.muestras.derecha` y un texto que explique cómo repetirla.
  - **Lo que guarda:** las tres muestras cifradas, `formato_muestras = 2`, `id_empleado_registra` (el mismo titular) y el token de equipo nuevo, como hoy.
- **`GET /api/v1/auth/rostro`:** como hoy, más `debeRegistrarseDeNuevo` y `puedeUsarRostro` (si es de gestión).
- **`DELETE /api/v1/auth/rostro`:** como hoy. Borra las cuatro columnas cifradas.

**«Rostros registrados» (S16, gestión):**
- **`GET /api/v1/rostros`**
  - **Qué lista:** los empleados **activos de gestión**.
  - **Cada fila:** `{ idEmpleado, nombre, rol, estado: "activo"|"debe_registrarse"|"sin_registro"|"desactivado", fechaRegistro, fechaConsentimiento, registradoPor, fechaDesactivacion }`.
  - **El historial:** también trae las constancias revocadas de cada uno, las más recientes primero.
- **`PUT /api/v1/rostros/{idEmpleado}`**
  - **Uso:** gestión registra el rostro de **otro** empleado de gestión, con el titular presente.
  - **Cuerpo:** `{ passwordTitular, consentimiento, versionConsentimiento, muestras }`.
  - **Validación de la contraseña:** se valida la **contraseña del titular** con el limitador `Reautenticacion`. Si es incorrecta, responde 400 `datos_invalidos`.
  - **Lo que guarda:** `id_empleado_registra` = quien inició el registro.
  - **Errores:** si el titular no es de gestión, 403. Si es uno mismo, se usa `/auth/rostro`.
  - **Equipo:** no entrega un token de equipo. El titular autoriza su equipo desde su perfil.
- **`DELETE /api/v1/rostros/{idEmpleado}`:** desactiva y borra las muestras. Puede usarlo gestión, y el titular sobre sí mismo. Responde 204.

**Calibración (decisión 3, gestión, 404 si `RostroFacial:Calibracion:Activa` no es true):**
- **`POST /api/v1/rostros/calibracion`**
  - **Cuerpo:** `{ idEmpleado, etiqueta: "propio"|"otra_persona", desafio, capturas: [c1,c2,c3] }`. El desafío se pide con la misma ruta del ingreso.
  - **Lo que hace:** calcula las distancias contra las muestras del empleado y entre capturas, y los `g`. Guarda **solo números**.
  - **Respuesta:** **201** con la medición y si «habría entrado» con el umbral vigente.
- **`GET /api/v1/rostros/calibracion`**
  - **Respuesta:** `{ activa, umbralVigente, rangos, mediciones: [...], resumen: { propio: { n, min, max, promedio }, otra: { n, min, max, promedio }, umbralSugerido, margen } }`.
  - **`umbralSugerido`:** si las series se separan, el punto medio entre el máximo de «propio» y el mínimo de «otra»; si no, el valor que minimiza los errores, con `margen` negativo. Se calcula sobre la **peor distancia de cada intento**, el máximo de las tres.
- **`DELETE /api/v1/rostros/calibracion`:** borra todas las mediciones (gestión). Responde 204.

## Frontend

**Motor y giro:**
- **`src/rostro/motorFacial.js`:** expone la detección por cuadro, con cada rostro, sus 68 puntos y su descriptor, para el flujo guiado.
- **`src/rostro/giro.js`:** calcula `g` igual que el servidor. Las coordenadas son **sin espejo**: solo el `<video>` se muestra espejado con CSS.

**Componente guiado** (`src/rostro/FlujoFacial.jsx` o similar), usado para ingresar, registrar y calibrar:
- **Encuadre:** un óvalo.
- **Guía en vivo** según la detección: «Acércate» (rostro chico), «Aléjate» (rostro grande), «Falta luz» (brillo medio bajo), «Debe verse un solo rostro», «Centra tu rostro», «Gira un poco más» y «Gira un poco menos».
- **Pasos:** frente, y luego cada giro **en el orden del desafío**.
  - En cada giro, una flecha animada hacia el lado pedido y un progreso que se llena según `g`.
  - Cada paso **avanza solo** cuando `g` está en rango durante unos 300 ms.
  - Cada giro tiene 4 segundos y **un reintento automático**. Si falla de nuevo, el intento termina.
- **Escaneo:** una animación durante todo el proceso.
- **Con `prefers-reduced-motion`:** sin animación. La flecha queda fija, y el progreso es una barra con texto.
- **Instrucciones:** en texto, con `aria-live` cortés y los cambios de paso anunciados.
- **Cámara:**
  - se enciende solo al pulsar «Entrar con rostro» o «Registrar»;
  - hay un indicador visible «Cámara activa»;
  - «Cancelar» está siempre disponible y apaga la cámara.
- **Errores:** cámara denegada, sin cámara, sin rostro, varios rostros, poca luz, tiempo agotado y error de red, cada uno con su texto.
  - El rechazo del servidor es siempre el mismo: «No pudimos confirmar que eres tú. Inténtalo de nuevo o entra con tu contraseña.»
  - Tras 3 rechazos seguidos, el foco va a la contraseña y se resalta.

**`/personal/ingresar`:**
- **Pasos:** se escribe el email, se pulsa «Entrar con rostro» y se pide el desafío; luego viene el flujo guiado y el envío.
- **Al entrar:** se muestra «Entraste como {nombre} ({rol})».
- **La contraseña sigue siempre visible.**

**Perfil, sección de rostro:**
- **Quién la ve:** solo gestión. Los demás no ven la opción.
- **Registrar:** el registro usa el flujo guiado, con **vista previa de cada muestra** (imagen local en un canvas, que no se envía) y «Repetir esta muestra».
- **Otras acciones:** autorizar el equipo y desactivar, como hoy.
- **Aviso:** si `debeRegistrarseDeNuevo`, «Tu rostro debe registrarse de nuevo con las tres muestras».

**`/panel/rostros`, «Rostros registrados» (gestión):**
- una tabla con las columnas de S16 y el historial;
- «Registrar rostro» para otro empleado de gestión, con el flujo guiado y la contraseña del titular, escrita por él;
- «Desactivar», con confirmación.

**`/panel/rostros/calibrar`:**
- **Cuándo aparece:** solo si la calibración está activa.
- **Para medir:** se elige el empleado y la etiqueta («Soy yo» u «Otra persona») y se hace un intento guiado.
- **Lo que muestra:** la tabla de mediciones y el resumen con el umbral sugerido y el margen, más «Borrar mediciones».
- **Lo que no hace:** no cambia la configuración. El umbral se copia a mano a `RostroFacial:UmbralDistancia`.

**`index.html`:** la CSP suma `worker-src 'self' blob:`. Se comprueba que el motor carga sin el error del *worker*.

**Diseño:** sigue `DESIGN.md` de la fase 2.
