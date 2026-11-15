# Informe de cierre — Fase 10: rostro según requisitos

Fecha: 15-11-2026 · Rama: `fase-10-rostro-segun-requisitos` (nace de `fase-09-mantenedores-y-local`) · Estado: **aprobada el 15-11-2026**

Plan aprobado: `fase-10-plan.md`, con tus tres decisiones del 15-11-2026, y su especificación en `fase-10-especificacion.md`. Incluye tu pedido: la **prueba de vida con giros de cabeza** y la **calibración del umbral** con tu cámara.

## Qué se hizo

### Prueba de vida (tu pedido)
- **Desafío:** al pulsar «Entrar con rostro», el navegador pide un desafío (`POST /auth/login-facial/desafio`).
  - El servidor elige al azar el orden de los giros: izquierda y luego derecha, o al revés.
  - Es de un solo uso, vence a los 30 segundos y se guarda solo como hash.
  - La respuesta es igual exista o no el email.
- **Tres capturas seguidas:** de frente, giro moderado hacia el primer lado y hacia el otro. Por cada una viajan el descriptor y los 68 puntos del rostro, que **no se guardan ni se registran en los logs**.
- **El servidor revisa todo**, con el mismo 401 genérico si algo falla:
  - el desafío: que exista, no haya vencido, no se haya usado y sea de ese email. Se marca usado en una sola sentencia, aunque lleguen dos envíos a la vez;
  - el giro de cada captura, calculado en el servidor, y su orden;
  - que cada captura coincida con la muestra de su lado;
  - que las tres sean de la misma persona.
- **El mismo cálculo del giro** está en el servidor (`Giro.cs`) y en el navegador (`giro.js`), y los dos se prueban con el mismo archivo de casos.

### Solo dueño y administrador
- Los demás roles reciben el 401 genérico al ingresar con rostro y un 403 al intentar registrarlo.
- Se mantienen el equipo autorizado (decisión 2), el bloqueo tras 5 fallos, el límite por IP, el cifrado y el consentimiento con versión, que pasó a `2026-11-15` porque su texto cambió.

### Registro con tres muestras
- **Desde tu perfil:** el mismo flujo guiado, con una vista previa de cada muestra (frente, izquierda y derecha) y «Repetir esta muestra». La vista previa solo vive en la pantalla.
- **Desde «Rostros registrados»** (`/panel/rostros`):
  - gestión registra el rostro de otro empleado de gestión presente, que escribe **su propia** contraseña;
  - la tabla muestra el nombre, el rol, el estado, las fechas, quién lo registró, la desactivación y el historial (S16);
  - «Desactivar» pide confirmación y borra las muestras.
- **Un rostro antiguo**, con un solo promedio, queda como «debe registrarse de nuevo» y ya no sirve para entrar.

### Flujo guiado
- **Encuadre:** un óvalo, con guía en vivo: «Acércate», «Aléjate», «Falta luz», «Debe verse un solo rostro», «Centra tu rostro», «Gira un poco más» y «Gira un poco menos».
- **Giros:** una flecha animada y un progreso que se llena con el giro.
  - Cada paso avanza solo.
  - Cada giro tiene unos 4 segundos y un reintento automático.
- **Cámara:**
  - se enciende solo al pulsar el botón y se ve el indicador «Cámara activa»;
  - «Cancelar» la apaga en cualquier momento;
  - el video se ve como espejo.
- **Si falla:** el rechazo del servidor siempre dice lo mismo. Tras 3 rechazos, el foco va a la contraseña, que sigue siempre visible.
- **Accesibilidad:**
  - las instrucciones van en texto y se anuncian a los lectores de pantalla;
  - nada depende solo del color;
  - con movimiento reducido no hay animaciones.
- **Errores propios:** cámara denegada o inexistente, sin rostro, varios rostros, poca luz, tiempo agotado y error de red.
- **Política de contenido:** `index.html` permite el *worker* del motor facial (`worker-src 'self' blob:`). Desapareció el error «Creating a worker from blob…».

### Calibración (decisión 3)
- **Pantalla:** «Calibrar el rostro» (`/panel/rostros/calibrar`), solo para gestión.
  - Aparece solo con `RostroFacial:Calibracion:Activa`, que viene apagada.
  - Guarda **solo distancias y giros**, con 2 decimales y un tope por hora, para que no sirva de oráculo.
  - Muestra las dos series y sugiere un umbral.
- **Tu calibración del 15-11-2026**, con `dueno@letreria.test` en la demo:

| Serie | Intentos | Peor distancia de cada intento | Promedio |
|---|---|---|---|
| Tú («Soy yo») | 7 | 0.24 a 0.31 | 0.27 |
| Otra persona | 10 | 0.62 a 0.70 | 0.67 |

  - **Umbral sugerido:** 0.465, con un margen de ±0.155.
  - **Umbral elegido:** **0.45**, que elegiste tú y quedó en `appsettings.json`.
  - **Distancia entre capturas:** quedó entre 0.28 y 0.42, bajo su límite de 0.6.
  - **Giros:** el frente quedó entre −0.04 y 0.15, y los laterales entre 0.34 y 0.71, dentro de los rangos.
  - **Ingresos con rostro:** también entraste varias veces con tu rostro, sin fallos acumulados ni bloqueo.

### Migración `RostroConPruebaDeVida`
- **Qué agrega:** las tres columnas cifradas y quién registró en `rostros_empleado`, y dos tablas nuevas: `desafios_rostro` y `calibracion_rostro`, esta última sin datos biométricos.
- **Comprobaciones:**
  - se probó subirla, revertirla y volver a subirla en una base temporal, que se borró después;
  - revertirla se detiene si eso borraría datos.
- **Dónde está aplicada:** en la demo. **No en la real**, que sigue con 3 migraciones; con `RostroMuestrasPorLado` (abajo) le faltan 10.
- **Script:** se regeneró `LetreriaLosHerreraDB.sql` y se comprobó contra una base creada con las migraciones.

### Las dos decisiones que aprobaste al revisar
- **Límite propio para el desafío:** `POST /auth/login-facial/desafio` usa `DesafioFacial` (10 por minuto por IP, `RateLimiting:DesafioFacial`) y el ingreso sigue con `LoginFacial`. Agotar uno no gasta el otro ni la contraseña.
- **El lado de cada muestra en el cifrado:** los datos asociados pasan a `modelo:idEmpleado:lado`. Intercambiar las muestras de columna en la base ya no sirve para entrar. El señuelo se cifra por lado, así que todo intento sigue haciendo el mismo trabajo.
  - Los rostros nuevos usan `formato_muestras = 3`; los de formato 2 deben registrarse de nuevo.
  - Migración nueva `RostroMuestrasPorLado`: solo cambia la regla de la tabla. Se probó subir, revertir (se detiene si hay rostros de formato 3) y volver a subir en una base temporal, y se aplicó solo a la demo.
  - **Volviste a registrar tu rostro en la demo** (formato 3) y entraste con él; el registro anterior quedó revocado.
- **Pruebas:** 402 unitarias y 407 de integración, en verde. Hacen fallar su prueba: quitar el lado de los datos asociados (1) y devolver el desafío al límite del ingreso (3). La revisión de seguridad y la de base de datos no encontraron nada crítico, alto ni medio.

## Verificación

| Comprobación | Resultado |
|---|---|
| Backend | 402 unitarias y 399 de integración, en verde; compila sin advertencias |
| Frontend | 522 de Vitest (con los 42 casos del giro compartidos con el servidor) y 193 de Playwright con axe (pantallas a 360, 768 y 1440 px), en verde; lint sin errores; build correcto |
| Pruebas primero | Las nuevas se vieron fallar antes de implementar: 69 de 72 de integración y 23 del frontend |
| Pruebas no vacías | Hacen fallar su prueba: aceptar cualquier orden (1), no revisar el ángulo (9), permitir reusar el desafío (3) y no comparar entre capturas (1) |
| RNFP12 | El servidor responde el ingreso facial en 3 s o menos (`ElServidorResponde_EnTresSegundosOMenos`), medido en el proceso de pruebas y sin carga |
| **Con tu cámara (demo)** | Registraste tu rostro con las tres muestras, entraste varias veces con rostro y calibraste con 17 intentos |
| Sin cámara (mi navegador) | Desafío: 200, con un orden al azar y 30 s de vigencia. Un intento falso recibe el 401 genérico. «Cámara denegada» muestra su aviso y deja la contraseña. Consola sin errores |

## Revisión de ECC

Ningún hallazgo crítico. Se corrigieron, entre otros:
- **Seguridad, alto:** la calibración podía servir de oráculo. Ahora usa 2 decimales y tiene un tope de 30 mediciones por empleado y hora.
- **Seguridad, medio:** la contraseña del titular, al registrarlo otro, tenía que contarse aparte para no bloquear su ingreso con contraseña. El registro y la autorización del equipo ganaron límite por cuenta.
- **C# y base de datos:**
  - la regla del giro quedó en un solo lugar;
  - un CHECK más estricto sobre las muestras;
  - la purga por lotes;
  - un 500 con un empleado inactivo;
  - el log que un anónimo podía llenar.
- **React (alto):** «Cancelar» durante la verificación dejaba entrar igual. Además, el componente se redibujaba en cada cuadro y había una carrera entre dos desafíos.
- **Accesibilidad (altos):**
  - el foco se perdía tras cancelar o fallar;
  - Enter en la contraseña reiniciaba la captura;
  - el formulario quedaba sobre la cámara a 360 px;
  - había anuncios duplicados.

## Lo que no se pudo probar sin más cámaras ni personas
- **Calibración limitada:** usó una sola cámara, una luz, tu rostro y una persona ajena. Con más personas, lentes o poca luz el umbral podría necesitar ajuste: se recalibra encendiendo la calibración solo mientras se mide.
- **Umbrales de la guía:** los de luz, tamaño y centrado están puestos a ojo. No son de seguridad.
- **Tiempo en otros equipos:** RNFP12 y la duración del flujo (6 a 9 s) no se midieron en otros equipos. Tampoco si 4 s por giro alcanzan para todos.

## Pendiente (en `desviaciones.md`)
- **Límite de la prueba de vida:** un cliente modificado puede falsificar descriptores y puntos. Por eso se mantienen el equipo autorizado, el bloqueo y la contraseña.
- **Base real:** las 10 migraciones pendientes esperan al final de las fases. Al aplicarlas, tu rostro de la base real deberá registrarse de nuevo con las tres muestras.

## Cierre
Aprobaste las dos decisiones y la fase el 15-11-2026, con la condición de terminarlas primero; quedaron hechas y verificadas.
