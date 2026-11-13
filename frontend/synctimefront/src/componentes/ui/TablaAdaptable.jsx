import { Esqueleto } from './Esqueleto';
import { EstadoVacio } from './EstadoVacio';
import estilos from './TablaAdaptable.module.css';

/**
 * Tabla de datos que en pantallas angostas pasa a fichas apiladas (sin desplazamiento horizontal):
 * cada celda muestra su encabezado gracias a `data-etiqueta`. Los roles explícitos conservan la
 * semántica de tabla cuando el CSS cambia su forma.
 * columnas: [{ clave, encabezado, alinear: 'inicio' | 'fin', render: (fila) => nodo, principal }]
 * claveFila: nombre de la propiedad única de cada fila, o una función (fila) => clave.
 */
export function TablaAdaptable({
  titulo,
  columnas,
  filas,
  claveFila = 'id',
  cargando = false,
  filasEsqueleto = 4,
  vacio,
  tituloVisible = false,
  className,
}) {
  const clave = typeof claveFila === 'function' ? claveFila : (fila) => fila[claveFila];

  // La región de estado existe siempre y solo cambia su texto: así el lector de pantalla la anuncia.
  const anuncio = <p role="status" className="solo-lectores">{cargando ? `Cargando ${titulo.toLowerCase()}…` : ''}</p>;

  if (cargando) {
    return (
      <div className={className}>
        {anuncio}
        <div className={estilos.contenedor} aria-busy="true">
          <div className={estilos.esqueleto} aria-hidden="true">
            {Array.from({ length: filasEsqueleto }, (_, i) => (
              <Esqueleto key={i} alto="2.75rem" forma="bloque" style={{ borderRadius: 'var(--radio-md)' }} />
            ))}
          </div>
        </div>
      </div>
    );
  }

  if (!filas?.length) {
    return (
      <div className={className}>
        {anuncio}
        {vacio ?? <EstadoVacio titulo={`No hay ${titulo.toLowerCase()} para mostrar`} />}
      </div>
    );
  }

  return (
    <div className={className}>
      {anuncio}
      <div className={estilos.contenedor}>
        <table role="table" className={estilos.tabla}>
          <caption className={tituloVisible ? estilos.titulo : 'solo-lectores'}>{titulo}</caption>
          <thead role="rowgroup">
            <tr role="row">
              {columnas.map((c) => (
                <th key={c.clave} role="columnheader" scope="col" data-alinear={c.alinear}>{c.encabezado}</th>
              ))}
            </tr>
          </thead>
          <tbody role="rowgroup">
            {filas.map((fila) => (
              <tr key={clave(fila)} role="row">
                {columnas.map((c) => {
                  const contenido = c.render ? c.render(fila) : fila[c.clave];
                  // La columna principal (el cliente, el trabajo) es el encabezado de su fila.
                  return c.principal ? (
                    <th key={c.clave} role="rowheader" scope="row">{contenido}</th>
                  ) : (
                    <td key={c.clave} role="cell" data-etiqueta={c.encabezado} data-alinear={c.alinear}>{contenido}</td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
