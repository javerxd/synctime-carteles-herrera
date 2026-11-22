import L from 'leaflet';

// Teselas del mapa «Cómo llegar». OpenStreetMap bloquea las peticiones que no traen un Referer que
// identifique a la aplicación (política de uso de teselas): por eso la página ya no usa
// `referrer: no-referrer` y la capa pide con `strict-origin-when-cross-origin`, que envía solo el
// origen del sitio. Si el servidor rechaza el origen de desarrollo, VITE_MAPA_TESELAS permite cambiarlo
// (la CSP de index.html debe admitir ese servidor en `img-src`).
export const URL_TESELAS = import.meta.env.VITE_MAPA_TESELAS || 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';

export const OPCIONES_TESELAS = {
  maxZoom: 19,
  attribution: '© colaboradores de OpenStreetMap',
  referrerPolicy: 'strict-origin-when-cross-origin',
};

export function crearCapaTeselas() {
  return L.tileLayer(URL_TESELAS, OPCIONES_TESELAS);
}
