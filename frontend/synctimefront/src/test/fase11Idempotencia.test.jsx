import { act, fireEvent, render, renderHook, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ErrorApi } from '../api/cliente';
import { empleados, pedidos } from '../api/pedidos';
import { ProveedorAvisos } from '../avisos/Avisos';
import { menuPara } from '../estructuras/navegacion';
import { useClaveIdempotencia } from '../paginas/compartidos/useClaveIdempotencia';
import FichaPedido from '../paginas/pedidos/FichaPedido';
import { almacenSesion, useSesion } from '../sesion/sesion';

vi.mock('../api/authService', () => ({ COOKIE_CSRF: 'synctime_csrf', default: { renovar: vi.fn(), salir: vi.fn() } }));
vi.mock('../api/pedidos', () => ({
  pedidos: {
    listar: vi.fn(), obtener: vi.fn(), editar: vi.fn(), cambiarEstado: vi.fn(), registrarAbono: vi.fn(), anularAbono: vi.fn(),
    eliminar: vi.fn(), calendario: vi.fn(), alertas: vi.fn(), crearDesdeCotizacion: vi.fn(), crearDirecto: vi.fn(),
  },
  empleados: { listar: vi.fn() },
}));
vi.mock('../api/solicitudes', () => ({ clientes: { obtener: vi.fn() }, solicitudes: {}, misSolicitudes: {}, adjuntos: {} }));
vi.mock('../api/cotizaciones', () => ({ cotizaciones: { obtener: vi.fn() }, misCotizaciones: {}, materiales: { listar: vi.fn() } }));

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
const FICHA = {
  idPedido: 1, numero: '#1', origen: 'cotizacion', solicitud: { idSolicitud: 123, numero: '#123' }, cotizacion: { idCotizacion: 46, numero: '#46' },
  cliente: { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', telefono: '912345678' }, responsable: { idEmpleado: 2, nombre: 'Hugo Vera' },
  fechaPedido: '2026-11-08T10:00:00', fechaEntregaEstimada: '2026-11-16', fechaEntregaReal: null, modalidadEntrega: 'retiro_local',
  requiereInstalacion: false, direccionInstalacion: null, montoBaseTrabajo: 99065, costoInstalacionAdicional: 0, montoTotalFinal: 99065,
  estadoPedido: 'aceptado', estadoPago: 'no_pagado', montoAbonado: 0, saldoPendiente: 99065, numeroFactura: null, cicloCerrado: false,
  motivoAnulacion: null, aFavorDelCliente: 0, transicionesPosibles: ['en_proceso', 'anulado'], historial: [], pagos: [], rowVersion: 'AAAAAAAAPED=',
};
const escribir = (control, valor) => fireEvent.change(control, { target: { value: valor } });

function entrarComo(rol) {
  act(() => almacenSesion.aplicar({ token: 'jwt', tipo: 'empleado', usuario: { idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', rol } }));
}

beforeEach(() => {
  vi.clearAllMocks();
  empleados.listar.mockResolvedValue([]);
  pedidos.obtener.mockResolvedValue(FICHA);
});
afterEach(() => act(() => almacenSesion.cerrar()));

describe('clave de idempotencia por formulario abierto', () => {
  it('es un UUID estable entre renders y cambia solo al renovarla o al reabrir', () => {
    const { result, rerender } = renderHook(({ abierto }) => useClaveIdempotencia(abierto), { initialProps: { abierto: true } });
    const primera = result.current.clave;
    expect(primera).toMatch(UUID);
    rerender({ abierto: true });
    expect(result.current.clave).toBe(primera);
    act(() => result.current.renovar());
    const segunda = result.current.clave;
    expect(segunda).not.toBe(primera);
    rerender({ abierto: false });
    rerender({ abierto: true });
    expect(result.current.clave).not.toBe(segunda);
  });
});

describe('abono: misma clave al reintentar tras un corte, otra al reabrir', () => {
  it('reintenta con la misma clave y, tras el éxito, el siguiente abono lleva una nueva', async () => {
    entrarComo('dueño');
    pedidos.registrarAbono
      .mockRejectedValueOnce(new ErrorApi({ estado: 0, codigo: 'sin_conexion', mensaje: 'No pudimos conectar con el servidor.' }))
      .mockResolvedValueOnce(FICHA)
      .mockResolvedValueOnce(FICHA);
    render(
      <ProveedorAvisos>
        <MemoryRouter initialEntries={['/panel/pedidos/1']}>
          <Routes><Route path="/panel/pedidos/:id" element={<FichaPedido />} /></Routes>
        </MemoryRouter>
      </ProveedorAvisos>,
    );
    fireEvent.click(await screen.findByRole('button', { name: 'Registrar abono' }));
    let dialogo = screen.getByRole('dialog', { name: 'Registrar abono del pedido #1' });
    escribir(within(dialogo).getByRole('textbox', { name: /^Monto/ }), '10.000');
    escribir(within(dialogo).getByRole('combobox', { name: /^Medio de pago/ }), 'efectivo');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar abono' }));
    // Tras el corte la ficha se revisa: el abono no aparece y se puede reintentar.
    expect(await within(dialogo).findByText(/no aparece: puedes intentarlo de nuevo/)).toBeInTheDocument();
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar abono' }));
    await waitFor(() => expect(pedidos.registrarAbono).toHaveBeenCalledTimes(2));
    const [primera, segunda] = pedidos.registrarAbono.mock.calls.map((c) => c[2]?.clave);
    expect(primera).toMatch(UUID);
    expect(segunda).toBe(primera);
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    fireEvent.click(await screen.findByRole('button', { name: 'Registrar abono' }));
    dialogo = screen.getByRole('dialog', { name: 'Registrar abono del pedido #1' });
    escribir(within(dialogo).getByRole('textbox', { name: /^Monto/ }), '5.000');
    escribir(within(dialogo).getByRole('combobox', { name: /^Medio de pago/ }), 'efectivo');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar abono' }));
    await waitFor(() => expect(pedidos.registrarAbono).toHaveBeenCalledTimes(3));
    expect(pedidos.registrarAbono.mock.calls[2][2].clave).toMatch(UUID);
    expect(pedidos.registrarAbono.mock.calls[2][2].clave).not.toBe(primera);
  });
});

describe('menú por rol (fase 11)', () => {
  function rutasDe(rol) {
    entrarComo(rol);
    const { result } = renderHook(() => useSesion());
    return menuPara(result.current.tieneRol).flatMap((g) => g.entradas.map((e) => e.ruta));
  }

  it('operario y diseñador no ven auditoría ni correos no enviados', () => {
    for (const rol of ['operario', 'diseñador']) {
      const rutas = rutasDe(rol);
      expect(rutas).not.toContain('/panel/auditoria');
      expect(rutas).not.toContain('/panel/correos-no-enviados');
    }
  });

  it('dueño y administrador sí los ven', () => {
    for (const rol of ['dueño', 'administrador']) {
      const rutas = rutasDe(rol);
      expect(rutas).toContain('/panel/auditoria');
      expect(rutas).toContain('/panel/correos-no-enviados');
    }
  });
});
