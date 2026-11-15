import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * Carga datos al montar y cuando cambia `clave`. Devuelve { datos, error, cargando, recargando, recargar }.
 * Una respuesta que llega tarde (de una clave anterior) se descarta, y la consulta recibe una señal
 * (AbortSignal) que se cancela al cambiar la clave o al desmontar: quien la pase a la API corta la petición.
 * - Sin `conservar`, una clave nueva parte sin datos (cargando = true): nunca se muestran datos de otra consulta.
 * - Con `conservar` (listados que cambian de filtro o de página), se siguen mostrando los datos anteriores
 *   mientras llega la respuesta (recargando = true), así la tabla y la paginación no saltan al esqueleto.
 */
export function useConsulta(clave, consulta, { conservar = false } = {}) {
  const [estado, setEstado] = useState({ base: null, version: -1, datos: undefined, error: null });
  const [version, setVersion] = useState(0);
  const refConsulta = useRef(consulta);

  useEffect(() => {
    refConsulta.current = consulta;
  });

  useEffect(() => {
    let vigente = true;
    const control = new AbortController();
    Promise.resolve()
      .then(() => refConsulta.current(control.signal))
      .then(
        (datos) => vigente && setEstado({ base: clave, version, datos, error: null }),
        (error) => vigente && setEstado((anterior) => ({
          base: clave, version, datos: anterior.base === clave || conservar ? anterior.datos : undefined, error,
        })),
      );
    return () => {
      vigente = false;
      control.abort();
    };
  }, [clave, version, conservar]);

  const recargar = useCallback(() => setVersion((v) => v + 1), []);
  // Mientras se recarga la misma clave (o, con `conservar`, otra), se siguen mostrando los datos anteriores.
  const listo = estado.base === clave && estado.version === version;
  const datos = estado.base === clave || conservar ? estado.datos : undefined;
  return {
    datos,
    error: listo ? estado.error : null,
    cargando: !listo && datos === undefined,
    recargando: !listo && datos !== undefined,
    recargar,
  };
}
