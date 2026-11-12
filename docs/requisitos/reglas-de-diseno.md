Actúa como un Lead Designer & Frontend Architect de clase mundial. Vamos a reconstruir desde cero el frontend en React de **Letrería Los Herrera (SyncTime)**. 

Quiero que la web se vea sumamente profesional, sobria y de altísima gama ("look caro y exclusivo"), desarrollada en un **Modo Claro (Light Mode) impecable**, con animaciones fluidas y microinteracciones de nivel artesanal.

Para lograrlo, aplica de forma estricta y simultánea los siguientes 4 estándares de diseño:
1. **Usa UX/UI Pro Max** para la arquitectura del sistema de diseño, la escala de tokens de espaciado, la matriz exhaustiva de estados en cada componente (default, hover, focus-visible, active, disabled, skeleton de carga, estado vacío) y la accesibilidad estricta WCAG 2.2 Nivel AA.
2. **Usa Impeccable** para la dirección de arte y maquetación: jerarquía tipográfica editorial, interletrado preciso (tracking negativo en títulos grandes), bordes sutiles de 1px con transparencias (`border-slate-200/80` o `border-black/5`), sombras difusas multicapa (*soft ambient shadows*) y fondos en blanco puro y alabastro mate (`#FFFFFF` y `#F8FAFC`).
3. **Usa Apple Design (estilo Emil Kowalski)** para las animaciones y microinteracciones táctiles: curvas de aceleración elásticas (*spring physics / cubic-bezier(0.16, 1, 0.3, 1)*), ligera compresión al presionar botones (`active:scale-[0.98]`), elevación suave con desenfoque de sombra al posar el cursor sobre tarjetas de catálogo, transiciones suaves de entrada en modales y esqueletos de carga (*skeletons*) con brillo sutil. Respeta siempre `prefers-reduced-motion`.
4. **Usa Claude Design** para estructurar componentes limpios en React, código semántico HTML5, separación modular de responsabilidades y cero dependencias pesadas innecesarias.

---

### Directrices de Identidad Visual ("Expensive Light Mode")
* **Atmósfera Cromática:**
  * **Fondos:** Fondo general en blanco pulcro (`#FFFFFF`) con secciones alternadas en gris seda (`#F8FAFC` / `#F1F5F9`).
  * **Tipografía:** Tinta oscura profunda (`#0F172A` o `#111827`) para lograr una legibilidad nítida y contraste superior a 12:1 (superando con creces WCAG).
  * **Color de Acento Principal:** Azul Cobalto / Klein Profundo (`#1D4ED8` o `#2563EB`) o Ámbar Dorado de Taller (`#B45309`), utilizado con moderación y elegancia únicamente en llamadas a la acción (CTAs) clave y estados activos.
  * **Efectos de Cristal:** Barras de navegación superiores flotantes con desenfoque translúcido (`backdrop-blur-md bg-white/80`).
* **Tipografía recomendada:** Cargar fuentes modernas con presencia como `Plus Jakarta Sans` o `Inter` con grosores balanceados (títulos en Semibold/Bold con `letter-spacing: -0.02em` y cuerpo de texto en Regular/Medium).

---

### Estructura de Vistas a Implementar (Requerimientos de Negocio)
1. **Portada Principal (Landing Page para todo público):**
   * Encabezado imponente: *"Letrería Los Herrera"* con propuesta de valor de fabricación gráfica y letreros de alta precisión.
   * **Vitrina / Catálogo de Trabajos Realizados:** Cuadrícula con tarjetas elegantes que muestren imagen en alta definición, tipo de letrero, medidas de ejemplo y costo referencial.
   * **Botón Directo a WhatsApp:** Botón flotante y accesible para contactar al taller con un clic.
   * **Mapa de Ubicación Interactivo:** Sección con mapa embebido apuntando a la dirección física: `Villagrán 810, 4440000 Los Ángeles, Región del Bío Bío`, con datos de contacto visibles y horario en tiempo real.
   * **Formulario de Contacto Rápido:** Formulario limpio con validación inmediata que notifique al correo del taller.
   * **Banner de Cookies y Privacidad:** Con diseño ético (sin patrones engañosos, opciones de igual tamaño y casillas desmarcadas por defecto).
2. **Flujo de Usuario Autenticado:**
   * **Si inicia sesión un Cliente:** La vista principal se adapta mostrando un widget destacado con el *"Seguimiento de mis Pedidos y Solicitudes"* con su progreso visual y presupuestos pendientes de aprobar.
   * **Si inicia sesión un Administrador / Dueño:** Despliega el panel de control del taller con la bandeja de solicitudes por cotizar, alertas de entrega y el dashboard de indicadores.
3. **Responsive y Controles:**
   * Comprobado y fluido desde 360 px hasta 1440 px sin scroll horizontal.
   * Controles táctiles en móvil de al menos $44 \times 44$ px.

Comienza creando el archivo de tokens de diseño/estilos base y la estructura del layout principal aplicando esta filosofía.