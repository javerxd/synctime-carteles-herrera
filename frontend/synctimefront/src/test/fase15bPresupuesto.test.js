import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { gzipSync } from 'node:zlib';
import { PRESUPUESTO, analizarManifiesto, evaluarPresupuesto, resumenTexto } from '../../scripts/presupuesto.mjs';

// Fase 15B (RNFP24): presupuesto de peso del JavaScript. La lógica (analizar el manifiesto de Vite y comparar con el presupuesto) se
// prueba con un manifiesto armado a mano; la última prueba construye la aplicación de verdad y falla si algo se pasa del presupuesto.
const KB = 1024;
const MANIFIESTO = {
  'index.html': { file: 'assets/index.js', isEntry: true, css: ['assets/index.css'], imports: ['_ui.js', '_jsx.js'], dynamicImports: ['src/paginas/Portada.jsx', 'src/paginas/Panel.jsx', 'src/paginas/Visor.jsx'] },
  '_ui.js': { file: 'assets/ui.js', imports: ['_jsx.js'] },
  '_jsx.js': { file: 'assets/jsx.js' },
  '_compartido.js': { file: 'assets/compartido.js', imports: ['_jsx.js'] },
  'src/paginas/Portada.jsx': { file: 'assets/Portada.js', src: 'src/paginas/Portada.jsx', isDynamicEntry: true, imports: ['_ui.js', '_compartido.js'], css: ['assets/Portada.css'], dynamicImports: ['src/paginas/Visor.jsx'] },
  'src/paginas/Panel.jsx': { file: 'assets/Panel.js', src: 'src/paginas/Panel.jsx', isDynamicEntry: true, imports: ['_compartido.js'] },
  'src/paginas/Visor.jsx': { file: 'assets/Visor.js', src: 'src/paginas/Visor.jsx', isDynamicEntry: true },
};
const TAMANOS = {
  'assets/index.js': 60 * KB, 'assets/ui.js': 20 * KB, 'assets/jsx.js': 5 * KB, 'assets/compartido.js': 8 * KB,
  'assets/Portada.js': 12 * KB, 'assets/Panel.js': 15 * KB, 'assets/Visor.js': 200 * KB,
  'assets/index.css': 9 * KB, 'assets/Portada.css': 3 * KB,
};
const tamano = (archivo) => TAMANOS[archivo];
const holgado = { inicialJs: 150 * KB, inicialCss: 20 * KB, porRutaJs: 40 * KB, bajoDemanda: [{ patron: /Visor/, maximoJs: 250 * KB }] };

describe('analizarManifiesto', () => {
  it('el JavaScript inicial es la entrada y todo lo que importa de forma estática, sin duplicar', () => {
    const { inicial } = analizarManifiesto(MANIFIESTO, tamano);
    expect(inicial.archivosJs.sort()).toEqual(['assets/index.js', 'assets/jsx.js', 'assets/ui.js']);
    expect(inicial.js).toBe(85 * KB);
    expect(inicial.css).toBe(9 * KB);
  });

  it('el peso de una ruta suma su fragmento y lo compartido que el inicial no trae, pero no lo que ya se descargó', () => {
    const { rutas } = analizarManifiesto(MANIFIESTO, tamano);
    const portada = rutas.find((r) => r.nombre === 'src/paginas/Portada.jsx');
    expect(portada.archivosJs.sort()).toEqual(['assets/Portada.js', 'assets/compartido.js']);
    expect(portada.js).toBe(20 * KB);
    expect(portada.css).toBe(3 * KB);
    expect(rutas.find((r) => r.nombre === 'src/paginas/Panel.jsx').js).toBe(23 * KB);
  });

  it('separa las rutas de las piezas que se cargan bajo demanda dentro de una pantalla', () => {
    const { rutas, bajoDemanda } = analizarManifiesto(MANIFIESTO, tamano, { esRuta: (nombre) => !/Visor/.test(nombre) });
    expect(rutas.map((r) => r.nombre).sort()).toEqual(['src/paginas/Panel.jsx', 'src/paginas/Portada.jsx']);
    expect(bajoDemanda.map((r) => [r.nombre, r.js])).toEqual([['src/paginas/Visor.jsx', 200 * KB]]);
  });
});

describe('evaluarPresupuesto', () => {
  const analisis = () => analizarManifiesto(MANIFIESTO, tamano, { esRuta: (nombre) => !/Visor/.test(nombre) });

  it('no marca nada cuando todo cabe', () => {
    expect(evaluarPresupuesto(analisis(), holgado)).toEqual([]);
  });

  it('marca el JavaScript inicial que se pasa del presupuesto', () => {
    const marcas = evaluarPresupuesto(analisis(), { ...holgado, inicialJs: 80 * KB });
    expect(marcas).toEqual([expect.stringContaining('JavaScript inicial')]);
  });

  it('marca la ruta que se pasa de su tope y dice cuál es', () => {
    const marcas = evaluarPresupuesto(analisis(), { ...holgado, porRutaJs: 21 * KB });
    expect(marcas).toEqual([expect.stringContaining('src/paginas/Panel.jsx')]);
  });

  it('marca el CSS inicial que se pasa', () => {
    expect(evaluarPresupuesto(analisis(), { ...holgado, inicialCss: 8 * KB })).toEqual([expect.stringContaining('CSS inicial')]);
  });

  it('una pieza bajo demanda sin tope, o que se pasa del suyo, se marca', () => {
    expect(evaluarPresupuesto(analisis(), { ...holgado, bajoDemanda: [] })).toEqual([expect.stringContaining('no tiene tope')]);
    expect(evaluarPresupuesto(analisis(), { ...holgado, bajoDemanda: [{ patron: /Visor/, maximoJs: 100 * KB }] })).toEqual([expect.stringContaining('Visor')]);
  });
});

describe('resumenTexto', () => {
  it('muestra el inicial y las rutas más pesadas en KB comprimidos', () => {
    const texto = resumenTexto(analizarManifiesto(MANIFIESTO, tamano), holgado);
    expect(texto).toContain('85,0 KB');
    expect(texto).toContain('src/paginas/Panel.jsx');
  });
});

describe('la aplicación construida cabe en el presupuesto (RNFP24)', () => {
  it('JavaScript inicial ≤ 150 KB comprimidos y cada ruta dentro de su tope', async () => {
    const carpeta = mkdtempSync(join(tmpdir(), 'synctime-presupuesto-'));
    try {
      // En un proceso aparte y con NODE_ENV=production: dentro de Vitest NODE_ENV vale «test» y el build incluiría React en modo desarrollo
      // (más de 60 KB de más), lo que no es el peso que llega a las personas.
      const vite = join(process.cwd(), 'node_modules', 'vite', 'bin', 'vite.js');
      const resultado = spawnSync(process.execPath, [vite, 'build', '--outDir', carpeta, '--emptyOutDir', '--manifest'], {
        env: { ...process.env, NODE_ENV: 'production' }, encoding: 'utf8',
      });
      expect(resultado.status, resultado.stderr || resultado.stdout).toBe(0);
      const manifiesto = JSON.parse(readFileSync(join(carpeta, '.vite', 'manifest.json'), 'utf8'));
      const analisis = analizarManifiesto(manifiesto, (archivo) => gzipSync(readFileSync(join(carpeta, archivo))).length);
      console.info(resumenTexto(analisis, PRESUPUESTO));
      expect(PRESUPUESTO.inicialJs).toBe(150 * KB);
      expect(evaluarPresupuesto(analisis, PRESUPUESTO)).toEqual([]);
    } finally {
      rmSync(carpeta, { recursive: true, force: true });
    }
  }, 120_000);
});
