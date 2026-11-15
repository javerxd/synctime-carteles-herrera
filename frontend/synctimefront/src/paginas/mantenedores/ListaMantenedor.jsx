import { useId, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Plus } from 'lucide-react';
import {
  Aviso, BarraFiltros, Boton, Campo, EstadoVacio, Paginacion, TablaAdaptable,
} from '../../componentes/ui';
import { vitrina } from '../../api/mantenedores';
import { useAvisos } from '../../avisos/Avisos';
import { useListadoEnDireccion } from '../../utilidades/useListadoEnDireccion';
import {
  MENSAJE_LOGO, parametrosMantenedor, puedeSerVisible, TEXTOS_ACTIVO, TEXTOS_VISIBLE, textoConflicto,
} from './reglasMantenedores';
import { TIPOS } from './tipos';
import estilosPanel from '../panel/Panel.module.css';
import estilos from './Mantenedores.module.css';

/**
 * El listado de un mantenedor: búsqueda, filtro de activos (o, en la vitrina, de visibles) y páginas en la
 * dirección, como los listados de la fase 8. Cada fila abre su formulario.
 */
export default function ListaMantenedor() {
  const { tipo: claveTipo } = useParams();
  const tipo = TIPOS[claveTipo];
  return <Listado key={claveTipo} tipo={tipo} />;
}

function Listado({ tipo }) {
  const refTitulo = useRef(null);
  const {
    filtros, consulta, cambiar, limpiar, texto, escribir, hayFiltros, errorVisible, actualizando,
  } = useListadoEnDireccion(tipo.filtros, {
    clave: `mantenedor-${tipo.clave}`,
    consultar: (f, senal) => tipo.api.listar(parametrosMantenedor(f, tipo.filtros), { senal }),
  });
  const datos = consulta.datos;
  const filas = datos?.elementos ?? [];
  const total = datos?.total ?? 0;
  const esVitrina = tipo.clave === 'vitrina';
  const columnas = esVitrina
    ? [...tipo.columnas, { clave: 'visible', encabezado: 'En la vitrina', render: (r) => <InterruptorVisible publicacion={r} alCambiar={consulta.recargar} /> }]
    : tipo.columnas;

  return (
    <section aria-labelledby="titulo-mantenedor" className={estilos.listado}>
      <div className={estilosPanel.cabecera}>
        <div>
          <h2 ref={refTitulo} tabIndex={-1} id="titulo-mantenedor" className={estilos.tituloSeccion}>{tipo.titulo}</h2>
          <p className={estilosPanel.bajada}>{tipo.bajada}</p>
        </div>
        <Boton como={Link} to={`/panel/mantenedores/${tipo.clave}/nuevo`} iconoInicio={Plus}>{tipo.nuevo}</Boton>
      </div>

      <BarraFiltros titulo={`Filtros de ${tipo.titulo.toLowerCase()}`} hayFiltros={hayFiltros} alLimpiar={limpiar}
        resumen={datos ? `${total} ${total === 1 ? 'registro' : 'registros'}` : ''}>
        <Campo etiqueta="Buscar" type="search" ayuda={tipo.buscar} value={texto} onChange={(e) => escribir(e.target.value)} />
        {esVitrina ? (
          <Campo etiqueta="Estado" como="select" value={filtros.visible} onChange={(e) => cambiar({ visible: e.target.value })}>
            {Object.entries(TEXTOS_VISIBLE).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
          </Campo>
        ) : (
          <Campo etiqueta="Estado" como="select" value={filtros.activo} onChange={(e) => cambiar({ activo: e.target.value })}>
            {Object.entries(TEXTOS_ACTIVO).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
          </Campo>
        )}
      </BarraFiltros>

      {errorVisible ? (
        <Aviso tono="peligro" titulo={`No pudimos cargar ${tipo.titulo.toLowerCase()}`}
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {errorVisible.message}
        </Aviso>
      ) : (
        <TablaAdaptable titulo={tipo.titulo} columnas={columnas} filas={filas} claveFila={tipo.idCampo} cargando={consulta.cargando}
          actualizando={actualizando}
          vacio={(
            <EstadoVacio nivelTitulo={3}
              titulo={hayFiltros ? 'Nada coincide con estos filtros' : `Aún no hay ${tipo.titulo.toLowerCase()}`}
              descripcion={hayFiltros ? 'Prueba con otra búsqueda o cambia el estado.' : `Usa «${tipo.nuevo}» para registrar el primero.`}
              accion={hayFiltros ? <Boton variante="secundario" onClick={limpiar}>Limpiar filtros</Boton> : null} />
          )} />
      )}
      <Paginacion pagina={filtros.pagina} totalPaginas={consulta.cargando ? 0 : datos?.totalPaginas ?? 0} etiqueta={`Paginación de ${tipo.titulo.toLowerCase()}`}
        alCambiar={(numero) => { cambiar({ pagina: numero }); refTitulo.current?.focus(); }} />
    </section>
  );
}

/**
 * Interruptor «visible» de una fila de la vitrina. Con logo y sin autorización queda bloqueado y lo explica
 * (aria-disabled, sigue enfocable para leer el motivo); la API respondería 409 logo_sin_autorizacion.
 */
function InterruptorVisible({ publicacion, alCambiar }) {
  const { anunciar } = useAvisos();
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState(null);
  const idMotivo = useId();
  const visible = Boolean(publicacion.visible);
  const bloqueado = !visible && !puedeSerVisible(publicacion);

  async function alternar() {
    if (bloqueado || enviando) return;
    setEnviando(true);
    setError(null);
    try {
      const version = publicacion.rowVersion ?? (await vitrina.obtener(publicacion.idCatalogo))?.rowVersion;
      await vitrina.cambiarVisible(publicacion.idCatalogo, !visible, version);
      anunciar({ tono: 'exito', titulo: visible ? `«${publicacion.titulo}» quedó oculta` : `«${publicacion.titulo}» ya se ve en la vitrina` });
      alCambiar();
    } catch (falla) {
      setError(falla?.codigo === 'version_obsoleta' ? 'Otra persona la cambió. Recargamos el listado.' : textoConflicto(falla));
      if (falla?.estado === 409) alCambiar();
    } finally {
      setEnviando(false);
    }
  }

  return (
    <span className={estilos.celdaInterruptor}>
      {/* El nombre empieza con el texto visible («Mostrar», WCAG 2.5.3); el estado lo dicen aria-checked y el texto de al lado. */}
      <span className={estilos.filaInterruptor}>
        <button type="button" role="switch" aria-checked={visible} aria-label={`Mostrar «${publicacion.titulo}» en la vitrina`}
          aria-disabled={bloqueado || enviando ? 'true' : undefined} aria-describedby={bloqueado ? idMotivo : undefined}
          onClick={alternar} className={estilos.interruptor}>
          <span className={estilos.riel} aria-hidden="true"><span className={estilos.perilla} /></span>
          <span className={estilos.textoInterruptor}>Mostrar</span>
        </button>
        <span className={estilos.estadoInterruptor} aria-hidden="true">{visible ? 'Visible' : 'Oculta'}</span>
      </span>
      {bloqueado && <span id={idMotivo} className={estilos.motivoFila}>{MENSAJE_LOGO}</span>}
      {error && <span role="alert" className={estilos.errorFila}>{error}</span>}
    </span>
  );
}
