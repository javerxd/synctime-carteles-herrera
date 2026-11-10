# Evidencias de Seguridad, Accesibilidad y Pruebas
**Proyecto:** SyncTime — Sistema de Gestión Letreros Herrera  
**Evaluación 2:** Consumo de APIs RESTful desde aplicaciones frontend y seguridad  
**Fecha de Verificación:** 20-10-2026  

---

## 1. Evidencia de Auditoría de Dependencias (`npm audit`)

Se ejecutó la auditoría automatizada del árbol completo de paquetes y dependencias en el frontend:

```bash
> npm audit
found 0 vulnerabilities
```

**Resultado:**
- 138 paquetes analizados
- **0 vulnerabilidades detectadas** (0 críticas, 0 altas, 0 moderadas, 0 bajas)
- Archivo `package-lock.json` fijado para garantizar reproducibilidad en cualquier entorno de evaluación.

---

## 2. Evidencia de Ejecución de Pruebas Automatizadas

### 2.1 Pruebas de Componentes Frontend (`npm test` — Vitest + Testing Library)
```bash
> synctimefront@0.0.0 test
> vitest run

 RUN  v5.0.5 C:/Users/Admin/Desktop/ev2/Frontend/synctimefront

 ✓ src/test/RoleGuard.test.jsx (4 tests) 56ms
 ✓ src/test/ProtectedRoute.test.jsx (2 tests) 64ms
 ✓ src/test/Login.test.jsx (4 tests) 293ms
 ✓ src/test/SolicitudesCRUD.test.jsx (4 tests) 709ms

 Test Files  4 passed (4)
      Tests  14 passed (14)
   Duration  3.25s
```

### 2.2 Pruebas End-to-End Frontend (`npm run test:e2e` — Playwright)
Prueba automatizada del flujo completo exigido por la pauta: Login -> Crear Solicitud -> Listar Solicitud -> Ver Detalle.
```bash
> synctimefront@0.0.0 test:e2e
> playwright test

Running 1 test using 1 worker

  ok 1 [chromium] › e2e\flujo-solicitudes.spec.js:4:3 › Flujo E2E Obligatorio: Login -> Crear Solicitud -> Listar Solicitud › debe autenticarse, registrar una nueva solicitud y verificarla en la tabla (1.8s)

  1 passed (3.8s)
```

### 2.3 Suite de Integración del Backend (.NET 10 — Evaluación 1)
Verificación de que los endpoints protegidos y la suite de integración de ASP.NET Core continúan ejecutándose y pasando al 100%:
```bash
> dotnet run --project ../../Proyecto-Carteles-herrera-synctime-main/LetrerosHerreraSYNCTIMEapi.Tests

  Discovering: LetrerosHerreraSYNCTIMEapi.Tests
  Discovered:  LetrerosHerreraSYNCTIMEapi.Tests
  Starting:    LetrerosHerreraSYNCTIMEapi.Tests
  Finished:    LetrerosHerreraSYNCTIMEapi.Tests
=== TEST EXECUTION SUMMARY ===
   LetrerosHerreraSYNCTIMEapi.Tests  Total: 5, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 4,023s
```

### Detalle de Cobertura por Flujo:
1. **Flujo de Autenticación (`Login.test.jsx`):**
   - Renderizado con campos accesibles (`<label>`, inputs, botón de envío).
   - Bloqueo y mensajes de error en cliente ante campos requeridos vacíos.
   - Envío de credenciales y almacenamiento del token Bearer en `sessionStorage`.
   - Manejo de respuesta de error 401 del servidor con mensaje al usuario.
2. **Rutas Protegidas (`ProtectedRoute.test.jsx`):**
   - Bloqueo de usuarios sin sesión activa y redirección inmediata a `/login`.
   - Permiso de acceso al contenido interno cuando existe un token válido.
3. **Control de Acceso por Rol (`RoleGuard.test.jsx`):**
   - Renderizado condicional según rol (`dueno`, `disenador`, `operario`).
   - Ocultamiento de acciones no autorizadas (ej. botón eliminar solo para `dueno`).
   - Renderizado de interfaz de reemplazo (fallback) en caso de acceso no autorizado.
4. **CRUD Completo de Solicitudes (`SolicitudesCRUD.test.jsx` y `flujo-solicitudes.spec.js`):**
   - Obtención y visualización de la lista de solicitudes desde el backend.
   - Filtrado en vivo por texto de búsqueda (nombre, teléfono, material).
   - Apertura de modal con trampa de foco y consumo de clientes y productos.
   - Creación exitosa mediante `solicitudService.create`.
   - Visualización de la vista modal de detalle (Consultar).
   - Confirmación mediante modal accesible y eliminación mediante `solicitudService.remove`.

---

## 3. Evidencias de Seguridad OWASP en Código

### 3.1 Mitigación de XSS (Cross-Site Scripting)
- **Implementación:**
  - Cero ocurrencias de `dangerouslySetInnerHTML` en todo el proyecto.
  - React escapa automáticamente cualquier contenido dinámico como texto plano en el Virtual DOM.
  - Cabecera activa en `index.html`:
    ```html
    <meta http-equiv="Content-Security-Policy" content="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src https://fonts.gstatic.com; connect-src 'self' http://localhost:* https://localhost:*; img-src 'self' data:;" />
    ```

### 3.2 Mitigación de Robo de Token (Broken Authentication)
- **Implementación:**
  - El token se conserva únicamente en memoria de pestaña (`sessionStorage`), sin persistencia en disco ni compartición entre ventanas no relacionadas.
  - Al cerrar sesión (`logout`), se purga de inmediato:
    ```javascript
    sessionStorage.removeItem('synctime_token');
    sessionStorage.removeItem('synctime_user');
    ```
  - Si el backend responde `401 Unauthorized` por token expirado, el interceptor limpia el almacenamiento y redirige al login.

### 3.3 Mitigación de Consumo Sin Límites / Rate Limiting (429)
- **Implementación en `src/api/httpClient.js`:**
  ```javascript
  if (status === 429) {
    return Promise.reject({
      status: 429,
      message: 'Ha excedido el límite de peticiones. Espere un momento e intente de nuevo.',
      errors: {},
    });
  }
  ```

### 3.4 Control de Acceso Roto a Nivel de Función (API5)
- **Evidencia en Frontend:**
  Botón de eliminación envuelto en `<RoleGuard>`:
  ```jsx
  {hasRole('dueno') ? (
    <button title="Eliminar solicitud" onClick={() => setDeleteTarget(s)}>
      ...
    </button>
  ) : (
    <button disabled title="Solo el dueño puede eliminar">...</button>
  )}
  ```
### 3.5 Verificación de Política CORS Restringida a Orígenes Explícitos
- **Origen autorizado (`http://localhost:3000`):**
  ```bash
  > curl.exe -k -i -X OPTIONS https://localhost:7112/api/v1/solicitudes -H "Origin: http://localhost:3000" -H "Access-Control-Request-Method: GET"
  
  HTTP/1.1 204 No Content
  Access-Control-Allow-Methods: GET
  Access-Control-Allow-Origin: http://localhost:3000
  ```
- **Origen no autorizado (`http://evil.com`):**
  ```bash
  > curl.exe -k -i -X OPTIONS https://localhost:7112/api/v1/solicitudes -H "Origin: http://evil.com" -H "Access-Control-Request-Method: GET"
  
  HTTP/1.1 204 No Content
  # El servidor NO incluye el encabezado Access-Control-Allow-Origin, bloqueando el acceso en el navegador.
  ```

### 3.6 Verificación de Rechazo por Falta de Token (401 Unauthorized)
```bash
> curl.exe -k -i https://localhost:7112/api/v1/solicitudes

HTTP/1.1 401 Unauthorized
Date: Sun, 25 Oct 2026 21:57:42 GMT
Server: Kestrel
WWW-Authenticate: Bearer
```
El servidor rechaza peticiones anónimas y exige el token Bearer emitido en el login.

---

## 4. Evidencias de Accesibilidad (WCAG 2.2 Nivel AA)

### 4.1 Navegación Completa por Teclado
- **Skip Link:** Se incluye un enlace visible al pulsar Tab al inicio de la página (`Saltar al contenido principal`), permitiendo a usuarios con discapacidad motora saltarse la navegación lateral.
- **Trampa de Foco (Focus Trap):** En `SolicitudForm.jsx` y `ConfirmModal.jsx`:
  - Al abrirse, el foco se coloca automáticamente en el primer elemento interactivo.
  - Pulsar `Tab` en el último botón redirige el foco al primero; pulsar `Shift+Tab` en el primer botón redirige al último.
  - Pulsar `Escape` cierra la ventana modal y devuelve el control al usuario.

### 4.2 Formularios Accesibles
- Cada control posee su etiqueta `<label htmlFor="field-nombre">`.
- Estados de error enlazados semánticamente:
  ```jsx
  <input
    id="field-telefonoContacto"
    className={`form-input ${errors.telefonoContacto ? 'is-invalid' : ''}`}
    aria-invalid={Boolean(errors.telefonoContacto)}
    aria-describedby={errors.telefonoContacto ? 'error-telefonoContacto' : undefined}
  />
  {errors.telefonoContacto && (
    <span id="error-telefonoContacto" className="form-error" role="alert">
      {errors.telefonoContacto}
    </span>
  )}
  ```

### 4.3 Región Live para Lectores de Pantalla
- Región con atributo `aria-live="polite"` y clase `.sr-only` que anuncia adiciones, ediciones o eliminaciones en tiempo real sin interrumpir la lectura del usuario.

### 4.4 Relación de Contraste y Diseño Mobile-First
- Fondo `#f8fafc` contra texto `#0f172a`: relación de contraste **16.2:1** (ampliamente superior a la exigencia de 4.5:1).
- Botones de acción principales: contraste superior a **5.8:1**.
- Tamaño mínimo de botón y objetivo táctil: **44 × 44 px**, evitando errores de toque en pantallas de 320 px a 390 px.
- Menú hamburguesa accesible con botón de apertura (`aria-expanded`, `aria-label`).
