// Formatos de Chile (RNFO05): pesos sin decimales y fechas dd-mm-aaaa.
const SIN_DATO = '—';

const pesos = new Intl.NumberFormat('es-CL', {
  style: 'currency',
  currency: 'CLP',
  maximumFractionDigits: 0,
  minimumFractionDigits: 0,
});

export function formatearPesos(monto) {
  if (monto === null || monto === undefined || monto === '') return SIN_DATO;
  const numero = Number(monto);
  if (!Number.isFinite(numero)) return SIN_DATO;
  return pesos.format(Math.round(numero));
}

// Una fecha sin hora ("2026-11-04") se lee como día local: new Date() la tomaría como medianoche
// UTC, que en Chile es el día anterior.
function aFecha(valor) {
  if (valor instanceof Date) return Number.isNaN(valor.getTime()) ? null : valor;
  if (typeof valor !== 'string' || valor === '') return null;
  const soloDia = valor.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  const fecha = soloDia ? new Date(Number(soloDia[1]), Number(soloDia[2]) - 1, Number(soloDia[3])) : new Date(valor);
  return Number.isNaN(fecha.getTime()) ? null : fecha;
}

const dos = (n) => String(n).padStart(2, '0');

export function formatearFecha(valor) {
  const fecha = aFecha(valor);
  return fecha ? `${dos(fecha.getDate())}-${dos(fecha.getMonth() + 1)}-${fecha.getFullYear()}` : SIN_DATO;
}

export function formatearFechaHora(valor) {
  const fecha = aFecha(valor);
  return fecha ? `${formatearFecha(fecha)} ${dos(fecha.getHours())}:${dos(fecha.getMinutes())}` : SIN_DATO;
}

const MESES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];

/** Nombre del mes (1 = enero), para filtros y reportes. */
export function nombreMes(numero) {
  return MESES[numero - 1] ?? '';
}
