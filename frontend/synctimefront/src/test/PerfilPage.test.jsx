import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PerfilPage from '../pages/PerfilPage';
import { AuthProvider } from '../context/AuthContext';
import { ToastProvider } from '../context/ToastContext';
import authService from '../api/authService';
import { capturarDescriptores } from '../rostro/motorFacial';
import { guardarTokenEquipo, leerTokenEquipo } from '../rostro/tokenEquipo';
import { crearCamara, simularNavegador } from './rostroAuxiliares';

vi.mock('../api/authService', () => ({
  default: {
    estadoRostro: vi.fn(),
    activarRostro: vi.fn(),
    autorizarEquipo: vi.fn(),
    desactivarRostro: vi.fn(),
  },
}));

vi.mock('../rostro/motorFacial', async (original) => ({
  ...(await original()),
  capturarDescriptores: vi.fn(),
}));

const EMAIL = 'dueno@letrerosherrera.cl';
const consentimiento = { version: 'v-prueba', texto: ['Sistema biométrico: prueba.', 'Finalidad: prueba.'] };
const inactivo = { disponible: true, activo: false, requiereEquipo: true, equipoVigente: false, consentimiento };
const activo = { ...inactivo, activo: true, fechaConsentimiento: '2026-11-06T12:00:00+00:00', equipoVence: '2026-12-06T12:00:00+00:00', equipoVigente: true };

function renderPerfil() {
  sessionStorage.setItem('synctime_token', 't');
  sessionStorage.setItem('synctime_user', JSON.stringify({ nombre: 'Ana', apellido: 'Díaz', email: EMAIL, rol: 'dueño' }));
  return render(
    <AuthProvider>
      <ToastProvider>
        <PerfilPage />
      </ToastProvider>
    </AuthProvider>
  );
}

const escribirPassword = () =>
  fireEvent.change(screen.getByLabelText(/contraseña actual/i), { target: { value: 'mi-password' } });

describe('PerfilPage: acceso con rostro', () => {
  let getUserMedia;

  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
    localStorage.clear();
    getUserMedia = vi.fn().mockResolvedValue(crearCamara().camara);
    simularNavegador(getUserMedia);
    capturarDescriptores.mockResolvedValue([[0.1], [0.2], [0.3], [0.4], [0.5]]);
  });

  it('muestra el consentimiento antes de activar', async () => {
    authService.estadoRostro.mockResolvedValue(inactivo);
    renderPerfil();

    expect(await screen.findByText('Sistema biométrico: prueba.')).toBeInTheDocument();
    expect(screen.getByText('Finalidad: prueba.')).toBeInTheDocument();
    expect(getUserMedia).not.toHaveBeenCalled();
  });

  it('no abre la cámara sin aceptar el consentimiento', async () => {
    authService.estadoRostro.mockResolvedValue(inactivo);
    renderPerfil();
    await screen.findByText('Finalidad: prueba.');
    escribirPassword();

    fireEvent.click(screen.getByRole('button', { name: /activar con mi rostro/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/debe aceptar/i);
    expect(getUserMedia).not.toHaveBeenCalled();
  });

  it('no abre la cámara sin la contraseña', async () => {
    authService.estadoRostro.mockResolvedValue(inactivo);
    renderPerfil();
    await screen.findByText('Finalidad: prueba.');
    fireEvent.click(screen.getByRole('checkbox'));

    fireEvent.click(screen.getByRole('button', { name: /activar con mi rostro/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/contraseña/i);
    expect(getUserMedia).not.toHaveBeenCalled();
  });

  it('activa con la versión del consentimiento y guarda el token del equipo', async () => {
    authService.estadoRostro.mockResolvedValueOnce(inactivo).mockResolvedValue(activo);
    authService.activarRostro.mockResolvedValue({ activo: true, tokenEquipo: 'token-de-equipo' });
    renderPerfil();
    await screen.findByText('Finalidad: prueba.');
    fireEvent.click(screen.getByRole('checkbox'));
    escribirPassword();

    fireEvent.click(screen.getByRole('button', { name: /activar con mi rostro/i }));

    await waitFor(() => expect(authService.activarRostro).toHaveBeenCalledWith({
      password: 'mi-password',
      versionConsentimiento: 'v-prueba',
      descriptores: [[0.1], [0.2], [0.3], [0.4], [0.5]],
    }));
    expect(capturarDescriptores.mock.calls[0][1].cantidad).toBe(5);
    await waitFor(() => expect(leerTokenEquipo(EMAIL)).toBe('token-de-equipo'));
    expect(await screen.findByText(/este equipo está autorizado/i)).toBeInTheDocument();
  });

  it('si solo falla la consulta posterior, no dice que la activación falló', async () => {
    authService.estadoRostro.mockResolvedValueOnce(inactivo).mockRejectedValue({ status: 0, message: 'sin red' });
    authService.activarRostro.mockResolvedValue({ activo: true, tokenEquipo: 'token-de-equipo' });
    renderPerfil();
    await screen.findByText('Finalidad: prueba.');
    fireEvent.click(screen.getByRole('checkbox'));
    escribirPassword();

    fireEvent.click(screen.getByRole('button', { name: /activar con mi rostro/i }));

    await waitFor(() => expect(screen.getByText(/el cambio se guardó/i)).toBeInTheDocument());
    expect(screen.queryByText(/no se pudo activar/i)).not.toBeInTheDocument();
    expect(leerTokenEquipo(EMAIL)).toBe('token-de-equipo');
  });

  it('muestra el motivo si la contraseña es incorrecta y conserva la sesión', async () => {
    authService.estadoRostro.mockResolvedValue(inactivo);
    authService.activarRostro.mockRejectedValue({ status: 400, message: 'La contraseña actual no es correcta.' });
    renderPerfil();
    await screen.findByText('Finalidad: prueba.');
    fireEvent.click(screen.getByRole('checkbox'));
    escribirPassword();

    fireEvent.click(screen.getByRole('button', { name: /activar con mi rostro/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent('La contraseña actual no es correcta.');
    expect(sessionStorage.getItem('synctime_token')).toBe('t');
  });

  it('avisa si este equipo no está autorizado y permite autorizarlo con la contraseña', async () => {
    authService.estadoRostro.mockResolvedValue(activo);
    authService.autorizarEquipo.mockResolvedValue({ tokenEquipo: 'nuevo-token' });
    renderPerfil();

    expect(await screen.findByText(/este equipo no está autorizado/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /autorizar este equipo/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/contraseña/i);
    expect(authService.autorizarEquipo).not.toHaveBeenCalled();

    escribirPassword();
    fireEvent.click(screen.getByRole('button', { name: /autorizar este equipo/i }));

    await waitFor(() => expect(leerTokenEquipo(EMAIL)).toBe('nuevo-token'));
    expect(authService.autorizarEquipo).toHaveBeenCalledWith('mi-password');
  });

  it('no ofrece autorizar un equipo a los roles que no lo necesitan', async () => {
    authService.estadoRostro.mockResolvedValue({ ...activo, requiereEquipo: false, equipoVence: null, equipoVigente: false });
    renderPerfil();

    expect(await screen.findByText(/activado el/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /autorizar este equipo/i })).not.toBeInTheDocument();
  });

  it('desactiva tras confirmar, sin pedir contraseña, y borra el token local', async () => {
    guardarTokenEquipo(EMAIL, 'token-de-equipo');
    authService.estadoRostro.mockResolvedValueOnce(activo).mockResolvedValue(inactivo);
    authService.desactivarRostro.mockResolvedValue();
    renderPerfil();

    fireEvent.click(await screen.findByRole('button', { name: /desactivar y borrar mis datos/i }));
    expect(authService.desactivarRostro).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: /^desactivar y borrar$/i }));

    await waitFor(() => expect(authService.desactivarRostro).toHaveBeenCalled());
    await waitFor(() => expect(leerTokenEquipo(EMAIL)).toBeNull());
    expect(await screen.findByText('Finalidad: prueba.')).toBeInTheDocument();
  });

  it('informa si la función no está disponible', async () => {
    authService.estadoRostro.mockResolvedValue({ ...inactivo, disponible: false });
    renderPerfil();

    expect(await screen.findByText(/no está disponible/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /activar con mi rostro/i })).not.toBeInTheDocument();
  });
});
