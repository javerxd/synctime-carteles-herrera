import { EstadoSolicitud, Insignia, ListaAdjuntos, TablaAdaptable } from '../../componentes/ui';
import { adjuntos as apiAdjuntos } from '../../api/solicitudes';
import { formatearFechaHora } from '../../utilidades/formato';
import {
  formatearArea, medidasCm, nombreCompleto, numeroSolicitud, ORIGENES_SOLICITUD, telefonoVisible,
} from './reglasSolicitud';
import estilos from './Solicitudes.module.css';

const nombreProducto = (detalle) => detalle.producto?.nombre ?? detalle.producto ?? 'Letrero';

function Dato({ termino, children }) {
  return (
    <div className={estilos.dato}>
      <dt>{termino}</dt>
      <dd>{children}</dd>
    </div>
  );
}

/**
 * Comprobante de la solicitud: el mismo en la pantalla del cliente, en el registro presencial y en la
 * ficha del personal (número, fecha y hora de Chile, cliente, letreros con medidas, área y cantidad,
 * archivos adjuntos y estado). `nivel` es el nivel de sus subtítulos, para respetar el orden de la página.
 * `sinAdjuntos`: la ficha del personal los muestra aparte, con su zona para agregar o quitar.
 */
export function Comprobante({ solicitud, nivel = 2, conContacto = false, sinAdjuntos = false, encabezado = true }) {
  const Subtitulo = `h${nivel}`;
  const cliente = solicitud.cliente ?? {};
  const detalles = (solicitud.detalles ?? []).map((d, indice) => ({ ...d, indice }));
  const telefono = cliente.telefono ? String(cliente.telefono) : '';
  const columnas = [
    {
      clave: 'producto', encabezado: 'Letrero', principal: true,
      render: (d) => (
        <span className={estilos.celdaLetrero}>
          <span>{nombreProducto(d)}</span>
          {d.descripcion && <span className={estilos.detalleLetrero}>{d.descripcion}</span>}
        </span>
      ),
    },
    { clave: 'medidas', encabezado: 'Medidas', render: (d) => <span className="cifras">{medidasCm(d.anchoMm, d.altoMm)}</span> },
    { clave: 'cantidad', encabezado: 'Cantidad', alinear: 'fin', render: (d) => <span className="cifras">{d.cantidad}</span> },
    { clave: 'area', encabezado: 'Área', alinear: 'fin', render: (d) => <span className="cifras">{d.areaM2 ? formatearArea(d.areaM2) : '—'}</span> },
  ];

  return (
    <div className={estilos.comprobante}>
      {encabezado && (
        <div className={estilos.encabezadoComprobante}>
          <p className={estilos.numero}><span className="cifras">{numeroSolicitud(solicitud)}</span></p>
          <div className={estilos.insignias}>
            <EstadoSolicitud estado={solicitud.estado} />
            {solicitud.origen && <Insignia tono="neutro">{ORIGENES_SOLICITUD[solicitud.origen] ?? solicitud.origen}</Insignia>}
          </div>
        </div>
      )}

      {solicitud.estado === 'rechazado' && solicitud.motivoRechazo && (
        <p className={estilos.motivoRechazo}><strong>Motivo del rechazo:</strong> {solicitud.motivoRechazo}</p>
      )}

      <dl className={estilos.datos}>
        <Dato termino="Fecha y hora (Chile)"><span className="cifras">{formatearFechaHora(solicitud.fechaChile)}</span></Dato>
        <Dato termino="Cliente">{nombreCompleto(cliente)}</Dato>
        <Dato termino="Teléfono">
          {conContacto && telefono ? <a href={`tel:${telefono.replace(/[^+\d]/g, '')}`}>{telefonoVisible(telefono)}</a> : telefonoVisible(telefono)}
        </Dato>
        <Dato termino="Correo">{cliente.correo || cliente.email || 'Sin correo'}</Dato>
      </dl>

      <section aria-labelledby={`letreros-${solicitud.idSolicitud}`} className={estilos.bloque}>
        <Subtitulo id={`letreros-${solicitud.idSolicitud}`} className={estilos.subtituloBloque}>
          {detalles.length === 1 ? 'Letrero' : `Letreros (${detalles.length})`}
        </Subtitulo>
        <TablaAdaptable titulo="Letreros de la solicitud" columnas={columnas} filas={detalles}
          claveFila={(d) => `${d.indice}-${d.idDetalle ?? ''}`} />
        <p className={estilos.total}>Área total: <strong className="cifras">{formatearArea(solicitud.areaTotalM2)}</strong></p>
      </section>

      <section aria-labelledby={`descripcion-${solicitud.idSolicitud}`} className={estilos.bloque}>
        <Subtitulo id={`descripcion-${solicitud.idSolicitud}`} className={estilos.subtituloBloque}>Descripción</Subtitulo>
        <p className={estilos.descripcionGeneral}>{solicitud.descripcionGeneral}</p>
      </section>

      {!sinAdjuntos && (
        <section aria-labelledby={`adjuntos-${solicitud.idSolicitud}`} className={estilos.bloque}>
          <Subtitulo id={`adjuntos-${solicitud.idSolicitud}`} className={estilos.subtituloBloque}>
            Archivos adjuntos{solicitud.adjuntos?.length ? ` (${solicitud.adjuntos.length})` : ''}
          </Subtitulo>
          <ListaAdjuntos adjuntos={solicitud.adjuntos ?? []} cargarMiniatura={apiAdjuntos.miniatura} cargarArchivo={apiAdjuntos.archivo} />
        </section>
      )}
    </div>
  );
}
