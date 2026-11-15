/**
 * Índice de giro de la cabeza: el mismo cálculo que Negocio/Rostro/Giro.cs en el servidor
 * (docs/plan/fase-10-especificacion.md, «Medida del giro»). Si cambia aquí, cambia allá, con los mismos casos.
 *
 * Los 68 puntos llegan en coordenadas del cuadro de video SIN espejo (la imagen tal como la entrega la cámara).
 * Solo el <video> se muestra espejado, con CSS; esta cuenta no lo corrige porque no lo necesita.
 *
 *   r = (x[30] − x[0]) / (x[16] − x[0])   (punta de la nariz entre los extremos de la mandíbula)
 *   g = (r − 0.5) × 2, limitado a [−1, 1]
 *
 * g > 0: giro hacia la IZQUIERDA de la persona (en la imagen sin espejo, su lado izquierdo queda a la derecha).
 * g < 0: hacia su derecha.
 */
export const RANGOS_GIRO = Object.freeze({ frenteMaximo: 0.15, giroMinimo: 0.3, giroMaximo: 0.75 });

const PUNTOS = 68;
const MANDIBULA_MINIMA = 0.1; // del ancho del cuadro
const LADO_MAXIMO = 10000; // igual que Giro.LadoMaximoCuadro

/** 68 puntos [x, y] finitos y dentro de un cuadro de medidas positivas. */
export function puntosValidos(puntos, ancho, alto) {
  if (!Array.isArray(puntos) || puntos.length !== PUNTOS) return false;
  if (!(Number.isFinite(ancho) && ancho > 0 && ancho <= LADO_MAXIMO
    && Number.isFinite(alto) && alto > 0 && alto <= LADO_MAXIMO)) return false;
  return puntos.every((punto) => Array.isArray(punto)
    && Number.isFinite(punto[0]) && Number.isFinite(punto[1])
    && punto[0] >= 0 && punto[0] <= ancho && punto[1] >= 0 && punto[1] <= alto);
}

/** El índice g, o null si la captura no es válida. */
export function calcularGiro(puntos, ancho, alto) {
  if (!puntosValidos(puntos, ancho, alto)) return null;
  const x0 = puntos[0][0];
  const mandibula = puntos[16][0] - x0;
  if (mandibula < MANDIBULA_MINIMA * ancho) return null;
  const r = (puntos[30][0] - x0) / mandibula;
  return Math.min(1, Math.max(-1, (r - 0.5) * 2));
}

/** ¿El giro g cumple lo que pide el paso? frente: |g| ≤ máximo; izquierda: g en [mín, máx]; derecha: −g en [mín, máx]. */
export function giroEnRango(g, paso, rangos = RANGOS_GIRO) {
  if (!Number.isFinite(g)) return false;
  if (paso === 'frente') return Math.abs(g) <= rangos.frenteMaximo;
  const hacia = paso === 'izquierda' ? g : -g;
  return hacia >= rangos.giroMinimo && hacia <= rangos.giroMaximo;
}
