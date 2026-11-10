import { useEffect, useRef } from 'react';

/**
 * El botón que abre la captura desaparece mientras se usa, y el foco del teclado se perdería.
 * Devuelve la referencia del botón al que debe volver el foco cuando `activo` deja de ser verdadero.
 */
export function useDevolverFoco(activo) {
  const destino = useRef(null);
  const estabaActivo = useRef(false);

  useEffect(() => {
    if (estabaActivo.current && !activo) {
      destino.current?.focus();
    }
    estabaActivo.current = activo;
  }, [activo]);

  return destino;
}
