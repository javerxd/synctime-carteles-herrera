// Pasos del seguimiento del cliente (S04, fase 8).
import { formatearFecha } from '../../utilidades/formato';

/**
 * Los pasos del seguimiento (S04): solicitud enviada → cotización → fabricación → entrega. Cada paso dice su
 * estado con texto e ícono, no solo con color.
 */
export function pasosSeguimiento(solicitud) {
  const cotizacion = solicitud.cotizacion ?? null;
  const pedido = solicitud.pedido ?? null;
  const ep = pedido?.estadoPedido;
  if (solicitud.estado === 'rechazado') {
    return [
      { id: 'solicitud', titulo: 'Solicitud enviada', estado: 'hecho' },
      { id: 'cotizacion', titulo: 'Solicitud rechazada por el taller', estado: 'detenido', detalle: solicitud.motivoRechazo || null },
    ];
  }
  const cot = (() => {
    if (cotizacion?.estado === 'aprobada' || pedido) return { estado: 'hecho', detalle: 'Aprobada' };
    if (cotizacion?.estado === 'pendiente') return { estado: 'ahora', detalle: 'Lista para que la revises' };
    if (cotizacion?.estado === 'rechazada') return { estado: 'ahora', detalle: 'La rechazaste. El taller puede enviarte otra.' };
    return { estado: 'ahora', detalle: 'El taller la está preparando' };
  })();
  const eliminado = Boolean(pedido?.eliminado || (!pedido && cotizacion?.pedidoEliminado));
  if (ep === 'anulado' || eliminado) {
    return [
      { id: 'solicitud', titulo: 'Solicitud enviada', estado: 'hecho' },
      { id: 'cotizacion', titulo: 'Cotización', ...cot },
      { id: 'fabricacion', titulo: eliminado ? 'Pedido eliminado por el taller' : 'Pedido anulado', estado: 'detenido' },
    ];
  }
  const fabricacion = !pedido ? { estado: 'despues' }
    : ['terminado', 'entregado'].includes(ep) ? { estado: 'hecho', detalle: 'Terminado' }
      : { estado: 'ahora', detalle: ep === 'en_proceso' ? 'En fabricación' : 'Pedido aceptado, por comenzar' };
  const entrega = ep === 'entregado' ? { estado: 'hecho', detalle: 'Entregado' }
    : ep === 'terminado' ? { estado: 'ahora', detalle: 'Listo para entregar' }
      : { estado: 'despues', detalle: pedido?.fechaEntregaEstimada ? `Estimada para el ${formatearFecha(pedido.fechaEntregaEstimada)}` : null };
  return [
    { id: 'solicitud', titulo: 'Solicitud enviada', estado: 'hecho' },
    { id: 'cotizacion', titulo: 'Cotización', ...cot },
    { id: 'fabricacion', titulo: 'Fabricación', ...fabricacion },
    { id: 'entrega', titulo: 'Entrega', ...entrega },
  ];
}
