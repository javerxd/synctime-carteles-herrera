# SyncTime Frontend — Letreros Herrera
**Evaluación 2 — UT2: Consumo de APIs RESTful desde aplicaciones frontend y seguridad**  
**Asignatura:** Lenguaje Programación Web II  
**Docente:** Enso Guidotti  
**Tecnologías:** React 19, Vite, React Router 7, Axios, Vitest, Testing Library  

---

## 📋 Descripción del Proyecto
SyncTime Frontend es la interfaz de gestión y consumo de servicios para la API RESTful de **Letreros Herrera**, desarrollada en ASP.NET Core (Evaluación 1).  
Permite que usuarios autenticados gestionen el ciclo de vida de **Solicitudes** (recurso principal del flujo comercial), además de interactuar con **Clientes** y **Productos/Servicios** (recursos relacionados), aplicando controles estrictos de seguridad de extremo a extremo, autorización basada en roles (RBAC), accesibilidad **WCAG 2.2 nivel AA** y diseño mobile-first responsivo.

---

## 🚀 Requisitos Previos e Instalación

### Requisitos del Sistema
- Node.js versión 20 LTS o superior
- npm versión 10 o superior
- Backend API RESTful de Letreros Herrera en ejecución (HTTPS de desarrollo: `https://localhost:7112`)

### Instalación de Dependencias
```bash
# Clonar o situarse en el directorio del frontend
cd synctimefront

# Instalar dependencias
npm install
```

### Configuración de Variables de Entorno
Cree un archivo `.env` en la raíz de `synctimefront/` basándose en `.env.example`:
```bash
cp .env.example .env
```
Contenido de `.env`:
```ini
# URL base del backend RESTful (sin barra final)
VITE_API_URL=https://localhost:7112
```
> **Importante para desarrollo (fase 6):** deja `VITE_API_URL` vacía. Así la API se llama por el proxy de Vite (`/api`), en el mismo origen, y el navegador envía la cookie de renovación. Con una URL de otro origen, como la de `.env.example`, la cookie no viaja (`credentials: 'same-origin'` en `src/api/cliente.js`) y la sesión dura solo el token de 15 minutos. Es un pendiente de la fase 6 (ver `docs/plan/desviaciones.md`).
>
> **Nota de Seguridad:** Ningún secreto, credencial de base de datos ni clave de firma JWT se almacena en variables `VITE_*`. Estas variables solo contienen la URL pública del backend.

---

## 💻 Ejecución y Scripts Disponibles

```bash
# Iniciar servidor de desarrollo con proxy a la API (puerto 3000)
npm run dev

# Ejecutar suite de pruebas unitarias y de componentes (Vitest)
npm test

# Ejecutar suite de pruebas end-to-end completas (Playwright)
npm run test:e2e

# Ejecutar linter estático
npm run lint

# Compilar para producción
npm run build

# Previsualizar el build de producción
npm run preview
```

---

## 🏗️ Arquitectura del Proyecto

El frontend sigue una estructura modular orientada a capas y responsabilidades bien delimitadas:

```
synctimefront/
├── .env.example              # Plantilla de variables de entorno
├── index.html                # Plantilla HTML con CSP, meta tags y tipografía Inter
├── vite.config.js            # Configuración de Vite, proxy inverso /api y Vitest
├── src/
│   ├── api/                  # Capa de consumo HTTP centralizada
│   │   ├── httpClient.js     # Instancia Axios con interceptores JWT y normalización de errores
│   │   ├── authService.js    # Consumo de autenticación (POST /auth/login)
│   │   ├── solicitudService.js # Operaciones CRUD completas para Solicitudes
│   │   ├── clienteService.js # Consumo de Clientes (recurso relacionado)
│   │   └── productoService.js# Consumo de Productos/Servicios (catálogo)
│   ├── components/           # Componentes reutilizables de UI y seguridad
│   │   ├── AppLayout.jsx     # Layout con sidebar accesible, header y contenedor de rutas
│   │   ├── ProtectedRoute.jsx# Guard de rutas autenticadas (redirección a /login)
│   │   ├── RoleGuard.jsx     # Control condicional de renderizado por rol
│   │   └── ConfirmModal.jsx  # Diálogo modal accesible con trampa de foco para eliminar
│   ├── context/              # Estado global asíncrono y reactivo
│   │   ├── AuthContext.jsx   # Estado de sesión, usuario, JWT y permisos por rol
│   │   └── ToastContext.jsx  # Notificaciones toast con aria-live para lectores de pantalla
│   ├── pages/                # Vistas principales de la aplicación
│   │   ├── LoginPage.jsx     # Formulario de login accesible con validación
│   │   ├── DashboardPage.jsx # Estadísticas resumidas y accesos rápidos
│   │   ├── SolicitudesPage.jsx # CRUD principal: tabla, búsqueda, filtros y acciones
│   │   ├── SolicitudForm.jsx # Modal crear/editar con selectores de clientes y productos
│   │   ├── ClientesPage.jsx  # Vista y búsqueda de clientes registrados
│   │   ├── ProductosPage.jsx # Catálogo de productos y servicios con precios base
│   │   ├── ForbiddenPage.jsx # Página 403 Acceso Denegado
│   │   └── NotFoundPage.jsx  # Página 404 No Encontrado
│   ├── test/                 # Pruebas automatizadas (Vitest + Testing Library)
│   │   ├── setup.js          # Configuración de matchers jest-dom
│   │   ├── Login.test.jsx    # Pruebas de formulario de login, validación y errores 401
│   │   ├── ProtectedRoute.test.jsx # Pruebas de guardia de navegación protegida
│   │   ├── RoleGuard.test.jsx # Pruebas de control de acceso por rol (RBAC)
│   │   └── SolicitudesCRUD.test.jsx # Pruebas completas de listado, filtro, modal crear y borrar
│   ├── App.jsx               # Enrutamiento principal y anidación de providers
│   ├── index.css             # Design tokens, variables CSS, utilidades y accesibilidad
│   └── main.jsx              # Punto de entrada de la aplicación React 19
```

---

## 🔒 Seguridad Implementada (OWASP)

1. **Token de acceso solo en memoria:** el JWT de 15 minutos vive en la memoria de `src/sesion/sesion.js`, nunca en `localStorage` ni en `sessionStorage`. La sesión larga la guarda la API en una cookie `httpOnly`, y la página pide un token nuevo con `/auth/renovar`. En `sessionStorage` solo queda una marca sin datos, para saber si hay que renovar al recargar. Un script inyectado en la página sí puede pedir un token nuevo: `httpOnly` no lo impide, así que la protección contra XSS sigue siendo el punto 2.
2. **Mitigación de XSS:** Se prohíbe el uso de `dangerouslySetInnerHTML`. Todo dato ingresado por el usuario o devuelto por la API se escapa nativamente como texto en React. Se incluye cabecera `Content-Security-Policy` en `index.html`.
3. **Control de Acceso Basado en Roles (RBAC):** La interfaz protege rutas mediante `<ProtectedRoute>` y acciones mediante `<RoleGuard>`. Si un usuario intenta acceder a una operación restringida (ej. eliminación solo permitida al rol `dueno`), la interfaz bloquea el botón y el backend responde `403 Forbidden`, redirigiendo a `ForbiddenPage`.
4. **Manejo de Respuestas de Error y Rate Limit:**
   - `401 Unauthorized`: la sesión intenta renovarse una vez y repite la petición. Si no se puede, cierra la sesión y lleva al ingreso que corresponde (personal o cliente).
   - `403 Forbidden`: Muestra aviso de privilegios insuficientes.
   - `429 Too Many Requests`: Maneja límites de tasa informando al usuario sin bloquear la aplicación.
   - `400 / 422 ProblemDetails`: Mapea y asocia los errores de validación a cada campo del formulario.
5. **Auditoría de Dependencias:** Ejecución de `npm audit` con **0 vulnerabilidades**.

---

## ♿ Accesibilidad (WCAG 2.2 nivel AA)

- **Semántica HTML5:** Uso de `<header>`, `<nav>`, `<aside>`, `<main>`, `<table>`, `<dialog>`.
- **Formularios Accesibles:** Todos los campos cuentan con `<label>` vinculado mediante `htmlFor`, atributos `aria-invalid` y `aria-describedby` que enlazan al mensaje de error.
- **Trampa de Foco y Teclado:** Los diálogos modales atrapan el foco con las teclas `Tab` y `Shift+Tab`, y se cierran con la tecla `Escape`.
- **Lectores de Pantalla:** Región `aria-live="polite"` para anunciar notificaciones toast y cambios de estado dinámicos.
- **Contraste de Color:** Paleta validada con contraste superior a **4.5:1** en texto normal y foco visual de alta visibilidad (`outline: 2px solid var(--color-primary)`).

---

## 📱 Diseño Responsivo
- Metatag viewport configurado.
- Maquetación fluida mediante CSS Grid y Flexbox.
- Breakpoints adaptados a móvil (desde 320 px), tableta y escritorio.
- Menú lateral transformable en panel hamburguesa deslizable con overlay táctil.
- Objetivos táctiles con dimensión mínima de 44 × 44 px.

---

## Portada pública (fase 5)

- **Estado del local:** la portada lee `GET /local/estado` (hora de Chile) y lo vuelve a pedir cada 60 segundos y al volver a la pestaña. Los textos legales, en `src/paginas/publicas/legales/`.
- **Mapa:** la imagen `public/mapa/ubicacion-taller.webp` se genera con `scripts/mapa-estatico.py`, que necesita Python con Pillow y acceso a `tile.openstreetmap.org`:

  ```bash
  python scripts/mapa-estatico.py -37.46787 -72.34819
  ```

  Sin argumentos usa esas mismas coordenadas, las exactas del taller. Hay que volver a ejecutarlo si cambia la ubicación. Leaflet y las teselas de OpenStreetMap solo se cargan cuando el visitante pulsa «Mapa interactivo», y la vista de Google Street View solo con «Ver la fachada».
- **Vitrina con datos:** `sembrar-demo` del backend (ver su README) carga en la base demo los 22 trabajos con las fotos de `docs/imagenes-trabajos/vitrina/`.
- **Variable opcional:** `VITE_URL_PORTAL` fija la dirección pública del portal que codifica el código QR del panel. Sin ella se usa la del sitio que se está viendo. Todavía no está en `.env.example`.
- **Textos legales:** los documentos de `src/paginas/publicas/legales/textos.js` (vigentes, sin revisión legal por decisión del usuario) tienen marcados los datos pendientes como «[Pendiente de completar]» (razón social, RUT, abono). No deben publicarse así.
- **Producción:** el build reduce `connect-src` de la CSP a `'self'` y, si `VITE_API_URL` apunta a otro origen, a ese origen.

---

## Cuenta y sesión (fase 6)

- **Pantallas:** `/ingresar` (cliente, con correo y contraseña), `/cuenta/registro`, `/cuenta/recuperar` (en dos pasos: pide el código y cambia la contraseña; con `?tipo=personal`, la del personal), `/cuenta/verificar` (confirma el correo con el enlace) y `/cuenta` (Mi cuenta: datos, correo, contraseña y marketing). El personal entra por `/personal/ingresar`. Las pantallas están en `src/paginas/cuenta/`, y su cliente HTTP en `src/api/cuenta.js`.
- **Sesión** (`src/sesion/sesion.js`): el token vive solo en memoria. Se renueva un minuto antes de que venza y después de un `401`. Con `navigator.locks` una sola pestaña renueva a la vez, y `BroadcastChannel` avisa a las demás cuando alguien ingresa o sale. Al cargar la página, si hay marca en `sessionStorage`, pide un token nuevo con la cookie. `Guardas.jsx` lleva a cada usuario al ingreso que le corresponde (`RequierePersonal` y `RequiereCliente`).
- **Cabecera CSRF:** `POST /auth/renovar` y `POST /auth/salir` envían `X-CSRF-Token` con el valor de la cookie `synctime_csrf`, que la página sí puede leer. La cookie de renovación no la puede leer.
- **Pruebas:** `src/test/cuenta.test.jsx` y `src/test/sesion.test.jsx`, y en `e2e/pantallas.spec.js` el registro, la recuperación, la verificación del correo y que el token no quede en el almacenamiento del navegador.

## Solicitudes, adjuntos y clientes (fase 7)

- **Pantallas del cliente:** `/cuenta/solicitud` (ítems, descripción general y la zona de adjuntos; termina en el comprobante), `/cuenta/solicitudes` (Mis solicitudes) y `/cuenta/solicitudes/:id` (el comprobante de una). «Solicitar cotización» de la portada lleva a `/cuenta/solicitud`; con «Cotizar uno similar» llega con `?producto=` y `?referencia=`. Sin sesión pasa por `/ingresar` y vuelve a la página pedida.
- **Pantallas del personal:** `/panel` (solicitudes por atender), `/panel/solicitudes` (bandeja con filtros en la dirección, búsqueda y paginación), `/panel/solicitudes/nueva` (registro presencial: busca al cliente por teléfono o RUT antes de crearlo), `/panel/solicitudes/:id` (ficha con adjuntos, historial y acciones de estado), `/panel/clientes` y `/panel/clientes/:id`.
- **Código:** las pantallas y sus reglas están en `src/paginas/solicitudes/` (`reglasSolicitud.js`, `useSolicitudEditable.js`, `ContactoDirecto.jsx` y `panel/`). La zona de adjuntos, en `src/componentes/ui/Adjuntos.jsx`, con sus reglas en `reglasAdjuntos.js`. Las llamadas a la API, en `src/api/solicitudes.js`.
- **Adjuntos:** la zona acepta JPG, PNG, WebP y PDF de hasta 10 MB, hasta 5 por solicitud. El atributo `accept` limita el selector, pero el servidor vuelve a comprobar todo. Cada archivo se sube al elegirlo y muestra su avance. Las miniaturas, de 96 px, y los archivos se piden con el token y se muestran con `URL.createObjectURL`, que se libera al quitarlos. Los borradores que el cliente subió antes y no envió vuelven al formulario (`GET /mis/adjuntos`).
- **Edición y versión:** la ficha del personal envía la versión que vio en `If-Match`. Un `409` con `version_obsoleta` avisa y recarga la ficha; al editar, lo escrito se conserva.
- **Contacto directo:** cuando la regla contra solicitudes falsas frena un envío (`409` con `contacto_directo`), se muestra el horario, el estado del local (`GET /local/estado`), el teléfono y WhatsApp. Lo escrito se conserva. Los datos de contacto salen de `src/configuracion/taller.js`.
- **Lo escrito en la pestaña:** el texto del formulario (sin adjuntos) se guarda en `sessionStorage`, con una clave por usuario, y se borra al enviar la solicitud.
- **Pruebas:** `src/test/solicitudes.test.jsx` y `src/test/adjuntos.test.jsx`. En `e2e/pantallas.spec.js`, el flujo en línea con un adjunto hasta el comprobante, el contacto directo, el enlace «Ver solicitud» del correo y el inicio del panel con las solicitudes por atender.

---

## Cotizaciones, pedidos y calendario (fase 8)

- **Pantallas del personal:** `/panel/cotizaciones` (listado con filtros en la dirección), `/panel/cotizaciones/:id` (la cotización), `/panel/solicitudes/:id/cotizar` y `/panel/cotizaciones/:id/editar` (el editor de materiales, mano de obra y otros costos), `/panel/cotizaciones/:id/pedido` (el pedido desde la cotización aprobada, con abono inicial opcional), `/panel/pedidos` (listado con filtros; «Por cobrar» aplica la cobranza), `/panel/pedidos/:id` (la ficha: avance, edición, abonos e historial), `/panel/pedidos/directo` (el pedido presencial directo, que reemplaza al diálogo de la fase 3) y `/panel/calendario` (calendario de entregas). Las alertas de entregas y cobranza aparecen en `/panel`, solo para gestión.
- **Pantallas del cliente:** `/cuenta/solicitudes` y `/cuenta/solicitudes/:id` muestran el seguimiento: la cotización, con «Aprobar» y «Rechazar» mientras está pendiente, y el pedido, con su estado, su pago, su saldo, la fecha estimada y la modalidad. Cada estado se dice con texto, además del color.
- **Código:** las pantallas están en `src/paginas/cotizaciones/` y `src/paginas/pedidos/`, con sus reglas en `reglasCotizacion.js`, `reglasPedido.js` y `reglasCalendario.js`. El seguimiento del cliente está en `src/paginas/solicitudes/Seguimiento.jsx`, y lo que comparten (el aviso de versión vieja y la revisión tras un corte) en `src/paginas/compartidos/`. Las llamadas a la API, en `src/api/cotizaciones.js` y `src/api/pedidos.js`.
- **Montos y fechas:** los montos son pesos enteros (`formatearPesos`), y el total de la cotización se redondea al peso, con la mitad hacia arriba. Las fechas son de hora de Chile, y «hoy» es la fecha de Chile.
- **Versión:** cada edición, respuesta o cambio de estado envía la versión vista en `If-Match`. Si otra persona cambió el registro, la pantalla conserva lo escrito, avisa y permite cargar la versión actual. Registrar un abono no envía versión.
- **Sin clave de idempotencia:** la API no tiene una para cotizaciones, pedidos ni abonos. Si se pierde la respuesta, la pantalla revisa antes de dejar reintentar: si el registro ya quedó, lo abre en vez de crearlo otra vez (`src/paginas/compartidos/trasCorte.js`).
- **Pruebas:** `src/test/cotizaciones.test.jsx` y `src/test/pedidosFase8.test.jsx`. En `e2e/pantallas.spec.js`, el editor de cotización, la aprobación del cliente, el avance del pedido con sus abonos, el calendario y el pedido directo.

---

## 🧪 Ejecución de Pruebas Automatizadas

Al cerrar la fase 8, la suite tiene 348 pruebas de Vitest y 141 de Playwright:
```bash
# Pruebas unitarias y de componentes (Vitest)
npm test

# Pruebas end-to-end (Playwright)
npm run test:e2e
```