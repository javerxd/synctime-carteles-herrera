import { Link } from 'react-router-dom';
import { ClipboardPen } from 'lucide-react';
import { Aviso, Boton, Esqueleto, EstadoCotizacion, Tarjeta } from '../../componentes/ui';
import { cotizaciones } from '../../api/cotizaciones';
import { formatearFechaHora, formatearPesos } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { AccionesCotizacion } from './AccionesCotizacion';
import { cotizacionesDeSolicitud, numeroCotizacion, textoRespuesta } from './reglasCotizacion';
import estilos from './Cotizaciones.module.css';

const totalDe = (c) => c?.totalRedondeado ?? c?.total;

/**
 * Sección «Cotización» de la ficha de la solicitud (fase 8): la vigente con sus acciones y las anteriores.
 * «Cotizar» aparece mientras la solicitud está «Solicitada»; «Registrar respuesta del cliente» con una
 * pendiente; «Crear pedido» con una aprobada sin pedido, y el enlace al pedido cuando ya existe.
 */
export function SeccionCotizacion({ solicitud, alCambiar }) {
  // La ficha trae sus cotizaciones (fase 8): { idCotizacion, numero, fechaChile, total, estado, idPedido }.
  const lista = cotizacionesDeSolicitud(solicitud);
  const [vigente, ...anteriores] = lista;
  // La vigente se lee entera: trae su versión (If-Match) y si ya tiene pedido. Cambia con la versión de la solicitud.
  const detalle = useConsulta(`cotizacion:${vigente?.idCotizacion ?? 'ninguna'}:${solicitud.rowVersion ?? ''}`,
    () => (vigente ? cotizaciones.obtener(vigente.idCotizacion) : null), { conservar: true });
  // Solo la de esta misma cotización: al cambiar de vigente no se muestran (ni se usan) datos de otra.
  const cotizacion = vigente && detalle.datos?.idCotizacion === vigente.idCotizacion ? detalle.datos : null;
  const puedeCotizar = solicitud.estado === 'solicitado';

  return (
    <Tarjeta como="section" aria-labelledby="titulo-cotizacion" className={estilos.seccionCotizacion}>
      <div className={estilos.cabeceraSeccion}>
        <h2 id="titulo-cotizacion" className={estilos.subtituloEditor}>Cotización</h2>
        {puedeCotizar && (
          <Boton como={Link} to={`/panel/solicitudes/${solicitud.idSolicitud}/cotizar`} iconoInicio={ClipboardPen}>
            {vigente ? 'Cotizar de nuevo' : 'Cotizar'}
          </Boton>
        )}
      </div>

      {!lista.length && (
        <p className={estilos.ninguno}>
          {puedeCotizar
            ? 'Aún no tiene cotización. Arma una con los materiales del catálogo, la mano de obra y otros costos.'
            : 'Esta solicitud no tiene cotizaciones.'}
        </p>
      )}

      {vigente && (
        <div className={estilos.vigente}>
          <div className={estilos.filaVigente}>
            <Link to={`/panel/cotizaciones/${vigente.idCotizacion}`} className={estilos.enlaceFila}>
              Cotización <span className="cifras">{numeroCotizacion(vigente)}</span>
            </Link>
            <EstadoCotizacion estado={cotizacion?.estado ?? vigente.estado} />
            <span className={`${estilos.montoVigente} ${estilos.montoFila}`}>{formatearPesos(totalDe(cotizacion ?? vigente))}</span>
          </div>
          {cotizacion && (
            <p className={estilos.nota}>
              Emitida el <span className="cifras">{formatearFechaHora(cotizacion.fechaChile)}</span>
              {cotizacion.elaboradaPor?.nombre ? ` por ${cotizacion.elaboradaPor.nombre}` : ''}.
              {cotizacion.respuesta ? ` ${textoRespuesta(cotizacion, formatearFechaHora)}.` : ''}
            </p>
          )}
          {detalle.cargando && (<><p role="status" className="solo-lectores">Cargando la cotización…</p><Esqueleto lineas={2} /></>)}
          {detalle.error && (
            <Aviso tono="peligro" titulo="No pudimos cargar la cotización"
              accion={<Boton variante="secundario" tamano="compacto" onClick={detalle.recargar}>Reintentar</Boton>} />
          )}
          {cotizacion && (
            <AccionesCotizacion cotizacion={cotizacion} conVer alCambiar={() => { alCambiar?.(); detalle.recargar(); }}
              alEliminar={alCambiar} />
          )}
        </div>
      )}

      {anteriores.length > 0 && (
        <div>
          <h3 className={estilos.subtituloVista}>Cotizaciones anteriores</h3>
          <ul className={estilos.historialCotizaciones}>
            {anteriores.map((c) => (
              <li key={c.idCotizacion}>
                <Link to={`/panel/cotizaciones/${c.idCotizacion}`} className={estilos.enlaceFila}>
                  <span className="cifras">{numeroCotizacion(c)}</span>
                </Link>
                <span className="cifras">{formatearFechaHora(c.fechaChile)}</span>
                <EstadoCotizacion estado={c.estado} />
                {c.idPedido && (c.pedidoEliminado
                  ? <span className={estilos.nota}>Pedido <span className="cifras">#{c.idPedido}</span> eliminado</span>
                  : <Link to={`/panel/pedidos/${c.idPedido}`}>Pedido <span className="cifras">#{c.idPedido}</span></Link>)}
                <span className={estilos.montoFila}>{formatearPesos(totalDe(c))}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </Tarjeta>
  );
}
