// Fase 12A: períodos, comparación y filtros del dashboard (RFS09, RFU19, RFU27). Los usa la pantalla para la
// dirección y el adaptador de demostración para calcular; el backend de la 12B aplica las mismas reglas
// (docs/plan/fase-12-especificacion.md).
import { diaSemana, diasEntre, esFecha, fechaIso, nombreMesAnio, partesFecha, sumarAnios, sumarDias } from '../../utilidades/fechas';
import { formatearFecha } from '../../utilidades/formato';

export const PERIODOS = { semana: 'Semana', mes: 'Mes', anio: 'Año' };
export const COMPARACIONES = { anterior: 'Período anterior', anio_anterior: 'Mismo período del año anterior', ninguno: 'Sin comparación' };
// «Anulado» no se ofrece: ningún indicador cuenta los pedidos anulados.
export const ESTADOS_PEDIDO_FILTRO = ['aceptado', 'en_proceso', 'terminado', 'entregado'];
export const ESTADOS_PAGO_FILTRO = ['no_pagado', 'abonado', 'pagado_total'];
export const ORIGENES = { online: 'En línea', presencial: 'Presencial' };
export const TAMANO_POR_COBRAR = 10;

const ultimoDia = (anio, mes) => new Date(Date.UTC(anio, mes, 0)).getUTCDate();
const entero = (texto, min, max) => /^\d+$/.test(texto) && Number(texto) >= min && Number(texto) <= max;
const lista = (permitidos) => (valor) => {
  const partes = valor.split(',');
  return partes.length > 0 && new Set(partes).size === partes.length && partes.every((p) => permitidos.includes(p));
};

/** Los campos del dashboard que viven en la dirección (src/utilidades/filtrosUrl.js). */
export const DEFINICION_FILTROS = {
  campos: {
    periodo: (v) => v in PERIODOS,
    desde: (v, f) => esFecha(v) && esFecha(f.hasta) && v <= f.hasta,
    hasta: (v, f) => esFecha(v) && esFecha(f.desde) && f.desde <= v,
    anio: (v) => entero(v, 2020, 2100),
    mes: (v, f) => entero(v, 1, 12) && entero(f.anio ?? '', 2020, 2100),
    comparar: (v) => v in COMPARACIONES,
    estadoPedido: lista(ESTADOS_PEDIDO_FILTRO),
    estadoPago: (v) => v === 'por_cobrar' || lista(ESTADOS_PAGO_FILTRO)(v),
    categoria: (v) => entero(v, 1, 2_147_483_647),
    origen: (v) => v in ORIGENES,
    cliente: (v) => entero(v, 1, 2_147_483_647),
    alcanceSaldo: (v) => v === 'periodo' || v === 'vigente',
    // Segmento elegido en el anillo de cobranza: solo filtra la tabla «Pedidos por cobrar».
    cobro: (v) => v === 'no_pagado' || v === 'abonado',
  },
  predeterminados: { periodo: 'mes', comparar: 'anterior', alcanceSaldo: 'periodo' },
  tamano: TAMANO_POR_COBRAR,
};

const COMUNES = ['periodo', 'desde', 'hasta', 'anio', 'mes', 'comparar', 'estadoPedido', 'estadoPago', 'categoria', 'origen', 'cliente', 'alcanceSaldo'];

/**
 * Los parámetros que viajan a /api/v1/indicadores (los vacíos no viajan). Con `listado` (GET /por-cobrar) se
 * agregan la página y su tamaño, y el segmento del anillo reemplaza al estado de pago.
 */
export function parametrosIndicadores(filtros, { listado = false } = {}) {
  const parametros = {};
  for (const campo of COMUNES) {
    if (filtros[campo] !== '' && filtros[campo] !== undefined && filtros[campo] !== null) parametros[campo] = filtros[campo];
  }
  if (listado) {
    // El segmento del anillo afina el estado de pago elegido, nunca lo contradice.
    const elegidos = !filtros.estadoPago || filtros.estadoPago === 'por_cobrar' ? null : filtros.estadoPago.split(',');
    if (filtros.cobro && (!elegidos || elegidos.includes(filtros.cobro))) parametros.estadoPago = filtros.cobro;
    parametros.pagina = filtros.pagina ?? 1;
    parametros.tamano = TAMANO_POR_COBRAR;
  }
  return parametros;
}

/** Los filtros que no son de fecha ni de alcance: para «Limpiar» y para el resumen de la barra. */
export const FILTROS_DE_DATOS = ['estadoPedido', 'estadoPago', 'categoria', 'origen', 'cliente'];

function periodoDeMes(anio, mes) {
  return { tipo: 'mes', desde: fechaIso(anio, mes, 1), hasta: fechaIso(anio, mes, ultimoDia(anio, mes)), etiqueta: nombreMesAnio(anio, mes) };
}

const periodoDeAnio = (anio) => ({ tipo: 'anio', desde: fechaIso(anio, 1, 1), hasta: fechaIso(anio, 12, 31), etiqueta: String(anio) });

function periodoDeSemana(lunes) {
  const domingo = sumarDias(lunes, 6);
  return { tipo: 'semana', desde: lunes, hasta: domingo, etiqueta: `la semana del ${formatearFecha(lunes)} al ${formatearFecha(domingo)}` };
}

const periodoDeRango = (desde, hasta) => ({ tipo: 'rango', desde, hasta, etiqueta: `del ${formatearFecha(desde)} al ${formatearFecha(hasta)}` });

/**
 * El período que piden los filtros, con «hoy» en Chile («aaaa-mm-dd»). Prioridad: el rango propio (desde y
 * hasta), luego el año y el mes, y al final el período relativo a hoy (semana de lunes a domingo, mes o año).
 */
export function resolverPeriodo(filtros, hoy) {
  const { periodo = 'mes', desde, hasta, anio, mes } = filtros;
  if (esFecha(desde) && esFecha(hasta) && desde <= hasta) return periodoDeRango(desde, hasta);
  if (anio && mes) return periodoDeMes(Number(anio), Number(mes));
  if (anio) return periodoDeAnio(Number(anio));
  const h = partesFecha(hoy);
  if (periodo === 'semana') return periodoDeSemana(sumarDias(hoy, -diaSemana(hoy)));
  if (periodo === 'anio') return periodoDeAnio(h.anio);
  return periodoDeMes(h.anio, h.mes);
}

/** El período con el que se compara: el inmediatamente anterior de igual forma, el del año anterior o ninguno. */
export function periodoComparacion(periodo, comparar = 'anterior') {
  if (comparar === 'ninguno') return null;
  const inicio = partesFecha(periodo.desde);
  if (comparar === 'anio_anterior') {
    if (periodo.tipo === 'mes') return periodoDeMes(inicio.anio - 1, inicio.mes);
    if (periodo.tipo === 'anio') return periodoDeAnio(inicio.anio - 1);
    return { ...periodoDeRango(sumarAnios(periodo.desde, -1), sumarAnios(periodo.hasta, -1)), tipo: periodo.tipo };
  }
  if (periodo.tipo === 'mes') return inicio.mes === 1 ? periodoDeMes(inicio.anio - 1, 12) : periodoDeMes(inicio.anio, inicio.mes - 1);
  if (periodo.tipo === 'anio') return periodoDeAnio(inicio.anio - 1);
  if (periodo.tipo === 'semana') return periodoDeSemana(sumarDias(periodo.desde, -7));
  const dias = diasEntre(periodo.desde, periodo.hasta) + 1;
  return periodoDeRango(sumarDias(periodo.desde, -dias), sumarDias(periodo.desde, -1));
}

/** Cómo se agrupa la evolución: por día (semana o hasta 14 días), por semana (mes o hasta 92 días) o por mes. */
export function granularidadDe(periodo) {
  if (periodo.tipo === 'semana') return 'dia';
  if (periodo.tipo === 'mes') return 'semana';
  if (periodo.tipo === 'anio') return 'mes';
  const dias = diasEntre(periodo.desde, periodo.hasta) + 1;
  return dias <= 14 ? 'dia' : dias <= 92 ? 'semana' : 'mes';
}

/** Texto de la comparación para las tarjetas: «respecto de octubre de 2026». */
export const textoComparacion = (comparacion) => (comparacion ? `respecto de ${comparacion.etiqueta}` : '');
