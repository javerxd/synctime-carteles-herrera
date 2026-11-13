import { CircleHelp, DoorClosed, DoorOpen } from 'lucide-react';
import { unirClases } from './clases';
import { ESTADOS_PAGO, ESTADOS_PEDIDO } from './estados';
import estilos from './Insignia.module.css';

/**
 * Insignia de estado: texto, icono y color juntos, para que la información nunca dependa solo del
 * color (RNFP15). Tonos: neutro, info, listo, exito, aviso y peligro. `solido` la rellena, como el
 * cartel electrónico del local.
 */
export function Insignia({ tono = 'neutro', icono: Icono, solido = false, grande = false, className, children, ...resto }) {
  return (
    <span
      {...resto}
      className={unirClases(estilos.insignia, estilos[tono], solido && estilos.solido, grande && estilos.grande, className)}
    >
      {Icono && <Icono size={grande ? 20 : 14} strokeWidth={2} aria-hidden="true" className={estilos.icono} />}
      <span>{children}</span>
    </span>
  );
}

function InsigniaDeCatalogo({ catalogo, estado, ...resto }) {
  const conocido = catalogo[estado];
  if (!conocido) {
    // No se inventa un estado: se muestra el valor recibido, en tono neutro.
    return <Insignia tono="neutro" icono={CircleHelp} {...resto}>{String(estado ?? 'Sin estado')}</Insignia>;
  }
  return <Insignia tono={conocido.tono} icono={conocido.icono} {...resto}>{conocido.texto}</Insignia>;
}

export function EstadoPedido({ estado, ...resto }) {
  return <InsigniaDeCatalogo catalogo={ESTADOS_PEDIDO} estado={estado} {...resto} />;
}

export function EstadoPago({ estado, ...resto }) {
  return <InsigniaDeCatalogo catalogo={ESTADOS_PAGO} estado={estado} {...resto} />;
}

/** El cartel electrónico: verde «Abierto / Disponible» o rojo «Cerrado / Ausente», siempre con texto. */
export function EstadoCartel({ abierto, ...resto }) {
  return abierto ? (
    <Insignia tono="exito" icono={DoorOpen} solido {...resto}>Abierto / Disponible</Insignia>
  ) : (
    <Insignia tono="peligro" icono={DoorClosed} solido {...resto}>Cerrado / Ausente</Insignia>
  );
}
