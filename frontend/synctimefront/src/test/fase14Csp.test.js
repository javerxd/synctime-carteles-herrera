import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { cspDeProduccion } from '../../vite.config.js';

// Fase 14B: la política de contenido de index.html abre SOLO lo que Google documenta para su botón de ingreso
// (script, marco, conexión y estilos de accounts.google.com/gsi/). Nada más, y el sitio sigue sin cargar nada de terceros
// sin una acción de la persona (el script solo se pide al pulsar «Continuar con Google»).
const html = readFileSync(resolve(process.cwd(), 'index.html'), 'utf8');
const politica = /http-equiv="Content-Security-Policy"\s+content="([^"]+)"/.exec(html)?.[1] ?? '';
const directiva = (nombre, texto = politica) => {
  const fuentes = texto.split(';').map((d) => d.trim()).find((d) => d.startsWith(`${nombre} `));
  return fuentes ? fuentes.slice(nombre.length + 1).split(/\s+/) : null;
};

describe('política de contenido de index.html', () => {
  it('permite el script de Google solo desde su ruta exacta y conserva \'self\'', () => {
    expect(directiva('script-src')).toEqual(["'self'", 'https://accounts.google.com/gsi/client']);
  });

  it('permite el marco, la conexión y los estilos de Google solo en sus rutas /gsi/', () => {
    expect(directiva('frame-src')).toEqual(['https://www.google.com', 'https://accounts.google.com/gsi/']);
    expect(directiva('connect-src')).toEqual(expect.arrayContaining(['https://accounts.google.com/gsi/']));
    expect(directiva('style-src')).toEqual(["'self'", "'unsafe-inline'", 'https://accounts.google.com/gsi/style']);
  });

  it('no abre nada más de Google ni de otros terceros (sin comodines ni el dominio completo)', () => {
    const deGoogle = politica.match(/https?:\/\/[^\s;]*google[^\s;]*/g) ?? [];
    expect(deGoogle.sort()).toEqual([
      'https://accounts.google.com/gsi/',
      'https://accounts.google.com/gsi/',
      'https://accounts.google.com/gsi/client',
      'https://accounts.google.com/gsi/style',
      'https://www.google.com',
    ]);
    expect(politica).not.toMatch(/\*\.google|https:\/\/accounts\.google\.com(?![/])|https:\/\/\*/);
    expect(directiva('default-src')).toEqual(["'self'"]);
    expect(directiva('img-src')).toEqual(["'self'", 'data:', 'blob:', 'https://tile.openstreetmap.org']);
    expect(directiva('object-src')).toEqual(["'none'"]);
    expect(politica).not.toContain("'unsafe-eval'");
    expect(directiva('script-src')).not.toContain("'unsafe-inline'");
  });

  it('el HTML no trae ningún <script> de Google escrito a mano: se agrega solo al pulsar el botón', () => {
    expect(html).not.toMatch(/<script[^>]+accounts\.google\.com/);
  });

  it('la política del build de producción conserva las rutas de Google en connect-src (y cambia solo lo suyo)', () => {
    const transformado = cspDeProduccion('https://api.ejemplo.cl/v1').transformIndexHtml(html);
    const produccion = /http-equiv="Content-Security-Policy"\s+content="([^"]+)"/.exec(transformado)?.[1] ?? '';
    expect(directiva('connect-src', produccion)).toEqual(["'self'", 'https://api.ejemplo.cl', 'https://accounts.google.com/gsi/']);
    expect(directiva('script-src', produccion)).toEqual(directiva('script-src'));
    expect(directiva('frame-src', produccion)).toEqual(directiva('frame-src'));
    expect(directiva('style-src', produccion)).toEqual(directiva('style-src'));
  });
});
