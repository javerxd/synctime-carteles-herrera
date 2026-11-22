# Informe de cierre — Fase 14C: ingreso con Google para el personal

Fecha: 21-11-2026 · Rama: `fase-14c-google-personal` (nace de `fase-14b-google-frontend`, commit `fd6e882`) · Estado: **construida, en revisión; faltan dos decisiones tuyas y la prueba real con una cuenta de empleado** · Commit y push los haces tú (los comandos van al final)

Plan: `fase-14c-plan.md` (aprobado por ti el 21-11-2026). Cambio pedido por ti: **los empleados también entran con Google** (decisión 7 del plan de la 14 y requisito RFU05, que lo limitaban al cliente). Tus tres decisiones: se vincula **desde Mi perfil con la contraseña actual**, pueden usarlo **los cuatro roles** y es una **fase aparte**. **Migración nueva: `GoogleParaPersonal`** (solo en la base demo y en las de pruebas; la real la recibe al final). **Sin dependencias nuevas.**

## Qué se hizo

- **Mismo ingreso, otra cuenta.** El empleado, ya con sesión, abre **Mi perfil → «Ingreso con Google»**, escribe su contraseña actual, pulsa «Vincular Google» y elige su cuenta; la API exige que el **correo verificado de Google sea el de su ficha**. Desde entonces «Continuar con Google» en `/personal/ingresar` (arriba del correo, con el separador «o con tu correo») le abre **la misma sesión de personal que la contraseña** (mismo token, cookie de renovación, rol y caducidad). El ingreso con contraseña y con rostro no cambian. Los empleados **no se crean por Google**: los da de alta gestión.
- **Un correo de empleado sin vincular** responde `409 cuenta_personal_existente` («Este correo es de una cuenta del personal. Ingresa con tu contraseña y vincula Google desde Mi perfil.»): nunca se fusiona sola y **nunca se ofrece crear una cuenta de cliente** con ese correo. Un empleado desactivado recibe la misma respuesta única de un token malo (`401`), sin revelar que existía.
- **Datos (migración `GoogleParaPersonal`).** `identidades_externas` suma `id_empleado` (FK con borrado en cascada); `id_cliente` admite nulo; un CHECK exige **exactamente uno de los dos**; los índices por cuenta pasan a ser **filtrados** (uno por cliente y uno por empleado) y `(proveedor, sub)` sigue siendo único: un mismo Google no puede estar en dos cuentas, sea de cliente o de empleado. Diccionario de datos y MER actualizados.
- **API.** `GET`, `POST` y `DELETE /api/v1/mi-perfil/google` (los cuatro roles; clientes y visitantes reciben `403` y `401`), con el límite de las acciones con contraseña y un límite por cuenta. `POST /auth/google` resuelve la identidad (cliente o empleado) y devuelve la sesión que corresponde. Cambiar el correo del empleado **desvincula** su Google. `contrato.md`, `openapi.json` y `permisos.md` actualizados.
- **Pantallas.** El botón en el acceso del personal; «Ingreso con Google» en Mi perfil (la misma sección de Mi cuenta, reutilizada, sin duplicar código); avisos que dicen **dónde** ingresar si la persona está en el acceso equivocado; un empleado que entra por `/ingresar/cliente` o `/cuenta/registro` va al panel; en el acceso del personal, un Google que no es de nadie del equipo **no crea una cuenta de cliente** (avisa). Todo con los tokens y componentes del sistema, modo claro, foco visible y anuncios en regiones de estado.
- **Secretos.** Ningún ID, clave ni token en el repositorio; ni el token, ni el nonce, ni el `sub` aparecen en los registros ni en las respuestas (hay prueba).

## Pruebas

- **Backend: 749 unitarias y 686 de integración, todas en verde** (nuevas: 7 unitarias en `IngresoGooglePersonalTests` y 41 de integración en `GoogleFase14cTests`, más una de migración). Cubren, con los cuatro roles, un cliente y un visitante: vincular y desvincular con la contraseña correcta e incorrecta (la contraseña se revisa antes de gastar el nonce), correo de Google distinto al de la ficha, normalización del correo, un Google ya usado por otro empleado o por un cliente, tokens que no cumplen las condiciones (`400` en Mi perfil), Google caído (`503`), ingreso por `sub` con la sesión idéntica a la de la contraseña y su renovación, desactivado, cambio de correo, bitácora con el empleado como actor y sin el `sub`, registros sin secretos, límite por cuenta, y las restricciones y la cascada de la base.
- **Frontend: Vitest 918 de 918** (28 nuevas) y **Playwright 302 de 302** (15 nuevas, con axe a 360, 768 y 1440 px y el script de Google simulado: acceso del personal, empleado que entra al panel, aviso de correo de empleado sin vincular, Mi perfil con vincular y desvincular, y con Google ya vinculado; cada una sin violaciones de la política de contenido ni errores de consola). El recorrido general de pantallas ahora incluye la respuesta simulada de Mi perfil.
- **Fallos a propósito (detectados, código restaurado):** *backend* — (1) dejar entrar a un empleado desactivado, (2) no pedir la contraseña al vincular, (3) aceptar un correo de Google distinto al de la ficha, (4) tratar el correo de un empleado sin vincular como persona nueva: fallan 1, 3, 1 y 2 pruebas; *frontend* — (1) llevar la sesión de personal a Mi cuenta, (2) ofrecer crear una cuenta de cliente en el acceso del personal, (3) vincular contra la ruta del cliente, (4) quitar el botón del acceso del personal: fallan 3, 1, 6 y 6 pruebas.
- Las pruebas de lo que cambió por las revisiones (más abajo) se escribieron **antes** de corregir y fallaron primero (5 fallos).

## Revisiones de ECC y qué se hizo con ellas

- **Seguridad:** 0 críticas ni altas; el diseño cierra la toma de un cargo (un Google sin vincular nunca abre sesión de personal; vincular exige sesión, contraseña y correo de la ficha; la identidad es el `sub`). **Corregido:** desvincular no cerraba las sesiones abiertas (ahora **cierra todas** y abre una nueva en este equipo: `DELETE` responde `200 { token }`); desactivar a un empleado no desvinculaba su Google (ahora sí); una carrera entre vincular y cambiar el correo (candado de la fila). **Pendientes que no bloquean:** los intentos fallidos sobre el personal no dejan filas propias en la bitácora, el límite por cuenta no se comparte con las otras confirmaciones de contraseña (patrón anterior) y el correo de aviso no menciona la desvinculación.
- **C#:** 0 críticas ni altas. **Corregido:** un ingreso que coincide con el borrado de la identidad daba un 500 (ahora la respuesta única de token inválido), comentarios desactualizados, el `GET` de Mi perfil gastaba el cupo por IP de las acciones con contraseña, el correo del token debe seguir siendo el de la ficha al ingresar, y el parámetro `correoEsDePersonal` es obligatorio. Limpieza que queda para después (duplicación entre las rutas de cliente y de personal).
- **React:** sin defectos de seguridad, de hooks ni de navegación. **Corregido:** los avisos de «ya tienes una cuenta» ahora dicen dónde ingresar si se está en el acceso equivocado, el botón de Google se oculta mientras la cámara está abierta, la consulta de Mi perfil se cancela al salir y los mocks de las pruebas ya no heredan implementaciones.
- **Accesibilidad:** 0 bloqueantes. **Corregido:** «Reintentar» no pierde el foco y la sección siempre tiene su encabezado, el correo largo ya no desborda, las ayudas dicen «Para vincular / desvincular Google, confirma que eres tú», el aviso del botón dice qué se vincula, un intento nuevo con Google limpia el error viejo de la contraseña, el campo de correo ya no roba el foco al cargar cuando hay Google, y «Desvincular» ya no deja un error viejo junto al nuevo.
- Todo lo menor que quedó está en `desviaciones.md`.

## Dos decisiones (confirmadas por ti el 21-11-2026)

1. **Desactivar a un empleado desvincula su Google** (el plan decía que al reactivarlo el vínculo seguía). Lo cambié por seguridad: un Google que quizá ya no controla el taller no debe volver a abrir el panel solo; al reactivarlo, vincula de nuevo con su contraseña. Si prefieres lo contrario, es un cambio de una línea y una prueba.
2. **Desvincular cierra todas las sesiones del empleado** (y le abre una nueva en este equipo). Si su Google estuviera comprometido, la sesión del atacante (de hasta 12 horas) no sigue viva.

## Lo que queda de tu lado

La 14C **no cambió nada en Google Cloud**: el mismo ID de cliente y los mismos orígenes. Mientras la app esté en «Prueba», **agrega como usuarios de prueba los correos de Google de los empleados** que vayan a probarlo.

La **prueba real** con una cuenta de empleado tuya escribe en la base demo (la **migración `GoogleParaPersonal` se aplica sola al arrancar la API demo**, más la identidad y la bitácora), así que te aviso antes de levantarla. Guion: (1) entrar con contraseña → Mi perfil → «Ingreso con Google» → vincular (con la contraseña) con el Google del mismo correo de la ficha; (2) salir y entrar con «Continuar con Google» en `/personal/ingresar`; (3) en Mi perfil, desvincular y comprobar que te devuelve a una sesión válida; (4) con un correo de empleado sin vincular, el aviso a Mi perfil; (5) con un Google que no es de nadie del equipo, el aviso del acceso del personal. Además, **cada empleado debería activar la verificación en dos pasos de su cuenta de Google**: la API no puede exigirlo.

## Comandos para tu commit

Desde la carpeta del repositorio. Se agregan solo `backend`, `frontend` y `docs`, para **no arrastrar `.impeccable/` ni el archivo `python`**:

```powershell
git add backend frontend docs
git status --short
git commit -m "feat(fase-14c): ingreso con Google para el personal (vinculo desde Mi perfil, migracion GoogleParaPersonal, sesion de personal por Google)"
```
