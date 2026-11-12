# LetrerosHerreraSYNCTIMEapi

API REST para gestionar el flujo comercial de una empresa de letreros.

## Documentos principales

- `InformeTecnicoEntrega.md`: informe formal del proyecto.
- `OWASP.md`: mitigaciones de seguridad documentadas.

En la carpeta `docs/` de la raiz del repositorio:

- `docs/api/openapi.json`: contrato de la API. Se edita antes de implementar una ruta; una prueba falla si no coincide con la API.
- `docs/api/contrato.md`: reglas de negocio y notas para el frontend.
- `docs/api/permisos.md`: que puede hacer cada rol, verificado por una prueba.
- `docs/arquitectura.md`: capas, patrones y como agregar un modulo.

## Proyecto

La solucion esta en `LetrerosHerreraSYNCTIMEapi.slnx` e incluye:

- API: `LetrerosHerreraSYNCTIMEapi/`
- pruebas de integracion: `LetrerosHerreraSYNCTIMEapi.Tests/` (levantan la API contra una base temporal)
- pruebas unitarias: `LetrerosHerreraSYNCTIMEapi.UnitTests/` (reglas de negocio y arquitectura, sin base de datos)

## Base de datos

El modelo de EF Core y sus migraciones son la unica fuente de verdad del esquema. Para crear o actualizar la base:

```powershell
dotnet ef database update --project .\LetrerosHerreraSYNCTIMEapi
```

- `InitialCreateV6` (generada): tablas, restricciones, indices y datos iniciales.
- `ObjetosProgramablesV6` (escrita a mano): trigger, vista y procedimiento almacenado.
- `AgregarRostrosEmpleado` (generada): solo crea la tabla `rostros_empleado`, para el login facial.

Reglas para no volver a desincronizar la base:

- Los cambios de esquema se hacen en el modelo y con `dotnet ef migrations add`, no con scripts sueltos. Una prueba falla si el modelo cambia sin su migracion.
- Si alguna vez se ejecuta un `.sql` con acentos usando `sqlcmd`, hay que agregar `-f 65001`. Sin esa opcion las letras como la `ñ` se guardan corruptas.
- `dotnet ef migrations remove --force` tambien revierte la migracion en la base configurada: sus tablas y datos se pierden.
- La collation `Modern_Spanish_CI_AS` se aplica cuando EF crea la base. En una base creada a mano hay que definirla al crearla.
- La API aplica sola las migraciones pendientes solo en la base de demostracion (nombre terminado en `_Demo`) y en las bases temporales de las pruebas. En cualquier otra, incluida la real, solo avisa: hay que respaldarla y ejecutar `dotnet ef database update` a mano.

## Bases de datos y perfiles de arranque

Todo corre en la instancia local `localhost\SQLEXPRESS`, con autenticacion de Windows.

| Base | Para que | Como se usa |
| --- | --- | --- |
| `LetreriaLosHerreraDB_Demo` | Desarrollo y demostracion, con datos sinteticos | `dotnet run` (perfiles `http` y `https`) |
| `LetreriaLosHerreraDB` | La base real | `dotnet run --launch-profile base-real` |
| `SyncTime_Pruebas_…` | Temporales de las pruebas automaticas | Las crean y eliminan las propias pruebas |

Al arrancar, la API escribe en el registro que base esta usando. Los perfiles fijan la base con la variable `ConnectionStrings__DefaultConnection`, que tiene prioridad sobre User Secrets: si se ejecuta la API sin perfil, vale lo que digan los secretos.

Para cargar los datos de demostracion hay que definir primero la contraseña que tendran sus usuarios. `Out-Null` evita que se muestre en pantalla:

```powershell
dotnet user-secrets set "Demo:Password" "UNA_PASSWORD_PROPIA" --project .\LetrerosHerreraSYNCTIMEapi | Out-Null
```

```powershell
dotnet run --project .\LetrerosHerreraSYNCTIMEapi -- sembrar-demo
```

Crea un usuario por rol (`dueno`, `admin`, `disenador` y `operario`, todos `@letreria.test`), un cliente con cuenta (`cliente@letreria.test`), publicaciones y solicitudes. El comando se niega a escribir en una base cuyo nombre no termine en `_Demo`, y repetirlo no duplica nada.

## Primer administrador

Sin empleados nadie puede iniciar sesion. Al arrancar, si la tabla `empleados` esta vacia, la API crea el primero con los datos de `Bootstrap:Admin` (`Rut`, `Nombre`, `Apellido`, `Telefono`, `Email`, `Password` y, opcional, `Rol`). Se configuran con User Secrets, por ejemplo:

```powershell
dotnet user-secrets set "Bootstrap:Admin:Password" "UNA_PASSWORD_PROPIA" --project .\LetrerosHerreraSYNCTIMEapi
```

Despues del primer arranque hay que borrar la password de la configuracion:

```powershell
dotnet user-secrets remove "Bootstrap:Admin:Password" --project .\LetrerosHerreraSYNCTIMEapi
```

## Login facial

Opcional, solo para empleados. El diseño, sus riesgos y lo que falta confirmar con un asesor legal estan en la seccion 7 de `docs/api/contrato.md`. **Una foto puede suplantar a una persona:** no es una proteccion fuerte, sino una comodidad junto a la contraseña.

Para habilitarlo hay que aplicar la migracion y configurar una clave de 32 bytes en base64. Este comando la genera sin mostrarla: `Out-Null` es necesario porque `user-secrets set` imprime el valor que guarda.

```powershell
$b = New-Object byte[] 32; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); dotnet user-secrets set "RostroFacial:ClaveCifrado" ([Convert]::ToBase64String($b)) --project .\LetrerosHerreraSYNCTIMEapi | Out-Null
```

Sin clave, la API funciona igual y solo el login facial queda deshabilitado. Si la clave se pierde o cambia, cada empleado debe volver a activar su rostro. En produccion debe ir en una variable de entorno o un almacen de secretos, nunca junto a una copia de la base.

Opciones de `RostroFacial`, todas opcionales salvo la clave: `UmbralDistancia` (0,5, provisional), `DiasEquipo` (30) y `TiempoMinimoFalloMs` (400).

Al desactivar un empleado por SQL hay que borrar tambien sus datos biometricos. Si no, se borran en el siguiente intento de login facial de cualquier persona, y si se le reactiva antes, su rostro seguiria activo:

```sql
UPDATE rostros_empleado
SET descriptor_cifrado = NULL, token_equipo_hash = NULL, fecha_autorizacion_equipo = NULL, fecha_revocacion = SYSDATETIMEOFFSET()
WHERE id_empleado = @id AND fecha_revocacion IS NULL;
```

## Validacion

```powershell
dotnet test .\LetrerosHerreraSYNCTIMEapi.slnx -c Release
```

Las pruebas de integracion crean una base temporal con las migraciones y la eliminan al terminar: nunca usan la base real ni la de demostracion. Usan `localhost\SQLEXPRESS`; con la variable de entorno `TEST_SQL_SERVER` se puede indicar otro servidor. El codigo de pruebas solo elimina bases cuyo nombre empiece por `SyncTime_Pruebas_`.

Limites de intentos, todos por minuto y configurables en `RateLimiting`: `Login`, `LoginFacial`, `Reautenticacion` y `Publico` (10 por IP), `PorCuenta` (10 fallos por cuenta en los ingresos con contraseña), `ApiGeneral` (100 por usuario, o por IP sin sesion) y `Salud` (60 por IP).

Los secretos se configuran mediante User Secrets o variables de entorno. No se deben publicar claves JWT, passwords ni cadenas de conexion en GitHub.

## Antes de produccion

Una revision de seguridad dejo estos puntos, que no afectan al desarrollo pero deben resolverse antes de publicar la API:

- **Proxy:** los limites por IP usan `RemoteIpAddress`. Detras de un proxy todos los usuarios comparten su IP, y un atacante podria bloquear el login de todos. Hay que configurar `ForwardedHeaders` con la lista de proxies de confianza.
- **Telefonos sin verificar:** alguien puede registrar un numero ajeno, y su titular ya no podria hacerlo. Hay que verificar el telefono por SMS antes de crear la cuenta.
- **Enumeracion de usuarios:** el registro revela que telefonos, RUT y correos ya existen, y el login tarda menos si el usuario no existe porque no ejecuta BCrypt. Se corrige en la fase 6 del plan.
- **Bloqueo de una cuenta ajena:** el limite por cuenta permite que quien conoce el correo de otra persona agote sus intentos y le impida entrar con contraseña mientras insista. Es propio de todo limite por cuenta; el acceso con rostro no se ve afectado.
- **Limites en memoria:** el limite por cuenta vive en la memoria del proceso. Con mas de una instancia de la API se multiplica, y se pierde al reiniciar.
- **Sesiones:** un token sigue valido despues de desactivar al usuario, hasta que expira (8 horas por defecto).
- **HTTPS y sondas:** HSTS y la redireccion a HTTPS ya estan activos fuera de desarrollo. Una sonda de salud por HTTP recibira una redireccion: hay que sondear por HTTPS o desde la red interna.
- **Auditoria:** nada registra quien asigno credenciales a un cliente con `PUT /api/v1/clientes/{id}/credenciales`.
- **Migraciones:** la API solo las aplica sola en Development. En produccion hay que ejecutarlas como un paso del despliegue.
- **Login facial:**
  - Calibrar el umbral con pruebas consentidas de empleados reales. Hoy es un valor provisional, sin medicion.
  - Revisar con un asesor legal el consentimiento y las preguntas de la seccion 7 de `docs/api/contrato.md`.
  - La camara exige HTTPS, y la API debe agregarse a `connect-src` en `index.html`.
  - Detras de un proxy, los limites por IP del login facial y de la confirmacion de contraseña tienen el problema descrito arriba.
  - Crear una ruta para desactivar empleados que borre sus datos biometricos en el mismo paso.
