// Reglas del estado del local (fase 9): horario semanal y disponibilidad del personal (RFU20). Las mismas que
// valida la API: inicio antes del fin, sin cruces en el mismo día y cada franja dentro del horario de ese día.
// Días de la API: 1 = lunes … 7 = domingo. Horas «HH:mm» en el formulario y «HH:mm:ss» hacia la API.

export const DIAS = [
  { dia: 1, nombre: 'Lunes' }, { dia: 2, nombre: 'Martes' }, { dia: 3, nombre: 'Miércoles' }, { dia: 4, nombre: 'Jueves' },
  { dia: 5, nombre: 'Viernes' }, { dia: 6, nombre: 'Sábado' }, { dia: 7, nombre: 'Domingo' },
];

export const nombreDia = (dia) => DIAS.find((d) => d.dia === Number(dia))?.nombre ?? '';

export const LIMITES_CARTEL = { mensaje: 200, regreso: 100 };

/** «09:00:00» o «9:00» → «09:00»; vacío si no es una hora. */
export function horaCorta(valor) {
  const m = String(valor ?? '').match(/^(\d{1,2}):(\d{2})/);
  if (!m) return '';
  return `${m[1].padStart(2, '0')}:${m[2]}`;
}

/** Minutos desde la medianoche, o null. */
export function aMinutos(valor) {
  const corta = horaCorta(valor);
  if (!corta) return null;
  const [h, m] = corta.split(':').map(Number);
  if (h > 23 || m > 59) return null;
  return h * 60 + m;
}

export const paraHoraApi = (valor) => (horaCorta(valor) ? `${horaCorta(valor)}:00` : null);

// ── Horario del taller (gestión) ──

/** Los 7 días del formulario desde GET /horarios (los que falten, cerrados). */
export function horarioDesdeApi(dias = []) {
  return DIAS.map(({ dia, nombre }) => {
    const d = dias.find((x) => Number(x.diaSemana) === dia);
    return {
      diaSemana: dia,
      nombreDia: d?.nombreDia ?? nombre,
      abierto: Boolean(d?.abierto),
      horaInicio: horaCorta(d?.horaInicio),
      horaFin: horaCorta(d?.horaFin),
    };
  });
}

/** Errores por campo («3.horaFin»). Un día abierto necesita apertura y cierre, con el cierre después. */
export function validarHorario(dias) {
  const errores = {};
  for (const d of dias) {
    if (!d.abierto) continue;
    const inicio = aMinutos(d.horaInicio);
    const fin = aMinutos(d.horaFin);
    if (inicio === null) errores[`${d.diaSemana}.horaInicio`] = 'Indica la hora de apertura.';
    if (fin === null) errores[`${d.diaSemana}.horaFin`] = 'Indica la hora de cierre.';
    if (inicio !== null && fin !== null && fin <= inicio) errores[`${d.diaSemana}.horaFin`] = 'El cierre debe ser después de la apertura.';
  }
  return errores;
}

export function horarioParaApi(dias) {
  return dias.map((d) => ({
    diaSemana: d.diaSemana,
    abierto: d.abierto,
    horaInicio: d.abierto ? paraHoraApi(d.horaInicio) : null,
    horaFin: d.abierto ? paraHoraApi(d.horaFin) : null,
  }));
}

// ── Disponibilidad (RFU20) ──

let siguiente = 0;
const nuevaClave = () => {
  siguiente += 1;
  return `f${siguiente}`;
};

export const franjaVacia = (diaSemana) => ({ clave: nuevaClave(), diaSemana, horaInicio: '', horaFin: '' });

/** Las franjas de la API, en el orden de la semana y del día, con una clave para la lista. */
export function franjasDesdeApi(franjas = []) {
  return [...franjas]
    .map((f) => ({ clave: nuevaClave(), diaSemana: Number(f.diaSemana), horaInicio: horaCorta(f.horaInicio), horaFin: horaCorta(f.horaFin) }))
    .sort((a, b) => a.diaSemana - b.diaSemana || (aMinutos(a.horaInicio) ?? 0) - (aMinutos(b.horaInicio) ?? 0));
}

/**
 * Errores por franja (`${clave}.horaInicio`, `${clave}.horaFin`): horas completas, inicio antes del fin, dentro del
 * horario del taller de ese día (un día cerrado no admite franjas) y sin cruzarse con otra del mismo día.
 * Dos franjas que se tocan (9:00–13:00 y 13:00–15:00) no se cruzan.
 */
export function validarFranjas(franjas, horario = []) {
  const errores = {};
  for (const f of franjas) {
    const inicio = aMinutos(f.horaInicio);
    const fin = aMinutos(f.horaFin);
    if (inicio === null) errores[`${f.clave}.horaInicio`] = 'Indica desde qué hora.';
    if (fin === null) errores[`${f.clave}.horaFin`] = 'Indica hasta qué hora.';
    if (inicio === null || fin === null) continue;
    if (fin <= inicio) {
      errores[`${f.clave}.horaFin`] = 'Debe terminar después de empezar.';
      continue;
    }
    const dia = horario.find((d) => Number(d.diaSemana) === Number(f.diaSemana));
    if (!dia?.abierto) {
      errores[`${f.clave}.horaInicio`] = 'Ese día el taller está cerrado.';
      continue;
    }
    const abre = aMinutos(dia.horaInicio);
    const cierra = aMinutos(dia.horaFin);
    if (abre !== null && cierra !== null && (inicio < abre || fin > cierra)) {
      errores[`${f.clave}.horaInicio`] = `Debe quedar dentro del horario del taller (${horaCorta(dia.horaInicio)} a ${horaCorta(dia.horaFin)}).`;
      continue;
    }
    const cruce = franjas.find((otra) => otra !== f && Number(otra.diaSemana) === Number(f.diaSemana)
      && aMinutos(otra.horaInicio) !== null && aMinutos(otra.horaFin) !== null
      && aMinutos(otra.horaInicio) < fin && inicio < aMinutos(otra.horaFin));
    if (cruce) errores[`${f.clave}.horaInicio`] = `Se cruza con la franja de ${horaCorta(cruce.horaInicio)} a ${horaCorta(cruce.horaFin)}.`;
  }
  return errores;
}

export function franjasParaApi(franjas) {
  return franjasDesdeApi(franjas).map((f) => ({ diaSemana: f.diaSemana, horaInicio: paraHoraApi(f.horaInicio), horaFin: paraHoraApi(f.horaFin) }));
}

/** «9:00 a 13:00» */
export function textoFranja(desde, hasta) {
  const corta = (v) => horaCorta(v).replace(/^0(\d)/, '$1');
  return `${corta(desde)} a ${corta(hasta)}`;
}

/**
 * Disponibilidad de todo el personal (GET /disponibilidad) agrupada por persona, con sus franjas por día.
 * Acepta personas con sus franjas ([{ idEmpleado, nombre, franjas }]) o una fila por franja.
 */
export function equipoPorPersona(datos = []) {
  const personas = new Map();
  for (const fila of datos) {
    const id = fila.idEmpleado ?? fila.nombre;
    if (!personas.has(id)) personas.set(id, { idEmpleado: id, nombre: fila.nombre ?? '', franjas: [] });
    const persona = personas.get(id);
    const franjas = Array.isArray(fila.franjas) ? fila.franjas : fila.diaSemana ? [fila] : [];
    persona.franjas.push(...franjas.map((f) => ({ diaSemana: Number(f.diaSemana), horaInicio: f.horaInicio ?? f.desde, horaFin: f.horaFin ?? f.hasta })));
  }
  return [...personas.values()]
    .map((p) => ({ ...p, franjas: p.franjas.sort((a, b) => a.diaSemana - b.diaSemana || (aMinutos(a.horaInicio) ?? 0) - (aMinutos(b.horaInicio) ?? 0)) }))
    .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
}
