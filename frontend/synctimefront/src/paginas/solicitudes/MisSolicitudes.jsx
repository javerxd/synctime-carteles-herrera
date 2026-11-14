import { useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, Paperclip, Plus } from 'lucide-react';
import { Aviso, Boton, Enlace, Esqueleto, EstadoSolicitud, EstadoVacio, Paginacion, Tarjeta } from '../../componentes/ui';
import { misSolicitudes } from '../../api/solicitudes';
import { formatearFechaHora } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { Comprobante } from './Comprobante';
import { formatearArea, numeroSolicitud } from './reglasSolicitud';
import estilos from './Solicitudes.module.css';

const POR_PAGINA = 10;
const cantidadItems = (s) => s.cantidadItems ?? s.detalles?.length ?? null;
const cantidadAdjuntos = (s) => s.cantidadAdjuntos ?? s.adjuntos?.length ?? 0;

function resumen(s) {
  const items = cantidadItems(s);
  return [
    items !== null && `${items} ${items === 1 ? 'letrero' : 'letreros'}`,
    s.areaTotalM2 ? formatearArea(s.areaTotalM2) : null,
  ].filter(Boolean).join(' · ');
}

/** /cuenta/solicitudes: las solicitudes del cliente con su estado; cada una abre su comprobante. */
export default function MisSolicitudes() {
  const [pagina, setPagina] = useState(1);
  const consulta = useConsulta(`mis-solicitudes:${pagina}`, () => misSolicitudes.listar({ pagina, tamano: POR_PAGINA }));
  const refTitulo = useRef(null);
  const lista = consulta.datos?.elementos ?? [];
  // Una página fuera de rango lleva a la última que existe.
  const ultima = consulta.datos?.totalPaginas ?? 0;
  if (ultima > 0 && pagina > ultima) setPagina(ultima);

  return (
    <div className={estilos.paginaCliente}>
      <div className={estilos.cabecera}>
        <div>
          <Enlace como={Link} to="/cuenta" className={estilos.volver}><ArrowLeft size={16} aria-hidden="true" />Mi cuenta</Enlace>
          <h1 ref={refTitulo} tabIndex={-1} className={estilos.titulo}>Mis solicitudes</h1>
          <p className={estilos.bajada}>El estado de cada solicitud y su comprobante.</p>
        </div>
        {lista.length > 0 && <Boton como={Link} to="/cuenta/solicitud" iconoInicio={Plus}>Nueva solicitud</Boton>}
      </div>

      {consulta.cargando && (
        <div className={estilos.listaTarjetas}>
          <p role="status" className="solo-lectores">Cargando tus solicitudes…</p>
          {[1, 2].map((i) => <Esqueleto key={i} forma="bloque" alto="6rem" />)}
        </div>
      )}

      {consulta.error && (
        <Aviso tono="peligro" titulo="No pudimos cargar tus solicitudes"
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {consulta.error.message}
        </Aviso>
      )}

      {consulta.datos && lista.length === 0 && (
        <EstadoVacio
          titulo="Aún no envías solicitudes"
          descripcion="Cuéntanos qué letrero necesitas y te respondemos con una cotización sin compromiso."
          accion={<Boton como={Link} to="/cuenta/solicitud" iconoInicio={Plus}>Solicitar una cotización</Boton>}
        />
      )}

      {lista.length > 0 && (
        <ul className={estilos.listaTarjetas}>
          {lista.map((s) => (
            <li key={s.idSolicitud}>
              <Tarjeta interactiva className={estilos.tarjetaSolicitud}>
                <div className={estilos.filaTarjeta}>
                  <Link to={`/cuenta/solicitudes/${s.idSolicitud}`} className={estilos.enlaceTarjeta}>
                    Solicitud <span className="cifras">{numeroSolicitud(s)}</span>
                  </Link>
                  <EstadoSolicitud estado={s.estado} />
                </div>
                <p className={estilos.metaTarjeta}>
                  <span className="cifras">{formatearFechaHora(s.fechaChile)}</span>
                  {resumen(s) && <> · {resumen(s)}</>}
                  {cantidadAdjuntos(s) > 0 && (
                    <>{' · '}<span className={estilos.clip}>
                      <Paperclip size={14} aria-hidden="true" />
                      {cantidadAdjuntos(s)} {cantidadAdjuntos(s) === 1 ? 'archivo' : 'archivos'}
                    </span></>
                  )}
                </p>
                {s.resumen && <p className={estilos.textoTarjeta}>{s.resumen}</p>}
                {s.estado === 'rechazado' && s.motivoRechazo && (
                  <p className={estilos.textoTarjeta}><strong>Motivo del rechazo:</strong> {s.motivoRechazo}</p>
                )}
              </Tarjeta>
            </li>
          ))}
        </ul>
      )}

      <Paginacion pagina={pagina} totalPaginas={consulta.datos?.totalPaginas ?? 0} etiqueta="Paginación de mis solicitudes"
        alCambiar={(numero) => { setPagina(numero); refTitulo.current?.focus(); }} />
    </div>
  );
}

/** /cuenta/solicitudes/:id: el comprobante de una solicitud del cliente. */
export function MiSolicitud() {
  const { id } = useParams();
  const consulta = useConsulta(`mi-solicitud:${id}`, () => misSolicitudes.obtener(id));
  const solicitud = consulta.datos;
  return (
    <div className={estilos.paginaCliente}>
      <div>
        <Enlace como={Link} to="/cuenta/solicitudes" className={estilos.volver}><ArrowLeft size={16} aria-hidden="true" />Mis solicitudes</Enlace>
        <h1 className={estilos.titulo}>
          {solicitud ? <>Solicitud <span className="cifras">{numeroSolicitud(solicitud)}</span></> : 'Solicitud'}
        </h1>
      </div>
      {consulta.cargando && (
        <Tarjeta>
          <p role="status" className="solo-lectores">Cargando el comprobante…</p>
          <Esqueleto lineas={6} />
        </Tarjeta>
      )}
      {consulta.error && (
        <Aviso tono="peligro" titulo={consulta.error.estado === 404 ? 'No encontramos esta solicitud' : 'No pudimos cargar el comprobante'}
          accion={consulta.error.estado === 404 ? null : <Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {consulta.error.estado === 404 ? 'Puede que el enlace esté incompleto. Revisa la lista de tus solicitudes.' : consulta.error.message}
        </Aviso>
      )}
      {solicitud && (
        <Tarjeta className={estilos.tarjetaComprobante}>
          <Comprobante solicitud={solicitud} />
        </Tarjeta>
      )}
    </div>
  );
}
