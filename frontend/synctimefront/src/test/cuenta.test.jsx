import { StrictMode } from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import authService from '../api/authService';
import { cuenta } from '../api/cuenta';
import { ErrorApi } from '../api/cliente';
import { publico } from '../api/publico';
import { ProveedorAvisos } from '../avisos/Avisos';
import IngresoCliente from '../paginas/cuenta/IngresoCliente';
import MiCuenta from '../paginas/cuenta/MiCuenta';
import Recuperar from '../paginas/cuenta/Recuperar';
import Registro from '../paginas/cuenta/Registro';
import Verificar from '../paginas/cuenta/Verificar';
import { validarDatos, validarRegistro } from '../paginas/cuenta/reglasCuenta';
import { almacenSesion } from '../sesion/sesion';
import { rutaInterna } from '../sesion/rutas';

vi.mock('../api/authService', () => ({
  COOKIE_CSRF: 'synctime_csrf',
  default: {
    loginCliente: vi.fn(),
    registrarCliente: vi.fn(),
    recuperar: vi.fn(),
    restablecer: vi.fn(),
    verificarCorreo: vi.fn(),
    reenviarVerificacion: vi.fn(),
    renovar: vi.fn(),
    salir: vi.fn().mockResolvedValue(undefined),
  },
}));

vi.mock('../api/cuenta', () => ({
  cuenta: { obtener: vi.fn(), actualizar: vi.fn(), cambiarCorreo: vi.fn(), cambiarPassword: vi.fn(), marketing: vi.fn() },
}));

vi.mock('../api/publico', () => ({ publico: { documentosLegales: vi.fn() } }));

const DOCUMENTOS = [
  { tipo: 'terminos', version: 't-2' },
  { tipo: 'privacidad', version: 'p-3' },
  { tipo: 'marketing', version: 'm-1' },
  { tipo: 'cookies', version: 'c-1' },
];
const ROSA = { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', telefono: '912345678', email: 'rosa@correo.test', correoVerificado: false };
const sesionCliente = (token = 'jwt-cliente') => ({ token, tipo: 'cliente', usuario: ROSA });
const MI_CUENTA = {
  idCliente: 9, nombre: 'Rosa', apellido: 'Pino', rut: null, telefono: '912345678', correo: 'rosa@correo.test',
  direccion: null, correoVerificado: false, marketing: false,
};

/** Muestra la ruta y el estado de navegación al que se llegó. */
function Destino({ nombre }) {
  const { state } = useLocation();
  return <p>{nombre}{state?.aviso ? `: ${state.aviso}` : ''}{state?.bienvenida ? ': bienvenida' : ''}</p>;
}

function dibujar(entrada, elemento, ruta) {
  return render(
    <ProveedorAvisos>
      <MemoryRouter initialEntries={[entrada]}>
        <Routes>
          <Route path={ruta} element={elemento} />
          <Route path="/cuenta" element={<Destino nombre="Mi cuenta" />} />
          <Route path="/ingresar" element={<Destino nombre="Ingreso de clientes" />} />
          <Route path="/personal/ingresar" element={<Destino nombre="Acceso del personal" />} />
        </Routes>
      </MemoryRouter>
    </ProveedorAvisos>,
  );
}

const escribir = (etiqueta, valor) => fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: valor } });

beforeEach(() => {
  vi.clearAllMocks();
  sessionStorage.clear();
  almacenSesion.cerrar();
  publico.documentosLegales.mockResolvedValue(DOCUMENTOS);
});

describe('reglas de la cuenta', () => {
  it('validan teléfono chileno, RUT opcional con dígito verificador y contraseña de 8 a 72 caracteres', () => {
    expect(validarDatos({ nombre: 'Rosa', apellido: 'Pino', telefono: '+56 9 1234 5678' })).toEqual({});
    expect(validarDatos({ nombre: 'Rosa', apellido: 'Pino', telefono: '12345' }).telefono).toMatch(/9 dígitos/);
    expect(validarDatos({ nombre: 'Rosa', apellido: 'Pino', telefono: '912345678', rut: '12.345.678-9' }).rut).toMatch(/RUT no es válido/);
    expect(validarDatos({ nombre: 'Rosa', apellido: 'Pino', telefono: '912345678', rut: '12.345.678-5' })).toEqual({});
    const base = { nombre: 'R', apellido: 'P', telefono: '912345678', correo: 'r@c.cl', aceptaTerminosYPrivacidad: true };
    expect(validarRegistro({ ...base, password: '1234567' }).password).toMatch(/entre 8 y 72/);
    expect(validarRegistro({ ...base, password: 'x'.repeat(73) }).password).toMatch(/entre 8 y 72/);
    expect(validarRegistro({ ...base, password: 'x'.repeat(72) })).toEqual({});
  });
});

describe('Ingreso del cliente', () => {
  it('ingresa con correo y contraseña y no ofrece entrar con rostro', async () => {
    authService.loginCliente.mockResolvedValue(sesionCliente());
    dibujar('/ingresar', <IngresoCliente />, '/ingresar');

    expect(screen.queryByRole('button', { name: /rostro/i })).toBeNull();
    expect(screen.getByRole('link', { name: '¿Olvidaste tu contraseña?' })).toHaveAttribute('href', '/cuenta/recuperar');
    expect(screen.getByRole('link', { name: /crear cuenta/i })).toHaveAttribute('href', '/cuenta/registro');
    expect(screen.getByRole('link', { name: /acceso del personal/i })).toHaveAttribute('href', '/personal/ingresar');

    escribir(/correo electrónico/i, 'rosa@correo.test');
    escribir(/^contraseña/i, 'clave-de-prueba');
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }));

    expect(await screen.findByText('Mi cuenta')).toBeInTheDocument();
    expect(authService.loginCliente).toHaveBeenCalledWith('rosa@correo.test', 'clave-de-prueba');
  });

  it('muestra el mensaje de la API cuando hay demasiados intentos', async () => {
    authService.loginCliente.mockRejectedValue(new ErrorApi({ estado: 429, mensaje: 'Demasiados intentos. Espera 5 minutos.' }));
    dibujar('/ingresar', <IngresoCliente />, '/ingresar');
    escribir(/correo electrónico/i, 'rosa@correo.test');
    escribir(/^contraseña/i, 'clave-de-prueba');
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Demasiados intentos. Espera 5 minutos.');
  });
});

describe('Registro', () => {
  function llenar() {
    escribir(/^nombre/i, 'Rosa');
    escribir(/^apellido/i, 'Pino');
    escribir(/^teléfono/i, '9 1234 5678');
    escribir(/^correo electrónico/i, 'rosa@correo.test');
    escribir(/^contraseña/i, 'una-clave-larga');
  }

  it('las dos casillas vienen desmarcadas y separadas, con enlaces a los términos y la privacidad', async () => {
    dibujar('/cuenta/registro', <Registro />, '/cuenta/registro');
    const terminos = screen.getByRole('checkbox', { name: /acepto los términos y condiciones y la política de privacidad/i });
    const marketing = screen.getByRole('checkbox', { name: /novedades y promociones/i });
    expect(terminos).not.toBeChecked();
    expect(marketing).not.toBeChecked();
    expect(screen.getByRole('link', { name: /términos y condiciones/i })).toHaveAttribute('href', '/terminos');
    expect(screen.getByRole('link', { name: /política de privacidad/i })).toHaveAttribute('href', '/privacidad');
    await waitFor(() => expect(publico.documentosLegales).toHaveBeenCalled());
  });

  it('sin aceptar los términos no envía: marca la casilla y lleva el foco a ella', async () => {
    dibujar('/cuenta/registro', <Registro />, '/cuenta/registro');
    await waitFor(() => expect(publico.documentosLegales).toHaveBeenCalled());
    llenar();
    fireEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));

    const terminos = screen.getByRole('checkbox', { name: /acepto los términos/i });
    expect(terminos).toHaveAttribute('aria-invalid', 'true');
    expect(terminos).toHaveFocus();
    expect(screen.getByText(/debes aceptar los términos y condiciones/i)).toBeInTheDocument();
    expect(authService.registrarCliente).not.toHaveBeenCalled();
  });

  it('valida al salir del campo y enfoca el primer error al enviar', async () => {
    dibujar('/cuenta/registro', <Registro />, '/cuenta/registro');
    const telefono = screen.getByLabelText(/^teléfono/i);
    fireEvent.change(telefono, { target: { value: '123' } });
    expect(screen.queryByText(/9 dígitos, por ejemplo 9 1234 5678\.$/)).toBeInTheDocument(); // solo la ayuda
    fireEvent.blur(telefono);
    expect(telefono).toHaveAttribute('aria-invalid', 'true');

    fireEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));
    expect(screen.getByLabelText(/^nombre/i)).toHaveFocus();
  });

  it('envía los datos con las versiones vigentes y lleva a Mi cuenta con la bienvenida', async () => {
    authService.registrarCliente.mockResolvedValue(sesionCliente());
    dibujar('/cuenta/registro', <Registro />, '/cuenta/registro');
    await waitFor(() => expect(publico.documentosLegales).toHaveBeenCalled());
    llenar();
    fireEvent.click(screen.getByRole('checkbox', { name: /acepto los términos/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));

    expect(await screen.findByText('Mi cuenta: bienvenida')).toBeInTheDocument();
    expect(authService.registrarCliente).toHaveBeenCalledWith({
      nombre: 'Rosa', apellido: 'Pino', telefono: '9 1234 5678', correo: 'rosa@correo.test', rut: null, direccion: null,
      password: 'una-clave-larga', aceptaTerminosYPrivacidad: true, aceptaMarketing: false,
      versionTerminos: 't-2', versionPrivacidad: 'p-3', versionMarketing: null,
    });
    expect(almacenSesion.obtener().usuario).toMatchObject({ tipo: 'cliente', nombre: 'Rosa' });
  });

  it('con novedades marcadas envía también la versión de marketing', async () => {
    authService.registrarCliente.mockResolvedValue(sesionCliente());
    dibujar('/cuenta/registro', <Registro />, '/cuenta/registro');
    await waitFor(() => expect(publico.documentosLegales).toHaveBeenCalled());
    llenar();
    fireEvent.click(screen.getByRole('checkbox', { name: /acepto los términos/i }));
    fireEvent.click(screen.getByRole('checkbox', { name: /novedades y promociones/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));
    await waitFor(() => expect(authService.registrarCliente).toHaveBeenCalled());
    expect(authService.registrarCliente.mock.calls[0][0]).toMatchObject({ aceptaMarketing: true, versionMarketing: 'm-1' });
  });

  it('muestra los errores de la API junto a su campo, y el 409 arriba del formulario', async () => {
    authService.registrarCliente
      .mockRejectedValueOnce(new ErrorApi({ estado: 400, mensaje: 'Revisa los datos del formulario.', errores: { correo: ['Ese correo no sirve.'] } }))
      .mockRejectedValueOnce(new ErrorApi({ estado: 409, mensaje: 'Ya existe una cuenta con ese correo. Ingresa o recupera tu contraseña.' }));
    dibujar('/cuenta/registro', <Registro />, '/cuenta/registro');
    await waitFor(() => expect(publico.documentosLegales).toHaveBeenCalled());
    llenar();
    fireEvent.click(screen.getByRole('checkbox', { name: /acepto los términos/i }));

    fireEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));
    expect(await screen.findByText('Ese correo no sirve.')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText(/^correo electrónico/i)).toHaveFocus());

    fireEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Ya existe una cuenta con ese correo');
  });
});

describe('Recuperar la contraseña', () => {
  it('en dos pasos: pide el código y luego cambia la contraseña; termina en el ingreso del cliente', async () => {
    authService.recuperar.mockResolvedValue({ mensaje: 'Si el correo tiene una cuenta, te enviamos un código.' });
    authService.restablecer.mockResolvedValue(undefined);
    dibujar('/cuenta/recuperar', <Recuperar />, '/cuenta/recuperar');

    escribir(/correo electrónico/i, 'rosa@correo.test');
    fireEvent.click(screen.getByRole('button', { name: 'Enviar código' }));

    expect(await screen.findByText('Si el correo tiene una cuenta, te enviamos un código.')).toBeInTheDocument();
    expect(authService.recuperar).toHaveBeenCalledWith('cliente', 'rosa@correo.test');
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Escribe el código' })).toHaveFocus(), { timeout: 3000 });

    escribir(/código de 6 dígitos/i, '12345');
    escribir(/^contraseña nueva/i, 'nueva-clave-1');
    escribir(/repite la contraseña/i, 'otra-clave-1');
    fireEvent.click(screen.getByRole('button', { name: 'Cambiar contraseña' }));
    await waitFor(() => expect(screen.getByLabelText(/código de 6 dígitos/i)).toHaveFocus(), { timeout: 3000 });
    expect(screen.getByText(/el código tiene 6 dígitos/i)).toBeInTheDocument();
    expect(screen.getByText(/las contraseñas no coinciden/i)).toBeInTheDocument();
    expect(authService.restablecer).not.toHaveBeenCalled();

    escribir(/código de 6 dígitos/i, '123456');
    escribir(/repite la contraseña/i, 'nueva-clave-1');
    fireEvent.click(screen.getByRole('button', { name: 'Cambiar contraseña' }));

    expect(await screen.findByText(/Ingreso de clientes: Tu contraseña se cambió/)).toBeInTheDocument();
    expect(authService.restablecer).toHaveBeenCalledWith({ tipo: 'cliente', correo: 'rosa@correo.test', codigo: '123456', password: 'nueva-clave-1' });
  });

  it('un código equivocado se muestra junto al campo del código', async () => {
    authService.recuperar.mockResolvedValue({ mensaje: 'Revisa tu correo.' });
    authService.restablecer.mockRejectedValue(new ErrorApi({
      estado: 400, mensaje: 'El código no es válido o ya venció. Pide uno nuevo.', errores: { codigo: ['El código no es válido o ya venció. Pide uno nuevo.'] },
    }));
    dibujar('/cuenta/recuperar', <Recuperar />, '/cuenta/recuperar');
    escribir(/correo electrónico/i, 'rosa@correo.test');
    fireEvent.click(screen.getByRole('button', { name: 'Enviar código' }));
    await screen.findByRole('heading', { name: 'Escribe el código' });

    escribir(/código de 6 dígitos/i, '000000');
    escribir(/^contraseña nueva/i, 'nueva-clave-1');
    escribir(/repite la contraseña/i, 'nueva-clave-1');
    fireEvent.click(screen.getByRole('button', { name: 'Cambiar contraseña' }));

    const codigo = screen.getByLabelText(/código de 6 dígitos/i);
    await waitFor(() => expect(codigo).toHaveAttribute('aria-invalid', 'true'));
    expect(screen.getByText('El código no es válido o ya venció. Pide uno nuevo.')).toBeInTheDocument();
    await waitFor(() => expect(codigo).toHaveFocus());
  });

  it('con ?tipo=personal pide el código del personal y vuelve a su acceso', async () => {
    authService.recuperar.mockResolvedValue({ mensaje: 'Revisa tu correo.' });
    authService.restablecer.mockResolvedValue(undefined);
    dibujar('/cuenta/recuperar?tipo=personal', <Recuperar />, '/cuenta/recuperar');
    escribir(/correo electrónico/i, 'dueno@letreria.test');
    fireEvent.click(screen.getByRole('button', { name: 'Enviar código' }));
    await screen.findByRole('heading', { name: 'Escribe el código' });
    expect(authService.recuperar).toHaveBeenCalledWith('personal', 'dueno@letreria.test');

    escribir(/código de 6 dígitos/i, '654321');
    escribir(/^contraseña nueva/i, 'nueva-clave-1');
    escribir(/repite la contraseña/i, 'nueva-clave-1');
    fireEvent.click(screen.getByRole('button', { name: 'Cambiar contraseña' }));
    expect(await screen.findByText(/Acceso del personal: Tu contraseña se cambió/)).toBeInTheDocument();
  });

  it('un 429 muestra el mensaje de la API', async () => {
    authService.recuperar.mockRejectedValue(new ErrorApi({ estado: 429, mensaje: 'Hiciste demasiados intentos. Espera un minuto.' }));
    dibujar('/cuenta/recuperar', <Recuperar />, '/cuenta/recuperar');
    escribir(/correo electrónico/i, 'rosa@correo.test');
    fireEvent.click(screen.getByRole('button', { name: 'Enviar código' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Hiciste demasiados intentos. Espera un minuto.');
  });
});

describe('Verificar el correo', () => {
  it('verifica una sola vez aunque StrictMode monte la pantalla dos veces, y con sesión ofrece ir a la cuenta', async () => {
    almacenSesion.aplicar(sesionCliente());
    authService.verificarCorreo.mockResolvedValue({ verificado: true });
    render(
      <StrictMode>
        <MemoryRouter initialEntries={['/cuenta/verificar?token=enlace-unico']}>
          <Routes><Route path="/cuenta/verificar" element={<Verificar />} /></Routes>
        </MemoryRouter>
      </StrictMode>,
    );

    expect(await screen.findByRole('heading', { name: 'Tu correo está confirmado' })).toBeInTheDocument();
    expect(authService.verificarCorreo).toHaveBeenCalledTimes(1);
    expect(authService.verificarCorreo).toHaveBeenCalledWith('enlace-unico');
    expect(screen.getByRole('link', { name: 'Ir a mi cuenta' })).toHaveAttribute('href', '/cuenta');
    expect(almacenSesion.obtener().usuario.correoVerificado).toBe(true);
  });

  it('si el enlace no sirve, lo explica y ofrece ingresar', async () => {
    authService.verificarCorreo.mockRejectedValue(new ErrorApi({ estado: 400, mensaje: 'El enlace no es válido o ya venció. Ingresa a tu cuenta y pide uno nuevo.' }));
    dibujar('/cuenta/verificar?token=vencido', <Verificar />, '/cuenta/verificar');
    expect(await screen.findByRole('heading', { name: 'No pudimos confirmar tu correo' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('El enlace no es válido o ya venció.');
    expect(screen.getByRole('link', { name: 'Ingresar' })).toHaveAttribute('href', '/ingresar');
  });

  it('sin token no llama a la API', () => {
    dibujar('/cuenta/verificar', <Verificar />, '/cuenta/verificar');
    expect(screen.getByRole('heading', { name: 'No pudimos confirmar tu correo' })).toBeInTheDocument();
    expect(authService.verificarCorreo).not.toHaveBeenCalled();
  });
});

describe('Mi cuenta', () => {
  beforeEach(() => {
    almacenSesion.aplicar(sesionCliente());
    cuenta.obtener.mockResolvedValue(MI_CUENTA);
  });

  it('avisa que el correo no está confirmado y permite reenviar el enlace', async () => {
    authService.reenviarVerificacion.mockResolvedValue({ mensaje: 'Si tu correo aún no está confirmado, te enviamos un enlace nuevo.' });
    dibujar('/cuenta', <MiCuenta />, '/cuenta');
    expect(await screen.findByText('Confirma tu correo')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Reenviar enlace' }));
    expect(await screen.findAllByText('Si tu correo aún no está confirmado, te enviamos un enlace nuevo.')).not.toHaveLength(0);
    expect(authService.reenviarVerificacion).toHaveBeenCalledTimes(1);
  });

  it('cambiar la contraseña guarda el nuevo token en memoria', async () => {
    cuenta.cambiarPassword.mockResolvedValue({ token: 'jwt-nuevo' });
    dibujar('/cuenta', <MiCuenta />, '/cuenta');
    const seccion = await screen.findByRole('region', { name: 'Contraseña' });
    const campos = seccion.querySelectorAll('input');
    fireEvent.change(campos[0], { target: { value: 'clave-actual' } });
    fireEvent.change(campos[1], { target: { value: 'clave-nueva-1' } });
    fireEvent.change(campos[2], { target: { value: 'clave-nueva-1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cambiar contraseña' }));

    expect(await screen.findByText(/cerramos tu sesión en los demás equipos/i)).toBeInTheDocument();
    expect(cuenta.cambiarPassword).toHaveBeenCalledWith({ passwordActual: 'clave-actual', passwordNueva: 'clave-nueva-1' });
    expect(almacenSesion.obtenerToken()).toBe('jwt-nuevo');
  });

  it('cambiar el correo pide la contraseña y muestra su error junto al campo', async () => {
    cuenta.cambiarCorreo.mockRejectedValue(new ErrorApi({ estado: 400, mensaje: 'x', errores: { passwordActual: ['La contraseña actual no es correcta.'] } }));
    dibujar('/cuenta', <MiCuenta />, '/cuenta');
    const seccion = await screen.findByRole('region', { name: 'Correo' });
    const [correo, password] = seccion.querySelectorAll('input');
    fireEvent.change(correo, { target: { value: 'nuevo@correo.test' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cambiar correo' }));
    expect(password).toHaveFocus();
    expect(cuenta.cambiarCorreo).not.toHaveBeenCalled();

    fireEvent.change(password, { target: { value: 'equivocada' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cambiar correo' }));
    expect(await screen.findByText('La contraseña actual no es correcta.')).toBeInTheDocument();
    expect(password).toHaveAttribute('aria-invalid', 'true');
  });

  it('la casilla de novedades viene como la dejó el cliente y guarda con la versión vigente', async () => {
    cuenta.marketing.mockResolvedValue(undefined);
    dibujar('/cuenta', <MiCuenta />, '/cuenta');
    const casilla = await screen.findByRole('checkbox', { name: /novedades y promociones/i });
    expect(casilla).not.toBeChecked();
    await waitFor(() => expect(publico.documentosLegales).toHaveBeenCalled());
    await new Promise((r) => { setTimeout(r, 0); });
    fireEvent.click(casilla);
    await waitFor(() => expect(cuenta.marketing).toHaveBeenCalledWith({ acepta: true, version: 'm-1' }));
    await waitFor(() => expect(casilla).toBeChecked());
  });
});

describe('correcciones de la revisión', () => {
  it('rutaInterna solo acepta rutas del sitio y excluye las de ingreso, registro y recuperación', () => {
    expect(rutaInterna('/panel/pedidos?x=1', '/cuenta')).toBe('/panel/pedidos?x=1');
    expect(rutaInterna('//malicioso.test', '/cuenta')).toBe('/cuenta');
    expect(rutaInterna('/\\malicioso.test', '/cuenta')).toBe('/cuenta');
    expect(rutaInterna('https://malicioso.test', '/cuenta')).toBe('/cuenta');
    expect(rutaInterna('/ingresar', '/cuenta')).toBe('/cuenta');
    expect(rutaInterna('/cuenta/registro', '/cuenta')).toBe('/cuenta');
    expect(rutaInterna('/cuenta/recuperar?tipo=personal', '/panel')).toBe('/panel');
    expect(rutaInterna(null, '/cuenta')).toBe('/cuenta');
  });

  it('el ingreso del cliente no vuelve a una ruta externa recibida en el estado', async () => {
    authService.loginCliente.mockResolvedValue(sesionCliente());
    render(
      <MemoryRouter initialEntries={[{ pathname: '/ingresar', state: { desde: { pathname: '//malicioso.test' } } }]}>
        <Routes>
          <Route path="/ingresar" element={<IngresoCliente />} />
          <Route path="/cuenta" element={<p>Mi cuenta</p>} />
          <Route path="*" element={<p>Otra ruta</p>} />
        </Routes>
      </MemoryRouter>,
    );
    escribir(/correo electrónico/i, 'rosa@correo.test');
    escribir(/^contraseña/i, 'clave-de-prueba');
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }));
    expect(await screen.findByText('Mi cuenta')).toBeInTheDocument();
  });

  it('tras un ingreso rechazado, el foco vuelve a la contraseña (no cae al cuerpo)', async () => {
    authService.loginCliente.mockRejectedValue(new ErrorApi({ estado: 401, mensaje: 'No' }));
    dibujar('/ingresar', <IngresoCliente />, '/ingresar');
    escribir(/correo electrónico/i, 'rosa@correo.test');
    escribir(/^contraseña/i, 'equivocada');
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }));
    await screen.findByRole('alert');
    await waitFor(() => expect(screen.getByLabelText(/^contraseña/i)).toHaveFocus());
  });

  it('registro: con novedades marcadas y sin versión de marketing, no envía y lo explica', async () => {
    publico.documentosLegales.mockResolvedValue(DOCUMENTOS.filter((d) => d.tipo !== 'marketing'));
    dibujar('/cuenta/registro', <Registro />, '/cuenta/registro');
    await waitFor(() => expect(publico.documentosLegales).toHaveBeenCalled());
    await new Promise((r) => { setTimeout(r, 0); });
    escribir(/^nombre/i, 'Rosa');
    escribir(/^apellido/i, 'Pino');
    escribir(/^teléfono/i, '912345678');
    escribir(/^correo electrónico/i, 'rosa@correo.test');
    escribir(/^contraseña/i, 'una-clave-larga');
    fireEvent.click(screen.getByRole('checkbox', { name: /acepto los términos/i }));
    fireEvent.click(screen.getByRole('checkbox', { name: /novedades y promociones/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/novedades y promociones/);
    expect(authService.registrarCliente).not.toHaveBeenCalled();
  });

  it('verificar: saca el token de la dirección y lleva el foco al título del resultado', async () => {
    authService.verificarCorreo.mockResolvedValue({ verificado: true });
    function Direccion() {
      const { pathname, search } = useLocation();
      return <p data-testid="direccion">{pathname + search}</p>;
    }
    render(
      <MemoryRouter initialEntries={['/cuenta/verificar?token=secreto']}>
        <Routes><Route path="/cuenta/verificar" element={<><Verificar /><Direccion /></>} /></Routes>
      </MemoryRouter>,
    );
    await waitFor(() => expect(screen.getByTestId('direccion')).toHaveTextContent(/^\/cuenta\/verificar$/));
    const titulo = await screen.findByRole('heading', { name: 'Tu correo está confirmado' });
    expect(authService.verificarCorreo).toHaveBeenCalledWith('secreto');
    await waitFor(() => expect(titulo).toHaveFocus());
  });

  it('verificar: con sesión del personal no marca nada en su usuario', async () => {
    almacenSesion.aplicar({ token: 't', tipo: 'empleado', usuario: { idEmpleado: 1, nombre: 'Marta', rol: 'dueño' } });
    authService.verificarCorreo.mockResolvedValue({ verificado: true });
    dibujar('/cuenta/verificar?token=otro-enlace', <Verificar />, '/cuenta/verificar');
    await screen.findByRole('heading', { name: 'Tu correo está confirmado' });
    expect(almacenSesion.obtener().usuario.correoVerificado).toBeUndefined();
  });

  it('recuperar: el paso 2 incluye el correo como usuario para el gestor de contraseñas, y el reenvío se anuncia y recibe el foco', async () => {
    authService.recuperar.mockResolvedValue({ mensaje: 'Revisa tu correo.' });
    const { container } = dibujar('/cuenta/recuperar', <Recuperar />, '/cuenta/recuperar');
    escribir(/correo electrónico/i, 'rosa@correo.test');
    fireEvent.click(screen.getByRole('button', { name: 'Enviar código' }));
    await screen.findByRole('heading', { name: 'Escribe el código' });
    const usuario = container.querySelector('input[autocomplete="username"]');
    expect(usuario).toHaveValue('rosa@correo.test');

    fireEvent.click(screen.getByRole('button', { name: 'Enviar otro código' }));
    const aviso = await screen.findByText('Te enviamos otro código');
    expect(authService.recuperar).toHaveBeenCalledTimes(2);
    await waitFor(() => expect(aviso.closest('[tabindex="-1"]')).toHaveFocus());
  });

  it('Mi cuenta: si cambiar la contraseña no trae token, no se muestra como fallo y la sesión sigue', async () => {
    almacenSesion.aplicar(sesionCliente('jwt-actual'));
    cuenta.obtener.mockResolvedValue(MI_CUENTA);
    cuenta.cambiarPassword.mockResolvedValue(null);
    dibujar('/cuenta', <MiCuenta />, '/cuenta');
    const seccion = await screen.findByRole('region', { name: 'Contraseña' });
    const campos = seccion.querySelectorAll('input');
    fireEvent.change(campos[0], { target: { value: 'clave-actual' } });
    fireEvent.change(campos[1], { target: { value: 'clave-nueva-1' } });
    fireEvent.change(campos[2], { target: { value: 'clave-nueva-1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cambiar contraseña' }));
    expect(await screen.findByText(/Cambiamos tu contraseña/)).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();
    expect(almacenSesion.obtenerToken()).toBe('jwt-actual');
  });

  it('Mi cuenta: el resultado de guardar recibe el foco (no cae al cuerpo)', async () => {
    almacenSesion.aplicar(sesionCliente());
    cuenta.obtener.mockResolvedValue(MI_CUENTA);
    cuenta.actualizar.mockResolvedValue(MI_CUENTA);
    dibujar('/cuenta', <MiCuenta />, '/cuenta');
    await screen.findByRole('region', { name: 'Tus datos' });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar datos' }));
    const exito = await screen.findByText('Guardamos tus datos.');
    await waitFor(() => expect(exito.closest('[tabindex="-1"]')).toHaveFocus());
  });
});
