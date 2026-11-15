# Plan de la fase 10 — Rostro según requisitos

Fecha: 15-11-2026 · Rama: `fase-10-rostro-segun-requisitos` (nace de `fase-09-mantenedores-y-local`) · Estado: **aprobado el 15-11-2026** · especificación: `fase-10-especificacion.md` · construido el 15-11-2026, informe: `informes/fase-10.md`

Requisitos del plan general: RFS03 (ingreso facial), RFU21, RFU22 (parcial), RNFP12, RNFE06, E17, E18, S15, S16. Se suma tu pedido del 15-11-2026: la **prueba de vida con giros de cabeza**, con su flujo guiado y la calibración del umbral.

## Antes de empezar (comprobado)

- **Fases de las que depende:** la 6 (cuentas y sesión) y la 9 (mantenedores) están aprobadas, y la rama sale de la 9. Ya quedó registrada tu decisión de que el administrador puede dar y quitar el rol de dueño.
- **Rostros por rol:** consulté las bases solo para leer.
  - La real tiene **un solo rostro activo, de rol administrador** (el tuyo).
  - La demo no tiene ninguno.
  - No hay rostros de otros roles que retirar antes de restringir.
- **Lo que ya existe:**
  - descriptor cifrado con AES-GCM y atado a su empleado;
  - email primero y comparación 1 a 1 contra un señuelo cuando no hay rostro;
  - 401 genérico con un tiempo mínimo;
  - bloqueo tras 5 fallos durante 15 minutos;
  - límite de 10 intentos por minuto por IP;
  - token de «equipo autorizado» para dueño y administrador;
  - consentimiento con versión.
  - Hoy se guarda **un solo descriptor promedio**, y el rostro lo puede activar cualquier rol.
- **Un detalle encontrado:** la política de contenido de `index.html` no permite que el motor facial cree su *worker* (error «Creating a worker from blob…» en el perfil). Se corrige agregando `worker-src 'self' blob:`, y se comprueba que el motor sigue funcionando.

## 1. Lo que ya define el plan general

- **Solo dueño y administrador** usan el rostro. Los demás roles reciben el mismo 401 genérico al ingresar y un 403 al intentar activarlo.
- **Email primero y comparación 1 a 1**, como hoy. La contraseña sigue siempre visible.
- **Registro por los dos caminos:**
  - **el titular**, desde su perfil, con su contraseña;
  - **el dueño o el administrador**, desde «Rostros registrados», para otro empleado de gestión. El titular escribe **su propia** contraseña y acepta el consentimiento en ese momento, en el mismo equipo.
- **«Rostros registrados» (S16):** por empleado, nombre, rol, fecha de registro, fecha de consentimiento y estado. Gestión puede desactivar cualquiera, y el titular el suyo.
  - Desactivar **borra los descriptores** y conserva solo la constancia del consentimiento (RNFE06).
- **Resultado del ingreso (S15):** el nombre y el rol de quien entró, o el aviso de que no hubo coincidencia, con la contraseña a mano.

## 2. Prueba de vida con giros (tu pedido)

**Desafío:**
- Al pulsar «Entrar con rostro», el navegador pide un desafío: `POST /auth/login-facial/desafio`, con el email.
- El servidor genera el **orden de los giros al azar** (izquierda y luego derecha, o al revés). Es de un solo uso, dura poco (unos 30 segundos) y se guarda solo como hash. El navegador no puede elegirlo.
- La respuesta es igual exista o no el email: no revela cuentas.

**Capturas:** se toman tres seguidas, sin botones entre ellas:
1. **De frente**.
2. **Giro moderado** de unos 30 a 45 grados hacia el primer lado.
3. **Giro moderado** hacia el otro lado.

Por cada captura se envían el descriptor (128 números) y los 68 puntos de referencia del rostro. Estos sirven para medir el giro y **no se guardan ni se registran en los logs**.

**El servidor revisa todo, no solo el navegador.** Rechaza con el mismo 401 genérico si alguna de estas comprobaciones falla:
- el desafío existe, no venció, no se usó y corresponde a ese email;
- se marca usado en la misma operación, aunque lleguen dos envíos a la vez;
- cada captura coincide con la muestra correspondiente del empleado: la de frente con la de frente, y cada giro con la de su lado;
- el giro, calculado en el servidor a partir de los puntos, está en el rango pedido y en el orden del desafío. La de frente debe estar casi derecha;
- las tres capturas son de la misma persona: se comparan entre sí;
- todo llegó dentro de la vigencia del desafío.

**Lo que se mantiene igual:**
- el límite de 10 intentos por minuto y el bloqueo tras 5 fallos;
- el 401 genérico con tiempo mínimo;
- el cifrado de los descriptores;
- no se guardan fotos ni se registran descriptores ni puntos en los logs;
- el token de equipo autorizado (decisión 2).

**Registro:** guarda **tres muestras cifradas**, una de frente y una por cada lado con giro moderado, en lugar del promedio único. Esto cumple E17, «tres capturas». Usa la misma clave y los mismos datos asociados por empleado.

**Límite honesto** (va a `desviaciones.md`): los descriptores y los puntos se calculan en el navegador, así que **un cliente modificado podría falsificarlos**. La prueba de vida frena una foto o una pantalla sostenida frente a la cámara y sube mucho el costo de suplantar a alguien, **pero no es una protección infalible**. Por eso se mantienen la contraseña, el equipo autorizado y el bloqueo.

## 3. Experiencia de uso

**Flujo:**
- **Encuadre:** un óvalo para encuadrar el rostro, con guía en vivo: «Acércate», «Aléjate», «Falta luz», «Debe verse un solo rostro», «Gira un poco más» o «Gira un poco menos».
- **Giros:** una flecha animada hacia el lado pedido y un indicador de progreso que se completa con el giro.
  - **Cada paso avanza solo**, apenas se cumple.
  - Cada giro tiene unos 4 segundos y **un reintento automático** antes de rechazar.
- **Duración:** una animación de escaneo dura todo el proceso. La duración sale de los pasos reales, unos 6 a 9 segundos, sin esperas artificiales, y no se presenta como medida de seguridad.

**Si falla:**
- **Mensaje:** si el servidor rechaza, el mensaje final es genérico y no dice qué comprobación falló.
- **Contraseña:** siempre está visible. Tras 3 intentos fallidos se ofrece de inmediato.

**Cámara:**
- **Control:** se puede cancelar en cualquier momento.
- **Encendido:** la cámara se enciende solo al pulsar «Entrar con rostro» y se ve que está activa. Se apaga al terminar o al cancelar.
- **Imagen:** el video se ve como espejo, y la lógica del giro corrige esa inversión.

**Registro:** usa el mismo flujo guiado, con una vista previa de cada muestra (frente, izquierda y derecha) y la opción de repetirla.
- La vista previa es una imagen que **solo vive en la pantalla y no se envía**.

**Casos que se manejan:** cámara denegada o inexistente, sin rostro, varios rostros, luz insuficiente, tiempo agotado y error de red.

**Accesibilidad:**
- las instrucciones también van en texto y se anuncian a los lectores de pantalla;
- nada depende solo del color;
- con `prefers-reduced-motion`, la flecha y el escaneo pasan a indicadores quietos;
- respeta el sistema de diseño de la fase 2.

## 4. Umbral y calibración contigo

- **Configurables fuera del código:** el umbral (`RostroFacial:UmbralDistancia`) y los rangos de giro (`RostroFacial:GiroMinimo`, `RostroFacial:GiroMaximo` y `RostroFacial:FrenteMaximo`).
- **Modo de calibración:**
  - Lo enciende `RostroFacial:Calibracion:Activa`, que viene apagada, y solo lo usa gestión.
  - En una pantalla «Calibrar el rostro», haces intentos marcados como **«soy yo»** u **«otra persona»**.
  - Se guardan **solo las distancias**: frente, cada lado y entre capturas, sin imágenes, descriptores ni puntos.
  - La pantalla muestra las dos series y **sugiere un umbral** con datos, por ejemplo el punto que separa mejor las dos series, con su margen. Las mediciones se pueden borrar.
- **La calibración con una cámara y rostros reales la haces tú.** No daré la fase por cerrada sin ese resultado.

## 5. Migración `RostroConPruebaDeVida` (solo agrega; demo, no la real)

- **`rostros_empleado`:** tres columnas cifradas (`descriptor_frente`, `descriptor_izquierda`, `descriptor_derecha`), quién inició el registro y el formato de las muestras.
  - El descriptor promedio antiguo deja de usarse.
  - **Tu rostro actual se tendrá que registrar de nuevo** con las muestras laterales. Te avisaré cuándo: al terminar la fase, sobre la demo.
- **Tabla nueva `desafios_rostro`:** hash del desafío, email normalizado (o su hash), orden de los giros, emisión, vencimiento y uso.
  - Los desafíos vencidos se purgan.
- **Tabla nueva `calibracion_rostro`:** etiqueta, distancias, umbral y rangos vigentes, quién y cuándo. Sin datos biométricos.
- **Revertirla** se detiene si borraría datos. Se regenera el script de la base.

## 6. Pruebas y cierre

**Pruebas primero:**
- desafío al azar (los dos órdenes aparecen) y de un solo uso;
- orden equivocado, ángulo insuficiente o excesivo y desafío vencido;
- capturas de personas distintas y una captura que no coincide con su lado;
- roles sin permiso (diseñador y operario: 401 al ingresar y 403 al activar);
- empleado desactivado;
- límite de intentos;
- respuestas iguales con y sin email existente;
- el registro guarda tres muestras;
- desactivar borra las tres muestras.
- Además: ningún descriptor ni punto en los logs.

**Pruebas no vacías:** se reintroducen fallos en una copia temporal (aceptar cualquier orden, no revisar el ángulo, permitir reusar el desafío), se comprueba que su prueba falla y se restaura.

**Frontend:**
- **Vitest:** el flujo guiado y sus estados (encuadre, cada giro, reintento, éxito, rechazo, cancelación y los errores de cámara), con la detección simulada.
- **Playwright:** las pantallas a 360, 768 y 1440 px con axe.

**Revisiones:** `ecc:csharp-reviewer`, `ecc:react-reviewer` y `ecc:security-reviewer`.

**Al cerrar:** se actualizan la trazabilidad (RFS03, RNFE06 y RNFP12, más un identificador nuevo para la prueba de vida), el tablero, las desviaciones, `docs/api` y el informe `fase-10.md`.

**Lo que no se puede probar sin una cámara real** irá dicho con claridad en el informe: la precisión del giro calculado, el umbral real, la luz y el desempeño en tu equipo. Lo cubre tu calibración.

## Decisiones del usuario (15-11-2026)

1. **RNFP12:** los 3 segundos se miden como la respuesta del servidor después de las capturas. El flujo guiado completo, de 6 a 9 segundos por la prueba de vida, se anota en `desviaciones.md`.
2. **Equipo autorizado:** se mantiene para dueño y administrador.
3. **Calibración:** con la pantalla «Calibrar el rostro» en el panel, apagada por configuración, que guarda solo distancias en la base y sugiere un umbral.
