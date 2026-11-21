import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

// Lee tokens.css como texto y comprueba lo que exige el documento de diseño (valores fijados por el
// cliente) y WCAG 2.2 AA (contrastes medidos). Si alguien cambia un color, esta prueba lo detiene.
// Vitest se ejecuta desde la carpeta del frontend.
const css = readFileSync(resolve(process.cwd(), 'src/estilos/tokens.css'), 'utf8');

const tokens = Object.fromEntries(
  [...css.matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)].map(([, nombre, valor]) => [nombre, valor.trim()]),
);

function resolver(nombre, visitados = new Set()) {
  if (visitados.has(nombre)) throw new Error(`Referencia circular en ${nombre}`);
  visitados.add(nombre);
  const valor = tokens[nombre];
  if (valor === undefined) throw new Error(`Falta el token ${nombre}`);
  const referencia = valor.match(/^var\((--[\w-]+)\)$/);
  return referencia ? resolver(referencia[1], visitados) : valor;
}

function luminancia(hex) {
  const [r, g, b] = [1, 3, 5]
    .map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contraste(a, b) {
  const [claro, oscuro] = [luminancia(resolver(a)), luminancia(resolver(b))].sort((x, y) => y - x);
  return (claro + 0.05) / (oscuro + 0.05);
}

const fondos = ['--color-fondo', '--color-fondo-alterno', '--color-fondo-hundido'];
const tonos = ['neutro', 'info', 'listo', 'exito', 'aviso', 'peligro'];

describe('tokens del sistema de diseño', () => {
  it('respeta los valores fijados por las reglas de diseño del cliente', () => {
    expect(resolver('--color-fondo').toLowerCase()).toBe('#ffffff');
    expect(resolver('--color-fondo-alterno').toLowerCase()).toBe('#f8fafc');
    expect(resolver('--color-fondo-hundido').toLowerCase()).toBe('#f1f5f9');
    expect(resolver('--color-texto').toLowerCase()).toBe('#0f172a');
    expect(resolver('--color-acento').toLowerCase()).toBe('#c2410c');
    expect(resolver('--curva-resorte')).toBe('cubic-bezier(0.16, 1, 0.3, 1)');
    expect(resolver('--escala-presion')).toBe('0.98');
    expect(resolver('--tipografia-familia')).toMatch(/^'Plus Jakarta Sans'/);
    expect(resolver('--tracking-titulo')).toBe('-0.02em');
  });

  it('la tinta supera 12:1 sobre todos los fondos', () => {
    for (const fondo of fondos) expect(contraste('--color-texto', fondo)).toBeGreaterThan(12);
  });

  it('el texto secundario cumple 4,5:1 sobre todos los fondos', () => {
    for (const fondo of fondos) expect(contraste('--color-texto-secundario', fondo)).toBeGreaterThanOrEqual(4.5);
  });

  it('el texto tenue cumple 4,5:1 sobre los fondos donde se permite', () => {
    expect(contraste('--color-texto-tenue', '--color-fondo')).toBeGreaterThanOrEqual(4.5);
    expect(contraste('--color-texto-tenue', '--color-fondo-alterno')).toBeGreaterThanOrEqual(4.5);
  });

  it('el acento se lee como texto y lleva texto blanco legible', () => {
    for (const fondo of fondos) expect(contraste('--color-acento', fondo)).toBeGreaterThanOrEqual(4.5);
    expect(contraste('--color-sobre-acento', '--color-acento')).toBeGreaterThanOrEqual(4.5);
    expect(contraste('--color-sobre-acento', '--color-acento-hover')).toBeGreaterThanOrEqual(4.5);
  });

  it('los bordes de campos y el anillo de foco cumplen 3:1 (componentes de interfaz)', () => {
    for (const fondo of fondos) {
      expect(contraste('--color-borde-control', fondo)).toBeGreaterThanOrEqual(3);
      expect(contraste('--color-foco', fondo)).toBeGreaterThanOrEqual(3);
    }
  });

  it('cada tono de estado se lee sobre su fondo y su versión sólida lleva texto blanco legible', () => {
    for (const tono of tonos) {
      expect(contraste(`--tono-${tono}-texto`, `--tono-${tono}-fondo`)).toBeGreaterThanOrEqual(4.5);
      expect(contraste('--color-sobre-acento', `--tono-${tono}-solido`)).toBeGreaterThanOrEqual(4.5);
    }
  });

  // Fase 12D: el menú lateral azul, el botón rojo de cerrar sesión y las etiquetas de origen de una solicitud.
  it('el menú lateral azul: texto blanco ≥ 4,5:1 en reposo, al pasar y activo; foco blanco ≥ 3:1', () => {
    expect(resolver('--color-lateral-fondo').toLowerCase()).toBe('#1d4ed8');
    for (const fondo of ['--color-lateral-fondo', '--color-lateral-hover', '--color-lateral-activo']) {
      expect(contraste('--color-lateral-texto', fondo), fondo).toBeGreaterThanOrEqual(4.5);
    }
    expect(contraste('--color-lateral-texto-suave', '--color-lateral-fondo')).toBeGreaterThanOrEqual(4.5);
    expect(contraste('--color-lateral-foco', '--color-lateral-fondo')).toBeGreaterThanOrEqual(3);
  });

  it('«Cerrar sesión»: letras blancas sobre rojo ≥ 4,5:1, también al pasar el cursor', () => {
    expect(resolver('--color-lateral-salir').toLowerCase()).toBe('#b91c1c');
    expect(contraste('--color-lateral-texto', '--color-lateral-salir')).toBeGreaterThanOrEqual(4.5);
    expect(contraste('--color-lateral-texto', '--color-lateral-salir-hover')).toBeGreaterThanOrEqual(4.5);
  });

  // Fase 13B: los tres botones de exportación. Parten de los colores de las imágenes de referencia (docs/image) y se
  // oscurecen lo justo para que el texto blanco se lea (las de referencia, más claras, no llegan a 4,5:1).
  it('botones de exportación: Excel verde, CSV azul y PDF rojo con letras blancas ≥ 4,5:1, también al pasar el cursor', () => {
    for (const formato of ['excel', 'csv', 'pdf']) {
      expect(contraste('--color-sobre-exportar', `--color-exportar-${formato}`), formato).toBeGreaterThanOrEqual(4.5);
      expect(contraste('--color-sobre-exportar', `--color-exportar-${formato}-hover`), `${formato} hover`).toBeGreaterThanOrEqual(4.5);
    }
    expect(resolver('--color-exportar-excel').toLowerCase()).toBe('#15803d');
    expect(resolver('--color-exportar-csv').toLowerCase()).toBe('#1d4ed8');
    expect(resolver('--color-exportar-pdf').toLowerCase()).toBe('#b91c1c');
  });

  it('origen de la solicitud: verde («En línea») y amarillo («Presencial») con texto oscuro ≥ 4,5:1', () => {
    expect(contraste('--tono-origen-linea-texto', '--tono-origen-linea-fondo')).toBeGreaterThanOrEqual(4.5);
    expect(contraste('--tono-origen-presencial-texto', '--tono-origen-presencial-fondo')).toBeGreaterThanOrEqual(4.5);
    expect(resolver('--tono-origen-linea-fondo').toLowerCase()).toBe('#dcfce7');
    expect(resolver('--tono-origen-presencial-fondo').toLowerCase()).toBe('#fef9c3');
  });

  it('los semánticos apuntan a primitivos y los componentes nunca usan un color directo', () => {
    const semanticos = Object.keys(tokens).filter((t) => t.startsWith('--color-') || t.startsWith('--tono-'));
    for (const t of semanticos) expect(tokens[t], t).toMatch(/^var\(--primitivo-|^rgb\(/);

    const capaComponentes = css.slice(css.indexOf('Capa 3 · Componentes'));
    expect(capaComponentes.length).toBeGreaterThan(100);
    expect(capaComponentes).not.toMatch(/#[0-9a-fA-F]{3,8}\b/);
    expect(capaComponentes).not.toMatch(/var\(--primitivo-/);
  });
});
