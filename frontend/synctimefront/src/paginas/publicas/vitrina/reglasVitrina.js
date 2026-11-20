// Búsqueda de la vitrina (fase 12C, decisión 5): al escribir, la lista se filtra por «contiene»; los
// títulos que empiezan con lo escrito van primero. Sin distinguir mayúsculas ni tildes.

/** Minúsculas, sin tildes ni espacios sobrantes: «Señalización» -> «senalizacion». */
export function normalizar(texto) {
  return String(texto ?? '').normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase().trim();
}

/**
 * Trabajos que cumplen la categoría (si hay) y contienen el texto en el título, el tipo de letrero o la
 * categoría. Orden: primero los de título que empieza con el texto; el resto conserva su orden.
 */
export function filtrarTrabajos(trabajos, { texto = '', categoria = null } = {}) {
  const buscado = normalizar(texto);
  const porCategoria = categoria === null || categoria === undefined
    ? trabajos
    : trabajos.filter((t) => t.categoria?.idCategoria === categoria);
  if (!buscado) return porCategoria;

  const conPrefijo = [];
  const conContenido = [];
  for (const trabajo of porCategoria) {
    const titulo = normalizar(trabajo.titulo);
    if (titulo.startsWith(buscado)) conPrefijo.push(trabajo);
    else {
      // Con varias palabras, cada una debe aparecer en el título, el tipo o la categoría (en cualquier orden).
      const conjunto = [titulo, normalizar(trabajo.tipoLetrero), normalizar(trabajo.categoria?.nombre)].join(' ');
      if (buscado.split(/\s+/).every((palabra) => conjunto.includes(palabra))) conContenido.push(trabajo);
    }
  }
  return [...conPrefijo, ...conContenido];
}
