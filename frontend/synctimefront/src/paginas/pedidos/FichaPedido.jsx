import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  ArrowLeft, ArrowRight, Ban, CheckCheck, HandCoins, Pencil, Save, Trash2, Truck, Undo2,
} from 'lucide-react';
import {
  Aviso, Boton, Enlace, Esqueleto, EstadoPago, EstadoPedido, Insignia, Tarjeta, TEXTOS_ESTADO_PEDIDO,
} from '../../componentes/ui';
import { empleados as apiEmpleados, pedidos } from '../../api/pedidos';
import { useAvisos } from '../../avisos/Avisos';
import { unirClases } from '../../componentes/ui/clases';
import { GESTION } from '../../estructuras/navegacion';
import { useSesion } from '../../sesion/sesion';
import { hoyChile } from '../../utilidades/fechas';
import { formatearFecha, formatearFechaHora, formatearPesos } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { useFormulario } from '../cuenta/useFormulario';
import { AvisoConflicto } from '../compartidos/AvisoConflicto';
import { AvisoFalla, Confirmacion } from '../compartidos/Dialogos';
import { errorTrasCorte, sinRespuesta } from '../compartidos/trasCorte';
import { useConflictoDeVersion } from '../compartidos/useConflictoDeVersion';
import { numeroCotizacion } from '../cotizaciones/reglasCotizacion';
import { numeroSolicitud, telefonoVisible } from '../solicitudes/reglasSolicitud';
import { CamposPedido } from './CamposPedido';
import {
  DialogoAbono, DialogoAnularAbono, DialogoAnularPedido, DialogoEntrega,
} from './DialogosPedido';
import {
  datosDesdePedido, datosPedidoParaApi, MEDIOS_PAGO, MODALIDADES, montoFinal, nombreDe, numeroPedido, ORDEN_DATOS_PEDIDO,
  ordenarTransiciones, ORIGENES_PEDIDO, validarDatosPedido,
} from './reglasPedido';
import estilosPanel from '../panel/Panel.module.css';
import estilosSolicitud from '../solicitudes/Solicitudes.module.css';
import estilos from './Pedidos.module.css';

const ICONOS_TRANSICION = { avanzar: ArrowRight, entregar: Truck, volver: Undo2, anular: Ban };
const textoEstado = TEXTOS_ESTADO_PEDIDO;

function Dato({ termino, children }) {
  return (
    <div className={estilosSolicitud.dato}>
      <dt>{termino}</dt>
      <dd>{children}</dd>
    </div>
  );
}

/** Historial de estados del pedido, del más antiguo al más reciente, con quién y cuándo. */
function HistorialPedido({ historial = [] }) {
  if (!historial.length) return <p className={estilosSolicitud.ninguno}>Sin cambios registrados.</p>;
  const ordenado = [...historial].sort((a, b) => String(a.fechaChile ?? '').localeCompare(String(b.fechaChile ?? '')));
  return (
    <ol className={estilosSolicitud.historial}>
      {ordenado.map((h, i) => (
        // El historial no trae id propio: su posición en el tiempo lo identifica.
        <li key={`${h.fechaChile}-${i}`} className={estilosSolicitud.paso}>
          <p className={estilosSolicitud.pasoEstado}>
            {h.estadoAnterior ? <>{textoEstado[h.estadoAnterior] ?? h.estadoAnterior} → </> : 'Creado como '}
            <strong>{textoEstado[h.estadoNuevo] ?? h.estadoNuevo}</strong>
          </p>
          <p className={estilosSolicitud.pasoMeta}>
            <span className="cifras">{formatearFechaHora(h.fechaChile)}</span>{h.empleado ? ` · ${nombreDe(h.empleado)}` : ''}
          </p>
          {h.nota && <p className={estilosSolicitud.pasoMotivo}>{h.estadoNuevo === 'anulado' ? 'Motivo' : 'Nota'}: {h.nota}</p>}
        </li>
      ))}
    </ol>
  );
}

/**
 * Abonos, como un libro de caja: fecha y medio, quién lo registró, la observación y el monto alineado a la
 * derecha. Los anulados quedan tachados, con la insignia «Anulado», su motivo, quién y cuándo; «Anular»
 * aparece solo donde la API lo permite (`puedeAnular`).
 */
function Abonos({ pagos = [], alAnular }) {
  const filas = [...pagos].sort((a, b) => String(b.fechaPago).localeCompare(String(a.fechaPago)) || (b.idPago ?? 0) - (a.idPago ?? 0));
  if (!filas.length) return <p className={estilosSolicitud.ninguno}>Aún no hay abonos. Un pedido se puede crear y entregar sin pago previo.</p>;
  return (
    <ul className={estilos.abonos} aria-label="Abonos del pedido">
      {filas.map((p) => (
        <li key={p.idPago} className={unirClases(estilos.abono, p.anulado && estilos.abonoAnulado)}>
          <div className={estilos.abonoDetalle}>
            <p className={estilos.abonoTitulo}>
              <span className={unirClases('cifras', p.anulado && estilos.tachado)}>{formatearFecha(p.fechaPago)}</span>
              {' · '}
              <span className={p.anulado ? estilos.tachado : undefined}>{MEDIOS_PAGO[p.medioPago] ?? p.medioPago}</span>
              {p.anulado && <span className={estilos.insigniaEnLinea}><Insignia tono="peligro" icono={Ban}>Anulado</Insignia></span>}
            </p>
            <p className={estilos.abonoMeta}>
              Registrado por {nombreDe(p.registradoPor)}
              {p.fechaRegistroChile && <> el <span className="cifras">{formatearFechaHora(p.fechaRegistroChile)}</span></>}
            </p>
            {p.observacion && <p className={unirClases(estilos.abonoMeta, p.anulado && estilos.tachado)}>{p.observacion}</p>}
            {p.anulado && (
              <p className={estilos.abonoMotivo}>
                Motivo: {p.motivoAnulacion || 'sin motivo'}
                {p.anuladoPor ? ` · anulado por ${nombreDe(p.anuladoPor)}` : ''}
                {p.fechaAnulacionChile ? <> el <span className="cifras">{formatearFechaHora(p.fechaAnulacionChile)}</span></> : null}
              </p>
            )}
          </div>
          <p className={estilos.abonoMonto}>
            {p.anulado
              ? <><s className="cifras">{formatearPesos(p.monto)}</s><span className="solo-lectores"> (anulado, no suma)</span></>
              : <strong className="cifras">{formatearPesos(p.monto)}</strong>}
          </p>
          {!p.anulado && p.puedeAnular && (
            <Boton variante="fantasma" tamano="compacto" iconoInicio={Undo2} className={estilos.abonoAccion} onClick={() => alAnular(p)}>
              Anular<span className="solo-lectores"> el abono de {formatearPesos(p.monto)} del {formatearFecha(p.fechaPago)}</span>
            </Boton>
          )}
        </li>
      ))}
    </ul>
  );
}

/**
 * Edición de los datos del pedido, con la versión vista. Si otra persona lo cambió (409 version_obsoleta), lo
 * escrito se queda: se relee la versión actual sin desmontar el formulario y se puede guardar sobre ella o
 * descartar. Los montos (trabajo e instalación) solo los cambia gestión: para el resto se muestran sin editar y
 * viajan como estaban.
 */
function EdicionPedido({ pedido, hoy, esGestion, alCancelar, alGuardar, alDescartar }) {
  const soloFactura = pedido.estadoPedido === 'entregado';
  const inicial = datosDesdePedido(pedido);
  const abonado = Number(pedido.montoAbonado ?? 0);
  const costoOriginal = Number(pedido.costoInstalacionAdicional ?? 0);
  const form = useFormulario({
    inicial,
    validar: (d) => {
      const e = validarDatosPedido(d, { hoy, fechaVigente: inicial.fechaEntregaEstimada, conMontoBase: !soloFactura && esGestion, abonado });
      if (!esGestion && !d.requiereInstalacion && costoOriginal > 0) {
        e.requiereInstalacion = 'Quitar la instalación cambia los montos: solo el dueño o el administrador pueden hacerlo.';
      }
      return e;
    },
    orden: ORDEN_DATOS_PEDIDO,
  });
  const { refFormulario } = form;
  const empleados = useConsulta('empleados', apiEmpleados.listar);
  const [estado, setEstado] = useState({ guardando: false, error: null });
  const version = useConflictoDeVersion({ releer: () => pedidos.obtener(pedido.idPedido), versionInicial: pedido.rowVersion });
  const actual = version.conflicto?.actual;

  /** El cuerpo del PUT: quien no es de gestión envía los montos como estaban. */
  function cuerpo() {
    const datos = soloFactura ? { ...inicial, numeroFactura: form.datos.numeroFactura } : form.datos;
    const base = datosPedidoParaApi(datos, { conMontoBase: true });
    if (esGestion) return base;
    return {
      ...base,
      montoBaseTrabajo: Number(pedido.montoBaseTrabajo ?? 0),
      costoInstalacionAdicional: base.requiereInstalacion ? costoOriginal : 0,
    };
  }

  /** `sobreActual`: el reintento explícito tras un conflicto, con la versión releída. */
  async function guardar(evento, sobreActual = false) {
    evento?.preventDefault();
    if (estado.guardando || (version.conflicto && !sobreActual)) return;
    if (!soloFactura && !form.comprobar()) return;
    setEstado({ guardando: true, error: null });
    try {
      await pedidos.editar(pedido.idPedido, cuerpo(), version.version);
      setEstado({ guardando: false, error: null });
      alGuardar();
    } catch (falla) {
      if (falla?.codigo === 'version_obsoleta') {
        setEstado({ guardando: false, error: null });
        version.detectar();
      } else if (form.aplicarErroresApi(falla)) setEstado({ guardando: false, error: null });
      else if (falla?.estado === 409 && /abonad/i.test(falla.message ?? '') && form.aplicarErroresApi({ errores: { montoBaseTrabajo: [falla.message] } })) {
        setEstado({ guardando: false, error: null });
      } else setEstado({ guardando: false, error: falla?.message || 'No pudimos guardar los cambios. Inténtalo de nuevo.' });
    }
  }

  const noSePuede = actual && actual.estadoPedido !== pedido.estadoPedido
    ? `Ahora está «${textoEstado[actual.estadoPedido] ?? actual.estadoPedido}»: descarta tus cambios y vuelve a editarlo si corresponde.`
    : null;
  const resumen = actual
    ? `${textoEstado[actual.estadoPedido] ?? actual.estadoPedido}, monto final ${formatearPesos(actual.montoTotalFinal)}, entrega estimada ${formatearFecha(actual.fechaEntregaEstimada)}${actual.numeroFactura ? `, factura ${actual.numeroFactura}` : ''}.`
    : null;

  return (
    <form ref={refFormulario} className={estilosPanel.formulario} onSubmit={guardar} noValidate aria-labelledby="titulo-edicion-pedido">
      <h2 id="titulo-edicion-pedido" className={estilosSolicitud.subtitulo}>Editar los datos del pedido</h2>
      <AvisoConflicto conflicto={version.conflicto} entidad="este pedido" resumen={resumen} noSePuede={noSePuede} guardando={estado.guardando}
        refAccion={version.refAccion} alGuardarSobre={() => guardar(null, true)} alDescartar={alDescartar} alReintentar={version.detectar} />
      {/* Un error común se anuncia solo (role="alert"); el foco no se mueve. */}
      {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
      <CamposPedido form={form} empleados={empleados} hoy={hoy} deshabilitado={estado.guardando} soloFactura={soloFactura}
        conMontoBase montosSoloLectura={!esGestion} fechaVigente={inicial.fechaEntregaEstimada} />
      {!soloFactura && (
        <p className={estilosSolicitud.nota}>
          Monto final con estos datos: <strong className="cifras">{formatearPesos(esGestion ? montoFinal(form.datos) : Number(pedido.montoTotalFinal ?? 0))}</strong>
          {abonado > 0 ? <> · ya abonado <span className="cifras">{formatearPesos(abonado)}</span></> : null}
        </p>
      )}
      <div className={estilosPanel.pieFormulario}>
        <Boton variante="secundario" onClick={alCancelar} disabled={estado.guardando}>Cancelar</Boton>
        <Boton type="submit" iconoInicio={Save} cargando={estado.guardando} textoCargando="Guardando…"
          aria-disabled={version.conflicto ? 'true' : undefined}>
          Guardar cambios
        </Boton>
      </div>
    </form>
  );
}

/**
 * /panel/pedidos/:id: ficha del pedido (S07). El avance (E09) muestra solo las transiciones que permite la API;
 * «Entregado» pide la fecha real y anular, un motivo. Los abonos (E25) se registran en un diálogo y se anulan con
 * motivo (quedan tachados). Editar y avanzar envían la versión vista (If-Match); un 409 recarga la ficha.
 */
export default function FichaPedido() {
  const { id } = useParams();
  // Otra ficha, otro estado: al cambiar de pedido no quedan abiertos la edición, un diálogo ni un avance.
  return <Ficha key={id} id={id} />;
}

function Ficha({ id }) {
  const navegar = useNavigate();
  const { anunciar } = useAvisos();
  const { tieneRol } = useSesion();
  const consulta = useConsulta(`pedido:${id}`, () => pedidos.obtener(id));
  const [editando, setEditando] = useState(false);
  const [dialogo, setDialogo] = useState(null);
  const [pagoElegido, setPagoElegido] = useState(null);
  const [avance, setAvance] = useState({ destino: null, falla: null });
  const refTitulo = useRef(null);
  const pedido = consulta.datos;
  const hoy = hoyChile();
  const esGestion = tieneRol(GESTION);

  const cargado = Boolean(pedido);
  useEffect(() => {
    if (cargado) refTitulo.current?.focus();
  }, [id, cargado]);

  /** Recarga la ficha ante un 409 (otra persona lo cambió o ya no se puede) y deja que el diálogo lo explique. */
  async function conRecarga(accion) {
    try {
      return await accion();
    } catch (falla) {
      if (falla?.estado === 409) consulta.recargar();
      throw falla;
    }
  }

  function terminarEdicion() {
    setEditando(false);
    requestAnimationFrame(() => refTitulo.current?.focus());
  }

  async function avanzar(destino) {
    if (avance.destino) return;
    setAvance({ destino, falla: null });
    try {
      await conRecarga(() => pedidos.cambiarEstado(pedido.idPedido, { estado: destino }, pedido.rowVersion));
      setAvance({ destino: null, falla: null });
      anunciar({ tono: 'exito', titulo: `Pedido ${numeroPedido(pedido)}: ${textoEstado[destino] ?? destino}` });
      consulta.recargar();
    } catch (falla) {
      setAvance({ destino: null, falla });
    }
  }

  async function entregar(cambio) {
    await conRecarga(() => pedidos.cambiarEstado(pedido.idPedido, { estado: 'entregado', ...cambio }, pedido.rowVersion));
    setDialogo(null);
    anunciar({ tono: 'exito', titulo: `Pedido ${numeroPedido(pedido)} entregado`, texto: Number(pedido.saldoPendiente) > 0 ? `Queda un saldo de ${formatearPesos(pedido.saldoPendiente)} por cobrar.` : 'El ciclo quedó cerrado.' });
    consulta.recargar();
  }

  async function anular(motivo) {
    await conRecarga(() => pedidos.cambiarEstado(pedido.idPedido, { estado: 'anulado', motivo }, pedido.rowVersion));
    setDialogo(null);
    anunciar({ tono: 'exito', titulo: `Pedido ${numeroPedido(pedido)} anulado`, texto: 'El motivo quedó en el historial.' });
    consulta.recargar();
  }

  /**
   * ¿El abono quedó registrado aunque se perdió la respuesta? Relee la ficha y busca un abono vigente nuevo con
   * el mismo monto y medio. 'registrado' | 'no_registrado' | 'sin_confirmar' (no se pudo releer).
   */
  async function verificarAbono(abono) {
    try {
      const fresca = await pedidos.obtener(pedido.idPedido);
      const antes = new Set((pedido.pagos ?? []).map((p) => p.idPago));
      const nuevo = (fresca?.pagos ?? []).find((p) => !antes.has(p.idPago) && !p.anulado
        && Number(p.monto) === Number(abono.monto) && p.medioPago === abono.medioPago);
      consulta.recargar();
      return nuevo ? 'registrado' : 'no_registrado';
    } catch {
      return 'sin_confirmar';
    }
  }

  function abonoListo(abono, { trasCorte = false } = {}) {
    setDialogo(null);
    anunciar({
      tono: 'exito',
      titulo: `Abono de ${formatearPesos(abono.monto)} registrado`,
      texto: trasCorte ? 'Se perdió la conexión, pero revisamos la ficha y el abono sí quedó registrado.' : undefined,
    });
    consulta.recargar();
  }

  async function registrarAbono(abono) {
    try {
      await pedidos.registrarAbono(pedido.idPedido, abono);
    } catch (falla) {
      if (sinRespuesta(falla)) {
        // Sin respuesta no sabemos si se guardó: se revisa antes de dejar reintentar (la API no tiene idempotencia).
        const resultado = await verificarAbono(abono);
        if (resultado !== 'registrado') throw errorTrasCorte(resultado);
        abonoListo(abono, { trasCorte: true });
        return;
      }
      // Si otro abono llegó antes, o el pedido se pagó o se anuló, la ficha muestra lo vigente detrás del diálogo.
      if (falla?.estado === 409) consulta.recargar();
      throw falla;
    }
    abonoListo(abono);
  }

  /** «Revisar de nuevo» tras un corte que no se pudo confirmar. */
  async function revisarAbono(abono) {
    const resultado = await verificarAbono(abono);
    if (resultado === 'registrado') abonoListo(abono, { trasCorte: true });
    return resultado;
  }

  async function anularAbono(motivo) {
    await conRecarga(() => pedidos.anularAbono(pedido.idPedido, pagoElegido.idPago, motivo));
    setDialogo(null);
    anunciar({ tono: 'exito', titulo: 'Abono anulado', texto: 'Quedó tachado en el historial y dejó de sumar.' });
    consulta.recargar();
  }

  async function eliminar() {
    await conRecarga(() => pedidos.eliminar(pedido.idPedido, pedido.rowVersion));
    anunciar({ tono: 'exito', titulo: `Pedido ${numeroPedido(pedido)} eliminado` });
    navegar('/panel/pedidos', { replace: true });
  }

  const volver = <Enlace como={Link} to="/panel/pedidos" className={estilosSolicitud.volver}><ArrowLeft size={16} aria-hidden="true" />Pedidos</Enlace>;

  if (!pedido) {
    const noExiste = consulta.error?.estado === 404;
    return (
      <div className={estilosPanel.pagina}>
        <div>{volver}<h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>Pedido</h1></div>
        {consulta.cargando && (<><p role="status" className="solo-lectores">Cargando el pedido…</p><Esqueleto lineas={8} /></>)}
        {consulta.error && (
          <Aviso tono="peligro" titulo={noExiste ? 'No encontramos este pedido' : 'No pudimos cargar el pedido'}
            accion={noExiste ? null : <Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
            {noExiste ? 'Puede que se haya eliminado o que el número no sea correcto.' : consulta.error.message}
          </Aviso>
        )}
      </div>
    );
  }

  const anulado = pedido.estadoPedido === 'anulado';
  const transiciones = ordenarTransiciones(pedido.estadoPedido, pedido.transicionesPosibles ?? []);
  const principal = transiciones.find((t) => t.tipo === 'avanzar' || t.tipo === 'entregar');
  const saldo = Number(pedido.saldoPendiente ?? 0);
  const puedeAbonar = !anulado && saldo > 0;
  const abonado = Number(pedido.montoAbonado ?? 0);
  const aFavor = Number(pedido.aFavorDelCliente ?? (anulado ? abonado : 0));
  // Anular un pedido que tiene o tuvo abonos (aunque se hayan anulado) es solo de gestión.
  const tuvoAbonos = abonado > 0 || (pedido.pagos ?? []).length > 0;
  const anularBloqueado = !esGestion && tuvoAbonos && !anulado && pedido.estadoPedido !== 'entregado';
  const cliente = pedido.cliente ?? {};
  const actualizando = consulta.recargando;

  return (
    <div className={estilosPanel.pagina}>
      <div className={estilosPanel.cabecera}>
        <div>
          {volver}
          <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>Pedido <span className="cifras">{numeroPedido(pedido)}</span></h1>
          <div className={estilosSolicitud.insignias}>
            <EstadoPedido estado={pedido.estadoPedido} />
            <EstadoPago estado={pedido.estadoPago} />
            <Insignia tono="neutro">{ORIGENES_PEDIDO[pedido.origen] ?? pedido.origen}</Insignia>
            {pedido.cicloCerrado && <Insignia tono="exito" icono={CheckCheck}>Ciclo cerrado</Insignia>}
          </div>
        </div>
      </div>

      {consulta.error && <Aviso tono="peligro" titulo="No pudimos actualizar la ficha">{consulta.error.message}</Aviso>}
      {anulado && (
        <Aviso tono="aviso" titulo={aFavor > 0 ? `A devolver o a favor del cliente: ${formatearPesos(aFavor)}` : 'Pedido anulado'}>
          {pedido.motivoAnulacion ? `Motivo: ${pedido.motivoAnulacion}. ` : ''}
          {aFavor > 0 ? 'Tenía abonos vigentes al anularse. La devolución se acuerda con el cliente y no se registra en el sistema.' : 'Un pedido anulado no cambia más ni recibe abonos.'}
        </Aviso>
      )}

      <div className={estilosSolicitud.fichaRejilla}>
        <div className={estilosSolicitud.fichaPrincipal}>
          <Tarjeta como="section" aria-labelledby="titulo-avance" className={estilosSolicitud.seccion}>
            <h2 id="titulo-avance" className={estilosSolicitud.subtitulo}>Avance</h2>
            <AvisoFalla falla={avance.falla} alVerActual={() => setAvance({ destino: null, falla: null })} entidad="este pedido" />
            {transiciones.length > 0 ? (
              <div className={estilos.transiciones}>
                {transiciones.filter((t) => !(t.tipo === 'anular' && anularBloqueado)).map((t) => (
                  <Boton key={t.destino} iconoInicio={ICONOS_TRANSICION[t.tipo]}
                    variante={t === principal && !editando ? 'primario' : t.tipo === 'anular' ? 'fantasma' : 'secundario'}
                    cargando={avance.destino === t.destino}
                    aria-disabled={actualizando || (avance.destino && avance.destino !== t.destino) ? 'true' : undefined}
                    onClick={() => {
                      if (actualizando || avance.destino) return;
                      if (t.tipo === 'entregar') setDialogo('entrega');
                      else if (t.tipo === 'anular') setDialogo('anular');
                      else avanzar(t.destino);
                    }}>
                    {t.etiqueta}
                  </Boton>
                ))}
              </div>
            ) : (
              <p className={estilosSolicitud.explicacion}>
                {anulado ? 'Un pedido anulado no cambia más.' : pedido.estadoPedido === 'entregado'
                  ? (pedido.cicloCerrado ? 'Entregado y pagado: el ciclo está cerrado.' : 'Entregado. El ciclo se cierra al completar el pago.')
                  : 'No hay cambios de estado disponibles.'}
              </p>
            )}
            {actualizando && <p className={estilosSolicitud.nota} role="status">Actualizando la ficha…</p>}
            {anularBloqueado && (
              <p className={estilosSolicitud.nota}>Tiene o tuvo abonos: solo el dueño o el administrador pueden anularlo.</p>
            )}
            {esGestion && anulado && abonado === 0 && (
              <div><Boton variante="fantasma" iconoInicio={Trash2} onClick={() => setDialogo('eliminar')}>Eliminar pedido</Boton></div>
            )}
          </Tarjeta>
          <Tarjeta como="section" aria-labelledby="titulo-montos" className={estilosSolicitud.seccion}>
            <h2 id="titulo-montos" className={estilosSolicitud.subtitulo}>Montos</h2>
            <dl className={estilos.desglose}>
              <div><dt>Trabajo</dt><dd className="cifras">{formatearPesos(pedido.montoBaseTrabajo)}</dd></div>
              <div><dt>Instalación</dt><dd className="cifras">{formatearPesos(pedido.costoInstalacionAdicional)}</dd></div>
              <div className={estilos.filaFuerte}><dt>Monto final</dt><dd className="cifras">{formatearPesos(pedido.montoTotalFinal)}</dd></div>
              <div><dt>Abonado</dt><dd className="cifras">{abonado ? `− ${formatearPesos(abonado)}` : formatearPesos(0)}</dd></div>
              <div className={unirClases(estilos.filaSaldo, saldo > 0 && !anulado && estilos.saldoPendiente)}>
                <dt>Saldo pendiente</dt><dd className="cifras">{formatearPesos(anulado ? 0 : saldo)}</dd>
              </div>
            </dl>
            <p className={estilosSolicitud.nota}>
              {pedido.numeroFactura ? <>Factura <span className="cifras">{pedido.numeroFactura}</span></> : 'Sin factura'} · Montos con IVA incluido.
            </p>
          </Tarjeta>

          <Tarjeta como="section" className={estilosSolicitud.seccion} aria-labelledby={editando ? 'titulo-edicion-pedido' : 'titulo-datos-pedido'}>
            {editando ? (
              <EdicionPedido pedido={pedido} hoy={hoy} esGestion={esGestion} alCancelar={terminarEdicion}
                alDescartar={() => { consulta.recargar(); terminarEdicion(); }}
                alGuardar={() => { anunciar({ tono: 'exito', titulo: 'Guardamos los datos del pedido' }); consulta.recargar(); terminarEdicion(); }} />
            ) : (
              <>
                <div className={estilos.cabeceraTarjeta}>
                  <h2 id="titulo-datos-pedido" className={estilosSolicitud.subtitulo}>Datos del pedido</h2>
                  {!anulado && (
                    <Boton variante="secundario" tamano="compacto" iconoInicio={Pencil} aria-disabled={actualizando ? 'true' : undefined}
                      onClick={() => !actualizando && setEditando(true)}>
                      {pedido.estadoPedido === 'entregado' ? 'Editar factura' : 'Editar datos'}
                    </Boton>
                  )}
                </div>
                <dl className={estilosSolicitud.datos}>
                  <Dato termino="Cliente">
                    {cliente.idCliente ? <Enlace como={Link} to={`/panel/clientes/${cliente.idCliente}`}>{nombreDe(cliente)}</Enlace> : nombreDe(cliente)}
                  </Dato>
                  <Dato termino="Teléfono">
                    {cliente.telefono ? <a href={`tel:${String(cliente.telefono).replace(/[^+\d]/g, '')}`}>{telefonoVisible(cliente.telefono)}</a> : '—'}
                  </Dato>
                  <Dato termino="Responsable">{nombreDe(pedido.responsable)}</Dato>
                  <Dato termino="Fecha del pedido"><span className="cifras">{formatearFecha(pedido.fechaPedido)}</span></Dato>
                  <Dato termino="Entrega estimada"><span className="cifras">{formatearFecha(pedido.fechaEntregaEstimada)}</span></Dato>
                  <Dato termino="Entregado el">{pedido.fechaEntregaReal ? <span className="cifras">{formatearFecha(pedido.fechaEntregaReal)}</span> : 'Aún no se entrega'}</Dato>
                  <Dato termino="Modalidad">{MODALIDADES[pedido.modalidadEntrega] ?? pedido.modalidadEntrega ?? '—'}</Dato>
                  <Dato termino="Instalación">{pedido.requiereInstalacion ? 'Requiere instalación' : 'Sin instalación'}</Dato>
                  {pedido.direccionInstalacion && <Dato termino="Dirección">{pedido.direccionInstalacion}</Dato>}
                  {pedido.solicitud?.idSolicitud && (
                    <Dato termino="Solicitud">
                      <Enlace como={Link} to={`/panel/solicitudes/${pedido.solicitud.idSolicitud}`}>
                        <span className="cifras">{numeroSolicitud(pedido.solicitud)}</span>
                      </Enlace>
                    </Dato>
                  )}
                  {pedido.cotizacion?.idCotizacion && (
                    <Dato termino="Cotización">
                      <Enlace como={Link} to={`/panel/cotizaciones/${pedido.cotizacion.idCotizacion}`}>
                        <span className="cifras">{numeroCotizacion(pedido.cotizacion)}</span>
                      </Enlace>
                    </Dato>
                  )}
                </dl>
              </>
            )}
          </Tarjeta>

          <Tarjeta como="section" aria-labelledby="titulo-abonos" className={estilosSolicitud.seccion}>
            <div className={estilos.cabeceraTarjeta}>
              <h2 id="titulo-abonos" className={estilosSolicitud.subtitulo}>Abonos</h2>
              {puedeAbonar && (
                <Boton variante={principal || editando ? 'secundario' : 'primario'} iconoInicio={HandCoins} onClick={() => setDialogo('abono')}>Registrar abono</Boton>
              )}
            </div>
            <Abonos pagos={pedido.pagos} alAnular={(pago) => { setPagoElegido(pago); setDialogo('anular-abono'); }} />
          </Tarjeta>
        </div>

        <aside className={estilosSolicitud.fichaLateral} aria-label="Historial">
          <Tarjeta como="section" aria-labelledby="titulo-historial-pedido" className={estilosSolicitud.seccion}>
            <h2 id="titulo-historial-pedido" className={estilosSolicitud.subtitulo}>Historial de estados</h2>
            <HistorialPedido historial={pedido.historial} />
          </Tarjeta>
        </aside>
      </div>

      <DialogoAbono pedido={pedido} hoy={hoy} abierto={dialogo === 'abono'} alCerrar={() => setDialogo(null)} alRegistrar={registrarAbono}
        alRevisar={revisarAbono} />
      <DialogoAnularAbono pago={pagoElegido} abierto={dialogo === 'anular-abono'} alCerrar={() => setDialogo(null)} alAnular={anularAbono} />
      <DialogoEntrega pedido={pedido} hoy={hoy} abierto={dialogo === 'entrega'} alCerrar={() => setDialogo(null)} alEntregar={entregar} />
      <DialogoAnularPedido pedido={pedido} abierto={dialogo === 'anular'} alCerrar={() => setDialogo(null)} alAnular={anular} />
      <Confirmacion abierto={dialogo === 'eliminar'} titulo={`¿Eliminar el pedido ${numeroPedido(pedido)}?`} entidad="este pedido"
        descripcion="Deja de verse en el panel. Queda guardado en la base." accion="Eliminar pedido" variante="peligro" icono={Trash2}
        alCerrar={() => setDialogo(null)} alConfirmar={eliminar} />
    </div>
  );
}
