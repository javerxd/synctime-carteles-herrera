import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ErrorApi } from '../api/cliente';
import { avisosTaller } from '../api/avisosTaller';
import authService from '../api/authService';
import { ProveedorAvisos } from '../avisos/Avisos';
import ConfirmarDestino from '../paginas/avisosTaller/ConfirmarDestino';
import {
  CONSULTAS_MAXIMAS, debeSeguirConsultando, enlaceVencido, enmascararCorreo, errorCorreoAviso, filasPrueba, ordenarDestinos, puedeQuitar,
  reemplazoPendiente, textoEstadoPrueba,
} from '../paginas/avisosTaller/reglasAvisosTaller';
import { SeccionAvisosTaller } from '../paginas/avisosTaller/SeccionAvisosTaller';
import Perfil from '../paginas/panel/Perfil';
import { almacenSesion } from '../sesion/sesion';

vi.mock('../api/authService', () => ({
  COOKIE_CSRF: 'synctime_csrf',
  default: { estadoRostro: vi.fn(), renovar: vi.fn(), salir: vi.fn() },
}));
vi.mock('../api/avisosTaller', () => ({
  avisosTaller: {
    obtener: vi.fn(), agregar: vi.fn(), reenviar: vi.fn(), cancelar: vi.fn(), quitar: vi.fn(), confirmar: vi.fn(), probar: vi.fn(), estadoPrueba: vi.fn(),
  },
}));

const ACTIVO = {
  idDestinatario: 1, correo: 'contacto@gmail.com', estado: 'activo', reemplazaA: null,
  fechaSolicitudChile: '2026-11-01T10:00:00', fechaConfirmacionChile: '2026-11-01T10:05:00', venceChile: null,
};
const PENDIENTE = {
  idDestinatario: 2, correo: 'nuevo@taller.cl', estado: 'pendiente', reemplazaA: { idDestinatario: 1, correo: 'contacto@gmail.com' },
  fechaSolicitudChile: '2026-11-14T10:00:00', fechaConfirmacionChile: null, venceChile: '2099-11-15T10:00:00',
};
const conDestinos = (destinos, extra = {}) => ({ destinos, usaRespaldo: false, historial: [], ...extra });

function dibujarSeccion() {
  return render(<ProveedorAvisos><SeccionAvisosTaller /></ProveedorAvisos>);
}

function entrarComo(rol) {
  act(() => almacenSesion.aplicar({ token: 'jwt', tipo: 'empleado', usuario: { idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', email: 'marta@taller.cl', rol } }));
}

beforeEach(() => {
  vi.clearAllMocks();
  authService.estadoRostro.mockResolvedValue({ disponible: false });
});
afterEach(() => {
  act(() => almacenSesion.cerrar());
  vi.useRealTimers();
});

describe('reglas de los avisos del taller', () => {
  it('enmascara el correo: primera y última letra, y el dominio completo', () => {
    expect(enmascararCorreo('contacto@gmail.com')).toBe('c•••o@gmail.com');
    expect(enmascararCorreo('a@b.cl')).toBe('a•••@b.cl');
    expect(enmascararCorreo('')).toBe('');
    expect(enmascararCorreo('sin-arroba')).toBe('•••');
  });

  it('valida el correo con la regla de la fase 6', () => {
    expect(errorCorreoAviso('')).toMatch(/Escribe el correo/);
    expect(errorCorreoAviso('malo@')).toMatch(/correo válido/);
    expect(errorCorreoAviso(' bien@taller.cl ')).toBeNull();
  });

  it('el último activo no se quita; con dos, sí. Los activos van primero', () => {
    expect(puedeQuitar(ACTIVO, [ACTIVO, PENDIENTE])).toBe(false);
    const otro = { ...ACTIVO, idDestinatario: 3, correo: 'otro@taller.cl' };
    expect(puedeQuitar(ACTIVO, [ACTIVO, otro])).toBe(true);
    expect(puedeQuitar(PENDIENTE, [ACTIVO, otro, PENDIENTE])).toBe(false);
    expect(ordenarDestinos([PENDIENTE, ACTIVO]).map((d) => d.idDestinatario)).toEqual([1, 2]);
    expect(reemplazoPendiente(ACTIVO, [ACTIVO, PENDIENTE])).toBe(PENDIENTE);
  });

  it('sabe cuándo venció el enlace de un pendiente', () => {
    const ahora = new Date('2026-11-15T12:00:00').getTime();
    expect(enlaceVencido({ ...PENDIENTE, venceChile: '2026-11-15T10:00:00' }, ahora)).toBe(true);
    expect(enlaceVencido(PENDIENTE, ahora)).toBe(false);
    expect(enlaceVencido(ACTIVO, ahora)).toBe(false);
  });

  it('la prueba se consulta mientras algo siga en cola y nunca más de 60 segundos', () => {
    expect(CONSULTAS_MAXIMAS).toBe(30);
    expect(debeSeguirConsultando([], 0)).toBe(true);
    expect(debeSeguirConsultando([{ estado: 'pendiente' }, { estado: 'enviado' }], 3)).toBe(true);
    expect(debeSeguirConsultando([{ estado: 'enviado' }, { estado: 'fallido' }], 3)).toBe(false);
    expect(debeSeguirConsultando([{ estado: 'pendiente' }], 30)).toBe(false);
  });

  it('dice «Salió bien», «No salió: …» o, al tope, «Sigue en cola»', () => {
    expect(textoEstadoPrueba({ estado: 'enviado' }).texto).toBe('Salió bien');
    expect(textoEstadoPrueba({ estado: 'fallido', error: 'buzón lleno' }).texto).toBe('No salió: buzón lleno');
    expect(textoEstadoPrueba({ estado: 'pendiente' }).texto).toBe('Enviando…');
    expect(textoEstadoPrueba({ estado: 'pendiente' }, { terminado: true }).texto).toMatch(/^Sigue en cola/);
    expect(filasPrueba({ idsCorreos: [7, 8], destinatarios: ['a@x.cl', 'b@x.cl'] }, [{ idCorreo: 8, destinatario: 'b@x.cl', estado: 'enviado' }]))
      .toEqual([
        { idCorreo: 7, destinatario: 'a@x.cl', estado: 'pendiente', error: null },
        { idCorreo: 8, destinatario: 'b@x.cl', estado: 'enviado', error: null },
      ]);
  });
});

describe('«Avisos del taller» en Mi perfil', () => {
  it('se dibuja para el dueño y el administrador, como configuración del taller', async () => {
    avisosTaller.obtener.mockResolvedValue(conDestinos([ACTIVO]));
    entrarComo('administrador');
    render(<ProveedorAvisos><Perfil /></ProveedorAvisos>);
    const seccion = await screen.findByRole('region', { name: 'Avisos del taller' });
    expect(within(seccion).getByText('Configuración del taller')).toBeInTheDocument();
    expect(within(seccion).getByText(/No cambia tu correo de ingreso/)).toBeInTheDocument();
    expect(await within(seccion).findByText('contacto@gmail.com', { selector: 'span' })).toBeInTheDocument();
  });

  it.each(['diseñador', 'operario'])('para un %s no se dibuja ni se pide a la API', async (rol) => {
    entrarComo(rol);
    render(<ProveedorAvisos><Perfil /></ProveedorAvisos>);
    await screen.findByRole('heading', { name: 'Mi perfil' });
    expect(screen.queryByText('Avisos del taller')).not.toBeInTheDocument();
    expect(avisosTaller.obtener).not.toHaveBeenCalled();
  });

  it('con un solo activo, «Quitar» queda inactivo y explica que hay que reemplazarlo', async () => {
    avisosTaller.obtener.mockResolvedValue(conDestinos([ACTIVO]));
    dibujarSeccion();
    const quitar = await screen.findByRole('button', { name: /Quitar contacto@gmail.com/ });
    expect(quitar).toHaveAttribute('aria-disabled', 'true');
    expect(quitar).toHaveAccessibleDescription(/Debe quedar al menos un correo activo/);
    fireEvent.click(quitar);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(avisosTaller.quitar).not.toHaveBeenCalled();
  });

  it('reemplazar envía el enlace y el anterior sigue activo hasta que se confirme', async () => {
    avisosTaller.obtener.mockResolvedValueOnce(conDestinos([ACTIVO])).mockResolvedValue(conDestinos([ACTIVO, PENDIENTE]));
    avisosTaller.agregar.mockResolvedValue({});
    dibujarSeccion();
    fireEvent.click(await screen.findByRole('button', { name: /Reemplazar contacto@gmail.com/ }));
    const campo = screen.getByRole('textbox', { name: /Correo que reemplaza a contacto@gmail.com/ });
    expect(campo).toHaveFocus();
    fireEvent.change(campo, { target: { value: 'Nuevo@Taller.cl' } });
    fireEvent.click(screen.getByRole('button', { name: 'Enviar enlace de confirmación' }));
    // Sin la contraseña no se envía: el error queda junto a su campo.
    expect(await screen.findByText('Escribe tu contraseña para confirmar el cambio.')).toBeInTheDocument();
    expect(screen.getByLabelText(/Tu contraseña, para confirmar el cambio/)).toHaveFocus();
    expect(avisosTaller.agregar).not.toHaveBeenCalled();
    fireEvent.change(screen.getByLabelText(/Tu contraseña, para confirmar el cambio/), { target: { value: 'mi-clave' } });
    fireEvent.click(screen.getByRole('button', { name: 'Enviar enlace de confirmación' }));
    await waitFor(() => expect(avisosTaller.agregar).toHaveBeenCalledWith({ correo: 'nuevo@taller.cl', idReemplaza: 1, passwordActual: 'mi-clave' }));
    // El formulario se cierra y el foco vuelve a «Agregar correo».
    await waitFor(() => expect(screen.getByRole('button', { name: 'Agregar correo' })).toHaveFocus());

    const filaAnterior = (await screen.findByText(/Sigue recibiendo los avisos hasta que se confirme el cambio/)).closest('li');
    expect(within(filaAnterior).getByText('Activo')).toBeInTheDocument();
    const filaNueva = screen.getByText('nuevo@taller.cl', { selector: 'span' }).closest('li');
    expect(within(filaNueva).getByText('Pendiente de confirmar')).toBeInTheDocument();
    expect(within(filaNueva).getByText(/El enlace vence el/)).toBeInTheDocument();
    expect(within(filaNueva).getByRole('button', { name: /Reenviar enlace/ })).toBeInTheDocument();
    expect(within(filaNueva).getByRole('button', { name: /Cancelar cambio/ })).toBeInTheDocument();
    // Mientras hay un cambio pendiente, no se ofrece otro reemplazo.
    expect(within(filaAnterior).queryByRole('button', { name: /Reemplazar/ })).not.toBeInTheDocument();
  });

  it('un correo ya activo o pendiente (409 ya_existe) se explica junto al campo', async () => {
    avisosTaller.obtener.mockResolvedValue(conDestinos([ACTIVO]));
    avisosTaller.agregar.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'ya_existe', mensaje: 'Ese correo ya recibe los avisos o está por confirmar.' }));
    dibujarSeccion();
    fireEvent.click(await screen.findByRole('button', { name: 'Agregar correo' }));
    fireEvent.change(screen.getByRole('textbox', { name: /Correo nuevo/ }), { target: { value: 'contacto@gmail.com' } });
    fireEvent.change(screen.getByLabelText(/Tu contraseña, para confirmar el cambio/), { target: { value: 'mi-clave' } });
    fireEvent.click(screen.getByRole('button', { name: 'Enviar enlace de confirmación' }));
    expect(await screen.findByText('Ese correo ya recibe los avisos o está por confirmar.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /Correo nuevo/ })).toHaveAttribute('aria-invalid', 'true');
  });

  async function abrirAgregar() {
    fireEvent.click(await screen.findByRole('button', { name: 'Agregar correo' }));
    fireEvent.change(screen.getByRole('textbox', { name: /Correo nuevo/ }), { target: { value: 'otro@taller.cl' } });
    fireEvent.change(screen.getByLabelText(/Tu contraseña, para confirmar el cambio/), { target: { value: 'equivocada' } });
    fireEvent.click(screen.getByRole('button', { name: 'Enviar enlace de confirmación' }));
  }

  it('una contraseña equivocada se explica junto a su campo, que se vacía y recibe el foco', async () => {
    avisosTaller.obtener.mockResolvedValue(conDestinos([ACTIVO]));
    avisosTaller.agregar.mockRejectedValue(new ErrorApi({ estado: 400, codigo: 'datos_invalidos', mensaje: 'La contraseña actual no es correcta.' }));
    dibujarSeccion();
    await abrirAgregar();
    expect(await screen.findByText('La contraseña actual no es correcta.')).toBeInTheDocument();
    const password = screen.getByLabelText(/Tu contraseña, para confirmar el cambio/);
    expect(password).toHaveValue('');
    await waitFor(() => expect(password).toHaveFocus());
    expect(screen.getByRole('textbox', { name: /Correo nuevo/ })).toHaveValue('otro@taller.cl');
  });

  it('con 3 correos por confirmar (409 demasiados_pendientes) lo explica sin marcar el correo', async () => {
    avisosTaller.obtener.mockResolvedValue(conDestinos([ACTIVO]));
    avisosTaller.agregar.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'demasiados_pendientes', mensaje: 'Otra persona cambió este registro. Recarga para ver la última versión.' }));
    dibujarSeccion();
    await abrirAgregar();
    expect(await screen.findByText(/Ya hay 3 correos esperando confirmación/)).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /Correo nuevo/ })).not.toHaveAttribute('aria-invalid');
  });

  it('quitar un activo pide confirmar con la contraseña', async () => {
    const otro = { ...ACTIVO, idDestinatario: 3, correo: 'otro@taller.cl' };
    avisosTaller.obtener.mockResolvedValue(conDestinos([ACTIVO, otro]));
    avisosTaller.quitar.mockResolvedValue(undefined);
    dibujarSeccion();
    fireEvent.click(await screen.findByRole('button', { name: 'Quitar otro@taller.cl' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Quitar este correo' });
    expect(within(dialogo).getByRole('button', { name: 'Cancelar' })).toHaveFocus();
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Quitar correo' }));
    expect(await within(dialogo).findByText('Escribe tu contraseña para confirmar el cambio.')).toBeInTheDocument();
    expect(avisosTaller.quitar).not.toHaveBeenCalled();
    fireEvent.change(within(dialogo).getByLabelText(/Tu contraseña, para confirmar el cambio/), { target: { value: 'mi-clave' } });
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Quitar correo' }));
    await waitFor(() => expect(avisosTaller.quitar).toHaveBeenCalledWith(3, 'mi-clave'));
  });

  it('un 403 (sin_permiso o solo_gestion) dice que es solo de gestión', async () => {
    avisosTaller.obtener.mockResolvedValue(conDestinos([ACTIVO, PENDIENTE]));
    avisosTaller.reenviar.mockRejectedValue(new ErrorApi({ estado: 403, codigo: 'sin_permiso', mensaje: 'No tiene permiso para realizar esta acción.' }));
    dibujarSeccion();
    fireEvent.click(await screen.findByRole('button', { name: 'Reenviar enlace a nuevo@taller.cl' }));
    expect(await screen.findByText('Solo el dueño o el administrador pueden hacer este cambio.')).toBeInTheDocument();
  });

  it('con usaRespaldo avisa que hoy van al correo de la configuración del servidor', async () => {
    avisosTaller.obtener.mockResolvedValue(conDestinos([], { usaRespaldo: true }));
    dibujarSeccion();
    expect(await screen.findByText('Hoy los avisos van al correo de la configuración del servidor')).toBeInTheDocument();
    expect(screen.getByText(/Agrega uno aquí para administrarlo desde el panel/)).toBeInTheDocument();
  });

  it('el historial muestra los correos enmascarados y quién lo hizo', async () => {
    avisosTaller.obtener.mockResolvedValue(conDestinos([ACTIVO], {
      historial: [
        { fechaChile: '2026-11-14T10:00:00', accion: 'confirmar', correo: 'n•••o@taller.cl', empleado: null, detalle: null },
        { fechaChile: '2026-11-14T09:00:00', accion: 'reemplazar', correo: 'n•••o@taller.cl', empleado: 'Marta Fuentes', detalle: null },
      ],
    }));
    dibujarSeccion();
    expect(await screen.findByText('Desde el enlace del correo')).toBeInTheDocument();
    expect(screen.getByText('Marta Fuentes')).toBeInTheDocument();
    expect(screen.getAllByText('n•••o@taller.cl')).toHaveLength(2);
  });
});

describe('correo de prueba', () => {
  async function enviarPrueba() {
    avisosTaller.obtener.mockResolvedValue(conDestinos([ACTIVO]));
    vi.useFakeTimers();
    dibujarSeccion();
    await act(async () => { await vi.advanceTimersByTimeAsync(0); });
    fireEvent.click(screen.getByRole('button', { name: 'Enviar correo de prueba' }));
    await act(async () => { await vi.advanceTimersByTimeAsync(0); });
  }

  it('consulta cada 2 segundos y se detiene cuando todo salió', async () => {
    avisosTaller.probar.mockResolvedValue({ idsCorreos: [5], destinatarios: ['contacto@gmail.com'], usaRespaldo: false });
    avisosTaller.estadoPrueba
      .mockResolvedValueOnce([{ idCorreo: 5, destinatario: 'contacto@gmail.com', estado: 'pendiente', intentos: 0, error: null }])
      .mockResolvedValue([{ idCorreo: 5, destinatario: 'contacto@gmail.com', estado: 'enviado', intentos: 1, error: null }]);
    await enviarPrueba();
    expect(screen.getByText('Enviando…', { selector: 'span' })).toBeInTheDocument();
    await act(async () => { await vi.advanceTimersByTimeAsync(2000); });
    expect(avisosTaller.estadoPrueba).toHaveBeenCalledTimes(1);
    expect(avisosTaller.estadoPrueba).toHaveBeenCalledWith([5], expect.objectContaining({ senal: expect.any(AbortSignal) }));
    await act(async () => { await vi.advanceTimersByTimeAsync(2000); });
    expect(screen.getByText('Salió bien')).toBeInTheDocument();
    await act(async () => { await vi.advanceTimersByTimeAsync(10000); });
    expect(avisosTaller.estadoPrueba).toHaveBeenCalledTimes(2);
  });

  it('un fallo muestra el motivo del servidor de correo', async () => {
    avisosTaller.probar.mockResolvedValue({ idsCorreos: [6], destinatarios: ['contacto@gmail.com'], usaRespaldo: false });
    avisosTaller.estadoPrueba.mockResolvedValue([{ idCorreo: 6, destinatario: 'contacto@gmail.com', estado: 'fallido', intentos: 3, error: 'El buzón no existe' }]);
    await enviarPrueba();
    await act(async () => { await vi.advanceTimersByTimeAsync(2000); });
    expect(screen.getByText('No salió: El buzón no existe')).toBeInTheDocument();
  });

  it('a los 60 segundos deja de consultar y dice que sigue en cola', async () => {
    avisosTaller.probar.mockResolvedValue({ idsCorreos: [7], destinatarios: ['contacto@gmail.com'], usaRespaldo: false });
    avisosTaller.estadoPrueba.mockResolvedValue([{ idCorreo: 7, destinatario: 'contacto@gmail.com', estado: 'pendiente', intentos: 0, error: null }]);
    await enviarPrueba();
    await act(async () => { await vi.advanceTimersByTimeAsync(70000); });
    expect(avisosTaller.estadoPrueba).toHaveBeenCalledTimes(30);
    expect(screen.getByText(/^Sigue en cola/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Enviar correo de prueba' })).not.toHaveAttribute('aria-busy');
  });

  it('el límite de 3 pruebas en 10 minutos (429) se explica', async () => {
    avisosTaller.probar.mockRejectedValue(new ErrorApi({ estado: 429, codigo: 'http_429', mensaje: 'Hiciste demasiados intentos.' }));
    await enviarPrueba();
    expect(screen.getByText(/Ya enviaste 3 pruebas en 10 minutos/)).toBeInTheDocument();
    expect(avisosTaller.estadoPrueba).not.toHaveBeenCalled();
  });
});

describe('/avisos/confirmar', () => {
  function abrir(entrada) {
    return render(
      <MemoryRouter initialEntries={[entrada]}>
        <Routes><Route path="/avisos/confirmar" element={<ConfirmarDestino />} /></Routes>
      </MemoryRouter>,
    );
  }

  it('confirma con el token y muestra el correo enmascarado, sin dejar el token en la dirección', async () => {
    avisosTaller.confirmar.mockResolvedValue({ correo: 'n•••o@taller.cl' });
    abrir('/avisos/confirmar?token=abc123');
    expect(await screen.findByRole('heading', { name: 'Correo confirmado' })).toHaveFocus();
    expect(screen.getByText('n•••o@taller.cl')).toBeInTheDocument();
    expect(avisosTaller.confirmar).toHaveBeenCalledTimes(1);
    expect(avisosTaller.confirmar).toHaveBeenCalledWith('abc123');
    // El estado se anuncia en una región que existía desde el inicio.
    expect(screen.getByRole('status')).toHaveTextContent('Correo confirmado: n•••o@taller.cl recibe los avisos.');
  });

  it('mientras está abierta, la página no envía su dirección (con el token) como Referer', async () => {
    avisosTaller.confirmar.mockResolvedValue({ correo: 'n•••o@taller.cl' });
    const { unmount } = abrir('/avisos/confirmar?token=referer-1');
    await screen.findByRole('heading', { name: 'Correo confirmado' });
    expect(document.head.querySelector('meta[name="referrer"]')).toHaveAttribute('content', 'no-referrer');
    unmount();
    expect(document.head.querySelector('meta[name="referrer"]')).toBeNull();
  });

  it('un enlace inválido o vencido lo dice sin revelar el correo', async () => {
    avisosTaller.confirmar.mockRejectedValue(new ErrorApi({ estado: 400, codigo: 'http_400', mensaje: 'El enlace no es válido o ya venció. Pide uno nuevo desde el perfil' }));
    abrir('/avisos/confirmar?token=vencido-1');
    expect(await screen.findByRole('heading', { name: 'No pudimos confirmar el correo' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('El enlace no es válido o ya venció');
  });

  it('sin token no llama a la API', async () => {
    abrir('/avisos/confirmar');
    expect(await screen.findByRole('alert')).toHaveTextContent(/El enlace está incompleto/);
    expect(avisosTaller.confirmar).not.toHaveBeenCalled();
  });
});
