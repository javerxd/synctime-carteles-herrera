import { LoaderCircle } from 'lucide-react';
import { unirClases } from './clases';
import estilos from './Boton.module.css';

const ICONO = { size: 18, strokeWidth: 1.75, 'aria-hidden': true };

/**
 * Botón del sistema. Variantes: primario (ámbar, la acción principal de la pantalla), secundario,
 * fantasma y peligro.
 *
 * - `aria-disabled`: inactivo pero enfocable (por ejemplo, «Elegir archivos» al llegar al tope), para que el
 *   foco no se pierda; quien lo usa decide qué hace el clic.
 * - `cargando`: sigue enfocable y no repite la acción. Lo dice con texto: con `textoCargando`
 *   («Guardando…») la etiqueta cambia; sin él, el nombre accesible suma «(cargando)».
 * - `como`: lo dibuja como enlace (`"a"` con `href`). Deshabilitado o cargando, el enlace pierde su
 *   `href` y sale del orden de tabulación, para que ni Ctrl+clic ni el clic central lo abran.
 *   Con el Link del enrutador, pasar `como="a"` sin destino mientras esté inactivo.
 */
export function Boton({
  variante = 'primario',
  tamano = 'normal',
  cargando = false,
  textoCargando,
  anchoCompleto = false,
  iconoInicio: IconoInicio,
  iconoFin: IconoFin,
  como: Elemento = 'button',
  type,
  disabled = false,
  onClick,
  className,
  children,
  ...resto
}) {
  const esBoton = Elemento === 'button';
  const inactivo = cargando || (!esBoton && disabled);
  const propsInactivo = !esBoton && inactivo ? { href: undefined, tabIndex: -1 } : {};

  function manejarClic(evento) {
    if (inactivo) {
      evento.preventDefault();
      return;
    }
    onClick?.(evento);
  }

  return (
    <Elemento
      {...resto}
      {...propsInactivo}
      type={esBoton ? (type ?? 'button') : undefined}
      disabled={esBoton ? disabled : undefined}
      aria-disabled={inactivo || resto['aria-disabled'] === 'true' || resto['aria-disabled'] === true ? 'true' : undefined}
      aria-busy={cargando ? 'true' : undefined}
      onClick={manejarClic}
      className={unirClases(
        estilos.boton,
        estilos[variante],
        tamano === 'compacto' && estilos.compacto,
        anchoCompleto && estilos.anchoCompleto,
        className,
      )}
    >
      {cargando ? (
        <LoaderCircle {...ICONO} strokeWidth={2} className={estilos.giro} />
      ) : (
        IconoInicio && <IconoInicio {...ICONO} />
      )}
      <span className={estilos.texto}>
        {cargando && textoCargando ? textoCargando : children}
        {cargando && !textoCargando && (
          <>
            {' '}
            <span className="solo-lectores">(cargando)</span>
          </>
        )}
      </span>
      {IconoFin && !cargando && <IconoFin {...ICONO} />}
    </Elemento>
  );
}
