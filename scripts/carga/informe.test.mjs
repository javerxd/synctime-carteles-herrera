// Pruebas del informe de la prueba de carga (fase 15A). Se ejecutan con: node --test "scripts/carga/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { redactarInforme } from './informe.mjs';

const muestra = (ms, extra = {}) => ({ ruta: 'GET /api/v1/x', estado: 200, ms, esperado: true, perfil: 'visitante', ...extra });

const base = (muestras, extra = {}) => ({
  fecha: '2026-11-21', configuracion: { usuarios: 20, duracionS: 60, calentamientoS: 10, pensarMs: [1000, 3000], limites: 'reales', visitantes: 6, clientes: 6, personal: 8 },
  volumen: { clientes: 2000, solicitudes: 6000, auditoria: 40000, publicaciones: 60 },
  equipo: { sistema: 'Windows_NT 10', procesador: 'CPU de prueba', nucleos: 8, ramTotalMb: 8000, ramLibreMb: 1500 },
  muestras, recursos: null, consultasLentas: [], ...extra,
});

test('el informe dice CUMPLE cuando el 95 % responde en 2 s o menos', () => {
  const texto = redactarInforme(base(Array.from({ length: 100 }, () => muestra(120))));
  assert.match(texto, /\*\*CUMPLE\*\*/);
  assert.doesNotMatch(texto, /NO CUMPLE/);
});

test('el informe dice NO CUMPLE y nombra la ruta que fallo', () => {
  const muestras = [...Array.from({ length: 80 }, () => muestra(100)), ...Array.from({ length: 20 }, () => muestra(4000, { ruta: 'GET /api/v1/lenta' }))];
  const texto = redactarInforme(base(muestras));
  assert.match(texto, /NO CUMPLE/);
  assert.match(texto, /GET \/api\/v1\/lenta/);
});

test('el informe cuenta los 429 y los 500 aparte', () => {
  const muestras = [muestra(5), muestra(5, { estado: 429, esperado: false }), muestra(5, { estado: 500, esperado: false })];
  const texto = redactarInforme(base(muestras));
  assert.match(texto, /429/);
  assert.match(texto, /Límite de tasa \(429\): 1/);
  assert.match(texto, /Errores del servidor \(5xx\): 1/);
});

test('el informe no incluye contrasenas ni correos de las cuentas de prueba', () => {
  const texto = redactarInforme(base([muestra(10)]));
  assert.doesNotMatch(texto, /@carga\.test/);
  assert.doesNotMatch(texto, /password|contraseña:/i);
});
