import { useRef } from 'react';
import { Link } from 'react-router-dom';
import {
  Aviso, BarraFiltros, Boton, Campo, EstadoCotizacion, EstadoVacio, Paginacion, TablaAdaptable, TEXTOS_ESTADO_COTIZACION,
} from '../../componentes/ui';
import { cotizaciones } from '../../api/cotizaciones';
import { formatearFechaHora, formatearPesos } from '../../utilidades/formato';
import { useListadoEnDireccion } from '../../utilidades/useListadoEnDireccion';
import { FiltroCliente } from '../compartidos/FiltroCliente';
import { nombreCompleto, numeroSolicitud, telefonoVisible } from '../solicitudes/reglasSolicitud';
import {
  FILTROS_COTIZACIONES, numeroCotizacion, parametrosCotizaciones, TEXTOS_ORDEN_COTIZACIONES,
} from './reglasCotizacion';
import estilosPanel from '../panel/Panel.module.css';
import estilosSolicitud from '../solicitudes/Solicitudes.module.css';
import estilos from './Cotizaciones.module.css';

const nombreDe = (persona) => (typeof persona === 'string' ? persona : persona?.nombre ? nombreCompleto(persona) : '—');

/** /panel/cotizaciones: las cotizaciones con filtros por estado, fechas y cliente, búsqueda, orden y páginas en la dirección. */
export default function ListaCotizaciones() {
  const refTitulo = useRef(null);
  const {
    filtros, consulta, cambiar, limpiar, texto, escribir, hayFiltros, errorVisible, actualizando,
  } = useListadoEnDireccion(FILTROS_COTIZACIONES, { clave: 'cotizaciones', consultar: (f, senal) => cotizaciones.listar(parametrosCotizaciones(f), { senal }) });
  const datos = consulta.datos;
  const filas = datos?.elementos ?? [];
  const total = datos?.total ?? 0;
  const rangoInvalido = filtros.desde && filtros.hasta && filtros.hasta < filtros.desde;

  const columnas = [
    {
      clave: 'numero', encabezado: 'N.º', principal: true,
      render: (c) => (
        <Link to={`/panel/cotizaciones/${c.idCotizacion}`} className={estilosSolicitud.enlaceFila} aria-label={`Cotización ${numeroCotizacion(c)}`}>
          <span className="cifras">{numeroCotizacion(c)}</span>
        </Link>
      ),
    },
    { clave: 'fecha', encabezado: 'Fecha (Chile)', render: (c) => <span className={`cifras ${estilos.sinCorte}`}>{formatearFechaHora(c.fechaChile)}</span> },
    {
      clave: 'solicitud', encabezado: 'Solicitud',
      render: (c) => (
        <Link to={`/panel/solicitudes/${c.idSolicitud}`} aria-label={`Solicitud ${c.numeroSolicitud ?? numeroSolicitud({ idSolicitud: c.idSolicitud })}`}>
          <span className="cifras">{c.numeroSolicitud ?? numeroSolicitud({ idSolicitud: c.idSolicitud })}</span>
        </Link>
      ),
    },
    {
      clave: 'cliente', encabezado: 'Cliente',
      render: (c) => (
        <span className={estilosSolicitud.celdaLetrero}>
          <span>{nombreDe(c.cliente)}</span>
          {c.cliente?.telefono && <span className={estilosSolicitud.detalleLetrero}>{telefonoVisible(c.cliente.telefono)}</span>}
        </span>
      ),
    },
    { clave: 'estado', encabezado: 'Estado', render: (c) => <EstadoCotizacion estado={c.estado} /> },
    { clave: 'total', encabezado: 'Total', alinear: 'fin', render: (c) => <strong className="cifras">{formatearPesos(c.totalRedondeado ?? c.total)}</strong> },
    { clave: 'elaboradaPor', encabezado: 'Elaborada por', render: (c) => nombreDe(c.elaboradaPor) },
  ];

  return (
    <div className={estilosPanel.pagina}>
      <div>
        <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>Cotizaciones</h1>
        <p className={estilosPanel.bajada}>Las emitidas a cada solicitud y su respuesta. Para cotizar, abre una solicitud «Solicitada».</p>
      </div>

      <BarraFiltros titulo="Filtros de cotizaciones" hayFiltros={hayFiltros} alLimpiar={limpiar}
        resumen={datos ? `${total} ${total === 1 ? 'cotización' : 'cotizaciones'}` : ''}>
        <Campo etiqueta="Buscar" type="search" ayuda="N.º, nombre, teléfono o RUT" value={texto}
          onChange={(e) => escribir(e.target.value)} />
        <Campo etiqueta="Estado" como="select" value={filtros.estado} onChange={(e) => cambiar({ estado: e.target.value })}>
          <option value="">Todos</option>
          {Object.entries(TEXTOS_ESTADO_COTIZACION).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
        </Campo>
        <Campo etiqueta="Desde" type="date" value={filtros.desde} max={filtros.hasta || undefined} onChange={(e) => cambiar({ desde: e.target.value })} />
        <Campo etiqueta="Hasta" type="date" value={filtros.hasta} min={filtros.desde || undefined}
          error={rangoInvalido ? 'La fecha «Hasta» no puede ser anterior a «Desde».' : undefined} onChange={(e) => cambiar({ hasta: e.target.value })} />
        <Campo etiqueta="Orden" como="select" value={filtros.orden} onChange={(e) => cambiar({ orden: e.target.value })}>
          {Object.entries(TEXTOS_ORDEN_COTIZACIONES).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
        </Campo>
        {filtros.idCliente && (
          <div className={estilosPanel.chips}><FiltroCliente idCliente={filtros.idCliente} alQuitar={() => cambiar({ idCliente: '' })} /></div>
        )}
      </BarraFiltros>

      {errorVisible ? (
        <Aviso tono="peligro" titulo="No pudimos cargar las cotizaciones"
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {errorVisible.message}
        </Aviso>
      ) : (
        <TablaAdaptable titulo="Cotizaciones" columnas={columnas} filas={filas} claveFila="idCotizacion" cargando={consulta.cargando}
          actualizando={actualizando}
          vacio={(
            <EstadoVacio
              titulo={hayFiltros ? 'Ninguna cotización coincide con estos filtros' : 'Aún no hay cotizaciones'}
              descripcion={hayFiltros
                ? 'Prueba con otro estado u otras fechas, o quita la búsqueda.'
                : 'Se emiten desde la ficha de una solicitud «Solicitada», con el botón «Cotizar».'}
              accion={hayFiltros
                ? <Boton variante="secundario" onClick={limpiar}>Limpiar filtros</Boton>
                : <Boton como={Link} to="/panel/solicitudes?estado=solicitado&orden=fecha_asc" variante="secundario">Ver solicitudes por cotizar</Boton>}
            />
          )} />
      )}
      <Paginacion pagina={filtros.pagina} totalPaginas={consulta.cargando ? 0 : datos?.totalPaginas ?? 0} etiqueta="Paginación de cotizaciones"
        alCambiar={(numero) => { cambiar({ pagina: numero }); refTitulo.current?.focus(); }} />
    </div>
  );
}
