// Fase 15B (RNFP27, RNFP15): sirve el build de producción (dist/) como lo serviría un hosting (gzip, caché larga para /assets/) y responde
// /api/v1/** con la API SIMULADA de las pruebas de Playwright (e2e/apiSimulada.js). Sirve para medir con Lighthouse páginas completas sin
// usar la API demo ni la base de datos. Nunca lleva credenciales: la sesión es un token falso de una hora.
//
//   npx vite build && node scripts/servidor-evidencia.mjs --puerto 4174 --sesion ninguna|personal|cliente
//
// Con --sesion personal o cliente, el HTML lleva un script propio (/__sesion.js) que marca la sesión (como la que recupera la aplicación
// al cargar), para poder medir pantallas del panel y de la cuenta.
import { createReadStream, existsSync, readFileSync, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { extname, join, normalize } from 'node:path';
import { gzipSync } from 'node:zlib';
import { CLIENTE, MI_CUENTA, RESPUESTAS, tokenDePrueba } from '../e2e/apiSimulada.js';

const argumento = (nombre, defecto) => {
  const i = process.argv.indexOf(`--${nombre}`);
  return i > -1 ? process.argv[i + 1] : defecto;
};
const PUERTO = Number(argumento('puerto', '4174'));
const SESION = argumento('sesion', 'ninguna');
const DIST = join(process.cwd(), 'dist');
const TIPOS = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.svg': 'image/svg+xml',
  '.png': 'image/png', '.webp': 'image/webp', '.woff2': 'font/woff2', '.ico': 'image/x-icon', '.txt': 'text/plain', '.map': 'application/json',
};
const COMPRIMIBLES = new Set(['.html', '.js', '.css', '.json', '.svg', '.txt']);

const SESIONES = {
  personal: { tipo: 'empleado', usuario: { idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', email: 'dueno@letreria.test', rol: 'dueño' } },
  cliente: { tipo: 'cliente', usuario: { ...CLIENTE, correoVerificado: true } },
};

function responderApi(peticion, respuesta, url) {
  const destino = url.pathname.replace(/\/$/, '');
  const enviar = (estado, cuerpo) => {
    respuesta.writeHead(estado, { 'Content-Type': 'application/json' });
    respuesta.end(cuerpo === undefined ? '' : JSON.stringify(cuerpo));
  };
  if (destino === '/api/v1/auth/renovar') return SESIONES[SESION] ? enviar(200, { token: tokenDePrueba(), ...SESIONES[SESION] }) : enviar(401, { error: 'Sin sesión' });
  if (destino === '/api/v1/auth/salir') return enviar(204);
  if (destino === '/api/v1/mi-cuenta') return enviar(200, { ...MI_CUENTA, correoVerificado: true });
  const cuerpo = RESPUESTAS[destino];
  if (typeof cuerpo === 'function') return enviar(200, cuerpo(url.searchParams));
  return cuerpo ? enviar(200, cuerpo) : enviar(404, { error: 'No encontrado' });
}

const INDICE = readFileSync(join(DIST, 'index.html'), 'utf8');
const INDICE_SERVIDO = SESIONES[SESION] ? INDICE.replace('<script type="module"', '<script src="/__sesion.js"></script><script type="module"') : INDICE;

createServer((peticion, respuesta) => {
  const url = new URL(peticion.url, `http://localhost:${PUERTO}`);
  if (url.pathname.startsWith('/api/')) return responderApi(peticion, respuesta, url);
  if (url.pathname === '/__sesion.js') {
    respuesta.writeHead(200, { 'Content-Type': 'text/javascript' });
    return respuesta.end("sessionStorage.setItem('synctime_hay_sesion', '1');");
  }
  const archivo = normalize(join(DIST, decodeURIComponent(url.pathname)));
  const existe = archivo.startsWith(DIST) && existsSync(archivo) && statSync(archivo).isFile();
  const extension = existe ? extname(archivo) : '.html';
  const cabeceras = { 'Content-Type': TIPOS[extension] ?? 'application/octet-stream' };
  // Como un hosting: los archivos con huella en el nombre (/assets/) se guardan un año; el HTML se revalida siempre.
  cabeceras['Cache-Control'] = url.pathname.startsWith('/assets/') ? 'public, max-age=31536000, immutable' : 'no-cache';
  const aceptaGzip = /\bgzip\b/.test(peticion.headers['accept-encoding'] ?? '');
  if (!existe || extension === '.html') {
    const html = Buffer.from(extension === '.html' && existe && url.pathname !== '/' ? readFileSync(archivo) : INDICE_SERVIDO);
    if (aceptaGzip) { cabeceras['Content-Encoding'] = 'gzip'; respuesta.writeHead(200, cabeceras); return respuesta.end(gzipSync(html)); }
    respuesta.writeHead(200, cabeceras);
    return respuesta.end(html);
  }
  if (aceptaGzip && COMPRIMIBLES.has(extension)) {
    cabeceras['Content-Encoding'] = 'gzip';
    respuesta.writeHead(200, cabeceras);
    return respuesta.end(gzipSync(readFileSync(archivo)));
  }
  respuesta.writeHead(200, cabeceras);
  return createReadStream(archivo).pipe(respuesta);
}).listen(PUERTO, () => console.info(`Servidor de evidencia en http://localhost:${PUERTO} (sesión: ${SESION}). Ctrl+C para detenerlo.`));
