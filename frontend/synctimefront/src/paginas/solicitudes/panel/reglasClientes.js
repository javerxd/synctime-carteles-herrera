import { esTelefono, LIMITES } from '../../cuenta/reglasCuenta';
import { formatearRut, validarRut } from '../../../utilidades/rut';

// Reglas para que el personal busque, cree o corrija un cliente (mismas reglas que la cuenta del cliente).
export const EDITAN_CLIENTES = ['dueño', 'administrador', 'operario'];
const CORREO = /^[^\s@;,"]+@[^\s@;,"]+\.[^\s@;,"]+$/;

/** «+56 9 1234 5678» → «912345678»; lo que no es teléfono queda igual (sin espacios). */
export function telefonoNormalizado(valor) {
  let limpio = String(valor ?? '').replace(/[\s-]/g, '');
  if (limpio.startsWith('+56')) limpio = limpio.slice(3);
  return limpio;
}

/** El RUT como lo guarda la API: 12.345.678-5. */
export const rutNormalizado = (valor) => (String(valor ?? '').trim() ? formatearRut(valor) : '');

/** Buscar: al menos uno, y bien escrito. */
export function validarBusqueda({ telefono = '', rut = '' }) {
  const errores = {};
  if (!telefono.trim() && !rut.trim()) {
    errores.telefono = 'Escribe el teléfono o el RUT del cliente para buscarlo.';
    return errores;
  }
  if (telefono.trim() && !esTelefono(telefono)) errores.telefono = 'Escribe un teléfono de 9 dígitos, por ejemplo 9 1234 5678.';
  if (rut.trim() && !validarRut(rut)) errores.rut = 'El RUT no es válido. Revisa el número y el dígito verificador, por ejemplo 12.345.678-5.';
  return errores;
}

export const ORDEN_CLIENTE = ['nombre', 'apellido', 'telefono', 'rut', 'email', 'direccion'];

/** Datos de un cliente que registra o corrige el personal. */
export function validarCliente({ nombre = '', apellido = '', telefono = '', rut = '', email = '', direccion = '' }) {
  const errores = {};
  if (!nombre.trim()) errores.nombre = 'Escribe el nombre del cliente.';
  else if (nombre.trim().length > LIMITES.nombre) errores.nombre = `Usa hasta ${LIMITES.nombre} caracteres.`;
  if (!apellido.trim()) errores.apellido = 'Escribe el apellido del cliente (o el nombre de su negocio).';
  else if (apellido.trim().length > LIMITES.nombre) errores.apellido = `Usa hasta ${LIMITES.nombre} caracteres.`;
  if (!esTelefono(telefono)) errores.telefono = 'Escribe un teléfono de 9 dígitos, por ejemplo 9 1234 5678.';
  if (rut.trim() && !validarRut(rut)) errores.rut = 'El RUT no es válido. Revisa el número y el dígito verificador, por ejemplo 12.345.678-5.';
  if (email.trim() && (!CORREO.test(email.trim()) || email.trim().length > LIMITES.correo)) errores.email = 'Escribe un correo válido, por ejemplo nombre@correo.cl, o déjalo en blanco.';
  if (direccion.trim().length > LIMITES.direccion) errores.direccion = `Usa hasta ${LIMITES.direccion} caracteres.`;
  return errores;
}

/** Los datos del formulario como los pide la API. */
export function clienteParaApi(datos) {
  return {
    nombre: datos.nombre.trim(),
    apellido: datos.apellido.trim(),
    telefono: telefonoNormalizado(datos.telefono),
    rut: rutNormalizado(datos.rut) || null,
    email: datos.email.trim() || null,
    direccion: datos.direccion?.trim() || null,
  };
}
