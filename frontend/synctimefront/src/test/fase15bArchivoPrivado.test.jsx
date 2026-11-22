import { act, renderHook, waitFor } from '@testing-library/react';
import { StrictMode } from 'react';
import { useArchivoPrivado } from '../componentes/ui/reglasAdjuntos';

// Fase 15B (RNFP23): la descarga de un archivo privado (miniaturas y vista grande de los adjuntos) se cancela al cambiar de archivo o
// desmontar. Con React StrictMode en desarrollo el montaje doble dejaba DOS descargas completas de la misma miniatura; ahora la
// primera se cancela y queda una sola petición real.
const blob = () => new Blob(['x'], { type: 'image/webp' });

describe('useArchivoPrivado', () => {
  beforeEach(() => {
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:prueba'), revokeObjectURL: vi.fn() }));
  });
  afterEach(() => vi.unstubAllGlobals());

  it('le entrega una señal a la descarga y la cancela al desmontar', async () => {
    const cargar = vi.fn((senal) => new Promise((resolver) => senal.addEventListener('abort', () => resolver(blob()))));
    const { unmount } = renderHook(() => useArchivoPrivado(cargar, 8));
    await waitFor(() => expect(cargar).toHaveBeenCalledTimes(1));
    const senal = cargar.mock.calls[0][0];
    expect(senal).toBeInstanceOf(AbortSignal);
    expect(senal.aborted).toBe(false);
    unmount();
    expect(senal.aborted).toBe(true);
  });

  it('con StrictMode la primera descarga queda cancelada y solo la segunda sigue', async () => {
    const cargar = vi.fn(() => Promise.resolve(blob()));
    const { result } = renderHook(() => useArchivoPrivado(cargar, 8), { wrapper: StrictMode });
    await waitFor(() => expect(result.current.url).toBe('blob:prueba'));
    const [primera, segunda] = cargar.mock.calls.map(([senal]) => senal);
    expect(primera.aborted).toBe(true);
    expect(segunda.aborted).toBe(false);
  });

  it('una descarga cancelada no deja un error en pantalla', async () => {
    const cargar = vi.fn((senal) => new Promise((_, rechazar) => senal.addEventListener('abort', () => rechazar(new DOMException('Cancelada', 'AbortError')))));
    const { result, rerender } = renderHook(({ clave }) => useArchivoPrivado(cargar, clave), { initialProps: { clave: 8 } });
    await waitFor(() => expect(cargar).toHaveBeenCalledTimes(1));
    rerender({ clave: 9 });
    await act(async () => { await Promise.resolve(); });
    expect(result.current.error).toBeNull();
  });

  it('al cambiar de archivo cancela el anterior', async () => {
    const cargar = vi.fn(() => new Promise(() => {}));
    const { rerender } = renderHook(({ clave }) => useArchivoPrivado(cargar, clave), { initialProps: { clave: 8 } });
    await waitFor(() => expect(cargar).toHaveBeenCalledTimes(1));
    rerender({ clave: 9 });
    await waitFor(() => expect(cargar).toHaveBeenCalledTimes(2));
    expect(cargar.mock.calls[0][0].aborted).toBe(true);
    expect(cargar.mock.calls[1][0].aborted).toBe(false);
  });
});
