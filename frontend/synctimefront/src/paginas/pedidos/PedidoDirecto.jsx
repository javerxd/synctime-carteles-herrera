import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, PackagePlus } from 'lucide-react';
import { Aviso, Boton, Campo, Enlace, Tarjeta } from '../../componentes/ui';
import { materiales as apiMateriales } from '../../api/cotizaciones';
import { publico } from '../../api/publico';
import { empleados as apiEmpleados, pedidos } from '../../api/pedidos';
import { useAvisos } from '../../avisos/Avisos';
import { useSesion } from '../../sesion/sesion';
import { hoyChile } from '../../utilidades/fechas';
import { formatearPesos } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { AvisoTrasCorte } from '../compartidos/AvisoTrasCorte';
import { bloqueaReintento, errorTrasCorte, sinRespuesta, TRAS_CORTE } from '../compartidos/trasCorte';
import { CostosCotizacion, EditorMateriales } from '../cotizaciones/EditorMateriales';
import { calcularTotales, cotizacionParaApi, pesosDesdeTexto } from '../cotizaciones/reglasCotizacion';
import { useCotizacionEditable } from '../cotizaciones/useCotizacionEditable';
import { EditorItems } from '../solicitudes/EditorItems';
import { BuscarCliente, ClienteElegido, CrearCliente } from '../solicitudes/panel/RegistroPresencial';
import { EDITAN_CLIENTES } from '../solicitudes/panel/reglasClientes';
import { LIMITES_SOLICITUD, nombreCompleto } from '../solicitudes/reglasSolicitud';
import { useSolicitudEditable } from '../solicitudes/useSolicitudEditable';
import { CamposAbono, CamposPedido } from './CamposPedido';
import { ResumenMontos } from './ResumenMontos';
import { abonoParaApi, costoInstalacion, datosPedidoParaApi, numeroPedido } from './reglasPedido';
import { usePedidoNuevo } from './usePedidoNuevo';
import estilosPanel from '../panel/Panel.module.css';
import estilosSolicitud from '../solicitudes/Solicitudes.module.css';
import estilos from './Pedidos.module.css';

/** Paso 1: buscar al cliente por teléfono o RUT y, si no existe, crearlo (rutas de la fase 7). */
function PasoCliente({ cliente, alElegir, alCambiar }) {
  const { tieneRol } = useSesion();
  const [paso, setPaso] = useState('buscar');
  const [busqueda, setBusqueda] = useState({});
  if (cliente) return <ClienteElegido cliente={cliente} alCambiar={() => { alCambiar(); setPaso('buscar'); }} />;
  if (paso === 'crear') {
    return tieneRol(EDITAN_CLIENTES) ? (
      <CrearCliente inicial={busqueda} alCrear={alElegir} alCancelar={() => setPaso('buscar')} paraQue="el pedido" />
    ) : (
      <>
        <Aviso tono="aviso" titulo="No hay un cliente con esos datos">
          Crear clientes lo hacen el dueño, el administrador o un operario. Pídeles que lo registren y vuelve a buscarlo.
        </Aviso>
        <div><Boton variante="secundario" onClick={() => setPaso('buscar')}>Buscar otra vez</Boton></div>
      </>
    );
  }
  return <BuscarCliente primario alEncontrar={alElegir} alNoEncontrar={(datos) => { setBusqueda(datos); setPaso('crear'); }} />;
}

/** Pasos 2 a 5, con el cliente ya elegido. */
function FormularioDirecto({ cliente, alCrear }) {
  const { usuario } = useSesion();
  const hoy = hoyChile();
  const productos = useConsulta('productos', publico.productos);
  const catalogo = useConsulta('materiales', apiMateriales.listar);
  const empleados = useConsulta('empleados', apiEmpleados.listar);
  const solicitud = useSolicitudEditable();
  const cotizacion = useCotizacionEditable({ materiales: catalogo.datos ?? [] });
  const trabajo = calcularTotales(cotizacion.datos).totalRedondeado;
  const { datos, abono, comprobar, aplicarError } = usePedidoNuevo({ hoy, trabajo, responsable: usuario?.idEmpleado });
  const { refFormulario: refSolicitud } = solicitud;
  const { refFormulario: refCotizacion } = cotizacion;
  const { refFormulario: refDatos } = datos;
  const { refFormulario: refAbono } = abono;
  const [estado, setEstado] = useState({ enviando: false, falla: null, candidato: null });
  const final = trabajo + costoInstalacion(datos.datos);

  // Al elegir el cliente, el foco va al primer letrero.
  const idCliente = cliente?.idCliente;
  useEffect(() => {
    if (idCliente) refSolicitud.current?.querySelector('select, input, textarea')?.focus();
  }, [idCliente, refSolicitud]);

  /**
   * Tras perder la respuesta: el pedido directo no tiene un registro previo que releer, así que se buscan los
   * pedidos directos de hoy del cliente por el mismo monto final. Si aparece uno, se muestra para que la persona
   * decida (ábrelo o crea otro); si no aparece, se puede reintentar; si no se pudo buscar, hay que revisar antes.
   */
  async function revisar() {
    setEstado({ enviando: true, falla: null, candidato: null });
    try {
      const recientes = await pedidos.listar({ idCliente: cliente.idCliente, orden: 'fecha_desc', pagina: 1, tamano: 10 });
      const candidato = (recientes?.elementos ?? []).find((p) => p.origen === 'directo'
        && String(p.fechaPedido ?? '').slice(0, 10) === hoy && Number(p.montoTotalFinal) === final);
      setEstado(candidato
        ? { enviando: false, falla: null, candidato }
        : { enviando: false, falla: errorTrasCorte('no_registrado', { que: 'se creó el pedido', donde: 'los pedidos del cliente' }), candidato: null });
    } catch {
      setEstado({ enviando: false, falla: errorTrasCorte('sin_confirmar', { que: 'se creó el pedido', donde: 'los pedidos del cliente' }), candidato: null });
    }
  }

  async function crear(evento) {
    evento.preventDefault();
    if (estado.enviando || !cliente || bloqueaReintento(estado.falla)) return;
    // De abajo hacia arriba: el foco queda en el primer error de la página.
    const bienPedido = comprobar();
    const bienCotizacion = cotizacion.comprobar();
    const bienSolicitud = solicitud.comprobar();
    if (!bienPedido || !bienCotizacion || !bienSolicitud) return;
    setEstado({ enviando: true, falla: null, candidato: null });
    try {
      const abonoInicial = abonoParaApi(abono.datos, { opcional: true });
      const ficha = await pedidos.crearDirecto({
        idCliente: cliente.idCliente,
        ...solicitud.paraApi(),
        ...cotizacionParaApi(cotizacion.datos),
        ...datosPedidoParaApi(datos.datos),
        ...(abonoInicial ? { abonoInicial } : {}),
      });
      alCrear(ficha);
    } catch (falla) {
      if (sinRespuesta(falla)) {
        await revisar();
        return;
      }
      const enCampos = [aplicarError(falla), cotizacion.aplicarErroresApi(falla), solicitud.aplicarErroresApi(falla)].some(Boolean);
      setEstado({ enviando: false, falla: enCampos ? null : falla, candidato: null });
    }
  }

  const montoAbono = pesosDesdeTexto(abono.datos.monto);
  const ocupado = estado.enviando;
  return (
    <div className={estilos.rejillaCrear}>
      <form id="formulario-directo" className={estilos.columnaCrear} onSubmit={crear} noValidate aria-label="Pedido directo">
        <Tarjeta como="section" aria-labelledby="titulo-letreros" className={estilosSolicitud.seccion} ref={refSolicitud}>
          <h2 id="titulo-letreros" className={estilosSolicitud.subtitulo}>2. Letreros y descripción</h2>
          <EditorItems items={solicitud.items} productos={productos.datos} cargandoProductos={productos.cargando} errores={solicitud.visibles}
            alCambiar={solicitud.cambiarItem} alTocar={solicitud.tocar} alAgregar={solicitud.agregarItem} alQuitar={solicitud.quitarItem} deshabilitado={ocupado} />
          <Campo etiqueta="Descripción del trabajo" como="textarea" requerido rows={3} maxLength={LIMITES_SOLICITUD.descripcionMaxima}
            ayuda="Lo acordado con el cliente: textos, colores y plazos." disabled={ocupado} {...solicitud.propiedades('descripcionGeneral')} />
        </Tarjeta>
        <Tarjeta className={estilosSolicitud.seccion} ref={refCotizacion}>
          <EditorMateriales editor={cotizacion} materiales={catalogo.datos ?? []} cargandoMateriales={catalogo.cargando} deshabilitado={ocupado}
            nivel={2} titulo="3. Materiales y costos" />
          <CostosCotizacion editor={cotizacion} deshabilitado={ocupado} nivel={3} />
        </Tarjeta>
        <Tarjeta como="section" aria-labelledby="titulo-datos-directo" className={estilosSolicitud.seccion} ref={refDatos}>
          <h2 id="titulo-datos-directo" className={estilosSolicitud.subtitulo}>4. Datos del pedido</h2>
          <CamposPedido form={datos} empleados={empleados} hoy={hoy} deshabilitado={ocupado} />
        </Tarjeta>
        <Tarjeta como="section" aria-labelledby="titulo-abono-directo" className={estilosSolicitud.seccion} ref={refAbono}>
          <div>
            <h2 id="titulo-abono-directo" className={estilosSolicitud.subtitulo}>5. Abono</h2>
            <p className={estilosSolicitud.nota}>Opcional. Déjalo en blanco si el cliente paga después.</p>
          </div>
          <CamposAbono form={abono} hoy={hoy} deshabilitado={ocupado} opcional prefijo="directo-" />
        </Tarjeta>
      </form>
      <aside className={estilos.columnaResumen} aria-label="Resumen y confirmación">
        <Tarjeta className={estilos.tarjetaResumen}>
          <ResumenMontos trabajo={trabajo} instalacion={costoInstalacion(datos.datos)} abono={Number.isInteger(montoAbono) ? montoAbono : 0} />
          {estado.candidato && (
            <Aviso tono="aviso" titulo="Se perdió la conexión y no supimos si se creó el pedido"
              accion={<Boton como={Link} to={`/panel/pedidos/${estado.candidato.idPedido}`} variante="secundario" tamano="compacto">
                Ver el pedido {numeroPedido(estado.candidato)}
              </Boton>}>
              Hoy ya hay un pedido directo de {nombreCompleto(cliente)} por <span className="cifras">{formatearPesos(estado.candidato.montoTotalFinal)}</span>.
              Si es el que acabas de crear, ábrelo; si no, puedes crear otro.
            </Aviso>
          )}
          <AvisoTrasCorte falla={estado.falla} alRevisar={revisar} revisando={estado.enviando} />
          {/* Un error común se anuncia solo (role="alert"); el foco no se mueve. */}
          {estado.falla && !Object.values(TRAS_CORTE).includes(estado.falla.codigo) && (
            <Aviso tono="peligro" titulo={estado.falla.message || 'No pudimos crear el pedido. Inténtalo de nuevo.'} />
          )}
          <Boton type="submit" form="formulario-directo" iconoInicio={PackagePlus} anchoCompleto cargando={ocupado} textoCargando="Creando…"
            aria-disabled={bloqueaReintento(estado.falla) ? 'true' : undefined}>
            {estado.candidato ? 'Crear otro pedido directo' : 'Crear pedido directo'}
          </Boton>
          <p className={estilosSolicitud.nota}>Crea la solicitud, la cotización aprobada y el pedido de una vez. No se envían correos.</p>
        </Tarjeta>
      </aside>
    </div>
  );
}

/**
 * /panel/pedidos/directo (RFU35, E26): para un trabajo acordado en el mostrador. Se busca o se crea al cliente
 * y luego van los letreros, los materiales y costos, los datos del pedido y el abono. La API crea en una
 * transacción la solicitud presencial, la cotización aprobada, el pedido «directo» y el abono.
 */
export default function PedidoDirecto() {
  const navegar = useNavigate();
  const { anunciar } = useAvisos();
  const [cliente, setCliente] = useState(null);

  function elegir(elegido) {
    setCliente(elegido);
    anunciar({ tono: 'exito', titulo: `Cliente: ${nombreCompleto(elegido)}` });
  }

  return (
    <div className={estilosPanel.pagina}>
      <div>
        <Enlace como={Link} to="/panel/pedidos" className={estilosSolicitud.volver}><ArrowLeft size={16} aria-hidden="true" />Pedidos</Enlace>
        <h1 className={estilosPanel.titulo}>Pedido directo en el taller</h1>
        <p className={estilosPanel.bajada}>Para un trabajo acordado en el mostrador, sin solicitud previa. Busca primero al cliente para no duplicarlo.</p>
      </div>
      <Tarjeta como="section" aria-labelledby="titulo-cliente-directo" className={`${estilosSolicitud.seccion} ${estilos.pasoCliente}`}>
        <h2 id="titulo-cliente-directo" className={estilosSolicitud.subtitulo}>1. Cliente</h2>
        <PasoCliente cliente={cliente} alElegir={elegir} alCambiar={() => setCliente(null)} />
      </Tarjeta>
      {!cliente && <p className={estilosSolicitud.explicacion}>Elige o crea al cliente para registrar el trabajo, los costos y el abono.</p>}
      {/* Queda montado aunque se cambie de cliente: lo escrito no se pierde. */}
      <div hidden={!cliente}>
        <FormularioDirecto cliente={cliente} alCrear={(ficha) => {
          anunciar({ tono: 'exito', titulo: `Pedido ${numeroPedido(ficha)} creado`, texto: 'Quedó «Aceptado».' });
          navegar(`/panel/pedidos/${ficha.idPedido}`, { replace: true });
        }} />
      </div>
    </div>
  );
}
