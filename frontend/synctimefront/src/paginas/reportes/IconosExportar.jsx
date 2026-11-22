// Fase 13B: los iconos de los botones de exportación, en SVG propio (no la imagen de referencia): una hoja con su insignia («X»,
// «CSV» o «PDF») y la flecha de descarga. Escalan sin pérdida y heredan el color del texto (`currentColor`).
const INSIGNIAS = { xlsx: 'X', csv: 'CSV', pdf: 'PDF' };

export function IconoArchivo({ formato, tamano = 28 }) {
  const insignia = INSIGNIAS[formato] ?? '';
  const ancha = insignia.length > 1;
  return (
    <svg width={tamano} height={tamano} viewBox="0 0 32 32" fill="none" aria-hidden="true" focusable="false">
      {/* La hoja, con la esquina doblada */}
      <path d="M9 3h9l6 6v9" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" opacity="0.7" />
      <path d="M18 3v6h6" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" opacity="0.7" />
      <path d="M9 12V3" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" opacity="0.7" />
      {/* La insignia del formato */}
      <rect x="2.5" y="12" width={ancha ? 15 : 11} height="9" rx="2" fill="currentColor" />
      <text x={2.5 + (ancha ? 7.5 : 5.5)} y="18.9" textAnchor="middle" fontSize={ancha ? 5.6 : 7} fontWeight="700" fontFamily="inherit"
        fill="var(--color-fondo)" stroke="none">{insignia}</text>
      {/* La flecha de descarga */}
      <path d="M21 17v7m0 0-3-3m3 3 3-3" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M17.5 28h7" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  );
}
