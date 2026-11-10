# Informe Técnico: Arquitectura, Seguridad y Accesibilidad Frontend
**Proyecto:** SyncTime — Sistema de Gestión Letreros Herrera  
**Evaluación 2:** Consumo de APIs RESTful desde aplicaciones frontend y seguridad  
**Asignatura:** Lenguaje Programación Web II  
**Estudiante:** Evaluación Individual  
**Docente:** Enso Guidotti  
**Fecha:** 20-10-2026  

---

## 1. Introducción y Contexto del Negocio

El presente informe documenta las decisiones de diseño arquitectural, la estrategia de seguridad integral y la implementación de accesibilidad y diseño responsivo aplicadas en el frontend de **SyncTime**, desarrollado para la empresa **Letreros Herrera**.

La aplicación consume la API RESTful desarrollada en ASP.NET Core (Evaluación 1), integrando el recurso principal (**Solicitudes**) y dos recursos vinculados (**Clientes** y **Productos/Servicios**). Toda la solución garantiza que el navegador actúe como un cliente seguro, validando entradas, controlando privilegios por rol (RBAC) y gestionando tokens JWT sin comprometer la integridad del sistema.

---

## 2. Decisiones de Diseño y Justificación Técnica

### 2.1 Framework y Herramienta de Construcción (Vite + React 19)
- **Decisión:** Se utilizó **React 19** junto al empaquetador **Vite 8**.
- **Justificación:** Vite proporciona Hot Module Replacement (HMR) ultrarrápido sobre módulos ES nativos y empaquetado optimizado mediante Rollup. React 19 ofrece renderizado declarativo, tipado estricto mediante PropTypes y optimizaciones nativas de rendimiento, permitiendo modularizar la interfaz en componentes reutilizables sin sobrecarga de dependencias.

### 2.2 Estructura por Capas y Separación de Responsabilidades
- **Decisión:** El código fuente se organizó en capas independientes:
  - `src/api`: Centralización del consumo HTTP y DTOs de comunicación.
  - `src/components`: Componentes reutilizables de UI, layouts y guardias de navegación (`ProtectedRoute`, `RoleGuard`, `ConfirmModal`).
  - `src/context`: Gestión de estado transversal asíncrono (`AuthContext`, `ToastContext`).
  - `src/pages`: Vistas de usuario y formularios asociados a las rutas.
  - `src/test`: Suite de pruebas unitarias y de integración de componentes.
- **Justificación:** Facilita el mantenimiento, desacopla la lógica de negocio del renderizado de componentes y permite auditar y testear individualmente cada módulo.

### 2.3 Capa de Consumo HTTP Centralizada (`httpClient`)
- **Decisión:** Creación de una instancia única de **Axios** (`httpClient.js`) configurada con base URL dinámica vía variables de entorno (`VITE_API_URL`), con interceptores de solicitud y respuesta.
- **Justificación:**
  - El **Request Interceptor** inyecta automáticamente el token Bearer en el encabezado `Authorization` para cada solicitud saliente, evitando código repetitivo.
  - El **Response Interceptor** normaliza los códigos de error HTTP (400 ProblemDetails, 401, 403, 404, 409, 429, 500), garantizando que los componentes reciban una estructura de error predecible y consistente.

### 2.4 Estrategia de Manejo de Estado
- **Decisión:** Estado global manejado mediante **React Context API** (`AuthContext` y `ToastContext`) y estado local de servidor mediante hooks con estado asíncrono (`useState`, `useEffect`, `useCallback`).
- **Justificación:** Para el tamaño y alcance de la aplicación, Context API nativo proporciona reactividad eficiente sin requerir librerías pesadas adicionales (como Redux), manteniendo la carga inicial liviana.

### 2.5 Estrategia de Almacenamiento del Token JWT
- **Decisión:** Almacenamiento en **`sessionStorage`** bajo la clave `synctime_token`.
- **Análisis de Riesgos y Justificación:**
  - *Comparación con localStorage:* `localStorage` persiste indefinidamente en el disco del navegador y es accesible por cualquier pestaña del mismo origen, lo que aumenta la ventana de exposición ante incidentes de XSS o equipos compartidos.
  - *Ventaja de sessionStorage:* El ciclo de vida del token está acotado estrictamente a la pestaña activa de navegación; al cerrar la pestaña o el navegador, el token se destruye de inmediato.
  - *Mitigación adicional:* Se complementa con una política estricta de mitigación de XSS (sin `dangerouslySetInnerHTML`, escapado nativo de React, y Content Security Policy activa).

---

## 3. Matriz de Riesgos de Seguridad Mitigados (OWASP)

De acuerdo con la pauta de evaluación, se mitigaron los siguientes riesgos del **OWASP API Security Top 10 (2023)** y **OWASP Top 10 Web (2021)**:

| # | Riesgo | Referencia OWASP | Lado | Dónde Aparece | Mitigación Implementada | Evidencia Verificable |
|---|---|---|:---:|---|---|---|
| **1** | **Cross-Site Scripting (XSS)** | OWASP Web: A03:2021 Injection | Cliente | Renderizado de textos de clientes, materiales y notas de solicitudes en tablas y diálogos. | Prohibición total de `dangerouslySetInnerHTML`. React escapa por defecto todos los valores interpolateados como texto plano. Adicionalmente se incluye `Content-Security-Policy` estricta en `index.html`. | Código en `SolicitudesPage.jsx`, `SolicitudForm.jsx` y meta tag CSP en `index.html`. |
| **2** | **Robo o Filtración de Token** | OWASP API: API2:2023 Broken Authentication | Cliente | Manejo y persistencia de credenciales y tokens JWT de sesión. | Almacenamiento en `sessionStorage` (destrucción automática al cerrar sesión o cerrar pestaña). Limpieza total en `logout()`. Eliminación de token en URLs y no logging en consola. | Código en `AuthContext.jsx` y `httpClient.js`. Pruebas en `ProtectedRoute.test.jsx`. |
| **3** | **Control de Acceso Roto a Nivel de Función** | OWASP API: API5:2023 Broken Function Level Authorization | Ambos | Acceso a operaciones críticas (ej. eliminación de solicitudes reservada al rol `dueno`). | Doble barrera: la interfaz condiciona la visibilidad y acción con `<RoleGuard>` y deshabilita botones. Si un usuario evade la interfaz, el backend valida el rol mediante `[Authorize(Roles = "dueno")]` y responde `403 Forbidden`. | Componente `RoleGuard.jsx`, vista `ForbiddenPage.jsx`, pruebas en `RoleGuard.test.jsx`. |
| **4** | **Consumo Desmedido y Fuerza Bruta (Rate Limiting)** | OWASP API: API4:2023 Unrestricted Resource Consumption | Ambos | Endpoint de autenticación `/api/v1/auth/login` y peticiones masivas. | Interceptor en `httpClient.js` detecta código HTTP `429 Too Many Requests` y notifica al usuario un mensaje informativo sin romper la ejecución ni provocar bloqueos silenciosos. | Código en `httpClient.js` (líneas 62-68) y prueba en `Login.test.jsx`. |
| **5** | **Validación de Entradas Insuficiente** | OWASP Web: A03:2021 Injection | Ambos | Formulario de creación y edición de solicitudes (`SolicitudForm.jsx`). | Validación rigurosa en el cliente previa al envío (campos requeridos, tipos de datos, formato de teléfono, cantidades positivas) y manejo transparente de respuestas `400 ProblemDetails` del backend mapeadas a los campos correspondientes. | Validación en `SolicitudForm.jsx` y prueba en `SolicitudesCRUD.test.jsx`. |
| **6** | **Secretos Expuestos en el Bundle del Cliente** | OWASP Web: A05:2021 Security Misconfiguration | Cliente | Configuración de entorno y empaquetado del bundle JavaScript. | El frontend solo utiliza la variable `VITE_API_URL` que contiene la dirección pública del servidor. Se prohíbe incluir llaves de firma JWT, contraseñas o connection strings en el código estático. Archivos `.env` ignorados en `.gitignore`. | `.gitignore`, `.env.example` y revisión del bundle generado en `dist/`. |
| **7** | **Dependencias Vulnerables y Obsoletas** | OWASP Web: A06:2021 Vulnerable Components | Cliente | Árbol de dependencias npm del proyecto frontend. | Bloqueo estricto de dependencias mediante `package-lock.json` y auditoría de seguridad automatizada mediante `npm audit`. | Reporte de auditoría `npm audit`: **0 vulnerabilidades encontradas**. |

---

## 4. Implementación de Accesibilidad (WCAG 2.2 Nivel AA)

1. **HTML Semántico:** Utilización estricta de elementos estructurales: `<header>`, `<aside>`, `<nav>`, `<main>`, `<table>`, `<thead>`, `<tbody>`, `<caption>`, `<dialog>`.
2. **Formularios con Asociación Explícita:**
   - Todos los inputs, selects y textareas poseen etiquetas `<label htmlFor="...">`.
   - Campos con error se marcan con `aria-invalid="true"`.
   - Los mensajes de error se vinculan al campo mediante `aria-describedby="error-[campo]"`.
3. **Navegación por Teclado y Gestión del Foco:**
   - Enlace accesible de salto al contenido principal (`<a className="skip-link" href="#main-content">`).
   - Indicador visual de foco nítido y de alto contraste (`outline: 2px solid var(--color-primary)` con `outline-offset: 2px`).
   - Trampa de foco en diálogos modales (`ConfirmModal.jsx` y `SolicitudForm.jsx`): el ciclo de tabulación (`Tab` / `Shift+Tab`) permanece confinado al modal abierto, y se cierran al pulsar la tecla `Escape`.
4. **Retroalimentación Dinámica:**
   - Regiones activas accesibles (`aria-live="polite"`) en el sistema de notificaciones toast y en las páginas CRUD para anunciar operaciones completadas a usuarios de lectores de pantalla (como NVDA o VoiceOver).
5. **Relación de Contraste:**
   - Texto principal contra fondo: relación superior a 7:1 (excediendo el umbral mínimo de 4.5:1 exigido por WCAG AA).
   - Insignias de estado con colores complementarios e iconos textuales, garantizando que el color no sea el único canal transmisor de información.

---

## 5. Estrategia de Pruebas Automatizadas

Se implementaron 14 pruebas automatizadas con **Vitest** y **@testing-library/react**:

1. **`Login.test.jsx` (4 pruebas):**
   - Renderizado completo del formulario con etiquetas accesibles.
   - Validación en cliente cuando los campos están vacíos.
   - Flujo exitoso de autenticación con guardado del token en `sessionStorage`.
   - Manejo de error 401 (credenciales inválidas) con alerta accesible.
2. **`ProtectedRoute.test.jsx` (2 pruebas):**
   - Redirección automática de usuarios anónimos hacia `/login`.
   - Acceso concedido al contenido cuando existe un token válido.
3. **`RoleGuard.test.jsx` (4 pruebas):**
   - Autorización por rol individual (`dueno`).
   - Autorización por lista de roles permitidos (`['dueno', 'disenador']`).
   - Ocultamiento de acciones no autorizadas.
   - Renderizado de interfaz alternativa o fallback.
4. **`SolicitudesCRUD.test.jsx` (4 pruebas):**
   - Carga y visualización de la lista de solicitudes desde el servicio API.
   - Búsqueda y filtrado interactivo.
   - Apertura del modal, llenado y creación exitosa mediante `solicitudService.create`.
   - Diálogo de confirmación accesible y eliminación mediante `solicitudService.remove`.

---

## 6. Conclusiones

La solución desarrollada para la Evaluación 2 integra con éxito la API RESTful de Letreros Herrera con un frontend moderno, seguro y accesible. Se cumplen la totalidad de los requisitos funcionales y no funcionales de la rúbrica: consumo centralizado, CRUD completo con validación y manejo de estados, autenticación JWT con control de roles, mitigación de riesgos OWASP documentada y verificada, diseño responsivo y suite de pruebas unitarias al 100% de aprobación.
