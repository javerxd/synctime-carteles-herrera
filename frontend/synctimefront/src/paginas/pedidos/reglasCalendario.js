// Calendario de entregas (S08, fase 8): la cuadrícula del mes empieza el lunes, como en Chile, y cubre
// semanas completas. Se pide a la API de la primera a la última celda (como mucho 42 días, bajo el tope de
// 62), así los días del mes anterior y del siguiente también muestran sus entregas.
import { diaSemana, fechaIso, partesFecha, sumarDias } from '../../utilidades/fechas';

const MES = /^(\d{4})-(0[1-9]|1[0-2])$/;

/** «2026-11» → { anio: 2026, mes: 11 }, o null. */
export function mesDesdeTexto(texto) {
  const m = String(texto ?? '').match(MES);
  if (!m) return null;
  const anio = Number(m[1]);
  return anio >= 2000 && anio <= 2100 ? { anio, mes: Number(m[2]) } : null;
}

export const textoMes = ({ anio, mes }) => `${anio}-${String(mes).padStart(2, '0')}`;

/** El mes de una fecha «aaaa-mm-dd». */
export function mesDeFecha(fecha) {
  const p = partesFecha(fecha);
  return p ? { anio: p.anio, mes: p.mes } : null;
}

/** El mes anterior (-1) o siguiente (+1). */
export function moverMes({ anio, mes }, pasos) {
  const total = anio * 12 + (mes - 1) + pasos;
  return { anio: Math.floor(total / 12), mes: (total % 12) + 1 };
}

/**
 * Semanas del mes: [[{ fecha, dia, enMes, esHoy, finDeSemana }, … 7], …]. Siempre semanas completas, de
 * lunes a domingo (4 a 6 filas).
 */
export function construirMes({ anio, mes }, hoy) {
  const primero = fechaIso(anio, mes, 1);
  const ultimo = fechaIso(anio, mes + 1, 0);
  const inicio = sumarDias(primero, -diaSemana(primero));
  const fin = sumarDias(ultimo, 6 - diaSemana(ultimo));
  const semanas = [];
  for (let fecha = inicio; fecha <= fin; fecha = sumarDias(fecha, 1)) {
    if (diaSemana(fecha) === 0) semanas.push([]);
    const p = partesFecha(fecha);
    semanas.at(-1).push({ fecha, dia: p.dia, enMes: p.mes === mes, esHoy: fecha === hoy, finDeSemana: diaSemana(fecha) >= 5 });
  }
  return semanas;
}

/** El rango que se pide a la API: de la primera a la última celda de la cuadrícula. */
export function rangoCuadricula(mesElegido) {
  const semanas = construirMes(mesElegido, '');
  return { desde: semanas[0][0].fecha, hasta: semanas.at(-1)[6].fecha };
}

/** Pendiente, entregado o atrasado: el entregado se ubica en su fecha real; el resto, en la estimada. */
export function estadoEntrega(pedido) {
  if (pedido.estadoPedido === 'entregado') return 'entregado';
  return pedido.atrasado ? 'atrasado' : 'pendiente';
}

/** { '2026-11-14': [pedidos…] }, cada día con los atrasados primero y luego por número. */
export function agruparPorDia(pedidos = []) {
  const orden = { atrasado: 0, pendiente: 1, entregado: 2 };
  const grupos = {};
  for (const p of pedidos) {
    const dia = String(p.fecha ?? '').slice(0, 10);
    if (!partesFecha(dia)) continue;
    (grupos[dia] ??= []).push(p);
  }
  for (const lista of Object.values(grupos)) {
    lista.sort((a, b) => orden[estadoEntrega(a)] - orden[estadoEntrega(b)] || (a.idPedido ?? 0) - (b.idPedido ?? 0));
  }
  return grupos;
}

/** Cuántos pedidos de cada estado hay en el mes (solo los días del mes, no los del borde). */
export function contarMes(pedidos = [], { anio, mes }) {
  const cuenta = { pendiente: 0, entregado: 0, atrasado: 0 };
  for (const p of pedidos) {
    const fecha = partesFecha(String(p.fecha ?? '').slice(0, 10));
    if (fecha && fecha.anio === anio && fecha.mes === mes) cuenta[estadoEntrega(p)] += 1;
  }
  return cuenta;
}
