// Fase 13B (RFS10, RFU24 a RFU26, RFU36, RNFP18, E16, E20, S18, S24): reglas puras de la pantalla de reportes. Quién ve qué
// pestaña, los filtros que viven en la dirección y los que viajan a cada reporte, el atajo de cobranza del dashboard, cómo se
// muestra cada celda y la máquina de estados de una descarga. Sin red ni DOM: el contrato está en docs/api/openapi.json.
import { GESTION, PERSONAL } from '../../estructuras/navegacion';
import { formatearFecha, formatearPesos } from '../../utilidades/formato';
import { paraDireccion } from '../../utilidades/filtrosUrl';
import { DEFINICION_FILTROS } from '../indicadores/reglasIndicadores';

/** Los tres reportes: el operativo lo ve todo el personal; el gerencial y el parametrizable, solo gestión (RFU24, RFU25). */
export const REPORTES = {
  operativo: { etiqueta: 'Operativo', titulo: 'Reporte operativo', roles: PERSONAL },
  gerencial: { etiqueta: 'Gerencial', titulo: 'Reporte gerencial', roles: GESTION },
  parametrizable: { etiqueta: 'Parametrizable', titulo: 'Reporte parametrizable', roles: GESTION },
};

export const FORMATOS = [
  { id: 'xlsx', etiqueta: 'Excel' },
  { id: 'csv', etiqueta: 'CSV' },
  { id: 'pdf', etiqueta: 'PDF' },
];

export const nombreDelReporte = (reporte) => REPORTES[reporte]?.titulo ?? REPORTES.operativo.titulo;

/** Las pestañas que ve un rol, en orden. Un rol desconocido (o un cliente) no ve ninguna. */
export const pestanasPara = (rol) => Object.keys(REPORTES).filter((id) => REPORTES[id].roles.includes(rol));

/** El reporte que se muestra: el pedido en la dirección si el rol lo ve; si no, el primero que sí ve (sin error). */
export function reporteDeRol(reporte, rol) {
  const visibles = pestanasPara(rol);
  return visibles.includes(reporte) ? reporte : visibles[0] ?? 'operativo';
}

// ── Filtros ──

// Los de siempre en un reporte; «comparar» es solo del gerencial y «alcanceSaldo», solo del parametrizable. Categoría y origen
// existen en el dashboard pero no en los reportes: la API respondería 400, así que nunca viajan.
const COMUNES = ['periodo', 'desde', 'hasta', 'anio', 'mes', 'estadoPedido', 'estadoPago', 'cliente'];
const POR_REPORTE = {
  operativo: COMUNES,
  gerencial: [...COMUNES, 'comparar'],
  parametrizable: [...COMUNES, 'alcanceSaldo'],
};

const { campos: validadores } = DEFINICION_FILTROS;

/** Los campos que viven en la dirección (src/utilidades/filtrosUrl.js): el reporte y los filtros. */
export const DEFINICION_REPORTES = {
  campos: {
    reporte: (v) => v in REPORTES,
    periodo: validadores.periodo,
    desde: validadores.desde,
    hasta: validadores.hasta,
    anio: validadores.anio,
    mes: validadores.mes,
    comparar: validadores.comparar,
    estadoPedido: validadores.estadoPedido,
    estadoPago: validadores.estadoPago,
    cliente: validadores.cliente,
    alcanceSaldo: validadores.alcanceSaldo,
  },
  predeterminados: { reporte: 'operativo', periodo: 'mes', comparar: 'anterior', alcanceSaldo: 'periodo' },
};

/** Los filtros que viajan a un reporte (los vacíos no viajan; la página y el reporte, nunca). */
export function parametrosReporte(reporte, filtros) {
  const parametros = {};
  for (const campo of POR_REPORTE[reporte] ?? COMUNES) {
    const valor = filtros[campo];
    if (valor !== '' && valor !== undefined && valor !== null) parametros[campo] = valor;
  }
  return parametros;
}

/**
 * «Exportar cobranza» (RFU36): la dirección del parametrizable ya filtrado por cobrar, con el período, el alcance y el cliente
 * del dashboard. Lo que los reportes no tienen (comparación, categoría, origen) no viaja; el segmento elegido en el anillo de
 * cobranza afina el estado de pago.
 */
export function enlaceCobranza(filtros) {
  const elegidos = {
    reporte: 'parametrizable',
    periodo: filtros.periodo ?? '',
    desde: filtros.desde ?? '',
    hasta: filtros.hasta ?? '',
    anio: filtros.anio ?? '',
    mes: filtros.mes ?? '',
    estadoPedido: filtros.estadoPedido ?? '',
    estadoPago: filtros.cobro || filtros.estadoPago || 'por_cobrar',
    cliente: filtros.cliente ?? '',
    alcanceSaldo: filtros.alcanceSaldo ?? '',
  };
  const texto = new URLSearchParams(paraDireccion(elegidos, DEFINICION_REPORTES)).toString();
  return `/panel/reportes${texto ? `?${texto}` : ''}`;
}

// ── Cómo se ve cada celda ──

/** El texto de una celda del documento: pesos con punto de miles, fecha día-mes-año, porcentaje con coma. */
export function formatearCelda(valor, columna) {
  if (valor === null || valor === undefined || valor === '') return columna.textoNulo || '—';
  switch (columna.tipo) {
    case 'pesos': return formatearPesos(valor);
    case 'fecha': return formatearFecha(valor);
    case 'porcentaje': return Number.isFinite(Number(valor)) ? `${Number(valor).toFixed(1).replace('.', ',')} %` : columna.textoNulo || '—';
    default: return String(valor);
  }
}

/** La fecha y hora de emisión del reporte en hora de Chile («20-11-2026 14:35»), sea cual sea la zona del equipo. */
export function emisionChile(instante) {
  const fecha = new Date(instante);
  if (Number.isNaN(fecha.getTime())) return '—';
  const partes = Object.fromEntries(new Intl.DateTimeFormat('es-CL', {
    timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
  }).formatToParts(fecha).map((p) => [p.type, p.value]));
  return `${partes.day}-${partes.month}-${partes.year} ${partes.hour}:${partes.minute}`;
}

const CSV_COMUN ='El CSV usa «;» como separador y UTF-8 (Excel lo abre con doble clic). ';

/** Lo que lleva cada archivo: el CSV es una sola tabla (decisión 5 del plan), y la pantalla lo dice. */
export function textoDelCsv(reporte) {
  if (reporte === 'operativo') return `${CSV_COMUN}Un CSV es una sola tabla: el operativo exporta solo la tabla de pedidos; Excel y PDF llevan las tres.`;
  if (reporte === 'gerencial') return `${CSV_COMUN}El gerencial exporta el detalle de pedidos entregados; Excel y PDF llevan todo el reporte.`;
  return `${CSV_COMUN}Exporta la tabla de pedidos.`;
}

// ── La descarga (RNFP18) ──
// inactiva → pidiendo → preparando → lista | fallida. Mientras hay una ocupada, nada se pide: un trabajo por persona.

export const EXPORTACION_INICIAL = { fase: 'inactiva' };

export const estaOcupada = (estado) => estado.fase === 'pidiendo' || estado.fase === 'preparando';

export function reducirExportacion(estado, accion) {
  switch (accion.tipo) {
    case 'pedir':
      return estaOcupada(estado) ? estado : { fase: 'pidiendo', formato: accion.formato, reporte: accion.reporte };
    case 'aceptada':
      return estado.fase === 'pidiendo' ? { fase: 'preparando', formato: estado.formato, reporte: estado.reporte, id: accion.id } : estado;
    case 'lista':
      return estaOcupada(estado) ? { fase: 'lista', formato: estado.formato, reporte: estado.reporte, id: estado.id, nombreArchivo: accion.nombreArchivo } : estado;
    case 'fallida':
      // También desde «lista»: volver a bajar un archivo que ya venció falla sin que se haya pedido nada nuevo.
      return estaOcupada(estado) || estado.fase === 'lista' ? { fase: 'fallida', formato: estado.formato, reporte: estado.reporte, mensaje: accion.mensaje, reintentable: accion.reintentable !== false } : estado;
    case 'cerrar':
      return estaOcupada(estado) ? estado : EXPORTACION_INICIAL;
    default:
      return estado;
  }
}

/** Lo que anuncia el lector de pantalla (región `status`); un fallo no se anuncia aquí: va en su propio aviso de error. */
export function anuncioExportacion(estado) {
  if (estaOcupada(estado)) return 'Preparando tu archivo';
  if (estado.fase === 'lista') return 'Tu archivo está listo';
  return '';
}
