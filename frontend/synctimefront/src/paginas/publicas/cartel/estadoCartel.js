// Qué dice el cartel según el estado del local (GET /local/estado). El servidor ya combina la hora de
// Chile con el estado manual del cartel: aquí solo se traduce `abiertoAhora` a la etiqueta corta.

/** { abierto, etiqueta: 'Abierto' | 'Cerrado' }, o null mientras no hay datos (no se afirma nada). */
export function estadoCartel(datos) {
  if (!datos) return null;
  const abierto = datos.abiertoAhora === true;
  return { abierto, etiqueta: abierto ? 'Abierto' : 'Cerrado' };
}
