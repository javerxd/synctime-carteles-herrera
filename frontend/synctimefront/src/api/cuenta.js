import { api } from './cliente';

/**
 * Mi cuenta del cliente (fase 6). La API toma el cliente del token: nunca se envía su id.
 * MiCuenta: { idCliente, nombre, apellido, rut, telefono, correo, direccion, correoVerificado, marketing, avisosEstado }
 * (avisosEstado desde la fase 11).
 */
export const cuenta = {
  obtener: () => api.get('/mi-cuenta'),
  /** { nombre, apellido, rut, telefono, direccion } → MiCuenta. 400 por campo o 409 (teléfono o RUT de otra persona). */
  actualizar: (datos) => api.put('/mi-cuenta', datos),
  /** { correo, passwordActual } → MiCuenta con correoVerificado en false (se envía un enlace al correo nuevo). */
  cambiarCorreo: (datos) => api.put('/mi-cuenta/correo', datos),
  /** { passwordActual, passwordNueva } → { token }: las demás sesiones se cierran y esta sigue con el token nuevo. */
  cambiarPassword: (datos) => api.put('/mi-cuenta/password', datos),
  /** { acepta, version? } → 204. Desde la fase 11 es un alias de preferencias. */
  async marketing(datos) {
    await api.put('/mi-cuenta/marketing', datos);
  },
  /**
   * Fase 11: los correos opcionales, { avisosEstado, marketing } (y `versionMarketing`, la del texto de marketing, al aceptarlo).
   * Cada cambio queda como consentimiento con fecha y versión.
   */
  preferencias: (datos) => api.put('/mi-cuenta/preferencias', datos),
  /**
   * Fase 11: todos los datos del cliente en un archivo JSON (Blob), con la contraseña. 400 datos_invalidos si no es
   * la correcta; 429 tras muchos intentos. No queda en la caché del navegador.
   */
  descargarDatos: (password) => api.postArchivo('/mi-cuenta/datos', { password }),
  /**
   * Fase 11: inhabilita la cuenta, { password, confirmacion: 'DAR DE BAJA' } → { comprobante: { numero, fecha } }.
   * Se conservan las solicitudes, cotizaciones, pedidos y abonos; se cierran las sesiones.
   */
  darDeBaja: (datos) => api.post('/mi-cuenta/baja', datos),
};
