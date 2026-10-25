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

1. **Almacenamiento seguro del JWT:** Se utiliza `sessionStorage` para aislar el token por pestaña de navegación. Al cerrar la pestaña o el navegador, la sesión se destruye automáticamente, reduciendo el riesgo de persistencia indebida.
2. **Mitigación de XSS:** Se prohíbe el uso de `dangerouslySetInnerHTML`. Todo dato ingresado por el usuario o devuelto por la API se escapa nativamente como texto en React. Se incluye cabecera `Content-Security-Policy` en `index.html`.
3. **Control de Acceso Basado en Roles (RBAC):** La interfaz protege rutas mediante `<ProtectedRoute>` y acciones mediante `<RoleGuard>`. Si un usuario intenta acceder a una operación restringida (ej. eliminación solo permitida al rol `dueno`), la interfaz bloquea el botón y el backend responde `403 Forbidden`, redirigiendo a `ForbiddenPage`.
4. **Manejo de Respuestas de Error y Rate Limit:**
   - `401 Unauthorized`: Destruye el token del almacenamiento y redirige limpiamente al login.
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

## 🧪 Ejecución de Pruebas Automatizadas

Para ejecutar las 14 pruebas automatizadas:
```bash
npm test
```

Resultado verificado:
```
✓ src/test/RoleGuard.test.jsx (4 tests)
✓ src/test/ProtectedRoute.test.jsx (2 tests)
✓ src/test/Login.test.jsx (4 tests)
✓ src/test/SolicitudesCRUD.test.jsx (4 tests)

Test Files  4 passed (4)
     Tests  14 passed (14)
```