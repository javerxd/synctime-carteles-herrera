import { act, render, renderHook, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import authService from '../api/authService';
import { api } from '../api/cliente';
import { almacenSesion, useSesion } from '../sesion/sesion';
import { RequierePersonal } from '../sesion/Guardas';

vi.mock('../api/authService', () => ({ default: { login: vi.fn(), loginFacial: vi.fn(), loginCliente: vi.fn() } }));

// Un JWT de prueba: solo importa que su carga útil traiga "exp" (segundos).
function token(segundosDeVida) {
  const carga = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + segundosDeVida }))
    .replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `cabecera.${carga}.firma`;
}

const DUENO = { idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', email: 'dueno@letreria.test', rol: 'dueño' };
const OPERARIO = { idEmpleado: 4, nombre: 'Hugo', apellido: 'Vera', email: 'operario@letreria.test', rol: 'operario' };

beforeEach(() => {
  vi.clearAllMocks();
  sessionStorage.clear();
  almacenSesion.cerrar();
});

describe('sesión', () => {
  it('el ingreso del personal guarda la sesión, su tipo y su rol, y el cliente HTTP usa su token', async () => {
    const t = token(3600);
    authService.login.mockResolvedValueOnce({ token: t, usuario: DUENO });
    const { result } = renderHook(() => useSesion());

    await act(() => result.current.ingresarPersonal('dueno@letreria.test', 'clave'));

    expect(result.current.usuario).toMatchObject({ tipo: 'empleado', rol: 'dueño', nombre: 'Marta' });
    expect(result.current.tieneRol(['dueño', 'administrador'])).toBe(true);
    expect(result.current.tieneRol('operario')).toBe(false);
    expect(JSON.parse(sessionStorage.getItem('synctime_sesion')).token).toBe(t);

    const fetchSimulado = vi.fn().mockResolvedValue(new Response('[]', { headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchSimulado);
    await api.get('/pedidos');
    expect(fetchSimulado.mock.calls[0][1].headers.get('Authorization')).toBe(`Bearer ${t}`);
    vi.unstubAllGlobals();
  });

  it('el ingreso de un cliente queda como tipo cliente', async () => {
    authService.loginCliente.mockResolvedValueOnce({ token: token(3600), cliente: { idCliente: 9, nombre: 'Rosa', apellido: 'Pino' } });
    const { result } = renderHook(() => useSesion());
    await act(() => result.current.ingresarCliente('900000201', 'clave'));
    expect(result.current.usuario).toMatchObject({ tipo: 'cliente', rol: 'cliente', nombre: 'Rosa' });
  });

  it('una sesión guardada con el token vencido no se recupera', () => {
    sessionStorage.setItem('synctime_sesion', JSON.stringify({ token: token(-10), usuario: { tipo: 'empleado', ...DUENO } }));
    almacenSesion.recargar();
    const { result } = renderHook(() => useSesion());
    expect(result.current.usuario).toBeNull();
  });

  it('si la API dice que la sesión venció, se cierra y se recuerda el motivo', () => {
    almacenSesion.guardar({ token: token(3600), usuario: { tipo: 'empleado', ...DUENO } });
    const { result } = renderHook(() => useSesion());
    act(() => almacenSesion.cerrar('vencida'));
    expect(result.current.usuario).toBeNull();
    expect(result.current.motivoCierre).toBe('vencida');
    expect(sessionStorage.getItem('synctime_sesion')).toBeNull();
  });

  it('salir borra la sesión', async () => {
    authService.login.mockResolvedValueOnce({ token: token(3600), usuario: DUENO });
    const { result } = renderHook(() => useSesion());
    await act(() => result.current.ingresarPersonal('a@b.cl', 'x'));
    act(() => result.current.salir());
    expect(result.current.usuario).toBeNull();
    expect(result.current.motivoCierre).toBe('salida');
  });
});

describe('RequierePersonal', () => {
  function rutas(entrada) {
    return render(
      <MemoryRouter initialEntries={[entrada]}>
        <Routes>
          <Route path="/personal/ingresar" element={<p>Pantalla de ingreso</p>} />
          <Route element={<RequierePersonal />}>
            <Route path="/panel" element={<p>Panel</p>} />
          </Route>
          <Route element={<RequierePersonal roles={['dueño', 'administrador']} />}>
            <Route path="/panel/reportes" element={<p>Reportes</p>} />
          </Route>
        </Routes>
      </MemoryRouter>,
    );
  }

  it('sin sesión lleva al ingreso del personal', () => {
    rutas('/panel');
    expect(screen.getByText('Pantalla de ingreso')).toBeInTheDocument();
  });

  it('un cliente no entra al panel del personal', () => {
    almacenSesion.guardar({ token: token(3600), usuario: { tipo: 'cliente', rol: 'cliente', nombre: 'Rosa' } });
    rutas('/panel');
    expect(screen.getByText('Pantalla de ingreso')).toBeInTheDocument();
  });

  it('con el rol permitido muestra la pantalla; sin él, la página 403', () => {
    almacenSesion.guardar({ token: token(3600), usuario: { tipo: 'empleado', ...OPERARIO } });
    const { unmount } = rutas('/panel');
    expect(screen.getByText('Panel')).toBeInTheDocument();
    unmount();
    rutas('/panel/reportes');
    expect(screen.getByRole('heading', { name: /no tienes permiso/i })).toBeInTheDocument();
    expect(screen.queryByText('Reportes')).toBeNull();
  });
});
