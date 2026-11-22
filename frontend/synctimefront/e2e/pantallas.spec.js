import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { trabajo } from './reportesFalsos.js';

// Recorrido de las pantallas de la revisión (fase 3) con la API simulada (e2e/apiSimulada.js, compartida desde la fase 15B): así se
// prueban sin la contraseña de la base demo. Los datos imitan la semilla de demostración.
import {
  paginaDe,
  POR_COBRAR,
  RESPUESTAS,
  COMPROBANTE,
  FICHA,
  CLIENTES,
  HOY_CHILE,
  diaChile,
  cotizacionDe,
  COT_PENDIENTE,
  COT_APROBADA,
  FICHA_PEDIDO,
  CALENDARIO,
  PERSONAL,
  PUBLICACION,
  PUBLICACION_LOGO,
  DESTINO_ACTIVO,
  AVISOS_TALLER,
  IMAGEN_PRUEBA,
  tokenDePrueba,
  destinoDe,
  CLIENTE,
  MI_CUENTA,
  entrarComo,
  entrarComoCliente,
} from './apiSimulada.js';
import { ESENCIALES, PANTALLAS, prepararApi } from './rutas.js';


for (const ancho of [360, 768, 1440]) {
  for (const [ruta, titulo] of PANTALLAS) {
    test(`${ruta} a ${ancho} px: carga, sin desborde ni fallas graves de accesibilidad${ESENCIALES.has(ruta) ? ' @esencial' : ''}`, async ({ page }) => {
      const errores = [];
      page.on('console', (m) => m.type() === 'error' && errores.push(m.text()));
      await page.setViewportSize({ width: ancho, height: 900 });
      await prepararApi(page, ruta);
      await page.goto(ruta);
      await expect(page.getByRole('heading', { level: 1, name: titulo })).toBeVisible();
      await page.waitForLoadState('networkidle');
      expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
      const { violations } = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
      expect(violations.filter((v) => ['critical', 'serious'].includes(v.impact)).map((v) => `${v.id}: ${v.nodes[0]?.target}`)).toEqual([]);
      expect(errores).toEqual([]);
      if (process.env.CAPTURAS) await page.screenshot({ path: `${process.env.CAPTURAS}/${ruta.replace(/[/?=&]/g, '_')}-${ancho}.png`, fullPage: true });
    });
  }
}

test('un operario ve su menú sin Gestión y recibe la página 403 en Indicadores', async ({ page }) => {
  await entrarComo(page, 'operario');
  await page.goto('/panel');
  await expect(page.getByRole('navigation', { name: 'Panel del taller' }).getByRole('link', { name: /Indicadores/ })).toHaveCount(0);
  await page.goto('/panel/indicadores');
  await expect(page.getByRole('heading', { name: 'No tienes permiso para ver esta página' })).toBeVisible();
});

// Fases 12A y 12B: el dashboard contra la API simulada (formato del contrato), a 360 px.
test('dashboard: el anillo filtra la tabla, cada gráfico tiene su tabla y el saldo cambia de alcance @esencial', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });
  await entrarComo(page, 'dueño');
  await page.goto('/panel/indicadores');
  await expect(page.getByText('Datos de demostración')).toHaveCount(0);

  const resumen = page.getByRole('region', { name: 'Resumen del período' });
  await expect(resumen.getByText('Saldo del período')).toBeVisible();
  await resumen.getByRole('button', { name: 'Ver toda la deuda vigente' }).click();
  await expect(resumen.getByText('Toda la deuda vigente')).toBeVisible();
  await expect(page).toHaveURL(/alcanceSaldo=vigente/);

  const cobranza = page.getByRole('region', { name: 'Cobranza' });
  await cobranza.getByRole('button', { name: /^No pagado/ }).click();
  await expect(page).toHaveURL(/cobro=no_pagado/);
  const tabla = page.getByRole('region', { name: 'Pedidos por cobrar' });
  await expect(tabla.getByRole('button', { name: /Quitar el filtro por estado de pago/ })).toBeVisible();

  const evolucion = page.getByRole('region', { name: 'Evolución comercial' });
  await evolucion.getByRole('button', { name: 'Tabla de datos' }).click();
  await expect(evolucion.getByRole('table')).toBeVisible();
  // Mientras un bloque se recarga se ve atenuado: se mide cuando todos terminaron y pasó la transición.
  await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);
  await page.waitForTimeout(300);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
  const { violations } = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(violations.filter((v) => ['critical', 'serious'].includes(v.impact)).map((v) => `${v.id}: ${v.nodes[0]?.target}`)).toEqual([]);
});

test('sin sesión, el panel lleva al acceso del personal', async ({ page }) => {
  await page.goto('/panel/pedidos');
  await expect(page.getByRole('heading', { name: 'Acceso del personal' })).toBeVisible();
});

test('el código QR del portal se dibuja y se puede descargar', async ({ page }) => {
  await entrarComo(page, 'dueño');
  await page.goto('/panel/local');
  await expect(page.getByRole('img', { name: /Código QR que abre/ })).toBeVisible();
  const descarga = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Descargar SVG' }).click();
  expect((await descarga).suggestedFilename()).toBe('qr-letreria-los-herrera.svg');
});

test('dar vuelta el cartel: el error enfoca el mensaje y al guardar el foco no se pierde', async ({ page }) => {
  await entrarComo(page, 'dueño');
  await page.goto('/panel/local');
  const mensaje = page.getByRole('textbox', { name: 'Mensaje para los clientes' });
  await mensaje.fill('');
  const boton = page.getByRole('button', { name: 'Dar vuelta el cartel' });
  await boton.click();
  await expect(mensaje).toBeFocused();
  await expect(page.getByText('Escribe el mensaje que verán los clientes.')).toBeVisible();
  await mensaje.fill('Volvemos pronto');
  await boton.click();
  await expect(page.getByText('El cartel dice Cerrado').first()).toBeVisible();
  await expect(boton).toBeFocused();
  await expect(mensaje).toHaveValue('Volvemos pronto');
});

test('desde otra página, «Horario» lleva a la portada y baja hasta la sección', async ({ page }) => {
  await page.route('**/api/v1/**', (ruta) => {
    const destino = new URL(ruta.request().url()).pathname.replace(/\/$/, '');
    return ruta.fulfill({ json: RESPUESTAS[destino] ?? [] });
  });
  await page.setViewportSize({ width: 1440, height: 700 });
  await page.goto('/ingresar');
  await page.getByRole('navigation').getByRole('link', { name: 'Horario' }).first().click();
  await expect(page).toHaveURL(/\/#horario$/);
  await expect(page.locator('#horario')).toBeInViewport();
});

// ── Fase 5 ──

function simularApiPublica(page) {
  return page.route('**/api/v1/**', (ruta) =>
    ruta.fulfill({ status: ruta.request().method() === 'POST' ? 202 : 200, json: RESPUESTAS[new URL(ruta.request().url()).pathname.replace(/\/$/, '')] ?? [] }));
}

test('la portada muestra el estado del local, la vitrina con su imagen de ejemplo y el horario en hora de Chile @esencial', async ({ page }) => {
  await simularApiPublica(page);
  await page.goto('/');
  const mostrador = page.getByRole('complementary', { name: 'En el taller ahora' });
  await expect(mostrador.getByText('Salimos a una instalación')).toBeVisible();
  await expect(mostrador.getByText('16:30')).toBeVisible();
  const vitrina = page.locator('#vitrina');
  await expect(vitrina.getByRole('article', { name: 'Letrero luminoso para panadería' }).locator('img')).toHaveAttribute('loading', 'lazy');
  await expect(vitrina.getByText('Imagen de ejemplo')).toBeVisible();
  await expect(page.getByText(/Hora de Chile\. Ahora son las 11:30/)).toBeVisible();
});

test('el aviso de cookies ofrece dos opciones iguales y no vuelve tras decidir @esencial', async ({ page }) => {
  await simularApiPublica(page);
  await page.goto('/');
  const aviso = page.getByRole('region', { name: 'Cookies y privacidad' });
  await expect(aviso.getByRole('checkbox', { name: /analítica/ })).not.toBeChecked();
  const rechazar = aviso.getByRole('button', { name: 'Rechazar opcionales' });
  const guardar = aviso.getByRole('button', { name: 'Guardar mi elección' });
  const cajaR = await rechazar.boundingBox();
  const cajaG = await guardar.boundingBox();
  expect(Math.abs(cajaR.width - cajaG.width)).toBeLessThan(2);
  expect(Math.abs(cajaR.height - cajaG.height)).toBeLessThan(2);
  await rechazar.click();
  await expect(aviso).toBeHidden();
  await page.reload();
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Cookies y privacidad' })).toHaveCount(0);
});

test('el formulario de contacto valida, exige la privacidad y confirma el envío', async ({ page }) => {
  await simularApiPublica(page);
  await page.addInitScript(() => localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false })));
  // Fase 12C: el formulario vive en /consultas; el enlace antiguo /#contacto lleva ahí.
  await page.goto('/#contacto');
  await expect(page).toHaveURL(/\/consultas$/);
  const formulario = page.locator('#contenido');
  await formulario.getByRole('button', { name: 'Enviar consulta' }).click();
  await expect(formulario.getByRole('textbox', { name: /Nombre/ })).toBeFocused();
  await formulario.getByRole('textbox', { name: /Nombre/ }).fill('Pedro Soto');
  await formulario.getByRole('textbox', { name: /Correo/ }).fill('pedro@correo.cl');
  await formulario.getByRole('textbox', { name: /Asunto/ }).fill('Letrero para mi local');
  await formulario.getByRole('textbox', { name: /Mensaje/ }).fill('Quisiera cotizar un letrero luminoso de dos metros.');
  await formulario.getByRole('button', { name: 'Enviar consulta' }).click();
  await expect(formulario.getByRole('checkbox', { name: /política de privacidad/ })).toHaveAttribute('aria-invalid', 'true');
  await formulario.getByRole('checkbox', { name: /política de privacidad/ }).check();
  const envio = page.waitForRequest((r) => r.url().endsWith('/api/v1/contacto') && r.method() === 'POST');
  await formulario.getByRole('button', { name: 'Enviar consulta' }).click();
  expect((await envio).postDataJSON()).toMatchObject({ aceptaPrivacidad: true, versionPrivacidad: '1.0', sitioWeb: '' });
  await expect(formulario.getByText('Consulta enviada')).toBeVisible();
});

test('el mapa no contacta a OpenStreetMap ni a Google hasta que el visitante lo activa', async ({ page }) => {
  const osm = [];
  const google = [];
  page.on('request', (r) => {
    if (r.url().includes('tile.openstreetmap.org')) osm.push(r.url());
    if (/google\.com|gstatic\.com|googleapis\.com/.test(r.url())) google.push(r.url());
  });
  await simularApiPublica(page);
  await page.route('https://tile.openstreetmap.org/**', (r) => r.fulfill({ status: 204 }));
  await page.route(/https:\/\/www\.google\.com\/maps\/embed.*/, (r) => r.fulfill({ status: 200, contentType: 'text/html', body: '<p>Street View</p>' }));
  await page.addInitScript(() => localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false })));
  await page.goto('/#ubicacion');
  await expect(page.getByRole('img', { name: /Mapa de Villagrán 810/ })).toBeVisible();
  await expect(page.locator('#ubicacion').getByRole('link', { name: /Abrir en Google Maps/ })).toHaveAttribute('href', 'https://maps.app.goo.gl/HzJtEy3jetsBmztNA');
  await expect(page.getByText(/aproximada/)).toHaveCount(0);
  await page.waitForLoadState('networkidle');
  expect(osm).toEqual([]);
  expect(google).toEqual([]);

  await page.getByRole('button', { name: 'Mapa interactivo' }).click();
  await expect(page.locator('.leaflet-container')).toBeVisible();
  await expect.poll(() => osm.length).toBeGreaterThan(0);
  expect(google).toEqual([]);

  await page.getByRole('button', { name: 'Ver la fachada' }).click();
  await expect(page.locator('iframe[title*="Street View"]')).toBeVisible();
  await expect.poll(() => google.length).toBeGreaterThan(0);
});

test('los documentos legales muestran su versión y llevan al apartado pedido', async ({ page }) => {
  await simularApiPublica(page);
  await page.addInitScript(() => localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false })));
  await page.goto('/privacidad#cookies');
  await expect(page.getByText(/Versión 1\.0/)).toBeVisible();
  await expect(page.locator('#cookies')).toBeInViewport();
});

// ── Fase 6: cuentas y sesión ──

test('el token no queda en el almacenamiento del navegador y la sesión se recupera al recargar @esencial', async ({ page }) => {
  await entrarComo(page, 'dueño');
  await page.goto('/panel');
  await expect(page.getByRole('heading', { level: 1, name: 'Hola, Marta' })).toBeVisible();
  const almacenado = await page.evaluate(() => JSON.stringify({ ...sessionStorage, ...localStorage }));
  expect(almacenado).not.toContain('cabecera.');
  await page.reload();
  await expect(page.getByRole('heading', { level: 1, name: 'Hola, Marta' })).toBeVisible();
});

test('salir del panel avisa a la API y vuelve al acceso del personal', async ({ page }) => {
  await entrarComo(page, 'dueño');
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/panel');
  const salida = page.waitForRequest((r) => r.url().endsWith('/api/v1/auth/salir') && r.method() === 'POST');
  await page.getByRole('button', { name: 'Cerrar sesión' }).click();
  await salida;
  await expect(page.getByRole('heading', { name: 'Acceso del personal' })).toBeVisible();
});

test('sin sesión, Mi cuenta lleva al ingreso de clientes; el acceso del personal enlaza a su recuperación', async ({ page }) => {
  await simularApiPublica(page);
  await page.goto('/cuenta');
  // Fase 12C: la guarda lleva al selector «¿Quién está iniciando sesión?»; el cliente elige su tarjeta.
  await expect(page.getByRole('heading', { level: 1, name: '¿Quién está iniciando sesión?' })).toBeVisible();
  await page.getByRole('main').getByRole('link', { name: /Cliente/ }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Ingresar a tu cuenta' })).toBeVisible();
  await expect(page.getByRole('button', { name: /rostro/i })).toHaveCount(0);
  await page.goto('/personal/ingresar');
  await expect(page.getByRole('link', { name: '¿Olvidaste tu contraseña?' })).toHaveAttribute('href', '/cuenta/recuperar?tipo=personal');
});

test('registro: casillas desmarcadas, la obligatoria bloquea el envío y al crear la cuenta se llega a Mi cuenta @esencial', async ({ page }) => {
  let registro = null;
  await page.route('**/api/v1/**', (ruta) => {
    const destino = destinoDe(ruta);
    if (destino === '/api/v1/auth/registro-cliente') {
      registro = ruta.request().postDataJSON();
      return ruta.fulfill({ status: 201, json: { token: tokenDePrueba(), tipo: 'cliente', usuario: CLIENTE } });
    }
    if (destino === '/api/v1/mi-cuenta') return ruta.fulfill({ json: MI_CUENTA });
    return ruta.fulfill({ json: RESPUESTAS[destino] ?? [] });
  });
  await page.addInitScript(() => localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false })));
  await page.goto('/ingresar/cliente');
  await page.getByRole('link', { name: 'Crear cuenta' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Crear cuenta' })).toBeVisible();

  const terminos = page.getByRole('checkbox', { name: 'Leí y acepto los términos y condiciones y la política de privacidad' });
  const marketing = page.getByRole('checkbox', { name: 'Quiero recibir novedades y promociones' });
  await expect(terminos).not.toBeChecked();
  await expect(marketing).not.toBeChecked();

  await page.getByRole('textbox', { name: /^Nombre/ }).fill('Rosa');
  await page.getByRole('textbox', { name: /^Apellido/ }).fill('Pino');
  await page.getByRole('textbox', { name: /^Teléfono/ }).fill('+56 9 1234 5678');
  await page.getByRole('textbox', { name: /^Correo electrónico/ }).fill('rosa@correo.test');
  await page.getByLabel(/^Contraseña/).fill('una-clave-larga');
  await page.getByRole('button', { name: 'Crear cuenta' }).click();
  await expect(terminos).toHaveAttribute('aria-invalid', 'true');
  await expect(terminos).toBeFocused();
  expect(registro).toBeNull();

  await terminos.check();
  await page.getByRole('button', { name: 'Crear cuenta' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Mi cuenta' })).toBeVisible();
  await expect(page.getByText('Tu cuenta está lista')).toBeVisible();
  await expect(page.getByText('Confirma tu correo')).toBeVisible();
  expect(registro).toMatchObject({
    correo: 'rosa@correo.test', aceptaTerminosYPrivacidad: true, aceptaMarketing: false, versionTerminos: '1.0', versionPrivacidad: '1.0',
  });
  // En el teléfono (menos de 64 rem) el menú vive dentro del cajón, cerrado: la página actual se comprueba solo con el menú lateral a la vista.
  if ((page.viewportSize()?.width ?? 1280) >= 1024) {
    await expect(page.getByRole('navigation', { name: 'Mi cuenta' }).getByRole('link', { name: 'Mi cuenta' })).toHaveAttribute('aria-current', 'page');
  }
});

test('recuperación en dos pasos: pide el código, cambia la contraseña y vuelve al ingreso con un aviso', async ({ page }) => {
  const pedidos = [];
  await page.route('**/api/v1/**', (ruta) => {
    const destino = destinoDe(ruta);
    if (destino === '/api/v1/auth/recuperar') {
      pedidos.push(ruta.request().postDataJSON());
      return ruta.fulfill({ status: 202, json: { mensaje: 'Si el correo tiene una cuenta, te enviamos un código. Vence en 15 minutos.' } });
    }
    if (destino === '/api/v1/auth/restablecer') {
      const cuerpo = ruta.request().postDataJSON();
      pedidos.push(cuerpo);
      const mensaje = 'El código no es válido o ya venció. Pide uno nuevo.';
      return cuerpo.codigo === '123456'
        ? ruta.fulfill({ status: 204 })
        : ruta.fulfill({ status: 400, json: { error: mensaje, codigo: 'datos_invalidos', nivel: 'api', errores: { codigo: [mensaje] } } });
    }
    return ruta.fulfill({ json: RESPUESTAS[destino] ?? [] });
  });
  await page.goto('/ingresar/cliente');
  await page.getByRole('link', { name: '¿Olvidaste tu contraseña?' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Recuperar tu contraseña' })).toBeVisible();
  await page.getByRole('textbox', { name: 'Correo electrónico' }).fill('rosa@correo.test');
  await page.getByRole('button', { name: 'Enviar código' }).click();

  await expect(page.getByRole('heading', { name: 'Escribe el código' })).toBeFocused();
  await expect(page.getByText('Si el correo tiene una cuenta, te enviamos un código. Vence en 15 minutos.')).toBeVisible();
  const codigo = page.getByRole('textbox', { name: /Código de 6 dígitos/ });
  await codigo.fill('000000');
  await page.getByLabel(/^Contraseña nueva/).fill('nueva-clave-1');
  await page.getByLabel(/^Repite la contraseña nueva/).fill('nueva-clave-1');
  await page.getByRole('button', { name: 'Cambiar contraseña' }).click();
  await expect(codigo).toHaveAttribute('aria-invalid', 'true');
  await expect(codigo).toBeFocused();

  await codigo.fill('123456');
  await page.getByRole('button', { name: 'Cambiar contraseña' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Ingresar a tu cuenta' })).toBeVisible();
  await expect(page.getByText('Tu contraseña se cambió. Ingresa con la nueva.')).toBeVisible();
  expect(pedidos[0]).toEqual({ tipo: 'cliente', correo: 'rosa@correo.test' });
  expect(pedidos[2]).toEqual({ tipo: 'cliente', correo: 'rosa@correo.test', codigo: '123456', password: 'nueva-clave-1' });
});

test('verificar el correo: llama una sola vez a la API y lo confirma', async ({ page }) => {
  let llamadas = 0;
  await page.route('**/api/v1/**', (ruta) => {
    const destino = destinoDe(ruta);
    if (destino === '/api/v1/auth/verificar-correo') {
      llamadas += 1;
      return ruta.fulfill({ json: { verificado: true } });
    }
    return ruta.fulfill({ json: RESPUESTAS[destino] ?? [] });
  });
  await page.goto('/cuenta/verificar?token=enlace-de-prueba');
  await expect(page.getByRole('heading', { level: 1, name: 'Tu correo está confirmado' })).toBeVisible();
  await page.waitForLoadState('networkidle');
  expect(llamadas).toBe(1);
  const { violations } = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(violations.filter((v) => ['critical', 'serious'].includes(v.impact)).map((v) => v.id)).toEqual([]);
  if (process.env.CAPTURAS) {
    for (const ancho of [360, 1440]) {
      await page.setViewportSize({ width: ancho, height: 900 });
      await page.screenshot({ path: `${process.env.CAPTURAS}/_cuenta_verificar-${ancho}.png`, fullPage: true });
    }
  }
});

// ── Fase 7: flujos de solicitudes, adjuntos y clientes ──

const PNG_1X1 = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==', 'base64');
const TEXTO_SOLICITUD = 'Letrero para la fachada de la panadería, con el horario abajo.';

/** Llena el primer letrero y la descripción del formulario del cliente. */
async function llenarSolicitud(page) {
  await page.getByRole('combobox', { name: /Tipo de letrero/ }).selectOption('1');
  await page.getByRole('textbox', { name: /^Ancho/ }).fill('120');
  await page.getByRole('textbox', { name: /^Alto/ }).fill('60');
  await page.getByRole('textbox', { name: /^Cantidad/ }).fill('2');
  await page.getByRole('textbox', { name: /^Descripción/ }).fill(TEXTO_SOLICITUD);
}

test('sin sesión, «Solicitar cotización» pasa por el ingreso y vuelve al formulario @esencial', async ({ page }) => {
  await page.route('**/api/v1/**', (ruta) => {
    const destino = destinoDe(ruta);
    if (destino === '/api/v1/auth/login-cliente') {
      return ruta.fulfill({ json: { token: tokenDePrueba(), tipo: 'cliente', usuario: { ...CLIENTE, correoVerificado: true } } });
    }
    if (destino === '/api/v1/mi-cuenta') return ruta.fulfill({ json: { ...MI_CUENTA, correoVerificado: true } });
    return ruta.fulfill({ json: RESPUESTAS[destino] ?? [] });
  });
  await page.addInitScript(() => localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false })));
  await page.goto('/');
  await page.getByRole('main').getByRole('link', { name: 'Solicitar cotización' }).first().click();
  // El selector conserva adónde volver: al elegir «Cliente» y entrar, termina en el formulario.
  await page.getByRole('main').getByRole('link', { name: /Cliente/ }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Ingresar a tu cuenta' })).toBeVisible();
  await page.getByRole('textbox', { name: 'Correo electrónico' }).fill('rosa@correo.test');
  await page.getByLabel(/^Contraseña/).fill('clave-de-prueba');
  await page.getByRole('button', { name: 'Ingresar' }).click();
  await expect(page).toHaveURL(/\/cuenta\/solicitud$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Solicitar una cotización' })).toBeVisible();
});

test('«Cotizar uno similar» abre el formulario con el tipo de letrero elegido', async ({ page }) => {
  await entrarComoCliente(page, { verificado: true });
  await page.addInitScript(() => localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false })));
  await page.goto('/#vitrina');
  await page.getByRole('link', { name: /Cotizar uno similar a Letrero luminoso para panadería/ }).click();
  await expect(page).toHaveURL(/\/cuenta\/solicitud\?producto=1&referencia=/);
  await expect(page.getByRole('combobox', { name: /Tipo de letrero/ })).toHaveValue('1');
  await expect(page.getByText('Trabajo de referencia:')).toBeVisible();
});

test('solicitud en línea con un adjunto, de punta a punta, termina en el comprobante @esencial', async ({ page }) => {
  await entrarComoCliente(page, { verificado: true });
  const subidas = [];
  let enviada = null;
  await page.route('**/api/v1/mis/adjuntos', (ruta) => {
    if (ruta.request().method() !== 'POST') return ruta.fallback();
    subidas.push(ruta.request().headers()['content-type']);
    return ruta.fulfill({ status: 201, json: { idAdjunto: 41, tipo: 'imagen', nombre: 'logo.png', bytes: PNG_1X1.length, ancho: 1, alto: 1, urlMiniatura: '/api/v1/adjuntos/41/miniatura' } });
  });
  await page.route('**/api/v1/mis/solicitudes', (ruta) => {
    if (ruta.request().method() !== 'POST') return ruta.fallback();
    enviada = ruta.request().postDataJSON();
    return ruta.fulfill({ status: 201, json: { ...COMPROBANTE, adjuntos: [{ idAdjunto: 41, tipo: 'imagen', nombre: 'logo.png', bytes: 70, urlMiniatura: '/api/v1/adjuntos/41/miniatura' }] } });
  });
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto('/cuenta/solicitud');
  await llenarSolicitud(page);
  await expect(page.getByText('1,44 m²').first()).toBeVisible();

  // Un SVG se rechaza en el navegador, sin subirlo; el PNG se sube y queda con su «×».
  await page.locator('input[type=file]').setInputFiles([
    { name: 'logo.svg', mimeType: 'image/svg+xml', buffer: Buffer.from('<svg/>') },
    { name: 'logo.png', mimeType: 'image/png', buffer: PNG_1X1 },
  ]);
  await expect(page.getByText(/expórtalo a PDF o PNG/).first()).toBeVisible();
  const quitar = page.getByRole('button', { name: 'Quitar logo.png' });
  await expect(quitar).toBeVisible();
  await expect(page.getByText('1 de 5', { exact: true })).toBeVisible();
  const caja = await quitar.boundingBox();
  expect(caja.width).toBeGreaterThanOrEqual(24);
  expect(caja.height).toBeGreaterThanOrEqual(24);
  expect(subidas).toHaveLength(1);
  expect(subidas[0]).toMatch(/^multipart\/form-data/);
  if (process.env.CAPTURAS) await page.screenshot({ path: `${process.env.CAPTURAS}/_cuenta_solicitud_adjunto-360.png`, fullPage: true });

  await page.getByRole('button', { name: 'Enviar solicitud' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Recibimos tu solicitud #123' })).toBeFocused();
  expect(enviada).toEqual({
    descripcionGeneral: TEXTO_SOLICITUD,
    detalles: [{ idProducto: 1, descripcion: null, anchoMm: 1200, altoMm: 600, cantidad: 2 }],
    adjuntos: [41],
  });
  await expect(page.getByRole('button', { name: 'Ver logo.png en grande' }).locator('img')).toHaveAttribute('src', /^blob:/);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
  const { violations } = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(violations.filter((v) => ['critical', 'serious'].includes(v.impact)).map((v) => v.id)).toEqual([]);
  if (process.env.CAPTURAS) await page.screenshot({ path: `${process.env.CAPTURAS}/_cuenta_solicitud_comprobante-360.png`, fullPage: true });
});

test('si el servidor está ocupado (503), reintenta una vez tras Retry-After y termina en el comprobante', async ({ page }) => {
  await entrarComoCliente(page, { verificado: true });
  let intentos = 0;
  await page.route('**/api/v1/mis/solicitudes', (ruta) => {
    if (ruta.request().method() !== 'POST') return ruta.fallback();
    intentos += 1;
    return intentos === 1
      ? ruta.fulfill({ status: 503, headers: { 'Retry-After': '1' }, json: { error: 'Ocupado', codigo: 'servicio_ocupado', nivel: 'api' } })
      : ruta.fulfill({ status: 201, json: COMPROBANTE });
  });
  await page.goto('/cuenta/solicitud');
  await llenarSolicitud(page);
  await page.getByRole('button', { name: 'Enviar solicitud' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Recibimos tu solicitud #123' })).toBeVisible();
  expect(intentos).toBe(2);
});

test('si sigue ocupado tras el reintento, lo explica sin perder lo escrito', async ({ page }) => {
  await entrarComoCliente(page, { verificado: true });
  await page.route('**/api/v1/mis/solicitudes', (ruta) => (ruta.request().method() === 'POST'
    ? ruta.fulfill({ status: 503, headers: { 'Retry-After': '0' }, json: { error: 'Ocupado', codigo: 'servicio_ocupado', nivel: 'api' } })
    : ruta.fallback()));
  await page.goto('/cuenta/solicitud');
  await llenarSolicitud(page);
  await page.getByRole('button', { name: 'Enviar solicitud' }).click();
  await expect(page.getByText('Estamos recibiendo muchas solicitudes; vuelve a intentarlo en unos segundos.')).toBeVisible();
  await expect(page.getByRole('textbox', { name: /^Descripción/ })).toHaveValue(TEXTO_SOLICITUD);
});

test('contacto directo: la regla contra solicitudes falsas muestra cómo contactarnos sin perder lo escrito', async ({ page }) => {
  await entrarComoCliente(page, { verificado: true });
  const mensaje = 'Para esta solicitud es preferible contactarse directamente con el local.';
  await page.route('**/api/v1/mis/solicitudes', (ruta) => (ruta.request().method() === 'POST'
    ? ruta.fulfill({ status: 409, json: { error: mensaje, codigo: 'contacto_directo', nivel: 'negocio' } })
    : ruta.fallback()));
  await page.goto('/cuenta/solicitud');
  await llenarSolicitud(page);
  await page.getByRole('button', { name: 'Enviar solicitud' }).click();
  await expect(page.getByText('Para esta solicitud es preferible contactarse directamente con el local')).toBeVisible();
  await expect(page.getByText(/Escríbenos por WhatsApp o llámanos/)).toBeVisible();
  await expect(page.getByText(/Hoy: 9:00 a 18:30/)).toBeVisible();
  await expect(page.getByRole('main').getByText('Cerrado / Ausente')).toBeVisible();
  await expect(page.getByRole('main').getByRole('link', { name: /Escribir por WhatsApp/ })).toHaveAttribute('href', /^https:\/\/wa\.me\//);
  await expect(page.getByRole('link', { name: /^Llamar al/ })).toHaveAttribute('href', /^tel:/);
  await expect(page.getByRole('textbox', { name: /^Descripción/ })).toHaveValue(TEXTO_SOLICITUD);
  if (process.env.CAPTURAS) {
    for (const ancho of [360, 1440]) {
      await page.setViewportSize({ width: ancho, height: 900 });
      await page.screenshot({ path: `${process.env.CAPTURAS}/_cuenta_solicitud_contacto-${ancho}.png`, fullPage: true });
    }
  }
});

test('con el correo sin confirmar, el formulario explica cómo hacerlo y ofrece reenviar el enlace', async ({ page }) => {
  await entrarComoCliente(page);
  await page.goto('/cuenta/solicitud');
  await expect(page.getByText('Confirma tu correo para enviar solicitudes')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Reenviar enlace' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Enviar solicitud' })).toHaveCount(0);
});

test('bandeja → ficha → rechazar con motivo @esencial', async ({ page }) => {
  let rechazo = null;
  let metodo = null;
  // RFU11: también el operario rechaza.
  await entrarComo(page, 'operario');
  let version = null;
  await page.route('**/api/v1/solicitudes/123/estado', (ruta) => {
    rechazo = ruta.request().postDataJSON();
    version = ruta.request().headers()['if-match'];
    metodo = ruta.request().method();
    return ruta.fulfill({ status: 204 });
  });
  await page.route('**/api/v1/solicitudes/123', (ruta) => ruta.fulfill({
    json: rechazo ? {
      ...FICHA, estado: 'rechazado', motivoRechazo: rechazo.motivo,
      historial: [...FICHA.historial, {
        estadoAnterior: 'solicitado', estadoNuevo: 'rechazado', motivo: rechazo.motivo, fechaChile: COMPROBANTE.fechaChile,
        actor: 'personal', por: 'Marta Fuentes',
      }],
    } : FICHA,
  }));
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/panel/solicitudes');
  const filtros = page.getByRole('search', { name: 'Filtros de solicitudes' });
  await expect(filtros.getByRole('status')).toHaveText('3 solicitudes');
  const pedido = page.waitForRequest((r) => r.url().includes('/api/v1/solicitudes?') && r.url().includes('estado=solicitado'));
  await filtros.getByRole('combobox', { name: 'Estado' }).selectOption('solicitado');
  await pedido;
  await expect(page).toHaveURL(/estado=solicitado/);

  await page.getByRole('link', { name: 'Solicitud #123' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Solicitud #123' })).toBeVisible();
  const miniatura = page.getByRole('button', { name: 'Ver fachada.jpg en grande' });
  await expect(miniatura.locator('img')).toHaveAttribute('src', /^blob:/);
  await miniatura.click();
  await expect(page.getByRole('dialog', { name: 'fachada.jpg' }).getByRole('img')).toBeVisible();
  await page.keyboard.press('Escape');

  await page.getByRole('button', { name: 'Rechazar con motivo' }).click();
  const dialogo = page.getByRole('dialog', { name: 'Rechazar la solicitud #123' });
  const motivo = dialogo.getByRole('textbox', { name: /Motivo del rechazo/ });
  await expect(motivo).toBeFocused();
  await dialogo.getByRole('button', { name: 'Rechazar solicitud' }).click();
  await expect(motivo).toHaveAttribute('aria-invalid', 'true');
  expect(rechazo).toBeNull();
  await motivo.fill('No fabricamos letreros de ese tamaño');
  await dialogo.getByRole('button', { name: 'Rechazar solicitud' }).click();
  await expect(dialogo).toBeHidden();
  expect(metodo).toBe('PATCH');
  expect(version).toBe('"AAAAAAAAB9E="');
  expect(rechazo).toEqual({ estado: 'rechazado', motivo: 'No fabricamos letreros de ese tamaño' });
  await expect(page.getByText('Motivo: No fabricamos letreros de ese tamaño')).toBeVisible();
  await expect(page.getByRole('main').getByText('Rechazada', { exact: true }).first()).toBeVisible();
});

test('el enlace «Ver solicitud» del correo pide ingresar y vuelve a la ficha', async ({ page }) => {
  let conectado = false;
  const sesion = () => ({ token: tokenDePrueba(), tipo: 'empleado', usuario: { idEmpleado: 2, nombre: 'Hugo', apellido: 'Vera', email: 'operario@letreria.test', rol: 'operario' } });
  await page.route('**/api/v1/**', (ruta) => {
    const destino = destinoDe(ruta);
    if (destino === '/api/v1/auth/login') {
      conectado = true;
      return ruta.fulfill({ json: sesion() });
    }
    if (destino === '/api/v1/auth/renovar') return conectado ? ruta.fulfill({ json: sesion() }) : ruta.fulfill({ status: 401, json: { error: 'Sin sesión' } });
    if (/\/adjuntos\/\d+\/miniatura$/.test(destino)) return ruta.fulfill({ path: IMAGEN_PRUEBA, contentType: 'image/webp' });
    return ruta.fulfill({ json: RESPUESTAS[destino] ?? [] });
  });
  await page.goto('/panel/solicitudes/123');
  await expect(page.getByRole('heading', { name: 'Acceso del personal' })).toBeVisible();
  await page.getByRole('textbox', { name: 'Correo electrónico' }).fill('operario@letreria.test');
  await page.getByLabel(/^Contraseña/).fill('clave-de-prueba');
  await page.getByRole('button', { name: 'Iniciar sesión' }).click();
  await expect(page).toHaveURL(/\/panel\/solicitudes\/123$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Solicitud #123' })).toBeVisible();
});

test('el inicio del panel muestra las solicitudes por atender', async ({ page }) => {
  await entrarComo(page, 'diseñador');
  await page.goto('/panel');
  const seccion = page.getByRole('region', { name: /Solicitudes por atender/ });
  await expect(seccion.getByRole('link', { name: /#123 · Rosa Pino/ })).toHaveAttribute('href', '/panel/solicitudes/123');
});

// ── Fase 8: flujos de cotizaciones, pedidos, abonos y calendario ──

/** Revisa que la página no desborde y no tenga fallas graves de accesibilidad. */
async function sinFallasGraves(page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
  const { violations } = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(violations.filter((v) => ['critical', 'serious'].includes(v.impact)).map((v) => `${v.id}: ${v.nodes[0]?.target}`)).toEqual([]);
}

test('el personal cotiza la solicitud: el catálogo sugiere el costo, el total va en vivo y la vista previa lo muestra', async ({ page }) => {
  await entrarComo(page, 'operario');
  let emitida = null;
  await page.route('**/api/v1/solicitudes/123/cotizaciones', (ruta) => {
    emitida = ruta.request().postDataJSON();
    return ruta.fulfill({ status: 201, json: { ...cotizacionDe(90, 'pendiente'), costoManoObra: 30000, otrosCostos: 0 } });
  });
  await page.route('**/api/v1/cotizaciones/90', (ruta) => ruta.fulfill({ json: cotizacionDe(90, 'pendiente') }));
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/panel/solicitudes/123');
  await page.getByRole('link', { name: 'Cotizar', exact: true }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Cotizar la solicitud #123' })).toBeFocused();

  await page.getByRole('button', { name: 'Agregar material' }).click();
  const material = page.getByRole('combobox', { name: /^Material/ });
  await expect(material).toBeFocused();
  await expect(material.getByRole('option', { name: 'Perfil de aluminio' })).toHaveCount(0);
  await material.selectOption('1');
  await expect(page.getByRole('textbox', { name: /^Costo unitario/ })).toHaveValue('18500');
  await page.getByRole('textbox', { name: /^Cantidad \(m2\)/ }).fill('1,5');
  await page.getByRole('textbox', { name: /^Medida/ }).fill('120 × 60 cm');
  await page.getByRole('textbox', { name: /^Mano de obra/ }).fill('30.000');
  const pie = page.locator('form').getByText('Total de la cotización', { exact: true }).locator('..');
  await expect(pie).toContainText('$57.750');
  const previa = page.getByRole('complementary', { name: 'Vista previa para el cliente' });
  await expect(previa.getByRole('listitem').filter({ hasText: 'Acrílico 3 mm' })).toBeVisible();
  await expect(previa.getByText('$57.750')).toBeVisible();
  await sinFallasGraves(page);
  if (process.env.CAPTURAS) await page.screenshot({ path: `${process.env.CAPTURAS}/_flujo_cotizar-1440.png`, fullPage: true });

  await page.getByRole('button', { name: 'Emitir cotización' }).click();
  await expect(page).toHaveURL(/\/panel\/cotizaciones\/90$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Cotización #90' })).toBeVisible();
  expect(emitida).toEqual({
    materiales: [{ idMaterial: 1, medidaAplicada: '120 × 60 cm', cantidad: 1.5, costoUnitario: 18500 }],
    costoManoObra: 30000, otrosCostos: 0, observaciones: null,
  });
});

test('el personal registra que el cliente aprobó por teléfono, con la versión vista', async ({ page }) => {
  await entrarComo(page, 'diseñador');
  let respuesta = null;
  let version = null;
  await page.route('**/api/v1/cotizaciones/45/respuesta', (ruta) => {
    respuesta = ruta.request().postDataJSON();
    version = ruta.request().headers()['if-match'];
    return ruta.fulfill({ json: { ...COT_APROBADA, idCotizacion: 45, numero: '#45' } });
  });
  await page.goto('/panel/cotizaciones/45');
  await page.getByRole('button', { name: 'Registrar respuesta del cliente' }).click();
  const dialogo = page.getByRole('dialog', { name: /Respuesta del cliente a la cotización #45/ });
  await expect(dialogo.getByRole('radio', { name: /La aprobó/ })).toBeFocused();
  await dialogo.getByRole('button', { name: 'Registrar respuesta' }).click();
  await expect(dialogo.getByText('Elige si el cliente la aprobó o la rechazó.')).toBeVisible();
  await dialogo.getByRole('radio', { name: /La aprobó/ }).check();
  await dialogo.getByRole('radio', { name: 'WhatsApp' }).check();
  await dialogo.getByRole('button', { name: 'Registrar respuesta' }).click();
  await expect(dialogo).toBeHidden();
  expect(respuesta).toEqual({ decision: 'aprobada', via: 'whatsapp' });
  expect(version).toBe('"AAAAAAAACOT="');
});

test('el cliente revisa su cotización y la aprueba tras confirmar «Al aprobar, el taller comienza tu trabajo»', async ({ page }) => {
  await entrarComoCliente(page, { verificado: true });
  let respuesta = null;
  let version = null;
  await page.route('**/api/v1/mis/cotizaciones/45/respuesta', (ruta) => {
    respuesta = ruta.request().postDataJSON();
    version = ruta.request().headers()['if-match'];
    return ruta.fulfill({ json: { ...COT_PENDIENTE, estado: 'aprobada', respuesta: { fechaChile: `${HOY_CHILE}T12:00:00`, via: 'cliente_web', motivo: null, registradaPor: null } } });
  });
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto('/cuenta/solicitudes');
  await expect(page.getByText('Por responder: ábrela para aprobarla o rechazarla.')).toBeVisible();
  await expect(page.getByText('Saldo $59.065', { exact: false })).toBeVisible();
  await page.getByRole('link', { name: 'Solicitud #123' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Solicitud #123' })).toBeVisible();
  const seguimiento = page.getByRole('region', { name: 'Seguimiento' });
  await expect(seguimiento.getByText('Lista para que la revises')).toBeVisible();
  const cotizacion = page.getByRole('region', { name: 'Tu cotización' });
  await expect(cotizacion.getByText('$84.065').first()).toBeVisible();
  await sinFallasGraves(page);
  if (process.env.CAPTURAS) await page.screenshot({ path: `${process.env.CAPTURAS}/_flujo_cliente_cotizacion-360.png`, fullPage: true });

  // Rechazar y aprobar se ven igual de grandes.
  const aprobar = cotizacion.getByRole('button', { name: 'Aprobar', exact: true });
  const rechazar = cotizacion.getByRole('button', { name: 'Rechazar', exact: true });
  expect(Math.abs((await aprobar.boundingBox()).height - (await rechazar.boundingBox()).height)).toBeLessThan(2);
  await aprobar.click();
  const confirmar = page.getByRole('dialog', { name: '¿Aprobar la cotización #45?' });
  await expect(confirmar).toContainText('Al aprobar, el taller comienza tu trabajo');
  await expect(confirmar.getByRole('button', { name: 'Volver' })).toBeFocused();
  await confirmar.getByRole('button', { name: 'Aprobar cotización' }).click();
  await expect(confirmar).toBeHidden();
  expect(respuesta).toEqual({ decision: 'aprobada' });
  expect(version).toBe('"AAAAAAAACOT="');
});

test('el cliente rechaza con un motivo opcional; un 409 de versión se explica con palabras para el cliente', async ({ page }) => {
  await entrarComoCliente(page, { verificado: true });
  let intentos = 0;
  await page.route('**/api/v1/mis/cotizaciones/45/respuesta', (ruta) => {
    intentos += 1;
    return intentos === 1
      ? ruta.fulfill({ status: 409, json: { error: 'Otra persona modificó esta cotización.', codigo: 'version_obsoleta', nivel: 'negocio' } })
      : ruta.fulfill({ json: { ...COT_PENDIENTE, estado: 'rechazada' } });
  });
  await page.goto('/cuenta/solicitudes/123');
  await page.getByRole('region', { name: 'Tu cotización' }).getByRole('button', { name: 'Rechazar', exact: true }).click();
  const dialogo = page.getByRole('dialog', { name: '¿Rechazar la cotización #45?' });
  await dialogo.getByRole('textbox', { name: /Por qué la rechazas/ }).fill('Necesito un tamaño menor');
  await dialogo.getByRole('button', { name: 'Rechazar cotización' }).click();
  await expect(dialogo.getByText('La cotización cambió. Te mostramos la versión actual.')).toBeVisible();
  await dialogo.getByRole('button', { name: 'Rechazar cotización' }).click();
  await expect(dialogo).toBeHidden();
  expect(intentos).toBe(2);
});

test('avance del pedido y abonos: transición con If-Match, tope del saldo y 409 abono_excede_saldo @esencial', async ({ page }) => {
  await entrarComo(page, 'operario');
  let cambio = null;
  let version = null;
  let abonos = 0;
  let ultimoAbono = null;
  let ficha = FICHA_PEDIDO;
  await page.route('**/api/v1/pedidos/1', (ruta) => ruta.fulfill({ json: ficha }));
  await page.route('**/api/v1/pedidos/1/estado', (ruta) => {
    cambio = ruta.request().postDataJSON();
    version = ruta.request().headers()['if-match'];
    ficha = {
      ...FICHA_PEDIDO, estadoPedido: 'en_proceso', transicionesPosibles: ['terminado', 'aceptado', 'anulado'], rowVersion: 'AAAAAAAAPE2=',
      historial: [...FICHA_PEDIDO.historial, { fechaChile: `${HOY_CHILE}T10:00:00`, estadoAnterior: 'aceptado', estadoNuevo: 'en_proceso', nota: null, empleado: 'Marta Fuentes' }],
    };
    return ruta.fulfill({ json: ficha });
  });
  await page.route('**/api/v1/pedidos/1/pagos', (ruta) => {
    abonos += 1;
    ultimoAbono = ruta.request().postDataJSON();
    if (abonos === 1) return ruta.fulfill({ status: 409, json: { error: 'El abono supera el saldo pendiente ($19.065).', codigo: 'abono_excede_saldo', nivel: 'negocio' } });
    ficha = { ...ficha, montoAbonado: 59065, saldoPendiente: 40000 };
    return ruta.fulfill({ status: 201, json: ficha });
  });
  await page.setViewportSize({ width: 768, height: 900 });
  await page.goto('/panel/pedidos/1');
  await expect(page.getByRole('heading', { level: 1, name: 'Pedido #1' })).toBeVisible();
  // El abono anulado queda tachado, con su motivo.
  await expect(page.getByText('Motivo: Se ingresó dos veces', { exact: false })).toBeVisible();
  await expect(page.locator('s', { hasText: '$4.000' })).toBeVisible();

  await page.getByRole('button', { name: 'Pasar a «En proceso»' }).click();
  await expect(page.getByRole('button', { name: 'Pasar a «Terminado»' })).toBeVisible();
  expect(cambio).toEqual({ estado: 'en_proceso' });
  expect(version).toBe('"AAAAAAAAPED="');
  await expect(page.getByRole('button', { name: 'Volver a «Aceptado»' })).toBeVisible();

  await page.getByRole('button', { name: 'Registrar abono' }).click();
  const dialogo = page.getByRole('dialog', { name: 'Registrar abono del pedido #1' });
  const monto = dialogo.getByRole('textbox', { name: /^Monto/ });
  await expect(monto).toBeFocused();
  await monto.fill('70.000');
  await dialogo.getByRole('combobox', { name: /^Medio de pago/ }).selectOption('efectivo');
  await dialogo.getByRole('button', { name: 'Registrar abono' }).click();
  await expect(monto).toHaveAccessibleDescription(/supera el saldo pendiente \(\$59\.065\)/);
  expect(abonos).toBe(0);

  await monto.fill('20.000');
  await dialogo.getByRole('button', { name: 'Registrar abono' }).click();
  await expect(monto).toHaveAccessibleDescription('El abono supera el saldo pendiente ($19.065).');
  await expect(monto).toHaveValue('20.000');
  await monto.fill('19.065');
  await dialogo.getByRole('button', { name: 'Registrar abono' }).click();
  await expect(dialogo).toBeHidden();
  expect(ultimoAbono).toEqual({ monto: 19065, medioPago: 'efectivo', observacion: null });
  await sinFallasGraves(page);
  if (process.env.CAPTURAS) await page.screenshot({ path: `${process.env.CAPTURAS}/_flujo_pedido-768.png`, fullPage: true });
});

test('editar el pedido con una versión vieja avisa sin perder lo escrito y ofrece guardar sobre la actual', async ({ page }) => {
  await entrarComo(page, 'administrador');
  await page.route('**/api/v1/pedidos/1', (ruta) => (ruta.request().method() === 'PUT'
    ? ruta.fulfill({ status: 409, json: { error: 'Otra persona modificó este pedido.', codigo: 'version_obsoleta', nivel: 'negocio' } })
    : ruta.fulfill({ json: FICHA_PEDIDO })));
  await page.goto('/panel/pedidos/1');
  await page.getByRole('button', { name: 'Editar datos' }).click();
  const factura = page.getByRole('textbox', { name: /^Número de factura/ });
  await factura.fill('F-1204');
  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(page.getByText('Otra persona modificó este pedido mientras lo editabas', { exact: true })).toBeVisible();
  await expect(factura).toHaveValue('F-1204');
  await expect(page.getByRole('button', { name: 'Guardar sobre la versión actual' })).toBeFocused();
  await expect(page.getByRole('button', { name: 'Descartar mis cambios y ver la versión actual' })).toBeVisible();
});

test('«Por cobrar» pide cobranza=true a la API y muestra el total de todo el filtro', async ({ page }) => {
  await entrarComo(page, 'administrador');
  const pedidosCobranza = [];
  await page.route('**/api/v1/pedidos?*', (ruta) => {
    const url = new URL(ruta.request().url());
    const cobranza = url.searchParams.get('cobranza') === 'true';
    if (cobranza) pedidosCobranza.push(url.search);
    return ruta.fulfill({ json: cobranza ? paginaDe(POR_COBRAR, { total: 31, totalPaginas: 2, resumen: { totalPorCobrar: 1234567, cantidadPorCobrar: 31 } }) : RESPUESTAS['/api/v1/pedidos'] });
  });
  await page.goto('/panel/pedidos');
  const filtros = page.getByRole('search', { name: 'Filtros de pedidos' });
  await expect(filtros.getByRole('status')).toHaveText('6 pedidos · $235.000 por cobrar');
  await filtros.getByRole('button', { name: 'Por cobrar' }).click();
  await expect(page).toHaveURL(/cobranza=true/);
  await expect(page.getByRole('heading', { level: 1, name: 'Pedidos por cobrar' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Total por cobrar' })).toContainText('$1.234.567');
  await expect(filtros.getByRole('status')).toHaveText('31 pedidos · $1.234.567 por cobrar');
  expect(pedidosCobranza.at(-1)).toContain('pagina=1');
  await page.getByRole('link', { name: 'Pedido #2' }).click();
  await expect(page).toHaveURL(/\/panel\/pedidos\/2$/);
});

test('calendario: mes con color, ícono y texto; anterior, siguiente y «Hoy»; cada pedido abre su ficha', async ({ page }) => {
  await entrarComo(page, 'operario');
  const rangos = [];
  await page.route('**/api/v1/pedidos/calendario?*', (ruta) => {
    const url = new URL(ruta.request().url());
    rangos.push([url.searchParams.get('desde'), url.searchParams.get('hasta')]);
    return ruta.fulfill({ json: CALENDARIO });
  });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/panel/calendario');
  const cuadricula = page.getByRole('list', { name: /^Entregas de / });
  await expect(cuadricula.getByRole('link', { name: /Atrasado.*#4 · Minimarket Don Lucho/ })).toBeVisible();
  await expect(cuadricula.getByRole('link', { name: /Entregado.*#2 · Ferretería Biobío/ })).toBeVisible();
  await expect(page.locator('[aria-current="date"]')).toHaveCount(1);
  const [desde, hasta] = rangos[0];
  const dias = (Date.parse(hasta) - Date.parse(desde)) / 86400000 + 1;
  expect(dias % 7).toBe(0);
  expect(dias).toBeLessThanOrEqual(62);
  await sinFallasGraves(page);
  if (process.env.CAPTURAS) await page.screenshot({ path: `${process.env.CAPTURAS}/_flujo_calendario-1440.png`, fullPage: true });

  await page.getByRole('button', { name: /^Mes siguiente/ }).click();
  await expect(page).toHaveURL(/\?mes=\d{4}-\d{2}$/);
  await expect.poll(() => rangos.length).toBeGreaterThan(1);
  await page.getByRole('button', { name: 'Hoy' }).click();
  await expect(page).toHaveURL(/\/panel\/calendario$/);

  // En el teléfono, una lista con solo los días que tienen entregas.
  await page.setViewportSize({ width: 360, height: 800 });
  await expect(page.getByRole('heading', { level: 3, name: /entregas?$/ }).first()).toBeVisible();
  await sinFallasGraves(page);
  if (process.env.CAPTURAS) await page.screenshot({ path: `${process.env.CAPTURAS}/_flujo_calendario-360.png`, fullPage: true });
  await page.getByRole('link', { name: /#1 · Panadería El Trigal/ }).click();
  await expect(page).toHaveURL(/\/panel\/pedidos\/1$/);
});

test('pedido directo: busca al cliente, describe el trabajo, cotiza, fija la entrega y deja un abono', async ({ page }) => {
  await entrarComo(page, 'dueño');
  let creado = null;
  await page.route('**/api/v1/clientes/buscar?*', (ruta) => ruta.fulfill({ json: [CLIENTES.elementos[0]] }));
  await page.route('**/api/v1/pedidos/directo', (ruta) => {
    creado = ruta.request().postDataJSON();
    return ruta.fulfill({ status: 201, json: { ...FICHA_PEDIDO, idPedido: 77, numero: '#77', origen: 'directo' } });
  });
  await page.route('**/api/v1/pedidos/77', (ruta) => ruta.fulfill({ json: { ...FICHA_PEDIDO, idPedido: 77, numero: '#77', origen: 'directo' } }));
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/panel/pedidos');
  await page.getByRole('link', { name: 'Pedido directo en el taller' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Pedido directo en el taller' })).toBeVisible();
  await page.getByRole('textbox', { name: /^Teléfono/ }).fill('912345678');
  await page.getByRole('button', { name: 'Buscar cliente' }).click();
  await expect(page.getByText('Rosa Pino', { exact: true })).toBeVisible();

  const tipo = page.getByRole('combobox', { name: /Tipo de letrero/ });
  await expect(tipo).toBeFocused();
  await tipo.selectOption('1');
  await page.getByRole('textbox', { name: /^Ancho/ }).fill('100');
  await page.getByRole('textbox', { name: /^Alto/ }).fill('50');
  await page.getByRole('textbox', { name: /^Descripción del trabajo/ }).fill('Cartel para la vitrina, entrega rápida.');
  await page.getByRole('button', { name: 'Agregar material' }).click();
  await page.getByRole('combobox', { name: /^Material/ }).selectOption('2');
  await page.getByRole('textbox', { name: /^Mano de obra/ }).fill('20000');
  await page.getByRole('textbox', { name: /^Entrega estimada/ }).fill(diaChile(5));
  await page.getByRole('radio', { name: 'Retiro en el local' }).check();
  await page.getByRole('textbox', { name: /^Monto/ }).fill('10.000');
  await page.getByRole('combobox', { name: /^Medio de pago/ }).selectOption('efectivo');
  const resumen = page.getByRole('region', { name: 'Montos del pedido' });
  await expect(resumen).toContainText('$29.900');
  await expect(resumen).toContainText('$19.900');
  await sinFallasGraves(page);
  if (process.env.CAPTURAS) await page.screenshot({ path: `${process.env.CAPTURAS}/_flujo_directo-1440.png`, fullPage: true });

  await page.getByRole('button', { name: 'Crear pedido directo' }).click();
  await expect(page).toHaveURL(/\/panel\/pedidos\/77$/);
  expect(creado).toEqual({
    idCliente: 9,
    descripcionGeneral: 'Cartel para la vitrina, entrega rápida.',
    detalles: [{ idProducto: 1, descripcion: null, anchoMm: 1000, altoMm: 500, cantidad: 1 }],
    materiales: [{ idMaterial: 2, medidaAplicada: null, cantidad: 1, costoUnitario: 9900 }],
    costoManoObra: 20000, otrosCostos: 0, observaciones: null,
    idEmpleadoResponsable: 1, fechaEntregaEstimada: diaChile(5), modalidadEntrega: 'retiro_local', requiereInstalacion: false,
    direccionInstalacion: null, costoInstalacionAdicional: 0, numeroFactura: null,
    abonoInicial: { monto: 10000, medioPago: 'efectivo', observacion: null },
  });
});

test('el inicio del panel muestra a gestión las entregas atrasadas, las próximas y el total por cobrar', async ({ page }) => {
  await entrarComo(page, 'dueño');
  await page.goto('/panel');
  const alertas = page.getByRole('region', { name: 'Entregas y cobranza' });
  await expect(alertas.getByRole('region', { name: /Atrasadas/ }).getByRole('link', { name: /#4 · Minimarket Don Lucho/ })).toHaveAttribute('href', '/panel/pedidos/4');
  await expect(alertas.getByRole('region', { name: /Por cobrar/ })).toContainText('$235.000');
  await expect(alertas.getByRole('link', { name: 'Ver cobranza' })).toHaveAttribute('href', '/panel/pedidos?cobranza=true');
});

test('un operario no ve las alertas de gestión en el inicio', async ({ page }) => {
  await entrarComo(page, 'operario');
  await page.goto('/panel');
  await expect(page.getByRole('heading', { level: 1, name: 'Hola, Marta' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Entregas y cobranza' })).toHaveCount(0);
});

// ── Fase 9 ──

test('avisos del taller: reemplazar el correo deja el anterior activo hasta que el nuevo confirma desde el enlace', async ({ page }) => {
  await entrarComo(page, 'dueño');
  let pedido = null;
  await page.route('**/api/v1/avisos-taller', (ruta) => ruta.fulfill({
    json: pedido
      ? {
        ...AVISOS_TALLER,
        destinos: [DESTINO_ACTIVO, {
          idDestinatario: 2, correo: pedido.correo, estado: 'pendiente', reemplazaA: { idDestinatario: 1, correo: DESTINO_ACTIVO.correo },
          fechaSolicitudChile: `${HOY_CHILE}T10:00:00`, fechaConfirmacionChile: null, venceChile: `${diaChile(1)}T10:00:00`,
        }],
      }
      : AVISOS_TALLER,
  }));
  await page.route('**/api/v1/avisos-taller/destinos', (ruta) => {
    pedido = ruta.request().postDataJSON();
    return ruta.fulfill({ status: 201, json: {} });
  });
  await page.goto('/panel/perfil');
  const seccion = page.getByRole('region', { name: 'Avisos del taller' });
  await expect(seccion.getByText('Configuración del taller', { exact: true })).toBeVisible();
  await expect(seccion.getByRole('button', { name: `Quitar ${DESTINO_ACTIVO.correo}` })).toHaveAttribute('aria-disabled', 'true');
  await seccion.getByRole('button', { name: `Reemplazar ${DESTINO_ACTIVO.correo}` }).click();
  const campo = seccion.getByRole('textbox', { name: /Correo que reemplaza/ });
  await expect(campo).toBeFocused();
  await campo.fill('nuevo@taller.cl');
  await seccion.getByLabel(/Tu contraseña, para confirmar el cambio/).fill('mi-clave');
  await seccion.getByRole('button', { name: 'Enviar enlace de confirmación' }).click();
  await expect(seccion.getByText('Pendiente de confirmar')).toBeVisible();
  await expect(seccion.getByText(/Sigue recibiendo los avisos hasta que se confirme el cambio/)).toBeVisible();
  expect(pedido).toEqual({ correo: 'nuevo@taller.cl', idReemplaza: 1, passwordActual: 'mi-clave' });
  await expect(seccion.getByRole('button', { name: 'Agregar correo' })).toBeFocused();
  await sinFallasGraves(page);

  // Quien recibe el correo abre el enlace, sin sesión.
  const confirmacion = page.waitForRequest((r) => r.url().endsWith('/api/v1/avisos-taller/confirmar') && r.method() === 'POST');
  await page.goto('/avisos/confirmar?token=un-solo-uso');
  expect((await confirmacion).postDataJSON()).toEqual({ token: 'un-solo-uso' });
  await expect(page.getByRole('heading', { level: 1, name: 'Correo confirmado' })).toBeVisible();
  await expect(page).toHaveURL(/\/avisos\/confirmar$/);
});

test('avisos del taller: el correo de prueba consulta el estado hasta «Salió bien»', async ({ page }) => {
  await entrarComo(page, 'administrador');
  let consultas = 0;
  await page.route('**/api/v1/avisos-taller/prueba', (ruta) => ruta.fulfill({
    status: 202, json: { idsCorreos: [41], destinatarios: [DESTINO_ACTIVO.correo], usaRespaldo: false },
  }));
  await page.route('**/api/v1/avisos-taller/prueba/estado?**', (ruta) => {
    consultas += 1;
    return ruta.fulfill({ json: [{ idCorreo: 41, destinatario: DESTINO_ACTIVO.correo, estado: consultas > 1 ? 'enviado' : 'pendiente', intentos: consultas - 1, error: null }] });
  });
  await page.goto('/panel/perfil');
  await page.getByRole('button', { name: 'Enviar correo de prueba' }).click();
  const resultado = page.getByRole('list', { name: 'Resultado del correo de prueba' });
  await expect(resultado.getByText('Salió bien')).toBeVisible({ timeout: 10000 });
  expect(consultas).toBe(2);
});

test('un operario no ve los avisos del taller en su perfil', async ({ page }) => {
  await entrarComo(page, 'operario');
  const pedidas = [];
  page.on('request', (r) => r.url().includes('/avisos-taller') && pedidas.push(r.url()));
  await page.goto('/panel/perfil');
  await expect(page.getByRole('heading', { level: 1, name: 'Mi perfil' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Avisos del taller' })).toHaveCount(0);
  expect(pedidas).toEqual([]);
});

test('un operario da vuelta el cartel y edita su disponibilidad, pero no el horario', async ({ page }) => {
  await entrarComo(page, 'operario');
  let cartel = null;
  let franjas = null;
  await page.route('**/api/v1/cartel-digital', (ruta) => {
    if (ruta.request().method() === 'PUT') {
      cartel = ruta.request().postDataJSON();
      return ruta.fulfill({ status: 204 });
    }
    return ruta.fulfill({ json: RESPUESTAS['/api/v1/cartel-digital'] });
  });
  await page.route('**/api/v1/mi-disponibilidad', (ruta) => {
    if (ruta.request().method() === 'PUT') {
      franjas = ruta.request().postDataJSON();
      return ruta.fulfill({ status: 204 });
    }
    return ruta.fulfill({ json: RESPUESTAS['/api/v1/mi-disponibilidad'] });
  });
  await page.goto('/panel/local');
  await page.getByRole('combobox', { name: 'Estado' }).selectOption('abierto');
  await page.getByRole('button', { name: 'Dar vuelta el cartel' }).click();
  await expect(page.getByText('El cartel dice Abierto').first()).toBeVisible();
  expect(cartel).toEqual({ abiertoEnLocal: true, mensajeEstado: 'Local abierto y atendiendo', tiempoEstimadoRegreso: null });
  await expect(page.getByRole('button', { name: 'Guardar horario' })).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'Disponibilidad del equipo' })).toHaveCount(0);

  await page.getByRole('button', { name: 'Agregar franja el martes' }).click();
  await page.getByRole('button', { name: 'Guardar mi disponibilidad' }).click();
  await expect(page.getByText('Disponibilidad guardada').first()).toBeVisible();
  expect(franjas).toEqual({ franjas: [
    { diaSemana: 1, horaInicio: '09:00:00', horaFin: '13:00:00' }, { diaSemana: 2, horaInicio: '09:00:00', horaFin: '18:30:00' },
  ] });
  await sinFallasGraves(page);
});

test('desactivar a una persona pide confirmar y envía la versión vista', async ({ page }) => {
  await entrarComo(page, 'dueño');
  let cambio = null;
  await page.route('**/api/v1/empleados/2/activo', (ruta) => {
    cambio = { cuerpo: ruta.request().postDataJSON(), version: ruta.request().headers()['if-match'] };
    return ruta.fulfill({ json: { ...PERSONAL[1], activo: false } });
  });
  await page.goto('/panel/empleados');
  await page.getByRole('link', { name: 'Hugo Vera' }).click();
  await page.getByRole('button', { name: 'Desactivar a esta persona' }).click();
  const dialogo = page.getByRole('dialog', { name: 'Desactivar a Hugo Vera' });
  await expect(dialogo.getByText('Se borrará su acceso con rostro y se cerrarán sus sesiones.')).toBeVisible();
  await expect(dialogo.getByRole('button', { name: 'Cancelar' })).toBeFocused();
  await sinFallasGraves(page);
  await dialogo.getByRole('button', { name: 'Desactivar' }).click();
  await expect(dialogo).toBeHidden();
  expect(cambio).toEqual({ cuerpo: { activo: false }, version: '"AAAAAAAAEM2="' });
});

test('la vitrina bloquea mostrar una publicación con logo sin autorización y lo explica', async ({ page }) => {
  await entrarComo(page, 'administrador');
  await page.route('**/api/v1/catalogo?**', (ruta) => ruta.fulfill({ json: paginaDe([PUBLICACION, PUBLICACION_LOGO]) }));
  await page.goto('/panel/mantenedores/vitrina');
  const interruptor = page.getByRole('switch', { name: `Mostrar «${PUBLICACION_LOGO.titulo}» en la vitrina` });
  await expect(interruptor).toHaveAttribute('aria-disabled', 'true');
  await expect(interruptor).toHaveAccessibleDescription(/registra su autorización/);
  await expect(page.getByRole('switch', { name: `Mostrar «${PUBLICACION.titulo}» en la vitrina` })).toHaveAttribute('aria-checked', 'true');
  await sinFallasGraves(page);
});

test('la portada muestra las franjas de atención junto al horario, sin nombres', async ({ page }) => {
  await simularApiPublica(page);
  await page.goto('/#horario');
  const horario = page.locator('#horario');
  await expect(horario.getByText('Atención: 9:00 a 13:00 · 15:00 a 18:00').first()).toBeVisible();
  await expect(horario.getByText(/Marta|Hugo/)).toHaveCount(0);
});

// ── Fase 11: notificaciones, auditoría y privacidad ──

for (const ancho of [360, 768, 1440]) {
  test(`fase 11: reenviar un correo a ${ancho} px, la confirmación dice a quién irá y no tiene fallas graves`, async ({ page }) => {
    await page.setViewportSize({ width: ancho, height: 900 });
    await entrarComo(page, 'dueño');
    await page.goto('/panel/correos-no-enviados');
    await page.getByRole('button', { name: 'Reenviar el correo #51' }).click();
    const dialogo = page.getByRole('dialog', { name: 'Reenviar este correo' });
    await expect(dialogo.getByText(/destinos activos de hoy/)).toBeVisible();
    await expect(dialogo.getByRole('button', { name: 'Cancelar' })).toBeFocused();
    await sinFallasGraves(page);
    await dialogo.getByRole('button', { name: 'Cancelar' }).click();
    await expect(dialogo).toBeHidden();
    await page.getByRole('button', { name: 'Reenviar el correo #52' }).click();
    await expect(page.getByRole('dialog', { name: 'Reenviar este correo' }).getByText(/misma dirección.*r•••a@correo\.test/)).toBeVisible();
  });

  test(`fase 11: auditoría a ${ancho} px, el detalle se abre y se lee sin fallas graves`, async ({ page }) => {
    await page.setViewportSize({ width: ancho, height: 900 });
    await entrarComo(page, 'dueño');
    await page.goto('/panel/auditoria');
    await page.getByText('Ver detalle').first().click();
    await expect(page.getByText('Estado del pedido')).toBeVisible();
    await expect(page.getByText('en_proceso')).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
    await sinFallasGraves(page);
  });

  test(`fase 11: mi cuenta a ${ancho} px, el diálogo de baja explica qué se conserva y no tiene fallas graves`, async ({ page }) => {
    await page.setViewportSize({ width: ancho, height: 900 });
    await entrarComoCliente(page, { verificado: true });
    await page.goto('/cuenta');
    await expect(page.getByRole('heading', { name: 'Correos que recibo' })).toBeVisible();
    await expect(page.getByText('Los correos de seguridad de tu cuenta siempre se envían.')).toBeVisible();
    await page.getByRole('button', { name: 'Dar de baja mi cuenta' }).click();
    const dialogo = page.getByRole('dialog', { name: 'Dar de baja mi cuenta' });
    await expect(dialogo.getByText(/Se conservan tus solicitudes, cotizaciones, pedidos y abonos/)).toBeVisible();
    await sinFallasGraves(page);
  });
}

test('fase 11: el enlace de baja no actúa al abrirse, saca el token de la dirección y da de baja solo al confirmar', async ({ page }) => {
  const posts = [];
  await page.route('**/api/v1/**', (ruta) => {
    const destino = new URL(ruta.request().url()).pathname.replace(/\/$/, '');
    if (destino === '/api/v1/correos/baja' && ruta.request().method() === 'POST') {
      posts.push(ruta.request().postDataJSON());
      return ruta.fulfill({ json: { avisosEstado: false, marketing: false } });
    }
    return ruta.fulfill({ json: RESPUESTAS[destino] ?? [] });
  });
  await page.goto('/baja?t=abc123');
  await expect(page.getByRole('heading', { level: 1, name: 'Darte de baja de los correos' })).toBeVisible();
  await page.waitForLoadState('networkidle');
  expect(posts).toEqual([]);
  await expect(page).toHaveURL(/\/baja$/);
  await page.getByRole('button', { name: 'Confirmar baja' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Listo, te diste de baja' })).toBeFocused();
  expect(posts).toEqual([{ t: 'abc123', avisosEstado: false, marketing: false }]);
  await sinFallasGraves(page);
});

test('fase 11: el inicio avisa de los correos no enviados y la preferencia de avisos marca el error sin enviar', async ({ page }) => {
  await entrarComo(page, 'dueño');
  await page.goto('/panel');
  await page.getByRole('link', { name: 'Ver los correos no enviados' }).click();
  await expect(page).toHaveURL(/\/panel\/correos-no-enviados\?revisado=false$/);
  await page.goto('/panel/perfil');
  const grupo = page.getByRole('group', { name: /Avisos que recibe contacto/ });
  await grupo.getByRole('checkbox', { name: 'Solicitudes en línea' }).uncheck();
  await grupo.getByRole('button', { name: 'Guardar preferencia' }).click();
  await expect(grupo.getByText('Marca al menos un tipo de solicitud.')).toBeVisible();
  await expect(grupo.getByRole('checkbox', { name: 'Solicitudes en línea' })).toBeFocused();
});

test('fase 11: un operario no ve auditoría ni correos no enviados en el menú y recibe 403 por la dirección', async ({ page }) => {
  await entrarComo(page, 'operario');
  await page.goto('/panel');
  const menu = page.getByRole('navigation', { name: 'Panel del taller' });
  await expect(menu.getByRole('link', { name: /Auditoría/ })).toHaveCount(0);
  await expect(menu.getByRole('link', { name: /Correos no enviados/ })).toHaveCount(0);
  await page.goto('/panel/auditoria');
  await expect(page.getByRole('heading', { name: 'No tienes permiso para ver esta página' })).toBeVisible();
});

// ── Fase 12C: portal público (barra, cartel, consultas, vitrina e ingreso) ──

test('12C: la barra tiene Trabajos, Horario y Consultas; sin «Cómo llegar»; el cartel pequeño dice el estado', async ({ page }) => {
  await simularApiPublica(page);
  await page.setViewportSize({ width: 1440, height: 800 });
  await page.goto('/');
  const barra = page.getByRole('banner');
  for (const nombre of ['Trabajos', 'Horario', 'Consultas']) await expect(barra.getByRole('link', { name: nombre })).toBeVisible();
  await expect(barra.getByRole('link', { name: 'Cómo llegar' })).toHaveCount(0);
  await expect(barra.getByRole('link', { name: 'Contacto' })).toHaveCount(0);
  await expect(barra.getByText('Cerrado')).toBeVisible();
  await barra.getByRole('link', { name: 'Consultas' }).click();
  await expect(page).toHaveURL(/\/consultas$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Escríbenos' })).toBeVisible();
});

test('12C: los botones de la barra pasan a naranja con letras blancas al pasar el cursor y al enfocar', async ({ page }) => {
  await simularApiPublica(page);
  await page.setViewportSize({ width: 1440, height: 800 });
  await page.goto('/');
  const enlace = page.getByRole('banner').getByRole('link', { name: 'Horario' });
  await enlace.hover();
  await expect(enlace).toHaveCSS('background-color', 'rgb(194, 65, 12)');
  await expect(enlace).toHaveCSS('color', 'rgb(255, 255, 255)');
  await page.mouse.move(5, 400);
  await expect(enlace).not.toHaveCSS('background-color', 'rgb(194, 65, 12)');
  await enlace.focus();
  await page.keyboard.press('Tab');
  await page.keyboard.press('Shift+Tab');
  await expect(enlace).toHaveCSS('background-color', 'rgb(194, 65, 12)');
});

test('12C: con reducción de movimiento el cartel y los botones no se mueven', async ({ page }) => {
  await simularApiPublica(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.setViewportSize({ width: 1440, height: 800 });
  await page.goto('/');
  await expect(page.getByRole('complementary', { name: 'En el taller ahora.' }).getByText('Cerrado', { exact: true })).toBeVisible();
  const animacion = await page.evaluate(() => getComputedStyle(document.querySelector('aside h2').closest('[data-estado]').parentElement).animationName);
  expect(animacion).toBe('none');
  const boton = page.getByRole('main').getByRole('link', { name: 'Solicitar cotización' });
  await boton.hover();
  await page.waitForTimeout(250);
  await expect(boton).toHaveCSS('transform', 'none');
});

test('12C: el cartel se balancea sin parar; con reducción de movimiento arranca quieto y un botón lo mueve', async ({ page }) => {
  await simularApiPublica(page);
  await page.setViewportSize({ width: 1440, height: 800 });
  await page.goto('/');
  const cartel = page.getByRole('complementary', { name: 'En el taller ahora.' });
  const giro = () => cartel.evaluate((e) => getComputedStyle(e.querySelector('[data-estado]')).transform);
  await expect.poll(giro).not.toBe('none');
  const primero = await giro();
  await expect.poll(giro).not.toBe(primero);
  await cartel.getByRole('button', { name: 'Pausar el balanceo' }).click();
  await expect.poll(giro).toBe('none');

  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.evaluate(() => localStorage.removeItem('synctime.cartel.movimiento'));
  await page.reload();
  await expect(cartel.getByRole('button', { name: 'Balancear el cartel' })).toBeVisible();
  expect(await giro()).toBe('none');
  await cartel.getByRole('button', { name: 'Balancear el cartel' }).click();
  await expect.poll(giro).not.toBe('none');
});

test('12C: la marca, «Consultas» y «Ingresar» llevan arriba aunque ya se esté en esa página', async ({ page }) => {
  await simularApiPublica(page);
  // Ventana baja: /consultas e /ingresar son cortas y así tienen algo que desplazar.
  await page.setViewportSize({ width: 1440, height: 420 });
  await page.goto('/');
  const barra = page.getByRole('banner');
  // Se repite el desplazamiento hasta que la página (que aún carga) sea lo bastante larga.
  const bajar = () => expect.poll(() => page.evaluate(() => { window.scrollTo(0, 600); return window.scrollY; })).toBeGreaterThan(100);
  for (const [enlace, ruta] of [['Letreros Herrera', '/'], ['Consultas', '/consultas'], ['Ingresar', '/ingresar']]) {
    await bajar();
    await barra.getByRole('link', { name: enlace }).click();
    await expect(page).toHaveURL(new RegExp(`${ruta}$`));
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
    await bajar(); // otra vez, ya en esa página
    await barra.getByRole('link', { name: enlace }).click();
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
  }
});

test('12C: sin reducción de movimiento el botón se eleva al pasar el cursor, pero no en el panel del taller', async ({ page }) => {
  await simularApiPublica(page);
  await page.setViewportSize({ width: 1440, height: 800 });
  await page.goto('/');
  const boton = page.getByRole('main').getByRole('link', { name: 'Solicitar cotización' });
  await boton.hover();
  await expect.poll(() => boton.evaluate((e) => getComputedStyle(e).transform)).not.toBe('none');
  await entrarComo(page, 'dueño');
  await page.goto('/panel/pedidos');
  const enPanel = page.getByRole('main').getByRole('button').first();
  await enPanel.hover();
  await page.waitForTimeout(250);
  await expect(enPanel).toHaveCSS('transform', 'none');
});

test('12C: la vitrina filtra al escribir, sin tildes, y se combina con la categoría', async ({ page }) => {
  await simularApiPublica(page);
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  const vitrina = page.locator('#vitrina');
  await expect(vitrina.getByRole('status')).toHaveText('2 trabajos');
  await vitrina.getByRole('searchbox', { name: 'Buscar trabajos' }).fill('vitrina');
  await expect(vitrina.getByRole('status')).toHaveText('1 trabajo');
  await expect(vitrina.getByRole('heading', { level: 3, name: 'Vinilo para vitrina de ferretería' })).toBeVisible();
  await vitrina.getByRole('searchbox', { name: 'Buscar trabajos' }).fill('zzz');
  await expect(vitrina.getByRole('heading', { level: 3, name: 'No hay trabajos con esa búsqueda' })).toBeVisible();
  await vitrina.getByRole('button', { name: 'Limpiar' }).click();
  await expect(vitrina.getByRole('status')).toHaveText('2 trabajos');
});

test('12C: el selector de ingreso conserva adónde volver y el cliente entra por su formulario', async ({ page }) => {
  await simularApiPublica(page);
  await page.goto('/cuenta/solicitud');
  await expect(page.getByRole('heading', { level: 1, name: '¿Quién está iniciando sesión?' })).toBeVisible();
  const tarjetas = page.getByRole('main').getByRole('link');
  await expect(tarjetas.nth(0)).toContainText('Cliente');
  await expect(tarjetas.nth(1)).toContainText('Personal del taller');
  const a = await tarjetas.nth(0).boundingBox();
  const b = await tarjetas.nth(1).boundingBox();
  expect(a.x).toBeLessThan(b.x);
  await tarjetas.nth(0).click();
  await expect(page).toHaveURL(/\/ingresar\/cliente$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Ingresar a tu cuenta' })).toBeVisible();
});

test('12C: en el teléfono las tarjetas de ingreso quedan una bajo la otra, con el cliente primero', async ({ page }) => {
  await simularApiPublica(page);
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto('/ingresar');
  const tarjetas = page.getByRole('main').getByRole('link');
  const a = await tarjetas.nth(0).boundingBox();
  const b = await tarjetas.nth(1).boundingBox();
  expect(a.y).toBeLessThan(b.y);
  expect(Math.abs(a.x - b.x)).toBeLessThan(2);
});

test('12C: a 360 px la barra muestra marca, cartel, «Ingresar» y menú sin desbordar', async ({ page }) => {
  await simularApiPublica(page);
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto('/');
  const barra = page.getByRole('banner');
  await expect(barra.getByRole('link', { name: 'Ingresar' })).toBeVisible();
  await expect(barra.getByRole('button', { name: 'Menú' })).toBeVisible();
  await expect(barra.getByText('Cerrado')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
});

// ── Fase 12D: paneles con menú lateral azul, cuenta del cliente, inicio en dos columnas, origen y catálogo editable ──

for (const ancho of [360, 768, 1440]) {
  test(`fase 12D: la cuenta del cliente tiene su menú lateral (${ancho} px), con el pie anclado y sin fallas graves`, async ({ page }) => {
    await page.setViewportSize({ width: ancho, height: 800 });
    await entrarComoCliente(page, { verificado: true });
    await page.goto('/cuenta');
    await expect(page.getByRole('heading', { level: 1, name: 'Mi cuenta' })).toBeVisible();

    // «Mi cuenta» ya no trae los accesos que pasaron al menú.
    await expect(page.getByRole('link', { name: 'Solicitar una cotización' })).toHaveCount(0);
    if (ancho < 1024) {
      await page.getByRole('button', { name: 'Menú' }).click();
      const cajon = page.getByRole('dialog', { name: 'Menú: Mi cuenta' });
      await expect(cajon.getByRole('link', { name: 'Mis solicitudes' })).toBeVisible();
      await expect(cajon.getByRole('link', { name: 'Solicitar cotización' })).toBeVisible();
      await expect(cajon.getByRole('button', { name: 'Cerrar sesión' })).toBeInViewport();
      await expect(cajon.getByRole('link', { name: 'Volver a la página principal' })).toBeInViewport();
      await sinFallasGraves(page);
      await page.keyboard.press('Escape');
      await expect(cajon).toBeHidden();
      await expect(page.getByRole('button', { name: 'Menú' })).toBeFocused();
    } else {
      const menu = page.getByRole('navigation', { name: 'Mi cuenta' });
      await expect(menu.getByRole('link', { name: 'Mis solicitudes' })).toBeVisible();
      await expect(menu.getByRole('link', { name: 'Solicitar cotización' })).toBeVisible();
      await expect(menu.getByRole('link', { name: 'Mi cuenta' })).toHaveAttribute('aria-current', 'page');
      await expect(page.getByRole('button', { name: 'Cerrar sesión' })).toBeInViewport();
      const fondo = await menu.evaluate((el) => getComputedStyle(el.parentElement).backgroundColor);
      expect(fondo).toBe('rgb(29, 78, 216)');
      await sinFallasGraves(page);
    }
  });
}

test('fase 12D: el pie del panel queda siempre visible aunque el menú sea largo y la ventana baja', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 560 });
  await entrarComo(page, 'dueño');
  await page.goto('/panel');
  await expect(page.getByRole('heading', { level: 1, name: 'Hola, Marta' })).toBeVisible();

  await expect(page.getByRole('link', { name: 'Volver a la página principal' })).toBeInViewport();
  await expect(page.getByRole('button', { name: 'Cerrar sesión' })).toBeInViewport();
  const menu = page.getByRole('navigation', { name: 'Panel del taller' });
  expect(await menu.evaluate((el) => el.scrollHeight > el.clientHeight)).toBe(true);
  // El menú se desplaza solo: el lateral entero no.
  expect(await page.evaluate(() => document.querySelector('nav[aria-label="Panel del taller"]').parentElement.scrollHeight <= document.querySelector('nav[aria-label="Panel del taller"]').parentElement.clientHeight)).toBe(true);
  await expect(page.getByRole('link', { name: 'Volver a la página principal' })).toHaveAttribute('href', '/');
  await expect(page.locator('[data-animar]')).toHaveCount(0);
});

test('fase 12D: el dueño ve «Empleados» en el menú y la pantalla lista al personal', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await entrarComo(page, 'dueño');
  await page.goto('/panel');
  await page.getByRole('navigation', { name: 'Panel del taller' }).getByRole('link', { name: 'Empleados' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Empleados' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Hugo Vera' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Agregar persona' })).toBeVisible();
  // Ya no es una pestaña del mantenedor genérico.
  await page.goto('/panel/mantenedores/categorias');
  await expect(page.getByRole('navigation', { name: 'Mantenedores' }).getByRole('link', { name: 'Personal' })).toHaveCount(0);
  await expect(page.getByRole('navigation', { name: 'Mantenedores' }).getByRole('link', { name: 'Empleados' })).toHaveCount(0);
});

test('fase 12D: diseñador y operario no ven «Empleados» y reciben 403 en su ruta', async ({ page }) => {
  await entrarComo(page, 'diseñador');
  await page.goto('/panel');
  await expect(page.getByRole('navigation', { name: 'Panel del taller' }).getByRole('link', { name: 'Empleados' })).toHaveCount(0);
  await page.goto('/panel/empleados');
  await expect(page.getByRole('heading', { name: 'No tienes permiso para ver esta página' })).toBeVisible();
});

test('fase 12D: el inicio pone las solicitudes a la izquierda y los indicadores a la derecha; en el teléfono, apilados', async ({ page }) => {
  await entrarComo(page, 'dueño');
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/panel');
  const solicitudes = page.getByRole('region', { name: /Solicitudes por atender/ });
  const indicadores = page.getByRole('region', { name: 'Indicadores del mes' });
  await expect(solicitudes).toBeVisible();
  await expect(indicadores).toBeVisible();
  const a = await solicitudes.boundingBox();
  const b = await indicadores.boundingBox();
  expect(a.x).toBeLessThan(b.x);
  expect(Math.abs(a.y - b.y)).toBeLessThan(40);

  await page.setViewportSize({ width: 360, height: 900 });
  const apilada = await solicitudes.boundingBox();
  const debajo = await indicadores.boundingBox();
  expect(apilada.y).toBeLessThan(debajo.y);
  expect(Math.abs(apilada.x - debajo.x)).toBeLessThan(8);
});

test('fase 12D: el diseñador ve solo las solicitudes por atender, a todo el ancho', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await entrarComo(page, 'diseñador');
  await page.goto('/panel');
  await expect(page.getByRole('region', { name: /Solicitudes por atender/ })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Indicadores del mes' })).toHaveCount(0);
});

test('fase 12D: el origen de cada solicitud lleva color, texto e icono, en la lista de solicitudes', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await entrarComo(page, 'dueño');
  await page.goto('/panel/solicitudes');
  await expect(page.getByRole('heading', { level: 1, name: 'Solicitudes' })).toBeVisible();
  const enLinea = page.getByRole('table').getByText('En línea').first();
  const presencial = page.getByRole('table').getByText('Presencial').first();
  await expect(enLinea).toBeVisible();
  await expect(presencial).toBeVisible();
  const fondo = (etiqueta) => etiqueta.evaluate((n) => getComputedStyle(n.parentElement).backgroundColor);
  expect(await fondo(enLinea)).toBe('rgb(220, 252, 231)');
  expect(await fondo(presencial)).toBe('rgb(254, 249, 195)');
  // Cada etiqueta lleva su icono además del texto.
  expect(await enLinea.evaluate((n) => n.parentElement.querySelectorAll('svg').length)).toBe(1);
  expect(await presencial.evaluate((n) => n.parentElement.querySelectorAll('svg').length)).toBe(1);
  await sinFallasGraves(page);
});

test('fase 12D: el editor de medidas y opciones guarda en milímetros con la versión vista', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await entrarComo(page, 'dueño');
  let envio = null;
  await page.route('**/api/v1/productos/1/medidas-y-opciones', (ruta) => {
    envio = { cuerpo: ruta.request().postDataJSON(), version: ruta.request().headers()['if-match'] };
    return ruta.fulfill({ json: RESPUESTAS['/api/v1/productos/1'] });
  });
  await page.goto('/panel/mantenedores/tipos-de-letrero/1');
  await expect(page.getByRole('heading', { name: 'Medidas y opciones' })).toBeVisible();
  await expect(page.getByRole('textbox', { name: 'Ancho mínimo (cm)' })).toHaveValue('80');

  await page.getByRole('textbox', { name: 'Ancho mínimo (cm)' }).fill('70,5');
  await page.getByRole('button', { name: 'Agregar valor' }).click();
  await page.getByRole('textbox', { name: 'Valor 3 de Diseño' }).fill('Mixto');
  await sinFallasGraves(page);
  await page.getByRole('button', { name: 'Guardar medidas y opciones' }).click();

  await expect(page.getByText('Medidas y opciones guardadas', { exact: true })).toBeVisible();
  expect(envio.version).toBe('"AAAAAAAAPRO="');
  expect(envio.cuerpo).toEqual({
    anchoMinMm: 705, anchoMaxMm: 1500, altoMinMm: 2000, altoMaxMm: 3000, medidasFijas: [],
    opciones: [{ grupo: 'Diseño', valores: ['Del cliente', 'Desarrollado por la oficina', 'Mixto'] }],
  });
});

test('fase 12D: un nombre con números se rechaza en el registro, junto a su campo y sin cambiar lo escrito', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });
  await page.route('**/api/v1/**', (r) => r.fulfill({ json: RESPUESTAS[new URL(r.request().url()).pathname.replace(/\/$/, '')] ?? [] }));
  await page.goto('/cuenta/registro');
  const nombre = page.getByRole('textbox', { name: /^Nombre/ });
  await expect(nombre).toHaveAttribute('maxlength', '20');
  await nombre.fill('Ana2');
  await page.getByRole('textbox', { name: /^Apellido/ }).fill('Pérez');
  await page.getByRole('button', { name: 'Crear cuenta' }).click();
  await expect(page.getByText('Usa solo letras, hasta 20 caracteres.').first()).toBeVisible();
  await expect(nombre).toHaveAttribute('aria-invalid', 'true');
  await expect(nombre).toHaveValue('Ana2');
});

// ── Fase 13B: reportes y exportaciones (RFS10, RFU24 a RFU26, RFU36, RNFP18) ──

const ARCHIVO_DE_PRUEBA = Buffer.from('PK-archivo-de-prueba');

/** La API de exportaciones simulada: el POST responde 202 y el estado pasa por «procesando» antes de «listo». */
async function exportacionesSimuladas(page, { formato = 'xlsx', fallar = false, consultasAntes = 1 } = {}) {
  const nombre = `reporte-operativo-2026-11-20.${formato}`;
  const trabajoBase = { formato, nombreArchivo: nombre };
  const registro = { pedidos: [], consultas: 0 };
  await page.route('**/api/v1/reportes/*/exportaciones', (ruta) => {
    registro.pedidos.push(ruta.request().postDataJSON());
    return fallar
      ? ruta.fulfill({ status: 500, json: { error: 'El servidor tuvo un problema.', codigo: 'error_interno', nivel: 'api' } })
      : ruta.fulfill({ status: 202, json: trabajo('pendiente', trabajoBase), headers: { Location: '/api/v1/reportes/exportaciones/3f2b6c1e-0000-4000-8000-000000000001' } });
  });
  await page.route('**/api/v1/reportes/exportaciones/*/archivo', (ruta) => ruta.fulfill({
    status: 200, body: ARCHIVO_DE_PRUEBA, headers: { 'Content-Type': 'application/octet-stream', 'Content-Disposition': `attachment; filename=${nombre}` },
  }));
  await page.route(/\/api\/v1\/reportes\/exportaciones\/[0-9a-f-]+$/, (ruta) => {
    registro.consultas += 1;
    return ruta.fulfill({ json: registro.consultas <= consultasAntes
      ? trabajo('procesando', trabajoBase)
      : trabajo('listo', { ...trabajoBase, fechaTermino: '2026-11-20T14:35:09-03:00', filas: 6, tamanoBytes: ARCHIVO_DE_PRUEBA.length }) });
  });
  return registro;
}

for (const ancho of [360, 768, 1440]) {
  test(`reportes a ${ancho} px: pedir un Excel, ver cómo se prepara y bajarlo, sin desborde ni fallas graves de accesibilidad`, async ({ page }) => {
    await page.setViewportSize({ width: ancho, height: 900 });
    await entrarComo(page, 'dueño');
    const registro = await exportacionesSimuladas(page);
    await page.goto('/panel/reportes?anio=2026&mes=11');
    await expect(page.getByRole('table', { name: 'Pedidos' })).toBeVisible();
    await sinFallasGraves(page);

    const descarga = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Exportar a Excel' }).click();

    // Mientras se prepara: el botón dice «Preparando…», los tres quedan inactivos y la pantalla sigue usable.
    const preparando = page.getByRole('button', { name: /Preparando/ });
    await expect(preparando).toBeVisible();
    await expect(preparando).toHaveAttribute('aria-disabled', 'true');
    await expect(page.getByRole('button', { name: 'Exportar a CSV' })).toHaveAttribute('aria-disabled', 'true');
    await expect(page.getByRole('button', { name: 'Exportar a PDF' })).toHaveAttribute('aria-disabled', 'true');
    await expect(page.getByRole('status').filter({ hasText: 'Preparando tu archivo' })).toBeVisible();
    await sinFallasGraves(page);

    expect((await descarga).suggestedFilename()).toBe('reporte-operativo-2026-11-20.xlsx');
    await expect(page.getByRole('status').filter({ hasText: 'Tu archivo está listo' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Descargar de nuevo' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Exportar a CSV' })).not.toHaveAttribute('aria-disabled', 'true');
    // Los filtros de pantalla viajaron tal cual y solo los que el operativo admite.
    expect(registro.pedidos).toEqual([{ formato: 'xlsx', filtros: { periodo: 'mes', anio: '2026', mes: '11' } }]);
    await sinFallasGraves(page);
  });
}

test('reportes: un fallo al pedir el archivo es una alerta con el motivo y «Reintentar», sin perder los filtros', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });
  await entrarComo(page, 'dueño');
  await exportacionesSimuladas(page, { fallar: true });
  await page.goto('/panel/reportes?anio=2026&mes=11&estadoPago=por_cobrar');
  await expect(page.getByRole('table', { name: 'Pedidos' })).toBeVisible();

  await page.getByRole('button', { name: 'Exportar a PDF' }).click();

  const alerta = page.getByRole('alert').filter({ hasText: 'No pudimos preparar tu archivo' });
  await expect(alerta).toBeVisible();
  await expect(alerta).toContainText('El servidor tuvo un problema.');
  await expect(alerta.getByRole('button', { name: 'Reintentar' })).toBeVisible();
  await expect(page).toHaveURL(/estadoPago=por_cobrar/);
  await sinFallasGraves(page);
});

test('reportes: el gerencial lleva su resumen, sus gráficos con su tabla y el margen con los otros costos a la vista', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });
  await entrarComo(page, 'dueño');
  await page.goto('/panel/reportes');
  await page.getByRole('tab', { name: 'Gerencial' }).click();

  await expect(page).toHaveURL(/reporte=gerencial/);
  await expect(page.getByRole('table', { name: 'Resumen del período' })).toBeVisible();
  await expect(page.getByText('Margen (ingresos con costo − materiales − otros costos)').first()).toBeVisible();
  await expect(page.getByRole('img', { name: /Ingresos y costos por tramo/ })).toBeVisible();
  await expect(page.getByRole('img', { name: /Ingresos por tipo de letrero/ })).toBeVisible();
  await expect(page.getByRole('table', { name: 'Pedidos entregados' })).toBeVisible();
  await sinFallasGraves(page);
});

test('reportes: el diseñador ve solo el operativo; el cajón del teléfono lleva la entrada «Reportes»', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });
  await entrarComo(page, 'diseñador');
  await page.goto('/panel/reportes?reporte=gerencial');

  await expect(page.getByRole('heading', { level: 1, name: 'Reportes' })).toBeVisible();
  await expect(page.getByRole('tab')).toHaveCount(1);
  await expect(page.getByRole('tab', { name: 'Operativo' })).toBeVisible();
  await expect(page.getByRole('table', { name: 'Pedidos' })).toBeVisible();
  await expect(page).not.toHaveURL(/reporte=gerencial/);

  await page.getByRole('button', { name: 'Menú' }).click();
  await expect(page.getByRole('dialog').getByRole('link', { name: 'Reportes' })).toBeVisible();
  await sinFallasGraves(page);
});

test('reportes: el dashboard enlaza a la cobranza con el período y el alcance que tiene', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await entrarComo(page, 'dueño');
  await page.goto('/panel/indicadores?alcanceSaldo=vigente');
  const enlace = page.getByRole('link', { name: 'Exportar cobranza' });
  await expect(enlace).toBeVisible();

  await enlace.click();

  await expect(page).toHaveURL(/\/panel\/reportes\?/);
  await expect(page).toHaveURL(/reporte=parametrizable/);
  await expect(page).toHaveURL(/estadoPago=por_cobrar/);
  await expect(page).toHaveURL(/alcanceSaldo=vigente/);
  await expect(page.getByRole('table', { name: 'Pedidos por cobrar' })).toBeVisible();
});

test('reportes: al imprimir queda una hoja limpia, con título, emisión y filtros, y sin menú ni botones', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await entrarComo(page, 'dueño');
  await page.goto('/panel/reportes?anio=2026&mes=11');
  await expect(page.getByRole('table', { name: 'Pedidos' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Exportar a Excel' })).toBeVisible();

  await page.emulateMedia({ media: 'print' });

  const hoja = page.getByRole('region', { name: 'Datos para imprimir' });
  await expect(hoja).toBeVisible();
  await expect(hoja.getByRole('heading', { name: 'Reporte operativo' })).toBeVisible();
  await expect(hoja).toContainText('Emitido el 20-11-2026 14:35');
  await expect(hoja).toContainText('noviembre de 2026');
  await expect(page.getByRole('table', { name: 'Pedidos' })).toBeVisible();
  await expect(page.getByRole('table', { name: 'Entregas' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Exportar a Excel' })).toBeHidden();
  await expect(page.getByRole('button', { name: 'Imprimir' })).toBeHidden();
  await expect(page.getByRole('navigation', { name: 'Panel del taller' })).toBeHidden();
  await expect(page.getByRole('tablist')).toBeHidden();
  await expect(page.getByRole('search', { name: 'Filtros del reporte' })).toBeHidden();
  await page.emulateMedia({ media: 'screen' });
});

test('reportes: el papel sale apaisado, con el título del reporte, sin el enlace de salto y con las tablas como tablas', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await entrarComo(page, 'dueño');
  // Se viene de otra página que puso su propio título: el papel no puede decir «Auditoría».
  await page.goto('/panel/reportes?reporte=parametrizable&estadoPago=por_cobrar&alcanceSaldo=vigente');
  await expect(page.getByRole('table', { name: 'Pedidos por cobrar' })).toBeVisible();
  await expect(page).toHaveTitle('Reporte parametrizable · SyncTime');

  await page.emulateMedia({ media: 'print' });
  await expect(page.getByRole('link', { name: 'Saltar al contenido' })).toBeHidden();
  const pdf = await page.pdf({ preferCSSPageSize: true });
  if (process.env.PDF_SALIDA) (await import('node:fs')).writeFileSync(process.env.PDF_SALIDA, pdf);
  // A4 apaisado: 842 × 595 puntos (la tabla conserva sus columnas; en vertical se volvía una pila de fichas).
  expect(pdf.toString('latin1')).toMatch(/\/MediaBox\s*\[\s*0\s+0\s+841\.\d+\s+59[45]\.\d+\s*\]/);
  expect(await page.getByRole('table', { name: 'Pedidos por cobrar' }).evaluate((el) => getComputedStyle(el).display)).toBe('table');
  await page.emulateMedia({ media: 'screen' });
});
