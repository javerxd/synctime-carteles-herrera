# Informe de cierre — Fase 12D: paneles, cuentas, validaciones y catálogo editable

Fecha: 20-11-2026 · Rama: `fase-12d-paneles-y-cuentas` (nace de `fase-12c-portal-publico`) · Estado: **aprobada el 20-11-2026** (incluye el ajuste del zoom de las tarjetas de la vitrina, ver `desviaciones.md`)

Plan: `fase-12d-plan.md`. Migración: **ninguna** (las tablas `producto_medidas_fijas` y `producto_opciones` ya existían). Cambios de permisos: **ninguno**.

## Qué se hizo

- **Menú lateral azul (`#1D4ED8`) en el panel del taller y en la cuenta del cliente.** Una sola estructura compartida (`EstructuraLateral`), con texto blanco (6,7:1), ítem activo con fondo claro, texto blanco y barra blanca a la izquierda, y foco blanco. El **pie queda anclado abajo y siempre visible**, también con el menú largo, con poca altura y en el cajón del teléfono: «Volver a la página principal» (borde y letras blancas) y «Cerrar sesión» (rojo `#B91C1C` con letras blancas, 6,5:1). Solo el menú se desplaza. El panel del taller **no** lleva fondo ni botones animados; la cuenta del cliente **sí** (zona `data-animar` de la 12C).
- **Cuenta del cliente con su propio panel** (`/cuenta/*` con sesión): «Mis solicitudes», «Solicitar cotización» y «Mi cuenta». «Mi cuenta» quedó sin los accesos a solicitudes ni el botón de cerrar sesión (ahora están en el menú). Registro, recuperación y verificación siguen en la estructura pública, sin menú.
- **Inicio del panel en dos columnas** (solo gestión): solicitudes por atender a la izquierda, indicadores del mes a la derecha (apiladas bajo 75 rem, con las solicitudes primero). Diseñador y operario ven solo las solicitudes, a todo el ancho. Las filas son cortas: cliente, producto («Banners y 2 más»), fecha y origen. Para eso la bandeja de la API trae `primerProducto` (campo nuevo y opcional).
- **Etiquetas de origen:** «En línea» en verde (`#DCFCE7`/`#166534`) con icono de globo y «Presencial» en amarillo (`#FEF9C3`/`#854D0E`) con icono de tienda, en la lista, la ficha, el comprobante y «Solicitudes por atender». Nunca solo color.
- **Validación de nombres y apellidos** (una regla, dos capas, mismos casos): solo letras (con tildes, diéresis y ñ), hasta 20 caracteres, sin espacios en los bordes y un solo espacio entre palabras (hasta 6 palabras). Backend: `Negocio/Cuentas/ReglaNombre.cs`; frontend: `utilidades/reglaNombre.js`; la tabla de casos es **un solo archivo** (`CasosNombre.json`) que leen xUnit y Vitest. Aplicada en registro, «Mis datos», ficha de cliente, registro presencial, alta y edición de empleados y formulario de consultas. Los campos limitan la escritura a 20 y los mensajes van junto a cada campo.
- **Empleados:** `/panel/empleados` (lista, alta, ficha) sacada del mantenedor genérico, con entrada «Empleados» en el menú y tarjeta en el inicio, solo para dueño y administrador (el administrador conserva el acceso, sin cambios de permisos). «Eliminar» = desactivar, con diálogo que dice a quién, que no podrá entrar, que su historial se conserva y cómo reactivarlo. No se desactiva a sí mismo ni al último dueño (409, ya existía). `/panel/mantenedores/personal…` redirige a la ruta nueva.
- **Catálogo editable:** en la ficha de cada tipo de letrero, «Medidas y opciones»: límites de ancho y alto (cm en pantalla, mm en la API), medidas fijas y opciones por grupo (agregar, renombrar, quitar). API: `PUT /api/v1/productos/{id}/medidas-y-opciones` (solo gestión, con `If-Match`). Reglas en `Negocio/Mantenedores/ValidacionMedidasYOpciones.cs`: los cuatro límites o ninguno (1 a 50 000 mm), mínimo ≤ máximo, límites **o** medidas fijas (hasta 20, sin repetir), hasta 10 grupos de hasta 30 valores sin repetidos y con al menos uno. Se tocan solo las filas que cambian, y **cada cambio queda en la auditoría** (`producto_medida`, `producto_opcion`). Las solicitudes ya hechas conservan lo elegido; el formulario nuevo valida contra lo vigente.

## Datos que ya existían (solo lectura)

Revisé la base demo (13 clientes, 4 empleados) y la real (0 clientes, 1 empleado): **ningún nombre incumple la regla nueva**. No agregué restricción en la base.

## Pruebas

- **Backend:** unitarias 560 de 560 (nuevas: `ReglaNombreTests`, 38 casos con la tabla compartida; `MedidasYOpcionesTests`); integración 524 de 524 antes de las correcciones de las revisiones y 49 de 49 de las clases tocadas después (nuevas: `Fase12dTests`, 21 casos: permisos por rol, errores por campo sin guardar nada, versión obsoleta, auditoría, orden de grupos, cambio solo de mayúsculas, edición sin cambios, 404 y 400, `primerProducto`, nombres en registro, empleado y consulta, desactivación). Se ajustó 1 prueba antigua que usaba un apellido con dígitos.
- **Fallo a propósito:** con el máximo de nombre en 21 en lugar de 20, 5 pruebas fallan; vuelto a 20, pasan.
- **Vitest:** 768 de 768 (nuevas: `reglaNombreCasosCompartidos`, `fase12dReglas`, `fase12dPantallas`, y contrastes del menú, el botón rojo y las etiquetas en `tokens.test.js`). Una o dos pruebas antiguas (`cotizaciones`, `pedidosFase8`) fallan a veces solo cuando el equipo está bajo carga y pasan solas.
- **Playwright con axe a 360, 768 y 1440 px:** recorrido completo más 11 pruebas nuevas de la 12D (cuenta con cajón abierto y cerrado, pie visible con ventana baja, dos columnas y apilado, origen, editor de medidas, rechazo de nombres), sin fallas graves ni desplazamiento horizontal.
- **Contraste:** el validador del proyecto comprueba azul/blanco, rojo/blanco y las dos etiquetas.
- Build de producción y lint sin errores nuevos.

## Revisiones de ECC y qué se hizo con ellas

- **Seguridad:** sin hallazgos críticos ni altos. Permisos, validación, auditoría y secretos verificados. Corregido: marca combinante suelta aceptada como nombre (regla y tabla compartida), y recorrido de textos enormes (tope de 200 caracteres antes de revisar).
- **C#:** corregidos elementos `null` en las listas (daban 500), duplicados que la base ve distinto de .NET (ahora 400 por campo), el orden de los grupos (ahora se conserva) y `AsSplitQuery`. Agregadas las pruebas que faltaban.
- **React:** corregidos el redondeo silencioso de medidas (un solo decimal), el conflicto de versión (conserva lo escrito, como «Datos»), los errores que reaparecían al cambiar de forma de medir, y `resumenProducto` movido a `reglasBandeja.js`.
- **Accesibilidad:** corregidos `aria-invalid` descartado por `Campo` (ahora se respeta), nombres accesibles que contienen el texto visible, `aria-describedby` hacia el error de la lista, foco al quitar valores, grupos con nombre, foco al contenido tras elegir un enlace del cajón, cierre del cajón al ensanchar la ventana, anillo de foco sin recortar, desplazamiento conjunto con poca altura o zoom, barra en alto contraste, borde del botón rojo y títulos de grupo del menú sin encabezados antes del `h1`.

## Pendientes y límites (no bloquean)

- **Datos antiguos:** un cliente o empleado que tuviera un nombre con guion, apóstrofe o más de 20 letras tendría que corregirlo para guardar cualquier edición (decisión del plan; hoy no hay ninguno). Rechaza «Pérez-Gil» y «O'Higgins», como se te avisó.
- **Sin normalización a NFC:** una tilde escrita como letra + marca se acepta tal cual, sin convertirla (para no cambiar lo escrito).
- El tope de 20 en el campo corta lo pegado sin avisar; el mensaje de largo aparece solo si el texto llega por otra vía.
- «Cerrar sesión» rojo sobre azul tiene casi la misma luminancia: lo identifican su texto blanco y su borde.
- No pude detener el servidor de desarrollo de la API que ya estaba abierto (bloqueaba los binarios): compilé y probé en carpetas aparte.

## Cierre

Documentos actualizados: `tablero.md`, `trazabilidad.md`, `desviaciones.md`, `contrato.md`, `permisos.md`, `openapi.json`. Los comandos de GitHub te los paso; yo no subo nada.
