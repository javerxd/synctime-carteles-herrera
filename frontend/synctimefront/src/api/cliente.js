// Cliente HTTP de SyncTime sobre fetch. Una sola puerta para toda la API:
// - Traduce el formato único de error de la API (error, codigo, nivel, errores) a ErrorApi.
// - Un 401 con sesión pide a la sesión UNA renovación y, si resulta, repite la petición una sola vez.
//   Si no, avisa que la sesión venció; quien decide adónde ir es la sesión (según el tipo de usuario),
//   no este archivo. Un 401 sin sesión (por ejemplo, un ingreso fallido) solo se informa.
// - Las cookies de la sesión (renovación y CSRF) viajan solas: la API se sirve en el mismo origen.
// - Dos lecturas iguales al mismo tiempo comparten una sola petición.
const BASE = `${import.meta.env.VITE_API_URL ?? ''}/api/v1`;

const MENSAJES = {
  sin_conexion: 'No pudimos conectar con el servidor. Revisa tu conexión e inténtalo de nuevo.',
  400: 'Revisa los datos ingresados.',
  401: 'Debes iniciar sesión para continuar.',
  403: 'No tienes permiso para hacer esto.',
  404: 'No encontramos lo que buscas.',
  409: 'Otra persona cambió este registro. Recarga para ver la última versión.',
  429: 'Hiciste demasiados intentos. Espera un minuto y vuelve a intentarlo.',
  ocupado: 'Estamos recibiendo muchas solicitudes; vuelve a intentarlo en unos segundos.',
  tiempo_agotado: 'La subida tardó demasiado. Revisa tu conexión e inténtalo de nuevo.',
  servidor: 'El servidor tuvo un problema. Inténtalo de nuevo en unos minutos.',
};

/** Valor de una cookie legible por la página (por ejemplo, synctime_csrf), o null. */
export function leerCookie(nombre) {
  if (typeof document === 'undefined') return null;
  for (const parte of document.cookie.split(';')) {
    const [clave, ...valor] = parte.trim().split('=');
    if (clave === nombre) {
      try {
        return decodeURIComponent(valor.join('='));
      } catch {
        return valor.join('=');
      }
    }
  }
  return null;
}

// Una subida que no termina en 2 minutos se corta con un error claro.
const TIEMPO_MAXIMO_SUBIDA_MS = 120_000;

// 503 servicio_ocupado: se reintenta UNA vez tras Retry-After (en segundos), con un tope para no colgar la pantalla.
const ESPERA_OCUPADO_POR_DEFECTO_MS = 2000;
const ESPERA_OCUPADO_MAXIMA_MS = 15000;

/** Milisegundos a esperar según Retry-After (segundos o fecha HTTP). */
export function esperaReintento(valor) {
  if (valor === null || valor === undefined || valor === '') return ESPERA_OCUPADO_POR_DEFECTO_MS;
  const segundos = Number(valor);
  const ms = Number.isFinite(segundos) ? segundos * 1000 : Date.parse(valor) - Date.now();
  if (!Number.isFinite(ms)) return ESPERA_OCUPADO_POR_DEFECTO_MS;
  return Math.min(Math.max(ms, 0), ESPERA_OCUPADO_MAXIMA_MS);
}

const dormir = (ms, senal) => new Promise((resolver, rechazar) => {
  const temporizador = setTimeout(resolver, ms);
  senal?.addEventListener('abort', () => {
    clearTimeout(temporizador);
    rechazar(new DOMException('Cancelada', 'AbortError'));
  }, { once: true });
});

// Solo el 503 que la API marca como servicio_ocupado (no una caída del servidor ni las rutas de la sesión).
const estaOcupado = (estado, cuerpo) => estado === 503 && cuerpo?.codigo === 'servicio_ocupado';

function errorOcupado() {
  return new ErrorApi({ estado: 503, codigo: 'servicio_ocupado', mensaje: MENSAJES.ocupado });
}

export class ErrorApi extends Error {
  constructor({ estado = 0, codigo = 'desconocido', mensaje, errores = null, nivel = null }) {
    super(mensaje);
    this.name = 'ErrorApi';
    this.estado = estado;
    this.codigo = codigo;
    this.errores = errores;
    this.nivel = nivel;
  }
}

let opciones = { obtenerToken: () => null, renovarSesion: async () => false, alSesionVencida: () => {} };

/** La sesión registra aquí cómo leer el token, cómo renovarlo y qué hacer cuando vence. */
export function configurarClienteApi(nuevas) {
  opciones = { ...opciones, ...nuevas };
}

const enCurso = new Map();

function construirUrl(ruta, parametros) {
  const consulta = new URLSearchParams();
  for (const [clave, valor] of Object.entries(parametros ?? {})) {
    if (valor !== undefined && valor !== null && valor !== '') consulta.append(clave, String(valor));
  }
  const texto = consulta.toString();
  return `${BASE}${ruta}${texto ? `?${texto}` : ''}`;
}

async function leerCuerpo(respuesta) {
  if (respuesta.status === 204) return null;
  const tipo = respuesta.headers.get('Content-Type') ?? '';
  if (tipo.includes('json')) {
    try {
      return await respuesta.json();
    } catch {
      return null;
    }
  }
  return null;
}

function errorDesde(respuesta, cuerpo) {
  const estado = respuesta.status;
  const mensajePorEstado = MENSAJES[estado] ?? (estado >= 500 ? MENSAJES.servidor : MENSAJES[400]);
  return new ErrorApi({
    estado,
    codigo: cuerpo?.codigo ?? `http_${estado}`,
    mensaje: cuerpo?.error || cuerpo?.title || mensajePorEstado,
    errores: cuerpo?.errores ?? null,
    nivel: cuerpo?.nivel ?? null,
  });
}

/**
 * `cabeceras`: cabeceras extra (por ejemplo, X-CSRF-Token, If-Match o Idempotency-Key). `sinRenovar`: un 401 no
 * intenta renovar la sesión (las rutas de la propia sesión y la petición ya repetida). Una respuesta 2xx con
 * `Idempotent-Replayed: true` (la API repite la de un envío anterior con la misma clave) es un éxito como cualquier otro.
 */
async function pedir(metodo, ruta, configuracion = {}) {
  const {
    cuerpo, parametros, senal, cabeceras: extra, sinRenovar = false, comoArchivo = false, conCabeceras = false, reintentoOcupado = false,
  } = configuracion;
  const token = opciones.obtenerToken();
  const cabeceras = new Headers({ Accept: comoArchivo ? '*/*' : 'application/json' });
  if (token) cabeceras.set('Authorization', `Bearer ${token}`);
  if (cuerpo !== undefined) cabeceras.set('Content-Type', 'application/json');
  for (const [clave, valor] of Object.entries(extra ?? {})) {
    if (valor) cabeceras.set(clave, valor);
  }

  let respuesta;
  try {
    respuesta = await fetch(construirUrl(ruta, parametros), {
      method: metodo,
      headers: cabeceras,
      body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
      credentials: 'same-origin',
      // Los archivos privados no quedan en la caché del navegador.
      cache: comoArchivo ? 'no-store' : undefined,
      signal: senal,
    });
  } catch (error) {
    if (error?.name === 'AbortError') throw error;
    throw new ErrorApi({ estado: 0, codigo: 'sin_conexion', mensaje: MENSAJES.sin_conexion });
  }

  // Un archivo privado (adjunto) llega como Blob: la pantalla lo muestra con URL.createObjectURL.
  if (respuesta.ok && comoArchivo) return respuesta.blob();
  const datos = await leerCuerpo(respuesta);
  // `conCabeceras`: quien necesita el ETag (la versión del horario y del cartel) recibe también las cabeceras.
  if (respuesta.ok) return conCabeceras ? { datos, cabeceras: respuesta.headers } : datos;

  if (await debeRepetir(respuesta.status, token, sinRenovar)) return pedir(metodo, ruta, { ...configuracion, sinRenovar: true });
  if (!sinRenovar && estaOcupado(respuesta.status, datos)) {
    if (reintentoOcupado) throw errorOcupado();
    await dormir(esperaReintento(respuesta.headers.get('Retry-After')), senal);
    return pedir(metodo, ruta, { ...configuracion, reintentoOcupado: true });
  }
  throw errorDesde(respuesta, datos);
}

/**
 * Tras un 401 con sesión: pide UNA renovación. true si la petición debe repetirse con el token nuevo.
 * Solo si el token sigue siendo el de la sesión actual (una respuesta tardía de otra sesión no cuenta).
 */
async function debeRepetir(estado, token, sinRenovar) {
  if (estado !== 401 || !token || opciones.obtenerToken() !== token) return false;
  // renovarSesion: true | 'renovada' (repetir), 'pasajero' (sin conexión o falla del servidor: la
  // sesión sigue, solo se informa el error) o false | 'rechazada' (la sesión ya no sirve).
  const resultado = sinRenovar ? false : await opciones.renovarSesion();
  if (resultado === true || resultado === 'renovada') return true;
  if (resultado !== 'pasajero' && opciones.obtenerToken() === token) opciones.alSesionVencida();
  return false;
}

function cuerpoXhr(xhr) {
  if (xhr.status === 204 || !(xhr.getResponseHeader('Content-Type') ?? '').includes('json')) return null;
  try {
    return JSON.parse(xhr.responseText);
  } catch {
    return null;
  }
}

/**
 * Sube un archivo con multipart/form-data (campo `campo`) e informa el avance con `alProgreso(0..1)`.
 * Usa XMLHttpRequest porque fetch todavía no informa el avance de la subida en todos los navegadores.
 * `senal` (AbortSignal) cancela la subida. Igual que pedir(): errores como ErrorApi y una renovación tras 401.
 */
function subir(ruta, archivo, { campo = 'archivo', alProgreso, senal, sinRenovar = false, reintentoOcupado = false } = {}) {
  const token = opciones.obtenerToken();
  const intento = new Promise((resolver, rechazar) => {
    if (senal?.aborted) {
      rechazar(new DOMException('Subida cancelada', 'AbortError'));
      return;
    }
    const xhr = new XMLHttpRequest();
    xhr.open('POST', construirUrl(ruta));
    xhr.withCredentials = true;
    xhr.setRequestHeader('Accept', 'application/json');
    if (token) xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    xhr.upload.onprogress = (evento) => {
      if (evento.lengthComputable) alProgreso?.(evento.loaded / evento.total);
    };
    xhr.onload = () => resolver(xhr);
    xhr.onerror = () => rechazar(new ErrorApi({ estado: 0, codigo: 'sin_conexion', mensaje: MENSAJES.sin_conexion }));
    xhr.onabort = () => rechazar(new DOMException('Subida cancelada', 'AbortError'));
    xhr.timeout = TIEMPO_MAXIMO_SUBIDA_MS;
    xhr.ontimeout = () => rechazar(new ErrorApi({ estado: 0, codigo: 'tiempo_agotado', mensaje: MENSAJES.tiempo_agotado }));
    senal?.addEventListener('abort', () => xhr.abort(), { once: true });
    const formulario = new FormData();
    formulario.append(campo, archivo, archivo.name);
    xhr.send(formulario);
  });

  return intento.then(async (xhr) => {
    const datos = cuerpoXhr(xhr);
    if (xhr.status >= 200 && xhr.status < 300) {
      alProgreso?.(1);
      return datos;
    }
    if (await debeRepetir(xhr.status, token, sinRenovar)) return subir(ruta, archivo, { campo, alProgreso, senal, sinRenovar: true, reintentoOcupado });
    if (estaOcupado(xhr.status, datos)) {
      if (reintentoOcupado) throw errorOcupado();
      alProgreso?.(0);
      await dormir(esperaReintento(xhr.getResponseHeader('Retry-After')), senal);
      return subir(ruta, archivo, { campo, alProgreso, senal, sinRenovar, reintentoOcupado: true });
    }
    throw errorDesde({ status: xhr.status }, datos);
  });
}

/** La versión de un ETag («"AAAAAAAAB9E="» o W/"…») sin comillas, o null. */
export function versionDeEtag(etag) {
  const texto = String(etag ?? '').trim().replace(/^W\//, '');
  const sinComillas = texto.replace(/^"(.*)"$/, '$1');
  return sinComillas || null;
}

/** Cabecera de idempotencia de un formulario (fase 11): la misma clave en cada reintento del mismo envío. */
export const conClave = (clave) => (clave ? { cabeceras: { 'Idempotency-Key': clave } } : undefined);

/** Ruta de la API sin el prefijo /api/v1 (la API entrega algunas URL completas, como urlMiniatura). */
export function rutaRelativa(url) {
  return String(url ?? '').replace(/^(https?:\/\/[^/]+)?\/api\/v1(?=\/)/, '');
}

function leer(ruta, configuracion = {}) {
  // Las lecturas con señal propia no se comparten: cancelar una no debe cancelar la otra.
  if (configuracion.senal) return pedir('GET', ruta, configuracion);
  const clave = `${opciones.obtenerToken() ?? ''} ${configuracion.conCabeceras ? 'cabeceras ' : ''}${construirUrl(ruta, configuracion.parametros)}`;
  const existente = enCurso.get(clave);
  if (existente) return existente;
  const promesa = pedir('GET', ruta, configuracion).finally(() => enCurso.delete(clave));
  enCurso.set(clave, promesa);
  return promesa;
}

export const api = {
  get: leer,
  post: (ruta, cuerpo, configuracion) => pedir('POST', ruta, { ...configuracion, cuerpo }),
  put: (ruta, cuerpo, configuracion) => pedir('PUT', ruta, { ...configuracion, cuerpo }),
  patch: (ruta, cuerpo, configuracion) => pedir('PATCH', ruta, { ...configuracion, cuerpo }),
  delete: (ruta, configuracion) => pedir('DELETE', ruta, configuracion),
  /** Descarga un archivo con la sesión (adjuntos privados) y lo entrega como Blob. */
  archivo: (ruta, configuracion) => pedir('GET', ruta, { ...configuracion, comoArchivo: true }),
  /** Un POST cuya respuesta es un archivo (por ejemplo, «Descargar mis datos», que pide la contraseña en el cuerpo). */
  postArchivo: (ruta, cuerpo, configuracion) => pedir('POST', ruta, { ...configuracion, cuerpo, comoArchivo: true }),
  subir,
};
