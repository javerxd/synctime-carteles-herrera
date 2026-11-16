import { useEffect, useRef } from 'react';
import { NavLink } from 'react-router-dom';
import { CircleCheck, CircleSlash } from 'lucide-react';
import {
  Aviso, BarraFiltros, Boton, Campo, EstadoVacio, Insignia, Paginacion, TablaAdaptable,
} from '../../componentes/ui';
import { auditoria } from '../../api/auditoria';
import { empleados as apiEmpleados } from '../../api/pedidos';
import { formatearFechaHora } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { useListadoEnDireccion } from '../../utilidades/useListadoEnDireccion';
import {
  ACCIONES, detalleLegible, ENTIDADES, FILTROS_AUDITORIA, FILTROS_CONSENTIMIENTOS, nombreUsuario, parametrosDe, textoAccion, textoEntidad,
  TIPOS_CONSENTIMIENTO,
} from './reglasAuditoria';
import estilosPanel from '../panel/Panel.module.css';
import estilos from './Auditoria.module.css';

const parametrosAuditoria = parametrosDe(FILTROS_AUDITORIA);
const parametrosConsentimientos = parametrosDe(FILTROS_CONSENTIMIENTOS);

/** El título, la explicación y las dos vistas (bitácora y consentimientos) como pestañas de navegación. */
function Cabecera({ refTitulo, vista }) {
  useEffect(() => {
    document.title = `Auditoría: ${vista} · SyncTime`;
  }, [vista]);
  return (
    <>
      <div>
        <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>Auditoría<span className="solo-lectores">: {vista}</span></h1>
        <p className={estilosPanel.bajada}>
          Quién creó, cambió o eliminó cada registro y cuándo. Es de solo lectura: nadie puede editarla ni borrarla.
        </p>
      </div>
      <nav aria-label="Vistas de la auditoría" className={estilos.vistas}>
        <NavLink to="/panel/auditoria" end className={estilos.vista}>Bitácora</NavLink>
        <NavLink to="/panel/auditoria/consentimientos" className={estilos.vista}>Consentimientos</NavLink>
      </nav>
    </>
  );
}

/** El detalle de un registro: los campos cambiados (antes → después solo si vienen) y los demás datos. */
function Detalle({ registro }) {
  const { cambios, otros } = detalleLegible(registro.detalle);
  const datos = registro.ip ? [...otros, { etiqueta: 'IP', valor: registro.ip }] : otros;
  if (!cambios.length && !datos.length) return <span className={estilos.tenue}>—</span>;
  return (
    <details className={estilos.detalle}>
      <summary>Ver detalle<span className="solo-lectores"> del registro {registro.idAuditoria}</span></summary>
      <dl className={estilos.lista}>
        {cambios.map((c, i) => (
          <div key={`c-${i}-${c.campo}`} className={estilos.fila}>
            <dt>{c.etiqueta}</dt>
            <dd>
              {c.conValores ? (
                <>
                  <span className={estilos.valor}>{c.antes ?? '—'}</span>
                  <span aria-hidden="true"> → </span>
                  <span className="solo-lectores"> pasó a </span>
                  <span className={estilos.valor}>{c.despues ?? '—'}</span>
                </>
              ) : <span className={estilos.tenue}>cambió</span>}
            </dd>
          </div>
        ))}
        {datos.map((o, i) => (
          <div key={`o-${i}-${o.etiqueta}`} className={estilos.fila}>
            <dt>{o.etiqueta}</dt>
            <dd className={estilos.valor}>{o.valor}</dd>
          </div>
        ))}
      </dl>
    </details>
  );
}

function FiltrosFecha({ filtros, cambiar }) {
  const rangoInvalido = filtros.desde && filtros.hasta && filtros.hasta < filtros.desde;
  return (
    <>
      <Campo etiqueta="Desde" type="date" value={filtros.desde} max={filtros.hasta || undefined} onChange={(e) => cambiar({ desde: e.target.value })} />
      <Campo etiqueta="Hasta" type="date" value={filtros.hasta} min={filtros.desde || undefined}
        error={rangoInvalido ? 'La fecha «Hasta» no puede ser anterior a «Desde».' : undefined} onChange={(e) => cambiar({ hasta: e.target.value })} />
    </>
  );
}

/**
 * /panel/auditoria (gestión, fase 11): la bitácora inmutable, del más reciente al más antiguo, con filtros por fecha,
 * usuario, entidad, acción e identificador, y páginas en la dirección. Sin exportar (llega en la fase 13).
 */
export default function Auditoria() {
  const refTitulo = useRef(null);
  const personal = useConsulta('empleados-activos', apiEmpleados.listar);
  const {
    filtros, consulta, cambiar, limpiar, texto, escribir, hayFiltros, errorVisible, actualizando,
  } = useListadoEnDireccion(FILTROS_AUDITORIA, { clave: 'auditoria', consultar: (f, senal) => auditoria.listar(parametrosAuditoria(f), { senal }) });
  const datos = consulta.datos;
  const total = datos?.total ?? 0;

  const columnas = [
    { clave: 'fecha', encabezado: 'Fecha (Chile)', principal: true, render: (r) => <span className={`cifras ${estilos.sinCorte}`}>{formatearFechaHora(r.fecha)}</span> },
    {
      clave: 'usuario', encabezado: 'Quién',
      render: (r) => (
        <span className={estilos.celda}>
          <span>{nombreUsuario(r.usuario)}</span>
          {r.rol && r.rol !== 'sistema' && <span className={estilos.tenue}>{r.rol}</span>}
        </span>
      ),
    },
    { clave: 'accion', encabezado: 'Acción', render: textoAccion },
    { clave: 'entidad', encabezado: 'Registro', render: (r) => <span className="cifras">{textoEntidad(r)}</span> },
    { clave: 'detalle', encabezado: 'Detalle', render: (r) => <Detalle registro={r} /> },
  ];

  return (
    <div className={estilosPanel.pagina}>
      <Cabecera refTitulo={refTitulo} vista="bitácora" />

      <BarraFiltros titulo="Filtros de la bitácora" hayFiltros={hayFiltros} alLimpiar={limpiar}
        resumen={datos ? `${total} ${total === 1 ? 'registro' : 'registros'}` : ''}>
        <FiltrosFecha filtros={filtros} cambiar={cambiar} />
        <Campo etiqueta="Usuario" como="select" value={filtros.idEmpleado} onChange={(e) => cambiar({ idEmpleado: e.target.value })}>
          <option value="">Todos</option>
          {(personal.datos ?? []).map((p) => (
            <option key={p.idEmpleado} value={String(p.idEmpleado)}>{[p.nombre, p.apellido].filter(Boolean).join(' ')}</option>
          ))}
        </Campo>
        <Campo etiqueta="Entidad" como="select" value={filtros.entidad} onChange={(e) => cambiar({ entidad: e.target.value })}>
          <option value="">Todas</option>
          {Object.entries(ENTIDADES).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
        </Campo>
        <Campo etiqueta="Acción" como="select" value={filtros.accion} onChange={(e) => cambiar({ accion: e.target.value })}>
          <option value="">Todas</option>
          {Object.entries(ACCIONES).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
        </Campo>
        <Campo etiqueta="N.º del registro" type="search" inputMode="numeric" maxLength={40} ayuda="Por ejemplo, 123" value={texto}
          onChange={(e) => escribir(e.target.value)} />
      </BarraFiltros>

      {errorVisible ? (
        <Aviso tono="peligro" titulo="No pudimos cargar la bitácora"
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {errorVisible.message}
        </Aviso>
      ) : (
        <TablaAdaptable titulo="Bitácora de auditoría" columnas={columnas} filas={datos?.elementos ?? []} claveFila="idAuditoria"
          cargando={consulta.cargando} actualizando={actualizando}
          vacio={(
            <EstadoVacio
              titulo={hayFiltros ? 'Ningún registro coincide con estos filtros' : 'Aún no hay registros en la bitácora'}
              descripcion={hayFiltros ? 'Prueba con otras fechas u otra acción.' : 'Cada cambio en solicitudes, pedidos, abonos, catálogo, clientes y personal quedará aquí.'}
              accion={hayFiltros ? <Boton variante="secundario" onClick={limpiar}>Limpiar filtros</Boton> : null} />
          )} />
      )}
      <Paginacion pagina={filtros.pagina} totalPaginas={consulta.cargando ? 0 : datos?.totalPaginas ?? 0} etiqueta="Paginación de la bitácora"
        alCambiar={(numero) => { cambiar({ pagina: numero }); refTitulo.current?.focus(); }} />
    </div>
  );
}

const titularDe = (t) => {
  if (!t) return '—';
  if (t.nombre) return t.nombre;
  if (t.tipo === 'visitante') return 'Visitante sin cuenta';
  return nombreUsuario(t);
};

/** /panel/auditoria/consentimientos (gestión, RFU28): quién aceptó o retiró los términos, la privacidad y el marketing, qué versión y cuándo. */
export function ConsentimientosAuditoria() {
  const refTitulo = useRef(null);
  const {
    filtros, consulta, cambiar, limpiar, hayFiltros, errorVisible, actualizando,
  } = useListadoEnDireccion(FILTROS_CONSENTIMIENTOS, {
    clave: 'auditoria-consentimientos', consultar: (f, senal) => auditoria.consentimientos(parametrosConsentimientos(f), { senal }),
  });
  const datos = consulta.datos;
  const total = datos?.total ?? 0;

  const columnas = [
    { clave: 'fecha', encabezado: 'Fecha (Chile)', principal: true, render: (c) => <span className={`cifras ${estilos.sinCorte}`}>{formatearFechaHora(c.fecha)}</span> },
    { clave: 'titular', encabezado: 'Titular', render: (c) => titularDe(c.titular) },
    { clave: 'tipo', encabezado: 'Documento', render: (c) => TIPOS_CONSENTIMIENTO[c.tipo] ?? c.tipo },
    { clave: 'version', encabezado: 'Versión', render: (c) => <span className="cifras">{c.version || '—'}</span> },
    {
      clave: 'acepta', encabezado: 'Decisión',
      render: (c) => (c.acepta
        ? <Insignia tono="exito" icono={CircleCheck}>Aceptó</Insignia>
        : <Insignia tono="neutro" icono={CircleSlash}>Retiró</Insignia>),
    },
  ];

  return (
    <div className={estilosPanel.pagina}>
      <Cabecera refTitulo={refTitulo} vista="consentimientos" />

      <BarraFiltros titulo="Filtros de consentimientos" hayFiltros={hayFiltros} alLimpiar={limpiar}
        resumen={datos ? `${total} ${total === 1 ? 'consentimiento' : 'consentimientos'}` : ''}>
        <Campo etiqueta="Tipo" como="select" value={filtros.tipo} onChange={(e) => cambiar({ tipo: e.target.value })}>
          <option value="">Todos</option>
          {Object.entries(TIPOS_CONSENTIMIENTO).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
        </Campo>
        <FiltrosFecha filtros={filtros} cambiar={cambiar} />
      </BarraFiltros>

      {errorVisible ? (
        <Aviso tono="peligro" titulo="No pudimos cargar los consentimientos"
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {errorVisible.message}
        </Aviso>
      ) : (
        <TablaAdaptable titulo="Consentimientos" columnas={columnas} filas={datos?.elementos ?? []}
          claveFila={(c) => c.idConsentimiento ?? `${c.titular?.tipo}-${c.titular?.id}-${c.tipo}-${c.version}-${c.fecha}-${c.acepta}`} cargando={consulta.cargando} actualizando={actualizando}
          vacio={(
            <EstadoVacio
              titulo={hayFiltros ? 'Ningún consentimiento coincide con estos filtros' : 'Aún no hay consentimientos registrados'}
              descripcion={hayFiltros ? 'Prueba con otro tipo u otras fechas.' : 'Se registran al crear una cuenta y al cambiar las preferencias de correo.'}
              accion={hayFiltros ? <Boton variante="secundario" onClick={limpiar}>Limpiar filtros</Boton> : null} />
          )} />
      )}
      <Paginacion pagina={filtros.pagina} totalPaginas={consulta.cargando ? 0 : datos?.totalPaginas ?? 0} etiqueta="Paginación de consentimientos"
        alCambiar={(numero) => { cambiar({ pagina: numero }); refTitulo.current?.focus(); }} />
    </div>
  );
}
