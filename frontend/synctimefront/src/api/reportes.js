import { api } from './cliente';
import { parametrosReporte } from '../paginas/reportes/reglasReportes';

// Reportes y exportaciones (fase 13): GET /api/v1/reportes/* y su exportación en segundo plano. Contrato en
// docs/api/openapi.json. El operativo lo ve todo el personal; el gerencial y el parametrizable, solo dueño y administrador (la
// API lo exige también al pedir y al descargar el archivo).

const filas = (lista) => (Array.isArray(lista) ? lista : lista?.elementos ?? []);

export const reportes = {
  /** Un reporte como documento (título, filtros aplicados, tablas con sus totales, gráficos y notas): el mismo que se exporta. */
  leer: (reporte, filtros, { senal } = {}) => api.get(`/reportes/${reporte}`, { parametros: parametrosReporte(reporte, filtros), senal }),

  /** Pide un archivo: responde 202 con el trabajo ({ id, estado: 'pendiente', … }), sin esperarlo. `filtros` ya son los de ese reporte. */
  pedirExportacion: (reporte, formato, filtros) => api.post(`/reportes/${reporte}/exportaciones`, { formato, filtros }),

  /** El estado de un trabajo: pendiente, procesando, listo (con nombreArchivo) o fallido (con su motivo en `error`). */
  estado: (id, { senal } = {}) => api.get(`/reportes/exportaciones/${encodeURIComponent(id)}`, { senal }),

  /** El archivo de un trabajo listo, como Blob. 409 si todavía se prepara o falló; 410 si venció (24 horas). */
  descargar: (id, { senal } = {}) => api.archivo(`/reportes/exportaciones/${encodeURIComponent(id)}/archivo`, { senal }),

  /** Los clientes para el filtro («Cliente»): [{ idCliente, nombre }]. */
  async clientes({ senal } = {}) {
    const lista = await api.get('/clientes', { parametros: { tamano: 100 }, senal });
    return filas(lista).map((c) => ({ idCliente: c.idCliente, nombre: [c.nombre, c.apellido].filter(Boolean).join(' ') }));
  },
};
