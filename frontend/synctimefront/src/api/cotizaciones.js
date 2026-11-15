import { api } from './cliente';
import { todasLasPaginas } from './mantenedores';
import { comoPaginado, siVersion } from './solicitudes';

// Cotizaciones (fase 8). Contrato: docs/plan/fase-08-especificacion.md, «Rutas».
//
// Cotización (S06): { idCotizacion, numero, idSolicitud, numeroSolicitud, fechaChile, estado,
//   cliente: { idCliente, nombre, apellido, telefono, correo },
//   items: [{ producto, descripcion, anchoMm, altoMm, cantidad, areaM2 }],
//   materiales: [{ idMaterial, material, unidad, medidaAplicada, cantidad, costoUnitario, subtotal }],
//   costoMaterialesTotal, costoManoObra, otrosCostos, total, totalRedondeado, observaciones,
//   elaboradaPor: { idEmpleado, nombre }, respuesta: null | { fechaChile, via, motivo, registradaPor },
//   idPedido, rowVersion }
// Cuerpo de emitir y editar: { materiales: [{ idMaterial, medidaAplicada?, cantidad, costoUnitario }],
//   costoManoObra, otrosCostos, observaciones? }.

/** Del personal del taller. */
export const cotizaciones = {
  /** POST /solicitudes/{id}/cotizaciones → 201 cotización. 409 si la solicitud ya tiene una pendiente. */
  emitir: (idSolicitud, cotizacion) => api.post(`/solicitudes/${idSolicitud}/cotizaciones`, cotizacion),
  /** Solo «pendiente», con la versión vista: 409 version_obsoleta si otra persona la cambió. */
  editar: (id, cotizacion, rowVersion) => api.put(`/cotizaciones/${id}`, cotizacion, siVersion(rowVersion)),
  /** Respuesta que el cliente dio por teléfono, WhatsApp o en el local: { decision, via, motivo? }. */
  responder: (id, respuesta, rowVersion) => api.post(`/cotizaciones/${id}/respuesta`, respuesta, siVersion(rowVersion)),
  /** { estado, desde, hasta, idCliente, q, orden, pagina, tamano } → paginado. */
  listar: (filtros = {}, { senal } = {}) => api.get('/cotizaciones', { parametros: filtros, senal }).then((r) => comoPaginado(r, filtros)),
  obtener: (id) => api.get(`/cotizaciones/${id}`),
  /** Gestión: borrado lógico (solo pendiente o rechazada y sin pedido) → 204. */
  async eliminar(id, rowVersion) {
    await api.delete(`/cotizaciones/${id}`, siVersion(rowVersion));
  },
};

/** Del cliente con sesión: solo las suyas. */
export const misCotizaciones = {
  obtener: (id) => api.get(`/mis/cotizaciones/${id}`),
  /** { decision: 'aprobada' | 'rechazada', motivo? }, con la versión vista (If-Match). */
  responder: (id, respuesta, rowVersion) => api.post(`/mis/cotizaciones/${id}/respuesta`, respuesta, siVersion(rowVersion)),
};

/**
 * Catálogo de materiales del taller (GET /materiales): { idMaterial, nombre, unidadMedida,
 * costoUnitarioAprox, activo }. Solo los activos se pueden cotizar.
 */
export const materiales = {
  // Desde la fase 9 el listado es paginado: se traen todas las páginas (también los inactivos, que una
  // cotización ya emitida puede tener).
  listar: () => todasLasPaginas('/materiales'),
};
