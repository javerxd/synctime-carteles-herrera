import { useId, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { CheckCheck, CircleAlert, Clock3, MailCheck, Send } from 'lucide-react';
import {
  Aviso, BarraFiltros, Boton, Campo, Dialogo, EstadoVacio, Insignia, Paginacion, TablaAdaptable,
} from '../../componentes/ui';
import { correos } from '../../api/correos';
import { useAvisos } from '../../avisos/Avisos';
import { formatearFechaHora } from '../../utilidades/formato';
import { useListadoEnDireccion } from '../../utilidades/useListadoEnDireccion';
import { mensajeFallaGestion } from '../compartidos/reautenticacion';
import {
  destinoDelReenvio, enlaceSeguro, FILTROS_CORREOS, motivoNoReenviable, parametrosCorreos, TEXTOS_REVISADO, TEXTOS_TIPO_CORREO,
  textoReferencia, textoTipo,
} from './reglasCorreos';
import estilosPanel from '../panel/Panel.module.css';
import estilos from './Correos.module.css';

function EstadoCorreo({ correo }) {
  if (correo.reenviadoComo) return <Insignia tono="info" icono={Send}>Reenviado</Insignia>;
  if (correo.revisado) return <Insignia tono="neutro" icono={CheckCheck}>Revisado</Insignia>;
  return <Insignia tono="aviso" icono={Clock3}>Sin revisar</Insignia>;
}

function Referencia({ correo }) {
  const texto = textoReferencia(correo.referencia);
  const enlace = enlaceSeguro(correo.enlace);
  return enlace ? <Link to={enlace}><span className="cifras">{texto}</span></Link> : <span className="cifras">{texto}</span>;
}

/** Reenviar y marcar como revisado. Si no se puede reenviar, el botón lo explica (aria-disabled, sigue enfocable). */
function Acciones({ correo, ocupado, alReenviar, alRevisar }) {
  const idMotivo = useId();
  const motivo = motivoNoReenviable(correo);
  return (
    <div className={estilos.acciones}>
      <Boton variante="secundario" tamano="compacto" iconoInicio={Send} onClick={motivo ? undefined : alReenviar}
        aria-disabled={motivo ? 'true' : undefined} aria-describedby={motivo ? idMotivo : undefined}
        className={motivo ? estilos.inactivo : undefined} aria-label={`Reenviar el correo #${correo.idCorreo}`}>
        Reenviar
      </Boton>
      {!correo.revisado && (
        <Boton variante="fantasma" tamano="compacto" iconoInicio={CheckCheck} onClick={alRevisar} cargando={ocupado} textoCargando="Marcando…"
          aria-label={ocupado ? undefined : `Marcar como revisado el correo #${correo.idCorreo}`}>
          Marcar como revisado
        </Boton>
      )}
      {motivo && <p id={idMotivo} className={estilos.motivo}>{motivo}</p>}
    </div>
  );
}

const MENSAJES_REENVIO = {
  ya_reenviado: 'Este correo ya se reenvió (quizás desde otra pestaña). Actualizamos el listado.',
  sin_contenido: 'Este correo ya no se puede reenviar: su contenido se borró por el plazo de conservación.',
  sin_destinos: 'No hay destinos activos que reciban este aviso. Revisa «Avisos del taller» en tu perfil.',
  cliente_no_recibe: 'El cliente ya no recibe estos avisos: los desactivó, dio de baja su cuenta o cambió su correo.',
};

/** Confirmación del reenvío: dice a quién irá antes de hacerlo. El foco empieza en «Cancelar». */
function DialogoReenviar({ correo, alCerrar, alReenviado, alCambio }) {
  const abierto = Boolean(correo);
  const [estado, setEstado] = useState({ enviando: false, error: null });
  const [abiertoAntes, setAbiertoAntes] = useState(abierto);
  const refCancelar = useRef(null);
  if (abierto !== abiertoAntes) {
    setAbiertoAntes(abierto);
    if (abierto) setEstado({ enviando: false, error: null });
  }

  async function reenviar() {
    if (estado.enviando) return;
    setEstado({ enviando: true, error: null });
    try {
      await correos.reenviar(correo.idCorreo);
      alReenviado();
    } catch (falla) {
      const propio = falla?.estado === 409 ? MENSAJES_REENVIO[falla.codigo] : falla?.estado === 404 ? 'Este correo ya no existe.' : null;
      setEstado({ enviando: false, error: propio ?? mensajeFallaGestion(falla, 'No pudimos reenviar el correo.') });
      if (propio) alCambio();
    }
  }

  return (
    <Dialogo abierto={abierto} alCerrar={() => !estado.enviando && alCerrar()} titulo="Reenviar este correo" enfoqueInicial={refCancelar} ancho="32rem"
      descripcion={correo ? `${textoTipo(correo)} · ${textoReferencia(correo.referencia)}` : ''}
      pie={(
        <>
          <Boton ref={refCancelar} variante="secundario" onClick={alCerrar} disabled={estado.enviando}>Cancelar</Boton>
          {!estado.error || !Object.values(MENSAJES_REENVIO).includes(estado.error) ? (
            <Boton iconoInicio={Send} onClick={reenviar} cargando={estado.enviando} textoCargando="Reenviando…">Reenviar correo</Boton>
          ) : null}
        </>
      )}>
      {correo && (
        <div className={estilos.cuerpoDialogo}>
          <p className={estilos.destino}><MailCheck size={18} strokeWidth={1.75} aria-hidden="true" />{destinoDelReenvio(correo)}</p>
          <p className={estilos.nota}>Vuelve a la cola de salida con el mismo contenido y queda registrado que lo pediste tú.</p>
          {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
        </div>
      )}
    </Dialogo>
  );
}

/**
 * /panel/correos-no-enviados (gestión, fase 11): los correos que quedaron `fallido` después de todos los intentos,
 * avisos del taller y correos a clientes. Muestra a qué se refieren, el destino enmascarado y el motivo; desde aquí
 * se reenvían (con una confirmación que dice a quién irán) o se marcan como revisados.
 */
export default function CorreosNoEnviados() {
  const refTitulo = useRef(null);
  const { anunciar } = useAvisos();
  const [reenviando, setReenviando] = useState(null);
  const [marcando, setMarcando] = useState(() => new Set());
  const {
    filtros, consulta, cambiar, limpiar, hayFiltros, errorVisible, actualizando,
  } = useListadoEnDireccion(FILTROS_CORREOS, { clave: 'correos-no-enviados', consultar: (f, senal) => correos.noEnviados(parametrosCorreos(f), { senal }) });
  const datos = consulta.datos;
  const total = datos?.total ?? 0;
  const rangoInvalido = filtros.desde && filtros.hasta && filtros.hasta < filtros.desde;

  const cambiarMarcando = (id, activo) => setMarcando((actual) => {
    const siguiente = new Set(actual);
    if (activo) siguiente.add(id);
    else siguiente.delete(id);
    return siguiente;
  });

  async function marcarRevisado(correo) {
    if (marcando.has(correo.idCorreo)) return;
    cambiarMarcando(correo.idCorreo, true);
    try {
      await correos.marcarRevisado(correo.idCorreo);
      anunciar({ tono: 'exito', titulo: `Correo #${correo.idCorreo} marcado como revisado` });
      // El botón desaparece con la recarga: el foco va al título en vez de perderse.
      refTitulo.current?.focus();
      consulta.recargar();
    } catch (falla) {
      anunciar({ tono: 'peligro', titulo: mensajeFallaGestion(falla, 'No pudimos marcarlo como revisado.') });
      if (falla?.estado === 404 || falla?.estado === 409) consulta.recargar();
    } finally {
      cambiarMarcando(correo.idCorreo, false);
    }
  }

  const columnas = [
    {
      clave: 'tipo', encabezado: 'Correo', principal: true,
      render: (c) => (
        <span className={estilos.celdaTipo}>
          <span>{textoTipo(c)}</span>
          <span className={`cifras ${estilos.detalle}`}>#{c.idCorreo}</span>
        </span>
      ),
    },
    { clave: 'referencia', encabezado: 'Se refiere a', render: (c) => <Referencia correo={c} /> },
    { clave: 'destino', encabezado: 'Destino', render: (c) => <span className={estilos.correo}>{c.destinoEnmascarado || '—'}</span> },
    {
      clave: 'fallo', encabezado: 'Falló', render: (c) => (
        <span className={estilos.celdaTipo}>
          <span className={`cifras ${estilos.sinCorte}`}>{formatearFechaHora(c.fechaFallo ?? c.fechaCreacion)}</span>
          <span className={estilos.detalle}><span className="cifras">{c.intentos}</span> {c.intentos === 1 ? 'intento' : 'intentos'}</span>
        </span>
      ),
    },
    { clave: 'motivo', encabezado: 'Motivo', render: (c) => c.motivo || '—' },
    { clave: 'estado', encabezado: 'Estado', render: (c) => <EstadoCorreo correo={c} /> },
    {
      clave: 'acciones', encabezado: 'Acciones',
      render: (c) => <Acciones correo={c} ocupado={marcando.has(c.idCorreo)} alReenviar={() => setReenviando(c)} alRevisar={() => marcarRevisado(c)} />,
    },
  ];

  return (
    <div className={estilosPanel.pagina}>
      <div>
        <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>Correos no enviados</h1>
        <p className={estilosPanel.bajada}>
          Los que no salieron después de todos los intentos: avisos del taller y correos a clientes. Nunca se muestra el contenido ni la dirección completa.
        </p>
      </div>

      <BarraFiltros titulo="Filtros de correos no enviados" hayFiltros={hayFiltros} alLimpiar={limpiar}
        resumen={datos ? `${total} ${total === 1 ? 'correo' : 'correos'}` : ''}>
        <Campo etiqueta="Tipo de correo" como="select" value={filtros.tipo} onChange={(e) => cambiar({ tipo: e.target.value })}>
          <option value="">Todos</option>
          {Object.entries(TEXTOS_TIPO_CORREO).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
        </Campo>
        <Campo etiqueta="Revisión" como="select" value={filtros.revisado} onChange={(e) => cambiar({ revisado: e.target.value })}>
          <option value="">Todos</option>
          {Object.entries(TEXTOS_REVISADO).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
        </Campo>
        <Campo etiqueta="Desde" type="date" value={filtros.desde} max={filtros.hasta || undefined} onChange={(e) => cambiar({ desde: e.target.value })} />
        <Campo etiqueta="Hasta" type="date" value={filtros.hasta} min={filtros.desde || undefined}
          error={rangoInvalido ? 'La fecha «Hasta» no puede ser anterior a «Desde».' : undefined} onChange={(e) => cambiar({ hasta: e.target.value })} />
      </BarraFiltros>

      {errorVisible ? (
        <Aviso tono="peligro" titulo="No pudimos cargar los correos no enviados"
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {errorVisible.message}
        </Aviso>
      ) : (
        <TablaAdaptable titulo="Correos no enviados" columnas={columnas} filas={datos?.elementos ?? []} claveFila="idCorreo"
          cargando={consulta.cargando} actualizando={actualizando}
          vacio={(
            <EstadoVacio icono={hayFiltros ? CircleAlert : MailCheck}
              titulo={hayFiltros ? 'Ningún correo coincide con estos filtros' : 'No hay correos sin enviar'}
              descripcion={hayFiltros ? 'Prueba con otro tipo u otras fechas.' : 'Todos los correos salieron bien. Si alguno falla después de todos los intentos, aparecerá aquí.'}
              accion={hayFiltros ? <Boton variante="secundario" onClick={limpiar}>Limpiar filtros</Boton> : null} />
          )} />
      )}
      <Paginacion pagina={filtros.pagina} totalPaginas={consulta.cargando ? 0 : datos?.totalPaginas ?? 0} etiqueta="Paginación de correos no enviados"
        alCambiar={(numero) => { cambiar({ pagina: numero }); refTitulo.current?.focus(); }} />

      <DialogoReenviar correo={reenviando} alCerrar={() => setReenviando(null)} alCambio={consulta.recargar}
        alReenviado={() => {
          anunciar({ tono: 'exito', titulo: 'Correo reenviado', texto: 'Volvió a la cola de salida. Si falla otra vez, aparecerá de nuevo aquí.' });
          setReenviando(null);
          consulta.recargar();
          // La fila puede salir del filtro («Sin revisar»): si el foco quedó en el cuerpo, vuelve al título.
          setTimeout(() => {
            if (!document.activeElement || document.activeElement === document.body) refTitulo.current?.focus();
          }, 300);
        }} />
    </div>
  );
}
