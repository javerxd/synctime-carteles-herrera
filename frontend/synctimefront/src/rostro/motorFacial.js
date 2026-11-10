/**
 * Único módulo que conoce la librería de reconocimiento facial.
 * La librería y sus modelos se descargan la primera vez que alguien usa la función,
 * para no hacer más lento el login normal. Los modelos se sirven desde el propio sitio.
 *
 * @vladmandic/face-api tiene su repositorio archivado. Cambiarla exigiría tocar este módulo,
 * el modelo del servidor (RostroFacialService) y volver a activar cada rostro.
 */
const RUTA_MODELOS = '/models';
const TIEMPO_MAXIMO_MS = 20000;
const PAUSA_ENTRE_INTENTOS_MS = 150;

export class ErrorFacial extends Error {
  constructor(codigo, causa) {
    super(codigo, { cause: causa });
    this.name = 'ErrorFacial';
    this.codigo = codigo;
  }
}

const MENSAJES = {
  permiso_denegado: 'El navegador no tiene permiso para usar la cámara. Permítalo, o entre con su contraseña.',
  sin_camara: 'No se encontró una cámara en este equipo. Entre con su contraseña.',
  camara_ocupada: 'Otro programa está usando la cámara. Ciérrelo e intente de nuevo, o entre con su contraseña.',
  no_soportado: 'Este navegador no permite usar la cámara en esta página. Entre con su contraseña.',
  sin_rostro: 'No se detectó ningún rostro. Mire a la cámara con buena luz e intente de nuevo, o entre con su contraseña.',
  varios_rostros: 'Se detectó más de un rostro. Debe aparecer solo usted en la imagen.',
  carga: 'No se pudieron cargar los archivos de reconocimiento. Revise la conexión, o entre con su contraseña.',
};

export function mensajeDeError(error) {
  return MENSAJES[error?.codigo] || 'No se pudo usar el reconocimiento facial. Entre con su contraseña.';
}

export async function abrirCamara() {
  if (!navigator.mediaDevices?.getUserMedia) {
    throw new ErrorFacial('no_soportado');
  }
  try {
    return await navigator.mediaDevices.getUserMedia({
      video: { facingMode: 'user', width: { ideal: 640 }, height: { ideal: 480 } },
      audio: false,
    });
  } catch (error) {
    throw new ErrorFacial(codigoDeCamara(error), error);
  }
}

function codigoDeCamara(error) {
  switch (error?.name) {
    case 'NotAllowedError':
    case 'SecurityError':
      return 'permiso_denegado';
    case 'NotFoundError':
    case 'OverconstrainedError':
      return 'sin_camara';
    case 'NotReadableError':
    case 'AbortError':
      return 'camara_ocupada';
    default:
      return 'no_soportado';
  }
}

export function apagarCamara(camara) {
  camara?.getTracks().forEach((pista) => pista.stop());
}

let cargaDelMotor = null;

function cargarMotor() {
  if (!cargaDelMotor) {
    cargaDelMotor = (async () => {
      const faceapi = await import('@vladmandic/face-api');
      // Sin WebGL funciona igual, aunque bastante más lento.
      if (!(await faceapi.tf.setBackend('webgl'))) {
        await faceapi.tf.setBackend('cpu');
      }
      await faceapi.tf.ready();
      await Promise.all([
        faceapi.nets.tinyFaceDetector.loadFromUri(RUTA_MODELOS),
        faceapi.nets.faceLandmark68Net.loadFromUri(RUTA_MODELOS),
        faceapi.nets.faceRecognitionNet.loadFromUri(RUTA_MODELOS),
      ]);
      return faceapi;
    })().catch((error) => {
      // Sin esto, un corte de red dejaría la función rota hasta recargar la página.
      cargaDelMotor = null;
      throw new ErrorFacial('carga', error);
    });
  }
  return cargaDelMotor;
}

const esperar = (ms) => new Promise((resolver) => setTimeout(resolver, ms));

/**
 * Calcula descriptores desde el video. Solo acepta imágenes con exactamente un rostro.
 * Nunca guarda ni envía la imagen: solo devuelve los 128 números de cada captura.
 *
 * @param {HTMLVideoElement} video
 * @param {{ cantidad: number, senal: AbortSignal, alEstado?: Function }} opciones
 * @returns {Promise<number[][]>}
 */
export async function capturarDescriptores(video, { cantidad, senal, alEstado }) {
  const faceapi = await cargarMotor();
  const detector = new faceapi.TinyFaceDetectorOptions({ inputSize: 320, scoreThreshold: 0.5 });
  let limite = Date.now() + TIEMPO_MAXIMO_MS;
  const descriptores = [];
  let problema = 'sin_rostro';

  while (descriptores.length < cantidad) {
    if (senal.aborted) throw new ErrorFacial('cancelado');
    if (Date.now() > limite) throw new ErrorFacial(problema);

    const rostros = await faceapi.detectAllFaces(video, detector).withFaceLandmarks().withFaceDescriptors();

    if (rostros.length === 1) {
      descriptores.push(Array.from(rostros[0].descriptor));
      // El plazo es para que aparezca un rostro: un equipo lento puede tardar en juntar varias capturas.
      limite = Date.now() + TIEMPO_MAXIMO_MS;
      alEstado?.({ tipo: 'capturando', avance: descriptores.length });
    } else {
      problema = rostros.length === 0 ? 'sin_rostro' : 'varios_rostros';
      alEstado?.({ tipo: problema, avance: descriptores.length });
    }
    await esperar(PAUSA_ENTRE_INTENTOS_MS);
  }
  return descriptores;
}
