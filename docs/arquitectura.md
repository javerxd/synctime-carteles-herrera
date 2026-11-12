# Arquitectura del backend

El backend se organiza en capas (RNFO10). Cada capa conoce solo a la que tiene debajo.

```text
Presentación   frontend/synctimefront (React)
     │  HTTP + JSON
API            Endpoints/  Api/  Middleware/  OpenApi/      rutas, permisos, formato de respuesta y de error
     │
Negocio        Negocio/  Services/  Security/               reglas, validaciones, estados, cálculos
     │
Datos          Datos/  Repositories/  Models/  Migrations/  repositorios, consultas, modelo y esquema
     │
SQL Server
```

Todo vive en un mismo proyecto, `LetrerosHerreraSYNCTIMEapi`, separado por carpetas y espacios de nombres. No se dividió en varios proyectos para no mover el modelo ni las migraciones ya aplicadas a la base real.

## Reglas, y cómo se hacen cumplir

Las comprueba `ArquitecturaTests` (proyecto `LetrerosHerreraSYNCTIMEapi.UnitTests`) leyendo el código fuente:

| Regla | Prueba |
|---|---|
| Un endpoint nuevo no consulta la base directamente: llama a un servicio de negocio | `UnEndpointNuevo_NoConsultaLaBaseDirectamente` |
| El negocio no conoce HTTP ni Entity Framework | `ElNegocio_NoConoceHttpNiLaBase` |
| Los datos no conocen la capa de API | `LosDatos_NoConocenLaCapaDeApi` |

**Deuda declarada.** Ocho archivos de `Endpoints/` son anteriores a esta arquitectura y todavía usan el `DbContext`. Están en la lista `EndpointsPendientesDeMigrar` de esa prueba, cada uno con la fase que lo migra. La lista solo puede achicarse (`LaListaDePendientes_NoConservaModulosYaMigrados` obliga a quitar lo ya migrado) y debe quedar vacía en la fase 17.

## Patrones de diseño

| Patrón | Dónde | Por qué |
|---|---|---|
| **Repository** | `Datos/Repositorio.cs` (`RepositorioBase<T>`), `Repositories/SolicitudRepository.cs` | Las consultas de cada entidad viven en un solo lugar, y el negocio se puede probar sin base de datos |
| **Unit of Work** | `Datos/Repositorio.cs` (`IUnidadDeTrabajo`) | Varios repositorios comparten un contexto por petición y sus cambios se guardan juntos, en una sola transacción |
| **Inyección de dependencias** | `Program.cs` | Cada pieza recibe lo que necesita; las pruebas pueden sustituir cualquiera |
| **Cadena de responsabilidad** | Middleware de `Program.cs` | Compresión, cabeceras, errores, CORS, autenticación, límites y permisos se aplican en orden, sin que los endpoints los conozcan |

## Cómo se agrega un módulo

1. Declarar sus rutas en `docs/api/openapi.json` (resumen y `x-acceso`). La prueba de contrato falla hasta implementarlas.
2. Escribir las pruebas: unitarias para las reglas de `Negocio/`, de integración para las rutas.
3. `Datos/`: su repositorio, que hereda de `RepositorioBase<T>` y agrega sus consultas, con proyecciones y paginación (`Ordenar`, `PaginarAsync`).
4. `Negocio/`: su servicio, con las reglas y validaciones (`ErroresDeCampos`, `Rut`, `Reglas`). Sin HTTP ni EF.
5. `Endpoints/`: rutas delgadas que llaman al servicio y traducen el resultado con `ErrorApi`.

## Convenciones comunes

- **Errores:** siempre `ErrorApi` (`Api/ErrorApi.cs`). Formato en el esquema `Error` de `docs/api/openapi.json`.
- **Listados:** parámetros `pagina`, `tamano`, `orden` y `buscar` (`ParametrosListado`), respuesta `ResultadoPaginado<T>`. El orden solo acepta campos de una lista escrita en el código.
- **Bases de datos:** las reglas sobre qué base admite operaciones automáticas están en `Datos/BasesDeDatos.cs`. Ver el README del backend.
