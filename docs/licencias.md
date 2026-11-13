# Licencias de fuentes, iconos, imágenes y paquetes (RNFO17)

Inventario de todo lo de terceros que usa el proyecto y la licencia que lo permite. Se actualiza en cada fase que agrega algo. Última revisión: 13-11-2026 (fase 3).

## Fuentes tipográficas

| Recurso | Licencia | Uso | Cómo se sirve |
|---|---|---|---|
| Plus Jakarta Sans 400, 500, 600 y 700 (paquete `@fontsource/plus-jakarta-sans` 5.3.0) | SIL Open Font License 1.1 | Toda la interfaz | Alojada en el propio sitio (archivos `woff2` del subconjunto latino, incluidos en la compilación). No se pide a Google Fonts ni a otro tercero (RNFP22) |

La OFL permite usar, incrustar y redistribuir la fuente en sitios y aplicaciones, siempre que no se venda sola. El texto de la licencia viaja con el paquete (`node_modules/@fontsource/plus-jakarta-sans/LICENSE`).

## Iconos

| Recurso | Licencia | Uso |
|---|---|---|
| Lucide (`lucide-react` 1.72.0) | ISC | Todos los iconos de la interfaz, con un trazo y un grosor uniformes. Solo se incluyen en la compilación los iconos importados |

## Imágenes

| Recurso | Licencia | Estado |
|---|---|---|
| Fotos de trabajos de la vitrina | Del taller | **Pendientes**: el usuario las entregará (fase 5). Mientras tanto, la vitrina muestra un espacio rotulado «Foto del trabajo pendiente» |
| Logotipo de Letrería Los Herrera | Del taller, entregado por el usuario el 13-11-2026 | En uso: versión en tinta derivada del original gris (barra, panel, acceso y favicon) en `frontend/synctimefront/public/marca/`. Originales en `docs/marca/originales/` |
| Logotipos de clientes | De cada cliente | Solo con autorización registrada (fase 5) |

## Paquetes de código del frontend

| Paquete | Versión | Licencia | Tipo |
|---|---|---|---|
| react, react-dom | 19.3.1 | MIT | Producción |
| react-router-dom | 7.18.4 | MIT | Producción |
| @vladmandic/face-api y sus modelos en `public/models` | 1.7.15 | MIT | Producción (ingreso facial) |
| uqr | 0.1.3 | MIT | Producción (código QR del portal, generado en el navegador; sin dependencias, unos 6 KB comprimido junto con la pantalla que lo usa, que se carga solo al abrirla) |
| @fontsource/plus-jakarta-sans | 5.3.0 | OFL-1.1 | Producción (fuente) |
| lucide-react | 1.72.0 | ISC | Producción (iconos) |
| vite, @vitejs/plugin-react | 8.3.4, 6.1.2 | MIT | Desarrollo |
| vitest, jsdom, @testing-library/react, @testing-library/jest-dom | 5.0.5, 30.1.3, 16.3.3, 7.0.1 | MIT | Desarrollo (pruebas) |
| @playwright/test | 1.64.1 | Apache-2.0 | Desarrollo (pruebas de extremo a extremo) |
| @axe-core/playwright | 4.14.0 | MPL-2.0 | Desarrollo (auditoría de accesibilidad); no se distribuye con el sitio |
| oxlint | 1.89.0 | MIT | Desarrollo |
| @types/react, @types/react-dom | 19.3.0 | MIT | Desarrollo |

`axios` y `prop-types` se eliminaron en la fase 3: el cliente HTTP usa `fetch` y los componentes ya no declaran `propTypes`.

Ninguna de estas licencias exige publicar el código propio del proyecto. La MPL-2.0 de axe solo obligaría a publicar cambios hechos a los archivos de axe, que no se modifican.

## Paquetes del backend

Se inventarían en la fase 16 junto con las imágenes de contenedor.
