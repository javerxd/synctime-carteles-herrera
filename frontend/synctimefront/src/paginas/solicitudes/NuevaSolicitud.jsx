import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { CircleCheck, RotateCcw, Send } from 'lucide-react';
import { Adjuntos, Aviso, Boton, Campo, Esqueleto, Tarjeta } from '../../componentes/ui';
import authService from '../../api/authService';
import { cuenta as apiCuenta } from '../../api/cuenta';
import { publico } from '../../api/publico';
import { adjuntos as apiAdjuntos, misSolicitudes } from '../../api/solicitudes';
import { useAvisos } from '../../avisos/Avisos';
import { useSesion } from '../../sesion/sesion';
import { useConsulta } from '../../utilidades/useConsulta';
import { fallaDeClave, useClaveIdempotencia } from '../compartidos/useClaveIdempotencia';
import { Comprobante } from './Comprobante';
import { ContactoDirecto } from './ContactoDirecto';
import { EditorItems } from './EditorItems';
import { useSolicitudEditable } from './useSolicitudEditable';
import { LIMITES_SOLICITUD, numeroSolicitud } from './reglasSolicitud';
import estilos from './Solicitudes.module.css';

/** Aviso mientras el correo no esté confirmado: explica cómo hacerlo y ofrece otro enlace. */
function CorreoPorConfirmar({ correo, alComprobar, comprobando, refFoco }) {
  const { anunciar } = useAvisos();
  const [estado, setEstado] = useState({ enviando: false, mensaje: null, error: null });

  async function reenviar() {
    setEstado({ enviando: true, mensaje: null, error: null });
    try {
      const respuesta = await authService.reenviarVerificacion();
      const mensaje = respuesta?.mensaje ?? 'Te enviamos un enlace nuevo.';
      setEstado({ enviando: false, mensaje, error: null });
      anunciar({ tono: 'exito', titulo: 'Enlace enviado', texto: mensaje });
    } catch (error) {
      setEstado({ enviando: false, mensaje: null, error: error?.message || 'No pudimos enviar el enlace. Inténtalo de nuevo en unos minutos.' });
    }
  }

  return (
    <div ref={refFoco} tabIndex={-1} className={estilos.foco}>
      <Aviso tono="aviso" titulo="Confirma tu correo para enviar solicitudes">
        <p>
          Para evitar solicitudes falsas, primero confirma que {correo ? <strong>{correo}</strong> : 'tu correo'} es tuyo:
          abre el enlace que te enviamos (vence en 24 horas) y vuelve a esta página.
        </p>
        {estado.mensaje && <p>{estado.mensaje}</p>}
        {estado.error && <p><strong>{estado.error}</strong></p>}
        <div className={estilos.accionesContacto}>
          <Boton variante="secundario" tamano="compacto" iconoInicio={Send} onClick={reenviar} cargando={estado.enviando} textoCargando="Enviando…">
            Reenviar enlace
          </Boton>
          <Boton variante="secundario" tamano="compacto" iconoInicio={RotateCcw} onClick={alComprobar} cargando={comprobando} textoCargando="Comprobando…">
            Ya lo confirmé
          </Boton>
        </div>
      </Aviso>
    </div>
  );
}

const textoBorradores = (n) => `Opcional. JPG, PNG, WebP o PDF, de hasta 10 MB cada uno. Recuperamos ${n === 1 ? 'el archivo' : `los ${n} archivos`} que subiste antes y no enviaste; quita los que no quieras.`;

/** Lo que ve el cliente al terminar: el comprobante, también enviado a su correo. */
function SolicitudEnviada({ comprobante }) {
  const refTitulo = useRef(null);
  useEffect(() => {
    window.scrollTo?.(0, 0);
    refTitulo.current?.focus();
  }, []);
  return (
    <div className={estilos.paginaCliente}>
      <div className={estilos.exito}>
        <CircleCheck size={28} strokeWidth={1.75} aria-hidden="true" className={estilos.iconoExito} />
        <div>
          <h1 ref={refTitulo} tabIndex={-1} className={estilos.titulo}>Recibimos tu solicitud {numeroSolicitud(comprobante)}</h1>
          <p className={estilos.bajada}>
            Te enviamos este comprobante a tu correo. Revisaremos lo que necesitas y te responderemos con una cotización.
          </p>
        </div>
      </div>
      <Tarjeta className={estilos.tarjetaComprobante}>
        <Comprobante solicitud={comprobante} />
      </Tarjeta>
      <div className={estilos.accionesPie}>
        <Boton como={Link} to="/cuenta/solicitudes" variante="secundario">Ver mis solicitudes</Boton>
        <Boton como={Link} to="/" variante="fantasma">Volver a la portada</Boton>
      </div>
    </div>
  );
}

/**
 * /cuenta/solicitud (RFS04): el cliente con sesión y correo confirmado describe sus letreros, adjunta su
 * logo o diseño y envía. Termina en el comprobante. Si la regla contra solicitudes falsas lo frena,
 * muestra el contacto directo sin perder lo escrito. «Cotizar uno similar» llega con ?producto=&referencia=.
 */
export default function NuevaSolicitud() {
  const [parametros] = useSearchParams();
  const productoInicial = parametros.get('producto');
  // La referencia viene en la dirección: se recorta igual donde se muestra y donde se escribe.
  const referencia = parametros.get('referencia')?.slice(0, 120) || null;
  const { usuario } = useSesion();
  const datosCuenta = useConsulta('mi-cuenta', apiCuenta.obtener);
  const productos = useConsulta('productos', publico.productos);
  const { anunciar } = useAvisos();
  const {
    agregarItem, aplicarErroresApi, cambiarItem, comprobar, items, limpiarErrorApi, olvidar, paraApi, propiedades, quitarItem, refFormulario,
    restaurado, tocar, visibles,
  } = useSolicitudEditable({
    productoInicial,
    descripcionInicial: referencia ? `Me interesa un trabajo similar a «${referencia}». ` : '',
    // Lo escrito (sin adjuntos) se guarda en esta pestaña y vuelve si el cliente sale y regresa.
    guardarEn: usuario?.id ? `synctime_solicitud_${usuario.id}` : null,
  });
  // Los archivos que subió antes y no envió siguen como borradores: se muestran para verlos o quitarlos.
  const borradores = useConsulta('mis-adjuntos', misSolicitudes.borradores);
  const refEnviando = useRef(false);
  const [adjuntos, setAdjuntos] = useState({ listos: [], subiendo: false });
  const [estado, setEstado] = useState({ enviando: false, error: null, contacto: null, correoPendiente: false });
  const [comprobante, setComprobante] = useState(null);
  // Fase 11: la misma clave en cada reintento de este formulario; una nueva tras enviarlo.
  const idempotencia = useClaveIdempotencia();
  const refContacto = useRef(null);
  const refCorreo = useRef(null);
  const refError = useRef(null);
  const enfocar = useRef(null);

  // Tras responder la API, el foco va al aviso que explica qué pasó.
  useEffect(() => {
    const destino = enfocar.current;
    if (!destino || estado.enviando) return;
    enfocar.current = null;
    ({ contacto: refContacto, correo: refCorreo, error: refError })[destino]?.current?.focus();
  }, [estado]);

  if (comprobante) return <SolicitudEnviada comprobante={comprobante} />;

  const correoSinConfirmar = datosCuenta.datos && !datosCuenta.datos.correoVerificado;
  const mostrarFormulario = !datosCuenta.cargando && (!correoSinConfirmar || estado.correoPendiente);

  async function enviar(evento) {
    evento.preventDefault();
    // Un ref (síncrono) evita un segundo envío por doble clic antes de que el estado se actualice.
    if (refEnviando.current) return;
    if (!comprobar()) return;
    if (adjuntos.subiendo) {
      enfocar.current = 'error';
      setEstado((e) => ({ ...e, error: 'Espera a que terminen de subir tus archivos y vuelve a enviar.' }));
      return;
    }
    refEnviando.current = true;
    setEstado({ enviando: true, error: null, contacto: null, correoPendiente: estado.correoPendiente });
    try {
      const respuesta = await misSolicitudes.enviar({ ...paraApi(), adjuntos: adjuntos.listos.map((a) => a.idAdjunto) }, { clave: idempotencia.clave });
      idempotencia.renovar();
      olvidar();
      anunciar({ tono: 'exito', titulo: `Recibimos tu solicitud ${numeroSolicitud(respuesta)}` });
      setComprobante(respuesta);
    } catch (falla) {
      refEnviando.current = false;
      const error = fallaDeClave(falla, idempotencia.renovar);
      if (error?.codigo === 'contacto_directo') {
        enfocar.current = 'contacto';
        setEstado({ enviando: false, error: null, contacto: error.message, correoPendiente: false });
      } else if (error?.codigo === 'correo_sin_confirmar') {
        enfocar.current = 'correo';
        setEstado({ enviando: false, error: null, contacto: null, correoPendiente: true });
      } else if (aplicarErroresApi(error)) {
        setEstado({ enviando: false, error: null, contacto: null, correoPendiente: estado.correoPendiente });
      } else {
        enfocar.current = 'error';
        setEstado({ enviando: false, error: error?.message || 'No pudimos enviar tu solicitud. Inténtalo de nuevo.', contacto: null, correoPendiente: estado.correoPendiente });
      }
    }
  }

  return (
    <div className={estilos.paginaCliente}>
      <div>
        <h1 className={estilos.titulo}>Solicitar una cotización</h1>
        <p className={estilos.bajada}>Cuéntanos qué letreros necesitas y adjunta tu logo o diseño si lo tienes. Te respondemos con una cotización sin compromiso.</p>
      </div>

      {datosCuenta.cargando && (
        <Tarjeta>
          <p role="status" className="solo-lectores">Cargando…</p>
          <Esqueleto lineas={5} />
        </Tarjeta>
      )}

      {(correoSinConfirmar || estado.correoPendiente) && (
        <CorreoPorConfirmar correo={datosCuenta.datos?.correo} refFoco={refCorreo} comprobando={datosCuenta.recargando}
          alComprobar={() => { setEstado((e) => ({ ...e, correoPendiente: false })); datosCuenta.recargar(); }} />
      )}

      {estado.contacto !== null && <ContactoDirecto ref={refContacto} mensaje={estado.contacto} />}

      {mostrarFormulario && (
        <form ref={refFormulario} className={estilos.formulario} onSubmit={enviar} noValidate>
          {referencia && <p className={estilos.referencia}>Trabajo de referencia: <strong>{referencia}</strong></p>}
          {restaurado && (
            <p className={estilos.nota} role="status">Recuperamos lo que habías escrito en esta pestaña.</p>
          )}

          <Tarjeta como="section" aria-labelledby="titulo-letreros" className={estilos.seccion}>
            <h2 id="titulo-letreros" className={estilos.subtitulo}>Tus letreros</h2>
            <p className={estilos.explicacion}>Si no sabes las medidas exactas, déjalas en blanco o escribe unas aproximadas.</p>
            <EditorItems
              items={items}
              productos={productos.datos}
              cargandoProductos={productos.cargando}
              errores={visibles}
              alCambiar={cambiarItem}
              alTocar={tocar}
              alAgregar={agregarItem}
              alQuitar={quitarItem}
              deshabilitado={estado.enviando}
            />
            {productos.error && (
              <Aviso tono="peligro" titulo="No pudimos cargar los tipos de letrero"
                accion={<Boton variante="secundario" tamano="compacto" onClick={productos.recargar}>Reintentar</Boton>} />
            )}
          </Tarjeta>

          <Tarjeta como="section" aria-labelledby="titulo-descripcion" className={estilos.seccion}>
            <h2 id="titulo-descripcion" className={estilos.subtitulo}>Cuéntanos más</h2>
            <Campo etiqueta="Descripción" como="textarea" requerido rows={5} maxLength={LIMITES_SOLICITUD.descripcionMaxima}
              ayuda="Qué dice el letrero, colores, si es luminoso, dónde se instala y para cuándo lo necesitas."
              disabled={estado.enviando} {...propiedades('descripcionGeneral')} />
          </Tarjeta>

          <Tarjeta como="section" className={estilos.seccion}>
            {borradores.cargando ? (
              <>
                <p role="status" className="solo-lectores">Cargando tus archivos…</p>
                <Esqueleto forma="bloque" alto="9rem" />
              </>
            ) : (
            <Adjuntos
              iniciales={borradores.datos ?? []}
              {...(borradores.datos?.length ? { descripcion: textoBorradores(borradores.datos.length) } : {})}
              subir={misSolicitudes.subirAdjunto}
              quitar={misSolicitudes.quitarAdjunto}
              cargarMiniatura={apiAdjuntos.miniatura}
              cargarArchivo={apiAdjuntos.archivo}
              deshabilitado={estado.enviando}
              error={visibles.adjuntos}
              alCambiar={(listos, { subiendo }) => { setAdjuntos({ listos, subiendo }); limpiarErrorApi('adjuntos'); }}
            />
            )}
          </Tarjeta>

          <div ref={refError} tabIndex={-1} className={estilos.foco}>
            {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
          </div>

          <div className={estilos.envio}>
            <Boton type="submit" iconoInicio={Send} cargando={estado.enviando} textoCargando="Enviando…">Enviar solicitud</Boton>
            {datosCuenta.datos?.correo && (
              <p className={estilos.nota}>Te enviaremos el comprobante a <strong>{datosCuenta.datos.correo}</strong>.</p>
            )}
          </div>
        </form>
      )}
    </div>
  );
}
