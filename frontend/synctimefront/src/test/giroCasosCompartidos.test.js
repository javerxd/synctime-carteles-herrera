import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { RANGOS_GIRO, calcularGiro, giroEnRango } from '../rostro/giro';

// Los mismos casos que usa Giro.cs en el servidor: un solo archivo para los dos lados.
// Vitest corre desde frontend/synctimefront.
const ruta = resolve(process.cwd(),
  '../../backend/Proyecto-Carteles-herrera-synctime-main/LetrerosHerreraSYNCTIMEapi.UnitTests/CasosGiro.json');
const datos = JSON.parse(readFileSync(ruta, 'utf-8'));
const numero = (v) => (v === 'NaN' ? NaN : v === 'Infinity' ? Infinity : v);

function armar(caso) {
  const puntos = Array.from({ length: caso.cantidad ?? 68 }, () => [caso.ancho / 2, caso.alto / 2]);
  puntos[0][0] = caso.x0;
  puntos[16][0] = caso.x16;
  puntos[30][0] = caso.x30;
  for (const c of caso.cambios ?? []) puntos[c.i] = [numero(c.x), numero(c.y)];
  return puntos;
}

describe('índice de giro con los casos compartidos con el servidor', () => {
  it.each(datos.casos.map((c) => [c.nombre, c]))('%s', (_nombre, caso) => {
    const g = calcularGiro(armar(caso), caso.ancho, caso.alto);
    if (caso.g === null) expect(g).toBeNull();
    else expect(Math.abs(g - caso.g)).toBeLessThanOrEqual(1e-12);
  });

  it('usa los mismos rangos por defecto', () => {
    expect({ ...RANGOS_GIRO }).toEqual(datos.rangosPorDefecto);
  });

  it.each(datos.rangos.map((r) => [r.g, r]))('g = %s cae en el rango esperado', (_g, r) => {
    expect(giroEnRango(r.g, 'frente')).toBe(r.frente);
    expect(giroEnRango(r.g, 'izquierda')).toBe(r.izquierda);
    expect(giroEnRango(r.g, 'derecha')).toBe(r.derecha);
  });
});
