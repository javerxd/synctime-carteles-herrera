import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import IngresoPersonal from '../paginas/publicas/IngresoPersonal';
import { ProveedorAvisos } from '../avisos/Avisos';
import { almacenSesion } from '../sesion/sesion';
import authService from '../api/authService';
import { detectarCuadro, prepararMotor } from '../rostro/motorFacial';
import {
  avanzar, crearCamara, errorDeCamara, personaQueSigue, simularMovimientoReducido, simularNavegador,
} from './rostroAuxiliares';

vi.mock('../api/authService', () => ({
  default: { login: vi.fn(), loginFacial: vi.fn(), desafioFacial: vi.fn() },
}));

vi.mock('../rostro/motorFacial', async (original) => ({
  ...(await original()),
  prepararMotor: vi.fn(),
  detectarCuadro: vi.fn(),
}));

const EMAIL = 'admin@letrerosherrera.cl';
const RECHAZO = 'No pudimos confirmar que eres tú. Inténtalo de nuevo o entra con tu contraseña.';
const EXITO = { token: 't', tipo: 'empleado', usuario: { nombre: 'Ana', rol: 'administrador' }, nombre: 'Ana Díaz', rol: 'administrador' };

function renderLogin() {
  return render(
    <ProveedorAvisos>
      <MemoryRouter initialEntries={['/personal/ingresar']}>
        <Routes>
          <Route path="/personal/ingresar" element={<IngresoPersonal />} />
          <Route path="/panel" element={<h1>Panel</h1>} />
        </Routes>
      </MemoryRouter>
    </ProveedorAvisos>
  );
}

const escribirEmail = (valor = EMAIL) =>
  fireEvent.change(screen.getByLabelText(/correo electrónico/i), { target: { value: valor } });
const pulsarRostro = () => fireEvent.click(screen.getByRole('button', { name: /entrar con rostro/i }));
const campoPassword = () => screen.getByLabelText(/^contraseña/i);

/** Pulsa «Entrar con rostro» y deja correr el flujo guiado completo (unos segundos simulados). */
async function intentarConRostro() {
  // Una persona nueva en cada intento: vuelve a empezar de frente.
  detectarCuadro.mockImplementation(personaQueSigue(['derecha', 'izquierda']));
  pulsarRostro();
  await avanzar(6000);
}

describe('Acceso del personal: entrar con rostro y prueba de vida', () => {
  let getUserMedia;
  let pista;

  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
    sessionStorage.clear();
    almacenSesion.cerrar();
    simularMovimientoReducido(false);
    const simulada = crearCamara();
    pista = simulada.pista;
    getUserMedia = vi.fn().mockResolvedValue(simulada.camara);
    simularNavegador(getUserMedia);
    prepararMotor.mockResolvedValue();
    authService.desafioFacial.mockResolvedValue({ desafio: 'desafio-1', orden: ['derecha', 'izquierda'], venceEnSegundos: 30 });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('no abre la cámara ni pide el desafío hasta pulsar el botón; la contraseña está a la vista', () => {
    renderLogin();
    escribirEmail();

    expect(screen.getByRole('button', { name: /entrar con rostro/i })).toBeInTheDocument();
    expect(campoPassword()).toBeVisible();
    expect(getUserMedia).not.toHaveBeenCalled();
    expect(authService.desafioFacial).not.toHaveBeenCalled();
  });

  it('exige primero el email, sin abrir la cámara', async () => {
    renderLogin();
    pulsarRostro();
    await avanzar(10);

    expect(screen.getByText(/el correo es obligatorio/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/correo electrónico/i)).toHaveFocus();
    expect(getUserMedia).not.toHaveBeenCalled();
  });

  it('pide el desafío, sigue sus giros y entra mostrando el nombre y el rol', async () => {
    authService.loginFacial.mockResolvedValue(EXITO);
    renderLogin();
    escribirEmail();

    await intentarConRostro();

    expect(authService.desafioFacial).toHaveBeenCalledWith(EMAIL, expect.any(AbortSignal));
    expect(authService.loginFacial).toHaveBeenCalledTimes(1);
    const [email, { desafio, capturas }] = authService.loginFacial.mock.calls[0];
    expect(email).toBe(EMAIL);
    expect(desafio).toBe('desafio-1');
    expect(capturas.map((c) => c.paso)).toEqual(['frente', 'derecha', 'izquierda']);
    expect(pista.stop).toHaveBeenCalled();
    expect(almacenSesion.obtenerToken()).toBe('t');
    expect(screen.getAllByText('Entraste como Ana Díaz (administrador)').length).toBeGreaterThan(0);
    expect(screen.getByRole('heading', { name: 'Panel' })).toBeInTheDocument();
  });

  it('un rechazo del servidor muestra siempre el mismo mensaje y deja usar la contraseña', async () => {
    authService.loginFacial.mockRejectedValue({ estado: 401, codigo: 'no_autorizado' });
    authService.login.mockResolvedValue({ token: 'con-password', tipo: 'empleado', usuario: { nombre: 'Ana', rol: 'administrador' } });
    renderLogin();
    escribirEmail();

    await intentarConRostro();

    expect(screen.getByRole('alert')).toHaveTextContent(RECHAZO);
    expect(pista.stop).toHaveBeenCalled();
    expect(screen.getByRole('button', { name: /entrar con rostro/i })).toHaveFocus();

    fireEvent.change(campoPassword(), { target: { value: 'una-password' } });
    fireEvent.click(screen.getByRole('button', { name: /iniciar sesión/i }));
    await avanzar(10);
    expect(almacenSesion.obtenerToken()).toBe('con-password');
  });

  it('tras 3 rechazos seguidos el foco va a la contraseña y se resalta', async () => {
    authService.loginFacial.mockRejectedValue({ estado: 401 });
    renderLogin();
    escribirEmail();

    await intentarConRostro();
    await intentarConRostro();
    expect(campoPassword()).not.toHaveFocus();
    await intentarConRostro();

    expect(authService.desafioFacial).toHaveBeenCalledTimes(3);
    expect(campoPassword()).toHaveFocus();
    expect(campoPassword()).toHaveAccessibleDescription(/entra con tu contraseña/i);
  });

  it('demasiados intentos: lo explica y lleva a la contraseña', async () => {
    authService.loginFacial.mockRejectedValue({ estado: 429 });
    renderLogin();
    escribirEmail();

    await intentarConRostro();

    expect(screen.getByRole('alert')).toHaveTextContent(/demasiados intentos.*contraseña/i);
    expect(campoPassword()).toHaveFocus();
  });

  it('un error de red al pedir el desafío se explica y apaga la cámara', async () => {
    authService.desafioFacial.mockRejectedValue({ estado: 0, codigo: 'sin_conexion', message: 'x' });
    renderLogin();
    escribirEmail();

    await intentarConRostro();

    expect(screen.getByRole('alert')).toHaveTextContent(/no pudimos conectar/i);
    expect(pista.stop).toHaveBeenCalled();
    expect(authService.loginFacial).not.toHaveBeenCalled();
  });

  it('un error de red al enviar las capturas se explica con su texto', async () => {
    authService.loginFacial.mockRejectedValue({ estado: 0, codigo: 'sin_conexion', message: 'x' });
    renderLogin();
    escribirEmail();

    await intentarConRostro();

    expect(screen.getByRole('alert')).toHaveTextContent(/no pudimos conectar/i);
  });

  it('un desafío con un orden extraño no se usa', async () => {
    authService.desafioFacial.mockResolvedValue({ desafio: 'd', orden: ['izquierda', 'izquierda'] });
    renderLogin();
    escribirEmail();

    await intentarConRostro();

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(authService.loginFacial).not.toHaveBeenCalled();
  });

  it.each([
    ['NotAllowedError', /no tiene permiso/i],
    ['NotFoundError', /no encontramos una cámara/i],
  ])('ante %s explica el problema y deja usar la contraseña', async (nombre, mensaje) => {
    getUserMedia.mockRejectedValue(errorDeCamara(nombre));
    renderLogin();
    escribirEmail();

    await intentarConRostro();

    expect(screen.getByRole('alert')).toHaveTextContent(mensaje);
    expect(campoPassword()).toBeEnabled();
    expect(authService.loginFacial).not.toHaveBeenCalled();
  });

  it('bloquea el email durante el flujo y «Cancelar» apaga la cámara y devuelve el foco', async () => {
    detectarCuadro.mockImplementation(async () => ({ ancho: 640, alto: 480, brillo: 120, rostros: [] }));
    renderLogin();
    escribirEmail();
    pulsarRostro();
    await avanzar(100);

    expect(screen.getByLabelText(/correo electrónico/i)).toBeDisabled();
    expect(screen.getByText('Cámara activa')).toBeInTheDocument();
    const cancelar = screen.getByRole('button', { name: 'Cancelar' });
    expect(cancelar).toHaveFocus();

    fireEvent.click(cancelar);
    await avanzar(10);

    expect(pista.stop).toHaveBeenCalled();
    expect(screen.getByLabelText(/correo electrónico/i)).toBeEnabled();
    expect(screen.getByRole('button', { name: /entrar con rostro/i })).toHaveFocus();
  });
});
