import { api, configurarClienteApi, ErrorApi } from '../api/cliente';

function respuesta(estado, cuerpo, cabeceras = {}) {
  return Promise.resolve(new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status: estado,
    headers: { 'Content-Type': 'application/json', ...cabeceras },
  }));
}

describe('cliente de la API', () => {
  let fetchSimulado;
  let alSesionVencida;

  beforeEach(() => {
    fetchSimulado = vi.fn();
    vi.stubGlobal('fetch', fetchSimulado);
    alSesionVencida = vi.fn();
    configurarClienteApi({ obtenerToken: () => null, alSesionVencida });
  });

  afterEach(() => vi.unstubAllGlobals());

  it('pide JSON a /api/v1 y devuelve el cuerpo', async () => {
    fetchSimulado.mockReturnValue(respuesta(200, [{ id: 1 }]));
    await expect(api.get('/catalogo')).resolves.toEqual([{ id: 1 }]);
    const [url, opciones] = fetchSimulado.mock.calls[0];
    expect(url).toBe('/api/v1/catalogo');
    expect(opciones.headers.get('Accept')).toBe('application/json');
    expect(opciones.headers.has('Authorization')).toBe(false);
  });

  it('agrega los parámetros de búsqueda y omite los vacíos', async () => {
    fetchSimulado.mockReturnValue(respuesta(200, []));
    await api.get('/catalogo', { parametros: { categoria: 3, buscar: '', pagina: undefined } });
    expect(fetchSimulado.mock.calls[0][0]).toBe('/api/v1/catalogo?categoria=3');
  });

  it('con sesión envía el token', async () => {
    configurarClienteApi({ obtenerToken: () => 'abc', alSesionVencida });
    fetchSimulado.mockReturnValue(respuesta(200, {}));
    await api.get('/pedidos');
    expect(fetchSimulado.mock.calls[0][1].headers.get('Authorization')).toBe('Bearer abc');
  });

  it('envía JSON en POST y PUT y entiende un 204 sin cuerpo', async () => {
    fetchSimulado.mockReturnValue(respuesta(204));
    await expect(api.put('/cartel', { abiertoEnLocal: false })).resolves.toBeNull();
    const [, opciones] = fetchSimulado.mock.calls[0];
    expect(opciones.method).toBe('PUT');
    expect(opciones.headers.get('Content-Type')).toBe('application/json');
    expect(JSON.parse(opciones.body)).toEqual({ abiertoEnLocal: false });
  });

  it('convierte el formato único de error de la API en un ErrorApi con su mensaje en español', async () => {
    fetchSimulado.mockReturnValue(respuesta(400, {
      error: 'Revisa los datos marcados.', codigo: 'datos_invalidos', nivel: 'api', errores: { email: ['Correo inválido.'] },
    }));
    const error = await api.post('/contacto', {}).catch((e) => e);
    expect(error).toBeInstanceOf(ErrorApi);
    expect(error.estado).toBe(400);
    expect(error.codigo).toBe('datos_invalidos');
    expect(error.message).toBe('Revisa los datos marcados.');
    expect(error.errores).toEqual({ email: ['Correo inválido.'] });
  });

  it('sin conexión explica qué pasó', async () => {
    fetchSimulado.mockRejectedValue(new TypeError('Failed to fetch'));
    const error = await api.get('/horarios').catch((e) => e);
    expect(error).toBeInstanceOf(ErrorApi);
    expect(error.codigo).toBe('sin_conexion');
    expect(error.message).toMatch(/No pudimos conectar/);
  });

  it('un error sin el formato de la API igual trae un mensaje comprensible', async () => {
    fetchSimulado.mockReturnValue(Promise.resolve(new Response('<html>502</html>', { status: 502 })));
    const error = await api.get('/horarios').catch((e) => e);
    expect(error.estado).toBe(502);
    expect(error.message).toMatch(/servidor/);
  });

  it('un 401 con sesión avisa que la sesión venció; sin sesión no', async () => {
    fetchSimulado.mockReturnValue(respuesta(401, { error: 'Debes iniciar sesión.', codigo: 'no_autenticado' }));
    await api.get('/catalogo').catch(() => {});
    expect(alSesionVencida).not.toHaveBeenCalled();

    configurarClienteApi({ obtenerToken: () => 'vencido', alSesionVencida });
    fetchSimulado.mockReturnValue(respuesta(401, { error: 'Debes iniciar sesión.', codigo: 'no_autenticado' }));
    await api.get('/pedidos').catch(() => {});
    expect(alSesionVencida).toHaveBeenCalledTimes(1);
  });

  it('un 401 que llega después de cambiar de sesión no cierra la sesión nueva', async () => {
    let token = 'de-la-sesion-anterior';
    configurarClienteApi({ obtenerToken: () => token, alSesionVencida });
    let responder;
    fetchSimulado.mockReturnValue(new Promise((r) => { responder = r; }));
    const pendiente = api.get('/pedidos').catch((e) => e);
    token = 'de-la-sesion-nueva';
    responder(new Response(JSON.stringify({ error: 'Debes iniciar sesión.' }), { status: 401, headers: { 'Content-Type': 'application/json' } }));
    const error = await pendiente;
    expect(error).toBeInstanceOf(ErrorApi);
    expect(error.estado).toBe(401);
    expect(alSesionVencida).not.toHaveBeenCalled();
  });

  it('dos lecturas iguales al mismo tiempo comparten una sola petición', async () => {
    fetchSimulado.mockImplementation(() => respuesta(200, [{ id: 7 }]));
    const [a, b] = await Promise.all([api.get('/categorias'), api.get('/categorias')]);
    expect(fetchSimulado).toHaveBeenCalledTimes(1);
    expect(a).toEqual(b);
    await api.get('/categorias');
    expect(fetchSimulado).toHaveBeenCalledTimes(2);
  });

  it('un 503 servicio_ocupado se reintenta una vez tras Retry-After', async () => {
    fetchSimulado
      .mockReturnValueOnce(respuesta(503, { error: 'Ocupado', codigo: 'servicio_ocupado' }, { 'Retry-After': '0' }))
      .mockReturnValueOnce(respuesta(201, { idSolicitud: 1 }));
    await expect(api.post('/mis/solicitudes', { a: 1 })).resolves.toEqual({ idSolicitud: 1 });
    expect(fetchSimulado).toHaveBeenCalledTimes(2);
  });

  it('si sigue ocupado, explica que vuelva a intentarlo en unos segundos', async () => {
    fetchSimulado.mockImplementation(() => respuesta(503, { error: 'Ocupado', codigo: 'servicio_ocupado' }, { 'Retry-After': '0' }));
    const error = await api.post('/mis/solicitudes', {}).catch((e) => e);
    expect(fetchSimulado).toHaveBeenCalledTimes(2);
    expect(error).toMatchObject({ estado: 503, codigo: 'servicio_ocupado' });
    expect(error.message).toBe('Estamos recibiendo muchas solicitudes; vuelve a intentarlo en unos segundos.');
  });

  it('PATCH con If-Match envía la versión y ningún cuerpo si no lo hay', async () => {
    fetchSimulado.mockReturnValue(respuesta(204));
    await api.patch('/solicitudes/1/eliminar-logico', undefined, { cabeceras: { 'If-Match': '"AAAB"' } });
    const [, opciones] = fetchSimulado.mock.calls[0];
    expect(opciones.method).toBe('PATCH');
    expect(opciones.body).toBeUndefined();
    expect(opciones.headers.get('If-Match')).toBe('"AAAB"');
  });

  it('los archivos privados se piden sin caché y llegan como Blob', async () => {
    fetchSimulado.mockReturnValue(Promise.resolve(new Response('x', { status: 200, headers: { 'Content-Type': 'image/webp' } })));
    const blob = await api.archivo('/adjuntos/1/miniatura');
    expect(blob).toBeInstanceOf(Blob);
    expect(fetchSimulado.mock.calls[0][1].cache).toBe('no-store');
  });
});
