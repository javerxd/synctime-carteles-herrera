import { CircleDashed, ShieldCheck, ShieldOff, TriangleAlert } from 'lucide-react';

/** Estados de «Rostros registrados» (S16): texto, tono e icono, para que nada dependa solo del color. */
export const ESTADOS_ROSTRO = {
  activo: { texto: 'Activo', tono: 'exito', icono: ShieldCheck },
  debe_registrarse: { texto: 'Debe registrarse de nuevo', tono: 'aviso', icono: TriangleAlert },
  sin_registro: { texto: 'Sin registro', tono: 'neutro', icono: CircleDashed },
  desactivado: { texto: 'Desactivado', tono: 'neutro', icono: ShieldOff },
};

export const ETIQUETAS_CALIBRACION = { propio: 'Soy yo', otra_persona: 'Otra persona' };

/** Fecha corta en hora de Chile, o una raya si no hay. */
export function fechaCorta(iso) {
  if (!iso) return '—';
  const fecha = new Date(iso);
  if (Number.isNaN(fecha.getTime())) return '—';
  return fecha.toLocaleDateString('es-CL', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'America/Santiago' });
}

/** Fecha y hora corta en hora de Chile. */
export function fechaHora(iso) {
  if (!iso) return '—';
  const fecha = new Date(iso);
  if (Number.isNaN(fecha.getTime())) return '—';
  return fecha.toLocaleString('es-CL', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', timeZone: 'America/Santiago' });
}

/** Un número con tres decimales (0,520), o una raya. */
export function decimal(valor) {
  return Number.isFinite(valor) ? valor.toLocaleString('es-CL', { minimumFractionDigits: 3, maximumFractionDigits: 3 }) : '—';
}

/** registradoPor puede llegar como texto o como { nombre }. */
export function nombreDe(valor) {
  if (!valor) return '—';
  return typeof valor === 'string' ? valor : valor.nombre ?? '—';
}

/** La lista puede llegar como arreglo o paginada ({ elementos }). */
export function filasDe(respuesta) {
  if (Array.isArray(respuesta)) return respuesta;
  return Array.isArray(respuesta?.elementos) ? respuesta.elementos : [];
}

/** Puede registrarse (o volver a registrarse) quien no tiene un rostro activo con las tres muestras. */
export const puedeRegistrar = (fila) => fila.estado !== 'activo';
/** Puede desactivarse quien tiene un rostro guardado, aunque le falten las muestras nuevas. */
export const puedeDesactivar = (fila) => fila.estado === 'activo' || fila.estado === 'debe_registrarse';
