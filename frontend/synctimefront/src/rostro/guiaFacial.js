import { RANGOS_GIRO, calcularGiro, giroEnRango } from './giro';
import { ErrorFacial } from './motorFacial';

/**
 * Lógica del flujo guiado del rostro (fase 10), sin cámara ni React: recibe una función que detecta un
 * cuadro y decide qué decirle a la persona y cuándo tomar cada captura. Así se prueba con la detección simulada.
 *
 * Pasos: 'frente' y luego cada giro ('izquierda' | 'derecha') en el orden que se pida (el del desafío del servidor).
 * Cada paso avanza solo cuando el cuadro cumple durante `sostenerMs`; entonces se toma un cuadro más, con
 * descriptor, y si sigue cumpliendo es la captura. Cada giro tiene `giroMs` y un reintento automático.
 * No hay esperas artificiales: la duración sale de lo que tarda la persona.
 */
export const TIEMPOS_FLUJO = Object.freeze({
  sostenerMs: 300,
  pausaMs: 50,
  giroMs: 4000,
  frenteMs: 12000,
});

// Medidas de la guía en vivo. Son una ayuda para encuadrar, no una comprobación de seguridad: el servidor
// revisa el giro con los mismos puntos. Se ajustan con la cámara real (no pueden probarse sin ella).
const GUIA = Object.freeze({
  brilloMinimo: 50, // luminancia media 0 a 255
  rostroMinimo: 0.22, // ancho de la caja / ancho del cuadro
  rostroMaximo: 0.6,
  centroMaximo: 0.2, // distancia del centro de la caja al centro del cuadro, en fracción de cada lado
  comenzoGiro: 0.15, // fracción del progreso desde la que se dice «Gira un poco más»
});

const DESCRIPTOR = 128;
const PROBLEMAS = { falta_luz: 'poca_luz', sin_rostro: 'sin_rostro', varios_rostros: 'varios_rostros' };

const esperar = (ms, senal) => new Promise((resolver) => {
  const alAbortar = () => {
    clearTimeout(temporizador);
    resolver();
  };
  const temporizador = setTimeout(() => {
    senal?.removeEventListener('abort', alAbortar);
    resolver();
  }, ms);
  senal?.addEventListener('abort', alAbortar, { once: true });
});

/**
 * Qué decirle a la persona con este cuadro, para este paso.
 * guia: 'falta_luz' | 'sin_rostro' | 'varios_rostros' | 'acercate' | 'alejate' | 'centra' | 'mira_al_frente'
 *   | 'gira' | 'gira_mas' | 'gira_menos' | 'listo'. progreso (0 a 1): cuánto falta para el giro mínimo.
 */
export function evaluarCuadro(cuadro, paso, rangos = RANGOS_GIRO) {
  const resultado = (guia, g = null, progreso = 0) => ({ guia, g, progreso });
  if (Number.isFinite(cuadro?.brillo) && cuadro.brillo < GUIA.brilloMinimo) return resultado('falta_luz');
  const rostros = cuadro?.rostros ?? [];
  if (rostros.length === 0) return resultado('sin_rostro');
  if (rostros.length > 1) return resultado('varios_rostros');

  const [rostro] = rostros;
  const { ancho, alto } = cuadro;
  const g = calcularGiro(rostro.puntos, ancho, alto);
  if (g === null) return resultado('centra');
  const tamano = rostro.caja.ancho / ancho;
  if (tamano < GUIA.rostroMinimo) return resultado('acercate', g);
  if (tamano > GUIA.rostroMaximo) return resultado('alejate', g);
  const centroX = (rostro.caja.x + rostro.caja.ancho / 2) / ancho - 0.5;
  const centroY = (rostro.caja.y + rostro.caja.alto / 2) / alto - 0.5;
  if (Math.abs(centroX) > GUIA.centroMaximo || Math.abs(centroY) > GUIA.centroMaximo) return resultado('centra', g);

  if (paso === 'frente') return resultado(giroEnRango(g, paso, rangos) ? 'listo' : 'mira_al_frente', g, 1);

  const hacia = paso === 'izquierda' ? g : -g;
  const progreso = Math.min(1, Math.max(0, hacia / rangos.giroMinimo));
  if (hacia > rangos.giroMaximo) return resultado('gira_menos', g, 1);
  if (giroEnRango(g, paso, rangos)) return resultado('listo', g, 1);
  return resultado(progreso > GUIA.comenzoGiro ? 'gira_mas' : 'gira', g, progreso);
}

/** El cuadro final sirve de captura: cumple el paso y trae un descriptor completo. */
function capturaDesde(cuadro, paso, rangos) {
  if (evaluarCuadro(cuadro, paso, rangos).guia !== 'listo') return null;
  const [rostro] = cuadro.rostros;
  if (!Array.isArray(rostro.descriptor) || rostro.descriptor.length !== DESCRIPTOR) return null;
  return { paso, descriptor: rostro.descriptor, puntos: rostro.puntos, ancho: cuadro.ancho, alto: cuadro.alto };
}

/**
 * Corre el flujo completo y devuelve las capturas [{ paso, descriptor, puntos, ancho, alto }] en el orden de `pasos`.
 * Falla con ErrorFacial: 'cancelado', 'poca_luz', 'sin_rostro', 'varios_rostros' o 'tiempo_agotado'.
 *
 * @param {{ detectar: (op: { conDescriptor: boolean }) => Promise<object>, pasos: string[], senal: AbortSignal,
 *   rangos?: object, alEstado?: Function, alCaptura?: Function, tiempos?: object }} opciones
 */
export async function ejecutarFlujo({ detectar, pasos, senal, rangos = RANGOS_GIRO, alEstado, alCaptura, tiempos = TIEMPOS_FLUJO }) {
  const capturas = [];
  const total = pasos.length;

  for (let indice = 0; indice < total; indice += 1) {
    const paso = pasos[indice];
    const intentos = paso === 'frente' ? 1 : 2;
    let captura = null;
    let problema = null;

    for (let intento = 1; intento <= intentos && !captura; intento += 1) {
      const limite = Date.now() + (paso === 'frente' ? tiempos.frenteMs : tiempos.giroMs);
      let desde = null;
      alEstado?.({ paso, indice, total, intento, guia: null, progreso: 0 });

      while (!captura && Date.now() <= limite) {
        if (senal.aborted) throw new ErrorFacial('cancelado');
        const cuadro = await detectar({ conDescriptor: false });
        if (senal.aborted) throw new ErrorFacial('cancelado');
        const evaluacion = evaluarCuadro(cuadro, paso, rangos);
        alEstado?.({ paso, indice, total, intento, guia: evaluacion.guia, progreso: evaluacion.progreso });

        if (evaluacion.guia !== 'listo') {
          desde = null;
          problema = evaluacion.guia;
        } else {
          // Cumplía al acabarse el tiempo: no se culpa a un problema viejo (sin rostro, luz…).
          problema = null;
          desde ??= Date.now();
          if (Date.now() - desde >= tiempos.sostenerMs) {
            captura = capturaDesde(await detectar({ conDescriptor: true }), paso, rangos);
            if (senal.aborted) throw new ErrorFacial('cancelado');
            if (!captura) desde = null;
          }
        }
        if (!captura) await esperar(tiempos.pausaMs, senal);
      }
    }

    if (!captura) throw new ErrorFacial(PROBLEMAS[problema] ?? 'tiempo_agotado');
    capturas.push(captura);
    alCaptura?.(captura, indice);
  }
  return capturas;
}
