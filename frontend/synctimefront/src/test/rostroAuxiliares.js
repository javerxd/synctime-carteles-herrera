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
