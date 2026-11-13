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
  ['/panel', 'Hola, Marta'],
  ['/panel/pedidos', 'Pedidos'],
  ['/panel/local', 'Estado del local'],
  ['/panel/indicadores', 'Indicadores'],
  ['/panel/reportes', 'Reportes'],
];

for (const ancho of [360, 1440]) {
  for (const [ruta, titulo] of PANTALLAS) {
    test(`${ruta} a ${ancho} px: carga, sin desborde ni fallas graves de accesibilidad`, async ({ page }) => {
      const errores = [];
      page.on('console', (m) => m.type() === 'error' && errores.push(m.text()));
      await page.setViewportSize({ width: ancho, height: 900 });
      await entrarComo(page, 'dueño');
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

test('la portada abre el modal de solicitud, exige la privacidad y muestra el aviso de contacto directo', async ({ page }) => {
  await page.route('**/api/v1/**', (ruta) => {
    const destino = new URL(ruta.request().url()).pathname.replace(/\/$/, '');
    if (destino === '/api/v1/catalogo') return ruta.fulfill({ json: [] });
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
