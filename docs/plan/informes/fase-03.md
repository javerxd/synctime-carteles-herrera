# Informe de cierre — Fase 3: estructura de la aplicación web y pantallas de la revisión

Fecha: 13-11-2026 · Rama: `fase-03-estructura-y-pantallas` (nace de `fase-02-sistema-diseno`) · Estado: **en revisión, esperando el visto bueno del usuario**

Esta fase reemplaza las páginas antiguas del frontend por las dos estructuras nuevas (sitio público y panel del taller) y deja navegables las pantallas de la revisión de avance del lunes, alimentadas por la base demo. La lógica que les falta llega en su fase, y cada pantalla lo dice.

## Cómo verlo

En una terminal, la API sobre la base demo (el perfil `https` usa `LetreriaLosHerreraDB_Demo`):

```
cd backend/Proyecto-Carteles-herrera-synctime-main
dotnet run --project .\LetrerosHerreraSYNCTIMEapi --launch-profile https
```

En otra, el frontend:

```
cd frontend/synctimefront
npm run dev
```

Abrir `http://localhost:3000`. El panel está en `/panel`: se entra desde «Acceso del personal» (pie de la portada) con un usuario demo (`dueno@letreria.test`, `admin@letreria.test`, `disenador@letreria.test` u `operario@letreria.test`) y la contraseña que definiste en `Demo:Password`. Si la base demo no tiene pedidos todavía, repetir `sembrar-demo` los agrega (ver el README del backend).

## Qué se hizo

### Estructura

| Tema | Resultado | Dónde |
|---|---|---|
| Sitio público | Barra de una fila con el logotipo, «Trabajos», «Horario», «Cómo llegar», «Ingresar» y «Solicitar cotización»; queda fija arriba (salvo con poca altura, por ejemplo con zoom) y bajo 896 px los enlaces pasan a un menú desplegable. Pie con dirección, mapa y enlaces legales. Enlace para saltar al contenido | `src/estructuras/EstructuraPublica.jsx` |
| Panel del taller | Menú lateral agrupado (Taller, Gestión, Cuenta), filtrado por rol, con la fase de cada sección pendiente. En el teléfono es un cajón (`<dialog>`) que devuelve el foco al botón «Menú». Pie con el usuario, su rol, «Salir» y enlaces legales | `src/estructuras/EstructuraPanel.jsx`, `navegacion.js` |
| Páginas de error | 404, 403 («No tienes permiso para ver esta página») y error general con «Reintentar» y «Recargar». Un error en una página no tumba la estructura | `src/paginas/errores/`, `src/componentes/ErrorBoundary.jsx` |
| Cliente HTTP | `fetch` con el formato único de error de la API traducido a mensajes en español, «sin conexión» propio y lecturas iguales en curso compartidas. Un 401 con sesión cierra la sesión según su tipo; un 401 de otra sesión anterior no cierra la actual | `src/api/cliente.js` |
| Sesión | Dos tipos de usuario (personal y cliente), guardada en `sessionStorage`, que vence con el token. Guardas de ruta con los roles reales (`dueño`, `administrador`, `diseñador`, `operario`) | `src/sesion/` |
| Rutas | Cada página se carga aparte; el reconocimiento facial solo al usarlo. `/login` y `/dashboard` redirigen a las rutas nuevas | `src/App.jsx` |
| Utilidades | `formatearPesos`, `formatearFecha` (dd-mm-aaaa, sin correrse un día por la zona horaria), `validarRut`, `formatearRut`, `useDebounce` (300 ms) y `useConsulta`, que al recargar conserva los datos que ya se ven | `src/utilidades/` |
| Avisos | Mensajes de éxito y error anunciados por el lector de pantalla desde regiones que existen siempre | `src/avisos/Avisos.jsx` |
| Logotipo | Versión en tinta del original gris, en la barra, el panel, el acceso y el ícono de la pestaña | `public/marca/`, originales en `docs/marca/originales/` |
| CSP | Solo fuentes, estilos, imágenes y scripts propios; nada de Google Fonts | `index.html` |
| Limpieza (RNFO16) | Se eliminaron `axios`, `prop-types`, el `index.css` oscuro, `App.css`, las páginas, servicios, recursos y pruebas del contrato antiguo (también la prueba de navegador del flujo antiguo de solicitudes, como anunciaba el informe de la fase 0). `npm audit --omit=dev`: sin vulnerabilidades | — |
| Acceso del personal | El de la fase 1 (contraseña y rostro), con el sistema de diseño y la voz «tú». Tras «Salir», quien entre después parte en el inicio del panel | `src/paginas/publicas/IngresoPersonal.jsx`, `src/paginas/panel/Perfil.jsx` |

### Pantallas del lunes

| Pantalla | Qué tiene | Qué falta y en qué fase |
|---|---|---|
| **Menú general** | Público y del panel, filtrado por rol; inicio del panel con saludo, pedidos en proceso, próximos 7 días, atrasados, saldo por cobrar (solo gestión), estado del cartel y accesos | — |
| **Portada «Mostrador»** (RFS01, S02) | Titular, el estado del taller como un mostrador (cartel, mensaje, regreso, hoy, dirección, cómo llegar), vitrina con filtro por categoría y «Cotizar uno similar», horario semanal con «Hoy» y ubicación | Fotos reales (fase 5); WhatsApp aparece solo cuando me des el número |
| **Modal de solicitud** (RFS04, S25) | Tipo, medidas, cantidad, detalle, nombre, celular y correo; casilla de privacidad desmarcada; el primer error recibe el foco. Al enviar muestra «Para esta solicitud es preferible contactarse directamente con el local» | El envío y la regla de cuándo mostrar el aviso (fase 7) |
| **Estado del local** (RFS02, RFU16) | Cartel verde o rojo con texto, mensaje, regreso y última actualización; «Dar vuelta el cartel» para dueño y administrador; horario semanal; **código QR** del portal, descargable en PNG o SVG, que se imprime solo, sin el resto de la página | Editar el horario y registrar quién cambió el cartel (fase 9) |
| **Pedidos** (RFS06, RFU36, E27, S24) | Estado del pedido y estado de pago por separado, monto final, abonado y saldo; filtros por estado, pago, año, mes y cliente (nombre sin tildes, RUT o teléfono) y «Por cobrar», con resumen anunciado; ficha del pedido; tabla que pasa a fichas cuando el espacio es angosto | Filtros en la API y acciones sobre el pedido (fase 8) |
| **Pedido presencial directo** (RFU35) | Cliente por RUT o teléfono, tipo, medidas, glosa, costos, instalación, entrega y abono con su medio; valida RUT, montos enteros y que el abono no supere el monto final | Guardarlo en una transacción (fase 8) |
| **Indicadores** (RFS09) | Seis KPI con su fórmula a la vista y cuatro gráficos de barras de una serie con su tabla de datos. Rotulados «Cálculo provisional en el navegador» | Cálculo en la API con filtros (fase 12) |
| **Reportes** (RFS10) | Pestañas Operativo, Gerencial y Parametrizable (filtros por fechas, estado y pago) con vista previa | Exportar a Excel, PDF y CSV, e imprimir (fase 13) |

### Semilla demo ampliada

`sembrar-demo` ahora también carga 12 pedidos repartidos en cuatro meses, cada uno con su solicitud y su cotización aprobada, en todos los estados del pedido y de pago (abonados, entregados sin pagar y anulados incluidos), con ambos orígenes (cotización y pedido directo) y con abonos en el historial. Se ejecutó sobre la base demo: 12 pedidos y 9 abonos. Se niega a escribir en una base que no sea demo o de pruebas.

## Verificación

| Comprobación | Resultado |
|---|---|
| Pruebas del frontend (Vitest) | 164 de 164 en 15 archivos. Nuevas: utilidades y `useConsulta`, cliente HTTP, sesión y reglas de los filtros de pedidos |
| Pruebas en navegador real (Playwright, Chromium) | 31 de 31 en las pantallas y 14 de 14 en la galería |
| Recorrido a 360, 768 y 1440 px | Portada, «Ingresar», acceso del personal, inicio del panel, pedidos, estado del local, indicadores y reportes: título visible, sin desplazamiento horizontal y sin errores en la consola |
| axe (WCAG 2.0, 2.1 y 2.2, A y AA) | Sin errores críticos ni serios en esas 8 pantallas a los 3 anchos |
| Flujos probados | «Por cobrar» y ficha del pedido; operario sin «Gestión» y con 403 en indicadores; sin sesión, el panel lleva al acceso; descarga del QR; cartel con foco en el error y foco conservado al guardar; «Horario» desde otra página baja hasta la sección; modal con la privacidad exigida y el aviso S25 |
| Pruebas del backend | 96 unitarias y 187 de integración (incluye la semilla: estados, orígenes, meses y `MontoAbonado` igual a la suma de los abonos, y que repetirla no duplica) |
| Peso (presupuesto: 150 KB) | JavaScript inicial de 101,8 KB comprimido. El reconocimiento facial (325 KB) se carga solo al usarlo |
| Capturas revisadas | Portada con la API demo real a 390 y 1440 px; pedidos, indicadores y portada a 768 px |
| oxlint | Sin errores. Quedan 4 avisos de «fast refresh» por archivos que exportan un componente y una función; no afectan al sitio |

## Revisión de ECC

**`security-reviewer`:** nada grave. Se aplicaron sus tres puntos menores: la sesión exige que el token tenga vencimiento; la vitrina solo muestra imágenes del propio sitio o por `https`; la semilla verifica la base también al cargar pedidos.

**`react-reviewer`:** sin hallazgos críticos. Lo corregido:

| Hallazgo | Corrección |
|---|---|
| Al cerrar el modal de solicitud, el pedido directo o la ficha, su contenido desaparecía antes de la animación de salida | El diálogo decide cuándo desmontarlo; la ficha conserva el último pedido mientras se cierra |
| En «Estado del local», guardar el cartel recargaba la tarjeta entera y el foco se perdía | Recargar conserva los datos que ya se ven (`recargando`); el error de validación enfoca el mensaje y el error de la API se muestra aparte |
| Una respuesta 401 tardía de una sesión anterior podía cerrar la sesión nueva | Solo cierra si el token sigue siendo el de la sesión actual |
| La confirmación de «Desactivar y borrar mis datos» enfocaba la acción destructiva | Enfoca «Cancelar» |
| La conversión decía «Sin datos» mientras cargaba o si fallaba | «—» mientras carga y «No disponible» si falla |
| Indicadores recalculaba todo en cada render | Cálculo memorizado |
| «Operativo» y «Parametrizable» mostraban la misma tabla con los mismos filtros | El operativo muestra todo; el parametrizable, lo filtrado |
| Los enlaces «Trabajos», «Horario» y «Cómo llegar» recargaban la página y no bajaban a la sección | Navegan sin recargar y bajan a la sección cuando ya existe |
| Tras «Salir», el siguiente usuario volvía a la pantalla del anterior | Tras salir se parte en el inicio |

Al agregar el ancho de 768 px a las pruebas apareció un desborde en Pedidos: la tabla ensanchaba la página. Ahora la página nunca se estira con su contenido, y la tabla adaptable (fase 2) decide entre tabla y fichas según su propio espacio y no según la ventana, porque en el panel el menú lateral se lleva parte del ancho. Si en modo tabla las columnas no caben, se desplaza dentro de su marco. Quedó en `DESIGN.md`.

Quedan anotados para la fase 15, sin cambio de código:
- cada pantalla vuelve a pedir sus datos al abrirse (no hay caché entre pantallas);
- cerrar el pedido directo con Esc descarta lo escrito (en la fase 8 conviene pedir confirmación);
- los avisos se cierran a los 6 segundos aunque el puntero esté encima. El lector de pantalla ya los anunció;
- las consultas no se cancelan al salir de una pantalla. Su respuesta se descarta;
- el error de Mi perfil no recibe el foco.

## Decisiones que conviene que revises

Están en `desviaciones.md`, salvo la portada, que elegiste tú:

- **Portada «Mostrador»:** la composición que elegiste. El estado del taller va arriba, junto al titular.
- **Logotipo:** usé la versión gris pasada a tinta `#0F172A` (sin redibujarla). Combina con el modo claro y no compite con el ámbar de las acciones. La roja tenía el anillo cortado en los bordes de la imagen.
- **Quién da vuelta el cartel:** hoy la API solo lo permite a dueño y administrador, y la pantalla respeta ese permiso. El requisito RFU16 dice «trabajador»: **decídelo para la fase 9**.
- **Fotos de la vitrina:** la semilla apunta a fotos de ejemplo que no existen. Se muestra «Foto del trabajo pendiente» hasta la fase 5.
- **Indicadores y reportes:** se calculan en el navegador con los pedidos demo y lo dicen en pantalla. El cálculo real es de las fases 12 y 13.
- **Modal de solicitud:** no envía todavía. Al enviar siempre muestra el aviso de contacto directo (S25).
- **Ingreso de clientes:** «Ingresar» ofrece las dos puertas. La del cliente indica que llega en la fase 6.

## Lo que no se verificó

- **Ingreso con contraseña sobre la base demo, con la API real:** no lo hice porque no escribo contraseñas por ti. Lo cubren las pruebas de integración del backend (ingreso real contra una base temporal) y las del frontend (con la API simulada). El recorrido de punta a punta lo haces tú.
- **Pantallas del panel con la API real:** las probé con la API simulada en Playwright. Con la API real solo probé la portada, que no requiere sesión.
- **Tu ingreso con rostro contra la base real:** lo pruebas tú, como dice el plan. Ojo: la base real sigue con 3 migraciones. El acceso con rostro no toca los pedidos, pero las pantallas con pedidos (inicio del panel, pedidos, indicadores y reportes) fallarán contra la base real hasta aplicar las 2 migraciones del ajuste. No las aplico sin que me lo digas.
- **Navegadores:** solo Chromium. Firefox y Safari quedan para la fase 15.
- **Lector de pantalla:** NVDA no se probó (fase 15).
- **Teléfonos reales:** no se probaron; se emularon los anchos.

## Cambios en archivos

- **Frontend:** `src/api/`, `src/sesion/`, `src/estructuras/`, `src/avisos/`, `src/configuracion/`, `src/utilidades/`, `src/paginas/`, `src/App.jsx`, `index.html`, `public/marca/`, `src/test/` y `e2e/pantallas.spec.js`. En la tabla adaptable de la fase 2 cambió el criterio de ancho. `package.json`: sale `axios` y `prop-types`, entra `uqr`. Se eliminaron las páginas, servicios, recursos y pruebas antiguas.
- **Backend:** `Datos/SemillaDemo.cs` (pedidos y abonos demo) y su prueba en `CimientosTests.cs`. README: qué carga `sembrar-demo`.
- **Documentos:** `docs/licencias.md`, `trazabilidad.md`, `tablero.md`, `desviaciones.md`, `DESIGN.md`, `docs/marca/originales/` y este informe.

## Lo que necesito de ti

1. Recorrer las pantallas antes del lunes, entrando con un usuario demo, y probar tu ingreso con rostro.
2. Tu visto bueno a esta fase, o lo que quieras cambiar.
3. El número de WhatsApp, el teléfono y el correo del taller, si quieres que aparezcan en la portada (hoy están vacíos en `src/configuracion/taller.js` y su botón no se muestra).
4. Para la fase 9: si diseñador y operario también pueden dar vuelta el cartel.
5. Cuándo aplicar a la base real las 2 migraciones del ajuste (`SepararEstadoPedidoYPago` e `HistorialDePagos`). No las aplico sin tu orden.
