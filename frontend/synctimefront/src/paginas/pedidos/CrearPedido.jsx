import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, PackagePlus } from 'lucide-react';
import { Aviso, Boton, Enlace, Esqueleto, Tarjeta } from '../../componentes/ui';
import { cotizaciones } from '../../api/cotizaciones';
import { empleados as apiEmpleados, pedidos } from '../../api/pedidos';
import { useAvisos } from '../../avisos/Avisos';
import { useSesion } from '../../sesion/sesion';
import { hoyChile } from '../../utilidades/fechas';
import { useConsulta } from '../../utilidades/useConsulta';
import { AvisoTrasCorte } from '../compartidos/AvisoTrasCorte';
import { bloqueaReintento, errorTrasCorte, sinRespuesta, TRAS_CORTE } from '../compartidos/trasCorte';
import { numeroCotizacion, pesosDesdeTexto } from '../cotizaciones/reglasCotizacion';
import { nombreCompleto } from '../solicitudes/reglasSolicitud';
import { CamposAbono, CamposPedido } from './CamposPedido';
import { usePedidoNuevo } from './usePedidoNuevo';
import { ResumenMontos } from './ResumenMontos';
import {
  abonoParaApi, costoInstalacion, datosPedidoParaApi, numeroPedido,
} from './reglasPedido';
import estilosPanel from '../panel/Panel.module.css';
import estilosSolicitud from '../solicitudes/Solicitudes.module.css';
import estilos from './Pedidos.module.css';

/**
 * /panel/cotizaciones/:id/pedido (E08): el pedido nace «aceptado» desde una cotización aprobada y sin pedido,
 * con su responsable, fecha estimada, modalidad, instalación y un abono inicial opcional, todo en una
 * transacción. Todo el personal crea pedidos (RFU13).
 */
export default function CrearPedido() {
  const { id } = useParams();
  const navegar = useNavigate();
  const { anunciar } = useAvisos();
  const consulta = useConsulta(`cotizacion:${id}`, () => cotizaciones.obtener(id));
  const cotizacion = consulta.datos;
  const refTitulo = useRef(null);
  const cargada = Boolean(cotizacion);
  useEffect(() => {
    if (cargada) refTitulo.current?.focus();
  }, [cargada]);
  // Si al crear resultó que ya tenía pedido (409 y recarga), el formulario desaparece: el foco va al título,
  // que antecede al aviso con el enlace al pedido.
  const lista = cotizacion ? cotizacion.estado === 'aprobada' && !cotizacion.idPedido : null;
  const refListaAntes = useRef(lista);
  useEffect(() => {
    if (refListaAntes.current === true && lista === false) refTitulo.current?.focus();
    refListaAntes.current = lista;
  }, [lista]);

  const volver = (
    <Enlace como={Link} to={`/panel/cotizaciones/${id}`} className={estilosSolicitud.volver}>
      <ArrowLeft size={16} aria-hidden="true" />Cotización {cotizacion ? numeroCotizacion(cotizacion) : ''}
    </Enlace>
  );

  if (!cotizacion) {
    const noExiste = consulta.error?.estado === 404;
    return (
      <div className={estilosPanel.pagina}>
        <div>{volver}<h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>Crear pedido</h1></div>
        {consulta.cargando && (<><p role="status" className="solo-lectores">Cargando la cotización…</p><Esqueleto lineas={6} /></>)}
        {consulta.error && (
          <Aviso tono="peligro" titulo={noExiste ? 'No encontramos esta cotización' : 'No pudimos cargar la cotización'}
            accion={noExiste ? null : <Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
            {noExiste ? 'Puede que se haya eliminado o que el número no sea correcto.' : consulta.error.message}
          </Aviso>
        )}
      </div>
    );
  }

  const listo = lista;
  const eliminado = Boolean(cotizacion.idPedido && cotizacion.pedidoEliminado);
  return (
    <div className={estilosPanel.pagina}>
      <div>
        {volver}
        <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>
          Crear pedido de la cotización <span className="cifras">{numeroCotizacion(cotizacion)}</span>
        </h1>
        <p className={estilosPanel.bajada}>Para {nombreCompleto(cotizacion.cliente)}. Nace «Aceptado» con el monto de la cotización.</p>
      </div>
      {eliminado ? (
        <Aviso tono="aviso" titulo={`El pedido #${cotizacion.idPedido} fue eliminado`}
          accion={<Boton como={Link} to={`/panel/cotizaciones/${id}`} variante="secundario" tamano="compacto">Volver a la cotización</Boton>}>
          Una cotización da a lo más un pedido, aunque se haya eliminado.
        </Aviso>
      ) : !listo ? (
        <Aviso tono="aviso" titulo={cotizacion.idPedido ? 'Esta cotización ya tiene su pedido' : 'Solo una cotización aprobada da un pedido'}
          accion={cotizacion.idPedido
            ? <Boton como={Link} to={`/panel/pedidos/${cotizacion.idPedido}`} variante="secundario" tamano="compacto">Ver el pedido</Boton>
            : <Boton como={Link} to={`/panel/cotizaciones/${id}`} variante="secundario" tamano="compacto">Volver a la cotización</Boton>}>
          {cotizacion.idPedido ? 'Una cotización da a lo más un pedido.' : 'Registra primero la respuesta del cliente.'}
        </Aviso>
      ) : (
        <FormularioPedido cotizacion={cotizacion} alCrear={(ficha, { trasCorte = false } = {}) => {
          anunciar({
            tono: 'exito', titulo: `Pedido ${numeroPedido(ficha)} creado`,
            texto: trasCorte ? 'Se perdió la conexión, pero revisamos la cotización y el pedido sí se creó.' : 'Quedó «Aceptado».',
          });
          navegar(`/panel/pedidos/${ficha.idPedido}`, { replace: true });
        }} alRecargar={consulta.recargar} />
      )}
    </div>
  );
}

function FormularioPedido({ cotizacion, alCrear, alRecargar }) {
  const { usuario } = useSesion();
  const hoy = hoyChile();
  const empleados = useConsulta('empleados', apiEmpleados.listar);
  const trabajo = cotizacion.totalRedondeado ?? Math.round(Number(cotizacion.total ?? 0));
  const { datos, abono, comprobar, aplicarError } = usePedidoNuevo({ hoy, trabajo, responsable: usuario?.idEmpleado });
  const { refFormulario: refDatos } = datos;
  const { refFormulario: refAbono } = abono;
  const [estado, setEstado] = useState({ enviando: false, falla: null });

  /** ¿Se creó el pedido aunque se perdió la respuesta? La cotización releída ya trae su idPedido. */
  async function revisar() {
    setEstado({ enviando: true, falla: null });
    try {
      const actual = await cotizaciones.obtener(cotizacion.idCotizacion);
      if (actual?.idPedido) {
        alCrear({ idPedido: actual.idPedido, numero: `#${actual.idPedido}` }, { trasCorte: true });
        return;
      }
      setEstado({ enviando: false, falla: errorTrasCorte('no_registrado', { que: 'se creó el pedido', donde: 'la cotización' }) });
    } catch {
      setEstado({ enviando: false, falla: errorTrasCorte('sin_confirmar', { que: 'se creó el pedido', donde: 'la cotización' }) });
    }
  }

  async function crear(evento) {
    evento.preventDefault();
    if (estado.enviando || bloqueaReintento(estado.falla) || !comprobar()) return;
    setEstado({ enviando: true, falla: null });
    try {
      const abonoInicial = abonoParaApi(abono.datos, { opcional: true });
      const ficha = await pedidos.crearDesdeCotizacion(cotizacion.idCotizacion, {
        ...datosPedidoParaApi(datos.datos), ...(abonoInicial ? { abonoInicial } : {}),
      });
      alCrear(ficha);
    } catch (falla) {
      if (sinRespuesta(falla)) await revisar();
      else if (aplicarError(falla)) setEstado({ enviando: false, falla: null });
      else {
        setEstado({ enviando: false, falla });
        if (falla?.estado === 409) alRecargar();
      }
    }
  }

  const montoAbono = pesosDesdeTexto(abono.datos.monto);
  return (
    <form className={estilos.rejillaCrear} onSubmit={crear} noValidate aria-label="Crear pedido">
      <div className={estilos.columnaCrear}>
        <Tarjeta como="section" aria-labelledby="titulo-datos" className={estilosSolicitud.seccion} ref={refDatos}>
          <h2 id="titulo-datos" className={estilosSolicitud.subtitulo}>Datos del pedido</h2>
          <CamposPedido form={datos} empleados={empleados} hoy={hoy} deshabilitado={estado.enviando} />
        </Tarjeta>
        <Tarjeta como="section" aria-labelledby="titulo-abono" className={estilosSolicitud.seccion} ref={refAbono}>
          <div>
            <h2 id="titulo-abono" className={estilosSolicitud.subtitulo}>Abono inicial</h2>
            <p className={estilosSolicitud.nota}>Opcional. Déjalo en blanco si el cliente paga después.</p>
          </div>
          <CamposAbono form={abono} hoy={hoy} deshabilitado={estado.enviando} opcional prefijo="abono-" />
        </Tarjeta>
      </div>
      <aside className={estilos.columnaResumen} aria-label="Resumen y confirmación">
        <Tarjeta className={estilos.tarjetaResumen}>
          <ResumenMontos trabajo={trabajo} instalacion={costoInstalacion(datos.datos)} abono={Number.isInteger(montoAbono) ? montoAbono : 0} />
          <AvisoTrasCorte falla={estado.falla} alRevisar={revisar} revisando={estado.enviando} />
          {/* Un error común se anuncia solo (role="alert"); el foco no se mueve. */}
          {estado.falla && !Object.values(TRAS_CORTE).includes(estado.falla.codigo) && (
            <Aviso tono="peligro" titulo={estado.falla.message || 'No pudimos crear el pedido. Inténtalo de nuevo.'} />
          )}
          <Boton type="submit" iconoInicio={PackagePlus} anchoCompleto cargando={estado.enviando} textoCargando="Creando…"
            aria-disabled={bloqueaReintento(estado.falla) ? 'true' : undefined}>
            Crear pedido
          </Boton>
        </Tarjeta>
      </aside>
    </form>
  );
}
