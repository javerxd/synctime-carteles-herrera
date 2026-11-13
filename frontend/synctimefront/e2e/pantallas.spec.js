import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

// Recorrido de las pantallas de la revisión (fase 3) con la API simulada: así se prueban sin la
// contraseña de la base demo. Los datos imitan la semilla de demostración.
const hoy = new Date();
const fecha = (mesesAtras, dia) => new Date(hoy.getFullYear(), hoy.getMonth() - mesesAtras, dia, 10).toISOString().slice(0, 19);
const cliente = (nombre, apellido, telefono) => ({ nombre, apellido, telefono, rut: '46.000.011-K' });
const pedido = (id, estadoPedido, estadoPago, total, abonado, meses, c) => ({
  idPedido: id, estadoPedido, estadoPago, origen: id % 3 ? 'cotizacion' : 'directo', fechaPedido: fecha(meses, 5),
  fechaEntregaEstimada: fecha(meses, 15), fechaEntregaReal: estadoPedido === 'entregado' ? fecha(meses, 14) : null,
  montoBaseTrabajo: total, costoInstalacionAdicional: 0, montoTotalFinal: total, montoAbonado: abonado, saldoPendiente: total - abonado,
  modalidadEntrega: 'retiro_local', numeroFactura: null, rowVersion: 'AA==', solicitud: { cliente: c },
  empleadoResponsable: { nombre: 'Hugo', apellido: 'Vera' },
});
const PEDIDOS = [
  pedido(1, 'en_proceso', 'abonado', 185000, 100000, 0, cliente('Panadería', 'El Trigal', '900000211')),
  pedido(2, 'entregado', 'no_pagado', 96000, 0, 0, cliente('Ferretería', 'Biobío', '900000212')),
  pedido(3, 'terminado', 'pagado_total', 275000, 275000, 1, cliente('Clínica Dental', 'Sonríe', '900000213')),
  pedido(4, 'aceptado', 'no_pagado', 54000, 0, 0, cliente('Minimarket', 'Don Lucho', '900000214')),
  pedido(5, 'entregado', 'pagado_total', 410000, 410000, 1, cliente('Transportes', 'Ruiz', '900000215')),
  pedido(6, 'anulado', 'no_pagado', 72000, 0, 2, cliente('Luis', 'Araya', '900000202')),
];
const RESPUESTAS = {
  '/api/v1/pedidos': PEDIDOS,
  '/api/v1/cartel-digital': { abiertoEnLocal: false, mensajeEstado: 'Salimos a una instalación', tiempoEstimadoRegreso: '16:30', fechaUltimaModificacion: fecha(0, 1) },
  '/api/v1/horarios': [1, 2, 3, 4, 5, 6, 7].map((d) => ({ diaSemana: d, nombreDia: ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'][d - 1], horaInicio: d < 7 ? '09:00:00' : null, horaFin: d < 6 ? '18:30:00' : d === 6 ? '14:00:00' : null, abierto: d < 7 })),
  '/api/v1/metricas/conversion-online': [{ anio: hoy.getFullYear(), mes: hoy.getMonth() + 1, totalSolicitudesOnline: 4, solicitudesConcretadasEnPedido: 1, porcentajeConversion: 25 }],
  '/api/v1/productos': [{ idProducto: 1, nombre: 'Cartel metálico impreso' }],
  '/api/v1/catalogo': {
    total: 2, pagina: 1, tamano: 9, totalPaginas: 1,
    elementos: [
      { idCatalogo: 1, titulo: 'Letrero luminoso para panadería', descripcion: 'Caja de luz con acrílico y LED.', idProducto: 1, tipoLetrero: 'Letrero luminoso', categoria: { idCategoria: 1, nombre: 'Luminosos' }, anchoMm: 1200, altoMm: 600, precioReferencial: 85000, imagen: { urlGrande: '/marca/logo-lh-512.webp', urlMiniatura: '/marca/logo-lh-256.webp', ancho: 512, alto: 512, esEjemplo: true } },
      { idCatalogo: 2, titulo: 'Vinilo para vitrina de ferretería', descripcion: 'Vinilo de corte con horario.', idProducto: 2, tipoLetrero: 'Vinilo', categoria: { idCategoria: 2, nombre: 'Vinilos' }, anchoMm: null, altoMm: null, precioReferencial: null, imagen: null },
    ],
  },
  '/api/v1/local/estado': {
    abiertoAhora: false, motivo: 'ausente', dentroDeHorario: true, horaChile: `${hoy.toISOString().slice(0, 10)}T11:30:00`,
    hoy: { diaSemana: 1, nombreDia: 'Lunes', abierto: true, horaInicio: '09:00:00', horaFin: '18:30:00' },
    proximaApertura: null,
    cartel: { abiertoEnLocal: false, mensajeEstado: 'Salimos a una instalación', tiempoEstimadoRegreso: '16:30', fechaUltimaModificacion: fecha(0, 1) },
    semana: [1, 2, 3, 4, 5, 6, 7].map((d) => ({ diaSemana: d, nombreDia: ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'][d - 1], abierto: d < 7, horaInicio: d < 7 ? '09:00:00' : null, horaFin: d < 6 ? '18:30:00' : d === 6 ? '14:00:00' : null })),
  },
  '/api/v1/documentos-legales': ['privacidad', 'terminos', 'marketing', 'cookies'].map((tipo) => ({ tipo, version: '1.0' })),
  '/api/v1/contacto': { recibida: true, mensaje: 'Recibimos tu consulta. Te responderemos a tu correo lo antes posible.' },
  '/api/v1/categorias': [{ idCategoria: 1, nombre: 'Luminosos' }, { idCategoria: 2, nombre: 'Vinilos' }],
};

function tokenDePrueba() {
  const carga = Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url');
  return `cabecera.${carga}.firma`;
}

async function entrarComo(page, rol) {
  await page.route('**/api/v1/**', (ruta) => {
    const url = new URL(ruta.request().url());
    const cuerpo = RESPUESTAS[url.pathname.replace(/\/$/, '')];
    return cuerpo ? ruta.fulfill({ json: cuerpo }) : ruta.fulfill({ status: 404, json: { error: 'No encontrado' } });
  });
  await page.addInitScript(([token, usuario]) => {
    sessionStorage.setItem('synctime_sesion', JSON.stringify({ token, usuario }));
  }, [tokenDePrueba(), { tipo: 'empleado', idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', email: 'dueno@letreria.test', rol }]);
}

const PANTALLAS = [
  ['/', 'Letreros que se ven de lejos.'],
  ['/ingresar', 'Ingresar'],
  ['/personal/ingresar', 'Acceso del personal'],
  ['/privacidad', 'Política de privacidad'],
  ['/terminos', 'Términos y condiciones'],
  ['/panel', 'Hola, Marta'],
  ['/panel/pedidos', 'Pedidos'],
  ['/panel/local', 'Estado del local'],
  ['/panel/indicadores', 'Indicadores'],
  ['/panel/reportes', 'Reportes'],
];

for (const ancho of [360, 768, 1440]) {
  for (const [ruta, titulo] of PANTALLAS) {
    test(`${ruta} a ${ancho} px: carga, sin desborde ni fallas graves de accesibilidad`, async ({ page }) => {
      const errores = [];
      page.on('console', (m) => m.type() === 'error' && errores.push(m.text()));
      await page.setViewportSize({ width: ancho, height: 900 });
      if (ruta.startsWith('/panel')) await entrarComo(page, 'dueño');
      else await page.route('**/api/v1/**', (r) => r.fulfill({ json: RESPUESTAS[new URL(r.request().url()).pathname.replace(/\/$/, '')] ?? [] }));
      await page.goto(ruta);
      await expect(page.getByRole('heading', { level: 1, name: titulo })).toBeVisible();
      await page.waitForLoadState('networkidle');
      expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
      const { violations } = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
      expect(violations.filter((v) => ['critical', 'serious'].includes(v.impact)).map((v) => `${v.id}: ${v.nodes[0]?.target}`)).toEqual([]);
      expect(errores).toEqual([]);
      if (process.env.CAPTURAS) await page.screenshot({ path: `${process.env.CAPTURAS}/${ruta.replaceAll('/', '_')}-${ancho}.png`, fullPage: true });
    });
  }
}

test('el filtro «Por cobrar» deja los pedidos no anulados con saldo y la ficha muestra ambos estados', async ({ page }) => {
  await entrarComo(page, 'administrador');
  await page.goto('/panel/pedidos');
  const filtros = page.getByRole('search', { name: 'Filtros de pedidos' });
  await expect(filtros.getByRole('status')).toHaveText('6 pedidos · $235.000 por cobrar');
  await filtros.getByRole('button', { name: 'Por cobrar' }).click();
  await expect(filtros.getByRole('status')).toHaveText('3 pedidos · $235.000 por cobrar');
  await page.getByRole('button', { name: 'Ver ficha del pedido 2' }).click();
  const ficha = page.getByRole('dialog', { name: 'Pedido N.º 2' });
  await expect(ficha.getByText('Entregado', { exact: true })).toBeVisible();
  await expect(ficha.getByText('No pagado', { exact: true })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(ficha).toBeHidden();
});

test('un operario ve su menú sin Gestión y recibe la página 403 en Indicadores', async ({ page }) => {
  await entrarComo(page, 'operario');
  await page.goto('/panel');
  await expect(page.getByRole('navigation', { name: 'Panel del taller' }).getByRole('link', { name: /Indicadores/ })).toHaveCount(0);
  await page.goto('/panel/indicadores');
  await expect(page.getByRole('heading', { name: 'No tienes permiso para ver esta página' })).toBeVisible();
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

test('la portada abre el modal de solicitud, exige la privacidad y muestra el aviso de contacto directo', async ({ page }) => {
  await page.route('**/api/v1/**', (ruta) => {
    const destino = new URL(ruta.request().url()).pathname.replace(/\/$/, '');
    if (destino === '/api/v1/catalogo') return ruta.fulfill({ json: { elementos: [], total: 0, pagina: 1, tamano: 9, totalPaginas: 0 } });
    return ruta.fulfill({ json: RESPUESTAS[destino] ?? [] });
  });
  await page.goto('/');
  await page.getByRole('main').getByRole('button', { name: 'Solicitar cotización' }).first().click();
  const modal = page.getByRole('dialog', { name: 'Solicitar una cotización' });
  await modal.getByRole('combobox', { name: 'Tipo de letrero' }).selectOption('1');
  await modal.getByRole('textbox', { name: 'Detalle' }).fill('Letrero luminoso para la fachada del local');
  await modal.getByRole('textbox', { name: 'Nombre' }).fill('María Herrera');
  await modal.getByRole('textbox', { name: 'Celular' }).fill('912345678');
  await modal.getByRole('button', { name: 'Enviar solicitud' }).click();
  await expect(modal.getByRole('checkbox', { name: 'Acepto la política de privacidad' })).toHaveAttribute('aria-invalid', 'true');
  await modal.getByRole('checkbox', { name: 'Acepto la política de privacidad' }).check();
  await modal.getByRole('button', { name: 'Enviar solicitud' }).click();
  await expect(modal.getByText('Para esta solicitud es preferible contactarse directamente con el local')).toBeVisible();
});

// ── Fase 5 ──

function simularApiPublica(page) {
  return page.route('**/api/v1/**', (ruta) =>
    ruta.fulfill({ status: ruta.request().method() === 'POST' ? 202 : 200, json: RESPUESTAS[new URL(ruta.request().url()).pathname.replace(/\/$/, '')] ?? [] }));
}

test('la portada muestra el estado del local, la vitrina con su imagen de ejemplo y el horario en hora de Chile', async ({ page }) => {
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

test('el aviso de cookies ofrece dos opciones iguales y no vuelve tras decidir', async ({ page }) => {
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
  await page.goto('/#contacto');
  const formulario = page.locator('#contacto');
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

test('los documentos legales muestran su versión y que son borradores', async ({ page }) => {
  await simularApiPublica(page);
  await page.addInitScript(() => localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false })));
  await page.goto('/privacidad#cookies');
  await expect(page.getByText(/Versión 1\.0/)).toBeVisible();
  await expect(page.getByText('Borrador pendiente de revisión legal')).toBeVisible();
  await expect(page.locator('#cookies')).toBeInViewport();
});
