import { Check, X } from 'lucide-react';
import { unirClases } from './clases';
import estilos from './Chip.module.css';

/**
 * Filtro rápido de activar y desactivar (por ejemplo, «Por cobrar»). Es un botón con aria-pressed;
 * al activarse muestra además una marca, para que el estado no dependa solo del color.
 * Con `quitable` es el chip de un filtro ya aplicado (por ejemplo, «Cliente: Rosa Pino»): un botón que lo quita,
 * sin aria-pressed y con una ×; quien lo usa le da un nombre que diga qué quita.
 */
export function Chip({ seleccionado = false, quitable = false, alCambiar, contador, onClick, className, children, ...resto }) {
  return (
    <button
      {...resto}
      type="button"
      aria-pressed={quitable ? undefined : seleccionado ? 'true' : 'false'}
      onClick={(evento) => {
        onClick?.(evento);
        alCambiar?.(!seleccionado);
      }}
      className={unirClases(estilos.chip, quitable && estilos.quitable, className)}
    >
      {seleccionado && <Check size={16} strokeWidth={2.25} aria-hidden="true" className={estilos.marca} />}
      <span>{children}</span>
      {contador !== undefined && <span className={estilos.contador}>{contador}</span>}
      {quitable && <X size={16} strokeWidth={2} aria-hidden="true" className={estilos.marca} />}
    </button>
  );
}
