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
- `SepararEstadoPedidoYPago` (generada y completada a mano): el pedido pasa a tener estado operativo (`estado_pedido`, con `anulado` en vez de `cancelado`) y estado de pago calculado (`estado_pago`), con `monto_abonado`, `saldo_pendiente`, `numero_factura` y `origen`. Convierte los pedidos `cancelado` y recrea la vista de conversion. Revision de la profesora guia, 12-11-2026.
- `HistorialDePagos` (generada y completada a mano): tabla `pagos` con cada abono (monto, fecha, medio de pago, quien lo registro) y el trigger `trg_pagos_recalcular_monto_abonado`, que mantiene `pedidos.monto_abonado` igual a la suma de los pagos. Aprobada por el usuario el 12-11-2026.
- `CuentasYSesion` (generada por EF): tablas `tokens_renovacion` (sesiones con renovacion) y `codigos_cuenta` (enlaces de verificacion y codigos de recuperacion), la columna `clientes.fecha_verificacion_correo` y la restriccion `ck_clientes_cuenta_con_correo`. Solo agrega. Aplicada en la base demo; la base real espera la autorizacion del usuario.
- `SolicitudesYAdjuntos` (generada por EF y completada a mano): tablas `adjuntos_solicitud` (logos y diseños de cada solicitud, privados) y `solicitudes_historial` (cada cambio de estado); la columna `solicitudes.huella_ip` con su índice; y las columnas ampliadas `solicitudes.descripcion_general` (2000), `solicitudes.motivo_rechazo` (500) y `solicitud_detalles.descripcion` (500). Solo agrega y amplía: no borra nada. Aplicada en la base demo; la base real espera la autorización del usuario.

El script SQL completo de la base, el diagrama entidad-relacion y el diccionario de datos estan en `docs/base-de-datos/` de la raiz del repositorio.

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

Crea un usuario por rol (`dueno`, `admin`, `disenador` y `operario`, todos `@letreria.test`), un cliente con cuenta (`cliente@letreria.test`), publicaciones y solicitudes. Desde la fase 3 tambien carga 12 pedidos de varios meses, con su solicitud, su cotizacion aprobada y sus abonos, que cubren todos los estados del pedido y de pago (entre ellos abonados, entregados sin pagar y anulados) y ambos origenes (cotizacion y pedido directo). Desde la fase 5 tambien carga la vitrina de demostracion: 22 trabajos con fotos reales del taller, tomadas de `docs/imagenes-trabajos/vitrina/` (otra carpeta con `Demo:CarpetaFotosVitrina`), con la autorizacion de logo registrada a nombre del dueño demo. Las publicaciones inventadas de las fases anteriores quedan ocultas. Si la base demo ya tenia los datos de la fase 1, repetir el comando agrega solo lo que falta (pedidos, trabajos e imagenes). El comando se niega a escribir en una base cuyo nombre no termine en `_Demo`, y repetirlo no duplica nada.

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

## Correo

Por cada solicitud nueva, presencial o en linea, la API deja en una cola un aviso para el taller. Un proceso en segundo plano lo envia. Si el servidor de correo falla, reintenta a los 1, 5, 15, 60 y 180 minutos; la solicitud nunca espera ni falla por eso. Lo enviado o fallido se borra de la cola a los 30 dias, porque lleva datos del cliente.

**Modos** (`Correo:Modo`):
- `Smtp`: envio real. Es el valor por defecto.
- `Bandeja`: el de desarrollo (`appsettings.Development.json`). Cada correo queda como un archivo `.eml` en `bandeja-correo/` y se ve en `https://localhost:7112/dev/bandeja`, una ruta que solo existe en desarrollo. No sale nada del equipo.

### Gmail

`appsettings.json` ya trae `smtp.gmail.com`, puerto 587 y STARTTLS. Faltan tres valores, que van en User Secrets y nunca en el codigo. En produccion van como variables de entorno: `Correo__Smtp__Usuario`, `Correo__Smtp__Password` y `Correo__AvisosTaller`.

1. En la cuenta de Google que enviara los avisos, crear una **contraseña de aplicacion** en <https://myaccount.google.com/apppasswords>. Exige tener activa la verificacion en dos pasos. Son 16 letras y se escriben sin espacios.
2. Guardar los valores. La contraseña se escribe sin que se vea en pantalla, y `Out-Null` evita que `user-secrets` la repita:

```powershell
dotnet user-secrets set "Correo:Smtp:Usuario" "cuenta.del.taller@gmail.com" --project .\LetrerosHerreraSYNCTIMEapi
$clave = Read-Host "Contraseña de aplicacion" -AsSecureString
dotnet user-secrets set "Correo:Smtp:Password" ([Net.NetworkCredential]::new('', $clave).Password) --project .\LetrerosHerreraSYNCTIMEapi | Out-Null
Remove-Variable clave
dotnet user-secrets set "Correo:AvisosTaller" "destino.de.los.avisos@gmail.com" --project .\LetrerosHerreraSYNCTIMEapi
```

   `Correo:AvisosTaller` admite varios correos separados por `;`. Ningun otro archivo lee ese valor: todo pasa por `IDestinatariosAvisos`, que en la fase 9 leera primero el correo que el dueño guarde desde el panel.

3. Probar el envio real. No pasa por la cola ni toca la base de datos. Dice a cuantos destinatarios llego y, si falla, por que, sin mostrar credenciales:

```powershell
dotnet run --project .\LetrerosHerreraSYNCTIMEapi --launch-profile https -- probar-correo
```

4. Para que en desarrollo los avisos de las solicitudes salgan por Gmail en vez de la bandeja local, agregar `Correo:Modo` = `Smtp` a User Secrets. Para volver a la bandeja, quitarlo con `dotnet user-secrets remove "Correo:Modo"`.

**Si la cuenta no admite contraseñas de aplicacion:** pasa, por ejemplo, si el administrador de Google Workspace las desactivo o si la cuenta tiene la Proteccion Avanzada. La alternativa es OAuth2 (XOAUTH2, que MailKit admite). Exige un proyecto en Google Cloud con su pantalla de consentimiento y un token de actualizacion. Mientras la aplicacion siga «en prueba», Google vence ese token a los 7 dias, asi que hay que publicarla. Se agrega solo si hace falta.

Otras opciones de `Correo`:
- `Remitente:Nombre` (`Letreros Herrera`).
- `Remitente:Direccion`. Si se deja vacia, se usa la cuenta, que es lo que exige Gmail.
- `Smtp:TiempoEsperaSegundos` (20).
- `TopeAvisosPorHora` (20). Pasado ese numero de solicitudes en una hora, llega un solo aviso de «muchas solicitudes» en vez de uno por cada una; todas se guardan igual. Protege el cupo diario de Gmail (unos 500 envios) ante un envio masivo de solicitudes falsas.
- `TopeConsultasPorHora` (5): cuantos de esos avisos por hora pueden ser de consultas del formulario de contacto. El resto del cupo queda para las solicitudes.
- `Cola:Activa`, `Cola:IntervaloSegundos` (30), `Cola:Lote` (10), `Cola:MaxIntentos` (6) y `Cola:DiasRetencion` (30).

`Correo:Modo` y `Correo:Smtp:Seguridad` se revisan al arrancar: un valor mal escrito impide iniciar la API, en vez de aceptar solicitudes que nunca avisarian. Si el servidor o la cuenta fallan (Gmail caido, contraseña cambiada), los avisos se siguen intentando cada 3 horas sin agotarse y salen en cuanto se arregla. Solo un fallo del propio mensaje los agota.

**Como saber si los avisos estan llegando:** `GET /salud/correo` responde `ok` o `degradado`, este ultimo si hay avisos fallidos o que llevan mas de 30 minutos sin salir, si falta configuracion o si no hay destinatarios. No afecta a `/salud/lista`: un problema del correo no saca la API de servicio. Un monitor externo gratuito puede revisar esa ruta y avisar al dueño por otro medio. Ademas, mientras haya problemas, la API los repite cada hora en el registro de errores.

## Imagenes

`POST /api/v1/imagenes` (dueño y administrador) recibe un JPG, PNG o WebP de hasta 10 MB y lo guarda en WebP en dos tamaños: 1600 y 640 px por el lado mayor. El tipo se reconoce por el contenido del archivo y no por su nombre. La foto se endereza segun la camara y pierde sus metadatos, incluida la ubicacion GPS. Los archivos quedan en `almacen/imagenes` (`Imagenes:Carpeta`), fuera del repositorio. En produccion esa carpeta debe ser un volumen persistente (fase 16).

## Portada publica

Los endpoints publicos de la portada estan en `Endpoints/PortadaApi.cs`, y la vitrina, en `Endpoints/CatalogoApi.cs`. Una vez al dia, `PurgaDatosPersonales` aplica los plazos de la politica de privacidad: borra las consultas de contacto a los 12 meses y los consentimientos de visitantes a los 24 meses (los de clientes y personal, 24 meses despues de retirarlos); `Privacidad:PurgaActiva=false` la desactiva. Los correos para clientes salen de la casilla de avisos y piden responder a `Correo:ContactoTaller` (el correo publico), que la API exige distinto de `Correo:Smtp:Usuario`. Lo que devuelve cada uno esta en `docs/api/contrato.md`, seccion «Portada publica».

- **Estado del local** (`GET /api/v1/local/estado`): abierto solo si la hora de Chile cae dentro del horario y el cartel dice abierto. Usa la zona `America/Santiago`. Si el servidor no la tiene, la API no arranca y lo indica. En Linux hace falta el paquete `tzdata`.
- **Formulario de contacto** (`POST /api/v1/contacto`): guarda la consulta y su consentimiento de privacidad, y deja un aviso al taller por correo, con la respuesta dirigida a quien escribio. Tiene un campo trampa, un tamaño máximo de 16 KB y 5 envios por hora por IP.
- **Cookies** (`POST /api/v1/consentimientos/cookies`): registra la aceptacion de la analitica o su retiro. Hoy el sitio no tiene analitica.
- **Logotipos de clientes:** una publicacion con logo solo se muestra con la autorizacion registrada (`PUT /api/v1/catalogo/{id}/autorizacion-logo`, dueño o administrador).
- **Base:** la tabla `consultas_contacto` y las columnas nuevas de `catalogo` estan en la migracion `PortadaPublica`. Esta aplicada en la base demo; la base real espera a que terminen todas las fases.

## Cuentas y sesion (fase 6)

Los endpoints estan en `Endpoints/AuthApi.cs` (ingreso, registro, renovacion y recuperacion), `Endpoints/CuentaApi.cs` (`/api/v1/mi-cuenta`) y `Endpoints/CookiesSesion.cs` (las cookies). La logica esta en `Services/ServiciosCuenta.cs`: `ServicioSesiones`, `ServicioCodigosCuenta` y `ServicioMiCuenta`. Lo que devuelve cada ruta esta en `docs/api/contrato.md`, seccion «Cuenta y sesion (fase 6)».

- **Sesion:** el token de acceso dura 15 minutos (`Jwt:ExpirationMinutes`). La sesion la mantiene la cookie `synctime_renovacion`, `httpOnly` y solo para `/api/v1/auth`, que rota en cada renovacion. Dura 12 horas para el personal (`Sesion:HorasPersonal`) y 7 dias para el cliente (`Sesion:DiasCliente`), desde el ingreso. Si vuelve a aparecer un token ya usado, se cierra esa sesion.
- **Correo:** el cliente ingresa con su correo, no con su telefono. Si la cuenta tiene contraseña, tiene correo. El enlace para confirmarlo sale desde `Sitio:UrlPublica`: si esa clave esta vacia, la cuenta funciona, pero el enlace no se envia y queda un error en el registro. El cliente puede pedirlo de nuevo desde Mi cuenta.
- **Recuperacion:** codigo de 6 digitos por correo, que vence en 15 minutos y admite 5 intentos. Como maximo un codigo por minuto y cinco por hora por cuenta. La respuesta es la misma exista o no la cuenta. Los codigos se guardan con HMAC y `Jwt:Key`: cambiar esa clave invalida los pendientes.
- **Limites:** ademas de los de `RateLimiting` por IP, `LimitadorPorCuenta` cuenta 10 fallos por minuto para el ingreso, la recuperacion y los cambios de correo y contraseña (`RateLimiting:PorCuenta`).
- **Cookies y CORS:** las cookies son `Secure` y `SameSite=Strict`. CORS acepta credenciales solo de los origenes de `Cors:AllowedOrigins`, nunca `*`. La web y la API deben servirse en el mismo sitio: si no, el navegador no envia la cookie de renovacion.
- **Purga:** una vez al dia, `PurgaDatosPersonales` borra las sesiones y los codigos vencidos hace mas de un dia.

Configuracion nueva:

- `Jwt:ExpirationMinutes`: 15 por defecto. Si falta o no es mayor que cero, se usa 15.
- `Sesion:HorasPersonal` (12) y `Sesion:DiasCliente` (7).
- `Sitio:UrlPublica`: la direccion de la web desde la que se confirma el correo. En Development es `http://localhost:3000`. Sin ella no se envian los enlaces. En produccion debe ser la direccion publica (fase 16).
- `RateLimiting:PorCuenta` (10 fallos por minuto).
- `Correo:ContactoTaller`: la direccion a la que responden los correos de la cuenta (desde la fase 5).

## Solicitudes, adjuntos y clientes (fase 7)

Los endpoints del personal están en `Endpoints/SolicitudApi.cs`, los del cliente y los adjuntos privados, en `Endpoints/MisSolicitudesApi.cs`, la lectura del formulario de archivos, en `Endpoints/ArchivoSubido.cs`, y los de clientes, en `Endpoints/CatalogoApi.cs`. La lógica está en `Services/ServiciosSolicitudes.cs` (`ServicioSolicitudes`, `ServicioAdjuntos` y `ServicioClientes`). Las reglas, sin HTTP ni base de datos, están en `Negocio/Solicitudes/` (`ReglasSolicitud.cs`, `ReglasAdjunto.cs`, `Comprobante.cs`). Los archivos los guarda `Infraestructura/Adjuntos/AlmacenAdjuntos.cs`. Lo que devuelve cada ruta está en `docs/api/contrato.md`, en las secciones «Solicitudes (fase 7)», «Portal del cliente (fase 7)» y «Adjuntos (fase 7)».

- **Regla contra solicitudes falsas:** antes de guardar una solicitud en línea revisa la cuenta, el RUT y la IP. Si frena, no escribe nada y responde `409` con `contacto_directo`. Las solicitudes en línea se revisan y guardan de a una: un bloqueo de aplicación (`sp_getapplock`, recurso `synctime_solicitudes_en_linea`) pone en fila los envíos. Si no consigue el turno en `Solicitudes:EsperaBloqueoMs`, responde `503` con `servicio_ocupado` y `Retry-After: 5`.
- **Huella de la IP:** la tabla guarda el HMAC-SHA256 de la IP (`solicitudes.huella_ip`), nunca la IP. En IPv6 se usa el prefijo /64. La clave sale de `Solicitudes:ClaveHuella` o, si no existe, de `Jwt:Key` con HKDF.
- **Correos:** el aviso al taller y la confirmación al cliente se escriben en `correos_salientes` dentro de la misma transacción que la solicitud (`ColaEnTransaccion`, `IAvisosDelTaller.SolicitudNuevaAsync`). El envío lo hace la cola en segundo plano (fase 4). Las presenciales no avisan al taller.
- **Adjuntos:** el formato se decide por el contenido. JPG, PNG y WebP pasan por `ProcesadorImagenes` (WebP grande y miniatura, sin metadatos). Un PDF (`%PDF-`) se guarda tal cual. Lo demás se rechaza. `PurgaDatosPersonales`, una vez al día, borra los borradores de más de 24 horas y los archivos que ya no tienen registro.
- **Historial:** cada cambio de estado de una solicitud queda en `solicitudes_historial`, con quién lo hizo.

Configuración nueva:

- `Adjuntos:Carpeta` (`almacen/adjuntos`): carpeta privada, fuera del repositorio (`.gitignore`: `**/almacen/`). Ninguna ruta pública la sirve. En producción debe ser un volumen persistente que no se publique (fase 16).
- `RateLimiting:Adjuntos` (10): subidas de adjuntos por minuto y por usuario.
- `Solicitudes:ClaveHuella` (opcional, de al menos 16 bytes): clave del HMAC de la IP. Si no existe, se deriva de `Jwt:Key`. Cambiarla hace que las huellas guardadas ya no coincidan con las nuevas: la regla por IP solo mira las últimas 24 horas.
- `Solicitudes:EsperaBloqueoMs` (15000, entre 100 y 60000): cuánto espera una solicitud o una subida el turno antes de responder `503`.

Pruebas: `LetrerosHerreraSYNCTIMEapi.Tests/SolicitudesFase7Tests.cs` (integración, con base temporal) y `LetrerosHerreraSYNCTIMEapi.UnitTests/SolicitudesTests.cs` (reglas puras).

## Validacion

```powershell
dotnet test .\LetrerosHerreraSYNCTIMEapi.slnx -c Release
```

Las pruebas de integracion crean una base temporal con las migraciones y la eliminan al terminar: nunca usan la base real ni la de demostracion. Usan `localhost\SQLEXPRESS`; con la variable de entorno `TEST_SQL_SERVER` se puede indicar otro servidor. El codigo de pruebas solo elimina bases cuyo nombre empiece por `SyncTime_Pruebas_`.

Limites de intentos, configurables en `RateLimiting`. Son por minuto, salvo `ContactoPorHora` y `CookiesPorHora` (5 por hora cada uno, para el formulario de contacto y el aviso de cookies): `Login`, `LoginFacial`, `Reautenticacion` y `Publico` (10 por IP), `PorCuenta` (10 fallos por cuenta por minuto en los ingresos, la recuperacion y los cambios de correo o contraseña), `ApiGeneral` (100 por usuario, o por IP sin sesion), `Salud` (60 por IP) e `Imagenes` (600 por IP, para servir las imagenes de la vitrina).

Los secretos se configuran mediante User Secrets o variables de entorno. No se deben publicar claves JWT, passwords ni cadenas de conexion en GitHub.

## Antes de produccion

Una revision de seguridad dejo estos puntos, que no afectan al desarrollo pero deben resolverse antes de publicar la API:

- **Proxy:** los limites por IP usan `RemoteIpAddress`. Detras de un proxy todos los usuarios comparten su IP, y un atacante podria bloquear el login de todos. Hay que configurar `ForwardedHeaders` con la lista de proxies de confianza. Desde la fase 7 esto también afecta a la regla contra solicitudes falsas: sin ello, las solicitudes de todos los clientes llegan con la IP del proxy y la regla frena a todos a la vez.
- **Telefonos sin verificar:** alguien puede registrar un numero ajeno, y su titular ya no podria hacerlo. Hay que verificar el telefono por SMS antes de crear la cuenta.
- **Enumeracion de usuarios:** el registro revela que un correo ya tiene cuenta. Desde la fase 6, un telefono o un RUT ya registrados dan el mismo mensaje, y el login gasta el mismo tiempo exista o no la cuenta. Para no revelar tampoco el correo habria que confirmarlo antes de crear la cuenta: queda como decision pendiente.
- **Bloqueo de una cuenta ajena:** el limite por cuenta permite que quien conoce el correo de otra persona agote sus intentos y le impida entrar con contraseña mientras insista. Es propio de todo limite por cuenta; el acceso con rostro no se ve afectado. Desde la fase 6 tambien bloquea la recuperacion de esa cuenta.
- **Limites en memoria:** el limite por cuenta vive en la memoria del proceso. Con mas de una instancia de la API se multiplica, y se pierde al reiniciar.
- **Sesiones:** el token de acceso sigue valido hasta 15 minutos despues de desactivar al usuario, cambiar su contraseña o cerrar su sesion. La renovacion se corta de inmediato (fase 6).
- **HTTPS y sondas:** HSTS y la redireccion a HTTPS ya estan activos fuera de desarrollo. Una sonda de salud por HTTP recibira una redireccion: hay que sondear por HTTPS o desde la red interna.
- **Auditoria:** nada registra quien asigno credenciales a un cliente con `PUT /api/v1/clientes/{id}/credenciales`, ni quien cambio el correo de un cliente con `PUT /api/v1/clientes/{id}`. Desde la fase 7, solo las solicitudes tienen historial de cambios (`solicitudes_historial`).
- **Portada:** `frame-ancestors` (contra clickjacking) no funciona en la etiqueta `meta` de `index.html`: la cabecera la debe enviar el servidor. Las consultas de contacto y los consentimientos de cookies no tienen plazo de borrado todavia.
- **Migraciones:** la API solo las aplica sola en Development. En produccion hay que ejecutarlas como un paso del despliegue.
- **Sesion y sitio:** la cookie de renovacion es `SameSite=Strict`, así que la web y la API deben servirse en el mismo sitio, y `Sitio:UrlPublica` debe ser la direccion publica de la web. Ver `docs/plan/desviaciones.md`.
- **Login facial:**
  - Calibrar el umbral con pruebas consentidas de empleados reales. Hoy es un valor provisional, sin medicion.
  - Revisar con un asesor legal el consentimiento y las preguntas de la seccion 7 de `docs/api/contrato.md`.
  - La camara exige HTTPS, y la API debe agregarse a `connect-src` en `index.html`.
  - Detras de un proxy, los limites por IP del login facial y de la confirmacion de contraseña tienen el problema descrito arriba.
  - Crear una ruta para desactivar empleados que borre sus datos biometricos en el mismo paso.
