# Lighthouse móvil — fase 15B (RNFP27, RNFP15)

Medido el 22-11-2026 con **Lighthouse 13.6.0** (`npx lighthouse`, perfil **móvil simulado**: red 4G lenta y CPU 4 veces más lenta, el estándar de Lighthouse) en
Chrome 157 instalado, sobre el **build de producción** (`vite build`) servido con gzip y caché larga en `/assets/` por `frontend/synctimefront/scripts/servidor-evidencia.mjs`,
con la **API simulada** de las pruebas (nunca la API demo ni la base de datos). Las páginas del panel se midieron con una sesión de personal simulada.
Los informes completos (HTML) están en `docs/evidencias/lighthouse/`. Las cifras dependen de este equipo (ver `desviaciones.md`).

Umbrales del plan (aprobados el 21-11-2026): **rendimiento ≥ 85** y **accesibilidad ≥ 95** en móvil.

| Página | Rendimiento | Accesibilidad | Buenas prácticas | FCP | LCP | TBT | CLS |
|---|---:|---:|---:|---:|---:|---:|---:|
| Portada `/` | **100** | **100** | 100 | 1,5 s | 1,5 s | 20 ms | 0 |
| Ingreso del cliente `/ingresar/cliente` | **100** | **100** | 100 | 1,5 s | 1,5 s | 20 ms | 0,003 |
| Consultas `/consultas` | **96** | **100** | 100 | 1,5 s | 2,7 s | 30 ms | 0,003 |
| Panel, inicio `/panel` (personal) | **93** | **100** | 100 | 0,9 s | 3,3 s | 20 ms | 0 |
| Panel, pedidos `/panel/pedidos` (personal) | **95** | **100** | 100 | 0,9 s | 3,0 s | 20 ms | 0 |

**Cumple los dos umbrales en las cinco páginas.** Lo único por debajo de 1 son avisos informativos: JavaScript sin usar (la aplicación se divide por ruta; ya está dentro
del presupuesto de 150 KB), la entrega de imágenes de la portada y, en el panel, la **caché del servidor** (los archivos con huella en el nombre deben servirse con
`Cache-Control: immutable`: esto se cierra en la fase 16, RNFP25; el servidor de evidencia ya lo hace como lo haría un hosting).

Lighthouse no mide desde un teléfono real ni con tus datos reales: la prueba en un teléfono (RNFP02) y la del servidor en la nube (fase 16) quedan pendientes.
