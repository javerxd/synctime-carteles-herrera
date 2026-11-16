// Fase 12A: adaptador de demostración de /api/v1/indicadores. Calcula en el navegador, con un conjunto fijo de
// pedidos y solicitudes de ejemplo, las respuestas EXACTAS del contrato (docs/plan/fase-12-especificacion.md).
// Fórmulas literales del plan general:
// - pedidos: sin anulados (S10);
// - ingresos: monto final de los entregados, por la fecha de entrega real (S11);
// - concretadas: solicitudes en línea con un pedido no anulado (S13); conversión = concretadas / en línea (S14);
// - saldo por cobrar: pedidos no anulados en «no pagado» o «abonado» (S24).
// La 12B reemplaza este archivo por la API real sin tocar las pantallas (VITE_INDICADORES_DEMO).
import { diasEntre, fechaIso, partesFecha, sumarDias } from '../utilidades/fechas';
import { formatearFecha, nombreMes } from '../utilidades/formato';
import { granularidadDe, periodoComparacion, resolverPeriodo } from '../paginas/indicadores/reglasIndicadores';

const redondear1 = (n) => Math.round(n * 10) / 10;
const suma = (lista, campo) => lista.reduce((s, x) => s + (typeof campo === 'function' ? campo(x) : x[campo]), 0);
const enRango = (fecha, rango) => Boolean(fecha) && fecha >= rango.desde && fecha <= rango.hasta;
const saldoDe = (p) => p.montoTotalFinal - p.montoAbonado;
const porCobrar = (p) => p.estadoPedido !== 'anulado' && p.estadoPago !== 'pagado_total';
const listaDe = (valor) => (valor ? String(valor).split(',') : []);

export const variacionPorcentual = (valor, anterior) => (valor === null || anterior === null || anterior === undefined || anterior === 0
  ? null : redondear1(((valor - anterior) / anterior) * 100));
export const variacionPuntos = (valor, anterior) => (valor === null || anterior === null || anterior === undefined ? null : redondear1(valor - anterior));

/** Los filtros que no son de fecha. Los anulados nunca cuentan. */
function filtrarPedidos(pedidos, filtros) {
  const estados = listaDe(filtros.estadoPedido);
  const pagos = filtros.estadoPago === 'por_cobrar' ? ['no_pagado', 'abonado'] : listaDe(filtros.estadoPago);
  return pedidos.filter((p) => p.estadoPedido !== 'anulado'
    && (!estados.length || estados.includes(p.estadoPedido))
    && (!pagos.length || pagos.includes(p.estadoPago))
    && (!filtros.categoria || p.items.some((i) => i.idCategoria === Number(filtros.categoria)))
    && (!filtros.origen || p.origen === filtros.origen)
    && (!filtros.cliente || p.idCliente === Number(filtros.cliente)));
}

/** Solicitudes en línea: el estado y el origen del pedido no las filtran; «presencial» las deja fuera. */
function filtrarSolicitudes(solicitudes, filtros) {
  if (filtros.origen === 'presencial') return [];
  return solicitudes.filter((s) => (!filtros.categoria || s.idCategoria === Number(filtros.categoria))
    && (!filtros.cliente || s.idCliente === Number(filtros.cliente)));
}

const solicitudesConPedido = (datos) => new Set(datos.pedidos.filter((p) => p.estadoPedido !== 'anulado' && p.idSolicitud).map((p) => p.idSolicitud));

function medirPeriodo(datos, filtros, rango) {
  const pedidos = filtrarPedidos(datos.pedidos, filtros);
  const delPeriodo = pedidos.filter((p) => enRango(p.fechaPedido, rango));
  const entregados = pedidos.filter((p) => p.estadoPedido === 'entregado' && enRango(p.fechaEntregaReal, rango));
  const aTiempo = entregados.filter((p) => p.fechaEntregaReal <= p.fechaEntregaEstimada).length;
  const conPedido = solicitudesConPedido(datos);
  const solicitudes = filtrarSolicitudes(datos.solicitudes, filtros).filter((s) => enRango(s.fechaSolicitud, rango));
  const concretadas = solicitudes.filter((s) => conPedido.has(s.idSolicitud)).length;
  const enLinea = delPeriodo.filter((p) => p.origen === 'online').length;
  const deuda = delPeriodo.filter(porCobrar);
  return {
    ingresos: suma(entregados, 'montoTotalFinal'),
    pedidos: delPeriodo.length,
    origen: enLinea,
    presenciales: delPeriodo.length - enLinea,
    solicitudesEnLinea: solicitudes.length,
    concretadas,
    conversion: solicitudes.length ? redondear1((concretadas / solicitudes.length) * 100) : null,
    ticketPromedio: delPeriodo.length ? Math.round(suma(delPeriodo, 'montoTotalFinal') / delPeriodo.length) : null,
    entregasATiempo: entregados.length ? redondear1((aTiempo / entregados.length) * 100) : null,
    saldo: suma(deuda, saldoDe),
    cantidadSaldo: deuda.length,
  };
}

function contexto(datos, filtros) {
  const periodo = resolverPeriodo(filtros, datos.hoy);
  const comparacion = periodoComparacion(periodo, filtros.comparar ?? 'anterior');
  return { periodo, comparacion, vigente: filtros.alcanceSaldo === 'vigente' };
}

/** Pedidos para la deuda: los del período o, con «toda la deuda vigente», sin límite de fecha. */
const pedidosDeDeuda = (datos, filtros, periodo, vigente) => filtrarPedidos(datos.pedidos, filtros)
  .filter((p) => vigente || enRango(p.fechaPedido, periodo));

/** GET /indicadores/resumen */
export function calcularResumen(datos, filtros) {
  const { periodo, comparacion, vigente } = contexto(datos, filtros);
  const actual = medirPeriodo(datos, filtros, periodo);
  const previo = comparacion ? medirPeriodo(datos, filtros, comparacion) : null;
  const tarjeta = (clave, unidad, tipoVariacion, detalle) => {
    const valor = actual[clave];
    const anterior = previo ? previo[clave] : null;
    return {
      clave, unidad, valor, anterior, tipoVariacion,
      variacion: tipoVariacion === 'puntos' ? variacionPuntos(valor, anterior) : variacionPorcentual(valor, anterior),
      ...(detalle ? { detalle } : {}),
    };
  };
  const deudaVigente = vigente ? pedidosDeDeuda(datos, filtros, periodo, true).filter(porCobrar) : null;

  return {
    periodo,
    comparacion,
    tarjetas: [
      tarjeta('ingresos', 'pesos', 'porcentaje'),
      tarjeta('pedidos', 'cantidad', 'porcentaje'),
      tarjeta('origen', 'cantidad', 'porcentaje', {
        presenciales: actual.presenciales,
        porcentajeEnLinea: actual.pedidos ? redondear1((actual.origen / actual.pedidos) * 100) : null,
      }),
      tarjeta('concretadas', 'cantidad', 'porcentaje', { solicitudesEnLinea: actual.solicitudesEnLinea }),
      tarjeta('conversion', 'porcentaje', 'puntos'),
      tarjeta('ticketPromedio', 'pesos', 'porcentaje'),
      tarjeta('entregasATiempo', 'porcentaje', 'puntos'),
    ],
    saldo: vigente
      ? { alcance: 'vigente', valor: suma(deudaVigente, saldoDe), cantidad: deudaVigente.length, anterior: null, variacion: null }
      : {
        alcance: 'periodo', valor: actual.saldo, cantidad: actual.cantidadSaldo,
        anterior: previo ? previo.saldo : null, variacion: previo ? variacionPorcentual(actual.saldo, previo.saldo) : null,
      },
  };
}

const corta = (fecha) => formatearFecha(fecha).slice(0, 5);

/** Los tramos de la evolución, desde el inicio del período. */
function tramos(periodo, granularidad) {
  const lista = [];
  if (granularidad === 'mes') {
    let { anio, mes } = partesFecha(periodo.desde);
    while (fechaIso(anio, mes, 1) <= periodo.hasta) {
      const desde = fechaIso(anio, mes, 1);
      const hasta = fechaIso(anio, mes + 1, 0);
      lista.push({ desde: desde < periodo.desde ? periodo.desde : desde, hasta: hasta > periodo.hasta ? periodo.hasta : hasta, etiqueta: nombreMes(mes).slice(0, 3) });
      mes += 1;
      if (mes > 12) { mes = 1; anio += 1; }
    }
    return lista;
  }
  const paso = granularidad === 'semana' ? 7 : 1;
  for (let desde = periodo.desde; desde <= periodo.hasta; desde = sumarDias(desde, paso)) {
    const fin = sumarDias(desde, paso - 1);
    lista.push({ desde, hasta: fin > periodo.hasta ? periodo.hasta : fin, etiqueta: corta(desde) });
  }
  return lista;
}

/** GET /indicadores/evolucion: ingresos y pedidos por tramo, con el período de comparación alineado por posición. */
export function calcularEvolucion(datos, filtros) {
  const { periodo, comparacion } = contexto(datos, filtros);
  const granularidad = granularidadDe(periodo);
  const pedidos = filtrarPedidos(datos.pedidos, filtros);
  const medir = (tramo) => ({
    ingresos: suma(pedidos.filter((p) => p.estadoPedido === 'entregado' && enRango(p.fechaEntregaReal, tramo)), 'montoTotalFinal'),
    pedidos: pedidos.filter((p) => enRango(p.fechaPedido, tramo)).length,
  });
  const comparados = comparacion ? tramos(comparacion, granularidad) : [];
  return {
    periodo,
    comparacion,
    granularidad,
    serie: tramos(periodo, granularidad).map((tramo, i) => {
      const previo = comparados[i] ? medir(comparados[i]) : null;
      return { ...tramo, ...medir(tramo), ingresosComparacion: previo?.ingresos ?? null, pedidosComparacion: previo?.pedidos ?? null };
    }),
  };
}

/** GET /indicadores/cobranza: los tres estados de pago (cantidad y saldo) y el saldo por cobrar. */
export function calcularCobranza(datos, filtros) {
  const { periodo, vigente } = contexto(datos, filtros);
  const pedidos = pedidosDeDeuda(datos, filtros, periodo, vigente);
  const segmentos = ['no_pagado', 'abonado', 'pagado_total'].map((estadoPago) => {
    const grupo = pedidos.filter((p) => p.estadoPago === estadoPago);
    return { estadoPago, cantidad: grupo.length, saldo: suma(grupo, saldoDe) };
  });
  const deuda = pedidos.filter(porCobrar);
  return { periodo, alcance: vigente ? 'vigente' : 'periodo', segmentos, saldoTotal: suma(deuda, saldoDe), cantidadPorCobrar: deuda.length };
}

/** GET /indicadores/embudo: solicitudes en línea → cotizadas → aprobadas → pedidos concretados (no anulados). */
export function calcularEmbudo(datos, filtros) {
  const { periodo } = contexto(datos, filtros);
  const solicitudes = filtrarSolicitudes(datos.solicitudes, filtros).filter((s) => enRango(s.fechaSolicitud, periodo));
  const conPedido = solicitudesConPedido(datos);
  const cantidades = [
    ['solicitudes', solicitudes.length],
    ['cotizadas', solicitudes.filter((s) => s.cotizada).length],
    ['aprobadas', solicitudes.filter((s) => s.aprobada).length],
    ['concretadas', solicitudes.filter((s) => conPedido.has(s.idSolicitud)).length],
  ];
  const base = cantidades[0][1];
  return {
    periodo,
    pasos: cantidades.map(([clave, cantidad], i) => ({
      clave,
      cantidad,
      porcentaje: base ? redondear1((cantidad / base) * 100) : null,
      porcentajePaso: i === 0 ? null : cantidades[i - 1][1] ? redondear1((cantidad / cantidades[i - 1][1]) * 100) : null,
    })),
  };
}

/** GET /indicadores/demanda: monto (subtotal de los ítems) y unidades por categoría, de mayor a menor monto. */
export function calcularDemanda(datos, filtros) {
  const { periodo } = contexto(datos, filtros);
  const pedidos = filtrarPedidos(datos.pedidos, filtros).filter((p) => enRango(p.fechaPedido, periodo));
  const categorias = datos.categorias
    .filter((c) => !filtros.categoria || c.idCategoria === Number(filtros.categoria))
    .map((c) => {
      const items = pedidos.flatMap((p) => p.items.filter((i) => i.idCategoria === c.idCategoria).map((i) => ({ ...i, idPedido: p.idPedido })));
      return { idCategoria: c.idCategoria, nombre: c.nombre, monto: suma(items, 'subtotal'), cantidad: suma(items, 'cantidad'), pedidos: new Set(items.map((i) => i.idPedido)).size };
    })
    .filter((c) => c.pedidos > 0)
    .sort((a, b) => b.monto - a.monto || a.nombre.localeCompare(b.nombre, 'es'));
  return { periodo, categorias };
}

/** GET /indicadores/por-cobrar: pedidos con saldo, de mayor a menor saldo (a igual saldo, el más antiguo primero). */
export function calcularPorCobrar(datos, filtros) {
  const { periodo, vigente } = contexto(datos, filtros);
  const tamano = Number(filtros.tamano) || 10;
  const clientes = new Map(datos.clientes.map((c) => [c.idCliente, c]));
  const deuda = pedidosDeDeuda(datos, filtros, periodo, vigente).filter(porCobrar)
    .sort((a, b) => saldoDe(b) - saldoDe(a) || a.fechaPedido.localeCompare(b.fechaPedido) || a.idPedido - b.idPedido);
  const totalPaginas = Math.ceil(deuda.length / tamano);
  const pagina = Math.min(Math.max(1, Number(filtros.pagina) || 1), Math.max(totalPaginas, 1));
  return {
    periodo,
    alcance: vigente ? 'vigente' : 'periodo',
    elementos: deuda.slice((pagina - 1) * tamano, pagina * tamano).map((p) => {
      const c = clientes.get(p.idCliente);
      return {
        idPedido: p.idPedido,
        cliente: { idCliente: p.idCliente, nombre: c ? `${c.nombre} ${c.apellido}`.trim() : `Cliente N.º ${p.idCliente}` },
        fechaPedido: p.fechaPedido,
        fechaEntregaEstimada: p.fechaEntregaEstimada,
        diasDesdePedido: diasEntre(p.fechaPedido, datos.hoy),
        estadoPedido: p.estadoPedido,
        estadoPago: p.estadoPago,
        montoTotalFinal: p.montoTotalFinal,
        montoAbonado: p.montoAbonado,
        saldoPendiente: saldoDe(p),
        listoSinPagar: (p.estadoPedido === 'terminado' || p.estadoPedido === 'entregado') && p.estadoPago === 'no_pagado',
      };
    }),
    total: deuda.length,
    pagina,
    tamano,
    totalPaginas,
    totalSaldo: suma(deuda, saldoDe),
  };
}
