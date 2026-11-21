import { defineConfig, devices } from '@playwright/test'

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
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:3000',
    reuseExistingServer: true,
    timeout: 120 * 1000,
    // Fase 14B: un ID de cliente de Google INVENTADO para que el botón aparezca en las pruebas (el script de Google se simula).
    // El verdadero vive solo en .env.local y en los User Secrets, nunca en el repositorio. Una variable de entorno gana a .env.local.
    env: { VITE_GOOGLE_CLIENT_ID: 'id-de-prueba-14b.apps.googleusercontent.com' },
  },
})
