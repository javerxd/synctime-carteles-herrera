// Fase 12A: conjunto fijo de pedidos y solicitudes de ejemplo para el adaptador de demostración del dashboard.
// Se genera con una semilla fija (siempre los mismos datos) desde octubre de 2025 hasta hoy, sin pasar de 2026:
// trae varios meses de 2026 (enero incluido), pedidos anulados, terminados y entregados sin pagar, abonados, los
// dos orígenes (en línea y presencial) y una docena de clientes, para que las comparaciones, el embudo y los
// filtros muestren movimiento. Los nombres son de negocios ficticios.
import { diasEntre, fechaIso, partesFecha, sumarDias } from '../utilidades/fechas';

export const CATEGORIAS_DEMO = [
  { idCategoria: 1, nombre: 'Letreros luminosos', base: 320000 },
  { idCategoria: 2, nombre: 'Vinilos y rotulación', base: 90000 },
  { idCategoria: 3, nombre: 'Letreros metálicos', base: 210000 },
  { idCategoria: 4, nombre: 'Señalética', base: 60000 },
  { idCategoria: 5, nombre: 'Impresión gran formato', base: 140000 },
];

export const CLIENTES_DEMO = [
  ['Panadería', 'El Trigal'], ['Ferretería', 'Biobío'], ['Clínica Dental', 'Sonríe'], ['Minimarket', 'Don Lucho'],
  ['Transportes', 'Ruiz'], ['Rosa', 'Pino'], ['Luis', 'Araya'], ['Camila', 'Reyes'], ['Café', 'La Esquina'],
  ['Gimnasio', 'Fuerza Sur'], ['Óptica', 'Visión Clara'], ['Colegio', 'San Andrés'],
].map(([nombre, apellido], i) => ({ idCliente: i + 1, nombre, apellido }));

const INICIO = { anio: 2025, mes: 10 };
const FIN_DATOS = '2026-12-31';

/** Generador pseudoaleatorio con semilla (mulberry32): mismos datos en cada carga. */
function semilla(valor) {
  let estado = valor >>> 0;
  return () => {
    estado = (estado + 0x6d2b79f5) >>> 0;
    let t = estado;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const redondearMil = (n) => Math.max(1000, Math.round(n / 1000) * 1000);

/**
 * Los datos de ejemplo hasta `hoy` («aaaa-mm-dd», en Chile). El estado de cada pedido depende de su antigüedad:
 * los antiguos ya están entregados (o terminados sin retirar), los recientes siguen en proceso.
 */
export function crearDatosDemo(hoy) {
  const azar = semilla(20261116);
  const entre = (min, max) => min + Math.floor(azar() * (max - min + 1));
  const ultimo = hoy < FIN_DATOS ? hoy : FIN_DATOS;
  const solicitudes = [];
  const pedidos = [];
  const clientePara = () => (azar() < 0.55 ? entre(1, 5) : entre(1, CLIENTES_DEMO.length));

  function crearPedido({ idCliente, origen, idSolicitud = null, fechaPedido, idCategoria }) {
    const categorias = [idCategoria];
    if (azar() < 0.35) categorias.push(((idCategoria + entre(0, 3)) % CATEGORIAS_DEMO.length) + 1);
    const items = [...new Set(categorias)].map((id) => {
      const cantidad = entre(1, 4);
      return { idCategoria: id, cantidad, subtotal: redondearMil(CATEGORIAS_DEMO[id - 1].base * (0.6 + azar()) * (1 + (cantidad - 1) * 0.35)) };
    });
    const montoTotalFinal = items.reduce((s, i) => s + i.subtotal, 0);
    const fechaEntregaEstimada = sumarDias(fechaPedido, entre(8, 20));
    let estadoPedido;
    let fechaEntregaReal = null;
    if (azar() < 0.07) {
      estadoPedido = 'anulado';
    } else if (fechaEntregaEstimada < ultimo) {
      const real = sumarDias(fechaEntregaEstimada, entre(-3, 5));
      if (real <= ultimo && azar() < 0.92) {
        estadoPedido = 'entregado';
        fechaEntregaReal = real;
      } else {
        estadoPedido = 'terminado';
      }
    } else {
      estadoPedido = diasEntre(fechaPedido, ultimo) < 3 ? 'aceptado' : 'en_proceso';
    }
    const r = azar();
    const reparto = {
      entregado: [0.72, 0.86], terminado: [0.3, 0.62], anulado: [0, 0], aceptado: [0.08, 0.55], en_proceso: [0.12, 0.7],
    }[estadoPedido];
    const estadoPago = r < reparto[0] ? 'pagado_total' : r < reparto[1] ? 'abonado' : 'no_pagado';
    const montoAbonado = estadoPago === 'pagado_total' ? montoTotalFinal
      : estadoPago === 'abonado' ? Math.min(montoTotalFinal - 1000, redondearMil(montoTotalFinal * (0.3 + azar() * 0.3))) : 0;
    pedidos.push({
      idPedido: pedidos.length + 1, idCliente, origen, idSolicitud, fechaPedido, fechaEntregaEstimada, fechaEntregaReal,
      estadoPedido, estadoPago, montoTotalFinal, montoAbonado, items,
    });
  }

  for (let { anio, mes } = INICIO; fechaIso(anio, mes, 1) <= ultimo; ({ anio, mes } = partesFecha(fechaIso(anio, mes + 1, 1)))) {
    const dias = new Date(Date.UTC(anio, mes, 0)).getUTCDate();
    // Algo de estacionalidad: más movimiento en marzo y en noviembre-diciembre.
    const extra = [3, 11, 12].includes(mes) ? 3 : 0;
    for (let i = 0, total = entre(6, 10) + extra; i < total; i += 1) {
      const fechaSolicitud = fechaIso(anio, mes, entre(1, dias));
      if (fechaSolicitud > ultimo) continue;
      const cotizada = azar() < 0.82;
      const aprobada = cotizada && azar() < 0.66;
      const solicitud = { idSolicitud: solicitudes.length + 1, idCliente: clientePara(), idCategoria: entre(1, CATEGORIAS_DEMO.length), fechaSolicitud, cotizada, aprobada };
      solicitudes.push(solicitud);
      const fechaPedido = sumarDias(fechaSolicitud, entre(2, 6));
      if (aprobada && fechaPedido <= ultimo) {
        crearPedido({ idCliente: solicitud.idCliente, origen: 'online', idSolicitud: solicitud.idSolicitud, fechaPedido, idCategoria: solicitud.idCategoria });
      }
    }
    for (let i = 0, total = entre(3, 6) + Math.floor(extra / 2); i < total; i += 1) {
      const fechaPedido = fechaIso(anio, mes, entre(1, dias));
      if (fechaPedido <= ultimo) crearPedido({ idCliente: clientePara(), origen: 'presencial', fechaPedido, idCategoria: entre(1, CATEGORIAS_DEMO.length) });
    }
  }

  // Casos que siempre están, sea cual sea el azar: deuda antigua de enero y un trabajo terminado sin retirar ni pagar.
  const fijos = [
    { idCliente: 4, origen: 'presencial', fechaPedido: '2026-01-12', fechaEntregaEstimada: '2026-01-26', fechaEntregaReal: '2026-01-27', estadoPedido: 'entregado', estadoPago: 'no_pagado', items: [{ idCategoria: 3, cantidad: 1, subtotal: 245000 }] },
    { idCliente: 9, origen: 'online', fechaPedido: '2026-01-20', fechaEntregaEstimada: '2026-02-03', fechaEntregaReal: '2026-02-02', estadoPedido: 'entregado', estadoPago: 'abonado', montoAbonado: 60000, items: [{ idCategoria: 1, cantidad: 1, subtotal: 180000 }] },
    { idCliente: 11, origen: 'presencial', fechaPedido: '2026-09-08', fechaEntregaEstimada: '2026-09-22', fechaEntregaReal: null, estadoPedido: 'terminado', estadoPago: 'no_pagado', items: [{ idCategoria: 5, cantidad: 2, subtotal: 156000 }] },
  ].filter((p) => p.fechaPedido <= ultimo);
  for (const fijo of fijos) {
    const montoTotalFinal = fijo.items.reduce((s, i) => s + i.subtotal, 0);
    pedidos.push({ idSolicitud: null, montoAbonado: 0, ...fijo, fechaEntregaReal: fijo.fechaEntregaReal && fijo.fechaEntregaReal <= ultimo ? fijo.fechaEntregaReal : null, idPedido: pedidos.length + 1, montoTotalFinal });
  }

  return {
    hoy,
    categorias: CATEGORIAS_DEMO.map(({ idCategoria, nombre }) => ({ idCategoria, nombre })),
    clientes: CLIENTES_DEMO,
    solicitudes,
    pedidos,
  };
}
