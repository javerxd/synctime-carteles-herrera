import { beforeEach, describe, expect, it, vi } from 'vitest';
import authService from '../api/authService';
import { rostros } from '../api/rostros';
import { api } from '../api/cliente';
import { guardarTokenEquipo } from '../rostro/tokenEquipo';

vi.mock('../api/cliente', () => ({
  api: {
    get: vi.fn().mockResolvedValue({}),
    post: vi.fn().mockResolvedValue({}),
    put: vi.fn().mockResolvedValue({}),
    delete: vi.fn().mockResolvedValue(null),
  },
  leerCookie: vi.fn(() => null),
}));

const captura = (paso) => ({ paso, descriptor: [0.1], puntos: [[1, 2]], ancho: 640, alto: 480 });
const CAPTURAS = [captura('frente'), captura('izquierda'), captura('derecha')];
const SIN_PASO = CAPTURAS.map(({ descriptor, puntos, ancho, alto }) => ({ descriptor, puntos, ancho, alto }));

describe('authService: ingreso con rostro (fase 10)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it('pide el desafío con el email, sin renovar la sesión', async () => {
    const senal = new AbortController().signal;
    await authService.desafioFacial('dueno@letreros.cl', senal);

    expect(api.post).toHaveBeenCalledWith('/auth/login-facial/desafio', { email: 'dueno@letreros.cl' }, { sinRenovar: true, senal });
  });

  it('envía email, desafío, el token del equipo de ese email y las tres capturas, en el orden recibido', async () => {
    guardarTokenEquipo('Dueno@Letreros.cl ', 'token-del-dueno');
    guardarTokenEquipo('otro@letreros.cl', 'token-de-otro');

    await authService.loginFacial('dueno@letreros.cl', { desafio: 'd-1', capturas: CAPTURAS });

    expect(api.post).toHaveBeenCalledWith('/auth/login-facial', {
      email: 'dueno@letreros.cl',
      desafio: 'd-1',
      tokenEquipo: 'token-del-dueno',
      capturas: SIN_PASO,
    }, { sinRenovar: true });
  });

  it('funciona sin token de equipo: el navegador no conoce el rol', async () => {
    await authService.loginFacial('operario@letreros.cl', { desafio: 'd', capturas: CAPTURAS });

    expect(api.post.mock.calls[0][1].tokenEquipo).toBeNull();
  });

  it('registra el propio rostro con las tres muestras por lado', async () => {
    await authService.activarRostro({
      password: 'clave', versionConsentimiento: 'v1',
      muestras: { frente: captura('frente'), izquierda: captura('izquierda'), derecha: captura('derecha') },
    });

    expect(api.put).toHaveBeenCalledWith('/auth/rostro', {
      password: 'clave', consentimiento: true, versionConsentimiento: 'v1',
      muestras: { frente: SIN_PASO[0], izquierda: SIN_PASO[1], derecha: SIN_PASO[2] },
    });
  });
});

describe('API de rostros registrados (S16) y calibración', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('lista, registra a otro titular y desactiva', async () => {
    await rostros.listar();
    expect(api.get).toHaveBeenCalledWith('/rostros', { senal: undefined });

    await rostros.registrar(7, {
      passwordTitular: 'clave-del-titular', versionConsentimiento: 'v1',
      muestras: { frente: captura('frente'), izquierda: captura('izquierda'), derecha: captura('derecha') },
    });
    expect(api.put).toHaveBeenCalledWith('/rostros/7', {
      passwordTitular: 'clave-del-titular', consentimiento: true, versionConsentimiento: 'v1',
      muestras: { frente: SIN_PASO[0], izquierda: SIN_PASO[1], derecha: SIN_PASO[2] },
    });

    await rostros.desactivar(7);
    expect(api.delete).toHaveBeenCalledWith('/rostros/7');
  });

  it('calibración: consulta, mide un intento y borra las mediciones', async () => {
    await rostros.calibracion.obtener();
    expect(api.get).toHaveBeenCalledWith('/rostros/calibracion', { senal: undefined });

    await rostros.calibracion.medir({ idEmpleado: 3, etiqueta: 'propio', desafio: 'd-2', capturas: CAPTURAS });
    expect(api.post).toHaveBeenCalledWith('/rostros/calibracion', {
      idEmpleado: 3, etiqueta: 'propio', desafio: 'd-2', capturas: SIN_PASO,
    });

    await rostros.calibracion.borrar();
    expect(api.delete).toHaveBeenCalledWith('/rostros/calibracion');
  });
});
