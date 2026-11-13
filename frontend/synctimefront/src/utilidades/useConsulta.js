import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * Carga datos al montar y cuando cambia `clave`. Devuelve { datos, error, cargando, recargar }.
 * Una respuesta que llega tarde (de una clave anterior) se descarta. El estado se cambia solo al
 * llegar la respuesta, así una carga nunca deja la pantalla con datos de otra consulta.
 */
export function useConsulta(clave, consulta) {
  const [estado, setEstado] = useState({ clave: null, datos: undefined, error: null });
  const [version, setVersion] = useState(0);
  const refConsulta = useRef(consulta);
  const claveActual = `${clave}#${version}`;

  useEffect(() => {
    refConsulta.current = consulta;
  });

  useEffect(() => {
    let vigente = true;
    Promise.resolve()
      .then(() => refConsulta.current())
      .then(
        (datos) => vigente && setEstado({ clave: claveActual, datos, error: null }),
        (error) => vigente && setEstado({ clave: claveActual, datos: undefined, error }),
      );
    return () => {
      vigente = false;
    };
  }, [claveActual]);

  const recargar = useCallback(() => setVersion((v) => v + 1), []);
  const cargando = estado.clave !== claveActual;
  return { datos: cargando ? undefined : estado.datos, error: cargando ? null : estado.error, cargando, recargar };
}
