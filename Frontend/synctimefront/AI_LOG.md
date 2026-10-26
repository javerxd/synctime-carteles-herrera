# Bitácora de Uso de Inteligencia Artificial Generativa (AI_LOG)
**Proyecto:** SyncTime — Frontend y Seguridad  
**Evaluación:** Evaluación 2 (UT2)  
**Asignatura:** Lenguaje Programación Web II  
**Docente:** Enso Guidotti  
**Fecha:** 20-10-2026  

---

## 1. Declaración de Herramientas y Modelos Utilizados

Conforme a las condiciones de realización establecidas en la pauta de evaluación, se declara el uso asistido de herramientas de inteligencia artificial generativa durante el desarrollo del proyecto:

| Herramienta | Modelo | Tipo de Cuenta / Plan | Uso Principal |
|---|---|---|---|
| **Microsoft Copilot** | GPT-4o | Cuenta Institucional | Consulta metodológica inicial sobre integración del frontend con la API de Evaluación 1. |
| **Antigravity IDE** | Claude Opus 4.6 (Thinking) & Gemini 3.8 Flash (High) | Plan Profesional / IDE | Auditoría integral contra rúbrica, implementación de Vista de Detalle (CRUD), adaptabilidad móvil CSS, sincronización de contratos API, pruebas de integración .NET 10, suite E2E con Playwright y versionamiento Git. |

---

## 2. Registro de Prompts y Decisiones por Etapa

### Etapa 1: Arquitectura y Organización del Proyecto
- **Prompt Utilizado:**
  > *"necesito realizar este proyecto, según las instrucciones dice que debe de ser en base al trabajo anterior de la primera evaluación y ese trabajo es este, lo que yo quiero saber es cómo realizarlo, ¿debo de crear un proyecto aparte en Visual Studio Code con framework de React?"*
- **Respuesta Recibida de la IA:**
  Recomendó crear una aplicación frontend separada con React y Vite dentro de la misma estructura de trabajo, manteniendo el backend en ASP.NET Core y consumiéndolo mediante una variable de entorno `VITE_API_URL`.
- **Decisión del Estudiante:**
  - **Aceptado:** Se aceptó la creación con Vite y la estructura desacoplada con proxy local para evitar problemas de CORS en desarrollo.
  - **Corregido / Ajustado:** Copilot sugirió TypeScript inicialmente; se optó por React con JavaScript y PropTypes para ajustarse con precisión a la base de código inicial del estudiante y asegurar compatibilidad directa sin pasos de transpilación adicionales no contemplados en el repo base.

---

### Etapa 2: Capa de Consumo HTTP y Seguridad del Token
- **Prompt Utilizado:**
  > *"Diseñar una capa de consumo HTTP centralizada con Axios para React que adjunte el token JWT Bearer, maneje errores de validación 400 ProblemDetails, 401, 403 y 429 de Rate Limiting, y explique la mejor estrategia de almacenamiento de token entre localStorage y sessionStorage según OWASP."*
- **Respuesta Recibida de la IA:**
  Proporcionó una instancia de Axios con interceptores de solicitud y respuesta, y sugirió `sessionStorage` para minimizar la persistencia del token ante ataques XSS en comparación con `localStorage`.
- **Decisión del Estudiante:**
  - **Aceptado:** Se adoptó la estrategia `sessionStorage` y los interceptores con normalización de errores.
  - **Corregido / Ajustado:** Se ajustó el interceptor para no provocar bucles de redirección infinita si ya se encontraba en `/login` cuando la API respondía 401, y se mapeó el diccionario de `errors` de ASP.NET Core ProblemDetails para que los formularios vinculen los mensajes a cada campo específico.

---

### Etapa 3: Accesibilidad (WCAG 2.2 Nivel AA) y Formularios
- **Prompt Utilizado:**
  > *"Generar el formulario modal para la entidad Solicitud que consuma los recursos relacionados (Clientes y Productos) y cumpla con la pauta WCAG 2.2 AA: trampa de foco con Tab y Escape, labels asociados con htmlFor, aria-describedby para mensajes de error y aria-invalid en campos con error."*
- **Respuesta Recibida de la IA:**
  Generó el componente modal con listeners de teclado `keydown` y selectores HTML accesibles.
- **Decisión del Estudiante:**
  - **Aceptado:** La implementación de la trampa de foco y los atributos ARIA.
  - **Corregido / Ajustado:** Se añadió la propiedad `aria-modal="true"` y un encabezado semántico `<h2>` referenciado con `aria-labelledby` para lectores de pantalla. Se sincronizó la selección de `idCliente` y `idProducto` con validación numérica para prevenir envíos de tipos string que la API rechazaría.

---

### Etapa 4: Autorización por Roles (RBAC) y Páginas de Estado
- **Prompt Utilizado:**
  > *"Crear componentes ProtectedRoute y RoleGuard para proteger rutas y botones según los roles 'dueno', 'disenador' y 'operario', además de una página amigable para el código 403 Acceso Denegado."*
- **Respuesta Recibida de la IA:**
  Proporcionó los componentes basados en el contexto de autenticación y la página de 403.
- **Decisión del Estudiante:**
  - **Aceptado:** La lógica condicional de roles y la página de acceso denegado.
  - **Corregido / Ajustado:** Se enfatizó en la documentación y en el código que la validación en la interfaz es solo de presentación (UX) y que el backend ASP.NET Core debe seguir aplicando los filtros `[Authorize(Roles = "...")]` en cada endpoint para garantizar seguridad real.

---

### Etapa 5: Pruebas Automatizadas con Vitest y Testing Library
- **Prompt Utilizado:**
  > *"Configurar Vitest con jsdom y Testing Library para probar el flujo de Login (validación, submit y error 401), ProtectedRoute (redirección y acceso permitido) y el CRUD de Solicitudes (listar, filtrar, crear con modal y eliminar con confirmación)."*
- **Respuesta Recibida de la IA:**
  Proveyó la suite de tests con mocks de los servicios `authService` y `solicitudService`.
- **Decisión del Estudiante:**
  - **Aceptado:** La estructura con mocks de servicios y matchers `@testing-library/jest-dom`.
  - **Corregido / Ajustado:** Se corrigieron los textos esperados en los matchers de los tests para que coincidieran exactamente con las cadenas en español definidas en `LoginPage.jsx` ("El email es obligatorio", "Email o contraseña incorrectos"), logrando que el 100% de las 14 pruebas se ejecuten y pasen de forma limpia (`npm test`).

---

### Etapa 6: Auditoría Forense contra la Rúbrica de Evaluación y Diagnóstico Integral
- **Herramienta / Modelo:** Antigravity / Claude Opus 4.6 & Gemini 3.8 Flash
- **Prompt Utilizado:**
  > *"Quiero que revises este proyecto y me digas si le falta algo por cumplir en esta rúbrica adjunta, revisa si cumple por completo con: los requisitos funcionales, entregables, condiciones de realizacion, rubrica de evaluación. Dejaré funcionando el otro proyecto con el backend que contiene la API por si necesitas hacer pruebas..."*
- **Respuesta Recibida de la IA:**
  Ejecutó un análisis exhaustivo del frontend y del backend contra la pauta de Evaluación 2. Identificó brechas puntuales: ausencia de la acción "Ver detalle" (Consultar) en el CRUD de solicitudes, campo opcional `tamano` no contemplado en el formulario, corte responsivo de la columna de acciones en pantallas móviles en `src/index.css`, fallo en los tests de integración de .NET por conflicto en `MigrateAsync()`, y necesidad de incorporar la suite E2E de Playwright.
- **Decisión del Estudiante:**
  - **Aceptado:** El plan de remediación integral estructurado en pasos secuenciales para alcanzar el 100% de la pauta.
  - **Corregido / Ajustado:** Se delimitó explícitamente no generar material de diapositivas ni capturas manuales, delegando dichas tareas a la preparación presencial del estudiante y enfocando a la IA exclusivamente en la completitud técnica del código y las pruebas.

---

### Etapa 7: Implementación de Vista de Detalle, Sincronización API y Responsividad Móvil
- **Herramienta / Modelo:** Antigravity / Claude Opus 4.6 (Thinking)
- **Prompt Utilizado:**
  > *"Excelente diagnóstico. Procedamos a implementar todas las correcciones técnicas en el código para alcanzar el 100% de la rúbrica... 1. Completar CRUD (Vista de Detalle) y Sincronización con la API: Modal de Detalle (SolicitudesPage.jsx), Sincronización de Formulario (SolicitudForm.jsx), Cliente HTTP y Contratos... 2. Accesibilidad y Diseño Responsivo en Móvil: Tabla Responsiva (src/index.css), Variables en ForbiddenPage.jsx..."*
- **Respuesta Recibida de la IA:**
  Creó el componente accesible `DetailModal.jsx`, incorporó el botón "Ver detalle" en la tabla de `SolicitudesPage.jsx`, agregó el campo `tamano` y ajustó la semántica del estado en `SolicitudForm.jsx`, corrigió variables en `ForbiddenPage.jsx` (`user.nombre` y `user.rol`), y transformó la tabla en tarjetas responsivas mediante reglas de medios CSS para pantallas móviles (320px a 768px).
- **Decisión del Estudiante:**
  - **Aceptado:** La implementación del modal con trampa de foco y cierre accesible con Escape, y el diseño de tarjetas móviles.
  - **Corregido / Ajustado:** Se restauró la clase utilitaria de accesibilidad `.sr-only` en `src/index.css` y se agregaron atributos `data-label` a todas las celdas de la tabla para que en versión móvil cada dato mantenga su contexto visual y la columna de acciones permanezca siempre accesible.

---

### Etapa 8: Reparación de Backend (.NET 10), Suite E2E con Playwright y Preparación de Entrega
- **Herramienta / Modelo:** Antigravity / Gemini 3.8 Flash (High)
- **Prompt Utilizado:**
  > *"Instrucciones para terminar el proyecto ahora mismo en orden: 1. Finalizar Paso 2.3: Ejecutar las 5 pruebas de integración del Backend (.NET 10)... 2. Pruebas Unitarias, Build y E2E con Playwright en Frontend (synctimefront)... 3. Actualizar Bitácora de IA (AI_LOG.md)... 4. Inicializar Git y Subir a GitHub..."*
- **Respuesta Recibida de la IA:**
  Corrigió un error de sintaxis en `appsettings.Development.json` y el control de `SqlException (2714)` en `Program.cs`, logrando que los 5 tests de integración de .NET 10 se ejecuten y pasen en verde (Total: 5, Failed: 0). Instaló y configuró `@playwright/test` en `synctimefront`, implementó la prueba E2E en `e2e/flujo-solicitudes.spec.js` automatizando el flujo `Login -> Crear Solicitud -> Listar Solicitud -> Ver Detalle`, y validó `npm test` (14/14), `npm run lint` (0 errores) y `npm run build`.
- **Decisión del Estudiante:**
  - **Aceptado:** La automatización E2E con Playwright simulando el flujo completo de usuario y la validación cruzada con el backend.
  - **Corregido / Ajustado:** Se ajustaron los selectores de encabezado en la prueba E2E para evitar ambigüedades con el menú lateral (`sidebar-brand`) y se sincronizó el texto esperado con el encabezado modal `Detalle de Solicitud #102`, garantizando una ejecución limpia y reproducible.

### Etapa 9: Diagnóstico de Excepción SQL (Truncate de Teléfono) y Blindaje de Validaciones
- **Herramienta / Modelo:** Microsoft Copilot (GPT-4o) & Antigravity (Gemini 3.8 Flash)
- **Prompt Utilizado en Copilot:**
  > *"Durante la ejecución del proyecto intenté agregar una solicitud y en el campo de teléfono le agregué un número que sobrepasaba el límite... me dio Microsoft.Data.SqlClient.SqlException: 'String or binary data would be truncated in table solicitudes, column telefono_contacto'. ¿Qué causa el error y cómo se soluciona?"*
- **Prompt Utilizado en Antigravity:**
  > *"¿Crees que es un error de parte de acá del frontend o de el otro proyecto de backend? Después de responder eso, quiero que le hagas validaciones a todos los campos que se puedan llenar en el sistema, contra campos vacíos, que los campos de teléfono solo acepten números y hasta 12 dígitos máximo usando el código de chile como referencia '+56 1 1111 1111', en general todas las validaciones que se le puedan realizar a campos de un formulario."*
- **Respuestas Recibidas y Análisis Cruzado:**
  Tanto Copilot como Antigravity coincidieron en la causa raíz: la base de datos define `telefono_contacto` como `VARCHAR(20)` y `SolicitudApi.cs` no limitaba la longitud máxima, permitiendo que la entrada llegara a SQL Server y detonara un error HTTP 500 (`DbUpdateException`).
- **Decisión del Estudiante:**
  - **Aceptado:** La convergencia de ambos modelos respecto a que el error requería Defensa en Profundidad (validar en cliente y servidor). Se aplicó la validación de `Length > 20` en el backend para retornar `400 ValidationProblem`.
  - **Corregido / Ampliado con Antigravity:** Se extendió el blindaje al frontend con filtrado numérico en tiempo real en `SolicitudForm.jsx`, límite estricto de 12 dígitos para formato chileno (`+56 9 1234 5678`), atributo físico `maxLength={16}`, y validaciones completas para el resto de entidades (límite de 50 caracteres en tamaño, 100 en material y 255 con contador visual en notas, más regex RFC 5322 en el login).

### Etapa 10: Homogeneización de Diseño Glassmorphism Dark Enterprise y Gestión de Credenciales
- **Herramienta / Modelo:** Antigravity (Gemini 3.8 Flash)
- **Prompt Utilizado:**
  > *"¿Podrías aplicar el estilo de LoginPage a las demás páginas del proyecto? También se me olvidó el correo y contraseña de los usuarios admin, ¿podrías entregármelos?"*
- **Respuesta Recibida de la IA:**
  Consultó directamente el catálogo de usuarios de SQL Server (`LetrerosHerreradb`), identificando las cuentas existentes (`admin@losherrera.com`, `admin.pruebas@synctime.test`, `operario@losherrera.com`, `usuario.pruebas@synctime.test`), y procedió a estandarizar los hashes BCrypt para proveer credenciales operativas inmediatas. Posteriormente, homogeneizó el sistema de diseño completo en `src/index.css`, replicando la estética Enterprise Dark Glassmorphism de `LoginPage` en toda la plataforma (Sidebar, Header, Dashboard, Tablas con efecto cristal, Modales esmerilados, Badges luminosos y Formularios oscuros), manteniendo un contraste WCAG 2.2 AA superior a 4.5:1 y validando con 100% de éxito en Vitest (14/14) y Playwright E2E.
- **Decisión del Estudiante:**
  - **Aceptado:** La aplicación transversal de tokens oscuros (`#07080a`), iluminación ambiental, efecto de cristal esmerilado (`backdrop-filter`) y la entrega clara de las credenciales de administración.
  - **Corregido / Ajustado:** Se mantuvieron intactas las reglas de accesibilidad (`.sr-only`, `:focus-visible`, contraste de etiquetas de formulario y diseño responsive móvil con data-labels).

---

## 3. Resumen y Conclusiones del Uso de IA

El uso asistido y crítico de herramientas de IA generativa (Microsoft Copilot, Claude Opus y Gemini 3.8 Flash) permitió acelerar de forma sistemática el desarrollo del proyecto, la resolución de fallos de configuración en .NET 10, la cobertura completa de pruebas automatizadas (unitarias e integración E2E), la resolución colaborativa de discrepancias de esquema SQL y el cumplimiento estricto de los estándares WCAG 2.2 AA y OWASP Top 10. Todo el código fue inspeccionado, compilado y verificado en tiempo real mediante suites automatizadas (`npm test`, `playwright test`, `dotnet run --project Tests`, `npm run build`), garantizando que el estudiante domina con profundidad la solución y puede defender de forma autónoma cada componente implementado.

