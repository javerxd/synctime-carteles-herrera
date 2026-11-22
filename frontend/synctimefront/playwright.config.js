import { existsSync } from 'node:fs'
import { defineConfig, devices } from '@playwright/test'

// Fase 15B (RNFP05, desviación del 20-11-2026): Chrome, Edge y Brave, los tres con motor Chromium. Se usan los EJECUTABLES YA
// INSTALADOS en el equipo; no se descarga ningún navegador. Firefox, WebKit y Safari quedan fuera por decisión del usuario.
//   - `chrome`: el recorrido completo (todas las pruebas).
//   - `edge`, `brave` y `movil`: el conjunto esencial (las pruebas marcadas con la etiqueta @esencial).
//   - `red-lenta`: red 3G/4G lenta y CPU lenta con perfil de teléfono, contra el build de producción (redLenta.spec.js).
// Brave: la ruta se puede cambiar con la variable BRAVE_PATH. Brave se prueba tal cual viene (con su bloqueador de rastreadores).
const BRAVE = process.env.BRAVE_PATH ?? 'C:/Program Files/BraveSoftware/Brave-Browser/Application/brave.exe'
const ESCRITORIO = devices['Desktop Chrome']
// Brave solo entra si está instalado en esa ruta (o en BRAVE_PATH): sin él se avisa y el resto de la corrida sigue.
const HAY_BRAVE = existsSync(BRAVE)
if (!HAY_BRAVE) console.warn(`Brave no está en ${BRAVE}: el proyecto «brave» se omite (defina BRAVE_PATH).`)
// El build de producción (y su servidor de vista previa) solo se levanta para medir la red lenta, y SIEMPRE se reconstruye: reutilizar un
// servidor viejo mediría un dist desactualizado. Si el puerto 4173 está ocupado, la corrida falla y lo dice.
const CON_PREVIEW = process.argv.some((a) => /red-lenta/.test(a)) || Boolean(process.env.CON_PREVIEW)
// Un ID de cliente de Google INVENTADO para que el botón aparezca en las pruebas (el script de Google se simula). El verdadero vive solo
// en .env.local y en los User Secrets, nunca en el repositorio. Una variable de entorno gana a .env.local.
const ENTORNO_PRUEBA = { VITE_GOOGLE_CLIENT_ID: 'id-de-prueba-14b.apps.googleusercontent.com' }

export default defineConfig({
  testDir: './e2e',
  timeout: 30 * 1000,
  expect: {
    timeout: 5000,
  },
  fullyParallel: false,
  workers: 1,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'on-first-retry',
    headless: true,
  },
  projects: [
    // El Chromium que trae Playwright: queda como antes para quien lo use (`--project=chromium`), pero ya no es parte del recorrido.
    { name: 'chromium', use: { ...ESCRITORIO }, testIgnore: /redLenta/ },
    { name: 'chrome', use: { ...ESCRITORIO, channel: 'chrome' }, testIgnore: /redLenta/ },
    { name: 'edge', use: { ...ESCRITORIO, channel: 'msedge' }, grep: /@esencial/, testIgnore: /redLenta/ },
    ...(HAY_BRAVE ? [{ name: 'brave', use: { ...ESCRITORIO, launchOptions: { executablePath: BRAVE } }, grep: /@esencial/, testIgnore: /redLenta/ }] : []),
    { name: 'movil', use: { ...devices['Pixel 7'], channel: 'chrome' }, grep: /@esencial/, testIgnore: /redLenta/ },
    {
      name: 'red-lenta',
      testMatch: /redLenta\.spec\.js/,
      timeout: 90 * 1000,
      use: { ...ESCRITORIO, channel: 'chrome', baseURL: 'http://localhost:4173' },
    },
  ],
  webServer: [
    {
      command: 'npm run dev',
      url: 'http://localhost:3000',
      reuseExistingServer: true,
      timeout: 120 * 1000,
      env: ENTORNO_PRUEBA,
    },
    ...(CON_PREVIEW ? [{
      // El build de producción (comprimido con gzip) para medir la red lenta: el servidor de desarrollo sirve cientos de módulos sin empaquetar.
      command: 'npx vite build && npx vite preview --port 4173 --strictPort',
      url: 'http://localhost:4173',
      reuseExistingServer: false,
      timeout: 180 * 1000,
      env: ENTORNO_PRUEBA,
    }] : []),
  ],
})
