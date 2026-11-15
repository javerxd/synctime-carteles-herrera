import { api, leerCookie } from './cliente';
import { leerTokenEquipo } from '../rostro/tokenEquipo';
import { comoCaptura, comoMuestras } from './rostros';

// Cookie legible que la API deja junto a la de renovación (httpOnly). Se repite en la cabecera
// X-CSRF-Token al renovar y al salir: un sitio ajeno no puede leerla, así que no puede imitar esas peticiones.
export const COOKIE_CSRF = 'synctime_csrf';
const conCsrf = (senal) => ({ cabeceras: { 'X-CSRF-Token': leerCookie(COOKIE_CSRF) }, sinRenovar: true, senal });

// Las rutas de ingreso responden 401 cuando la clave no es correcta: eso no es una sesión vencida.
const SIN_RENOVAR = { sinRenovar: true };

/**
 * Autenticación del personal y de clientes, sesión y acceso con rostro (contrato en docs/api/contrato.md).
 * Ingreso, registro y renovación responden lo mismo: { token, tipo: 'empleado' | 'cliente', usuario }.
 */
const authService = {
  /** POST /auth/login: personal con correo y contraseña. */
  login(email, password) {
    return api.post('/auth/login', { email, password }, SIN_RENOVAR);
  },

  /**
   * POST /auth/login-facial/desafio (fase 10): { desafio, orden: ['izquierda','derecha'] | ['derecha','izquierda'],
   * venceEnSegundos }. Responde igual exista o no el email; el orden lo elige el servidor.
   */
  desafioFacial(email, senal) {
    return api.post('/auth/login-facial/desafio', { email }, { ...SIN_RENOVAR, senal });
  },

  /**
   * POST /auth/login-facial: devuelve lo mismo que login, más { nombre, rol } (S15). Cualquier fallo es un 401 genérico.
   * capturas: frente, primer giro y segundo giro, cada una { descriptor, puntos, ancho, alto } (nunca la imagen).
   * Se envía el token de equipo si existe: el navegador no conoce el rol antes de iniciar sesión.
   */
  loginFacial(email, { desafio, capturas }) {
    return api.post('/auth/login-facial', {
      email, desafio, tokenEquipo: leerTokenEquipo(email), capturas: capturas.map(comoCaptura),
    }, SIN_RENOVAR);
  },

  /** POST /auth/login-cliente: el cliente ingresa con su correo y su contraseña. */
  loginCliente(correo, password) {
    return api.post('/auth/login-cliente', { correo, password }, SIN_RENOVAR);
  },

  /**
   * POST /auth/registro-cliente (201): crea la cuenta y deja la sesión iniciada.
   * datos: { nombre, apellido, telefono, correo, rut?, direccion?, password, aceptaTerminosYPrivacidad,
   * aceptaMarketing, versionTerminos, versionPrivacidad, versionMarketing }.
   */
  registrarCliente(datos) {
    return api.post('/auth/registro-cliente', datos, SIN_RENOVAR);
  },

  /** POST /auth/renovar (`senal`: tiempo máximo): con la cookie httpOnly y la cabecera CSRF. 200 | 401 | 409 (otra pestaña renovaba). */
  renovar(senal) {
    return api.post('/auth/renovar', undefined, conCsrf(senal));
  },

  /** POST /auth/salir (204): revoca la sesión de esta cookie. */
  async salir(senal) {
    await api.post('/auth/salir', undefined, conCsrf(senal));
  },

  /** POST /auth/verificar-correo: { verificado: true } o 400 si el enlace no sirve. */
  verificarCorreo(token) {
    return api.post('/auth/verificar-correo', { token }, SIN_RENOVAR);
  },

  /** POST /auth/reenviar-verificacion (cliente con sesión): 202 { mensaje }. */
  reenviarVerificacion() {
    return api.post('/auth/reenviar-verificacion');
  },

  /** POST /auth/recuperar: 202 { mensaje }, la misma respuesta exista o no la cuenta. tipo: 'cliente' | 'personal'. */
  recuperar(tipo, correo) {
    return api.post('/auth/recuperar', { tipo, correo }, SIN_RENOVAR);
  },

  /** POST /auth/restablecer: 204, o 400 con errores.codigo o errores.password. */
  async restablecer({ tipo, correo, codigo, password }) {
    await api.post('/auth/restablecer', { tipo, correo, codigo, password }, SIN_RENOVAR);
  },

  /** GET /auth/rostro: estado del acceso con rostro, consentimiento, debeRegistrarseDeNuevo y puedeUsarRostro. */
  estadoRostro() {
    return api.get('/auth/rostro');
  },

  /**
   * PUT /auth/rostro (solo gestión): registra las tres muestras { frente, izquierda, derecha } y devuelve
   * tokenEquipo. 400 con errores.muestras.<lado> si una muestra no sirve; 403 solo_gestion para otros roles.
   */
  activarRostro({ password, versionConsentimiento, muestras }) {
    return api.put('/auth/rostro', { password, consentimiento: true, versionConsentimiento, muestras: comoMuestras(muestras) });
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
