import { api } from './cliente';

// Rutas públicas (sin sesión) de la portada.
const lista = (r) => (Array.isArray(r) ? r : r?.elementos ?? []);
export const publico = {
  /** Vitrina paginada: { elementos, total, pagina, tamano, totalPaginas }. `categoria` es opcional. */
  catalogo: ({ categoria, pagina, tamano } = {}) => api.get('/catalogo', { parametros: { categoria, pagina, tamano } }),
  /**
   * Toda la vitrina (fase 12C): pide páginas de 100 (el máximo de la API) hasta completar, para que la
   * búsqueda se haga en el navegador al escribir, sin una consulta por letra. Tope de seguridad: 20 páginas.
   */
  catalogoCompleto: async (senal) => {
    const porId = new Map();
    for (let pagina = 1; pagina <= 20; pagina += 1) {
      const resultado = await api.get('/catalogo', { parametros: { pagina, tamano: 100 }, senal });
      // La paginación es por posición: si el taller publica algo a mitad de camino, un trabajo puede repetirse.
      for (const trabajo of resultado?.elementos ?? []) porId.set(trabajo.idCatalogo, trabajo);
      if (pagina >= (resultado?.totalPaginas ?? 1)) break;
    }
    return [...porId.values()];
  },
  /** Las categorías activas (la API pagina desde la fase 9: se piden de una vez). */
  categorias: () => api.get('/categorias', { parametros: { activo: true, tamano: 100 } }).then(lista),
  /** Tipos de letrero activos, para la lista del modal de solicitud. */
  productos: () => api.get('/productos', { parametros: { activo: true, tamano: 100 } }).then(lista),
  /** Siete días, de lunes (1) a domingo (7). Desde la fase 11 la API puede envolverlos con su versión ({ dias, rowVersion }). */
  horarios: () => api.get('/horarios').then((r) => (Array.isArray(r) ? r : r?.dias ?? [])),
  /** { abiertoEnLocal, mensajeEstado, tiempoEstimadoRegreso, fechaUltimaModificacion } */
  cartel: () => api.get('/cartel-digital'),
  /**
   * Estado del local ahora (hora de Chile): { abiertoAhora, motivo, dentroDeHorario, horaChile, hoy,
   * proximaApertura, cartel, semana }. motivo: abierto | ausente | fuera_de_horario | cerrado_hoy.
   */
  estadoLocal: () => api.get('/local/estado'),
  /** Franjas de atención (fase 9): [{ diaSemana, nombreDia, franjas: [{ desde, hasta }] }], sin nombres. */
  atencion: () => api.get('/local/atencion').then(lista),
  /** [{ tipo, version }] de privacidad, términos, marketing y cookies. */
  documentosLegales: () => api.get('/documentos-legales'),
  /** Formulario de contacto. Responde 202 { recibida, mensaje }. */
  enviarContacto: (consulta) => api.post('/contacto', consulta),
  /** Registra que el visitante aceptó la analítica (204). */
  aceptarCookies: (visitante, version) => api.post('/consentimientos/cookies', { visitante, version }),
  /** Registra que el visitante retiró la analítica que había aceptado (204). */
  retirarCookies: (visitante) => api.post('/consentimientos/cookies', { visitante, acepta: false }),
};
