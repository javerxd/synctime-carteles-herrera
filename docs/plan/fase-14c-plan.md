# Plan de la fase 14C — Ingreso con Google para el personal

Fecha: 21-11-2026 · Rama: `fase-14c-google-personal` (nace de `fase-14b-google-frontend`, una vez commiteada) · Estado: **aprobado por el usuario el 21-11-2026** (tal como se propuso, con sus cinco decisiones y supuestos); construcción sin iniciar

Origen: tu pedido del 21-11-2026 («quiero que los empleados también tengan implementado el login con google») y tus tres decisiones: **(1)** el empleado vincula su Google **desde Mi perfil, con su contraseña actual**; **(2)** pueden usarlo **los cuatro roles** (dueño, administrador, diseñador y operario); **(3)** se hace en una **fase 14C aparte**, y la 14B se cierra como está.

**Esto cambia una decisión del plan de la 14** (la 7: «el personal no entra por Google») y el requisito RFU05, que lo limitaba al cliente. Queda registrado como cambio pedido por ti en `desviaciones.md` y en la trazabilidad (RFS03, RFU05, E02).

## 1. Cómo funciona

Es **el mismo ingreso con Google de los clientes** (mismo botón, mismo nonce, mismo token de identidad, misma verificación en la API). Lo único nuevo es a qué cuenta lleva la identidad de Google:

| Situación | Resultado |
|---|---|
| La identidad (`sub`) está vinculada a un **empleado activo** | Abre la **sesión de personal**, la misma de la contraseña (token de 15 minutos y cookie de renovación): `{ token, tipo: "empleado", usuario }`. La pantalla lo lleva al panel. |
| La identidad está vinculada a un cliente | Sesión de cliente, como hoy. |
| No está vinculada y su correo es **el de un empleado** | `409 cuenta_personal_existente`: «Este correo es de una cuenta del personal. Ingresa con tu contraseña y vincula Google desde Mi perfil.» **Nunca se fusiona sola y nunca se ofrece crear una cuenta de cliente con ese correo.** |
| No está vinculada y el correo no es de nadie | Registro pendiente de cliente, como hoy (el personal **no** se crea por Google: a los empleados los da de alta gestión). |
| Empleado desactivado | La misma respuesta única que un token malo (`401 google_token_invalido`): no revela que la cuenta existía. |

- **Vincular** (`Mi perfil → Ingreso con Google`): el empleado, ya con sesión, escribe su **contraseña actual**, pulsa «Vincular Google» y elige su cuenta. La API exige que el **correo verificado de Google sea el de su ficha de empleado** (como en los clientes: nadie se «cuelga» de una cuenta ajena). **Desvincular** también pide la contraseña. Como todo empleado tiene contraseña, **siempre se puede desvincular** (no existe el caso «sin contraseña» de los clientes).
- **Un Google, una cuenta:** una misma identidad de Google no puede estar a la vez en un cliente y en un empleado, ni en dos empleados (índice único de la base, como hoy).
- **No cambia** el ingreso con contraseña ni el ingreso con rostro (solo dueño y administrador): son independientes de Google.

## 2. Qué cambia en el backend (14C-A)

- **Migración `GoogleParaPersonal`** (solo en la demo y en las bases de pruebas, como las anteriores): `identidades_externas` suma `id_empleado` (FK a `empleados`, borrado en cascada), `id_cliente` pasa a admitir nulo y un CHECK exige **exactamente uno de los dos**; índices únicos por empleado y por (`proveedor`, `sub`). Se actualizan el diccionario de datos y el MER.
- **`POST /auth/google`** resuelve primero la identidad (cliente o empleado) y devuelve la sesión que corresponda; la regla del tercer caso de la tabla anterior (`ReglasGoogle.Decidir`) se amplía con una prueba por cada rama.
- **Vincular y desvincular del personal:** `POST` y `DELETE /api/v1/mi-perfil/google` (política de personal, límite de las acciones delicadas, como las rutas de Mi cuenta) y `GET` del estado (`{ vinculado, correo }`). En el personal, un token inválido responde `400 google_token_invalido` (un `401` allí significaría «tu sesión venció»).
- **Seguridad:** se mantienen todas las condiciones del token (firma, emisor, audiencia, vencimiento, `email_verified`, nonce de un solo uso). **Cambiar el correo de un empleado desvincula su Google** (la identidad se ligó a un correo que ya no es el suyo) y se avisa; desactivarlo o cambiarle el rol ya cierra sus sesiones. El ingreso, la vinculación y la desvinculación quedan en la bitácora (`ingreso_google`, `vincular_google`, `desvincular_google`, con el empleado como actor, sin el token ni el `sub`).
- **Riesgo que dejo a la vista:** con Google, la seguridad del acceso al panel pasa a depender también de **la cuenta de Google del empleado**. Conviene que cada empleado tenga activada la verificación en dos pasos de su cuenta de Google; la API no puede exigirlo. Si algún día quieres más control, se puede sumar una lista de correos permitidos o limitar Google a ciertos roles (hoy: los cuatro).
- `openapi.json`, `contrato.md` y `permisos.md` se actualizan y el contrato se verifica contra la API como siempre.

## 3. Qué cambia en el frontend (14C-B)

- **`/personal/ingresar`:** «Continuar con Google» **arriba del correo**, con el separador «o con tu correo» (el mismo patrón y el mismo componente de los clientes; sin ID de cliente no se muestra). El acceso con rostro y la contraseña siguen igual. La sesión de personal lleva al panel; el aviso `cuenta_personal_existente` se explica con texto.
- **Mi perfil → «Ingreso con Google»** (los cuatro roles): vincular y desvincular con la contraseña actual, con la misma lógica de foco y avisos de Mi cuenta (se reutiliza lo construido en la 14B, sin duplicarlo).
- **Clientes en la misma pantalla de ingreso:** si alguien entra por `/ingresar/cliente` con un Google vinculado a un empleado, se abre la sesión de personal y va al panel (la API decide; la pantalla solo sigue la respuesta).
- Todo con los tokens y componentes del sistema, modo claro, foco visible y anuncios en regiones de estado, a 360, 768 y 1440 px.

## 4. Lo que tienes que hacer tú

- **En Google Cloud no cambia nada:** el mismo ID de cliente, los mismos orígenes. Mientras la app esté en «Prueba», **agrega como usuarios de prueba los correos de Google de los empleados** que vayan a probarlo.
- La **prueba real** con una cuenta de empleado tuya: vincular desde Mi perfil, salir, entrar con Google, desvincular, y el caso del correo del personal sin vincular. Escribe en la base demo (bitácora e identidad): te aviso antes.

## 5. Pruebas primero y revisiones

- **Unitarias:** `ReglasGoogle.Decidir` con personal (cada rama), la regla «correo de empleado no vinculado → 409» y que nunca se ofrezca registro de cliente con ese correo.
- **Integración (SQL Server), con los cuatro roles, un cliente y un visitante:** vincular y desvincular con la contraseña correcta e incorrecta; correo de Google distinto al de la ficha (`409 google_correo_distinto`); ingreso por `sub` con sesión de personal idéntica a la de la contraseña (mismo rol y mismas cookies); empleado desactivado (respuesta única); `sub` ya usado por un cliente o por otro empleado; cambiar el correo del empleado desvincula; el cliente y el visitante no pueden usar `/mi-perfil/google` (`403` y `401`); límite de tasa; ni token, ni nonce ni `sub` en registros; la bitácora; el ingreso con contraseña y con rostro no cambian; la migración (restricciones y borrado en cascada).
- **Fallos a propósito:** dejar entrar a un empleado desactivado, no pedir la contraseña al vincular y aceptar un correo de Google distinto al de la ficha; las pruebas deben fallar.
- **Vitest:** el botón y el separador en el acceso del personal; la sesión de personal por Google va al panel; `cuenta_personal_existente`; Mi perfil con vincular, desvincular y sus errores, para los cuatro roles; nada de token, nonce ni pase en el almacenamiento ni en la URL.
- **Playwright con axe a 360, 768 y 1440 px**, con el script de Google simulado: acceso del personal, Mi perfil, sin violaciones de la política de contenido ni errores de consola.
- **ECC:** `csharp-reviewer` y `security-reviewer` (la parte del backend: suplantación y toma de un cargo, fusión, desactivados, correos) y `react-reviewer` y `a11y-architect` (la del frontend). Parada con el informe `informes/fase-14c.md`, el tablero, la trazabilidad (RFS03, RFU05, E02, RNFE02, RNFP17), `contrato.md`, `permisos.md`, `openapi.json`, el diccionario de datos y el MER.

## Decisiones y supuestos a tu vista

1. **Un correo de empleado sin vincular no puede crear una cuenta de cliente con Google** (responde `409` y manda a Mi perfil). Hoy, para la API, ese correo se trataba como uno cualquiera; lo cierro para que el ingreso con Google no tenga un camino confuso en el personal. Quien sea empleado y además quiera ser cliente usa otro correo o el formulario.
2. **Cambiar el correo de un empleado desvincula su Google.** Alternativa: conservar el vínculo (la identidad es el `sub`); lo evito porque el vínculo se probó con un correo que ya no es el suyo.
3. **Sin lista de correos permitidos ni exigencia de dos pasos:** la API no puede imponer cómo cuida cada empleado su cuenta de Google (ver el riesgo en la sección 2).
4. **El personal no se crea por Google:** a los empleados los da de alta gestión, como hoy.
5. **Nombre de la ruta:** `/api/v1/mi-perfil/google` (el equivalente de `/mi-cuenta/google` de los clientes).
