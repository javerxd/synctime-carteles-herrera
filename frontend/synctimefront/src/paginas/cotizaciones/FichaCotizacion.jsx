import { useEffect, useRef } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { Aviso, Boton, Enlace, Esqueleto, EstadoCotizacion, Tarjeta } from '../../componentes/ui';
import { cotizaciones } from '../../api/cotizaciones';
import { useConsulta } from '../../utilidades/useConsulta';
import { nombreCompleto, numeroSolicitud, telefonoVisible } from '../solicitudes/reglasSolicitud';
import { AccionesCotizacion } from './AccionesCotizacion';
import { numeroCotizacion } from './reglasCotizacion';
import { VistaCotizacion } from './VistaCotizacion';
import estilosPanel from '../panel/Panel.module.css';
import estilosSolicitud from '../solicitudes/Solicitudes.module.css';

/**
 * /panel/cotizaciones/:id: la cotización (S06) con lo que se puede hacer según su estado: registrar la
 * respuesta del cliente, editarla, crear el pedido o ir a él, y eliminarla (gestión).
 */
export default function FichaCotizacion() {
  const { id } = useParams();
  const consulta = useConsulta(`cotizacion:${id}`, () => cotizaciones.obtener(id));
  const refTitulo = useRef(null);
  const cotizacion = consulta.datos;

  const cargada = Boolean(cotizacion);
  useEffect(() => {
    if (cargada) refTitulo.current?.focus();
  }, [id, cargada]);

  const volver = (
    <Enlace como={Link} to="/panel/cotizaciones" className={estilosSolicitud.volver}><ArrowLeft size={16} aria-hidden="true" />Cotizaciones</Enlace>
  );

  if (!cotizacion) {
    const noExiste = consulta.error?.estado === 404;
    return (
      <div className={estilosPanel.pagina}>
        <div>
          {volver}
          <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>Cotización</h1>
        </div>
        {consulta.cargando && (<><p role="status" className="solo-lectores">Cargando la cotización…</p><Esqueleto lineas={8} /></>)}
        {consulta.error && (
          <Aviso tono="peligro" titulo={noExiste ? 'No encontramos esta cotización' : 'No pudimos cargar la cotización'}
            accion={noExiste ? null : <Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
            {noExiste ? 'Puede que se haya eliminado o que el número no sea correcto.' : consulta.error.message}
          </Aviso>
        )}
      </div>
    );
  }

  const cliente = cotizacion.cliente ?? {};
  return (
    <div className={estilosPanel.pagina}>
      <div className={estilosPanel.cabecera}>
        <div>
          {volver}
          <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>
            Cotización <span className="cifras">{numeroCotizacion(cotizacion)}</span>
          </h1>
          <div className={estilosSolicitud.insignias}><EstadoCotizacion estado={cotizacion.estado} /></div>
        </div>
      </div>
      {consulta.error && <Aviso tono="peligro" titulo="No pudimos actualizar la cotización">{consulta.error.message}</Aviso>}

      <div className={estilosSolicitud.fichaRejilla}>
        <Tarjeta como="section" className={estilosSolicitud.seccion}>
          <h2 className="solo-lectores">Detalle de la cotización</h2>
          <VistaCotizacion cotizacion={cotizacion} nivel={3} sinEncabezado />
        </Tarjeta>

        <aside className={estilosSolicitud.fichaLateral} aria-label="Acciones de la cotización">
          <Tarjeta como="section" className={estilosSolicitud.seccion}>
            <h2 className={estilosSolicitud.subtitulo}>Acciones</h2>
            {consulta.recargando && <p className={estilosSolicitud.nota} role="status">Actualizando la cotización…</p>}
            <AccionesCotizacion cotizacion={cotizacion} alCambiar={consulta.recargar} anchoCompleto />
            {cotizacion.estado === 'rechazada' && (
              <p className={estilosSolicitud.explicacion}>Una cotización rechazada queda como historial. La solicitud se puede cotizar de nuevo.</p>
            )}
            <dl className={estilosSolicitud.datosLaterales}>
              <div className={estilosSolicitud.dato}>
                <dt>Solicitud</dt>
                <dd>
                  <Enlace como={Link} to={`/panel/solicitudes/${cotizacion.idSolicitud}`}>
                    Solicitud <span className="cifras">{cotizacion.numeroSolicitud ?? numeroSolicitud({ idSolicitud: cotizacion.idSolicitud })}</span>
                  </Enlace>
                </dd>
              </div>
              <div className={estilosSolicitud.dato}>
                <dt>Cliente</dt>
                <dd>
                  {cliente.idCliente
                    ? <Enlace como={Link} to={`/panel/clientes/${cliente.idCliente}`}>{nombreCompleto(cliente)}</Enlace>
                    : nombreCompleto(cliente)}
                </dd>
              </div>
              {cliente.telefono && (
                <div className={estilosSolicitud.dato}>
                  <dt>Teléfono</dt>
                  <dd><a href={`tel:${String(cliente.telefono).replace(/[^+\d]/g, '')}`}>{telefonoVisible(cliente.telefono)}</a></dd>
                </div>
              )}
            </dl>
          </Tarjeta>
        </aside>
      </div>
    </div>
  );
}
