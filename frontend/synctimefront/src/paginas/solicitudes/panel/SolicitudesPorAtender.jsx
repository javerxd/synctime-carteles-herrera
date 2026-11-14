import { Link } from 'react-router-dom';
import { ArrowRight, Paperclip } from 'lucide-react';
import { Aviso, Boton, Esqueleto, EstadoVacio, Insignia, Tarjeta } from '../../../componentes/ui';
import { solicitudes } from '../../../api/solicitudes';
import { formatearFechaHora } from '../../../utilidades/formato';
import { useConsulta } from '../../../utilidades/useConsulta';
import { nombreCompleto, numeroSolicitud, ORIGENES_SOLICITUD } from '../reglasSolicitud';
import { FILTRO_PENDIENTES } from './reglasBandeja';
import estilos from '../Solicitudes.module.css';

/**
 * Inicio del panel: las solicitudes por atender (estado «Solicitada»), de la más antigua a la más nueva,
 * para que nadie espere de más. Ahí llegan el diseñador y el operario al ingresar.
 */
export function SolicitudesPorAtender() {
  const consulta = useConsulta('pendientes-inicio', () => solicitudes.listar(FILTRO_PENDIENTES));
  const lista = consulta.datos?.elementos ?? [];
  const total = consulta.datos?.total ?? 0;

  return (
    <section aria-labelledby="titulo-pendientes">
      <div className={estilos.cabeceraSeccion}>
        <h2 id="titulo-pendientes" className={estilos.subtituloPanel}>
          Solicitudes por atender{consulta.datos ? <> <span className={estilos.cuenta}>({total})</span></> : ''}
        </h2>
        {total > 0 && (
          <Link to="/panel/solicitudes?estado=solicitado&orden=fecha_asc" className={estilos.verTodas}>
            Ver todas<ArrowRight size={16} aria-hidden="true" />
          </Link>
        )}
      </div>
      {consulta.cargando && (
        <div className={estilos.listaPendientes}>
          <p role="status" className="solo-lectores">Cargando las solicitudes por atender…</p>
          <Esqueleto forma="bloque" alto="4.5rem" />
          <Esqueleto forma="bloque" alto="4.5rem" />
        </div>
      )}
      {consulta.error && (
        <Aviso tono="peligro" titulo="No pudimos cargar las solicitudes por atender"
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {consulta.error.message}
        </Aviso>
      )}
      {consulta.datos && lista.length === 0 && (
        <EstadoVacio nivelTitulo={3} titulo="No hay solicitudes por atender"
          descripcion="Cuando llegue una nueva, aparecerá aquí y llegará un aviso al correo del taller." />
      )}
      {lista.length > 0 && (
        <ul className={estilos.listaPendientes}>
          {lista.map((s) => (
            <li key={s.idSolicitud}>
              <Tarjeta interactiva className={estilos.tarjetaSolicitud}>
                <div className={estilos.filaTarjeta}>
                  <Link to={`/panel/solicitudes/${s.idSolicitud}`} className={estilos.enlaceTarjeta}>
                    <span className="cifras">{numeroSolicitud(s)}</span> · {nombreCompleto(s.cliente)}
                  </Link>
                  <Insignia tono="neutro">{ORIGENES_SOLICITUD[s.origen] ?? s.origen}</Insignia>
                </div>
                <p className={estilos.metaTarjeta}>
                  <span className="cifras">{formatearFechaHora(s.fechaChile)}</span>
                  {s.cantidadItems ? ` · ${s.cantidadItems} ${s.cantidadItems === 1 ? 'letrero' : 'letreros'}` : ''}
                  {s.cantidadAdjuntos > 0 && (
                    <>{' · '}<span className={estilos.clip}>
                      <Paperclip size={14} aria-hidden="true" />{s.cantidadAdjuntos} {s.cantidadAdjuntos === 1 ? 'archivo' : 'archivos'}
                    </span></>
                  )}
                </p>
              </Tarjeta>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
