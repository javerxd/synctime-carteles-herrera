import { unirClases } from './clases';
import estilos from './Esqueleto.module.css';

/**
 * Silueta de carga con un brillo sutil. Es decorativa (aria-hidden): quien la usa debe anunciar
 * la carga con texto, por ejemplo con un role="status" «Cargando pedidos…».
 * forma: "linea" (con `lineas` renglones), "bloque" o "circulo".
 */
export function Esqueleto({ forma = 'linea', lineas = 1, ancho, alto, className, style }) {
  const medidas = { width: ancho, height: alto, ...style };
  if (forma === 'linea' && lineas > 1) {
    return (
      <div aria-hidden="true" className={unirClases(estilos.grupo, className)} style={{ width: ancho, ...style }}>
        {Array.from({ length: lineas }, (_, i) => (
          // La última línea más corta, como un párrafo real.
          <span key={i} className={unirClases(estilos.esqueleto, estilos.linea)} style={i === lineas - 1 ? { width: '60%' } : undefined} />
        ))}
      </div>
    );
  }
  return <span aria-hidden="true" className={unirClases(estilos.esqueleto, estilos[forma], className)} style={medidas} />;
}
