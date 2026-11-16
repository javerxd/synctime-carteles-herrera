import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ErrorApi } from '../api/cliente';
import { cuenta } from '../api/cuenta';
import { panel } from '../api/panel';
import { publico } from '../api/publico';
import { clientes, misSolicitudes } from '../api/solicitudes';
import { ProveedorAvisos } from '../avisos/Avisos';
import EstadoLocal from '../paginas/panel/EstadoLocal';
import NuevaSolicitud from '../paginas/solicitudes/NuevaSolicitud';
import { FichaCliente } from '../paginas/solicitudes/panel/Clientes';
import { almacenSesion } from '../sesion/sesion';

vi.mock('../api/authService', () => ({ COOKIE_CSRF: 'synctime_csrf', default: { renovar: vi.fn(), salir: vi.fn(), reenviarVerificacion: vi.fn() } }));
vi.mock('../api/panel', () => ({
  panel: {
    horario: vi.fn(), cartel: vi.fn(), actualizarCartel: vi.fn(), guardarHorario: vi.fn(), miDisponibilidad: vi.fn(), guardarMiDisponibilidad: vi.fn(),
    disponibilidadEquipo: vi.fn(),
  },
}));
vi.mock('../api/publico', () => ({ publico: { productos: vi.fn(), estadoLocal: vi.fn() } }));
vi.mock('../api/cuenta', () => ({ cuenta: { obtener: vi.fn() } }));
vi.mock('../api/solicitudes', () => ({
  misSolicitudes: { subirAdjunto: vi.fn(), quitarAdjunto: vi.fn(), enviar: vi.fn(), borradores: vi.fn() },
  adjuntos: { miniatura: vi.fn(), archivo: vi.fn() },
  solicitudes: {},
  clientes: { obtener: vi.fn(), actualizar: vi.fn() },
}));
vi.mock('../paginas/panel/CodigoQr', () => ({ CodigoQr: () => null, descargarPng: vi.fn(), descargarSvg: vi.fn() }));

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
const NOMBRES = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
const HORARIO = NOMBRES.map((nombreDia, i) => ({
  diaSemana: i + 1, nombreDia, abierto: i < 6, horaInicio: i < 6 ? '09:00:00' : null, horaFin: i < 5 ? '18:30:00' : i === 5 ? '14:00:00' : null,
}));
const CARTEL = { abiertoEnLocal: true, mensajeEstado: 'Local abierto y atendiendo', tiempoEstimadoRegreso: null, fechaUltimaModificacion: '2026-11-14T09:00:00', rowVersion: 'AAAAAAAACA1=' };
const version409 = () => new ErrorApi({ estado: 409, codigo: 'version_obsoleta', mensaje: 'Otra persona cambió este registro. Recarga para ver la última versión.' });
const escribir = (control, valor) => fireEvent.change(control, { target: { value: valor } });

function entrarComo(rol, tipo = 'empleado') {
  act(() => almacenSesion.aplicar({ token: 'jwt', tipo, usuario: { idEmpleado: 2, idCliente: 9, id: 9, nombre: 'Hugo', apellido: 'Vera', rol, correoVerificado: true } }));
}

beforeEach(() => {
  vi.clearAllMocks();
  panel.horario.mockResolvedValue({ dias: HORARIO, rowVersion: 'AAAAAAAAHO1=' });
  panel.cartel.mockResolvedValue(CARTEL);
  panel.miDisponibilidad.mockResolvedValue([]);
  panel.disponibilidadEquipo.mockResolvedValue([]);
});
afterEach(() => act(() => almacenSesion.cerrar()));

describe('horario y cartel con versión (If-Match y 409)', () => {
  it('guardar el horario envía la versión; un 409 conserva lo escrito y guarda sobre la versión actual', async () => {
    entrarComo('dueño');
    panel.guardarHorario.mockRejectedValueOnce(version409()).mockResolvedValueOnce(HORARIO);
    render(<ProveedorAvisos><EstadoLocal /></ProveedorAvisos>);
    const martes = await screen.findByRole('group', { name: 'Martes' });
    escribir(within(martes).getByLabelText('Cierre'), '17:00');
    panel.horario.mockResolvedValue({ dias: HORARIO, rowVersion: 'AAAAAAAAHO2=' });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar horario' }));
    await waitFor(() => expect(panel.guardarHorario).toHaveBeenCalledWith(expect.any(Array), 'AAAAAAAAHO1='));
    const sobre = await screen.findByRole('button', { name: 'Guardar sobre la versión actual' });
    expect(screen.getByText('Otra persona modificó el horario mientras lo editabas')).toBeInTheDocument();
    expect(within(martes).getByLabelText('Cierre')).toHaveValue('17:00');
    fireEvent.click(sobre);
    await waitFor(() => expect(panel.guardarHorario).toHaveBeenLastCalledWith(expect.any(Array), 'AAAAAAAAHO2='));
    expect(panel.guardarHorario.mock.calls[1][0][1]).toEqual({ diaSemana: 2, abierto: true, horaInicio: '09:00:00', horaFin: '17:00:00' });
  });

  it('«Descartar» recarga el horario actual', async () => {
    entrarComo('administrador');
    panel.guardarHorario.mockRejectedValueOnce(version409());
    render(<ProveedorAvisos><EstadoLocal /></ProveedorAvisos>);
    const martes = await screen.findByRole('group', { name: 'Martes' });
    escribir(within(martes).getByLabelText('Cierre'), '17:00');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar horario' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Descartar mis cambios y ver la versión actual' }));
    await waitFor(() => expect(within(screen.getByRole('group', { name: 'Martes' })).getByLabelText('Cierre')).toHaveValue('18:30'));
  });

  it('dar vuelta el cartel envía la versión; un 409 ofrece guardar sobre la versión actual', async () => {
    entrarComo('operario');
    panel.actualizarCartel.mockRejectedValueOnce(version409()).mockResolvedValueOnce({ ...CARTEL, abiertoEnLocal: false });
    render(<ProveedorAvisos><EstadoLocal /></ProveedorAvisos>);
    fireEvent.change(await screen.findByRole('combobox', { name: 'Estado' }), { target: { value: 'cerrado' } });
    panel.cartel.mockResolvedValue({ ...CARTEL, rowVersion: 'AAAAAAAACA2=' });
    fireEvent.click(screen.getByRole('button', { name: 'Dar vuelta el cartel' }));
    await waitFor(() => expect(panel.actualizarCartel).toHaveBeenCalledWith(expect.objectContaining({ abiertoEnLocal: false }), 'AAAAAAAACA1='));
    expect(await screen.findByText('Otra persona modificó el cartel mientras lo editabas')).toBeInTheDocument();
    fireEvent.click(await screen.findByRole('button', { name: 'Guardar sobre la versión actual' }));
    await waitFor(() => expect(panel.actualizarCartel).toHaveBeenLastCalledWith(expect.objectContaining({ abiertoEnLocal: false }), 'AAAAAAAACA2='));
    expect((await screen.findAllByText('El cartel dice Cerrado'))[0]).toBeInTheDocument();
  });
});

describe('ficha de clientes: guardar sobre la versión actual', () => {
  const ROSA = {
    idCliente: 9, nombre: 'Rosa', apellido: 'Pino', telefono: '912345678', rut: null, email: 'rosa@correo.test', direccion: null, activo: true,
    registrado: true, correoConfirmado: true, rowVersion: 'AAAAAAAACL1=',
  };

  it('un 409 conserva lo escrito, relee y guarda con la versión nueva', async () => {
    entrarComo('operario');
    clientes.obtener.mockResolvedValueOnce(ROSA).mockResolvedValue({ ...ROSA, direccion: 'Colón 455', rowVersion: 'AAAAAAAACL2=' });
    clientes.actualizar.mockRejectedValueOnce(version409()).mockResolvedValueOnce({ ...ROSA, nombre: 'Rosa María' });
    render(
      <ProveedorAvisos>
        <MemoryRouter initialEntries={['/panel/clientes/9']}>
          <Routes><Route path="/panel/clientes/:id" element={<FichaCliente />} /></Routes>
        </MemoryRouter>
      </ProveedorAvisos>,
    );
    const nombre = await screen.findByRole('textbox', { name: /^Nombre/ });
    escribir(nombre, 'Rosa María');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar datos' }));
    await waitFor(() => expect(clientes.actualizar).toHaveBeenCalledWith(9, expect.objectContaining({ nombre: 'Rosa María' }), 'AAAAAAAACL1='));
    const sobre = await screen.findByRole('button', { name: 'Guardar sobre la versión actual' });
    expect(screen.getByText('Otra persona modificó este cliente mientras lo editabas')).toBeInTheDocument();
    expect(screen.getByText(/Colón 455/)).toBeInTheDocument();
    expect(nombre).toHaveValue('Rosa María');
    fireEvent.click(sobre);
    await waitFor(() => expect(clientes.actualizar).toHaveBeenLastCalledWith(9, expect.objectContaining({ nombre: 'Rosa María' }), 'AAAAAAAACL2='));
  });
});

describe('solicitud del cliente: misma clave tras un corte de red', () => {
  it('el reintento reusa la clave del formulario abierto', async () => {
    entrarComo(undefined, 'cliente');
    publico.productos.mockResolvedValue([{ idProducto: 2, nombre: 'Letras corpóreas' }]);
    cuenta.obtener.mockResolvedValue({ idCliente: 9, correo: 'rosa@correo.test', correoVerificado: true });
    misSolicitudes.borradores.mockResolvedValue([]);
    misSolicitudes.enviar
      .mockRejectedValueOnce(new ErrorApi({ estado: 0, codigo: 'sin_conexion', mensaje: 'No pudimos conectar con el servidor.' }))
      .mockResolvedValueOnce({ idSolicitud: 123, numero: '#123', detalles: [], adjuntos: [], cliente: {}, fechaChile: '2026-11-15T10:00:00' });
    sessionStorage.clear();
    render(
      <ProveedorAvisos>
        <MemoryRouter initialEntries={['/cuenta/solicitud']}>
          <Routes><Route path="/cuenta/solicitud" element={<NuevaSolicitud />} /></Routes>
        </MemoryRouter>
      </ProveedorAvisos>,
    );
    const tipo = await screen.findByRole('combobox', { name: /Tipo de letrero/ });
    await waitFor(() => expect(within(tipo).getByRole('option', { name: 'Letras corpóreas' })).toBeInTheDocument());
    escribir(tipo, '2');
    escribir(screen.getByRole('textbox', { name: /^Cantidad/ }), '1');
    escribir(screen.getByRole('textbox', { name: /^Descripción/ }), 'Letras para la fachada del local');
    fireEvent.click(screen.getByRole('button', { name: 'Enviar solicitud' }));
    expect(await screen.findByText('No pudimos conectar con el servidor.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Enviar solicitud' }));
    await waitFor(() => expect(misSolicitudes.enviar).toHaveBeenCalledTimes(2));
    const [primera, segunda] = misSolicitudes.enviar.mock.calls.map((c) => c[1]?.clave);
    expect(primera).toMatch(UUID);
    expect(segunda).toBe(primera);
  });
});
