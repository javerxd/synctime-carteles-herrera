// Pruebas de las opciones de linea de comandos de la prueba de carga (fase 15A).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { crearOpciones } from './opciones.mjs';

test('por omision: 20 usuarios (8 visitantes, 6 clientes y 6 del personal), limites reales y 2 minutos', () => {
  const o = crearOpciones([]);
  assert.equal(o.visitantes + o.clientes + o.personal, 20);
  assert.deepEqual([o.visitantes, o.clientes, o.personal], [8, 6, 6]);
  assert.equal(o.limites, 'reales');
  assert.equal(o.duracionS, 120);
  assert.deepEqual(o.pensarMs, [1000, 3000]);
});

test('la suma de usuarios siempre da lo pedido', () => {
  for (const n of [1, 2, 3, 7, 10, 20, 33, 50]) {
    const o = crearOpciones(['--usuarios', String(n)]);
    assert.equal(o.visitantes + o.clientes + o.personal, n, `para ${n}`);
    assert.ok(o.personal >= 1 || n < 3);
  }
});

test('--pensar acepta «min,max» en milisegundos y rechaza lo ilogico', () => {
  assert.deepEqual(crearOpciones(['--pensar', '100,400']).pensarMs, [100, 400]);
  assert.throws(() => crearOpciones(['--pensar', '500,100']), /pensar/);
  assert.throws(() => crearOpciones(['--pensar', 'rapido']), /pensar/);
});

test('--limites solo admite reales u holgados', () => {
  assert.equal(crearOpciones(['--limites', 'holgados']).limites, 'holgados');
  assert.throws(() => crearOpciones(['--limites', 'ninguno']), /limites/);
});

test('rechaza opciones desconocidas y numeros absurdos', () => {
  assert.throws(() => crearOpciones(['--no-existe']), /no-existe/);
  assert.throws(() => crearOpciones(['--usuarios', '0']), /usuarios/);
  assert.throws(() => crearOpciones(['--usuarios', '500']), /usuarios/);
  assert.throws(() => crearOpciones(['--duracion', '-3']), /duracion/);
});

test('el volumen de datos sinteticos tiene tope: es una base temporal, no un estres de disco', () => {
  assert.throws(() => crearOpciones(['--datos-clientes', '10000000']), /datos-clientes/);
  const o = crearOpciones(['--datos-clientes', '1500', '--datos-solicitudes', '4000']);
  assert.equal(o.volumen.clientes, 1500);
  assert.equal(o.volumen.solicitudes, 4000);
});

test('la salida por omision es docs/evidencias/carga-15a.md', () => {
  assert.equal(crearOpciones([]).salida, 'docs/evidencias/carga-15a.md');
  assert.equal(crearOpciones(['--salida', 'otra/ruta.md']).salida, 'otra/ruta.md');
});
