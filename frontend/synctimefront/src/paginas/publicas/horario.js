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

const DIAS = { 1: 'el lunes', 2: 'el martes', 3: 'el miércoles', 4: 'el jueves', 5: 'el viernes', 6: 'el sábado', 7: 'el domingo' };

/**
 * La frase del mostrador según el estado del local (GET /local/estado): abierto, ausente con el mensaje
 * del cartel, o cerrado con la próxima apertura.
 */
export function textoEstado(estado) {
  if (!estado) return '';
  if (estado.motivo === 'abierto') return estado.cartel?.mensajeEstado || 'Abierto y atendiendo.';
  if (estado.motivo === 'ausente') return estado.cartel?.mensajeEstado || 'Volvemos pronto.';
  const proxima = estado.proximaApertura;
  if (!proxima) return 'Cerrado por ahora.';
  const cuando = proxima.esHoy ? 'hoy' : DIAS[proxima.diaSemana] ?? proxima.nombreDia;
  return `Cerrado. Abrimos ${cuando} a las ${hora(proxima.hora)}.`;
}
