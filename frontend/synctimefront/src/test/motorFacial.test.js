import { beforeEach, describe, expect, it, vi } from 'vitest';

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
      withFaceLandmarks: () => ({
        then: (resolver, rechazar) => libreria.detectar(false).then(resolver, rechazar),
        withFaceDescriptors: () => libreria.detectar(true),
      }),
    }),
  };
});

const resultado = (x, conDescriptor) => ({
  detection: { box: { x, y: 20, width: 100, height: 120 } },
  landmarks: { positions: Array.from({ length: 68 }, (_, i) => ({ x: x + i, y: 50 })) },
  ...(conDescriptor ? { descriptor: new Float32Array(128).fill(0.25) } : {}),
});

const VIDEO = { videoWidth: 640, videoHeight: 480 };

async function cargarModulo() {
  vi.resetModules();
  return import('../rostro/motorFacial');
}

describe('motorFacial', () => {
  beforeEach(() => {
    libreria.cargarModelo = vi.fn().mockResolvedValue();
    libreria.detectar = vi.fn(async (conDescriptor) => [resultado(10, conDescriptor)]);
    libreria.elegirMotor = vi.fn().mockResolvedValue(true);
  });

  it('carga los tres modelos desde el propio sitio, una sola vez', async () => {
    const motor = await cargarModulo();

    await motor.prepararMotor();
    await motor.detectarCuadro(VIDEO);

    expect(libreria.cargarModelo).toHaveBeenCalledTimes(3);
    expect(libreria.cargarModelo).toHaveBeenCalledWith('/models');
  });

  it('usa la CPU si WebGL no está disponible', async () => {
    libreria.elegirMotor.mockResolvedValueOnce(false);
    const motor = await cargarModulo();

    await motor.prepararMotor();

    expect(libreria.elegirMotor.mock.calls.map(([nombre]) => nombre)).toEqual(['webgl', 'cpu']);
  });

  it('entrega cada rostro del cuadro con su caja y sus 68 puntos, sin descriptor si no se pide', async () => {
    libreria.detectar.mockImplementation(async (c) => [resultado(10, c), resultado(300, c)]);
    const motor = await cargarModulo();

    const cuadro = await motor.detectarCuadro(VIDEO);

    expect(libreria.detectar).toHaveBeenCalledWith(false);
    expect(cuadro.ancho).toBe(640);
    expect(cuadro.alto).toBe(480);
    expect(cuadro.rostros).toHaveLength(2);
    expect(cuadro.rostros[0].caja).toEqual({ x: 10, y: 20, ancho: 100, alto: 120 });
    expect(cuadro.rostros[0].puntos).toHaveLength(68);
    expect(cuadro.rostros[0].puntos[3]).toEqual([13, 50]);
    expect(cuadro.rostros[0].descriptor).toBeNull();
  });

  it('con descriptor lo entrega como arreglo común de 128 números', async () => {
    const motor = await cargarModulo();

    const cuadro = await motor.detectarCuadro(VIDEO, { conDescriptor: true });

    expect(libreria.detectar).toHaveBeenCalledWith(true);
    expect(Array.isArray(cuadro.rostros[0].descriptor)).toBe(true);
    expect(cuadro.rostros[0].descriptor).toHaveLength(128);
  });

  it('mide el brillo medio del cuadro (0 a 255) cuando el navegador permite leer la imagen', async () => {
    const datos = new Uint8ClampedArray(4 * 4).fill(0);
    for (let i = 0; i < datos.length; i += 4) { datos[i] = 100; datos[i + 1] = 100; datos[i + 2] = 100; datos[i + 3] = 255; }
    const contexto = { drawImage: vi.fn(), getImageData: vi.fn(() => ({ data: datos })) };
    const original = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = vi.fn(() => contexto);
    try {
      const motor = await cargarModulo();
      expect(motor.medirBrillo(VIDEO)).toBeCloseTo(100, 5);
      expect(contexto.drawImage).toHaveBeenCalled();
    } finally {
      HTMLCanvasElement.prototype.getContext = original;
    }
  });

  it('sin poder leer la imagen el brillo queda sin medir', async () => {
    const motor = await cargarModulo();
    expect(motor.medirBrillo(VIDEO)).toBeNull();
  });

  it('informa un error de red al cargar los modelos y permite reintentar', async () => {
    libreria.cargarModelo.mockRejectedValueOnce(new TypeError('Failed to fetch'));
    const motor = await cargarModulo();

    await expect(motor.prepararMotor()).rejects.toMatchObject({ codigo: 'carga' });
    await expect(motor.prepararMotor()).resolves.toBeUndefined();
  });

  it('tiene un mensaje propio, con alternativa, para cada problema', async () => {
    const motor = await cargarModulo();

    for (const codigo of ['permiso_denegado', 'sin_camara', 'camara_ocupada', 'no_soportado', 'sin_rostro', 'carga', 'poca_luz', 'tiempo_agotado']) {
      expect(motor.mensajeDeError({ codigo })).toMatch(/contraseña/);
    }
    expect(motor.mensajeDeError({ codigo: 'varios_rostros' })).toMatch(/más de un rostro/);
    expect(motor.mensajeDeError({ codigo: 'poca_luz' })).toMatch(/falta luz/i);
    expect(motor.mensajeDeError({ codigo: 'tiempo_agotado' })).toMatch(/tiempo/i);
    expect(motor.mensajeDeError({ estado: 0, codigo: 'sin_conexion' })).toMatch(/no pudimos conectar/i);
    expect(motor.mensajeDeError({ estado: 429 })).toMatch(/demasiados intentos/i);
    expect(motor.mensajeDeError(new Error('otro'))).toMatch(/contraseña/);
  });

  it('ya no expone la captura antigua de un descriptor promedio', async () => {
    const motor = await cargarModulo();
    expect(motor.capturarDescriptores).toBeUndefined();
  });
});
