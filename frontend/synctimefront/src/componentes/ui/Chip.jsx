import { Check } from 'lucide-react';
import { unirClases } from './clases';
import estilos from './Chip.module.css';

/**
 * Filtro rápido de activar y desactivar (por ejemplo, «Por cobrar»). Es un botón con aria-pressed;
 * al activarse muestra además una marca, para que el estado no dependa solo del color.
 */
export function Chip({ seleccionado = false, alCambiar, contador, onClick, className, children, ...resto }) {
  return (
    <button
      {...resto}
      type="button"
      aria-pressed={seleccionado ? 'true' : 'false'}
      onClick={(evento) => {
        onClick?.(evento);
        alCambiar?.(!seleccionado);
      }}
      className={unirClases(estilos.chip, className)}
    >
      {seleccionado && <Check size={16} strokeWidth={2.25} aria-hidden="true" className={estilos.marca} />}
      <span>{children}</span>
      {contador !== undefined && <span className={estilos.contador}>{contador}</span>}
    </button>
  );
}
