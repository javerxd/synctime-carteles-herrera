import { StrictMode } from 'react';
import { act, render, screen, waitFor, fireEvent } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import CapturaFacial from '../components/CapturaFacial';
import { capturarDescriptores } from '../rostro/motorFacial';
import { crearCamara, errorDeCamara, simularNavegador } from './rostroAuxiliares';

// Se reemplaza solo la captura, que carga la librería. Abrir y apagar la cámara es el código real.
vi.mock('../rostro/motorFacial', async (original) => ({
  ...(await original()),
  capturarDescriptores: vi.fn(),
}));

function renderCaptura(props = {}) {
  const avisos = { onCaptura: vi.fn(), onError: vi.fn(), onCancelar: vi.fn() };
  const vista = render(<CapturaFacial {...avisos} {...props} />);
  return { ...avisos, ...vista };
}

describe('CapturaFacial', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('devuelve los descriptores y apaga la cámara al terminar', async () => {
    const { pista, camara } = crearCamara();
    simularNavegador(vi.fn().mockResolvedValue(camara));
    capturarDescriptores.mockResolvedValue([[0.1, 0.2]]);

    const { onCaptura, onError } = renderCaptura();

    await waitFor(() => expect(onCaptura).toHaveBeenCalledWith([[0.1, 0.2]]));
    expect(pista.stop).toHaveBeenCalled();
    expect(onError).not.toHaveBeenCalled();
  });

  it('apaga la cámara si la captura falla', async () => {
    const { pista, camara } = crearCamara();
    simularNavegador(vi.fn().mockResolvedValue(camara));
    const fallo = Object.assign(new Error('x'), { codigo: 'sin_rostro' });
    capturarDescriptores.mockRejectedValue(fallo);

    const { onError } = renderCaptura();

    await waitFor(() => expect(onError).toHaveBeenCalledWith(fallo));
    expect(pista.stop).toHaveBeenCalled();
  });

  it('apaga la cámara al cerrarse a mitad de la captura y no avisa', async () => {
    const { pista, camara } = crearCamara();
    simularNavegador(vi.fn().mockResolvedValue(camara));
    let terminar;
    capturarDescriptores.mockReturnValue(new Promise((resolver) => { terminar = resolver; }));

    const { onCaptura, unmount } = renderCaptura();
    await waitFor(() => expect(capturarDescriptores).toHaveBeenCalled());
    expect(pista.stop).not.toHaveBeenCalled();

    unmount();
    expect(pista.stop).toHaveBeenCalled();

    await act(async () => terminar([[0.1]]));
    expect(onCaptura).not.toHaveBeenCalled();
  });

  it('apaga la cámara si el permiso llega después de cerrar', async () => {
    const { pista, camara } = crearCamara();
    let conceder;
    simularNavegador(vi.fn().mockReturnValue(new Promise((resolver) => { conceder = resolver; })));

    const { unmount, onError } = renderCaptura();
    unmount();
    await act(async () => conceder(camara));

    expect(pista.stop).toHaveBeenCalled();
    expect(capturarDescriptores).not.toHaveBeenCalled();
    expect(onError).not.toHaveBeenCalled();
  });

  it('no carga la librería si se cancela mientras el video arranca', async () => {
    const { camara } = crearCamara();
    simularNavegador(vi.fn().mockResolvedValue(camara));
    let arrancar;
    window.HTMLMediaElement.prototype.play = vi.fn(() => new Promise((resolver) => { arrancar = resolver; }));

    const { unmount } = renderCaptura();
    await waitFor(() => expect(window.HTMLMediaElement.prototype.play).toHaveBeenCalled());
    unmount();
    await act(async () => arrancar());

    expect(capturarDescriptores).not.toHaveBeenCalled();
  });

  it('con StrictMode, como en la app, solo queda encendida la cámara del último montaje', async () => {
    const camaras = [crearCamara(), crearCamara()];
    let pedidas = 0;
    simularNavegador(vi.fn(async () => camaras[pedidas++].camara));
    capturarDescriptores.mockReturnValue(new Promise(() => {}));
    const avisos = { onCaptura: vi.fn(), onError: vi.fn(), onCancelar: vi.fn() };

    const { unmount } = render(<StrictMode><CapturaFacial {...avisos} /></StrictMode>);

    await waitFor(() => expect(pedidas).toBe(2));
    await waitFor(() => expect(camaras[0].pista.stop).toHaveBeenCalled());
    expect(camaras[1].pista.stop).not.toHaveBeenCalled();
    expect(avisos.onError).not.toHaveBeenCalled();

    unmount();
    expect(camaras[1].pista.stop).toHaveBeenCalled();
  });

  it.each([
    ['NotAllowedError', 'permiso_denegado'],
    ['SecurityError', 'permiso_denegado'],
    ['NotFoundError', 'sin_camara'],
    ['OverconstrainedError', 'sin_camara'],
    ['NotReadableError', 'camara_ocupada'],
  ])('traduce %s a %s', async (nombre, codigo) => {
    simularNavegador(vi.fn().mockRejectedValue(errorDeCamara(nombre)));

    const { onError } = renderCaptura();

    await waitFor(() => expect(onError).toHaveBeenCalled());
    expect(onError.mock.calls[0][0].codigo).toBe(codigo);
    expect(capturarDescriptores).not.toHaveBeenCalled();
  });

  it('avisa si el navegador no permite usar la cámara', async () => {
    simularNavegador(undefined);

    const { onError } = renderCaptura();

    await waitFor(() => expect(onError).toHaveBeenCalled());
    expect(onError.mock.calls[0][0].codigo).toBe('no_soportado');
  });

  it('permite cancelar', async () => {
    const { camara } = crearCamara();
    simularNavegador(vi.fn().mockResolvedValue(camara));
    capturarDescriptores.mockReturnValue(new Promise(() => {}));

    const { onCancelar } = renderCaptura();
    fireEvent.click(screen.getByRole('button', { name: /cancelar/i }));

    expect(onCancelar).toHaveBeenCalled();
  });
});
