import { cmAMm, numero } from '../solicitudes/reglasSolicitud';

// Fase 12D (decisión 6): el editor de medidas y opciones de un producto. Las mismas reglas que valida la API
// (Negocio/Mantenedores/ValidacionMedidasYOpciones.cs). En pantalla las medidas van en centímetros; la API las
// recibe y devuelve en milímetros. Sin React.

export const MODOS = { ninguno: 'ninguno', rangos: 'rangos', fijas: 'fijas' };

export const TOPE_CM = 5000;
export const MAX_FIJAS = 20;
export const MAX_GRUPOS = 10;
export const MAX_VALORES = 30;
export const LARGO_GRUPO = 60;
export const LARGO_VALOR = 80;

const CAMPOS_RANGO = [
  ['anchoMin', 'anchoMinMm', 'El ancho mínimo'],
  ['anchoMax', 'anchoMaxMm', 'El ancho máximo'],
  ['altoMin', 'altoMinMm', 'El alto mínimo'],
  ['altoMax', 'altoMaxMm', 'El alto máximo'],
];

const aTexto = (mm) => (mm == null ? '' : String(mm / 10).replace('.', ','));
const aCm = (texto) => numero(texto);
// Entre 0,1 y 5.000 cm y con un solo decimal: la API trabaja en milímetros enteros y no se redondea en silencio.
const mmValido = (cm) => Number.isFinite(cm) && cm >= 0.1 && cm <= TOPE_CM && Math.abs(cm * 10 - Math.round(cm * 10)) < 1e-9;
const cmTexto = (cm) => String(cm).replace('.', ',');

/** La ficha de la API (milímetros, opciones planas) como formulario (centímetros, opciones por grupo). */
export function medidasDesdeApi(producto) {
  const fijas = (producto?.medidasFijas ?? []).map((m) => ({ ancho: aTexto(m.anchoMm), alto: aTexto(m.altoMm) }));
  const hayRangos = producto?.anchoMinMm != null || producto?.anchoMaxMm != null || producto?.altoMinMm != null || producto?.altoMaxMm != null;
  const grupos = [];
  for (const { grupo, valor } of producto?.opciones ?? []) {
    const existente = grupos.find((g) => g.grupo === grupo);
    if (existente) existente.valores.push(valor);
    else grupos.push({ grupo, valores: [valor] });
  }
  return {
    modo: fijas.length ? MODOS.fijas : hayRangos ? MODOS.rangos : MODOS.ninguno,
    anchoMin: aTexto(producto?.anchoMinMm), anchoMax: aTexto(producto?.anchoMaxMm),
    altoMin: aTexto(producto?.altoMinMm), altoMax: aTexto(producto?.altoMaxMm),
    fijas,
    grupos,
  };
}

/** El formulario como lo recibe la API: milímetros, solo lo que corresponde al modo y los textos sin espacios de más. */
export function medidasParaApi(form) {
  const rangos = form.modo === MODOS.rangos;
  return {
    anchoMinMm: rangos ? cmAMm(aCm(form.anchoMin)) : null,
    anchoMaxMm: rangos ? cmAMm(aCm(form.anchoMax)) : null,
    altoMinMm: rangos ? cmAMm(aCm(form.altoMin)) : null,
    altoMaxMm: rangos ? cmAMm(aCm(form.altoMax)) : null,
    medidasFijas: form.modo === MODOS.fijas ? form.fijas.map((m) => ({ anchoMm: cmAMm(aCm(m.ancho)), altoMm: cmAMm(aCm(m.alto)) })) : [],
    opciones: form.grupos.map((g) => ({ grupo: g.grupo.trim(), valores: g.valores.map((v) => v.trim()).filter(Boolean) })),
  };
}

/** Errores por campo, con los nombres que usa la API (anchoMinMm, medidasFijas, opciones…). Vacío: se puede guardar. */
export function validarMedidasYOpciones(form) {
  const errores = {};
  if (form.modo === MODOS.rangos) {
    for (const [campo, clave, nombre] of CAMPOS_RANGO) {
      const cm = aCm(form[campo]);
      if (cm === null || Number.isNaN(cm)) errores[clave] = `${nombre} es obligatorio: escríbelo en centímetros.`;
      else if (!mmValido(cm)) errores[clave] = `${nombre} debe estar entre 0,1 y 5.000 cm, con un solo decimal.`;
    }
    for (const [minimo, maximo, claveMaximo, eje] of [['anchoMin', 'anchoMax', 'anchoMaxMm', 'ancho'], ['altoMin', 'altoMax', 'altoMaxMm', 'alto']]) {
      const min = aCm(form[minimo]);
      const max = aCm(form[maximo]);
      if (!errores[claveMaximo] && Number.isFinite(min) && Number.isFinite(max) && max < min) {
        errores[claveMaximo] = `El ${eje} máximo no puede ser menor que el mínimo.`;
      }
    }
  }
  if (form.modo === MODOS.fijas) {
    const error = errorMedidasFijas(form.fijas);
    if (error) errores.medidasFijas = error;
  }
  const errorOpciones = errorGrupos(form.grupos);
  if (errorOpciones) errores.opciones = errorOpciones;
  return errores;
}

function errorMedidasFijas(fijas) {
  if (fijas.length === 0) return 'Agrega al menos una medida fija, o elige otra forma de medir.';
  if (fijas.length > MAX_FIJAS) return `Puede tener hasta ${MAX_FIJAS} medidas fijas.`;
  const vistas = new Set();
  for (const m of fijas) {
    const ancho = aCm(m.ancho);
    const alto = aCm(m.alto);
    if (!mmValido(ancho) || !mmValido(alto)) return 'Cada medida fija necesita ancho y alto entre 0,1 y 5.000 cm.';
    const clave = `${cmAMm(ancho)}x${cmAMm(alto)}`;
    if (vistas.has(clave)) return `La medida ${cmTexto(ancho)} x ${cmTexto(alto)} cm está repetida.`;
    vistas.add(clave);
  }
  return null;
}

function errorGrupos(grupos) {
  if (grupos.length > MAX_GRUPOS) return `Puede tener hasta ${MAX_GRUPOS} grupos de opciones.`;
  const nombres = new Set();
  for (const g of grupos) {
    const nombre = g.grupo.trim();
    if (!nombre || nombre.length > LARGO_GRUPO) return `Cada grupo necesita un nombre de hasta ${LARGO_GRUPO} caracteres.`;
    if (nombres.has(nombre.toLowerCase())) return `El grupo «${nombre}» está repetido.`;
    nombres.add(nombre.toLowerCase());
    const valores = g.valores.map((v) => v.trim()).filter(Boolean);
    if (valores.length === 0) return `El grupo «${nombre}» necesita al menos un valor.`;
    if (valores.length > MAX_VALORES) return `El grupo «${nombre}» puede tener hasta ${MAX_VALORES} valores.`;
    if (valores.some((v) => v.length > LARGO_VALOR)) return `En «${nombre}», cada valor puede tener hasta ${LARGO_VALOR} caracteres.`;
    const vistos = new Set();
    for (const v of valores) {
      if (vistos.has(v.toLowerCase())) return `En «${nombre}», el valor «${v}» está repetido.`;
      vistos.add(v.toLowerCase());
    }
  }
  return null;
}

// ── Cambios sobre el formulario (devuelven uno nuevo, sin tocar el anterior) ──

export const agregarMedidaFija = (form) => ({ ...form, fijas: [...form.fijas, { ancho: '', alto: '' }] });
export const quitarMedidaFija = (form, indice) => ({ ...form, fijas: form.fijas.filter((_, i) => i !== indice) });
export const cambiarMedidaFija = (form, indice, campo, valor) => ({
  ...form, fijas: form.fijas.map((m, i) => (i === indice ? { ...m, [campo]: valor } : m)),
});
export const agregarGrupo = (form) => ({ ...form, grupos: [...form.grupos, { grupo: '', valores: [''] }] });
export const quitarGrupo = (form, indice) => ({ ...form, grupos: form.grupos.filter((_, i) => i !== indice) });
export const cambiarGrupo = (form, indice, nombre) => ({ ...form, grupos: form.grupos.map((g, i) => (i === indice ? { ...g, grupo: nombre } : g)) });
export const agregarValor = (form, indice) => ({
  ...form, grupos: form.grupos.map((g, i) => (i === indice ? { ...g, valores: [...g.valores, ''] } : g)),
});
export const cambiarValor = (form, indice, posicion, valor) => ({
  ...form, grupos: form.grupos.map((g, i) => (i === indice ? { ...g, valores: g.valores.map((v, j) => (j === posicion ? valor : v)) } : g)),
});
export const quitarValor = (form, indice, posicion) => ({
  ...form, grupos: form.grupos.map((g, i) => (i === indice ? { ...g, valores: g.valores.filter((_, j) => j !== posicion) } : g)),
});
