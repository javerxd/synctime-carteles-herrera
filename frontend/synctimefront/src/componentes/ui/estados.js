import {
  AlarmClock, Ban, BadgeCheck, CalendarCheck, CalendarClock, CircleCheck, CircleDollarSign, CircleX, ClipboardCheck, Coins,
  FileCheck, FileClock, FileText, FileX, Hammer, Inbox, PackageCheck, Truck,
} from 'lucide-react';

// Estado operativo del pedido. «Anulado» y no «cancelado»: en Chile «cancelado» se entiende como pagado.
// Cada estado tiene su propio icono, además de su texto y su tono.
export const ESTADOS_PEDIDO = {
  aceptado: { texto: 'Aceptado', tono: 'neutro', icono: ClipboardCheck },
  en_proceso: { texto: 'En proceso', tono: 'info', icono: Hammer },
  terminado: { texto: 'Terminado', tono: 'listo', icono: PackageCheck },
  entregado: { texto: 'Entregado', tono: 'exito', icono: Truck },
  anulado: { texto: 'Anulado', tono: 'peligro', icono: Ban },
};

// Estado de pago, independiente del operativo: lo calcula la base a partir de los abonos.
export const ESTADOS_PAGO = {
  no_pagado: { texto: 'No pagado', tono: 'aviso', icono: CircleDollarSign },
  abonado: { texto: 'Abonado', tono: 'info', icono: Coins },
  pagado_total: { texto: 'Pagado total', tono: 'exito', icono: CircleCheck },
};

// Estado de la solicitud (fase 7): solicitado → cotizado → aceptado, o rechazado con motivo.
export const ESTADOS_SOLICITUD = {
  solicitado: { texto: 'Solicitada', tono: 'info', icono: Inbox },
  cotizado: { texto: 'Cotizada', tono: 'listo', icono: FileText },
  aceptado: { texto: 'Aceptada', tono: 'exito', icono: BadgeCheck },
  rechazado: { texto: 'Rechazada', tono: 'peligro', icono: CircleX },
};

// Estado de la cotización (fase 8): pendiente → aprobada o rechazada, por el cliente o por el personal.
export const ESTADOS_COTIZACION = {
  pendiente: { texto: 'Pendiente', tono: 'aviso', icono: FileClock },
  aprobada: { texto: 'Aprobada', tono: 'exito', icono: FileCheck },
  rechazada: { texto: 'Rechazada', tono: 'peligro', icono: FileX },
};

// La entrega en el calendario (fase 8): se calcula del estado del pedido y de su fecha estimada.
export const ESTADOS_ENTREGA = {
  pendiente: { texto: 'Pendiente', tono: 'info', icono: CalendarClock },
  entregado: { texto: 'Entregado', tono: 'exito', icono: CalendarCheck },
  atrasado: { texto: 'Atrasado', tono: 'peligro', icono: AlarmClock },
};

const soloTextos = (catalogo) => Object.fromEntries(Object.entries(catalogo).map(([clave, { texto }]) => [clave, texto]));

// Para listas desplegables de filtros: { aceptado: 'Aceptado', ... }
export const TEXTOS_ESTADO_PEDIDO = soloTextos(ESTADOS_PEDIDO);
export const TEXTOS_ESTADO_PAGO = soloTextos(ESTADOS_PAGO);
export const TEXTOS_ESTADO_SOLICITUD = soloTextos(ESTADOS_SOLICITUD);
export const TEXTOS_ESTADO_COTIZACION = soloTextos(ESTADOS_COTIZACION);
