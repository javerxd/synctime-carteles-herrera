import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import IngresoPersonal from '../paginas/publicas/IngresoPersonal';
import { almacenSesion } from '../sesion/sesion';
import authService from '../api/authService';

vi.mock('../api/authService', () => ({
  default: {
    login: vi.fn(),
  },
}));

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/personal/ingresar']}>
      <IngresoPersonal />
    </MemoryRouter>
  );
}

describe('Acceso del personal con contraseña', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
    almacenSesion.cerrar();
  });

  it('renders login form with inputs and submit button', () => {
    renderLogin();

    expect(screen.getByLabelText(/correo electrónico/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/contraseña/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /iniciar sesión/i })).toBeInTheDocument();
  });

  it('displays client-side validation errors when submitting empty form', async () => {
    renderLogin();

    const submitBtn = screen.getByRole('button', { name: /iniciar sesión/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(screen.getByText(/el correo es obligatorio/i)).toBeInTheDocument();
      expect(screen.getByText(/la contraseña es obligatoria/i)).toBeInTheDocument();
    });
    expect(authService.login).not.toHaveBeenCalled();
  });

  it('calls authService.login and stores token on successful submit', async () => {
    authService.login.mockResolvedValueOnce({
      token: 'fake-jwt-token-123',
      usuario: { id: 1, email: 'admin@letrerosherrera.cl', nombre: 'Admin Dueño', rol: 'dueno' },
    });

    renderLogin();

    fireEvent.change(screen.getByLabelText(/correo electrónico/i), {
      target: { value: 'admin@letrerosherrera.cl' },
    });
    fireEvent.change(screen.getByLabelText(/contraseña/i), {
      target: { value: 'Password123!' },
    });

    fireEvent.click(screen.getByRole('button', { name: /iniciar sesión/i }));

    await waitFor(() => {
      expect(authService.login).toHaveBeenCalledWith('admin@letrerosherrera.cl', 'Password123!');
      expect(almacenSesion.obtenerToken()).toBe('fake-jwt-token-123');
      expect(almacenSesion.obtener().usuario).toMatchObject({ tipo: 'empleado', email: 'admin@letrerosherrera.cl' });
    });
    // El token vive solo en memoria: ni sessionStorage ni localStorage lo guardan.
    expect(JSON.stringify({ ...sessionStorage })).not.toContain('fake-jwt-token-123');
    expect(JSON.stringify({ ...localStorage })).not.toContain('fake-jwt-token-123');
  });

  it('displays error alert on 401 unauthorized credentials', async () => {
    authService.login.mockRejectedValueOnce({ estado: 401,
      message: 'Credenciales inválidas.',
    });

    renderLogin();

    fireEvent.change(screen.getByLabelText(/correo electrónico/i), {
      target: { value: 'test@invalid.cl' },
    });
    fireEvent.change(screen.getByLabelText(/contraseña/i), {
      target: { value: 'wrongpass' },
    });

    fireEvent.click(screen.getByRole('button', { name: /iniciar sesión/i }));

    await waitFor(() => {
      expect(screen.getByText(/correo o contraseña incorrectos/i)).toBeInTheDocument();
    });
  });
});
