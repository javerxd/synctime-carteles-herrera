import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { ErrorApi } from '../api/cliente';
import { reportes } from '../api/reportes';
import { enlaceCobranza } from '../paginas/reportes/reglasReportes';
import { TablaPorCobrar } from '../paginas/indicadores/TablaPorCobrar';
import Reportes from '../paginas/reportes/Reportes';
import { almacenSesion } from '../sesion/sesion';

// Fase 13B (RFS10, RFU24 a RFU26, RFU36, S18, S24): la pantalla de reportes contra el contrato (docs/api/openapi.json), con
// documentos de ejemplo. Las cifras reales y los archivos se prueban en el backend (ReportesFase13aTests).

vi.mock('../api/reportes', () => ({
  reportes: { leer: vi.fn(), pedirExportacion: vi.fn(), estado: vi.fn(), descargar: vi.fn() },
}));

const col = (clave, titulo, tipo = 'texto', extra = {}) => ({ clave, titulo, tipo, suma: false, textoNulo: '', ...extra });
const TABLA_PEDIDOS = {
  clave: 'pedidos', titulo: 'Pedidos', principal: true, nota: 'Los pedidos del período, también los anulados.',
  exclusion: { columna: 4, valor: 'Anulado' },
  columnas: [
    col('id', 'Pedido N.º', 'entero'), col('cliente', 'Cliente'), col('fechaPedido', 'Fecha del pedido', 'fecha'), col('entregaEstimada', 'Entrega estimada', 'fecha'),
    col('estadoPedido', 'Estado del pedido'), col('estadoPago', 'Estado de pago'),
    col('monto', 'Monto', 'pesos', { suma: true }), col('abonado', 'Abonado', 'pesos', { suma: true }), col('saldo', 'Saldo', 'pesos', { suma: true }),
  ],
  filas: [
    [101, 'Ana Pérez', '2026-11-04', '2026-11-10', 'Entregado', 'Pagado total', 100000, 100000, 0],
    [102, 'Luis Soto', '2026-11-05', '2026-11-20', 'Anulado', 'No pagado', 50000, 0, 50000],
    [103, 'Ñandú Peña', '2026-11-06', '2026-11-12', 'En proceso', 'Abonado', 60000, 20000, 40000],
  ],
  totales: [null, null, null, null, null, null, 160000, 120000, 40000],
};
const TABLA_ENTREGAS = {
  clave: 'entregas', titulo: 'Entregas', principal: false, nota: null, exclusion: null, totales: [null, null, null, null, null],
  columnas: [col('id', 'Pedido N.º', 'entero'), col('cliente', 'Cliente'), col('estimada', 'Entrega estimada', 'fecha'), col('real', 'Entrega real', 'fecha'), col('aTiempo', 'A tiempo')],
  filas: [[101, 'Ana Pérez', '2026-11-10', '2026-11-09', 'Sí']],
};
const TABLA_SOLICITUDES = {
  clave: 'solicitudes', titulo: 'Solicitudes', principal: false, nota: null, exclusion: null, totales: [null, null, null, null, null],
  columnas: [col('id', 'Solicitud N.º', 'entero'), col('fecha', 'Fecha', 'fecha'), col('cliente', 'Cliente'), col('origen', 'Origen'), col('estado', 'Estado')],
  filas: [[7, '2026-11-03', 'Ana Pérez', 'En línea', 'Aceptado']],
};

const OPERATIVO = {
  reporte: 'operativo', titulo: 'Reporte operativo', emitidoEn: '2026-11-20T14:35:00-03:00',
  filtros: [{ clave: 'periodo', etiqueta: 'Período', valor: 'noviembre de 2026' }],
  tablas: [TABLA_PEDIDOS, TABLA_ENTREGAS, TABLA_SOLICITUDES], graficos: [], notas: [],
};

const GERENCIAL = {
  reporte: 'gerencial', titulo: 'Reporte gerencial', emitidoEn: '2026-11-20T14:35:00-03:00',
  filtros: [{ clave: 'periodo', etiqueta: 'Período', valor: 'noviembre de 2026' }, { clave: 'comparar', etiqueta: 'Comparado con', valor: 'octubre de 2026' }],
  tablas: [
    {
      clave: 'resumen', titulo: 'Resumen del período', principal: false, nota: null, exclusion: null, totales: [null, null, null, null],
      columnas: [col('concepto', 'Concepto'), col('actual', 'Período', 'pesos'), col('anterior', 'Período de comparación', 'pesos'), col('variacion', 'Variación', 'porcentaje', { textoNulo: '—' })],
      filas: [['Ingresos (pedidos entregados)', 180000, 120000, 50], ['Margen (ingresos con costo − materiales − otros costos)', 75000, 112498, -33.3]],
    },
    {
      clave: 'tramos', titulo: 'Ingresos y costos por tramo', principal: false, nota: 'Agrupado por semana.', exclusion: null, totales: [null, null, null, 180000, 55000],
      columnas: [col('tramo', 'Tramo'), col('desde', 'Desde', 'fecha'), col('hasta', 'Hasta', 'fecha'), col('ingresos', 'Ingresos', 'pesos', { suma: true }), col('costos', 'Costos', 'pesos', { suma: true })],
      filas: [['01-11', '2026-11-01', '2026-11-07', 0, 0], ['08-11', '2026-11-08', '2026-11-14', 180000, 55000]],
    },
    {
      clave: 'pedidos', titulo: 'Pedidos entregados', principal: true, nota: null, exclusion: null, totales: [null, null, null, 180000, 55000, 20000, 5000, 75000],
      columnas: [col('id', 'Pedido N.º', 'entero'), col('cliente', 'Cliente'), col('entrega', 'Entrega real', 'fecha'), col('ingreso', 'Ingreso', 'pesos', { suma: true }),
        col('costo', 'Costo total', 'pesos', { suma: true, textoNulo: 'Sin costo' }), col('materiales', 'Materiales', 'pesos', { suma: true, textoNulo: 'Sin costo' }),
        col('otros', 'Otros costos', 'pesos', { suma: true, textoNulo: 'Sin costo' }), col('margen', 'Margen', 'pesos', { suma: true, textoNulo: 'Sin costo' })],
      filas: [[101, 'Ana Pérez', '2026-11-09', 100000, 55000, 20000, 5000, 75000], [103, 'Luis Soto', '2026-11-14', 80000, null, null, null, null]],
    },
  ],
  graficos: [{ clave: 'tramos', titulo: 'Ingresos y costos por tramo', forma: 'barras', etiquetas: ['01-11', '08-11'], series: [{ nombre: 'Ingresos', valores: [0, 180000] }, { nombre: 'Costos', valores: [0, 55000] }] }],
  notas: ['Costos: los de la cotización aceptada de cada pedido.'],
};

const PARAMETRIZABLE = { ...OPERATIVO, reporte: 'parametrizable', titulo: 'Reporte parametrizable', tablas: [{ ...TABLA_PEDIDOS, titulo: 'Pedidos por cobrar', exclusion: null }] };

function Ruta() {
  const { pathname, search } = useLocation();
  return <output data-testid="ruta">{`${pathname}${search}`}</output>;
}

function entrarComo(rol) {
  act(() => almacenSesion.aplicar({ token: 'jwt', tipo: 'empleado', usuario: { idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', rol } }));
}

function dibujar(entrada = '/panel/reportes') {
  return render(
    <MemoryRouter initialEntries={[entrada]}>
      <Routes><Route path="/panel/reportes" element={<><Reportes /><Ruta /></>} /></Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date('2026-11-20T12:00:00-03:00'));
  vi.clearAllMocks();
  reportes.leer.mockImplementation(async (reporte) => ({ operativo: OPERATIVO, gerencial: GERENCIAL, parametrizable: PARAMETRIZABLE }[reporte]));
});
afterEach(() => {
  vi.useRealTimers();
  act(() => almacenSesion.cerrar());
});

describe('quién ve qué pestaña (RFU24, RFU25)', () => {
  it('el dueño ve las tres pestañas', async () => {
    entrarComo('dueño');
    dibujar();
    const pestanas = within(await screen.findByRole('tablist', { name: 'Tipo de reporte' })).getAllByRole('tab');
    expect(pestanas.map((p) => p.textContent)).toEqual(['Operativo', 'Gerencial', 'Parametrizable']);
  });

  it('el diseñador y el operario ven solo el operativo, y nunca piden el gerencial', async () => {
    entrarComo('diseñador');
    dibujar('/panel/reportes?reporte=gerencial');
    const pestanas = within(await screen.findByRole('tablist', { name: 'Tipo de reporte' })).getAllByRole('tab');
    expect(pestanas.map((p) => p.textContent)).toEqual(['Operativo']);
    await screen.findByRole('table', { name: 'Pedidos' });
    expect(reportes.leer).toHaveBeenCalledTimes(1);
    expect(reportes.leer.mock.calls[0][0]).toBe('operativo');
  });
});

describe('el reporte operativo', () => {
  it('lleva sus tres tablas, la de pedidos con el anulado marcado y fuera de los totales', async () => {
    entrarComo('dueño');
    dibujar();

    const pedidos = await screen.findByRole('table', { name: 'Pedidos' });
    expect(screen.getByRole('table', { name: 'Entregas' })).toBeInTheDocument();
    expect(screen.getByRole('table', { name: 'Solicitudes' })).toBeInTheDocument();
    expect(within(pedidos).getByText('Ñandú Peña')).toBeInTheDocument();
    expect(within(pedidos).getByText('04-11-2026')).toBeInTheDocument();
    // El anulado se ve marcado y avisa que no suma.
    expect(within(pedidos).getByText(/no suma en los totales/i)).toBeInTheDocument();

    const totales = screen.getByRole('list', { name: 'Totales de Pedidos' });
    expect(within(totales).getByText('Monto').nextSibling).toHaveTextContent(/160\.000/);
    expect(within(totales).getByText('Abonado').nextSibling).toHaveTextContent(/120\.000/);
    expect(within(totales).getByText('Saldo').nextSibling).toHaveTextContent(/40\.000/);
  });

  it('pide el reporte con los filtros de la dirección, solo los que el operativo admite', async () => {
    entrarComo('dueño');
    dibujar('/panel/reportes?anio=2026&mes=10&estadoPago=por_cobrar&cliente=7&comparar=ninguno');

    await screen.findByRole('table', { name: 'Pedidos' });
    const [reporte, filtros] = reportes.leer.mock.calls[0];
    expect(reporte).toBe('operativo');
    expect(filtros).toMatchObject({ anio: '2026', mes: '10', estadoPago: 'por_cobrar', cliente: '7' });
  });

  it('cambiar un filtro lo pone en la dirección y vuelve a pedir el reporte', async () => {
    entrarComo('dueño');
    dibujar();
    await screen.findByRole('table', { name: 'Pedidos' });

    fireEvent.click(screen.getByText('Filtrar pedidos'));
    fireEvent.click(screen.getByRole('button', { name: 'Por cobrar' }));

    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent('estadoPago=por_cobrar'));
    await waitFor(() => expect(reportes.leer).toHaveBeenCalledTimes(2));
    expect(reportes.leer.mock.calls[1][1]).toMatchObject({ estadoPago: 'por_cobrar' });
  });

  it('lleva la hoja de impresión: título, emisión y filtros aplicados, para el papel', async () => {
    entrarComo('dueño');
    dibujar();

    const hoja = await screen.findByRole('region', { name: 'Datos para imprimir', hidden: true });
    expect(within(hoja).getByRole('heading', { name: 'Reporte operativo' })).toBeInTheDocument();
    expect(within(hoja).getByText(/Emitido el 20-11-2026 14:35/)).toBeInTheDocument();
    expect(within(hoja).getByText(/noviembre de 2026/)).toBeInTheDocument();
  });
});

describe('el reporte gerencial', () => {
  it('muestra el resumen con su variación, los tramos, el detalle y el gráfico con su descripción', async () => {
    entrarComo('dueño');
    dibujar('/panel/reportes?reporte=gerencial');

    const resumen = await screen.findByRole('table', { name: 'Resumen del período' });
    expect(within(resumen).getByText('Margen (ingresos con costo − materiales − otros costos)')).toBeInTheDocument();
    expect(within(resumen).getByText('50,0 %')).toBeInTheDocument();
    expect(within(resumen).getByText('-33,3 %')).toBeInTheDocument();
    const detalle = screen.getByRole('table', { name: 'Pedidos entregados' });
    // El margen se puede verificar a ojo: materiales y otros costos (los dos que se descuentan) están a la vista, uno junto al otro.
    expect(within(detalle).getAllByRole('columnheader').map((h) => h.textContent))
      .toEqual(['Pedido N.º', 'Cliente', 'Entrega real', 'Ingreso', 'Costo total', 'Materiales', 'Otros costos', 'Margen']);
    expect(within(detalle).getAllByText('$5.000').length).toBeGreaterThan(0);
    // El pedido sin cotización no tiene costo, ni materiales, ni otros costos, ni margen: no se inventa uno.
    expect(within(detalle).getAllByText('Sin costo')).toHaveLength(4);
    expect(screen.getByRole('img', { name: /Ingresos y costos por tramo/ })).toBeInTheDocument();
    expect(screen.getByText('Costos: los de la cotización aceptada de cada pedido.')).toBeInTheDocument();
  });

  it('pide «comparar» (solo el gerencial lo tiene)', async () => {
    entrarComo('dueño');
    dibujar('/panel/reportes?reporte=gerencial&comparar=anio_anterior');

    await screen.findByRole('table', { name: 'Resumen del período' });
    expect(reportes.leer.mock.calls[0][0]).toBe('gerencial');
    expect(reportes.leer.mock.calls[0][1]).toMatchObject({ comparar: 'anio_anterior' });
  });

  it('cambiar de pestaña cambia el reporte en la dirección y lo pide', async () => {
    entrarComo('dueño');
    dibujar();
    await screen.findByRole('table', { name: 'Pedidos' });

    fireEvent.click(screen.getByRole('tab', { name: 'Gerencial' }));

    await screen.findByRole('table', { name: 'Resumen del período' });
    expect(screen.getByTestId('ruta')).toHaveTextContent('reporte=gerencial');
  });
});

describe('el parametrizable y la cobranza (RFU36)', () => {
  it('llega ya filtrado por cobrar desde el atajo del dashboard', async () => {
    entrarComo('dueño');
    dibujar('/panel/reportes?reporte=parametrizable&estadoPago=por_cobrar&alcanceSaldo=vigente&cliente=7');

    await screen.findByRole('table', { name: 'Pedidos por cobrar' });
    const [reporte, filtros] = reportes.leer.mock.calls[0];
    expect(reporte).toBe('parametrizable');
    expect(filtros).toMatchObject({ estadoPago: 'por_cobrar', alcanceSaldo: 'vigente', cliente: '7' });
  });

  it('el dashboard enlaza a la cobranza con el período, el alcance y el cliente que tiene', () => {
    const enlace = enlaceCobranza({ periodo: 'mes', anio: '2026', mes: '10', alcanceSaldo: 'vigente', cliente: '7', cobro: '' });
    const datos = {
      alcance: 'vigente', periodo: { etiqueta: 'octubre de 2026' }, total: 0, totalSaldo: 0, pagina: 1, totalPaginas: 0, elementos: [],
    };
    render(
      <MemoryRouter>
        <TablaPorCobrar datos={datos} actualizando={false} cobro="" alQuitarCobro={() => {}} alCambiarAlcance={() => {}} alCambiarPagina={() => {}} enlaceExportar={enlace} />
      </MemoryRouter>,
    );
    const atajo = screen.getByRole('link', { name: 'Exportar cobranza' });
    expect(atajo).toHaveAttribute('href', enlace);
    expect(enlace).toContain('reporte=parametrizable');
    expect(enlace).toContain('estadoPago=por_cobrar');
  });
});

describe('carga, vacío y error por bloque (RNFP23)', () => {
  it('mientras carga: un aviso de estado, sin tablas', async () => {
    entrarComo('dueño');
    reportes.leer.mockImplementation(() => new Promise(() => {}));
    dibujar();

    expect(await screen.findByText('Cargando el reporte…', { selector: '[role="status"]' })).toBeInTheDocument();
    expect(screen.queryByRole('table')).toBeNull();
  });

  it('un reporte sin filas lo dice con un estado vacío, no con tablas en blanco', async () => {
    entrarComo('dueño');
    reportes.leer.mockResolvedValue({ ...OPERATIVO, tablas: OPERATIVO.tablas.map((t) => ({ ...t, filas: [], totales: t.totales.map((x) => (x === null ? null : 0)) })) });
    dibujar();

    expect(await screen.findByText('No hay datos para estos filtros')).toBeInTheDocument();
    expect(screen.queryByRole('table')).toBeNull();
  });

  it('un reporte de decenas de miles de filas pinta las primeras 1.000 y lo dice; los totales siguen siendo los de todas', async () => {
    entrarComo('dueño');
    const filas = Array.from({ length: 1500 }, (_, i) => [i + 1, `Cliente ${i + 1}`, '2026-11-04', '2026-11-10', 'Entregado', 'Pagado total', 1000, 1000, 0]);
    reportes.leer.mockResolvedValue({ ...OPERATIVO, tablas: [{ ...TABLA_PEDIDOS, filas, totales: [null, null, null, null, null, null, 1500000, 1500000, 0] }] });
    dibujar();

    expect(await screen.findByText(/Se muestran las primeras 1000 de 1500 filas/)).toBeInTheDocument();
    const tabla = screen.getByRole('table', { name: 'Pedidos' });
    expect(within(tabla).getAllByRole('row')).toHaveLength(1001);   // encabezado + 1.000
    expect(screen.getByRole('list', { name: 'Totales de Pedidos' })).toHaveTextContent('1.500.000');
  });

  it('un error muestra el motivo con «Reintentar», y reintentar vuelve a pedir', async () => {
    entrarComo('dueño');
    reportes.leer.mockRejectedValueOnce(new ErrorApi({ estado: 500, codigo: 'error_interno', mensaje: 'El servidor tuvo un problema.' }));
    dibujar();

    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent('El servidor tuvo un problema.');
    fireEvent.click(within(alerta).getByRole('button', { name: 'Reintentar' }));

    await screen.findByRole('table', { name: 'Pedidos' });
    expect(reportes.leer).toHaveBeenCalledTimes(2);
  });

  it('un reporte de más de 50.000 filas muestra el mensaje de la API («acota el rango»)', async () => {
    entrarComo('dueño');
    reportes.leer.mockRejectedValue(new ErrorApi({ estado: 400, codigo: 'reporte_muy_grande', mensaje: 'El reporte tiene más de 50.000 filas. Acota el rango de fechas o agrega filtros y vuelve a intentarlo.' }));
    dibujar();

    expect(await screen.findByRole('alert')).toHaveTextContent('Acota el rango de fechas');
  });
});

describe('exportar desde la pantalla (RFU26, RNFP18)', () => {
  it('los tres botones piden el archivo con los filtros de pantalla', async () => {
    entrarComo('dueño');
    reportes.pedirExportacion.mockResolvedValue({ id: 'abc', estado: 'pendiente' });
    reportes.estado.mockImplementation(() => new Promise(() => {}));
    dibujar('/panel/reportes?anio=2026&mes=11');
    await screen.findByRole('table', { name: 'Pedidos' });

    fireEvent.click(screen.getByRole('button', { name: 'Exportar a Excel' }));

    await waitFor(() => expect(reportes.pedirExportacion).toHaveBeenCalledTimes(1));
    expect(reportes.pedirExportacion).toHaveBeenCalledWith('operativo', 'xlsx', { periodo: 'mes', anio: '2026', mes: '11' });
  });

  it('mientras se prepara, la pantalla sigue usable: se puede cambiar de pestaña', async () => {
    entrarComo('dueño');
    reportes.pedirExportacion.mockResolvedValue({ id: 'abc', estado: 'pendiente' });
    reportes.estado.mockImplementation(() => new Promise(() => {}));
    dibujar();
    await screen.findByRole('table', { name: 'Pedidos' });

    fireEvent.click(screen.getByRole('button', { name: 'Exportar a PDF' }));
    await screen.findByRole('button', { name: /Preparando/ });
    fireEvent.click(screen.getByRole('tab', { name: 'Gerencial' }));

    await screen.findByRole('table', { name: 'Resumen del período' });
    expect(screen.getByRole('button', { name: /Preparando/ })).toHaveAttribute('aria-disabled', 'true');
  });
});
