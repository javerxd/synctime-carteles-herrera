import { api } from './cliente';
import { comoPaginado, siVersion } from './solicitudes';

// Pedidos, abonos, calendario y alertas (fase 8). Contrato: docs/plan/fase-08-especificacion.md, «Rutas».
// Las lecturas aceptan { senal } (AbortSignal): un filtro que cambia corta la petición anterior.
// Registrar un abono o crear un pedido no tienen clave de idempotencia: si la respuesta se pierde (estado 0),
// la pantalla relee el pedido o la cotización antes de dejar intentarlo de nuevo.
//
// Ficha (S07): { idPedido, numero, origen, solicitud: { idSolicitud, numero }, cotizacion, cliente,
//   responsable: { idEmpleado, nombre }, fechaPedido, fechaEntregaEstimada, fechaEntregaReal, modalidadEntrega,
//   requiereInstalacion, direccionInstalacion, montoBaseTrabajo, costoInstalacionAdicional, montoTotalFinal,
//   estadoPedido, estadoPago, montoAbonado, saldoPendiente, numeroFactura, cicloCerrado, motivoAnulacion,
//   aFavorDelCliente, transicionesPosibles, historial, pagos: [{ idPago, monto, fechaPago, medioPago,
//   observacion, registradoPor, fechaRegistroChile, anulado, motivoAnulacion, anuladoPor, fechaAnulacionChile,
//   puedeAnular }], rowVersion }
// Datos del pedido (E08): { idEmpleadoResponsable, fechaEntregaEstimada, modalidadEntrega, requiereInstalacion,
//   direccionInstalacion?, costoInstalacionAdicional, numeroFactura? }.
// Abono (E25): { monto, fechaPago?, medioPago, observacion?, numeroFactura? }.

const lista = (r) => (Array.isArray(r) ? r : r?.elementos ?? []);

export const pedidos = {
  /**
   * { estadoPedido, estadoPago, cobranza, anio, mes, idCliente, idResponsable, q, orden, pagina, tamano } →
   * { elementos, total, pagina, tamano, totalPaginas, resumen: { totalPorCobrar, cantidadPorCobrar } }.
   */
  listar: (filtros = {}, { senal } = {}) => api.get('/pedidos', { parametros: filtros, senal }).then((r) => comoPaginado(r, filtros)),
  obtener: (id) => api.get(`/pedidos/${id}`),
  /** Desde una cotización aprobada y sin pedido: datos E08 y `abonoInicial` opcional → 201 ficha. */
  crearDesdeCotizacion: (idCotizacion, datos) => api.post(`/cotizaciones/${idCotizacion}/pedido`, datos),
  /** Pedido presencial directo (RFU35, E26) → 201 ficha. */
  crearDirecto: (datos) => api.post('/pedidos/directo', datos),
  /** Los datos editables, con la versión vista (If-Match). */
  editar: (id, datos, rowVersion) => api.put(`/pedidos/${id}`, datos, siVersion(rowVersion)),
  /** { estado, fechaEntregaReal?, motivo?, nota? }, con la versión vista. 409 transicion_invalida o version_obsoleta. */
  cambiarEstado: (id, cambio, rowVersion) => api.patch(`/pedidos/${id}/estado`, cambio, siVersion(rowVersion)),
  /** Registrar un abono no exige versión (solo suma; la base protege el tope). 409 abono_excede_saldo → 201 ficha. */
  registrarAbono: (id, abono) => api.post(`/pedidos/${id}/pagos`, abono),
  /** { motivo } → 200 ficha. 403 solo_gestion si no es de quien lo registró en las últimas 24 horas. */
  anularAbono: (id, idPago, motivo) => api.post(`/pedidos/${id}/pagos/${idPago}/anular`, { motivo }),
  /** Gestión: borrado lógico de un pedido anulado sin abonos vigentes → 204. */
  async eliminar(id, rowVersion) {
    await api.delete(`/pedidos/${id}`, siVersion(rowVersion));
  },
  /** Como máximo 62 días: [{ idPedido, numero, fecha, cliente, modalidadEntrega, estadoPedido, estadoPago, atrasado }]. */
  calendario: ({ desde, hasta }, { senal } = {}) => api.get('/pedidos/calendario', { parametros: { desde, hasta }, senal }).then(lista),
  /** Gestión: { proximas, atrasadas, totalPorCobrar, cantidadPorCobrar }. */
  alertas: () => api.get('/pedidos/alertas'),
};

/** El personal activo, para elegir el responsable del pedido (GET /empleados). */
export const empleados = {
  listar: () => api.get('/empleados').then(lista),
};
