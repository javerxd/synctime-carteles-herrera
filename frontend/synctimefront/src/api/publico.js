import { api } from './cliente';

// Rutas públicas (sin sesión): catálogo, categorías, horario y cartel del local.
export const publico = {
  /** Trabajos visibles de la vitrina; la API no filtra por categoría, se filtra en la pantalla. */
  catalogo: () => api.get('/catalogo'),
  categorias: () => api.get('/categorias'),
  /** Tipos de letrero activos, para la lista del modal de solicitud. */
  productos: () => api.get('/productos'),
  /** Siete días, de lunes (1) a domingo (7). */
  horarios: () => api.get('/horarios'),
  /** { abiertoEnLocal, mensajeEstado, tiempoEstimadoRegreso, fechaUltimaModificacion } */
  cartel: () => api.get('/cartel-digital'),
};
