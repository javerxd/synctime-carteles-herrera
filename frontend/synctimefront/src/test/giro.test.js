import { describe, expect, it } from 'vitest';
import { RANGOS_GIRO, calcularGiro, giroEnRango, puntosValidos } from '../rostro/giro';

// Los mismos casos que Negocio/Rostro/Giro.cs (docs/plan/fase-10-especificacion.md, «Medida del giro»).
const ANCHO = 640;
const ALTO = 480;

function puntos({ x0 = 100, x16 = 300, x30 = 200 } = {}) {
  const lista = Array.from({ length: 68 }, () => [200, 200]);
  lista[0] = [x0, 220];
  lista[16] = [x16, 220];
  lista[30] = [x30, 240];
  return lista;
}

describe('índice de giro', () => {
  it('de frente vale 0', () => {
    expect(calcularGiro(puntos({ x30: 200 }), ANCHO, ALTO)).toBeCloseTo(0, 10);
  });

  it('la nariz hacia la derecha de la imagen sin espejo es un giro a la izquierda de la persona (g > 0)', () => {
    expect(calcularGiro(puntos({ x30: 260 }), ANCHO, ALTO)).toBeCloseTo(0.6, 10);
  });

  it('hacia la derecha de la persona es negativo', () => {
    expect(calcularGiro(puntos({ x30: 140 }), ANCHO, ALTO)).toBeCloseTo(-0.6, 10);
  });

  it('se limita a [−1, 1]', () => {
    expect(calcularGiro(puntos({ x30: 320 }), ANCHO, ALTO)).toBe(1);
    expect(calcularGiro(puntos({ x30: 0 }), ANCHO, ALTO)).toBe(-1);
  });

  it('no es válido si la mandíbula mide menos del 10 % del ancho', () => {
    expect(calcularGiro(puntos({ x0: 100, x16: 163 }), ANCHO, ALTO)).toBeNull();
    expect(calcularGiro(puntos({ x0: 300, x16: 100 }), ANCHO, ALTO)).toBeNull();
    expect(calcularGiro(puntos({ x0: 100, x16: 164, x30: 132 }), ANCHO, ALTO)).toBeCloseTo(0, 10);
  });

  it('no es válido con un punto que no es un número finito', () => {
    const conNaN = puntos();
    conNaN[45] = [Number.NaN, 200];
    expect(calcularGiro(conNaN, ANCHO, ALTO)).toBeNull();
    const conInfinito = puntos();
    conInfinito[2] = [200, Number.POSITIVE_INFINITY];
    expect(calcularGiro(conInfinito, ANCHO, ALTO)).toBeNull();
  });

  it('no es válido con un punto fuera del cuadro', () => {
    const afuera = puntos();
    afuera[8] = [200, 481];
    expect(calcularGiro(afuera, ANCHO, ALTO)).toBeNull();
    const negativo = puntos();
    negativo[8] = [-1, 200];
    expect(calcularGiro(negativo, ANCHO, ALTO)).toBeNull();
  });

  it('exige exactamente 68 puntos y un cuadro con medidas', () => {
    expect(puntosValidos(puntos().slice(0, 67), ANCHO, ALTO)).toBe(false);
    expect(puntosValidos(puntos(), 0, ALTO)).toBe(false);
    expect(puntosValidos(null, ANCHO, ALTO)).toBe(false);
    expect(puntosValidos(puntos(), ANCHO, ALTO)).toBe(true);
  });
});

describe('rangos de cada paso', () => {
  it('usa los valores por defecto del servidor', () => {
    expect(RANGOS_GIRO).toEqual({ frenteMaximo: 0.15, giroMinimo: 0.3, giroMaximo: 0.75 });
  });

  it.each([
    ['frente', 0, true], ['frente', 0.15, true], ['frente', -0.16, false],
    ['izquierda', 0.3, true], ['izquierda', 0.75, true], ['izquierda', 0.29, false], ['izquierda', 0.8, false], ['izquierda', -0.5, false],
    ['derecha', -0.3, true], ['derecha', -0.75, true], ['derecha', -0.2, false], ['derecha', 0.5, false],
  ])('%s con g = %s: %s', (paso, g, esperado) => {
    expect(giroEnRango(g, paso)).toBe(esperado);
  });

  it('acepta rangos propios (los de la calibración)', () => {
    expect(giroEnRango(0.25, 'izquierda', { frenteMaximo: 0.1, giroMinimo: 0.2, giroMaximo: 0.6 })).toBe(true);
  });
});
