// Fase 12A: cómo se escriben las cifras del dashboard (RNFO05: pesos sin decimales, coma decimal) y las frases que
// resumen cada gráfico, para que la tendencia se lea sin mirar el dibujo.
import { formatearPesos } from '../../utilidades/formato';

const numero = new Intl.NumberFormat('es-CL', { maximumFractionDigits: 1 });
export const formatearNumero = (n) => (n === null || n === undefined ? '—' : numero.format(n));
export const formatearPorcentaje = (n) => (n === null || n === undefined ? '—' : `${numero.format(n)} %`);

/** Un valor según su unidad del contrato: 'pesos', 'cantidad' o 'porcentaje'. */
export function formatearValor(valor, unidad) {
  if (valor === null || valor === undefined) return 'Sin datos';
  if (unidad === 'pesos') return formatearPesos(valor);
  if (unidad === 'porcentaje') return formatearPorcentaje(valor);
  return formatearNumero(valor);
}

const conSigno = (n) => (n > 0 ? `+${numero.format(n)}` : n < 0 ? `−${numero.format(Math.abs(n))}` : '0');

/** «+12 %» o «−3,5 pp»: los porcentajes varían en puntos porcentuales. */
export function formatearVariacion(variacion, tipo) {
  if (variacion === null || variacion === undefined) return null;
  return `${conSigno(variacion)} ${tipo === 'puntos' ? 'pp' : '%'}`;
}

/** Sentido de la variación según si subir es bueno (ingresos) o malo (saldo por cobrar). */
export function sentidoDe(variacion, subirEsBueno = true) {
  if (variacion === null || variacion === undefined || variacion === 0) return 'neutro';
  return (variacion > 0) === subirEsBueno ? 'favorable' : 'desfavorable';
}

const verbo = (variacion, [sube, baja, igual]) => (variacion > 0 ? sube : variacion < 0 ? baja : igual);
const variacionPct = (actual, anterior) => (anterior ? Math.round(((actual - anterior) / anterior) * 1000) / 10 : null);
const suma = (serie, campo) => serie.reduce((s, x) => s + (x[campo] ?? 0), 0);

/** Evolución: «Los ingresos subieron 12 % respecto de octubre de 2026.» */
export function narrativaEvolucion(datos, metrica) {
  if (!datos.serie?.length) return '';
  const actual = suma(datos.serie, metrica);
  const nombre = metrica === 'ingresos' ? 'Los ingresos' : 'Los pedidos';
  const total = metrica === 'ingresos' ? formatearPesos(actual) : `${formatearNumero(actual)} pedidos`;
  if (!datos.comparacion) return `${metrica === 'ingresos' ? 'Ingresos' : 'Pedidos'} de ${datos.periodo.etiqueta}: ${total}.`;
  const anterior = suma(datos.serie, `${metrica}Comparacion`);
  const v = variacionPct(actual, anterior);
  if (v === null) return `${nombre} del período suman ${total}; en ${datos.comparacion.etiqueta} no hubo movimiento para comparar.`;
  return `${nombre} ${verbo(v, ['subieron', 'bajaron', 'se mantuvieron'])}${v ? ` ${formatearNumero(Math.abs(v))} %` : ''} respecto de ${datos.comparacion.etiqueta} (${total}).`;
}

/** Cobranza: cuánto falta por cobrar y cuántos pedidos no tienen ningún pago. */
export function narrativaCobranza(datos) {
  const sinPago = datos.segmentos?.find((s) => s.estadoPago === 'no_pagado') ?? { cantidad: 0 };
  if (!datos.cantidadPorCobrar) return 'No hay saldos pendientes: todos los pedidos están pagados.';
  return `Quedan ${formatearPesos(datos.saldoTotal)} por cobrar en ${datos.cantidadPorCobrar} ${datos.cantidadPorCobrar === 1 ? 'pedido' : 'pedidos'}; `
    + `${sinPago.cantidad} ${sinPago.cantidad === 1 ? 'no tiene' : 'no tienen'} ningún pago.`;
}

/** Embudo: de cuántas solicitudes en línea salieron pedidos. */
export function narrativaEmbudo(datos) {
  const [primero] = datos.pasos ?? [];
  const ultimo = datos.pasos?.at(-1);
  if (!primero?.cantidad) return 'No llegaron solicitudes en línea en este período.';
  return `De ${formatearNumero(primero.cantidad)} solicitudes en línea, ${formatearNumero(ultimo.cantidad)} terminaron en un pedido (${formatearPorcentaje(ultimo.porcentaje)}).`;
}

/** Demanda: la categoría que lidera y su parte del total. */
export function narrativaDemanda(categorias, medida) {
  if (!categorias.length) return '';
  const [lider] = categorias;
  const total = suma(categorias, medida);
  const parte = total ? Math.round((lider[medida] / total) * 100) : 0;
  const valor = medida === 'monto' ? formatearPesos(lider.monto) : `${formatearNumero(lider.cantidad)} unidades`;
  return `${lider.nombre} lidera con ${valor}, el ${parte} % del total del período.`;
}

/** Nombre y fórmula de cada tarjeta del resumen. */
export const TARJETAS = {
  ingresos: { nombre: 'Ingresos', formula: 'Monto final de los pedidos entregados en el período (S11).' },
  pedidos: { nombre: 'Pedidos', formula: 'Pedidos creados en el período, sin contar los anulados (S10).' },
  origen: { nombre: 'En línea y presenciales', formula: 'Pedidos del período que llegaron por la web y en el mostrador (S12).' },
  concretadas: { nombre: 'Solicitudes concretadas', formula: 'Solicitudes en línea que terminaron en un pedido no anulado (S13).' },
  conversion: { nombre: 'Conversión en línea', formula: 'Concretadas sobre solicitudes en línea; varía en puntos porcentuales (S14).' },
  ticketPromedio: { nombre: 'Ticket promedio', formula: 'Monto final promedio de los pedidos no anulados del período.' },
  entregasATiempo: { nombre: 'Entregas a tiempo', formula: 'Entregados en o antes de la fecha estimada, sobre los entregados del período.' },
};

export const NOMBRES_PASOS = {
  solicitudes: 'Solicitudes en línea',
  cotizadas: 'Cotizadas',
  aprobadas: 'Aprobadas por el cliente',
  concretadas: 'Pedidos concretados',
};

// Solo «no pagado» y «abonado» tienen saldo: son los segmentos del anillo que filtran la tabla «Pedidos por cobrar».
export const SEGMENTOS_FILTRABLES = ['no_pagado', 'abonado'];

/** Ordena las categorías de mayor a menor según la medida elegida ('monto' o 'cantidad'). */
export const ordenarDemanda = (categorias, medida) => [...categorias].sort((a, b) => b[medida] - a[medida] || a.nombre.localeCompare(b.nombre, 'es'));

/** «noviembre de 2026» → «Noviembre de 2026». */
export const conMayuscula = (texto) => (texto ? `${texto[0].toUpperCase()}${texto.slice(1)}` : '');
