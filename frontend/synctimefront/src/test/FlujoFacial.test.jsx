import { StrictMode } from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import FlujoFacial from '../rostro/FlujoFacial';
import { detectarCuadro, mensajeDeError, prepararMotor } from '../rostro/motorFacial';
import { TIEMPOS_FLUJO } from '../rostro/guiaFacial';
import {
  avanzar, crearCamara, cuadroCon, errorDeCamara, personaQueSigue, simularMovimientoReducido, simularNavegador,
} from './rostroAuxiliares';

// Se reemplaza solo la detección (que carga la librería). Abrir y apagar la cámara es el código real.
vi.mock('../rostro/motorFacial', async (original) => ({
  ...(await original()),
  prepararMotor: vi.fn(),
  detectarCuadro: vi.fn(),
}));

const PASOS = ['frente', 'izquierda', 'derecha'];

function renderFlujo(props = {}) {
  const avisos = { alTerminar: vi.fn(), alError: vi.fn(), alCancelar: vi.fn() };
  const vista = render(<FlujoFacial pasos={PASOS} {...avisos} {...props} />);
  return { ...avisos, ...vista };
}

const instruccion = () => screen.getByTestId('instruccion');

describe('FlujoFacial', () => {
  let pista;
  let getUserMedia;

  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
    simularMovimientoReducido(false);
    const simulada = crearCamara();
    pista = simulada.pista;
    getUserMedia = vi.fn().mockResolvedValue(simulada.camara);
    simularNavegador(getUserMedia);
    prepararMotor.mockResolvedValue();
    detectarCuadro.mockImplementation(personaQueSigue(['izquierda', 'derecha']));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('enciende la cámara al montarse, lo dice y la apaga al terminar antes de entregar las capturas', async () => {
    const { alTerminar } = renderFlujo();
    await avanzar(10);
    expect(getUserMedia).toHaveBeenCalledTimes(1);
    expect(screen.getByText('Cámara activa')).toBeInTheDocument();

    alTerminar.mockImplementation(() => {
      expect(pista.stop).toHaveBeenCalled();
    });
    await avanzar(5000);

    expect(alTerminar).toHaveBeenCalledTimes(1);
    const [capturas] = alTerminar.mock.calls[0];
    expect(capturas.map((c) => c.paso)).toEqual(PASOS);
    expect(Object.keys(capturas[0]).sort()).toEqual(['alto', 'ancho', 'descriptor', 'paso', 'puntos']);
    expect(screen.queryByText('Cámara activa')).not.toBeInTheDocument();
  });

  it('muestra el paso de frente y luego cada giro, con la flecha hacia el lado pedido', async () => {
    detectarCuadro.mockImplementation(personaQueSigue(['derecha', 'izquierda'], { demora: 1000 }));
    renderFlujo({ pasos: ['frente', 'derecha', 'izquierda'] });
    await avanzar(10);
    expect(screen.getByText(/paso 1 de 3/i)).toBeInTheDocument();

    await avanzar(TIEMPOS_FLUJO.sostenerMs + 200);
    expect(screen.getByText(/paso 2 de 3/i)).toBeInTheDocument();
    expect(instruccion()).toHaveTextContent(/gira la cabeza hacia tu derecha/i);
    expect(screen.getByTestId('flecha')).toHaveAttribute('data-lado', 'derecha');
    expect(screen.getByRole('progressbar', { name: /giro/i, hidden: true })).toHaveAttribute('aria-valuenow', '0');
  });

  it('las instrucciones están en una región viva cortés', async () => {
    renderFlujo();
    await avanzar(10);
    expect(screen.getByTestId('anuncio')).toHaveAttribute('aria-live', 'polite');
    expect(screen.getByTestId('anuncio')).toContainElement(instruccion());
    expect(screen.getByText('La cámara está encendida.')).toBeInTheDocument();
  });

  it.each([
    [{ escala: 0.4 }, 'Acércate'],
    [{ escala: 2.4 }, 'Aléjate'],
    [{ brillo: 20 }, 'Falta luz'],
    [{ rostros: 2 }, 'Debe verse un solo rostro'],
    [{ desplazar: 180 }, 'Centra tu rostro'],
  ])('guía en vivo con %j: «%s»', async (cuadro, texto) => {
    detectarCuadro.mockImplementation(async () => cuadroCon(cuadro));
    renderFlujo();
    await avanzar(2000);
    expect(instruccion()).toHaveTextContent(texto);
  });

  it('pide girar un poco más o un poco menos', async () => {
    let g = 0;
    detectarCuadro.mockImplementation(async (_v, op) => cuadroCon({ g, conDescriptor: op?.conDescriptor }));
    renderFlujo({ pasos: ['frente', 'izquierda'] });
    await avanzar(TIEMPOS_FLUJO.sostenerMs + 200);
    g = 0.1;
    await avanzar(1800);
    expect(instruccion()).toHaveTextContent('Gira un poco más');
    g = 0.95;
    await avanzar(1800);
    expect(instruccion()).toHaveTextContent('Gira un poco menos');
  });

  it('reintenta el giro una vez y lo anuncia', async () => {
    detectarCuadro.mockImplementation(personaQueSigue(['izquierda', 'derecha'], { demora: TIEMPOS_FLUJO.giroMs + 800 }));
    const { alTerminar, alError } = renderFlujo();
    await avanzar(TIEMPOS_FLUJO.sostenerMs + 200 + TIEMPOS_FLUJO.giroMs + 100);
    expect(instruccion()).toHaveTextContent(/otra vez/i);

    await avanzar(10000);
    expect(alError).not.toHaveBeenCalled();
    expect(alTerminar).toHaveBeenCalled();
  });

  it('tiempo agotado: avisa con su código y apaga la cámara', async () => {
    detectarCuadro.mockImplementation(async (_v, op) => cuadroCon({ g: 0, conDescriptor: op?.conDescriptor }));
    const { alError, alTerminar } = renderFlujo();
    await avanzar(20000);

    expect(alError).toHaveBeenCalledTimes(1);
    expect(alError.mock.calls[0][0].codigo).toBe('tiempo_agotado');
    expect(pista.stop).toHaveBeenCalled();
    expect(alTerminar).not.toHaveBeenCalled();
  });

  it.each([
    [{ rostros: 2 }, 'varios_rostros', /más de un rostro/i],
    [{ rostros: 0 }, 'sin_rostro', /no se detectó ningún rostro/i],
    [{ brillo: 10 }, 'poca_luz', /falta luz/i],
  ])('con %j termina con %s y un mensaje propio', async (cuadro, codigo, mensaje) => {
    detectarCuadro.mockImplementation(async () => cuadroCon(cuadro));
    const { alError } = renderFlujo();
    await avanzar(20000);

    expect(alError.mock.calls[0][0].codigo).toBe(codigo);
    expect(mensajeDeError(alError.mock.calls[0][0])).toMatch(mensaje);
    expect(pista.stop).toHaveBeenCalled();
  });

  it('«Cancelar» está disponible y apaga la cámara de inmediato', async () => {
    const { alCancelar } = renderFlujo();
    await avanzar(100);

    fireEvent.click(screen.getByRole('button', { name: 'Cancelar' }));

    expect(pista.stop).toHaveBeenCalled();
    expect(alCancelar).toHaveBeenCalled();
  });

  it('desmontarlo a mitad del flujo apaga la cámara y no avisa nada', async () => {
    const { alTerminar, alError, unmount } = renderFlujo();
    await avanzar(100);
    unmount();
    expect(pista.stop).toHaveBeenCalled();
    await avanzar(10000);
    expect(alTerminar).not.toHaveBeenCalled();
    expect(alError).not.toHaveBeenCalled();
  });

  it('con StrictMode solo queda encendida la cámara del último montaje', async () => {
    const camaras = [crearCamara(), crearCamara()];
    let pedidas = 0;
    simularNavegador(vi.fn(async () => camaras[pedidas++].camara));
    detectarCuadro.mockImplementation(async () => cuadroCon({ rostros: 0 }));
    const avisos = { alTerminar: vi.fn(), alError: vi.fn(), alCancelar: vi.fn() };
    const { unmount } = render(<StrictMode><FlujoFacial pasos={PASOS} {...avisos} /></StrictMode>);
    await avanzar(50);

    expect(pedidas).toBe(2);
    expect(camaras[0].pista.stop).toHaveBeenCalled();
    expect(camaras[1].pista.stop).not.toHaveBeenCalled();
    unmount();
    expect(camaras[1].pista.stop).toHaveBeenCalled();
  });

  it.each([
    ['NotAllowedError', 'permiso_denegado', /no tiene permiso/i],
    ['NotFoundError', 'sin_camara', /no encontramos una cámara/i],
  ])('cámara: %s → %s', async (nombre, codigo, mensaje) => {
    getUserMedia.mockRejectedValue(errorDeCamara(nombre));
    const { alError } = renderFlujo();
    await avanzar(10);

    expect(alError.mock.calls[0][0].codigo).toBe(codigo);
    expect(mensajeDeError(alError.mock.calls[0][0])).toMatch(mensaje);
    expect(detectarCuadro).not.toHaveBeenCalled();
  });

  it('sin cámara en el navegador avisa que no está soportado', async () => {
    simularNavegador(undefined);
    const { alError } = renderFlujo();
    await avanzar(10);
    expect(alError.mock.calls[0][0].codigo).toBe('no_soportado');
  });

  it('pide los pasos (el desafío) con la cámara lista y avisa un error de red', async () => {
    const fallo = { estado: 0, codigo: 'sin_conexion', message: 'sin red' };
    const prepararPasos = vi.fn().mockRejectedValue(fallo);
    const { alError } = renderFlujo({ pasos: undefined, prepararPasos });
    await avanzar(10);

    expect(prepararPasos).toHaveBeenCalledTimes(1);
    expect(alError).toHaveBeenCalledWith(fallo);
    expect(mensajeDeError(fallo)).toMatch(/no pudimos conectar/i);
    expect(pista.stop).toHaveBeenCalled();
  });

  it('usa los pasos que entrega prepararPasos', async () => {
    detectarCuadro.mockImplementation(personaQueSigue(['derecha', 'izquierda']));
    const prepararPasos = vi.fn().mockResolvedValue(['frente', 'derecha', 'izquierda']);
    const { alTerminar } = renderFlujo({ pasos: undefined, prepararPasos });
    await avanzar(5000);

    expect(alTerminar.mock.calls[0][0].map((c) => c.paso)).toEqual(['frente', 'derecha', 'izquierda']);
  });

  it('un fallo al cargar los modelos avisa con su código', async () => {
    prepararMotor.mockRejectedValue(Object.assign(new Error('x'), { name: 'ErrorFacial', codigo: 'carga' }));
    const { alError } = renderFlujo();
    await avanzar(10);
    expect(alError.mock.calls[0][0].codigo).toBe('carga');
    expect(pista.stop).toHaveBeenCalled();
  });

  it('mientras se verifica, la cámara ya está apagada y lo dice', async () => {
    const { alTerminar, alCancelar } = renderFlujo();
    alTerminar.mockReturnValue(new Promise(() => {}));
    await avanzar(5000);

    expect(screen.getByText(/verificando/i)).toBeInTheDocument();
    expect(screen.queryByText('Cámara activa')).not.toBeInTheDocument();
    expect(pista.stop).toHaveBeenCalled();
    // Con las capturas enviadas, «Cancelar» queda inactivo: no podría detener la verificación.
    const cancelar = screen.getByRole('button', { name: 'Cancelar' });
    expect(cancelar).toHaveAttribute('aria-disabled', 'true');
    fireEvent.click(cancelar);
    expect(alCancelar).not.toHaveBeenCalled();
  });

  it('con movimiento reducido no anima y muestra el avance con texto', async () => {
    simularMovimientoReducido(true);
    let g = 0;
    detectarCuadro.mockImplementation(async (_v, op) => cuadroCon({ g, conDescriptor: op?.conDescriptor }));
    const { container } = renderFlujo({ pasos: ['frente', 'izquierda'] });
    await avanzar(TIEMPOS_FLUJO.sostenerMs + 200);
    g = 0.15;
    await avanzar(1000);

    expect(container.querySelector('[data-movimiento]')).toHaveAttribute('data-movimiento', 'reducido');
    expect(screen.getByTestId('flecha')).toHaveAttribute('data-animada', 'false');
    expect(screen.getByText('50 %')).toBeInTheDocument();
  });

  it('con movimiento normal la flecha se anima', async () => {
    renderFlujo({ pasos: ['frente', 'izquierda'] });
    detectarCuadro.mockImplementation(async (_v, op) => cuadroCon({ g: op?.conDescriptor ? 0 : 0, conDescriptor: op?.conDescriptor }));
    await avanzar(TIEMPOS_FLUJO.sostenerMs + 300);
    expect(screen.getByTestId('flecha')).toHaveAttribute('data-animada', 'true');
  });

  it('entrega una vista previa local de cada muestra si se pide', async () => {
    const { alTerminar } = renderFlujo({ conVistaPrevia: true });
    await avanzar(5000);

    const [, vistas] = alTerminar.mock.calls[0];
    expect(Object.keys(vistas).sort()).toEqual(['derecha', 'frente', 'izquierda']);
    expect(vistas.frente).toBeInstanceOf(HTMLCanvasElement);
  });
});
