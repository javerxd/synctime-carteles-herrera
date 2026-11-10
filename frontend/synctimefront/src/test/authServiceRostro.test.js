import { beforeEach, describe, expect, it, vi } from 'vitest';
import authService from '../api/authService';
import httpClient from '../api/httpClient';
import { guardarTokenEquipo } from '../rostro/tokenEquipo';

vi.mock('../api/httpClient', () => ({
  default: { post: vi.fn().mockResolvedValue({ data: {} }) },
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

    expect(httpClient.post).toHaveBeenCalledWith('/auth/login-facial', {
      email: 'dueno@letreros.cl',
      descriptor: [0.1],
      tokenEquipo: 'token-del-dueno',
    });
  });

  it('funciona sin token: el navegador no conoce el rol', async () => {
    await authService.loginFacial('operario@letreros.cl', [0.1]);

    expect(httpClient.post.mock.calls[0][1].tokenEquipo).toBeNull();
  });
});
