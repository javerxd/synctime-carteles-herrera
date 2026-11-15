import { act } from '@testing-library/react';
import { vi } from 'vitest';

/** Cámara simulada: permite comprobar que las pistas se apagan. */
export function crearCamara() {
  const pista = { stop: vi.fn() };
  return { pista, camara: { getTracks: () => [pista] } };
}

/** Reemplaza getUserMedia y la reproducción de video, que jsdom no implementa. */
export function simularNavegador(getUserMedia) {
  Object.defineProperty(navigator, 'mediaDevices', {
    configurable: true,
    value: getUserMedia ? { getUserMedia } : undefined,
  });
  window.HTMLMediaElement.prototype.play = vi.fn().mockResolvedValue();
}

export function errorDeCamara(name) {
  return new DOMException('simulado', name);
}

/** Simula prefers-reduced-motion (jsdom no trae matchMedia). */
export function simularMovimientoReducido(reducido) {
  window.matchMedia = vi.fn((consulta) => ({
    matches: reducido && consulta.includes('prefers-reduced-motion'),
    media: consulta,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
  }));
}

export const ANCHO = 640;
export const ALTO = 480;
const DESCRIPTOR = Array.from({ length: 128 }, (_, i) => i / 1000);

/**
 * Un rostro detectado en un cuadro de 640 × 480 sin espejo, con el índice de giro `g` pedido.
 * `escala` cambia su tamaño (1 = 200 px de ancho) y `desplazar` lo saca del centro.
 */
export function rostroCon({ g = 0, escala = 1, desplazar = 0, conDescriptor = false } = {}) {
  const ancho = 200 * escala;
  const izquierda = 320 - ancho / 2 + desplazar;
  const puntos = Array.from({ length: 68 }, () => [izquierda + ancho / 2, 240]);
  puntos[0] = [izquierda, 230];
  puntos[16] = [izquierda + ancho, 230];
  puntos[30] = [izquierda + ancho * (0.5 + g / 2), 250];
  return {
    caja: { x: izquierda, y: 130, ancho, alto: ancho * 1.1 },
    puntos,
    descriptor: conDescriptor ? DESCRIPTOR : null,
  };
}

/** Un cuadro completo como el que entrega detectarCuadro. */
export function cuadroCon({ rostros = 1, brillo = 120, conDescriptor = false, ...rostro } = {}) {
  return {
    ancho: ANCHO,
    alto: ALTO,
    brillo,
    rostros: Array.from({ length: rostros }, (_, i) => rostroCon({ ...rostro, desplazar: (rostro.desplazar ?? 0) + i * 5, conDescriptor })),
  };
}

const GIRO_DE = { frente: 0, izquierda: 0.5, derecha: -0.5 };

/**
 * Una persona que sigue las instrucciones: mira de frente y luego gira hacia cada lado en el orden dado.
 * Cada captura (la detección con descriptor) la hace pasar al paso siguiente. `demora` (ms) la hace
 * tardar en empezar cada giro, para probar el reintento y el tiempo agotado.
 */
export function personaQueSigue(orden = ['izquierda', 'derecha'], { demora = 0, ahora = () => Date.now() } = {}) {
  const pasos = ['frente', ...orden];
  let indice = 0;
  let inicioPaso = null;
  return vi.fn(async (_video, { conDescriptor = false } = {}) => {
    const paso = pasos[Math.min(indice, pasos.length - 1)];
    inicioPaso ??= ahora();
    const yaGiro = paso === 'frente' || ahora() - inicioPaso >= demora;
    const cuadro = cuadroCon({ g: yaGiro ? GIRO_DE[paso] : 0, conDescriptor });
    if (conDescriptor) {
      indice += 1;
      inicioPaso = null;
    }
    return cuadro;
  });
}

/** Avanza el reloj simulado dentro de act(), dejando correr las promesas pendientes. */
export async function avanzar(ms) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}
