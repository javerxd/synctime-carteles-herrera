import { formatearPesos } from '../../utilidades/formato';
import { useDebounce } from '../../utilidades/useDebounce';
import estilos from './Pedidos.module.css';

/**
 * Los montos de un pedido que se está creando: trabajo + instalación = monto final; menos el abono inicial,
 * el saldo. En columna, a la derecha y con cifras tabulares. El lector de pantalla oye el monto final y el
 * saldo cuando se deja de escribir.
 */
export function ResumenMontos({ trabajo, instalacion = 0, abono = 0, titulo = 'Montos del pedido' }) {
  const final = trabajo + instalacion;
  const saldo = Math.max(final - abono, 0);
  const anuncio = useDebounce(`Monto final ${formatearPesos(final)}. Saldo al crear ${formatearPesos(saldo)}.`, 900);
  return (
    <section aria-label={titulo} className={estilos.resumenMontos}>
      <dl className={estilos.desglose}>
        <div><dt>Trabajo</dt><dd className="cifras">{formatearPesos(trabajo)}</dd></div>
        <div><dt>Instalación</dt><dd className="cifras">{formatearPesos(instalacion)}</dd></div>
        <div className={estilos.filaFuerte}><dt>Monto final</dt><dd className="cifras">{formatearPesos(final)}</dd></div>
        <div><dt>Abono inicial</dt><dd className="cifras">{abono ? `− ${formatearPesos(abono)}` : formatearPesos(0)}</dd></div>
        <div className={estilos.filaSaldo}><dt>Saldo al crear</dt><dd className="cifras">{formatearPesos(saldo)}</dd></div>
      </dl>
      <p className={estilos.nota}>Montos con IVA incluido. El pago no se exige para crear el pedido.</p>
      <p role="status" aria-atomic="true" className="solo-lectores">{anuncio}</p>
    </section>
  );
}
