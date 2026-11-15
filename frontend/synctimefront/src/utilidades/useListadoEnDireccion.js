import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useConsulta } from './useConsulta';
import { useDebounce } from './useDebounce';
import {
  camposRechazados, filtrosDesde, hayFiltrosAplicados, paraDireccion, parametrosInvalidos,
} from './filtrosUrl';

/**
 * Un listado paginado cuyos filtros viven en la dirección (el patrón de la bandeja de solicitudes):
 * - `cambiar(cambios)` vuelve a la página 1 salvo que se pida otra; `limpiar()` deja todo como al inicio.
 * - La búsqueda se escribe en `texto` y se aplica cuando la persona deja de escribir; si la dirección
 *   cambia por navegación (menú, atrás), el campo la sigue sin volver a aplicar lo anterior.
 * - Un parámetro inválido, o rechazado por la API con 400, se quita en silencio.
 * - Una página fuera de rango lleva a la última que existe.
 * `consultar(filtros, senal)` devuelve la promesa del listado; `clave` identifica la consulta (caché de useConsulta).
 * `actualizando`: hay filas a la vista, pero corresponden al filtro anterior y ya se piden las nuevas.
 */
export function useListadoEnDireccion(definicion, { clave, consultar }) {
  const [parametros, setParametros] = useSearchParams();
  const filtros = filtrosDesde(parametros, definicion);
  // Al cambiar de filtro o de página se siguen viendo las filas anteriores (recargando) y la petición vieja se corta.
  const consulta = useConsulta(`${clave}:${JSON.stringify(filtros)}`, (senal) => consultar(filtros, senal), { conservar: true });

  function cambiar(cambios) {
    setParametros(paraDireccion({ ...filtros, pagina: 1, ...cambios }, definicion), { replace: true });
  }

  const tieneBusqueda = 'q' in definicion.campos;
  const [texto, setTextoInterno] = useState(filtros.q ?? '');
  const buscado = useDebounce(texto);
  const qActual = filtros.q ?? '';
  const [qVisto, setQVisto] = useState(qActual);
  if (qActual !== qVisto) {
    setQVisto(qActual);
    setTextoInterno(qActual);
  }
  const refTecleo = useRef(false);
  const refCambiar = useRef(cambiar);
  useEffect(() => {
    refCambiar.current = cambiar;
  });
  useEffect(() => {
    if (!tieneBusqueda || !refTecleo.current) return;
    refTecleo.current = false;
    if (buscado !== qActual) refCambiar.current({ q: buscado });
  }, [buscado, qActual, tieneBusqueda]);

  function escribir(valor) {
    refTecleo.current = true;
    setTextoInterno(valor);
  }

  const rechazados = camposRechazados(consulta.error, definicion);
  const aQuitar = [...new Set([...parametrosInvalidos(parametros, definicion), ...rechazados])].join(',');
  useEffect(() => {
    if (!aQuitar) return;
    setParametros((actuales) => {
      const siguiente = new URLSearchParams(actuales);
      aQuitar.split(',').forEach((c) => siguiente.delete(c));
      return siguiente;
    }, { replace: true });
  }, [aQuitar, setParametros]);

  const ultimaPagina = consulta.datos?.totalPaginas ?? 0;
  useEffect(() => {
    if (ultimaPagina > 0 && filtros.pagina > ultimaPagina) refCambiar.current({ pagina: ultimaPagina });
  }, [ultimaPagina, filtros.pagina]);

  function limpiar() {
    setTextoInterno('');
    setParametros({}, { replace: true });
  }

  return {
    filtros,
    consulta,
    cambiar,
    limpiar,
    texto,
    escribir,
    hayFiltros: hayFiltrosAplicados(filtros, definicion),
    actualizando: consulta.recargando,
    // Un 400 por un filtro se corrige solo: no es un error que mostrar.
    errorVisible: consulta.error && !rechazados.length ? consulta.error : null,
  };
}
