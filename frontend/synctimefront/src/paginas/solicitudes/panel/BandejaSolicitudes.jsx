import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Paperclip, Plus } from 'lucide-react';
import {
  Aviso, BarraFiltros, Boton, Campo, EstadoSolicitud, EstadoVacio, Insignia, Paginacion, TablaAdaptable, TEXTOS_ESTADO_SOLICITUD,
} from '../../../componentes/ui';
import { solicitudes } from '../../../api/solicitudes';
import { formatearFechaHora } from '../../../utilidades/formato';
import { useConsulta } from '../../../utilidades/useConsulta';
import { useDebounce } from '../../../utilidades/useDebounce';
import { nombreCompleto, numeroSolicitud, ORIGENES_SOLICITUD } from '../reglasSolicitud';
import { telefonoVisible } from '../reglasSolicitud';
import {
  camposRechazados, filtrosDesde, ORDEN_PREDETERMINADO, parametrosApi, parametrosInvalidos, registradaPor,
} from './reglasBandeja';
import estilosPanel from '../../panel/Panel.module.css';
import estilos from '../Solicitudes.module.css';

/** /panel/solicitudes: bandeja de solicitudes en línea y presenciales, con filtros, búsqueda, orden y paginación. */
export default function BandejaSolicitudes() {
  const [parametros, setParametros] = useSearchParams();
  const filtros = filtrosDesde(parametros);
  const [texto, setTexto] = useState(filtros.q);
  const buscado = useDebounce(texto);
  const refTitulo = useRef(null);
  const peticion = parametrosApi(filtros);
  const consulta = useConsulta(`bandeja:${JSON.stringify(peticion)}`, () => solicitudes.listar(peticion));

  function cambiar(cambios) {
    const siguiente = { ...filtros, pagina: 1, ...cambios };
    const limpio = Object.fromEntries(Object.entries(siguiente)
      .filter(([clave, valor]) => valor !== '' && valor !== undefined && !(clave === 'pagina' && valor === 1) && !(clave === 'orden' && valor === ORDEN_PREDETERMINADO))
      .map(([clave, valor]) => [clave, String(valor)]));
    setParametros(limpio, { replace: true });
  }

  // La búsqueda sigue a la dirección: si q cambia por navegación (menú, atrás, limpiar), el campo se ajusta.
  const qActual = filtros.q;
  const [qVisto, setQVisto] = useState(qActual);
  if (qActual !== qVisto) {
    setQVisto(qActual);
    setTexto(qActual);
  }
  // Y se aplica solo cuando la persona deja de escribir (no por un cambio de la dirección).
  const refTecleo = useRef(false);
  const refCambiar = useRef(cambiar);
  useEffect(() => {
    refCambiar.current = cambiar;
  });
  useEffect(() => {
    if (!refTecleo.current) return;
    refTecleo.current = false;
    if (buscado !== qActual) refCambiar.current({ q: buscado });
  }, [buscado, qActual]);

  // Un parámetro inválido en la dirección (o rechazado por la API) se quita en silencio: no es un error del usuario.
  const malos = parametrosInvalidos(parametros);
  const rechazados = camposRechazados(consulta.error);
  const aQuitar = [...new Set([...malos, ...rechazados])].join(',');
  useEffect(() => {
    if (!aQuitar) return;
    setParametros((actuales) => {
      const siguiente = new URLSearchParams(actuales);
      aQuitar.split(',').forEach((c) => siguiente.delete(c));
      return siguiente;
    }, { replace: true });
  }, [aQuitar, setParametros]);

  const datos = consulta.datos;
  // Una página fuera de rango (por ejemplo, tras un filtro) lleva a la última que existe.
  const ultimaPagina = datos?.totalPaginas ?? 0;
  useEffect(() => {
    if (ultimaPagina > 0 && filtros.pagina > ultimaPagina) refCambiar.current({ pagina: ultimaPagina });
  }, [ultimaPagina, filtros.pagina]);

  const hayFiltros = Boolean(filtros.estado || filtros.origen || filtros.desde || filtros.hasta || filtros.q || filtros.orden !== ORDEN_PREDETERMINADO);
  const rangoInvalido = filtros.desde && filtros.hasta && filtros.hasta < filtros.desde;
  const filas = datos?.elementos ?? [];
  const total = datos?.total ?? 0;

  function limpiar() {
    setTexto('');
    setParametros({}, { replace: true });
  }

  const columnas = [
    {
      clave: 'numero', encabezado: 'N.º', principal: true,
      render: (s) => (
        <Link to={`/panel/solicitudes/${s.idSolicitud}`} className={estilos.enlaceFila} aria-label={`Solicitud ${numeroSolicitud(s)}`}>
          <span className="cifras">{numeroSolicitud(s)}</span>
        </Link>
      ),
    },
    { clave: 'fecha', encabezado: 'Fecha (Chile)', render: (s) => <span className="cifras">{formatearFechaHora(s.fechaChile)}</span> },
    {
      clave: 'cliente', encabezado: 'Cliente',
      render: (s) => (
        <span className={estilos.celdaLetrero}>
          <span>{nombreCompleto(s.cliente)}</span>
          {s.cliente?.telefono && <span className={estilos.detalleLetrero}>{telefonoVisible(s.cliente.telefono)}</span>}
        </span>
      ),
    },
    { clave: 'origen', encabezado: 'Origen', render: (s) => <Insignia tono="neutro">{ORIGENES_SOLICITUD[s.origen] ?? s.origen ?? '—'}</Insignia> },
    { clave: 'estado', encabezado: 'Estado', render: (s) => <EstadoSolicitud estado={s.estado} /> },
    { clave: 'items', encabezado: 'Letreros', alinear: 'fin', render: (s) => <span className="cifras">{s.cantidadItems ?? '—'}</span> },
    {
      clave: 'adjuntos', encabezado: 'Archivos', alinear: 'fin',
      render: (s) => (s.cantidadAdjuntos ? (
        <span className={estilos.clip}><Paperclip size={14} aria-hidden="true" /><span className="cifras">{s.cantidadAdjuntos}</span></span>
      ) : <span aria-label="Sin archivos">—</span>),
    },
    { clave: 'registradaPor', encabezado: 'Registrada por', render: registradaPor },
  ];

  return (
    <div className={estilosPanel.pagina}>
      <div className={estilosPanel.cabecera}>
        <div>
          <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>Solicitudes</h1>
          <p className={estilosPanel.bajada}>Las solicitudes en línea y las registradas en el local, con su comprobante.</p>
        </div>
        <Boton como={Link} to="/panel/solicitudes/nueva" iconoInicio={Plus}>Registrar solicitud presencial</Boton>
      </div>

      <BarraFiltros
        titulo="Filtros de solicitudes"
        hayFiltros={hayFiltros}
        alLimpiar={limpiar}
        resumen={datos ? `${total} ${total === 1 ? 'solicitud' : 'solicitudes'}` : ''}
      >
        <Campo etiqueta="Buscar" type="search" ayuda="Número, nombre, teléfono o RUT" value={texto}
          onChange={(e) => { refTecleo.current = true; setTexto(e.target.value); }} />
        <Campo etiqueta="Estado" como="select" value={filtros.estado} onChange={(e) => cambiar({ estado: e.target.value })}>
          <option value="">Todos</option>
          {Object.entries(TEXTOS_ESTADO_SOLICITUD).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
        </Campo>
        <Campo etiqueta="Origen" como="select" value={filtros.origen} onChange={(e) => cambiar({ origen: e.target.value })}>
          <option value="">Todos</option>
          {Object.entries(ORIGENES_SOLICITUD).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
        </Campo>
        <Campo etiqueta="Desde" type="date" value={filtros.desde} max={filtros.hasta || undefined}
          onChange={(e) => cambiar({ desde: e.target.value })} />
        <Campo etiqueta="Hasta" type="date" value={filtros.hasta} min={filtros.desde || undefined}
          error={rangoInvalido ? 'La fecha «Hasta» no puede ser anterior a «Desde».' : undefined}
          onChange={(e) => cambiar({ hasta: e.target.value })} />
        <Campo etiqueta="Orden" como="select" value={filtros.orden} onChange={(e) => cambiar({ orden: e.target.value })}>
          <option value="fecha_desc">Más recientes primero</option>
          <option value="fecha_asc">Más antiguas primero</option>
        </Campo>
      </BarraFiltros>

      {consulta.error && !rechazados.length ? (
        <Aviso tono="peligro" titulo="No pudimos cargar las solicitudes"
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {consulta.error.message}
        </Aviso>
      ) : (
        <TablaAdaptable
          titulo="Solicitudes"
          columnas={columnas}
          filas={filas}
          claveFila="idSolicitud"
          cargando={consulta.cargando}
          vacio={(
            <EstadoVacio
              titulo={hayFiltros ? 'Ninguna solicitud coincide con estos filtros' : 'Aún no hay solicitudes'}
              descripcion={hayFiltros
                ? 'Prueba con otro estado u otras fechas, o quita la búsqueda.'
                : 'Las solicitudes en línea llegan aquí y avisan por correo. También puedes registrar una presencial.'}
              accion={hayFiltros ? <Boton variante="secundario" onClick={limpiar}>Limpiar filtros</Boton> : null}
            />
          )}
        />
      )}
      <Paginacion pagina={filtros.pagina} totalPaginas={consulta.cargando ? 0 : datos?.totalPaginas ?? 0} etiqueta="Paginación de solicitudes"
        alCambiar={(numero) => { cambiar({ pagina: numero }); refTitulo.current?.focus(); }} />
    </div>
  );
}
