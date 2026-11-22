import { api } from './cliente';

// Fase 15B (RFU27): el filtro «Cliente» de indicadores y reportes busca en el servidor, no en una lista de 100 traída de antemano.
// GET /api/v1/clientes?q=&tamano=10 (solo personal) busca por nombre, apellido, teléfono o RUT; GET /clientes/{id} da el nombre
// del cliente que viene en la dirección.

/** Cuántas sugerencias se piden: las que caben en la lista del campo. */
export const SUGERENCIAS = 10;

const filas = (lista) => (Array.isArray(lista) ? lista : lista?.elementos ?? []);
// `detalle` (teléfono y RUT) va en la etiqueta de la lista: el navegador filtra las sugerencias por el valor Y por la etiqueta, así que
// escribir un teléfono o un RUT también las muestra.
const aOpcion = (c) => ({
  idCliente: c.idCliente,
  nombre: [c.nombre, c.apellido].filter(Boolean).join(' '),
  detalle: [c.telefono, c.rut].filter(Boolean).join(' · '),
});

export const clientesFiltro = {
  /** Los clientes que coinciden con `texto`: [{ idCliente, nombre }]. */
  async buscar(texto, { senal } = {}) {
    return filas(await api.get('/clientes', { parametros: { q: texto, tamano: SUGERENCIAS }, senal })).map(aOpcion);
  },

  /** El nombre de un cliente por su número, o null si no se puede saber (no existe o no hay permiso). */
  async nombre(idCliente, { senal } = {}) {
    try {
      const cliente = await api.get(`/clientes/${encodeURIComponent(idCliente)}`, { senal });
      return cliente ? aOpcion(cliente).nombre || null : null;
    } catch (error) {
      if (error?.name === 'AbortError') throw error;
      return null;
    }
  },
};
