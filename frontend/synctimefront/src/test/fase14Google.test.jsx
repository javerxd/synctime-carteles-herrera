import { StrictMode } from 'react';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import authService from '../api/authService';
import { ErrorApi } from '../api/cliente';
import { cuenta } from '../api/cuenta';
import { google } from '../api/google';
import { publico } from '../api/publico';
import { ProveedorAvisos } from '../avisos/Avisos';
import { reiniciarCargaGoogle } from '../google/cargarGoogle';
import { googleDisponible } from '../google/configuracion';
import { paseGoogle } from '../google/pase';
import CompletarGoogle from '../paginas/cuenta/CompletarGoogle';
import IngresoCliente from '../paginas/cuenta/IngresoCliente';
import MiCuenta from '../paginas/cuenta/MiCuenta';
import Registro from '../paginas/cuenta/Registro';
import { almacenSesion } from '../sesion/sesion';
import {
  ID_PRUEBA, URL_SCRIPT_GOOGLE, almacenamientos, bloquearScript, entregarCredencial, entregarScript, instalarGoogleFalso, limpiarGoogle,
  scriptDeGoogle, ultimoNonce,
} from './googleFalso';

vi.mock('../api/authService', () => ({
  COOKIE_CSRF: 'synctime_csrf',
  default: {
    loginCliente: vi.fn(), registrarCliente: vi.fn(), recuperar: vi.fn(), restablecer: vi.fn(), verificarCorreo: vi.fn(),
    reenviarVerificacion: vi.fn(), renovar: vi.fn(), salir: vi.fn().mockResolvedValue(undefined),
  },
}));
vi.mock('../api/google', () => ({
  google: { nonce: vi.fn(), ingresar: vi.fn(), completar: vi.fn(), vincular: vi.fn(), desvincular: vi.fn() },
}));
vi.mock('../api/cuenta', () => ({
  cuenta: {
    obtener: vi.fn(), actualizar: vi.fn(), cambiarCorreo: vi.fn(), cambiarPassword: vi.fn(), marketing: vi.fn(), preferencias: vi.fn(),
    descargarDatos: vi.fn(), darDeBaja: vi.fn(),
  },
}));
vi.mock('../api/publico', () => ({ publico: { documentosLegales: vi.fn() } }));

const DOCUMENTOS = [
  { tipo: 'terminos', version: 't-2' }, { tipo: 'privacidad', version: 'p-3' }, { tipo: 'marketing', version: 'm-1' },
];
const ROSA = { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', telefono: '912345678', email: 'rosa@correo.test', correoVerificado: true };
const sesionCliente = () => ({ token: 'jwt-de-la-sesion', tipo: 'cliente', usuario: ROSA });
const MI_CUENTA = {
  idCliente: 9, nombre: 'Rosa', apellido: 'Pino', rut: null, telefono: '912345678', correo: 'rosa@correo.test',
  direccion: null, correoVerificado: true, marketing: false, avisosEstado: true, googleVinculado: false, tieneContrasena: true,
};
const PASE = 'PASE-SECRETO-DE-REGISTRO';
const pendiente = (extra = {}) => ({
  estado: 'registro_pendiente', pase: PASE, venceEn: new Date(Date.now() + 10 * 60_000).toISOString(),
  correo: 'nuevo@correo.test', nombre: 'Nora', apellido: '', ...extra,
});
const error = (estado, codigo, mensaje, extra = {}) => new ErrorApi({ estado, codigo, mensaje, ...extra });

/** Muestra dónde se llegó y con qué estado de navegación (un texto, para comprobar que no lleva secretos). */
function Destino({ nombre }) {
  const ubicacion = useLocation();
  const { state } = ubicacion;
  return (
    <div>
      <p>{nombre}{state?.aviso ? `: ${state.aviso}` : ''}{state?.bienvenida ? ': bienvenida' : ''}</p>
      <output data-testid="ubicacion">{JSON.stringify({ ruta: ubicacion.pathname, busqueda: ubicacion.search, hash: ubicacion.hash, state })}</output>
    </div>
  );
}

function dibujar(entrada, elemento, ruta) {
  return render(
    <ProveedorAvisos>
      <MemoryRouter initialEntries={[entrada]}>
        <Routes>
          <Route path={ruta} element={elemento} />
          <Route path="/cuenta" element={<Destino nombre="Mi cuenta (destino)" />} />
          <Route path="/ingresar/cliente" element={<Destino nombre="Ingreso de clientes" />} />
          <Route path="/cuenta/google/completar" element={<Destino nombre="Completa tu registro (destino)" />} />
          <Route path="/cuenta/recuperar" element={<Destino nombre="Recuperar" />} />
        </Routes>
      </MemoryRouter>
    </ProveedorAvisos>,
  );
}

const escribir = (etiqueta, valor) => fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: valor } });
const botonGoogle = () => screen.getByRole('button', { name: /continuar con google/i });
let contadorNonce;

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubEnv('VITE_GOOGLE_CLIENT_ID', ID_PRUEBA);
  sessionStorage.clear();
  localStorage.clear();
  almacenSesion.cerrar();
  paseGoogle.limpiar();
  limpiarGoogle();
  reiniciarCargaGoogle();
  contadorNonce = 0;
  google.nonce.mockImplementation(async () => ({ nonce: `nonce-de-prueba-${++contadorNonce}`, venceEn: new Date(Date.now() + 5 * 60_000).toISOString() }));
  publico.documentosLegales.mockResolvedValue(DOCUMENTOS);
  cuenta.obtener.mockResolvedValue(MI_CUENTA);
});

afterEach(() => {
  vi.unstubAllEnvs();
  limpiarGoogle();
});

describe('configuración', () => {
  it('Google está disponible solo si hay un ID de cliente', () => {
    expect(googleDisponible()).toBe(true);
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', '');
    expect(googleDisponible()).toBe(false);
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', '   ');
    expect(googleDisponible()).toBe(false);
  });
});

describe('/ingresar/cliente: el botón de Google', () => {
  it('sin ID de cliente no aparece el botón ni el separador, y el formulario sigue igual', () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', '');
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    expect(screen.queryByRole('button', { name: /google/i })).toBeNull();
    expect(screen.queryByText(/^o con tu correo$/i)).toBeNull();
    expect(screen.getByLabelText(/correo electrónico/i)).toBeInTheDocument();
    expect(scriptDeGoogle()).toBeNull();
  });

  it('va ARRIBA del correo, con el separador «o con tu correo» entre el botón y el campo', () => {
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    const boton = botonGoogle();
    const separador = screen.getByText(/^o con tu correo$/i);
    const correo = screen.getByLabelText(/correo electrónico/i);
    expect(boton.compareDocumentPosition(separador) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(separador.compareDocumentPosition(correo) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    // El separador es decorativo para quien usa un lector: el texto se lee, las rayas no son contenido.
    expect(separador.closest('[role="separator"]')).toBeNull();
  });

  it('el script de Google NO se carga al abrir la página: solo al pulsar el botón (dos clics)', async () => {
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    expect(scriptDeGoogle()).toBeNull();
    expect(document.querySelector('script[src*="google"]')).toBeNull();
    expect(google.nonce).not.toHaveBeenCalled();

    fireEvent.click(botonGoogle());
    await waitFor(() => expect(scriptDeGoogle()).not.toBeNull());
    expect(scriptDeGoogle().getAttribute('src')).toBe(URL_SCRIPT_GOOGLE);
    // El nonce se pide recién cuando el script ya llegó, y solo entonces se muestra el botón oficial.
    expect(google.nonce).not.toHaveBeenCalled();
    expect(screen.queryByText('Botón oficial de Google')).toBeNull();

    const estado = await entregarScript();
    expect(await screen.findByText('Botón oficial de Google')).toBeInTheDocument();
    expect(google.nonce).toHaveBeenCalledTimes(1);
    expect(estado.configuraciones).toHaveLength(1);
    expect(estado.configuraciones[0]).toMatchObject({ client_id: ID_PRUEBA, nonce: 'nonce-de-prueba-1', auto_select: false });
    expect(screen.getByText(/para elegir tu cuenta/i)).toBeInTheDocument();
    expect(document.querySelectorAll(`script[src="${URL_SCRIPT_GOOGLE}"]`)).toHaveLength(1);
  });

  it('camino 1: la identidad ya está vinculada y se abre la sesión como con la contraseña', async () => {
    google.ingresar.mockResolvedValue(sesionCliente());
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await entregarCredencial(estado, 'TOKEN-DE-GOOGLE-SECRETO');

    expect(google.ingresar).toHaveBeenCalledWith('TOKEN-DE-GOOGLE-SECRETO', 'nonce-de-prueba-1');
    expect(almacenSesion.obtener().usuario).toMatchObject({ tipo: 'cliente', id: 9 });
    expect(almacenSesion.obtenerToken()).toBe('jwt-de-la-sesion');
    expect(await screen.findByText('Mi cuenta (destino)')).toBeInTheDocument();
    // Ni el token de Google ni el nonce ni el de la sesión quedan en el almacenamiento del navegador.
    const guardado = almacenamientos();
    for (const secreto of ['TOKEN-DE-GOOGLE-SECRETO', 'nonce-de-prueba-1', 'jwt-de-la-sesion']) expect(guardado).not.toContain(secreto);
  });

  it('camino 2: el correo ya tiene cuenta (409 cuenta_existente): avisa que ingrese con su contraseña y pide un nonce nuevo', async () => {
    google.ingresar.mockRejectedValue(error(409, 'cuenta_existente', 'Ya tienes una cuenta con este correo.'));
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await entregarCredencial(estado);

    const aviso = await screen.findByText(/ya tienes una cuenta con este correo/i);
    expect(aviso.closest('[role]')).toHaveTextContent(/ingresa con tu contraseña y vincula google desde mi cuenta/i);
    expect(almacenSesion.obtener().usuario).toBeNull();
    // El nonce se gasta siempre: antes de reintentar se pide otro y se le da a Google.
    await waitFor(() => expect(google.nonce).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(ultimoNonce(estado)).toBe('nonce-de-prueba-2'));
    expect(estado.configuraciones).toHaveLength(2);
    expect(await screen.findByText('Botón oficial de Google')).toBeInTheDocument();
    // La contraseña sigue disponible.
    expect(screen.getByLabelText(/^contraseña/i)).toBeEnabled();
  });

  it('camino 3: correo nuevo (registro_pendiente) lleva a «Completa tu registro», sin sesión y sin el pase en la URL ni en el almacenamiento', async () => {
    google.ingresar.mockResolvedValue(pendiente());
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await entregarCredencial(estado);

    expect(await screen.findByText('Completa tu registro (destino)')).toBeInTheDocument();
    expect(almacenSesion.obtener().usuario).toBeNull();
    expect(paseGoogle.obtener()).toMatchObject({ pase: PASE, correo: 'nuevo@correo.test', nombre: 'Nora' });
    const ubicacion = screen.getByTestId('ubicacion').textContent;
    expect(JSON.parse(ubicacion).ruta).toBe('/cuenta/google/completar');
    for (const secreto of [PASE, 'TOKEN-DE-GOOGLE-SECRETO', 'nonce-de-prueba-1']) {
      expect(ubicacion).not.toContain(secreto);
      expect(almacenamientos()).not.toContain(secreto);
      expect(JSON.stringify(window.history.state ?? {})).not.toContain(secreto);
    }
  });

  it.each([
    [error(401, 'google_token_invalido', 'No pudimos verificar tu cuenta de Google.'), /no pudimos verificar tu cuenta de google/i],
    [error(503, 'google_no_disponible', 'Google no responde.'), /google no responde en este momento/i],
    [error(503, 'google_no_configurado', 'Sin configurar.'), /no está disponible por ahora/i],
    [error(409, 'sin_acceso_web', 'Pide en el local que activen tu acceso web.'), /pide en el local que activen tu acceso web/i],
    [error(429, 'demasiados_intentos', 'Hiciste demasiados intentos. Espera un minuto.'), /demasiados intentos/i],
    [error(0, 'sin_conexion', 'No pudimos conectar con el servidor.'), /no pudimos conectar con el servidor/i],
  ])('un fallo (%#) se explica con texto y se pide un nonce nuevo antes de reintentar', async (falla, texto) => {
    google.ingresar.mockRejectedValue(falla);
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await entregarCredencial(estado);

    expect(await screen.findByText(texto)).toBeInTheDocument();
    await waitFor(() => expect(google.nonce).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(ultimoNonce(estado)).toBe('nonce-de-prueba-2'));
    expect(almacenSesion.obtener().usuario).toBeNull();
    // La contraseña sigue disponible y el formulario no se bloqueó.
    expect(screen.getByRole('button', { name: 'Ingresar' })).toBeEnabled();
  });

  it('script bloqueado o sin red: un aviso claro, la contraseña sigue disponible y se puede reintentar', async () => {
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    fireEvent.click(botonGoogle());
    await bloquearScript();

    const aviso = await screen.findByText(/no pudimos cargar google/i);
    expect(aviso.closest('[role]')).toHaveTextContent(/correo y contraseña/i);
    expect(google.nonce).not.toHaveBeenCalled();
    expect(screen.getByLabelText(/correo electrónico/i)).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Ingresar' })).toBeEnabled();
    // Reintentar vuelve a pedir el script (el bloqueado no queda guardado como éxito).
    expect(scriptDeGoogle()).toBeNull();
    fireEvent.click(botonGoogle());
    await entregarScript();
    expect(await screen.findByText('Botón oficial de Google')).toBeInTheDocument();
  });

  it('un token que no llega (credential vacío) no se envía a la API', async () => {
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await act(async () => { await estado.callback({}); });
    expect(google.ingresar).not.toHaveBeenCalled();
  });

  it('el botón de Google mientras prepara avisa con texto y queda ocupado (no repite la acción)', async () => {
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    fireEvent.click(botonGoogle());
    const ocupado = await screen.findByRole('button', { name: /cargando google/i });
    expect(ocupado).toHaveAttribute('aria-busy', 'true');
    fireEvent.click(ocupado);
    expect(document.querySelectorAll(`script[src="${URL_SCRIPT_GOOGLE}"]`)).toHaveLength(1);
  });
});

describe('/cuenta/registro: el mismo botón arriba del formulario', () => {
  it('aparece arriba del nombre con el separador, y sin ID no aparece', () => {
    dibujar('/cuenta/registro', <Registro />, '/cuenta/registro');
    const boton = botonGoogle();
    const nombre = screen.getByLabelText(/^nombre/i);
    expect(boton.compareDocumentPosition(nombre) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(screen.getByText(/^o con tu correo$/i)).toBeInTheDocument();
    expect(scriptDeGoogle()).toBeNull();
  });

  it('sin ID de cliente el registro queda como antes', () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', '');
    dibujar('/cuenta/registro', <Registro />, '/cuenta/registro');
    expect(screen.queryByRole('button', { name: /google/i })).toBeNull();
    expect(screen.queryByText(/^o con tu correo$/i)).toBeNull();
  });

  it('registrarse con Google con un correo nuevo sigue el mismo camino hacia «Completa tu registro»', async () => {
    google.ingresar.mockResolvedValue(pendiente());
    dibujar('/cuenta/registro', <Registro />, '/cuenta/registro');
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    await entregarCredencial(estado);
    expect(await screen.findByText('Completa tu registro (destino)')).toBeInTheDocument();
  });
});

describe('/cuenta/google/completar: «Completa tu registro»', () => {
  const llenarValidos = () => {
    escribir(/^apellido/i, 'Vega');
    escribir(/^teléfono/i, '9 8765 4321');
  };

  it('sin pase en memoria (por ejemplo, tras recargar) vuelve al ingreso con un aviso', async () => {
    dibujar('/cuenta/google/completar', <CompletarGoogle />, '/cuenta/google/completar');
    expect(await screen.findByText(/Ingreso de clientes: /)).toHaveTextContent(/venció|vencid/i);
  });

  it('con el pase vencido vuelve al ingreso con un aviso y olvida el pase', async () => {
    paseGoogle.guardar(pendiente({ venceEn: new Date(Date.now() - 1000).toISOString() }));
    dibujar('/cuenta/google/completar', <CompletarGoogle />, '/cuenta/google/completar');
    expect(await screen.findByText(/Ingreso de clientes: /)).toHaveTextContent(/venció|vencid/i);
    expect(paseGoogle.obtener()).toBeNull();
  });

  it('muestra el correo y el nombre de Google, las casillas sin marcar con los textos de siempre y el teléfono obligatorio', async () => {
    paseGoogle.guardar(pendiente());
    dibujar('/cuenta/google/completar', <CompletarGoogle />, '/cuenta/google/completar');
    expect(await screen.findByRole('heading', { name: 'Completa tu registro' })).toBeInTheDocument();
    expect(screen.getByText('nuevo@correo.test')).toBeInTheDocument();
    expect(screen.getByLabelText(/^nombre/i)).toHaveValue('Nora');
    expect(screen.getByLabelText(/^apellido/i)).toHaveValue('');
    expect(screen.getByLabelText(/^teléfono/i)).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /leí y acepto los términos y condiciones y la política de privacidad/i })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: /quiero recibir novedades y promociones/i })).not.toBeChecked();
    expect(screen.getByRole('link', { name: /términos y condiciones/i })).toHaveAttribute('href', '/terminos');
    expect(screen.getByRole('link', { name: /política de privacidad/i })).toHaveAttribute('href', '/privacidad');
    // No hay contraseña: la cuenta nace con Google.
    expect(screen.queryByLabelText(/^contraseña/i)).toBeNull();
  });

  it('valida al enviar, no llama a la API y lleva el foco al primer error (apellido vacío)', async () => {
    paseGoogle.guardar(pendiente());
    dibujar('/cuenta/google/completar', <CompletarGoogle />, '/cuenta/google/completar');
    await screen.findByRole('heading', { name: 'Completa tu registro' });
    fireEvent.click(screen.getByRole('button', { name: 'Crear mi cuenta' }));

    expect(await screen.findByText('Escribe tu apellido.')).toBeInTheDocument();
    expect(screen.getByText(/teléfono de 9 dígitos/i)).toBeInTheDocument();
    expect(screen.getByText(/debes aceptar los términos y condiciones y la política de privacidad/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^apellido/i)).toHaveFocus();
    expect(google.completar).not.toHaveBeenCalled();
  });

  it('exige los términos aunque lo demás esté bien, y manda el foco a la casilla', async () => {
    paseGoogle.guardar(pendiente());
    dibujar('/cuenta/google/completar', <CompletarGoogle />, '/cuenta/google/completar');
    await screen.findByRole('heading', { name: 'Completa tu registro' });
    llenarValidos();
    fireEvent.click(screen.getByRole('button', { name: 'Crear mi cuenta' }));
    expect(await screen.findByRole('checkbox', { name: /leí y acepto/i })).toHaveFocus();
    expect(google.completar).not.toHaveBeenCalled();
  });

  it('crea la cuenta con el pase y las versiones vigentes, abre la sesión y la lleva a Mi cuenta con la bienvenida', async () => {
    paseGoogle.guardar(pendiente());
    google.completar.mockResolvedValue(sesionCliente());
    dibujar('/cuenta/google/completar', <CompletarGoogle />, '/cuenta/google/completar');
    await screen.findByRole('heading', { name: 'Completa tu registro' });
    llenarValidos();
    escribir(/^rut/i, '12.345.678-5');
    fireEvent.click(screen.getByRole('checkbox', { name: /leí y acepto/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Crear mi cuenta' }));

    expect(await screen.findByText('Mi cuenta (destino): bienvenida')).toBeInTheDocument();
    expect(google.completar).toHaveBeenCalledWith({
      pase: PASE, nombre: 'Nora', apellido: 'Vega', telefono: '9 8765 4321', rut: '12.345.678-5', direccion: null,
      aceptaTerminosYPrivacidad: true, aceptaMarketing: false, versionTerminos: 't-2', versionPrivacidad: 'p-3', versionMarketing: null,
    });
    expect(almacenSesion.obtener().usuario).toMatchObject({ tipo: 'cliente', id: 9 });
    expect(paseGoogle.obtener()).toBeNull();
    expect(almacenamientos()).not.toContain(PASE);
  });

  it('envía la versión del marketing solo si la persona marcó la casilla', async () => {
    paseGoogle.guardar(pendiente());
    google.completar.mockResolvedValue(sesionCliente());
    dibujar('/cuenta/google/completar', <CompletarGoogle />, '/cuenta/google/completar');
    await screen.findByRole('heading', { name: 'Completa tu registro' });
    llenarValidos();
    fireEvent.click(screen.getByRole('checkbox', { name: /leí y acepto/i }));
    fireEvent.click(screen.getByRole('checkbox', { name: /quiero recibir novedades/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Crear mi cuenta' }));
    await screen.findByText(/Mi cuenta \(destino\)/);
    expect(google.completar).toHaveBeenCalledWith(expect.objectContaining({ aceptaMarketing: true, versionMarketing: 'm-1' }));
  });

  it('un 400 por campo (teléfono ya conocido) se muestra junto al campo, enfoca el campo y el pase sigue sirviendo', async () => {
    paseGoogle.guardar(pendiente());
    google.completar.mockRejectedValue(error(400, 'datos_invalidos', 'Revisa los datos del formulario.', { errores: { telefono: ['Ya existe una cuenta con ese teléfono.'] } }));
    dibujar('/cuenta/google/completar', <CompletarGoogle />, '/cuenta/google/completar');
    await screen.findByRole('heading', { name: 'Completa tu registro' });
    llenarValidos();
    fireEvent.click(screen.getByRole('checkbox', { name: /leí y acepto/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Crear mi cuenta' }));

    expect(await screen.findByText('Ya existe una cuenta con ese teléfono.')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText(/^teléfono/i)).toHaveFocus());
    expect(paseGoogle.obtener()).toMatchObject({ pase: PASE });
    expect(almacenSesion.obtener().usuario).toBeNull();
  });

  it('un 409 (los textos legales cambiaron) se explica arriba y vuelve a pedir los documentos', async () => {
    paseGoogle.guardar(pendiente());
    google.completar.mockRejectedValue(error(409, 'conflicto', 'Los términos cambiaron. Revísalos y vuelve a enviar.'));
    dibujar('/cuenta/google/completar', <CompletarGoogle />, '/cuenta/google/completar');
    await screen.findByRole('heading', { name: 'Completa tu registro' });
    llenarValidos();
    fireEvent.click(screen.getByRole('checkbox', { name: /leí y acepto/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Crear mi cuenta' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Los términos cambiaron. Revísalos y vuelve a enviar.');
    await waitFor(() => expect(publico.documentosLegales).toHaveBeenCalledTimes(2));
  });

  it('pase vencido o ya usado (401 google_pase_invalido): olvida el pase y vuelve al ingreso con un aviso', async () => {
    paseGoogle.guardar(pendiente());
    google.completar.mockRejectedValue(error(401, 'google_pase_invalido', 'Tu pase venció.'));
    dibujar('/cuenta/google/completar', <CompletarGoogle />, '/cuenta/google/completar');
    await screen.findByRole('heading', { name: 'Completa tu registro' });
    llenarValidos();
    fireEvent.click(screen.getByRole('checkbox', { name: /leí y acepto/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Crear mi cuenta' }));
    expect(await screen.findByText(/Ingreso de clientes: /)).toHaveTextContent(/venció|vencid/i);
    expect(paseGoogle.obtener()).toBeNull();
  });
});

describe('Mi cuenta → «Ingreso con Google»', () => {
  const seccion = () => screen.getByRole('region', { name: 'Ingreso con Google' });

  async function abrir(datos = {}) {
    cuenta.obtener.mockResolvedValue({ ...MI_CUENTA, ...datos });
    almacenSesion.aplicar(sesionCliente());
    dibujar('/cuenta', <MiCuenta />, '/cuenta');
    await screen.findByRole('region', { name: 'Ingreso con Google' }).catch(() => null);
  }

  it('sin Google vinculado y con contraseña: explica y ofrece «Vincular Google»', async () => {
    await abrir();
    expect(within(seccion()).getByText(/no está vinculado|aún no vinculaste/i)).toBeInTheDocument();
    expect(within(seccion()).getByRole('button', { name: 'Vincular Google' })).toBeInTheDocument();
    expect(within(seccion()).getByLabelText(/contraseña actual/i)).toBeInTheDocument();
    expect(scriptDeGoogle()).toBeNull();
  });

  it('sin ID de cliente y sin Google vinculado la sección no se muestra', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', '');
    cuenta.obtener.mockResolvedValue(MI_CUENTA);
    almacenSesion.aplicar(sesionCliente());
    dibujar('/cuenta', <MiCuenta />, '/cuenta');
    await screen.findByRole('heading', { name: 'Mi cuenta' });
    await screen.findByRole('region', { name: 'Tus datos' });
    expect(screen.queryByRole('region', { name: 'Ingreso con Google' })).toBeNull();
  });

  it('pide la contraseña antes de cargar Google: vacía, marca el error, enfoca el campo y no carga el script', async () => {
    await abrir();
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Vincular Google' }));
    expect(await within(seccion()).findByText(/escribe tu contraseña actual/i)).toBeInTheDocument();
    expect(within(seccion()).getByLabelText(/contraseña actual/i)).toHaveFocus();
    expect(scriptDeGoogle()).toBeNull();
    expect(google.nonce).not.toHaveBeenCalled();
  });

  it('vincula: carga Google al pulsar, manda token, nonce y contraseña, y muestra el estado vinculado', async () => {
    google.vincular.mockResolvedValue({ vinculado: true, correo: 'rosa@correo.test' });
    await abrir();
    fireEvent.change(within(seccion()).getByLabelText(/contraseña actual/i), { target: { value: 'clave-de-prueba' } });
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Vincular Google' }));
    const estado = await entregarScript();
    await within(seccion()).findByText('Botón oficial de Google');
    await entregarCredencial(estado, 'TOKEN-DE-GOOGLE-SECRETO');

    expect(google.vincular).toHaveBeenCalledWith({ idToken: 'TOKEN-DE-GOOGLE-SECRETO', nonce: 'nonce-de-prueba-1', passwordActual: 'clave-de-prueba' });
    expect(await within(seccion()).findByText(/vinculamos tu cuenta de google/i)).toBeInTheDocument();
    expect(within(seccion()).getByRole('button', { name: 'Desvincular Google' })).toBeInTheDocument();
    expect(within(seccion()).queryByRole('button', { name: 'Vincular Google' })).toBeNull();
    for (const secreto of ['TOKEN-DE-GOOGLE-SECRETO', 'nonce-de-prueba-1', 'clave-de-prueba']) expect(almacenamientos()).not.toContain(secreto);
  });

  it('contraseña incorrecta (400 errores.passwordActual): el error va junto al campo y se pide un nonce nuevo', async () => {
    google.vincular.mockRejectedValue(error(400, 'datos_invalidos', 'Revisa los datos.', { errores: { passwordActual: ['La contraseña actual no es correcta.'] } }));
    await abrir();
    fireEvent.change(within(seccion()).getByLabelText(/contraseña actual/i), { target: { value: 'equivocada' } });
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Vincular Google' }));
    const estado = await entregarScript();
    await within(seccion()).findByText('Botón oficial de Google');
    await entregarCredencial(estado);

    expect(await within(seccion()).findByText('La contraseña actual no es correcta.')).toBeInTheDocument();
    await waitFor(() => expect(ultimoNonce(estado)).toBe('nonce-de-prueba-2'));
    expect(google.nonce).toHaveBeenCalledTimes(2);
  });

  it.each([
    [error(400, 'google_token_invalido', 'No pudimos verificar tu cuenta de Google.'), /no pudimos verificar tu cuenta de google/i],
    [error(409, 'google_correo_distinto', 'El correo de esa cuenta de Google no es el de tu cuenta.'), /no es el de tu cuenta/i],
    [error(409, 'google_en_uso', 'Esa cuenta de Google ya está vinculada a otra cuenta.'), /ya está vinculada a otra cuenta/i],
    [error(409, 'google_ya_vinculada', 'Tu cuenta ya tiene un Google vinculado.'), /ya tiene un google vinculado/i],
    [error(503, 'google_no_disponible', 'Google no responde.'), /google no responde en este momento/i],
  ])('un fallo al vincular (%#) se explica con texto', async (falla, texto) => {
    google.vincular.mockRejectedValue(falla);
    await abrir();
    fireEvent.change(within(seccion()).getByLabelText(/contraseña actual/i), { target: { value: 'clave-de-prueba' } });
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Vincular Google' }));
    const estado = await entregarScript();
    await within(seccion()).findByText('Botón oficial de Google');
    await entregarCredencial(estado);
    expect(await within(seccion()).findByText(texto)).toBeInTheDocument();
    await waitFor(() => expect(google.nonce).toHaveBeenCalledTimes(2));
  });

  it('con Google vinculado y contraseña: se desvincula con la contraseña actual', async () => {
    google.desvincular.mockResolvedValue(undefined);
    await abrir({ googleVinculado: true });
    expect(within(seccion()).getByText(/tu cuenta tiene google vinculado/i)).toBeInTheDocument();
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Desvincular Google' }));
    expect(await within(seccion()).findByText(/escribe tu contraseña actual/i)).toBeInTheDocument();
    expect(google.desvincular).not.toHaveBeenCalled();

    fireEvent.change(within(seccion()).getByLabelText(/contraseña actual/i), { target: { value: 'clave-de-prueba' } });
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Desvincular Google' }));
    await waitFor(() => expect(google.desvincular).toHaveBeenCalledWith('clave-de-prueba'));
    expect(await within(seccion()).findByText(/desvinculamos tu cuenta de google/i)).toBeInTheDocument();
    expect(within(seccion()).getByRole('button', { name: 'Vincular Google' })).toBeInTheDocument();
  });

  it('desvincular con la contraseña incorrecta marca el campo', async () => {
    google.desvincular.mockRejectedValue(error(400, 'datos_invalidos', 'Revisa los datos.', { errores: { passwordActual: ['La contraseña actual no es correcta.'] } }));
    await abrir({ googleVinculado: true });
    fireEvent.change(within(seccion()).getByLabelText(/contraseña actual/i), { target: { value: 'mala' } });
    fireEvent.click(within(seccion()).getByRole('button', { name: 'Desvincular Google' }));
    expect(await within(seccion()).findByText('La contraseña actual no es correcta.')).toBeInTheDocument();
    await waitFor(() => expect(within(seccion()).getByLabelText(/contraseña actual/i)).toHaveFocus());
  });

  it('una cuenta SIN contraseña lo explica, no ofrece desvincular y ofrece «Crear una contraseña» (la recuperación de siempre)', async () => {
    await abrir({ googleVinculado: true, tieneContrasena: false });
    expect(within(seccion()).getByText(/no tiene contraseña/i)).toBeInTheDocument();
    expect(within(seccion()).queryByRole('button', { name: 'Desvincular Google' })).toBeNull();
    expect(within(seccion()).queryByLabelText(/contraseña actual/i)).toBeNull();
    const enlace = within(seccion()).getByRole('link', { name: 'Crear una contraseña' });
    expect(enlace).toHaveAttribute('href', '/cuenta/recuperar');
    // Las acciones que piden la contraseña actual no se ofrecen como si fueran a funcionar.
    expect(screen.queryByRole('button', { name: 'Cambiar contraseña' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Cambiar correo' })).toBeNull();
  });

  it('una cuenta con contraseña sigue viendo los formularios de siempre', async () => {
    await abrir({ googleVinculado: true });
    expect(await screen.findByRole('button', { name: 'Cambiar contraseña' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cambiar correo' })).toBeInTheDocument();
  });
});

describe('robustez del botón (revisión de ECC)', () => {
  it('bajo StrictMode (como main.jsx) se agrega un solo script y se pide un solo nonce', async () => {
    render(
      <StrictMode>
        <ProveedorAvisos>
          <MemoryRouter initialEntries={['/ingresar/cliente']}>
            <Routes><Route path="/ingresar/cliente" element={<IngresoCliente />} /></Routes>
          </MemoryRouter>
        </ProveedorAvisos>
      </StrictMode>,
    );
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    expect(document.querySelectorAll(`script[src="${URL_SCRIPT_GOOGLE}"]`)).toHaveLength(1);
    expect(google.nonce).toHaveBeenCalledTimes(1);
    expect(estado.configuraciones).toHaveLength(1);
  });

  it('un callback repetido de Google con el mismo token se ignora (no gasta otro nonce ni muestra un error falso)', async () => {
    google.ingresar.mockRejectedValue(error(409, 'cuenta_existente', 'Ya existe.'));
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    fireEvent.click(botonGoogle());
    const estado = await entregarScript();
    await screen.findByText('Botón oficial de Google');
    const primerCallback = estado.callback;
    await act(async () => { await Promise.all([primerCallback({ credential: 'T1' }), primerCallback({ credential: 'T1' })]); });
    await screen.findByText(/ya tienes una cuenta con este correo/i);
    // Un callback tardío del dibujo anterior (su nonce ya se gastó) tampoco llega a la API.
    await act(async () => { await primerCallback({ credential: 'T1' }); });
    expect(google.ingresar).toHaveBeenCalledTimes(1);
    expect(google.nonce).toHaveBeenCalledTimes(2);
  });

  it('si Google no se puede inicializar, avisa y vuelve a empezar en vez de dejar un contenedor vacío', async () => {
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    fireEvent.click(botonGoogle());
    await waitFor(() => expect(scriptDeGoogle()).not.toBeNull());
    const estado = instalarGoogleFalso();
    window.google.accounts.id.renderButton.mockImplementation(() => { throw new Error('boom'); });
    await act(async () => { scriptDeGoogle().dispatchEvent(new Event('load')); });
    expect(await screen.findByText(/no pudimos continuar con google|boom/i)).toBeInTheDocument();
    expect(botonGoogle()).toBeInTheDocument();
    expect(estado.configuraciones.length).toBeGreaterThan(0);
  });

  it('un script que llega tarde (después del tiempo agotado) no se duplica al reintentar', async () => {
    dibujar('/ingresar/cliente', <IngresoCliente />, '/ingresar/cliente');
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      fireEvent.click(botonGoogle());
      await waitFor(() => expect(scriptDeGoogle()).not.toBeNull());
      await act(async () => { await vi.advanceTimersByTimeAsync(16_000); });
      expect(await screen.findByText(/no pudimos cargar google/i)).toBeInTheDocument();
    } finally {
      vi.useRealTimers();
    }
    // Reintenta: reutiliza el <script> que sigue descargándose.
    fireEvent.click(botonGoogle());
    await waitFor(() => expect(botonGoogle).toBeDefined());
    expect(document.querySelectorAll(`script[src="${URL_SCRIPT_GOOGLE}"]`)).toHaveLength(1);
    await entregarScript();
    expect(await screen.findByText('Botón oficial de Google')).toBeInTheDocument();
  });

  it('en Mi cuenta, si el campo se vacía después de que apareció el botón de Google, no se manda una contraseña vacía', async () => {
    cuenta.obtener.mockResolvedValue(MI_CUENTA);
    almacenSesion.aplicar(sesionCliente());
    dibujar('/cuenta', <MiCuenta />, '/cuenta');
    const seccion = await screen.findByRole('region', { name: 'Ingreso con Google' });
    fireEvent.change(within(seccion).getByLabelText(/contraseña actual/i), { target: { value: 'clave-de-prueba' } });
    fireEvent.click(within(seccion).getByRole('button', { name: 'Vincular Google' }));
    const estado = await entregarScript();
    await within(seccion).findByText('Botón oficial de Google');
    fireEvent.change(within(seccion).getByLabelText(/contraseña actual/i), { target: { value: '' } });
    await entregarCredencial(estado);
    expect(google.vincular).not.toHaveBeenCalled();
    expect(await within(seccion).findByText(/escribe tu contraseña actual/i)).toBeInTheDocument();
    await waitFor(() => expect(google.nonce).toHaveBeenCalledTimes(2));
  });

  it('cerrar la sesión olvida un pase de registro pendiente', () => {
    paseGoogle.guardar(pendiente());
    almacenSesion.cerrar('salida');
    expect(paseGoogle.obtener()).toBeNull();
  });
});

describe('el pase en memoria', () => {
  it('se guarda, se lee y se olvida; nunca toca el almacenamiento del navegador', () => {
    paseGoogle.guardar(pendiente());
    expect(paseGoogle.obtener()).toMatchObject({ pase: PASE });
    expect(almacenamientos()).not.toContain(PASE);
    paseGoogle.limpiar();
    expect(paseGoogle.obtener()).toBeNull();
  });
});

// Evita que una prueba deje cosas colgadas si falla antes de tiempo.
afterAll(() => {
  authService.salir.mockClear();
});
