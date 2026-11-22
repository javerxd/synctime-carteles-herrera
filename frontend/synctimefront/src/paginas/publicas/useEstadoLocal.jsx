import { createContext, useContext, useEffect } from 'react';
import { publico } from '../../api/publico';
import { useConsulta } from '../../utilidades/useConsulta';

// El estado «abierto ahora» cambia con la hora: una pestaña abierta todo el día se actualiza sola.
const REFRESCO_ESTADO_MS = 60_000;

const ContextoEstado = createContext(null);

/**
 * Una sola consulta del estado del local para todo el portal: la barra (cartel pequeño) y la portada
 * (cartel grande) la comparten, así no se pide dos veces ni se desincronizan. Se actualiza cada minuto y
 * al volver a la pestaña.
 */
export function ProveedorEstadoLocal({ children }) {
  const estado = useConsulta('estado-local', publico.estadoLocal);
  const { recargar } = estado;

  useEffect(() => {
    const refrescar = () => {
      if (!document.hidden) recargar();
    };
    const cada = window.setInterval(refrescar, REFRESCO_ESTADO_MS);
    document.addEventListener('visibilitychange', refrescar);
    return () => {
      window.clearInterval(cada);
      document.removeEventListener('visibilitychange', refrescar);
    };
  }, [recargar]);

  return <ContextoEstado.Provider value={estado}>{children}</ContextoEstado.Provider>;
}

/** { datos, cargando, error, recargar } del estado del local. Solo dentro de la estructura pública. */
export function useEstadoLocal() {
  const estado = useContext(ContextoEstado);
  if (!estado) throw new Error('useEstadoLocal se usa dentro de ProveedorEstadoLocal (EstructuraPublica).');
  return estado;
}
