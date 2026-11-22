import { useEffect, useRef, useState } from 'react';

// Reglas de los diseños adjuntos (fase 7). El servidor vuelve a comprobarlas: esto solo evita subir en vano.
export const REGLAS_ADJUNTOS = {
  maximo: 5,
  bytesMaximos: 10 * 1024 * 1024,
  tipos: ['image/jpeg', 'image/png', 'image/webp', 'application/pdf'],
  extensiones: ['jpg', 'jpeg', 'png', 'webp', 'pdf'],
};
export const ACEPTA_ADJUNTOS = REGLAS_ADJUNTOS.tipos.join(',');
/** El texto que se muestra junto al archivo cuando la subida falla. */
export function mensajeSubida(falla) {
  switch (falla?.codigo) {
    case 'correo_sin_confirmar':
      return 'Confirma tu correo para adjuntar archivos: abre el enlace que te enviamos y vuelve a intentarlo.';
    case 'demasiados_intentos':
      return 'Subiste muchos archivos seguidos. Espera un minuto y vuelve a intentarlo.';
    case 'tope_adjuntos':
      return 'Ya tienes 5 archivos guardados, contando los que subiste antes y no enviaste. Quita uno de la lista para adjuntar este.';
    case 'tiempo_agotado':
      return 'La subida tardó demasiado. Revisa tu conexión e inténtalo de nuevo.';
    default:
      if (falla?.estado === 429) return 'Subiste muchos archivos seguidos. Espera un minuto y vuelve a intentarlo.';
      return falla?.message || 'No pudimos subirlo. Inténtalo de nuevo.';
  }
}

export const MENSAJE_FORMATO = 'Formato no aceptado. Sube JPG, PNG, WebP o PDF; si tu diseño es SVG o AI, expórtalo a PDF o PNG.';

const megas = (bytes) => new Intl.NumberFormat('es-CL', { maximumFractionDigits: 1 }).format(bytes / (1024 * 1024));

/** Peso legible: «850 KB» o «2,4 MB». */
export function pesoLegible(bytes) {
  if (!Number.isFinite(bytes)) return '';
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  return `${megas(bytes)} MB`;
}

const extension = (nombre) => String(nombre ?? '').split('.').pop().toLowerCase();

/** El motivo por el que un archivo no se puede adjuntar, o null si sirve. */
export function validarArchivo(archivo) {
  // Algunos teléfonos no informan el tipo: entonces decide la extensión.
  const tipoValido = archivo.type ? REGLAS_ADJUNTOS.tipos.includes(archivo.type) : REGLAS_ADJUNTOS.extensiones.includes(extension(archivo.name));
  if (!tipoValido) return MENSAJE_FORMATO;
  if (archivo.size === 0) return 'El archivo está vacío. Elige otro.';
  if (archivo.size > REGLAS_ADJUNTOS.bytesMaximos) {
    return `Es demasiado grande (${megas(archivo.size)} MB). El máximo es 10 MB por archivo.`;
  }
  return null;
}

export const esPdf = (archivo) => archivo.type === 'application/pdf' || extension(archivo.name) === 'pdf';

export function movimientoReducido() {
  return typeof window !== 'undefined' && typeof window.matchMedia === 'function'
    && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

export const crearUrl = (blob) => (typeof URL.createObjectURL === 'function' ? URL.createObjectURL(blob) : null);
export const liberarUrl = (url) => {
  if (url && typeof URL.revokeObjectURL === 'function') URL.revokeObjectURL(url);
};

/**
 * Un archivo privado (adjunto) descargado con la sesión y mostrado con URL.createObjectURL. La URL se
 * libera al desmontar o al cambiar de archivo. `cargar(senal)` devuelve una promesa de Blob; la señal se cancela en esos casos.
 */
export function useArchivoPrivado(cargar, clave) {
  const [estado, setEstado] = useState({ clave: null, url: null, error: null });
  const refCargar = useRef(cargar);
  useEffect(() => {
    refCargar.current = cargar;
  });

  useEffect(() => {
    if (clave === null || clave === undefined) return undefined;
    let vigente = true;
    let url = null;
    // La descarga recibe una señal que se cancela al cambiar de archivo o al desmontar (RNFP23): con StrictMode en desarrollo
    // el montaje doble ya no deja dos descargas completas de la misma miniatura.
    const control = new AbortController();
    Promise.resolve()
      .then(() => refCargar.current(control.signal))
      .then((blob) => {
        if (!vigente) return;
        url = crearUrl(blob);
        setEstado({ clave, url, error: null });
      }, (error) => vigente && setEstado({ clave, url: null, error }));
    return () => {
      vigente = false;
      control.abort();
      liberarUrl(url);
    };
  }, [clave]);

  return estado.clave === clave ? estado : { clave, url: null, error: null };
}
