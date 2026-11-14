# Informe de cierre — Fase 6: cuentas y sesión

Fecha: 13-11-2026 · Rama: `fase-06-cuentas-y-sesion` (nace de `fase-05-portada-publica`) · Estado: **aprobada el 14-11-2026**

Los clientes ya pueden crear su cuenta, ingresar con su correo, confirmar el correo, recuperar la contraseña y corregir sus datos. La sesión de todos (clientes y personal) pasó a un token corto que se renueva solo, sin guardarse en el navegador.

## Antes de migrar

Consulta de solo lectura en la base real: **0 clientes** y 1 empleado activo. No hay cuentas sin correo que decidir.

## Qué se hizo

### Sesión (RNFP08, RNFP29)
- Token de acceso de 15 minutos (`Jwt:ExpirationMinutes`). La página lo guarda solo en memoria.
- Renovación en una cookie que el código de la página no puede leer (httpOnly, Secure, SameSite=Strict, solo para `/api/v1/auth`), más protección CSRF con una segunda cookie y la cabecera `X-CSRF-Token`.
- Cada renovación cambia el token. Si un token ya usado vuelve a aparecer, se cierra esa sesión entera. Dos pestañas que renuevan a la vez no se expulsan entre sí.
- Duración máxima desde el ingreso: 12 horas el personal y 7 días los clientes (`Sesion:HorasPersonal`, `Sesion:DiasCliente`).
- Salir, cambiar o recuperar la contraseña y desactivar la cuenta cierran las sesiones. El ingreso con rostro abre la misma sesión.
- CORS con credenciales y orígenes explícitos.

### Cuentas de cliente (RFS03, RFU04, RFU05, E01, E02, E23)
- **Ingreso con el correo**, ya no con el teléfono.
- **Registro:**
  - Obligatorios: nombre, apellido, teléfono, correo y contraseña. El RUT es opcional; si se escribe, se valida.
  - Dos casillas separadas y desmarcadas: términos y privacidad (obligatoria) y novedades (opcional). Cada una queda registrada con la versión del documento.
- **Confirmación del correo:** un enlace de un solo uso que vence en 24 horas, con opción de reenviarlo.
- **Recuperación de la contraseña (RNFP13), para clientes y personal:**
  - código de 6 dígitos que vence en 15 minutos y se guarda solo como hash;
  - 5 intentos, reservados en la base;
  - como máximo 1 código por minuto y 5 por hora;
  - la respuesta es la misma, y tarda lo mismo, exista o no la cuenta.
- **Mi cuenta (RNFE01):** datos, correo (pide la contraseña y avisa al correo anterior), contraseña y novedades.
- Los correos para clientes salen de la casilla automática, con el pie de «no respondas».
- **Límites (RNFP07):** 10 intentos por minuto por IP y por cuenta en el ingreso y la recuperación. El intento 11 recibe 429 con mensaje.

### Migración `CuentasYSesion`
- **Qué agrega:**
  - las tablas `tokens_renovacion` y `codigos_cuenta`;
  - la columna `clientes.fecha_verificacion_correo`;
  - la regla de que no hay cuenta sin correo.
- **Comprobaciones:**
  - solo agrega;
  - se probó subir, revertir y volver a subir en una base temporal, que luego se borró.
- **Dónde está aplicada:** en la demo, **no en la real**.

### Frontend
- Pantallas `/ingresar`, `/cuenta/registro`, `/cuenta/recuperar`, `/cuenta/verificar` y `/cuenta`.
- El acceso del personal sigue aparte y es el único con «Entrar con rostro». Ahora tiene «¿Olvidaste tu contraseña?».
- Las pestañas se avisan entre sí al salir o al cambiar de persona.

## Verificación

| Comprobación | Resultado |
|---|---|
| Backend | 211 unitarias y 274 de integración, en verde |
| Frontend | 230 de Vitest y 71 de Playwright con axe (las pantallas nuevas a 360, 768 y 1440 px), en verde; lint sin errores; build correcto |
| Pruebas no vacías | Quitar la revocación ante un token reutilizado, el tope de intentos o el cierre de sesiones al cambiar la contraseña hace fallar su prueba |
| La app en marcha (demo) | Recorrido completo:<br>• registro, con las casillas desmarcadas;<br>• la sesión sigue tras recargar, sin el token guardado en el navegador;<br>• enlace de confirmación desde la bandeja local;<br>• salida, con las cookies borradas;<br>• recuperación con el código del correo;<br>• ingreso con la contraseña nueva |

## Revisión de ECC

**Seguridad y C# del backend:** ninguno crítico. Corregidos los altos y los medios, entre ellos:
- el tope de intentos del código, que se podía saltar con peticiones simultáneas;
- PATCH bloqueado por CORS;
- tiempos de respuesta que revelaban qué correos tenían cuenta;
- cambios de contraseña sin transacción;
- códigos que seguían sirviendo tras cambiar el correo;
- la falta de un límite por cuenta en las acciones que piden la contraseña.

**React y accesibilidad del frontend:** ninguno crítico. Corregidos los cuatro altos:
- una espera «Comprobando…» que podía no terminar nunca;
- la renovación sin tiempo máximo;
- un bucle de renovación con el reloj del equipo adelantado;
- un cambio silencioso de persona entre pestañas.

También los medios: foco tras los errores, el token fuera de la dirección, salida fallida avisada y fallos de red que no expulsan.

## Decisiones que conviene que revises (en `desviaciones.md`)

- **RUT opcional** en la cuenta.
- **Mensajes del registro:** teléfono o RUT ya registrados dan un mismo mensaje. El correo repetido sí se informa, para que la persona sepa que debe ingresar.
- **Tokens ya entregados:** tras cerrar una sesión, el token ya entregado vale hasta 15 minutos más. Cerrarlo al instante costaría una consulta por petición.
- **Novedades sin correo confirmado:** se pueden aceptar antes de confirmar el correo. Las campañas de la fase 11 solo deben ir a correos confirmados.
- **Mismo sitio:** con SameSite=Strict, la web y la API deben servirse en el mismo sitio, lo que se define en la fase 16. Por eso `VITE_API_URL` queda vacía en `.env.example`.
- **Política de privacidad:** describe ahora las cookies de sesión. Sigue en la versión 1.0 porque aún no hay usuarios reales.

## Lo que no se verificó

- Ingreso con Google: es de la fase 14.
- Exigir el correo confirmado para enviar solicitudes: es de la fase 7.
- Un teléfono real: la revisión se hizo con la vista de 360 px del navegador.

## Lo que necesito de ti

1. **Tu visto bueno a esta fase**, o lo que quieras cambiar.
2. Cuándo aplicar a la base real las migraciones pendientes, que ahora son 5. Siguen esperando al final de las fases, como decidiste.
