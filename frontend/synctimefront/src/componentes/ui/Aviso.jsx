import { CircleAlert, CircleCheck, Info, TriangleAlert } from 'lucide-react';
import { unirClases } from './clases';
import estilos from './Aviso.module.css';

const ICONOS = { info: Info, exito: CircleCheck, aviso: TriangleAlert, peligro: CircleAlert };

/**
 * Mensaje en línea. Un error («peligro») usa role="alert": se anuncia al insertarse e interrumpe al
 * lector de pantalla. Los demás usan role="status", que espera su turno; pero una región que nace ya
 * con su texto no siempre se anuncia (NVDA). Para confirmar una acción en pantalla (por ejemplo,
 * «Solicitud enviada»), anunciarla además en la región de avisos de la página (fase 3).
 */
export function Aviso({ tono = 'info', titulo, accion, className, children, ...resto }) {
  const Icono = ICONOS[tono] ?? Info;
  return (
    <div
      {...resto}
      role={tono === 'peligro' ? 'alert' : 'status'}
      className={unirClases(estilos.mensaje, estilos[tono], className)}
    >
      <Icono size={20} strokeWidth={1.75} aria-hidden="true" className={estilos.icono} />
      <div className={estilos.cuerpo}>
        {titulo && <p className={estilos.titulo}>{titulo}</p>}
        {children && <div className={estilos.texto}>{children}</div>}
      </div>
      {accion && <div className={estilos.accion}>{accion}</div>}
    </div>
  );
}
