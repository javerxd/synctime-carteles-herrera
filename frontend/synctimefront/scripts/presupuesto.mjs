// Fase 15B (RNFP24): presupuesto de peso del JavaScript. Lee el manifiesto de Vite (`build.manifest`) de una carpeta construida, mide cada
// archivo comprimido con gzip y lo compara con el presupuesto. La prueba src/test/fase15bPresupuesto.test.js construye la aplicación y
// falla si algo se pasa; también se puede mirar a mano:
//
//   npx vite build --outDir ../../.tmp-dist --manifest && node scripts/presupuesto.mjs ../../.tmp-dist
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { gzipSync } from 'node:zlib';
import { fileURLToPath } from 'node:url';

const KB = 1024;

/**
 * El presupuesto (todo en bytes COMPRIMIDOS con gzip, como los muestra Vite):
 * - `inicialJs`: lo que baja una persona al abrir cualquier página, antes de ver nada. 150 KB desde la fase 3 (RNFP24).
 * - `inicialCss`: el CSS que viaja con eso.
 * - `porRutaJs`: lo que una pantalla suma, además del inicial, incluyendo los fragmentos compartidos que todavía no se habían bajado.
 * - `bajoDemanda`: piezas pesadas que una pantalla carga solo cuando se usan (el mapa, el reconocimiento facial), cada una con su tope.
 *   Una pieza dinámica que no es una pantalla y no tiene tope aquí hace fallar la prueba: obliga a decidir.
 */
export const PRESUPUESTO = {
  inicialJs: 150 * KB,
  inicialCss: 20 * KB,
  porRutaJs: 40 * KB,
  bajoDemanda: [
    { patron: /MapaInteractivo|leaflet/i, maximoJs: 60 * KB },
    { patron: /face-api/i, maximoJs: 350 * KB },
  ],
};

// Una pantalla es un módulo de src/paginas que no es una pieza pesada con tope propio (el mapa vive en src/paginas, pero se carga bajo demanda).
const esPantalla = (nombre) => nombre.startsWith('src/paginas/') && !PRESUPUESTO.bajoDemanda.some((r) => r.patron.test(nombre));
const kb = (bytes) => `${(bytes / KB).toFixed(1).replace('.', ',')} KB`;

/** Todos los fragmentos que `clave` trae de forma estática (sí mismo incluido), sin repetir. */
function conImportaciones(manifiesto, clave, vistos = new Set()) {
  if (vistos.has(clave) || !manifiesto[clave]) return vistos;
  vistos.add(clave);
  for (const hija of manifiesto[clave].imports ?? []) conImportaciones(manifiesto, hija, vistos);
  return vistos;
}

const pesar = (manifiesto, claves, tamano) => {
  const archivosJs = [...new Set([...claves].map((c) => manifiesto[c]?.file).filter((f) => f?.endsWith('.js')))];
  const archivosCss = [...new Set([...claves].flatMap((c) => manifiesto[c]?.css ?? []))];
  return {
    archivosJs, archivosCss,
    js: archivosJs.reduce((suma, f) => suma + tamano(f), 0),
    css: archivosCss.reduce((suma, f) => suma + tamano(f), 0),
  };
};

/**
 * Mide el manifiesto de Vite: { inicial, rutas, bajoDemanda }. `tamano(archivo)` da los bytes comprimidos de un archivo del build.
 * `esRuta(nombre)` separa las pantallas (rutas) de las piezas que se cargan bajo demanda dentro de una pantalla.
 */
export function analizarManifiesto(manifiesto, tamano, { esRuta = esPantalla } = {}) {
  const entrada = Object.keys(manifiesto).find((c) => manifiesto[c].isEntry);
  if (!entrada) throw new Error('El manifiesto no tiene una entrada: ¿se construyó con build.manifest?');
  const clavesIniciales = conImportaciones(manifiesto, entrada);
  const inicial = pesar(manifiesto, clavesIniciales, tamano);

  const dinamicas = Object.keys(manifiesto).filter((c) => manifiesto[c].isDynamicEntry);
  const medir = (clave) => {
    const propias = new Set([...conImportaciones(manifiesto, clave)].filter((c) => !clavesIniciales.has(c)));
    return { nombre: manifiesto[clave].src ?? clave, ...pesar(manifiesto, propias, tamano) };
  };
  const medidas = dinamicas.map(medir);
  return {
    inicial,
    rutas: medidas.filter((m) => esRuta(m.nombre)).sort((a, b) => b.js - a.js),
    bajoDemanda: medidas.filter((m) => !esRuta(m.nombre)).sort((a, b) => b.js - a.js),
  };
}

/** Lo que se pasa del presupuesto, en palabras: [] si todo cabe. */
export function evaluarPresupuesto({ inicial, rutas, bajoDemanda }, presupuesto = PRESUPUESTO) {
  const marcas = [];
  if (inicial.js > presupuesto.inicialJs) marcas.push(`El JavaScript inicial pesa ${kb(inicial.js)} comprimidos y el presupuesto es ${kb(presupuesto.inicialJs)}.`);
  if (inicial.css > presupuesto.inicialCss) marcas.push(`El CSS inicial pesa ${kb(inicial.css)} comprimidos y el presupuesto es ${kb(presupuesto.inicialCss)}.`);
  for (const ruta of rutas) {
    if (ruta.js > presupuesto.porRutaJs) marcas.push(`La ruta ${ruta.nombre} suma ${kb(ruta.js)} de JavaScript comprimido y el tope por ruta es ${kb(presupuesto.porRutaJs)}.`);
  }
  for (const pieza of bajoDemanda) {
    const regla = presupuesto.bajoDemanda.find((r) => r.patron.test(pieza.nombre) || pieza.archivosJs.some((f) => r.patron.test(f)));
    if (!regla) marcas.push(`La pieza ${pieza.nombre} (${kb(pieza.js)}) se carga bajo demanda y no tiene tope en el presupuesto.`);
    else if (pieza.js > regla.maximoJs) marcas.push(`La pieza ${pieza.nombre} suma ${kb(pieza.js)} y su tope es ${kb(regla.maximoJs)}.`);
  }
  return marcas;
}

/** El resumen que se pega en el informe: el inicial, las rutas más pesadas y las piezas bajo demanda. */
export function resumenTexto({ inicial, rutas, bajoDemanda }, presupuesto = PRESUPUESTO) {
  const lineas = [
    `JavaScript inicial: ${kb(inicial.js)} comprimidos (presupuesto ${kb(presupuesto.inicialJs)}) · CSS inicial: ${kb(inicial.css)} (presupuesto ${kb(presupuesto.inicialCss)})`,
    `Rutas más pesadas (tope ${kb(presupuesto.porRutaJs)} de JavaScript cada una):`,
    ...rutas.slice(0, 8).map((r) => `  ${r.nombre}: ${kb(r.js)} JS + ${kb(r.css)} CSS`),
    'Piezas bajo demanda:',
    ...bajoDemanda.map((p) => `  ${p.nombre}: ${kb(p.js)}`),
  ];
  return lineas.join('\n');
}

// Uso a mano: node scripts/presupuesto.mjs <carpeta construida con manifiesto>
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const carpeta = process.argv[2];
  const ruta = carpeta && join(carpeta, '.vite', 'manifest.json');
  if (!ruta || !existsSync(ruta)) {
    console.error('Uso: node scripts/presupuesto.mjs <carpeta construida con --manifest>');
    process.exit(2);
  }
  const analisis = analizarManifiesto(JSON.parse(readFileSync(ruta, 'utf8')), (archivo) => gzipSync(readFileSync(join(carpeta, archivo))).length);
  console.info(resumenTexto(analisis));
  const marcas = evaluarPresupuesto(analisis);
  marcas.forEach((m) => console.error(`✘ ${m}`));
  process.exit(marcas.length ? 1 : 0);
}
