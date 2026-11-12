# Matriz de permisos por rol

Qué puede hacer cada rol en la API. La columna "Hoy" no es una descripción: la comprueba una prueba automática (`ContratoTests.ElAccesoDeclarado_CoincideConLoQueLaApiPermite`), que llama a cada operación como visitante, cliente, operario, diseñador y administrador y compara el resultado con `docs/api/openapi.json`.

Roles (RNFO08): `dueño`, `administrador`, `diseñador` y `operario` para el personal, y `cliente`.

- **Gestión** = dueño y administrador.
- **Trabajador**, en los requisitos, es quien atiende y produce. Aquí se propone que sean `diseñador` y `operario`; dueño y administrador pueden hacer todo lo que hace un trabajador.

## Estado actual, verificado

| Área | Visitante | Cliente | Operario | Diseñador | Gestión |
|---|---|---|---|---|---|
| Ver catálogo, categorías, productos, horario y estado del local | Sí | Sí | Sí | Sí | Sí |
| Enviar una solicitud en línea | Sí | Sí | Sí | Sí | Sí |
| Registrarse e iniciar sesión | Sí | — | — | — | — |
| Ver sus propias solicitudes y pedidos (`/mis/…`) | No | Sí | No | No | No |
| Listar y ver clientes, solicitudes, cotizaciones, pedidos, materiales y empleados | No | No | Sí | Sí | Sí |
| Registrar y modificar clientes; asignarles contraseña | No | No | Sí | No | Sí |
| Registrar y modificar solicitudes | No | No | Sí | Sí | Sí |
| Cambiar el estado de una solicitud (incluye rechazarla) o eliminarla | No | No | No | No | Sí |
| Crear y modificar cotizaciones | No | No | No | Sí | Sí |
| Crear y modificar pedidos | No | No | Sí | No | Sí |
| Eliminar clientes, cotizaciones y pedidos | No | No | No | No | Sí |
| Mantener categorías, productos y publicaciones | No | No | No | No | Sí |
| Cambiar el estado del local | No | No | No | No | Sí |
| Crear empleados | No | No | No | No | Sí |
| Ver la métrica de conversión | No | No | No | No | Sí |
| Activar y desactivar su propio acceso con rostro | No | No | Sí | Sí | Sí |

## Diferencias con los requisitos

Lo que hoy permite la API y lo que piden los requisitos no coincide en estos puntos. Cada uno se corrige en la fase indicada.

| # | Requisito | Hoy | Debe quedar | Fase |
|---|---|---|---|---|
| 1 | RNFO03, RFU16: solo el personal cambia el estado del local | Solo gestión | Todo el personal | 9 |
| 2 | RFU11: el trabajador consulta, modifica, **rechaza y elimina** solicitudes | Rechazar y eliminar es solo de gestión | Todo el personal | 7 |
| 3 | RFU12: el trabajador cotiza | El operario no puede | Todo el personal | 8 |
| 4 | RFU13, RFU14: el trabajador confirma el pago, crea el pedido y registra su avance | El diseñador no puede | Todo el personal | 8 |
| 5 | Decisión del 12-11-2026: el rostro es solo para dueño y administrador | Lo puede activar todo el personal | Solo gestión | 10 |
| 6 | `POST /auth/rostro/equipo` | La ruta admite a todo el personal y rechaza a los demás roles con un 400 de su lógica interna | Rechazo con 403 en la propia ruta | 10 |

## Tres decisiones por confirmar

Las propuestas siguientes son las que aplicaré si no indicas otra cosa. Cambiarlas después es posible, pero conviene decidirlas antes de las fases de negocio.

1. **¿Dueño y administrador pueden lo mismo?**
   Propuesta: **sí, exactamente lo mismo**, como hoy. Los requisitos hablan del "dueño" para mantenedores, empleados, reportes y auditoría, y RFS26 trata a ambos roles como "dueño".
   Alternativa: reservar solo al `dueño` la gestión de empleados y roles, la auditoría y la desactivación de rostros ajenos.

2. **¿Diseñador y operario pueden lo mismo?**
   Propuesta: **sí**. Los requisitos nombran un único "trabajador" para solicitudes, cotizaciones, pagos, pedidos y entregas (RFU10 a RFU16). Hoy están repartidos: el diseñador cotiza y el operario maneja clientes y pedidos.
   Alternativa: mantener el reparto actual, lo que dejaría sin cumplir RFU12 y RFU13 para uno de los dos roles.

3. **¿Quién puede eliminar registros?**
   Propuesta: el trabajador puede eliminar **solicitudes** (lo pide RFU11), siempre como borrado lógico y con registro en la auditoría. Eliminar clientes, cotizaciones y pedidos sigue siendo solo de gestión, y también pasa a ser borrado lógico (fases 7 y 8).
