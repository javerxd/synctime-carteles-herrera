# Licencias de fuentes, iconos, imágenes y paquetes (RNFO17)

Inventario de todo lo de terceros que usa el proyecto y la licencia que lo permite. Se actualiza en cada fase que agrega algo. Última revisión: 13-11-2026 (fase 5).

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
| Fotos reales de los trabajos (`docs/imagenes-trabajos/vitrina/`) | Del taller, entregadas por el usuario el 13-11-2026 | En uso en la vitrina de la base demo: 22 fotos recortadas y convertidas a WebP. Las fotos muestran nombres y logotipos de clientes, con su autorización registrada (fila siguiente). Las tres imágenes generadas por IA de la carpeta original no se usan |
| Mapa de la portada (`frontend/synctimefront/public/mapa/ubicacion-taller.webp`) | Generado con `scripts/mapa-estatico.py` a partir de teselas de OpenStreetMap | **Por verificar** antes de la entrega: ver «Datos y mapas de OpenStreetMap» |
| Logotipo de Letreros Herrera | Del taller, entregado por el usuario el 13-11-2026 | En uso: versión en tinta derivada del original gris (barra, panel, acceso y favicon) en `frontend/synctimefront/public/marca/`. Originales en `docs/marca/originales/` |
| Logotipos de clientes | De cada cliente | Solo con autorización registrada: desde la fase 5 la API no muestra una publicación con logo sin ella (`PUT /api/v1/catalogo/{id}/autorizacion-logo`) |

## Paquetes de código del frontend

| Paquete | Versión | Licencia | Tipo |
|---|---|---|---|
| react, react-dom | 19.3.1 | MIT | Producción |
| react-router-dom | 7.18.4 | MIT | Producción |
| @vladmandic/face-api y sus modelos en `public/models` | 1.7.15 | MIT | Producción (ingreso facial) |
| uqr | 0.1.3 | MIT | Producción (código QR del portal, generado en el navegador; sin dependencias, unos 6 KB comprimido junto con la pantalla que lo usa, que se carga solo al abrirla) |
| @fontsource/plus-jakarta-sans | 5.3.0 | OFL-1.1 | Producción (fuente) |
| lucide-react | 1.72.0 | ISC | Producción (iconos) |
| leaflet | 1.9.4 | BSD-2-Clause | Producción (mapa interactivo de la portada; se carga solo cuando el visitante lo pide) |
| vite, @vitejs/plugin-react | 8.3.4, 6.1.2 | MIT | Desarrollo |
| vitest, jsdom, @testing-library/react, @testing-library/jest-dom | 5.0.5, 30.1.3, 16.3.3, 7.0.1 | MIT | Desarrollo (pruebas) |
| @playwright/test | 1.64.1 | Apache-2.0 | Desarrollo (pruebas de extremo a extremo) |
| @axe-core/playwright | 4.14.0 | MPL-2.0 | Desarrollo (auditoría de accesibilidad); no se distribuye con el sitio |
| oxlint | 1.89.0 | MIT | Desarrollo |
| @types/react, @types/react-dom | 19.3.0 | MIT | Desarrollo |
| Pillow (Python; solo `scripts/mapa-estatico.py`) | Sin versión fijada | HPND (MIT-CMU) | Desarrollo: genera la imagen del mapa; no se distribuye con el sitio |

`axios` y `prop-types` se eliminaron en la fase 3: el cliente HTTP usa `fetch` y los componentes ya no declaran `propTypes`.

Ninguna de estas licencias exige publicar el código propio del proyecto. La MPL-2.0 de axe solo obligaría a publicar cambios hechos a los archivos de axe, que no se modifican.

## Datos y mapas de OpenStreetMap

| Recurso | Licencia | Uso | Cómo se sirve |
|---|---|---|---|
| Datos del mapa (© colaboradores de OpenStreetMap) | ODbL | Ubicación del taller en la portada | Atribución visible junto al mapa, con enlace a la página de copyright de OpenStreetMap |
| Teselas (imágenes del mapa) de `tile.openstreetmap.org` | **Por verificar** (ver abajo) | Mapa interactivo, solo si el visitante lo activa; y la imagen estática de `public/mapa/` | Se piden al servidor de OpenStreetMap solo al activar el mapa. Hay que cumplir la política de uso de teselas de la OSMF |
| Vista de la fachada (Google Street View, inserción `google.com/maps/embed` entregada por el taller) y enlace «Abrir en Google Maps» | Condiciones de servicio de Google Maps; la inserción es gratuita y no usa clave | Solo si el visitante pulsa «Ver la fachada» o abre el enlace | Atribución y controles de Google dentro de la inserción, sin modificarlos |

**Por verificar antes de la entrega:** no se pudo comprobar en línea desde el equipo de desarrollo. La página de copyright de OpenStreetMap podría asignar a las teselas la licencia CC BY-SA 2.0, distinta de la ODbL que cubre los datos. Si es así, la imagen estática generada con esas teselas debe cumplir CC BY-SA 2.0 (atribución y compartir igual).

## Paquetes del backend

El inventario completo se hace en la fase 16, junto con las imágenes de contenedor. Los que agrega cada fase se anotan aquí al incorporarlos:

| Paquete | Versión | Licencia | Uso |
|---|---|---|---|
| MailKit (incluye MimeKit) | 4.18.2 | MIT | Fase 4: envío de correo por SMTP y armado de los mensajes. Se eligió la 4.18.2 y no la 5.0.0, publicada tres días antes, para no estrenar una versión mayor |
| DocumentFormat.OpenXml (Open XML SDK, de Microsoft) | 3.5.1 | MIT | Fase 13A: escribir los reportes en Excel (`.xlsx`) con hojas, formatos de fecha y de número y fórmulas de totales |
| PDFsharp-MigraDoc (PDFsharp y MigraDoc) | **6.2.4** | MIT | Fase 13A: armar el PDF de los reportes (tablas con encabezado repetido, «Página X de Y», texto seleccionable e imágenes). **No actualizar a la 6.5.0 ni a una posterior sin revisar su licencia: la 6.5.0 está publicada como AGPL-3.0-only** (verificado el 20-11-2026 en el `.nuspec`), que obligaría a publicar el código del sistema. La 6.2.4 es la última MIT |
| PdfPig (`UglyToad.PdfPig`) | 0.1.16 | Apache-2.0 | Fase 13A, **solo en los proyectos de pruebas**: abrir los PDF generados para comprobar que el texto es seleccionable, el título, los filtros y «Página X de Y». No se distribuye con la API |
| Plus Jakarta Sans (TrueType, `Recursos/Fuentes/`) | 2.x (la misma de la interfaz) | SIL OFL 1.1 | Fase 13A: la fuente incrustada en los PDF. Son los dos archivos `woff` del paquete `@fontsource/plus-jakarta-sans` (latino, pesos 400 y 700) convertidos a TrueType sin cambiar ningún glifo ni tabla; el texto de la licencia está en `Recursos/Fuentes/OFL-PlusJakartaSans.txt` |
| SkiaSharp y `SkiaSharp.NativeAssets.Linux.NoDependencies` | 4.154.1 | MIT (bibliotecas nativas de Skia, libwebp, libpng y libjpeg-turbo: BSD y similares) | Fase 4: conversión de imágenes a WebP (fase 13A: también dibuja los gráficos del PDF gerencial). El paquete de Linux deja lista la imagen de contenedor de la fase 16 |
