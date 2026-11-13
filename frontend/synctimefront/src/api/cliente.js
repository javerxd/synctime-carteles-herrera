// Cliente HTTP de SyncTime sobre fetch. Una sola puerta para toda la API:
// - Traduce el formato único de error de la API (error, codigo, nivel, errores) a ErrorApi.
// - Un 401 con sesión avisa que la sesión venció; quien decide adónde ir es la sesión (según el tipo
//   de usuario), no este archivo. Un 401 sin sesión (por ejemplo, un ingreso fallido) solo se informa.
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
  servidor: 'El servidor tuvo un problema. Inténtalo de nuevo en unos minutos.',
};

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

let opciones = { obtenerToken: () => null, alSesionVencida: () => {} };

/** La sesión registra aquí cómo leer el token y qué hacer cuando vence. */
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

async function pedir(metodo, ruta, { cuerpo, parametros, senal } = {}) {
  const token = opciones.obtenerToken();
  const cabeceras = new Headers({ Accept: 'application/json' });
  if (token) cabeceras.set('Authorization', `Bearer ${token}`);
  if (cuerpo !== undefined) cabeceras.set('Content-Type', 'application/json');

  let respuesta;
  try {
    respuesta = await fetch(construirUrl(ruta, parametros), {
      method: metodo,
      headers: cabeceras,
      body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
      signal: senal,
    });
  } catch (error) {
    if (error?.name === 'AbortError') throw error;
    throw new ErrorApi({ estado: 0, codigo: 'sin_conexion', mensaje: MENSAJES.sin_conexion });
  }

  const datos = await leerCuerpo(respuesta);
  if (respuesta.ok) return datos;

  if (respuesta.status === 401 && token) opciones.alSesionVencida();
  throw errorDesde(respuesta, datos);
}

function leer(ruta, configuracion = {}) {
  // Las lecturas con señal propia no se comparten: cancelar una no debe cancelar la otra.
  if (configuracion.senal) return pedir('GET', ruta, configuracion);
  const clave = `${opciones.obtenerToken() ?? ''} ${construirUrl(ruta, configuracion.parametros)}`;
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
  delete: (ruta, configuracion) => pedir('DELETE', ruta, configuracion),
};
