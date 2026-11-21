import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import authService from '../api/authService';
import { ErrorApi } from '../api/cliente';
import { google } from '../api/google';
import { ProveedorAvisos } from '../avisos/Avisos';
import { reiniciarCargaGoogle } from '../google/cargarGoogle';
import { paseGoogle } from '../google/pase';
import IngresoCliente from '../paginas/cuenta/IngresoCliente';
import Perfil from '../paginas/panel/Perfil';
import IngresoPersonal from '../paginas/publicas/IngresoPersonal';
import { almacenSesion } from '../sesion/sesion';
import {
  ID_PRUEBA, URL_SCRIPT_GOOGLE, almacenamientos, entregarCredencial, entregarScript, limpiarGoogle, scriptDeGoogle, ultimoNonce,
} from './googleFalso';

// Fase 14C (RFS03, RFU05, E02, RNFE02): el personal también entra con Google. Aquí, la parte del navegador: el botón en el acceso
// del personal, la sesión de personal que llega por Google y «Ingreso con Google» en Mi perfil, para los cuatro roles.

vi.mock('../api/authService', () => ({
  COOKIE_CSRF: 'synctime_csrf',
  default: {
    login: vi.fn(), loginCliente: vi.fn(), renovar: vi.fn(), salir: vi.fn().mockResolvedValue(undefined), estadoRostro: vi.fn(),
    desafioFacial: vi.fn(), loginFacial: vi.fn(),
  },
}));
vi.mock('../api/google', () => ({
  google: {
    nonce: vi.fn(), ingresar: vi.fn(), completar: vi.fn(), vincular: vi.fn(), desvincular: vi.fn(),
    perfil: { estado: vi.fn(), vincular: vi.fn(), desvincular: vi.fn() },
  },
}));
vi.mock('../api/avisosTaller', () => ({
  avisosTaller: { obtener: vi.fn(() => Promise.resolve({ destinos: [], usaRespaldo: false, historial: [] })) },
}));
vi.mock('../rostro/motorFacial', async (original) => ({ ...(await original()), prepararMotor: vi.fn(), detectarCuadro: vi.fn() }));

const EMPLEADO = { idEmpleado: 4, nombre: 'Hugo', apellido: 'Vera', email: 'hugo@letreria.test', rol: 'operario' };
const sesionEmpleado = (rol = 'operario') => ({ token: 'jwt-del-personal', tipo: 'empleado', usuario: { ...EMPLEADO, rol } });
const ROSA = { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', telefono: '912345678', email: 'rosa@correo.test', correoVerificado: true };
const error = (estado, codigo, mensaje, extra = {}) => new ErrorApi({ estado, codigo, mensaje, ...extra });
const pendiente = () => ({
  estado: 'registro_pendiente', pase: 'PASE-DE-PERSONAL', venceEn: new Date(Date.now() + 600_000).toISOString(), correo: 'nadie@correo.test', nombre: 'Nora', apellido: '',
});

function Destino({ nombre }) {
  const ubicacion = useLocation();
  return (
    <div>
      <p>{nombre}</p>
      <output data-testid="ubicacion">{JSON.stringify({ ruta: ubicacion.pathname, busqueda: ubicacion.search, state: ubicacion.state })}</output>
    </div>
  );
}

function dibujar(entrada, elemento, ruta) {
  return render(
    <ProveedorAvisos>
      <MemoryRouter initialEntries={[entrada]}>
        <Routes>
          <Route path={ruta} element={elemento} />
          <Route path="/panel" element={<Destino nombre="Panel del personal" />} />
          <Route path="/cuenta" element={<Destino nombre="Mi cuenta (cliente)" />} />
          <Route path="/cuenta/google/completar" element={<Destino nombre="Completa tu registro (destino)" />} />
        </Routes>
      </MemoryRouter>
    </ProveedorAvisos>,
  );
}

const botonGoogle = () => screen.getByRole('button', { name: /continuar con google/i });
let contadorNonce;

beforeEach(() => {
  vi.clearAllMocks();
  for (const mock of [google.ingresar, google.completar, google.vincular, google.desvincular, google.perfil.estado, google.perfil.vincular, google.perfil.desvincular]) mock.mockReset();
  vi.stubEnv('VITE_GOOGLE_CLIENT_ID', ID_PRUEBA);
  sessionStorage.clear();
  localStorage.clear();
  almacenSesion.cerrar();
  paseGoogle.limpiar();
  limpiarGoogle();
  reiniciarCargaGoogle();
  contadorNonce = 0;
  google.nonce.mockImplementation(async () => ({ nonce: `nonce-personal-${++contadorNonce}`, venceEn: new Date(Date.now() + 300_000).toISOString() }));
  google.perfil.estado.mockResolvedValue({ vinculado: false, correo: null });
  authService.estadoRostro.mockResolvedValue({
    disponible: true, activo: false, requiereEquipo: true, equipoVigente: false, consentimiento: { version: 'v', texto: ['x'] }, puedeUsarRostro: true, debeRegistrarseDeNuevo: false,
  });
});

afterEach(() => {
  vi.unstubAllEnvs();
  limpiarGoogle();
});

describe('/personal/ingresar: «Continuar con Google»', () => {
  it('sin ID de cliente no aparece el botón ni el separador, y el acceso queda como antes', () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', '');
    dibujar('/personal/ingresar', <IngresoPersonal />, '/personal/ingresar');
    expect(screen.queryByRole('button', { name: /google/i })).toBeNull();
    expect(screen.queryByText(/^o con tu correo$/i)).toBeNull();
    expect(screen.getByLabelText(/correo electrónico/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /entrar con rostro/i })).toBeInTheDocument();
  });

  it('va arriba del correo, con el separador, y el script solo se carga al pulsar', async () => {
    dibujar('/personal/ingresar', <IngresoPersonal />, '/personal/ingresar');
    const separador = screen.getByText(/^o con tu correo$/i);
    const correo = screen.getByLabelText(/correo electrónico/i);
    expect(botonGoogle().compareDocumentPosition(separador) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(separador.compareDocumentPosition(correo) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    // La contraseña y el rostro siguen disponibles.
    expect(screen.getByLabelText(/^contraseña/i)).toBeEnabled();
    expect(screen.getByRole('button', { name: /iniciar sesión/i })).toBeEnabled();
    expect(scriptDeGoogle()).toBeNull();
    expect(google.nonce).not.toHaveBeenCalled();

    fireEvent.click(botonGoogle());
    await waitFor(() => expect(scriptDeGoogle()).not.toBeNull());
    expect(scriptDeGoogle().getAttribute('src')).toBe(URL_SCRIPT_GOOGLE);
    const estado = await entregarScript();
    expect(await screen.findByText('Botón oficial de Google')).toBeInTheDocument();
    expect(estado.configuraciones[0]).toMatchObject({ client_id: ID_PRUEBA, nonce: 'nonce-personal-1' });
  });

  it('un empleado con Google vinculado abre su sesión de personal y va al panel; nada queda en el almacenamiento', async () => {
    google.ingresar.mockResolvedValue(sesionEmpleado('dueño'));
    dibujar('/personal/ingresar', <IngresoPersonal />, '/personal/ingresar');
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await entregarCredencial(estado, 'TOKEN-DE-GOOGLE-DEL-PERSONAL');

    expect(google.ingresar).toHaveBeenCalledWith('TOKEN-DE-GOOGLE-DEL-PERSONAL', 'nonce-personal-1');
    expect(await screen.findByText('Panel del personal')).toBeInTheDocument();
    expect(almacenSesion.obtener().usuario).toMatchObject({ tipo: 'empleado', rol: 'dueño', id: 4 });
    expect(almacenSesion.obtenerToken()).toBe('jwt-del-personal');
    for (const secreto of ['TOKEN-DE-GOOGLE-DEL-PERSONAL', 'nonce-personal-1', 'jwt-del-personal']) expect(almacenamientos()).not.toContain(secreto);
  });

  it('vuelve a la pantalla que pidió el personal (no siempre al inicio del panel)', async () => {
    google.ingresar.mockResolvedValue(sesionEmpleado());
    render(
      <ProveedorAvisos>
        <MemoryRouter initialEntries={[{ pathname: '/personal/ingresar', state: { desde: { pathname: '/panel/pedidos' } } }]}>
          <Routes>
            <Route path="/personal/ingresar" element={<IngresoPersonal />} />
            <Route path="/panel/pedidos" element={<Destino nombre="Pedidos del panel" />} />
            <Route path="/panel" element={<Destino nombre="Panel del personal" />} />
          </Routes>
        </MemoryRouter>
      </ProveedorAvisos>,
    );
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await entregarCredencial(estado);
    expect(await screen.findByText('Pedidos del panel')).toBeInTheDocument();
  });

  it('un correo de empleado sin vincular (409 cuenta_personal_existente) se explica con texto y se pide un nonce nuevo', async () => {
    google.ingresar.mockRejectedValue(error(409, 'cuenta_personal_existente', 'Este correo es de una cuenta del personal.'));
    dibujar('/personal/ingresar', <IngresoPersonal />, '/personal/ingresar');
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await entregarCredencial(estado);

    const aviso = await screen.findByText(/este correo es de una cuenta del personal/i);
    expect(aviso.closest('[role]')).toHaveTextContent(/vincula google desde mi perfil/i);
    expect(almacenSesion.obtener().usuario).toBeNull();
    await waitFor(() => expect(google.nonce).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(ultimoNonce(estado)).toBe('nonce-personal-2'));
    expect(screen.getByLabelText(/^contraseña/i)).toBeEnabled();
  });

  it('un Google que no es de nadie del personal NO lleva a crear una cuenta de cliente desde aquí: avisa y no deja el pase', async () => {
    google.ingresar.mockResolvedValue(pendiente());
    dibujar('/personal/ingresar', <IngresoPersonal />, '/personal/ingresar');
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await entregarCredencial(estado);

    expect(await screen.findByText(/no está vinculado a una cuenta del personal/i)).toBeInTheDocument();
    expect(screen.queryByText('Completa tu registro (destino)')).toBeNull();
    expect(paseGoogle.obtener()).toBeNull();
    expect(almacenSesion.obtener().usuario).toBeNull();
    await waitFor(() => expect(google.nonce).toHaveBeenCalledTimes(2));
  });

  it('un cliente cuyo Google está vinculado a su cuenta y entra por el acceso del personal va a su cuenta', async () => {
    google.ingresar.mockResolvedValue({ token: 'jwt-cliente', tipo: 'cliente', usuario: ROSA });
    dibujar('/personal/ingresar', <IngresoPersonal />, '/personal/ingresar');
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await entregarCredencial(estado);
    expect(await screen.findByText('Mi cuenta (cliente)')).toBeInTheDocument();
  });

  it('script bloqueado: aviso claro y la contraseña sigue disponible', async () => {
    dibujar('/personal/ingresar', <IngresoPersonal />, '/personal/ingresar');
    fireEvent.click(botonGoogle());
    await waitFor(() => expect(scriptDeGoogle()).not.toBeNull());
    scriptDeGoogle().dispatchEvent(new Event('error'));
    expect(await screen.findByText(/no pudimos cargar google/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /iniciar sesión/i })).toBeEnabled();
  });
});

describe('avisos que dicen dónde ingresar cuando se está en el acceso equivocado', () => {
  async function pulsarYFallar(entrada, elemento, ruta, falla) {
    google.ingresar.mockRejectedValue(falla);
    dibujar(entrada, elemento, ruta);
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await entregarCredencial(estado);
  }

  it('un correo de empleado sin vincular, en el acceso de clientes, manda al acceso del personal', async () => {
    await pulsarYFallar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente', error(409, 'cuenta_personal_existente', 'Este correo es de una cuenta del personal.'));
    const aviso = (await screen.findByText(/este correo es de una cuenta del personal/i)).closest('[role]');
    expect(within(aviso).getByRole('link', { name: /acceso del personal/i })).toHaveAttribute('href', '/personal/ingresar');
  });

  it('un correo de cliente, en el acceso del personal, manda a la página de clientes', async () => {
    await pulsarYFallar('/personal/ingresar', <IngresoPersonal />, '/personal/ingresar', error(409, 'cuenta_existente', 'Ya tienes una cuenta con este correo.'));
    const enlace = await screen.findByRole('link', { name: /página de clientes/i });
    expect(enlace).toHaveAttribute('href', '/ingresar/cliente');
    expect(screen.getByText(/ya tienes una cuenta de cliente con este correo/i)).toBeInTheDocument();
  });

  it('un Google sin cuenta del personal, en el acceso del personal, enlaza a la página de clientes', async () => {
    google.ingresar.mockResolvedValue(pendiente());
    dibujar('/personal/ingresar', <IngresoPersonal />, '/personal/ingresar');
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await entregarCredencial(estado);
    expect(await screen.findByRole('link', { name: /página de clientes/i })).toHaveAttribute('href', '/ingresar/cliente');
  });

  it('en su propio acceso los avisos no cambian (cliente en clientes)', async () => {
    await pulsarYFallar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente', error(409, 'cuenta_existente', 'Ya tienes una cuenta con este correo.'));
    expect(await screen.findByText(/vincula google desde mi cuenta/i)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /página de clientes/i })).toBeNull();
  });
});

describe('/ingresar/cliente: un Google vinculado a un empleado', () => {
  it('abre la sesión de personal y va al panel, no a Mi cuenta', async () => {
    google.ingresar.mockResolvedValue(sesionEmpleado('administrador'));
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await entregarCredencial(estado);

    expect(await screen.findByText('Panel del personal')).toBeInTheDocument();
    expect(almacenSesion.obtener().usuario).toMatchObject({ tipo: 'empleado', rol: 'administrador' });
    expect(screen.queryByText('Mi cuenta (cliente)')).toBeNull();
  });
});

describe('Mi perfil → «Ingreso con Google»', () => {
  const seccion = () => screen.getByRole('region', { name: 'Ingreso con Google' });

  async function abrir(rol = 'operario', estado = { vinculado: false, correo: null }) {
    google.perfil.estado.mockResolvedValue(estado);
    almacenSesion.aplicar(sesionEmpleado(rol));
    render(
      <ProveedorAvisos>
        <MemoryRouter><Perfil /></MemoryRouter>
      </ProveedorAvisos>,
    );
    // La región existe también mientras carga: se espera a que aparezca la acción de verdad.
    await screen.findByRole('button', { name: /^(vincular|desvincular) google$/i });
  }

  it.each(['dueño', 'administrador', 'diseñador', 'operario'])('el rol %s ve la sección y puede vincular (sin explicación de «sin contraseña»)', async (rol) => {
    await abrir(rol);
    expect(within(seccion()).getByRole('button', { name: 'Vincular Google' })).toBeInTheDocument();
    expect(within(seccion()).getByText(/google no está vinculado/i)).toBeInTheDocument();
    expect(within(seccion()).queryByRole('link', { name: /crear una contraseña/i })).toBeNull();
    expect(scriptDeGoogle()).toBeNull();
  });

  it('sin ID de cliente no se muestra la sección ni se consulta nada', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', '');
    almacenSesion.aplicar(sesionEmpleado('operario'));
    render(<ProveedorAvisos><MemoryRouter><Perfil /></MemoryRouter></ProveedorAvisos>);
    await screen.findByRole('heading', { name: 'Mi perfil' });
    expect(screen.queryByRole('region', { name: 'Ingreso con Google' })).toBeNull();
    expect(google.perfil.estado).not.toHaveBeenCalled();
  });

  it('pide la contraseña antes de cargar Google', async () => {
    await abrir();
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Vincular Google' }));
    expect(await within(seccion()).findByText(/escribe tu contraseña actual/i)).toBeInTheDocument();
    expect(within(seccion()).getByLabelText(/contraseña actual/i)).toHaveFocus();
    expect(scriptDeGoogle()).toBeNull();
    expect(google.nonce).not.toHaveBeenCalled();
  });

  it('vincula: manda token, nonce y contraseña a /mi-perfil/google y muestra el estado vinculado', async () => {
    google.perfil.vincular.mockResolvedValue({ vinculado: true, correo: EMPLEADO.email });
    await abrir('diseñador');
    fireEvent.change(within(seccion()).getByLabelText(/contraseña actual/i), { target: { value: 'clave-de-prueba' } });
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Vincular Google' }));
    const estado = await entregarScript();
    await within(seccion()).findByText('Botón oficial de Google');
    await entregarCredencial(estado, 'TOKEN-DE-GOOGLE-DEL-PERSONAL');

    expect(google.perfil.vincular).toHaveBeenCalledWith({ idToken: 'TOKEN-DE-GOOGLE-DEL-PERSONAL', nonce: 'nonce-personal-1', passwordActual: 'clave-de-prueba' });
    expect(google.vincular).not.toHaveBeenCalled();
    expect(await within(seccion()).findByText(/vinculamos tu cuenta de google/i)).toBeInTheDocument();
    expect(within(seccion()).getByRole('button', { name: 'Desvincular Google' })).toBeInTheDocument();
    for (const secreto of ['TOKEN-DE-GOOGLE-DEL-PERSONAL', 'nonce-personal-1', 'clave-de-prueba']) expect(almacenamientos()).not.toContain(secreto);
  });

  it('contraseña incorrecta: el error va junto al campo y se pide un nonce nuevo', async () => {
    google.perfil.vincular.mockRejectedValue(error(400, 'datos_invalidos', 'Revisa los datos.', { errores: { passwordActual: ['La contraseña actual no es correcta.'] } }));
    await abrir();
    fireEvent.change(within(seccion()).getByLabelText(/contraseña actual/i), { target: { value: 'mala' } });
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Vincular Google' }));
    const estado = await entregarScript();
    await within(seccion()).findByText('Botón oficial de Google');
    await entregarCredencial(estado);

    expect(await within(seccion()).findByText('La contraseña actual no es correcta.')).toBeInTheDocument();
    await waitFor(() => expect(ultimoNonce(estado)).toBe('nonce-personal-2'));
  });

  it.each([
    [error(409, 'google_correo_distinto', 'El correo de esa cuenta de Google no es el de tu cuenta.'), /no es el de tu cuenta/i],
    [error(409, 'google_en_uso', 'Esa cuenta de Google ya está vinculada a otra cuenta.'), /ya está vinculada a otra cuenta/i],
    [error(400, 'google_token_invalido', 'No pudimos verificar tu cuenta de Google.'), /no pudimos verificar tu cuenta de google/i],
  ])('un fallo al vincular (%#) se explica con texto', async (falla, texto) => {
    google.perfil.vincular.mockRejectedValue(falla);
    await abrir();
    fireEvent.change(within(seccion()).getByLabelText(/contraseña actual/i), { target: { value: 'clave-de-prueba' } });
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Vincular Google' }));
    const estado = await entregarScript();
    await within(seccion()).findByText('Botón oficial de Google');
    await entregarCredencial(estado);
    expect(await within(seccion()).findByText(texto)).toBeInTheDocument();
  });

  it('con Google vinculado se desvincula con la contraseña actual (siempre se puede)', async () => {
    google.perfil.desvincular.mockResolvedValue({ token: 'jwt-nuevo-del-personal' });
    await abrir('dueño', { vinculado: true, correo: EMPLEADO.email });
    expect(within(seccion()).getByText(/tu cuenta tiene google vinculado/i)).toBeInTheDocument();
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Desvincular Google' }));
    expect(await within(seccion()).findByText(/escribe tu contraseña actual/i)).toBeInTheDocument();
    expect(google.perfil.desvincular).not.toHaveBeenCalled();

    fireEvent.change(within(seccion()).getByLabelText(/contraseña actual/i), { target: { value: 'clave-de-prueba' } });
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Desvincular Google' }));
    await waitFor(() => expect(google.perfil.desvincular).toHaveBeenCalledWith('clave-de-prueba'));
    expect(google.desvincular).not.toHaveBeenCalled();
    expect(await within(seccion()).findByText(/desvinculamos tu cuenta de google/i)).toBeInTheDocument();
    // La API cerró todas las sesiones y abrió una nueva en este equipo: su token reemplaza al anterior y se avisa.
    expect(almacenSesion.obtenerToken()).toBe('jwt-nuevo-del-personal');
    expect(within(seccion()).getByText(/cerramos tu sesión en los demás equipos/i)).toBeInTheDocument();
    expect(almacenamientos()).not.toContain('jwt-nuevo-del-personal');
    expect(within(seccion()).getByRole('button', { name: 'Vincular Google' })).toBeInTheDocument();
  });

  it('desvincular con la contraseña incorrecta marca el campo', async () => {
    google.perfil.desvincular.mockRejectedValue(error(400, 'datos_invalidos', 'Revisa los datos.', { errores: { passwordActual: ['La contraseña actual no es correcta.'] } }));
    await abrir('operario', { vinculado: true, correo: EMPLEADO.email });
    fireEvent.change(within(seccion()).getByLabelText(/contraseña actual/i), { target: { value: 'mala' } });
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Desvincular Google' }));
    expect(await within(seccion()).findByText('La contraseña actual no es correcta.')).toBeInTheDocument();
    await waitFor(() => expect(within(seccion()).getByLabelText(/contraseña actual/i)).toHaveFocus());
  });

  it('si no se pudo consultar el estado lo dice y deja reintentar', async () => {
    google.perfil.estado.mockRejectedValueOnce(error(0, 'sin_conexion', 'No pudimos conectar con el servidor.'));
    almacenSesion.aplicar(sesionEmpleado('operario'));
    render(<ProveedorAvisos><MemoryRouter><Perfil /></MemoryRouter></ProveedorAvisos>);
    expect(await screen.findByText(/no pudimos consultar tu ingreso con google/i)).toBeInTheDocument();
    google.perfil.estado.mockResolvedValue({ vinculado: false, correo: null });
    fireEvent.click(screen.getByRole('button', { name: /reintentar/i }));
    // Mientras reintenta, el botón sigue montado (no pierde el foco) y avisa con texto.
    expect(await screen.findByRole('button', { name: /^(vincular|desvincular) google$/i })).toBeInTheDocument();
    expect(screen.queryByText(/no pudimos consultar tu ingreso con google/i)).toBeNull();
  });

  it('la sección tiene su encabezado también cuando falla la consulta', async () => {
    google.perfil.estado.mockRejectedValue(error(0, 'sin_conexion', 'No pudimos conectar con el servidor.'));
    almacenSesion.aplicar(sesionEmpleado('operario'));
    render(<ProveedorAvisos><MemoryRouter><Perfil /></MemoryRouter></ProveedorAvisos>);
    expect(await screen.findByRole('heading', { name: 'Ingreso con Google' })).toBeInTheDocument();
    expect(await screen.findByText(/no pudimos consultar tu ingreso con google/i)).toBeInTheDocument();
  });
});
