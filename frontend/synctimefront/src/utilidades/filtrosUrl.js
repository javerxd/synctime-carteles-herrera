// Filtros de un listado que viven en la dirección (fase 8, como la bandeja de solicitudes de la fase 7):
// así se puede volver atrás, recargar o compartir la vista. Cada listado declara sus campos con un
// validador; lo que no sirve se ignora y se limpia de la dirección, sin mostrarlo como error.
//
// definicion: { campos: { estado: (valor, filtros) => bool, ... }, predeterminados: { orden: 'fecha_desc' }, tamano: 20 }

/** Filtros desde la dirección: { ...campos, pagina }. Un valor inválido queda vacío (o en su predeterminado). */
export function filtrosDesde(parametros, definicion) {
  const { campos, predeterminados = {} } = definicion;
  const crudos = Object.fromEntries(Object.keys(campos).map((c) => [c, parametros.get(c) ?? '']));
  const filtros = Object.fromEntries(Object.entries(campos).map(([c, valido]) => {
    const valor = crudos[c];
    const sirve = valor !== '' && valido(valor, crudos);
    return [c, sirve ? valor : predeterminados[c] ?? ''];
  }));
  filtros.pagina = Math.max(1, Number.parseInt(parametros.get('pagina') ?? '1', 10) || 1);
  return filtros;
}

/** Los parámetros de la dirección que sobran o no sirven (para quitarlos en silencio). */
export function parametrosInvalidos(parametros, definicion) {
  const filtros = filtrosDesde(parametros, definicion);
  const malos = [];
  for (const [clave, valor] of parametros.entries()) {
    if (clave === 'pagina') {
      if (String(filtros.pagina) !== valor || filtros.pagina === 1) malos.push(clave);
    } else if (!(clave in definicion.campos) || filtros[clave] !== valor || valor === (definicion.predeterminados?.[clave] ?? '')) {
      malos.push(clave);
    }
  }
  return malos;
}

/** Los campos que la API rechazó con 400 (errores por campo): se quitan de la dirección. */
export function camposRechazados(error, definicion) {
  if (error?.estado !== 400 || !error.errores) return [];
  const validos = [...Object.keys(definicion.campos), 'pagina'];
  return Object.keys(error.errores).map((c) => c.charAt(0).toLowerCase() + c.slice(1)).filter((c) => validos.includes(c));
}

/** Lo que va a la dirección: sin vacíos, sin la página 1 y sin los valores predeterminados. */
export function paraDireccion(filtros, definicion) {
  const predeterminados = definicion.predeterminados ?? {};
  return Object.fromEntries(Object.entries(filtros)
    .filter(([clave, valor]) => valor !== '' && valor !== undefined && valor !== null
      && !(clave === 'pagina' && Number(valor) === 1) && predeterminados[clave] !== valor)
    .map(([clave, valor]) => [clave, String(valor)]));
}

/** Los parámetros que se piden a la API (los vacíos no viajan; la búsqueda va sin espacios de sobra). */
export function parametrosBase(filtros, definicion) {
  const parametros = {};
  for (const campo of Object.keys(definicion.campos)) {
    const valor = campo === 'q' ? String(filtros.q ?? '').trim() : filtros[campo];
    parametros[campo] = valor === '' || valor === undefined ? undefined : valor;
  }
  parametros.pagina = filtros.pagina;
  parametros.tamano = definicion.tamano ?? 20;
  return parametros;
}

/** ¿Hay algo distinto de lo predeterminado? (para «Limpiar filtros»). */
export function hayFiltrosAplicados(filtros, definicion) {
  const predeterminados = definicion.predeterminados ?? {};
  return Object.keys(definicion.campos).some((c) => (filtros[c] ?? '') !== (predeterminados[c] ?? ''));
}
