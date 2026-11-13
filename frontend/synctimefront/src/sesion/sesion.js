import { useSyncExternalStore } from 'react';
import authService from '../api/authService';
import { configurarClienteApi } from '../api/cliente';

// Sesión del usuario: personal (empleado) o cliente. Vive fuera de React para que el cliente HTTP
// lea el token sin depender del orden de los componentes. Se guarda en sessionStorage (se borra al
// cerrar la pestaña); la fase 6 la reemplaza por un token corto con renovación en cookie httpOnly.
const CLAVE = 'synctime_sesion';

function vencimientoDe(token) {
  try {
    const carga = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const { exp } = JSON.parse(atob(carga));
    return typeof exp === 'number' ? exp * 1000 : null;
  } catch {
    return null;
  }
}

function vigente(sesion) {
  if (!sesion?.token || !sesion?.usuario) return false;
  const vence = vencimientoDe(sesion.token);
  return vence === null || vence > Date.now();
}

function leerGuardada() {
  try {
    const sesion = JSON.parse(sessionStorage.getItem(CLAVE));
    return vigente(sesion) ? sesion : null;
  } catch {
    return null;
  }
}

let estado = { sesion: leerGuardada(), motivoCierre: null };
const oyentes = new Set();
let temporizador = null;

function publicar(nuevo) {
  estado = nuevo;
  oyentes.forEach((oyente) => oyente());
}

function programarVencimiento(sesion) {
  clearTimeout(temporizador);
  const vence = sesion ? vencimientoDe(sesion.token) : null;
  if (vence === null) return;
  // setTimeout no acepta más de ~24,8 días; las sesiones duran horas.
  temporizador = setTimeout(() => almacenSesion.cerrar('vencida'), Math.min(vence - Date.now(), 2 ** 31 - 1));
}

export const almacenSesion = {
  obtener: () => estado,
  suscribir(oyente) {
    oyentes.add(oyente);
    return () => oyentes.delete(oyente);
  },
  guardar(sesion) {
    sessionStorage.setItem(CLAVE, JSON.stringify(sesion));
    programarVencimiento(sesion);
    publicar({ sesion, motivoCierre: null });
  },
  /** motivo: 'salida' (el usuario salió) o 'vencida' (la API la rechazó o expiró). */
  cerrar(motivo = null) {
    sessionStorage.removeItem(CLAVE);
    clearTimeout(temporizador);
    publicar({ sesion: null, motivoCierre: motivo });
  },
  /** Vuelve a leer sessionStorage (al iniciar y en pruebas). */
  recargar() {
    const sesion = leerGuardada();
    programarVencimiento(sesion);
    publicar({ sesion, motivoCierre: null });
  },
};

programarVencimiento(estado.sesion);

configurarClienteApi({
  obtenerToken: () => estado.sesion?.token ?? null,
  alSesionVencida: () => almacenSesion.cerrar('vencida'),
});

function normalizar(texto) {
  return String(texto ?? '').normalize('NFC').toLowerCase();
}

function sesionDePersonal({ token, usuario }) {
  return { token, usuario: { tipo: 'empleado', id: usuario.idEmpleado, ...usuario } };
}

async function ingresarPersonal(email, password) {
  almacenSesion.guardar(sesionDePersonal(await authService.login(email, password)));
}

async function ingresarPersonalConRostro(email, descriptor) {
  almacenSesion.guardar(sesionDePersonal(await authService.loginFacial(email, descriptor)));
}

async function ingresarCliente(telefono, password) {
  const { token, cliente } = await authService.loginCliente(telefono, password);
  almacenSesion.guardar({ token, usuario: { tipo: 'cliente', rol: 'cliente', id: cliente.idCliente, ...cliente } });
}

function salir() {
  almacenSesion.cerrar('salida');
}

/** Sesión actual y acciones. Se actualiza sola cuando la sesión cambia o vence. */
export function useSesion() {
  const { sesion, motivoCierre } = useSyncExternalStore(almacenSesion.suscribir, almacenSesion.obtener);
  const usuario = sesion?.usuario ?? null;
  return {
    usuario,
    motivoCierre,
    estaAutenticado: Boolean(usuario),
    esPersonal: usuario?.tipo === 'empleado',
    tieneRol(roles) {
      if (!usuario) return false;
      const lista = Array.isArray(roles) ? roles : [roles];
      return lista.some((rol) => normalizar(rol) === normalizar(usuario.rol));
    },
    ingresarPersonal,
    ingresarPersonalConRostro,
    ingresarCliente,
    salir,
  };
}
