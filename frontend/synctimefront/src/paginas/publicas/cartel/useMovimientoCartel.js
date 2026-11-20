import { useCallback, useState } from 'react';
import { useMovimientoReducido } from '../../../rostro/useMovimientoReducido';

const CLAVE = 'synctime.cartel.movimiento';

function leerPreferencia() {
  try {
    const valor = window.localStorage.getItem(CLAVE);
    return valor === 'si' || valor === 'no' ? valor : null;
  } catch {
    return null;
  }
}

function guardarPreferencia(valor) {
  try {
    window.localStorage.setItem(CLAVE, valor);
  } catch {
    // Sin almacenamiento (ventana privada o bloqueado): la elección vale solo mientras la página está abierta.
  }
}

/**
 * Si el cartel grande se balancea. Por defecto sigue al sistema: se mueve, salvo que la persona haya pedido
 * menos movimiento (prefers-reduced-motion, p. ej. «Mostrar animaciones» apagado en Windows). Un botón junto
 * al cartel permite pausarlo o, aun con esa preferencia, activarlo; la elección se recuerda en el navegador.
 * Como el balanceo no se detiene solo, el botón es también el control de pausa que pide WCAG 2.2.2.
 */
export function useMovimientoCartel() {
  const reducido = useMovimientoReducido();
  const [elegido, setElegido] = useState(leerPreferencia);
  const mover = elegido ? elegido === 'si' : !reducido;

  const alternar = useCallback(() => {
    const siguiente = mover ? 'no' : 'si';
    setElegido(siguiente);
    guardarPreferencia(siguiente);
  }, [mover]);

  return { mover, alternar, reducido };
}
