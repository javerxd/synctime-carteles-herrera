import { useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { abrirCamara, apagarCamara, capturarDescriptores } from '../rostro/motorFacial';
import './CapturaFacial.css';

const TEXTOS = {
  preparando: 'Preparando la cámara…',
  buscando: 'Mire a la cámara.',
  capturando: 'Mantenga la mirada en la cámara…',
  sin_rostro: 'No se detecta un rostro. Mire a la cámara con buena luz.',
  varios_rostros: 'Se detecta más de un rostro. Debe aparecer solo usted.',
};

/**
 * CapturaFacial
 * —————————————
 * Enciende la cámara solo mientras este componente está visible, y la apaga al terminar,
 * al fallar o al cancelar. Debe montarse únicamente como respuesta a una acción del usuario.
 * El video no se guarda ni se envía: solo salen los descriptores calculados en el navegador.
 */
export default function CapturaFacial({ cantidad = 1, onCaptura, onError, onCancelar }) {
  const videoRef = useRef(null);
  const [estado, setEstado] = useState({ tipo: 'preparando', avance: 0 });

  // Refs para que un cambio de estas funciones entre renders no apague y vuelva a encender la cámara.
  const avisos = useRef({ onCaptura, onError });
  useEffect(() => {
    avisos.current = { onCaptura, onError };
  });

  useEffect(() => {
    const control = new AbortController();
    const video = videoRef.current;
    let camara = null;

    async function capturar() {
      try {
        camara = await abrirCamara();
        if (control.signal.aborted) return;

        video.srcObject = camara;
        await video.play();
        // Sin esto, cancelar en este momento descargaría la librería y los modelos para nada.
        if (control.signal.aborted) return;
        setEstado({ tipo: 'buscando', avance: 0 });

        const descriptores = await capturarDescriptores(video, {
          cantidad,
          senal: control.signal,
          alEstado: setEstado,
        });
        apagarCamara(camara);
        if (!control.signal.aborted) avisos.current.onCaptura(descriptores);
      } catch (error) {
        apagarCamara(camara);
        if (!control.signal.aborted) avisos.current.onError(error);
      } finally {
        // Si se canceló mientras se abría la cámara, la limpieza ya pasó y aquí se apaga.
        if (control.signal.aborted) apagarCamara(camara);
      }
    }

    capturar();

    return () => {
      control.abort();
      apagarCamara(camara);
      video.srcObject = null;
    };
  }, [cantidad]);

  return (
    <div className="captura-facial">
      <video
        ref={videoRef}
        className="captura-facial-video"
        muted
        playsInline
        aria-label="Vista de la cámara"
      />
      <p className="captura-facial-estado" role="status" aria-live="polite">
        {TEXTOS[estado.tipo]}
        {cantidad > 1 && estado.avance > 0 ? ` (${estado.avance} de ${cantidad})` : ''}
      </p>
      <button type="button" className="btn btn-secondary" onClick={onCancelar} autoFocus>
        Cancelar
      </button>
    </div>
  );
}

CapturaFacial.propTypes = {
  cantidad: PropTypes.number,
  onCaptura: PropTypes.func.isRequired,
  onError: PropTypes.func.isRequired,
  onCancelar: PropTypes.func.isRequired,
};
