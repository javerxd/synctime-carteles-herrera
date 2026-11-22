import { api } from './cliente';

// Las rutas anónimas (nonce, ingreso, completar) no intentan renovar la sesión: un 401 aquí es del token de Google o del pase.
const SIN_RENOVAR = { sinRenovar: true };

/**
 * Ingreso con Google (fase 14A, contrato en docs/api/contrato.md, «Ingreso con Google»). La API verifica el token de
 * identidad que entrega Google; aquí nunca se guarda: ni el token, ni el nonce, ni el pase salen de la memoria ni de
 * los cuerpos de estas peticiones (jamás van en la URL).
 */
export const google = {
  /**
   * GET /auth/google/nonce → { nonce, venceEn }. De un solo uso. Cada petición lleva su propia señal: el cliente HTTP
   * comparte las lecturas iguales que van al mismo tiempo, y dos personas (o dos intentos) no pueden compartir un nonce.
   */
  nonce: () => api.get('/auth/google/nonce', { ...SIN_RENOVAR, senal: new AbortController().signal }),

  /**
   * POST /auth/google { idToken, nonce } → la sesión ({ token, tipo, usuario }) o, si el correo es nuevo,
   * { estado: 'registro_pendiente', pase, venceEn, correo, nombre, apellido }. 409 cuenta_existente | sin_acceso_web;
   * 401 google_token_invalido; 503 google_no_disponible | google_no_configurado; 429.
   */
  ingresar: (idToken, nonce) => api.post('/auth/google', { idToken, nonce }, SIN_RENOVAR),

  /**
   * POST /auth/google/completar (201): crea la cuenta y devuelve la sesión. datos: { pase, nombre?, apellido?, telefono,
   * rut?, direccion?, aceptaTerminosYPrivacidad, aceptaMarketing, versionTerminos, versionPrivacidad, versionMarketing? }.
   * 400 por campo (el pase sigue sirviendo); 409; 401 google_pase_invalido.
   */
  completar: (datos) => api.post('/auth/google/completar', datos, SIN_RENOVAR),

  /**
   * POST /mi-cuenta/google { idToken, nonce, passwordActual } → { vinculado: true, correo }. 400 con
   * errores.passwordActual o google_token_invalido (en Mi cuenta es 400: un 401 significaría «tu sesión venció»);
   * 409 google_correo_distinto | google_en_uso | google_ya_vinculada; 503.
   */
  vincular: ({ idToken, nonce, passwordActual }) => api.post('/mi-cuenta/google', { idToken, nonce, passwordActual }),

  /** DELETE /mi-cuenta/google { passwordActual } → 204. 400 errores.passwordActual; 409 sin_contrasena; 404 si no había Google. */
  async desvincular(passwordActual) {
    await api.delete('/mi-cuenta/google', { cuerpo: { passwordActual } });
  },

  /**
   * Fase 14C: Mi perfil del personal (los cuatro roles). Mismas reglas que Mi cuenta del cliente, en /mi-perfil/google:
   * el correo verificado de Google debe ser el de su ficha de empleado y siempre se puede desvincular (todo empleado
   * tiene contraseña). Un token inválido es 400 (no 401: un 401 significaría «tu sesión venció»).
   */
  perfil: {
    /** GET → { vinculado, correo }. */
    estado: ({ senal } = {}) => api.get('/mi-perfil/google', { senal }),
    /** POST { idToken, nonce, passwordActual } → { vinculado: true, correo }. 400 errores.passwordActual | google_token_invalido; 409 google_correo_distinto | google_en_uso | google_ya_vinculada; 503; 429. */
    vincular: ({ idToken, nonce, passwordActual }) => api.post('/mi-perfil/google', { idToken, nonce, passwordActual }),
    /**
     * DELETE { passwordActual } → 200 { token }: desvincular cierra TODAS las sesiones del empleado (si su Google estuviera
     * comprometido, la del atacante también) y abre una nueva en este equipo, como al cambiar la contraseña. 400
     * errores.passwordActual; 404 si no había Google.
     */
    desvincular: (passwordActual) => api.delete('/mi-perfil/google', { cuerpo: { passwordActual } }),
  },
};
