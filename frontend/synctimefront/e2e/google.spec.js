import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

// Fase 14B: ingreso con Google, con el script de Google SIMULADO y la API simulada. Nada llama a Google ni a la API real:
// se intercepta accounts.google.com/gsi/client y se entrega un reemplazo de `google.accounts.id`. El ID de cliente es
// inventado (lo pone playwright.config.js); el verdadero vive solo en .env.local.
//
// Además de la accesibilidad y el desborde, cada prueba comprueba que NO haya violaciones de la política de contenido
// (index.html) ni errores en la consola: es la prueba de que los orígenes que se abrieron son los justos.

const URL_GOOGLE = 'https://accounts.google.com/gsi/client';
const PASE = 'PASE-E2E-SECRETO';
const TOKEN_GOOGLE = 'TOKEN-E2E-DE-GOOGLE';
const CLIENTE = { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', telefono: '912345678', email: 'rosa@correo.test', correoVerificado: true };
const MI_CUENTA = {
  idCliente: 9, nombre: 'Rosa', apellido: 'Pino', rut: null, telefono: '912345678', correo: 'rosa@correo.test',
  direccion: null, correoVerificado: true, marketing: false, avisosEstado: true, googleVinculado: false, tieneContrasena: true,
};
const EMPLEADO = { idEmpleado: 4, nombre: 'Marta', apellido: 'Fuentes', email: 'marta@letreria.test', rol: 'operario' };
const DOCUMENTOS = ['privacidad', 'terminos', 'marketing', 'cookies'].map((tipo) => ({ tipo, version: '1.0' }));

// Reemplazo mínimo de Google Identity Services: guarda lo que la página le pide y dibuja un botón que entrega un token.
const SCRIPT_SIMULADO = `
  window.__google = { configuraciones: [], dibujos: 0, callback: null };
  window.google = { accounts: { id: {
    initialize(configuracion) { window.__google.configuraciones.push({ nonce: configuracion.nonce, client_id: configuracion.client_id }); window.__google.callback = configuracion.callback; },
    renderButton(elemento) {
      window.__google.dibujos += 1;
      elemento.innerHTML = '';
      const boton = document.createElement('button');
      boton.type = 'button';
      boton.textContent = 'Botón de Google simulado';
      boton.style.cssText = 'height:40px;min-width:200px;max-width:100%;padding:0 16px;border:1px solid #747775;border-radius:4px;background:#fff;color:#1f1f1f;font:500 14px Arial,sans-serif;cursor:pointer';
      boton.onclick = () => window.__google.callback({ credential: '${TOKEN_GOOGLE}' });
      elemento.appendChild(boton);
    },
    cancel() {}, disableAutoSelect() {},
  } } };
`;

function tokenDePrueba() {
  const carga = Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url');
  return `cabecera.${carga}.firma`;
}

/** Escucha violaciones de la política de contenido, errores de consola y peticiones a Google. */
async function vigilar(page) {
  const vigilado = { consola: [], solicitudesGoogle: [] };
  page.on('console', (m) => m.type() === 'error' && vigilado.consola.push(m.text()));
  page.on('pageerror', (e) => vigilado.consola.push(`pageerror: ${e.message}`));
  page.on('request', (r) => { if (new URL(r.url()).hostname === 'accounts.google.com') vigilado.solicitudesGoogle.push(r.url()); });
  await page.addInitScript(() => {
    window.__csp = [];
    document.addEventListener('securitypolicyviolation', (e) => window.__csp.push(`${e.violatedDirective} → ${e.blockedURI}`));
  });
  return vigilado;
}

/**
 * Simula Google y la API. `ingreso` responde a POST /auth/google; `completar`, a POST /auth/google/completar.
 * Devuelve lo que la API recibió, para comprobar los cuerpos.
 */
async function simular(page, { google = 'ok', ingreso, completar, miCuenta = MI_CUENTA, sesion = false, vincular, desvincular, perfil = { vinculado: false, correo: null } } = {}) {
  // `sesion`: false | true (cliente) | 'empleado' (fase 14C).
  const recibido = { nonces: 0, ingresos: [], completados: [], vinculaciones: [], desvinculaciones: [], perfilVinculaciones: [], perfilDesvinculaciones: [] };
  if (google === 'ok') await page.route(URL_GOOGLE, (ruta) => ruta.fulfill({ contentType: 'text/javascript', body: SCRIPT_SIMULADO }));
  else await page.route(URL_GOOGLE, (ruta) => ruta.abort('blockedbyclient'));

  await page.route('**/api/v1/**', async (ruta) => {
    const destino = new URL(ruta.request().url()).pathname.replace(/\/$/, '');
    const metodo = ruta.request().method();
    const cuerpo = () => ruta.request().postDataJSON();
    if (destino === '/api/v1/auth/google/nonce') {
      recibido.nonces += 1;
      return ruta.fulfill({ json: { nonce: `nonce-e2e-${recibido.nonces}`, venceEn: new Date(Date.now() + 300_000).toISOString() } });
    }
    if (destino === '/api/v1/auth/google') {
      recibido.ingresos.push(cuerpo());
      return ingreso(ruta);
    }
    if (destino === '/api/v1/auth/google/completar') {
      recibido.completados.push(cuerpo());
      return completar(ruta);
    }
    if (destino === '/api/v1/mi-cuenta/google' && metodo === 'POST') {
      recibido.vinculaciones.push(cuerpo());
      return vincular(ruta);
    }
    if (destino === '/api/v1/mi-cuenta/google' && metodo === 'DELETE') {
      recibido.desvinculaciones.push(cuerpo());
      return desvincular(ruta);
    }
    if (destino === '/api/v1/mi-perfil/google') {
      if (metodo === 'GET') return ruta.fulfill({ json: perfil });
      if (metodo === 'POST') {
        recibido.perfilVinculaciones.push(cuerpo());
        return vincular(ruta);
      }
      recibido.perfilDesvinculaciones.push(cuerpo());
      return desvincular(ruta);
    }
    if (destino === '/api/v1/auth/renovar') {
      if (sesion === 'empleado') return ruta.fulfill({ json: { token: tokenDePrueba(), tipo: 'empleado', usuario: EMPLEADO } });
      return sesion ? ruta.fulfill({ json: { token: tokenDePrueba(), tipo: 'cliente', usuario: CLIENTE } }) : ruta.fulfill({ status: 401, json: { error: 'Sin sesión' } });
    }
    if (destino === '/api/v1/auth/salir') return ruta.fulfill({ status: 204 });
    if (destino === '/api/v1/mi-cuenta') return ruta.fulfill({ json: miCuenta });
    if (destino === '/api/v1/documentos-legales') return ruta.fulfill({ json: DOCUMENTOS });
    return ruta.fulfill({ json: [] });
  });
  if (sesion) await page.addInitScript(() => sessionStorage.setItem('synctime_hay_sesion', '1'));
  return recibido;
}

const sesionDeCliente = () => ({ token: tokenDePrueba(), tipo: 'cliente', usuario: CLIENTE });
const pendiente = () => ({
  estado: 'registro_pendiente', pase: PASE, venceEn: new Date(Date.now() + 600_000).toISOString(), correo: 'nuevo@correo.test', nombre: 'Nora', apellido: '',
});

/** Sin desborde horizontal ni fallas graves de accesibilidad (WCAG 2.2 AA). */
async function sinFallasGraves(page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
  const { violations } = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(violations.filter((v) => ['critical', 'serious'].includes(v.impact)).map((v) => `${v.id}: ${v.nodes[0]?.target}`)).toEqual([]);
}

async function sinViolacionesDePolitica(page, vigilado) {
  expect(await page.evaluate(() => window.__csp)).toEqual([]);
  expect(vigilado.consola.filter((m) => !/Failed to load resource/.test(m))).toEqual([]);
}

/** Lo que el navegador guardó (almacenamiento y dirección): nada de eso puede llevar el pase ni el token de Google. */
async function sinSecretosGuardados(page) {
  const guardado = await page.evaluate(() => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }) + location.href + JSON.stringify(history.state));
  for (const secreto of [PASE, TOKEN_GOOGLE, 'nonce-e2e']) expect(guardado).not.toContain(secreto);
}

for (const ancho of [360, 768, 1440]) {
  test.describe(`a ${ancho} px`, () => {
    test.beforeEach(async ({ page }) => {
      await page.setViewportSize({ width: ancho, height: 900 });
    });

    test('/ingresar/cliente: el botón va arriba del correo, el script de Google solo se pide al pulsar y la política lo permite', async ({ page }) => {
      const vigilado = await vigilar(page);
      await simular(page, { ingreso: (ruta) => ruta.fulfill({ status: 409, json: { error: 'x', codigo: 'cuenta_existente' } }) });
      await page.goto('/ingresar/cliente');

      const boton = page.getByRole('button', { name: 'Continuar con Google' });
      await expect(boton).toBeVisible();
      const correo = page.getByLabel('Correo electrónico');
      expect((await boton.boundingBox()).y).toBeLessThan((await correo.boundingBox()).y);
      await expect(page.getByText('o con tu correo', { exact: true })).toBeVisible();
      // Antes de pulsar, Google no recibe nada: ni el script ni una sola petición.
      expect(vigilado.solicitudesGoogle).toEqual([]);
      await sinFallasGraves(page);

      await boton.click();
      await expect(page.getByRole('button', { name: 'Botón de Google simulado' })).toBeVisible();
      expect(vigilado.solicitudesGoogle).toEqual([URL_GOOGLE]);
      await expect(page.getByText(/para elegir tu cuenta/i)).toBeVisible();
      // El botón del sistema se desmontó: el foco no cae al cuerpo, va a la zona del botón de Google (se lee con su texto).
      await expect(page.getByRole('group', { name: /para elegir tu cuenta/i })).toBeFocused();
      await sinFallasGraves(page);
      await sinViolacionesDePolitica(page, vigilado);
    });

    test('un correo que ya tiene cuenta (409) avisa con texto, pide un nonce nuevo y la contraseña sigue disponible', async ({ page }) => {
      const vigilado = await vigilar(page);
      const recibido = await simular(page, { ingreso: (ruta) => ruta.fulfill({ status: 409, json: { error: 'Ya existe.', codigo: 'cuenta_existente' } }) });
      await page.goto('/ingresar/cliente');
      await page.getByRole('button', { name: 'Continuar con Google' }).click();
      await page.getByRole('button', { name: 'Botón de Google simulado' }).click();

      await expect(page.getByText('Ya tienes una cuenta con este correo.')).toBeVisible();
      await expect(page.getByText('Ingresa con tu contraseña y vincula Google desde Mi cuenta.')).toBeVisible();
      // Tras un fallo el foco va a la zona de avisos, que contiene el mensaje.
      await expect(page.locator('div[tabindex="-1"]:has(> [role="status"])')).toBeFocused();
      await expect(page.getByRole('button', { name: 'Botón de Google simulado' })).toBeVisible();
      expect(recibido.ingresos).toEqual([{ idToken: TOKEN_GOOGLE, nonce: 'nonce-e2e-1' }]);
      expect(recibido.nonces).toBe(2);
      expect(await page.evaluate(() => window.__google.configuraciones.map((c) => c.nonce))).toEqual(['nonce-e2e-1', 'nonce-e2e-2']);
      await expect(page.getByLabel('Contraseña', { exact: true })).toBeEnabled();
      await sinFallasGraves(page);
      await sinSecretosGuardados(page);
      await sinViolacionesDePolitica(page, vigilado);
    });

    test('script bloqueado: aviso claro y el ingreso con contraseña sigue disponible', async ({ page }) => {
      const vigilado = await vigilar(page);
      const recibido = await simular(page, { google: 'bloqueado' });
      await page.goto('/ingresar/cliente');
      await page.getByRole('button', { name: 'Continuar con Google' }).click();

      await expect(page.getByText('No pudimos cargar Google')).toBeVisible();
      await expect(page.getByRole('button', { name: 'Continuar con Google' })).toBeEnabled();
      await expect(page.getByRole('button', { name: 'Ingresar', exact: true })).toBeEnabled();
      expect(recibido.nonces).toBe(0);
      await sinFallasGraves(page);
      await sinViolacionesDePolitica(page, { ...vigilado, consola: vigilado.consola.filter((m) => !/ERR_BLOCKED_BY_CLIENT|accounts\.google\.com/.test(m)) });
    });

    test('correo nuevo: «Completa tu registro», crea la cuenta y la lleva a Mi cuenta sin dejar el pase en la dirección ni en el almacenamiento', async ({ page }) => {
      const vigilado = await vigilar(page);
      const recibido = await simular(page, {
        ingreso: (ruta) => ruta.fulfill({ json: pendiente() }),
        completar: (ruta) => ruta.fulfill({ status: 201, json: sesionDeCliente() }),
      });
      await page.goto('/ingresar/cliente');
      await page.getByRole('button', { name: 'Continuar con Google' }).click();
      await page.getByRole('button', { name: 'Botón de Google simulado' }).click();

      await expect(page.getByRole('heading', { name: 'Completa tu registro' })).toBeVisible();
      await expect(page).toHaveURL(/\/cuenta\/google\/completar$/);
      await expect(page.getByText('nuevo@correo.test')).toBeVisible();
      await expect(page.getByRole('checkbox', { name: /leí y acepto/i })).not.toBeChecked();
      await expect(page.getByRole('checkbox', { name: /quiero recibir novedades/i })).not.toBeChecked();
      await sinFallasGraves(page);
      await sinSecretosGuardados(page);

      // Enviar vacío: el foco va al primer error.
      await page.getByRole('button', { name: 'Crear mi cuenta' }).click();
      await expect(page.getByText('Escribe tu apellido.')).toBeVisible();
      await expect(page.getByLabel('Apellido')).toBeFocused();
      await sinFallasGraves(page);

      await page.getByLabel('Apellido').fill('Vega');
      await page.getByLabel('Teléfono').fill('9 8765 4321');
      await page.getByRole('checkbox', { name: /leí y acepto/i }).check();
      await page.getByRole('button', { name: 'Crear mi cuenta' }).click();

      await expect(page.getByRole('heading', { name: 'Mi cuenta', exact: true })).toBeVisible();
      expect(recibido.completados).toEqual([expect.objectContaining({
        pase: PASE, nombre: 'Nora', apellido: 'Vega', telefono: '9 8765 4321', aceptaTerminosYPrivacidad: true, aceptaMarketing: false,
        versionTerminos: '1.0', versionPrivacidad: '1.0',
      })]);
      await sinSecretosGuardados(page);
      await sinViolacionesDePolitica(page, vigilado);
    });

    test('«Completa tu registro» sin pase (recarga) vuelve al ingreso con un aviso', async ({ page }) => {
      await simular(page);
      await page.goto('/cuenta/google/completar');
      await expect(page).toHaveURL(/\/ingresar\/cliente$/);
      await expect(page.getByText(/tu registro con google venció/i)).toBeVisible();
      await sinFallasGraves(page);
    });

    test('/cuenta/registro: el mismo botón arriba del formulario', async ({ page }) => {
      const vigilado = await vigilar(page);
      await simular(page);
      await page.goto('/cuenta/registro');
      const boton = page.getByRole('button', { name: 'Continuar con Google' });
      await expect(boton).toBeVisible();
      expect((await boton.boundingBox()).y).toBeLessThan((await page.getByLabel(/^Nombre/).boundingBox()).y);
      expect(vigilado.solicitudesGoogle).toEqual([]);
      await sinFallasGraves(page);
    });

    test('Mi cuenta: vincular con la contraseña y desvincular', async ({ page }) => {
      const vigilado = await vigilar(page);
      const recibido = await simular(page, {
        sesion: true,
        vincular: (ruta) => ruta.fulfill({ json: { vinculado: true, correo: 'rosa@correo.test' } }),
        desvincular: (ruta) => ruta.fulfill({ status: 204 }),
      });
      await page.goto('/cuenta');
      const seccion = page.getByRole('region', { name: 'Ingreso con Google' });
      await expect(seccion).toBeVisible();

      // Sin contraseña no se carga Google.
      await seccion.getByRole('button', { name: 'Vincular Google' }).click();
      await expect(seccion.getByText('Escribe tu contraseña actual para confirmar que eres tú.')).toBeVisible();
      await expect(seccion.getByLabel(/Contraseña actual/)).toBeFocused();
      expect(vigilado.solicitudesGoogle).toEqual([]);

      await seccion.getByLabel(/Contraseña actual/).fill('clave-de-prueba');
      await seccion.getByRole('button', { name: 'Vincular Google' }).click();
      await seccion.getByRole('button', { name: 'Botón de Google simulado' }).click();
      await expect(seccion.getByText(/vinculamos tu cuenta de google/i)).toBeVisible();
      expect(recibido.vinculaciones).toEqual([{ idToken: TOKEN_GOOGLE, nonce: 'nonce-e2e-1', passwordActual: 'clave-de-prueba' }]);
      await expect(seccion.getByRole('button', { name: 'Desvincular Google' })).toBeVisible();
      await sinFallasGraves(page);

      await seccion.getByLabel(/Contraseña actual/).fill('clave-de-prueba');
      await seccion.getByRole('button', { name: 'Desvincular Google' }).click();
      await expect(seccion.getByText(/desvinculamos tu cuenta de google/i)).toBeVisible();
      expect(recibido.desvinculaciones).toEqual([{ passwordActual: 'clave-de-prueba' }]);
      await sinSecretosGuardados(page);
      await sinViolacionesDePolitica(page, vigilado);
    });

    test('Mi cuenta sin contraseña: lo explica, no ofrece desvincular y ofrece crear una contraseña', async ({ page }) => {
      await simular(page, { sesion: true, miCuenta: { ...MI_CUENTA, googleVinculado: true, tieneContrasena: false } });
      await page.goto('/cuenta');
      const seccion = page.getByRole('region', { name: 'Ingreso con Google' });
      await expect(seccion.getByText(/no tiene contraseña/i)).toBeVisible();
      await expect(seccion.getByRole('link', { name: 'Crear una contraseña' })).toHaveAttribute('href', '/cuenta/recuperar');
      await expect(seccion.getByRole('button', { name: 'Desvincular Google' })).toHaveCount(0);
      await expect(page.getByRole('button', { name: 'Cambiar contraseña' })).toHaveCount(0);
      await sinFallasGraves(page);
    });

    // ── Fase 14C: el personal ──

    test('/personal/ingresar: el botón va arriba del correo, el script solo se pide al pulsar y la política lo permite', async ({ page }) => {
      const vigilado = await vigilar(page);
      await simular(page, { ingreso: (ruta) => ruta.fulfill({ status: 409, json: { error: 'x', codigo: 'cuenta_personal_existente' } }) });
      await page.goto('/personal/ingresar');

      const boton = page.getByRole('button', { name: 'Continuar con Google' });
      await expect(boton).toBeVisible();
      expect((await boton.boundingBox()).y).toBeLessThan((await page.getByLabel('Correo electrónico').boundingBox()).y);
      await expect(page.getByText('o con tu correo', { exact: true })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Entrar con rostro' })).toBeVisible();
      expect(vigilado.solicitudesGoogle).toEqual([]);
      await sinFallasGraves(page);

      await boton.click();
      await expect(page.getByRole('button', { name: 'Botón de Google simulado' })).toBeVisible();
      expect(vigilado.solicitudesGoogle).toEqual([URL_GOOGLE]);
      await sinFallasGraves(page);
      await sinViolacionesDePolitica(page, vigilado);
    });

    test('un empleado con Google vinculado entra al panel con su sesión de personal', async ({ page }) => {
      const vigilado = await vigilar(page);
      const recibido = await simular(page, {
        ingreso: (ruta) => ruta.fulfill({ json: { token: tokenDePrueba(), tipo: 'empleado', usuario: EMPLEADO } }),
      });
      await page.goto('/personal/ingresar');
      await page.getByRole('button', { name: 'Continuar con Google' }).click();
      await page.getByRole('button', { name: 'Botón de Google simulado' }).click();

      await expect(page).toHaveURL(/\/panel$/);
      expect(recibido.ingresos).toEqual([{ idToken: TOKEN_GOOGLE, nonce: 'nonce-e2e-1' }]);
      await sinSecretosGuardados(page);
      await sinViolacionesDePolitica(page, vigilado);
    });

    test('un correo de empleado sin vincular: avisa que lo vincule desde Mi perfil y la contraseña sigue disponible', async ({ page }) => {
      const vigilado = await vigilar(page);
      const recibido = await simular(page, { ingreso: (ruta) => ruta.fulfill({ status: 409, json: { error: 'Este correo es de una cuenta del personal.', codigo: 'cuenta_personal_existente' } }) });
      await page.goto('/personal/ingresar');
      await page.getByRole('button', { name: 'Continuar con Google' }).click();
      await page.getByRole('button', { name: 'Botón de Google simulado' }).click();

      await expect(page.getByText('Este correo es de una cuenta del personal.')).toBeVisible();
      await expect(page.getByText('Ingresa con tu contraseña y vincula Google desde Mi perfil.')).toBeVisible();
      await expect(page.getByRole('button', { name: 'Botón de Google simulado' })).toBeVisible();
      expect(recibido.nonces).toBe(2);
      await expect(page.getByLabel('Contraseña', { exact: true })).toBeEnabled();
      await sinFallasGraves(page);
      await sinSecretosGuardados(page);
      await sinViolacionesDePolitica(page, vigilado);
    });

    test('Mi perfil: el empleado vincula y desvincula su Google con la contraseña', async ({ page }) => {
      const vigilado = await vigilar(page);
      const recibido = await simular(page, {
        sesion: 'empleado',
        vincular: (ruta) => ruta.fulfill({ json: { vinculado: true, correo: EMPLEADO.email } }),
        desvincular: (ruta) => ruta.fulfill({ status: 204 }),
      });
      await page.goto('/panel/perfil');
      const seccion = page.getByRole('region', { name: 'Ingreso con Google' });
      await expect(seccion).toBeVisible();
      await expect(seccion.getByText(/google no está vinculado/i)).toBeVisible();

      await seccion.getByRole('button', { name: 'Vincular Google' }).click();
      await expect(seccion.getByText('Escribe tu contraseña actual para confirmar que eres tú.')).toBeVisible();
      await expect(seccion.getByLabel(/Contraseña actual/)).toBeFocused();
      expect(vigilado.solicitudesGoogle).toEqual([]);

      await seccion.getByLabel(/Contraseña actual/).fill('clave-de-prueba');
      await seccion.getByRole('button', { name: 'Vincular Google' }).click();
      await seccion.getByRole('button', { name: 'Botón de Google simulado' }).click();
      await expect(seccion.getByText(/vinculamos tu cuenta de google/i)).toBeVisible();
      expect(recibido.perfilVinculaciones).toEqual([{ idToken: TOKEN_GOOGLE, nonce: 'nonce-e2e-1', passwordActual: 'clave-de-prueba' }]);
      await sinFallasGraves(page);

      await seccion.getByLabel(/Contraseña actual/).fill('clave-de-prueba');
      await seccion.getByRole('button', { name: 'Desvincular Google' }).click();
      await expect(seccion.getByText(/desvinculamos tu cuenta de google/i)).toBeVisible();
      expect(recibido.perfilDesvinculaciones).toEqual([{ passwordActual: 'clave-de-prueba' }]);
      await sinSecretosGuardados(page);
      await sinViolacionesDePolitica(page, vigilado);
    });

    test('Mi perfil con Google ya vinculado: ofrece desvincular (sin explicación de contraseña)', async ({ page }) => {
      await simular(page, { sesion: 'empleado', perfil: { vinculado: true, correo: EMPLEADO.email } });
      await page.goto('/panel/perfil');
      const seccion = page.getByRole('region', { name: 'Ingreso con Google' });
      await expect(seccion.getByText(/tu cuenta tiene google vinculado/i)).toBeVisible();
      await expect(seccion.getByRole('button', { name: 'Desvincular Google' })).toBeVisible();
      await expect(seccion.getByRole('link', { name: 'Crear una contraseña' })).toHaveCount(0);
      await sinFallasGraves(page);
    });
  });
}
