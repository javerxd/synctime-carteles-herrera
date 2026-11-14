import { act, render, renderHook, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { api } from '../api/cliente';
import { almacenSesion, MARCA, MENSAJE_SALIDA_FALLIDA, useSesion } from '../sesion/sesion';
import { RequiereCliente, RequierePersonal } from '../sesion/Guardas';

// Un JWT de prueba: solo importa que su carga útil traiga "exp" (segundos).
function token(segundosDeVida, marca = 'x') {
  const carga = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + segundosDeVida, marca }))
    .replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `cabecera.${carga}.firma`;
}

const DUENO = { idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', email: 'dueno@letreria.test', rol: 'dueño' };
const OPERARIO = { idEmpleado: 4, nombre: 'Hugo', apellido: 'Vera', email: 'operario@letreria.test', rol: 'operario' };
const ROSA = { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', telefono: '900000201', email: 'rosa@correo.test', correoVerificado: false };
const sesionPersonal = (t = token(900), usuario = DUENO) => ({ token: t, tipo: 'empleado', usuario });
const sesionCliente = (t = token(900)) => ({ token: t, tipo: 'cliente', usuario: ROSA });

function json(estado, cuerpo) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status: estado,
    headers: { 'Content-Type': 'application/json' },
  });
}

/** fetch simulado: `rutas` responde por "MÉTODO /ruta"; cada valor es una función o una lista de respuestas en orden. */
function simularApi(rutas) {
  const restantes = Object.fromEntries(Object.entries(rutas).map(([k, v]) => [k, Array.isArray(v) ? [...v] : v]));
  const fetchSimulado = vi.fn(async (url, opciones) => {
    const clave = `${opciones.method} ${url.replace('/api/v1', '')}`;
    const respuesta = restantes[clave];
    if (!respuesta) throw new Error(`Petición inesperada: ${clave}`);
    const siguiente = Array.isArray(respuesta) ? respuesta.shift() : respuesta;
    return typeof siguiente === 'function' ? siguiente(opciones) : siguiente;
  });
  vi.stubGlobal('fetch', fetchSimulado);
  return fetchSimulado;
}

const llamadas = (fetchSimulado, clave) => fetchSimulado.mock.calls.filter(([url, o]) => `${o.method} ${url.replace('/api/v1', '')}` === clave);

function borrarCookie() {
  document.cookie = 'synctime_csrf=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
}

beforeEach(() => {
  vi.clearAllMocks();
  sessionStorage.clear();
  localStorage.clear();
  borrarCookie();
  almacenSesion.cerrar();
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
  borrarCookie();
});

describe('sesión: el token vive solo en memoria', () => {
  it('el ingreso del personal deja la sesión con su tipo y su rol, el token solo en memoria y una marca sin datos', async () => {
    const t = token(900);
    const fetchSimulado = simularApi({
      'POST /auth/login': json(200, sesionPersonal(t)),
      'GET /pedidos': json(200, []),
    });
    const { result } = renderHook(() => useSesion());

    await act(() => result.current.ingresarPersonal('dueno@letreria.test', 'clave-de-prueba'));

    expect(result.current.usuario).toMatchObject({ tipo: 'empleado', rol: 'dueño', nombre: 'Marta', id: 1 });
    expect(result.current.tieneRol(['dueño', 'administrador'])).toBe(true);
    expect(result.current.tieneRol('operario')).toBe(false);
    expect(almacenSesion.obtenerToken()).toBe(t);
    // En el almacenamiento del navegador solo queda la marca; nunca el token ni los datos del usuario.
    expect({ ...sessionStorage }).toEqual({ [MARCA]: '1' });
    expect(localStorage.length).toBe(0);

    await api.get('/pedidos');
    const [, opciones] = llamadas(fetchSimulado, 'GET /pedidos')[0];
    expect(opciones.headers.get('Authorization')).toBe(`Bearer ${t}`);
    expect(opciones.credentials).toBe('same-origin');
  });

  it('el cliente ingresa con su correo y queda como tipo cliente', async () => {
    const fetchSimulado = simularApi({ 'POST /auth/login-cliente': json(200, sesionCliente()) });
    const { result } = renderHook(() => useSesion());

    await act(() => result.current.ingresarCliente('rosa@correo.test', 'clave-de-prueba'));

    expect(JSON.parse(fetchSimulado.mock.calls[0][1].body)).toEqual({ correo: 'rosa@correo.test', password: 'clave-de-prueba' });
    expect(result.current.usuario).toMatchObject({ tipo: 'cliente', rol: 'cliente', nombre: 'Rosa', correoVerificado: false });
    expect(result.current.esCliente).toBe(true);
    expect(result.current.esPersonal).toBe(false);
  });

  it('el registro deja la sesión del cliente iniciada', async () => {
    simularApi({ 'POST /auth/registro-cliente': json(201, sesionCliente()) });
    const { result } = renderHook(() => useSesion());
    await act(() => result.current.registrarCliente({ nombre: 'Rosa' }));
    expect(result.current.usuario).toMatchObject({ tipo: 'cliente', idCliente: 9 });
  });
});

describe('sesión: recuperarla al cargar la página', () => {
  it('con la marca, mientras renueva está «comprobando»; luego recupera la sesión enviando la cookie y el CSRF', async () => {
    sessionStorage.setItem(MARCA, '1');
    document.cookie = 'synctime_csrf=valor-csrf';
    let responder;
    const fetchSimulado = simularApi({ 'POST /auth/renovar': () => new Promise((r) => { responder = r; }) });
    const { result } = renderHook(() => useSesion());

    let promesa;
    act(() => { promesa = almacenSesion.iniciar(); });
    expect(result.current.comprobando).toBe(true);
    expect(result.current.usuario).toBeNull();

    await vi.waitFor(() => expect(responder).toBeTypeOf('function'));
    await act(async () => {
      responder(json(200, sesionPersonal()));
      await promesa;
    });
    expect(result.current.comprobando).toBe(false);
    expect(result.current.usuario).toMatchObject({ tipo: 'empleado', nombre: 'Marta' });
    const [, opciones] = fetchSimulado.mock.calls[0];
    expect(opciones.headers.get('X-CSRF-Token')).toBe('valor-csrf');
    expect(opciones.credentials).toBe('same-origin');
    expect(opciones.body).toBeUndefined();
  });

  it('sin marca ni cookie de sesión no pregunta a la API', async () => {
    const fetchSimulado = simularApi({});
    await act(() => almacenSesion.iniciar());
    expect(fetchSimulado).not.toHaveBeenCalled();
    expect(almacenSesion.obtener()).toMatchObject({ usuario: null, comprobando: false });
  });

  it('si la cookie ya no sirve (401), queda sin sesión, sin marca y sin motivo de cierre', async () => {
    sessionStorage.setItem(MARCA, '1');
    simularApi({ 'POST /auth/renovar': json(401) });
    await act(() => almacenSesion.iniciar());
    expect(almacenSesion.obtener()).toMatchObject({ usuario: null, comprobando: false, motivoCierre: null });
    expect(sessionStorage.getItem(MARCA)).toBeNull();
  });
});

describe('sesión: renovación silenciosa', () => {
  it('se programa un minuto antes de que venza el token y reemplaza el token en memoria', async () => {
    vi.useFakeTimers();
    const nuevo = token(900, 'nuevo');
    const fetchSimulado = simularApi({ 'POST /auth/renovar': json(200, sesionPersonal(nuevo)) });
    act(() => almacenSesion.aplicar(sesionPersonal(token(120, 'viejo'))));

    await act(() => vi.advanceTimersByTimeAsync(59 * 1000));
    expect(fetchSimulado).not.toHaveBeenCalled();

    await act(() => vi.advanceTimersByTimeAsync(1000));
    expect(llamadas(fetchSimulado, 'POST /auth/renovar')).toHaveLength(1);
    expect(almacenSesion.obtenerToken()).toBe(nuevo);
    expect(almacenSesion.obtener().usuario).toMatchObject({ nombre: 'Marta' });
  });

  it('un 401 con sesión renueva una vez y repite la petición una sola vez con el token nuevo', async () => {
    const nuevo = token(900, 'nuevo');
    const fetchSimulado = simularApi({
      'GET /pedidos': [json(401, { error: 'Debes iniciar sesión.' }), json(200, [{ idPedido: 1 }])],
      'POST /auth/renovar': json(200, sesionPersonal(nuevo)),
    });
    almacenSesion.aplicar(sesionPersonal(token(900, 'viejo')));

    await expect(api.get('/pedidos')).resolves.toEqual([{ idPedido: 1 }]);

    const pedidos = llamadas(fetchSimulado, 'GET /pedidos');
    expect(pedidos).toHaveLength(2);
    expect(pedidos[1][1].headers.get('Authorization')).toBe(`Bearer ${nuevo}`);
    expect(llamadas(fetchSimulado, 'POST /auth/renovar')).toHaveLength(1);
  });

  it('si la petición repetida vuelve a dar 401, no hay un segundo intento: la sesión se cierra como vencida', async () => {
    const fetchSimulado = simularApi({
      'GET /pedidos': [json(401), json(401)],
      'POST /auth/renovar': json(200, sesionPersonal(token(900, 'nuevo'))),
    });
    almacenSesion.aplicar(sesionPersonal());

    const error = await api.get('/pedidos').catch((e) => e);

    expect(error.estado).toBe(401);
    expect(llamadas(fetchSimulado, 'GET /pedidos')).toHaveLength(2);
    expect(llamadas(fetchSimulado, 'POST /auth/renovar')).toHaveLength(1);
    expect(almacenSesion.obtener()).toMatchObject({ usuario: null, motivoCierre: 'vencida' });
  });

  it('si la renovación es rechazada, la sesión se cierra como vencida y la petición no se repite', async () => {
    const fetchSimulado = simularApi({ 'GET /pedidos': [json(401)], 'POST /auth/renovar': json(401) });
    almacenSesion.aplicar(sesionPersonal());

    await expect(api.get('/pedidos')).rejects.toMatchObject({ estado: 401 });

    expect(llamadas(fetchSimulado, 'GET /pedidos')).toHaveLength(1);
    expect(almacenSesion.obtener()).toMatchObject({ usuario: null, motivoCierre: 'vencida' });
    expect(sessionStorage.getItem(MARCA)).toBeNull();
  });

  it('dos peticiones que reciben 401 a la vez comparten una sola renovación', async () => {
    let responder;
    const fetchSimulado = simularApi({
      'GET /pedidos': [json(401), json(200, [])],
      'GET /cartel-digital': [json(401), json(200, {})],
      'POST /auth/renovar': () => new Promise((r) => { responder = r; }),
    });
    almacenSesion.aplicar(sesionPersonal());

    const ambas = Promise.all([api.get('/pedidos'), api.get('/cartel-digital')]);
    await vi.waitFor(() => expect(responder).toBeTypeOf('function'));
    await new Promise((r) => { setTimeout(r, 0); });
    responder(json(200, sesionPersonal(token(900, 'nuevo'))));

    await expect(ambas).resolves.toEqual([[], {}]);
    expect(llamadas(fetchSimulado, 'POST /auth/renovar')).toHaveLength(1);
  });

  it('coordina las pestañas con navigator.locks cuando existe', async () => {
    const request = vi.fn((_nombre, tarea) => tarea());
    vi.stubGlobal('navigator', { ...navigator, locks: { request } });
    simularApi({ 'POST /auth/renovar': json(200, sesionPersonal()) });
    almacenSesion.aplicar(sesionPersonal());

    await expect(almacenSesion.renovar()).resolves.toBe('renovada');
    expect(request).toHaveBeenCalledWith('synctime-renovar', expect.any(Function));
  });

  it('si otra pestaña renovaba a la vez (409), espera unos 300 ms y reintenta una vez', async () => {
    vi.useFakeTimers();
    const fetchSimulado = simularApi({
      'POST /auth/renovar': [json(409, { error: 'La sesión se está renovando en otra pestaña.' }), json(200, sesionPersonal(token(900, 'nuevo')))],
    });
    almacenSesion.aplicar(sesionPersonal());

    const promesa = almacenSesion.renovar();
    await vi.advanceTimersByTimeAsync(250);
    expect(llamadas(fetchSimulado, 'POST /auth/renovar')).toHaveLength(1);
    await vi.advanceTimersByTimeAsync(100);

    await expect(promesa).resolves.toBe('renovada');
    expect(llamadas(fetchSimulado, 'POST /auth/renovar')).toHaveLength(2);
  });
});

describe('sesión: salir', () => {
  it('llama a /auth/salir con el CSRF y borra la sesión local', async () => {
    document.cookie = 'synctime_csrf=valor-csrf';
    const fetchSimulado = simularApi({ 'POST /auth/salir': json(204) });
    almacenSesion.aplicar(sesionPersonal());
    const { result } = renderHook(() => useSesion());

    await act(() => result.current.salir());

    const [, opciones] = llamadas(fetchSimulado, 'POST /auth/salir')[0];
    expect(opciones.headers.get('X-CSRF-Token')).toBe('valor-csrf');
    expect(result.current.usuario).toBeNull();
    expect(result.current.motivoCierre).toBe('salida');
    expect(almacenSesion.obtenerToken()).toBeNull();
    expect(sessionStorage.getItem(MARCA)).toBeNull();
  });

  it('aunque la API no responda, la sesión local se borra', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));
    almacenSesion.aplicar(sesionPersonal());
    const { result } = renderHook(() => useSesion());

    let error;
    await act(async () => { error = await result.current.salir().catch((e) => e); });

    // La sesión local se borra, pero la persona sabe que la del servidor puede seguir abierta.
    expect(error?.message).toBe(MENSAJE_SALIDA_FALLIDA);
    expect(result.current.usuario).toBeNull();
    expect(result.current.motivoCierre).toBe('salida');
  });

  it('salir espera la renovación en curso y luego cierra: la sesión no revive', async () => {
    let responder;
    const fetchSimulado = simularApi({ 'POST /auth/renovar': () => new Promise((r) => { responder = r; }), 'POST /auth/salir': json(204) });
    almacenSesion.aplicar(sesionPersonal());
    const { result } = renderHook(() => useSesion());

    const renovacion = almacenSesion.renovar();
    await vi.waitFor(() => expect(responder).toBeTypeOf('function'));
    let salida;
    act(() => { salida = result.current.salir(); });
    await new Promise((r) => { setTimeout(r, 0); });
    expect(llamadas(fetchSimulado, 'POST /auth/salir')).toHaveLength(0);

    responder(json(200, sesionPersonal(token(900, 'tarde'))));
    await act(() => salida);
    await renovacion;

    expect(llamadas(fetchSimulado, 'POST /auth/salir')).toHaveLength(1);
    expect(almacenSesion.obtener().usuario).toBeNull();
    expect(almacenSesion.obtenerToken()).toBeNull();
  });
});

describe('guardas de rutas', () => {
  function rutas(entrada) {
    return render(
      <MemoryRouter initialEntries={[entrada]}>
        <Routes>
          <Route path="/personal/ingresar" element={<p>Pantalla de ingreso</p>} />
          <Route path="/ingresar" element={<p>Ingreso de clientes</p>} />
          <Route element={<RequierePersonal />}>
            <Route path="/panel" element={<p>Panel</p>} />
          </Route>
          <Route element={<RequierePersonal roles={['dueño', 'administrador']} />}>
            <Route path="/panel/reportes" element={<p>Reportes</p>} />
          </Route>
          <Route path="/cuenta" element={<RequiereCliente><p>Mi cuenta</p></RequiereCliente>} />
        </Routes>
      </MemoryRouter>,
    );
  }

  it('sin sesión, el panel lleva al ingreso del personal y Mi cuenta al ingreso de clientes', () => {
    const { unmount } = rutas('/panel');
    expect(screen.getByText('Pantalla de ingreso')).toBeInTheDocument();
    unmount();
    rutas('/cuenta');
    expect(screen.getByText('Ingreso de clientes')).toBeInTheDocument();
  });

  it('mientras se comprueba la sesión no expulsa a nadie; al recuperarla muestra la pantalla', async () => {
    sessionStorage.setItem(MARCA, '1');
    let responder;
    simularApi({ 'POST /auth/renovar': () => new Promise((r) => { responder = r; }) });
    let promesa;
    act(() => { promesa = almacenSesion.iniciar(); });
    rutas('/panel');

    expect(screen.getByRole('status')).toHaveTextContent('Comprobando tu sesión');
    expect(screen.queryByText('Pantalla de ingreso')).toBeNull();

    await vi.waitFor(() => expect(responder).toBeTypeOf('function'));
    await act(async () => {
      responder(json(200, sesionPersonal()));
      await promesa;
    });
    expect(screen.getByText('Panel')).toBeInTheDocument();
  });

  it('un cliente no entra al panel del personal, pero sí a Mi cuenta', () => {
    almacenSesion.aplicar(sesionCliente());
    const { unmount } = rutas('/panel');
    expect(screen.getByText('Pantalla de ingreso')).toBeInTheDocument();
    unmount();
    rutas('/cuenta');
    expect(screen.getByText('Mi cuenta')).toBeInTheDocument();
  });

  it('con el rol permitido muestra la pantalla; sin él, la página 403', () => {
    almacenSesion.aplicar(sesionPersonal(token(900), OPERARIO));
    const { unmount } = rutas('/panel');
    expect(screen.getByText('Panel')).toBeInTheDocument();
    unmount();
    rutas('/panel/reportes');
    expect(screen.getByRole('heading', { name: /no tienes permiso/i })).toBeInTheDocument();
    expect(screen.queryByText('Reportes')).toBeNull();
  });
});

describe('sesión: casos límite de la revisión', () => {
  it('un 200 de /renovar sin JSON válido no deja «comprobando» colgado y no aplica nada', async () => {
    sessionStorage.setItem(MARCA, '1');
    simularApi({ 'POST /auth/renovar': new Response('<html>proxy</html>', { status: 200, headers: { 'Content-Type': 'text/html' } }) });
    let resultado;
    await act(async () => { resultado = await almacenSesion.iniciar(); });
    expect(resultado).toBe('pasajero');
    expect(almacenSesion.obtener()).toMatchObject({ usuario: null, comprobando: false, falloComprobacion: true });
    expect(almacenSesion.obtenerToken()).toBeNull();
  });

  it('una respuesta con forma incorrecta (sin tipo) también es pasajera', async () => {
    simularApi({ 'POST /auth/renovar': json(200, { token: 'x.y.z', usuario: DUENO }) });
    almacenSesion.aplicar(sesionPersonal());
    await expect(almacenSesion.renovar()).resolves.toBe('pasajero');
    expect(almacenSesion.obtener().usuario).toMatchObject({ nombre: 'Marta' });
  });

  it('/renovar y /salir llevan un tiempo máximo (AbortSignal)', async () => {
    const fetchSimulado = simularApi({ 'POST /auth/renovar': json(200, sesionPersonal()), 'POST /auth/salir': json(204) });
    almacenSesion.aplicar(sesionPersonal());
    await almacenSesion.renovar();
    const { result } = renderHook(() => useSesion());
    await act(() => result.current.salir());
    expect(fetchSimulado).toHaveBeenCalledTimes(2);
    for (const [, opciones] of fetchSimulado.mock.calls) expect(opciones.signal).toBeInstanceOf(AbortSignal);
  });

  it('una renovación abortada por tiempo es pasajera: la sesión sigue', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new DOMException('Tiempo agotado', 'TimeoutError')));
    almacenSesion.aplicar(sesionPersonal());
    await expect(almacenSesion.renovar()).resolves.toBe('pasajero');
    expect(almacenSesion.obtener().usuario).not.toBeNull();
  });

  it('con el reloj del equipo adelantado, la renovación no entra en bucle: usa exp - iat y un piso de 30 s', async () => {
    vi.useFakeTimers();
    // Según el reloj local el token ya venció (el equipo va una hora adelantado), pero dura 900 s.
    const ahora = Math.floor(Date.now() / 1000);
    const carga = btoa(JSON.stringify({ iat: ahora - 3600, exp: ahora - 3600 + 900 })).replace(/=+$/, '');
    const t = `c.${carga}.f`;
    const fetchSimulado = simularApi({ 'POST /auth/renovar': () => json(200, sesionPersonal(t)) });
    act(() => almacenSesion.aplicar(sesionPersonal(t)));

    await act(() => vi.advanceTimersByTimeAsync(10 * 1000));
    expect(fetchSimulado).not.toHaveBeenCalled();
    // 900 s de vida - 60 s de anticipo = 840 s desde que llegó la respuesta.
    await act(() => vi.advanceTimersByTimeAsync(831 * 1000));
    expect(llamadas(fetchSimulado, 'POST /auth/renovar')).toHaveLength(1);
  });

  it('si la renovación trae a otra persona (ingresó en otra pestaña), cierra esta sesión sin adoptarla', async () => {
    simularApi({ 'POST /auth/renovar': json(200, sesionCliente()) });
    almacenSesion.aplicar(sesionPersonal());
    await expect(almacenSesion.renovar()).resolves.toBe('rechazada');
    expect(almacenSesion.obtener()).toMatchObject({ usuario: null, motivoCierre: 'vencida' });
    expect(almacenSesion.obtenerToken()).toBeNull();
  });

  it('un 401 con una renovación fallida por el servidor no cierra la sesión: solo informa el error', async () => {
    simularApi({ 'GET /pedidos': [json(401)], 'POST /auth/renovar': json(503) });
    almacenSesion.aplicar(sesionPersonal());
    await expect(api.get('/pedidos')).rejects.toMatchObject({ estado: 401 });
    expect(almacenSesion.obtener().usuario).not.toBeNull();
  });

  it('otra pestaña que sale cierra esta sesión (BroadcastChannel)', async () => {
    almacenSesion.aplicar(sesionPersonal());
    const otra = new BroadcastChannel('synctime-sesion');
    otra.postMessage({ tipo: 'salida' });
    await vi.waitFor(() => expect(almacenSesion.obtener().usuario).toBeNull());
    otra.close();
  });

  it('si la comprobación inicial falla por el servidor, lo explica y deja reintentar', async () => {
    sessionStorage.setItem(MARCA, '1');
    simularApi({ 'POST /auth/renovar': [json(503), json(200, sesionPersonal())] });
    await act(() => almacenSesion.iniciar());
    render(
      <MemoryRouter initialEntries={['/panel']}>
        <Routes>
          <Route path="/personal/ingresar" element={<p>Pantalla de ingreso</p>} />
          <Route element={<RequierePersonal />}><Route path="/panel" element={<p>Panel</p>} /></Route>
        </Routes>
      </MemoryRouter>,
    );
    expect(screen.getByText('No pudimos comprobar tu sesión')).toBeInTheDocument();
    expect(screen.queryByText('Pantalla de ingreso')).toBeNull();
    await act(async () => { screen.getByRole('button', { name: 'Reintentar' }).click(); });
    expect(await screen.findByText('Panel')).toBeInTheDocument();
  });
});
