# Informe de cierre — Fase 14A: backend del ingreso con Google

Fecha: 21-11-2026 · Rama: `fase-14a-google-backend` (nace de `fase-13b-reportes-frontend`, commit `b61eecf`) · Estado: **aprobada el 21-11-2026**; commit y push los hace el usuario (los comandos van al final)

Plan: `fase-14-plan.md` (las diez decisiones, tal como las aprobaste el 21-11-2026). Migración: **`IngresoConGoogle`** (tabla `identidades_externas`), aplicada **solo en la base demo** y en las de pruebas; la real la recibe al final con todas. Dependencia nueva: **`Google.Apis.Auth` 1.78.0** (Apache-2.0).

## Qué se hizo

- **Ingreso con Google, solo para clientes** (el personal no entra por Google, RFU05). Con el *token de identidad* de Google Identity Services: **sin redirección y sin clave secreta de cliente**; la API solo verifica la firma y las condiciones del token.
  - `GET /api/v1/auth/google/nonce`: nonce aleatorio, de un solo uso, vence a los 5 minutos.
  - `POST /api/v1/auth/google`: tres caminos. Si el `sub` ya está vinculado a una cuenta activa, abre la sesión (misma cookie y mismo token que el ingreso con contraseña). Si el correo ya tiene una cuenta, responde `409 cuenta_existente` y **nunca la fusiona sola**. Si el correo es nuevo, responde `200` con `estado: registro_pendiente` y un *pase* firmado (10 minutos, de un solo uso); **no crea nada todavía**.
  - `POST /api/v1/auth/google/completar`: con el pase y lo que Google no entrega (teléfono, apellido si falta, RUT y dirección opcionales, consentimientos sin marcar) crea **en una sola transacción** la cuenta (correo ya verificado, sin contraseña), su identidad y los consentimientos, y abre la sesión (`201`). **Reutiliza `ServicioMiCuenta.RegistrarAsync`** (con un parámetro opcional) y `AuthApi.SesionClienteAsync`: mismas validaciones, mismos mensajes (teléfono o RUT conocidos dan el mismo texto de siempre) y misma comprobación de las versiones de los textos legales.
  - `POST` y `DELETE /api/v1/mi-cuenta/google`: vincular y desvincular. Piden la contraseña actual; vincular exige que el correo verificado de Google sea el de la cuenta; desvincular no se puede si la cuenta no tiene contraseña (`409 sin_contrasena`). La ruta quedó bajo `/mi-cuenta`, no `/mi/cuenta` como decía el plan (errata; ver `desviaciones.md`).
- **Qué verifica la API en el token** (RNFE02, RNFP17): firma de Google (con `Google.Apis.Auth`), emisor, **audiencia = nuestro ID de cliente**, vencimiento, **nonce igual al emitido y sin usar**, `email_verified` verdadero y `sub` y correo presentes. Las condiciones del negocio las vuelve a comprobar nuestro código (`ReglasGoogle`) aunque la librería ya las revise. **La identidad es el `sub`, no el correo.**
- **Si Google falla o tarda:** `503 google_no_disponible` en menos de 5 segundos, sin bloquear a los demás. Sin `Google:ClientId`: `503 google_no_configurado` (el frontend no debe mostrar el botón).
- **Datos:** tabla `identidades_externas` (índices únicos: un mismo Google no puede estar en dos cuentas ni una cuenta tener dos Google; CHECK del proveedor; se borra con su cliente). Nunca se guarda el token.
- **Auditoría:** acciones nuevas `ingreso_google`, `vincular_google` y `desvincular_google` (entidad `cuenta`, con quién y cuándo, sin token ni `sub`), y la tabla nueva se audita con el interceptor como las demás.
- **Configuración y secretos:** `Google:ClientId` por User Secrets o la variable `Google__ClientId`; `appsettings.json` lleva el campo vacío. **No hay ningún ID de cliente ni credencial en el repositorio** (las pruebas usan un ID inventado).
- **Para que una cuenta sin contraseña funcione** (decisión 4 del plan) se ajustó lo que ya existía: la renovación de la sesión, la recuperación de contraseña (así quien nace con Google **crea su contraseña con el código al correo**), la ficha del cliente en el panel del personal y la baja de la cuenta (que libera el Google). `GET /mi-cuenta` suma `googleVinculado` y `tieneContrasena`, que la 14B necesita. Todo en `desviaciones.md`.
- **Documentos:** `openapi.json` (5 operaciones), `contrato.md` (sección «Ingreso con Google (fase 14A)»), `permisos.md`, diccionario de datos, MER, `licencias.md`, `desviaciones.md`, `trazabilidad.md` y `tablero.md`.

## Dependencia nueva

| Paquete | Versión | Licencia | Verificado el 21-11-2026 |
|---|---|---|---|
| Google.Apis.Auth (y Google.Apis, Google.Apis.Core) | 1.78.0 | Apache-2.0 | Licencia en el `.nuspec`; última versión estable; `dotnet list package --vulnerable --include-transitive`: sin paquetes vulnerables. Trae Newtonsoft.Json 13.0.4 (MIT) |

## Pruebas

- **Unitarias: 742 de 742** (67 nuevas en `IngresoGoogleTests`: cada condición del token —emisor, audiencia exacta, vencimiento, correo sin verificar, sin `sub` o con `sub` raro, correo inválido o demasiado largo, nonce distinto o ausente—, el nonce de un solo uso con su vencimiento y su tope, el pase firmado —vencido, manipulado, de otra clave, reutilizado—, los tres caminos y la regla de no fusionar, que sin contraseña no se desvincula, y el límite de espera a Google).
- **Integración (SQL Server): 645 de 645** (64 nuevas en `GoogleFase14aTests` y 1 de migración). Cubren, con roles: cuenta nueva completa (cuenta, identidad y consentimientos en una transacción; **si falla un consentimiento no queda nada**); ingreso repetido por `sub` aunque el correo de la cuenta haya cambiado; correo existente con contraseña (`409` y nada se modifica, también con mayúsculas y espacios); **baja real** y cuenta desactivada (la misma respuesta que un token malo); cliente del local sin acceso web; teléfono o RUT conocidos (el mismo mensaje de siempre); versión de los textos desactualizada y datos faltantes (el pase sigue sirviendo para corregir); pase manipulado, vencido o reutilizado, y dos «completar» simultáneos (una sola cuenta); vincular y desvincular con y sin contraseña, con otro correo, con un Google ya usado y con cuenta que ya tiene uno; **el personal y el visitante no pueden usar los endpoints de Mi cuenta (`403` y `401`)** y el personal no entra por Google; **Google caído, lento o que ignora la cancelación (`503` en menos de 5 s, sin bloquear otras peticiones)**; la API sin `Google:ClientId`; el **verificador real** contra tokens mal formados; límite de tasa propio; crear contraseña con el código de recuperación; la baja libera el Google; los índices únicos de la base; la bitácora (`ingreso_google`, `vincular_google`, `desvincular_google`); y que **ni el token, ni el nonce, ni el pase aparecen en los registros ni en las respuestas de error**.
- **Contrato:** `openapi.json` coincide con la API (`ContratoTests`, incluido el acceso declarado contra el real).
- **Fallos a propósito (detectados, código restaurado):** (1) quitar la comprobación de `email_verified` → falla 1 prueba unitaria y 3 de integración; (2) quitar la comprobación de la audiencia → fallan 3 unitarias y 1 de integración; (3) permitir la fusión automática de cuentas → falla 1 unitaria y 2 de integración.
- **Sin paquetes vulnerables** (`dotnet list package --vulnerable --include-transitive`). Búsqueda de credenciales en todo el árbol (`googleusercontent.com`, `GOCSPX`, `client_secret`, claves `AIza…`): ninguna; las pruebas usan un ID inventado.

## Revisiones de ECC y qué se hizo con ellas

- **Seguridad:** 0 críticas, 0 altas. Verificado sin hallazgo: condiciones del token (comprobadas dos veces), nonce de un solo uso, no fusionar, vincular, pase firmado, baja, ausencia de secretos en el árbol y en el historial de git. **Corregido:** el flujo del personal no reconocía las cuentas nacidas con Google (ahora contraseña **o** Google en la edición, el cambio de correo y la reasignación de acceso, que avisa al titular); un solo contador compartido con el ingreso con contraseña y el nonce en memoria agotable (ahora política `IngresoGoogle` aparte, IPv6 por /64 y tope de 200.000); `sub` sin formato (ahora ASCII alfanumérico y columna binaria); «ya existe» disfrazaba cualquier error de la base (ahora solo violaciones de índice único); cuentas de baja o sin acceso web recibían un `409` sin salida y se distinguían de las demás (ahora `401` genérico o `sin_acceso_web` con la salida); la espera a Google no cortaba la descarga (ahora el cliente HTTP tiene el mismo tope).
- **C#:** 0 críticas, 2 altas, **corregidas** (los conflictos de la base y el callejón de las cuentas de baja, ambos con prueba, incluida la baja real que antes se simulaba). También corregidos: la bitácora tras el alta no se corta si el navegador cierra; «completar» simultáneo comprobaba solo que una gane; correo de más de 100 caracteres que atascaba el registro; la descarga de datos no incluía el vínculo con Google; el `201` se decidía mirando la ruta; carrera al desvincular; valores por defecto que ocultaban un olvido; resultado de registro no previsto que se disfrazaba de `404`.
- **Dejado a propósito (en `desviaciones.md`, para tu decisión):** aviso por correo cuando una cuenta de Google crea su contraseña, pase no ligado al navegador (la 14B lo mantiene solo en memoria y en el cuerpo), bitácora de seguridad más rica en los rechazos, largo mínimo de `Jwt:Key` y `ForwardedHeaders` (fase 16), y que quien nace con Google crea una contraseña antes de poder descargar sus datos o darse de baja.
- **Por probar contigo (no se puede automatizar):** que Google acepte nuestro ID de cliente y que una caída real de Google dé `503` y no `401` con la librería. Se prueba en la 14B con tu cuenta.

## Lo que queda de tu lado (sección 7 del plan)

La 14A no necesita nada tuyo para quedar aprobada: se probó completa con un verificador simulado. **Lo que solo tú puedes hacer** se necesita cuando llegue la pantalla (14B), porque sin el botón no hay forma de pedirle un token a Google:

1. **En Google Cloud → Credenciales → tu ID de cliente OAuth:** confirmar que es de tipo «Aplicación web» y agregar como *Orígenes de JavaScript autorizados* `http://localhost:3000` y también `http://localhost`. No hace falta ningún URI de redirección ni registrar `https://localhost:7112`.
2. **En «Pantalla de consentimiento de OAuth»:** alcances solo `openid`, `email` y `profile`; si la app está en «Prueba», agregar como usuarios de prueba los correos de Google con los que vas a ingresar.
3. **Guardar el ID en tu equipo** (yo no lo escribo en ningún archivo): para la API, un comando de User Secrets con tu ID (va más abajo); para el frontend, `VITE_GOOGLE_CLIENT_ID` en `frontend/synctimefront/.env.local` (ya ignorado por git), que se pide en la 14B.
4. **Probar el ingreso real con una cuenta de Google tuya** (14B): cuenta nueva, ingreso repetido, un correo que ya tiene cuenta con contraseña, y vincular y desvincular.

## Comandos para tu commit

Desde la carpeta del repositorio. Se agregan solo las carpetas `backend` y `docs`, para **no arrastrar `.impeccable/` ni el archivo vacío `python`**, que estaban sin seguimiento desde antes y no son de esta fase:

```powershell
git add backend docs
git status --short
git commit -m "feat(fase-14a): backend del ingreso con Google (token de identidad, nonce, pase de registro, vincular y desvincular)"
```

Para guardar el ID de cliente en la API cuando llegue la 14B (cambia `TU-ID` por el tuyo; se guarda en tu equipo, no en el repositorio):

```powershell
dotnet user-secrets set "Google:ClientId" "TU-ID" --project backend/Proyecto-Carteles-herrera-synctime-main/LetrerosHerreraSYNCTIMEapi
```

## Pendientes y límites (no bloquean)

- La base demo recibirá la migración `IngresoConGoogle` la próxima vez que arranques la API demo (en desarrollo se aplica sola solo a la demo y a las de pruebas). Ahora no está en marcha ninguna API demo.

- La verificación real con Google la prueba solo el usuario con su cuenta; lo automatizado cubre todas las condiciones con el verificador simulado y el verificador real contra tokens mal formados.
- El nonce y el pase viven en la memoria de la API (con varias instancias habría que llevarlos a la base): anotado para la fase 16 en `desviaciones.md`.
- Quien nace con Google no puede descargar sus datos ni darse de baja hasta que crea una contraseña (esas acciones piden la contraseña actual).
- La migración está aplicada en la demo y en las bases de pruebas, **no en la real**.
