// Fechas de calendario («2026-11-14») en hora de Chile (fase 8). «Hoy» es la fecha de Chile, no la del
// equipo: un computador con otra zona horaria no corre el día. Las cuentas se hacen en UTC sobre la fecha
// sin hora, así el cambio de horario de verano no salta ni repite días.

const ZONA_CHILE = 'America/Santiago';
const FECHA = /^(\d{4})-(\d{2})-(\d{2})$/;

const formatoDia = new Intl.DateTimeFormat('en-CA', { timeZone: ZONA_CHILE, year: 'numeric', month: '2-digit', day: '2-digit' });

/** La fecha de hoy en Chile, «aaaa-mm-dd». `ahora` permite fijarla en las pruebas. */
export function hoyChile(ahora = new Date()) {
  return formatoDia.format(ahora);
}

/** «2026-11-14» → { anio, mes, dia }, o null si no es una fecha real. */
export function partesFecha(texto) {
  const m = String(texto ?? '').slice(0, 10).match(FECHA);
  if (!m) return null;
  const [anio, mes, dia] = [Number(m[1]), Number(m[2]), Number(m[3])];
  const fecha = new Date(Date.UTC(anio, mes - 1, dia));
  if (fecha.getUTCFullYear() !== anio || fecha.getUTCMonth() !== mes - 1 || fecha.getUTCDate() !== dia) return null;
  return { anio, mes, dia };
}

export const esFecha = (texto) => partesFecha(texto) !== null;

const dos = (n) => String(n).padStart(2, '0');

/** { anio, mes, dia } (el día puede pasarse del mes: se ajusta) → «aaaa-mm-dd». */
export function fechaIso(anio, mes, dia = 1) {
  const fecha = new Date(Date.UTC(anio, mes - 1, dia));
  return `${fecha.getUTCFullYear()}-${dos(fecha.getUTCMonth() + 1)}-${dos(fecha.getUTCDate())}`;
}

/** Suma (o resta) días a una fecha «aaaa-mm-dd». */
export function sumarDias(texto, dias) {
  const p = partesFecha(texto);
  return p ? fechaIso(p.anio, p.mes, p.dia + dias) : null;
}

/** Días entre dos fechas (b − a). */
export function diasEntre(a, b) {
  const pa = partesFecha(a);
  const pb = partesFecha(b);
  if (!pa || !pb) return Number.NaN;
  return Math.round((Date.UTC(pb.anio, pb.mes - 1, pb.dia) - Date.UTC(pa.anio, pa.mes - 1, pa.dia)) / 86_400_000);
}

/** Día de la semana de Chile, de lunes (0) a domingo (6). */
export function diaSemana(texto) {
  const p = partesFecha(texto);
  if (!p) return Number.NaN;
  return (new Date(Date.UTC(p.anio, p.mes - 1, p.dia)).getUTCDay() + 6) % 7;
}

/** Suma años conservando el día (el 29 de febrero pasa al 28). */
export function sumarAnios(texto, anios) {
  const p = partesFecha(texto);
  if (!p) return null;
  const ultimo = new Date(Date.UTC(p.anio + anios, p.mes, 0)).getUTCDate();
  return fechaIso(p.anio + anios, p.mes, Math.min(p.dia, ultimo));
}

export const DIAS_SEMANA = ['lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado', 'domingo'];
const MESES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];

/** «2026-11-14» → «sábado 14 de noviembre». */
export function fechaLarga(texto, { conAnio = false } = {}) {
  const p = partesFecha(texto);
  if (!p) return '—';
  return `${DIAS_SEMANA[diaSemana(texto)]} ${p.dia} de ${MESES[p.mes - 1]}${conAnio ? ` de ${p.anio}` : ''}`;
}

/** (2026, 11) → «noviembre de 2026». */
export const nombreMesAnio = (anio, mes) => `${MESES[mes - 1]} de ${anio}`;
