import { api } from './cliente';
import { leerTokenEquipo } from '../rostro/tokenEquipo';

/**
 * Autenticación del personal y de clientes, y acceso con rostro (contrato en docs/api/contrato.md).
 * Mismas funciones que antes; ahora sobre el cliente fetch (api/cliente.js).
 */
const authService = {
  /** POST /auth/login: personal con correo y contraseña. Devuelve { token, usuario }. */
  login(email, password) {
    return api.post('/auth/login', { email, password });
  },

  /**
   * POST /auth/login-facial: devuelve lo mismo que login.
   * Se envía el token de equipo si existe: el navegador no conoce el rol antes de iniciar sesión.
   */
  loginFacial(email, descriptor) {
    return api.post('/auth/login-facial', { email, descriptor, tokenEquipo: leerTokenEquipo(email) });
  },

  /** POST /auth/login-cliente: cliente con teléfono y contraseña (la fase 6 lo pasa a correo). */
  loginCliente(telefono, password) {
    return api.post('/auth/login-cliente', { telefono, password });
  },

  /** GET /auth/rostro: estado del acceso con rostro y texto del consentimiento. */
  estadoRostro() {
    return api.get('/auth/rostro');
  },

  /** PUT /auth/rostro: activa el acceso; devuelve tokenEquipo si el rol lo necesita. */
  activarRostro({ password, versionConsentimiento, descriptores }) {
    return api.put('/auth/rostro', { password, consentimiento: true, versionConsentimiento, descriptores });
  },

  /** POST /auth/rostro/equipo: autoriza este equipo y anula el anterior. */
  autorizarEquipo(password) {
    return api.post('/auth/rostro/equipo', { password });
  },

  /** DELETE /auth/rostro: desactiva el acceso y borra los datos biométricos. */
  async desactivarRostro() {
    await api.delete('/auth/rostro');
  },

  /** GET /auth/me: datos del usuario autenticado. */
  getProfile() {
    return api.get('/auth/me');
  },
};

export default authService;
