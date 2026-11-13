import {
  Ban, CircleCheck, CircleDollarSign, ClipboardCheck, Coins, Hammer, PackageCheck, Truck,
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

const soloTextos = (catalogo) => Object.fromEntries(Object.entries(catalogo).map(([clave, { texto }]) => [clave, texto]));

// Para listas desplegables de filtros: { aceptado: 'Aceptado', ... }
export const TEXTOS_ESTADO_PEDIDO = soloTextos(ESTADOS_PEDIDO);
export const TEXTOS_ESTADO_PAGO = soloTextos(ESTADOS_PAGO);
