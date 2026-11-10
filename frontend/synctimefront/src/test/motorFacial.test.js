import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const libreria = vi.hoisted(() => ({
  detectar: null,
  cargarModelo: null,
  elegirMotor: null,
}));

// La librería real se prueba en el navegador; aquí se comprueba qué se hace con sus resultados.
vi.mock('@vladmandic/face-api', () => {
  const red = { loadFromUri: (...args) => libreria.cargarModelo(...args) };
  return {
    tf: { setBackend: (...args) => libreria.elegirMotor(...args), ready: vi.fn().mockResolvedValue() },
    nets: { tinyFaceDetector: red, faceLandmark68Net: red, faceRecognitionNet: red },
    TinyFaceDetectorOptions: class {},
    detectAllFaces: () => ({
      withFaceLandmarks: () => ({ withFaceDescriptors: () => libreria.detectar() }),
    }),
  };
});

const rostro = (valor) => ({ descriptor: new Float32Array([valor, valor]) });

async function cargarModulo() {
  vi.resetModules();
  return import('../rostro/motorFacial');
}

async function capturar(motor, cantidad = 1, senal = new AbortController().signal) {
  let fallo;
  const resultado = motor
    .capturarDescriptores({}, { cantidad, senal })
    .catch((error) => { fallo = error; });
  await vi.advanceTimersByTimeAsync(25000);
  await resultado;
  return { fallo, resultado: await resultado };
}

describe('motorFacial', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    libreria.cargarModelo = vi.fn().mockResolvedValue();
    libreria.detectar = vi.fn();
    libreria.elegirMotor = vi.fn().mockResolvedValue(true);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('carga los modelos desde el propio sitio', async () => {
    libreria.detectar.mockResolvedValue([rostro(0.1)]);
    const motor = await cargarModulo();

    await capturar(motor);

    expect(libreria.cargarModelo).toHaveBeenCalledTimes(3);
    expect(libreria.cargarModelo).toHaveBeenCalledWith('/models');
  });

  it('usa la CPU si WebGL no está disponible', async () => {
    libreria.elegirMotor.mockResolvedValueOnce(false);
    libreria.detectar.mockResolvedValue([rostro(0.1)]);
    const motor = await cargarModulo();

    const { fallo } = await capturar(motor);

    expect(fallo).toBeUndefined();
    expect(libreria.elegirMotor.mock.calls.map(([nombre]) => nombre)).toEqual(['webgl', 'cpu']);
  });

  it('un equipo lento puede demorar más que el plazo en juntar las capturas', async () => {
    let llamadas = 0;
    libreria.detectar.mockImplementation(async () => {
      llamadas += 1;
      await new Promise((resolver) => setTimeout(resolver, 15000));
      return [rostro(llamadas / 10)];
    });
    const motor = await cargarModulo();
    const captura = motor.capturarDescriptores({}, { cantidad: 3, senal: new AbortController().signal });

    await vi.advanceTimersByTimeAsync(50000);

    await expect(captura).resolves.toHaveLength(3);
  });

  it('junta las capturas pedidas con un solo rostro', async () => {
    libreria.detectar.mockResolvedValue([rostro(0.5)]);
    const motor = await cargarModulo();
    const captura = motor.capturarDescriptores({}, { cantidad: 3, senal: new AbortController().signal });

    await vi.advanceTimersByTimeAsync(1000);

    const descriptores = await captura;
    expect(descriptores).toHaveLength(3);
    expect(descriptores[0]).toEqual([0.5, 0.5]);
    expect(Array.isArray(descriptores[0])).toBe(true);
  });

  it('avisa que no hay rostro al acabarse el tiempo', async () => {
    libreria.detectar.mockResolvedValue([]);
    const motor = await cargarModulo();

    const { fallo } = await capturar(motor);

    expect(fallo.codigo).toBe('sin_rostro');
  });

  it('no acepta una imagen con varios rostros', async () => {
    libreria.detectar.mockResolvedValue([rostro(0.1), rostro(0.2)]);
    const motor = await cargarModulo();

    const { fallo } = await capturar(motor);

    expect(fallo.codigo).toBe('varios_rostros');
  });

  it('se detiene al cancelar', async () => {
    libreria.detectar.mockResolvedValue([]);
    const motor = await cargarModulo();
    const control = new AbortController();
    control.abort();

    const { fallo } = await capturar(motor, 1, control.signal);

    expect(fallo.codigo).toBe('cancelado');
    expect(libreria.detectar).not.toHaveBeenCalled();
  });

  it('informa un error de red al cargar los modelos y permite reintentar', async () => {
    libreria.cargarModelo.mockRejectedValueOnce(new TypeError('Failed to fetch'));
    libreria.detectar.mockResolvedValue([rostro(0.1)]);
    const motor = await cargarModulo();

    const { fallo } = await capturar(motor);
    expect(fallo.codigo).toBe('carga');

    const reintento = await capturar(motor);
    expect(reintento.fallo).toBeUndefined();
    expect(reintento.resultado).toHaveLength(1);
  });

  it('tiene un mensaje con alternativa para cada problema', async () => {
    const motor = await cargarModulo();

    for (const codigo of ['permiso_denegado', 'sin_camara', 'camara_ocupada', 'no_soportado', 'sin_rostro', 'carga']) {
      expect(motor.mensajeDeError({ codigo })).toMatch(/contraseña/);
    }
    expect(motor.mensajeDeError({ codigo: 'varios_rostros' })).toMatch(/más de un rostro/);
    expect(motor.mensajeDeError(new Error('otro'))).toMatch(/contraseña/);
  });
});
