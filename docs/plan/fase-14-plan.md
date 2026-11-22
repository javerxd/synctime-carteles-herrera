# Plan de la fase 14 — Ingreso con Google

Fecha: 21-11-2026 · Ramas: `fase-14a-google-backend` y `fase-14b-google-frontend` (la primera nace de `fase-13b-reportes-frontend`, commit `5b5c04b`; la segunda, de la 14A) · Estado: **aprobado por el usuario el 21-11-2026** (las diez decisiones, tal como se propusieron); construcción sin iniciar

Origen: el plan general (`plan-de-construccion.md`, fase 14, con el ajuste del 20-11-2026: **sin asistente de IA**, solo el botón de Google), los requisitos RFS03 (Google), RFS12 (consentimiento al completar el registro), RFU05 (la parte de Google), RFU06 (enviar una solicitud con cuenta de Google), E02, RNFE02, RNFP17 y RNFO13, y los datos que me diste: el ID de cliente OAuth de Google Cloud, el frontend en `http://localhost:3000` y la API en `https://localhost:7112`.

Qué hay hoy: el cliente ingresa solo con correo y contraseña (`POST /auth/login-cliente`) y se registra con el formulario completo (`POST /auth/registro-cliente`: nombre, apellido, **teléfono obligatorio y único**, RUT opcional, contraseña y consentimientos). La sesión es un token de 15 minutos más una cookie `httpOnly` de renovación (`ServicioSesiones`). La política de seguridad de contenido de `index.html` es `script-src 'self'`, así que **Google no carga hasta que se amplíe**. **No hay** ninguna pieza de Google en el código.

Esta fase **sí necesita una migración** (tabla `identidades_externas`) y **una dependencia nueva** (`Google.Apis.Auth`, Apache-2.0): ambas están en las decisiones del final.

## 0. Cómo se divide (como la 13)

- **14A — Backend.** Verificación del token de Google, los endpoints, la tabla, auditoría, pruebas con un verificador simulado y revisiones de ECC. Parada con informe.
- **14B — Frontend.** El botón, la pantalla «Completa tu registro», «Ingreso con Google» en Mi cuenta, la política de contenido, pruebas con un script de Google simulado y revisiones de ECC. Parada con informe.

## 1. Cómo funciona el ingreso (token de identidad, sin clave secreta)

Se usa **Google Identity Services** con el *token de identidad* (un JWT firmado por Google) que el navegador entrega a nuestra API. **No hay redirección ni «secreto de cliente»**: la API solo verifica la firma y las condiciones del token. Por eso la API (`https://localhost:7112`) **no se registra en Google**: solo importa el origen de la página.

1. La persona pulsa «Continuar con Google» en `/ingresar`. **Recién ahí** se carga el script de Google (nadie le avisa a Google que visitó la página).
2. La página pide un **nonce** a `GET /auth/google/nonce` (aleatorio, de un solo uso, vence en 5 minutos) y se lo pasa a Google.
3. Google devuelve el token. La página lo manda a `POST /auth/google` con el nonce.
4. La API lo verifica y decide entre **tres caminos**:
   - **La identidad de Google ya está vinculada** a una cuenta activa → abre la sesión (mismos cookie y token que el ingreso con contraseña).
   - **El correo ya tiene una cuenta con contraseña** que no está vinculada → **no se fusiona sola**. Responde `409` (`cuenta_existente`): «Ya tienes una cuenta con este correo. Ingresa con tu contraseña y vincula Google desde Mi cuenta.»
   - **Es un correo nuevo** → responde `200` con `estado: registro_pendiente` y un *pase* firmado (vence en 10 minutos, de un solo uso) con el correo y el nombre que dio Google. La pantalla **«Completa tu registro»** pide lo que Google no entrega: apellido (si falta), **teléfono**, RUT opcional y dirección opcional, y las **casillas de consentimiento sin marcar** (términos y privacidad; marketing, opcional). Con eso `POST /auth/google/completar` crea la cuenta (con el correo ya verificado, sin contraseña), la identidad y los consentimientos en una sola transacción, y abre la sesión.

## 2. Qué verifica la API en el token (RNFE02, RNFP17)

- Firma de Google, emisor (`accounts.google.com`), **audiencia = nuestro ID de cliente**, vencimiento y **nonce** igual al emitido y aún sin usar.
- **`email_verified` verdadero**, `email` y `sub` presentes. Si el correo no está verificado, `401` y nada se crea.
- **La identidad es el `sub`**, no el correo (el correo de una persona puede cambiar). Un mismo `sub` no puede estar en dos cuentas, ni una cuenta con dos Google.
- Una cuenta dada de baja o desactivada no entra por Google, y el mensaje no revela que existía.
- **Si Google falla o tarda** (5 segundos como máximo), la API responde el error estándar (`503`, código `google_no_disponible`), sin bloquear el resto. El token, el nonce y el pase **nunca** se guardan en registros ni en respuestas de error.
- Límite de tasa del ingreso (política `Login`, 10 por minuto por IP) y tamaño máximo del cuerpo de 8 KB.
- Sin el ID de cliente configurado, `POST /auth/google` responde `503` (`google_no_configurado`) y el frontend **no muestra el botón**.

## 3. Datos

Migración **`IngresoConGoogle`** (solo en la base demo hasta el final, como las anteriores):

| Tabla `identidades_externas` | |
|---|---|
| `id_identidad` | clave |
| `id_cliente` | FK a `clientes` |
| `proveedor` | `google` (con restricción CHECK, para poder sumar otros) |
| `sub` | identificador estable de Google; **único por proveedor** |
| `correo` | el que verificó Google al vincular |
| `fecha_vinculacion`, `fecha_ultimo_ingreso` | para la bitácora |

`clientes.password_hash` **ya admite nulo**: una cuenta creada con Google no tiene contraseña, y el ingreso con contraseña la rechaza igual que a un correo sin cuenta (`VerificarOFingir`). La tabla se audita como las demás, y hay acciones nuevas de bitácora: `ingreso_google`, `vincular_google`, `desvincular_google` (la bitácora no restringe la lista de acciones). Se actualizan el diccionario de datos y el MER.

## 4. Endpoints (14A)

| Ruta | Quién | Qué hace |
|---|---|---|
| `GET /api/v1/auth/google/nonce` | anónimo | Entrega el nonce de un solo uso. |
| `POST /api/v1/auth/google` | anónimo | Los tres caminos de la sección 1. |
| `POST /api/v1/auth/google/completar` | anónimo, con el pase | Crea la cuenta con sus consentimientos y abre la sesión (`201`). Mismas validaciones que el registro por formulario: teléfono y RUT ya conocidos dan el **mismo mensaje** de siempre, y la versión de los documentos legales debe ser la vigente. |
| `POST /api/v1/mi/cuenta/google` | cliente con sesión | **Vincula** su Google: pide la contraseña actual (como las demás acciones delicadas) y exige que el correo verificado de Google **sea el de la cuenta**. |
| `DELETE /api/v1/mi/cuenta/google` | cliente con sesión | Desvincula, con la contraseña. **No se puede si la cuenta no tiene contraseña** (quedaría sin forma de entrar). |

`openapi.json`, `contrato.md` y `permisos.md` se actualizan, y el contrato se verifica contra la API como siempre. Para registrar el cliente se **reutiliza** el código de `ServicioMiCuenta.RegistrarAsync` (sin duplicarlo). El personal **no** entra por Google: RFU05 lo limita al cliente.

## 5. Frontend (14B)

- **`/ingresar`** (tras «¿Quién está iniciando sesión?», 12C): debajo del formulario, la separación «o» y el botón «Continuar con Google». Al pulsarlo carga el script y muestra el **botón oficial de Google** (es lo que Google exige para el ingreso) con un texto breve que lo explica; **un clic más**. Si el script no carga (bloqueador, sin red), un aviso claro y la contraseña sigue disponible.
- **`/cuenta/google/completar`** (14B): «Completa tu registro», con el correo y el nombre de Google a la vista, el formulario de lo que falta y las casillas de consentimiento, con el foco al primer error y los textos de siempre. Si el pase venció, vuelve a `/ingresar` con un aviso.
- **Mi cuenta → «Ingreso con Google»:** estado (vinculado o no), «Vincular Google» y «Desvincular» (con la contraseña). Una cuenta sin contraseña lo explica y ofrece «Crear una contraseña» (el flujo de recuperación que ya existe, con código al correo).
- **Política de seguridad de contenido** (`index.html`, que también aplica en desarrollo): se agregan **solo** los orígenes que Google documenta: `script-src https://accounts.google.com/gsi/client`, `frame-src https://accounts.google.com/gsi/`, `connect-src https://accounts.google.com/gsi/` y `style-src https://accounts.google.com/gsi/style`. Nada más se abre. En producción (fase 16) el servidor **no debe** enviar `Cross-Origin-Opener-Policy: same-origin`, porque bloquea la ventana de Google: queda anotado para esa fase.
- **Accesibilidad y diseño:** el botón de Google conserva su aspecto oficial, pero todo lo demás (separador, avisos, pantalla de registro) usa los tokens y los componentes del sistema, en modo claro; foco visible, errores con su texto, anuncios en regiones de estado, a 360, 768 y 1440 px.

## 6. Configuración y secretos (RNFP17)

El ID de cliente **no es un secreto** (cualquiera lo ve en el navegador), pero lo tratamos como configuración y **no lo escribo en el repositorio**:

- **API:** `Google:ClientId`, por *User Secrets* en desarrollo y por variable de entorno en producción. `appsettings.json` lleva el campo vacío.
- **Frontend:** `VITE_GOOGLE_CLIENT_ID` en `frontend/synctimefront/.env.local` (ya ignorado por git); `.env.example` lleva el campo vacío.
- **No hay clave secreta de cliente**: este flujo no la usa, y por eso no hay nada que filtrar.

## 7. Lo que tienes que hacer tú (en la consola de Google Cloud y en tu equipo)

Todo esto es manual: yo no entro a tu cuenta de Google ni escribo credenciales. Te dejo los pasos exactos al llegar a la 14A:

1. **En Credenciales → tu ID de cliente OAuth**, confirmar que el tipo es **«Aplicación web»**, y en **Orígenes de JavaScript autorizados** agregar **`http://localhost:3000`** y también **`http://localhost`** (Google lo pide para pruebas locales). **No** hace falta ningún URI de redirección ni registrar `https://localhost:7112`.
2. **En «Pantalla de consentimiento de OAuth»**: alcances solo `openid`, `email` y `profile` (no sensibles). Si la app está en estado **«Prueba»**, agregar como *usuarios de prueba* los correos de Google con los que vas a ingresar; sin eso, Google rechaza el ingreso con un mensaje de acceso denegado.
3. **Guardar el ID en tu equipo** con dos comandos que te paso yo y ejecutas tú (User Secrets para la API y `.env.local` para el frontend).
4. **Probar el ingreso real en tu navegador**, con una cuenta de Google tuya: no se puede automatizar contra Google. Te dejo el guion (ingreso de cuenta nueva, ingreso repetido, un correo que ya tiene cuenta con contraseña, vincular y desvincular).

Si te falta algo de la lista, **me detengo y te pregunto** antes de seguir, como pediste.

## 8. Pruebas primero y revisiones

**14A (backend)**
- **Unitarias (con un verificador simulado detrás de una interfaz, `IVerificadorGoogle`):** token válido; **audiencia ajena**, emisor ajeno, vencido, **correo no verificado**, sin `sub` ni correo; nonce ausente, distinto o **reutilizado**; pase vencido, manipulado o reutilizado; la regla de «no fusionar»; que sin contraseña no se puede desvincular.
- **Integración (xUnit contra SQL Server), con roles:** cuenta nueva completa (cuenta, identidad y consentimientos, en una transacción; si falla un consentimiento no queda nada); ingreso repetido por `sub` aunque el correo de la cuenta haya cambiado; correo existente con contraseña (`409` y nada se modifica); teléfono o RUT ya conocidos (el mismo mensaje de siempre); cuenta inhabilitada; versión de términos desactualizada; un `sub` en dos cuentas (lo impide la base); vincular y desvincular con y sin contraseña; **el personal y el visitante** no pueden usar los endpoints de Mi cuenta (`403` y `401`); límite de tasa; **Google caído o lento** (`503` en menos de 5 s, sin bloquear otras peticiones); la API sin `Google:ClientId`.
- **Seguridad:** ni el token, ni el nonce, ni el pase aparecen en registros ni en respuestas; el contrato OpenAPI coincide; la bitácora lleva `ingreso_google`, `vincular_google` y `desvincular_google` con quién y cuándo.
- **Fallos a propósito (se detectan):** quitar la verificación de `email_verified`, quitar la comprobación de audiencia y permitir la fusión automática, para comprobar que las pruebas fallan.
- **ECC:** `csharp-reviewer` y `security-reviewer` (suplantación de identidad, fusión de cuentas, repetición del token, nonce, secretos, límites).

**14B (frontend)**
- **Vitest:** el botón aparece solo con el ID configurado; el script se carga **solo al pulsar** (y no antes); los tres caminos de la respuesta; «Completa tu registro» (validación, foco al primer error, casillas sin marcar); Mi cuenta con y sin contraseña; el pase vencido; que el token no se guarda en `localStorage` ni en la URL.
- **Playwright con axe a 360, 768 y 1440 px**, con el script de Google **simulado** (se intercepta `accounts.google.com/gsi/client` y se entrega un reemplazo de `google.accounts.id`): el ingreso, la pantalla de completar, el aviso de «cuenta existente» y el script bloqueado; sin desplazamiento horizontal ni errores en la consola, **y sin violaciones de la política de contenido**.
- **ECC:** `react-reviewer` y `a11y-architect`.
- **Verificación en la app real**, contra la base demo y con tu cuenta de Google (sección 7). Esto **escribe** en la demo (cuenta, identidad, consentimientos, bitácora): te aviso antes.

**Cierre de cada parte:** informes `informes/fase-14a.md` y `fase-14b.md`, tablero, trazabilidad (RFS03, RFS12, RFU05, RFU06, E02, RNFE02, RNFP17, RNFO13), `contrato.md`, `permisos.md`, `openapi.json`, diccionario de datos, MER y `licencias.md` (la dependencia nueva), `desviaciones.md` y commit en la rama. Los comandos de git te los paso; yo no subo nada.

## Decisiones y supuestos que te dejo a la vista

1. **Dependencia nueva a tu aprobación: `Google.Apis.Auth`** (Google, Apache-2.0), que verifica el token con las claves públicas de Google y las mantiene al día. Alternativa: verificar el JWT a mano con `System.IdentityModel.Tokens.Jwt` y descargar las claves de Google nosotros mismos, que es más código propio y más riesgo. Verifico versión, licencia y vulnerabilidades antes de instalar. La verificación real con Google solo la compruebas tú con una cuenta real; la lógica de las condiciones sí se prueba por completo con el verificador simulado.
2. **Nunca se fusiona solo** (como dice el plan general): si el correo ya tiene cuenta con contraseña, se pide ingresar con ella y vincular desde Mi cuenta. Alternativa más cómoda: tras ingresar con la contraseña, vincular automáticamente lo que Google acababa de entregar; la evito porque exige guardar el token de Google varios minutos.
3. **Vincular exige que el correo de Google sea el de la cuenta** y la contraseña actual. Una cuenta con otro correo de Google no se puede vincular (evita que alguien con su propio Google se «cuelgue» de una cuenta ajena).
4. **Una cuenta nacida en Google no tiene contraseña.** Las acciones delicadas (cambiar correo o contraseña, bajar los datos, dar de baja) piden la contraseña actual: esa persona primero **crea una contraseña** con el código al correo (la recuperación que ya existe). Alternativa: aceptar un token de Google reciente como comprobación; toca cuatro endpoints y lo dejo fuera.
5. **Dos clics (el botón de Google aparece al pulsar «Continuar con Google»)** para que Google no vea la visita a la página. Alternativa: cargar el script al abrir `/ingresar` y mostrar el botón de una vez; es más cómodo, pero Google se entera de cada visita, y el plan general pedía lo contrario.
6. **Teléfono obligatorio al completar el registro**, porque la base lo exige único y no nulo (RNFP33 y la búsqueda presencial lo usan). Google no lo entrega. Si prefieres que sea opcional, hay que cambiar la base y varias pantallas: no lo hago sin tu decisión.
7. **El personal no entra con Google** (RFU05 lo limita al cliente), y el ingreso con rostro y con contraseña no cambian.
8. **Corrección de una referencia:** el plan general cita RFS11 en la fase 14, pero RFS11 es el asistente de IA (ya desviado). El requisito de Google es RFS03 (en la numeración antigua, RFS05); lo corrijo en `plan-de-construccion.md` y en la trazabilidad.
9. **La migración solo en la demo** hasta el final, igual que las anteriores. Los orígenes de producción de Google (y el dominio) se registran en la fase 16, cuando exista hosting.
10. **Dividir en 14A y 14B**, con parada en cada una (sección 0). Si prefieres una sola entrega, se junta, a costa de un informe más largo.
