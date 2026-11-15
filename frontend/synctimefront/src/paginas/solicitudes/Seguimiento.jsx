import { useEffect, useRef, useState } from 'react';
import { Ban, Check, Circle, CircleCheck, CircleDot, FileCheck, FileX } from 'lucide-react';
import {
  Aviso, Boton, Campo, Dialogo, Esqueleto, EstadoCotizacion, EstadoPago, EstadoPedido, Tarjeta,
} from '../../componentes/ui';
import { misCotizaciones } from '../../api/cotizaciones';
import { useAvisos } from '../../avisos/Avisos';
import { unirClases } from '../../componentes/ui/clases';
import { formatearFecha, formatearPesos } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { AvisoFalla } from '../compartidos/Dialogos';
import { useEnvioDialogo } from '../compartidos/useEnvioDialogo';
import { LIMITES_COTIZACION, numeroCotizacion } from '../cotizaciones/reglasCotizacion';
import { VistaCotizacion } from '../cotizaciones/VistaCotizacion';
import { MODALIDADES, numeroPedido } from '../pedidos/reglasPedido';
import { pasosSeguimiento } from './reglasSeguimiento';
import estilos from './Solicitudes.module.css';

const ICONOS_PASO = { hecho: CircleCheck, ahora: CircleDot, despues: Circle, detenido: Ban };
const TEXTO_PASO = { hecho: 'Listo', ahora: 'En curso', despues: 'Pendiente', detenido: 'Detenido' };

/** La línea de avance de la solicitud del cliente. */
export function Seguimiento({ solicitud }) {
  const pasos = pasosSeguimiento(solicitud);
  return (
    <Tarjeta como="section" aria-labelledby="titulo-seguimiento" className={estilos.seccion}>
      <h2 id="titulo-seguimiento" className={estilos.subtitulo}>Seguimiento</h2>
      <ol className={estilos.pasosSeguimiento}>
        {pasos.map((p) => {
          const Icono = ICONOS_PASO[p.estado];
          return (
            <li key={p.id} className={unirClases(estilos.pasoSeguimiento, estilos[`paso_${p.estado}`])} aria-current={p.estado === 'ahora' ? 'step' : undefined}>
              <Icono size={20} strokeWidth={1.75} aria-hidden="true" className={estilos.iconoPaso} />
              <div className={estilos.textoPaso}>
                <p className={estilos.tituloPaso}>{p.titulo}</p>
                <p className={estilos.detallePaso}>
                  <span className={estilos.estadoPaso}>{TEXTO_PASO[p.estado]}</span>
                  {p.detalle ? ` · ${p.detalle}` : ''}
                </p>
              </div>
            </li>
          );
        })}
      </ol>
    </Tarjeta>
  );
}

/** Rechazar con un motivo opcional (máximo 500 caracteres). Rechazar cuesta lo mismo que aprobar. */
function DialogoRechazar({ cotizacion, abierto, alCerrar, alRechazar }) {
  const [motivo, setMotivo] = useState('');
  const envio = useEnvioDialogo(abierto);
  const refMotivo = useRef(null);
  const [abiertoAntes, setAbiertoAntes] = useState(abierto);
  if (abierto !== abiertoAntes) {
    setAbiertoAntes(abierto);
    if (abierto) setMotivo('');
  }
  const error = motivo.trim().length > LIMITES_COTIZACION.motivo ? 'Usa hasta 500 caracteres.' : null;
  async function enviar(evento) {
    evento.preventDefault();
    if (error) {
      refMotivo.current?.focus();
      return;
    }
    await envio.ejecutar(() => alRechazar(motivo.trim()));
  }
  return (
    <Dialogo abierto={abierto} alCerrar={() => !envio.enviando && alCerrar()} titulo={`¿Rechazar la cotización ${numeroCotizacion(cotizacion)}?`}
      descripcion="El taller verá tu respuesta y podrá enviarte otra cotización." enfoqueInicial={refMotivo} ancho="32rem"
      pie={(
        <>
          <Boton variante="secundario" onClick={alCerrar} disabled={envio.enviando}>Volver</Boton>
          <Boton type="submit" form="formulario-rechazar-cotizacion" variante="peligro" iconoInicio={FileX} cargando={envio.enviando} textoCargando="Enviando…">
            Rechazar cotización
          </Boton>
        </>
      )}>
      <form id="formulario-rechazar-cotizacion" onSubmit={enviar} noValidate className={estilos.formularioDialogo}>
        <AvisoFalla falla={envio.falla} alVerActual={alCerrar} entidad="esta cotización" paraCliente />
        <Campo ref={refMotivo} etiqueta="¿Por qué la rechazas?" como="textarea" rows={3} name="motivo" maxLength={LIMITES_COTIZACION.motivo}
          ayuda="Opcional. Nos ayuda a mejorar la propuesta, por ejemplo: «Necesito un tamaño menor»." value={motivo}
          onChange={(e) => setMotivo(e.target.value)} error={error ?? undefined} disabled={envio.enviando} />
      </form>
    </Dialogo>
  );
}

/** Confirmar la aprobación: «Al aprobar, el taller comienza tu trabajo». El foco empieza en «Volver». */
function DialogoAprobar({ cotizacion, abierto, alCerrar, alAprobar }) {
  const envio = useEnvioDialogo(abierto);
  const refVolver = useRef(null);
  const total = cotizacion.totalRedondeado ?? cotizacion.total;
  return (
    <Dialogo abierto={abierto} alCerrar={() => !envio.enviando && alCerrar()} titulo={`¿Aprobar la cotización ${numeroCotizacion(cotizacion)}?`}
      descripcion={`Al aprobar, el taller comienza tu trabajo. Total: ${formatearPesos(total)}, con IVA incluido.`}
      enfoqueInicial={refVolver} ancho="30rem"
      pie={(
        <>
          <Boton ref={refVolver} variante="secundario" onClick={alCerrar} disabled={envio.enviando}>Volver</Boton>
          <Boton iconoInicio={FileCheck} onClick={() => envio.ejecutar(alAprobar)} cargando={envio.enviando} textoCargando="Aprobando…">Aprobar cotización</Boton>
        </>
      )}>
      <AvisoFalla falla={envio.falla} alVerActual={alCerrar} entidad="esta cotización" paraCliente />
    </Dialogo>
  );
}

/**
 * La cotización del cliente (S06) con «Aprobar» y «Rechazar» mientras está pendiente (RFU08). Cada respuesta
 * envía la versión vista: si el taller la cambió mientras la mirabas, se recarga y se explica.
 */
export function CotizacionCliente({ resumen, versionSolicitud, alCambiar }) {
  const { anunciar } = useAvisos();
  // Tras responder se recarga: se sigue viendo la cotización (no vuelve al esqueleto) mientras llega la nueva.
  const consulta = useConsulta(`mi-cotizacion:${resumen.idCotizacion}:${versionSolicitud ?? ''}`, () => misCotizaciones.obtener(resumen.idCotizacion),
    { conservar: true });
  const [dialogo, setDialogo] = useState(null);
  const datos = consulta.datos;
  const cotizacion = datos?.idCotizacion === resumen.idCotizacion ? datos : null;
  const refTitulo = useRef(null);
  const enfocar = useRef(false);
  // Respondida, los botones desaparecen: el foco va al título de la cotización en vez de perderse.
  const estadoActual = cotizacion?.estado;
  useEffect(() => {
    if (enfocar.current && estadoActual && estadoActual !== 'pendiente') {
      enfocar.current = false;
      refTitulo.current?.focus();
    }
  }, [estadoActual]);

  async function responder(respuesta) {
    try {
      await misCotizaciones.responder(cotizacion.idCotizacion, respuesta, cotizacion.rowVersion);
    } catch (falla) {
      if (falla?.estado === 409) consulta.recargar();
      throw falla;
    }
    enfocar.current = true;
    setDialogo(null);
    anunciar(respuesta.decision === 'aprobada'
      ? { tono: 'exito', titulo: 'Aprobaste la cotización', texto: 'El taller comienza tu trabajo. Aquí verás el avance de tu pedido.' }
      : { tono: 'exito', titulo: 'Rechazaste la cotización', texto: 'Le avisamos al taller.' });
    alCambiar?.();
    consulta.recargar();
  }

  return (
    <Tarjeta como="section" aria-labelledby="titulo-mi-cotizacion" className={estilos.seccion}>
      <div className={estilos.cabeceraTarjeta}>
        <h2 id="titulo-mi-cotizacion" ref={refTitulo} tabIndex={-1} className={`${estilos.subtitulo} ${estilos.tituloEnfocable}`}>Tu cotización</h2>
        <EstadoCotizacion estado={cotizacion?.estado ?? resumen.estado} />
      </div>
      {!cotizacion && !consulta.error && (<><p role="status" className="solo-lectores">Cargando tu cotización…</p><Esqueleto lineas={6} /></>)}
      {consulta.error && !cotizacion && (
        <Aviso tono="peligro" titulo="No pudimos cargar tu cotización"
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {consulta.error.message}
        </Aviso>
      )}
      {cotizacion && (
        <>
          {cotizacion.estado === 'pendiente' && (
            <Aviso tono="info" titulo="Revisa la cotización y respóndenos">Puedes aprobarla para que comencemos o rechazarla si no te acomoda.</Aviso>
          )}
          <VistaCotizacion cotizacion={cotizacion} nivel={3} paraCliente />
          {cotizacion.idPedido && cotizacion.pedidoEliminado && (
            <Aviso tono="info" titulo={`El pedido #${cotizacion.idPedido} fue eliminado`}>Si tienes dudas, escríbenos o llámanos.</Aviso>
          )}
          {cotizacion.estado === 'pendiente' && (
            <div className={estilos.respuestaCliente}>
              <Boton iconoInicio={Check} onClick={() => setDialogo('aprobar')}>Aprobar</Boton>
              <Boton variante="secundario" iconoInicio={FileX} onClick={() => setDialogo('rechazar')}>Rechazar</Boton>
            </div>
          )}
          <DialogoAprobar cotizacion={cotizacion} abierto={dialogo === 'aprobar'} alCerrar={() => setDialogo(null)}
            alAprobar={() => responder({ decision: 'aprobada' })} />
          <DialogoRechazar cotizacion={cotizacion} abierto={dialogo === 'rechazar'} alCerrar={() => setDialogo(null)}
            alRechazar={(motivo) => responder({ decision: 'rechazada', ...(motivo ? { motivo } : {}) })} />
        </>
      )}
    </Tarjeta>
  );
}

/** El pedido del cliente: estado, pago, saldo, fecha estimada y modalidad (sin el historial interno). */
export function PedidoCliente({ pedido }) {
  const anulado = pedido.estadoPedido === 'anulado';
  const saldo = Number(pedido.saldoPendiente ?? 0);
  if (pedido.eliminado) {
    // Un pedido eliminado por el taller ya no tiene estados ni saldos que mostrar, ni acciones.
    return (
      <Tarjeta como="section" aria-labelledby="titulo-mi-pedido" className={estilos.seccion}>
        <h2 id="titulo-mi-pedido" className={estilos.subtitulo}>Tu pedido <span className="cifras">{numeroPedido(pedido)}</span></h2>
        <Aviso tono="info" titulo="Este pedido fue eliminado por el taller">Si tienes dudas, escríbenos o llámanos.</Aviso>
      </Tarjeta>
    );
  }
  return (
    <Tarjeta como="section" aria-labelledby="titulo-mi-pedido" className={estilos.seccion}>
      <div className={estilos.cabeceraTarjeta}>
        <h2 id="titulo-mi-pedido" className={estilos.subtitulo}>Tu pedido <span className="cifras">{numeroPedido(pedido)}</span></h2>
        <div className={estilos.insignias}>
          <EstadoPedido estado={pedido.estadoPedido} />
          <EstadoPago estado={pedido.estadoPago} />
        </div>
      </div>
      {pedido.cicloCerrado && <Aviso tono="exito" titulo="Entregado y pagado">Tu trabajo está cerrado.</Aviso>}
      <dl className={estilos.datos}>
        <div className={estilos.dato}><dt>Saldo pendiente</dt><dd className={unirClases('cifras', estilos.saldoCliente)}>{formatearPesos(anulado ? 0 : saldo)}</dd></div>
        <div className={estilos.dato}>
          <dt>Entrega estimada</dt>
          <dd className="cifras">{formatearFecha(pedido.fechaEntregaEstimada)}</dd>
        </div>
        <div className={estilos.dato}><dt>Modalidad</dt><dd>{MODALIDADES[pedido.modalidadEntrega] ?? pedido.modalidadEntrega ?? '—'}</dd></div>
      </dl>
      {anulado && <p className={estilos.explicacion}>Este pedido se anuló. Si tenías abonos, acuerda con el taller su devolución o que queden a tu favor.</p>}
    </Tarjeta>
  );
}
