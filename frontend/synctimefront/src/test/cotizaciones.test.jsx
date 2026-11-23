import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { ErrorApi } from '../api/cliente';
import { cotizaciones, materiales, misCotizaciones } from '../api/cotizaciones';
import { misSolicitudes, solicitudes } from '../api/solicitudes';
import { ProveedorAvisos } from '../avisos/Avisos';
import EditorCotizacion from '../paginas/cotizaciones/EditorCotizacion';
import FichaCotizacion from '../paginas/cotizaciones/FichaCotizacion';
import { SeccionCotizacion } from '../paginas/cotizaciones/SeccionCotizacion';
import {
  calcularTotales, cantidadDesdeTexto, cotizacionParaApi, erroresCotizacionDesdeApi, formularioDesdeCotizacion, lineaVacia,
  parametrosCotizaciones, pesosDesdeTexto, subtotalLinea, validarCotizacion, FILTROS_COTIZACIONES,
} from '../paginas/cotizaciones/reglasCotizacion';
import { MiSolicitud } from '../paginas/solicitudes/MisSolicitudes';
import { pasosSeguimiento } from '../paginas/solicitudes/reglasSeguimiento';
import { almacenSesion } from '../sesion/sesion';
import { filtrosDesde, paraDireccion, parametrosInvalidos } from '../utilidades/filtrosUrl';

vi.mock('../api/authService', () => ({
  COOKIE_CSRF: 'synctime_csrf',
  default: { reenviarVerificacion: vi.fn(), renovar: vi.fn(), salir: vi.fn() },
}));
vi.mock('../api/cotizaciones', () => ({
  cotizaciones: { emitir: vi.fn(), editar: vi.fn(), responder: vi.fn(), listar: vi.fn(), obtener: vi.fn(), eliminar: vi.fn() },
  misCotizaciones: { obtener: vi.fn(), responder: vi.fn() },
  materiales: { listar: vi.fn() },
}));
vi.mock('../api/solicitudes', () => ({
  misSolicitudes: { obtener: vi.fn(), listar: vi.fn() },
  solicitudes: { obtener: vi.fn() },
  adjuntos: { miniatura: vi.fn(), archivo: vi.fn() },
  clientes: {},
}));

const linea = (idMaterial, cantidad, costoUnitario, medidaAplicada = '') => ({ ...lineaVacia(), idMaterial: String(idMaterial), cantidad, costoUnitario, medidaAplicada });
const escribir = (control, valor) => fireEvent.change(control, { target: { value: valor } });

const MATERIALES = [
  { idMaterial: 1, nombre: 'Acrílico 3 mm', unidadMedida: 'm2', costoUnitarioAprox: 18500, activo: true },
  { idMaterial: 2, nombre: 'Vinilo impreso', unidadMedida: 'm2', costoUnitarioAprox: 9900.4, activo: true },
  { idMaterial: 3, nombre: 'Perfil de aluminio', unidadMedida: 'ml', costoUnitarioAprox: 4200, activo: false },
];
const ROSA = { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', telefono: '912345678', correo: 'rosa@correo.test' };
const SOLICITUD = {
  idSolicitud: 123, numero: '#123', fechaChile: '2026-11-14T10:32:00', origen: 'online', estado: 'solicitado', cliente: ROSA, idCliente: 9,
  detalles: [{ idProducto: 2, producto: 'Letras corpóreas', descripcion: null, anchoMm: 1200, altoMm: 600, cantidad: 2, areaM2: 1.44 }],
  areaTotalM2: 1.44, descripcionGeneral: 'Letras para la fachada del local', rowVersion: 'AAAAAAAAB9E=', adjuntos: [], historial: [],
};
const COTIZACION = {
  idCotizacion: 45, numero: '#45', idSolicitud: 123, numeroSolicitud: '#123', fechaChile: '2026-11-14T11:00:00', estado: 'pendiente', cliente: ROSA,
  items: [{ producto: 'Letras corpóreas', descripcion: null, anchoMm: 1200, altoMm: 600, cantidad: 2, areaM2: 1.44 }],
  materiales: [{ idMaterial: 1, material: 'Acrílico 3 mm', unidad: 'm2', medidaAplicada: '120 × 60 cm', cantidad: 1.5, costoUnitario: 18500, subtotal: 27750 }],
  costoMaterialesTotal: 27750, costoManoObra: 30000, otrosCostos: 0, total: 57750, totalRedondeado: 57750, observaciones: null,
  elaboradaPor: { idEmpleado: 1, nombre: 'Marta Fuentes' }, respuesta: null, idPedido: null, rowVersion: 'AAAAAAAACOT=',
};

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

function entrarComo(rol, tipo = 'empleado') {
  act(() => almacenSesion.aplicar({ token: 'jwt', tipo, usuario: { idEmpleado: 1, idCliente: 9, nombre: 'Marta', apellido: 'Fuentes', rol } }));
}

beforeEach(() => {
  vi.clearAllMocks();
  materiales.listar.mockResolvedValue(MATERIALES);
});
afterEach(() => act(() => almacenSesion.cerrar()));

describe('reglas de la cotización (fase 8)', () => {
  it('lee pesos con puntos de miles y rechaza decimales; la cantidad acepta coma o punto', () => {
    expect(pesosDesdeTexto('12.990')).toBe(12990);
    expect(pesosDesdeTexto('$ 1.500.000')).toBe(1500000);
    expect(pesosDesdeTexto('12990')).toBe(12990);
    expect(pesosDesdeTexto('')).toBeNull();
    expect(pesosDesdeTexto('12,5')).toBeNaN();
    expect(pesosDesdeTexto('1.5')).toBeNaN();
    expect(cantidadDesdeTexto('2,75')).toBe(2.75);
    expect(cantidadDesdeTexto('2.75')).toBe(2.75);
    expect(cantidadDesdeTexto('dos')).toBeNaN();
  });

  it('suma en centavos y redondea el total al peso, mitad hacia arriba', () => {
    // 1,5 × $999 = $1.498,50 → $1.499
    expect(calcularTotales({ lineas: [linea(1, '1,5', '999')] })).toMatchObject({ materiales: 1498.5, total: 1498.5, totalRedondeado: 1499, conCentavos: true });
    // 0,1 + 0,2 sin restos de coma flotante: 0,1 × $10 + 0,2 × $10 = $3
    expect(calcularTotales({ lineas: [linea(1, '0,1', '10'), linea(2, '0,2', '10')] })).toMatchObject({ total: 3, totalRedondeado: 3, conCentavos: false });
    // 1,01 × $14.836 = $14.984,36, más $1.000 y $500 → $16.484,36 baja a $16.484
    expect(calcularTotales({ lineas: [linea(1, '1,01', '14836')], costoManoObra: '1.000', otrosCostos: '500' }))
      .toMatchObject({ total: 16484.36, totalRedondeado: 16484 });
    expect(subtotalLinea(linea(1, '2,25', '10.000'))).toBe(22500);
    expect(subtotalLinea(linea(1, '', '10.000'))).toBeNull();
  });

  it('valida cantidad (hasta 2 decimales), costos enteros y un total mayor que $0 y hasta $2.000.000.000', () => {
    const errores = validarCotizacion({ lineas: [linea('', '1,234', '12,5')], costoManoObra: '-3', otrosCostos: '' });
    expect(Object.keys(errores).sort()).toEqual(['costoManoObra', 'materiales.0.cantidad', 'materiales.0.costoUnitario', 'materiales.0.idMaterial']);
    expect(errores['materiales.0.cantidad']).toMatch(/2 decimales/);
    expect(validarCotizacion({ lineas: [], costoManoObra: '', otrosCostos: '' })).toEqual({ costoManoObra: expect.stringMatching(/más de \$0/) });
    expect(validarCotizacion({ lineas: [linea(1, '100000', '100.000.000')], costoManoObra: '0' }))
      .toEqual({ costoManoObra: expect.stringMatching(/2\.000\.000\.000/) });
    expect(validarCotizacion({ lineas: [linea(3, '1', '1')] }, { materialesActivos: new Set([1, 2]) }))
      .toEqual({ 'materiales.0.idMaterial': expect.stringMatching(/ya no está activo/) });
    expect(validarCotizacion({ lineas: [], costoManoObra: '45.000' })).toEqual({});
  });

  it('arma el cuerpo de la API y traduce sus errores por campo', () => {
    expect(cotizacionParaApi({ lineas: [linea(2, '0,75', '9.900', ' 120 × 60 cm ')], costoManoObra: '45.000', otrosCostos: '', observaciones: '  ' })).toEqual({
      materiales: [{ idMaterial: 2, medidaAplicada: '120 × 60 cm', cantidad: 0.75, costoUnitario: 9900 }], costoManoObra: 45000, otrosCostos: 0, observaciones: null,
    });
    expect(erroresCotizacionDesdeApi({ 'materiales[1].Cantidad': ['Muy grande'], CostoManoObra: ['Debe ser entero'], Total: ['Debe ser mayor que 0'] }))
      .toEqual({ 'materiales.1.cantidad': 'Muy grande', costoManoObra: 'Debe ser mayor que 0' });
    expect(formularioDesdeCotizacion(COTIZACION)).toMatchObject({
      lineas: [expect.objectContaining({ idMaterial: '1', cantidad: '1,5', costoUnitario: '18500', medidaAplicada: '120 × 60 cm' })],
      costoManoObra: '30000', otrosCostos: '', observaciones: '',
    });
  });

  it('los filtros del listado salen de la dirección; lo inválido se ignora y un rango al revés no viaja', () => {
    const parametros = new URLSearchParams('estado=aprobada&desde=2026-11-30&hasta=2026-11-01&orden=azar&pagina=2&otro=1&idCliente=9');
    const filtros = filtrosDesde(parametros, FILTROS_COTIZACIONES);
    expect(filtros).toMatchObject({ estado: 'aprobada', orden: 'fecha_desc', pagina: 2, idCliente: '9' });
    expect(parametrosInvalidos(parametros, FILTROS_COTIZACIONES).sort()).toEqual(['orden', 'otro']);
    expect(parametrosCotizaciones(filtros)).toMatchObject({ estado: 'aprobada', desde: undefined, hasta: undefined, idCliente: '9', pagina: 2, tamano: 20 });
    expect(paraDireccion({ ...filtros, pagina: 1, q: '' }, FILTROS_COTIZACIONES)).toEqual({ estado: 'aprobada', desde: '2026-11-30', hasta: '2026-11-01', idCliente: '9' });
  });

  it('el seguimiento del cliente dice cada paso con texto: cotización por revisar, fabricación, entrega', () => {
    expect(pasosSeguimiento({ estado: 'cotizado', cotizacion: { estado: 'pendiente' } }).map((p) => [p.id, p.estado]))
      .toEqual([['solicitud', 'hecho'], ['cotizacion', 'ahora'], ['fabricacion', 'despues'], ['entrega', 'despues']]);
    expect(pasosSeguimiento({ estado: 'aceptado', pedido: { estadoPedido: 'terminado', fechaEntregaEstimada: '2026-11-20' } }).at(-1))
      .toMatchObject({ estado: 'ahora', detalle: 'Listo para entregar' });
    expect(pasosSeguimiento({ estado: 'rechazado', motivoRechazo: 'No fabricamos eso' }).at(-1)).toMatchObject({ estado: 'detenido', detalle: 'No fabricamos eso' });
    expect(pasosSeguimiento({ estado: 'aceptado', pedido: { estadoPedido: 'anulado' } }).at(-1)).toMatchObject({ titulo: 'Pedido anulado', estado: 'detenido' });
    expect(pasosSeguimiento({ estado: 'aceptado', cotizacion: { estado: 'aprobada', pedidoEliminado: true }, pedido: null }).at(-1))
      .toMatchObject({ titulo: 'Pedido eliminado por el taller', estado: 'detenido' });
    expect(pasosSeguimiento({ estado: 'aceptado', cotizacion: { estado: 'aprobada' }, pedido: { estadoPedido: 'en_proceso', eliminado: true } }).at(-1))
      .toMatchObject({ titulo: 'Pedido eliminado por el taller', estado: 'detenido' });
  });
});

describe('editor de cotización', () => {
  beforeEach(() => entrarComo('operario'));

  it('el material sugiere su costo, el total va en vivo y se emite con el cuerpo de la API', async () => {
    solicitudes.obtener.mockResolvedValue(SOLICITUD);
    cotizaciones.emitir.mockResolvedValue({ ...COTIZACION, idCotizacion: 90, numero: '#90' });
    dibujar('/panel/solicitudes/123/cotizar', '/panel/solicitudes/:id/cotizar', <EditorCotizacion modo="cotizar" />);
    expect(await screen.findByRole('heading', { level: 1, name: 'Cotizar la solicitud #123' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Agregar material' }));
    const material = await screen.findByRole('combobox', { name: /^Material/ });
    // Con la máquina cargada (Vitest junto a Playwright, o los 2 núcleos de un ejecutor de CI con 55 archivos en paralelo), el foco y la lista
    // pueden tardar mucho más de 1 s: en la CI de la fase 16B una ejecución falló a los 3 s y otras tres pasaron con el mismo código.
    await waitFor(() => expect(material).toHaveFocus(), { timeout: 20000 });
    await waitFor(() => expect(within(material).getByRole('option', { name: 'Vinilo impreso' })).toBeInTheDocument(), { timeout: 20000 });
    expect(within(material).queryByRole('option', { name: 'Perfil de aluminio' })).not.toBeInTheDocument();
    escribir(material, '2');
    const costo = screen.getByRole('textbox', { name: /^Costo unitario/ });
    expect(costo).toHaveValue('9900');
    escribir(screen.getByRole('textbox', { name: /^Cantidad \(m2\)/ }), '2,5');
    escribir(screen.getByRole('textbox', { name: /^Mano de obra/ }), '30.000');
    // 2,5 × $9.900 = $24.750; + $30.000
    expect(screen.getByText('Total de la cotización').parentElement).toHaveTextContent('$54.750');
    const previa = screen.getByRole('complementary', { name: 'Vista previa para el cliente' });
    expect(within(previa).getByText('Vinilo impreso')).toBeInTheDocument();
    expect(within(previa).getByText('$54.750')).toBeInTheDocument();

    // Un costo escrito a mano no se pisa al cambiar de material.
    escribir(costo, '10.500');
    escribir(material, '1');
    expect(costo).toHaveValue('10.500');

    fireEvent.click(screen.getByRole('button', { name: 'Emitir cotización' }));
    await waitFor(() => expect(cotizaciones.emitir).toHaveBeenCalledWith(123, {
      materiales: [{ idMaterial: 1, medidaAplicada: null, cantidad: 2.5, costoUnitario: 10500 }], costoManoObra: 30000, otrosCostos: 0, observaciones: null,
    }, { clave: expect.stringMatching(/^[0-9a-f-]{36}$/) }));
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent('/panel/cotizaciones/90'));
  }, 60_000);

  it('sin materiales ni mano de obra no se emite: el error queda en la mano de obra, con el foco', async () => {
    solicitudes.obtener.mockResolvedValue(SOLICITUD);
    dibujar('/panel/solicitudes/123/cotizar', '/panel/solicitudes/:id/cotizar', <EditorCotizacion modo="cotizar" />);
    fireEvent.click(await screen.findByRole('button', { name: 'Emitir cotización' }));
    const manoObra = screen.getByRole('textbox', { name: /^Mano de obra/ });
    await waitFor(() => expect(manoObra).toHaveFocus(), { timeout: 8000 });
    expect(manoObra).toHaveAccessibleDescription(/más de \$0/);
    expect(cotizaciones.emitir).not.toHaveBeenCalled();
  }, 30_000);

  it('una solicitud que ya no está «Solicitada» no se cotiza', async () => {
    solicitudes.obtener.mockResolvedValue({ ...SOLICITUD, estado: 'cotizado' });
    dibujar('/panel/solicitudes/123/cotizar', '/panel/solicitudes/:id/cotizar', <EditorCotizacion modo="cotizar" />);
    expect(await screen.findByText('Esta solicitud ya no se puede cotizar')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Emitir cotización' })).not.toBeInTheDocument();
  });

  it('un 409 version_obsoleta conserva lo escrito, relee la versión actual y permite guardar sobre ella', async () => {
    const ACTUAL = { ...COTIZACION, costoManoObra: 35000, totalRedondeado: 62750, rowVersion: 'AAAAAAAACO2=' };
    cotizaciones.obtener.mockResolvedValueOnce(COTIZACION).mockResolvedValueOnce(ACTUAL);
    cotizaciones.editar
      .mockRejectedValueOnce(new ErrorApi({ estado: 409, codigo: 'version_obsoleta', mensaje: 'Otra persona modificó esta cotización.' }))
      .mockResolvedValueOnce(ACTUAL);
    dibujar('/panel/cotizaciones/45/editar', '/panel/cotizaciones/:id/editar', <EditorCotizacion modo="editar" />);
    expect(await screen.findByRole('heading', { level: 1, name: 'Editar la cotización #45' })).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^Cantidad/ })).toHaveValue('1,5');
    escribir(screen.getByRole('textbox', { name: /^Otros costos/ }), '7.000');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    await waitFor(() => expect(cotizaciones.editar).toHaveBeenCalledWith(45, expect.objectContaining({ otrosCostos: 7000, costoManoObra: 30000 }), 'AAAAAAAACOT='));
    expect(await screen.findByText('Otra persona modificó esta cotización mientras lo editabas')).toBeInTheDocument();
    // La versión actual se relee sin desmontar el formulario: lo escrito sigue ahí.
    const guardarSobre = await screen.findByRole('button', { name: 'Guardar sobre la versión actual' });
    await waitFor(() => expect(guardarSobre).toHaveFocus());
    expect(screen.getByText(/Versión actual: total \$62\.750/)).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^Otros costos/ })).toHaveValue('7.000');
    expect(cotizaciones.obtener).toHaveBeenCalledTimes(2);
    // «Guardar cambios» no reenvía sin decidir; «Guardar sobre la versión actual» usa la versión releída.
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    expect(cotizaciones.editar).toHaveBeenCalledTimes(1);
    fireEvent.click(guardarSobre);
    await waitFor(() => expect(cotizaciones.editar).toHaveBeenLastCalledWith(45, expect.objectContaining({ otrosCostos: 7000 }), 'AAAAAAAACO2='));
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent('/panel/cotizaciones/45'));
  });

  it('tras un conflicto, «Descartar mis cambios» carga la versión actual en el formulario', async () => {
    const ACTUAL = { ...COTIZACION, otrosCostos: 9000, rowVersion: 'AAAAAAAACO2=' };
    cotizaciones.obtener.mockResolvedValueOnce(COTIZACION).mockResolvedValueOnce(ACTUAL);
    cotizaciones.editar.mockRejectedValueOnce(new ErrorApi({ estado: 409, codigo: 'version_obsoleta', mensaje: 'x' }));
    dibujar('/panel/cotizaciones/45/editar', '/panel/cotizaciones/:id/editar', <EditorCotizacion modo="editar" />);
    escribir(await screen.findByRole('textbox', { name: /^Otros costos/ }), '7.000');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Descartar mis cambios y ver la versión actual' }));
    await waitFor(() => expect(screen.getByRole('textbox', { name: /^Otros costos/ })).toHaveValue('9000'));
    expect(screen.queryByText(/mientras lo editabas/)).not.toBeInTheDocument();
  });

  it('si la versión actual ya fue respondida, no ofrece guardar sobre ella', async () => {
    cotizaciones.obtener.mockResolvedValueOnce(COTIZACION).mockResolvedValueOnce({ ...COTIZACION, estado: 'aprobada', rowVersion: 'X' });
    cotizaciones.editar.mockRejectedValueOnce(new ErrorApi({ estado: 409, codigo: 'version_obsoleta', mensaje: 'x' }));
    dibujar('/panel/cotizaciones/45/editar', '/panel/cotizaciones/:id/editar', <EditorCotizacion modo="editar" />);
    fireEvent.click(await screen.findByRole('button', { name: 'Guardar cambios' }));
    expect(await screen.findByText(/Ahora está «Aprobada»/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Guardar sobre la versión actual' })).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Descartar mis cambios y ver la versión actual' })).toHaveFocus());
  });

  it('si se pierde la respuesta al emitir, revisa la solicitud: si quedó emitida, abre la cotización', async () => {
    solicitudes.obtener
      .mockResolvedValueOnce({ ...SOLICITUD, cotizaciones: [{ idCotizacion: 40, numero: '#40', estado: 'rechazada' }] })
      .mockResolvedValueOnce({ ...SOLICITUD, estado: 'cotizado', cotizaciones: [{ idCotizacion: 91, numero: '#91', estado: 'pendiente' }, { idCotizacion: 40, estado: 'rechazada' }] });
    cotizaciones.emitir.mockRejectedValue(new ErrorApi({ estado: 0, codigo: 'sin_conexion', mensaje: 'Sin conexión' }));
    dibujar('/panel/solicitudes/123/cotizar', '/panel/solicitudes/:id/cotizar', <EditorCotizacion modo="cotizar" />);
    escribir(await screen.findByRole('textbox', { name: /^Mano de obra/ }), '30.000');
    fireEvent.click(screen.getByRole('button', { name: 'Emitir cotización' }));
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent('/panel/cotizaciones/91'));
    expect(cotizaciones.emitir).toHaveBeenCalledTimes(1);
  });

  it('si se pierde la respuesta al emitir y no aparece, lo dice y deja reintentar; si no se pudo revisar, bloquea hasta revisar', async () => {
    solicitudes.obtener
      .mockResolvedValueOnce(SOLICITUD)
      .mockResolvedValueOnce({ ...SOLICITUD, cotizaciones: [] })
      .mockRejectedValueOnce(new ErrorApi({ estado: 0, codigo: 'sin_conexion', mensaje: 'x' }))
      .mockResolvedValueOnce({ ...SOLICITUD, cotizaciones: [] });
    cotizaciones.emitir.mockRejectedValue(new ErrorApi({ estado: 0, codigo: 'tiempo_agotado', mensaje: 'x' }));
    dibujar('/panel/solicitudes/123/cotizar', '/panel/solicitudes/:id/cotizar', <EditorCotizacion modo="cotizar" />);
    escribir(await screen.findByRole('textbox', { name: /^Mano de obra/ }), '30.000');
    fireEvent.click(screen.getByRole('button', { name: 'Emitir cotización' }));
    expect(await screen.findByText(/no supimos si se emitió\. Revisamos la solicitud y no aparece: puedes intentarlo de nuevo/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Emitir cotización' }));
    expect(await screen.findByText(/Tampoco pudimos revisar la solicitud/)).toBeInTheDocument();
    expect(cotizaciones.emitir).toHaveBeenCalledTimes(2);
    const emitir = screen.getByRole('button', { name: 'Emitir cotización' });
    expect(emitir).toHaveAttribute('aria-disabled', 'true');
    fireEvent.click(emitir);
    expect(cotizaciones.emitir).toHaveBeenCalledTimes(2);
    fireEvent.click(screen.getByRole('button', { name: 'Revisar de nuevo' }));
    expect(await screen.findByText(/Revisamos la solicitud y no aparece/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Emitir cotización' })).not.toHaveAttribute('aria-disabled');
  });

  it('un 400 con errores por línea los pone junto a su campo', async () => {
    cotizaciones.obtener.mockResolvedValue(COTIZACION);
    cotizaciones.editar.mockRejectedValue(new ErrorApi({ estado: 400, mensaje: 'Revisa', errores: { 'materiales[0].cantidad': ['La cantidad va hasta 100.000.'] } }));
    dibujar('/panel/cotizaciones/45/editar', '/panel/cotizaciones/:id/editar', <EditorCotizacion modo="editar" />);
    fireEvent.click(await screen.findByRole('button', { name: 'Guardar cambios' }));
    const cantidad = screen.getByRole('textbox', { name: /^Cantidad/ });
    await waitFor(() => expect(cantidad).toHaveAccessibleDescription('La cantidad va hasta 100.000.'));
    await waitFor(() => expect(cantidad).toHaveFocus());
  });
});

describe('respuesta del cliente registrada por el personal', () => {
  beforeEach(() => {
    entrarComo('diseñador');
    cotizaciones.obtener.mockResolvedValue(COTIZACION);
  });

  it('pide decisión y vía, y envía la versión vista; el motivo solo cuenta si se rechaza', async () => {
    cotizaciones.responder.mockResolvedValue({ ...COTIZACION, estado: 'rechazada' });
    dibujar('/panel/cotizaciones/45', '/panel/cotizaciones/:id', <FichaCotizacion />);
    fireEvent.click(await screen.findByRole('button', { name: 'Registrar respuesta del cliente' }));
    const dialogo = screen.getByRole('dialog', { name: /Respuesta del cliente a la cotización #45/ });
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar respuesta' }));
    expect(within(dialogo).getByText('Elige si el cliente la aprobó o la rechazó.')).toBeInTheDocument();
    expect(within(dialogo).getByText('Elige por dónde respondió el cliente.')).toBeInTheDocument();
    expect(within(dialogo).getByRole('radio', { name: 'Teléfono' })).toHaveAttribute('aria-invalid', 'true');
    expect(cotizaciones.responder).not.toHaveBeenCalled();

    fireEvent.click(within(dialogo).getByRole('radio', { name: /La rechazó/ }));
    fireEvent.click(within(dialogo).getByRole('radio', { name: 'Teléfono' }));
    escribir(within(dialogo).getByRole('textbox', { name: /Motivo del rechazo/ }), 'Le pareció caro');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar respuesta' }));
    await waitFor(() => expect(cotizaciones.responder).toHaveBeenCalledWith(45, { decision: 'rechazada', via: 'telefono', motivo: 'Le pareció caro' }, 'AAAAAAAACOT='));
  });

  it('un 409 al responder recarga la cotización y lo explica en el diálogo', async () => {
    cotizaciones.responder.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'transicion_invalida', mensaje: 'La cotización ya fue respondida.' }));
    dibujar('/panel/cotizaciones/45', '/panel/cotizaciones/:id', <FichaCotizacion />);
    fireEvent.click(await screen.findByRole('button', { name: 'Registrar respuesta del cliente' }));
    const dialogo = screen.getByRole('dialog');
    fireEvent.click(within(dialogo).getByRole('radio', { name: /La aprobó/ }));
    fireEvent.click(within(dialogo).getByRole('radio', { name: 'WhatsApp' }));
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar respuesta' }));
    expect(await within(dialogo).findByText('La cotización ya fue respondida.')).toBeInTheDocument();
    await waitFor(() => expect(cotizaciones.obtener).toHaveBeenCalledTimes(2));
  });

  it('una aprobada sin pedido ofrece «Crear pedido»; con pedido, el enlace al pedido', async () => {
    cotizaciones.obtener.mockResolvedValue({ ...COTIZACION, estado: 'aprobada' });
    const { unmount } = dibujar('/panel/cotizaciones/45', '/panel/cotizaciones/:id', <FichaCotizacion />);
    expect(await screen.findByRole('link', { name: 'Crear pedido' })).toHaveAttribute('href', '/panel/cotizaciones/45/pedido');
    expect(screen.queryByRole('button', { name: 'Registrar respuesta del cliente' })).not.toBeInTheDocument();
    unmount();
    cotizaciones.obtener.mockResolvedValue({ ...COTIZACION, estado: 'aprobada', idPedido: 7 });
    dibujar('/panel/cotizaciones/45', '/panel/cotizaciones/:id', <FichaCotizacion />);
    expect(await screen.findByRole('link', { name: 'Ver el pedido' })).toHaveAttribute('href', '/panel/pedidos/7');
    expect(screen.queryByRole('link', { name: 'Crear pedido' })).not.toBeInTheDocument();
  });

  it('una cotización cuyo pedido se eliminó no ofrece crear otro: lo dice', async () => {
    cotizaciones.obtener.mockResolvedValue({ ...COTIZACION, estado: 'aprobada', idPedido: 7, pedidoEliminado: true });
    dibujar('/panel/cotizaciones/45', '/panel/cotizaciones/:id', <FichaCotizacion />);
    expect(await screen.findByText(/fue eliminado/)).toHaveTextContent('El pedido #7 fue eliminado.');
    expect(screen.queryByRole('link', { name: 'Crear pedido' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Ver el pedido' })).not.toBeInTheDocument();
  });

  it('la sección de la solicitud usa sus cotizaciones: la vigente con sus acciones y las anteriores con su pedido', async () => {
    cotizaciones.obtener.mockResolvedValue(COTIZACION);
    const solicitud = {
      ...SOLICITUD, estado: 'cotizado', cotizaciones: [
        { idCotizacion: 40, numero: '#40', fechaChile: '2026-11-10T10:00:00', total: 50000, estado: 'rechazada', idPedido: null },
        { idCotizacion: 45, numero: '#45', fechaChile: '2026-11-14T11:00:00', total: 57750, estado: 'pendiente', idPedido: null },
      ],
    };
    dibujar('/panel/solicitudes/123', '/panel/solicitudes/:id', <SeccionCotizacion solicitud={solicitud} alCambiar={() => {}} />);
    expect(await screen.findByRole('button', { name: 'Registrar respuesta del cliente' })).toBeInTheDocument();
    expect(cotizaciones.obtener).toHaveBeenCalledWith(45);
    expect(screen.getByRole('link', { name: 'Cotización #45' })).toHaveAttribute('href', '/panel/cotizaciones/45');
    expect(screen.getByRole('link', { name: '#40' })).toHaveAttribute('href', '/panel/cotizaciones/40');
    expect(screen.queryByRole('link', { name: 'Cotizar' })).not.toBeInTheDocument();
    expect(cotizaciones.listar).not.toHaveBeenCalled();
  });
});

describe('seguimiento del cliente: aprobar o rechazar su cotización', () => {
  const MI_SOLICITUD = {
    ...SOLICITUD, estado: 'cotizado', cotizacion: { idCotizacion: 45, numero: '#45', total: 57750, estado: 'pendiente' }, pedido: null,
  };

  beforeEach(() => {
    entrarComo(undefined, 'cliente');
    misSolicitudes.obtener.mockResolvedValue(MI_SOLICITUD);
    misCotizaciones.obtener.mockResolvedValue({ ...COTIZACION, elaboradaPor: { nombre: 'Marta' } });
  });

  const abrir = () => dibujar('/cuenta/solicitudes/123', '/cuenta/solicitudes/:id', <MiSolicitud />);

  it('aprobar pide confirmar «Al aprobar, el taller comienza tu trabajo» y envía la versión vista', async () => {
    misCotizaciones.responder.mockResolvedValue({ ...COTIZACION, estado: 'aprobada' });
    abrir();
    const tarjeta = await screen.findByRole('region', { name: 'Tu cotización' });
    expect(await within(tarjeta).findByText('$57.750')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Seguimiento' })).toHaveTextContent('En curso · Lista para que la revises');
    fireEvent.click(within(tarjeta).getByRole('button', { name: 'Aprobar' }));
    const dialogo = screen.getByRole('dialog', { name: '¿Aprobar la cotización #45?' });
    expect(dialogo).toHaveTextContent('Al aprobar, el taller comienza tu trabajo');
    await waitFor(() => expect(within(dialogo).getByRole('button', { name: 'Volver' })).toHaveFocus());
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Aprobar cotización' }));
    await waitFor(() => expect(misCotizaciones.responder).toHaveBeenCalledWith(45, { decision: 'aprobada' }, 'AAAAAAAACOT='));
    await waitFor(() => expect(misSolicitudes.obtener).toHaveBeenCalledTimes(2));
  });

  it('si la cotización cambió mientras la veía, se lo dice con palabras para el cliente', async () => {
    misCotizaciones.responder.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'version_obsoleta', mensaje: 'Otra persona modificó esta cotización.' }));
    abrir();
    const tarjeta = await screen.findByRole('region', { name: 'Tu cotización' });
    fireEvent.click(await within(tarjeta).findByRole('button', { name: 'Aprobar' }));
    const dialogo = screen.getByRole('dialog');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Aprobar cotización' }));
    expect(await within(dialogo).findByText('La cotización cambió. Te mostramos la versión actual.')).toBeInTheDocument();
    expect(within(dialogo).queryByText(/Otra persona/)).not.toBeInTheDocument();
    expect(within(dialogo).queryByText(/dueño/)).not.toBeInTheDocument();
  });

  it('tras aprobar, la cotización no vuelve al esqueleto y el foco va a su título', async () => {
    misCotizaciones.responder.mockResolvedValue({ ...COTIZACION, estado: 'aprobada' });
    abrir();
    const tarjeta = await screen.findByRole('region', { name: 'Tu cotización' });
    fireEvent.click(await within(tarjeta).findByRole('button', { name: 'Aprobar' }));
    misSolicitudes.obtener.mockResolvedValue({ ...MI_SOLICITUD, estado: 'aceptado', rowVersion: 'NUEVA', cotizacion: { ...MI_SOLICITUD.cotizacion, estado: 'aprobada' } });
    misCotizaciones.obtener.mockResolvedValue({ ...COTIZACION, estado: 'aprobada', respuesta: { fechaChile: '2026-11-14T12:00:00', via: 'cliente_web', motivo: null, registradaPor: null } });
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Aprobar cotización' }));
    await waitFor(() => expect(misSolicitudes.obtener).toHaveBeenCalledTimes(2));
    // Mientras recarga, la cotización sigue a la vista.
    expect(within(tarjeta).getByText('$57.750')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('heading', { level: 2, name: 'Tu cotización' })).toHaveFocus());
    expect(within(tarjeta).queryByRole('button', { name: 'Aprobar' })).not.toBeInTheDocument();
  });

  it('un pedido eliminado por el taller lo dice, sin estados, saldo ni acciones', async () => {
    misSolicitudes.obtener.mockResolvedValue({
      ...MI_SOLICITUD, estado: 'aceptado', cotizacion: { ...MI_SOLICITUD.cotizacion, estado: 'aprobada' },
      pedido: { idPedido: 7, numero: '#7', estadoPedido: 'anulado', estadoPago: 'no_pagado', saldoPendiente: 0, fechaEntregaEstimada: '2026-11-20', modalidadEntrega: 'retiro_local', cicloCerrado: false, eliminado: true },
    });
    misCotizaciones.obtener.mockResolvedValue({ ...COTIZACION, estado: 'aprobada' });
    abrir();
    const pedido = await screen.findByRole('region', { name: 'Tu pedido #7' });
    expect(pedido).toHaveTextContent('Este pedido fue eliminado por el taller');
    expect(pedido).not.toHaveTextContent('Saldo pendiente');
    expect(within(pedido).queryByRole('button')).not.toBeInTheDocument();
  });

  it('rechazar acepta un motivo opcional y no lo envía si queda vacío', async () => {
    misCotizaciones.responder.mockResolvedValue({ ...COTIZACION, estado: 'rechazada' });
    abrir();
    const tarjeta = await screen.findByRole('region', { name: 'Tu cotización' });
    fireEvent.click(await within(tarjeta).findByRole('button', { name: 'Rechazar' }));
    const dialogo = screen.getByRole('dialog', { name: '¿Rechazar la cotización #45?' });
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Rechazar cotización' }));
    await waitFor(() => expect(misCotizaciones.responder).toHaveBeenCalledWith(45, { decision: 'rechazada' }, 'AAAAAAAACOT='));
  });

  it('con una cotización ya respondida no ofrece aprobar ni rechazar, y muestra el pedido con su saldo', async () => {
    misSolicitudes.obtener.mockResolvedValue({
      ...MI_SOLICITUD, estado: 'aceptado', cotizacion: { ...MI_SOLICITUD.cotizacion, estado: 'aprobada' },
      pedido: { idPedido: 7, numero: '#7', estadoPedido: 'en_proceso', estadoPago: 'abonado', saldoPendiente: 17750, fechaEntregaEstimada: '2026-11-20', modalidadEntrega: 'retiro_local', cicloCerrado: false },
    });
    misCotizaciones.obtener.mockResolvedValue({ ...COTIZACION, estado: 'aprobada', respuesta: { fechaChile: '2026-11-14T12:00:00', via: 'cliente_web', motivo: null, registradaPor: null } });
    abrir();
    const pedido = await screen.findByRole('region', { name: 'Tu pedido #7' });
    expect(pedido).toHaveTextContent('En proceso');
    expect(pedido).toHaveTextContent('Abonado');
    expect(pedido).toHaveTextContent('$17.750');
    expect(pedido).toHaveTextContent('20-11-2026');
    expect(pedido).toHaveTextContent('Retiro en el local');
    const tarjeta = screen.getByRole('region', { name: 'Tu cotización' });
    expect(await within(tarjeta).findByText('La aprobaste')).toBeInTheDocument();
    expect(within(tarjeta).queryByRole('button', { name: 'Aprobar' })).not.toBeInTheDocument();
  });
});
