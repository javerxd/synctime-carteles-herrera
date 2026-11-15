import { useId } from 'react';
import { CircleAlert } from 'lucide-react';
import { unirClases } from './clases';
import estilos from './Opciones.module.css';

/**
 * Grupo de opciones excluyentes (radios nativos) con su leyenda: el navegador ya da las flechas y el
 * orden de tabulación. Cada opción es una fila pulsable de al menos 44 px, con texto e icono opcional.
 * `enLinea` las pone una al lado de otra cuando caben. Ninguna viene marcada salvo que se indique.
 * `refPrimera`: ref del primer radio (por ejemplo, para el foco inicial de un diálogo).
 * `deshabilitado` (mientras se envía) usa aria-disabled: el radio no cambia, pero el foco no se pierde.
 * opciones: [{ valor, etiqueta, descripcion?, icono? }]
 */
export function Opciones({
  leyenda, nombre, opciones, valor, alCambiar, ayuda, error, requerido = false, enLinea = false, deshabilitado = false, refPrimera, className,
}) {
  const base = useId();
  const idAyuda = `${base}-ayuda`;
  const idError = `${base}-error`;
  const describe = [ayuda && idAyuda, error && idError].filter(Boolean).join(' ') || undefined;

  return (
    <fieldset className={unirClases(estilos.grupo, className)} aria-describedby={describe} data-error={error ? 'true' : undefined}
      data-deshabilitado={deshabilitado ? 'true' : undefined}>
      <legend className={estilos.leyenda}>
        {leyenda}
        {requerido && <span className={estilos.obligatorio} aria-hidden="true">Obligatorio</span>}
      </legend>
      {ayuda && <p id={idAyuda} className={estilos.ayuda}>{ayuda}</p>}
      <div className={unirClases(estilos.lista, enLinea && estilos.enLinea)}>
        {opciones.map(({ valor: opcion, etiqueta, descripcion, icono: Icono }, indice) => {
          const id = `${base}-${opcion}`;
          return (
            <label key={opcion} htmlFor={id} className={unirClases(estilos.opcion, valor === opcion && estilos.elegida)}>
              <input
                ref={indice === 0 ? refPrimera : undefined}
                id={id}
                type="radio"
                name={nombre}
                value={opcion}
                checked={valor === opcion}
                required={requerido || undefined}
                aria-invalid={error ? 'true' : undefined}
                aria-disabled={deshabilitado ? 'true' : undefined}
                onChange={() => !deshabilitado && alCambiar(opcion)}
                className={estilos.control}
              />
              {Icono && <Icono size={18} strokeWidth={1.75} aria-hidden="true" className={estilos.icono} />}
              <span className={estilos.textos}>
                <span className={estilos.etiqueta}>{etiqueta}</span>
                {descripcion && <span className={estilos.descripcion}>{descripcion}</span>}
              </span>
            </label>
          );
        })}
      </div>
      <p id={idError} className={estilos.error} aria-live="polite">
        {error && (
          <>
            <CircleAlert size={16} strokeWidth={2} aria-hidden="true" className={estilos.iconoError} />
            <span>{error}</span>
          </>
        )}
      </p>
    </fieldset>
  );
}
