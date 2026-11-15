import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { panel } from '../api/panel';
import { publico } from '../api/publico';
import { ProveedorAvisos } from '../avisos/Avisos';
import {
  aMinutos, equipoPorPersona, franjasParaApi, horaCorta, horarioDesdeApi, horarioParaApi, textoFranja, validarFranjas, validarHorario,
} from '../paginas/local/reglasLocal';
import EstadoLocal from '../paginas/panel/EstadoLocal';
import { almacenSesion } from '../sesion/sesion';

vi.mock('../api/authService', () => ({ COOKIE_CSRF: 'synctime_csrf', default: { renovar: vi.fn(), salir: vi.fn() } }));
vi.mock('../api/panel', () => ({
  panel: {
    actualizarCartel: vi.fn(), guardarHorario: vi.fn(), miDisponibilidad: vi.fn(), guardarMiDisponibilidad: vi.fn(), disponibilidadEquipo: vi.fn(),
  },
}));
vi.mock('../api/publico', () => ({ publico: { cartel: vi.fn(), horarios: vi.fn() } }));
vi.mock('../paginas/panel/CodigoQr', () => ({ CodigoQr: () => null, descargarPng: vi.fn(), descargarSvg: vi.fn() }));

const NOMBRES = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
const HORARIO = NOMBRES.map((nombreDia, i) => ({
  diaSemana: i + 1, nombreDia, abierto: i < 6, horaInicio: i < 6 ? '09:00:00' : null, horaFin: i < 5 ? '18:30:00' : i === 5 ? '14:00:00' : null,
}));
const franja = (clave, diaSemana, horaInicio, horaFin) => ({ clave, diaSemana, horaInicio, horaFin });

function entrarComo(rol) {
  act(() => almacenSesion.aplicar({ token: 'jwt', tipo: 'empleado', usuario: { idEmpleado: 2, nombre: 'Hugo', apellido: 'Vera', rol } }));
}

beforeEach(() => {
  vi.clearAllMocks();
  publico.cartel.mockResolvedValue({ abiertoEnLocal: true, mensajeEstado: 'Local abierto y atendiendo', tiempoEstimadoRegreso: null, fechaUltimaModificacion: '2026-11-14T09:00:00' });
  publico.horarios.mockResolvedValue(HORARIO);
  panel.miDisponibilidad.mockResolvedValue([{ diaSemana: 1, horaInicio: '09:00:00', horaFin: '13:00:00' }]);
  panel.disponibilidadEquipo.mockResolvedValue([{ idEmpleado: 2, nombre: 'Hugo Vera', franjas: [{ diaSemana: 1, horaInicio: '09:00:00', horaFin: '13:00:00' }] }]);
});
afterEach(() => act(() => almacenSesion.cerrar()));

describe('reglas del horario y la disponibilidad', () => {
  it('lee y escribe horas', () => {
    expect(horaCorta('9:05:00')).toBe('09:05');
    expect(aMinutos('18:30')).toBe(1110);
    expect(aMinutos('25:00')).toBeNull();
    expect(textoFranja('09:00:00', '13:30:00')).toBe('9:00 a 13:30');
  });

  it('un día abierto pide apertura y cierre, con el cierre después', () => {
    const dias = horarioDesdeApi(HORARIO);
    expect(validarHorario(dias)).toEqual({});
    const malo = dias.map((d) => (d.diaSemana === 2 ? { ...d, horaFin: '08:00' } : d.diaSemana === 7 ? { ...d, abierto: true } : d));
    expect(validarHorario(malo)).toEqual({
      '2.horaFin': 'El cierre debe ser después de la apertura.', '7.horaInicio': 'Indica la hora de apertura.', '7.horaFin': 'Indica la hora de cierre.',
    });
    expect(horarioParaApi(dias)[6]).toEqual({ diaSemana: 7, abierto: false, horaInicio: null, horaFin: null });
    expect(horarioParaApi(dias)[0]).toEqual({ diaSemana: 1, abierto: true, horaInicio: '09:00:00', horaFin: '18:30:00' });
  });

  it('las franjas: inicio antes del fin, sin cruces y dentro del horario; un día cerrado no admite franjas', () => {
    const horario = horarioDesdeApi(HORARIO);
    expect(validarFranjas([franja('a', 1, '09:00', '13:00'), franja('b', 1, '13:00', '18:30')], horario)).toEqual({});
    expect(validarFranjas([franja('a', 1, '13:00', '09:00')], horario)).toEqual({ 'a.horaFin': 'Debe terminar después de empezar.' });
    expect(validarFranjas([franja('a', 1, '09:00', '13:00'), franja('b', 1, '12:00', '15:00')], horario))
      .toMatchObject({ 'a.horaInicio': 'Se cruza con la franja de 12:00 a 15:00.', 'b.horaInicio': 'Se cruza con la franja de 09:00 a 13:00.' });
    expect(validarFranjas([franja('a', 6, '13:00', '15:00')], horario)).toEqual({ 'a.horaInicio': 'Debe quedar dentro del horario del taller (09:00 a 14:00).' });
    expect(validarFranjas([franja('a', 7, '10:00', '12:00')], horario)).toEqual({ 'a.horaInicio': 'Ese día el taller está cerrado.' });
    expect(validarFranjas([franja('a', 2, '', '12:00')], horario)).toEqual({ 'a.horaInicio': 'Indica desde qué hora.' });
    // Mismo horario en días distintos no es un cruce.
    expect(validarFranjas([franja('a', 1, '09:00', '13:00'), franja('b', 2, '09:00', '13:00')], horario)).toEqual({});
  });

  it('envía las franjas ordenadas con segundos y agrupa la disponibilidad del equipo por persona', () => {
    expect(franjasParaApi([franja('b', 2, '10:00', '11:00'), franja('a', 1, '15:00', '16:00'), franja('c', 1, '09:00', '10:00')])).toEqual([
      { diaSemana: 1, horaInicio: '09:00:00', horaFin: '10:00:00' },
      { diaSemana: 1, horaInicio: '15:00:00', horaFin: '16:00:00' },
      { diaSemana: 2, horaInicio: '10:00:00', horaFin: '11:00:00' },
    ]);
    expect(equipoPorPersona([
      { idEmpleado: 3, nombre: 'Zoe', diaSemana: 2, horaInicio: '10:00', horaFin: '12:00' },
      { idEmpleado: 2, nombre: 'Ana', diaSemana: 1, horaInicio: '09:00', horaFin: '11:00' },
      { idEmpleado: 3, nombre: 'Zoe', diaSemana: 1, horaInicio: '09:00', horaFin: '10:00' },
    ]).map((p) => [p.nombre, p.franjas.map((f) => f.diaSemana)])).toEqual([['Ana', [1]], ['Zoe', [1, 2]]]);
  });
});

describe('/panel/local (fase 9)', () => {
  it('un operario da vuelta el cartel, ve el horario sin editarlo y no ve la disponibilidad del equipo', async () => {
    entrarComo('operario');
    panel.actualizarCartel.mockResolvedValue(null);
    render(<ProveedorAvisos><EstadoLocal /></ProveedorAvisos>);
    fireEvent.change(await screen.findByRole('combobox', { name: 'Estado' }), { target: { value: 'cerrado' } });
    fireEvent.change(screen.getByRole('textbox', { name: 'Regreso estimado' }), { target: { value: '16:30' } });
    fireEvent.click(screen.getByRole('button', { name: 'Dar vuelta el cartel' }));
    await waitFor(() => expect(panel.actualizarCartel).toHaveBeenCalledWith({
      abiertoEnLocal: false, mensajeEstado: 'Salimos a una instalación en terreno', tiempoEstimadoRegreso: '16:30',
    }));
    expect(screen.getByText('Lo cambian el dueño o el administrador.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Guardar horario' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Disponibilidad del equipo' })).not.toBeInTheDocument();
    expect(panel.disponibilidadEquipo).not.toHaveBeenCalled();
    expect(screen.getByRole('textbox', { name: 'Mensaje para los clientes' })).toHaveAttribute('maxLength', '200');
  });

  it('«Mi disponibilidad»: agrega una franja, valida el cruce sin enviar y guarda al corregir', async () => {
    entrarComo('diseñador');
    panel.guardarMiDisponibilidad.mockResolvedValue(null);
    render(<ProveedorAvisos><EstadoLocal /></ProveedorAvisos>);
    fireEvent.click(await screen.findByRole('button', { name: 'Agregar franja el lunes' }));
    const desde = screen.getAllByLabelText('Desde');
    const hasta = screen.getAllByLabelText('Hasta');
    expect(desde).toHaveLength(2);
    expect(desde[1]).toHaveValue('13:00');
    fireEvent.change(desde[1], { target: { value: '12:00' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar mi disponibilidad' }));
    expect(await screen.findByText('Se cruza con la franja de 12:00 a 18:30.')).toBeInTheDocument();
    expect(panel.guardarMiDisponibilidad).not.toHaveBeenCalled();
    fireEvent.change(screen.getAllByLabelText('Desde')[1], { target: { value: '14:00' } });
    fireEvent.change(hasta[1], { target: { value: '17:00' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar mi disponibilidad' }));
    await waitFor(() => expect(panel.guardarMiDisponibilidad).toHaveBeenCalledWith([
      { diaSemana: 1, horaInicio: '09:00:00', horaFin: '13:00:00' }, { diaSemana: 1, horaInicio: '14:00:00', horaFin: '17:00:00' },
    ]));
    // El domingo está cerrado: no ofrece agregar.
    expect(screen.queryByRole('button', { name: 'Agregar franja el domingo' })).not.toBeInTheDocument();
  });

  it('corregir una franja solo limpia sus errores; quitar la de un día cerrado lleva el foco al día', async () => {
    entrarComo('operario');
    panel.miDisponibilidad.mockResolvedValue([
      { diaSemana: 1, horaInicio: '09:00:00', horaFin: '13:00:00' }, { diaSemana: 1, horaInicio: '12:00:00', horaFin: '15:00:00' },
      { diaSemana: 7, horaInicio: '10:00:00', horaFin: '12:00:00' },
    ]);
    render(<ProveedorAvisos><EstadoLocal /></ProveedorAvisos>);
    fireEvent.click(await screen.findByRole('button', { name: 'Guardar mi disponibilidad' }));
    expect(await screen.findByText('Se cruza con la franja de 12:00 a 15:00.')).toBeInTheDocument();
    expect(screen.getByText('Se cruza con la franja de 09:00 a 13:00.')).toBeInTheDocument();
    expect(screen.getByText('Ese día el taller está cerrado.')).toBeInTheDocument();
    // Se corrige la segunda franja del lunes: el error de la primera sigue a la vista hasta volver a guardar.
    fireEvent.change(screen.getAllByLabelText('Desde')[1], { target: { value: '13:00' } });
    expect(screen.queryByText('Se cruza con la franja de 09:00 a 13:00.')).not.toBeInTheDocument();
    expect(screen.getByText('Se cruza con la franja de 12:00 a 15:00.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Quitar la franja 1 del domingo' }));
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Domingo' })).toHaveFocus());
  });

  it('gestión edita el horario y ve la disponibilidad de todo el equipo', async () => {
    entrarComo('dueño');
    panel.guardarHorario.mockResolvedValue(HORARIO);
    render(<ProveedorAvisos><EstadoLocal /></ProveedorAvisos>);
    const equipo = await screen.findByRole('region', { name: 'Disponibilidad del equipo' });
    expect(await within(equipo).findByText('Hugo Vera')).toBeInTheDocument();
    const martes = screen.getByRole('group', { name: 'Martes' });
    fireEvent.change(within(martes).getByLabelText('Cierre'), { target: { value: '08:00' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar horario' }));
    expect(await within(martes).findByText('El cierre debe ser después de la apertura.')).toBeInTheDocument();
    expect(panel.guardarHorario).not.toHaveBeenCalled();
    fireEvent.change(within(martes).getByLabelText('Cierre'), { target: { value: '17:00' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar horario' }));
    await waitFor(() => expect(panel.guardarHorario).toHaveBeenCalledTimes(1));
    expect(panel.guardarHorario.mock.calls[0][0][1]).toEqual({ diaSemana: 2, abierto: true, horaInicio: '09:00:00', horaFin: '17:00:00' });
  });
});
