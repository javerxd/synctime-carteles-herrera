// Regla única de nombres y apellidos de personas (fase 12D). Solo letras (con tildes, diéresis y ñ), hasta 20
// caracteres en total, sin espacios al inicio ni al final y un solo espacio entre dos palabras, con 5 espacios
// como máximo (seis palabras). Nunca cambia lo que la persona escribió: si no cumple, lo dice.
// Es la misma regla que Negocio/Cuentas/ReglaNombre.cs; los dos se prueban con los mismos casos
// (backend/…/LetrerosHerreraSYNCTIMEapi.UnitTests/CasosNombre.json).
export const MAXIMO_CARACTERES = 20;
export const MAXIMO_ESPACIOS = 5;
const TOPE_DE_REVISION = 200;

export const MENSAJES_NOMBRE = {
  bordes: 'Quita los espacios al inicio y al final.',
  caracteres: `Usa solo letras, hasta ${MAXIMO_CARACTERES} caracteres.`,
  espacios: `Deja un solo espacio entre palabras, hasta ${MAXIMO_ESPACIOS + 1} palabras.`,
  largo: `Usa solo letras, hasta ${MAXIMO_CARACTERES} caracteres.`,
};

// Letras con sus marcas combinadas (una tilde escrita aparte). Un espacio simple separa las palabras.
const SOLO_LETRAS_Y_ESPACIOS = /^[\p{L}\p{M} ]+$/u;
const PALABRAS_CON_UN_ESPACIO = /^[\p{L}\p{M}]+( [\p{L}\p{M}]+)*$/u;

/** null si el valor cumple; si no, el código de la primera regla que falla: vacio, bordes, caracteres, espacios o largo. */
export function revisarNombre(valor) {
  const texto = valor == null ? '' : String(valor);
  if (texto.trim() === '') return 'vacio';
  if (/^\s|\s$/.test(texto)) return 'bordes';
  // Un texto enorme no se recorre entero para decir que es largo.
  if (texto.length > TOPE_DE_REVISION) return 'largo';
  if (!SOLO_LETRAS_Y_ESPACIOS.test(texto)) return 'caracteres';
  // Cada palabra empieza con una letra: una marca combinante suelta no es un nombre.
  if (texto.split(' ').some((palabra) => palabra.length > 0 && !/^\p{L}/u.test(palabra))) return 'caracteres';
  if (!PALABRAS_CON_UN_ESPACIO.test(texto) || texto.split(' ').length - 1 > MAXIMO_ESPACIOS) return 'espacios';
  return texto.length > MAXIMO_CARACTERES ? 'largo' : null;
}

/** El mensaje para mostrar junto al campo, o null si es válido. `mensajeVacio` es el del formulario («Escribe tu nombre.»). */
export function errorNombre(valor, mensajeVacio) {
  const codigo = revisarNombre(valor);
  if (codigo === null) return null;
  return codigo === 'vacio' ? mensajeVacio : MENSAJES_NOMBRE[codigo];
}
