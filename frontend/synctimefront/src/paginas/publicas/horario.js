// Ayudas para mostrar el horario de atención que entrega la API (dia_semana 1 = lunes … 7 = domingo).

/** "09:00:00" -> "9:00" */
export function hora(valor) {
  if (!valor) return '';
  const [h, m] = String(valor).split(':');
  return `${Number(h)}:${m}`;
}

/** Día de hoy en la numeración de la API. */
export function diaDeHoy(fecha = new Date()) {
  return fecha.getDay() === 0 ? 7 : fecha.getDay();
}

/** "9:00 a 18:30", o "Cerrado" si ese día no se atiende. */
export function franja(dia) {
  if (!dia?.abierto || !dia.horaInicio || !dia.horaFin) return 'Cerrado';
  return `${hora(dia.horaInicio)} a ${hora(dia.horaFin)}`;
}
