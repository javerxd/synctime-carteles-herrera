// Reglas del pedido, sus abonos y su listado (fase 8), las mismas que valida la API
// (docs/plan/fase-08-especificacion.md). Las fechas se comparan con «hoy» en Chile.
import { ESTADOS_PAGO, ESTADOS_PEDIDO } from '../../componentes/ui/estados';
import { formatearPesos } from '../../utilidades/formato';
import { diasEntre, esFecha, sumarAnios, sumarDias } from '../../utilidades/fechas';
import { parametrosBase } from '../../utilidades/filtrosUrl';
import { pesosDesdeTexto } from '../cotizaciones/reglasCotizacion';
import { nombreCompleto } from '../solicitudes/reglasSolicitud';

export const MODALIDADES = {
  retiro_local: 'Retiro en el local',
  entrega_domicilio: 'Entrega a domicilio',
  instalacion_terreno: 'Instalación en terreno',
};

/** La modalidad en pocas letras, para el calendario. */
export const MODALIDADES_CORTAS = { retiro_local: 'Retiro', entrega_domicilio: 'Despacho', instalacion_terreno: 'Instalación' };

export const ORIGENES_PEDIDO = { cotizacion: 'Desde cotización', directo: 'Directo en el taller' };

export const MEDIOS_PAGO = {
  efectivo: 'Efectivo',
  transferencia: 'Transferencia',
  tarjeta_debito: 'Tarjeta de débito',
  tarjeta_credito: 'Tarjeta de crédito',
  cheque: 'Cheque',
  otro: 'Otro',
};

export const LIMITES_PEDIDO = {
  direccion: 300,
  factura: 30,
  observacionAbono: 300,
  diasAtrasAbono: 365,
  motivoMinimo: 10,
  motivoAnulacion: 500,
  motivoAbono: 300,
  nota: 500,
  montoMaximo: 2_000_000_000,
};

/** El número que se muestra: el de la API («#45») o el id. */
export const numeroPedido = (p) => p?.numero ?? (p?.idPedido ? `#${p.idPedido}` : '');

/** Nombre de un cliente o un empleado, llegue como texto o como objeto. */
export const nombreDe = (persona) => (typeof persona === 'string' ? persona || '—' : nombreCompleto(persona));

// ── Avance (E09) ──

const ORDEN_ESTADOS = ['aceptado', 'en_proceso', 'terminado', 'entregado'];
const textoEstado = (estado) => ESTADOS_PEDIDO[estado]?.texto ?? estado;

/** Si ir de `actual` a `destino` es avanzar, volver un paso o anular. */
export function tipoTransicion(actual, destino) {
  if (destino === 'anulado') return 'anular';
  if (destino === 'entregado') return 'entregar';
  return ORDEN_ESTADOS.indexOf(destino) < ORDEN_ESTADOS.indexOf(actual) ? 'volver' : 'avanzar';
}

/** El texto del botón de cada transición posible. */
export function etiquetaTransicion(actual, destino) {
  switch (tipoTransicion(actual, destino)) {
    case 'anular': return 'Anular pedido';
    case 'entregar': return 'Registrar entrega';
    case 'volver': return `Volver a «${textoEstado(destino)}»`;
    default: return `Pasar a «${textoEstado(destino)}»`;
  }
}

/**
 * Las transiciones que entrega la API, ordenadas para la pantalla: primero avanzar (o entregar), luego volver
 * y al final anular. La primera que avanza es la acción principal de la ficha.
 */
export function ordenarTransiciones(actual, posibles = []) {
  const peso = { avanzar: 0, entregar: 0, volver: 1, anular: 2 };
  return [...posibles].filter((d) => d !== actual)
    .sort((a, b) => peso[tipoTransicion(actual, a)] - peso[tipoTransicion(actual, b)])
    .map((destino) => ({ destino, tipo: tipoTransicion(actual, destino), etiqueta: etiquetaTransicion(actual, destino) }));
}

/** Motivo con largo mínimo y máximo (anular un pedido o un abono). null si sirve. */
export function errorMotivo(texto, { minimo = LIMITES_PEDIDO.motivoMinimo, maximo, que = 'el motivo' } = {}) {
  const largo = String(texto ?? '').trim().length;
  if (largo < minimo) return `Escribe ${que}, con al menos ${minimo} caracteres. Queda en el historial.`;
  if (largo > maximo) return `Usa hasta ${maximo.toLocaleString('es-CL')} caracteres.`;
  return null;
}

/** Fecha real de entrega: no futura ni anterior a la fecha del pedido. */
export function errorFechaEntrega(fecha, { hoy, fechaPedido }) {
  if (!esFecha(fecha)) return 'Indica la fecha en que se entregó.';
  if (diasEntre(hoy, fecha) > 0) return 'La fecha de entrega no puede ser futura.';
  const desde = String(fechaPedido ?? '').slice(0, 10);
  if (esFecha(desde) && diasEntre(desde, fecha) < 0) return 'La entrega no puede ser anterior a la fecha del pedido.';
  return null;
}

// ── Datos del pedido (E08) ──

export const DATOS_VACIOS = {
  idEmpleadoResponsable: '', fechaEntregaEstimada: '', modalidadEntrega: '', requiereInstalacion: false,
  direccionInstalacion: '', costoInstalacionAdicional: '', numeroFactura: '',
};

export const ORDEN_DATOS_PEDIDO = [
  'idEmpleadoResponsable', 'fechaEntregaEstimada', 'modalidadEntrega', 'requiereInstalacion', 'direccionInstalacion',
  'costoInstalacionAdicional', 'montoBaseTrabajo', 'numeroFactura',
];

/**
 * Errores de los datos del pedido. `fechaVigente` es la que ya tenía al editar (se acepta aunque haya pasado).
 * `conMontoBase` pide además el monto del trabajo (edición) y `abonado` es el tope inferior del monto final.
 */
export function validarDatosPedido(datos, { hoy, fechaVigente = null, conMontoBase = false, abonado = 0 } = {}) {
  const e = {};
  if (!datos.idEmpleadoResponsable) e.idEmpleadoResponsable = 'Elige quién es responsable del pedido.';
  const fecha = datos.fechaEntregaEstimada;
  if (!esFecha(fecha)) e.fechaEntregaEstimada = 'Indica la fecha estimada de entrega.';
  else if (fecha !== fechaVigente) {
    if (diasEntre(hoy, fecha) < 0) e.fechaEntregaEstimada = 'La fecha estimada no puede ser anterior a hoy.';
    else if (diasEntre(sumarAnios(hoy, 2), fecha) > 0) e.fechaEntregaEstimada = 'La fecha estimada puede ser como mucho dentro de 2 años.';
  }
  if (!(datos.modalidadEntrega in MODALIDADES)) e.modalidadEntrega = 'Elige cómo se entrega el pedido.';
  if (datos.modalidadEntrega === 'instalacion_terreno' && !datos.requiereInstalacion) {
    e.requiereInstalacion = 'La instalación en terreno requiere instalación: márcala.';
  }
  const direccion = datos.direccionInstalacion.trim();
  if (datos.modalidadEntrega && datos.modalidadEntrega !== 'retiro_local' && !direccion) {
    e.direccionInstalacion = 'Escribe la dirección de entrega o instalación.';
  } else if (direccion.length > LIMITES_PEDIDO.direccion) e.direccionInstalacion = 'Usa hasta 300 caracteres.';
  const costo = pesosDesdeTexto(datos.costoInstalacionAdicional);
  if (datos.requiereInstalacion && costo !== null && (!Number.isInteger(costo) || costo < 0 || costo > LIMITES_PEDIDO.montoMaximo)) {
    e.costoInstalacionAdicional = 'Escribe el costo de instalación en pesos, sin decimales.';
  }
  if (conMontoBase) {
    const base = pesosDesdeTexto(datos.montoBaseTrabajo);
    if (base === null || !Number.isInteger(base) || base < 0 || base > LIMITES_PEDIDO.montoMaximo) {
      e.montoBaseTrabajo = 'Escribe el monto del trabajo en pesos, sin decimales.';
    } else if (montoFinal(datos) < abonado) {
      e.montoBaseTrabajo = `El monto final no puede quedar bajo lo ya abonado (${formatearPesos(abonado)}).`;
    }
  }
  if (datos.numeroFactura.trim().length > LIMITES_PEDIDO.factura) e.numeroFactura = 'Usa hasta 30 caracteres.';
  return e;
}

/** Costo de instalación que cuenta: 0 si no requiere instalación. */
export function costoInstalacion(datos) {
  if (!datos.requiereInstalacion) return 0;
  const costo = pesosDesdeTexto(datos.costoInstalacionAdicional);
  return Number.isInteger(costo) && costo >= 0 ? costo : 0;
}

/** Monto final = monto base del trabajo + instalación (en la edición, con el monto base escrito). */
export function montoFinal(datos, montoBase = pesosDesdeTexto(datos.montoBaseTrabajo)) {
  return (Number.isInteger(montoBase) ? montoBase : 0) + costoInstalacion(datos);
}

/** Los datos E08 como los pide la API. */
export function datosPedidoParaApi(datos, { conMontoBase = false } = {}) {
  const cuerpo = {
    idEmpleadoResponsable: Number(datos.idEmpleadoResponsable),
    fechaEntregaEstimada: datos.fechaEntregaEstimada,
    modalidadEntrega: datos.modalidadEntrega,
    requiereInstalacion: Boolean(datos.requiereInstalacion),
    direccionInstalacion: datos.modalidadEntrega === 'retiro_local' ? null : datos.direccionInstalacion.trim() || null,
    costoInstalacionAdicional: costoInstalacion(datos),
    numeroFactura: datos.numeroFactura.trim() || null,
  };
  if (conMontoBase) cuerpo.montoBaseTrabajo = pesosDesdeTexto(datos.montoBaseTrabajo);
  return cuerpo;
}

/** La ficha como estado del formulario de edición. */
export function datosDesdePedido(p) {
  return {
    idEmpleadoResponsable: p?.responsable?.idEmpleado ? String(p.responsable.idEmpleado) : '',
    fechaEntregaEstimada: String(p?.fechaEntregaEstimada ?? '').slice(0, 10),
    modalidadEntrega: p?.modalidadEntrega ?? '',
    requiereInstalacion: Boolean(p?.requiereInstalacion),
    direccionInstalacion: p?.direccionInstalacion ?? '',
    costoInstalacionAdicional: p?.costoInstalacionAdicional ? String(Math.round(p.costoInstalacionAdicional)) : '',
    numeroFactura: p?.numeroFactura ?? '',
    montoBaseTrabajo: p?.montoBaseTrabajo === null || p?.montoBaseTrabajo === undefined ? '' : String(Math.round(p.montoBaseTrabajo)),
  };
}

// ── Abonos (E25) ──

export const ABONO_VACIO = { monto: '', fechaPago: '', medioPago: '', observacion: '', numeroFactura: '' };
export const ORDEN_ABONO = ['monto', 'fechaPago', 'medioPago', 'observacion', 'numeroFactura'];

/**
 * Errores del abono. `saldo` es el tope (el saldo pendiente o, al crear el pedido, el monto final); si es
 * null, no se compara. `opcional`: el abono inicial puede quedar en blanco entero.
 */
export function validarAbono(abono, { hoy, saldo = null, opcional = false } = {}) {
  const e = {};
  const enBlanco = !abono.monto.trim() && !abono.medioPago && !abono.observacion.trim();
  if (opcional && enBlanco) return e;
  const monto = pesosDesdeTexto(abono.monto);
  if (monto === null || !Number.isInteger(monto) || monto <= 0) e.monto = 'Escribe el monto en pesos, sin decimales y mayor que $0.';
  else if (saldo !== null && monto > saldo) e.monto = `El abono supera el saldo pendiente (${formatearPesos(saldo)}).`;
  if (abono.fechaPago) {
    if (!esFecha(abono.fechaPago)) e.fechaPago = 'Escribe una fecha válida.';
    else if (diasEntre(hoy, abono.fechaPago) > 0) e.fechaPago = 'La fecha de pago no puede ser futura.';
    else if (diasEntre(abono.fechaPago, hoy) > LIMITES_PEDIDO.diasAtrasAbono) e.fechaPago = 'La fecha de pago puede ser de hasta un año atrás.';
  }
  if (!(abono.medioPago in MEDIOS_PAGO)) e.medioPago = 'Elige el medio de pago.';
  if (abono.observacion.trim().length > LIMITES_PEDIDO.observacionAbono) e.observacion = 'Usa hasta 300 caracteres.';
  if ((abono.numeroFactura ?? '').trim().length > LIMITES_PEDIDO.factura) e.numeroFactura = 'Usa hasta 30 caracteres.';
  return e;
}

/** El abono como lo pide la API (la fecha vacía la pone la API: hoy). null si el abono opcional quedó en blanco. */
export function abonoParaApi(abono, { opcional = false } = {}) {
  if (opcional && !abono.monto.trim()) return null;
  const cuerpo = {
    monto: pesosDesdeTexto(abono.monto),
    fechaPago: abono.fechaPago || undefined,
    medioPago: abono.medioPago,
    observacion: abono.observacion.trim() || null,
  };
  if (abono.numeroFactura?.trim()) cuerpo.numeroFactura = abono.numeroFactura.trim();
  return cuerpo;
}

// ── Listado (RFU36, E27, S24) ──

const ORDENES_PEDIDOS = ['fecha_desc', 'fecha_asc', 'entrega_asc', 'saldo_desc'];
export const TEXTOS_ORDEN_PEDIDOS = {
  fecha_desc: 'Más recientes',
  fecha_asc: 'Más antiguos',
  entrega_asc: 'Entrega más próxima',
  saldo_desc: 'Mayor saldo',
};

export const FILTROS_PEDIDOS = {
  campos: {
    estadoPedido: (v) => v in ESTADOS_PEDIDO,
    estadoPago: (v) => v in ESTADOS_PAGO,
    cobranza: (v) => v === 'true',
    anio: (v) => /^\d{4}$/.test(v) && Number(v) >= 2000 && Number(v) <= 2100,
    // El mes solo filtra junto con el año.
    mes: (v, crudos) => /^(?:[1-9]|1[0-2])$/.test(v) && Boolean(crudos.anio) && FILTROS_PEDIDOS.campos.anio(crudos.anio),
    idCliente: (v) => /^[1-9]\d{0,9}$/.test(v),
    q: (v) => v.length <= 100,
    orden: (v) => ORDENES_PEDIDOS.includes(v),
  },
  predeterminados: { orden: 'fecha_desc' },
  tamano: 20,
};

/** Los parámetros de GET /pedidos: `cobranza` viaja como booleano. */
export function parametrosPedidos(filtros) {
  const base = parametrosBase(filtros, FILTROS_PEDIDOS);
  return { ...base, cobranza: filtros.cobranza === 'true' ? true : undefined };
}

/**
 * Los años que ofrece el filtro: desde 2024 hasta el próximo, más el de la dirección si es válido y queda fuera
 * (así la lista siempre muestra el filtro aplicado). Del más reciente al más antiguo.
 */
export function aniosFiltro(hoy, elegido = '') {
  const actual = Number(String(hoy).slice(0, 4));
  const anios = Array.from({ length: actual + 2 - 2024 }, (_, i) => actual + 1 - i);
  const extra = Number(elegido);
  if (elegido && FILTROS_PEDIDOS.campos.anio(String(elegido)) && !anios.includes(extra)) anios.push(extra);
  return anios.sort((a, b) => b - a);
}

/** Hoy y los próximos 3 días: el rango de las «próximas entregas». */
export const rangoProximas = (hoy) => ({ desde: hoy, hasta: sumarDias(hoy, 3) });
