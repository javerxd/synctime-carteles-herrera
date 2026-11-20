// Qué cartel mostrar según el estado del local (GET /local/estado). El servidor ya combina la hora de
// Chile con el estado manual del cartel: aquí solo se traduce `abiertoAhora` a la imagen y la etiqueta.

export const CARTEL_ABIERTO = '/imagenes/cartel-abierto.webp';
export const CARTEL_CERRADO = '/imagenes/cartel-cerrado.webp';

/** { abierto, imagen, etiqueta }, o null mientras no hay datos (no se afirma nada). */
export function estadoCartel(datos) {
  if (!datos) return null;
  const abierto = datos.abiertoAhora === true;
  return { abierto, imagen: abierto ? CARTEL_ABIERTO : CARTEL_CERRADO, etiqueta: abierto ? 'Abierto' : 'Cerrado' };
}
