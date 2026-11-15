/**
 * Único módulo que conoce la librería de reconocimiento facial.
 * La librería y sus modelos se descargan la primera vez que alguien usa la función,
 * para no hacer más lento el login normal. Los modelos se sirven desde el propio sitio.
 *
 * @vladmandic/face-api tiene su repositorio archivado. Cambiarla exigiría tocar este módulo,
 * el modelo del servidor (RostroFacialService) y volver a activar cada rostro.
 */
const RUTA_MODELOS = '/models';

export class ErrorFacial extends Error {
  constructor(codigo, causa) {
    super(codigo, { cause: causa });
    this.name = 'ErrorFacial';
    this.codigo = codigo;
  }
}

const MENSAJES = {
  permiso_denegado: 'El navegador no tiene permiso para usar la cámara. Permítelo o entra con tu contraseña.',
  sin_camara: 'No encontramos una cámara en este equipo. Entra con tu contraseña.',
  camara_ocupada: 'Otro programa está usando la cámara. Ciérralo e inténtalo de nuevo, o entra con tu contraseña.',
  no_soportado: 'Este navegador no permite usar la cámara en esta página. Entra con tu contraseña.',
  sin_rostro: 'No se detectó ningún rostro. Ponlo dentro del óvalo, con buena luz, e inténtalo de nuevo, o entra con tu contraseña.',
  varios_rostros: 'Se detectó más de un rostro. Debes aparecer solo tú en la imagen. Inténtalo de nuevo o entra con tu contraseña.',
  poca_luz: 'Falta luz para ver bien tu rostro. Busca un lugar más iluminado e inténtalo de nuevo, o entra con tu contraseña.',
  tiempo_agotado: 'Se acabó el tiempo para el giro. Inténtalo de nuevo siguiendo la flecha, o entra con tu contraseña.',
  carga: 'No pudimos cargar los archivos de reconocimiento. Revisa la conexión o entra con tu contraseña.',
  desafio_invalido: 'No pudimos preparar la prueba del rostro. Inténtalo de nuevo o entra con tu contraseña.',
};

const RED = 'No pudimos conectar con el servidor. Revisa tu conexión e inténtalo de nuevo, o entra con tu contraseña.';
const DEMASIADOS = 'Demasiados intentos con rostro. Espera un momento o entra con tu contraseña.';
const NO_DISPONIBLE = 'El acceso con rostro no está disponible en este momento. Entra con tu contraseña.';

/**
 * Texto para cualquier fallo del rostro: los de la cámara y la detección (ErrorFacial) y los de la API
 * (ErrorApi: sin conexión, demasiados intentos, servicio no disponible). Nunca dice qué comprobó el servidor.
 */
export function mensajeDeError(error) {
  if (MENSAJES[error?.codigo]) return MENSAJES[error.codigo];
  if (error?.estado === 429) return DEMASIADOS;
  if (error?.estado === 503) return NO_DISPONIBLE;
  if (error?.estado === 0 || error?.codigo === 'sin_conexion') return RED;
  return 'No pudimos usar el reconocimiento facial. Entra con tu contraseña.';
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
let opcionesDetector = null;

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
      opcionesDetector = new faceapi.TinyFaceDetectorOptions({ inputSize: 320, scoreThreshold: 0.5 });
      return faceapi;
    })().catch((error) => {
      // Sin esto, un corte de red dejaría la función rota hasta recargar la página.
      cargaDelMotor = null;
      throw new ErrorFacial('carga', error);
    });
  }
  return cargaDelMotor;
}

/** Descarga la librería y los modelos (una sola vez). Falla con ErrorFacial('carga'). */
export async function prepararMotor() {
  await cargarMotor();
}

/**
 * Detecta los rostros de UN cuadro del video. Nunca guarda ni envía la imagen.
 * Devuelve { ancho, alto, brillo, rostros: [{ caja: { x, y, ancho, alto }, puntos: [[x, y] × 68], descriptor }] }
 * en coordenadas del cuadro SIN espejo (videoWidth × videoHeight). El descriptor (128 números) solo se calcula
 * con `conDescriptor`, porque es lo más lento: la guía en vivo no lo necesita, la captura sí.
 *
 * @param {HTMLVideoElement} video
 * @param {{ conDescriptor?: boolean }} opciones
 */
export async function detectarCuadro(video, { conDescriptor = false } = {}) {
  const faceapi = await cargarMotor();
  const conPuntos = faceapi.detectAllFaces(video, opcionesDetector).withFaceLandmarks();
  const resultados = await (conDescriptor ? conPuntos.withFaceDescriptors() : conPuntos);
  return {
    ancho: video.videoWidth,
    alto: video.videoHeight,
    brillo: medirBrillo(video),
    rostros: resultados.map((r) => ({
      caja: { x: r.detection.box.x, y: r.detection.box.y, ancho: r.detection.box.width, alto: r.detection.box.height },
      puntos: r.landmarks.positions.map((p) => [p.x, p.y]),
      descriptor: r.descriptor ? Array.from(r.descriptor) : null,
    })),
  };
}

// Un lienzo pequeño basta para el brillo medio; vive solo en memoria y nunca se muestra ni se envía.
const LADO_BRILLO = { ancho: 32, alto: 24 };
let lienzoBrillo = null;

/** Brillo medio del cuadro (0 a 255, luminancia), o null si el navegador no deja leer la imagen. */
export function medirBrillo(video) {
  try {
    lienzoBrillo ??= Object.assign(document.createElement('canvas'), { width: LADO_BRILLO.ancho, height: LADO_BRILLO.alto });
    const contexto = lienzoBrillo.getContext('2d', { willReadFrequently: true });
    if (!contexto) return null;
    contexto.drawImage(video, 0, 0, LADO_BRILLO.ancho, LADO_BRILLO.alto);
    const { data } = contexto.getImageData(0, 0, LADO_BRILLO.ancho, LADO_BRILLO.alto);
    let suma = 0;
    for (let i = 0; i < data.length; i += 4) suma += 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
    return suma / (data.length / 4);
  } catch {
    return null;
  }
}
