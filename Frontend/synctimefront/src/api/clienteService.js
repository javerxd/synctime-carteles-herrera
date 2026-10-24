import httpClient from './httpClient';

/**
 * Servicio de clientes.
 * Se usa principalmente para poblar selectores en formularios.
 */
const clienteService = {
  /** GET /clientes */
  async getAll() {
    const { data } = await httpClient.get('/clientes');
    return data;
  },

  /** GET /clientes/:id */
  async getById(id) {
    const { data } = await httpClient.get(`/clientes/${id}`);
    return data;
  },
};

export default clienteService;
