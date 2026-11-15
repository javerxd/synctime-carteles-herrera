import { api } from './cliente';

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
  /** Solo dueño y administrador: { abiertoEnLocal, mensajeEstado, tiempoEstimadoRegreso } */
  actualizarCartel: (cartel) => api.put('/cartel-digital', cartel),
  /** Solo dueño y administrador: conversión en línea por año y mes. */
  conversionEnLinea: () => api.get('/metricas/conversion-online'),
};
