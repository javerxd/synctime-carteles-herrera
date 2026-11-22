# Plan de la fase 12D — Paneles, cuentas, validaciones y catálogo editable

Fecha: 20-11-2026 · Rama: `fase-12d-paneles-y-cuentas` (nace de `fase-12c-portal-publico`) · Estado: **esperando tu aprobación**; construcción sin iniciar

Origen: tu lista de cambios del 20-11-2026 y tus respuestas a las decisiones 1, 2, 3 y 6. Esta fase toca el **panel del taller** (fase 3 y siguientes), crea el **panel lateral del cliente**, endurece la **validación de nombres** en todo el sistema y deja el **catálogo editable** desde el mantenedor. Depende de la 12C (naranja, fondo y botones). No cambia ningún permiso.

Requisitos que toca: RFS01 y RFS07 (catálogo y mantenedores), RFS04 (solicitudes), RFS12 y RFU34 (cuenta del cliente), RFS26 (empleados y roles), RNFE (validación de entradas), RNFO08 (roles), y las reglas de diseño y accesibilidad.

## 1. Panel del taller: menú lateral

- **Color:** el menú lateral pasa a un **azul de la empresa**. Propongo `#1D4ED8`, con texto blanco (≈ 6,7:1) y el ítem activo con fondo blanco translúcido, texto blanco y una barra blanca a la izquierda. No uso el naranja sobre el azul: su contraste entre sí no pasa. Lo mido con el validador antes de fijarlo; `DESIGN.md` y los tokens se actualizan.
- **Pie anclado abajo**, siempre visible (también con el menú largo y en el cajón del teléfono):
  - encima, **«Volver a la página principal»** (borde blanco, letras blancas), que lleva a `/`;
  - debajo, **«Cerrar sesión»** en rojo con letras blancas (`#B91C1C` con blanco ≈ 6,5:1), con confirmación del estado de salida como hoy.
- **Sin animaciones de botón** en este panel (tu excepción de la 12C). El foco por teclado se mantiene.
- **Fondo:** el área de contenido del panel sigue sin imagen de fondo, para no cargar al personal que lo usa todo el día.

## 2. Panel lateral del cliente (nuevo)

Hoy el cliente tiene páginas sueltas bajo `/cuenta` y la pantalla «Mi cuenta» mezcla sus datos con los accesos a solicitudes. Pasa a tener su propia estructura, igual a la del taller pero más sencilla.

- **Estructura nueva para `/cuenta/*`** (con sesión de cliente): menú lateral azul, el mismo color y pie que el del taller (sección 1, sin el botón de panel).
- **Opciones del menú:** **Mis solicitudes** (`/cuenta/solicitudes`), **Solicitar cotización** (`/cuenta/solicitud`) y **Mi cuenta** (`/cuenta`).
- **«Cerrar sesión»** anclado abajo, rojo con letras blancas, y encima **«Volver a la página principal»**.
- **«Mi cuenta» se limpia:** se quitan de esa pantalla los accesos a «Mis solicitudes» y «Solicitar una cotización», que ahora viven en el menú. Quedan sus datos, correo, contraseña, correos que recibe, descarga de datos y baja de la cuenta.
- **Teléfono:** el menú es un cajón que se abre con un botón, con foco atrapado y cierre con Esc, como el del taller.
- **Páginas que no llevan menú:** registro, recuperación y verificación (el cliente aún no tiene sesión). Siguen en la estructura pública.
- **Fondo y animaciones:** esta zona **sí** lleva el fondo de baja opacidad y los botones animados de la 12C.

## 3. Inicio del panel: dos columnas

- Una sola sección, con **«Solicitudes por atender» a la izquierda** (filas más cortas: cliente, producto, fecha y estado, sin líneas largas) e **«Indicadores del mes» a la derecha**.
- **Teléfono:** las dos columnas se apilan, con **solicitudes primero**.
- **Por rol:** los indicadores son de gestión. Diseñador y operario ven «Solicitudes por atender» a todo el ancho, como hoy.
- Se mantienen los estados de carga, vacío y error por bloque; si un bloque falla, el otro sigue.

## 4. Etiquetas de «Origen»

- **«En línea» en verde y «Presencial» en amarillo**, en la lista de solicitudes, su ficha y «Solicitudes por atender». Fondos claros con texto oscuro (verde `#DCFCE7` / `#166534`, amarillo `#FEF9C3` / `#854D0E`), ambos ≥ 4,5:1.
- **Nunca solo color:** cada etiqueta lleva su texto y, además, un icono distinto (Wifi o Globe para «En línea», Store para «Presencial»), para quien no distingue colores.

## 5. Validación de nombres y apellidos (tu decisión 1)

**Regla:** solo letras (incluidas tildes, diéresis y ñ), **hasta 20 caracteres**, **sin espacios al inicio ni al final**, y **como máximo 5 espacios entre palabras**, siempre uno solo entre dos palabras (no se aceptan dos espacios seguidos). Se rechazan números, guiones, apóstrofos y símbolos.

- **Dónde se aplica:** registro del cliente, «Mis datos», ficha de cliente y registro presencial en el panel, alta y edición de empleados, formulario de consultas y todo otro campo de nombre o apellido que aparezca al revisar. Lo busco con una revisión de todos los formularios, no solo los que recuerdo.
- **Dos capas:** una función de negocio única en el backend (`Negocio/Cuentas`, hoy el límite es 80 y no revisa los caracteres) y una regla única en el frontend. Mismos casos de prueba en ambos lados, para que no se contradigan.
- **Mensajes claros** por campo («Usa solo letras, hasta 20 caracteres.»), asociados al campo y anunciados al lector de pantalla. El campo limita la escritura a 20 y no cambia lo que el usuario escribió sin avisarle.
- **Datos que ya existen:** antes de endurecer, reviso la demo y, en solo lectura, la base real: si algún nombre actual tiene más de 20 caracteres o símbolos, te muestro la lista. **No** agrego una restricción en la base de datos (rompería registros existentes); la validación se aplica al crear y al editar, y editar un registro antiguo pide corregir ese campo. Queda en `desviaciones.md`.
- **Lo que esto rechaza, para que lo sepas:** «Pérez-Gil», «O'Higgins» y nombres de más de 20 letras. «María José» y «De la Fuente» sí pasan.

## 6. Administrar empleados (tu decisión 3)

- **Entrada para dueño y administrador**, de dos formas: una opción **«Empleados»** en el menú lateral y una tarjeta en el inicio del panel. Diseñador y operario no la ven.
- **La pantalla** reutiliza lo que ya hizo la fase 9 (dar de alta, editar datos, cambiar el rol, desactivar) en una ruta propia, `/panel/empleados`, sacada del mantenedor genérico. Alta y cambios de correo o rol siguen pidiendo la contraseña de quien los hace.
- **«Eliminar» = desactivar** (`activo = false`) con **diálogo de confirmación**: dice a quién se desactiva, qué pasa (no podrá entrar, su historial se conserva) y cómo reactivarlo. Se evita desactivarse a uno mismo y dejar el taller sin dueño activo.
- **Permisos sin cambios (decisión del 20-11-2026):** el administrador conserva el acceso a `/empleados`, como hoy y como se confirmó en la fase 9. No cambian `permisos.md` ni la API.

## 7. Catálogo editable: medidas y opciones de un producto (tu decisión 6)

Cierra el pendiente de la 12B. Hoy los límites de medidas y las opciones de cada producto solo se cambian con una migración.

- **En el mantenedor de productos** (solo gestión): editar los **límites de ancho y alto** (en centímetros en pantalla, milímetros en la base), las **medidas fijas** (agregar y quitar) y las **opciones** por grupo (agregar, renombrar y quitar valores).
- **Reglas en el backend** (`Negocio`): mínimo ≤ máximo, valores positivos y dentro del tope que ya usa el catálogo, medidas fijas sin repetir, un grupo con al menos un valor, sin valores repetidos, textos con largo limitado. Un producto con medidas fijas no tiene rangos, y al revés. Errores `400` por campo.
- **Sin migración:** las tablas `producto_medidas_fijas` y `producto_opciones` ya existen. Cada cambio queda en la auditoría.
- **Las solicitudes ya hechas** conservan lo que el cliente eligió, aunque después se quite esa opción; el formulario nuevo solo ofrece lo vigente.

## 8. Pruebas primero y revisiones

- **Backend (xUnit contra SQL Server):** validación de nombres con casos límite (20 y 21 letras, 5 y 6 espacios, espacio al inicio o al final, doble espacio, tildes, ñ, guion, dígitos, nulo y vacío); `/empleados` con `403` al diseñador y al operario, y `200` a dueño y administrador; no desactivarse a sí mismo ni al último dueño; reglas del catálogo (rangos invertidos, medidas repetidas, grupo vacío); auditoría de cada cambio. **Fallos a propósito** sobre la regla de nombres y los permisos, para comprobar que las pruebas los detectan.
- **Vitest:** la misma tabla de casos del nombre; menú por rol (dueño y administrador ven «Empleados»; diseñador y operario no); pie del panel y de la cuenta; etiquetas de origen; columnas del inicio por rol; confirmación de desactivar; editor de medidas y opciones.
- **Playwright con axe a 360, 768 y 1440 px:** inicio del panel, menú del taller y del cliente (abierto y cerrado en el teléfono), «Mi cuenta», empleados, mantenedor de productos; sin desplazamiento horizontal; el pie anclado se ve sin desplazarse.
- **Contraste:** el validador del proyecto comprueba azul con blanco, rojo con blanco y las dos etiquetas.
- **ECC:** `csharp-reviewer`, `security-reviewer` (permisos, validación de entradas), `react-reviewer`, `a11y-architect` (cajón, foco, pie anclado).
- **Verificación en la app:** con la sesión de dueño y de cliente, a las tres anchuras, la consola sin errores. Con la de administrador compruebo que sigue viendo «Empleados». Sin contraseñas en el repositorio.
- **Cierre:** informe `informes/fase-12d.md`, tablero, trazabilidad, `permisos.md`, `contrato.md`, `openapi.json`, diccionario si cambia algo, `desviaciones.md` y commit en la rama. Los comandos de GitHub te los paso; yo no subo nada.

## Decisiones y supuestos que te dejo a la vista

1. **Azul `#1D4ED8` para ambos menús** (sección 1). Dime si quieres otro tono o el de tu logotipo.
2. **Empleados sigue siendo de gestión** (dueño y administrador), sin cambios de permisos (sección 6).
3. **Un solo espacio entre palabras** en nombres y apellidos (sección 5): tu regla dice «máximo 5 espacios», lo leo como cinco espacios sueltos entre seis palabras, no como espacios dobles.
4. **No se agrega restricción en la base de datos** para los nombres (sección 5), para no romper registros existentes.
5. **Las páginas de registro y recuperación siguen en la estructura pública**, sin menú lateral (sección 2).
