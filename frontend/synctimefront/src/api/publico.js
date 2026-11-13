import { api } from './cliente';

// Rutas públicas (sin sesión) de la portada.
export const publico = {
  /** Vitrina paginada: { elementos, total, pagina, tamano, totalPaginas }. `categoria` es opcional. */
  catalogo: ({ categoria, pagina, tamano } = {}) => api.get('/catalogo', { parametros: { categoria, pagina, tamano } }),
  categorias: () => api.get('/categorias'),
  /** Tipos de letrero activos, para la lista del modal de solicitud. */
  productos: () => api.get('/productos'),
  /** Siete días, de lunes (1) a domingo (7). */
  horarios: () => api.get('/horarios'),
  /** { abiertoEnLocal, mensajeEstado, tiempoEstimadoRegreso, fechaUltimaModificacion } */
  cartel: () => api.get('/cartel-digital'),
  /**
   * Estado del local ahora (hora de Chile): { abiertoAhora, motivo, dentroDeHorario, horaChile, hoy,
   * proximaApertura, cartel, semana }. motivo: abierto | ausente | fuera_de_horario | cerrado_hoy.
   */
  estadoLocal: () => api.get('/local/estado'),
  /** [{ tipo, version }] de privacidad, términos, marketing y cookies. */
  documentosLegales: () => api.get('/documentos-legales'),
  /** Formulario de contacto. Responde 202 { recibida, mensaje }. */
  enviarContacto: (consulta) => api.post('/contacto', consulta),
  /** Registra que el visitante aceptó la analítica (204). */
  aceptarCookies: (visitante, version) => api.post('/consentimientos/cookies', { visitante, version }),
  /** Registra que el visitante retiró la analítica que había aceptado (204). */
  retirarCookies: (visitante) => api.post('/consentimientos/cookies', { visitante, acepta: false }),
};
