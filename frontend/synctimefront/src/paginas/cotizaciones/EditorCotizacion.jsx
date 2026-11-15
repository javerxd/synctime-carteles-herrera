import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Save, Send } from 'lucide-react';
import { Aviso, Boton, Enlace, Esqueleto, Tarjeta, TEXTOS_ESTADO_COTIZACION } from '../../componentes/ui';
import { cotizaciones, materiales as apiMateriales } from '../../api/cotizaciones';
import { solicitudes } from '../../api/solicitudes';
import { useAvisos } from '../../avisos/Avisos';
import { useSesion } from '../../sesion/sesion';
import { formatearPesos } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { AvisoConflicto } from '../compartidos/AvisoConflicto';
import { AvisoTrasCorte } from '../compartidos/AvisoTrasCorte';
import { bloqueaReintento, errorTrasCorte, sinRespuesta, TRAS_CORTE } from '../compartidos/trasCorte';
import { useConflictoDeVersion } from '../compartidos/useConflictoDeVersion';
import { formatearArea, medidasCm, nombreCompleto, numeroSolicitud } from '../solicitudes/reglasSolicitud';
import { CostosCotizacion, EditorMateriales, TotalEnVivo } from './EditorMateriales';
import {
  borradorParaVista, cotizacionParaApi, formularioDesdeCotizacion, numeroCotizacion,
} from './reglasCotizacion';
import { useCotizacionEditable } from './useCotizacionEditable';
import { VistaCotizacion } from './VistaCotizacion';
import estilosPanel from '../panel/Panel.module.css';
import estilosSolicitud from '../solicitudes/Solicitudes.module.css';
import estilos from './Cotizaciones.module.css';

const nombreProducto = (d) => d.producto?.nombre ?? d.producto ?? 'Letrero';

/** Lo que pidió el cliente, para tenerlo a la vista mientras se cotiza. */
function LoPedido({ items = [], descripcion }) {
  return (
    <Tarjeta como="section" aria-labelledby="titulo-pedido" className={estilos.resumenSolicitud}>
      <h2 id="titulo-pedido" className={estilos.subtituloEditor}>Lo que pidió el cliente</h2>
      {items.length > 0 ? (
        <ul className={estilos.letreros}>
          {items.map((item, i) => (
            // Los letreros no traen id y pueden repetirse.
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
      ) : <p className={estilos.ninguno}>La solicitud no trae letreros detallados.</p>}
      {descripcion && <p className={estilos.observaciones}>{descripcion}</p>}
    </Tarjeta>
  );
}

/** El formulario y su vista previa. Se monta con los datos ya cargados (al editar, los de la cotización). */
function FormularioCotizacion({ inicial, catalogo, base, modo, rowVersion, alGuardar, alDescartar, volverA }) {
  const editor = useCotizacionEditable({ inicial, materiales: catalogo.datos ?? [] });
  const { comprobar, aplicarErroresApi, refFormulario, datos } = editor;
  const editar = modo === 'editar';
  const [estado, setEstado] = useState({ enviando: false, falla: null });
  const ultimo = useRef(null);
  const version = useConflictoDeVersion({ releer: () => cotizaciones.obtener(base.idCotizacion), versionInicial: rowVersion });
  const actual = version.conflicto?.actual;

  /**
   * Tras perder la respuesta al emitir: ¿quedó emitida? Relee la solicitud y busca una cotización pendiente que
   * no estaba. Devuelve la cotización encontrada, null si no está, o lanza si no se pudo releer.
   */
  async function buscarEmitida() {
    const solicitud = await solicitudes.obtener(base.idSolicitud);
    const antes = new Set((base.cotizacionesPrevias ?? []).map((c) => c.idCotizacion));
    return (solicitud?.cotizaciones ?? []).find((c) => !antes.has(c.idCotizacion) && c.estado === 'pendiente') ?? null;
  }

  async function revisarEmision() {
    setEstado({ enviando: true, falla: null });
    try {
      const emitida = await buscarEmitida();
      if (emitida) {
        alGuardar(emitida, { trasCorte: true });
        return;
      }
      setEstado({ enviando: false, falla: errorTrasCorte('no_registrado', { que: 'se emitió', donde: 'la solicitud' }) });
    } catch {
      setEstado({ enviando: false, falla: errorTrasCorte('sin_confirmar', { que: 'se emitió', donde: 'la solicitud' }) });
    }
  }

  /** `sobreActual`: el reintento explícito tras un conflicto, con la versión releída. */
  async function enviar(evento, sobreActual = false) {
    evento?.preventDefault();
    if (estado.enviando || (version.conflicto && !sobreActual) || bloqueaReintento(estado.falla) || !comprobar()) return;
    setEstado({ enviando: true, falla: null });
    ultimo.current = cotizacionParaApi(datos);
    try {
      const respuesta = editar
        ? await cotizaciones.editar(base.idCotizacion, ultimo.current, version.version)
        : await cotizaciones.emitir(base.idSolicitud, ultimo.current);
      setEstado({ enviando: false, falla: null });
      alGuardar(respuesta);
    } catch (falla) {
      if (falla?.codigo === 'version_obsoleta') {
        setEstado({ enviando: false, falla: null });
        version.detectar();
      } else if (!editar && sinRespuesta(falla)) {
        // Emitir no tiene idempotencia: antes de dejar reintentar se revisa si quedó emitida.
        await revisarEmision();
      } else if (aplicarErroresApi(falla)) setEstado({ enviando: false, falla: null });
      else setEstado({ enviando: false, falla });
    }
  }

  const borrador = borradorParaVista({ datos, materiales: catalogo.datos ?? [], base });
  const noSePuede = actual && actual.estado !== 'pendiente'
    ? `Ahora está «${TEXTOS_ESTADO_COTIZACION[actual.estado] ?? actual.estado}»: una cotización respondida ya no se edita.`
    : null;
  const resumen = actual
    ? `total ${formatearPesos(actual.totalRedondeado ?? actual.total)}, ${actual.materiales?.length ?? 0} ${actual.materiales?.length === 1 ? 'material' : 'materiales'}, mano de obra ${formatearPesos(actual.costoManoObra)}.`
    : null;
  const errorComun = estado.falla && !Object.values(TRAS_CORTE).includes(estado.falla.codigo) ? estado.falla : null;

  return (
    <div className={estilos.rejillaEditor}>
      <form ref={refFormulario} className={estilos.columnaEditor} onSubmit={enviar} noValidate aria-label={editar ? 'Editar cotización' : 'Nueva cotización'}>
        <LoPedido items={base.items} descripcion={base.descripcionGeneral} />
        {catalogo.error && (
          <Aviso tono="peligro" titulo="No pudimos cargar el catálogo de materiales"
            accion={<Boton variante="secundario" tamano="compacto" onClick={catalogo.recargar}>Reintentar</Boton>}>
            Puedes cotizar solo con mano de obra y otros costos, o reintentar.
          </Aviso>
        )}
        <Tarjeta>
          <EditorMateriales editor={editor} materiales={catalogo.datos ?? []} cargandoMateriales={catalogo.cargando} deshabilitado={estado.enviando} nivel={2} />
        </Tarjeta>
        <Tarjeta>
          <CostosCotizacion editor={editor} deshabilitado={estado.enviando} nivel={2} />
        </Tarjeta>

        <AvisoConflicto conflicto={version.conflicto} entidad="esta cotización" resumen={resumen} noSePuede={noSePuede}
          guardando={estado.enviando} refAccion={version.refAccion} alGuardarSobre={() => enviar(null, true)}
          alDescartar={() => alDescartar(actual)} alReintentar={version.detectar} />
        <AvisoTrasCorte falla={estado.falla} alRevisar={revisarEmision} revisando={estado.enviando} />
        {/* Un error común se anuncia solo (role="alert"); el foco no se mueve. */}
        {errorComun && <Aviso tono="peligro" titulo={errorComun.message || 'No pudimos guardar la cotización. Inténtalo de nuevo.'} />}

        <div className={estilos.pieEditor}>
          <TotalEnVivo datos={datos} />
          <div className={estilos.accionesPie}>
            <Boton como={Link} to={volverA} variante="secundario">Cancelar</Boton>
            <Boton type="submit" iconoInicio={editar ? Save : Send} cargando={estado.enviando} textoCargando={editar ? 'Guardando…' : 'Emitiendo…'}
              aria-disabled={version.conflicto || bloqueaReintento(estado.falla) ? 'true' : undefined}>
              {editar ? 'Guardar cambios' : 'Emitir cotización'}
            </Boton>
          </div>
        </div>
        {!editar && (
          <p className={estilos.nota}>
            Al emitirla, la solicitud pasa a «Cotizada» y el cliente recibe un correo para revisarla si tiene su correo confirmado.
          </p>
        )}
      </form>

      <aside className={estilos.columnaPrevia} aria-labelledby="titulo-previa">
        <Tarjeta className={estilos.previa}>
          <div>
            <h2 id="titulo-previa" className={estilos.tituloPrevia}>Vista previa para el cliente</h2>
            <p className={estilos.nota}>Así la verá en su cuenta. Se actualiza mientras escribes.</p>
          </div>
          <VistaCotizacion cotizacion={borrador} nivel={3} paraCliente borrador />
        </Tarjeta>
      </aside>
    </div>
  );
}

/**
 * Editor de cotización (RFS05, E06): /panel/solicitudes/:id/cotizar (nueva) y /panel/cotizaciones/:id/editar
 * (solo pendiente, con la versión vista). Todo el personal cotiza (RFU12). Materiales del catálogo con su costo
 * sugerido, mano de obra, otros costos y observaciones; el total se ve en vivo, junto con la vista previa de lo
 * que verá el cliente.
 */
export default function EditorCotizacion({ modo = 'cotizar' }) {
  const { id } = useParams();
  const navegar = useNavigate();
  const { anunciar } = useAvisos();
  const { usuario } = useSesion();
  const editar = modo === 'editar';
  const consulta = useConsulta(`${editar ? 'cotizacion' : 'solicitud'}:${id}`, () => (editar ? cotizaciones.obtener(id) : solicitudes.obtener(id)));
  const catalogo = useConsulta('materiales', apiMateriales.listar);
  const [version, setVersion] = useState(0);
  // Al descartar tras un conflicto, el formulario se monta de nuevo con la versión releída (sin esperar otra carga).
  const [releida, setReleida] = useState(null);
  const [esperando, setEsperando] = useState(false);
  if (esperando && !consulta.recargando && !consulta.cargando) {
    setEsperando(false);
    setVersion((v) => v + 1);
  }
  const refTitulo = useRef(null);
  const datos = releida ?? consulta.datos;

  const cargada = Boolean(datos);
  useEffect(() => {
    if (cargada) refTitulo.current?.focus();
  }, [cargada]);

  const volverA = editar ? `/panel/cotizaciones/${id}` : `/panel/solicitudes/${id}`;
  const enlaceVolver = (
    <Enlace como={Link} to={volverA} className={estilosSolicitud.volver}>
      <ArrowLeft size={16} aria-hidden="true" />
      {editar ? `Cotización ${datos ? numeroCotizacion(datos) : ''}` : `Solicitud ${datos ? numeroSolicitud(datos) : ''}`}
    </Enlace>
  );
  const titulo = !datos
    ? (editar ? 'Editar cotización' : 'Cotizar solicitud')
    : editar
      ? <>Editar la cotización <span className="cifras">{numeroCotizacion(datos)}</span></>
      : <>Cotizar la solicitud <span className="cifras">{numeroSolicitud(datos)}</span></>;

  if (!datos) {
    const noExiste = consulta.error?.estado === 404;
    return (
      <div className={estilosPanel.pagina}>
        <div>
          {enlaceVolver}
          <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>{titulo}</h1>
        </div>
        {consulta.cargando && (
          <>
            <p role="status" className="solo-lectores">Cargando…</p>
            <Esqueleto lineas={8} />
          </>
        )}
        {consulta.error && (
          <Aviso tono="peligro" titulo={noExiste ? (editar ? 'No encontramos esta cotización' : 'No encontramos esta solicitud') : 'No pudimos cargar los datos'}
            accion={noExiste ? null : <Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
            {noExiste ? 'Puede que se haya eliminado o que el número no sea correcto.' : consulta.error.message}
          </Aviso>
        )}
      </div>
    );
  }

  const noSePuede = editar ? datos.estado !== 'pendiente' : datos.estado !== 'solicitado';
  const base = editar
    ? {
      idCotizacion: datos.idCotizacion, idSolicitud: datos.idSolicitud, numero: datos.numero, numeroSolicitud: datos.numeroSolicitud,
      fechaChile: datos.fechaChile, estado: 'pendiente', items: datos.items ?? [], cliente: datos.cliente, elaboradaPor: datos.elaboradaPor,
    }
    : {
      idSolicitud: datos.idSolicitud, numeroSolicitud: numeroSolicitud(datos), estado: 'pendiente', items: datos.detalles ?? [],
      cotizacionesPrevias: datos.cotizaciones ?? [],
      cliente: datos.cliente, elaboradaPor: { nombre: usuario?.nombre ?? '' }, descripcionGeneral: datos.descripcionGeneral,
    };

  function alGuardar(respuesta, { trasCorte = false } = {}) {
    const numero = numeroCotizacion(respuesta) || numeroCotizacion(datos);
    anunciar({
      tono: 'exito',
      titulo: editar ? `Guardamos la cotización ${numero}` : `Cotización ${numero} emitida`,
      texto: trasCorte ? 'Se perdió la conexión, pero revisamos la solicitud y la cotización sí quedó emitida.' : editar ? undefined : 'La solicitud quedó «Cotizada».',
    });
    navegar(`/panel/cotizaciones/${respuesta?.idCotizacion ?? datos.idCotizacion ?? ''}`, { replace: true });
  }

  return (
    <div className={estilosPanel.pagina}>
      <div className={estilosPanel.cabecera}>
        <div>
          {enlaceVolver}
          <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>{titulo}</h1>
          <p className={estilosPanel.bajada}>Para {nombreCompleto(datos.cliente)}. Los montos van en pesos, con IVA incluido.</p>
        </div>
      </div>
      {consulta.error && <Aviso tono="peligro" titulo="No pudimos actualizar los datos">{consulta.error.message}</Aviso>}
      {noSePuede ? (
        <Aviso tono="aviso" titulo={editar ? 'Esta cotización ya no se puede editar' : 'Esta solicitud ya no se puede cotizar'}
          accion={<Boton como={Link} to={volverA} variante="secundario" tamano="compacto">Volver</Boton>}>
          {editar
            ? 'Solo se edita una cotización pendiente. Una aprobada o rechazada queda como historial.'
            : 'Solo se cotiza una solicitud «Solicitada». Si ya tiene una cotización pendiente, edítala o espera la respuesta del cliente.'}
        </Aviso>
      ) : (
        <FormularioCotizacion
          key={version}
          inicial={editar ? formularioDesdeCotizacion(datos) : undefined}
          catalogo={catalogo}
          base={base}
          modo={modo}
          rowVersion={datos.rowVersion}
          volverA={volverA}
          alGuardar={alGuardar}
          alDescartar={(actual) => {
            if (actual) {
              setReleida(actual);
              setVersion((v) => v + 1);
            } else {
              // Sin la versión actual a mano: se carga y el formulario se monta cuando llegue.
              setReleida(null);
              consulta.recargar();
              setEsperando(true);
            }
          }}
        />
      )}
    </div>
  );
}
