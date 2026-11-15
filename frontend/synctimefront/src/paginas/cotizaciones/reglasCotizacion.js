// Reglas de la cotización (fase 8), las mismas que valida la API (docs/plan/fase-08-especificacion.md):
// montos en pesos enteros con IVA incluido; la cantidad de un material admite hasta 2 decimales, así que su
// subtotal puede tener centavos; el total se redondea al peso, mitad hacia arriba. Las cuentas se hacen en
// centavos enteros para que 0,1 + 0,2 no deje restos de coma flotante.
import { ESTADOS_COTIZACION } from '../../componentes/ui/estados';
import { esFecha } from '../../utilidades/fechas';
import { parametrosBase } from '../../utilidades/filtrosUrl';

export const LIMITES_COTIZACION = {
  lineas: 30,
  medida: 100,
  cantidadMaxima: 100_000,
  costoMaximo: 100_000_000,
  totalMaximo: 2_000_000_000,
  observaciones: 2000,
  motivo: 500,
};

let siguiente = 1;
/** Una línea de material vacía. `clave` solo sirve a React; `sugerido` recuerda el costo que propuso el catálogo. */
export function lineaVacia() {
  return { clave: `m${siguiente++}`, idMaterial: '', medidaAplicada: '', cantidad: '1', costoUnitario: '', sugerido: null };
}

/**
 * Pesos escritos a mano: «12.990», «$ 12990» o «12 990» → 12990. Vacío → null. Con decimales o letras → NaN
 * (en Chile la coma es el decimal, y los montos van sin decimales).
 */
export function pesosDesdeTexto(texto) {
  const limpio = String(texto ?? '').trim().replace(/[\s$]/g, '');
  if (limpio === '') return null;
  if (/^\d{1,3}(\.\d{3})+$/.test(limpio)) return Number(limpio.replaceAll('.', ''));
  return /^\d+$/.test(limpio) ? Number(limpio) : Number.NaN;
}

/** Cantidad con coma o punto decimal: «2,5» → 2.5. Vacío → null; no numérico → NaN. */
export function cantidadDesdeTexto(texto) {
  const limpio = String(texto ?? '').trim().replace(/\s/g, '').replace(',', '.');
  if (limpio === '') return null;
  return /^\d+(\.\d+)?$/.test(limpio) ? Number(limpio) : Number.NaN;
}

const decimales = (texto) => (String(texto ?? '').trim().replace(',', '.').split('.')[1] ?? '').length;
const centesimas = (cantidad) => Math.round(cantidad * 100);

/** Subtotal en centavos de una línea (cantidad × costo unitario), o null si falta un dato válido. */
function subtotalCentavos(linea) {
  const cantidad = cantidadDesdeTexto(linea.cantidad);
  const costo = pesosDesdeTexto(linea.costoUnitario);
  if (!(cantidad > 0) || !Number.isFinite(cantidad) || !Number.isInteger(costo) || costo < 0) return null;
  return centesimas(cantidad) * costo;
}

/** Subtotal de una línea en pesos (puede tener centavos), o null. */
export function subtotalLinea(linea) {
  const centavos = subtotalCentavos(linea);
  return centavos === null ? null : centavos / 100;
}

/** Redondeo al peso, mitad hacia arriba, desde centavos enteros. */
export const redondearCentavos = (centavos) => Math.floor((centavos + 50) / 100);

const pesosOCero = (texto) => {
  const valor = pesosDesdeTexto(texto);
  return Number.isInteger(valor) ? valor : 0;
};

/**
 * Totales en vivo: { materiales, total, totalRedondeado, conCentavos }. Las líneas o montos que aún no son
 * válidos cuentan como 0, para que el total se vea mientras se escribe.
 */
export function calcularTotales({ lineas = [], costoManoObra = '', otrosCostos = '' }) {
  const materialesCentavos = lineas.reduce((suma, linea) => suma + (subtotalCentavos(linea) ?? 0), 0);
  const totalCentavos = materialesCentavos + (pesosOCero(costoManoObra) + pesosOCero(otrosCostos)) * 100;
  return {
    materiales: materialesCentavos / 100,
    total: totalCentavos / 100,
    totalRedondeado: redondearCentavos(totalCentavos),
    conCentavos: totalCentavos % 100 !== 0 || lineas.some((l) => (subtotalCentavos(l) ?? 0) % 100 !== 0),
  };
}

export const campoLinea = (indice, campo) => `materiales.${indice}.${campo}`;

const mensajeMonto = (que) => `Escribe ${que} en pesos, sin decimales, de $0 a $100.000.000. Puedes usar puntos de miles.`;

function errorMonto(texto, que, { obligatorio = false } = {}) {
  const valor = pesosDesdeTexto(texto);
  if (valor === null) return obligatorio ? mensajeMonto(que) : null;
  if (!Number.isInteger(valor) || valor < 0 || valor > LIMITES_COTIZACION.costoMaximo) return mensajeMonto(que);
  return null;
}

/**
 * Errores del formulario: { 'materiales.0.idMaterial': '…', costoManoObra: '…' }. El total (> $0 y hasta
 * $2.000.000.000) se informa junto a la mano de obra, que es lo que suele faltar.
 */
export function validarCotizacion({ lineas = [], costoManoObra = '', otrosCostos = '', observaciones = '' }, { materialesActivos } = {}) {
  const errores = {};
  lineas.forEach((linea, i) => {
    const nombre = (campo) => campoLinea(i, campo);
    if (!linea.idMaterial) errores[nombre('idMaterial')] = 'Elige el material del catálogo.';
    else if (materialesActivos && !materialesActivos.has(Number(linea.idMaterial))) {
      errores[nombre('idMaterial')] = 'Este material ya no está activo en el catálogo. Elige otro.';
    }
    if (linea.medidaAplicada.length > LIMITES_COTIZACION.medida) errores[nombre('medidaAplicada')] = 'Usa hasta 100 caracteres.';
    const cantidad = cantidadDesdeTexto(linea.cantidad);
    if (cantidad === null || !Number.isFinite(cantidad) || cantidad <= 0 || cantidad > LIMITES_COTIZACION.cantidadMaxima) {
      errores[nombre('cantidad')] = 'Escribe una cantidad mayor que 0 y hasta 100.000, por ejemplo 2,5.';
    } else if (decimales(linea.cantidad) > 2) {
      errores[nombre('cantidad')] = 'Usa hasta 2 decimales, por ejemplo 2,75.';
    }
    const costo = errorMonto(linea.costoUnitario, 'el costo unitario', { obligatorio: true });
    if (costo) errores[nombre('costoUnitario')] = costo;
  });
  const manoObra = errorMonto(costoManoObra, 'la mano de obra');
  if (manoObra) errores.costoManoObra = manoObra;
  const otros = errorMonto(otrosCostos, 'los otros costos');
  if (otros) errores.otrosCostos = otros;
  if (observaciones.length > LIMITES_COTIZACION.observaciones) errores.observaciones = 'Usa hasta 2.000 caracteres.';

  const sinErroresDeMonto = !Object.keys(errores).some((c) => /costoUnitario|cantidad|costoManoObra|otrosCostos/.test(c));
  if (sinErroresDeMonto) {
    const { total } = calcularTotales({ lineas, costoManoObra, otrosCostos });
    if (total <= 0) errores.costoManoObra = 'La cotización debe sumar más de $0: agrega materiales o escribe la mano de obra.';
    else if (total > LIMITES_COTIZACION.totalMaximo) errores.costoManoObra = 'El total no puede pasar de $2.000.000.000. Revisa los montos.';
  }
  return errores;
}

/** El orden visual de los campos, para llevar el foco al primer error. */
export function ordenCamposCotizacion(lineas) {
  return [
    ...lineas.flatMap((_, i) => ['idMaterial', 'medidaAplicada', 'cantidad', 'costoUnitario'].map((c) => campoLinea(i, c))),
    'costoManoObra', 'otrosCostos', 'observaciones',
  ];
}

/** El cuerpo que piden POST /solicitudes/{id}/cotizaciones y PUT /cotizaciones/{id}. */
export function cotizacionParaApi({ lineas, costoManoObra, otrosCostos, observaciones }) {
  return {
    materiales: lineas.map((linea) => ({
      idMaterial: Number(linea.idMaterial),
      medidaAplicada: linea.medidaAplicada.trim() || null,
      cantidad: cantidadDesdeTexto(linea.cantidad),
      costoUnitario: pesosDesdeTexto(linea.costoUnitario),
    })),
    costoManoObra: pesosDesdeTexto(costoManoObra) ?? 0,
    otrosCostos: pesosDesdeTexto(otrosCostos) ?? 0,
    observaciones: observaciones.trim() || null,
  };
}

const formatoCantidad = new Intl.NumberFormat('es-CL', { maximumFractionDigits: 2 });
/** 2.5 → «2,5»; 1200 → «1.200». */
export const formatearCantidad = (valor) => (valor === null || valor === undefined || !Number.isFinite(Number(valor)) ? '—' : formatoCantidad.format(Number(valor)));

const aTexto = (valor) => (valor === null || valor === undefined ? '' : String(valor).replace('.', ','));

/** Una cotización de la API como estado del editor (para editarla). */
export function formularioDesdeCotizacion(cotizacion) {
  return {
    lineas: (cotizacion?.materiales ?? []).map((m) => ({
      ...lineaVacia(),
      idMaterial: m.idMaterial ? String(m.idMaterial) : '',
      medidaAplicada: m.medidaAplicada ?? '',
      cantidad: aTexto(m.cantidad),
      costoUnitario: m.costoUnitario === null || m.costoUnitario === undefined ? '' : String(Math.round(Number(m.costoUnitario))),
    })),
    costoManoObra: cotizacion?.costoManoObra ? String(Math.round(Number(cotizacion.costoManoObra))) : '',
    otrosCostos: cotizacion?.otrosCostos ? String(Math.round(Number(cotizacion.otrosCostos))) : '',
    observaciones: cotizacion?.observaciones ?? '',
  };
}

/**
 * Errores por campo de la API ({ 'materiales[0].Cantidad': ['…'], CostoManoObra: ['…'] }) con los nombres
 * del formulario ({ 'materiales.0.cantidad': '…', costoManoObra: '…' }). Un error del total va a la mano de obra.
 */
export function erroresCotizacionDesdeApi(errores) {
  const resultado = {};
  for (const [clave, mensajes] of Object.entries(errores ?? {})) {
    const mensaje = Array.isArray(mensajes) ? mensajes[0] : String(mensajes);
    const linea = clave.match(/^materiales\[(\d+)\]\.(\w+)$/i);
    if (linea) {
      resultado[campoLinea(Number(linea[1]), linea[2].charAt(0).toLowerCase() + linea[2].slice(1))] = mensaje;
      continue;
    }
    const campo = clave.charAt(0).toLowerCase() + clave.slice(1);
    if (campo === 'materiales') resultado[campoLinea(0, 'idMaterial')] = mensaje;
    else if (campo === 'total') resultado.costoManoObra = mensaje;
    else resultado[campo] = mensaje;
  }
  return resultado;
}

/** El número que se muestra: el de la API («#45») o el id. */
export const numeroCotizacion = (c) => c?.numero ?? (c?.idCotizacion ? `#${c.idCotizacion}` : '');

export const VIAS_RESPUESTA = { cliente_web: 'En la web', telefono: 'Por teléfono', whatsapp: 'Por WhatsApp', presencial: 'En el local' };

/** «Aprobada en la web el 14-11-2026 10:32» (sin fecha si no viene). */
export function textoRespuesta(cotizacion, formatearFechaHora) {
  const r = cotizacion?.respuesta;
  if (!r) return '';
  const decision = cotizacion.estado === 'rechazada' ? 'Rechazada' : 'Aprobada';
  const via = (VIAS_RESPUESTA[r.via] ?? r.via ?? '').toLowerCase();
  return [decision, via, r.fechaChile ? `el ${formatearFechaHora(r.fechaChile)}` : ''].filter(Boolean).join(' ');
}

/** Las cotizaciones de una solicitud, de la más reciente a la más antigua (`cotizaciones` o, si no, `cotizacion`). */
export function cotizacionesDeSolicitud(solicitud) {
  const lista = Array.isArray(solicitud?.cotizaciones) ? solicitud.cotizaciones : solicitud?.cotizacion ? [solicitud.cotizacion] : [];
  return [...lista].sort((a, b) => String(b.fechaChile ?? '').localeCompare(String(a.fechaChile ?? '')) || (b.idCotizacion ?? 0) - (a.idCotizacion ?? 0));
}

// ── Listado (RNFP32) ──

export const TEXTOS_ORDEN_COTIZACIONES = {
  fecha_desc: 'Más recientes',
  fecha_asc: 'Más antiguas',
  total_desc: 'Mayor total',
};

export const FILTROS_COTIZACIONES = {
  campos: {
    estado: (v) => v in ESTADOS_COTIZACION,
    desde: esFecha,
    hasta: esFecha,
    idCliente: (v) => /^[1-9]\d{0,9}$/.test(v),
    q: (v) => v.length <= 100,
    orden: (v) => v in TEXTOS_ORDEN_COTIZACIONES,
  },
  predeterminados: { orden: 'fecha_desc' },
  tamano: 20,
};

/** Los parámetros de GET /cotizaciones. Un rango de fechas al revés no se envía. */
export function parametrosCotizaciones(filtros) {
  const base = parametrosBase(filtros, FILTROS_COTIZACIONES);
  if (base.desde && base.hasta && base.hasta < base.desde) return { ...base, desde: undefined, hasta: undefined };
  return base;
}

const numeroO0 = (valor) => (Number.isFinite(valor) ? valor : 0);

/** El borrador del editor con la forma de la cotización (S06), para la vista previa del cliente. */
export function borradorParaVista({ datos, materiales = [], base = {} }) {
  const totales = calcularTotales(datos);
  return {
    ...base,
    materiales: datos.lineas.map((linea) => {
      const material = materiales.find((m) => String(m.idMaterial) === String(linea.idMaterial));
      return {
        idMaterial: linea.idMaterial,
        material: material?.nombre ?? 'Material sin elegir',
        unidad: material?.unidadMedida ?? '',
        medidaAplicada: linea.medidaAplicada.trim(),
        cantidad: numeroO0(cantidadDesdeTexto(linea.cantidad)),
        costoUnitario: numeroO0(pesosDesdeTexto(linea.costoUnitario)),
        subtotal: subtotalLinea(linea) ?? 0,
      };
    }),
    costoMaterialesTotal: totales.materiales,
    costoManoObra: numeroO0(pesosDesdeTexto(datos.costoManoObra)),
    otrosCostos: numeroO0(pesosDesdeTexto(datos.otrosCostos)),
    total: totales.total,
    totalRedondeado: totales.totalRedondeado,
    observaciones: datos.observaciones.trim(),
  };
}
