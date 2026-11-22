import { useId } from 'react';
import { formatearPesos } from '../../utilidades/formato';
import estilos from './Reportes.module.css';

// El gráfico del reporte gerencial (barras por tramo, o barras horizontales por tipo de letrero), en SVG propio. Es un apoyo
// visual: sus mismos datos están en la tabla de abajo, y la descripción corta lo cuenta con palabras (RNFP15).
const ANCHO = 600;

/** Un número «redondo» por encima del máximo, para el techo del eje. */
function techo(maximo) {
  if (maximo <= 0) return 1;
  const potencia = 10 ** Math.floor(Math.log10(maximo));
  return [1, 2, 2.5, 5, 10].map((p) => p * potencia).find((v) => maximo <= v) ?? 10 * potencia;
}

/** Los números grandes del eje, cortos: «$12,5 M» en vez de «$12.500.000» (que no cabe). */
const ejePesos = (v) => (v >= 1_000_000 ? `${(v / 1_000_000).toFixed(1).replace('.', ',').replace(',0', '')} M` : formatearPesos(v));
const corta = (texto, largo) => (texto.length > largo ? `${texto.slice(0, largo - 1)}…` : texto);

const descripcion = (g) => `${g.titulo}. ${g.series.map((s) => `${s.nombre}: ${g.etiquetas.map((e, i) => `${e} ${formatearPesos(s.valores[i] ?? 0)}`).join(', ')}`).join('. ')}.`;

export function GraficoReporte({ grafico }) {
  const idRayas = useId();
  const horizontal = grafico.forma === 'barras-horizontales';
  const maximo = techo(Math.max(0, ...grafico.series.flatMap((s) => s.valores)));
  const alto = horizontal ? Math.max(120, grafico.etiquetas.length * 28 + 16) : 220;

  return (
    <figure className={estilos.grafico}>
      {/* El marco se desplaza hacia los lados en pantallas angostas (el gráfico conserva un ancho legible); es enfocable para el teclado. */}
      <div className={estilos.marcoGrafico} tabIndex={0} role="group" aria-label={`Gráfico: ${grafico.titulo}`}>
        <svg viewBox={`0 0 ${ANCHO} ${alto}`} role="img" aria-label={descripcion(grafico)} focusable="false">
          <defs>
            <pattern id={idRayas} width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
              <rect width="6" height="6" className={estilos.serie1} />
              <rect width="2.5" height="6" className={estilos.rayas} />
            </pattern>
          </defs>
          {horizontal ? <Horizontales g={grafico} maximo={maximo} alto={alto} /> : <Verticales g={grafico} maximo={maximo} alto={alto} rayas={idRayas} />}
        </svg>
      </div>
      {grafico.series.length > 1 && (
        <ul role="list" className={estilos.leyendaGrafico} aria-label="Series del gráfico">
          {grafico.series.map((s, i) => (
            <li key={s.nombre}><span className={`${estilos.muestra} ${estilos[`serie${i % 2}`]}`} aria-hidden="true" />{s.nombre}</li>
          ))}
        </ul>
      )}
    </figure>
  );
}

function Verticales({ g, maximo, alto, rayas }) {
  const izquierda = 78;
  const base = alto - 28;
  const area = ANCHO - izquierda - 8;
  const grupo = area / Math.max(g.etiquetas.length, 1);
  const barra = Math.min((grupo * 0.8) / g.series.length, 36);
  return (
    <>
      {[0, 0.5, 1].map((f) => (
        <g key={f}>
          <line x1={izquierda} x2={ANCHO} y1={base - (base - 8) * f} y2={base - (base - 8) * f} className={estilos.rejilla} />
          <text x={izquierda - 6} y={base - (base - 8) * f + 4} textAnchor="end" className={estilos.ejeTexto}>{ejePesos(maximo * f)}</text>
        </g>
      ))}
      {g.etiquetas.map((etiqueta, i) => {
        const inicio = izquierda + grupo * i + (grupo - barra * g.series.length) / 2;
        return (
          <g key={i}>
            {g.series.map((s, j) => {
              const altura = ((base - 8) * Math.max(s.valores[i] ?? 0, 0)) / maximo;
              return <rect key={s.nombre} x={inicio + barra * j} y={base - altura} width={Math.max(barra - 2, 1)} height={altura} className={j % 2 === 0 ? estilos.serie0 : undefined} fill={j % 2 === 0 ? undefined : `url(#${rayas})`} />;
            })}
            <text x={izquierda + grupo * i + grupo / 2} y={alto - 8} textAnchor="middle" className={estilos.ejeTexto}>{corta(etiqueta, g.etiquetas.length > 6 ? 5 : 12)}</text>
          </g>
        );
      })}
    </>
  );
}

function Horizontales({ g, maximo }) {
  const izquierda = 170;
  const largo = ANCHO - izquierda - 90;
  const serie = g.series[0] ?? { valores: [] };
  return g.etiquetas.map((etiqueta, i) => {
    const y = 8 + i * 28;
    const ancho = (largo * Math.max(serie.valores[i] ?? 0, 0)) / maximo;
    return (
      <g key={i}>
        <text x={izquierda - 8} y={y + 14} textAnchor="end" className={estilos.ejeTexto}>{etiqueta.length > 26 ? `${etiqueta.slice(0, 25)}…` : etiqueta}</text>
        <rect x={izquierda} y={y + 2} width={Math.max(ancho, 1)} height={18} className={estilos.serie0} />
        <text x={izquierda + ancho + 6} y={y + 15} className={estilos.ejeTexto}>{formatearPesos(serie.valores[i] ?? 0)}</text>
      </g>
    );
  });
}
