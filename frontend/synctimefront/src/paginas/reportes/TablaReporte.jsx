import { useId } from 'react';
import { EstadoVacio, Insignia, TablaAdaptable } from '../../componentes/ui';
import { formatearCelda } from './reglasReportes';
import estilos from './Reportes.module.css';

const NUMERICOS = ['pesos', 'entero', 'porcentaje'];
// Un reporte puede llegar a 50.000 filas (el tope de la API): pintarlas todas congelaría el navegador. Aquí se ven las primeras; el
// archivo exportado lleva todas, y los totales son los de todas.
export const FILAS_EN_PANTALLA = 1000;

/**
 * Una tabla del documento del reporte, tal como la exportan Excel, CSV y PDF (lo que ves es lo que sale): sus columnas, sus
 * filas y, si alguna columna suma, sus totales. Las filas que la API excluye de los totales (los pedidos anulados) se ven
 * marcadas y avisan que no suman. La tabla es de datos: se vuelve fichas en un contenedor angosto (TablaAdaptable).
 */
export function TablaReporte({ tabla }) {
  const id = useId();
  const excluida = (celdas) => tabla.exclusion && String(celdas[tabla.exclusion.columna]) === tabla.exclusion.valor;
  // El primer texto (el cliente, el concepto, el tramo) es el encabezado de su fila.
  const principal = Math.max(tabla.columnas.findIndex((c) => c.tipo === 'texto'), 0);
  const columnas = tabla.columnas.map((c, i) => ({
    clave: c.clave,
    encabezado: c.titulo,
    principal: i === principal,
    alinear: NUMERICOS.includes(c.tipo) ? 'fin' : undefined,
    render: ({ celdas }) => {
      const texto = formatearCelda(celdas[i], c);
      if (tabla.exclusion?.columna === i && excluida(celdas)) {
        return (
          <>
            <Insignia tono="peligro">{texto}</Insignia>
            <span className="solo-lectores"> (no suma en los totales)</span>
          </>
        );
      }
      return <span className={NUMERICOS.includes(c.tipo) || c.tipo === 'fecha' ? 'cifras' : undefined}>{texto}</span>;
    },
  }));
  const filas = tabla.filas.slice(0, FILAS_EN_PANTALLA).map((celdas, indice) => ({ indice, celdas }));
  const recortada = tabla.filas.length > FILAS_EN_PANTALLA;
  const conTotales = filas.length > 0 && tabla.columnas.some((c) => c.suma);

  return (
    <section aria-labelledby={`${id}-titulo`} className={estilos.seccion}>
      <h2 id={`${id}-titulo`} className={estilos.tituloSeccion}>{tabla.titulo}</h2>
      {tabla.nota && <p className={estilos.notaTabla}>{tabla.nota}</p>}
      {recortada && (
        <p className={estilos.notaTabla}>Se muestran las primeras {FILAS_EN_PANTALLA} de {tabla.filas.length} filas; el archivo exportado lleva todas, y los totales son los de todas.</p>
      )}
      <TablaAdaptable titulo={tabla.titulo} columnas={columnas} filas={filas} claveFila="indice"
        vacio={<EstadoVacio titulo={`No hay ${tabla.titulo.toLowerCase()} para mostrar`} nivelTitulo={3} />} />
      {conTotales && (
        <ul role="list" aria-label={`Totales de ${tabla.titulo}`} className={estilos.totales}>
          {tabla.columnas.map((c, i) => (c.suma ? (
            <li key={c.clave}>
              <span>{c.titulo}</span>
              <strong className="cifras">{formatearCelda(tabla.totales[i], c)}</strong>
            </li>
          ) : null))}
        </ul>
      )}
    </section>
  );
}
