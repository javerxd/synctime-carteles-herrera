# Informe de cierre — Fase 1: cimientos del backend y base demo

Fecha: 14-11-2026 · Rama: `fase-01-cimientos-backend` (nace de `fase-00-preparacion`) · Estado: **en revisión, esperando el visto bueno del usuario**

Esta fase no cambia reglas de negocio ni el esquema de la base. Deja las piezas comunes que usarán todas las fases siguientes.

## Qué se hizo

| Tema | Resultado | Dónde |
|---|---|---|
| Formato único de error (RFS31, S21) | Toda respuesta de error, también 401, 403, 404, 429 y 500, es un ProblemDetails con `error`, `codigo`, `nivel` y `errores` por campo. Los 41 errores que devolvían los endpoints pasaron al formato nuevo. El campo `error` se conserva, así que el frontend actual sigue funcionando | `Api/ErrorApi.cs`, `Middleware/ApiExceptionHandler.cs` |
| Validación (RFS31, RNFO05, RNFO06) | Validadores de RUT con dígito verificador, monto en pesos, teléfono y correo, y un recolector de errores por campo, con mensajes en español | `Negocio/Validacion.cs` |
| Listados (RFS32) | Parámetros `pagina`, `tamano`, `orden` y `buscar`; respuesta paginada; orden solo por campos permitidos y con desempate | `Negocio/Listados.cs`, `Datos/Repositorio.cs` |
| Capas (RNFO10) | Carpetas `Api`, `Negocio` y `Datos`; repositorio base y unidad de trabajo; reglas comprobadas por pruebas de arquitectura | `docs/arquitectura.md` |
| Límites (RNFP07) | Límite por cuenta en los ingresos con contraseña (10 fallos por minuto), además del límite por IP. Con sesión, el límite general cuenta por usuario | `Security/LimitadorPorCuenta.cs`, `Program.cs` |
| Servidor (RNFP25) | Compresión Brotli y Gzip, cabeceras de seguridad, HSTS fuera de desarrollo, rutas `/salud` y `/salud/lista` | `Program.cs`, `Api/Infraestructura.cs` |
| Contrato (RNFP11, RNFP19) | `docs/api/openapi.json` con las 58 operaciones, su resumen y quién puede usarlas. Una prueba falla si el contrato y la API difieren | `docs/api/` |
| Permisos (RFS23) | Matriz por rol verificada automáticamente, con las diferencias frente a los requisitos | `docs/api/permisos.md` |
| Pruebas (RNFO13) | Proyecto nuevo de pruebas unitarias. Las de integración usan `localhost\SQLEXPRESS` con bases `SyncTime_Pruebas_…` y solo pueden eliminar bases con ese prefijo | `LetrerosHerreraSYNCTIMEapi.UnitTests/` |
| Base demo (RNFO12 reducido) | `LetreriaLosHerreraDB_Demo` creada con las 3 migraciones. Comando `sembrar-demo` listo | `Datos/SemillaDemo.cs` |

## Verificación

| Comprobación | Resultado |
|---|---|
| Pruebas unitarias | 96 de 96 |
| Pruebas de integración sobre `localhost\SQLEXPRESS` | 166 de 166 (136 anteriores y 30 nuevas). No quedan bases temporales |
| Pruebas del frontend | 79 de 79, sin cambios |
| Compilación | Sin errores ni advertencias |
| Contrato frente a la API | 58 de 58 operaciones coinciden |
| Acceso declarado frente al real | Cada operación se llamó como visitante, cliente, operario, diseñador y administrador. Encontró una diferencia real, ya corregida en el contrato |
| Pruebas no vacuas | Se reintrodujeron 6 errores. 5 hicieron fallar alguna prueba; el sexto demostró que el cambio que protegía no era necesario (ver hallazgos) |
| API en marcha contra la base demo | Arranca, `/salud/lista` responde bien, los errores salen con el formato estándar y las respuestas se comprimen |
| Base real | Intacta: 3 migraciones, 1 empleado, 1 rostro. No recibió datos ni migraciones |
| Semilla | Se niega a ejecutarse sin `Demo:Password` y contra la base real, en ambos casos con código de salida 1 |

## Revisión de ECC

Dos revisiones independientes (C# y seguridad). Ninguna encontró fallos críticos, y ambas confirmaron que la semilla, la migración automática y la limpieza de las pruebas no pueden tocar la base real.

**Corregido:**
- El límite por cuenta contaba también los aciertos y guardaba el correo completo como clave. Ahora cuenta solo los fallos, un acierto lo borra, la clave es un hash de tamaño fijo con normalización Unicode, la limpieza corre como mucho una vez por minuto y hay un tope de cuentas en memoria.
- Las cabeceras de seguridad se perdían en las respuestas que pasan por el manejador de excepciones (409 y 500). Ahora se escriben al enviar la respuesta. Una prueba lo cubre.
- `Ordenar` no tenía desempate, así que dos páginas podían repetir u omitir filas.
- La prueba de contrato ejecutaba de verdad `DELETE /auth/rostro`. Ahora no.
- Menores: formato del RUT, correos con corchetes o puntos dobles, límite de tasa en `/salud/lista`, largo máximo de correo y contraseña al iniciar sesión, `Cache-Control: no-store` en las respuestas de autenticación, liberación de recursos en las pruebas y RUT de la semilla en un rango alto.

**Descartado con evidencia:** se propuso mover CORS antes del manejador de excepciones. La prueba pasa en ambos órdenes, así que el orden no era el problema.

## Hallazgos

1. **Un defecto mío, detectado al rehacer el limitador.** Apliqué el límite por cuenta también al acceso con rostro. Eso contradecía una regla del diseño original: un anónimo no debe poder bloquear el rostro de otra persona. Lo retiré de ese ingreso, que conserva su límite por IP y su bloqueo propio. Una prueba lo cubre.
2. **Los User Secrets apuntan a la base real.** `ConnectionStrings:DefaultConnection` está definido ahí y tiene prioridad sobre `appsettings.Development.json`. Al arrancar la API sin perfil se conectó a la base real; la guarda impidió que migrara o escribiera. Solución: los perfiles `http` y `https` fijan la base demo, hay un perfil `base-real`, y la API anuncia al arrancar qué base usa. Si se ejecuta sin perfil, siguen valiendo los secretos.
3. **SQL Server falló por falta de memoria del equipo.** Con 1 GB libre de 7,9 GB, dos ejecuciones de la suite fallaron por tiempo de espera al conectar. Se dieron esperas más amplias a las pruebas. Conviene cerrar aplicaciones pesadas antes de ejecutar la suite.

## Lo que queda pendiente, y en qué fase

- **Los validadores todavía no se aplican a las rutas existentes.** Se aplican al rehacer cada módulo: cuentas (6), clientes y solicitudes (7), empleados (9).
- **Ningún listado está paginado aún.** Cada uno se adapta en su fase.
- **Ocho archivos de `Endpoints/` consultan la base directamente.** Están declarados en la prueba de arquitectura, con su fase.
- **El contrato no declara los códigos de respuesta por operación**, y la documentación interactiva (Scalar) todavía no muestra los resúmenes. Se completan módulo a módulo.
- **Riesgos de seguridad que no se corrigen aquí** (anotados en el README del backend): el ingreso con contraseña tarda menos si la cuenta no existe, y el registro revela qué teléfonos, RUT y correos ya existen. Ambos existían antes y se corrigen en la fase 6. El límite por cuenta permite bloquear la contraseña de otra persona mientras se insista.
- **SDK de .NET.** Sin respuesta sobre instalar el SDK 10: se siguió con el 11 preliminar.

## Lo que no se verificó

- El tope de tamaño del cuerpo en los ingresos (8 KB): lo aplica el servidor real, no el de pruebas.
- HSTS y la política de contenido estricta: solo se activan fuera de desarrollo.
- La semilla contra la base demo real: falta la contraseña del usuario. Sí está probada contra una base temporal.

## Se necesita del usuario

1. **Confirmar la matriz de permisos**: las tres decisiones al final de `docs/api/permisos.md`.
2. **Definir la contraseña de los usuarios demo y cargar la semilla**, con los dos comandos del README del backend (sección "Bases de datos y perfiles de arranque").
3. Decidir si instala el SDK de .NET 10.
