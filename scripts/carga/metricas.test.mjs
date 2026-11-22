// Pruebas de los calculos del guion de carga (fase 15A). Se ejecutan con: node --test scripts/carga
// Son numeros puros: no usan red, reloj ni base de datos.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { percentil, evaluarMeta, resumirPorRuta, resumirEstados } from './metricas.mjs';

const muestra = (ms, extra = {}) => ({ ruta: 'GET /x', estado: 200, ms, esperado: true, ...extra });

test('percentil: rango mas cercano sobre una lista desordenada', () => {
  const valores = Array.from({ length: 100 }, (_, i) => 100 - i); // 100..1
  assert.equal(percentil(valores, 50), 50);
  assert.equal(percentil(valores, 95), 95);
  assert.equal(percentil(valores, 99), 99);
  assert.equal(percentil(valores, 100), 100);
  assert.equal(percentil([7], 95), 7);
});

test('percentil: una lista vacia no inventa un numero', () => {
  assert.equal(percentil([], 95), null);
});

test('percentil: no cambia la lista que recibe', () => {
  const valores = [3, 1, 2];
  percentil(valores, 50);
  assert.deepEqual(valores, [3, 1, 2]);
});

test('la meta se cumple con 95 de cada 100 respuestas en 2 s o menos', () => {
  const muestras = [...Array(95).fill(0).map(() => muestra(1999)), ...Array(5).fill(0).map(() => muestra(5000))];
  const meta = evaluarMeta(muestras, { umbralMs: 2000, fraccion: 0.95 });
  assert.equal(meta.cumple, true);
  assert.equal(meta.fraccionBuena, 0.95);
});

test('la meta NO se cumple con 94 de cada 100', () => {
  const muestras = [...Array(94).fill(0).map(() => muestra(100)), ...Array(6).fill(0).map(() => muestra(2001))];
  const meta = evaluarMeta(muestras, { umbralMs: 2000, fraccion: 0.95 });
  assert.equal(meta.cumple, false);
  assert.equal(meta.fraccionBuena, 0.94);
});

test('una respuesta rapida pero equivocada (500, 429) no cuenta como buena', () => {
  const muestras = [
    ...Array(90).fill(0).map(() => muestra(50)),
    ...Array(5).fill(0).map(() => muestra(10, { estado: 500, esperado: false })),
    ...Array(5).fill(0).map(() => muestra(10, { estado: 429, esperado: false })),
  ];
  const meta = evaluarMeta(muestras, { umbralMs: 2000, fraccion: 0.95 });
  assert.equal(meta.fraccionBuena, 0.9);
  assert.equal(meta.cumple, false);
});

test('un error de red (sin estado) cuenta como respuesta fallida', () => {
  const muestras = [muestra(100), { ruta: 'GET /x', estado: 0, ms: 30000, esperado: false, error: 'timeout' }];
  const meta = evaluarMeta(muestras, { umbralMs: 2000, fraccion: 0.5 });
  assert.equal(meta.fraccionBuena, 0.5);
});

test('sin muestras la meta no se da por cumplida', () => {
  assert.equal(evaluarMeta([], { umbralMs: 2000, fraccion: 0.95 }).cumple, false);
});

test('resumirPorRuta: mediana, p95, maximo y cuantas pasaron la meta, ordenadas de la mas lenta a la mas rapida', () => {
  const muestras = [
    ...[100, 200, 300, 400].map((ms) => muestra(ms, { ruta: 'GET /rapida' })),
    ...[1000, 2500, 3000, 4000].map((ms) => muestra(ms, { ruta: 'GET /lenta' })),
  ];
  const filas = resumirPorRuta(muestras, { umbralMs: 2000 });
  assert.deepEqual(filas.map((f) => f.ruta), ['GET /lenta', 'GET /rapida']);
  const lenta = filas[0];
  assert.equal(lenta.n, 4);
  assert.equal(lenta.mediana, 2500);
  assert.equal(lenta.p95, 4000);
  assert.equal(lenta.max, 4000);
  assert.equal(lenta.dentroDeLaMeta, 1);
  assert.equal(filas[1].dentroDeLaMeta, 4);
});

test('resumirEstados cuenta cada codigo y marca los 429 aparte', () => {
  const muestras = [muestra(1), muestra(1), muestra(1, { estado: 429, esperado: false }), muestra(1, { estado: 500, esperado: false })];
  const estados = resumirEstados(muestras);
  assert.deepEqual(estados.porCodigo, { 200: 2, 429: 1, 500: 1 });
  assert.equal(estados.limitadas, 1);
  assert.equal(estados.errores5xx, 1);
  assert.equal(estados.inesperadas, 2);
});
