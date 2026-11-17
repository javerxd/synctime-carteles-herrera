// Respuestas simuladas de GET /api/v1/indicadores/* para Vitest y Playwright (fase 12B). Cumplen el contrato de
// docs/api/openapi.json con cifras fijas y reaccionan solo a lo que las pruebas de pantalla ejercitan (período, comparación,
// alcance del saldo, estado de pago de la tabla y agrupación de la demanda). Las cifras reales se prueban en el backend
// (IndicadoresFase12bTests), contra SQL Server y con cuentas hechas a mano.

const leer = (params, clave) => (params instanceof URLSearchParams ? params.get(clave) : params?.[clave]) ?? null;

const MES = { tipo: 'mes', desde: '2026-11-01', hasta: '2026-11-30', etiqueta: 'noviembre de 2026' };
const OCTUBRE = { tipo: 'mes', desde: '2026-10-01', hasta: '2026-10-31', etiqueta: 'octubre de 2026' };
const ANIO = { tipo: 'anio', desde: '2026-01-01', hasta: '2026-12-31', etiqueta: '2026' };
const ANIO_ANTERIOR = { tipo: 'anio', desde: '2025-01-01', hasta: '2025-12-31', etiqueta: '2025' };

function periodos(params) {
  const periodo = leer(params, 'periodo') === 'anio' ? ANIO : MES;
  const comparar = leer(params, 'comparar') ?? 'anterior';
  return { periodo, comparacion: comparar === 'ninguno' ? null : periodo === ANIO ? ANIO_ANTERIOR : OCTUBRE };
}

const tarjeta = (clave, unidad, tipoVariacion, valor, anterior, variacion, detalle) => ({
  clave, unidad, valor, anterior, variacion, tipoVariacion, ...(detalle ? { detalle } : {}),
});

function resumen(params) {
  const { periodo, comparacion } = periodos(params);
  const sin = comparacion === null;
  const vigente = leer(params, 'alcanceSaldo') === 'vigente';
  return {
    periodo,
    comparacion,
    tarjetas: [
      tarjeta('ingresos', 'pesos', 'porcentaje', 2_384_000, sin ? null : 3_717_000, sin ? null : -35.9),
      tarjeta('pedidos', 'cantidad', 'porcentaje', 6, sin ? null : 10, sin ? null : -40, {
        porEstado: [{ estado: 'aceptado', cantidad: 1 }, { estado: 'en_proceso', cantidad: 3 }, { estado: 'terminado', cantidad: 0 }, { estado: 'entregado', cantidad: 2 }],
      }),
      tarjeta('origen', 'cantidad', 'porcentaje', 3, sin ? null : 5, sin ? null : -40, { presenciales: 3, porcentajeEnLinea: 50 }),
      tarjeta('concretadas', 'cantidad', 'porcentaje', 2, sin ? null : 6, sin ? null : -66.7, { solicitudesEnLinea: 3 }),
      tarjeta('conversion', 'porcentaje', 'puntos', 66.7, sin ? null : 66.7, sin ? null : 0),
      tarjeta('ticketPromedio', 'pesos', 'porcentaje', 277_333, sin ? null : 384_000, sin ? null : -27.8),
      tarjeta('entregasATiempo', 'porcentaje', 'puntos', 50, sin ? null : 22.2, sin ? null : 27.8),
    ],
    saldo: vigente
      ? { alcance: 'vigente', valor: 12_831_000, cantidad: 14, anterior: null, variacion: null }
      : { alcance: 'periodo', valor: 1_430_000, cantidad: 6, anterior: sin ? null : 543_000, variacion: sin ? null : 163.4 },
  };
}

function evolucion(params) {
  const { periodo, comparacion } = periodos(params);
  const tramo = (i, ingresos, pedidos, ic, pc) => ({
    desde: `2026-11-${String(1 + i * 7).padStart(2, '0')}`, hasta: `2026-11-${String(Math.min(7 + i * 7, 30)).padStart(2, '0')}`,
    etiqueta: `${String(1 + i * 7).padStart(2, '0')}-11`, ingresos, pedidos,
    ingresosComparacion: comparacion ? ic : null, pedidosComparacion: comparacion ? pc : null,
  });
  return {
    periodo, comparacion, granularidad: 'semana',
    serie: [tramo(0, 1_182_000, 3, 900_000, 2), tramo(1, 902_000, 2, 1_200_000, 3), tramo(2, 300_000, 1, 1_300_000, 3), tramo(3, 0, 0, 317_000, 2), tramo(4, 0, 0, null, null)],
  };
}

function cobranza(params) {
  const { periodo } = periodos(params);
  const vigente = leer(params, 'alcanceSaldo') === 'vigente';
  return {
    periodo,
    alcance: vigente ? 'vigente' : 'periodo',
    segmentos: vigente
      ? [{ estadoPago: 'no_pagado', cantidad: 6, saldo: 6_100_000 }, { estadoPago: 'abonado', cantidad: 8, saldo: 6_731_000 }, { estadoPago: 'pagado_total', cantidad: 12, saldo: 0 }]
      : [{ estadoPago: 'no_pagado', cantidad: 2, saldo: 982_000 }, { estadoPago: 'abonado', cantidad: 4, saldo: 448_000 }, { estadoPago: 'pagado_total', cantidad: 0, saldo: 0 }],
    saldoTotal: vigente ? 12_831_000 : 1_430_000,
    cantidadPorCobrar: vigente ? 14 : 6,
  };
}

const embudo = (params) => ({
  periodo: periodos(params).periodo,
  pasos: [
    { clave: 'solicitudes', cantidad: 3, porcentaje: 100, porcentajePaso: null },
    { clave: 'cotizadas', cantidad: 3, porcentaje: 100, porcentajePaso: 100 },
    { clave: 'aprobadas', cantidad: 2, porcentaje: 66.7, porcentajePaso: 66.7 },
    { clave: 'concretadas', cantidad: 2, porcentaje: 66.7, porcentajePaso: 100 },
  ],
});

function demanda(params) {
  const porProducto = leer(params, 'agrupar') === 'producto';
  return {
    periodo: periodos(params).periodo,
    agrupar: porProducto ? 'producto' : 'categoria',
    categorias: porProducto
      ? [{ idProducto: 9, nombre: 'Letreros luminosos', monto: 1_500_000, cantidad: 4, pedidos: 2 }, { idProducto: 1, nombre: 'Impresión en tela PVC / Gigantografías', monto: 884_000, cantidad: 3, pedidos: 1 }]
      : [{ idCategoria: 2, nombre: 'Letreros y estructuras', monto: 1_500_000, cantidad: 4, pedidos: 2 }, { idCategoria: 1, nombre: 'Impresión y publicidad gráfica', monto: 884_000, cantidad: 3, pedidos: 1 }],
  };
}

const FILAS = [
  { idPedido: 118, cliente: { idCliente: 3, nombre: 'Clínica Dental Sonríe' }, fechaPedido: '2026-11-05', fechaEntregaEstimada: '2026-11-15', diasDesdePedido: 12, estadoPedido: 'entregado', estadoPago: 'no_pagado', montoTotalFinal: 525_000, montoAbonado: 0, saldoPendiente: 525_000, listoSinPagar: true },
  { idPedido: 127, cliente: { idCliente: 1, nombre: 'Panadería El Trigal' }, fechaPedido: '2026-11-07', fechaEntregaEstimada: '2026-11-20', diasDesdePedido: 10, estadoPedido: 'en_proceso', estadoPago: 'no_pagado', montoTotalFinal: 457_000, montoAbonado: 0, saldoPendiente: 457_000, listoSinPagar: false },
  { idPedido: 130, cliente: { idCliente: 1, nombre: 'Panadería El Trigal' }, fechaPedido: '2026-11-02', fechaEntregaEstimada: '2026-11-18', diasDesdePedido: 15, estadoPedido: 'en_proceso', estadoPago: 'abonado', montoTotalFinal: 296_000, montoAbonado: 96_000, saldoPendiente: 200_000, listoSinPagar: false },
  { idPedido: 126, cliente: { idCliente: 4, nombre: 'Minimarket Don Lucho' }, fechaPedido: '2026-11-13', fechaEntregaEstimada: '2026-11-25', diasDesdePedido: 4, estadoPedido: 'en_proceso', estadoPago: 'abonado', montoTotalFinal: 234_000, montoAbonado: 78_000, saldoPendiente: 156_000, listoSinPagar: false },
];

function porCobrar(params) {
  const { periodo } = periodos(params);
  const vigente = leer(params, 'alcanceSaldo') === 'vigente';
  const pagos = leer(params, 'estadoPago');
  const elegidos = pagos && pagos !== 'por_cobrar' ? pagos.split(',') : null;
  const antigua = { ...FILAS[0], idPedido: 12, fechaPedido: '2026-01-12', diasDesdePedido: 309, estadoPedido: 'terminado', listoSinPagar: true, montoTotalFinal: 245_000, saldoPendiente: 245_000 };
  const base = vigente ? [...FILAS, antigua] : FILAS;
  const elementos = base.filter((f) => !elegidos || elegidos.includes(f.estadoPago)).sort((a, b) => b.saldoPendiente - a.saldoPendiente);
  return {
    periodo, alcance: vigente ? 'vigente' : 'periodo', elementos, total: elementos.length, pagina: 1, tamano: 10, totalPaginas: 1,
    totalSaldo: elementos.reduce((suma, f) => suma + f.saldoPendiente, 0),
  };
}

const RUTAS = { resumen, evolucion, cobranza, embudo, demanda, 'por-cobrar': porCobrar };

/** La respuesta de /indicadores/{ruta} para esos parámetros (un objeto o URLSearchParams); undefined si la ruta no existe. */
export const respuestaIndicadores = (ruta, params) => RUTAS[ruta]?.(params);

export const CATALOGOS_FALSOS = {
  categorias: [{ idCategoria: 1, nombre: 'Impresión y publicidad gráfica' }, { idCategoria: 2, nombre: 'Letreros y estructuras' }],
  clientes: [{ idCliente: 1, nombre: 'Panadería', apellido: 'El Trigal' }, { idCliente: 3, nombre: 'Clínica Dental', apellido: 'Sonríe' }],
};
