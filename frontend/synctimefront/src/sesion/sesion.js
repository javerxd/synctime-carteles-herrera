import { useSyncExternalStore } from 'react';
import authService, { COOKIE_CSRF } from '../api/authService';
import { configurarClienteApi, ErrorApi, leerCookie } from '../api/cliente';
import { google } from '../api/google';
import { paseGoogle } from '../google/pase';

// Sesión del usuario: personal (empleado) o cliente (fase 6, RNFP06-08).
// - El token de acceso (JWT de 15 minutos) vive SOLO en la memoria de este módulo: nunca en
//   sessionStorage ni localStorage, así un script ajeno no puede leerlo del almacenamiento.
// - La sesión larga la guarda la API en una cookie httpOnly; con ella se pide un token nuevo a
//   /auth/renovar al cargar la página, un minuto antes de que venza el actual y tras un 401.
// - En sessionStorage solo queda una marca sin datos («hay sesión») para saber si vale la pena renovar.
// - Las pestañas se avisan por BroadcastChannel cuando alguien ingresa o sale.
// Vive fuera de React para que el cliente HTTP lea el token sin depender del orden de los componentes.
export const MARCA = 'synctime_hay_sesion';
const ANTICIPO_RENOVACION_MS = 60 * 1000;
const ESPERA_MINIMA_MS = 30 * 1000;
const ESPERA_CONFLICTO_MS = 300;
const REINTENTO_SIN_CONEXION_MS = 30 * 1000;
const LOCK = 'synctime-renovar';
const CANAL = 'synctime-sesion';

function cargaDe(token) {
  try {
    const carga = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(atob(carga));
  } catch {
    return null;
  }
}

/**
 * Cuánto dura el token, medido sin depender del reloj del equipo: (exp - iat) del propio token. Sin
 * iat, se usa el reloj local como último recurso. null si el token no trae vencimiento.
 */
function duracionDe(token) {
  const carga = cargaDe(token);
  if (typeof carga?.exp !== 'number') return null;
  if (typeof carga.iat === 'number') return (carga.exp - carga.iat) * 1000;
  return carga.exp * 1000 - Date.now();
}

const ahoraMonotono = () => (typeof performance !== 'undefined' ? performance.now() : Date.now());

function marcar(hay) {
  try {
    if (hay) sessionStorage.setItem(MARCA, '1');
    else sessionStorage.removeItem(MARCA);
  } catch {
    // Sin almacenamiento (modo privado estricto): la sesión igual funciona en esta pestaña.
  }
}

function hayMarca() {
  try {
    return sessionStorage.getItem(MARCA) === '1';
  } catch {
    return false;
  }
}

const esperar = (ms) => new Promise((resolver) => { setTimeout(resolver, ms); });

/** La respuesta de ingreso, registro o renovación tiene la forma esperada. */
function esSesionValida(respuesta) {
  return typeof respuesta?.token === 'string' && respuesta.token.length > 0
    && (respuesta.tipo === 'cliente' || respuesta.tipo === 'empleado')
    && typeof respuesta.usuario === 'object' && respuesta.usuario !== null;
}

/** Lo que la API devuelve ({ token, tipo, usuario }) al usuario que usan las pantallas. */
function usuarioDesde({ tipo, usuario }) {
  if (tipo === 'cliente') return { ...usuario, tipo: 'cliente', rol: 'cliente', id: usuario.idCliente };
  return { ...usuario, tipo: 'empleado', id: usuario.idEmpleado };
}

let token = null;
// Momento (reloj monótono) en que vence el token actual.
let venceLocal = null;
let estado = { usuario: null, comprobando: false, falloComprobacion: false, motivoCierre: null };
const oyentes = new Set();
let temporizador = null;
let renovacionEnCurso = null;
// Cambia con cada ingreso o cierre: una renovación que termina tarde no revive una sesión ya cerrada
// ni pisa la de quien acaba de ingresar.
let generacion = 0;

let canal = null;
function obtenerCanal() {
  if (canal || typeof BroadcastChannel === 'undefined') return canal;
  canal = new BroadcastChannel(CANAL);
  canal.onmessage = ({ data }) => {
    if (data?.tipo === 'salida' && token) cerrarLocal('salida');
    // Otra pestaña ingresó: se pide un token con la cookie nueva. Si es otra persona, renovar cierra esta sesión.
    if (data?.tipo === 'ingreso') {
      if (token) renovar();
      else if (!renovacionEnCurso) almacenSesion.iniciar();
    }
  };
  return canal;
}

function difundir(tipo) {
  try {
    obtenerCanal()?.postMessage({ tipo });
  } catch {
    // Sin canal entre pestañas: cada una se entera en su próxima renovación.
  }
}

function publicar(cambios) {
  estado = { ...estado, ...cambios };
  oyentes.forEach((oyente) => oyente());
}

function programarRenovacion() {
  clearTimeout(temporizador);
  temporizador = null;
  if (venceLocal === null) return;
  // Piso de 30 s: un reloj desfasado o un token muy corto nunca provocan un bucle de renovaciones.
  const espera = Math.min(Math.max(venceLocal - ahoraMonotono() - ANTICIPO_RENOVACION_MS, ESPERA_MINIMA_MS), 2 ** 31 - 1);
  temporizador = setTimeout(renovarProgramada, espera);
}

async function renovarProgramada() {
  const resultado = await renovar();
  if (resultado !== 'pasajero' || !token) return;
  // Falla pasajera: se reintenta mientras el token siga vigente; vencido, la sesión se da por cerrada.
  if (venceLocal !== null && venceLocal - ahoraMonotono() > REINTENTO_SIN_CONEXION_MS) {
    clearTimeout(temporizador);
    temporizador = setTimeout(renovarProgramada, REINTENTO_SIN_CONEXION_MS);
  } else {
    cerrarLocal('vencida');
  }
}

function aplicar(respuesta) {
  generacion += 1;
  token = respuesta.token;
  const duracion = duracionDe(token);
  venceLocal = duracion === null ? null : ahoraMonotono() + duracion;
  marcar(true);
  programarRenovacion();
  obtenerCanal();
  publicar({ usuario: usuarioDesde(respuesta), comprobando: false, falloComprobacion: false, motivoCierre: null });
}

/** Un ingreso nuevo (no una renovación): se avisa a las demás pestañas. */
function aplicarIngreso(respuesta) {
  aplicar(respuesta);
  difundir('ingreso');
}

function cerrarLocal(motivo = null) {
  generacion += 1;
  token = null;
  venceLocal = null;
  // Una renovación pendiente de la sesión cerrada ya no cuenta (su resultado se descarta por la generación).
  renovacionEnCurso = null;
  clearTimeout(temporizador);
  temporizador = null;
  // Un pase de registro con Google pendiente no sobrevive a un cierre de sesión (equipos compartidos).
  paseGoogle.limpiar();
  marcar(false);
  publicar({ usuario: null, comprobando: false, falloComprobacion: false, motivoCierre: motivo });
}

const conTiempoMaximo = () => (typeof AbortSignal?.timeout === 'function' ? AbortSignal.timeout(10_000) : undefined);

/** Una petición a /renovar; si otra pestaña renovaba con la misma cookie (409), espera y reintenta una vez. */
async function pedirRenovacion() {
  try {
    return await authService.renovar(conTiempoMaximo());
  } catch (error) {
    if (error?.estado !== 409) throw error;
    await esperar(ESPERA_CONFLICTO_MS);
    return authService.renovar(conTiempoMaximo());
  }
}

/** Entre pestañas, una renovación a la vez (Web Locks); sin esa API, sin bloqueo. */
function conBloqueo(tarea) {
  const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined;
  return typeof locks?.request === 'function' ? locks.request(LOCK, tarea) : tarea();
}

const RESPUESTA_INVALIDA = Symbol('respuesta-invalida');

/**
 * Pide un token nuevo con la cookie de renovación. Dentro de la pestaña, quien llega mientras otra
 * renovación está en curso espera esa misma. Resuelve 'renovada', 'rechazada' (la sesión se cierra),
 * 'pasajero' (sin conexión, tiempo agotado, falla del servidor o respuesta ilegible: la sesión local
 * queda como estaba) o 'descartada'. Nunca deja `comprobando` colgado.
 */
function renovar() {
  if (renovacionEnCurso) return renovacionEnCurso;
  const desde = generacion;
  const anterior = estado.usuario;
  const enComprobacion = estado.comprobando;

  function pasajero() {
    if (estado.comprobando) publicar({ comprobando: false, falloComprobacion: enComprobacion });
    return 'pasajero';
  }

  const esta = Promise.resolve()
    .then(() => conBloqueo(pedirRenovacion))
    .then((respuesta) => {
      if (generacion !== desde) return 'descartada';
      if (!esSesionValida(respuesta)) throw RESPUESTA_INVALIDA;
      const nuevo = usuarioDesde(respuesta);
      // Otra persona (u otro tipo de cuenta) ingresó en otra pestaña: esta sesión no la hereda en silencio.
      if (anterior && (anterior.tipo !== nuevo.tipo || anterior.id !== nuevo.id)) {
        cerrarLocal('vencida');
        return 'rechazada';
      }
      aplicar(respuesta);
      return 'renovada';
    })
    .catch((error) => {
      if (generacion !== desde) return 'descartada';
      // Solo 400 (falta la cookie o el CSRF) y 401 dicen que la sesión ya no existe.
      if (error !== RESPUESTA_INVALIDA && (error?.estado === 400 || error?.estado === 401)) {
        cerrarLocal(anterior ? 'vencida' : null);
        return 'rechazada';
      }
      return pasajero();
    })
    .finally(() => { if (renovacionEnCurso === esta) renovacionEnCurso = null; });
  renovacionEnCurso = esta;
  return esta;
}

export const almacenSesion = {
  obtener: () => estado,
  suscribir(oyente) {
    oyentes.add(oyente);
    return () => oyentes.delete(oyente);
  },
  /** El token actual (solo en memoria). */
  obtenerToken: () => token,
  /** Inicia la sesión con la respuesta de la API: { token, tipo, usuario }. */
  aplicar,
  /** motivo: 'salida' (el usuario salió) o 'vencida' (la API la rechazó o expiró). */
  cerrar: cerrarLocal,
  renovar,
  /**
   * Al cargar la aplicación: si esta pestaña tenía sesión (marca) o el navegador tiene la cookie de la
   * sesión (CSRF), la recupera con /renovar. Mientras tanto `comprobando` es verdadero y las guardas esperan.
   */
  iniciar() {
    obtenerCanal();
    if (token || renovacionEnCurso) return renovacionEnCurso ?? Promise.resolve('renovada');
    if (!hayMarca() && !leerCookie(COOKIE_CSRF)) {
      if (estado.falloComprobacion) publicar({ falloComprobacion: false });
      return Promise.resolve('sin_sesion');
    }
    publicar({ comprobando: true, falloComprobacion: false });
    return renovar();
  },
};

configurarClienteApi({
  obtenerToken: () => token,
  renovarSesion: async () => (token ? renovar() : false),
  alSesionVencida: () => { if (token) cerrarLocal('vencida'); },
});

function normalizar(texto) {
  return String(texto ?? '').normalize('NFC').toLowerCase();
}

async function ingresarPersonal(email, password) {
  aplicarIngreso(await authService.login(email, password));
}

/** Ingreso con rostro y prueba de vida (fase 10). Devuelve la respuesta: trae { nombre, rol } para mostrarlos (S15). */
async function ingresarPersonalConRostro(email, { desafio, capturas }) {
  const respuesta = await authService.loginFacial(email, { desafio, capturas });
  aplicarIngreso(respuesta);
  return respuesta;
}

async function ingresarCliente(correo, password) {
  aplicarIngreso(await authService.loginCliente(correo, password));
}

/** Crea la cuenta del cliente; la API deja la sesión iniciada. */
async function registrarCliente(datos) {
  aplicarIngreso(await authService.registrarCliente(datos));
}

/**
 * Ingreso con Google (fase 14B): POST /auth/google. Devuelve { estado: 'sesion', tipo } si se abrió la sesión (la identidad ya
 * estaba vinculada; `tipo` es 'cliente' o, desde la fase 14C, 'empleado') o { estado: 'registro_pendiente' } si el correo es nuevo: el pase queda SOLO en memoria (paseGoogle)
 * y sigue la pantalla «Completa tu registro». Un 409 cuenta_existente llega como ErrorApi.
 */
async function ingresarConGoogle(idToken, nonce) {
  const respuesta = await google.ingresar(idToken, nonce);
  if (respuesta?.estado === 'registro_pendiente') {
    paseGoogle.guardar(respuesta);
    return { estado: 'registro_pendiente' };
  }
  if (!esSesionValida(respuesta)) throw new ErrorApi({ estado: 0, codigo: 'respuesta_invalida', mensaje: 'La respuesta del servidor no es válida. Inténtalo de nuevo.' });
  aplicarIngreso(respuesta);
  return { estado: 'sesion', tipo: respuesta.tipo };
}

/** Crea la cuenta con Google (POST /auth/google/completar, 201) y deja la sesión iniciada. */
async function completarRegistroGoogle(datos) {
  const respuesta = await google.completar(datos);
  if (!esSesionValida(respuesta)) throw new ErrorApi({ estado: 0, codigo: 'respuesta_invalida', mensaje: 'La respuesta del servidor no es válida. Inténtalo de nuevo.' });
  paseGoogle.limpiar();
  aplicarIngreso(respuesta);
}

export const MENSAJE_SALIDA_FALLIDA = 'No pudimos cerrar tu sesión en el servidor. Vuelve a intentarlo.';

/**
 * Cierra la sesión en la API (con CSRF) y en este navegador. Espera la renovación en curso (que rota la
 * cookie) para no cerrar con una cookie ya reemplazada. Si la API no responde, la sesión local igual se
 * borra, pero se lanza un ErrorApi para que la pantalla avise que la del servidor puede seguir abierta.
 */
async function salir() {
  if (renovacionEnCurso) await renovacionEnCurso;
  let fallo = null;
  try {
    await authService.salir(conTiempoMaximo());
  } catch (error) {
    fallo = error;
  }
  cerrarLocal('salida');
  difundir('salida');
  if (fallo) throw new ErrorApi({ estado: fallo?.estado ?? 0, codigo: 'salida_fallida', mensaje: MENSAJE_SALIDA_FALLIDA });
}

/** Cambia datos del usuario en pantalla (por ejemplo, tras editar Mi cuenta o confirmar el correo). */
function actualizarUsuario(cambios) {
  if (!estado.usuario) return;
  publicar({ usuario: { ...estado.usuario, ...cambios } });
}

/** Reemplaza el token (al cambiar la contraseña, la API cierra las demás sesiones y entrega uno nuevo). */
function actualizarToken(nuevo) {
  if (!estado.usuario || !nuevo) return;
  token = nuevo;
  const duracion = duracionDe(token);
  venceLocal = duracion === null ? null : ahoraMonotono() + duracion;
  programarRenovacion();
}

/** Sesión actual y acciones. Se actualiza sola cuando la sesión cambia, se renueva o vence. */
export function useSesion() {
  const { usuario, comprobando, falloComprobacion, motivoCierre } = useSyncExternalStore(almacenSesion.suscribir, almacenSesion.obtener);
  return {
    usuario,
    motivoCierre,
    comprobando,
    falloComprobacion,
    estaAutenticado: Boolean(usuario),
    esPersonal: usuario?.tipo === 'empleado',
    esCliente: usuario?.tipo === 'cliente',
    tieneRol(roles) {
      if (!usuario) return false;
      const lista = Array.isArray(roles) ? roles : [roles];
      return lista.some((rol) => normalizar(rol) === normalizar(usuario.rol));
    },
    ingresarPersonal,
    ingresarPersonalConRostro,
    ingresarCliente,
    registrarCliente,
    ingresarConGoogle,
    completarRegistroGoogle,
    salir,
    actualizarUsuario,
    actualizarToken,
  };
}
