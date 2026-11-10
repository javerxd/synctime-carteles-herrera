import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import LoginPage from '../pages/LoginPage';
import { AuthProvider } from '../context/AuthContext';
import { ToastProvider } from '../context/ToastContext';
import authService from '../api/authService';
import { capturarDescriptores } from '../rostro/motorFacial';
import { crearCamara, errorDeCamara, simularNavegador } from './rostroAuxiliares';

vi.mock('../api/authService', () => ({
  default: { login: vi.fn(), loginFacial: vi.fn() },
}));

vi.mock('../rostro/motorFacial', async (original) => ({
  ...(await original()),
  capturarDescriptores: vi.fn(),
}));

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <AuthProvider>
        <ToastProvider>
          <LoginPage />
        </ToastProvider>
      </AuthProvider>
    </MemoryRouter>
  );
}

const escribirEmail = (valor = 'empleado@letrerosherrera.cl') =>
  fireEvent.change(screen.getByLabelText(/correo electrónico/i), { target: { value: valor } });

const pulsarRostro = () => fireEvent.click(screen.getByRole('button', { name: /entrar con rostro/i }));

describe('LoginPage: entrar con rostro', () => {
  let getUserMedia;
  let pista;

  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
    const simulada = crearCamara();
    pista = simulada.pista;
    getUserMedia = vi.fn().mockResolvedValue(simulada.camara);
    simularNavegador(getUserMedia);
    capturarDescriptores.mockResolvedValue([[0.1, 0.2]]);
  });

  it('no abre la cámara ni carga la captura hasta pulsar el botón', () => {
    renderLogin();
    escribirEmail();

    expect(screen.getByRole('button', { name: /entrar con rostro/i })).toBeInTheDocument();
    expect(getUserMedia).not.toHaveBeenCalled();
    expect(capturarDescriptores).not.toHaveBeenCalled();
  });

  it('exige primero el email, sin abrir la cámara', async () => {
    renderLogin();

    pulsarRostro();

    expect(await screen.findByText(/el email es obligatorio/i)).toBeInTheDocument();
    expect(getUserMedia).not.toHaveBeenCalled();
  });

  it('no pide la contraseña para entrar con rostro', async () => {
    authService.loginFacial.mockResolvedValue({ token: 't', usuario: { nombre: 'Ana', rol: 'operario' } });
    renderLogin();
    escribirEmail();

    pulsarRostro();

    await waitFor(() => expect(authService.loginFacial).toHaveBeenCalledWith('empleado@letrerosherrera.cl', [0.1, 0.2]));
    expect(screen.queryByText(/la contraseña es obligatoria/i)).not.toBeInTheDocument();
    await waitFor(() => expect(sessionStorage.getItem('synctime_token')).toBe('t'));
    expect(pista.stop).toHaveBeenCalled();
  });

  it('ante un rechazo muestra un mensaje genérico y deja usar la contraseña', async () => {
    authService.loginFacial.mockRejectedValue({ status: 401 });
    authService.login.mockResolvedValue({ token: 'con-password', usuario: { nombre: 'Ana', rol: 'operario' } });
    renderLogin();
    escribirEmail();

    pulsarRostro();

    expect(await screen.findByRole('alert')).toHaveTextContent(/no se pudo verificar su rostro.*contraseña/i);
    expect(pista.stop).toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText(/^contraseña/i), { target: { value: 'una-password' } });
    fireEvent.click(screen.getByRole('button', { name: /iniciar sesión/i }));

    await waitFor(() => expect(sessionStorage.getItem('synctime_token')).toBe('con-password'));
  });

  it.each([
    ['NotAllowedError', /no tiene permiso/i],
    ['NotFoundError', /no se encontró una cámara/i],
  ])('ante %s explica el problema y deja usar la contraseña', async (nombre, mensaje) => {
    getUserMedia.mockRejectedValue(errorDeCamara(nombre));
    renderLogin();
    escribirEmail();

    pulsarRostro();

    expect(await screen.findByRole('alert')).toHaveTextContent(mensaje);
    expect(screen.getByLabelText(/^contraseña/i)).toBeEnabled();
    expect(screen.getByRole('button', { name: /entrar con rostro/i })).toBeEnabled();
    expect(authService.loginFacial).not.toHaveBeenCalled();
  });

  it('explica un error de red', async () => {
    authService.loginFacial.mockRejectedValue({ status: 0, message: 'No se pudo conectar con el servidor.' });
    renderLogin();
    escribirEmail();

    pulsarRostro();

    expect(await screen.findByRole('alert')).toHaveTextContent(/no se pudo conectar/i);
  });

  it('explica cuando se supera el límite de intentos', async () => {
    authService.loginFacial.mockRejectedValue({ status: 429 });
    renderLogin();
    escribirEmail();

    pulsarRostro();

    expect(await screen.findByRole('alert')).toHaveTextContent(/demasiados intentos.*contraseña/i);
  });

  it('cancelar apaga la cámara', async () => {
    capturarDescriptores.mockReturnValue(new Promise(() => {}));
    renderLogin();
    escribirEmail();

    pulsarRostro();
    await waitFor(() => expect(getUserMedia).toHaveBeenCalled());
    await waitFor(() => expect(capturarDescriptores).toHaveBeenCalled());
    fireEvent.click(screen.getByRole('button', { name: /cancelar/i }));

    await waitFor(() => expect(pista.stop).toHaveBeenCalled());
    expect(screen.getByRole('button', { name: /entrar con rostro/i })).toBeInTheDocument();
  });

  it('bloquea el email durante la captura: el rostro no puede enviarse con otro correo', async () => {
    capturarDescriptores.mockReturnValue(new Promise(() => {}));
    renderLogin();
    escribirEmail();

    pulsarRostro();

    await waitFor(() => expect(screen.getByLabelText(/correo electrónico/i)).toBeDisabled());
    expect(screen.getByLabelText(/^contraseña/i)).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: /cancelar/i }));
    await waitFor(() => expect(screen.getByLabelText(/correo electrónico/i)).toBeEnabled());
    expect(screen.getByLabelText(/^contraseña/i)).toBeEnabled();
  });

  it('el foco pasa a Cancelar y vuelve al botón al terminar', async () => {
    capturarDescriptores.mockReturnValue(new Promise(() => {}));
    renderLogin();
    escribirEmail();

    pulsarRostro();

    const cancelar = await screen.findByRole('button', { name: /cancelar/i });
    expect(cancelar).toHaveFocus();

    fireEvent.click(cancelar);

    await waitFor(() => expect(screen.getByRole('button', { name: /entrar con rostro/i })).toHaveFocus());
  });

  it('tras un rechazo del servidor el foco vuelve al botón', async () => {
    authService.loginFacial.mockRejectedValue({ status: 401 });
    renderLogin();
    escribirEmail();

    pulsarRostro();

    await screen.findByRole('alert');
    await waitFor(() => expect(screen.getByRole('button', { name: /entrar con rostro/i })).toHaveFocus());
  });
});
