import httpClient from './httpClient';

/**
 * Servicio de autenticación.
 * Gestiona login, perfil del usuario autenticado.
 */
const authService = {
  /** POST /auth/login — devuelve { token, usuario } */
  async login(email, password) {
    const { data } = await httpClient.post('/auth/login', { email, password });
    return data;
  },

  /** GET /auth/me — devuelve datos del usuario autenticado */
  async getProfile() {
    const { data } = await httpClient.get('/auth/me');
    return data;
  },
};

export default authService;
