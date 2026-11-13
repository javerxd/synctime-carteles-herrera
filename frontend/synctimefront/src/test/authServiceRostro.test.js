import { beforeEach, describe, expect, it, vi } from 'vitest';
import authService from '../api/authService';
import { api } from '../api/cliente';
import { guardarTokenEquipo } from '../rostro/tokenEquipo';

vi.mock('../api/cliente', () => ({
  api: { post: vi.fn().mockResolvedValue({}) },
}));

describe('authService.loginFacial', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it('envía el token del equipo guardado para ese email, sin importar mayúsculas', async () => {
    guardarTokenEquipo('Dueno@Letreros.cl ', 'token-del-dueno');
    guardarTokenEquipo('otro@letreros.cl', 'token-de-otro');

    await authService.loginFacial('dueno@letreros.cl', [0.1]);

    expect(api.post).toHaveBeenCalledWith('/auth/login-facial', {
      email: 'dueno@letreros.cl',
      descriptor: [0.1],
      tokenEquipo: 'token-del-dueno',
    });
  });

  it('funciona sin token: el navegador no conoce el rol', async () => {
    await authService.loginFacial('operario@letreros.cl', [0.1]);

    expect(api.post.mock.calls[0][1].tokenEquipo).toBeNull();
  });
});
