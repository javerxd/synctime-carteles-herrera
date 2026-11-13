import { useMemo } from 'react';
import { encode } from 'uqr';

/**
 * Código QR dibujado como SVG propio a partir de la matriz que calcula uqr (licencia MIT, sin
 * dependencias). Tinta sobre blanco con margen de 2 módulos, legible por cualquier lector.
 */
export function CodigoQr({ texto, titulo, idSvg }) {
  const { tamano, camino } = useMemo(() => {
    const { data } = encode(texto, { border: 2, ecc: 'M' });
    const filas = [];
    data.forEach((fila, y) => fila.forEach((lleno, x) => lleno && filas.push(`M${x},${y}h1v1h-1z`)));
    return { tamano: data.length, camino: filas.join('') };
  }, [texto]);

  return (
    <svg id={idSvg} xmlns="http://www.w3.org/2000/svg" viewBox={`0 0 ${tamano} ${tamano}`} role="img" aria-label={titulo}
      shapeRendering="crispEdges">
      <rect width={tamano} height={tamano} fill="#FFFFFF" />
      <path d={camino} fill="#0F172A" />
    </svg>
  );
}

function descargar(nombre, blob) {
  const url = URL.createObjectURL(blob);
  const enlace = Object.assign(document.createElement('a'), { href: url, download: nombre });
  document.body.append(enlace);
  enlace.click();
  enlace.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export function descargarSvg(idSvg, nombre) {
  const svg = document.getElementById(idSvg);
  if (svg) descargar(`${nombre}.svg`, new Blob([svg.outerHTML], { type: 'image/svg+xml' }));
}

/** PNG de 1024 px, para imprimir en una adhesiva o pegar en la vitrina del local. */
export function descargarPng(idSvg, nombre) {
  const svg = document.getElementById(idSvg);
  if (!svg) return;
  const imagen = new Image();
  const url = URL.createObjectURL(new Blob([svg.outerHTML], { type: 'image/svg+xml' }));
  imagen.onload = () => {
    const lienzo = Object.assign(document.createElement('canvas'), { width: 1024, height: 1024 });
    const contexto = lienzo.getContext('2d');
    contexto.imageSmoothingEnabled = false;
    contexto.drawImage(imagen, 0, 0, 1024, 1024);
    URL.revokeObjectURL(url);
    lienzo.toBlob((blob) => blob && descargar(`${nombre}.png`, blob), 'image/png');
  };
  imagen.src = url;
}
