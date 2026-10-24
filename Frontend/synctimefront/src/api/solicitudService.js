import httpClient from './httpClient';

/**
 * Servicio CRUD de solicitudes.
 * Recurso principal del flujo de negocio.
 */
const solicitudService = {
  /** GET /solicitudes */
  async getAll() {
    const { data } = await httpClient.get('/solicitudes');
    return data;
  },

  /** GET /solicitudes/:id */
  async getById(id) {
    const { data } = await httpClient.get(`/solicitudes/${id}`);
    return data;
  },

  /** POST /solicitudes */
  async create(solicitud) {
    const { data } = await httpClient.post('/solicitudes', solicitud);
    return data;
  },

  /** PUT /solicitudes/:id */
  async update(id, solicitud) {
    const { data } = await httpClient.put(`/solicitudes/${id}`, solicitud);
    return data;
  },

  /** DELETE /solicitudes/:id */
  async remove(id) {
    await httpClient.delete(`/solicitudes/${id}`);
  },
};

export default solicitudService;
