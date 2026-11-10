# Informe tecnico

## LetrerosHerreraSYNCTIMEapi

**Asignatura:** Lenguaje de Programacion Web II  
**Unidad:** UT1 - Diseno e implementacion de APIs RESTful  
**Base de datos:** `LetreriaLosHerreraDB`  
**Tecnologia:** ASP.NET Core Minimal API, .NET 10 y Entity Framework Core  
**Fecha de entrega original:** 07-09-2026  
**Ultima actualizacion:** 06-11-2026

> La version entregada el 07-09-2026 describia la base anterior, `LetrerosHerreradb`. Este informe describe el estado actual, despues de migrar a `LetreriaLosHerreraDB` y agregar el login facial de empleados. El texto original se conserva en el historial de Git.

---

## 1. Resumen del proyecto

`LetrerosHerreraSYNCTIMEapi` es una API REST para apoyar la gestion de una empresa de letreros. El sistema organiza el flujo principal del negocio:

1. Un cliente envia una solicitud por la web, o el personal la registra en el local.
2. El personal consulta los productos y prepara una cotizacion.
3. Una cotizacion aprobada, sobre una solicitud aceptada, se convierte en un pedido.
4. El personal actualiza el pedido y el cliente con cuenta puede seguir sus solicitudes y pedidos.
5. El negocio publica una vitrina de trabajos, sus horarios y un cartel digital de estado.

La API utiliza Minimal API, siguiendo el estilo estudiado en `GamesApi`. La diferencia es que el dominio cambia de videojuegos a letreros y se incorporan controles adicionales solicitados por la rubrica: autenticacion, roles, validaciones, middleware, pruebas, documentacion y medidas de seguridad.

---

## 2. Objetivos

### Objetivo general

Construir una API REST funcional que permita administrar el flujo comercial de una empresa de letreros, conectada a SQL Server y protegida mediante autenticacion JWT y autorizacion por roles.

### Objetivos especificos

- Aplicar Minimal API sin controladores MVC.
- Definir el esquema de `LetreriaLosHerreraDB` con el modelo de Entity Framework Core y sus migraciones.
- Exponer operaciones CRUD para clientes, categorias, productos, catalogo, solicitudes, cotizaciones y pedidos.
- Validar datos, relaciones y reglas del flujo antes de guardar.
- Implementar login con contrasenas hasheadas mediante BCrypt.
- Ofrecer a los empleados un login facial opcional, sin reemplazar la contraseña y explicitando lo que no protege.
- Generar tokens JWT con identidad y rol.
- Restringir operaciones segun el rol del usuario.
- Evitar que una edicion con datos desactualizados pise el trabajo de otra persona.
- Documentar la API con OpenAPI y Scalar.
- Agregar middleware propio y pruebas de integracion.
- Mantener secretos fuera de los archivos versionables.

---

## 3. Caso de negocio

La empresa recibe solicitudes para fabricar letreros y otros productos o servicios relacionados. Una solicitud puede llegar en forma presencial o por la web; es evaluada por el personal mediante una cotizacion; y, si se aprueba, se transforma en un pedido gestionado por el personal operativo.

La base de datos tiene 14 tablas: `clientes`, `empleados`, `categorias`, `productos`, `catalogo`, `solicitudes`, `solicitud_detalles`, `materiales`, `cotizaciones`, `cotizacion_materiales`, `pedidos`, `horarios_atencion`, `cartel_digital` y `rostros_empleado`. Ademas tiene:

- el trigger `trg_cotizacion_materiales_recalcular_total`, que recalcula el total de una cotizacion cuando cambian sus materiales;
- la vista `vw_metricas_conversion_online`, que mide cuantas solicitudes online terminan en pedido;
- el procedimiento `sp_depurar_solicitudes_no_concretadas`, que elimina fisicamente las solicitudes antiguas sin concretar.

---

## 4. Arquitectura general

El proyecto usa una arquitectura simple por responsabilidades:

```text
Cliente HTTP / Scalar / Postman
              |
              v
        ASP.NET Core pipeline
              |
     Middleware, CORS, rate limit
              |
       JWT y autorizacion por rol
              |
           Endpoints
              |
       EF Core o Repository
              |
          SQL Server
```

### Flujo de una peticion

1. La aplicacion recibe una peticion HTTP.
2. El middleware registra metodo, ruta, estado y duracion.
3. CORS revisa el origen permitido y rate limiting controla el volumen.
4. Authentication valida el JWT cuando la ruta es protegida.
5. Authorization revisa si el usuario tiene el rol necesario.
6. El endpoint valida el JSON y sus relaciones.
7. EF Core o el Repository consulta o modifica la base.
8. Se devuelve una respuesta JSON con un codigo HTTP apropiado.

### Decisiones de diseño y justificacion

| Decision | Justificacion |
| --- | --- |
| Minimal API en lugar de controladores MVC | El proyecto tiene endpoints agrupados por dominio y un alcance acotado. Minimal API reduce codigo ceremonial y mantiene visible el recorrido de cada request. |
| Endpoints separados por area del negocio | `AuthApi`, `CatalogoApi`, `SolicitudApi`, `PortalClienteApi`, `CotizacionApi` y `PedidoApi` evitan concentrar toda la logica en `Program.cs` y facilitan mantener cada flujo. |
| Entity Framework Core como ORM | Permite consultar SQL Server desde C# mediante LINQ, mapear relaciones y centralizar la configuracion en el `DbContext`, reduciendo SQL repetido en los endpoints. |
| Modelo y migraciones como unica fuente del esquema | Un script entregado por la profesora sirvio de referencia, pero la base se crea solo con las migraciones. Asi el esquema, los datos iniciales y los objetos programables quedan versionados y una base nueva sale siempre igual. |
| Repository para solicitudes | `ISolicitudRepository` aisla el acceso a solicitudes y demuestra la separacion entre la capa HTTP y persistencia, sin agregar una capa artificial a cada CRUD simple. |
| DTOs/records para requests | Evitan enlazar directamente entidades completas desde el JSON recibido y permiten validar solo los campos que cada operacion debe aceptar. |
| JWT con roles | La API tiene perfiles con responsabilidades diferentes. Un token firmado permite identificar al usuario y `RequireAuthorization` limita operaciones sensibles por rol. |
| Identificadores tomados del token | El empleado que registra una solicitud o el cliente que consulta sus pedidos salen del JWT, no del cuerpo, para que nadie pueda actuar en nombre de otro. |
| Eliminacion logica de solicitudes | Una solicitud eliminada desaparece de los listados, pero sigue contando en la metrica de conversion. |
| Control de concurrencia con `row_version` | Solicitudes y pedidos los edita mas de una persona. Quien guarda con una version vieja recibe `409` y vuelve a cargar los datos, en vez de borrar sin aviso el trabajo del otro. |
| User Secrets y variables de entorno | La clave JWT y los datos del primer administrador son configuracion del entorno, no codigo fuente. Separarlos evita publicar credenciales en GitHub y permite cambiar valores entre desarrollo y produccion. |
| OpenAPI y Scalar | La API necesita ser navegable y demostrable durante la exposicion. OpenAPI describe el contrato y Scalar permite probarlo, incluyendo Bearer JWT. |
| Pruebas con `WebApplicationFactory` y base propia | Verifican el contrato HTTP atravesando la aplicacion real, con escrituras reales, sobre una base temporal creada por las migraciones. |

Estas decisiones siguen las capacidades documentadas por Microsoft para [Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis), [Entity Framework Core](https://learn.microsoft.com/ef/core/), [autenticacion JWT Bearer](https://learn.microsoft.com/aspnet/core/security/authentication/jwt) y [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets).

---

## 5. Estructura de carpetas

### `Program.cs`

Es el punto de entrada y configuracion de la aplicacion. Registra el `DbContext`, JWT, politicas de autorizacion, servicios, CORS, rate limiting, errores y OpenAPI. Tambien define el orden del pipeline y registra cada grupo de endpoints mediante metodos `Map...Api()`.

### `Endpoints/`

Es la capa HTTP. Cada archivo agrupa las rutas de un area del negocio.

- `AuthApi.cs`: login de empleados y clientes, registro de clientes, creacion de empleados y perfil.
- `CatalogoApi.cs`: clientes, productos, categorias, catalogo, empleados, materiales, horarios y cartel digital.
- `SolicitudApi.cs`: CRUD de solicitudes, cambio de estado, eliminacion logica y metrica de conversion.
- `PortalClienteApi.cs`: solicitud online sin cuenta y consultas del cliente con sesion.
- `CotizacionApi.cs`: CRUD de cotizaciones y sus materiales.
- `PedidoApi.cs`: CRUD de pedidos y reglas para crearlos.
- `Concurrencia.cs`: comprobacion de `rowVersion`.
- `RostroApi.cs`: login facial de empleados; activar, consultar y desactivar el rostro; autorizar un equipo.
- `Reautenticacion.cs`: confirmacion de la contraseña en las acciones delicadas.

### `Models/`

Contiene las entidades que representan las tablas y relaciones de la base. `LetrerosHerreradbContext.cs` es el `DbContext`: traduce consultas LINQ de C# a consultas SQL Server y conoce las claves foraneas, indices, columnas, restricciones y datos iniciales.

### `Security/`

`Roles.cs` centraliza los nombres de los roles, las politicas de autorizacion y la lectura del identificador del usuario desde el token.

### `Services/`

Contiene logica reutilizable. `AuthService.cs` hashea contrasenas, verifica credenciales y genera JWT con los claims de usuario, email e identidad del rol. `AdminBootstrapper.cs` crea el primer administrador cuando la tabla de empleados esta vacia. `RostroFacialService.cs` cifra y compara descriptores faciales, y `ConsentimientoFacial.cs` contiene el texto que acepta el empleado.

### `Repositories/`

Contiene el contrato `ISolicitudRepository` y su implementacion `SolicitudRepository`. Esta capa concentra el acceso a solicitudes para que el endpoint no tenga que conocer todos los detalles de las consultas al `DbContext`.

### `Middleware/`

Contiene codigo que se ejecuta alrededor de las peticiones. `RequestLoggingMiddleware` mide el tiempo y registra el resultado de cada solicitud. `ApiExceptionHandler` traduce a `400` o `409` los errores causados por los datos recibidos, como un duplicado o una restriccion incumplida.

### `OpenApi/`

Contiene el transformador que registra el esquema Bearer JWT en el documento OpenAPI para que Scalar pueda documentar y probar la autenticacion.

### `Migrations/`

Contiene las tres migraciones que crean la base completa:

- `InitialCreateV6`, generada desde el modelo: tablas, restricciones, indices y datos iniciales.
- `ObjetosProgramablesV6`, escrita a mano: trigger, vista y procedimiento almacenado.
- `AgregarRostrosEmpleado`, generada desde el modelo: solo crea la tabla `rostros_empleado`.

### `LetrerosHerreraSYNCTIMEapi.Tests/`

Contiene las pruebas automatizadas con xUnit y `WebApplicationFactory`. Las pruebas levantan la aplicacion, crean su propia base y comprueban respuestas HTTP reales.

---

## 6. Base de datos y acceso a datos

La base se crea o actualiza con:

```powershell
dotnet ef database update --project .\LetrerosHerreraSYNCTIMEapi
```

Los cambios de esquema se hacen en el modelo y con `dotnet ef migrations add`, no con scripts sueltos. Una prueba falla si el modelo cambia sin su migracion. Se comprobo que una base creada por las migraciones es equivalente al script de referencia en columnas, claves foraneas, restricciones `CHECK`, claves primarias, collation, datos iniciales y objetos programables. Las diferencias que quedan no cambian el comportamiento: 8 indices adicionales sobre claves foraneas, que agrega EF, y valores por defecto escritos de otra forma.

El `DbContext` se registra por inyeccion de dependencias:

```csharp
builder.Services.AddDbContext<LetrerosHerreradbContext>(options =>
    options.UseSqlServer(connectionString));
```

La cadena de conexion de desarrollo esta en `appsettings.json` y usa autenticacion integrada de Windows, por lo que no contiene credenciales. En otro ambiente se reemplaza con la variable `ConnectionStrings__DefaultConnection`.

El Repository se aplica al flujo de solicitudes. Los otros endpoints usan directamente el `DbContext` porque realizan operaciones simples sobre recursos concretos. Esta es una decision de alcance del proyecto y no se presenta como un Repository completo para todas las tablas.

---

## 7. Endpoints implementados

Todas las rutas de negocio usan el prefijo `/api/v1`. "Personal" significa cualquiera de los roles `dueño`, `administrador`, `diseñador` y `operario`. El detalle de cada ruta esta en `ContratoFrontend.md`.

| Area | Ruta principal | Operaciones | Proteccion |
| --- | --- | --- | --- |
| Autenticacion | `/auth` | login, login de cliente, registro de cliente, crear empleado, perfil | login y registro publicos; crear empleado solo `dueño` y `administrador`, confirmando su contraseña |
| Login facial | `/auth/login-facial`, `/auth/rostro` | iniciar sesion, consultar, activar, desactivar, autorizar equipo | login publico con limite propio; el resto, solo personal; activar y autorizar equipo confirman la contraseña |
| Clientes | `/clientes` | GET, POST, PUT, DELETE, asignar credenciales | lectura del personal; escritura `dueño`, `administrador` y `operario`; DELETE sin `operario`; asignar credenciales confirma la contraseña |
| Categorias y productos | `/categorias`, `/productos` | GET, POST, PUT, DELETE | GET publico, solo lo activo; escritura `dueño` y `administrador` |
| Catalogo | `/catalogo` | GET, POST, PUT, DELETE | GET publico, solo lo visible; escritura `dueño` y `administrador` |
| Solicitudes | `/solicitudes` | GET, POST, PUT, cambio de estado, eliminacion logica | personal; cambio de estado y eliminacion solo `dueño` y `administrador` |
| Solicitud online | `/solicitudes/online` | POST | publico, con limite por IP |
| Portal del cliente | `/mis` | GET solicitudes, GET pedidos, POST solicitudes | rol `cliente`; solo sus propios datos |
| Cotizaciones | `/cotizaciones` | GET, POST, PUT, DELETE | lectura del personal; escritura `dueño`, `administrador` y `diseñador`; DELETE sin `diseñador` |
| Pedidos | `/pedidos` | GET, POST, PUT, DELETE | lectura del personal; escritura `dueño`, `administrador` y `operario`; DELETE sin `operario` |
| Empleados y materiales | `/empleados`, `/materiales` | GET | personal |
| Horarios | `/horarios` | GET | publico |
| Cartel digital | `/cartel-digital` | GET, PUT | GET publico; PUT `dueño` y `administrador` |
| Metricas | `/metricas/conversion-online` | GET | `dueño` y `administrador` |

### Codigos HTTP usados

- `200 OK`: consulta o actualizacion exitosa.
- `201 Created`: recurso creado correctamente.
- `204 No Content`: eliminacion exitosa.
- `400 Bad Request`: datos invalidos.
- `401 Unauthorized`: falta el token o las credenciales no son validas.
- `403 Forbidden`: el token es valido, pero el rol no tiene permiso.
- `404 Not Found`: no existe el recurso, o no es visible para quien lo pide.
- `409 Conflict`: registro duplicado, con dependencias, o modificado por otra persona.
- `413 Payload Too Large`: el cuerpo supera el tope de las rutas de rostro.
- `429 Too Many Requests`: se supero el limite de peticiones.
- `503 Service Unavailable`: el login facial no tiene configurada su clave de cifrado.

---

## 8. Autenticacion y autorizacion

Los empleados inician sesion con email y password; los clientes, con telefono y password. El endpoint busca un usuario activo y verifica la password contra el hash BCrypt almacenado. Si es correcta, `AuthService` genera un JWT firmado.

El token incluye:

- identificador del usuario;
- nombre;
- email;
- rol;
- tipo de usuario, empleado o cliente.

En cada ruta protegida, ASP.NET Core valida firma, issuer, audience y expiracion. Luego las politicas de `RequireAuthorization` comparan el rol del token con los roles permitidos.

Roles utilizados:

- `dueño` y `administrador`: administran empleados, catalogo y cartel digital; cambian el estado de las solicitudes; y son los unicos que eliminan registros.
- `diseñador`: prepara cotizaciones.
- `operario`: trabaja con clientes y pedidos.
- `cliente`: ve y crea solo sus propias solicitudes, y consulta sus pedidos.

Los telefonos todavia no se verifican. Por eso, un telefono ya registrado no se puede reclamar desde la web, porque daria acceso a pedidos ajenos conociendo solo el numero: ese acceso lo activa el personal despues de comprobar la identidad del cliente. Por la misma razon, una solicitud online con el telefono de una cuenta se rechaza: su titular la envia despues de iniciar sesion.

### Login facial de empleados

Es una comodidad opcional junto a la contraseña, no una proteccion fuerte. El navegador calcula un descriptor de 128 numeros con `@vladmandic/face-api` y lo envia al servidor, que lo compara contra el rostro del email indicado y genera el mismo token con `AuthService`. El servidor no puede saber si el descriptor viene de una persona presente: **una foto puede suplantar a alguien**, y asi lo comprobo la prueba de navegador con una imagen fija.

- `dueño` y `administrador` ademas necesitan un equipo autorizado: un token aleatorio de 30 dias, entregado al confirmar la contraseña y guardado en el servidor solo como hash. Frena una foto o un descriptor copiado desde otro equipo, pero no a alguien con una foto en ese equipo, ni el robo del token desde el navegador.
- `diseñador` y `operario` no lo necesitan. Es un riesgo aceptado por decision del responsable del proyecto: con su email y una foto se puede entrar desde cualquier lugar.
- Crear empleados, asignar credenciales, activar el rostro y autorizar un equipo siguen pidiendo la contraseña, en todas las sesiones.
- Los descriptores se guardan cifrados con AES-256-GCM, con la clave fuera del codigo, y se comparan en el servidor. No se guardan fotos. Al desactivar se borran y queda solo la constancia del consentimiento.
- Todo fallo responde el mismo `401`, con un tiempo minimo. Tiene limite por IP con contador propio, y 5 fallos bloquean el rostro de la cuenta 15 minutos sin afectar a la contraseña.

El detalle y lo que falta confirmar con un asesor legal sobre la Ley 21.719 estan en la seccion 7 de `ContratoFrontend.md`.

---

## 9. Seguridad y OWASP

El proyecto documenta y aplica siete categorias del [OWASP API Security Top 10](https://owasp.org/API-Security/editions/2023/en/0x11-t10/). La siguiente tabla explica el problema, la mitigacion y la evidencia concreta dentro del proyecto.

| Riesgo OWASP | Problema que se evita | Como se aborda | Evidencia |
| --- | --- | --- | --- |
| **API1 - Broken Object Level Authorization** | Un usuario podria consultar o modificar recursos de otra persona. | Las rutas `/mis` filtran por el identificador del token, un cliente recibe `403` en las rutas del personal y se validan las relaciones entre registros antes de guardar. | `Endpoints/PortalClienteApi.cs`, `CotizacionApi.cs` y `PedidoApi.cs`. |
| **API2 - Broken Authentication** | Un login debil podria exponer contrasenas o revelar si existe un usuario. | Las contrasenas se almacenan con BCrypt; login invalido responde `401` generico; JWT valida firma, issuer, audience y expiracion. El login facial no prueba que la persona este presente: se limita y se documenta como una comodidad, y las acciones delicadas piden la contraseña. | `Services/AuthService.cs`, `Services/RostroFacialService.cs`, `Endpoints/AuthApi.cs`, `Endpoints/RostroApi.cs` y `Program.cs`. |
| **API3 - Broken Object Property Level Authorization** | El cliente podria enviar propiedades que no deberia controlar, como roles, estados o el empleado responsable del registro; o recibir otras que no deberia ver. | Se reciben records/DTOs de entrada, se validan listas permitidas y rangos, los identificadores de quien actua salen del token y los totales los calcula la base. Los hashes de contrasena nunca se serializan, y las rutas publicas solo devuelven lo necesario. | Records de `AuthApi.cs`, `SolicitudApi.cs`, `CotizacionApi.cs` y `PedidoApi.cs`. |
| **API4 - Unrestricted Resource Consumption** | Un atacante podria abusar del login o saturar la API con muchas solicitudes. | Rate limiting por IP: 10 intentos de login, 10 logins con rostro, 10 confirmaciones de contraseña, 10 envios publicos y 100 solicitudes generales por minuto; el exceso responde `429`. Una solicitud admite hasta 50 detalles, y las rutas de rostro limitan el tamaño del cuerpo. | Configuracion `AddRateLimiter` en `Program.cs`, `SolicitudValidacion` y `RostroApi.cs`. |
| **API5 - Broken Function Level Authorization** | Un usuario autenticado podria ejecutar funciones reservadas a otro perfil. | Politicas y `RequireAuthorization` protegen creacion de empleados, escrituras y eliminaciones segun el rol. El estado de una solicitud solo cambia por la ruta reservada a gestion. | `Security/Roles.cs`, `Endpoints/AuthApi.cs`, `CatalogoApi.cs`, `SolicitudApi.cs`, `CotizacionApi.cs` y `PedidoApi.cs`. |
| **API8 - Security Misconfiguration** | Claves, CORS abierto o errores detallados podrian exponer el sistema. | Secretos fuera de `appsettings`, CORS limitado a origenes configurados, `ProblemDetails`, manejo centralizado de excepciones y HTTPS. | `Program.cs`, `Middleware/ApiExceptionHandler.cs`, `.gitignore` y User Secrets. |
| **API9 - Improper Inventory Management** | Rutas sin versionar o sin documentar pueden quedar olvidadas y ser dificiles de controlar. | Todas las rutas usan `/api/v1`, OpenAPI se expone en Development, y `ContratoFrontend.md` y el archivo `.http` documentan las rutas. | `Endpoints/*.cs`, `OpenApi/BearerSecuritySchemeTransformer.cs`, `ContratoFrontend.md` y `.http`. |

La clasificacion y los nombres de los riesgos se basan en la [lista oficial OWASP API Security Top 10 2023](https://owasp.org/API-Security/editions/2023/en/0x11-t10/). La configuracion de JWT sigue la documentacion de [Microsoft Authentication and Authorization](https://learn.microsoft.com/aspnet/core/security/authentication/). Las medidas no significan que la API sea invulnerable: son controles concretos implementados para reducir los riesgos identificados. Lo que falta antes de publicarla esta en la seccion "Antes de produccion" del `README.md`.

---

## 10. Secretos y GitHub

GitHub almacena el codigo y su historial. Por eso no se deben subir claves JWT ni contrasenas.

En este proyecto:

- `appsettings.json` no contiene credenciales ni la clave JWT.
- User Secrets contiene la configuracion local sensible.
- `.gitignore` excluye `bin/`, `obj/`, `.vs/` y archivos locales de secretos.
- El archivo `.http` usa marcadores en lugar de passwords.
- Las migraciones no crean usuarios. El primer administrador se crea al arrancar con los datos de `Bootstrap:Admin`, y su password se borra de la configuracion despues del primer arranque.

Antes de publicar el repositorio se debe revisar el historial Git. Si alguna clave real fue subida anteriormente, no basta con borrarla del archivo: se debe cambiar la clave y limpiar el historial si corresponde.

---

## 11. Pruebas de integracion

El proyecto de pruebas crea una base temporal con las migraciones, nunca usa la de desarrollo, y la elimina al terminar. Las pruebas verifican, entre otras cosas:

- Las migraciones crean el esquema, los objetos programables y los datos iniciales, con acentos correctos, y el modelo no tiene cambios sin migrar.
- Se recorre el flujo completo solicitud, cotizacion y pedido, con los totales que calcula el trigger.
- Cada rol recibe `401`, `403` o `200` donde corresponde, y un cliente no ve datos ajenos.
- La eliminacion logica oculta la solicitud y conserva la metrica.
- Los datos invalidos, duplicados o con dependencias responden `400` o `409`, no `500`.
- Una edicion con una version vieja responde `409`, tambien si otra persona guarda justo antes, y entre varias escrituras simultaneas solo una gana.
- Lo inactivo u oculto no se muestra ni se puede pedir desde fuera, y el personal puede administrarlo.
- El login facial se activa solo con contraseña y consentimiento; los fallos son indistinguibles; se aplican el umbral, el bloqueo y los limites; dueño y administrador necesitan su equipo; los clientes quedan fuera; y ningun descriptor sale en respuestas o logs. Usan vectores sinteticos: la precision con rostros reales no se ha medido.

Comando:

```powershell
dotnet test .\LetrerosHerreraSYNCTIMEapi.slnx -c Release
```

---

## 12. Documentacion y ejecucion

En Development se dispone de:

- OpenAPI: `/openapi/v1.json`.
- Scalar: `/scalar`.
- Archivo de requests: `LetrerosHerreraSYNCTIMEapi.http`.
- Contrato para el frontend: `ContratoFrontend.md`.

Para ejecutar la API se requieren los User Secrets locales con:

- `Jwt:Key`.
- `Jwt:Issuer`.
- `Jwt:Audience`.
- `Jwt:ExpirationMinutes`.
- `Bootstrap:Admin:*`, solo para crear el primer administrador.
- `RostroFacial:ClaveCifrado`, solo para habilitar el login facial.

No se incluyen los valores en este informe ni en el repositorio.

---

## 13. Alcance y limites actuales

El alcance implementado cubre el flujo principal de clientes, catalogo, solicitudes, cotizaciones y pedidos, ademas del portal del cliente. Quedan fuera de esta version:

- Adaptar el frontend: salvo el login facial, todavia usa el contrato anterior. Los cambios que necesita estan en `ContratoFrontend.md`.
- Cargar el catalogo real: categorias, productos y materiales iniciales son datos de ejemplo del script de referencia.
- Login facial: no hay prueba de vida, y no se ha medido su precision con rostros y camaras reales, por lo que su umbral es provisional. El consentimiento es un borrador pendiente de revision legal. Hoy los empleados solo se desactivan por SQL, asi que sus datos biometricos se borran en el siguiente intento de login facial de cualquier persona.
- Verificar los telefonos por SMS.
- Control de concurrencia en otras tablas: solo `solicitudes` y `pedidos` tienen `row_version`. En cotizaciones, clientes o productos, la ultima edicion prevalece. Las eliminaciones no piden version.
- Avance automatico de estados: crear una cotizacion no cambia por si solo el estado de la solicitud.
- Ruta para `sp_depurar_solicitudes_no_concretadas`: el procedimiento se ejecuta desde la base.
- Aplicar migraciones en produccion: la API solo las aplica sola en Development.

Estas limitaciones se dejan explicitas para que el informe represente el estado real del sistema.

---

## 14. Conclusion

El proyecto aplica el patron de `GamesApi` a un caso de negocio distinto y agrega los elementos exigidos por el Trabajo 1: Minimal API, ORM, CRUD, validacion, versionamiento, OpenAPI, JWT, roles, middleware, Repository, seguridad OWASP y pruebas automatizadas.

La aplicacion queda organizada para que cada integrante pueda explicar que hace cada carpeta y como una peticion avanza desde HTTP hasta SQL Server.

## 15. Referencias

- OWASP Foundation. [OWASP API Security Top 10 - 2023](https://owasp.org/API-Security/editions/2023/en/0x11-t10/).
- Microsoft Learn. [Minimal APIs en ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis).
- Microsoft Learn. [Entity Framework Core](https://learn.microsoft.com/ef/core/).
- Microsoft Learn. [JWT Bearer authentication en ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/jwt).
- Microsoft Learn. [Safe storage of app secrets in development](https://learn.microsoft.com/aspnet/core/security/app-secrets).
- Microsoft Learn. [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests).
