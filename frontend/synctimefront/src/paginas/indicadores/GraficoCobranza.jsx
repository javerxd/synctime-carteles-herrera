import { Check } from 'lucide-react';
import { ESTADOS_PAGO } from '../../componentes/ui';
import { formatearPesos } from '../../utilidades/formato';
import { useId } from 'react';
import { formatearNumero, SEGMENTOS_FILTRABLES } from './formatoIndicadores';
import estilos from './Indicadores.module.css';

const RADIO = 70;
const CIRCUNFERENCIA = 2 * Math.PI * RADIO;
const SEPARACION = 3; // un espacio blanco entre segmentos: se distinguen aunque los colores se parezcan

const plural = (n) => `${formatearNumero(n)} ${n === 1 ? 'pedido' : 'pedidos'}`;

export function TablaCobranza({ datos }) {
  return (
    <div className={estilos.marcoTabla} tabIndex={0} role="group" aria-label={`Tabla: Cobranza por estado de pago`}>
      <table className={estilos.tablaDatos}>
        <caption className="solo-lectores">Cobranza por estado de pago</caption>
        <thead><tr><th scope="col">Estado de pago</th><th scope="col">Pedidos</th><th scope="col">Saldo</th></tr></thead>
        <tbody>
          {datos.segmentos.map((s) => (
            <tr key={s.estadoPago}><th scope="row">{ESTADOS_PAGO[s.estadoPago].texto}</th><td className="cifras">{formatearNumero(s.cantidad)}</td><td className="cifras">{formatearPesos(s.saldo)}</td></tr>
          ))}
        </tbody>
        <tfoot><tr><th scope="row">Total por cobrar</th><td className="cifras">{formatearNumero(datos.cantidadPorCobrar)}</td><td className="cifras">{formatearPesos(datos.saldoTotal)}</td></tr></tfoot>
      </table>
    </div>
  );
}

/**
 * Anillo de cobranza en SVG propio: tres segmentos (por cantidad de pedidos) y el saldo por cobrar en el centro.
 * Cada estado lleva su icono, su nombre y sus cifras en la leyenda, así no depende del color. Un clic en un
 * segmento con saldo (o en su botón de la leyenda, que es lo que se usa con teclado) filtra la tabla de abajo;
 * otro clic lo quita.
 */
export function GraficoCobranza({ datos, elegido, alElegir }) {
  const idAyuda = useId();
  const total = datos.segmentos.reduce((s, x) => s + x.cantidad, 0);
  const largos = datos.segmentos.map((s) => (total ? (s.cantidad / total) * CIRCUNFERENCIA : 0));
  const arcos = datos.segmentos.map((s, i) => ({ ...s, largo: largos[i], inicio: largos.slice(0, i).reduce((a, b) => a + b, 0) }));
  const elegir = (estado) => SEGMENTOS_FILTRABLES.includes(estado) && alElegir(elegido === estado ? '' : estado);

  return (
    <div className={estilos.cobranza}>
      <div className={estilos.anillo}>
        <svg viewBox="0 0 180 180" aria-hidden="true">
          <circle cx="90" cy="90" r={RADIO} className={estilos.pistaAnillo} />
          {arcos.filter((a) => a.largo > 0).map((a) => {
            const visible = Math.max(a.largo - (arcos.filter((x) => x.largo > 0).length > 1 ? SEPARACION : 0), 1);
            const filtrable = SEGMENTOS_FILTRABLES.includes(a.estadoPago);
            return (
              <circle
                key={a.estadoPago}
                cx="90" cy="90" r={RADIO}
                strokeDasharray={`${visible} ${CIRCUNFERENCIA - visible}`}
                strokeDashoffset={-a.inicio}
                transform="rotate(-90 90 90)"
                className={[estilos.segmento, estilos[`segmento_${a.estadoPago}`], elegido === a.estadoPago && estilos.segmentoElegido, filtrable && estilos.segmentoFiltrable].filter(Boolean).join(' ')}
                onClick={filtrable ? () => elegir(a.estadoPago) : undefined}
              />
            );
          })}
        </svg>
        <div className={estilos.centroAnillo}>
          <span className={estilos.valorCentro}>{formatearPesos(datos.saldoTotal)}</span>
          <span className={estilos.textoCentro}>por cobrar</span>
        </div>
      </div>

      <p id={idAyuda} className="solo-lectores">Pulsa «No pagado» o «Abonado» para filtrar la tabla «Pedidos por cobrar»; púlsalo de nuevo para quitar el filtro.</p>
      <ul className={estilos.leyendaCobranza}>
        {datos.segmentos.map((s) => {
          const { texto, icono: Icono } = ESTADOS_PAGO[s.estadoPago];
          const contenido = (
            <>
              <span className={[estilos.muestraSegmento, estilos[`muestra_${s.estadoPago}`]].join(' ')} aria-hidden="true" />
              <Icono size={16} strokeWidth={1.75} aria-hidden="true" className={estilos.iconoLeyenda} />
              <span className={estilos.textoLeyenda}>
                <span className={estilos.nombreLeyenda}>{texto}</span>
                <span className={estilos.cifrasLeyenda}>{plural(s.cantidad)}{s.estadoPago !== 'pagado_total' && ` · ${formatearPesos(s.saldo)}`}</span>
              </span>
              {elegido === s.estadoPago && <Check size={16} strokeWidth={2.25} aria-hidden="true" className={estilos.marcaLeyenda} />}
            </>
          );
          return (
            <li key={s.estadoPago}>
              {SEGMENTOS_FILTRABLES.includes(s.estadoPago) ? (
                <button type="button" aria-pressed={elegido === s.estadoPago} onClick={() => elegir(s.estadoPago)} className={estilos.itemLeyenda}
                  aria-label={`${texto}, ${plural(s.cantidad)}, ${formatearPesos(s.saldo)}`} aria-describedby={idAyuda}>
                  {contenido}
                </button>
              ) : (
                <div className={estilos.itemLeyenda}>{contenido}</div>
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
