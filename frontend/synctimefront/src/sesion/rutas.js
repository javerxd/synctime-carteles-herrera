// Rutas a las que se puede volver tras ingresar: solo internas del sitio (empiezan con una barra que
// no va seguida de otra barra ni de una barra invertida, que el navegador leería como otro dominio) y
// nunca las propias pantallas de ingreso, registro o recuperación.
const EXCLUIDAS = ['/ingresar', '/personal/ingresar', '/cuenta/registro', '/cuenta/recuperar'];

/** El destino si es una ruta interna permitida; si no, `porDefecto`. */
export function rutaInterna(destino, porDefecto) {
  if (typeof destino !== 'string' || !/^\/(?![/\\])/.test(destino)) return porDefecto;
  const ruta = destino.split(/[?#]/)[0].replace(/\/+$/, '') || '/';
  return EXCLUIDAS.includes(ruta) ? porDefecto : destino;
}
