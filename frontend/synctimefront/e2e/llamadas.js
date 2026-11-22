// Fase 15B (RNFP23): cuenta las peticiones a la API que hace una pantalla y marca las idénticas que salen más de una vez sin motivo.
// Lo usan los recorridos de Playwright; la lógica pura (`llamadasDuplicadas`, `resumirLlamadas`) se prueba en
// src/test/fase15bLlamadas.test.js con listas armadas a mano.

const ES_API = /\/api\/v1\//;

/**
 * Empieza a anotar las peticiones a /api/v1/**. Devuelve la lista viva: { metodo, url, cuerpo, abortada }. Una petición que el
 * navegador cancela (el cliente HTTP la aborta al cambiar de pantalla, o StrictMode en desarrollo monta dos veces) queda
 * `abortada` y no cuenta como llamada.
 */
export function registrarLlamadas(page) {
  const lista = [];
  const porPeticion = new Map();
  page.on('request', (peticion) => {
    if (!ES_API.test(peticion.url())) return;
    const url = new URL(peticion.url());
    const llamada = { metodo: peticion.method(), url: `${url.pathname}${url.search}`, cuerpo: peticion.postData(), abortada: false };
    porPeticion.set(peticion, llamada);
    lista.push(llamada);
  });
  page.on('requestfailed', (peticion) => {
    const llamada = porPeticion.get(peticion);
    if (llamada && /ABORTED/i.test(peticion.failure()?.errorText ?? '')) llamada.abortada = true;
  });
  return lista;
}

/**
 * Las llamadas idénticas repetidas: [{ clave, veces }]. Una lectura es idéntica si tiene el mismo método y la misma dirección con su
 * consulta; un envío, si además lleva el mismo cuerpo. Una escritura separa las lecturas (volver a pedir la ficha después de guardar
 * es lo correcto). `permitidas` son expresiones de direcciones que se repiten a propósito (por ejemplo, consultar un estado hasta
 * que termine).
 */
export function llamadasDuplicadas(llamadas, { permitidas = [] } = {}) {
  const duplicadas = new Map();
  let lecturas = new Map();
  const envios = new Map();
  for (const { metodo, url, cuerpo, abortada } of llamadas) {
    if (abortada || permitidas.some((patron) => patron.test(url))) continue;
    const esLectura = metodo === 'GET' || metodo === 'HEAD';
    const clave = esLectura ? `${metodo} ${url}` : `${metodo} ${url} ${cuerpo ?? ''}`.trimEnd();
    const vistas = esLectura ? lecturas : envios;
    const veces = (vistas.get(clave) ?? 0) + 1;
    vistas.set(clave, veces);
    if (veces > 1) duplicadas.set(clave, veces);
    if (!esLectura) lecturas = new Map();
  }
  return [...duplicadas].map(([clave, veces]) => ({ clave, veces }));
}

/** Cuántas llamadas hizo la pantalla (sin las canceladas): { total, lecturas, escrituras }. */
export function resumirLlamadas(llamadas) {
  const vigentes = llamadas.filter((l) => !l.abortada);
  const lecturas = vigentes.filter((l) => l.metodo === 'GET' || l.metodo === 'HEAD').length;
  return { total: vigentes.length, lecturas, escrituras: vigentes.length - lecturas };
}
