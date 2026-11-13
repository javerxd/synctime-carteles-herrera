import { FilterX } from 'lucide-react';
import { Boton } from './Boton';
import { unirClases } from './clases';
import estilos from './BarraFiltros.module.css';

/**
 * Zona de filtros de un listado (role="search", con nombre propio). Los controles van como hijos.
 * `resumen` («12 pedidos · $340.000 por cobrar») se anuncia al cambiar; «Limpiar filtros» aparece
 * solo cuando hay algún filtro aplicado.
 */
export function BarraFiltros({ titulo = 'Filtros', resumen, hayFiltros = false, alLimpiar, className, children }) {
  return (
    <div role="search" aria-label={titulo} className={unirClases(estilos.barra, className)}>
      <div className={estilos.controles}>{children}</div>
      <div className={estilos.pie}>
        <p role="status" className={estilos.resumen}>{resumen}</p>
        {hayFiltros && (
          <Boton variante="fantasma" tamano="compacto" iconoInicio={FilterX} onClick={alLimpiar}>
            Limpiar filtros
          </Boton>
        )}
      </div>
    </div>
  );
}
