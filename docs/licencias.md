# Licencias de fuentes, iconos, imágenes y paquetes (RNFO17)

Inventario de todo lo de terceros que usa el proyecto y la licencia que lo permite. Se actualiza en cada fase que agrega algo. Última revisión: 12-11-2026 (fase 2).

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
| Fotos de trabajos de la vitrina | Del taller | **Pendientes**: el usuario las entregará (fase 5). Mientras tanto, la galería muestra un espacio rotulado «Foto del trabajo» |
| Logotipo de Letrería Los Herrera | Del taller | **Pendiente**: el usuario lo entregará. Mientras tanto se usa el nombre en texto |
| Logotipos de clientes | De cada cliente | Solo con autorización registrada (fase 5) |

## Paquetes de código del frontend

| Paquete | Versión | Licencia | Tipo |
|---|---|---|---|
| react, react-dom | 19.3.1 | MIT | Producción |
| react-router-dom | 7.18.4 | MIT | Producción |
| @vladmandic/face-api y sus modelos en `public/models` | 1.7.15 | MIT | Producción (ingreso facial) |
| axios | 1.20.0 | MIT | Producción; se elimina en la fase 3 |
| prop-types | 15.8.1 | MIT | Producción; se elimina en la fase 3 |
| @fontsource/plus-jakarta-sans | 5.3.0 | OFL-1.1 | Producción (fuente) |
| lucide-react | 1.72.0 | ISC | Producción (iconos) |
| vite, @vitejs/plugin-react | 8.3.4, 6.1.2 | MIT | Desarrollo |
| vitest, jsdom, @testing-library/react, @testing-library/jest-dom | 5.0.5, 30.1.3, 16.3.3, 7.0.1 | MIT | Desarrollo (pruebas) |
| @playwright/test | 1.64.1 | Apache-2.0 | Desarrollo (pruebas de extremo a extremo) |
| @axe-core/playwright | 4.14.0 | MPL-2.0 | Desarrollo (auditoría de accesibilidad); no se distribuye con el sitio |
| oxlint | 1.89.0 | MIT | Desarrollo |
| @types/react, @types/react-dom | 19.3.0 | MIT | Desarrollo |

Ninguna de estas licencias exige publicar el código propio del proyecto. La MPL-2.0 de axe solo obligaría a publicar cambios hechos a los archivos de axe, que no se modifican.

## Paquetes del backend

Se inventarían en la fase 16 junto con las imágenes de contenedor.
