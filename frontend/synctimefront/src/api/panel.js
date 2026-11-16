import { api, versionDeEtag } from './cliente';
import { siVersion } from './solicitudes';

// Rutas del panel del taller (requieren sesión del personal).

const POR_PAGINA = 100; // el máximo que acepta la API
const PAGINAS_MAXIMAS = 20;

export const panel = {
  /**
   * Todos los pedidos (no eliminados), página por página. Lo usan los cálculos provisionales de Indicadores
   * y Reportes hasta que las fases 12 y 13 los lleven al servidor; el listado de pedidos filtra en la API.
   */
  async pedidos() {
    const todos = [];
    for (let pagina = 1; pagina <= PAGINAS_MAXIMAS; pagina += 1) {
      const respuesta = await api.get('/pedidos', { parametros: { pagina, tamano: POR_PAGINA } });
      if (Array.isArray(respuesta)) return respuesta;
      todos.push(...(respuesta?.elementos ?? []));
      if (!respuesta?.totalPaginas || pagina >= respuesta.totalPaginas) break;
    }
    return todos;
  },
  /**
   * El horario con su versión (fase 11): { dias, rowVersion }. La versión llega en el cuerpo ({ dias, rowVersion }) o,
   * si la API sigue entregando la lista de días, en el ETag.
   */
  async horario({ senal } = {}) {
    const { datos, cabeceras } = await api.get('/horarios', { senal, conCabeceras: true });
    const dias = Array.isArray(datos) ? datos : datos?.dias ?? [];
    return { dias, rowVersion: (Array.isArray(datos) ? null : datos?.rowVersion) ?? versionDeEtag(cabeceras.get('ETag')) };
  },
  /** El cartel con su versión (fase 11): la del cuerpo o, si no viene, la del ETag. */
  async cartel({ senal } = {}) {
    const { datos, cabeceras } = await api.get('/cartel-digital', { senal, conCabeceras: true });
    return { ...datos, rowVersion: datos?.rowVersion ?? versionDeEtag(cabeceras.get('ETag')) };
  },
  /**
   * Todo el personal (fase 9): { abiertoEnLocal, mensajeEstado (máx. 200), tiempoEstimadoRegreso? (máx. 100) }, con la
   * versión vista (If-Match, fase 11): 409 version_obsoleta si otra persona lo cambió; 400 sin_version sin ella.
   */
  actualizarCartel: (cartel, rowVersion) => api.put('/cartel-digital', cartel, siVersion(rowVersion)),
  /**
   * Gestión: { dias: [{ diaSemana, abierto, horaInicio?, horaFin? }] } con los 7 días → los 7 días guardados. Con la
   * versión vista (If-Match, fase 11).
   */
  guardarHorario: (dias, rowVersion) => api.put('/horarios', { dias }, siVersion(rowVersion)),
  /** Las franjas propias: [{ diaSemana, horaInicio, horaFin }] (o { franjas }). */
  miDisponibilidad: () => api.get('/mi-disponibilidad').then((r) => (Array.isArray(r) ? r : r?.franjas ?? [])),
  /** Reemplaza todas las franjas propias. */
  guardarMiDisponibilidad: (franjas) => api.put('/mi-disponibilidad', { franjas }),
  /** Gestión: las franjas de todo el personal, con su nombre. */
  disponibilidadEquipo: () => api.get('/disponibilidad').then((r) => (Array.isArray(r) ? r : r?.elementos ?? [])),
  /** Solo dueño y administrador: conversión en línea por año y mes. */
  conversionEnLinea: () => api.get('/metricas/conversion-online'),
};
