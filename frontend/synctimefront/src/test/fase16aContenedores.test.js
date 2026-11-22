import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { cspDeProduccion } from '../../vite.config.js'

// Fase 16A (RNFP25, RNFE03): lo que sirve el sitio en la nube (vercel.json) y en compose (nginx) debe ser lo mismo que ya probó el
// build de producción. La política de contenido NO se escribe dos veces a mano: se compara con la que el build deja en index.html.
// Un cambio en una de las tres sin las otras rompe esta prueba, en vez de romper el sitio en el navegador de un cliente.
const AQUI = dirname(fileURLToPath(import.meta.url))
const RAIZ_FRONT = join(AQUI, '..', '..')
const leer = (relativo) => readFileSync(join(RAIZ_FRONT, relativo), 'utf8')

const vercel = JSON.parse(leer('vercel.json'))
const cabecerasNginx = leer('nginx/cabeceras-seguridad.conf')
const plantillaNginx = leer('nginx/default.conf.template')

// La política que el build de producción deja en <meta http-equiv="Content-Security-Policy"> (sin API en otro origen: /api es del mismo sitio).
function cspDelBuild() {
  const html = cspDeProduccion('').transformIndexHtml(leer('index.html'))
  const meta = html.match(/http-equiv="Content-Security-Policy" content="([^"]*)"/)
  if (!meta) throw new Error('index.html no tiene la política de contenido')
  return meta[1]
}

function cabeceraDeVercel(clave, fuente = '/((?!api/).*)') {
  const bloque = vercel.headers.find((b) => b.source === fuente)
  return bloque?.headers.find((h) => h.key.toLowerCase() === clave.toLowerCase())?.value
}

describe('política de contenido de producción', () => {
  it('el build de producción solo admite el propio sitio y los orígenes de Google, sin localhost', () => {
    const csp = cspDelBuild()
    expect(csp).toContain("connect-src 'self' https://accounts.google.com/gsi/")
    expect(csp).not.toMatch(/localhost/)
    expect(csp).not.toMatch(/ws:/)
  })

  it('vercel.json lleva la misma política del build, más frame-ancestors (que solo vale como cabecera)', () => {
    expect(cabeceraDeVercel('Content-Security-Policy')).toBe(`${cspDelBuild()}; frame-ancestors 'none'`)
  })

  it('nginx (compose) lleva la misma política que vercel.json', () => {
    const csp = cabecerasNginx.match(/add_header Content-Security-Policy "([^"]*)"/)?.[1]
    expect(csp).toBe(cabeceraDeVercel('Content-Security-Policy'))
  })
})

describe('cabeceras que no deben estar', () => {
  it('ni Vercel ni nginx envían Cross-Origin-Opener-Policy: same-origin (bloquearía la ventana de «Continuar con Google»)', () => {
    const todas = vercel.headers.flatMap((b) => b.headers.map((h) => `${h.key}: ${h.value}`)).join('\n')
    expect(todas).not.toMatch(/cross-origin-opener-policy/i)
    expect(cabecerasNginx + plantillaNginx).not.toMatch(/add_header\s+Cross-Origin-Opener-Policy/i)
  })
})

describe('permisos del navegador y herencia de cabeceras', () => {
  it('Permissions-Policy es la misma en Vercel y en nginx y deja la cámara al propio sitio (ingreso con rostro)', () => {
    const vercelPermisos = cabeceraDeVercel('Permissions-Policy')
    const nginxPermisos = cabecerasNginx.match(/add_header Permissions-Policy "([^"]*)"/)?.[1]
    expect(nginxPermisos).toBe(vercelPermisos)
    expect(vercelPermisos).toContain('camera=(self)')
  })

  it('la política de contenido no necesita eval ni WebAssembly (el reconocimiento facial usa WebGL o CPU)', () => {
    expect(cabeceraDeVercel('Content-Security-Policy')).not.toMatch(/unsafe-eval|wasm-unsafe-eval/)
  })

  it('en nginx, cada location que escribe cabeceras propias vuelve a incluir las de seguridad (add_header no se hereda)', () => {
    const bloques = [...plantillaNginx.matchAll(/location [^{]*{([^}]*)}/g)].map((m) => m[1])
    const conCabeceras = bloques.filter((b) => /add_header/.test(b))
    expect(conCabeceras.length).toBeGreaterThanOrEqual(4)
    for (const b of conCabeceras) expect(b).toContain('include /etc/nginx/snippets/cabeceras-seguridad.conf;')
  })
})

describe('caché (RNFP25)', () => {
  const ANO_INMUTABLE = 'public, max-age=31536000, immutable'

  it('/assets/ (con huella en el nombre) se guarda un año en Vercel y en nginx', () => {
    expect(cabeceraDeVercel('Cache-Control', '/assets/(.*)')).toBe(ANO_INMUTABLE)
    expect(plantillaNginx).toMatch(/location \^~ \/assets\/ \{[^}]*max-age=31536000, immutable/)
  })

  it('la caché larga se limita a /assets/: ninguna otra regla la usa', () => {
    const otras = vercel.headers.filter((b) => b.source !== '/assets/(.*)')
    for (const bloque of otras)
      for (const h of bloque.headers)
        if (h.key.toLowerCase() === 'cache-control') expect(h.value).not.toMatch(/max-age=31536000|immutable/)
    const sinAssets = plantillaNginx.replace(/location \^~ \/assets\/ \{[^}]*\}/, '')
    expect(sinAssets).not.toMatch(/immutable|max-age=31536000/)
  })

  it('index.html se revisa en cada visita (no-cache) en Vercel y en nginx', () => {
    expect(cabeceraDeVercel('Cache-Control', '/index.html')).toBe('no-cache')
    expect(cabeceraDeVercel('Cache-Control', '/')).toBe('no-cache')
    expect(plantillaNginx).toMatch(/location = \/index\.html \{[^}]*no-cache/)
  })

  it('los archivos que Vite emite en /assets/ llevan huella, así que la caché larga es segura', () => {
    // Si el build dejara de poner un hash en el nombre, un año de caché serviría versiones viejas. Se mira dist/assets cuando existe
    // (la prueba del presupuesto de peso construye la aplicación; sin build previo no hay nada que mirar).
    const carpeta = join(RAIZ_FRONT, 'dist', 'assets')
    if (!existsSync(carpeta)) return
    const archivos = readdirSync(carpeta).filter((f) => /.(js|css)$/.test(f))
    expect(archivos.length).toBeGreaterThan(0)
    for (const f of archivos) expect(f).toMatch(/-[A-Za-z0-9_-]{6,}.(js|css)$/)
  })
})

describe('reescritura de /api y rutas de la aplicación', () => {
  it('Vercel reenvía /api al backend por HTTPS, en el mismo origen del sitio (la cookie de sesión viaja sin CORS)', () => {
    const api = vercel.rewrites.find((r) => r.source.startsWith('/api/'))
    expect(api.destination).toMatch(/^https:\/\/[a-z0-9.-]+\/api\/:path\*$/)
  })

  it('las rutas de la aplicación vuelven a index.html, pero /api y /assets nunca (un archivo que falta no puede ser HTML)', () => {
    const rutas = vercel.rewrites.find((r) => r.destination === '/index.html')
    // La regla se EVALUA (no solo se lee su texto): la sintaxis de Vercel es la de path-to-regexp, que aquí es una expresión regular.
    const aplica = (ruta) => new RegExp('^' + rutas.source + '$').test(ruta)
    for (const spa of ['/panel', '/cuenta/solicitudes/3', '/ingresar/cliente', '/apiary', '/']) expect(aplica(spa), spa).toBe(true)
    for (const archivo of ['/api/v1/x', '/assets/a.js', '/models/x.bin', '/marca/logo.png', '/imagenes/a.webp', '/mapa/t.png']) expect(aplica(archivo), archivo).toBe(false)
    expect(plantillaNginx).toContain('try_files $uri /index.html;')
    expect(plantillaNginx).toMatch(/location \^~ \/assets\/ \{\s*try_files \$uri =404;/)
  })

  it('nginx comprime y reenvía /api con la IP del visitante sobrescrita (nadie la elige)', () => {
    expect(plantillaNginx).toContain('gzip on;')
    expect(plantillaNginx).toMatch(/proxy_set_header X-Forwarded-For \$remote_addr;/)
    expect(plantillaNginx).not.toMatch(/\$proxy_add_x_forwarded_for/)
  })
})
