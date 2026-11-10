/**
 * Token que autoriza este navegador para el acceso con rostro de dueño y administrador.
 * Va por email porque un equipo puede estar autorizado para más de una persona.
 *
 * Límite conocido: cualquier script que corra en esta página, o un programa malicioso
 * en el equipo, puede leerlo. Por sí solo no da acceso: también hace falta el rostro.
 */
const clave = (email) => `synctime_equipo:${email.trim().toLowerCase()}`;

export function leerTokenEquipo(email) {
  try {
    return localStorage.getItem(clave(email));
  } catch {
    return null;
  }
}

export function guardarTokenEquipo(email, token) {
  try {
    localStorage.setItem(clave(email), token);
    return true;
  } catch {
    return false;
  }
}

export function borrarTokenEquipo(email) {
  try {
    localStorage.removeItem(clave(email));
  } catch {
    // Sin acceso al almacenamiento no hay token que borrar.
  }
}
