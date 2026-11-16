import { useCallback, useEffect, useRef, useState } from 'react';
import { indicadores } from '../../api/indicadores';

/** Los seis bloques del dashboard, uno por ruta de /api/v1/indicadores. */
export const BLOQUES = ['resumen', 'evolucion', 'cobranza', 'embudo', 'demanda', 'porCobrar'];

// Los filtros que NO cambian cada bloque: así un cambio solo vuelve a pedir los bloques que dependen de él (el
// alcance del saldo no recarga la evolución; la página del listado solo recarga «por cobrar»).
const IGNORA = {
  resumen: ['cobro', 'pagina'],
  evolucion: ['cobro', 'pagina', 'alcanceSaldo'],
  cobranza: ['cobro', 'pagina'],
  embudo: ['cobro', 'pagina', 'alcanceSaldo'],
  demanda: ['cobro', 'pagina', 'alcanceSaldo'],
  porCobrar: [],
};

const claveDe = (bloque, filtros) => JSON.stringify(Object.entries(filtros).filter(([c]) => !IGNORA[bloque].includes(c)));

const vacio = { datos: undefined, error: null, cargando: true };

/**
 * Carga los bloques del dashboard EN PARALELO con fallo aislado (RNFP23): los que dependen de un filtro que cambió
 * se disparan a la vez, cada uno se muestra apenas llega y Promise.allSettled espera a todos sin que un error tumbe
 * al resto. Cada bloque tiene su propia señal de cancelación: un filtro nuevo corta su petición vieja, y
 * «Reintentar» vuelve a pedir solo el bloque que falló. Mientras un bloque se recarga, conserva sus datos (actualizando).
 * `consultas` permite inyectar otras en las pruebas.
 * Devuelve { bloques: { resumen: { datos, error, cargando, actualizando }, ... }, listos, reintentar(bloque) }.
 */
export function useIndicadores(filtros, consultas = indicadores) {
  const claves = Object.fromEntries(BLOQUES.map((b) => [b, claveDe(b, filtros)]));
  const firma = JSON.stringify(claves);
  const [estado, setEstado] = useState(() => Object.fromEntries(BLOQUES.map((b) => [b, vacio])));
  const refFiltros = useRef(filtros);
  const refConsultas = useRef(consultas);
  const refClaves = useRef({});
  const controles = useRef(new Map());

  // Va primero: los efectos de abajo leen siempre los filtros y las consultas vigentes.
  useEffect(() => {
    refFiltros.current = filtros;
    refConsultas.current = consultas;
  });

  const cargar = useCallback((bloque) => {
    controles.current.get(bloque)?.abort();
    const control = new AbortController();
    controles.current.set(bloque, control);
    const vigente = () => controles.current.get(bloque) === control && !control.signal.aborted;
    setEstado((previo) => ({ ...previo, [bloque]: { ...previo[bloque], error: null, cargando: true } }));
    let promesa;
    try {
      // La consulta se llama ya, no en una microtarea: así salen todas juntas.
      promesa = Promise.resolve(refConsultas.current[bloque](refFiltros.current, { senal: control.signal }));
    } catch (error) {
      promesa = Promise.reject(error);
    }
    return promesa.then(
      (datos) => {
        if (vigente()) setEstado((previo) => ({ ...previo, [bloque]: { datos, error: null, cargando: false } }));
        return datos;
      },
      (error) => {
        if (vigente()) setEstado((previo) => ({ ...previo, [bloque]: { ...previo[bloque], error, cargando: false } }));
        throw error;
      },
    );
  }, []);

  useEffect(() => {
    const actuales = JSON.parse(firma);
    const cambiados = BLOQUES.filter((b) => refClaves.current[b] !== actuales[b]);
    refClaves.current = actuales;
    if (cambiados.length) Promise.allSettled(cambiados.map((bloque) => cargar(bloque)));
  }, [firma, cargar]);

  useEffect(() => {
    const activos = controles.current;
    return () => {
      activos.forEach((control) => control.abort());
      // Si el componente vuelve a montarse (StrictMode), pide todo de nuevo.
      refClaves.current = {};
    };
  }, []);

  const reintentar = useCallback((bloque) => {
    cargar(bloque).catch(() => {});
  }, [cargar]);

  const bloques = Object.fromEntries(BLOQUES.map((b) => {
    const { datos, error, cargando } = estado[b];
    return [b, { datos, error, cargando: cargando && datos === undefined, actualizando: cargando && datos !== undefined }];
  }));
  const listos = BLOQUES.every((b) => !estado[b].cargando);
  return { bloques, listos, reintentar };
}
