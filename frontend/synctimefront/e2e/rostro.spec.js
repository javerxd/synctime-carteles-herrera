import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

// Fase 10: rostro con prueba de vida. La API va simulada con rutas interceptadas y la cámara es la falsa de
// Chromium (un patrón de prueba, sin rostro): así se prueban la cámara, la guía, el CSP del motor y la
// accesibilidad del flujo, pero NO la detección ni el giro reales, que exigen una cámara y una persona.
test.use({
  permissions: ['camera'],
  launchOptions: { args: ['--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream'] },
});

const ANCHOS = [360, 768, 1440];
const CONSENTIMIENTO = { version: '2026-11', texto: ['Sistema biométrico: tres muestras cifradas de tu rostro.', 'Finalidad: entrar al panel.'] };
const ROSTRO_PROPIO = {
  disponible: true, activo: false, puedeUsarRostro: true, debeRegistrarseDeNuevo: false, requiereEquipo: true, equipoVigente: false,
  consentimiento: CONSENTIMIENTO,
};
const ROSTROS = [
  {
    idEmpleado: 1, nombre: 'Marta Fuentes', rol: 'dueño', estado: 'activo', fechaRegistro: '2026-11-02T10:00:00',
    fechaConsentimiento: '2026-11-02T10:00:00', registradoPor: 'Marta Fuentes', fechaDesactivacion: null, historial: [],
  },
  {
    idEmpleado: 4, nombre: 'Pedro Soto', rol: 'administrador', estado: 'sin_registro', fechaRegistro: null, fechaConsentimiento: null,
    registradoPor: null, fechaDesactivacion: null, historial: [],
  },
];

const destinoDe = (ruta) => new URL(ruta.request().url()).pathname.replace(/\/$/, '');

function tokenDePrueba() {
  const carga = Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url');
  return `cabecera.${carga}.firma`;
}

async function sinFallasGraves(page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
  const { violations } = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(violations.filter((v) => ['critical', 'serious'].includes(v.impact)).map((v) => `${v.id}: ${v.nodes[0]?.target}`)).toEqual([]);
}

/** Errores de consola y violaciones del CSP (por ejemplo, el worker del motor bloqueado). */
function vigilarErrores(page) {
  const errores = [];
  page.on('console', (m) => m.type() === 'error' && errores.push(m.text()));
  page.on('pageerror', (e) => errores.push(e.message));
  return errores;
}

async function simularApi(page, respuestas, { sesion } = {}) {
  await page.route('**/api/v1/**', (ruta) => {
    const destino = destinoDe(ruta);
    if (destino === '/api/v1/auth/renovar' && sesion) return ruta.fulfill({ json: { token: tokenDePrueba(), ...sesion } });
    const cuerpo = respuestas[destino];
    if (typeof cuerpo === 'function') return cuerpo(ruta);
    return cuerpo !== undefined ? ruta.fulfill({ json: cuerpo }) : ruta.fulfill({ json: [] });
  });
  if (sesion) await page.addInitScript(() => sessionStorage.setItem('synctime_hay_sesion', '1'));
}

const DUENO = { tipo: 'empleado', usuario: { idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', email: 'dueno@letreria.test', rol: 'dueño' } };

for (const ancho of ANCHOS) {
  test(`ingreso con rostro a ${ancho} px: pide el desafío, enciende la cámara espejada, guía y cancela`, async ({ page }) => {
    const errores = vigilarErrores(page);
    const desafios = [];
    await page.setViewportSize({ width: ancho, height: 900 });
    await simularApi(page, {
      '/api/v1/auth/login-facial/desafio': (ruta) => {
        desafios.push(ruta.request().postDataJSON());
        return ruta.fulfill({ json: { desafio: 'desafio-e2e', orden: ['derecha', 'izquierda'], venceEnSegundos: 30 } });
      },
    });
    await page.goto('/personal/ingresar');
    await page.getByLabel('Correo electrónico').fill('dueno@letreria.test');
    await page.getByRole('button', { name: 'Entrar con rostro' }).click();

    await expect(page.getByText('Cámara activa')).toBeVisible();
    await expect(page.getByLabel('Contraseña')).toBeVisible();
    // Con los modelos cargados empieza el paso 1; la cámara falsa no tiene rostro y la guía lo dice.
    await expect(page.getByText('Paso 1 de 3')).toBeVisible({ timeout: 25000 });
    await expect(page.getByTestId('instruccion')).toHaveText(/Pon tu rostro dentro del óvalo|Falta luz|Centra tu rostro/, { timeout: 10000 });
    expect(desafios).toEqual([{ email: 'dueno@letreria.test' }]);
    const transformacion = await page.locator('video').evaluate((v) => getComputedStyle(v).transform);
    expect(transformacion).toBe('matrix(-1, 0, 0, 1, 0, 0)');
    await sinFallasGraves(page);

    await page.getByRole('button', { name: 'Cancelar' }).click();
    await expect(page.getByText('Cámara activa')).toHaveCount(0);
    await expect(page.locator('video')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Entrar con rostro' })).toBeFocused();
    expect(errores.filter((e) => /worker|Content Security Policy|blob:/i.test(e))).toEqual([]);
    expect(errores).toEqual([]);
  });
}

test('sin cámara permitida, el ingreso con rostro lo explica y deja la contraseña', async ({ browser }) => {
  const contexto = await browser.newContext({ permissions: [] });
  const page = await contexto.newPage();
  await page.addInitScript(() => {
    navigator.mediaDevices.getUserMedia = () => Promise.reject(new DOMException('denegado', 'NotAllowedError'));
  });
  await simularApi(page, {});
  await page.goto('/personal/ingresar');
  await page.getByLabel('Correo electrónico').fill('dueno@letreria.test');
  await page.getByRole('button', { name: 'Entrar con rostro' }).click();
  await expect(page.getByRole('alert')).toContainText('no tiene permiso para usar la cámara');
  await expect(page.getByLabel('Contraseña')).toBeEnabled();
  await sinFallasGraves(page);
  await contexto.close();
});

for (const ancho of ANCHOS) {
  test(`registro del propio rostro a ${ancho} px: consentimiento, contraseña y flujo guiado`, async ({ page }) => {
    const errores = vigilarErrores(page);
    await page.setViewportSize({ width: ancho, height: 900 });
    await simularApi(page, { '/api/v1/auth/rostro': ROSTRO_PROPIO, '/api/v1/avisos-taller': { destinos: [], usaRespaldo: false, historial: [] } }, { sesion: DUENO });
    await page.goto('/panel/perfil');
    const seccion = page.getByRole('region', { name: 'Acceso con rostro' });
    await expect(seccion.getByText('Sistema biométrico: tres muestras cifradas de tu rostro.')).toBeVisible();
    await seccion.getByRole('checkbox').check();
    await seccion.getByLabel('Contraseña actual').fill('una-clave-de-prueba');
    await seccion.getByRole('button', { name: 'Registrar mi rostro' }).click();
    await expect(seccion.getByText('Cámara activa')).toBeVisible();
    await expect(seccion.getByText('Paso 1 de 3')).toBeVisible({ timeout: 25000 });
    await sinFallasGraves(page);
    await seccion.getByRole('button', { name: 'Cancelar' }).click();
    await expect(seccion.getByText('Cámara activa')).toHaveCount(0);
    expect(errores).toEqual([]);
  });
}

test('rostros registrados: registrar a otro abre su sección con el foco, y desactivar pide confirmar', async ({ page }) => {
  let desactivado = null;
  await simularApi(page, {
    '/api/v1/rostros': ROSTROS,
    '/api/v1/rostros/calibracion': (ruta) => ruta.fulfill({ status: 404, json: { error: 'No encontrado' } }),
    '/api/v1/auth/rostro': ROSTRO_PROPIO,
    '/api/v1/rostros/1': (ruta) => {
      desactivado = ruta.request().method();
      return ruta.fulfill({ status: 204 });
    },
  }, { sesion: DUENO });
  await page.goto('/panel/rostros');
  await expect(page.getByRole('heading', { level: 1, name: 'Rostros registrados' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Calibrar el rostro' })).toHaveCount(0);

  await page.getByRole('button', { name: 'Registrar rostro de Pedro Soto' }).click();
  const seccion = page.getByRole('region', { name: 'Registrar el rostro de Pedro Soto' });
  await expect(seccion.getByRole('heading', { name: 'Registrar el rostro de Pedro Soto' })).toBeFocused();
  await expect(seccion.getByLabel('Contraseña de Pedro Soto')).toHaveAttribute('autocomplete', 'new-password');
  await sinFallasGraves(page);

  await page.getByRole('button', { name: 'Desactivar el rostro de Marta Fuentes' }).click();
  const dialogo = page.getByRole('dialog', { name: 'Desactivar el rostro de Marta Fuentes' });
  await expect(dialogo.getByRole('button', { name: 'Cancelar' })).toBeFocused();
  // axe mide el contraste con la opacidad del momento: se espera a que termine la entrada del diálogo.
  await page.waitForFunction(() => getComputedStyle(document.querySelector('dialog[open]')).opacity === '1');
  await sinFallasGraves(page);
  await dialogo.getByRole('button', { name: 'Desactivar' }).click();
  await expect(dialogo).toBeHidden();
  expect(desactivado).toBe('DELETE');
});

for (const ancho of ANCHOS) {
  test(`calibración apagada a ${ancho} px: lo explica sin ofrecer intentos`, async ({ page }) => {
    await page.setViewportSize({ width: ancho, height: 900 });
    await simularApi(page, {
      '/api/v1/rostros/calibracion': (ruta) => ruta.fulfill({ status: 404, json: { error: 'No encontrado' } }),
    }, { sesion: DUENO });
    await page.goto('/panel/rostros/calibrar');
    await expect(page.getByRole('heading', { level: 1, name: 'Calibrar el rostro' })).toBeVisible();
    await expect(page.getByText('La calibración está apagada')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Hacer un intento' })).toHaveCount(0);
    await sinFallasGraves(page);
  });
}

test('un operario no ve «Rostros registrados» en el menú ni la sección del rostro en su perfil', async ({ page }) => {
  const pedidas = [];
  page.on('request', (r) => /\/api\/v1\/(auth\/rostro|rostros)/.test(r.url()) && pedidas.push(r.url()));
  await simularApi(page, {}, { sesion: { ...DUENO, usuario: { ...DUENO.usuario, rol: 'operario' } } });
  await page.goto('/panel/perfil');
  await expect(page.getByRole('heading', { level: 1, name: 'Mi perfil' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Panel del taller' }).getByRole('link', { name: /Rostros registrados/ })).toHaveCount(0);
  await expect(page.getByRole('region', { name: 'Acceso con rostro' })).toHaveCount(0);
  expect(pedidas).toEqual([]);
  await page.goto('/panel/rostros');
  await expect(page.getByRole('heading', { name: 'No tienes permiso para ver esta página' })).toBeVisible();
});
