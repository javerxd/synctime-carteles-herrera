import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { FICHA_PEDIDO, entrarComo, paginaDe } from './apiSimulada.js';
import { REDIRECCIONES, PANTALLAS, prepararApi } from './rutas.js';

// Fase 15B (RNFP15, RNFE08, RNFP03): lo que el recorrido de axe por todas las rutas (pantallas.spec.js) no cubre: las direcciones antiguas
// que redirigen, los estados (diálogo abierto, errores de un formulario, lista vacía), el recorrido con teclado, el reflujo a 320 px (el
// zoom de 400 %) y el alto contraste forzado. axe: reglas WCAG 2.2 A y AA, sin críticas ni serias; y sin desplazamiento horizontal.
const ANCHOS = [360, 768, 1440];
const titulos = new Map(PANTALLAS);

async function sinFallasGraves(page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
  const { violations } = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(violations.filter((v) => ['critical', 'serious'].includes(v.impact)).map((v) => `${v.id}: ${v.nodes[0]?.target}`)).toEqual([]);
}

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false })));
});

test.describe('direcciones antiguas', () => {
  for (const [desde, hasta, titulo] of REDIRECCIONES) {
    test(`${desde} redirige a ${hasta}`, async ({ page }) => {
      await prepararApi(page, hasta);
      await page.goto(desde);
      await expect(page).toHaveURL(new RegExp(`${hasta}$`));
      await expect(page.getByRole('heading', { level: 1, name: titulo })).toBeVisible();
    });
  }
});

test.describe('estados que antes no se miraban', () => {
  for (const ancho of ANCHOS) {
    test(`diálogo de abono abierto a ${ancho} px: foco dentro, Esc lo cierra y el foco vuelve al botón`, async ({ page }) => {
      await page.setViewportSize({ width: ancho, height: 900 });
      await entrarComo(page, 'operario');
      await page.route('**/api/v1/pedidos/1', (ruta) => ruta.fulfill({ json: FICHA_PEDIDO }));
      await page.goto('/panel/pedidos/1');
      await expect(page.getByRole('heading', { level: 1, name: 'Pedido #1' })).toBeVisible();
      const boton = page.getByRole('button', { name: 'Registrar abono' });
      await boton.click();
      const dialogo = page.getByRole('dialog', { name: 'Registrar abono del pedido #1' });
      await expect(dialogo.getByRole('textbox', { name: /^Monto/ })).toBeFocused();
      await sinFallasGraves(page);
      // El foco queda atrapado: varias veces Tab y sigue dentro del diálogo.
      for (let i = 0; i < 8; i += 1) {
        await page.keyboard.press('Tab');
        expect(await page.evaluate(() => Boolean(document.activeElement?.closest('dialog')))).toBe(true);
      }
      await page.keyboard.press('Escape');
      await expect(dialogo).toBeHidden();
      await expect(boton).toBeFocused();
    });

    test(`ingreso con errores a ${ancho} px: los errores se anuncian junto a su campo y no hay fallas graves`, async ({ page }) => {
      await page.setViewportSize({ width: ancho, height: 900 });
      await prepararApi(page, '/ingresar/cliente');
      await page.goto('/ingresar/cliente');
      await page.getByRole('button', { name: 'Ingresar' }).click();
      const correo = page.getByRole('textbox', { name: 'Correo electrónico' });
      await expect(correo).toHaveAttribute('aria-invalid', 'true');
      await expect(correo).toBeFocused();
      await expect(correo).toHaveAccessibleDescription(/.+/);
      await sinFallasGraves(page);
    });

    test(`lista vacía (pedidos) a ${ancho} px: lo dice con palabras y no hay fallas graves`, async ({ page }) => {
      await page.setViewportSize({ width: ancho, height: 900 });
      await entrarComo(page, 'dueño');
      await page.route((url) => url.pathname === '/api/v1/pedidos', (ruta) => ruta.fulfill({ json: paginaDe([], { resumen: { totalPorCobrar: 0, cantidadPorCobrar: 0 } }) }));
      await page.goto('/panel/pedidos');
      await expect(page.getByRole('heading', { level: 1, name: 'Pedidos' })).toBeVisible();
      await expect(page.getByText(/no hay pedidos|sin pedidos|no encontramos pedidos/i).first()).toBeVisible();
      await sinFallasGraves(page);
    });
  }
});

// Las pantallas donde una persona con teclado empieza a trabajar.
const CON_TECLADO = ['/', '/ingresar/cliente', '/personal/ingresar', '/cuenta/registro', '/cuenta/solicitud', '/panel', '/panel/pedidos'];

test.describe('teclado: cada Tab cae en algo visible, con nombre, y el foco se ve', () => {
  for (const ruta of CON_TECLADO) {
    test(`${ruta}`, async ({ page }) => {
      await page.setViewportSize({ width: 1280, height: 800 });
      await prepararApi(page, ruta);
      await page.goto(ruta);
      await expect(page.getByRole('heading', { level: 1, name: titulos.get(ruta) })).toBeVisible();
      await page.waitForLoadState('networkidle');
      const vistos = new Set();
      for (let i = 0; i < 45; i += 1) {
        await page.keyboard.press('Tab');
        const foco = await page.evaluate(() => {
          const e = document.activeElement;
          if (!e || e === document.body) return null;
          const caja = e.getBoundingClientRect();
          const nombre = (e.getAttribute('aria-label') || e.labels?.[0]?.textContent || e.textContent || e.getAttribute('title') || e.getAttribute('alt') || '').trim();
          // El foco se ve si el propio control o, hasta tres niveles arriba, su tarjeta tiene contorno o sombra (los enlaces que cubren toda la tarjeta).
          let contorno = false;
          for (let n = e, i = 0; n && i < 4 && !contorno; n = n.parentElement, i += 1) {
            const s = getComputedStyle(n);
            contorno = (s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0) || s.boxShadow !== 'none';
          }
          return { id: `${e.tagName}|${nombre.slice(0, 40)}|${Math.round(caja.x)},${Math.round(caja.y + window.scrollY)}`, visible: caja.width > 0 && caja.height > 0, nombre, contorno };
        });
        if (!foco) break; // el foco salió de la página (a la barra del navegador): se recorrió todo
        if (vistos.has(foco.id)) break; // dio la vuelta
        vistos.add(foco.id);
        expect(foco.visible, `foco invisible: ${foco.id}`).toBe(true);
        expect(foco.nombre, `foco sin nombre: ${foco.id}`).not.toBe('');
        expect(foco.contorno, `el foco no se ve: ${foco.id}`).toBe(true);
      }
      expect(vistos.size).toBeGreaterThan(3);
    });
  }
});

// WCAG 1.4.10: a 320 px de ancho y 256 de alto (400 % de zoom en 1280) el contenido se reordena sin desplazamiento horizontal.
test.describe('reflujo a 320 × 256 px (zoom de 400 %)', () => {
  for (const ruta of ['/', '/ingresar/cliente', '/cuenta/registro', '/cuenta/solicitud', '/panel', '/panel/pedidos', '/panel/indicadores']) {
    test(`${ruta}`, async ({ page }) => {
      await page.setViewportSize({ width: 320, height: 256 });
      await prepararApi(page, ruta);
      await page.goto(ruta);
      await expect(page.getByRole('heading', { level: 1, name: titulos.get(ruta) })).toBeVisible();
      await page.waitForLoadState('networkidle');
      expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
    });
  }
});

test.describe('alto contraste forzado y movimiento reducido', () => {
  for (const ruta of ['/', '/ingresar/cliente', '/cuenta/solicitud', '/panel', '/panel/pedidos']) {
    test(`${ruta}: forced-colors y reduced-motion no rompen la pantalla`, async ({ page }) => {
      await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' });
      await page.setViewportSize({ width: 360, height: 800 });
      await prepararApi(page, ruta);
      await page.goto(ruta);
      await expect(page.getByRole('heading', { level: 1, name: titulos.get(ruta) })).toBeVisible();
      await page.waitForLoadState('networkidle');
      await sinFallasGraves(page);
      // Con movimiento reducido no queda ninguna animación infinita corriendo por sí sola.
      const infinitas = await page.evaluate(() => document.getAnimations().filter((a) => a.effect?.getComputedTiming().iterations === Infinity && a.playState === 'running').length);
      expect(infinitas).toBe(0);
    });
  }
});
