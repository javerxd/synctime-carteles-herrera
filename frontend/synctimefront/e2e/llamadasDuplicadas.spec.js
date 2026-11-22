import { mkdirSync, writeFileSync } from 'node:fs';
import { test, expect } from '@playwright/test';
import {
  CLIENTE, COMPROBANTE, FICHA_PEDIDO, MI_CUENTA, RESPUESTAS, cotizacionDe, destinoDe, entrarComo, entrarComoCliente, tokenDePrueba,
} from './apiSimulada.js';
import { llamadasDuplicadas, registrarLlamadas, resumirLlamadas } from './llamadas.js';
import { PANTALLAS, prepararApi } from './rutas.js';

// Fase 15B (RNFP23): cada pantalla pide sus datos UNA vez al abrirse y cada formulario envía UNA vez. La prueba anota las peticiones a
// /api/v1/** y falla si una idéntica sale dos veces (React StrictMode, que en desarrollo monta dos veces y cancela la primera, se
// descuenta: las canceladas no cuentan). Lo que se repite a propósito va en `PERMITIDAS`, con su motivo.
const PERMITIDAS = [
  // Hoy no hay ninguna: un reintento tras 503 (servicio_ocupado) es una repetición legítima del ENVÍO, pero esta prueba no simula 503.
];

// Las pantallas principales de cada contexto (las demás las cubre el recorrido de accesibilidad, que las abre todas).
const PRINCIPALES = [
  '/', '/ingresar', '/ingresar/cliente', '/consultas', '/personal/ingresar', '/cuenta/registro', '/cuenta/recuperar',
  '/cuenta', '/cuenta/solicitud', '/cuenta/solicitudes', '/cuenta/solicitudes/123',
  '/panel', '/panel/pedidos', '/panel/pedidos/1', '/panel/solicitudes', '/panel/solicitudes/123', '/panel/clientes', '/panel/clientes/9',
  '/panel/cotizaciones', '/panel/cotizaciones/45', '/panel/calendario', '/panel/local', '/panel/indicadores', '/panel/reportes',
  '/panel/perfil', '/panel/empleados', '/panel/mantenedores/categorias', '/panel/auditoria', '/panel/correos-no-enviados',
];
const titulos = new Map(PANTALLAS);
const evidencia = [];

for (const ruta of PRINCIPALES) if (!titulos.has(ruta)) throw new Error(`${ruta} no está en PANTALLAS (e2e/rutas.js)`);

const comoTexto = (duplicadas) => duplicadas.map(({ clave, veces }) => `${veces} × ${clave}`);

/** Espera a que la pantalla se asiente: sin peticiones pendientes y un respiro para captar lecturas repetidas en cadena. */
async function asentar(page) {
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(400);
}

test.afterAll(() => {
  // Solo el recorrido completo en Chrome escribe la evidencia (una corrida filtrada o de otro proyecto no la pisa).
  if (!process.env.EVIDENCIAS || evidencia.length < PRINCIPALES.length + 5 || test.info().project.name !== 'chrome') return;
  const filas = evidencia.map(({ nombre, total, lecturas, escrituras, duplicadas }) =>
    `| ${nombre} | ${total} | ${lecturas} | ${escrituras} | ${duplicadas.length === 0 ? '—' : comoTexto(duplicadas).join('; ')} |`);
  mkdirSync(process.env.EVIDENCIAS, { recursive: true });
  writeFileSync(`${process.env.EVIDENCIAS}/llamadas-15b.md`, [
    '# Llamadas a la API por pantalla y por formulario — fase 15B (RNFP23)', '',
    `Medido con Playwright (Chrome, servidor de desarrollo con StrictMode, API simulada) el ${new Date().toISOString().slice(0, 10)}.`,
    'Una llamada «duplicada» es una petición idéntica (mismo método, dirección, consulta y cuerpo) que sale más de una vez sin una',
    'escritura entre medio; las canceladas por el navegador (StrictMode en desarrollo) no cuentan.', '',
    '| Pantalla o formulario | Llamadas | Lecturas | Escrituras | Duplicadas |', '|---|---:|---:|---:|---|', ...filas, '',
  ].join('\n'));
});

for (const ruta of PRINCIPALES) {
  test(`${ruta}: al abrirse no repite ninguna petición idéntica`, async ({ page }) => {
    const llamadas = registrarLlamadas(page);
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.addInitScript(() => localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false })));
    await prepararApi(page, ruta);
    await page.goto(ruta);
    await expect(page.getByRole('heading', { level: 1, name: titulos.get(ruta) })).toBeVisible();
    await asentar(page);
    const duplicadas = llamadasDuplicadas(llamadas, { permitidas: PERMITIDAS });
    evidencia.push({ nombre: `Abrir ${ruta}`, ...resumirLlamadas(llamadas), duplicadas });
    expect(comoTexto(duplicadas)).toEqual([]);
  });
}

test.describe('formularios: cada envío sale una sola vez', () => {
  test('ingreso del cliente', async ({ page }) => {
    const llamadas = registrarLlamadas(page);
    await page.route('**/api/v1/**', (ruta) => {
      const destino = destinoDe(ruta);
      if (destino === '/api/v1/auth/login-cliente') return ruta.fulfill({ json: { token: tokenDePrueba(), tipo: 'cliente', usuario: { ...CLIENTE, correoVerificado: true } } });
      if (destino === '/api/v1/mi-cuenta') return ruta.fulfill({ json: { ...MI_CUENTA, correoVerificado: true } });
      return ruta.fulfill({ json: RESPUESTAS[destino] ?? [] });
    });
    await page.addInitScript(() => localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false })));
    await page.goto('/ingresar/cliente');
    await page.getByRole('textbox', { name: 'Correo electrónico' }).fill('rosa@correo.test');
    await page.getByLabel(/^Contraseña/).fill('clave-de-prueba');
    await page.getByRole('button', { name: 'Ingresar' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Mi cuenta' })).toBeVisible();
    await asentar(page);
    expect(llamadas.filter((l) => l.metodo === 'POST' && l.url.endsWith('/auth/login-cliente'))).toHaveLength(1);
    const duplicadas = llamadasDuplicadas(llamadas, { permitidas: PERMITIDAS });
    evidencia.push({ nombre: 'Enviar el ingreso del cliente', ...resumirLlamadas(llamadas), duplicadas });
    expect(comoTexto(duplicadas)).toEqual([]);
  });

  test('registro de cliente', async ({ page }) => {
    const llamadas = registrarLlamadas(page);
    await page.route('**/api/v1/**', (ruta) => {
      const destino = destinoDe(ruta);
      if (destino === '/api/v1/auth/registro-cliente') return ruta.fulfill({ status: 201, json: { token: tokenDePrueba(), tipo: 'cliente', usuario: CLIENTE } });
      if (destino === '/api/v1/mi-cuenta') return ruta.fulfill({ json: MI_CUENTA });
      return ruta.fulfill({ json: RESPUESTAS[destino] ?? [] });
    });
    await page.addInitScript(() => localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false })));
    await page.goto('/cuenta/registro');
    await page.getByRole('textbox', { name: /^Nombre/ }).fill('Rosa');
    await page.getByRole('textbox', { name: /^Apellido/ }).fill('Pino');
    await page.getByRole('textbox', { name: /^Teléfono/ }).fill('+56 9 1234 5678');
    await page.getByRole('textbox', { name: /^Correo electrónico/ }).fill('rosa@correo.test');
    await page.getByLabel(/^Contraseña/).fill('una-clave-larga');
    await page.getByRole('checkbox', { name: 'Leí y acepto los términos y condiciones y la política de privacidad' }).check();
    await page.getByRole('button', { name: 'Crear cuenta' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Mi cuenta' })).toBeVisible();
    await asentar(page);
    expect(llamadas.filter((l) => l.metodo === 'POST' && l.url.endsWith('/auth/registro-cliente'))).toHaveLength(1);
    const duplicadas = llamadasDuplicadas(llamadas, { permitidas: PERMITIDAS });
    evidencia.push({ nombre: 'Enviar el registro', ...resumirLlamadas(llamadas), duplicadas });
    expect(comoTexto(duplicadas)).toEqual([]);
  });

  test('solicitud en línea', async ({ page }) => {
    await entrarComoCliente(page, { verificado: true });
    const llamadas = registrarLlamadas(page);
    await page.route('**/api/v1/mis/solicitudes', (ruta) => (ruta.request().method() === 'POST'
      ? ruta.fulfill({ status: 201, json: COMPROBANTE })
      : ruta.fallback()));
    await page.addInitScript(() => localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false })));
    await page.goto('/cuenta/solicitud');
    await page.getByRole('combobox', { name: /Tipo de letrero/ }).selectOption('1');
    await page.getByRole('textbox', { name: /^Ancho/ }).fill('120');
    await page.getByRole('textbox', { name: /^Alto/ }).fill('60');
    await page.getByRole('textbox', { name: /^Cantidad/ }).fill('2');
    await page.getByRole('textbox', { name: /^Descripción/ }).fill('Letrero para la fachada de la panadería, con el horario abajo.');
    await page.getByRole('button', { name: 'Enviar solicitud' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Recibimos tu solicitud #123' })).toBeVisible();
    await asentar(page);
    expect(llamadas.filter((l) => l.metodo === 'POST' && l.url === '/api/v1/mis/solicitudes')).toHaveLength(1);
    const duplicadas = llamadasDuplicadas(llamadas, { permitidas: PERMITIDAS });
    evidencia.push({ nombre: 'Enviar la solicitud en línea', ...resumirLlamadas(llamadas), duplicadas });
    expect(comoTexto(duplicadas)).toEqual([]);
  });

  test('cotización', async ({ page }) => {
    await entrarComo(page, 'operario');
    const llamadas = registrarLlamadas(page);
    await page.route('**/api/v1/solicitudes/123/cotizaciones', (ruta) => ruta.fulfill({ status: 201, json: { ...cotizacionDe(90, 'pendiente'), costoManoObra: 30000, otrosCostos: 0 } }));
    await page.route('**/api/v1/cotizaciones/90', (ruta) => ruta.fulfill({ json: cotizacionDe(90, 'pendiente') }));
    await page.goto('/panel/solicitudes/123/cotizar');
    await expect(page.getByRole('heading', { level: 1, name: 'Cotizar la solicitud #123' })).toBeVisible();
    await page.getByRole('button', { name: 'Agregar material' }).click();
    await page.getByRole('combobox', { name: /^Material/ }).selectOption('1');
    await page.getByRole('textbox', { name: /^Cantidad \(m2\)/ }).fill('1,5');
    await page.getByRole('textbox', { name: /^Medida/ }).fill('120 × 60 cm');
    await page.getByRole('textbox', { name: /^Mano de obra/ }).fill('30.000');
    await page.getByRole('button', { name: 'Emitir cotización' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Cotización #90' })).toBeVisible();
    await asentar(page);
    expect(llamadas.filter((l) => l.metodo === 'POST' && l.url.endsWith('/cotizaciones'))).toHaveLength(1);
    const duplicadas = llamadasDuplicadas(llamadas, { permitidas: PERMITIDAS });
    evidencia.push({ nombre: 'Emitir la cotización', ...resumirLlamadas(llamadas), duplicadas });
    expect(comoTexto(duplicadas)).toEqual([]);
  });

  test('abono de un pedido', async ({ page }) => {
    await entrarComo(page, 'operario');
    const llamadas = registrarLlamadas(page);
    let ficha = FICHA_PEDIDO;
    await page.route('**/api/v1/pedidos/1', (ruta) => ruta.fulfill({ json: ficha }));
    await page.route('**/api/v1/pedidos/1/pagos', (ruta) => {
      ficha = { ...ficha, montoAbonado: 59065, saldoPendiente: 40000 };
      return ruta.fulfill({ status: 201, json: ficha });
    });
    await page.goto('/panel/pedidos/1');
    await expect(page.getByRole('heading', { level: 1, name: 'Pedido #1' })).toBeVisible();
    await page.getByRole('button', { name: 'Registrar abono' }).click();
    const dialogo = page.getByRole('dialog', { name: 'Registrar abono del pedido #1' });
    await dialogo.getByRole('textbox', { name: /^Monto/ }).fill('19.065');
    await dialogo.getByRole('combobox', { name: /^Medio de pago/ }).selectOption('efectivo');
    await dialogo.getByRole('button', { name: 'Registrar abono' }).click();
    await expect(dialogo).toBeHidden();
    await asentar(page);
    expect(llamadas.filter((l) => l.metodo === 'POST' && l.url.endsWith('/pedidos/1/pagos'))).toHaveLength(1);
    const duplicadas = llamadasDuplicadas(llamadas, { permitidas: PERMITIDAS });
    evidencia.push({ nombre: 'Registrar un abono', ...resumirLlamadas(llamadas), duplicadas });
    expect(comoTexto(duplicadas)).toEqual([]);
  });
});
