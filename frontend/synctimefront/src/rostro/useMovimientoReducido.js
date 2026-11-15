import { useEffect, useState } from 'react';

const CONSULTA = '(prefers-reduced-motion: reduce)';

function consultar() {
  try {
    return window.matchMedia?.(CONSULTA)?.matches ?? false;
  } catch {
    return false;
  }
}

/** true si la persona pidió menos movimiento en su sistema; sigue los cambios mientras la pantalla está abierta. */
export function useMovimientoReducido() {
  const [reducido, setReducido] = useState(consultar);

  useEffect(() => {
    const lista = window.matchMedia?.(CONSULTA);
    if (!lista?.addEventListener) return undefined;
    const alCambiar = () => setReducido(lista.matches);
    lista.addEventListener('change', alCambiar);
    return () => lista.removeEventListener('change', alCambiar);
  }, []);

  return reducido;
}
