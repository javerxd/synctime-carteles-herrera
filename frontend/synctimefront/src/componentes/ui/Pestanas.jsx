import { useId, useRef, useState } from 'react';
import { unirClases } from './clases';
import estilos from './Pestanas.module.css';

/**
 * Pestañas según el patrón de WAI-ARIA, con activación automática: las flechas mueven foco y
 * selección; Inicio y Fin van a la primera y la última. Solo la pestaña activa entra en el orden
 * de tabulación. Se usa sin control (`porDefecto`) o controlada (`activa` y `alCambiar`).
 * Todos los paneles quedan montados (los inactivos, ocultos), así conservan su estado.
 */
export function Pestanas({ etiqueta, pestanas, activa, porDefecto, alCambiar, className }) {
  const [interna, setInterna] = useState(porDefecto ?? pestanas[0]?.id);
  const controlada = activa !== undefined;
  const pedida = controlada ? activa : interna;
  // Si la pestaña pedida no existe, la primera queda activa para que el teclado la alcance.
  const actual = pestanas.some((p) => p.id === pedida) ? pedida : pestanas[0]?.id;
  const base = useId();
  const refs = useRef(new Map());

  function seleccionar(id, enfocar = false) {
    if (!controlada) setInterna(id);
    alCambiar?.(id);
    if (enfocar) refs.current.get(id)?.focus();
  }

  function alTeclear(evento, indice) {
    const total = pestanas.length;
    if (total === 0) return;
    const destinos = { ArrowRight: (indice + 1) % total, ArrowLeft: (indice - 1 + total) % total, Home: 0, End: total - 1 };
    const destino = destinos[evento.key];
    if (destino === undefined) return;
    evento.preventDefault();
    seleccionar(pestanas[destino].id, true);
  }

  const idPestana = (id) => `${base}-pestana-${id}`;
  const idPanel = (id) => `${base}-panel-${id}`;

  return (
    <div className={unirClases(estilos.pestanas, className)}>
      <div role="tablist" aria-label={etiqueta} className={estilos.lista}>
        {pestanas.map((pestana, indice) => {
          const seleccionada = pestana.id === actual;
          return (
            <button
              key={pestana.id}
              ref={(nodo) => {
                if (nodo) refs.current.set(pestana.id, nodo);
                else refs.current.delete(pestana.id);
              }}
              type="button"
              role="tab"
              id={idPestana(pestana.id)}
              aria-selected={seleccionada}
              aria-controls={idPanel(pestana.id)}
              tabIndex={seleccionada ? 0 : -1}
              onClick={() => seleccionar(pestana.id)}
              onKeyDown={(evento) => alTeclear(evento, indice)}
              className={estilos.pestana}
            >
              {pestana.etiqueta}
            </button>
          );
        })}
      </div>
      {pestanas.map((pestana) => (
        <div
          key={pestana.id}
          role="tabpanel"
          id={idPanel(pestana.id)}
          aria-labelledby={idPestana(pestana.id)}
          hidden={pestana.id !== actual}
          tabIndex={0}
          className={estilos.panel}
        >
          {pestana.contenido}
        </div>
      ))}
    </div>
  );
}
