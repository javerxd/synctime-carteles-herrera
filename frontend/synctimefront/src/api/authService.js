import httpClient from './httpClient';
import { leerTokenEquipo } from '../rostro/tokenEquipo';

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

  /**
   * POST /auth/login-facial — devuelve lo mismo que login.
   * Se envía el token de equipo si existe: el navegador no conoce el rol antes de iniciar sesión.
   */
  async loginFacial(email, descriptor) {
    const { data } = await httpClient.post('/auth/login-facial', {
      email,
      descriptor,
      tokenEquipo: leerTokenEquipo(email),
    });
    return data;
  },

  /** GET /auth/rostro — estado del acceso con rostro y texto del consentimiento */
  async estadoRostro() {
    const { data } = await httpClient.get('/auth/rostro');
    return data;
  },

  /** PUT /auth/rostro — activa el acceso; devuelve tokenEquipo si el rol lo necesita */
  async activarRostro({ password, versionConsentimiento, descriptores }) {
    const { data } = await httpClient.put('/auth/rostro', {
      password,
      consentimiento: true,
      versionConsentimiento,
      descriptores,
    });
    return data;
  },

  /** POST /auth/rostro/equipo — autoriza este equipo y anula el anterior */
  async autorizarEquipo(password) {
    const { data } = await httpClient.post('/auth/rostro/equipo', { password });
    return data;
  },

  /** DELETE /auth/rostro — desactiva el acceso y borra los datos biométricos */
  async desactivarRostro() {
    await httpClient.delete('/auth/rostro');
  },

  /** GET /auth/me — devuelve datos del usuario autenticado */
  async getProfile() {
    const { data } = await httpClient.get('/auth/me');
    return data;
  },
};

export default authService;
