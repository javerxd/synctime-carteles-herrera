import { Component, useId, useState } from 'react';
import { BarChart3, RotateCcw, Table2 } from 'lucide-react';
import { Aviso, Boton, Esqueleto, EstadoVacio } from '../../componentes/ui';
import estilos from './Indicadores.module.css';

/** Conmutador de dos o más opciones con aria-pressed (no son pestañas: no cambian de página). */
export function Conmutador({ etiqueta, opciones, valor, alCambiar }) {
  return (
    <div role="group" aria-label={etiqueta} className={estilos.conmutador}>
      {opciones.map(({ valor: opcion, texto, icono: Icono }) => (
        <button key={opcion} type="button" aria-pressed={valor === opcion} onClick={() => alCambiar(opcion)} className={estilos.opcionConmutador}>
          {Icono && <Icono size={16} strokeWidth={1.75} aria-hidden="true" />}
          {texto}
        </button>
      ))}
    </div>
  );
}

/**
 * Si un bloque falla al dibujarse (por ejemplo, una respuesta con otra forma), solo ese bloque muestra el aviso: los
 * demás siguen en pie (RNFP23). «Reintentar» vuelve a pedir el bloque y a dibujarlo.
 */
class LimiteBloque extends Component {
  state = { fallo: false };

  static getDerivedStateFromError() {
    return { fallo: true };
  }

  render() {
    if (!this.state.fallo) return this.props.children;
    const reintentar = () => {
      this.setState({ fallo: false });
      this.props.alReintentar?.();
    };
    return (
      <>
        <h2 id={this.props.idTitulo} className={estilos.tituloBloque}>{this.props.titulo}</h2>
        <Aviso tono="peligro" titulo="No pudimos mostrar este bloque"
          accion={<Boton variante="secundario" tamano="compacto" iconoInicio={RotateCcw} onClick={reintentar}>Reintentar</Boton>}>
          Los datos llegaron con una forma inesperada. Los demás bloques siguen disponibles.
        </Aviso>
      </>
    );
  }
}

const VISTAS = [{ valor: 'grafico', texto: 'Gráfico', icono: BarChart3 }, { valor: 'tabla', texto: 'Tabla de datos', icono: Table2 }];

/**
 * Un bloque del dashboard (una región con nombre): título, frase que resume la tendencia y, si `conTabla`, el
 * conmutador «Gráfico / Tabla de datos». Cada bloque tiene sus propios estados (RNFP23): esqueleto mientras
 * carga, vacío, y error con «Reintentar» que vuelve a pedir solo este bloque. La carga se anuncia una sola vez, a
 * nivel de página (Indicadores.jsx), para no encadenar seis anuncios.
 * `children(vista, datos)` dibuja el contenido; `narrativa(datos)` y `vacio(datos)` se calculan con los datos.
 */
export function BloqueIndicador({
  titulo, descripcion, estado, alReintentar, narrativa, vacio, textoVacio = 'Sin datos en este período', conTabla = false,
  controles, altoEsqueleto = '14rem', className, children,
}) {
  const id = useId();
  const [vista, setVista] = useState('grafico');
  const { datos, error, cargando, actualizando } = estado;
  return (
    <section aria-labelledby={`${id}-titulo`} aria-busy={cargando || actualizando ? 'true' : undefined}
      className={[estilos.bloque, actualizando && estilos.actualizando, className].filter(Boolean).join(' ')}>
      <LimiteBloque alReintentar={alReintentar} titulo={titulo} idTitulo={`${id}-titulo`}>
        <ContenidoBloque {...{ id, titulo, descripcion, datos, error, cargando, alReintentar, narrativa, vacio, textoVacio, conTabla, controles, altoEsqueleto, vista, setVista, children }} />
      </LimiteBloque>
    </section>
  );
}

function ContenidoBloque({
  id, titulo, descripcion, datos, error, cargando, alReintentar, narrativa, vacio, textoVacio, conTabla, controles, altoEsqueleto, vista, setVista, children,
}) {
  const sinDatos = datos !== undefined && vacio?.(datos);
  const listo = datos !== undefined && !sinDatos;

  return (
    <>
      <header className={estilos.cabeceraBloque}>
        <div className={estilos.textosBloque}>
          <h2 id={`${id}-titulo`} className={estilos.tituloBloque}>{titulo}</h2>
          {listo && narrativa && <p className={estilos.narrativa}>{narrativa(datos)}</p>}
          {descripcion && <p className={estilos.descripcionBloque}>{descripcion}</p>}
        </div>
        {listo && (controles || conTabla) && (
          <div className={estilos.controlesBloque}>
            {controles}
            {conTabla && <Conmutador etiqueta={`Ver ${titulo.toLowerCase()} como`} opciones={VISTAS} valor={vista} alCambiar={setVista} />}
          </div>
        )}
      </header>

      {error && (
        <Aviso tono="peligro" titulo="No pudimos cargar este bloque"
          accion={<Boton variante="secundario" tamano="compacto" iconoInicio={RotateCcw} onClick={alReintentar}>Reintentar</Boton>}>
          {datos !== undefined ? `Se muestran los datos anteriores${datos?.periodo?.etiqueta ? `, de ${datos.periodo.etiqueta}` : ''}. ` : ''}{error.message || 'Revisa tu conexión e inténtalo de nuevo.'}
        </Aviso>
      )}
      {cargando && !error && <Esqueleto forma="bloque" alto={altoEsqueleto} style={{ borderRadius: 'var(--radio-md)' }} />}
      {sinDatos && <EstadoVacio titulo={textoVacio} nivelTitulo={3} className={estilos.vacioBloque} />}
      {listo && children(vista, datos)}
    </>
  );
}
