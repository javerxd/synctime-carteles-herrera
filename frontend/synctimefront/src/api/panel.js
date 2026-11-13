import { api } from './cliente';

// Rutas del panel del taller (requieren sesión del personal).
export const panel = {
  /** Todos los pedidos con su cliente; la fase 8 lleva filtros y paginación a la API. */
  pedidos: () => api.get('/pedidos'),
  pedido: (id) => api.get(`/pedidos/${id}`),
  /** Solo dueño y administrador: { abiertoEnLocal, mensajeEstado, tiempoEstimadoRegreso } */
  actualizarCartel: (cartel) => api.put('/cartel-digital', cartel),
  /** Solo dueño y administrador: conversión en línea por año y mes. */
  conversionEnLinea: () => api.get('/metricas/conversion-online'),
};
