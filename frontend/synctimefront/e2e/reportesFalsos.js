// Respuestas simuladas de GET /api/v1/reportes/* para Playwright (fase 13B). Cumplen el contrato de docs/api/openapi.json con
// cifras fijas; las cifras reales y los archivos se prueban en el backend (ReportesFase13aTests, ArchivosDeReportesTests).

const col = (clave, titulo, tipo = 'texto', extra = {}) => ({ clave, titulo, tipo, suma: false, textoNulo: '', ...extra });

const PEDIDOS = {
  clave: 'pedidos', titulo: 'Pedidos', principal: true, nota: 'Los pedidos del período, también los anulados, que se muestran marcados y no suman en los totales.',
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

const ENTREGAS = {
  clave: 'entregas', titulo: 'Entregas', principal: false, nota: 'Pedidos entregados dentro del período, por su fecha de entrega real.', exclusion: null,
  totales: [null, null, null, null, null],
  columnas: [col('id', 'Pedido N.º', 'entero'), col('cliente', 'Cliente'), col('estimada', 'Entrega estimada', 'fecha'), col('real', 'Entrega real', 'fecha'), col('aTiempo', 'A tiempo')],
  filas: [[101, 'Ana Pérez', '2026-11-10', '2026-11-09', 'Sí']],
};

const SOLICITUDES = {
  clave: 'solicitudes', titulo: 'Solicitudes', principal: false, nota: null, exclusion: null, totales: [null, null, null, null, null],
  columnas: [col('id', 'Solicitud N.º', 'entero'), col('fecha', 'Fecha', 'fecha'), col('cliente', 'Cliente'), col('origen', 'Origen'), col('estado', 'Estado')],
  filas: [[7, '2026-11-03', 'Ana Pérez', 'En línea', 'Aceptado'], [8, '2026-11-05', 'Luis Soto', 'Presencial', 'Solicitado']],
};

const EMISION = '2026-11-20T14:35:00-03:00';

export const OPERATIVO = {
  reporte: 'operativo', titulo: 'Reporte operativo', emitidoEn: EMISION,
  filtros: [{ clave: 'periodo', etiqueta: 'Período', valor: 'noviembre de 2026' }],
  tablas: [PEDIDOS, ENTREGAS, SOLICITUDES], graficos: [], notas: [],
};

export const GERENCIAL = {
  reporte: 'gerencial', titulo: 'Reporte gerencial', emitidoEn: EMISION,
  filtros: [{ clave: 'periodo', etiqueta: 'Período', valor: 'noviembre de 2026' }, { clave: 'comparar', etiqueta: 'Comparado con', valor: 'octubre de 2026' }],
  tablas: [
    {
      clave: 'resumen', titulo: 'Resumen del período', principal: false, nota: null, exclusion: null, totales: [null, null, null, null],
      columnas: [col('concepto', 'Concepto'), col('actual', 'Período', 'pesos'), col('anterior', 'Período de comparación', 'pesos'), col('variacion', 'Variación', 'porcentaje', { textoNulo: '—' })],
      filas: [
        ['Ingresos (pedidos entregados)', 180000, 120000, 50], ['Ingresos de los pedidos con costo', 100000, 120000, -16.7], ['Costo de materiales', 20000, 7502, 166.6],
        ['Costo de mano de obra', 30000, 40000, -25], ['Otros costos', 5000, 0, null], ['Costos totales', 55000, 47502, 15.8],
        ['Margen (ingresos con costo − materiales − otros costos)', 75000, 112498, -33.3],
      ],
    },
    {
      clave: 'tramos', titulo: 'Ingresos y costos por tramo', principal: false, nota: 'Agrupado por semana (según la extensión del período).', exclusion: null, totales: [null, null, null, 180000, 55000],
      columnas: [col('tramo', 'Tramo'), col('desde', 'Desde', 'fecha'), col('hasta', 'Hasta', 'fecha'), col('ingresos', 'Ingresos', 'pesos', { suma: true }), col('costos', 'Costos', 'pesos', { suma: true })],
      filas: [['01-11', '2026-11-01', '2026-11-07', 0, 0], ['08-11', '2026-11-08', '2026-11-14', 180000, 55000], ['15-11', '2026-11-15', '2026-11-21', 0, 0]],
    },
    {
      clave: 'tipos', titulo: 'Ingresos por tipo de letrero', principal: false, nota: 'El monto de cada pedido se reparte entre sus ítems según su cantidad.', exclusion: null, totales: [null, 180000, 4],
      columnas: [col('tipo', 'Tipo de letrero'), col('ingresos', 'Ingresos', 'pesos', { suma: true }), col('unidades', 'Unidades', 'entero', { suma: true }), col('pedidos', 'Pedidos', 'entero')],
      filas: [['Letrero luminoso', 120000, 2, 2], ['Gráfica para vitrina', 60000, 2, 1]],
    },
    {
      clave: 'pedidos', titulo: 'Pedidos entregados', principal: true, nota: null, exclusion: null, totales: [null, null, null, 180000, 55000, 20000, 5000, 75000],
      columnas: [col('id', 'Pedido N.º', 'entero'), col('cliente', 'Cliente'), col('entrega', 'Entrega real', 'fecha'), col('ingreso', 'Ingreso', 'pesos', { suma: true }),
        col('costo', 'Costo total', 'pesos', { suma: true, textoNulo: 'Sin costo' }), col('materiales', 'Materiales', 'pesos', { suma: true, textoNulo: 'Sin costo' }),
        col('otros', 'Otros costos', 'pesos', { suma: true, textoNulo: 'Sin costo' }), col('margen', 'Margen', 'pesos', { suma: true, textoNulo: 'Sin costo' })],
      filas: [[101, 'Ana Pérez', '2026-11-09', 100000, 55000, 20000, 5000, 75000], [103, 'Luis Soto', '2026-11-14', 80000, null, null, null, null]],
    },
  ],
  graficos: [
    { clave: 'tramos', titulo: 'Ingresos y costos por tramo', forma: 'barras', etiquetas: ['01-11', '08-11', '15-11'], series: [{ nombre: 'Ingresos', valores: [0, 180000, 0] }, { nombre: 'Costos', valores: [0, 55000, 0] }] },
    { clave: 'tipos', titulo: 'Ingresos por tipo de letrero', forma: 'barras-horizontales', etiquetas: ['Letrero luminoso', 'Gráfica para vitrina'], series: [{ nombre: 'Ingresos', valores: [120000, 60000] }] },
  ],
  notas: ['Costos: los de la cotización aceptada de cada pedido. Un pedido sin cotización aparece como «Sin costo» y no entra en el margen.'],
};

export const PARAMETRIZABLE = {
  ...OPERATIVO, reporte: 'parametrizable', titulo: 'Reporte parametrizable',
  filtros: [{ clave: 'periodo', etiqueta: 'Período', valor: 'noviembre de 2026' }, { clave: 'estadoPago', etiqueta: 'Estado de pago', valor: 'Por cobrar' }],
  tablas: [{ ...PEDIDOS, titulo: 'Pedidos por cobrar', exclusion: null, nota: null, filas: PEDIDOS.filas.filter((f) => f[5] !== 'Pagado total' && f[4] !== 'Anulado'), totales: [null, null, null, null, null, null, 60000, 20000, 40000] }],
  notas: ['Los pedidos anulados no se incluyen.'],
};

export const DOCUMENTOS = { operativo: OPERATIVO, gerencial: GERENCIAL, parametrizable: PARAMETRIZABLE };

/** Un trabajo de exportación, como lo devuelve la API. */
export const trabajo = (estado, extra = {}) => ({
  id: '3f2b6c1e-0000-4000-8000-000000000001', reporte: 'operativo', formato: 'xlsx', estado, fechaPedido: '2026-11-20T14:35:00-03:00', fechaTermino: null,
  fechaVencimiento: null, filas: null, tamanoBytes: null, nombreArchivo: null, error: null, vencido: false, ...extra,
});
