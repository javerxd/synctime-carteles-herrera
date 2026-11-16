import { ArrowDown } from 'lucide-react';
import { formatearPesos } from '../../utilidades/formato';
import { formatearNumero, formatearPorcentaje, NOMBRES_PASOS } from './formatoIndicadores';
import estilos from './Indicadores.module.css';

/**
 * Embudo del canal web: cada paso es una barra centrada, proporcional al primero, con su cantidad y su porcentaje
 * escritos; entre pasos se indica qué parte pasó al siguiente.
 */
export function GraficoEmbudo({ datos }) {
  const base = datos.pasos[0].cantidad || 1;
  return (
    <ol className={estilos.embudo}>
      {datos.pasos.map((p, i) => (
        <li key={p.clave} className={estilos.pasoEmbudo}>
          {i > 0 && (
            <span className={estilos.transicion}>
              <ArrowDown size={14} strokeWidth={2} aria-hidden="true" />
              {p.porcentajePaso === null ? 'Sin paso anterior' : `${formatearPorcentaje(p.porcentajePaso)} pasa a este paso`}
            </span>
          )}
          <span className={estilos.filaEmbudo}>
            <span className={estilos.nombrePaso}>{NOMBRES_PASOS[p.clave]}</span>
            <span className={estilos.cifrasPaso}>
              <strong>{formatearNumero(p.cantidad)}</strong> · {formatearPorcentaje(p.porcentaje)}
            </span>
          </span>
          <span className={estilos.pistaEmbudo} aria-hidden="true">
            <span className={[estilos.barraEmbudo, i === datos.pasos.length - 1 && estilos.barraFinal].filter(Boolean).join(' ')}
              style={{ width: `${Math.max((p.cantidad / base) * 100, p.cantidad ? 2 : 0)}%` }} />
          </span>
        </li>
      ))}
    </ol>
  );
}

export function TablaEmbudo({ datos }) {
  return (
    <div className={estilos.marcoTabla} tabIndex={0} role="group" aria-label={`Tabla: Embudo del canal web`}>
      <table className={estilos.tablaDatos}>
        <caption className="solo-lectores">Embudo del canal web</caption>
        <thead><tr><th scope="col">Paso</th><th scope="col">Cantidad</th><th scope="col">Del total</th><th scope="col">Del paso anterior</th></tr></thead>
        <tbody>
          {datos.pasos.map((p) => (
            <tr key={p.clave}>
              <th scope="row">{NOMBRES_PASOS[p.clave]}</th>
              <td className="cifras">{formatearNumero(p.cantidad)}</td>
              <td className="cifras">{formatearPorcentaje(p.porcentaje)}</td>
              <td className="cifras">{formatearPorcentaje(p.porcentajePaso)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Demanda por categoría: barras horizontales de mayor a menor, con la etiqueta y el valor escritos. */
export function GraficoDemanda({ categorias, medida }) {
  const maximo = Math.max(1, ...categorias.map((c) => c[medida]));
  return (
    <ul className={estilos.demanda}>
      {categorias.map((c) => (
        <li key={c.idCategoria} className={estilos.filaDemanda}>
          <span className={estilos.nombreDemanda}>{c.nombre}</span>
          <span className={estilos.pistaDemanda} aria-hidden="true">
            <span className={estilos.barraDemanda} style={{ width: `${Math.max((c[medida] / maximo) * 100, 2)}%` }} />
          </span>
          <span className={estilos.valorDemanda}>{medida === 'monto' ? formatearPesos(c.monto) : `${formatearNumero(c.cantidad)} u.`}</span>
        </li>
      ))}
    </ul>
  );
}

export function TablaDemanda({ categorias }) {
  return (
    <div className={estilos.marcoTabla} tabIndex={0} role="group" aria-label={`Tabla: Demanda por categoría`}>
      <table className={estilos.tablaDatos}>
        <caption className="solo-lectores">Demanda por categoría</caption>
        <thead><tr><th scope="col">Categoría</th><th scope="col">Monto</th><th scope="col">Unidades</th><th scope="col">Pedidos</th></tr></thead>
        <tbody>
          {categorias.map((c) => (
            <tr key={c.idCategoria}>
              <th scope="row">{c.nombre}</th>
              <td className="cifras">{formatearPesos(c.monto)}</td>
              <td className="cifras">{formatearNumero(c.cantidad)}</td>
              <td className="cifras">{formatearNumero(c.pedidos)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
