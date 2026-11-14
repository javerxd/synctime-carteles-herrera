import { api } from './cliente';

/**
 * Mi cuenta del cliente (fase 6). La API toma el cliente del token: nunca se envía su id.
 * MiCuenta: { idCliente, nombre, apellido, rut, telefono, correo, direccion, correoVerificado, marketing }.
 */
export const cuenta = {
  obtener: () => api.get('/mi-cuenta'),
  /** { nombre, apellido, rut, telefono, direccion } → MiCuenta. 400 por campo o 409 (teléfono o RUT de otra persona). */
  actualizar: (datos) => api.put('/mi-cuenta', datos),
  /** { correo, passwordActual } → MiCuenta con correoVerificado en false (se envía un enlace al correo nuevo). */
  cambiarCorreo: (datos) => api.put('/mi-cuenta/correo', datos),
  /** { passwordActual, passwordNueva } → { token }: las demás sesiones se cierran y esta sigue con el token nuevo. */
  cambiarPassword: (datos) => api.put('/mi-cuenta/password', datos),
  /** { acepta, version? } → 204. */
  async marketing(datos) {
    await api.put('/mi-cuenta/marketing', datos);
  },
};
