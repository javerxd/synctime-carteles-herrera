import { ErrorApi } from '../api/cliente';

export const URL_SCRIPT_GOOGLE = 'https://accounts.google.com/gsi/client';
const TIEMPO_MAXIMO_MS = 15_000;

export const MENSAJE_SCRIPT_BLOQUEADO = 'El script de Google no llegó. Revisa tu conexión o si un bloqueador lo impide.';

let carga = null;

const falla = () => new ErrorApi({ estado: 0, codigo: 'google_script', mensaje: MENSAJE_SCRIPT_BLOQUEADO });
const identidadDe = () => window.google?.accounts?.id ?? null;
const scriptDePagina = () => document.querySelector(`script[src="${URL_SCRIPT_GOOGLE}"]`);

/**
 * Carga el script de Google Identity Services SOLO cuando alguien lo pide (al pulsar «Continuar con Google»): así Google no
 * se entera de quién visita la página (fase 14B, decisión 5). Resuelve con `google.accounts.id`. Si el script no llega
 * (bloqueador, sin red, tiempo agotado) rechaza con un ErrorApi `google_script` y se puede reintentar. Dos pedidos a la vez
 * comparten el mismo script, y un reintento reutiliza el <script> que ya está en la página (quitarlo no cancela la descarga:
 * crear otro cargaría Google dos veces); si llegó tarde, el reintento resuelve de inmediato.
 */
export function cargarGoogle() {
  if (identidadDe()) return Promise.resolve(identidadDe());
  if (carga) return carga;

  carga = new Promise((resolver, rechazar) => {
    const existente = scriptDePagina();
    const script = existente ?? document.createElement('script');
    let temporizador = null;

    function terminar(error) {
      clearTimeout(temporizador);
      script.removeEventListener('load', alCargar);
      script.removeEventListener('error', alFallar);
      if (error) {
        // El <script> se queda: si llega tarde, el próximo intento ya encuentra `google.accounts.id`. Solo uno que falló se quita.
        if (error.codigo === 'google_script' && script.dataset.fallo === '1') script.remove();
        carga = null;
        rechazar(error);
      } else {
        resolver(identidadDe());
      }
    }

    function alCargar() {
      terminar(identidadDe() ? null : falla());
    }
    function alFallar() {
      script.dataset.fallo = '1';
      terminar(falla());
    }

    script.addEventListener('load', alCargar);
    script.addEventListener('error', alFallar);
    temporizador = setTimeout(() => terminar(falla()), TIEMPO_MAXIMO_MS);
    if (!existente) {
      script.src = URL_SCRIPT_GOOGLE;
      script.async = true;
      document.head.appendChild(script);
    }
  });
  return carga;
}

/** Solo para las pruebas: olvida una carga anterior. */
export function reiniciarCargaGoogle() {
  carga = null;
}
