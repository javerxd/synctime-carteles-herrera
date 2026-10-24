import httpClient from './httpClient';

/**
 * Servicio de productos/servicios.
 * Se usa para poblar selectores en el formulario de solicitudes.
 * Los productos son de lectura pública (AllowAnonymous en la API).
 */
const productoService = {
  /** GET /productos */
  async getAll() {
    const { data } = await httpClient.get('/productos');
    return data;
  },

  /** GET /productos/:id */
  async getById(id) {
    const { data } = await httpClient.get(`/productos/${id}`);
    return data;
  },
};

export default productoService;
