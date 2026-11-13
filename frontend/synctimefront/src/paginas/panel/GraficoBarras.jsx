import { useId } from 'react';
import estilos from './GraficoBarras.module.css';

/**
 * Gráfico de barras de una sola serie, en SVG propio (sin librería). Las categorías se identifican
 * por su etiqueta, nunca solo por el color; cada barra trae su valor al pasar el cursor y el gráfico
 * tiene su tabla de datos para el lector de pantalla y para quien prefiera leer cifras.
 * datos: [{ etiqueta, valor }]; formato: (valor) => texto.
 */
export function GraficoBarras({ titulo, descripcion, datos, formato = String, horizontal = false, nombreValor = 'Valor' }) {
  const id = useId();
  const maximo = Math.max(...datos.map((d) => d.valor), 1);
  const resumen = datos.map((d) => `${d.etiqueta}: ${formato(d.valor)}`).join('; ');

  return (
    <figure className={estilos.figura} aria-labelledby={`${id}-titulo`}>
      <figcaption className={estilos.leyenda}>
        <span id={`${id}-titulo`} className={estilos.titulo}>{titulo}</span>
        <span className={estilos.descripcion}>{descripcion}</span>
      </figcaption>

      {horizontal ? (
        <ul className={estilos.horizontal} aria-hidden="true">
          {datos.map((d) => (
            <li key={d.etiqueta} title={`${d.etiqueta}: ${formato(d.valor)}`}>
              <span className={estilos.etiquetaH}>{d.etiqueta}</span>
              <span className={estilos.pista}>
                <span className={estilos.barraH} style={{ width: `${(d.valor / maximo) * 100}%` }} />
              </span>
              <span className={estilos.valorH}>{formato(d.valor)}</span>
            </li>
          ))}
        </ul>
      ) : (
        <svg className={estilos.svg} viewBox={`0 0 ${datos.length * 40} 120`} preserveAspectRatio="none" role="img" aria-label={`${titulo}. ${resumen}`}>
          {[0.25, 0.5, 0.75].map((f) => (
            <line key={f} x1="0" x2={datos.length * 40} y1={110 - f * 100} y2={110 - f * 100} className={estilos.guia} />
          ))}
          {datos.map((d, i) => {
            const alto = Math.max((d.valor / maximo) * 100, d.valor > 0 ? 2 : 0);
            return (
              <g key={d.etiqueta}>
                <title>{`${d.etiqueta}: ${formato(d.valor)}`}</title>
                <rect x={i * 40 + 10} y={110 - alto} width="20" height={alto} rx="2" className={estilos.barra} />
                <rect x={i * 40} y="0" width="40" height="110" fill="transparent" />
              </g>
            );
          })}
          <line x1="0" x2={datos.length * 40} y1="110" y2="110" className={estilos.base} />
        </svg>
      )}
      {!horizontal && (
        <div className={estilos.ejes} aria-hidden="true" style={{ gridTemplateColumns: `repeat(${datos.length}, 1fr)` }}>
          {datos.map((d) => <span key={d.etiqueta}>{d.etiqueta}</span>)}
        </div>
      )}

      <details className={estilos.tabla}>
        <summary>Ver los datos en una tabla</summary>
        <table>
          <caption className="solo-lectores">{titulo}</caption>
          <thead><tr><th scope="col">Categoría</th><th scope="col">{nombreValor}</th></tr></thead>
          <tbody>
            {datos.map((d) => <tr key={d.etiqueta}><th scope="row">{d.etiqueta}</th><td className="cifras">{formato(d.valor)}</td></tr>)}
          </tbody>
        </table>
      </details>
    </figure>
  );
}
