import { EstadoCotizacion } from '../../componentes/ui';
import { unirClases } from '../../componentes/ui/clases';
import { formatearFechaHora, formatearPesos } from '../../utilidades/formato';
import { formatearArea, medidasCm, nombreCompleto, numeroSolicitud } from '../solicitudes/reglasSolicitud';
import { formatearCantidad, numeroCotizacion, VIAS_RESPUESTA } from './reglasCotizacion';
import estilos from './Cotizaciones.module.css';

const nombreProducto = (item) => item.producto?.nombre ?? item.producto ?? 'Letrero';
const totalVisible = (c) => c.totalRedondeado ?? (c.total === null || c.total === undefined ? null : Math.round(Number(c.total)));

/** Quién respondió y por qué vía: «La aprobaste en la web» (cliente) o «El cliente la rechazó por teléfono» (personal). */
function Respuesta({ cotizacion, paraCliente }) {
  const r = cotizacion.respuesta;
  if (!r) return null;
  const aprobada = cotizacion.estado === 'aprobada';
  const via = r.via === 'cliente_web' ? 'en la web' : (VIAS_RESPUESTA[r.via] ?? r.via ?? '').toLowerCase();
  const sujeto = paraCliente
    ? (aprobada ? 'La aprobaste' : 'La rechazaste')
    : (aprobada ? 'El cliente la aprobó' : 'El cliente la rechazó');
  const registro = !paraCliente && r.registradaPor?.nombre ? ` (lo registró ${r.registradaPor.nombre})` : '';
  return (
    <div className={unirClases(estilos.respuesta, aprobada ? estilos.respuestaAprobada : estilos.respuestaRechazada)}>
      <p>
        <strong>{sujeto}</strong> {via}
        {r.fechaChile && <> el <span className="cifras">{formatearFechaHora(r.fechaChile)}</span></>}{registro}.
      </p>
      {!aprobada && r.motivo && <p>Motivo: {r.motivo}</p>}
    </div>
  );
}

/**
 * La cotización (S06): la misma para el cliente, para el personal y como vista previa del editor. Muestra los
 * letreros de la solicitud, el detalle de materiales, la mano de obra, otros costos y el total redondeado al
 * peso, con IVA incluido. `nivel` es el nivel de sus subtítulos. `paraCliente` oculta lo interno.
 */
export function VistaCotizacion({ cotizacion, nivel = 2, paraCliente = false, borrador = false, sinEncabezado = false, className }) {
  const Subtitulo = `h${nivel}`;
  const base = `cot-${cotizacion.idCotizacion ?? 'borrador'}`;
  const items = cotizacion.items ?? [];
  const materiales = (cotizacion.materiales ?? []).map((m, indice) => ({ ...m, indice }));
  const total = totalVisible(cotizacion);
  const conCentavos = materiales.some((m) => Math.round(Number(m.subtotal) * 100) % 100 !== 0);

  return (
    <div className={unirClases(estilos.vista, className)}>
      {!sinEncabezado && (
        <div className={estilos.encabezadoVista}>
          <p className={estilos.numeroVista}>
            {borrador && !cotizacion.numero ? 'Cotización (borrador)' : <>Cotización <span className="cifras">{numeroCotizacion(cotizacion)}</span></>}
          </p>
          {cotizacion.estado && <EstadoCotizacion estado={cotizacion.estado} />}
        </div>
      )}

      <dl className={estilos.meta}>
        {cotizacion.fechaChile && (
          <div><dt>Fecha (Chile)</dt><dd className="cifras">{formatearFechaHora(cotizacion.fechaChile)}</dd></div>
        )}
        {(cotizacion.numeroSolicitud || cotizacion.idSolicitud) && (
          <div><dt>Solicitud</dt><dd className="cifras">{cotizacion.numeroSolicitud ?? numeroSolicitud({ idSolicitud: cotizacion.idSolicitud })}</dd></div>
        )}
        {!paraCliente && cotizacion.cliente && <div><dt>Cliente</dt><dd>{nombreCompleto(cotizacion.cliente)}</dd></div>}
        {cotizacion.elaboradaPor?.nombre && <div><dt>Elaborada por</dt><dd>{cotizacion.elaboradaPor.nombre}</dd></div>}
      </dl>

      {items.length > 0 && (
        <section aria-labelledby={`${base}-letreros`} className={estilos.bloqueVista}>
          <Subtitulo id={`${base}-letreros`} className={estilos.subtituloVista}>{items.length === 1 ? 'Letrero' : `Letreros (${items.length})`}</Subtitulo>
          <ul className={estilos.letreros}>
            {items.map((item, i) => (
              // Los letreros no traen id y pueden repetirse: su posición los identifica.
              <li key={i}>
                <span className={estilos.letreroNombre}><span className="cifras">{item.cantidad ?? 1}</span> × {nombreProducto(item)}</span>
                <span className={estilos.detalle}>
                  <span className="cifras">{medidasCm(item.anchoMm, item.altoMm)}</span>
                  {item.areaM2 ? <> · <span className="cifras">{formatearArea(item.areaM2)}</span></> : null}
                  {item.descripcion ? ` · ${item.descripcion}` : ''}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section aria-labelledby={`${base}-materiales`} className={estilos.bloqueVista}>
        <Subtitulo id={`${base}-materiales`} className={estilos.subtituloVista}>Materiales</Subtitulo>
        {materiales.length ? (
          // Como una boleta: el material con su cantidad por el costo unitario, y el subtotal a la derecha.
          <ul className={estilos.lineasVista}>
            {materiales.map((m) => (
              <li key={`${m.indice}-${m.idMaterial ?? ''}`}>
                <span className={estilos.celdaDoble}>
                  <span className={estilos.nombreMaterial}>{m.material ?? 'Material'}</span>
                  <span className={estilos.detalle}>
                    <span className="cifras">{formatearCantidad(m.cantidad)}{m.unidad ? ` ${m.unidad}` : ''} × {formatearPesos(m.costoUnitario)}</span>
                    {m.medidaAplicada ? ` · ${m.medidaAplicada}` : ''}
                  </span>
                </span>
                <span className={`cifras ${estilos.subtotalVista}`}>
                  <span className="solo-lectores">Subtotal: </span>{formatearPesos(m.subtotal)}
                </span>
              </li>
            ))}
          </ul>
        ) : (
          <p className={estilos.ninguno}>Sin materiales detallados: el valor es de mano de obra y otros costos.</p>
        )}
      </section>

      <section aria-labelledby={`${base}-total`} className={estilos.bloqueVista}>
        <Subtitulo id={`${base}-total`} className="solo-lectores">Total</Subtitulo>
        <dl className={estilos.desglose}>
          <div><dt>Materiales</dt><dd className="cifras">{formatearPesos(cotizacion.costoMaterialesTotal)}</dd></div>
          <div><dt>Mano de obra</dt><dd className="cifras">{formatearPesos(cotizacion.costoManoObra)}</dd></div>
          <div><dt>Otros costos</dt><dd className="cifras">{formatearPesos(cotizacion.otrosCostos)}</dd></div>
          <div className={estilos.filaTotal}><dt>Total</dt><dd className="cifras">{formatearPesos(total)}</dd></div>
        </dl>
        <p className={estilos.notaTotal}>
          Valores en pesos, con IVA incluido.{conCentavos ? ' Los subtotales se muestran redondeados al peso.' : ''}
        </p>
      </section>

      {cotizacion.observaciones && (
        <section aria-labelledby={`${base}-observaciones`} className={estilos.bloqueVista}>
          <Subtitulo id={`${base}-observaciones`} className={estilos.subtituloVista}>Observaciones del taller</Subtitulo>
          <p className={estilos.observaciones}>{cotizacion.observaciones}</p>
        </section>
      )}

      <Respuesta cotizacion={cotizacion} paraCliente={paraCliente} />
    </div>
  );
}
