import { aniosDisponibles, filtrarPedidos, nombreCliente, resumirPedidos } from '../paginas/panel/reglasPedidos';

const pedido = (id, { estadoPedido = 'aceptado', estadoPago = 'no_pagado', fecha = '2026-11-04T10:00:00', total = 100000, saldo = total, cliente = {} } = {}) => ({
  idPedido: id,
  estadoPedido,
  estadoPago,
  fechaPedido: fecha,
  montoTotalFinal: total,
  montoAbonado: total - saldo,
  saldoPendiente: saldo,
  solicitud: { cliente: { nombre: 'Rosa', apellido: 'Pino', rut: '12.345.678-5', telefono: '900000201', ...cliente } },
});

const PEDIDOS = [
  pedido(1, { estadoPedido: 'en_proceso', estadoPago: 'abonado', saldo: 60000 }),
  pedido(2, { estadoPedido: 'entregado', estadoPago: 'no_pagado', fecha: '2026-10-15T09:00:00', cliente: { nombre: 'Luis', apellido: 'Araya', rut: null, telefono: '900000202' } }),
  pedido(3, { estadoPedido: 'entregado', estadoPago: 'pagado_total', saldo: 0, fecha: '2025-12-20T09:00:00' }),
  pedido(4, { estadoPedido: 'anulado', estadoPago: 'no_pagado', fecha: '2026-10-01T09:00:00' }),
  pedido(5, { estadoPedido: 'terminado', estadoPago: 'abonado', saldo: 20000, cliente: { nombre: 'Camila', apellido: 'Reyes' } }),
];

const ids = (lista) => lista.map((p) => p.idPedido);

// Desde la fase 8 el listado filtra en la API; estas reglas quedan para los cálculos provisionales de
// Indicadores y Reportes (fases 12 y 13).
describe('filtros de pedidos en el navegador (Indicadores y Reportes)', () => {
  it('sin filtros devuelve todos, del más nuevo al más antiguo (a igual fecha, el número mayor)', () => {
    expect(ids(filtrarPedidos(PEDIDOS, {}))).toEqual([5, 1, 2, 4, 3]);
  });

  it('filtra por estado del pedido y por estado de pago por separado', () => {
    expect(ids(filtrarPedidos(PEDIDOS, { estadoPedido: 'entregado' }))).toEqual([2, 3]);
    expect(ids(filtrarPedidos(PEDIDOS, { estadoPago: 'abonado' }))).toEqual([5, 1]);
    expect(ids(filtrarPedidos(PEDIDOS, { estadoPedido: 'entregado', estadoPago: 'no_pagado' }))).toEqual([2]);
  });

  it('cobranza: pedidos no anulados que no están pagados del todo', () => {
    expect(ids(filtrarPedidos(PEDIDOS, { cobranza: true }))).toEqual([5, 1, 2]);
  });

  it('filtra por año y por mes de la fecha del pedido', () => {
    expect(ids(filtrarPedidos(PEDIDOS, { anio: 2025 }))).toEqual([3]);
    expect(ids(filtrarPedidos(PEDIDOS, { anio: 2026, mes: 10 }))).toEqual([2, 4]);
  });

  it('busca el cliente por nombre sin importar tildes ni mayúsculas, por RUT o por teléfono', () => {
    expect(ids(filtrarPedidos(PEDIDOS, { cliente: 'luis araya' }))).toEqual([2]);
    expect(ids(filtrarPedidos(PEDIDOS, { cliente: 'CAMILA' }))).toEqual([5]);
    expect(ids(filtrarPedidos(PEDIDOS, { cliente: '12345678' }))).toEqual([5, 1, 4, 3]);
    expect(ids(filtrarPedidos(PEDIDOS, { cliente: '0202' }))).toEqual([2]);
  });

  it('el resumen cuenta pedidos y suma el saldo por cobrar sin los anulados', () => {
    expect(resumirPedidos(PEDIDOS)).toEqual({ cantidad: 5, porCobrar: 60000 + 100000 + 20000 });
  });

  it('ofrece los años que tienen pedidos, del más reciente al más antiguo', () => {
    expect(aniosDisponibles(PEDIDOS)).toEqual([2026, 2025]);
  });

  it('el nombre del cliente se arma con nombre y apellido, desde la fila de la API (fase 8) o su solicitud (fase 3)', () => {
    expect(nombreCliente(PEDIDOS[1])).toBe('Luis Araya');
    expect(nombreCliente({ cliente: { nombre: 'Rosa', apellido: 'Pino' } })).toBe('Rosa Pino');
    expect(nombreCliente({ cliente: 'Ferretería Biobío' })).toBe('Ferretería Biobío');
    expect(nombreCliente({ solicitud: null })).toBe('Cliente sin datos');
  });

  it('con la fila de la API (fase 8), busca también por el teléfono del cliente', () => {
    const fila = { ...PEDIDOS[0], solicitud: undefined, cliente: { nombre: 'Rosa', apellido: 'Pino', telefono: '912345678' } };
    expect(ids(filtrarPedidos([fila], { cliente: '45678' }))).toEqual([1]);
  });
});
