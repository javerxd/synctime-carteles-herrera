import { test, expect } from '@playwright/test';

test.describe('Flujo E2E Obligatorio: Login -> Crear Solicitud -> Listar Solicitud', () => {
  test('debe autenticarse, registrar una nueva solicitud y verificarla en la tabla', async ({ page }) => {
    // Estado en memoria para simular backend persistente durante la prueba E2E
    let solicitudesList = [
      {
        idSolicitud: 1,
        idCliente: 1,
        idProducto: 1,
        cantidad: 2,
        telefonoContacto: '+56987654321',
        materialSolicitado: 'Acrílico blanco',
        tamano: '1m x 0.5m',
        notasWhatsapp: 'Solicitud previa existente',
        fechaSolicitud: new Date().toISOString(),
        fechaDecision: null,
        estado: 'pendiente',
      },
    ];

    const mockClientes = [
      {
        idCliente: 1,
        nombre: 'Empresa Alpha SpA',
        telefono: '+56911223344',
        email: 'contacto@alpha.cl',
        rut: '76.123.456-7',
      },
      {
        idCliente: 2,
        nombre: 'Comercial Beta Ltda',
        telefono: '+56999887766',
        email: 'ventas@beta.cl',
        rut: '77.654.321-0',
      },
    ];

    const mockProductos = [
      {
        idProducto: 1,
        nombre: 'Letrero Neón LED',
        precioBase: 120000,
        tipo: 'producto',
        unidadMedida: 'unidad',
      },
      {
        idProducto: 2,
        nombre: 'Corte Láser Acrílico',
        precioBase: 45000,
        tipo: 'servicio',
        unidadMedida: 'metro lineal',
      },
    ];

    // Mock de rutas API
    await page.route('**/api/v1/auth/login', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          token: 'mocked-jwt-token-playwright',
          usuario: {
            idUsuario: 1,
            email: 'admin@letrerosherrera.cl',
            nombre: 'Administrador Dueño',
            rol: 'dueno',
          },
        }),
      });
    });

    await page.route('**/api/v1/clientes', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockClientes),
      });
    });

    await page.route('**/api/v1/productos', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockProductos),
      });
    });

    await page.route('**/api/v1/solicitudes/**', async (route) => {
      const request = route.request();
      if (request.method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(solicitudesList),
        });
      } else {
        await route.continue();
      }
    });

    await page.route('**/api/v1/solicitudes', async (route) => {
      const request = route.request();
      if (request.method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(solicitudesList),
        });
      } else if (request.method() === 'POST') {
        const postData = JSON.parse(request.postData() || '{}');
        const nuevaSolicitud = {
          idSolicitud: 102,
          idCliente: Number(postData.idCliente),
          idProducto: Number(postData.idProducto),
          cantidad: Number(postData.cantidad),
          telefonoContacto: postData.telefonoContacto,
          tamano: postData.tamano || '2m x 1m',
          materialSolicitado: postData.materialSolicitado || 'Acrílico LED Retroiluminado',
          notasWhatsapp: postData.notasWhatsapp || 'Entrega prioritaria',
          fechaSolicitud: new Date().toISOString(),
          fechaDecision: null,
          estado: 'pendiente',
        };
        solicitudesList = [nuevaSolicitud, ...solicitudesList];
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(nuevaSolicitud),
        });
      } else {
        await route.continue();
      }
    });

    // 1. Navegar al Login
    await page.goto('/login');
    await expect(page.getByRole('heading', { name: 'SyncTime' })).toBeVisible();

    // 2. Iniciar Sesión
    await page.fill('#login-email', 'admin@letrerosherrera.cl');
    await page.fill('#login-password', 'Password123!');
    await page.click('button[type="submit"]');

    // 3. Confirmar llegada al Dashboard
    await page.waitForURL('**/dashboard');
    await expect(page.locator('.sidebar-user')).toContainText('Administrador Dueño');

    // 4. Navegar a Solicitudes
    await page.click('a[href="/solicitudes"]');
    await page.waitForURL('**/solicitudes');
    await expect(page.locator('.page-header h1')).toContainText('Solicitudes');

    // 5. Abrir formulario de creación
    const createBtn = page.locator('button:has-text("Nueva solicitud")');
    await expect(createBtn).toBeVisible();
    await createBtn.click();

    // 6. Verificar apertura de modal de creación
    const modal = page.locator('div[role="dialog"]');
    await expect(modal).toBeVisible();
    await expect(modal.locator('h2')).toContainText('Nueva Solicitud');

    // 7. Completar los campos requeridos
    await page.selectOption('#field-idCliente', '1');
    await page.selectOption('#field-idProducto', '1');
    await page.fill('#field-cantidad', '5');
    await page.fill('#field-telefonoContacto', '+56912345678');
    await page.fill('#field-tamano', '2.5m x 1.2m');
    await page.fill('#field-materialSolicitado', 'Acrílico LED Retroiluminado');
    await page.fill('#field-notasWhatsapp', 'Logotipo en relieve 3D con luz cálida');

    // 8. Enviar formulario
    await page.click('button:has-text("Crear Solicitud")');

    // 9. Verificar que el modal se cierra y la solicitud aparece en la tabla
    await expect(modal).not.toBeVisible();

    // Comprobar que la nueva solicitud #102 aparece listada en la tabla
    const row = page.locator('tr:has-text("#102")');
    await expect(row).toBeVisible();
    await expect(row.locator('td[data-label="Cliente"]')).toContainText('Empresa Alpha SpA');
    await expect(row.locator('td[data-label="Producto"]')).toContainText('Letrero Neón LED');
    await expect(row.locator('td[data-label="Cantidad"]')).toContainText('5');
    await expect(row.locator('td[data-label="Teléfono"]')).toContainText('+56912345678');
    await expect(row.locator('td[data-label="Estado"]')).toContainText('pendiente');

    // 10. Probar Modal de Detalle (Consultar)
    const detailBtn = row.locator('button[title="Ver detalle"]');
    await expect(detailBtn).toBeVisible();
    await detailBtn.click();

    const detailModal = page.locator('div[role="dialog"]');
    await expect(detailModal).toBeVisible();
    await expect(detailModal.locator('h2')).toContainText('Detalle de Solicitud #102');
    await expect(detailModal).toContainText('Acrílico LED Retroiluminado');
    await expect(detailModal).toContainText('2.5m x 1.2m');
    await expect(detailModal).toContainText('Logotipo en relieve 3D con luz cálida');

    // Cerrar modal con tecla Escape (WCAG 2.2)
    await page.keyboard.press('Escape');
    await expect(detailModal).not.toBeVisible();
  });
});
