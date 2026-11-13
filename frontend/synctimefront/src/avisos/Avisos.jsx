import { createContext, useCallback, useContext, useMemo, useRef, useState } from 'react';
import { X } from 'lucide-react';
import { Aviso } from '../componentes/ui';
import estilos from './Avisos.module.css';

const ContextoAvisos = createContext(null);
const DURACION_MS = 6000;

/**
 * Avisos de la aplicación («Cartel actualizado», «Tu sesión venció»). El texto se anuncia en una
 * región viva que existe desde el inicio (así el lector de pantalla la oye cambiar) y se muestra en
 * una pila visual que se cierra sola o con su botón. Los errores usan la región assertive.
 */
export function ProveedorAvisos({ children }) {
  const [avisos, setAvisos] = useState([]);
  const [anuncio, setAnuncio] = useState({ amable: '', urgente: '' });
  const siguienteId = useRef(1);

  const cerrar = useCallback((id) => setAvisos((lista) => lista.filter((a) => a.id !== id)), []);

  const anunciar = useCallback(({ tono = 'info', titulo, texto }) => {
    const id = siguienteId.current++;
    setAvisos((lista) => [...lista.slice(-2), { id, tono, titulo, texto }]);
    const frase = [titulo, texto].filter(Boolean).join('. ');
    setAnuncio(tono === 'peligro' ? { amable: '', urgente: frase } : { amable: frase, urgente: '' });
    setTimeout(() => cerrar(id), DURACION_MS);
  }, [cerrar]);

  const valor = useMemo(() => ({ anunciar }), [anunciar]);

  return (
    <ContextoAvisos.Provider value={valor}>
      {children}
      {/* Regiones vivas sin rol: se anuncian igual y no se confunden con los avisos de cada pantalla */}
      <div className="solo-lectores" aria-live="polite" aria-atomic="true">{anuncio.amable}</div>
      <div className="solo-lectores" aria-live="assertive" aria-atomic="true">{anuncio.urgente}</div>
      {/* La pila es solo visual: su texto ya se anunció arriba */}
      <div className={estilos.pila} aria-hidden="true">
        {avisos.map((aviso) => (
          <div key={aviso.id} className={estilos.elemento}>
            <Aviso
              tono={aviso.tono}
              titulo={aviso.titulo}
              accion={(
                <button type="button" className={estilos.cerrar} onClick={() => cerrar(aviso.id)} tabIndex={-1}>
                  <X size={16} aria-hidden="true" />
                </button>
              )}
            >
              {aviso.texto}
            </Aviso>
          </div>
        ))}
      </div>
    </ContextoAvisos.Provider>
  );
}

export function useAvisos() {
  const contexto = useContext(ContextoAvisos);
  if (!contexto) throw new Error('useAvisos debe usarse dentro de ProveedorAvisos');
  return contexto;
}
