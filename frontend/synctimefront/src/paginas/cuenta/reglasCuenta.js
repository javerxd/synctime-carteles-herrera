import { MAXIMO_CARACTERES, errorNombre } from '../../utilidades/reglaNombre';
import { validarRut } from '../../utilidades/rut';

// Reglas de la cuenta del cliente: las mismas que valida la API (Negocio/Cuentas/ValidacionCuenta.cs y
// AuthService.EsPasswordValida), para que la persona vea el mismo mensaje antes y después de enviar.
// Nombre y apellido: la regla única de la fase 12D (utilidades/reglaNombre.js), hasta 20 caracteres.
export const LIMITES = { nombre: MAXIMO_CARACTERES, correo: 100, direccion: 255, passwordMinima: 8, passwordMaxima: 72 };

const CORREO = /^[^\s@;,"]+@[^\s@;,"]+\.[^\s@;,"]+$/;

const MENSAJES = {
  correo: 'Escribe un correo válido, por ejemplo nombre@correo.cl. Con él ingresas y recuperas tu contraseña.',
  telefono: 'Escribe un teléfono de 9 dígitos, por ejemplo 9 1234 5678.',
  rut: 'El RUT no es válido. Revisa el número y el dígito verificador, por ejemplo 12.345.678-5.',
  password: `La contraseña debe tener entre ${LIMITES.passwordMinima} y ${LIMITES.passwordMaxima} caracteres.`,
};

export function esTelefono(valor) {
  let limpio = String(valor ?? '').replace(/\s/g, '');
  if (limpio.startsWith('+56')) limpio = limpio.slice(3);
  return /^\d{9}$/.test(limpio);
}

export function errorCorreo(correo = '') {
  const c = correo.trim();
  if (!c) return 'Escribe tu correo.';
  if (!CORREO.test(c) || c.length > LIMITES.correo) return MENSAJES.correo;
  return null;
}

export function errorPassword(password = '', { vacia = 'Escribe una contraseña.' } = {}) {
  if (!password) return vacia;
  if (password.length < LIMITES.passwordMinima || password.length > LIMITES.passwordMaxima) return MENSAJES.password;
  return null;
}

/** Datos personales (registro y Mi cuenta). Un objeto vacío significa que se puede enviar. */
export function validarDatos({ nombre = '', apellido = '', telefono = '', rut = '', direccion = '' }) {
  const errores = {
    nombre: errorNombre(nombre, 'Escribe tu nombre.'),
    apellido: errorNombre(apellido, 'Escribe tu apellido.'),
    telefono: esTelefono(telefono) ? null : MENSAJES.telefono,
    rut: rut.trim() && !validarRut(rut) ? MENSAJES.rut : null,
    direccion: direccion.trim().length > LIMITES.direccion ? `La dirección puede tener hasta ${LIMITES.direccion} caracteres.` : null,
  };
  return Object.fromEntries(Object.entries(errores).filter(([, mensaje]) => mensaje));
}

export const ORDEN_REGISTRO = ['nombre', 'apellido', 'telefono', 'correo', 'rut', 'direccion', 'password', 'aceptaTerminosYPrivacidad'];

export function validarRegistro(datos) {
  const errores = { ...validarDatos(datos) };
  const correo = errorCorreo(datos.correo);
  if (correo) errores.correo = correo;
  const password = errorPassword(datos.password);
  if (password) errores.password = password;
  if (!datos.aceptaTerminosYPrivacidad) {
    errores.aceptaTerminosYPrivacidad = 'Para crear la cuenta debes aceptar los términos y condiciones y la política de privacidad.';
  }
  return errores;
}

// Fase 14B: «Completa tu registro» del ingreso con Google. El correo y la identidad ya vienen verificados por Google (no se
// piden) y no hay contraseña: la cuenta nace con Google. Las mismas reglas de los datos personales y el mismo consentimiento.
export const ORDEN_COMPLETAR_GOOGLE = ['nombre', 'apellido', 'telefono', 'rut', 'direccion', 'aceptaTerminosYPrivacidad'];

export function validarCompletarGoogle(datos) {
  const errores = { ...validarDatos(datos) };
  if (!datos.aceptaTerminosYPrivacidad) {
    errores.aceptaTerminosYPrivacidad = 'Para crear la cuenta debes aceptar los términos y condiciones y la política de privacidad.';
  }
  return errores;
}

/** Nueva contraseña con su repetición (recuperación y cambio de contraseña). */
export function validarNuevaPassword(password, repetida, campo = 'password') {
  const errores = {};
  const error = errorPassword(password, { vacia: 'Escribe la nueva contraseña.' });
  if (error) errores[campo] = error;
  else if (password !== repetida) errores.repetida = 'Las contraseñas no coinciden. Escríbela igual en los dos campos.';
  return errores;
}

export function validarCodigo(codigo = '') {
  return /^\d{6}$/.test(codigo.trim()) ? null : 'El código tiene 6 dígitos. Lo encuentras en el correo que te enviamos.';
}

/** Teléfono como lo guarda la API (9 dígitos), para mostrarlo legible: 9 1234 5678. */
export function telefonoLegible(telefono = '') {
  const t = String(telefono).replace(/\s/g, '');
  return /^\d{9}$/.test(t) ? `${t[0]} ${t.slice(1, 5)} ${t.slice(5)}` : telefono;
}

// ── Fase 11: Mis datos y baja de la cuenta ──

/** La frase que se escribe para confirmar la baja (la API la exige igual). */
export const FRASE_BAJA = 'DAR DE BAJA';

const dos = (n) => String(n).padStart(2, '0');

/** Nombre del archivo de «Descargar mis datos», como el que propone la API: mis-datos-letreros-herrera-AAAAMMDD.json. */
export function nombreArchivoDatos(fecha = new Date()) {
  return `mis-datos-letreros-herrera-${fecha.getFullYear()}${dos(fecha.getMonth() + 1)}${dos(fecha.getDate())}.json`;
}

/** Errores del diálogo de baja: la contraseña y la frase exacta (en mayúsculas). */
export function validarBaja({ password = '', confirmacion = '' }) {
  const errores = {};
  if (!password) errores.password = 'Escribe tu contraseña para confirmar.';
  if (confirmacion.trim() !== FRASE_BAJA) errores.confirmacion = `Escribe ${FRASE_BAJA}, en mayúsculas, para confirmar.`;
  return errores;
}
