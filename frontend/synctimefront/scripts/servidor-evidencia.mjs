// Fase 15B (RNFP27, RNFP15): sirve el build de producción (dist/) como lo serviría un hosting (gzip, caché larga para /assets/) y responde
// /api/v1/** con la API SIMULADA de las pruebas de Playwright (e2e/apiSimulada.js). Sirve para medir con Lighthouse páginas completas sin
// usar la API demo ni la base de datos. Nunca lleva credenciales: la sesión es un token falso de una hora.
//
//   npx vite build && node scripts/servidor-evidencia.mjs --puerto 4174 --sesion ninguna|personal|cliente
//
// Con --sesion personal o cliente, la aplicación arranca con esa sesión (para medir pantallas del panel y de la cuenta); con ninguna, se
// ingresa con una cuenta de prueba: cualquier correo y la contraseña de prueba del script. Ingresar, registrarse, enviar una solicitud o
// registrar un abono responde como la API pero NO guarda nada (la API demo y la base de datos no se usan).
import { createReadStream, existsSync, readFileSync, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { extname, join, normalize } from 'node:path';
import { gzipSync } from 'node:zlib';
import { CLIENTE, COMPROBANTE, FICHA_PEDIDO, CLIENTES, MI_CUENTA, RESPUESTAS, tokenDePrueba } from '../e2e/apiSimulada.js';

const argumento = (nombre, defecto) => {
  const i = process.argv.indexOf(`--${nombre}`);
  return i > -1 ? process.argv[i + 1] : defecto;
};
const PUERTO = Number(argumento('puerto', '4174'));
// La sesión cambia al ingresar, registrarse y salir, como en una API de verdad (solo en la memoria de este proceso).
let sesionActual = argumento('sesion', 'ninguna');
// Contraseña de las cuentas de PRUEBA de este servidor simulado (ninguna cuenta real existe aquí): cualquier otra da el error de credenciales.
const CLAVE_DE_PRUEBA = 'clave-de-prueba';
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

function leerCuerpo(peticion) {
  return new Promise((resolver) => {
    const partes = [];
    peticion.on('data', (p) => partes.push(p));
    peticion.on('end', () => {
      try { resolver(JSON.parse(Buffer.concat(partes).toString('utf8') || '{}')); } catch { resolver({}); }
    });
  });
}

async function responderApi(peticion, respuesta, url) {
  const destino = url.pathname.replace(/\/$/, '');
  const enviar = (estado, cuerpo) => {
    respuesta.writeHead(estado, { 'Content-Type': 'application/json' });
    respuesta.end(cuerpo === undefined ? '' : JSON.stringify(cuerpo));
  };
  const metodo = peticion.method;
  const cuerpoPedido = metodo === 'GET' ? {} : await leerCuerpo(peticion);
  if (destino === '/api/v1/auth/renovar') return SESIONES[sesionActual] ? enviar(200, { token: tokenDePrueba(), ...SESIONES[sesionActual] }) : enviar(401, { error: 'Sin sesión' });
  if (destino === '/api/v1/auth/salir') { sesionActual = 'ninguna'; return enviar(204); }
  if (metodo === 'POST' && (destino === '/api/v1/auth/login' || destino === '/api/v1/auth/login-cliente')) {
    if (cuerpoPedido.password !== CLAVE_DE_PRUEBA) return enviar(401, { error: 'Correo o contraseña incorrectos.', codigo: 'credenciales_invalidas' });
    sesionActual = destino.endsWith('login') ? 'personal' : 'cliente';
    return enviar(200, { token: tokenDePrueba(), ...SESIONES[sesionActual] });
  }
  if (metodo === 'POST' && destino === '/api/v1/auth/registro-cliente') { sesionActual = 'cliente'; return enviar(201, { token: tokenDePrueba(), ...SESIONES.cliente }); }
  if (metodo === 'POST' && destino === '/api/v1/mis/solicitudes') return enviar(201, COMPROBANTE);
  if (metodo === 'POST' && destino === '/api/v1/mis/adjuntos') return enviar(201, { idAdjunto: 41, tipo: 'imagen', nombre: 'adjunto-de-prueba.png', bytes: 70, ancho: 1, alto: 1, urlMiniatura: '/api/v1/adjuntos/41/miniatura' });
  if (metodo === 'POST' && /\/pedidos\/\d+\/pagos$/.test(destino)) return enviar(201, { ...FICHA_PEDIDO, montoAbonado: 59065, saldoPendiente: 40000 });
  if (metodo === 'DELETE') return enviar(204);
  if (metodo !== 'GET') return enviar(200, {});
  if (/^\/api\/v1\/adjuntos\/\d+\/(miniatura|archivo)$/.test(destino)) { respuesta.writeHead(200, { 'Content-Type': 'image/webp' }); return respuesta.end(readFileSync(join(DIST, 'marca', 'logo-lh-256.webp'))); }
  if (destino === '/api/v1/clientes') {
    const q = (url.searchParams.get('q') ?? '').toLowerCase();
    const elementos = CLIENTES.elementos.filter((c) => !q || [c.nombre, c.apellido, c.telefono, c.rut].filter(Boolean).join(' ').toLowerCase().includes(q));
    return enviar(200, { ...CLIENTES, elementos, total: elementos.length });
  }
  if (destino === '/api/v1/mi-cuenta') return enviar(200, { ...MI_CUENTA, correoVerificado: true });
  const cuerpo = RESPUESTAS[destino];
  if (typeof cuerpo === 'function') return enviar(200, cuerpo(url.searchParams));
  return cuerpo ? enviar(200, cuerpo) : enviar(404, { error: 'No encontrado' });
}

const INDICE = readFileSync(join(DIST, 'index.html'), 'utf8');
// El marcador de sesión del navegador se pone siempre: sin sesión, /auth/renovar responde 401 y la aplicación sigue como visitante.
const INDICE_SERVIDO = INDICE.replace('<script type="module"', '<script src="/__sesion.js"></script><script type="module"');

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
}).listen(PUERTO, () => console.info(`Servidor de evidencia en http://localhost:${PUERTO} (sesión inicial: ${sesionActual}). Ctrl+C para detenerlo.`));
