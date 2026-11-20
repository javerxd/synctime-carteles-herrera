import { MAXIMO_CARACTERES, errorNombre } from '../../../utilidades/reglaNombre';

// Reglas del formulario de contacto: las mismas que valida la API (Negocio/Portada/ValidacionContacto.cs),
// para que el visitante vea el mismo mensaje antes y después de enviar.
export const LIMITES = { nombre: MAXIMO_CARACTERES, asunto: 150, mensajeMinimo: 10, mensaje: 2000 };

const CORREO = /^[^\s@;,"]+@[^\s@;,"]+\.[^\s@;,"]+$/;

function esTelefono(valor) {
  let limpio = valor.replace(/\s/g, '');
  if (limpio.startsWith('+56')) limpio = limpio.slice(3);
  return /^\d{9}$/.test(limpio);
}

/** Errores por campo, en español. Un objeto vacío significa que se puede enviar. */
export function validarContacto({ nombre = '', correo = '', telefono = '', asunto = '', mensaje = '', aceptaPrivacidad = false }) {
  const errores = {};
  const errorDelNombre = errorNombre(nombre, 'Escribe tu nombre.');
  if (errorDelNombre) errores.nombre = errorDelNombre;

  const c = correo.trim();
  if (!CORREO.test(c) || c.length > 254) errores.correo = 'Escribe un correo válido, por ejemplo nombre@correo.cl, para poder responderte.';

  if (telefono.trim() && !esTelefono(telefono)) errores.telefono = 'El teléfono debe tener 9 dígitos, por ejemplo 9 1234 5678. Es opcional.';

  const a = asunto.trim();
  if (!a) errores.asunto = 'Escribe el asunto de tu consulta.';
  else if (a.length > LIMITES.asunto) errores.asunto = `El asunto puede tener hasta ${LIMITES.asunto} caracteres.`;

  const m = mensaje.trim();
  if (m.length < LIMITES.mensajeMinimo) errores.mensaje = `Cuéntanos un poco más: el mensaje debe tener al menos ${LIMITES.mensajeMinimo} caracteres.`;
  else if (m.length > LIMITES.mensaje) errores.mensaje = `El mensaje puede tener hasta ${LIMITES.mensaje} caracteres.`;

  if (!aceptaPrivacidad) errores.aceptaPrivacidad = 'Para enviar la consulta debes aceptar la política de privacidad.';
  return errores;
}

export const ORDEN_CAMPOS = ['nombre', 'correo', 'telefono', 'asunto', 'mensaje', 'aceptaPrivacidad'];
