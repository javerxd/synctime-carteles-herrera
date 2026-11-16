import { useCallback, useState } from 'react';
import { ErrorApi } from '../../api/cliente';

const MENSAJES_CLAVE = {
  // La API ya recibió este formulario con otros datos (por ejemplo, se editó tras un corte): puede que ya esté guardado.
  clave_reutilizada: 'Ya habíamos recibido este formulario con otros datos. Revisa si quedó registrado; si no, vuelve a enviarlo.',
  en_proceso: 'Todavía estamos guardando el envío anterior. Espera unos segundos y vuelve a intentarlo.',
};

/**
 * Traduce los 409 propios de la idempotencia. Con `clave_reutilizada` la clave se renueva, para que un nuevo envío
 * deliberado (tras revisar) no choque otra vez. Devuelve la falla tal cual si no es de la clave.
 */
export function fallaDeClave(falla, renovar) {
  const mensaje = falla?.estado === 409 ? MENSAJES_CLAVE[falla.codigo] : null;
  if (!mensaje) return falla;
  if (falla.codigo === 'clave_reutilizada') renovar?.();
  return new ErrorApi({ estado: 409, codigo: falla.codigo, mensaje });
}

/** Un UUID v4. `crypto.randomUUID` existe en contextos seguros (https y localhost); si no, se arma con getRandomValues. */
export function nuevaClave() {
  const c = globalThis.crypto;
  if (typeof c?.randomUUID === 'function') return c.randomUUID();
  const bytes = c.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = [...bytes].map((b) => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

/**
 * Clave de idempotencia de un formulario de creación (fase 11, Idempotency-Key). Nace al abrirse el formulario y es
 * la misma en cada reintento del mismo envío (un corte de red, un doble clic): así la API no crea dos registros.
 * Cambia solo tras un éxito (`renovar()`) o al reabrir el formulario: un diálogo pasa `abierto`, y cada vez que vuelve
 * a abrirse recibe una clave nueva.
 */
export function useClaveIdempotencia(abierto = true) {
  const [clave, setClave] = useState(nuevaClave);
  const [abiertoAntes, setAbiertoAntes] = useState(abierto);
  if (abierto !== abiertoAntes) {
    setAbiertoAntes(abierto);
    if (abierto) setClave(nuevaClave());
  }
  const renovar = useCallback(() => setClave(nuevaClave()), []);
  return { clave, renovar };
}
