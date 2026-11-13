import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

// Revisión automática de la galería del sistema de diseño (fase 2) en un navegador real.
// 640 px equivale a una pantalla de 1280 px con zoom al 200 %; 320 px, al reflujo de WCAG.
const ANCHOS = [320, 360, 640, 768, 1440];
const ETIQUETAS_WCAG = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

function vigilarConsola(page) {
  const errores = [];
  page.on('console', (mensaje) => mensaje.type() === 'error' && errores.push(mensaje.text()));
  page.on('pageerror', (error) => errores.push(error.message));
  return errores;
}

for (const ancho of ANCHOS) {
  test(`a ${ancho} px no hay desplazamiento horizontal ni fallas graves de accesibilidad`, async ({ page }) => {
    await page.setViewportSize({ width: ancho, height: 900 });
    const errores = vigilarConsola(page);
    await page.goto('/galeria.html');
    await expect(page.getByRole('heading', { level: 1, name: 'Sistema de diseño de SyncTime' })).toBeVisible();

    const desborde = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(desborde).toBeLessThanOrEqual(0);

    const { violations } = await new AxeBuilder({ page }).withTags(ETIQUETAS_WCAG).analyze();
    const graves = violations.filter((v) => v.impact === 'critical' || v.impact === 'serious');
    expect(graves.map((v) => `${v.id} (${v.nodes.length}): ${v.nodes[0]?.target}`)).toEqual([]);
    expect(errores).toEqual([]);
  });
}

test('los controles táctiles miden al menos 44 × 44 px en un teléfono', async ({ browser }) => {
  const contexto = await browser.newContext({ viewport: { width: 360, height: 800 }, hasTouch: true, isMobile: true });
  const page = await contexto.newPage();
  await page.goto('/galeria.html');
  const pequenos = await page.evaluate(() => [...document.querySelectorAll('main button, main select, main input:not([type="checkbox"])')]
    .filter((el) => el.offsetParent !== null)
    .map((el) => ({ el, r: el.getBoundingClientRect() }))
    .filter(({ r }) => r.height < 44 || r.width < 44)
    .map(({ el, r }) => `${el.tagName} «${(el.textContent || el.getAttribute('aria-label') || '').trim()}» ${Math.round(r.width)}×${Math.round(r.height)}`));
  expect(pequenos).toEqual([]);
  await contexto.close();
});

test('el diálogo retiene el foco, se cierra con Esc y devuelve el foco a quien lo abrió', async ({ page }) => {
  await page.goto('/galeria.html');
  const abrir = page.getByRole('button', { name: 'Solicitar este trabajo' });
  await abrir.click();
  const dialogo = page.getByRole('dialog', { name: 'Nueva solicitud' });
  await expect(dialogo).toBeVisible();

  for (let i = 0; i < 12; i += 1) {
    await page.keyboard.press('Tab');
    const fuera = await page.evaluate(() => {
      const activo = document.activeElement;
      return Boolean(activo && activo !== document.body && !activo.closest('dialog'));
    });
    expect(fuera).toBe(false);
  }

  await page.keyboard.press('Escape');
  await expect(dialogo).toBeHidden();
  await expect(abrir).toBeFocused();
});

test('al abrir el formulario de solicitud el foco va al primer campo', async ({ page }) => {
  await page.goto('/galeria.html');
  await page.getByRole('button', { name: 'Solicitar este trabajo' }).click();
  await expect(page.getByRole('dialog').getByRole('combobox', { name: 'Tipo de letrero' })).toBeFocused();
});

test('en alto contraste la pestaña activa y la página actual siguen marcadas', async ({ page }) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await page.goto('/galeria.html');
  // En alto contraste los fondos pasan al color del lienzo: el indicador debe distinguirse de él.
  const { indicador, lienzo } = await page.getByRole('tab', { name: 'Este mes' }).evaluate((el) => ({
    indicador: getComputedStyle(el, '::after').backgroundColor,
    lienzo: getComputedStyle(document.body).backgroundColor,
  }));
  expect(indicador).not.toBe('rgba(0, 0, 0, 0)');
  expect(indicador).not.toBe(lienzo);
  const borde = await page.getByRole('dialog', { includeHidden: true }).first()
    .evaluate((el) => getComputedStyle(el).borderTopStyle);
  expect(borde).toBe('solid');
});

test('en el teléfono, recorrer con Tab nunca deja el foco tapado por la barra', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 700 });
  await page.goto('/galeria.html');
  for (let i = 0; i < 25; i += 1) {
    await page.keyboard.press('Tab');
    const tapado = await page.evaluate(() => {
      const activo = document.activeElement;
      const barra = document.querySelector('header');
      if (!activo || activo === document.body || barra.contains(activo)) return false;
      const estilo = getComputedStyle(barra);
      if (estilo.position !== 'sticky' && estilo.position !== 'fixed') return false;
      return activo.getBoundingClientRect().bottom <= barra.getBoundingClientRect().bottom;
    });
    expect(tapado).toBe(false);
  }
});

test('enviar sin aceptar la privacidad anuncia el error; con la casilla marcada se envía', async ({ page }) => {
  await page.goto('/galeria.html');
  await page.getByRole('button', { name: 'Solicitar este trabajo' }).click();
  const dialogo = page.getByRole('dialog', { name: 'Nueva solicitud' });
  const casilla = dialogo.getByRole('checkbox', { name: 'Acepto la política de privacidad' });
  await expect(casilla).not.toBeChecked();

  await dialogo.getByRole('button', { name: 'Enviar solicitud' }).click();
  await expect(casilla).toHaveAttribute('aria-invalid', 'true');
  await expect(dialogo.getByText('Debes aceptar la política de privacidad para enviar la solicitud.')).toBeVisible();

  await casilla.check();
  await dialogo.getByRole('button', { name: 'Enviar solicitud' }).click();
  await expect(dialogo).toBeHidden();
  await expect(page.getByRole('status').filter({ hasText: 'Solicitud enviada (simulada)' })).toBeVisible();
});

test('las pestañas se recorren con las flechas del teclado', async ({ page }) => {
  await page.goto('/galeria.html');
  const lista = page.getByRole('tablist', { name: 'Periodo del resumen' });
  await lista.getByRole('tab', { name: 'Este mes' }).focus();
  await page.keyboard.press('ArrowRight');
  await expect(lista.getByRole('tab', { name: 'Este año' })).toBeFocused();
  await expect(lista.getByRole('tab', { name: 'Este año' })).toHaveAttribute('aria-selected', 'true');
  await page.keyboard.press('Home');
  await expect(lista.getByRole('tab', { name: 'Esta semana' })).toBeFocused();
});

test('el esqueleto brilla, salvo con movimiento reducido, que además quita la compresión del botón', async ({ page }) => {
  const medir = () => page.evaluate(() => {
    const esqueleto = document.querySelector('span[aria-hidden="true"][class*="_esqueleto_"]');
    return {
      animacion: esqueleto ? getComputedStyle(esqueleto).animationName : 'sin esqueleto',
      escala: getComputedStyle(document.documentElement).getPropertyValue('--escala-presion').trim(),
    };
  });

  await page.goto('/galeria.html');
  const normal = await medir();
  expect(normal.animacion).toMatch(/brillo/);
  expect(normal.escala).toBe('0.98');

  await page.emulateMedia({ reducedMotion: 'reduce' });
  const reducido = await medir();
  expect(reducido.animacion).toBe('none');
  expect(reducido.escala).toBe('1');
});

test('el filtro «Por cobrar» deja solo pedidos no anulados con saldo y lo anuncia', async ({ page }) => {
  await page.goto('/galeria.html');
  const filtros = page.getByRole('search', { name: 'Filtros de pedidos' });
  await filtros.getByRole('button', { name: 'Por cobrar' }).click();
  await expect(filtros.getByRole('button', { name: 'Por cobrar' })).toHaveAttribute('aria-pressed', 'true');
  await expect(filtros.getByRole('status')).toHaveText('4 pedidos · $263.000 por cobrar');
  await filtros.getByRole('button', { name: 'Limpiar filtros' }).click();
  await expect(filtros.getByRole('status')).toHaveText('7 pedidos · $263.000 por cobrar');
});
