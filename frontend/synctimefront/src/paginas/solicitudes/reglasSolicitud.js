// Reglas de la solicitud (fase 7), las mismas que valida la API (docs/plan/fase-07-especificacion.md).
// En pantalla las medidas van en centímetros; a la API viajan en milímetros.

export const LIMITES_SOLICITUD = {
  items: 10,
  cmMaximo: 5000, // 50 000 mm
  cantidadMaxima: 999,
  descripcionItem: 500,
  descripcionMinima: 10,
  descripcionMaxima: 2000,
  motivoMinimo: 10,
  motivoMaximo: 500,
};

let siguiente = 1;
/** Un letrero vacío del formulario. `clave` solo sirve a React para seguirle la pista. */
export function itemVacio(idProducto = '') {
  return { clave: `i${siguiente++}`, idProducto: idProducto ? String(idProducto) : '', ancho: '', alto: '', cantidad: '1', descripcion: '', opciones: {} };
}

/** «120,5» → 120.5; vacío → null; texto que no es número → NaN. */
export function numero(texto) {
  const limpio = String(texto ?? '').trim().replace(/\s/g, '').replace(',', '.');
  if (limpio === '') return null;
  return /^\d+(\.\d+)?$/.test(limpio) ? Number(limpio) : Number.NaN;
}

export const cmAMm = (cm) => Math.round(cm * 10);

/** Área de un letrero en m² (ancho × alto en cm × cantidad), o null si faltan medidas válidas. */
export function areaItem(item) {
  const ancho = numero(item.ancho);
  const alto = numero(item.alto);
  const cantidad = numero(item.cantidad);
  if (!(ancho > 0) || !(alto > 0) || !Number.isInteger(cantidad) || cantidad < 1) return null;
  // La API redondea a 2 decimales sobre los milímetros: aquí, lo mismo.
  return Math.round(((cmAMm(ancho) * cmAMm(alto)) / 1_000_000) * cantidad * 100) / 100;
}

export function areaTotal(items) {
  const areas = items.map(areaItem).filter((a) => a !== null);
  return areas.length ? Math.round(areas.reduce((s, a) => s + a, 0) * 100) / 100 : null;
}

const formatoArea = new Intl.NumberFormat('es-CL', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
/** 0.72 → «0,72 m²». */
export const formatearArea = (m2) => (m2 === null || m2 === undefined || !Number.isFinite(Number(m2)) ? '—' : `${formatoArea.format(Number(m2))} m²`);

const formatoCm = new Intl.NumberFormat('es-CL', { maximumFractionDigits: 1 });
/** 1200 × 600 mm → «120 × 60 cm»; sin medidas, «Sin medidas». */
export function medidasCm(anchoMm, altoMm) {
  if (!anchoMm || !altoMm) return 'Sin medidas';
  return `${formatoCm.format(anchoMm / 10)} × ${formatoCm.format(altoMm / 10)} cm`;
}

/** Nombre del campo de un letrero: detalles.0.ancho. */
export const campoItem = (indice, campo) => `detalles.${indice}.${campo}`;

/**
 * Errores del formulario: { 'detalles.0.idProducto': '…', descripcionGeneral: '…' }. Los mensajes dicen
 * qué pasó y cómo seguir.
 */
export function validarSolicitud({ items, descripcionGeneral }) {
  const errores = {};
  items.forEach((item, i) => {
    const nombre = (campo) => campoItem(i, campo);
    if (!item.idProducto) errores[nombre('idProducto')] = 'Elige el tipo de letrero.';
    const ancho = numero(item.ancho);
    const alto = numero(item.alto);
    for (const [campo, valor, texto] of [['ancho', ancho, 'el ancho'], ['alto', alto, 'el alto']]) {
      if (valor !== null && (!Number.isFinite(valor) || valor <= 0 || valor > LIMITES_SOLICITUD.cmMaximo || cmAMm(valor) < 1)) {
        errores[nombre(campo)] = `Escribe ${texto} en centímetros, entre 0,1 y 5.000.`;
      }
    }
    if (ancho !== null && alto === null && !errores[nombre('ancho')]) errores[nombre('alto')] = 'Escribe también el alto, o deja las dos medidas en blanco.';
    if (alto !== null && ancho === null && !errores[nombre('alto')]) errores[nombre('ancho')] = 'Escribe también el ancho, o deja las dos medidas en blanco.';
    const cantidad = numero(item.cantidad);
    if (!Number.isInteger(cantidad) || cantidad < 1 || cantidad > LIMITES_SOLICITUD.cantidadMaxima) {
      errores[nombre('cantidad')] = 'La cantidad va de 1 a 999.';
    }
    if (item.descripcion.length > LIMITES_SOLICITUD.descripcionItem) errores[nombre('descripcion')] = 'Usa hasta 500 caracteres.';
  });
  const largo = descripcionGeneral.trim().length;
  if (largo < LIMITES_SOLICITUD.descripcionMinima) {
    errores.descripcionGeneral = 'Cuéntanos un poco más: al menos 10 letras sobre lo que necesitas.';
  } else if (largo > LIMITES_SOLICITUD.descripcionMaxima) {
    errores.descripcionGeneral = 'Usa hasta 2.000 caracteres.';
  }
  return errores;
}

/** El orden visual de los campos, para llevar el foco al primer error. */
export function ordenCampos(items) {
  return [
    ...items.flatMap((_, i) => ['idProducto', 'ancho', 'alto', 'cantidad', 'descripcion', 'opciones'].map((c) => campoItem(i, c))),
    'descripcionGeneral',
    'adjuntos',
  ];
}

/** Los letreros del formulario como los pide la API (medidas en mm). */
export function detallesParaApi(items) {
  return items.map((item) => {
    const ancho = numero(item.ancho);
    const alto = numero(item.alto);
    const conMedidas = ancho !== null && alto !== null;
    return {
      idProducto: Number(item.idProducto),
      descripcion: item.descripcion.trim() || null,
      anchoMm: conMedidas ? cmAMm(ancho) : null,
      altoMm: conMedidas ? cmAMm(alto) : null,
      cantidad: numero(item.cantidad),
      // Lo elegido entre las opciones del producto (material, acabado…); lo que no se eligió no viaja.
      ...(opcionesElegidas(item.opciones) ? { opciones: opcionesElegidas(item.opciones) } : {}),
    };
  });
}

/** Solo los grupos con un valor elegido, o null si no eligió ninguno. */
export function opcionesElegidas(opciones) {
  const elegidas = Object.fromEntries(Object.entries(opciones ?? {}).filter(([, valor]) => valor));
  return Object.keys(elegidas).length ? elegidas : null;
}

/** Las opciones de un producto agrupadas: [{ grupo, valores: [...] }] (la API las manda planas, en orden). */
export function gruposDeOpciones(producto) {
  const grupos = [];
  for (const { grupo, valor } of producto?.opciones ?? []) {
    const existente = grupos.find((g) => g.grupo === grupo);
    if (existente) existente.valores.push(valor);
    else grupos.push({ grupo, valores: [valor] });
  }
  return grupos;
}

const cmTexto = (mm) => formatoCm.format(mm / 10);

/** Lo que el taller hace de un producto, en centímetros: «Ancho de 80 a 150 cm y alto de 200 a 300 cm», o null. */
export function textoMedidas(producto) {
  if (!producto) return null;
  const fijas = producto.medidasFijas ?? [];
  if (fijas.length === 1) return `Medida única: ${cmTexto(fijas[0].anchoMm)} × ${cmTexto(fijas[0].altoMm)} cm.`;
  if (fijas.length > 1) return `Medidas disponibles: ${fijas.map((m) => `${cmTexto(m.anchoMm)} × ${cmTexto(m.altoMm)}`).join(', ')} cm.`;
  if (producto.anchoMinMm == null) return null;
  return `Ancho de ${cmTexto(producto.anchoMinMm)} a ${cmTexto(producto.anchoMaxMm)} cm y alto de ${cmTexto(producto.altoMinMm)} a ${cmTexto(producto.altoMaxMm)} cm.`;
}

const aTextoCm = (mm) => (mm ? String(mm / 10).replace('.', ',') : '');

/**
 * Los detalles del comprobante como letreros del formulario (para editar). Si un detalle no trae su
 * idProducto, se busca el tipo por su nombre en la lista de productos.
 */
export function itemsDesdeDetalles(detalles = [], productos = []) {
  return detalles.map((d) => {
    const id = d.idProducto ?? productos.find((p) => p.nombre === (d.producto?.nombre ?? d.producto))?.idProducto ?? '';
    return { ...itemVacio(id), ancho: aTextoCm(d.anchoMm), alto: aTextoCm(d.altoMm), cantidad: String(d.cantidad ?? 1), descripcion: d.descripcion ?? '', opciones: { ...(d.opciones ?? {}) } };
  });
}

/**
 * Errores por campo de la API ({ 'detalles[0].anchoMm': ['…'], DescripcionGeneral: ['…'] }) con los
 * nombres del formulario ({ 'detalles.0.ancho': '…', descripcionGeneral: '…' }).
 */
export function erroresDesdeApi(errores) {
  const resultado = {};
  for (const [clave, mensajes] of Object.entries(errores ?? {})) {
    const mensaje = Array.isArray(mensajes) ? mensajes[0] : String(mensajes);
    const item = clave.match(/^detalles\[(\d+)\]\.(\w+)$/i);
    if (item) {
      const campo = item[2].charAt(0).toLowerCase() + item[2].slice(1);
      const enFormulario = { anchomm: 'ancho', altomm: 'alto' }[campo.toLowerCase()] ?? campo;
      resultado[campoItem(Number(item[1]), enFormulario)] = mensaje;
      continue;
    }
    const campo = clave.charAt(0).toLowerCase() + clave.slice(1);
    resultado[campo === 'detalles' ? 'detalles.0.idProducto' : campo] = mensaje;
  }
  return resultado;
}

/** «Rosa Pino» desde un cliente o cualquier objeto con nombre y apellido. */
export const nombreCompleto = (persona) => [persona?.nombre, persona?.apellido].filter(Boolean).join(' ') || '—';

/** El número que se muestra: el de la API («#123») o el id. */
export const numeroSolicitud = (s) => s?.numero ?? (s?.idSolicitud ? `#${s.idSolicitud}` : '');

export const ORIGENES_SOLICITUD = { online: 'En línea', presencial: 'Presencial' };

/** Estados en que la solicitud todavía se puede editar o rechazar. */
export const esEditable = (estado) => estado === 'solicitado' || estado === 'cotizado';

/** Teléfono para mostrar: 912345678 → «9 1234 5678». */
export function telefonoVisible(telefono) {
  const digitos = String(telefono ?? '').replace(/\D/g, '').replace(/^56(?=9\d{8}$)/, '');
  return /^9\d{8}$/.test(digitos) ? `${digitos[0]} ${digitos.slice(1, 5)} ${digitos.slice(5)}` : (telefono || '—');
}

export const MENSAJE_CONTACTO_DIRECTO = 'Escríbenos por WhatsApp o llámanos y te ayudamos con lo que necesitas. Lo que escribiste sigue aquí.';
