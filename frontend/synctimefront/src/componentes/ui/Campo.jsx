import { useId } from 'react';
import { ChevronDown, CircleAlert } from 'lucide-react';
import { unirClases } from './clases';
import estilos from './Campo.module.css';

/**
 * Campo de formulario con etiqueta vinculada, ayuda y error. `como` elige el control: `input`
 * (por defecto), `select` (las opciones van como hijos) o `textarea`.
 * El error se asocia al control (aria-invalid y aria-describedby) y vive en una región que el
 * lector de pantalla anuncia cuando aparece o cambia.
 */
export function Campo({
  etiqueta,
  ayuda,
  error,
  requerido = false,
  como: Control = 'input',
  id,
  className,
  children,
  'aria-describedby': describeExterno,
  ...resto
}) {
  const generado = useId();
  const idCampo = id ?? `campo${generado}`;
  const idAyuda = `${idCampo}-ayuda`;
  const idError = `${idCampo}-error`;
  const describe = [ayuda && idAyuda, error && idError, describeExterno].filter(Boolean).join(' ') || undefined;
  const esLista = Control === 'select';

  return (
    <div className={unirClases(estilos.campo, className)}>
      <label htmlFor={idCampo} className={estilos.etiqueta}>
        {etiqueta}
        {requerido && (
          <span className={estilos.obligatorio} aria-hidden="true">
            Obligatorio
          </span>
        )}
      </label>
      {ayuda && (
        <p id={idAyuda} className={estilos.ayuda}>
          {ayuda}
        </p>
      )}
      <div className={unirClases(estilos.marco, esLista && estilos.marcoLista)}>
        <Control
          {...resto}
          id={idCampo}
          required={requerido || resto.required || undefined}
          aria-invalid={error ? 'true' : undefined}
          aria-describedby={describe}
          className={unirClases(estilos.control, Control === 'textarea' && estilos.area)}
        >
          {children}
        </Control>
        {esLista && <ChevronDown size={18} strokeWidth={1.75} aria-hidden="true" className={estilos.flecha} />}
      </div>
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
