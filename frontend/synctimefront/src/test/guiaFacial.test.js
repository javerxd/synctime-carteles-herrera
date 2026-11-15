import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TIEMPOS_FLUJO, evaluarCuadro, ejecutarFlujo } from '../rostro/guiaFacial';
import { calcularGiro } from '../rostro/giro';
import { cuadroCon, personaQueSigue } from './rostroAuxiliares';

// La lógica de los pasos, sin cámara ni React: la detección es simulada.
describe('evaluarCuadro: la guía en vivo', () => {
  it.each([
    ['sin rostro', cuadroCon({ rostros: 0 }), 'sin_rostro'],
    ['dos rostros', cuadroCon({ rostros: 2 }), 'varios_rostros'],
    ['poca luz', cuadroCon({ brillo: 20 }), 'falta_luz'],
    ['rostro chico', cuadroCon({ escala: 0.4 }), 'acercate'],
    ['rostro grande', cuadroCon({ escala: 2.4 }), 'alejate'],
    ['rostro corrido', cuadroCon({ desplazar: 180 }), 'centra'],
  ])('%s → %s', (_, cuadro, guia) => {
    expect(evaluarCuadro(cuadro, 'frente').guia).toBe(guia);
  });

  it('de frente: listo con |g| bajo y pide mirar de frente si no', () => {
    expect(evaluarCuadro(cuadroCon({ g: 0.05 }), 'frente').guia).toBe('listo');
    expect(evaluarCuadro(cuadroCon({ g: 0.4 }), 'frente').guia).toBe('mira_al_frente');
  });

  it('en un giro pide girar más o menos, según g y el lado pedido', () => {
    expect(evaluarCuadro(cuadroCon({ g: 0.1 }), 'izquierda').guia).toBe('gira_mas');
    expect(evaluarCuadro(cuadroCon({ g: -0.5 }), 'izquierda').guia).toBe('gira');
    expect(evaluarCuadro(cuadroCon({ g: 0 }), 'derecha').guia).toBe('gira');
    expect(evaluarCuadro(cuadroCon({ g: 0.5 }), 'izquierda').guia).toBe('listo');
    expect(evaluarCuadro(cuadroCon({ g: 0.9 }), 'izquierda').guia).toBe('gira_menos');
    expect(evaluarCuadro(cuadroCon({ g: -0.5 }), 'derecha').guia).toBe('listo');
    expect(evaluarCuadro(cuadroCon({ g: -0.9 }), 'derecha').guia).toBe('gira_menos');
  });

  it('el progreso del giro se llena según g hasta el mínimo pedido', () => {
    expect(evaluarCuadro(cuadroCon({ g: 0 }), 'izquierda').progreso).toBe(0);
    expect(evaluarCuadro(cuadroCon({ g: 0.15 }), 'izquierda').progreso).toBeCloseTo(0.5, 5);
    expect(evaluarCuadro(cuadroCon({ g: 0.5 }), 'izquierda').progreso).toBe(1);
    expect(evaluarCuadro(cuadroCon({ g: 0.15 }), 'derecha').progreso).toBe(0);
  });

  it('sin brillo medido no inventa poca luz', () => {
    expect(evaluarCuadro({ ...cuadroCon(), brillo: null }, 'frente').guia).toBe('listo');
  });
});

describe('ejecutarFlujo', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // La detección simulada recibe (video, opciones), como detectarCuadro; el flujo solo pasa las opciones.
  const comoDetector = (simulada) => (opciones) => simulada(null, opciones);

  async function correr({ detectar, ...opciones }, ms = 30000) {
    let fallo;
    const promesa = ejecutarFlujo({ senal: new AbortController().signal, detectar: comoDetector(detectar), ...opciones }).catch((error) => { fallo = error; });
    await vi.advanceTimersByTimeAsync(ms);
    return { resultado: await promesa, fallo };
  }

  it.each([
    [['izquierda', 'derecha']],
    [['derecha', 'izquierda']],
  ])('captura frente y luego cada giro en el orden %j', async (orden) => {
    const detectar = personaQueSigue(orden);
    const { resultado, fallo } = await correr({ detectar, pasos: ['frente', ...orden] });

    expect(fallo).toBeUndefined();
    expect(resultado.map((c) => c.paso)).toEqual(['frente', ...orden]);
    const giros = resultado.map((c) => calcularGiro(c.puntos, c.ancho, c.alto));
    expect(Math.abs(giros[0])).toBeLessThan(0.15);
    expect(Math.sign(giros[1])).toBe(orden[0] === 'izquierda' ? 1 : -1);
    expect(Math.sign(giros[2])).toBe(orden[1] === 'izquierda' ? 1 : -1);
    expect(resultado[0].descriptor).toHaveLength(128);
  });

  it('avanza solo tras sostener el giro unos 300 ms, sin esperas artificiales', async () => {
    const detectar = personaQueSigue(['izquierda', 'derecha']);
    const promesa = ejecutarFlujo({ detectar: comoDetector(detectar), pasos: ['frente', 'izquierda', 'derecha'], senal: new AbortController().signal });

    await vi.advanceTimersByTimeAsync(TIEMPOS_FLUJO.sostenerMs - 60);
    expect(detectar.mock.calls.some(([, op]) => op?.conDescriptor)).toBe(false);
    await vi.advanceTimersByTimeAsync(4 * TIEMPOS_FLUJO.sostenerMs);

    await expect(promesa).resolves.toHaveLength(3);
  });

  it('reintenta un giro una vez de forma automática', async () => {
    const estados = [];
    const detectar = personaQueSigue(['izquierda', 'derecha'], { demora: TIEMPOS_FLUJO.giroMs + 500 });
    const { resultado, fallo } = await correr({ detectar, pasos: ['frente', 'izquierda', 'derecha'], alEstado: (e) => estados.push(e) });

    expect(fallo).toBeUndefined();
    expect(resultado).toHaveLength(3);
    expect(estados.some((e) => e.paso === 'izquierda' && e.intento === 2)).toBe(true);
  });

  it('si el giro no llega tras el reintento, termina con tiempo agotado', async () => {
    const detectar = vi.fn(async (_v, op) => cuadroCon({ g: 0, conDescriptor: op?.conDescriptor }));
    const { fallo } = await correr({ detectar, pasos: ['frente', 'izquierda', 'derecha'] });

    expect(fallo.codigo).toBe('tiempo_agotado');
  });

  it.each([
    [{ rostros: 2 }, 'varios_rostros'],
    [{ rostros: 0 }, 'sin_rostro'],
    [{ brillo: 15 }, 'poca_luz'],
  ])('termina con el problema que impidió la captura (%j → %s)', async (cuadro, codigo) => {
    const detectar = vi.fn(async () => cuadroCon(cuadro));
    const { fallo } = await correr({ detectar, pasos: ['frente', 'izquierda', 'derecha'] });

    expect(fallo.codigo).toBe(codigo);
  });

  it('si cumplía al acabarse el tiempo, no culpa a un problema anterior', async () => {
    let n = 0;
    // Primero sin rostro, después de frente pero sin descriptor válido: el fallo es tiempo agotado.
    const detectar = vi.fn(async () => (n++ < 5 ? cuadroCon({ rostros: 0 }) : cuadroCon({ g: 0 })));
    const { fallo } = await correr({ detectar, pasos: ['frente'] });

    expect(fallo.codigo).toBe('tiempo_agotado');
  });

  it('no acepta una captura final sin descriptor válido', async () => {
    const detectar = vi.fn(async () => cuadroCon({ g: 0 }));
    const { fallo } = await correr({ detectar, pasos: ['frente'] });

    expect(fallo.codigo).toBe('tiempo_agotado');
  });

  it('se detiene al cancelar', async () => {
    const control = new AbortController();
    const detectar = vi.fn(async () => cuadroCon({ g: 0 }));
    const promesa = ejecutarFlujo({ detectar: comoDetector(detectar), pasos: ['frente', 'izquierda'], senal: control.signal }).catch((e) => e);
    await vi.advanceTimersByTimeAsync(100);
    control.abort();
    await vi.advanceTimersByTimeAsync(100);
    const llamadas = detectar.mock.calls.length;
    await vi.advanceTimersByTimeAsync(1000);

    expect((await promesa).codigo).toBe('cancelado');
    expect(detectar.mock.calls.length).toBe(llamadas);
  });

  it('informa cada captura con su posición', async () => {
    const alCaptura = vi.fn();
    await correr({ detectar: personaQueSigue(), pasos: ['frente', 'izquierda', 'derecha'], alCaptura });

    expect(alCaptura.mock.calls.map(([c, i]) => [c.paso, i])).toEqual([['frente', 0], ['izquierda', 1], ['derecha', 2]]);
  });
});
