import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * Carga datos al montar y cuando cambia `clave`. Devuelve { datos, error, cargando, recargar }.
 * Una respuesta que llega tarde (de una clave anterior) se descarta. El estado se cambia solo al
 * llegar la respuesta, así una carga nunca deja la pantalla con datos de otra consulta.
 */
export function useConsulta(clave, consulta) {
  const [estado, setEstado] = useState({ base: null, version: -1, datos: undefined, error: null });
  const [version, setVersion] = useState(0);
  const refConsulta = useRef(consulta);

  useEffect(() => {
    refConsulta.current = consulta;
  });

  useEffect(() => {
    let vigente = true;
    Promise.resolve()
      .then(() => refConsulta.current())
      .then(
        (datos) => vigente && setEstado({ base: clave, version, datos, error: null }),
        (error) => vigente && setEstado((anterior) => ({ base: clave, version, datos: anterior.base === clave ? anterior.datos : undefined, error })),
      );
    return () => {
      vigente = false;
    };
  }, [clave, version]);

  const recargar = useCallback(() => setVersion((v) => v + 1), []);
  // Mientras se recarga la misma clave, se siguen mostrando los datos anteriores (recargando = true).
  const listo = estado.base === clave && estado.version === version;
  const datos = estado.base === clave ? estado.datos : undefined;
  return {
    datos,
    error: listo ? estado.error : null,
    cargando: !listo && datos === undefined,
    recargando: !listo && datos !== undefined,
    recargar,
  };
}
