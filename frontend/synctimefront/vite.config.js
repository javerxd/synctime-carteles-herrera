import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// En desarrollo el CSP de index.html admite localhost (servidor de Vite y su recarga en vivo). El build
// de producción lo reduce a 'self' y, si la API vive en otro origen (VITE_API_URL), a ese origen.
export function cspDeProduccion(urlApi) {
  let origenApi = ''
  try {
    origenApi = urlApi ? ` ${new URL(urlApi).origin}` : ''
  } catch {
    throw new Error(`VITE_API_URL no es una URL válida: ${urlApi}`)
  }
  return {
    name: 'csp-de-produccion',
    apply: 'build',
    // Fase 14B: la ruta de Google (ingreso con Google) se conserva; solo se quitan los orígenes de desarrollo.
    transformIndexHtml: (html) => html.replace(/connect-src [^;"]*/, `connect-src 'self'${origenApi} https://accounts.google.com/gsi/`),
  }
}

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiTarget = env.VITE_API_URL || 'https://localhost:7112'

  return {
    plugins: [react(), cspDeProduccion(env.VITE_API_URL)],
    server: {
      port: 3000,
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true,
          secure: false,
        },
      },
    },
    test: {
      globals: true,
      environment: 'jsdom',
      setupFiles: './src/test/setup.js',
      exclude: ['e2e/**', 'node_modules/**'],
    },
  }
})
