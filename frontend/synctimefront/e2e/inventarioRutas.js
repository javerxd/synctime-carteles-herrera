// Fase 15B (RNFP15, RNFE08): el inventario de rutas de la aplicación, leído de src/App.jsx, para comprobar que el recorrido de accesibilidad
// (axe a 360, 768 y 1440 px) abre TODAS. Se prueba en src/test/fase15bRutas.test.js con un App.jsx de juguete y con el verdadero.

const JUNTAR = (base, parte) => `${base}/${parte}`.replace(/\/+/g, '/').replace(/(.)\/$/, '$1');

/** Lee un atributo de texto de las propiedades de una etiqueta: path="…". */
const atributo = (propiedades, nombre) => propiedades.match(new RegExp(`(?:^|\\s)${nombre}="([^"]*)"`))?.[1] ?? null;

/**
 * Las rutas de un App.jsx de React Router escrito con <Route>: [{ ruta, tipo }].
 * `tipo` es 'pantalla' (muestra algo), 'redireccion' (su elemento es <Navigate>) o 'noEncontrada' (path="*").
 * Un contenedor (con hijos) no cuenta; una `index` es la ruta de su contenedor.
 */
export function rutasDeApp(fuente) {
  const rutas = [];
  const pila = [];
  const buscador = /<Route\b|<\/Route>/g;
  let coincidencia;
  while ((coincidencia = buscador.exec(fuente))) {
    if (coincidencia[0] === '</Route>') {
      pila.pop();
      continue;
    }
    // Recorre las propiedades hasta el «>» que cierra la etiqueta: lo que va entre llaves y entre comillas no cuenta.
    let i = buscador.lastIndex;
    let llaves = 0;
    let comilla = null;
    for (; i < fuente.length; i += 1) {
      const c = fuente[i];
      if (comilla) { if (c === comilla) comilla = null; continue; }
      if (c === '"' || c === "'" || c === '`') comilla = c;
      else if (c === '{') llaves += 1;
      else if (c === '}') llaves -= 1;
      else if (c === '>' && llaves === 0) break;
    }
    const propiedades = fuente.slice(buscador.lastIndex, i);
    const autocerrada = fuente[i - 1] === '/';
    const path = atributo(propiedades, 'path');
    const esIndice = /(?:^|\s)index(?:\s|\/|$)/.test(propiedades);
    const base = pila[pila.length - 1] ?? '';
    const propia = path === null ? base : JUNTAR(base, path === '*' ? '*' : path);
    if (path !== null || esIndice) {
      const tipo = path === '*' ? 'noEncontrada' : /<Navigate\b/.test(propiedades) ? 'redireccion' : 'pantalla';
      // Una pantalla es una ruta sin hijos o una ruta de índice; un contenedor con hijos (la estructura del panel, Mantenedores) no.
      if (esIndice || autocerrada) rutas.push({ ruta: propia === '' ? '/' : propia, tipo });
    }
    buscador.lastIndex = i;
    if (!autocerrada) pila.push(propia);
  }
  return rutas.map((r) => ({ ...r, ruta: r.ruta.startsWith('/') ? r.ruta : `/${r.ruta}` }));
}

/** La expresión que reconoce una dirección concreta como esa ruta: «/panel/pedidos/:id» ↔ «/panel/pedidos/7». */
export function expresionDeRuta(ruta) {
  const cuerpo = ruta.replace(/[.+?^${}()|[\]\\]/g, '\\$&').replace(/:[A-Za-z]+/g, '[^/]+');
  return new RegExp(`^${cuerpo}$`);
}

/** ¿Alguna de las direcciones (con su ?consulta) abre esa ruta? */
export const cubre = (direcciones, ruta) => {
  const expresion = expresionDeRuta(ruta);
  return direcciones.some((d) => expresion.test(d.split('?')[0].split('#')[0]));
};
