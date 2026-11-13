import { ChevronLeft, ChevronRight } from 'lucide-react';
import { unirClases } from './clases';
import estilos from './Paginacion.module.css';

// La primera, la última, la actual y sus vecinas; el resto se resume con «…».
function paginasVisibles(pagina, total) {
  return [...new Set([1, pagina - 1, pagina, pagina + 1, total])]
    .filter((p) => p >= 1 && p <= total)
    .sort((a, b) => a - b);
}

/**
 * Paginación de listados. Cada botón dice a qué página lleva; la actual lleva aria-current.
 * En los extremos, «Anterior» y «Siguiente» quedan inactivos (aria-disabled) pero enfocables, para
 * que el foco no se pierda al llegar a la primera o la última página. No se dibuja si hay una sola.
 */
export function Paginacion({ pagina, totalPaginas, alCambiar, etiqueta = 'Paginación', className }) {
  if (!totalPaginas || totalPaginas <= 1) return null;
  const actual = Math.min(Math.max(1, pagina), totalPaginas);
  const paginas = paginasVisibles(actual, totalPaginas);
  const irA = (destino) => destino >= 1 && destino <= totalPaginas && destino !== actual && alCambiar(destino);

  return (
    <nav aria-label={etiqueta} className={unirClases(estilos.paginacion, className)}>
      <ul className={estilos.lista}>
        <li>
          <button type="button" aria-label="Página anterior" aria-disabled={actual <= 1 ? 'true' : undefined}
            onClick={() => irA(actual - 1)} className={unirClases(estilos.boton, estilos.extremo)}>
            <ChevronLeft size={18} strokeWidth={1.75} aria-hidden="true" />
            <span className={estilos.textoExtremo}>Anterior</span>
          </button>
        </li>
        {paginas.map((numero, i) => (
          <PaginaConSalto key={numero} numero={numero} anterior={paginas[i - 1]} actual={numero === actual} alCambiar={irA} />
        ))}
        <li>
          <button type="button" aria-label="Página siguiente" aria-disabled={actual >= totalPaginas ? 'true' : undefined}
            onClick={() => irA(actual + 1)} className={unirClases(estilos.boton, estilos.extremo)}>
            <span className={estilos.textoExtremo}>Siguiente</span>
            <ChevronRight size={18} strokeWidth={1.75} aria-hidden="true" />
          </button>
        </li>
      </ul>
    </nav>
  );
}

function PaginaConSalto({ numero, anterior, actual, alCambiar }) {
  return (
    <>
      {anterior !== undefined && numero - anterior > 1 && (
        <li aria-hidden="true" className={estilos.salto}>…</li>
      )}
      <li>
        <button type="button" aria-label={`Página ${numero}`} aria-current={actual ? 'page' : undefined}
          onClick={() => alCambiar(numero)} className={estilos.boton}>
          {numero}
        </button>
      </li>
    </>
  );
}
