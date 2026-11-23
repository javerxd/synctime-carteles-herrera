import { act, render, screen } from '@testing-library/react';
import { api, configurarClienteApi, ErrorApi, ESPERAS_DESPERTAR } from '../api/cliente';
import { estadoDespertar } from '../api/despertar';
import AvisoDespertando from '../componentes/AvisoDespertando';

// Fase 16C-1 (H13, H14): Render gratuito se duerme y tarda cerca de un minuto en despertar (y la base de Azure SQL gratuita tambien se pausa).
// La primera visita del dia recibe un 502/503/504 del proxy (Vercel) o de Render, sin el formato de error de la API. El cliente lo distingue de
// un error de la API, avisa «el sistema esta despertando» y repite las LECTURAS solo, sin que la persona recargue.

const sinFormato = (estado) => Promise.resolve(new Response('<html>Service unavailable</html>', { status: estado, headers: { 'Content-Type': 'text/html' } }));
const json = (estado, cuerpo) => Promise.resolve(new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } }));

describe('el cliente de la API frente a un servidor que despierta', () => {
  let fetchSimulado;

  beforeEach(() => {
    vi.useFakeTimers();
    fetchSimulado = vi.fn();
    vi.stubGlobal('fetch', fetchSimulado);
    configurarClienteApi({ obtenerToken: () => null, renovarSesion: async () => false, alSesionVencida: () => {}, esperasDespertar: undefined });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
    expect(estadoDespertar.obtener().activo).toBe(false);
  });

  it('una lectura que recibe 502, 503 o 504 sin el formato de la API se repite sola hasta que el servidor responde', async () => {
    for (const estado of [502, 503, 504]) {
      fetchSimulado.mockReset();
      fetchSimulado.mockReturnValueOnce(sinFormato(estado)).mockReturnValueOnce(sinFormato(estado)).mockReturnValueOnce(json(200, [{ id: 1 }]));

      const lectura = api.get(`/catalogo-${estado}`);
      await vi.advanceTimersByTimeAsync(0);
      expect(fetchSimulado).toHaveBeenCalledTimes(1);
      expect(estadoDespertar.obtener()).toMatchObject({ activo: true, intento: 1 });

      await vi.advanceTimersByTimeAsync(ESPERAS_DESPERTAR[0]);
      expect(fetchSimulado).toHaveBeenCalledTimes(2);
      expect(estadoDespertar.obtener()).toMatchObject({ activo: true, intento: 2 });

      await vi.advanceTimersByTimeAsync(ESPERAS_DESPERTAR[1]);
      await expect(lectura).resolves.toEqual([{ id: 1 }]);
      expect(fetchSimulado).toHaveBeenCalledTimes(3);
      expect(estadoDespertar.obtener().activo).toBe(false);
    }
  });

  it('las esperas crecen y en total cubren un arranque en frio de un minuto o mas', () => {
    expect(ESPERAS_DESPERTAR.length).toBeGreaterThanOrEqual(8);
    expect(ESPERAS_DESPERTAR.reduce((a, b) => a + b, 0)).toBeGreaterThanOrEqual(60_000);
    expect(Math.max(...ESPERAS_DESPERTAR)).toBeLessThanOrEqual(15_000);
    expect(ESPERAS_DESPERTAR).toEqual([...ESPERAS_DESPERTAR].sort((a, b) => a - b));
  });

  // Fallo a proposito: el servidor nunca despierta. Se rinde con un mensaje claro y el aviso desaparece.
  it('si el servidor no despierta, se rinde con un ErrorApi claro y quita el aviso', async () => {
    configurarClienteApi({ esperasDespertar: [10, 10] });
    fetchSimulado.mockImplementation(() => sinFormato(504));

    const lectura = api.get('/catalogo').catch((e) => e);
    await vi.advanceTimersByTimeAsync(100);
    const error = await lectura;

    expect(fetchSimulado).toHaveBeenCalledTimes(3);
    expect(error).toBeInstanceOf(ErrorApi);
    expect(error).toMatchObject({ estado: 504, codigo: 'http_504' });
    expect(error.message).toMatch(/sigue despertando/i);
    expect(estadoDespertar.obtener().activo).toBe(false);
  });

  it('un error de la API con su formato (codigo) NO es un servidor dormido: no se repite ni se avisa', async () => {
    fetchSimulado.mockReturnValue(json(503, { error: 'Google no responde.', codigo: 'google_no_disponible', nivel: 'api' }));

    const error = await api.get('/auth/google/config').catch((e) => e);

    expect(fetchSimulado).toHaveBeenCalledTimes(1);
    expect(error).toMatchObject({ estado: 503, codigo: 'google_no_disponible' });
    expect(estadoDespertar.obtener().activo).toBe(false);
  });

  it('un 500 o un 404 de siempre no se repite', async () => {
    for (const estado of [500, 404, 401, 429]) {
      fetchSimulado.mockReset();
      fetchSimulado.mockReturnValue(sinFormato(estado));
      const error = await api.get(`/x${estado}`).catch((e) => e);
      expect(error.estado).toBe(estado);
      expect(fetchSimulado).toHaveBeenCalledTimes(1);
    }
  });

  // Una escritura NO se repite sola: no se sabe si el servidor llego a procesarla antes de caer (podria duplicarse).
  it('un envio (POST, PUT, PATCH, DELETE) no se repite solo', async () => {
    for (const enviar of [
      () => api.post('/mis/solicitudes', { a: 1 }),
      () => api.put('/local', {}),
      () => api.patch('/pedidos/1', {}),
      () => api.delete('/pedidos/1'),
    ]) {
      fetchSimulado.mockReset();
      fetchSimulado.mockReturnValue(sinFormato(502));
      const error = await enviar().catch((e) => e);
      expect(error.estado).toBe(502);
      expect(fetchSimulado).toHaveBeenCalledTimes(1);
      expect(estadoDespertar.obtener().activo).toBe(false);
    }
  });

  it('el 503 servicio_ocupado sigue con su propio reintento, sin el aviso de «despertando»', async () => {
    fetchSimulado
      .mockReturnValueOnce(Promise.resolve(new Response(JSON.stringify({ error: 'Ocupado', codigo: 'servicio_ocupado' }), { status: 503, headers: { 'Content-Type': 'application/json', 'Retry-After': '0' } })))
      .mockReturnValueOnce(json(201, { idSolicitud: 1 }));

    const envio = api.post('/mis/solicitudes', { a: 1 });
    await vi.advanceTimersByTimeAsync(10);

    await expect(envio).resolves.toEqual({ idSolicitud: 1 });
    expect(estadoDespertar.obtener().activo).toBe(false);
  });

  it('sin conexion a internet no es un servidor dormido: el error de siempre, sin repetir', async () => {
    fetchSimulado.mockRejectedValue(new TypeError('Failed to fetch'));

    const error = await api.get('/horarios').catch((e) => e);

    expect(error.codigo).toBe('sin_conexion');
    expect(fetchSimulado).toHaveBeenCalledTimes(1);
  });

  it('cancelar la lectura (cambiar de pantalla) corta la espera y quita el aviso', async () => {
    fetchSimulado.mockImplementation(() => sinFormato(502));
    const control = new AbortController();

    const lectura = api.get('/catalogo', { senal: control.signal }).catch((e) => e);
    await vi.advanceTimersByTimeAsync(0);
    expect(estadoDespertar.obtener().activo).toBe(true);
    control.abort();
    const error = await lectura;

    expect(error.name).toBe('AbortError');
    expect(estadoDespertar.obtener().activo).toBe(false);
  });

  it('con varias lecturas esperando, el aviso se queda hasta que termina la ultima', async () => {
    fetchSimulado
      .mockReturnValueOnce(sinFormato(502)).mockReturnValueOnce(sinFormato(502))
      .mockReturnValue(json(200, []));

    const a = api.get('/a');
    const b = api.get('/b');
    await vi.advanceTimersByTimeAsync(0);
    expect(estadoDespertar.obtener().activo).toBe(true);

    await vi.advanceTimersByTimeAsync(ESPERAS_DESPERTAR[0]);
    await Promise.all([a, b]);
    expect(estadoDespertar.obtener().activo).toBe(false);
  });
});

describe('el aviso «el sistema esta despertando»', () => {
  afterEach(() => act(() => estadoDespertar.reiniciar()));

  it('antes de que haga falta, solo existe la region viva (vacia) para que el lector de pantalla oiga el cambio', () => {
    render(<AvisoDespertando />);

    const region = screen.getByRole('status');
    expect(region).toBeEmptyDOMElement();
    expect(region).toHaveAttribute('aria-live', 'polite');
    expect(screen.queryByText(/despertando/i)).toBeNull();
  });

  it('al despertar, anuncia el estado a los lectores de pantalla y lo muestra a la vista', () => {
    render(<AvisoDespertando />);

    act(() => { estadoDespertar.iniciar(); });

    expect(screen.getByRole('status')).toHaveTextContent(/el sistema está despertando/i);
    // La copia visual no se anuncia dos veces.
    const visual = screen.getByText(/puede tardar hasta un minuto/i).closest('[aria-hidden="true"]');
    expect(visual).not.toBeNull();
    expect(visual).toHaveTextContent(/el sistema está despertando/i);
  });

  it('cuando el servidor responde, el aviso desaparece y la region viva queda vacia', () => {
    render(<AvisoDespertando />);
    act(() => { estadoDespertar.iniciar(); });
    act(() => { estadoDespertar.terminar(); });

    expect(screen.getByRole('status')).toBeEmptyDOMElement();
    expect(screen.queryByText(/puede tardar hasta un minuto/i)).toBeNull();
  });

  it('no usa animaciones que ignoren «reducir movimiento» ni colores fuera de los tokens', async () => {
    const { readFileSync } = await import('node:fs');
    const { resolve } = await import('node:path');
    const css = readFileSync(resolve(process.cwd(), 'src/componentes/AvisoDespertando.module.css'), 'utf8');

    expect(css).not.toMatch(/#[0-9a-f]{3,8}\b/i);
    if (/animation/.test(css)) expect(css).toMatch(/prefers-reduced-motion/);
  });
});

describe('el almacen del estado', () => {
  afterEach(() => act(() => estadoDespertar.reiniciar()));

  it('cuenta los que esperan y avisa a quien se suscribe, sin repetir avisos iguales', () => {
    const oyente = vi.fn();
    const baja = estadoDespertar.suscribir(oyente);

    estadoDespertar.iniciar();
    estadoDespertar.iniciar();
    expect(estadoDespertar.obtener().activo).toBe(true);
    estadoDespertar.terminar();
    expect(estadoDespertar.obtener().activo).toBe(true);
    estadoDespertar.terminar();
    expect(estadoDespertar.obtener().activo).toBe(false);

    baja();
    estadoDespertar.iniciar();
    // Solo avisa cuando algo cambia: empezar (la primera vez) y terminar (la ultima). Tras darse de baja, nada.
    expect(oyente).toHaveBeenCalledTimes(2);
  });

  it('terminar de mas no deja un contador negativo', () => {
    estadoDespertar.terminar();
    estadoDespertar.terminar();
    estadoDespertar.iniciar();
    expect(estadoDespertar.obtener().activo).toBe(true);
  });

  it('la instantanea no cambia si no cambio nada (React la compara por identidad)', () => {
    const a = estadoDespertar.obtener();
    estadoDespertar.registrarIntento(0);
    expect(estadoDespertar.obtener()).toBe(a);
  });
});
