# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

- **Clientes y visitantes** de Los Ángeles (Región del Biobío) que necesitan un letrero, un vehículo rotulado o una gráfica. Entran casi siempre desde el celular: miran la vitrina de trabajos realizados, revisan si el local está abierto (cartel electrónico o el código QR pegado en la puerta), envían una solicitud y luego siguen sus pedidos, cotizaciones y saldos.
- **Personal del taller**, en el computador del local:
  - `dueño` y `administrador`: aprueban cotizaciones, crean pedidos (también presenciales directos), registran abonos, revisan la cobranza, el calendario de entregas, los indicadores y los reportes. Son los únicos que entran con rostro.
  - `diseñador` y `operario` (trabajadores): atienden las solicitudes pendientes y avanzan los pedidos.
- **Prioridad (confirmada el 12-11-2026):** ambas experiencias por igual. La portada pública se piensa primero para el celular y el panel interno primero para el escritorio, cada uno optimizado para su uso.

## Product Purpose

SyncTime digitaliza el ciclo de trabajo de Letreros Herrera: solicitud, cotización, aprobación, pedido, entrega y cobro. Reemplaza las llamadas para preguntar si el local está abierto, las planillas de pedidos y la memoria del dueño sobre quién debe qué.

Éxito: un cliente sin capacitación registra una solicitud en menos de 5 minutos desde el celular; el personal sabe en segundos qué entregar, qué cobrar y qué cotizar, sin pantallas vacías ni datos inventados.

## Positioning

Es el sistema de un taller real de oficio físico, no una tienda genérica: la vitrina muestra trabajos hechos por el taller, con medidas y costos de referencia; el cartel electrónico verde o rojo refleja si el dueño está en el local o en terreno; y el pedido separa lo que se fabrica de lo que se paga, como trabaja de verdad un taller con clientes habituales que pagan a fin de mes.

## Operating Context

- El dueño sale a instalar en terreno y «da vuelta» el cartel desde el panel; los clientes lo consultan antes de ir.
- Muchos trabajos se acuerdan en el mostrador: el personal registra el pedido presencial directo y el abono en el momento.
- Clientes habituales retiran sin pagar y pagan varios pedidos con una factura mensual.
- Es un proyecto de título con revisiones de avance ante una profesora guía; las pantallas se muestran con la base de datos de demostración.

## Capabilities and Constraints

- Requisitos completos en `docs/requisitos/requerimientos_del_sistema.md` (versión 2: 12 macro-requerimientos, 159 identificadores). Plan por fases en `docs/plan/`.
- Stack: React 19 con Vite, en JavaScript; CSS propio con variables y CSS Modules, sin Tailwind; API .NET 10 con SQL Server.
- Todo en español de Chile. Montos en pesos sin decimales (`$12.990`), fechas `dd-mm-aaaa`, RUT con dígito verificador.
- Pedido con dos estados independientes: del pedido (aceptado, en proceso, terminado, entregado, anulado; nunca «cancelado») y de pago (no pagado, abonado, pagado total).
- Ninguna petición a terceros (fuentes, mapas, analítica) antes de una acción o un consentimiento del visitante.
- Ley 21.719 de protección de datos: consentimientos versionados, casillas desmarcadas, baja por inhabilitación.

## Brand Commitments

- Nombre del negocio: **Letreros Herrera**. Nombre del sistema: **SyncTime**. Dirección: Villagrán 810, Los Ángeles, Región del Biobío.
- **El taller tiene logotipo y el usuario lo entregará más adelante.** Mientras tanto se usa el nombre en texto, sin inventar un símbolo.
- **Voz: tú, cercano** («Envía tu solicitud», «Revisa tus pedidos»), confirmada el 12-11-2026. Los errores dicen qué pasó y cómo seguir.
- Las reglas de diseño del cliente son vinculantes: `docs/requisitos/reglas-de-diseno.md` (modo claro de alta gama, acento naranja `#C2410C` desde la fase 12C (antes ámbar `#B45309`), Plus Jakarta Sans).

## Evidence on Hand

- Aún no hay fotos reales de trabajos, ni número de WhatsApp, correo o teléfono del taller (llegan en la fase 5). Mientras tanto se usan imágenes de ejemplo con licencia libre, rotuladas como tales.
- No hay testimonios, clientes destacados ni cifras públicas: no se inventan. Los logotipos de clientes solo se muestran con su autorización registrada (RNFO17).
- Los indicadores y reportes muestran datos de ejemplo rotulados hasta las fases 12 y 13.

## Product Principles

1. **La verdad primero.** Estados, montos, horarios y el cartel siempre muestran el dato real; lo que es de ejemplo se rotula.
2. **Nada depende solo del color.** Cada estado lleva texto e icono además de su color.
3. **Rechazar cuesta lo mismo que aceptar.** Sin patrones engañosos: cookies, cancelaciones y bajas igual de visibles que la opción contraria.
4. **El taller no espera.** El panel es rápido y denso, sin coreografías; el movimiento solo explica cambios de estado.
5. **Privacidad por defecto.** Lo mínimo necesario, con consentimiento explícito.

## Accessibility & Inclusion

WCAG 2.2 nivel AA en todas las pantallas (RNFP15, RNFE08): navegación completa con teclado y lector de pantalla (prueba con NVDA), foco visible que no queda tapado, enlace para saltar al contenido, errores anunciados, contraste 4,5:1 en texto y 3:1 en texto grande y componentes, zoom al 200 %, reflujo desde 320 px, sin desplazamiento horizontal entre 360 y 1440 px, controles táctiles de 44 × 44 px y respeto de `prefers-reduced-motion`.
