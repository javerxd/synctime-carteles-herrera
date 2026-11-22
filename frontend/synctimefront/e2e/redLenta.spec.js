import { mkdirSync, writeFileSync } from 'node:fs';
import { test, expect, devices } from '@playwright/test';
import { COMPROBANTE, CLIENTE, MI_CUENTA, RESPUESTAS, destinoDe, entrarComoCliente, tokenDePrueba } from './apiSimulada.js';

// Fase 15B (RNFP27): las rutas clave cargan y funcionan con red lenta, CPU lenta y perfil de teléfono, SIN perder funcionalidad. Se
// prueba el BUILD de producción (puerto 4173, comprimido con gzip), no el servidor de desarrollo, y con la caché vacía (primera visita).
// Los tiempos se miden en la página (FCP, LCP, e INP aproximado con las interacciones que dura 16 ms o más) y se anotan en
// docs/evidencias/red-lenta-15b.md cuando se define EVIDENCIAS. La API va simulada, con la latencia de la red lenta.
//
// Los perfiles de red son los del panel de DevTools de Chrome / Lighthouse; la CPU va a 4 veces más lenta (teléfono de gama media).
const REDES = {
  '3G rápida': { latency: 562, downloadThroughput: (1.6 * 1024 * 1024) / 8, uploadThroughput: (750 * 1024) / 8, texto: '1,6 Mbps ↓ · 0,75 Mbps ↑ · 562 ms' },
  '4G lenta': { latency: 170, downloadThroughput: (4 * 1024 * 1024) / 8, uploadThroughput: (3 * 1024 * 1024) / 8, texto: '4 Mbps ↓ · 3 Mbps ↑ · 170 ms' },
};
const { defaultBrowserType: _a, ...PIXEL } = devices['Pixel 7'];
const { defaultBrowserType: _b, ...IPHONE } = devices['iPhone 14'];
const TELEFONOS = { 'Pixel 7': PIXEL, 'iPhone 14': IPHONE };
const CPU_LENTA = 4;

// Topes de «se puede usar» (el título de la pantalla visible), con la primera visita, en milisegundos. Salieron de medir y dejar margen
// (ver docs/evidencias/red-lenta-15b.md); si una pantalla engorda, esta prueba lo dice.
// FACTOR_TOPES (por ejemplo 1.5) los alarga en un equipo cargado: los tiempos dependen de la máquina, no solo del código.
const FACTOR = Number(process.env.FACTOR_TOPES) || 1;
const TOPE_USABLE_MS = { '3G rápida': 7000 * FACTOR, '4G lenta': 3000 * FACTOR };
const TOPE_LCP_MS = { '3G rápida': 7000 * FACTOR, '4G lenta': 3000 * FACTOR };
const TOPE_INTERACCION_MS = 200 * FACTOR; // el «bueno» del INP

const SOLICITUD = 'Letrero para la fachada de la panadería, con el horario abajo.';
const filas = [];

/** Anota FCP, LCP, CLS y las interacciones en la página, desde antes de que cargue. */
const MEDIDOR = () => {
  window.__medidas = { lcp: 0, cls: 0, interacciones: [] };
  new PerformanceObserver((lista) => { for (const e of lista.getEntries()) window.__medidas.lcp = e.startTime; })
    .observe({ type: 'largest-contentful-paint', buffered: true });
  new PerformanceObserver((lista) => { for (const e of lista.getEntries()) if (!e.hadRecentInput) window.__medidas.cls += e.value; })
    .observe({ type: 'layout-shift', buffered: true });
  new PerformanceObserver((lista) => { for (const e of lista.getEntries()) if (e.interactionId) window.__medidas.interacciones.push(e.duration); })
    .observe({ type: 'event', durationThreshold: 16, buffered: true });
};

async function limitar(page, red) {
  const sesion = await page.context().newCDPSession(page);
  await sesion.send('Network.enable');
  await sesion.send('Network.emulateNetworkConditions', { offline: false, latency: REDES[red].latency, downloadThroughput: REDES[red].downloadThroughput, uploadThroughput: REDES[red].uploadThroughput });
  await sesion.send('Emulation.setCPUThrottlingRate', { rate: CPU_LENTA });
}

/** La API simulada tarda lo que tardaría una ida y vuelta por esa red. */
const conLatenciaDeApi = (page, red) => page.route('**/api/v1/**', async (ruta) => {
  await new Promise((resolver) => setTimeout(resolver, REDES[red].latency));
  // Si la página se cerró durante la espera, no hay a quién responder.
  await ruta.fallback().catch(() => {});
});

const leerMedidas = (page) => page.evaluate(() => {
  const pintura = performance.getEntriesByName('first-contentful-paint')[0];
  const navegacion = performance.getEntriesByType('navigation')[0];
  const recursos = performance.getEntriesByType('resource');
  return {
    fcp: Math.round(pintura?.startTime ?? 0),
    lcp: Math.round(window.__medidas.lcp),
    cls: Number(window.__medidas.cls.toFixed(3)),
    interaccionMaxima: Math.round(Math.max(0, ...window.__medidas.interacciones)),
    bytes: recursos.reduce((suma, r) => suma + (r.encodedBodySize || 0), navegacion?.encodedBodySize ?? 0),
    peticiones: recursos.length + 1,
  };
});

const RUTAS = {
  portada: {
    ruta: '/', titulo: 'Letreros que se ven de lejos.', contexto: 'publico',
    async usar(page) {
      // La vitrina carga con la portada: se filtra por texto, sin tildes.
      const vitrina = page.getByRole('region', { name: /Trabajos/ }).first();
      await page.getByRole('searchbox', { name: 'Buscar trabajos' }).fill('vitrina');
      await expect(vitrina.getByRole('status')).toHaveText('1 trabajo');
    },
  },
  ingreso: {
    ruta: '/ingresar/cliente', titulo: 'Ingresar a tu cuenta', contexto: 'publico',
    async usar(page) {
      await page.getByRole('textbox', { name: 'Correo electrónico' }).fill('rosa@correo.test');
      await page.getByLabel(/^Contraseña/).fill('clave-de-prueba');
      await page.getByRole('button', { name: 'Ingresar' }).click();
      await expect(page.getByRole('heading', { level: 1, name: 'Mi cuenta' })).toBeVisible({ timeout: 20_000 });
    },
  },
  solicitud: {
    ruta: '/cuenta/solicitud', titulo: 'Solicitar una cotización', contexto: 'cliente',
    async usar(page) {
      await page.getByRole('combobox', { name: /Tipo de letrero/ }).selectOption('1');
      await page.getByRole('textbox', { name: /^Ancho/ }).fill('120');
      await page.getByRole('textbox', { name: /^Alto/ }).fill('60');
      await page.getByRole('textbox', { name: /^Cantidad/ }).fill('2');
      await page.getByRole('textbox', { name: /^Descripción/ }).fill(SOLICITUD);
      await page.getByRole('button', { name: 'Enviar solicitud' }).click();
      await expect(page.getByRole('heading', { level: 1, name: 'Recibimos tu solicitud #123' })).toBeVisible({ timeout: 20_000 });
    },
  },
  miCuenta: {
    ruta: '/cuenta', titulo: 'Mi cuenta', contexto: 'cliente',
    async usar(page) {
      await expect(page.getByText('rosa@correo.test').first()).toBeVisible();
    },
  },
};

test.afterAll(() => {
  if (!process.env.EVIDENCIAS || filas.length === 0 || filas.length < 16) return; // un informe parcial (una corrida filtrada) no pisa al completo
  mkdirSync(process.env.EVIDENCIAS, { recursive: true });
  writeFileSync(`${process.env.EVIDENCIAS}/red-lenta-15b.md`, [
    '# Red lenta y perfil de teléfono — fase 15B (RNFP27)', '',
    `Medido con Playwright en Chrome (build de producción con gzip, caché vacía, API simulada con la latencia de la red) el ${new Date().toISOString().slice(0, 10)}.`,
    `CPU ${CPU_LENTA} veces más lenta. Redes: ${Object.entries(REDES).map(([n, r]) => `**${n}** (${r.texto})`).join('; ')}.`,
    '«Usable» = el título de la pantalla está visible desde que se pidió la página. LCP y FCP vienen del navegador; «interacción máx.» es la',
    'interacción más lenta (≥ 16 ms) que el recorrido produjo, un INP aproximado.', '',
    '| Teléfono | Red | Ruta | Usable (ms) | FCP (ms) | LCP (ms) | CLS | Interacción máx. (ms) | Peticiones | Transferido (KB) |',
    '|---|---|---|---:|---:|---:|---:|---:|---:|---:|',
    ...filas.map((f) => `| ${f.telefono} | ${f.red} | ${f.ruta} | ${f.usable} | ${f.fcp} | ${f.lcp} | ${f.cls} | ${f.interaccionMaxima} | ${f.peticiones} | ${(f.bytes / 1024).toFixed(0)} |`), '',
  ].join('\n'));
});

for (const [telefono, perfil] of Object.entries(TELEFONOS)) {
  for (const red of Object.keys(REDES)) {
    test.describe(`${telefono} · ${red}`, () => {
      test.use({ ...perfil });

      for (const [nombre, { ruta, titulo, contexto, usar }] of Object.entries(RUTAS)) {
        test(`${nombre}: carga y funciona sin perder funcionalidad`, async ({ page }) => {
          const hosts = new Set();
          page.on('request', (peticion) => { if (/^https?:/.test(peticion.url())) hosts.add(new URL(peticion.url()).host); });
          await page.addInitScript(MEDIDOR);
          await page.addInitScript(() => localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false })));

          // La sesión y la API simuladas se registran primero; la latencia, después (corre antes y delega).
          if (contexto === 'cliente') await entrarComoCliente(page, { verificado: true });
          else {
            await page.route('**/api/v1/**', (r) => {
              const destino = destinoDe(r);
              if (destino === '/api/v1/auth/login-cliente') return r.fulfill({ json: { token: tokenDePrueba(), tipo: 'cliente', usuario: { ...CLIENTE, correoVerificado: true } } });
              if (destino === '/api/v1/mi-cuenta') return r.fulfill({ json: { ...MI_CUENTA, correoVerificado: true } });
              return r.fulfill({ json: RESPUESTAS[destino] ?? [] });
            });
          }
          if (nombre === 'solicitud') {
            await page.route('**/api/v1/mis/solicitudes', (r) => (r.request().method() === 'POST' ? r.fulfill({ status: 201, json: COMPROBANTE }) : r.fallback()));
          }
          await conLatenciaDeApi(page, red);
          await limitar(page, red);

          const inicio = Date.now();
          await page.goto(ruta);
          await expect(page.getByRole('heading', { level: 1, name: titulo })).toBeVisible({ timeout: 30_000 });
          const usable = Date.now() - inicio;
          await usar(page);
          await page.waitForLoadState('networkidle');

          const medidas = await leerMedidas(page);
          filas.push({ telefono, red, ruta: nombre, usable, ...medidas });
          // Sin pérdida de funcionalidad: el recorrido de arriba ya lo comprobó. Y el rendimiento, dentro de los topes.
          expect(usable, `la pantalla tardó ${usable} ms en poder usarse`).toBeLessThanOrEqual(TOPE_USABLE_MS[red]);
          expect(medidas.lcp, `LCP ${medidas.lcp} ms`).toBeLessThanOrEqual(TOPE_LCP_MS[red]);
          expect(medidas.interaccionMaxima, `la interacción más lenta duró ${medidas.interaccionMaxima} ms`).toBeLessThanOrEqual(TOPE_INTERACCION_MS);
          expect(medidas.cls).toBeLessThanOrEqual(0.1);
          // Ningún servicio de terceros al abrir estas pantallas (Google solo carga al pulsar su botón; el mapa y las fuentes son propios).
          expect([...hosts].filter((h) => h !== 'localhost:4173')).toEqual([]);
        });
      }
    });
  }
}
