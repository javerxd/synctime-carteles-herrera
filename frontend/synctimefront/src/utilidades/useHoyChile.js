import { useEffect, useState } from 'react';
import { hoyChile } from './fechas';

/**
 * «Hoy» en Chile que se mantiene al día: una pantalla abierta de noche cambia de fecha a medianoche, y también
 * al volver a la pestaña o a la ventana (el equipo pudo dormir). Revisa cada minuto; si la fecha no cambió, no
 * vuelve a dibujar.
 */
export function useHoyChile() {
  const [hoy, setHoy] = useState(() => hoyChile());
  useEffect(() => {
    const revisar = () => setHoy(hoyChile());
    const intervalo = setInterval(revisar, 60_000);
    document.addEventListener('visibilitychange', revisar);
    window.addEventListener('focus', revisar);
    return () => {
      clearInterval(intervalo);
      document.removeEventListener('visibilitychange', revisar);
      window.removeEventListener('focus', revisar);
    };
  }, []);
  return hoy;
}
