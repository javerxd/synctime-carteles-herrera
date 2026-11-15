import { Chip } from '../../componentes/ui';
import { clientes } from '../../api/solicitudes';
import { useConsulta } from '../../utilidades/useConsulta';
import { nombreCompleto } from '../solicitudes/reglasSolicitud';

/**
 * El filtro por un cliente (`?idCliente=`, por ejemplo desde su ficha): un chip con su nombre y una × que, al
 * pulsarlo, quita el filtro (un botón, no un interruptor: sin aria-pressed). Si el nombre no carga, dice «Cliente N.º 9».
 */
export function FiltroCliente({ idCliente, alQuitar }) {
  const consulta = useConsulta(`cliente:${idCliente || 'ninguno'}`, () => (idCliente ? clientes.obtener(idCliente) : null));
  if (!idCliente) return null;
  const nombre = consulta.datos ? nombreCompleto(consulta.datos) : `Cliente N.º ${idCliente}`;
  return (
    <Chip quitable alCambiar={alQuitar} aria-label={`Quitar el filtro por cliente: ${nombre}`}>
      Cliente: {nombre}
    </Chip>
  );
}
