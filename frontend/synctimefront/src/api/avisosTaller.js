import { api } from './cliente';

// Avisos del taller (fase 9, pedido del usuario del 14-11-2026). Contrato: docs/plan/fase-09-especificacion.md,
// «Avisos del taller». Todo es de gestión (dueño y administrador), salvo confirmar, que es pública.
//
// GET /avisos-taller → { destinos: [{ idDestinatario, correo, estado: 'activo' | 'pendiente',
//   reemplazaA: { idDestinatario, correo } | null, fechaSolicitudChile, fechaConfirmacionChile, venceChile }],
//   usaRespaldo, historial: [{ fechaChile, accion, correo, empleado, detalle }] }

export const avisosTaller = {
  obtener: ({ senal } = {}) => api.get('/avisos-taller', { senal }),
  /**
   * { correo, idReemplaza?, passwordActual } → 201. Pide la contraseña de quien hace el cambio (400 si no es la
   * correcta; 429 tras muchos intentos). 409 ya_existe, 409 si el destino a reemplazar ya tiene un cambio pendiente
   * y 409 demasiados_pendientes con 3 por confirmar.
   */
  agregar: ({ correo, idReemplaza, passwordActual }) => api.post('/avisos-taller/destinos', {
    correo, ...(idReemplaza ? { idReemplaza } : {}), passwordActual,
  }),
  /** Enlace nuevo para un pendiente (invalida el anterior). 429 si se pide muy seguido. */
  reenviar: (id) => api.post(`/avisos-taller/destinos/${id}/reenviar`),
  /** El pendiente pasa a «cancelado». */
  cancelar: (id) => api.post(`/avisos-taller/destinos/${id}/cancelar`),
  /** Quita un activo, con la contraseña de quien lo quita (cuerpo { passwordActual }). 409 ultimo_destino si es el último. */
  async quitar(id, passwordActual) {
    await api.delete(`/avisos-taller/destinos/${id}`, { cuerpo: { passwordActual } });
  },
  /** Pública, sin sesión: { token } → { correo } enmascarado. 400 si el enlace no sirve. */
  confirmar: (token) => api.post('/avisos-taller/confirmar', { token }, { sinRenovar: true }),
  /** 202 { idsCorreos, destinatarios, usaRespaldo }. 429 si ya hubo 3 pruebas en 10 minutos. */
  probar: () => api.post('/avisos-taller/prueba'),
  /** [{ idCorreo, destinatario, estado: 'pendiente' | 'enviado' | 'fallido', intentos, error }] */
  estadoPrueba: (ids, { senal } = {}) => api.get('/avisos-taller/prueba/estado', { parametros: { ids: ids.join(',') }, senal }),
};
