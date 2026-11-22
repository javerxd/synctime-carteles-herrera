import { Globe, Store } from 'lucide-react';
import { Insignia } from '../../componentes/ui';
import { unirClases } from '../../componentes/ui/clases';
import { ORIGENES_SOLICITUD } from './reglasSolicitud';
import estilos from './OrigenSolicitud.module.css';

const ICONOS = { online: Globe, presencial: Store };
const CLASES = { online: estilos.enLinea, presencial: estilos.presencial };

/**
 * De dónde llegó una solicitud (fase 12D): «En línea» en verde y «Presencial» en amarillo. Nunca solo color: cada
 * etiqueta lleva su texto y un icono distinto (globo y tienda), para quien no distingue los colores.
 */
export function OrigenSolicitud({ origen, className }) {
  const texto = ORIGENES_SOLICITUD[origen] ?? origen ?? '—';
  return <Insignia tono="neutro" icono={ICONOS[origen]} className={unirClases(estilos.origen, CLASES[origen], className)}>{texto}</Insignia>;
}
