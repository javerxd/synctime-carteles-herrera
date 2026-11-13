import { Inbox } from 'lucide-react';
import { unirClases } from './clases';
import estilos from './EstadoVacio.module.css';

/**
 * Estado vacío que enseña: dice qué falta, por qué puede faltar y cuál es el siguiente paso.
 * `nivelTitulo` mantiene el orden de los encabezados de la página.
 */
export function EstadoVacio({ titulo, descripcion, accion, icono: Icono = Inbox, nivelTitulo = 2, className, ...resto }) {
  const Titulo = `h${Math.min(Math.max(nivelTitulo, 2), 6)}`;
  return (
    <div {...resto} className={unirClases(estilos.vacio, className)}>
      <span className={estilos.marcoIcono} aria-hidden="true">
        <Icono size={24} strokeWidth={1.5} />
      </span>
      <Titulo className={estilos.titulo}>{titulo}</Titulo>
      {descripcion && <p className={estilos.descripcion}>{descripcion}</p>}
      {accion && <div className={estilos.accion}>{accion}</div>}
    </div>
  );
}
