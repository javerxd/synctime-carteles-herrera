// Cálculos provisionales en el navegador para Indicadores y Reportes (fases 12 y 13). El listado de
// pedidos ya filtra en la API (fase 8, src/paginas/pedidos/reglasPedido.js).

/** El cliente del pedido: el de la fila de la API (fase 8) o el de su solicitud (fase 3). */
const clienteDe = (pedido) => pedido?.cliente ?? pedido?.solicitud?.cliente ?? null;

export function nombreCliente(pedido) {
  const cliente = clienteDe(pedido);
  if (!cliente) return 'Cliente sin datos';
  if (typeof cliente === 'string') return cliente;
  return [cliente.nombre, cliente.apellido].filter(Boolean).join(' ') || 'Cliente sin datos';
}

const sinTildes = (texto) => String(texto ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
const soloDigitos = (texto) => String(texto ?? '').replace(/\D/g, '');

/** Por cobrar: no anulado y sin pagar del todo. */
export const esPorCobrar = (p) => p.estadoPedido !== 'anulado' && p.estadoPago !== 'pagado_total';

function coincideCliente(pedido, busqueda) {
  const texto = sinTildes(busqueda).trim();
  if (!texto) return true;
  if (sinTildes(nombreCliente(pedido)).includes(texto)) return true;
  const digitos = soloDigitos(busqueda);
  const cliente = typeof clienteDe(pedido) === 'object' ? clienteDe(pedido) ?? {} : {};
  return digitos.length >= 3 && [cliente.rut, cliente.telefono].some((dato) => soloDigitos(dato).includes(digitos));
}

/**
 * filtros: { estadoPedido, estadoPago, cobranza, anio, mes, cliente }. Los vacíos no filtran.
 * Devuelve los pedidos del más nuevo al más antiguo.
 */
export function filtrarPedidos(pedidos, filtros) {
  const { estadoPedido, estadoPago, cobranza, anio, mes, cliente } = filtros;
  return pedidos
    .filter((p) => {
      const fecha = new Date(p.fechaPedido);
      return (!estadoPedido || p.estadoPedido === estadoPedido)
        && (!estadoPago || p.estadoPago === estadoPago)
        && (!cobranza || esPorCobrar(p))
        && (!anio || fecha.getFullYear() === Number(anio))
        && (!mes || fecha.getMonth() + 1 === Number(mes))
        && coincideCliente(p, cliente);
    })
    .sort((a, b) => new Date(b.fechaPedido) - new Date(a.fechaPedido) || b.idPedido - a.idPedido);
}

/** Cantidad de pedidos y saldo por cobrar (sin contar los anulados). */
export function resumirPedidos(pedidos) {
  return {
    cantidad: pedidos.length,
    porCobrar: pedidos.filter(esPorCobrar).reduce((suma, p) => suma + Number(p.saldoPendiente ?? 0), 0),
  };
}

export function aniosDisponibles(pedidos) {
  return [...new Set(pedidos.map((p) => new Date(p.fechaPedido).getFullYear()))].sort((a, b) => b - a);
}
