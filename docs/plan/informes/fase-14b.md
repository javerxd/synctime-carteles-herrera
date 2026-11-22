# Informe de cierre — Fase 14B: frontend del ingreso con Google

Fecha: 21-11-2026 · Rama: `fase-14b-google-frontend` (nace de `fase-14a-google-backend`, commit `220f42e`) · Estado: **construida, en revisión; falta tu prueba real con Google** · Commit y push los haces tú (los comandos van al final)

Plan: `fase-14-plan.md` (secciones 1, 5, 6, 7 y 8, parte 14B) y el contrato real de la API (`docs/api/contrato.md`, «Ingreso con Google (fase 14A)»). **Sin migración y sin dependencias nuevas** (el frontend no suma paquetes; el script de Google lo carga el navegador desde Google, solo al pulsar el botón).

## Qué se hizo

- **Botón «Continuar con Google» de dos clics, ARRIBA del correo** (cambio pedido por ti el 21-11-2026; el plan decía debajo del formulario: queda en `desviaciones.md`). En `/ingresar/cliente` va arriba del campo «Correo electrónico» y, debajo del botón, un separador sutil «— o con tu correo —». El mismo patrón está en `/cuenta/registro`, arriba del nombre. **Sin `VITE_GOOGLE_CLIENT_ID` no se muestra ni el botón ni el separador.**
  - **Primer clic:** el botón del sistema pasa a «Cargando Google…» y recién ahí se agrega el script `https://accounts.google.com/gsi/client` (**no está en el HTML**: antes de pulsar, Google no recibe ni una petición), luego se pide `GET /auth/google/nonce` y se dibuja el **botón oficial de Google** (conserva su aspecto) con un texto que lo explica. **Segundo clic:** el botón de Google.
  - **Los tres caminos** de `POST /auth/google`: sesión abierta como el ingreso con contraseña; `409 cuenta_existente` con el aviso «Ya tienes una cuenta con este correo. Ingresa con tu contraseña y vincula Google desde Mi cuenta.»; y `registro_pendiente`, que lleva a «Completa tu registro».
  - **El nonce se gasta siempre:** tras cualquier fallo (token malo, cuenta existente, Google caído, límite, contraseña equivocada al vincular) se pide un nonce nuevo y se vuelve a inicializar Google con él antes de mostrar el botón otra vez.
  - **Errores con texto y sin bloquear la contraseña:** script bloqueado o sin red (aviso claro; el formulario sigue igual y se puede reintentar), `401 google_token_invalido`, `503 google_no_disponible` y `google_no_configurado`, `409 sin_acceso_web` (el mensaje del local), `429`.
- **`/cuenta/google/completar` — «Completa tu registro».** Muestra el correo y el nombre de Google; campos Nombre y Apellido (con lo que Google entregó), **Teléfono obligatorio**, RUT y dirección opcionales, y las dos casillas de consentimiento **sin marcar**, con los textos y las versiones de siempre (el bloque se extrajo a `Consentimientos.jsx` y lo comparten este formulario y el registro). Al enviar con errores, el foco va al primero; los errores por campo de la API van junto a su campo y el pase sigue sirviendo. Con éxito: sesión abierta y Mi cuenta con la bienvenida. Pase vencido, ya usado o perdido por recargar: vuelve a `/ingresar/cliente` con un aviso.
- **El pase, el token y el nonce solo viven en memoria y en el cuerpo de la petición.** El pase queda en un módulo (`src/google/pase.js`), no en la URL, ni en `localStorage`/`sessionStorage`, ni en el estado de navegación (que el navegador guarda). Lo comprueban las pruebas.
- **Mi cuenta → «Ingreso con Google».** Con `GET /mi-cuenta` (`googleVinculado`, `tieneContrasena`): **vincular** (contraseña actual + botón de Google → `POST /mi-cuenta/google`; sin contraseña no se carga Google) y **desvincular** (`DELETE /mi-cuenta/google`), con el error de la contraseña junto al campo y el foco en él. Una cuenta **sin contraseña** lo explica, no ofrece desvincular, oculta los formularios de correo y contraseña (que piden la actual) y ofrece **«Crear una contraseña»** (la recuperación de siempre, con tu correo ya escrito). Errores `400 google_token_invalido` y `409 google_correo_distinto | google_en_uso | google_ya_vinculada | sin_contrasena` con su texto.
- **Política de contenido** (`index.html`, vale también en desarrollo): se agregan **solo** `script-src https://accounts.google.com/gsi/client`, `frame-src https://accounts.google.com/gsi/`, `connect-src https://accounts.google.com/gsi/` y `style-src https://accounts.google.com/gsi/style`. El build de producción conserva la ruta de Google en `connect-src`. Anotado para la fase 16: el servidor **no debe** enviar `Cross-Origin-Opener-Policy: same-origin`.
- **Diseño y accesibilidad:** todo lo que rodea al botón oficial usa los tokens y componentes del sistema (modo claro); foco visible; errores con texto (rol de alerta) y avisos en regiones de estado; el foco pasa a la zona del botón de Google cuando el botón del sistema desaparece; sin desplazamiento horizontal a 360, 768 y 1440 px.
- **Secretos (RNFP17):** el ID de cliente solo sale de `VITE_GOOGLE_CLIENT_ID` (`.env.local`, ignorado por git); `.env.example` lleva el campo vacío; las pruebas usan un ID inventado. **No hay ninguna credencial en el repositorio** (búsqueda en todo el árbol: sin `googleusercontent.com` real, `GOCSPX`, `client_secret` ni claves `AIza…`).

## Pruebas

- **Vitest (frontend): 890 de 890** (48 archivos; 55 nuevas). Nuevas: `fase14Google.test.jsx` (el botón aparece solo con el ID; va arriba del correo con su separador; el script **no** se carga al abrir la página sino al pulsar; los tres caminos; seis errores de ingreso; script bloqueado y reintento; «Completa tu registro» con validación, foco al primer error y casillas sin marcar; pase vencido o perdido; Mi cuenta con y sin contraseña, vincular, desvincular y cinco errores; que ni token, nonce ni pase queden en `localStorage`, `sessionStorage`, la URL ni `history.state`) y `fase14Csp.test.js` (la política abre solo lo de Google y el build la conserva).
- **Playwright con axe a 360, 768 y 1440 px, con el script de Google simulado** (`e2e/google.spec.js`, 24 pruebas: se intercepta `accounts.google.com/gsi/client`): ingreso, cuenta existente, script bloqueado, registro nuevo de punta a punta, pase perdido, registro con Google y Mi cuenta (vincular, desvincular y sin contraseña). Cada una comprueba **cero violaciones de la política de contenido** (detector `securitypolicyviolation`), **cero errores en la consola**, sin desborde horizontal y sin fallas graves de axe.
- **Fallos a propósito (detectados, código restaurado):** (1) cargar el script de Google al abrir la página → fallan 4 pruebas; (2) guardar el pase en `localStorage` → fallan 3; (3) no pedir un nonce nuevo tras un fallo → fallan 9; (4) quitar el script de Google de `script-src` → fallan 2 de Vitest y la de Playwright (el botón nunca aparece).
- **Playwright completo: 287 de 287** (24 nuevas de Google; el resto, las pantallas de siempre, que ahora incluyen el botón en el ingreso y el registro).
- **Backend sin cambios, en verde:** 742 de 742 unitarias y 645 de 645 de integración.
- **Lint (oxlint):** sin avisos en los archivos de esta fase. **Build de producción:** compila y deja la ruta de Google en la política de contenido.

## Revisiones de ECC y qué se hizo con ellas

- **`react-reviewer`:** 0 críticas ni de seguridad; 6 defectos menores, **todos corregidos** (la marca de foco que quedaba obsoleta; el callback repetido o tardío de Google, ahora atado al nonce de cada dibujo; «Vincular» con la contraseña vaciada después de aparecer el botón de Google; un `<script>` duplicado si Google llega tras el tiempo agotado; Google que no se puede inicializar; el pase que ahora se olvida al cerrar la sesión) y **6 pruebas nuevas** (StrictMode, callback repetido, inicialización fallida, script tardío, contraseña vaciada, cierre de sesión). Verificado sin hallazgo: hooks y cierres del callback de Google, StrictMode, secretos (nada de token, nonce ni pase en `localStorage`, `sessionStorage`, `history.state` ni consola), contrato con la API, CSP.
- **`a11y-architect`:** 0 bloqueantes; 8 importantes y 8 menores. **Corregidos:** regiones de estado que ya no desaparecen del árbol de accesibilidad, «Cargando Google…» anunciado, zona del botón de Google con nombre, foco que va a los avisos tras un fallo y durante la verificación (y de vuelta al botón si no se pudo renovar el nonce), foco al mensaje de éxito y de error en Mi cuenta, mensaje de éxito que ya no queda viejo, aviso de pase vencido como alerta, aviso de los 10 minutos del pase, rayas del separador visibles en alto contraste y `aria-required` en la casilla de términos. Las pruebas de Playwright ahora comprueban dónde queda el foco.
- **Dejado a propósito (menores, en `desviaciones.md`):** el ancho del botón de Google no se vuelve a medir con zoom hecho después, «Vincular» no es un `<form>` (Enter), vuelve siempre a `/cuenta` tras completar, el aviso de consola de Google por re-inicializar, que oxlint no tiene la regla de dependencias de efectos, y que lo que dibuja Google (el `title` del iframe, el Tab dentro de él, el foco al volver de su ventana) solo se comprueba a mano.

## Un error mío, ya resuelto

Mientras escribía este informe, un comando mío de shell interpretó los bloques de código del texto como órdenes y creó por error un commit local (`f99c2f0`). **Nada se subió.** El 21-11-2026 me pediste deshacerlo y lo deshice con `git reset --mixed HEAD~1`: todos los archivos siguen en su sitio, sin preparar, y el último commit de la rama vuelve a ser el de la 14A (`220f42e`).

## Lo que queda de tu lado: me detengo aquí antes de la verificación real

La verificación en la app real **escribe en la base demo** (una cuenta nueva, su identidad, sus consentimientos y filas en la bitácora) y necesita tu cuenta de Google. No la hago sin tu aviso. Pasos exactos (sección 7 del plan):

1. **Google Cloud → APIs y servicios → Credenciales → tu ID de cliente OAuth:** tipo **«Aplicación web»**; en **Orígenes de JavaScript autorizados** agrega `http://localhost:3000` y también `http://localhost`. **No** agregues ningún URI de redirección ni `https://localhost:7112`.
2. **Pantalla de consentimiento de OAuth:** alcances solo `openid`, `email` y `profile`. Si está en «Prueba», agrega como **usuarios de prueba** los correos con los que vas a ingresar (sin eso Google responde «acceso denegado»).
3. **Confirma que el ID está en los dos lados** (ya me dijiste que sí): User Secrets de la API (`Google:ClientId`) y `frontend/synctimefront/.env.local` (`VITE_GOOGLE_CLIENT_ID`). Si cambiaste algo, reinicia la API y `npm run dev`.
4. **Avísame** y levanto la API demo (puerto 7112; aplica la migración `IngresoConGoogle` a la demo si falta) y el frontend (`http://localhost:3000`). **La prueba con tu cuenta la haces tú en tu navegador** (no se puede automatizar), con este guion:
   - **Cuenta nueva:** con un correo de Google que no tenga cuenta en la demo → «Continuar con Google» → «Completa tu registro» (teléfono, términos) → quedas dentro, en Mi cuenta.
   - **Ingreso repetido:** sales y vuelves a entrar con Google (un clic, sin completar nada).
   - **Correo que ya tiene cuenta con contraseña:** con un correo de Google que ya exista en la demo con contraseña → debe salir el aviso «Ya tienes una cuenta con este correo…» y no entrar.
   - **Vincular y desvincular:** esa cuenta con contraseña entra con su contraseña → Mi cuenta → «Ingreso con Google» → vincular (con la contraseña) → salir y entrar con Google → desvincular.
   - **Cuenta nacida con Google:** en Mi cuenta debe explicar que no tiene contraseña y ofrecer «Crear una contraseña».
5. **Caída de Google (pendiente de la 14A):** conmigo, con la API sin salida a Google, comprobamos que la respuesta es `503 google_no_disponible` y no `401`.

## Comandos para tu commit

Desde la carpeta del repositorio. Se agregan solo `frontend` y `docs`, para **no arrastrar `.impeccable/` ni el archivo `python`** (sin seguimiento desde antes).

```powershell
git add frontend docs
git status --short
git commit -m "feat(fase-14b): frontend del ingreso con Google (boton de dos clics, completa tu registro, vincular y desvincular, politica de contenido)"
```
