import { useId } from 'react';
import { CircleAlert } from 'lucide-react';
import { unirClases } from './clases';
import estilos from './Casilla.module.css';

/**
 * Casilla de verificación. Nunca viene marcada salvo que se indique (consentimientos, Ley 21.719).
 * Toda la fila es pulsable y mide al menos 44 px de alto.
 */
export function Casilla({ etiqueta, descripcion, error, id, className, 'aria-describedby': describeExterno, ...resto }) {
  const generado = useId();
  const idCasilla = id ?? `casilla${generado}`;
  const idDescripcion = `${idCasilla}-descripcion`;
  const idError = `${idCasilla}-error`;
  const describe = [descripcion && idDescripcion, error && idError, describeExterno].filter(Boolean).join(' ') || undefined;

  return (
    <div className={unirClases(estilos.casilla, className)}>
      <div className={estilos.fila}>
        <input
          {...resto}
          type="checkbox"
          id={idCasilla}
          aria-invalid={error ? 'true' : undefined}
          aria-describedby={describe}
          className={estilos.control}
        />
        <label htmlFor={idCasilla} className={estilos.etiqueta}>
          {etiqueta}
        </label>
      </div>
      {descripcion && (
        <p id={idDescripcion} className={estilos.descripcion}>
          {descripcion}
        </p>
      )}
      <p id={idError} className={estilos.error} aria-live="polite">
        {error && (
          <>
            <CircleAlert size={16} strokeWidth={2} aria-hidden="true" className={estilos.iconoError} />
            <span>{error}</span>
          </>
        )}
      </p>
    </div>
  );
}
