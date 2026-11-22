# Red lenta y perfil de teléfono — fase 15B (RNFP27)

Medido con Playwright en Chrome (build de producción con gzip, caché vacía, API simulada con la latencia de la red) el 2026-11-22.
CPU 4 veces más lenta. Redes: **3G rápida** (1,6 Mbps ↓ · 0,75 Mbps ↑ · 562 ms); **4G lenta** (4 Mbps ↓ · 3 Mbps ↑ · 170 ms).
«Usable» = el título de la pantalla está visible desde que se pidió la página. LCP y FCP vienen del navegador; «interacción máx.» es la
interacción más lenta (≥ 16 ms) que el recorrido produjo, un INP aproximado.

| Teléfono | Red | Ruta | Usable (ms) | FCP (ms) | LCP (ms) | CLS | Interacción máx. (ms) | Peticiones | Transferido (KB) |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| Pixel 7 | 3G rápida | portada | 3822 | 2568 | 3884 | 0.003 | 0 | 30 | 313 |
| Pixel 7 | 3G rápida | ingreso | 4012 | 2548 | 3648 | 0.003 | 48 | 37 | 243 |
| Pixel 7 | 3G rápida | solicitud | 4823 | 3056 | 4804 | 0 | 72 | 37 | 236 |
| Pixel 7 | 3G rápida | miCuenta | 4266 | 3036 | 4900 | 0 | 0 | 30 | 234 |
| Pixel 7 | 4G lenta | portada | 2009 | 1168 | 1968 | 0.003 | 0 | 29 | 244 |
| Pixel 7 | 4G lenta | ingreso | 1887 | 1484 | 1852 | 0.005 | 64 | 37 | 243 |
| Pixel 7 | 4G lenta | solicitud | 2302 | 1616 | 2184 | 0 | 144 | 37 | 236 |
| Pixel 7 | 4G lenta | miCuenta | 2329 | 1612 | 2296 | 0 | 0 | 30 | 234 |
| iPhone 14 | 3G rápida | portada | 4258 | 2784 | 4152 | 0.004 | 0 | 30 | 313 |
| iPhone 14 | 3G rápida | ingreso | 4257 | 2716 | 3816 | 0.005 | 48 | 37 | 243 |
| iPhone 14 | 3G rápida | solicitud | 5173 | 3424 | 5140 | 0 | 80 | 37 | 236 |
| iPhone 14 | 3G rápida | miCuenta | 4544 | 3364 | 5164 | 0 | 0 | 30 | 234 |
| iPhone 14 | 4G lenta | portada | 1778 | 1232 | 1764 | 0.004 | 0 | 29 | 244 |
| iPhone 14 | 4G lenta | ingreso | 1587 | 1272 | 1588 | 0.005 | 48 | 37 | 243 |
| iPhone 14 | 4G lenta | solicitud | 2520 | 1668 | 2204 | 0 | 120 | 37 | 236 |
| iPhone 14 | 4G lenta | miCuenta | 2146 | 1416 | 2100 | 0 | 0 | 30 | 234 |
