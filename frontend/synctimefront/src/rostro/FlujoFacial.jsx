import { useEffect, useRef, useState } from 'react';
import { ArrowLeft, ArrowRight, Video } from 'lucide-react';
import { Boton, Insignia } from '../componentes/ui';
import { ejecutarFlujo } from './guiaFacial';
import { abrirCamara, apagarCamara, detectarCuadro, ErrorFacial, prepararMotor } from './motorFacial';
import { useMovimientoReducido } from './useMovimientoReducido';
import estilos from './FlujoFacial.module.css';

const PASO = {
  frente: 'Mira de frente a la cámara',
  izquierda: 'Gira la cabeza hacia tu izquierda',
  derecha: 'Gira la cabeza hacia tu derecha',
};

const GUIA = {
  falta_luz: 'Falta luz',
  sin_rostro: 'Pon tu rostro dentro del óvalo',
  varios_rostros: 'Debe verse un solo rostro',
  acercate: 'Acércate',
  alejate: 'Aléjate',
  centra: 'Centra tu rostro',
  mira_al_frente: 'Mira de frente a la cámara',
  gira_mas: 'Gira un poco más',
  gira_menos: 'Gira un poco menos',
  listo: 'Muy bien, quédate así',
};

// La guía cambia con cada cuadro; el texto que se anuncia cambia a lo más cada este tiempo, para no saturar
// al lector de pantalla. Un paso nuevo (o su reintento) se anuncia de inmediato.
const PAUSA_ANUNCIO_MS = 1200;
const VISTA = { ancho: 240, alto: 180 };

function textoDe({ paso, intento, guia }) {
  if (guia && guia !== 'gira' && GUIA[guia]) return `${GUIA[guia]}.`;
  return `${intento > 1 ? 'Otra vez: ' : ''}${PASO[paso]}.`;
}

/** Copia local del cuadro, espejada como la vio la persona. Solo vive en la pantalla: nunca se envía. */
function tomarVista(video) {
  const lienzo = document.createElement('canvas');
  lienzo.width = VISTA.ancho;
  lienzo.height = VISTA.alto;
  const contexto = lienzo.getContext('2d');
  if (contexto) {
    contexto.translate(VISTA.ancho, 0);
    contexto.scale(-1, 1);
    contexto.drawImage(video, 0, 0, VISTA.ancho, VISTA.alto);
  }
  return lienzo;
}

/**
 * FlujoFacial
 * ———————————
 * Flujo guiado del rostro (fase 10) para ingresar, registrar y calibrar. Debe montarse solo como respuesta a
 * una acción de la persona: al montarse enciende la cámara, y la apaga al terminar, al fallar, al cancelar o al
 * desmontarse. El video se ve espejado; las coordenadas que se entregan son las del cuadro sin espejo.
 * Para empezar de nuevo, quien lo usa cambia su `key`.
 *
 * - `pasos`: ['frente', 'izquierda', 'derecha'] (o un subconjunto, para repetir una muestra).
 * - `prepararPasos(senal)`: en vez de `pasos`, los pide con la cámara ya lista (el desafío del ingreso).
 * - `alTerminar(capturas, vistas)`: capturas [{ paso, descriptor, puntos, ancho, alto }]; vistas { paso: canvas }
 *   solo con `conVistaPrevia`. Mientras quien lo usa verifica, se muestra «Verificando…» con la cámara apagada.
 * - `alError(error)`: ErrorFacial (cámara, detección, tiempo) o el error de `prepararPasos` (red).
 * - `alCancelar()`: la cámara ya quedó apagada.
 */
export default function FlujoFacial({ pasos, prepararPasos, rangos, conVistaPrevia = false, alTerminar, alError, alCancelar }) {
  const videoRef = useRef(null);
  const refCamara = useRef(null);
  const refControl = useRef(null);
  const reducido = useMovimientoReducido();
  const [fase, setFase] = useState('preparando');
  const [camaraActiva, setCamaraActiva] = useState(false);
  const [estado, setEstado] = useState(null);
  const [instruccion, setInstruccion] = useState('Preparando la cámara…');
  const ultimoAnuncio = useRef({ clave: '', texto: '', cuando: 0 });

  // Refs para que un cambio de estas props entre renders no apague y vuelva a encender la cámara.
  const props = useRef({ pasos, prepararPasos, rangos, conVistaPrevia, alTerminar, alError });
  useEffect(() => {
    props.current = { pasos, prepararPasos, rangos, conVistaPrevia, alTerminar, alError };
  });

  useEffect(() => {
    const control = new AbortController();
    refControl.current = control;
    const video = videoRef.current;
    let camara = null;

    const apagar = () => {
      apagarCamara(camara);
      if (refCamara.current === camara) refCamara.current = null;
      if (!control.signal.aborted) setCamaraActiva(false);
    };

    // Se llama con cada cuadro (unas 20 veces por segundo): solo se vuelve a dibujar si cambia algo visible.
    let visto = '';
    function anunciar(nuevo) {
      const huella = `${nuevo.indice}|${nuevo.intento}|${nuevo.guia}|${Math.round(nuevo.progreso * 20)}`;
      if (huella !== visto) {
        visto = huella;
        setEstado(nuevo);
      }
      const clave = `${nuevo.indice}-${nuevo.intento}`;
      const texto = textoDe(nuevo);
      const ahora = Date.now();
      const anterior = ultimoAnuncio.current;
      if (clave !== anterior.clave || (texto !== anterior.texto && ahora - anterior.cuando >= PAUSA_ANUNCIO_MS)) {
        ultimoAnuncio.current = { clave, texto, cuando: ahora };
        setInstruccion(texto);
      }
    }

    async function correr() {
      let resultado = null;
      // Los modelos se descargan mientras la persona da el permiso de la cámara.
      const motor = prepararMotor();
      motor.catch(() => {}); // el error se atiende abajo, al esperarlo
      try {
        camara = await abrirCamara();
        if (control.signal.aborted) return;
        refCamara.current = camara;
        setCamaraActiva(true);
        video.srcObject = camara;
        await video.play();
        await motor;
        if (control.signal.aborted) return;

        const { prepararPasos: preparar, pasos: fijos } = props.current;
        const lista = preparar ? await preparar(control.signal) : fijos;
        if (control.signal.aborted) return;
        setFase('flujo');

        const vistas = {};
        const capturas = await ejecutarFlujo({
          detectar: (opciones) => detectarCuadro(video, opciones),
          pasos: lista,
          rangos: props.current.rangos,
          senal: control.signal,
          alEstado: anunciar,
          alCaptura: (captura) => {
            if (props.current.conVistaPrevia) vistas[captura.paso] = tomarVista(video);
          },
        });
        apagar();
        if (control.signal.aborted) return;
        setFase('verificando');
        setInstruccion('Verificando…');
        resultado = { capturas, vistas };
      } catch (error) {
        apagar();
        if (!control.signal.aborted && !(error instanceof ErrorFacial && error.codigo === 'cancelado')) {
          props.current.alError(error);
        }
      } finally {
        // Si se canceló mientras se abría la cámara, la limpieza ya pasó y aquí se apaga.
        if (control.signal.aborted) apagarCamara(camara);
      }
      // Fuera del try: un fallo de quien verifica no es un error de la cámara ni de la detección.
      if (resultado && !control.signal.aborted) props.current.alTerminar(resultado.capturas, resultado.vistas);
    }

    correr();

    return () => {
      control.abort();
      apagarCamara(camara);
      refCamara.current = null;
      video.srcObject = null;
    };
  }, []);

  function cancelar() {
    // Con las capturas ya enviadas, cancelar no detendría la verificación: el botón queda inactivo.
    if (fase === 'verificando') return;
    refControl.current?.abort();
    apagarCamara(refCamara.current);
    refCamara.current = null;
    setCamaraActiva(false);
    alCancelar();
  }

  const enGiro = fase === 'flujo' && estado && estado.paso !== 'frente';
  const porcentaje = Math.round((estado?.progreso ?? 0) * 100);
  const Flecha = estado?.paso === 'izquierda' ? ArrowLeft : ArrowRight;

  return (
    <div className={estilos.flujo} data-movimiento={reducido ? 'reducido' : 'normal'} data-fase={fase}>
      <div className={estilos.escena}>
        {/* La guía en texto es la alternativa del video: el lector de pantalla no lo anuncia. */}
        <video ref={videoRef} className={estilos.video} muted playsInline aria-hidden="true" />
        <div className={estilos.ovalo} data-listo={estado?.guia === 'listo' ? 'true' : undefined} aria-hidden="true">
          {!reducido && <span className={estilos.escaneo} />}
        </div>
        {enGiro && (
          <span
            data-testid="flecha"
            data-lado={estado.paso}
            data-animada={reducido ? 'false' : 'true'}
            className={estilos.flecha}
            aria-hidden="true"
          >
            <Flecha size={36} strokeWidth={2.25} />
          </span>
        )}
        {camaraActiva && (
          <Insignia tono="info" icono={Video} className={estilos.indicador} aria-hidden="true">Cámara activa</Insignia>
        )}
      </div>

      <p role="status" className="solo-lectores">{camaraActiva ? 'La cámara está encendida.' : ''}</p>
      {/* El paso y la instrucción se anuncian juntos. */}
      <div className={estilos.textos} role="status" aria-live="polite" aria-atomic="true" data-testid="anuncio">
        {fase === 'flujo' && estado && (
          <p className={estilos.pasoActual}>Paso {estado.indice + 1} de {estado.total}</p>
        )}
        <p data-testid="instruccion" className={estilos.instruccion}>{instruccion}</p>
      </div>
      {enGiro && (
        // La barra es para quien ve: el avance ya se dice con la guía, y su valor cambia muchas veces por segundo.
        <div className={estilos.avance} aria-hidden="true">
          <div
            role="progressbar"
            aria-label="Avance del giro"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={porcentaje}
            className={estilos.barra}
          >
            <span className={estilos.relleno} style={{ inlineSize: `${porcentaje}%` }} />
          </div>
          <span className={estilos.porcentaje}>{porcentaje} %</span>
        </div>
      )}

      <Boton variante="secundario" onClick={cancelar} autoFocus aria-disabled={fase === 'verificando' ? 'true' : undefined}>
        Cancelar
      </Boton>
    </div>
  );
}
