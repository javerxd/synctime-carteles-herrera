# LetrerosHerreraSYNCTIMEapi

API REST para gestionar el flujo comercial de una empresa de letreros.

## Documentos principales

- `InformeTecnicoEntrega.md`: informe formal del proyecto.
- `ContratoFrontend.md`: rutas, reglas y cambios pendientes para adaptar el frontend.
- `OWASP.md`: mitigaciones de seguridad documentadas.

## Proyecto

La solucion esta en `LetrerosHerreraSYNCTIMEapi.slnx` e incluye:

- API: `LetrerosHerreraSYNCTIMEapi/`
- pruebas: `LetrerosHerreraSYNCTIMEapi.Tests/`

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
- En Development, la API aplica sola las migraciones pendientes al arrancar. Para probar sin tocar la base configurada, hay que apuntar `ConnectionStrings__DefaultConnection` a una base temporal.

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

Opcional, solo para empleados. El diseño, sus riesgos y lo que falta confirmar con un asesor legal estan en la seccion 7 de `ContratoFrontend.md`. **Una foto puede suplantar a una persona:** no es una proteccion fuerte, sino una comodidad junto a la contraseña.

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

Las pruebas crean una base temporal con las migraciones y la eliminan al terminar: nunca usan la base de desarrollo. Usan LocalDB (`(localdb)\MSSQLLocalDB`); con la variable de entorno `TEST_SQL_SERVER` se puede indicar otro servidor.

Los secretos se configuran mediante User Secrets o variables de entorno. No se deben publicar claves JWT, passwords ni cadenas de conexion en GitHub.

## Antes de produccion

Una revision de seguridad dejo estos puntos, que no afectan al desarrollo pero deben resolverse antes de publicar la API:

- **Proxy:** los limites por IP usan `RemoteIpAddress`. Detras de un proxy todos los usuarios comparten su IP, y un atacante podria bloquear el login de todos. Hay que configurar `ForwardedHeaders` con la lista de proxies de confianza.
- **Telefonos sin verificar:** alguien puede registrar un numero ajeno, y su titular ya no podria hacerlo. Hay que verificar el telefono por SMS antes de crear la cuenta.
- **Enumeracion de usuarios:** el registro revela que telefonos existen, y el login tarda menos si el usuario no existe porque no ejecuta BCrypt. Tampoco hay bloqueo por cuenta, solo limite por IP.
- **Sesiones:** un token sigue valido despues de desactivar al usuario, hasta que expira (8 horas por defecto).
- **Cabeceras:** faltan `UseHsts` y las cabeceras de seguridad.
- **Auditoria:** nada registra quien asigno credenciales a un cliente con `PUT /api/v1/clientes/{id}/credenciales`.
- **Migraciones:** la API solo las aplica sola en Development. En produccion hay que ejecutarlas como un paso del despliegue.
- **Login facial:**
  - Calibrar el umbral con pruebas consentidas de empleados reales. Hoy es un valor provisional, sin medicion.
  - Revisar con un asesor legal el consentimiento y las preguntas de la seccion 7 de `ContratoFrontend.md`.
  - La camara exige HTTPS, y la API debe agregarse a `connect-src` en `index.html`.
  - Detras de un proxy, los limites por IP del login facial y de la confirmacion de contraseña tienen el problema descrito arriba.
  - Crear una ruta para desactivar empleados que borre sus datos biometricos en el mismo paso.
