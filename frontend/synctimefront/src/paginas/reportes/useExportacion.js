import { useCallback, useEffect, useReducer, useRef } from 'react';
import { ErrorApi } from '../../api/cliente';
import { reportes } from '../../api/reportes';
import { EXPORTACION_INICIAL, estaOcupada, parametrosReporte, reducirExportacion } from './reglasReportes';

// Fase 13B (RNFP18, RFU26, E20): pide el archivo, consulta su estado sin bloquear la pantalla y lo baja cuando está listo. Si la
// persona cambia de página, la consulta se corta (el trabajo sigue en el servidor y solo se pierde el aviso).
//   - Cada llamada tiene un tiempo máximo: una petición colgada no deja los botones inactivos para siempre.
//   - Un error pasajero al consultar el estado (red, 5xx) no pierde el trabajo: se sigue consultando y, si hay que abandonar,
//     «Reintentar» retoma ESE trabajo en vez de pedir otro (el servidor respondería 409: ya hay uno en preparación).
//   - «Reintentar» usa el reporte, el formato y los filtros del pedido original, no los que haya ahora en pantalla.

const PRIMERA_ESPERA_MS = 1000;
const ESPERA_MAXIMA_MS = 5000;
// Con la espera creciente y su tope, 60 consultas son unos cuatro minutos: más que el límite de 120 s de cada trabajo.
const CONSULTAS_MAXIMAS = 60;
const FALLOS_SEGUIDOS_MAXIMOS = 3;
const TIEMPO_MAXIMO_LLAMADA_MS = 30_000;
const MENSAJE_LENTO = 'El archivo tarda más de lo normal en prepararse. Inténtalo de nuevo en un momento.';
const MENSAJE_SIN_RESPUESTA = 'El servidor no responde. Revisa tu conexión e inténtalo de nuevo.';

/** Una espera que termina antes si la señal se cancela (al salir de la pantalla). */
function esperarMs(ms, senal) {
  return new Promise((resolver) => {
    if (senal?.aborted) {
      resolver();
      return;
    }
    const alCancelar = () => {
      clearTimeout(temporizador);
      resolver();
    };
    const temporizador = setTimeout(() => {
      senal?.removeEventListener('abort', alCancelar);
      resolver();
    }, ms);
    senal?.addEventListener('abort', alCancelar, { once: true });
  });
}

/** Entrega el archivo al navegador como una descarga (enlace temporal con el nombre del servidor). */
export function guardarBlob(blob, nombre) {
  const direccion = URL.createObjectURL(blob);
  const enlace = document.createElement('a');
  enlace.href = direccion;
  enlace.download = nombre || 'reporte';
  enlace.rel = 'noopener';
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();
  setTimeout(() => URL.revokeObjectURL(direccion), 10_000);
}

/** La llamada, con un tiempo máximo: si pasa, falla con un error reintentable (la petición que quedó colgada se ignora). */
function conTiempo(llamada, ms = TIEMPO_MAXIMO_LLAMADA_MS) {
  let temporizador;
  const limite = new Promise((_, rechazar) => {
    temporizador = setTimeout(() => rechazar(new ErrorApi({ estado: 0, codigo: 'tiempo_agotado', mensaje: MENSAJE_SIN_RESPUESTA })), ms);
  });
  return Promise.race([llamada(), limite]).finally(() => clearTimeout(temporizador));
}

// Un pedido mal formado o sin permiso no se arregla reintentando; lo demás (red, servidor, 409, 410, 429) sí.
const sePuedeReintentar = (error) => error?.estado !== 400 && error?.estado !== 403;
// Al consultar el estado, además, un 404 (el trabajo no existe) no mejora esperando.
const esPasajero = (error) => error?.estado === 0 || error?.estado >= 500 || error?.estado === 429;
const mensajeDe = (error) => error?.message || 'No se pudo preparar el archivo. Inténtalo de nuevo.';

/**
 * { estado, pedir(formato), reintentar(), descargarDeNuevo(), cerrar() }. `dependencias` (api, guardar, esperar) existe para las
 * pruebas; la pantalla usa las reales.
 */
export function useExportacion({ reporte, filtros }, dependencias = {}) {
  const { api = reportes, guardar = guardarBlob, esperar = esperarMs } = dependencias;
  const [estado, despachar] = useReducer(reducirExportacion, EXPORTACION_INICIAL);
  const enCurso = useRef(false);
  const bajando = useRef(false);
  const activo = useRef(true);
  const control = useRef(null);
  // Lo que se pidió: el reporte, los filtros y el formato (para «Reintentar»), y el trabajo si ya existe.
  const pedido = useRef(null);
  const archivo = useRef(null);

  useEffect(() => {
    activo.current = true;
    return () => {
      activo.current = false;
      control.current?.abort();
    };
  }, []);

  const bajar = useCallback(async (id, nombre) => {
    const blob = await conTiempo(() => api.descargar(id));
    if (!activo.current) return false;
    guardar(blob, nombre);
    return true;
  }, [api, guardar]);

  // Consulta el estado de un trabajo hasta que termine. Devuelve normalmente; deja el resultado en el estado de la pantalla.
  const sondear = useCallback(async (id) => {
    const { signal } = control.current;
    let espera = PRIMERA_ESPERA_MS;
    let fallosSeguidos = 0;
    for (let consulta = 0; consulta < CONSULTAS_MAXIMAS; consulta += 1) {
      let actual;
      try {
        actual = await conTiempo(() => api.estado(id, { senal: signal }));
        fallosSeguidos = 0;
      } catch (error) {
        if (!activo.current || error?.name === 'AbortError') return;
        fallosSeguidos += 1;
        if (!esPasajero(error) && error?.estado !== undefined && error.estado !== 0) throw error;
        if (fallosSeguidos >= FALLOS_SEGUIDOS_MAXIMOS) {
          despachar({ tipo: 'fallida', mensaje: mensajeDe(error), reintentable: true });
          return;
        }
        actual = { estado: 'procesando' };
      }
      if (!activo.current) return;
      if (actual.estado === 'listo') {
        if (!(await bajar(id, actual.nombreArchivo))) return;
        archivo.current = { id, nombre: actual.nombreArchivo };
        pedido.current = { ...pedido.current, id: null };
        despachar({ tipo: 'lista', nombreArchivo: actual.nombreArchivo || 'reporte' });
        return;
      }
      if (actual.estado === 'fallido') {
        pedido.current = { ...pedido.current, id: null };
        despachar({ tipo: 'fallida', mensaje: actual.error || 'No se pudo preparar el archivo. Inténtalo de nuevo.', reintentable: true });
        return;
      }
      await esperar(espera, signal);
      if (!activo.current) return;
      espera = Math.min(Math.round(espera * 1.5), ESPERA_MAXIMA_MS);
    }
    despachar({ tipo: 'fallida', mensaje: MENSAJE_LENTO, reintentable: true });
  }, [api, bajar, esperar]);

  const ejecutar = useCallback(async (datos) => {
    if (enCurso.current) return;
    enCurso.current = true;
    pedido.current = datos;
    control.current = new AbortController();
    despachar({ tipo: 'pedir', formato: datos.formato, reporte: datos.reporte });
    try {
      let { id } = datos;
      if (!id) {
        // El trabajo todavía no existe: se pide ahora.
        const trabajo = await conTiempo(() => api.pedirExportacion(datos.reporte, datos.formato, datos.filtros));
        if (!activo.current) return;
        id = trabajo.id;
        pedido.current = { ...datos, id };
      }
      despachar({ tipo: 'aceptada', id });
      await sondear(id);
    } catch (error) {
      if (!activo.current || error?.name === 'AbortError') return;
      despachar({ tipo: 'fallida', mensaje: mensajeDe(error), reintentable: sePuedeReintentar(error) });
    } finally {
      enCurso.current = false;
    }
  }, [api, sondear]);

  const pedir = useCallback((formato) => ejecutar({ reporte, formato, filtros: parametrosReporte(reporte, filtros), id: null }), [ejecutar, filtros, reporte]);

  // «Reintentar»: lo mismo que se pidió (aunque la pantalla ya muestre otro reporte u otros filtros); si el trabajo ya existía en
  // el servidor, se retoma su consulta en vez de pedir uno nuevo.
  const reintentar = useCallback(() => (pedido.current ? ejecutar(pedido.current) : undefined), [ejecutar]);

  const descargarDeNuevo = useCallback(async () => {
    if (!archivo.current || bajando.current) return;
    bajando.current = true;
    try {
      await bajar(archivo.current.id, archivo.current.nombre);
    } catch (error) {
      if (activo.current) despachar({ tipo: 'fallida', mensaje: mensajeDe(error), reintentable: sePuedeReintentar(error) });
    } finally {
      bajando.current = false;
    }
  }, [bajar]);

  const cerrar = useCallback(() => despachar({ tipo: 'cerrar' }), []);

  return { estado, ocupada: estaOcupada(estado), pedir, reintentar, descargarDeNuevo, cerrar };
}
