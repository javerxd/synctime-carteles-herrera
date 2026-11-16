import {
  calcularCobranza, calcularDemanda, calcularEmbudo, calcularEvolucion, calcularPorCobrar, calcularResumen,
} from '../api/indicadoresDemo';
import { crearDatosDemo } from '../api/datosDemoIndicadores';
import {
  DEFINICION_FILTROS, parametrosIndicadores, periodoComparacion, resolverPeriodo,
} from '../paginas/indicadores/reglasIndicadores';
import { filtrosDesde } from '../utilidades/filtrosUrl';

// Fase 12A: el adaptador de demostración responde exactamente el contrato de /api/v1/indicadores. Cada cifra
// esperada está calculada a mano sobre este conjunto pequeño (hoy: lunes 16-11-2026; período: noviembre).
const HOY = '2026-11-16';
const item = (idCategoria, cantidad, subtotal) => ({ idCategoria, cantidad, subtotal });
const DATOS = {
  hoy: HOY,
  categorias: [{ idCategoria: 1, nombre: 'Luminosos' }, { idCategoria: 2, nombre: 'Vinilos' }],
  clientes: [{ idCliente: 1, nombre: 'Rosa', apellido: 'Pino' }, { idCliente: 2, nombre: 'Luis', apellido: 'Araya' }, { idCliente: 3, nombre: 'Camila', apellido: 'Reyes' }],
  solicitudes: [
    { idSolicitud: 1, idCliente: 1, idCategoria: 1, fechaSolicitud: '2026-11-02', cotizada: true, aprobada: true },
    { idSolicitud: 2, idCliente: 2, idCategoria: 2, fechaSolicitud: '2026-11-03', cotizada: true, aprobada: true },
    { idSolicitud: 3, idCliente: 3, idCategoria: 1, fechaSolicitud: '2026-11-05', cotizada: true, aprobada: false },
    { idSolicitud: 4, idCliente: 1, idCategoria: 2, fechaSolicitud: '2026-11-07', cotizada: false, aprobada: false },
    { idSolicitud: 5, idCliente: 2, idCategoria: 1, fechaSolicitud: '2026-10-10', cotizada: true, aprobada: true },
    { idSolicitud: 6, idCliente: 3, idCategoria: 2, fechaSolicitud: '2026-10-12', cotizada: true, aprobada: false },
  ],
  pedidos: [
    { idPedido: 1, idCliente: 1, origen: 'online', idSolicitud: 1, fechaPedido: '2026-11-04', fechaEntregaEstimada: '2026-11-10', fechaEntregaReal: '2026-11-09', estadoPedido: 'entregado', estadoPago: 'pagado_total', montoTotalFinal: 100000, montoAbonado: 100000, items: [item(1, 1, 100000)] },
    { idPedido: 2, idCliente: 2, origen: 'online', idSolicitud: 2, fechaPedido: '2026-11-05', fechaEntregaEstimada: '2026-11-20', fechaEntregaReal: null, estadoPedido: 'anulado', estadoPago: 'no_pagado', montoTotalFinal: 50000, montoAbonado: 0, items: [item(2, 2, 50000)] },
    { idPedido: 3, idCliente: 3, origen: 'presencial', idSolicitud: null, fechaPedido: '2026-11-06', fechaEntregaEstimada: '2026-11-12', fechaEntregaReal: '2026-11-14', estadoPedido: 'entregado', estadoPago: 'no_pagado', montoTotalFinal: 80000, montoAbonado: 0, items: [item(1, 1, 30000), item(2, 3, 50000)] },
    { idPedido: 4, idCliente: 1, origen: 'presencial', idSolicitud: null, fechaPedido: '2026-11-10', fechaEntregaEstimada: '2026-11-25', fechaEntregaReal: null, estadoPedido: 'en_proceso', estadoPago: 'abonado', montoTotalFinal: 60000, montoAbonado: 20000, items: [item(2, 1, 60000)] },
    { idPedido: 5, idCliente: 2, origen: 'online', idSolicitud: 5, fechaPedido: '2026-10-14', fechaEntregaEstimada: '2026-10-20', fechaEntregaReal: '2026-10-20', estadoPedido: 'entregado', estadoPago: 'abonado', montoTotalFinal: 120000, montoAbonado: 40000, items: [item(1, 2, 120000)] },
    { idPedido: 6, idCliente: 3, origen: 'presencial', idSolicitud: null, fechaPedido: '2026-01-15', fechaEntregaEstimada: '2026-01-30', fechaEntregaReal: null, estadoPedido: 'terminado', estadoPago: 'no_pagado', montoTotalFinal: 90000, montoAbonado: 0, items: [item(1, 1, 90000)] },
  ],
};

const MES = { periodo: 'mes', comparar: 'anterior' };
const tarjeta = (resumen, clave) => resumen.tarjetas.find((t) => t.clave === clave);

describe('períodos y comparación', () => {
  it('el mes en curso se compara con el mes anterior completo', () => {
    const p = resolverPeriodo(MES, HOY);
    expect(p).toMatchObject({ desde: '2026-11-01', hasta: '2026-11-30', etiqueta: 'noviembre de 2026' });
    expect(periodoComparacion(p, 'anterior')).toMatchObject({ desde: '2026-10-01', hasta: '2026-10-31', etiqueta: 'octubre de 2026' });
  });

  it('la semana va de lunes a domingo y el año completo se compara con el anterior', () => {
    expect(resolverPeriodo({ periodo: 'semana' }, HOY)).toMatchObject({ desde: '2026-11-16', hasta: '2026-11-22' });
    expect(periodoComparacion(resolverPeriodo({ periodo: 'semana' }, HOY), 'anterior')).toMatchObject({ desde: '2026-11-09', hasta: '2026-11-15' });
    expect(periodoComparacion(resolverPeriodo({ periodo: 'anio' }, HOY), 'anterior')).toMatchObject({ desde: '2025-01-01', hasta: '2025-12-31' });
  });

  it('el acceso por año y mes gana al período, y el rango propio gana a los dos', () => {
    expect(resolverPeriodo({ periodo: 'semana', anio: '2026', mes: '1' }, HOY)).toMatchObject({ desde: '2026-01-01', hasta: '2026-01-31' });
    const rango = resolverPeriodo({ anio: '2026', desde: '2026-11-01', hasta: '2026-11-10' }, HOY);
    expect(rango).toMatchObject({ desde: '2026-11-01', hasta: '2026-11-10', etiqueta: 'del 01-11-2026 al 10-11-2026' });
    expect(periodoComparacion(rango, 'anterior')).toMatchObject({ desde: '2026-10-22', hasta: '2026-10-31' });
  });

  it('«mismo período del año anterior» y «sin comparación»', () => {
    expect(periodoComparacion(resolverPeriodo(MES, HOY), 'anio_anterior')).toMatchObject({ desde: '2025-11-01', hasta: '2025-11-30' });
    expect(periodoComparacion(resolverPeriodo(MES, HOY), 'ninguno')).toBeNull();
  });
});

describe('filtros en la dirección', () => {
  const desde = (texto) => filtrosDesde(new URLSearchParams(texto), DEFINICION_FILTROS);

  it('acepta varios estados separados por coma y descarta lo que no existe', () => {
    expect(desde('estadoPedido=terminado,entregado').estadoPedido).toBe('terminado,entregado');
    expect(desde('estadoPedido=terminado,anulado').estadoPedido).toBe('');
    expect(desde('estadoPago=por_cobrar').estadoPago).toBe('por_cobrar');
    expect(desde('estadoPago=por_cobrar,abonado').estadoPago).toBe('');
  });

  it('el mes exige año, el rango exige las dos fechas en orden y los predeterminados son mes y período anterior', () => {
    expect(desde('mes=3').mes).toBe('');
    expect(desde('anio=2026&mes=3').mes).toBe('3');
    expect(desde('desde=2026-11-10&hasta=2026-11-01').desde).toBe('');
    const base = desde('');
    expect(base).toMatchObject({ periodo: 'mes', comparar: 'anterior', alcanceSaldo: 'periodo' });
  });

  it('los parámetros que viajan a la API: sin vacíos y con la página solo en el listado', () => {
    const filtros = desde('periodo=anio&cliente=3&cobro=abonado&pagina=2');
    expect(parametrosIndicadores(filtros)).toEqual({ periodo: 'anio', comparar: 'anterior', cliente: '3', alcanceSaldo: 'periodo' });
    expect(parametrosIndicadores(filtros, { listado: true })).toMatchObject({ estadoPago: 'abonado', pagina: 2, tamano: 10 });
  });
});

describe('resumen: fórmulas literales del plan general', () => {
  const resumen = calcularResumen(DATOS, MES);

  it('pedidos sin anulados (S10) e ingresos de los entregados en el período (S11), con su variación', () => {
    expect(tarjeta(resumen, 'pedidos')).toMatchObject({ valor: 3, anterior: 1, variacion: 200, tipoVariacion: 'porcentaje' });
    expect(tarjeta(resumen, 'ingresos')).toMatchObject({ valor: 180000, anterior: 120000, variacion: 50 });
  });

  it('en línea frente a presenciales (S12) y concretadas (S13): una solicitud con pedido anulado no cuenta', () => {
    expect(tarjeta(resumen, 'origen')).toMatchObject({ valor: 1, anterior: 1, detalle: { presenciales: 2, porcentajeEnLinea: 33.3 } });
    expect(tarjeta(resumen, 'concretadas')).toMatchObject({ valor: 1, anterior: 1, detalle: { solicitudesEnLinea: 4 } });
  });

  it('conversión en línea (S14), ticket promedio y entregas a tiempo: los porcentajes varían en puntos', () => {
    expect(tarjeta(resumen, 'conversion')).toMatchObject({ valor: 25, anterior: 50, variacion: -25, tipoVariacion: 'puntos' });
    expect(tarjeta(resumen, 'ticketPromedio')).toMatchObject({ valor: 80000, anterior: 120000, variacion: -33.3 });
    expect(tarjeta(resumen, 'entregasATiempo')).toMatchObject({ valor: 50, anterior: 100, variacion: -50, tipoVariacion: 'puntos' });
  });

  it('saldo por cobrar (S24) del período: no anulados en «no pagado» o «abonado»', () => {
    expect(resumen.saldo).toEqual({ alcance: 'periodo', valor: 120000, cantidad: 2, anterior: 80000, variacion: 50 });
  });

  it('con «toda la deuda vigente» el saldo ignora las fechas y también cuenta la deuda de enero', () => {
    const vigente = calcularResumen(DATOS, { ...MES, alcanceSaldo: 'vigente' });
    expect(vigente.saldo).toEqual({ alcance: 'vigente', valor: 290000, cantidad: 4, anterior: null, variacion: null });
    expect(tarjeta(vigente, 'pedidos').valor).toBe(3); // las demás tarjetas siguen en el período
  });

  it('informa el período y la comparación usados', () => {
    expect(resumen.periodo).toMatchObject({ desde: '2026-11-01', hasta: '2026-11-30' });
    expect(resumen.comparacion).toMatchObject({ desde: '2026-10-01', hasta: '2026-10-31' });
    expect(calcularResumen(DATOS, { ...MES, comparar: 'ninguno' }).comparacion).toBeNull();
    expect(tarjeta(calcularResumen(DATOS, { ...MES, comparar: 'ninguno' }), 'pedidos')).toMatchObject({ anterior: null, variacion: null });
  });

  it('sin base de comparación la variación queda vacía, no infinita', () => {
    const r = calcularResumen(DATOS, { ...MES, comparar: 'anio_anterior' });
    expect(tarjeta(r, 'pedidos')).toMatchObject({ valor: 3, anterior: 0, variacion: null });
    expect(tarjeta(r, 'conversion')).toMatchObject({ anterior: null, variacion: null });
  });
});

describe('cada filtro cambia los números', () => {
  it('cliente', () => {
    const r = calcularResumen(DATOS, { ...MES, cliente: '1' });
    expect(tarjeta(r, 'pedidos').valor).toBe(2);
    expect(tarjeta(r, 'ingresos').valor).toBe(100000);
    expect(tarjeta(r, 'conversion').valor).toBe(50);
    expect(r.saldo.valor).toBe(40000);
  });

  it('origen presencial: no hay conversión en línea que medir', () => {
    const r = calcularResumen(DATOS, { ...MES, origen: 'presencial' });
    expect(tarjeta(r, 'pedidos').valor).toBe(2);
    expect(tarjeta(r, 'conversion').valor).toBeNull();
  });

  it('estado de pago «por cobrar» y estado del pedido', () => {
    expect(tarjeta(calcularResumen(DATOS, { ...MES, estadoPago: 'por_cobrar' }), 'pedidos').valor).toBe(2);
    expect(tarjeta(calcularResumen(DATOS, { ...MES, estadoPago: 'por_cobrar' }), 'ingresos').valor).toBe(80000);
    expect(tarjeta(calcularResumen(DATOS, { ...MES, estadoPedido: 'en_proceso' }), 'ingresos').valor).toBe(0);
  });

  it('categoría: el pedido entra si alguno de sus ítems es de esa categoría', () => {
    const r = calcularResumen(DATOS, { ...MES, categoria: '2' });
    expect(tarjeta(r, 'pedidos').valor).toBe(2);
    expect(tarjeta(r, 'conversion').valor).toBe(0);
  });

  it('año y mes: enero muestra la deuda antigua', () => {
    const r = calcularResumen(DATOS, { anio: '2026', mes: '1', comparar: 'anterior' });
    expect(tarjeta(r, 'pedidos').valor).toBe(1);
    expect(r.saldo.valor).toBe(90000);
    expect(r.comparacion).toMatchObject({ desde: '2025-12-01', hasta: '2025-12-31' });
  });
});

describe('evolución, cobranza, embudo, demanda y por cobrar', () => {
  it('evolución del mes por semanas, alineada con el mes de comparación', () => {
    const { granularidad, serie } = calcularEvolucion(DATOS, MES);
    expect(granularidad).toBe('semana');
    expect(serie).toHaveLength(5);
    expect(serie[0]).toMatchObject({ desde: '2026-11-01', hasta: '2026-11-07', pedidos: 2, ingresos: 0 });
    expect(serie[1]).toMatchObject({ pedidos: 1, ingresos: 180000, pedidosComparacion: 1 });
    expect(serie[2].ingresosComparacion).toBe(120000);
    expect(calcularEvolucion(DATOS, { periodo: 'anio' }).granularidad).toBe('mes');
    expect(calcularEvolucion(DATOS, { periodo: 'semana' }).serie).toHaveLength(7);
  });

  it('cobranza: tres segmentos y el saldo total, del período o de toda la deuda', () => {
    const periodo = calcularCobranza(DATOS, MES);
    expect(periodo.segmentos).toEqual([
      { estadoPago: 'no_pagado', cantidad: 1, saldo: 80000 },
      { estadoPago: 'abonado', cantidad: 1, saldo: 40000 },
      { estadoPago: 'pagado_total', cantidad: 1, saldo: 0 },
    ]);
    expect(periodo).toMatchObject({ alcance: 'periodo', saldoTotal: 120000, cantidadPorCobrar: 2 });
    const vigente = calcularCobranza(DATOS, { ...MES, alcanceSaldo: 'vigente' });
    expect(vigente).toMatchObject({ alcance: 'vigente', saldoTotal: 290000, cantidadPorCobrar: 4 });
    expect(vigente.segmentos[0]).toEqual({ estadoPago: 'no_pagado', cantidad: 2, saldo: 170000 });
  });

  it('embudo del canal web con el porcentaje de cada paso', () => {
    const { pasos } = calcularEmbudo(DATOS, MES);
    expect(pasos.map((p) => [p.clave, p.cantidad, p.porcentaje])).toEqual([
      ['solicitudes', 4, 100], ['cotizadas', 3, 75], ['aprobadas', 2, 50], ['concretadas', 1, 25],
    ]);
    expect(pasos[3].porcentajePaso).toBe(50);
  });

  it('demanda por categoría: monto y cantidad, de mayor a menor monto', () => {
    expect(calcularDemanda(DATOS, MES).categorias).toEqual([
      { idCategoria: 1, nombre: 'Luminosos', monto: 130000, cantidad: 2, pedidos: 2 },
      { idCategoria: 2, nombre: 'Vinilos', monto: 110000, cantidad: 4, pedidos: 2 },
    ]);
  });

  it('por cobrar: de mayor a menor saldo (a igual saldo, el más antiguo), con el total de todas las páginas', () => {
    const vigente = calcularPorCobrar(DATOS, { ...MES, alcanceSaldo: 'vigente', pagina: 1, tamano: 3 });
    expect(vigente.elementos.map((p) => p.idPedido)).toEqual([6, 5, 3]);
    expect(vigente).toMatchObject({ total: 4, totalPaginas: 2, totalSaldo: 290000, alcance: 'vigente' });
    expect(vigente.elementos[0]).toMatchObject({ cliente: { idCliente: 3, nombre: 'Camila Reyes' }, saldoPendiente: 90000, listoSinPagar: true });
    expect(vigente.elementos[1].listoSinPagar).toBe(false);
    const abonados = calcularPorCobrar(DATOS, { ...MES, alcanceSaldo: 'vigente', estadoPago: 'abonado' });
    expect(abonados.elementos.map((p) => p.idPedido)).toEqual([5, 4]);
    expect(calcularPorCobrar(DATOS, MES).elementos.map((p) => p.idPedido)).toEqual([3, 4]);
  });
});

describe('datos de demostración', () => {
  const demo = crearDatosDemo(HOY);
  const meses2026 = new Set(demo.pedidos.filter((p) => p.fechaPedido.startsWith('2026')).map((p) => p.fechaPedido.slice(5, 7)));

  it('cubren varios meses de 2026, enero incluido, y no pasan de hoy', () => {
    expect(meses2026.has('01')).toBe(true);
    expect(meses2026.size).toBeGreaterThanOrEqual(10);
    expect(demo.pedidos.every((p) => p.fechaPedido <= HOY && (!p.fechaEntregaReal || p.fechaEntregaReal <= HOY))).toBe(true);
  });

  it('traen anulados, terminados y entregados sin pagar, abonados, los dos orígenes y varios clientes', () => {
    const hay = (condicion) => demo.pedidos.some(condicion);
    expect(hay((p) => p.estadoPedido === 'anulado')).toBe(true);
    expect(hay((p) => p.estadoPedido === 'terminado' && p.estadoPago === 'no_pagado')).toBe(true);
    expect(hay((p) => p.estadoPedido === 'entregado' && p.estadoPago === 'no_pagado')).toBe(true);
    expect(hay((p) => p.estadoPago === 'abonado')).toBe(true);
    expect(hay((p) => p.origen === 'online') && hay((p) => p.origen === 'presencial')).toBe(true);
    expect(new Set(demo.pedidos.map((p) => p.idCliente)).size).toBeGreaterThanOrEqual(8);
  });

  it('son coherentes: lo abonado no supera el total y el embudo nunca crece', () => {
    expect(demo.pedidos.every((p) => p.montoAbonado >= 0 && p.montoAbonado <= p.montoTotalFinal)).toBe(true);
    expect(demo.pedidos.every((p) => p.items.reduce((s, i) => s + i.subtotal, 0) === p.montoTotalFinal)).toBe(true);
    const { pasos } = calcularEmbudo(demo, { periodo: 'anio' });
    pasos.slice(1).forEach((paso, i) => expect(paso.cantidad).toBeLessThanOrEqual(pasos[i].cantidad));
  });

  it('son siempre los mismos (semilla fija), así las capturas y las pruebas no cambian', () => {
    expect(crearDatosDemo(HOY)).toEqual(demo);
  });
});
