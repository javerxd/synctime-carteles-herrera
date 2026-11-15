import { useEffect, useRef, useState } from 'react';
import { avisosTaller } from '../../api/avisosTaller';
import { debeSeguirConsultando, filasPrueba, INTERVALO_PRUEBA_MS } from './reglasAvisosTaller';

const MENSAJE_LIMITE = 'Ya enviaste 3 pruebas en 10 minutos. Espera unos minutos y vuelve a intentarlo.';

/**
 * «Enviar correo de prueba»: la API lo encola y responde de inmediato (202); aquí se consulta el estado cada
 * 2 segundos hasta que todos salen o fallan, o hasta 60 segundos. Una consulta que falla no corta la espera.
 * Al desmontar se detiene todo (temporizador y petición en curso).
 * fase: 'inactiva' | 'enviando' | 'consultando' | 'terminada' | 'error'
 */
export function usePruebaAvisos(alEncolar) {
  const [estado, setEstado] = useState({ fase: 'inactiva', envio: null, filas: [], error: null });
  const refControl = useRef(null);

  useEffect(() => () => refControl.current?.abort(), []);

  async function enviar() {
    refControl.current?.abort();
    const control = new AbortController();
    refControl.current = control;
    setEstado({ fase: 'enviando', envio: null, filas: [], error: null });
    let envio;
    try {
      envio = await avisosTaller.probar();
    } catch (error) {
      if (control.signal.aborted) return;
      const mensaje = error?.estado === 429 ? (error.codigo === 'http_429' ? MENSAJE_LIMITE : error.message || MENSAJE_LIMITE) : error?.message;
      setEstado({ fase: 'error', envio: null, filas: [], error: { ...error, message: mensaje, estado: error?.estado } });
      return;
    }
    if (control.signal.aborted) return;
    alEncolar?.();
    const ids = envio?.idsCorreos ?? [];
    setEstado({ fase: 'consultando', envio, filas: filasPrueba(envio), error: null });
    if (!ids.length) {
      setEstado({ fase: 'terminada', envio, filas: [], error: null });
      return;
    }

    let consultas = 0;
    let ultimos = [];
    const esperar = () => new Promise((resolver) => {
      const temporizador = setTimeout(resolver, INTERVALO_PRUEBA_MS);
      control.signal.addEventListener('abort', () => clearTimeout(temporizador), { once: true });
    });
    while (debeSeguirConsultando(ultimos, consultas)) {
      await esperar();
      if (control.signal.aborted) return;
      consultas += 1;
      try {
        const respuesta = await avisosTaller.estadoPrueba(ids, { senal: control.signal });
        ultimos = Array.isArray(respuesta) ? respuesta : [];
        if (control.signal.aborted) return;
        setEstado((anterior) => ({ ...anterior, filas: filasPrueba(envio, ultimos) }));
      } catch {
        if (control.signal.aborted) return;
        // Se reintenta en la siguiente vuelta; al tope, lo que siga sin respuesta queda «en cola».
      }
    }
    setEstado((anterior) => ({ ...anterior, fase: 'terminada' }));
  }

  return { ...estado, enviar, ocupado: estado.fase === 'enviando' || estado.fase === 'consultando' };
}
