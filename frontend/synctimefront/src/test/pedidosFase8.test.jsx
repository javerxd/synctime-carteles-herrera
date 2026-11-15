import { act, fireEvent, render, renderHook, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { ErrorApi } from '../api/cliente';
import { cotizaciones } from '../api/cotizaciones';
import { empleados, pedidos } from '../api/pedidos';
import { clientes } from '../api/solicitudes';
import { ProveedorAvisos } from '../avisos/Avisos';
import CalendarioEntregas from '../paginas/pedidos/CalendarioEntregas';
import CrearPedido from '../paginas/pedidos/CrearPedido';
import FichaPedido from '../paginas/pedidos/FichaPedido';
import ListaPedidos from '../paginas/pedidos/ListaPedidos';
import {
  agruparPorDia, construirMes, contarMes, estadoEntrega, mesDesdeTexto, moverMes, rangoCuadricula,
} from '../paginas/pedidos/reglasCalendario';
import {
  abonoParaApi, ABONO_VACIO, datosPedidoParaApi, DATOS_VACIOS, etiquetaTransicion, FILTROS_PEDIDOS, ordenarTransiciones,
  parametrosPedidos, validarAbono, validarDatosPedido,
} from '../paginas/pedidos/reglasPedido';
import { separarErrores } from '../paginas/pedidos/usePedidoNuevo';
import { almacenSesion } from '../sesion/sesion';
import { diasEntre, fechaLarga, hoyChile, sumarAnios } from '../utilidades/fechas';
import { filtrosDesde, parametrosInvalidos } from '../utilidades/filtrosUrl';
import { useConsulta } from '../utilidades/useConsulta';

vi.mock('../api/authService', () => ({
  COOKIE_CSRF: 'synctime_csrf',
  default: { reenviarVerificacion: vi.fn(), renovar: vi.fn(), salir: vi.fn() },
}));
vi.mock('../api/pedidos', () => ({
  pedidos: {
    listar: vi.fn(), obtener: vi.fn(), editar: vi.fn(), cambiarEstado: vi.fn(), registrarAbono: vi.fn(), anularAbono: vi.fn(),
    eliminar: vi.fn(), calendario: vi.fn(), alertas: vi.fn(), crearDesdeCotizacion: vi.fn(), crearDirecto: vi.fn(),
  },
  empleados: { listar: vi.fn() },
}));
vi.mock('../api/solicitudes', () => ({ clientes: { obtener: vi.fn() }, solicitudes: {}, misSolicitudes: {}, adjuntos: {} }));
vi.mock('../api/cotizaciones', () => ({ cotizaciones: { obtener: vi.fn() }, misCotizaciones: {}, materiales: { listar: vi.fn() } }));

const HOY = '2026-11-14';
const escribir = (control, valor) => fireEvent.change(control, { target: { value: valor } });

function Ruta() {
  const { pathname, search } = useLocation();
  return <p data-testid="ruta">{pathname}{search}</p>;
}

function dibujar(entrada, ruta, elemento) {
  return render(
    <ProveedorAvisos>
      <MemoryRouter initialEntries={[entrada]}>
        <Routes>
          <Route path={ruta} element={<>{elemento}<Ruta /></>} />
          <Route path="*" element={<Ruta />} />
        </Routes>
      </MemoryRouter>
    </ProveedorAvisos>,
  );
}

function entrarComo(rol) {
  act(() => almacenSesion.aplicar({ token: 'jwt', tipo: 'empleado', usuario: { idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', rol } }));
}

const PAGO = (id, monto, extra = {}) => ({
  idPago: id, monto, fechaPago: '2026-11-08', medioPago: 'transferencia', observacion: null, registradoPor: 'Marta Fuentes',
  fechaRegistroChile: '2026-11-08T10:05:00', anulado: false, motivoAnulacion: null, anuladoPor: null, fechaAnulacionChile: null, puedeAnular: true, ...extra,
});
const FICHA = {
  idPedido: 1, numero: '#1', origen: 'cotizacion', solicitud: { idSolicitud: 123, numero: '#123' }, cotizacion: { idCotizacion: 46, numero: '#46' },
  cliente: { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', telefono: '912345678' }, responsable: { idEmpleado: 2, nombre: 'Hugo Vera' },
  fechaPedido: '2026-11-08T10:00:00', fechaEntregaEstimada: '2026-11-16', fechaEntregaReal: null, modalidadEntrega: 'retiro_local',
  requiereInstalacion: false, direccionInstalacion: null, montoBaseTrabajo: 99065, costoInstalacionAdicional: 0, montoTotalFinal: 99065,
  estadoPedido: 'aceptado', estadoPago: 'abonado', montoAbonado: 40000, saldoPendiente: 59065, numeroFactura: null, cicloCerrado: false,
  motivoAnulacion: null, aFavorDelCliente: 0, transicionesPosibles: ['en_proceso', 'anulado'],
  historial: [{ fechaChile: '2026-11-08T10:00:00', estadoAnterior: null, estadoNuevo: 'aceptado', nota: null, empleado: 'Marta Fuentes' }],
  pagos: [PAGO(11, 40000), PAGO(10, 4000, { anulado: true, motivoAnulacion: 'Se ingresó dos veces', anuladoPor: 'Marta Fuentes', fechaAnulacionChile: '2026-11-08T10:01:00', puedeAnular: false })],
  rowVersion: 'AAAAAAAAPED=',
};

beforeEach(() => {
  vi.clearAllMocks();
  empleados.listar.mockResolvedValue([{ idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', rol: 'dueño', activo: true }]);
});
afterEach(() => act(() => almacenSesion.cerrar()));

describe('fechas de Chile', () => {
  it('«hoy» es la fecha de Chile aunque en UTC ya sea mañana', () => {
    expect(hoyChile(new Date('2026-11-15T02:30:00Z'))).toBe('2026-11-14');
    expect(hoyChile(new Date('2026-11-15T04:00:00Z'))).toBe('2026-11-15');
  });

  it('suma años y cuenta días sin saltos por el horario de verano', () => {
    expect(sumarAnios('2024-02-29', 2)).toBe('2026-02-28');
    expect(diasEntre('2026-09-05', '2026-09-07')).toBe(2);
    expect(fechaLarga('2026-11-14')).toBe('sábado 14 de noviembre');
  });
});

describe('reglas del pedido (fase 8)', () => {
  it('nombra cada transición: avanzar, volver un paso, entregar y anular; la que avanza va primero', () => {
    expect(etiquetaTransicion('aceptado', 'en_proceso')).toBe('Pasar a «En proceso»');
    expect(etiquetaTransicion('terminado', 'en_proceso')).toBe('Volver a «En proceso»');
    expect(etiquetaTransicion('terminado', 'entregado')).toBe('Registrar entrega');
    expect(etiquetaTransicion('en_proceso', 'anulado')).toBe('Anular pedido');
    expect(ordenarTransiciones('en_proceso', ['anulado', 'aceptado', 'terminado']).map((t) => t.tipo)).toEqual(['avanzar', 'volver', 'anular']);
    expect(ordenarTransiciones('entregado', [])).toEqual([]);
  });

  it('valida los datos E08: responsable, fecha desde hoy y hasta 2 años, modalidad, instalación y dirección', () => {
    const base = { ...DATOS_VACIOS, idEmpleadoResponsable: '1', fechaEntregaEstimada: '2026-11-20', modalidadEntrega: 'retiro_local' };
    expect(validarDatosPedido(base, { hoy: HOY })).toEqual({});
    expect(validarDatosPedido({ ...base, fechaEntregaEstimada: '2026-11-13' }, { hoy: HOY })).toHaveProperty('fechaEntregaEstimada');
    expect(validarDatosPedido({ ...base, fechaEntregaEstimada: '2026-11-13' }, { hoy: HOY, fechaVigente: '2026-11-13' })).toEqual({});
    expect(validarDatosPedido({ ...base, fechaEntregaEstimada: '2028-11-15' }, { hoy: HOY })).toHaveProperty('fechaEntregaEstimada');
    expect(Object.keys(validarDatosPedido({ ...base, modalidadEntrega: 'instalacion_terreno' }, { hoy: HOY })).sort())
      .toEqual(['direccionInstalacion', 'requiereInstalacion']);
    expect(validarDatosPedido({ ...base, montoBaseTrabajo: '10.000' }, { hoy: HOY, conMontoBase: true, abonado: 40000 }))
      .toEqual({ montoBaseTrabajo: 'El monto final no puede quedar bajo lo ya abonado ($40.000).' });
  });

  it('el costo de instalación cuenta solo si se requiere y la dirección no viaja con retiro en el local', () => {
    const datos = {
      ...DATOS_VACIOS, idEmpleadoResponsable: '2', fechaEntregaEstimada: '2026-11-20', modalidadEntrega: 'retiro_local',
      direccionInstalacion: 'Colón 455', costoInstalacionAdicional: '15.000', numeroFactura: ' F-12 ',
    };
    expect(datosPedidoParaApi(datos)).toEqual({
      idEmpleadoResponsable: 2, fechaEntregaEstimada: '2026-11-20', modalidadEntrega: 'retiro_local', requiereInstalacion: false,
      direccionInstalacion: null, costoInstalacionAdicional: 0, numeroFactura: 'F-12',
    });
    expect(datosPedidoParaApi({ ...datos, modalidadEntrega: 'instalacion_terreno', requiereInstalacion: true }))
      .toMatchObject({ direccionInstalacion: 'Colón 455', costoInstalacionAdicional: 15000 });
  });

  it('el abono no supera el saldo, no es futuro ni de más de un año atrás, y el inicial puede quedar en blanco', () => {
    const abono = { ...ABONO_VACIO, monto: '60.000', medioPago: 'efectivo' };
    expect(validarAbono(abono, { hoy: HOY, saldo: 59065 })).toEqual({ monto: 'El abono supera el saldo pendiente ($59.065).' });
    expect(validarAbono({ ...abono, monto: '59.065' }, { hoy: HOY, saldo: 59065 })).toEqual({});
    expect(validarAbono({ ...abono, monto: '1', fechaPago: '2026-11-15' }, { hoy: HOY })).toHaveProperty('fechaPago');
    expect(validarAbono({ ...abono, monto: '1', fechaPago: '2025-11-13' }, { hoy: HOY })).toHaveProperty('fechaPago');
    expect(validarAbono(ABONO_VACIO, { hoy: HOY, opcional: true })).toEqual({});
    expect(abonoParaApi(ABONO_VACIO, { opcional: true })).toBeNull();
    expect(abonoParaApi({ ...abono, numeroFactura: 'F-9' })).toEqual({ monto: 60000, fechaPago: undefined, medioPago: 'efectivo', observacion: null, numeroFactura: 'F-9' });
    expect(separarErrores({ 'abonoInicial.Monto': ['x'], fechaEntregaEstimada: ['y'] })).toEqual({ datos: { fechaEntregaEstimada: ['y'] }, abono: { monto: ['x'] } });
  });

  it('los filtros de pedidos salen de la dirección: cobranza viaja como booleano y el mes solo con el año', () => {
    const parametros = new URLSearchParams('estadoPago=abonado&cobranza=true&mes=11&q=%20rosa%20&orden=saldo_desc&pagina=3');
    const filtros = filtrosDesde(parametros, FILTROS_PEDIDOS);
    expect(filtros).toMatchObject({ estadoPago: 'abonado', cobranza: 'true', mes: '', orden: 'saldo_desc', pagina: 3 });
    expect(parametrosInvalidos(parametros, FILTROS_PEDIDOS)).toEqual(['mes']);
    expect(parametrosPedidos(filtros)).toEqual({
      estadoPedido: undefined, estadoPago: 'abonado', cobranza: true, anio: undefined, mes: undefined, idCliente: undefined,
      q: 'rosa', orden: 'saldo_desc', pagina: 3, tamano: 20,
    });
    expect(filtrosDesde(new URLSearchParams('anio=2026&mes=11&cobranza=si'), FILTROS_PEDIDOS)).toMatchObject({ anio: '2026', mes: '11', cobranza: '' });
  });
});

describe('cuadrícula del calendario', () => {
  it('noviembre de 2026 empieza el lunes 26 de octubre y termina el domingo 6 de diciembre (6 semanas)', () => {
    const semanas = construirMes({ anio: 2026, mes: 11 }, HOY);
    expect(semanas).toHaveLength(6);
    expect(semanas.every((s) => s.length === 7)).toBe(true);
    expect(semanas[0][0]).toMatchObject({ fecha: '2026-10-26', enMes: false });
    expect(semanas.at(-1)[6]).toMatchObject({ fecha: '2026-12-06', enMes: false });
    expect(semanas.flat().filter((d) => d.esHoy).map((d) => d.fecha)).toEqual(['2026-11-14']);
    expect(rangoCuadricula({ anio: 2026, mes: 11 })).toEqual({ desde: '2026-10-26', hasta: '2026-12-06' });
    // Un mes que empieza lunes y cabe en 4 semanas (febrero de 2027 empieza lunes).
    expect(construirMes({ anio: 2027, mes: 2 }, HOY)).toHaveLength(4);
  });

  it('mueve de mes cruzando el año y valida el mes de la dirección', () => {
    expect(moverMes({ anio: 2026, mes: 12 }, 1)).toEqual({ anio: 2027, mes: 1 });
    expect(moverMes({ anio: 2026, mes: 1 }, -1)).toEqual({ anio: 2025, mes: 12 });
    expect(mesDesdeTexto('2026-11')).toEqual({ anio: 2026, mes: 11 });
    expect(mesDesdeTexto('2026-13')).toBeNull();
  });

  it('agrupa por día con los atrasados primero y cuenta solo los días del mes', () => {
    const lista = [
      { idPedido: 3, fecha: '2026-11-14', estadoPedido: 'entregado' },
      { idPedido: 2, fecha: '2026-11-14', estadoPedido: 'en_proceso', atrasado: true },
      { idPedido: 1, fecha: '2026-11-14', estadoPedido: 'aceptado' },
      { idPedido: 9, fecha: '2026-10-30', estadoPedido: 'aceptado' },
    ];
    expect(agruparPorDia(lista)['2026-11-14'].map((p) => p.idPedido)).toEqual([2, 1, 3]);
    expect(lista.map(estadoEntrega)).toEqual(['entregado', 'atrasado', 'pendiente', 'pendiente']);
    expect(contarMes(lista, { anio: 2026, mes: 11 })).toEqual({ pendiente: 1, entregado: 1, atrasado: 1 });
  });
});

describe('ficha del pedido', () => {
  beforeEach(() => pedidos.obtener.mockResolvedValue(FICHA));

  const abrir = () => dibujar('/panel/pedidos/1', '/panel/pedidos/:id', <FichaPedido />);

  it('muestra solo las transiciones que permite la API y avanza con la versión vista', async () => {
    entrarComo('operario');
    pedidos.obtener.mockResolvedValue({ ...FICHA, montoAbonado: 0, saldoPendiente: 99065, pagos: [] });
    pedidos.cambiarEstado.mockResolvedValue({});
    abrir();
    const avanzar = await screen.findByRole('button', { name: 'Pasar a «En proceso»' });
    expect(screen.getByRole('button', { name: 'Anular pedido' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Terminado/ })).not.toBeInTheDocument();
    fireEvent.click(avanzar);
    await waitFor(() => expect(pedidos.cambiarEstado).toHaveBeenCalledWith(1, { estado: 'en_proceso' }, 'AAAAAAAAPED='));
    await waitFor(() => expect(pedidos.obtener).toHaveBeenCalledTimes(2));
  });

  it('un 409 al avanzar se explica y recarga la ficha', async () => {
    entrarComo('dueño');
    pedidos.cambiarEstado.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'version_obsoleta', mensaje: 'Otra persona modificó este pedido.' }));
    abrir();
    fireEvent.click(await screen.findByRole('button', { name: 'Pasar a «En proceso»' }));
    expect(await screen.findByText('Otra persona modificó este pedido')).toBeInTheDocument();
    await waitFor(() => expect(pedidos.obtener).toHaveBeenCalledTimes(2));
  });

  it('si tiene o tuvo abonos (aunque estén anulados), quien no es de gestión no ve «Anular pedido» y se le explica', async () => {
    entrarComo('operario');
    pedidos.obtener.mockResolvedValue({ ...FICHA, montoAbonado: 0, saldoPendiente: 99065, pagos: [FICHA.pagos[1]] });
    abrir();
    await screen.findByRole('button', { name: 'Pasar a «En proceso»' });
    expect(screen.queryByRole('button', { name: 'Anular pedido' })).not.toBeInTheDocument();
    expect(screen.getByText('Tiene o tuvo abonos: solo el dueño o el administrador pueden anularlo.')).toBeInTheDocument();
  });

  it('anular pide un motivo de al menos 10 caracteres', async () => {
    entrarComo('administrador');
    pedidos.cambiarEstado.mockResolvedValue({});
    abrir();
    fireEvent.click(await screen.findByRole('button', { name: 'Anular pedido' }));
    const dialogo = screen.getByRole('dialog', { name: 'Anular el pedido #1' });
    expect(dialogo).toHaveTextContent('Tiene abonos por $40.000');
    const motivo = within(dialogo).getByRole('textbox', { name: /Motivo de la anulación/ });
    escribir(motivo, 'Corto');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Anular pedido' }));
    expect(motivo).toHaveAttribute('aria-invalid', 'true');
    expect(pedidos.cambiarEstado).not.toHaveBeenCalled();
    escribir(motivo, 'El cliente desistió del trabajo');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Anular pedido' }));
    await waitFor(() => expect(pedidos.cambiarEstado).toHaveBeenCalledWith(1, { estado: 'anulado', motivo: 'El cliente desistió del trabajo' }, 'AAAAAAAAPED='));
  });

  it('el abono anulado queda tachado, con su motivo; solo los que se pueden anular ofrecen «Anular»', async () => {
    entrarComo('operario');
    abrir();
    const lista = await screen.findByRole('list', { name: 'Abonos del pedido' });
    const [vigente, anulado] = within(lista).getAllByRole('listitem');
    expect(within(anulado).getByText('$4.000').tagName).toBe('S');
    expect(anulado).toHaveTextContent('Anulado');
    expect(anulado).toHaveTextContent('Motivo: Se ingresó dos veces · anulado por Marta Fuentes');
    expect(within(anulado).queryByRole('button')).not.toBeInTheDocument();
    const anular = within(vigente).getByRole('button');
    expect(anular).toHaveTextContent('Anular el abono de $40.000 del 08-11-2026');
  });

  it('el abono que supera el saldo se explica antes de enviarlo; un 409 abono_excede_saldo va junto al monto sin perder lo escrito', async () => {
    entrarComo('operario');
    pedidos.registrarAbono.mockRejectedValueOnce(new ErrorApi({ estado: 409, codigo: 'abono_excede_saldo', mensaje: 'El abono supera el saldo pendiente ($19.065).' }));
    pedidos.registrarAbono.mockResolvedValueOnce({ ...FICHA, saldoPendiente: 40000 });
    abrir();
    fireEvent.click(await screen.findByRole('button', { name: 'Registrar abono' }));
    const dialogo = screen.getByRole('dialog', { name: 'Registrar abono del pedido #1' });
    const monto = within(dialogo).getByRole('textbox', { name: /^Monto/ });
    await waitFor(() => expect(monto).toHaveFocus());
    escribir(monto, '70.000');
    escribir(within(dialogo).getByRole('combobox', { name: /^Medio de pago/ }), 'efectivo');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar abono' }));
    expect(monto).toHaveAccessibleDescription('El abono supera el saldo pendiente ($59.065).');
    expect(pedidos.registrarAbono).not.toHaveBeenCalled();

    escribir(monto, '20.000');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar abono' }));
    await waitFor(() => expect(monto).toHaveAccessibleDescription('El abono supera el saldo pendiente ($19.065).'));
    expect(monto).toHaveValue('20.000');
    expect(pedidos.registrarAbono).toHaveBeenCalledWith(1, { monto: 20000, fechaPago: undefined, medioPago: 'efectivo', observacion: null });

    escribir(monto, '19.065');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar abono' }));
    await waitFor(() => expect(pedidos.registrarAbono).toHaveBeenLastCalledWith(1, expect.objectContaining({ monto: 19065 })));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('editar con una versión vieja conserva lo escrito, relee la versión actual y guarda sobre ella', async () => {
    entrarComo('administrador');
    const ACTUAL = { ...FICHA, fechaEntregaEstimada: '2026-11-18', rowVersion: 'AAAAAAAAPE2=' };
    pedidos.obtener.mockResolvedValueOnce(FICHA).mockResolvedValueOnce(ACTUAL).mockResolvedValue(ACTUAL);
    pedidos.editar
      .mockRejectedValueOnce(new ErrorApi({ estado: 409, codigo: 'version_obsoleta', mensaje: 'Otra persona modificó este pedido.' }))
      .mockResolvedValueOnce(ACTUAL);
    abrir();
    fireEvent.click(await screen.findByRole('button', { name: 'Editar datos' }));
    const factura = await screen.findByRole('textbox', { name: /^Número de factura/ });
    escribir(factura, 'F-1204');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    await waitFor(() => expect(pedidos.editar).toHaveBeenCalledWith(1, expect.objectContaining({ numeroFactura: 'F-1204', montoBaseTrabajo: 99065 }), 'AAAAAAAAPED='));
    expect(await screen.findByText('Otra persona modificó este pedido mientras lo editabas')).toBeInTheDocument();
    const guardarSobre = await screen.findByRole('button', { name: 'Guardar sobre la versión actual' });
    await waitFor(() => expect(guardarSobre).toHaveFocus());
    expect(screen.getByText(/Versión actual: Aceptado, monto final \$99\.065, entrega estimada 18-11-2026/)).toBeInTheDocument();
    expect(factura).toHaveValue('F-1204');
    fireEvent.click(guardarSobre);
    await waitFor(() => expect(pedidos.editar).toHaveBeenLastCalledWith(1, expect.objectContaining({ numeroFactura: 'F-1204' }), 'AAAAAAAAPE2='));
    await waitFor(() => expect(screen.queryByRole('textbox', { name: /^Número de factura/ })).not.toBeInTheDocument());
  });

  it('quien no es de gestión ve los montos sin poder cambiarlos y los envía como estaban', async () => {
    entrarComo('operario');
    pedidos.obtener.mockResolvedValue({ ...FICHA, requiereInstalacion: true, modalidadEntrega: 'instalacion_terreno', direccionInstalacion: 'Colón 455', costoInstalacionAdicional: 15000, montoTotalFinal: 114065 });
    pedidos.editar.mockResolvedValue({});
    abrir();
    fireEvent.click(await screen.findByRole('button', { name: 'Editar datos' }));
    const base = await screen.findByRole('textbox', { name: /^Monto del trabajo/ });
    expect(base).toHaveAttribute('readonly');
    expect(base).toHaveAccessibleDescription(/Solo el dueño o el administrador cambian los montos/);
    expect(screen.getByRole('textbox', { name: /^Costo de instalación/ })).toHaveAttribute('readonly');
    // Quitar la instalación cambiaría los montos: no se permite.
    fireEvent.click(screen.getByRole('checkbox', { name: 'Requiere instalación' }));
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    expect(screen.getByRole('checkbox', { name: 'Requiere instalación' })).toHaveAccessibleDescription(/solo el dueño o el administrador/);
    expect(pedidos.editar).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('checkbox', { name: 'Requiere instalación' }));
    escribir(screen.getByRole('textbox', { name: /^Número de factura/ }), 'F-9');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    await waitFor(() => expect(pedidos.editar).toHaveBeenCalledWith(1, expect.objectContaining({
      montoBaseTrabajo: 99065, costoInstalacionAdicional: 15000, numeroFactura: 'F-9',
    }), 'AAAAAAAAPED='));
  });

  it('al cambiar de pedido no queda abierta la edición del anterior', async () => {
    entrarComo('administrador');
    const { Link } = await import('react-router-dom');
    render(
      <ProveedorAvisos>
        <MemoryRouter initialEntries={['/panel/pedidos/1']}>
          <Routes>
            <Route path="/panel/pedidos/:id" element={<><Link to="/panel/pedidos/2">Otro pedido</Link><FichaPedido /></>} />
          </Routes>
        </MemoryRouter>
      </ProveedorAvisos>,
    );
    fireEvent.click(await screen.findByRole('button', { name: 'Editar datos' }));
    expect(await screen.findByRole('heading', { name: 'Editar los datos del pedido' })).toBeInTheDocument();
    pedidos.obtener.mockResolvedValue({ ...FICHA, idPedido: 2, numero: '#2' });
    fireEvent.click(screen.getByRole('link', { name: 'Otro pedido' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'Pedido #2' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Editar los datos del pedido' })).not.toBeInTheDocument();
  });

  it('si se pierde la respuesta del abono, revisa la ficha: si quedó registrado, no deja duplicarlo', async () => {
    entrarComo('operario');
    const conNuevo = { ...FICHA, montoAbonado: 60000, saldoPendiente: 39065, pagos: [PAGO(12, 20000, { medioPago: 'efectivo' }), ...FICHA.pagos] };
    pedidos.obtener.mockResolvedValueOnce(FICHA).mockResolvedValue(conNuevo);
    pedidos.registrarAbono.mockRejectedValue(new ErrorApi({ estado: 0, codigo: 'sin_conexion', mensaje: 'Sin conexión' }));
    abrir();
    fireEvent.click(await screen.findByRole('button', { name: 'Registrar abono' }));
    const dialogo = screen.getByRole('dialog', { name: 'Registrar abono del pedido #1' });
    escribir(within(dialogo).getByRole('textbox', { name: /^Monto/ }), '20.000');
    escribir(within(dialogo).getByRole('combobox', { name: /^Medio de pago/ }), 'efectivo');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar abono' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(pedidos.registrarAbono).toHaveBeenCalledTimes(1);
    expect((await screen.findAllByText(/el abono sí quedó registrado/)).length).toBeGreaterThan(0);
  });

  it('si se pierde la respuesta y el abono no aparece, deja reintentar; si no se pudo revisar, bloquea hasta revisar', async () => {
    entrarComo('operario');
    // Carga, primera revisión, recarga de la ficha, segunda revisión (falla) y lo que sigue.
    pedidos.obtener
      .mockResolvedValueOnce(FICHA)
      .mockResolvedValueOnce(FICHA)
      .mockResolvedValueOnce(FICHA)
      .mockRejectedValueOnce(new ErrorApi({ estado: 0, codigo: 'sin_conexion', mensaje: 'x' }))
      .mockResolvedValue(FICHA);
    pedidos.registrarAbono.mockRejectedValue(new ErrorApi({ estado: 0, codigo: 'tiempo_agotado', mensaje: 'x' }));
    abrir();
    fireEvent.click(await screen.findByRole('button', { name: 'Registrar abono' }));
    const dialogo = screen.getByRole('dialog', { name: 'Registrar abono del pedido #1' });
    escribir(within(dialogo).getByRole('textbox', { name: /^Monto/ }), '20.000');
    escribir(within(dialogo).getByRole('combobox', { name: /^Medio de pago/ }), 'efectivo');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar abono' }));
    expect(await within(dialogo).findByText(/Revisamos la ficha y no aparece: puedes intentarlo de nuevo/)).toBeInTheDocument();

    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar abono' }));
    expect(await within(dialogo).findByText(/Tampoco pudimos revisar la ficha/)).toBeInTheDocument();
    expect(pedidos.registrarAbono).toHaveBeenCalledTimes(2);
    const registrar = within(dialogo).getByRole('button', { name: 'Registrar abono' });
    expect(registrar).toHaveAttribute('aria-disabled', 'true');
    fireEvent.click(registrar);
    expect(pedidos.registrarAbono).toHaveBeenCalledTimes(2);
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Revisar de nuevo' }));
    expect(await within(dialogo).findByText(/Revisamos la ficha y no aparece/)).toBeInTheDocument();
    expect(within(dialogo).getByRole('button', { name: 'Registrar abono' })).not.toHaveAttribute('aria-disabled');
  });

  it('un 409 porque el pedido ya no tiene saldo deja el diálogo sin enviar; una factura distinta va a su campo', async () => {
    entrarComo('operario');
    // Carga, recarga tras el 409 de la factura y, tras el segundo 409, el pedido ya pagado.
    pedidos.obtener.mockResolvedValueOnce(FICHA).mockResolvedValueOnce(FICHA)
      .mockResolvedValue({ ...FICHA, saldoPendiente: 0, montoAbonado: 99065, estadoPago: 'pagado_total' });
    pedidos.registrarAbono
      .mockRejectedValueOnce(new ErrorApi({ estado: 409, codigo: 'factura_distinta', mensaje: 'El pedido ya tiene la factura F-1. Déjala en blanco o usa esa.' }))
      .mockRejectedValueOnce(new ErrorApi({ estado: 409, codigo: 'transicion_invalida', mensaje: 'El pedido ya está pagado.' }));
    abrir();
    fireEvent.click(await screen.findByRole('button', { name: 'Registrar abono' }));
    const dialogo = screen.getByRole('dialog', { name: 'Registrar abono del pedido #1' });
    escribir(within(dialogo).getByRole('textbox', { name: /^Monto/ }), '20.000');
    escribir(within(dialogo).getByRole('combobox', { name: /^Medio de pago/ }), 'efectivo');
    const factura = within(dialogo).getByRole('textbox', { name: /^Número de factura/ });
    escribir(factura, 'F-2');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar abono' }));
    await waitFor(() => expect(factura).toHaveAccessibleDescription(/El pedido ya tiene la factura F-1/));

    escribir(factura, '');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar abono' }));
    expect(await within(dialogo).findByText('El pedido ya está pagado.')).toBeInTheDocument();
    expect(await within(dialogo).findByText('El pedido ya no tiene saldo pendiente.')).toBeInTheDocument();
    expect(within(dialogo).getByRole('button', { name: 'Registrar abono' })).toHaveAttribute('aria-disabled', 'true');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Ver versión actual' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('un pedido anulado con abonos muestra lo que queda a favor del cliente y no recibe abonos', async () => {
    entrarComo('dueño');
    pedidos.obtener.mockResolvedValue({
      ...FICHA, estadoPedido: 'anulado', motivoAnulacion: 'El cliente desistió', aFavorDelCliente: 40000, transicionesPosibles: [],
    });
    abrir();
    expect(await screen.findByText('A devolver o a favor del cliente: $40.000')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Registrar abono' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Editar datos' })).not.toBeInTheDocument();
    expect(screen.getByText('Un pedido anulado no cambia más.')).toBeInTheDocument();
  });

  it('un pedido entregado y pagado dice que su ciclo está cerrado', async () => {
    entrarComo('operario');
    pedidos.obtener.mockResolvedValue({ ...FICHA, estadoPedido: 'entregado', estadoPago: 'pagado_total', saldoPendiente: 0, montoAbonado: 99065, cicloCerrado: true, transicionesPosibles: [] });
    abrir();
    expect(await screen.findByText('Ciclo cerrado')).toBeInTheDocument();
    expect(screen.getByText('Entregado y pagado: el ciclo está cerrado.')).toBeInTheDocument();
  });
});

describe('listado de pedidos', () => {
  const PAGINA = {
    elementos: [{
      idPedido: 1, numero: '#1', origen: 'cotizacion', fechaPedido: '2026-11-08T10:00:00', cliente: { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', telefono: '912345678' },
      responsable: { idEmpleado: 2, nombre: 'Hugo Vera' }, fechaEntregaEstimada: '2026-11-16', modalidadEntrega: 'retiro_local', estadoPedido: 'aceptado',
      estadoPago: 'abonado', montoTotalFinal: 99065, montoAbonado: 40000, saldoPendiente: 59065, cicloCerrado: false,
    }],
    total: 31, pagina: 1, tamano: 20, totalPaginas: 2, resumen: { totalPorCobrar: 1234567, cantidadPorCobrar: 12 },
  };

  beforeEach(() => {
    entrarComo('administrador');
    pedidos.listar.mockResolvedValue(PAGINA);
  });

  it('pide a la API con los filtros de la dirección y «Por cobrar» muestra el total de todo el filtro', async () => {
    dibujar('/panel/pedidos?estadoPedido=aceptado', '/panel/pedidos', <ListaPedidos />);
    expect(await screen.findByRole('link', { name: 'Pedido #1' })).toHaveAttribute('href', '/panel/pedidos/1');
    expect(pedidos.listar).toHaveBeenLastCalledWith(expect.objectContaining({ estadoPedido: 'aceptado', cobranza: undefined, pagina: 1, tamano: 20, orden: 'fecha_desc' }), { senal: expect.any(AbortSignal) });
    expect(screen.getByRole('search', { name: 'Filtros de pedidos' })).toHaveTextContent('31 pedidos · $1.234.567 por cobrar');

    fireEvent.click(screen.getByRole('button', { name: 'Por cobrar' }));
    await waitFor(() => expect(pedidos.listar).toHaveBeenLastCalledWith(expect.objectContaining({ cobranza: true, estadoPedido: 'aceptado', pagina: 1 }), expect.anything()));
    expect(screen.getByTestId('ruta')).toHaveTextContent('/panel/pedidos?estadoPedido=aceptado&cobranza=true');
    expect(await screen.findByRole('heading', { level: 1, name: 'Pedidos por cobrar' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Total por cobrar' })).toHaveTextContent('$1.234.567');
    expect(screen.getByRole('region', { name: 'Total por cobrar' })).toHaveTextContent('12 pedidos no anulados');
  });

  it('un año válido fuera de la lista se agrega como opción; el filtro por cliente es un botón para quitarlo', async () => {
    clientes.obtener.mockResolvedValue({ idCliente: 9, nombre: 'Rosa', apellido: 'Pino' });
    dibujar('/panel/pedidos?anio=2019&idCliente=9', '/panel/pedidos', <ListaPedidos />);
    const anio = await screen.findByRole('combobox', { name: 'Año' });
    expect(anio).toHaveValue('2019');
    expect(within(anio).getByRole('option', { name: '2019' })).toBeInTheDocument();
    const chip = await screen.findByRole('button', { name: 'Quitar el filtro por cliente: Rosa Pino' });
    expect(chip).not.toHaveAttribute('aria-pressed');
    fireEvent.click(chip);
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent(/^\/panel\/pedidos\?anio=2019$/));
  });

  it('al cambiar un filtro, las filas anteriores siguen a la vista mientras llegan las nuevas', async () => {
    let soltar;
    dibujar('/panel/pedidos', '/panel/pedidos', <ListaPedidos />);
    await screen.findByRole('link', { name: 'Pedido #1' });
    pedidos.listar.mockImplementation(() => new Promise((r) => { soltar = () => r({ ...PAGINA, elementos: [] , total: 0, totalPaginas: 0 }); }));
    escribir(screen.getByRole('combobox', { name: 'Estado de pago' }), 'no_pagado');
    await waitFor(() => expect(pedidos.listar).toHaveBeenLastCalledWith(expect.objectContaining({ estadoPago: 'no_pagado' }), expect.anything()));
    expect(screen.getByRole('link', { name: 'Pedido #1' })).toBeInTheDocument();
    expect(screen.getByRole('table').closest('[aria-busy]')).toHaveAttribute('aria-busy', 'true');
    await act(async () => soltar());
    expect(await screen.findByText('Ningún pedido coincide con estos filtros')).toBeInTheDocument();
  });

  it('elegir un mes sin año toma el año en curso; un filtro inválido de la dirección se limpia', async () => {
    dibujar('/panel/pedidos?estadoPago=quizas', '/panel/pedidos', <ListaPedidos />);
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent(/^\/panel\/pedidos$/));
    escribir(screen.getByRole('combobox', { name: 'Mes' }), '3');
    await waitFor(() => expect(pedidos.listar).toHaveBeenLastCalledWith(expect.objectContaining({ mes: '3', anio: hoyChile().slice(0, 4) }), expect.anything()));
  });
});

describe('calendario de entregas', () => {
  beforeEach(() => {
    entrarComo('operario');
    pedidos.calendario.mockResolvedValue([
      { idPedido: 4, numero: '#4', fecha: '2026-03-13', cliente: { nombre: 'Minimarket', apellido: 'Don Lucho' }, modalidadEntrega: 'instalacion_terreno', estadoPedido: 'en_proceso', estadoPago: 'abonado', atrasado: true },
      { idPedido: 2, numero: '#2', fecha: '2026-03-14', cliente: { nombre: 'Ferretería', apellido: 'Biobío' }, modalidadEntrega: 'retiro_local', estadoPedido: 'entregado', estadoPago: 'pagado_total', atrasado: false },
    ]);
  });

  it('pide el rango de la cuadrícula, distingue los estados con texto y cambia de mes por la dirección', async () => {
    dibujar('/panel/calendario?mes=2026-03', '/panel/calendario', <CalendarioEntregas />);
    const atrasado = await screen.findByRole('link', { name: /Atrasado.*#4 · Minimarket Don Lucho/ });
    expect(atrasado).toHaveAttribute('href', '/panel/pedidos/4');
    expect(screen.getByRole('link', { name: /Entregado.*#2 · Ferretería Biobío/ })).toBeInTheDocument();
    expect(pedidos.calendario).toHaveBeenCalledWith({ desde: '2026-02-23', hasta: '2026-04-05' }, { senal: expect.any(AbortSignal) });
    expect(await screen.findByText('Marzo de 2026: 2 entregas.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'Marzo de 2026' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 3, name: /viernes 13 de marzo, 1 entrega/ })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /^Mes siguiente: abril de 2026/ }));
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent('/panel/calendario?mes=2026-04'));
    await waitFor(() => expect(pedidos.calendario).toHaveBeenLastCalledWith({ desde: '2026-03-30', hasta: '2026-05-03' }, expect.anything()));
    fireEvent.click(screen.getByRole('button', { name: 'Hoy' }));
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent(/^\/panel\/calendario$/));
    // En el mes actual, «Hoy» queda inactivo y no hace nada.
    const llamadas = pedidos.calendario.mock.calls.length;
    const hoyBoton = screen.getByRole('button', { name: 'Hoy' });
    expect(hoyBoton).toHaveAttribute('aria-disabled', 'true');
    fireEvent.click(hoyBoton);
    expect(screen.getByTestId('ruta')).toHaveTextContent(/^\/panel\/calendario$/);
    expect(pedidos.calendario.mock.calls.length).toBe(llamadas);
  });
});

describe('crear el pedido desde la cotización', () => {
  const COTIZACION = {
    idCotizacion: 46, numero: '#46', idSolicitud: 123, estado: 'aprobada', cliente: { nombre: 'Rosa', apellido: 'Pino' }, total: 84065,
    totalRedondeado: 84065, idPedido: null, rowVersion: 'C=',
  };

  async function llenar() {
    escribir(await screen.findByLabelText(/^Entrega estimada/), hoyChile());
    fireEvent.click(screen.getByRole('radio', { name: 'Retiro en el local' }));
  }

  beforeEach(() => entrarComo('operario'));

  it('si se pierde la respuesta y la cotización ya tiene su pedido, lo abre sin crear otro', async () => {
    cotizaciones.obtener.mockResolvedValueOnce(COTIZACION).mockResolvedValue({ ...COTIZACION, idPedido: 9 });
    pedidos.crearDesdeCotizacion.mockRejectedValue(new ErrorApi({ estado: 0, codigo: 'sin_conexion', mensaje: 'x' }));
    dibujar('/panel/cotizaciones/46/pedido', '/panel/cotizaciones/:id/pedido', <CrearPedido />);
    await llenar();
    fireEvent.click(screen.getByRole('button', { name: 'Crear pedido' }));
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent('/panel/pedidos/9'));
    expect(pedidos.crearDesdeCotizacion).toHaveBeenCalledTimes(1);
  });

  it('un 409 porque ya tiene pedido recarga, muestra el enlace y lleva el foco al título', async () => {
    cotizaciones.obtener.mockResolvedValueOnce(COTIZACION).mockResolvedValue({ ...COTIZACION, idPedido: 9 });
    pedidos.crearDesdeCotizacion.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'ya_tiene_pedido', mensaje: 'Esta cotización ya tiene un pedido.' }));
    dibujar('/panel/cotizaciones/46/pedido', '/panel/cotizaciones/:id/pedido', <CrearPedido />);
    await llenar();
    fireEvent.click(screen.getByRole('button', { name: 'Crear pedido' }));
    expect(await screen.findByRole('link', { name: 'Ver el pedido' })).toHaveAttribute('href', '/panel/pedidos/9');
    await waitFor(() => expect(screen.getByRole('heading', { level: 1 })).toHaveFocus());
  });

  it('con el pedido eliminado no ofrece crear otro', async () => {
    cotizaciones.obtener.mockResolvedValue({ ...COTIZACION, idPedido: 9, pedidoEliminado: true });
    dibujar('/panel/cotizaciones/46/pedido', '/panel/cotizaciones/:id/pedido', <CrearPedido />);
    expect(await screen.findByText('El pedido #9 fue eliminado')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Crear pedido' })).not.toBeInTheDocument();
  });
});

describe('useConsulta', () => {
  it('corta la petición anterior al cambiar de clave y, con «conservar», muestra los datos anteriores mientras recarga', async () => {
    const senales = [];
    let soltar;
    const consulta = vi.fn((senal) => {
      senales.push(senal);
      return senales.length === 1 ? Promise.resolve('primera') : new Promise((r) => { soltar = () => r('segunda'); });
    });
    const { result, rerender } = renderHook(({ clave }) => useConsulta(clave, consulta, { conservar: true }), { initialProps: { clave: 'a' } });
    await waitFor(() => expect(result.current.datos).toBe('primera'));
    rerender({ clave: 'b' });
    await waitFor(() => expect(consulta).toHaveBeenCalledTimes(2));
    expect(senales[0].aborted).toBe(true);
    expect(result.current).toMatchObject({ datos: 'primera', cargando: false, recargando: true });
    await act(async () => soltar());
    expect(result.current).toMatchObject({ datos: 'segunda', recargando: false });
  });
});
