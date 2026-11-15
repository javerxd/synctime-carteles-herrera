// Acciones delicadas de gestión (fase 9): piden de nuevo la contraseña de quien las hace. La API responde 400
// «La contraseña actual no es correcta.» (no 401, que cerraría la sesión) y 429 tras muchos intentos.

export const ETIQUETA_PASSWORD = 'Tu contraseña, para confirmar el cambio';
export const PASSWORD_VACIA = 'Escribe tu contraseña para confirmar el cambio.';

/** ¿La API rechazó la contraseña de quien hace el cambio? */
export function esPasswordIncorrecta(falla) {
  if (falla?.estado !== 400) return false;
  const delCampo = Object.keys(falla.errores ?? {}).some((c) => c.toLowerCase() === 'passwordactual');
  return delCampo || /contraseña actual/i.test(falla.message ?? '');
}

/** El texto del rechazo de la contraseña (el de la API o uno de respaldo). */
export function textoPasswordIncorrecta(falla) {
  const delCampo = Object.entries(falla?.errores ?? {}).find(([c]) => c.toLowerCase() === 'passwordactual')?.[1];
  return (Array.isArray(delCampo) ? delCampo[0] : delCampo) || falla?.message || 'La contraseña actual no es correcta.';
}

/** Gestión responde 403 sin_permiso (antes solo_gestion): se dice lo mismo con cualquiera de los dos. */
export const esSinPermiso = (falla) => falla?.estado === 403 && (falla.codigo === 'sin_permiso' || falla.codigo === 'solo_gestion' || falla.codigo === 'http_403');

export const MENSAJE_SIN_PERMISO = 'Solo el dueño o el administrador pueden hacer este cambio.';

export const MENSAJE_DEMASIADOS_INTENTOS = 'Hiciste demasiados intentos. Espera un minuto y vuelve a intentarlo.';

/** El mensaje común de una falla en estas acciones: permiso, intentos o el texto de la API. */
export function mensajeFallaGestion(falla, respaldo = 'No pudimos guardar el cambio.') {
  if (esSinPermiso(falla)) return MENSAJE_SIN_PERMISO;
  if (falla?.estado === 429) return falla.message && !/^Hiciste demasiados/.test(falla.message) ? falla.message : MENSAJE_DEMASIADOS_INTENTOS;
  return falla?.message || respaldo;
}
