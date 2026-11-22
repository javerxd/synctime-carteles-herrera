import { api } from './cliente';
import { parametrosIndicadores } from '../paginas/indicadores/reglasIndicadores';

// Dashboard de gestión (fase 12): GET /api/v1/indicadores/* (solo dueño y administrador). Contrato en
// docs/api/openapi.json y docs/plan/fase-12-especificacion.md. Desde la fase 12B las cifras las calcula el servidor
// sobre vistas SQL; el adaptador de demostración de la 12A ya no existe.

const consulta = (ruta, { listado = false } = {}) => (filtros, { senal } = {}) =>
  api.get(`/indicadores/${ruta}`, { parametros: parametrosIndicadores(filtros, { listado, ruta }), senal });

/**
 * Las seis consultas del dashboard. Cada una recibe los filtros de la pantalla ({ periodo, desde, hasta, anio, mes,
 * comparar, estadoPedido, estadoPago, categoria, origen, cliente, alcanceSaldo, agrupar, cobro, pagina }) y { senal }.
 */
export const indicadores = {
  resumen: consulta('resumen'),
  evolucion: consulta('evolucion'),
  cobranza: consulta('cobranza'),
  embudo: consulta('embudo'),
  demanda: consulta('demanda'),
  porCobrar: consulta('por-cobrar', { listado: true }),

  /** Las opciones de los filtros: las categorías. Los clientes se buscan en el servidor al escribir (clientesFiltro, RFU27). */
  async catalogos({ senal } = {}) {
    const categorias = await api.get('/categorias', { parametros: { tamano: 100 }, senal });
    const filas = Array.isArray(categorias) ? categorias : categorias?.elementos ?? [];
    return { categorias: filas.map(({ idCategoria, nombre }) => ({ idCategoria, nombre })) };
  },
};
