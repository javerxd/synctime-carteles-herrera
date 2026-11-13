import { unirClases } from './clases';
import estilos from './Tarjeta.module.css';

/**
 * Superficie con borde sutil. `interactiva` la eleva con una sombra difusa al pasar el cursor
 * (tarjetas de la vitrina, cuyo enlace principal cubre toda la tarjeta). `como` elige el elemento:
 * `article` para un trabajo de la vitrina, `section` para un bloque con título.
 */
export function Tarjeta({ como: Elemento = 'div', interactiva = false, sinRelleno = false, className, children, ...resto }) {
  return (
    <Elemento
      {...resto}
      className={unirClases(
        estilos.tarjeta,
        interactiva && estilos.interactiva,
        sinRelleno && estilos.sinRelleno,
        className,
      )}
    >
      {children}
    </Elemento>
  );
}
