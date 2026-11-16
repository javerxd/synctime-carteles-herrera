import { useId, useState } from 'react';
import { formatearPesos, formatearFecha } from '../../utilidades/formato';
import { formatearNumero } from './formatoIndicadores';
import estilos from './Indicadores.module.css';

const ANCHO = 600;
const ALTO = 200;
const MARGEN = 8;

const formato = (metrica, valor) => (valor === null || valor === undefined ? '—' : metrica === 'ingresos' ? formatearPesos(valor) : formatearNumero(valor));
const rango = (p) => (p.desde === p.hasta ? formatearFecha(p.desde) : `${formatearFecha(p.desde)} al ${formatearFecha(p.hasta)}`);

/** Línea (y su área) de una serie; los puntos sin dato cortan la línea. */
function trazo(valores, x, y) {
  let d = '';
  let enLinea = false;
  valores.forEach((v, i) => {
    if (v === null || v === undefined) { enLinea = false; return; }
    d += `${enLinea ? 'L' : 'M'}${x(i).toFixed(1)},${y(v).toFixed(1)} `;
    enLinea = true;
  });
  return d.trim();
}

/** Tabla de datos de la evolución (la alternativa al gráfico, también para el lector de pantalla). */
export function TablaEvolucion({ datos }) {
  const conComparacion = Boolean(datos.comparacion);
  return (
    <div className={estilos.marcoTabla} tabIndex={0} role="group" aria-label={`Tabla: Evolución comercial de ${datos.periodo.etiqueta}`}>
      <table className={estilos.tablaDatos}>
        <caption className="solo-lectores">Evolución comercial de {datos.periodo.etiqueta}</caption>
        <thead>
          <tr>
            <th scope="col">Tramo</th>
            <th scope="col">Ingresos</th>
            {conComparacion && <th scope="col">Comparación: ingresos</th>}
            <th scope="col">Pedidos</th>
            {conComparacion && <th scope="col">Comparación: pedidos</th>}
          </tr>
        </thead>
        <tbody>
          {datos.serie.map((p) => (
            <tr key={p.desde}>
              <th scope="row">{rango(p)}</th>
              <td className="cifras">{formatearPesos(p.ingresos)}</td>
              {conComparacion && <td className="cifras">{formato('ingresos', p.ingresosComparacion)}</td>}
              <td className="cifras">{formatearNumero(p.pedidos)}</td>
              {conComparacion && <td className="cifras">{formato('pedidos', p.pedidosComparacion)}</td>}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/**
 * Evolución comercial en SVG propio: el período actual en línea con área (tinta) y el de comparación en línea
 * punteada (gris), así se distinguen sin depender del color. Se recorre con el puntero o, con el foco en el
 * gráfico, con las flechas; el valor del tramo elegido se anuncia en una región viva.
 */
export function GraficoEvolucion({ datos, metrica }) {
  const id = useId();
  const [activo, setActivo] = useState(null);
  const { serie } = datos;
  const actuales = serie.map((p) => p[metrica]);
  const comparados = serie.map((p) => p[`${metrica}Comparacion`]);
  const maximo = Math.max(1, ...actuales, ...comparados.filter((v) => v !== null));
  const paso = serie.length > 1 ? (ANCHO - MARGEN * 2) / (serie.length - 1) : 0;
  const x = (i) => (serie.length > 1 ? MARGEN + i * paso : ANCHO / 2);
  const y = (v) => ALTO - MARGEN - (v / maximo) * (ALTO - MARGEN * 2);
  const linea = trazo(actuales, x, y);
  const area = serie.length ? `${linea} L${x(serie.length - 1).toFixed(1)},${ALTO - MARGEN} L${x(0).toFixed(1)},${ALTO - MARGEN} Z` : '';
  const lineaComparacion = datos.comparacion ? trazo(comparados, x, y) : '';
  const punto = activo === null ? null : serie[activo];
  const posicion = punto ? (x(activo) / ANCHO) * 100 : 0;
  const detalle = punto
    ? `${rango(punto)}: ${formato(metrica, punto[metrica])}${datos.comparacion ? `; comparación: ${formato(metrica, punto[`${metrica}Comparacion`])}` : ''}`
    : '';
  // Pocas etiquetas en el eje, para que quepan a 360 px.
  const cadaCuanto = Math.ceil(serie.length / 6);

  function alTeclear(evento) {
    const mover = { ArrowRight: 1, ArrowLeft: -1 }[evento.key];
    if (evento.key === 'Home' || evento.key === 'End') {
      evento.preventDefault();
      setActivo(evento.key === 'Home' ? 0 : serie.length - 1);
    } else if (mover) {
      evento.preventDefault();
      setActivo((a) => Math.min(serie.length - 1, Math.max(0, (a ?? (mover > 0 ? -1 : serie.length)) + mover)));
    } else if (evento.key === 'Escape') {
      setActivo(null);
    }
  }

  return (
    <div className={estilos.figuraEvolucion}>
      <ul className={estilos.leyendaSeries}>
        <li><span className={estilos.muestraActual} aria-hidden="true" />{datos.periodo.etiqueta}</li>
        {datos.comparacion && <li><span className={estilos.muestraComparacion} aria-hidden="true" />{datos.comparacion.etiqueta}</li>}
      </ul>
      <div
        className={estilos.lienzo}
        role="group"
        tabIndex={0}
        aria-label={`Evolución de ${metrica === 'ingresos' ? 'los ingresos' : 'los pedidos'}`}
        aria-describedby={`${id}-uso`}
        onKeyDown={alTeclear}
        onFocus={() => setActivo((a) => (a === null ? serie.length - 1 : a))}
        onPointerLeave={() => setActivo(null)}
        onBlur={() => setActivo(null)}
      >
        <span className={estilos.ejeMaximo} aria-hidden="true">{formato(metrica, maximo)}</span>
        <svg viewBox={`0 0 ${ANCHO} ${ALTO}`} preserveAspectRatio="none" className={estilos.svgEvolucion} aria-hidden="true">
          {[0.25, 0.5, 0.75].map((f) => <line key={f} x1="0" x2={ANCHO} y1={MARGEN + f * (ALTO - MARGEN * 2)} y2={MARGEN + f * (ALTO - MARGEN * 2)} className={estilos.guia} />)}
          <line x1="0" x2={ANCHO} y1={ALTO - MARGEN} y2={ALTO - MARGEN} className={estilos.base} />
          {area && <path d={area} className={estilos.area} />}
          {lineaComparacion && <path d={lineaComparacion} className={estilos.lineaComparacion} />}
          {linea && <path d={linea} className={estilos.lineaActual} />}
          {punto && <line x1={x(activo)} x2={x(activo)} y1={MARGEN} y2={ALTO - MARGEN} className={estilos.cursor} />}
          {serie.map((p, i) => (
            <rect key={p.desde} x={x(i) - paso / 2} y="0" width={Math.max(paso, 12)} height={ALTO} fill="transparent" onPointerEnter={() => setActivo(i)} />
          ))}
        </svg>
        {punto && (
          <div className={[estilos.globo, posicion < 20 && estilos.globoInicio, posicion > 80 && estilos.globoFin].filter(Boolean).join(' ')}
            style={{ left: `${posicion}%` }} aria-hidden="true">
            <strong>{rango(punto)}</strong>
            <span>{formato(metrica, punto[metrica])}</span>
            {datos.comparacion && <span className={estilos.globoComparacion}>Comparación: {formato(metrica, punto[`${metrica}Comparacion`])}</span>}
          </div>
        )}
      </div>
      <div className={estilos.ejeX} aria-hidden="true">
        {serie.map((p, i) => <span key={p.desde} style={{ left: `${(x(i) / ANCHO) * 100}%` }}>{i % cadaCuanto === 0 ? p.etiqueta : ''}</span>)}
      </div>
      <p id={`${id}-uso`} className="solo-lectores">Flechas izquierda y derecha para recorrer los tramos; Inicio y Fin para ir a los extremos; Escape para cerrar.</p>
      <p role="status" className="solo-lectores">{detalle}</p>
    </div>
  );
}
