# Mitigaciones OWASP API Security

## API1 - Broken Object Level Authorization

Los endpoints exigen autenticacion y roles. Un cliente solo accede a lo suyo mediante las rutas `/api/v1/mis`, que filtran por el identificador del token, y recibe `403` en las rutas del personal. Los identificadores relacionados se validan antes de guardar.

## API2 - Broken Authentication

El login devuelve una respuesta generica `401` tanto para usuario inexistente como para password incorrecto. Las contrasenas se almacenan con BCrypt y los JWT validan issuer, audience, firma y expiracion. Como los telefonos no se verifican, uno ya registrado no se puede reclamar desde la web, y una solicitud anonima con el telefono de una cuenta se rechaza.

## API3 - Broken Object Property Level Authorization

Los endpoints reciben DTOs/records de request en lugar de enlazar directamente entidades completas. Los campos de estado, rol, cantidades y precios se validan mediante listas permitidas y rangos. Los identificadores de quien actua salen del token, no del cuerpo. Los hashes de contrasena no se serializan, y las rutas publicas solo devuelven lo necesario: el formulario entrega un comprobante sin datos del cliente, y el cartel digital no revela que empleado lo modifico.

## API4 - Unrestricted Resource Consumption

Se configuraron limites fijos por IP: 10 intentos de login, 10 envios publicos y 100 requests generales por minuto. El middleware de rate limiting responde `429` al superar el limite. Una solicitud admite hasta 50 detalles.

## API5 - Broken Function Level Authorization

La autorizacion se aplica por endpoint con `RequireAuthorization` y las politicas de `Security/Roles.cs`. Por ejemplo, crear empleados, eliminar entidades y cambiar el estado de una solicitud requieren el rol `dueño` o `administrador`.

## API8 - Security Misconfiguration

La clave JWT no esta en `appsettings.json`: se carga desde User Secrets o variables de entorno. La cadena de conexion de desarrollo si esta, pero usa autenticacion integrada y no contiene credenciales. CORS no usa `AllowAnyOrigin`, sino origenes configurados. Los errores causados por datos se traducen a `400` o `409` sin mostrar detalles de SQL.

## API9 - Improper Inventory Management

La API mantiene versionamiento explicito `/api/v1`, documentacion OpenAPI y rutas separadas por recurso. `ContratoFrontend.md` lista cada ruta, y el archivo `.http` permite repetir las pruebas principales.

## Evidencia ejecutable

La suite de integracion ubicada en `LetrerosHerreraSYNCTIMEapi.Tests` verifica login, JWT, permisos por rol, acceso a datos ajenos, respuestas 401 y 403, y endpoints publicos y protegidos. Se ejecuta sobre una base temporal creada por las migraciones.

## Pendiente

Lo que falta antes de publicar la API esta en la seccion "Antes de produccion" del `README.md`.
