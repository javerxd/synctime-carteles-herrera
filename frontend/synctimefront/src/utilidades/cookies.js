// Preferencia de cookies del visitante (RFS12). Hoy el sitio no tiene analítica ni rastreadores: solo
// usa almacenamiento técnico (la sesión y esta misma preferencia). Si algún día se agrega analítica,
// debe cargarse solo cuando `analiticaPermitida()` sea verdadero.
const CLAVE = 'synctime_cookies';
const CLAVE_VISITANTE = 'synctime_visitante';
const EVENTO = 'synctime:preferencias-cookies';

function leerAlmacen(clave) {
  try {
    return window.localStorage.getItem(clave);
  } catch {
    return null;
  }
}

function guardarAlmacen(clave, valor) {
  try {
    window.localStorage.setItem(clave, valor);
    return true;
  } catch {
    return false;
  }
}

/** { analitica, fecha } o null si el visitante aún no decide. */
export function leerPreferencia() {
  try {
    const dato = JSON.parse(leerAlmacen(CLAVE) ?? 'null');
    return dato && typeof dato.analitica === 'boolean' ? dato : null;
  } catch {
    return null;
  }
}

export function guardarPreferencia(analitica) {
  guardarAlmacen(CLAVE, JSON.stringify({ analitica, fecha: new Date().toISOString() }));
}

export const analiticaPermitida = () => leerPreferencia()?.analitica === true;

/** Identificador anónimo y aleatorio de este navegador, para registrar su aceptación. */
export function identificadorVisitante() {
  let id = leerAlmacen(CLAVE_VISITANTE);
  if (!id) {
    id = crypto.randomUUID();
    guardarAlmacen(CLAVE_VISITANTE, id);
  }
  return id;
}

/** Vuelve a mostrar el aviso de cookies (enlace «Preferencias de cookies» del pie). */
export function abrirPreferenciasCookies() {
  window.dispatchEvent(new Event(EVENTO));
}

export function alPedirPreferencias(funcion) {
  window.addEventListener(EVENTO, funcion);
  return () => window.removeEventListener(EVENTO, funcion);
}
