import { useEffect, useState } from 'react';

/** Devuelve `valor` cuando deja de cambiar durante `espera` ms (búsquedas mientras se escribe). */
export function useDebounce(valor, espera = 300) {
  const [estable, setEstable] = useState(valor);
  useEffect(() => {
    const temporizador = setTimeout(() => setEstable(valor), espera);
    return () => clearTimeout(temporizador);
  }, [valor, espera]);
  return estable;
}
