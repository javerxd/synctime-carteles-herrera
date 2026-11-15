import { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Ban, Pencil, RotateCcw, Save, Trash2 } from 'lucide-react';
import {
  Adjuntos, Aviso, Boton, Campo, Dialogo, Enlace, Esqueleto, EstadoSolicitud, Insignia, ListaAdjuntos, Tarjeta, TEXTOS_ESTADO_SOLICITUD,
} from '../../../componentes/ui';
import { publico } from '../../../api/publico';
import { adjuntos as apiAdjuntos, solicitudes } from '../../../api/solicitudes';
import { useAvisos } from '../../../avisos/Avisos';
import { unirClases } from '../../../componentes/ui/clases';
import { formatearFechaHora } from '../../../utilidades/formato';
import { useConsulta } from '../../../utilidades/useConsulta';
import { SeccionCotizacion } from '../../cotizaciones/SeccionCotizacion';
import { Comprobante } from '../Comprobante';
import { EditorItems } from '../EditorItems';
import {
  esEditable, itemsDesdeDetalles, LIMITES_SOLICITUD, nombreCompleto, numeroSolicitud, ORIGENES_SOLICITUD,
} from '../reglasSolicitud';
import { useSolicitudEditable } from '../useSolicitudEditable';
import { registradaPor } from './reglasBandeja';
import estilosPanel from '../../panel/Panel.module.css';
import estilos from '../Solicitudes.module.css';

const textoEstado = (estado) => TEXTOS_ESTADO_SOLICITUD[estado] ?? estado;

/** Quién hizo el cambio de estado: { actor: 'personal' | 'cliente' | 'sistema', por: nombre }. */
function autorHistorial(h) {
  const por = typeof h.por === 'string' ? h.por : h.por?.nombre ? nombreCompleto(h.por) : '';
  if (h.actor === 'cliente') return por ? `${por} (cliente)` : 'El cliente';
  if (h.actor === 'sistema') return 'El sistema';
  return por || 'El personal';
}

/** Historial de estados y eventos (por ejemplo, un adjunto quitado), del más antiguo al más reciente. */
function Historial({ historial = [] }) {
  if (!historial.length) return <p className={estilos.ninguno}>Sin cambios registrados.</p>;
  const ordenado = [...historial].sort((a, b) => String(a.fechaChile ?? a.fecha).localeCompare(String(b.fechaChile ?? b.fecha)));
  return (
    <ol className={estilos.historial}>
      {ordenado.map((h, i) => (
        <li key={h.idHistorial ?? i} className={unirClases(estilos.paso, esEvento(h) && estilos.evento)}>
          {esEvento(h) ? (
            // El estado no cambió: es un evento (por ejemplo, «Se quitó el adjunto logo.pdf»).
            <p className={estilos.pasoEstado}>{h.motivo || 'Cambio en la solicitud'}</p>
          ) : (
            <p className={estilos.pasoEstado}>
              {h.estadoAnterior ? <>{textoEstado(h.estadoAnterior)} → </> : 'Creada como '}
              <strong>{textoEstado(h.estadoNuevo)}</strong>
            </p>
          )}
          <p className={estilos.pasoMeta}>
            <span className="cifras">{formatearFechaHora(h.fechaChile ?? h.fecha)}</span> · {autorHistorial(h)}
          </p>
          {h.motivo && !esEvento(h) && <p className={estilos.pasoMotivo}>Motivo: {h.motivo}</p>}
        </li>
      ))}
    </ol>
  );
}

const esEvento = (h) => Boolean(h.estadoAnterior) && h.estadoAnterior === h.estadoNuevo;

/** El aviso de una acción que no resultó. Si otra persona la cambió, ofrece cargar la versión actual. */
function AvisoFalla({ falla, alVerActual }) {
  if (!falla) return null;
  // Un 409 (otra persona la cambió o el estado ya no lo permite): la ficha ya se recargó detrás del diálogo.
  if (falla.estado === 409) {
    const obsoleta = falla.codigo === 'version_obsoleta';
    return (
      <Aviso tono="peligro" titulo={obsoleta ? 'Otra persona modificó esta solicitud' : falla.message || 'Esta acción ya no es posible'}
        accion={<Boton variante="secundario" tamano="compacto" iconoInicio={RotateCcw} onClick={alVerActual}>Ver versión actual</Boton>}>
        No se guardó nada. Ya cargamos la versión actual: revísala y vuelve a intentarlo si corresponde.
      </Aviso>
    );
  }
  return <Aviso tono="peligro" titulo={falla.message || 'No pudimos hacerlo. Inténtalo de nuevo.'} />;
}

/** Diálogo para rechazar con motivo obligatorio (10 a 500 caracteres). */
function DialogoRechazo({ solicitud, abierto, alCerrar, alRechazar }) {
  const [motivo, setMotivo] = useState('');
  const [intento, setIntento] = useState(false);
  const [estado, setEstado] = useState({ enviando: false, falla: null });
  const refMotivo = useRef(null);
  const [abiertoAntes, setAbiertoAntes] = useState(abierto);
  if (abierto !== abiertoAntes) {
    setAbiertoAntes(abierto);
    if (abierto) {
      setEstado({ enviando: false, falla: null });
      setIntento(false);
    }
  }
  const largo = motivo.trim().length;
  const error = largo < LIMITES_SOLICITUD.motivoMinimo
    ? 'Escribe el motivo del rechazo, con al menos 10 caracteres. El cliente y el personal lo verán en el historial.'
    : largo > LIMITES_SOLICITUD.motivoMaximo ? 'Usa hasta 500 caracteres.' : null;

  async function enviar(evento) {
    evento.preventDefault();
    setIntento(true);
    if (error) {
      refMotivo.current?.focus();
      return;
    }
    setEstado({ enviando: true, falla: null });
    try {
      await alRechazar(motivo.trim());
      setEstado({ enviando: false, falla: null });
      setMotivo('');
      setIntento(false);
    } catch (falla) {
      setEstado({ enviando: false, falla });
      refMotivo.current?.focus();
    }
  }

  return (
    <Dialogo
      abierto={abierto}
      alCerrar={() => !estado.enviando && alCerrar()}
      titulo={`Rechazar la solicitud ${numeroSolicitud(solicitud)}`}
      descripcion="El motivo queda registrado en el historial, con tu nombre y la fecha."
      enfoqueInicial={refMotivo}
      ancho="32rem"
      pie={(
        <>
          <Boton variante="secundario" onClick={alCerrar} disabled={estado.enviando}>Cancelar</Boton>
          <Boton type="submit" form="formulario-rechazo" variante="peligro" iconoInicio={Ban} cargando={estado.enviando} textoCargando="Rechazando…">
            Rechazar solicitud
          </Boton>
        </>
      )}
    >
      <form id="formulario-rechazo" className={estilosPanel.formulario} onSubmit={enviar} noValidate>
        <AvisoFalla falla={estado.falla} alVerActual={alCerrar} />
        <Campo ref={refMotivo} etiqueta="Motivo del rechazo" como="textarea" requerido rows={4} name="motivo" maxLength={LIMITES_SOLICITUD.motivoMaximo}
          ayuda="Entre 10 y 500 caracteres. Por ejemplo: «No fabricamos letreros de ese tamaño»." value={motivo}
          onChange={(e) => setMotivo(e.target.value)} onBlur={() => motivo && setIntento(true)}
          error={intento ? error ?? undefined : undefined} disabled={estado.enviando} />
      </form>
    </Dialogo>
  );
}

/** Confirmación simple (devolver a «Solicitada» o eliminar). */
function Confirmacion({ abierto, titulo, descripcion, accion, variante = 'secundario', icono, alCerrar, alConfirmar }) {
  const [estado, setEstado] = useState({ enviando: false, falla: null });
  const refCancelar = useRef(null);
  const [abiertoAntes, setAbiertoAntes] = useState(abierto);
  if (abierto !== abiertoAntes) {
    setAbiertoAntes(abierto);
    if (abierto) setEstado({ enviando: false, falla: null });
  }
  async function confirmar() {
    setEstado({ enviando: true, falla: null });
    try {
      await alConfirmar();
      setEstado({ enviando: false, falla: null });
    } catch (falla) {
      setEstado({ enviando: false, falla });
    }
  }
  return (
    <Dialogo abierto={abierto} alCerrar={() => !estado.enviando && alCerrar()} titulo={titulo} descripcion={descripcion}
      enfoqueInicial={refCancelar} ancho="30rem"
      pie={(
        <>
          <Boton ref={refCancelar} variante="secundario" onClick={alCerrar} disabled={estado.enviando}>Cancelar</Boton>
          <Boton variante={variante} iconoInicio={icono} onClick={confirmar} cargando={estado.enviando}>{accion}</Boton>
        </>
      )}>
      <AvisoFalla falla={estado.falla} alVerActual={alCerrar} />
    </Dialogo>
  );
}

/** Edición de los letreros y la descripción, con control de concurrencia (If-Match). */
function EdicionSolicitud({ solicitud, productos, alCancelar, alGuardar, alRecargar }) {
  const {
    agregarItem, aplicarErroresApi, cambiarItem, comprobar, items, paraApi, propiedades, quitarItem, refFormulario, tocar, visibles,
  } = useSolicitudEditable({
    itemsIniciales: itemsDesdeDetalles(solicitud.detalles, productos),
    descripcionInicial: solicitud.descripcionGeneral ?? '',
  });
  const [estado, setEstado] = useState({ guardando: false, error: null, conflicto: false });
  const refAviso = useRef(null);

  useEffect(() => {
    if (!estado.guardando && (estado.error || estado.conflicto)) refAviso.current?.focus();
  }, [estado]);

  async function guardar(evento) {
    evento.preventDefault();
    if (estado.guardando || !comprobar()) return;
    setEstado({ guardando: true, error: null, conflicto: false });
    try {
      const respuesta = await solicitudes.editar(solicitud.idSolicitud, paraApi(), solicitud.rowVersion);
      setEstado({ guardando: false, error: null, conflicto: false });
      alGuardar(respuesta);
    } catch (falla) {
      if (falla?.codigo === 'version_obsoleta') {
        setEstado({ guardando: false, error: falla.message, conflicto: true });
      } else if (aplicarErroresApi(falla)) {
        setEstado({ guardando: false, error: null, conflicto: false });
      } else {
        setEstado({ guardando: false, error: falla?.message || 'No pudimos guardar los cambios. Inténtalo de nuevo.', conflicto: false });
      }
    }
  }

  return (
    <form ref={refFormulario} className={estilosPanel.formulario} onSubmit={guardar} noValidate aria-labelledby="titulo-edicion">
      <h2 id="titulo-edicion" className={estilos.subtitulo}>Editar la solicitud</h2>
      {solicitud.estado === 'cotizado' && (
        <Aviso tono="aviso" titulo="Esta solicitud ya tiene una cotización pendiente">
          Al guardar, esa cotización quedará rechazada y la solicitud volverá a «Solicitada» para cotizarla de nuevo.
        </Aviso>
      )}
      <div ref={refAviso} tabIndex={-1} className={estilos.foco}>
        {estado.conflicto && (
          <Aviso tono="peligro" titulo="Otra persona modificó esta solicitud"
            accion={<Boton variante="secundario" tamano="compacto" iconoInicio={RotateCcw} onClick={alRecargar}>Cargar la versión actual</Boton>}>
            {estado.error && !/^Otra persona/.test(estado.error) ? `${estado.error} ` : ''}
            Tus cambios siguen aquí, pero no se guardaron. Si cargas la versión actual, se descartará lo que escribiste:
            copia antes lo que quieras conservar.
          </Aviso>
        )}
        {estado.error && !estado.conflicto && <Aviso tono="peligro" titulo={estado.error} />}
      </div>
      <EditorItems items={items} productos={productos} errores={visibles} alCambiar={cambiarItem}
        alTocar={tocar} alAgregar={agregarItem} alQuitar={quitarItem} deshabilitado={estado.guardando} />
      <Campo etiqueta="Descripción" como="textarea" requerido rows={5} maxLength={LIMITES_SOLICITUD.descripcionMaxima}
        disabled={estado.guardando} {...propiedades('descripcionGeneral')} />
      <div className={estilosPanel.pieFormulario}>
        <Boton variante="secundario" onClick={alCancelar} disabled={estado.guardando}>Cancelar</Boton>
        <Boton type="submit" iconoInicio={Save} cargando={estado.guardando} textoCargando="Guardando…">Guardar cambios</Boton>
      </div>
    </form>
  );
}

/**
 * /panel/solicitudes/:id: ficha con el comprobante, los adjuntos (se abren en grande; el PDF se descarga),
 * el historial de estados y quién la registró. Todo el personal (RFU11) edita, adjunta, rechaza con motivo,
 * la devuelve a «Solicitada» o la elimina (borrado lógico); cada acción envía la versión vista (If-Match).
 * Desde la fase 8 trae la sección «Cotización»: cotizar, registrar la respuesta del cliente y crear el pedido.
 * Es el destino del botón «Ver solicitud» del correo al taller.
 */
export default function FichaSolicitud() {
  const { id } = useParams();
  const navegar = useNavigate();
  const { state } = useLocation();
  const { anunciar } = useAvisos();
  const consulta = useConsulta(`solicitud:${id}`, () => solicitudes.obtener(id));
  const [editando, setEditando] = useState(false);
  const [dialogo, setDialogo] = useState(null);
  const productos = useConsulta('productos', publico.productos);
  const refTitulo = useRef(null);
  const solicitud = consulta.datos;
  // Mientras sube un adjunto del personal (o justo después, hasta recargar), no se edita: la versión cambia.
  const [adjuntosOcupados, setAdjuntosOcupados] = useState(false);
  const firmaAdjuntos = useRef(null);
  const editable = solicitud && esEditable(solicitud.estado);
  const actualizando = adjuntosOcupados || consulta.recargando;

  /** Ejecuta una acción de estado; ante un 409 recarga la ficha (detrás del diálogo) y deja que el diálogo lo explique. */
  async function conRecargaEnConflicto(accion) {
    try {
      await accion();
    } catch (falla) {
      if (falla?.estado === 409) consulta.recargar();
      throw falla;
    }
  }

  // B11: el h1 recibe el foco cuando llegan los datos (y al cambiar de solicitud).
  const cargada = Boolean(solicitud);
  useEffect(() => {
    if (cargada) refTitulo.current?.focus();
  }, [id, cargada]);

  function terminarEdicion() {
    setEditando(false);
    requestAnimationFrame(() => refTitulo.current?.focus());
  }

  async function rechazar(motivo) {
    await conRecargaEnConflicto(() => solicitudes.cambiarEstado(id, { estado: 'rechazado', motivo }, solicitud.rowVersion));
    setDialogo(null);
    anunciar({ tono: 'exito', titulo: `Solicitud ${numeroSolicitud(solicitud)} rechazada`, texto: 'El motivo quedó en el historial.' });
    consulta.recargar();
  }

  async function devolver() {
    await conRecargaEnConflicto(() => solicitudes.cambiarEstado(id, { estado: 'solicitado' }, solicitud.rowVersion));
    setDialogo(null);
    anunciar({ tono: 'exito', titulo: 'La solicitud volvió a «Solicitada»' });
    consulta.recargar();
  }

  async function eliminar() {
    await conRecargaEnConflicto(() => solicitudes.eliminar(id, solicitud.rowVersion));
    anunciar({ tono: 'exito', titulo: `Solicitud ${numeroSolicitud(solicitud)} eliminada` });
    navegar('/panel/solicitudes', { replace: true });
  }

  if (consulta.cargando) {
    return (
      <div className={estilosPanel.pagina}>
        <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>Solicitud</h1>
        <p role="status" className="solo-lectores">Cargando la solicitud…</p>
        <Esqueleto lineas={8} />
      </div>
    );
  }

  if (!solicitud) {
    const noExiste = consulta.error?.estado === 404;
    return (
      <div className={estilosPanel.pagina}>
        <Enlace como={Link} to="/panel/solicitudes" className={estilos.volver}><ArrowLeft size={16} aria-hidden="true" />Solicitudes</Enlace>
        <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>Solicitud</h1>
        <Aviso tono="peligro" titulo={noExiste ? 'No encontramos esta solicitud' : 'No pudimos cargar la solicitud'}
          accion={noExiste ? null : <Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {noExiste ? 'Puede que se haya eliminado o que el número no sea correcto.' : consulta.error?.message}
        </Aviso>
      </div>
    );
  }

  return (
    <div className={estilosPanel.pagina}>
      <div className={estilosPanel.cabecera}>
        <div>
          <Enlace como={Link} to="/panel/solicitudes" className={estilos.volver}><ArrowLeft size={16} aria-hidden="true" />Solicitudes</Enlace>
          <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>
            Solicitud <span className="cifras">{numeroSolicitud(solicitud)}</span>
          </h1>
          <div className={estilos.insignias}>
            <EstadoSolicitud estado={solicitud.estado} />
            <Insignia tono="neutro">{ORIGENES_SOLICITUD[solicitud.origen] ?? solicitud.origen}</Insignia>
          </div>
        </div>
      </div>

      {state?.registrada && (
        <Aviso tono="exito" titulo={`Solicitud ${numeroSolicitud(solicitud)} registrada`}>
          {solicitud.cliente?.correo || solicitud.cliente?.email ? 'Le enviamos el comprobante al cliente. ' : ''}
          Si trajo su logo o diseño, adjúntalo más abajo.
        </Aviso>
      )}
      {consulta.error && <Aviso tono="peligro" titulo="No pudimos actualizar la ficha">{consulta.error.message}</Aviso>}

      <div className={estilos.fichaRejilla}>
        <div className={estilos.fichaPrincipal}>
          <Tarjeta como="section" className={estilos.seccion}>
            {editando ? (
              productos.datos ? (
                <EdicionSolicitud solicitud={solicitud} productos={productos.datos} alCancelar={terminarEdicion}
                  alRecargar={() => { consulta.recargar(); terminarEdicion(); }}
                  alGuardar={() => {
                    anunciar({ tono: 'exito', titulo: 'Guardamos los cambios de la solicitud' });
                    consulta.recargar();
                    terminarEdicion();
                  }} />
              ) : productos.error ? (
                <Aviso tono="peligro" titulo="No pudimos cargar los tipos de letrero"
                  accion={<Boton variante="secundario" tamano="compacto" onClick={productos.recargar}>Reintentar</Boton>} />
              ) : (
                <>
                  <p role="status" className="solo-lectores">Preparando la edición…</p>
                  <Esqueleto lineas={6} />
                </>
              )
            ) : (
              <>
                <h2 className="solo-lectores">Comprobante</h2>
                <Comprobante solicitud={solicitud} nivel={3} conContacto sinAdjuntos encabezado={false} />
              </>
            )}
          </Tarjeta>

          <SeccionCotizacion solicitud={solicitud} alCambiar={consulta.recargar} />

          <Tarjeta como="section" className={estilos.seccion}>
            {editable ? (
              <Adjuntos
                key={solicitud.idSolicitud}
                titulo="Archivos adjuntos"
                descripcion="Toca una miniatura para verla en grande; el PDF se descarga. Puedes agregar JPG, PNG, WebP o PDF de hasta 10 MB."
                iniciales={solicitud.adjuntos ?? []}
                subir={(archivo, opciones) => solicitudes.subirAdjunto(solicitud.idSolicitud, archivo, opciones)}
                quitar={(idAdjunto) => solicitudes.quitarAdjunto(solicitud.idSolicitud, idAdjunto)}
                cargarMiniatura={apiAdjuntos.miniatura}
                cargarArchivo={apiAdjuntos.archivo}
                permitirVer
                alCambiar={(listos, { subiendo }) => {
                  // Subir o quitar un adjunto cambia la versión y suma una entrada al historial: se recarga la ficha.
                  const firma = listos.map((a) => a.idAdjunto).join(',');
                  if (firmaAdjuntos.current !== null && firmaAdjuntos.current !== firma) consulta.recargar();
                  firmaAdjuntos.current = firma;
                  setAdjuntosOcupados(subiendo);
                }}
              />
            ) : (
              <>
                <h2 className={estilos.subtitulo}>Archivos adjuntos{solicitud.adjuntos?.length ? ` (${solicitud.adjuntos.length})` : ''}</h2>
                <ListaAdjuntos adjuntos={solicitud.adjuntos ?? []} cargarMiniatura={apiAdjuntos.miniatura} cargarArchivo={apiAdjuntos.archivo} />
              </>
            )}
          </Tarjeta>
        </div>

        <aside className={estilos.fichaLateral} aria-label="Acciones e historial">
          <Tarjeta como="section" className={estilos.seccion}>
            <h2 className={estilos.subtitulo}>Acciones</h2>
            {editable ? (
              <div className={estilos.acciones}>
                {!editando && (
                  <Boton variante="secundario" iconoInicio={Pencil} anchoCompleto
                    aria-disabled={actualizando ? 'true' : undefined}
                    onClick={() => !actualizando && setEditando(true)}>
                    Editar solicitud
                  </Boton>
                )}
                {actualizando && <p className={estilos.nota} role="status">Actualizando la ficha…</p>}
                {solicitud.estado === 'cotizado' && (
                  <Boton variante="secundario" iconoInicio={RotateCcw} anchoCompleto onClick={() => setDialogo('devolver')}>Volver a «Solicitada»</Boton>
                )}
                <Boton variante="secundario" iconoInicio={Ban} anchoCompleto onClick={() => setDialogo('rechazar')}>Rechazar con motivo</Boton>
              </div>
            ) : (
              <p className={estilos.explicacion}>
                Una solicitud {textoEstado(solicitud.estado).toLowerCase()} ya no se edita ni cambia de estado.
              </p>
            )}
            {(solicitud.estado === 'solicitado' || solicitud.estado === 'rechazado') && (
              <Boton variante="fantasma" iconoInicio={Trash2} anchoCompleto onClick={() => setDialogo('eliminar')}>Eliminar solicitud</Boton>
            )}
            <dl className={estilos.datosLaterales}>
              <div className={estilos.dato}><dt>Registrada por</dt><dd>{registradaPor(solicitud)}</dd></div>
              {solicitud.idCliente && (
                <div className={estilos.dato}>
                  <dt>Cliente</dt>
                  <dd><Enlace como={Link} to={`/panel/clientes/${solicitud.idCliente}`}>{nombreCompleto(solicitud.cliente)}</Enlace></dd>
                </div>
              )}
            </dl>
          </Tarjeta>

          <Tarjeta como="section" className={estilos.seccion}>
            <h2 className={estilos.subtitulo}>Historial</h2>
            <Historial historial={solicitud.historial} />
          </Tarjeta>
        </aside>
      </div>

      <DialogoRechazo solicitud={solicitud} abierto={dialogo === 'rechazar'} alCerrar={() => setDialogo(null)} alRechazar={rechazar} />
      <Confirmacion abierto={dialogo === 'devolver'} titulo={`¿Volver la solicitud ${numeroSolicitud(solicitud)} a «Solicitada»?`}
        descripcion="La cotización pendiente quedará rechazada y la solicitud se podrá cotizar de nuevo." accion="Volver a «Solicitada»"
        icono={RotateCcw} alCerrar={() => setDialogo(null)} alConfirmar={devolver} />
      <Confirmacion abierto={dialogo === 'eliminar'} titulo={`¿Eliminar la solicitud ${numeroSolicitud(solicitud)}?`}
        descripcion="Deja de verse en la bandeja. Queda guardada en la base, pero no se puede recuperar desde el panel." accion="Eliminar solicitud"
        variante="peligro" icono={Trash2} alCerrar={() => setDialogo(null)} alConfirmar={eliminar} />
    </div>
  );
}
