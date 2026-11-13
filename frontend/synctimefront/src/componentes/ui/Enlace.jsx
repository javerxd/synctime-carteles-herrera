import { ExternalLink } from 'lucide-react';
import { unirClases } from './clases';
import estilos from './Enlace.module.css';

/**
 * Enlace de texto. Con `externo` abre otra pestaña: lo avisa con un icono y con texto para el lector
 * de pantalla, y corta el acceso de la página nueva a esta (`noopener noreferrer`).
 * `como` permite usar el Link del enrutador.
 */
export function Enlace({ como: Elemento = 'a', externo = false, className, children, ...resto }) {
  const propsExternas = externo ? { target: '_blank', rel: 'noopener noreferrer' } : {};
  return (
    <Elemento {...resto} {...propsExternas} className={unirClases(estilos.enlace, className)}>
      {children}
      {externo && (
        <>
          <ExternalLink size={14} strokeWidth={1.75} aria-hidden="true" className={estilos.icono} />
          <span className="solo-lectores"> (se abre en otra pestaña)</span>
        </>
      )}
    </Elemento>
  );
}
