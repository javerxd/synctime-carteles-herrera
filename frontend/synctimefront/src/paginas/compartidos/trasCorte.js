import { ErrorApi } from '../../api/cliente';

// Registrar un abono, crear un pedido o emitir una cotización no tienen clave de idempotencia en la API. Si la
// respuesta se pierde (corte de red o tiempo agotado), no sabemos si se guardó: antes de dejar intentarlo de
// nuevo, la pantalla relee el pedido o la cotización y lo compara. Así un reintento no duplica un abono.

/** ¿Se perdió la respuesta? (sin conexión o tiempo agotado; no una cancelación propia). */
export const sinRespuesta = (falla) => falla?.estado === 0 && falla?.name !== 'AbortError';

export const TRAS_CORTE = {
  noRegistrado: 'no_registrado_tras_corte',
  sinConfirmar: 'sin_confirmar',
};

/**
 * El error que se muestra después de revisar. `resultado`: 'no_registrado' (revisamos y no aparece: se puede
 * reintentar) o 'sin_confirmar' (tampoco se pudo revisar: hay que revisar antes de reintentar).
 */
export function errorTrasCorte(resultado, { que = 'se registró', donde = 'la ficha' } = {}) {
  if (resultado === 'no_registrado') {
    return new ErrorApi({
      estado: 0, codigo: TRAS_CORTE.noRegistrado,
      mensaje: `Se perdió la conexión y no supimos si ${que}. Revisamos ${donde} y no aparece: puedes intentarlo de nuevo.`,
    });
  }
  return new ErrorApi({
    estado: 0, codigo: TRAS_CORTE.sinConfirmar,
    mensaje: `Se perdió la conexión y no supimos si ${que}. Tampoco pudimos revisar ${donde}: revísala antes de intentarlo de nuevo.`,
  });
}

/** Mientras no se confirme qué pasó, no se puede volver a enviar. */
export const bloqueaReintento = (falla) => falla?.codigo === TRAS_CORTE.sinConfirmar;
