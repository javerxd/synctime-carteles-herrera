import { respuestaIndicadores } from './indicadoresFalsos.js';
import { DOCUMENTOS } from './reportesFalsos.js';

// API simulada compartida por los recorridos de Playwright (fase 15B: antes vivía dentro de pantallas.spec.js). Los datos imitan
// la semilla de demostración; `conSesion`, `entrarComo` y `entrarComoCliente` ponen la sesión y responden a /api/v1/**.
export const hoy = new Date();
export const fecha = (mesesAtras, dia) => new Date(hoy.getFullYear(), hoy.getMonth() - mesesAtras, dia, 10).toISOString().slice(0, 19);
export const cliente = (nombre, apellido, telefono) => ({ nombre, apellido, telefono, rut: '46.000.011-K' });
// Fase 8: las filas del listado de pedidos (GET /pedidos), con su cliente y el resumen de cobranza.
export const pedido = (id, estadoPedido, estadoPago, total, abonado, meses, c) => ({
  idPedido: id, numero: `#${id}`, estadoPedido, estadoPago, origen: id % 3 ? 'cotizacion' : 'directo', fechaPedido: fecha(meses, 5),
  fechaEntregaEstimada: fecha(meses, 15).slice(0, 10), fechaEntregaReal: estadoPedido === 'entregado' ? fecha(meses, 14).slice(0, 10) : null,
  montoTotalFinal: total, montoAbonado: abonado, saldoPendiente: total - abonado,
  modalidadEntrega: 'retiro_local', cliente: { idCliente: id + 20, ...c }, responsable: { idEmpleado: 1, nombre: 'Hugo Vera' },
  cicloCerrado: estadoPedido === 'entregado' && estadoPago === 'pagado_total',
});
export const PEDIDOS = [
  pedido(1, 'en_proceso', 'abonado', 185000, 100000, 0, cliente('Panadería', 'El Trigal', '900000211')),
  pedido(2, 'entregado', 'no_pagado', 96000, 0, 0, cliente('Ferretería', 'Biobío', '900000212')),
  pedido(3, 'terminado', 'pagado_total', 275000, 275000, 1, cliente('Clínica Dental', 'Sonríe', '900000213')),
  pedido(4, 'aceptado', 'no_pagado', 54000, 0, 0, cliente('Minimarket', 'Don Lucho', '900000214')),
  pedido(5, 'entregado', 'pagado_total', 410000, 410000, 1, cliente('Transportes', 'Ruiz', '900000215')),
  pedido(6, 'anulado', 'no_pagado', 72000, 0, 2, cliente('Luis', 'Araya', '900000202')),
];
export const paginaDe = (elementos, extra = {}) => ({ elementos, total: elementos.length, pagina: 1, tamano: 20, totalPaginas: 1, ...extra });
export const POR_COBRAR = PEDIDOS.filter((p) => p.estadoPedido !== 'anulado' && p.estadoPago !== 'pagado_total');
export const RESUMEN_COBRANZA = { totalPorCobrar: POR_COBRAR.reduce((s, p) => s + p.saldoPendiente, 0), cantidadPorCobrar: POR_COBRAR.length };
export const RESPUESTAS = {
  '/api/v1/pedidos': paginaDe(PEDIDOS, { resumen: RESUMEN_COBRANZA }),
  '/api/v1/cartel-digital': { abiertoEnLocal: false, mensajeEstado: 'Salimos a una instalación', tiempoEstimadoRegreso: '16:30', fechaUltimaModificacion: fecha(0, 1) },
  '/api/v1/horarios': [1, 2, 3, 4, 5, 6, 7].map((d) => ({ diaSemana: d, nombreDia: ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'][d - 1], horaInicio: d < 7 ? '09:00:00' : null, horaFin: d < 6 ? '18:30:00' : d === 6 ? '14:00:00' : null, abierto: d < 7 })),
  '/api/v1/metricas/conversion-online': [{ anio: hoy.getFullYear(), mes: hoy.getMonth() + 1, totalSolicitudesOnline: 4, solicitudesConcretadasEnPedido: 1, porcentajeConversion: 25 }],
  '/api/v1/productos': [{ idProducto: 1, nombre: 'Cartel metálico impreso' }],
  '/api/v1/catalogo': {
    total: 2, pagina: 1, tamano: 9, totalPaginas: 1,
    elementos: [
      { idCatalogo: 1, titulo: 'Letrero luminoso para panadería', descripcion: 'Caja de luz con acrílico y LED.', idProducto: 1, tipoLetrero: 'Letrero luminoso', categoria: { idCategoria: 1, nombre: 'Luminosos' }, anchoMm: 1200, altoMm: 600, precioReferencial: 85000, imagen: { urlGrande: '/marca/logo-lh-512.webp', urlMiniatura: '/marca/logo-lh-256.webp', ancho: 512, alto: 512, esEjemplo: true } },
      { idCatalogo: 2, titulo: 'Vinilo para vitrina de ferretería', descripcion: 'Vinilo de corte con horario.', idProducto: 2, tipoLetrero: 'Vinilo', categoria: { idCategoria: 2, nombre: 'Vinilos' }, anchoMm: null, altoMm: null, precioReferencial: null, imagen: null },
    ],
  },
  '/api/v1/local/estado': {
    abiertoAhora: false, motivo: 'ausente', dentroDeHorario: true, horaChile: `${hoy.toISOString().slice(0, 10)}T11:30:00`,
    hoy: { diaSemana: 1, nombreDia: 'Lunes', abierto: true, horaInicio: '09:00:00', horaFin: '18:30:00' },
    proximaApertura: null,
    cartel: { abiertoEnLocal: false, mensajeEstado: 'Salimos a una instalación', tiempoEstimadoRegreso: '16:30', fechaUltimaModificacion: fecha(0, 1) },
    semana: [1, 2, 3, 4, 5, 6, 7].map((d) => ({ diaSemana: d, nombreDia: ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'][d - 1], abierto: d < 7, horaInicio: d < 7 ? '09:00:00' : null, horaFin: d < 6 ? '18:30:00' : d === 6 ? '14:00:00' : null })),
  },
  '/api/v1/documentos-legales': ['privacidad', 'terminos', 'marketing', 'cookies'].map((tipo) => ({ tipo, version: '1.0' })),
  // Fase 14C: Mi perfil consulta si el empleado tiene Google vinculado (con el ID de cliente de pruebas que pone playwright.config.js).
  '/api/v1/mi-perfil/google': { vinculado: false, correo: null },
  '/api/v1/contacto': { recibida: true, mensaje: 'Recibimos tu consulta. Te responderemos a tu correo lo antes posible.' },
  '/api/v1/categorias': [{ idCategoria: 1, nombre: 'Impresión y publicidad gráfica' }, { idCategoria: 2, nombre: 'Letreros y estructuras' }],
  // Fase 12B: el dashboard de gestión (cifras simuladas con el formato del contrato).
  ...Object.fromEntries(['resumen', 'evolucion', 'cobranza', 'embudo', 'demanda', 'por-cobrar']
    .map((r) => [`/api/v1/indicadores/${r}`, (parametros) => respuestaIndicadores(r, parametros)])),
  // Fase 13B: los tres reportes (documentos con el formato del contrato).
  ...Object.fromEntries(Object.entries(DOCUMENTOS).map(([r, documento]) => [`/api/v1/reportes/${r}`, documento])),
};

// ── Fase 7: solicitudes, adjuntos y clientes ──
export const ROSA = { nombre: 'Rosa', apellido: 'Pino', telefono: '912345678', correo: 'rosa@correo.test' };
export const COMPROBANTE = {
  idSolicitud: 123, numero: '#123', fechaChile: `${hoy.toISOString().slice(0, 10)}T10:32:00`, origen: 'online', estado: 'solicitado', cliente: ROSA,
  detalles: [
    { idProducto: 1, producto: 'Cartel metálico impreso', descripcion: 'Fondo blanco, letras rojas', anchoMm: 1200, altoMm: 600, cantidad: 2, areaM2: 1.44 },
    { idProducto: 1, producto: 'Cartel metálico impreso', descripcion: null, anchoMm: null, altoMm: null, cantidad: 1, areaM2: null },
  ],
  areaTotalM2: 1.44, descripcionGeneral: 'Letrero para la fachada de la panadería, con el horario abajo.', rowVersion: 'AAAAAAAAB9E=',
  adjuntos: [
    { idAdjunto: 8, tipo: 'imagen', nombre: 'fachada.jpg', bytes: 240000, urlMiniatura: '/api/v1/adjuntos/8/miniatura' },
    { idAdjunto: 9, tipo: 'pdf', nombre: 'logo-panaderia-version-final.pdf', bytes: 120000, urlMiniatura: null },
  ],
};
export const FICHA = {
  ...COMPROBANTE,
  idCliente: 9,
  registradaPor: null,
  cotizaciones: [],
  historial: [{ estadoAnterior: null, estadoNuevo: 'solicitado', motivo: null, fechaChile: COMPROBANTE.fechaChile, actor: 'cliente', por: 'Rosa Pino' }],
};
export const FILA = (id, estado, origen, cliente, items, adjuntos) => ({
  idSolicitud: id, numero: `#${id}`, fechaChile: fecha(0, (id % 9) + 1), estado, origen, cliente, cantidadItems: items, cantidadAdjuntos: adjuntos, primerProducto: items > 1 ? 'Banners' : 'Cartel metálico impreso',
  registradaPor: origen === 'presencial' ? { nombre: 'Hugo', apellido: 'Vera' } : null,
});
export const BANDEJA = {
  elementos: [
    FILA(123, 'solicitado', 'online', ROSA, 2, 2),
    FILA(122, 'cotizado', 'presencial', { nombre: 'Ferretería', apellido: 'Biobío', telefono: '900000212' }, 1, 0),
    FILA(121, 'rechazado', 'online', { nombre: 'Luis', apellido: 'Araya', telefono: '900000202' }, 3, 1),
  ],
  total: 3, pagina: 1, tamano: 20, totalPaginas: 1,
};
export const CLIENTES = {
  elementos: [
    { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', telefono: '912345678', rut: '12.345.678-5', email: 'rosa@correo.test', activo: true },
    { idCliente: 10, nombre: 'Ferretería', apellido: 'Biobío', telefono: '900000212', rut: null, email: null, activo: true },
  ],
  total: 2, pagina: 1, tamano: 20, totalPaginas: 1,
};
Object.assign(RESPUESTAS, {
  '/api/v1/solicitudes': BANDEJA,
  '/api/v1/solicitudes/123': FICHA,
  '/api/v1/mis/solicitudes': { elementos: BANDEJA.elementos.slice(0, 2), total: 2, pagina: 1, tamano: 10, totalPaginas: 1 },
  '/api/v1/mis/solicitudes/123': COMPROBANTE,
  '/api/v1/clientes': CLIENTES,
  '/api/v1/clientes/9': CLIENTES.elementos[0],
  '/api/v1/mis/adjuntos': [],
});

// ── Fase 8: cotizaciones, pedidos, abonos, calendario y alertas ──
export const HOY_CHILE = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).format(hoy);
export const diaChile = (dias) => {
  const [a, m, d] = HOY_CHILE.split('-').map(Number);
  return new Date(Date.UTC(a, m - 1, d + dias)).toISOString().slice(0, 10);
};
export const MATERIALES = [
  { idMaterial: 1, nombre: 'Acrílico 3 mm', unidadMedida: 'm2', costoUnitarioAprox: 18500, activo: true },
  { idMaterial: 2, nombre: 'Vinilo impreso', unidadMedida: 'm2', costoUnitarioAprox: 9900, activo: true },
  { idMaterial: 3, nombre: 'Perfil de aluminio', unidadMedida: 'ml', costoUnitarioAprox: 4200, activo: false },
];
export const EMPLEADOS = [
  { idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', rol: 'dueño', activo: true },
  { idEmpleado: 2, nombre: 'Hugo', apellido: 'Vera', rol: 'operario', activo: true },
];
export const ITEMS = COMPROBANTE.detalles.map(({ producto, descripcion, anchoMm, altoMm, cantidad, areaM2 }) => ({ producto, descripcion, anchoMm, altoMm, cantidad, areaM2 }));
export const cotizacionDe = (id, estado, extra = {}) => ({
  idCotizacion: id, numero: `#${id}`, idSolicitud: 123, numeroSolicitud: '#123', fechaChile: `${HOY_CHILE}T09:15:00`, estado,
  cliente: { idCliente: 9, ...ROSA }, items: ITEMS,
  materiales: [
    { idMaterial: 1, material: 'Acrílico 3 mm', unidad: 'm2', medidaAplicada: '120 × 60 cm', cantidad: 1.44, costoUnitario: 18500, subtotal: 26640 },
    { idMaterial: 2, material: 'Vinilo impreso', unidad: 'm2', medidaAplicada: null, cantidad: 0.75, costoUnitario: 9900, subtotal: 7425 },
  ],
  costoMaterialesTotal: 34065, costoManoObra: 45000, otrosCostos: 5000, total: 84065, totalRedondeado: 84065,
  observaciones: 'Incluye la instalación en la fachada. Plazo: 5 días hábiles.', elaboradaPor: { idEmpleado: 1, nombre: 'Marta Fuentes' },
  respuesta: null, idPedido: null, rowVersion: 'AAAAAAAACOT=', ...extra,
});
export const COT_PENDIENTE = cotizacionDe(45, 'pendiente');
export const COT_APROBADA = cotizacionDe(46, 'aprobada', {
  respuesta: { fechaChile: `${HOY_CHILE}T11:02:00`, via: 'telefono', motivo: null, registradaPor: { nombre: 'Hugo Vera' } },
});
export const FILA_COT = (c) => ({
  idCotizacion: c.idCotizacion, numero: c.numero, idSolicitud: c.idSolicitud, fechaChile: c.fechaChile, cliente: { idCliente: 9, ...ROSA },
  total: c.total, estado: c.estado, elaboradaPor: c.elaboradaPor,
});
// En el listado, de otras solicitudes: la 123 sigue «Solicitada» y se puede cotizar.
export const LISTA_COT = paginaDe([
  { ...FILA_COT(COT_PENDIENTE), idSolicitud: 122 }, { ...FILA_COT(COT_APROBADA), idSolicitud: 120 },
  { ...FILA_COT(cotizacionDe(44, 'rechazada')), idSolicitud: 121, fechaChile: fecha(0, 2) },
]);
export const FICHA_PEDIDO = {
  idPedido: 1, numero: '#1', origen: 'cotizacion', solicitud: { idSolicitud: 123, numero: '#123' }, cotizacion: { idCotizacion: 46, numero: '#46' },
  cliente: { idCliente: 9, ...ROSA }, responsable: { idEmpleado: 2, nombre: 'Hugo Vera' }, fechaPedido: `${diaChile(-6)}T10:00:00`,
  fechaEntregaEstimada: diaChile(2), fechaEntregaReal: null, modalidadEntrega: 'instalacion_terreno', requiereInstalacion: true,
  direccionInstalacion: 'Colón 455, Los Ángeles', montoBaseTrabajo: 84065, costoInstalacionAdicional: 15000, montoTotalFinal: 99065,
  estadoPedido: 'aceptado', estadoPago: 'abonado', montoAbonado: 40000, saldoPendiente: 59065, numeroFactura: null, cicloCerrado: false,
  motivoAnulacion: null, aFavorDelCliente: 0, transicionesPosibles: ['en_proceso', 'anulado'],
  historial: [{ fechaChile: `${diaChile(-6)}T10:00:00`, estadoAnterior: null, estadoNuevo: 'aceptado', nota: null, empleado: 'Marta Fuentes' }],
  pagos: [
    {
      idPago: 11, monto: 40000, fechaPago: diaChile(-6), medioPago: 'transferencia', observacion: 'Transferencia 0042', registradoPor: 'Marta Fuentes',
      fechaRegistroChile: `${diaChile(-6)}T10:05:00`, anulado: false, motivoAnulacion: null, anuladoPor: null, fechaAnulacionChile: null, puedeAnular: true,
    },
    {
      idPago: 10, monto: 4000, fechaPago: diaChile(-6), medioPago: 'efectivo', observacion: null, registradoPor: 'Hugo Vera',
      fechaRegistroChile: `${diaChile(-6)}T09:58:00`, anulado: true, motivoAnulacion: 'Se ingresó dos veces', anuladoPor: 'Marta Fuentes',
      fechaAnulacionChile: `${diaChile(-6)}T10:01:00`, puedeAnular: false,
    },
  ],
  rowVersion: 'AAAAAAAAPED=',
};
export const entregaDe = (id, dias, estadoPedido, c, atrasado = false) => ({
  idPedido: id, numero: `#${id}`, fecha: diaChile(dias), cliente: { idCliente: id + 20, ...c },
  modalidadEntrega: id % 2 ? 'retiro_local' : 'instalacion_terreno', estadoPedido, estadoPago: 'abonado', atrasado,
});
export const CALENDARIO = [
  entregaDe(1, 2, 'aceptado', cliente('Panadería', 'El Trigal', '900000211')),
  entregaDe(4, -1, 'en_proceso', cliente('Minimarket', 'Don Lucho', '900000214'), true),
  entregaDe(2, 0, 'entregado', cliente('Ferretería', 'Biobío', '900000212')),
  entregaDe(7, 0, 'terminado', cliente('Clínica Dental', 'Sonríe', '900000213')),
];
export const SEGUIMIENTO_COT = { idCotizacion: 45, numero: '#45', total: 84065, estado: 'pendiente' };
export const MI_SOLICITUD = { ...COMPROBANTE, estado: 'cotizado', cotizacion: SEGUIMIENTO_COT, pedido: null };
Object.assign(RESPUESTAS, {
  '/api/v1/materiales': MATERIALES,
  '/api/v1/empleados': EMPLEADOS,
  '/api/v1/cotizaciones': LISTA_COT,
  '/api/v1/cotizaciones/45': COT_PENDIENTE,
  '/api/v1/cotizaciones/46': COT_APROBADA,
  '/api/v1/pedidos/1': FICHA_PEDIDO,
  '/api/v1/pedidos/calendario': CALENDARIO,
  '/api/v1/pedidos/alertas': { proximas: [CALENDARIO[0], CALENDARIO[3]], atrasadas: [CALENDARIO[1]], ...RESUMEN_COBRANZA },
  '/api/v1/mis/solicitudes/123': MI_SOLICITUD,
  '/api/v1/mis/cotizaciones/45': { ...COT_PENDIENTE, elaboradaPor: { nombre: 'Marta' } },
  '/api/v1/mis/solicitudes': {
    elementos: [
      { ...BANDEJA.elementos[0], estado: 'cotizado', cotizacion: SEGUIMIENTO_COT, pedido: null },
      {
        ...BANDEJA.elementos[1], estado: 'aceptado', cotizacion: { idCotizacion: 40, numero: '#40', total: 99065, estado: 'aprobada' },
        pedido: {
          idPedido: 1, numero: '#1', estadoPedido: 'en_proceso', estadoPago: 'abonado', saldoPendiente: 59065, fechaEntregaEstimada: diaChile(2),
          modalidadEntrega: 'retiro_local', cicloCerrado: false,
        },
      },
    ],
    total: 2, pagina: 1, tamano: 10, totalPaginas: 1,
  },
});
// ── Fase 9: mantenedores, estado del local y avisos del taller ──
export const PERSONAL = [
  { idEmpleado: 1, rut: '12.345.678-5', nombre: 'Marta', apellido: 'Fuentes', telefono: '912345678', email: 'dueno@letreria.test', rol: 'dueño', activo: true, rowVersion: 'AAAAAAAAEM1=' },
  { idEmpleado: 2, rut: '11.111.111-1', nombre: 'Hugo', apellido: 'Vera', telefono: '987654321', email: 'hugo@letreria.test', rol: 'operario', activo: true, rowVersion: 'AAAAAAAAEM2=' },
];
export const PUBLICACION = {
  ...RESPUESTAS['/api/v1/catalogo'].elementos[0], visible: true, incluyeLogoCliente: false, logoAutorizado: false, idImagen: 4, rowVersion: 'AAAAAAAACAT=',
};
export const PUBLICACION_LOGO = {
  ...RESPUESTAS['/api/v1/catalogo'].elementos[1], visible: false, incluyeLogoCliente: true, logoAutorizado: false, rowVersion: 'AAAAAAAACA2=',
};
export const DESTINO_ACTIVO = {
  idDestinatario: 1, correo: 'contacto.letrerosherrera@gmail.com', estado: 'activo', reemplazaA: null,
  fechaSolicitudChile: `${HOY_CHILE}T08:00:00`, fechaConfirmacionChile: `${HOY_CHILE}T08:05:00`, venceChile: null,
};
export const AVISOS_TALLER = {
  destinos: [DESTINO_ACTIVO], usaRespaldo: false,
  historial: [{ fechaChile: `${HOY_CHILE}T08:05:00`, accion: 'confirmar', correo: 'c•••a@gmail.com', empleado: null, detalle: null }],
};
export const FRANJAS_ATENCION = [{ desde: '09:00:00', hasta: '13:00:00' }, { desde: '15:00:00', hasta: '18:00:00' }];
export const DIAS_ATENCION = RESPUESTAS['/api/v1/horarios'].map((d) => ({
  diaSemana: d.diaSemana, nombreDia: d.nombreDia, franjas: d.abierto ? FRANJAS_ATENCION.slice(0, d.diaSemana === 6 ? 1 : 2) : [],
}));
// ── Fase 10: rostro con prueba de vida ──
export const ROSTRO_PROPIO = {
  disponible: true, activo: false, puedeUsarRostro: true, debeRegistrarseDeNuevo: false, requiereEquipo: true, equipoVigente: false,
  consentimiento: { version: '2026-11', texto: ['Sistema biométrico: tres muestras cifradas de tu rostro.', 'Finalidad: entrar al panel sin escribir la contraseña.'] },
};
export const ROSTROS = [
  {
    idEmpleado: 1, nombre: 'Marta Fuentes', rol: 'dueño', estado: 'activo', fechaRegistro: fecha(0, 2), fechaConsentimiento: fecha(0, 2),
    registradoPor: 'Marta Fuentes', fechaDesactivacion: null, historial: [],
  },
  {
    idEmpleado: 3, nombre: 'Javiera Herrera Contreras', rol: 'administrador', estado: 'debe_registrarse', fechaRegistro: fecha(2, 3),
    fechaConsentimiento: fecha(2, 3), registradoPor: 'Marta Fuentes', fechaDesactivacion: null,
    historial: [{ fechaConsentimiento: fecha(3, 1), fechaDesactivacion: fecha(2, 2) }],
  },
  {
    idEmpleado: 4, nombre: 'Pedro Soto', rol: 'administrador', estado: 'sin_registro', fechaRegistro: null, fechaConsentimiento: null,
    registradoPor: null, fechaDesactivacion: null, historial: [],
  },
];
export const medicion = (id, etiqueta, base) => ({
  idMedicion: id, idEmpleado: 1, empleado: 'Marta Fuentes', etiqueta, distanciaFrente: base, distanciaPrimerGiro: base + 0.03,
  distanciaSegundoGiro: base + 0.05, distanciaMaxEntreCapturas: base + 0.08, giroFrente: 0.02, giroPrimero: 0.44, giroSegundo: -0.46,
  umbralVigente: 0.5, fecha: fecha(0, 4),
});
export const CALIBRACION = {
  activa: true, umbralVigente: 0.5, rangos: { frenteMaximo: 0.15, giroMinimo: 0.3, giroMaximo: 0.75 },
  mediciones: [medicion(1, 'propio', 0.31), medicion(2, 'propio', 0.34), medicion(3, 'otra_persona', 0.62)],
  resumen: {
    propio: { n: 2, min: 0.36, max: 0.39, promedio: 0.375 }, otra: { n: 1, min: 0.67, max: 0.67, promedio: 0.67 },
    umbralSugerido: 0.53, margen: 0.14,
  },
};

Object.assign(RESPUESTAS, {
  '/api/v1/empleados/activos': EMPLEADOS,
  '/api/v1/empleados': paginaDe(PERSONAL),
  '/api/v1/empleados/2': PERSONAL[1],
  // Fase 12D: la ficha de un tipo de letrero con sus límites en milímetros, medidas fijas y opciones.
  '/api/v1/productos/1': {
    idProducto: 1, idCategoria: 1, categoria: { idCategoria: 1, nombre: 'Impresión y publicidad gráfica', activo: true }, nombre: 'Roll Up / Pendones Roller',
    descripcion: 'Estructura de aluminio con bolso de transporte.', precioBaseRef: null, activo: true, rowVersion: 'AAAAAAAAPRO=',
    anchoMinMm: 800, anchoMaxMm: 1500, altoMinMm: 2000, altoMaxMm: 3000, medidasFijas: [],
    opciones: [{ grupo: 'Diseño', valor: 'Del cliente' }, { grupo: 'Diseño', valor: 'Desarrollado por la oficina' }],
  },
  '/api/v1/categorias/1': { idCategoria: 1, nombre: 'Luminosos', descripcion: 'Cajas de luz y letras corpóreas', activo: true, rowVersion: 'AAAAAAAACA1=' },
  '/api/v1/catalogo/1': PUBLICACION,
  '/api/v1/catalogo/2': PUBLICACION_LOGO,
  '/api/v1/avisos-taller': AVISOS_TALLER,
  '/api/v1/avisos-taller/confirmar': { correo: 'n•••o@taller.cl' },
  '/api/v1/mi-disponibilidad': [{ diaSemana: 1, horaInicio: '09:00:00', horaFin: '13:00:00' }],
  '/api/v1/disponibilidad': [
    { idEmpleado: 1, nombre: 'Marta Fuentes', franjas: [{ diaSemana: 1, horaInicio: '09:00:00', horaFin: '13:00:00' }] },
    { idEmpleado: 2, nombre: 'Hugo Vera', franjas: [{ diaSemana: 2, horaInicio: '15:00:00', horaFin: '18:00:00' }] },
  ],
  '/api/v1/local/atencion': DIAS_ATENCION,
  // Fase 10: el dueño ve el registro con tres muestras; «Rostros registrados» y la calibración encendida.
  '/api/v1/auth/rostro': ROSTRO_PROPIO,
  '/api/v1/rostros': ROSTROS,
  '/api/v1/rostros/calibracion': CALIBRACION,
});
// ── Fase 11: notificaciones, auditoría y privacidad ──
export const correoFallido = (id, tipo, tipoTexto, referencia, enlace, destino, extra = {}) => ({
  idCorreo: id, tipo, tipoTexto, referencia, enlace, destinoEnmascarado: destino, intentos: 6, motivo: 'El servidor de correo no aceptó el envío',
  fechaCreacion: `${HOY_CHILE}T09:00:00`, fechaFallo: `${HOY_CHILE}T09:40:00`, revisado: false, reenviable: true, reenviadoComo: null, ...extra,
});
export const NO_ENVIADOS = paginaDe([
  correoFallido(51, 'aviso_solicitud_nueva', 'Aviso de solicitud nueva', 'solicitud:123', '/panel/solicitudes/123', 'c•••a@gmail.com'),
  correoFallido(52, 'estado_pedido', 'Estado del pedido', 'pedido:1', '/panel/pedidos/1', 'r•••a@correo.test'),
  correoFallido(53, 'cotizacion_lista', 'Cotización lista', 'cotizacion:45', null, 'r•••a@correo.test', { reenviable: false, revisado: true }),
]);
export const BITACORA = paginaDe([
  {
    idAuditoria: 900, fecha: `${HOY_CHILE}T10:30:00`, usuario: { tipo: 'empleado', id: 1, nombre: 'Marta Fuentes' }, rol: 'dueño', entidad: 'Pedido',
    entidadTexto: 'Pedido', idEntidad: '1', accion: 'modificar', accionTexto: 'Modificó',
    detalle: { campos: [{ campo: 'estadoPedido', antes: 'aceptado', despues: 'en_proceso' }, { campo: 'direccionInstalacion' }] }, ip: '10.0.0.8',
  },
  {
    idAuditoria: 899, fecha: `${HOY_CHILE}T09:40:00`, usuario: { tipo: 'sistema', id: null, nombre: null }, rol: 'sistema', entidad: 'CorreoSaliente',
    entidadTexto: 'Correo', idEntidad: '51', accion: 'correo_fallido', accionTexto: 'Correo no enviado',
    detalle: { tipo: 'aviso_solicitud_nueva', referencia: 'solicitud:123', destinoEnmascarado: 'c•••a@gmail.com', intentos: 6, motivo: 'rechazado' }, ip: null,
  },
  {
    idAuditoria: 898, fecha: `${HOY_CHILE}T09:15:00`, usuario: { tipo: 'empleado', id: 2, nombre: 'Hugo Vera' }, rol: 'operario', entidad: 'Pago',
    entidadTexto: 'Abono', idEntidad: '11', accion: 'crear', accionTexto: 'Creó', detalle: { campos: [{ campo: 'monto', despues: 40000 }] }, ip: '10.0.0.9',
  },
]);
export const CONSENTIMIENTOS = paginaDe([
  { titular: { tipo: 'cliente', id: 9, nombre: 'Rosa Pino' }, tipo: 'terminos', version: '1.0', acepta: true, fecha: fecha(1, 3) },
  { titular: { tipo: 'cliente', id: 9, nombre: 'Rosa Pino' }, tipo: 'marketing', version: '1.0', acepta: false, fecha: fecha(0, 2) },
  { titular: { tipo: 'cliente', id: 9, nombre: 'Rosa Pino' }, tipo: 'avisos_estado', version: '1.0', acepta: true, fecha: fecha(0, 3) },
]);
Object.assign(DESTINO_ACTIVO, { recibeEnLinea: true, recibePresenciales: false, rowVersion: 'AAAAAAAADE1=' });
Object.assign(RESPUESTAS, {
  '/api/v1/correos/no-enviados': NO_ENVIADOS,
  '/api/v1/correos/no-enviados/resumen': { sinRevisar: 2 },
  '/api/v1/auditoria': BITACORA,
  '/api/v1/auditoria/consentimientos': CONSENTIMIENTOS,
  '/api/v1/correos/baja': { correoEnmascarado: 'r•••a@correo.test', avisosEstado: true, marketing: true },
});
export const IMAGEN_PRUEBA = 'public/marca/logo-lh-256.webp';

export function tokenDePrueba() {
  const carga = Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url');
  return `cabecera.${carga}.firma`;
}

export const destinoDe = (ruta) => new URL(ruta.request().url()).pathname.replace(/\/$/, '');

export const CLIENTE = { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', telefono: '912345678', email: 'rosa@correo.test', correoVerificado: false };
export const MI_CUENTA = {
  idCliente: 9, nombre: 'Rosa', apellido: 'Pino', rut: null, telefono: '912345678', correo: 'rosa@correo.test',
  direccion: null, correoVerificado: false, marketing: false, avisosEstado: true,
};

/**
 * Sesión simulada (fase 6): el token vive solo en memoria, así que la página la recupera al cargar con
 * POST /auth/renovar. La marca de sessionStorage le dice que hay sesión; la API simulada entrega el token.
 */
export async function conSesion(page, sesion, extras = {}) {
  await page.route('**/api/v1/**', (ruta) => {
    const destino = destinoDe(ruta);
    if (destino === '/api/v1/auth/renovar') return ruta.fulfill({ json: { token: tokenDePrueba(), ...sesion } });
    if (destino === '/api/v1/auth/salir') return ruta.fulfill({ status: 204 });
    if (/^\/api\/v1\/adjuntos\/\d+\/(miniatura|archivo)$/.test(destino)) return ruta.fulfill({ path: IMAGEN_PRUEBA, contentType: 'image/webp' });
    const cuerpo = extras[destino] ?? RESPUESTAS[destino];
    // Una función recibe los parámetros de la consulta (el dashboard de la fase 12B depende de ellos).
    if (typeof cuerpo === 'function') return ruta.fulfill({ json: cuerpo(new URL(ruta.request().url()).searchParams) });
    return cuerpo ? ruta.fulfill({ json: cuerpo }) : ruta.fulfill({ status: 404, json: { error: 'No encontrado' } });
  });
  await page.addInitScript(() => sessionStorage.setItem('synctime_hay_sesion', '1'));
}

export function entrarComo(page, rol) {
  return conSesion(page, {
    tipo: 'empleado', usuario: { idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', email: 'dueno@letreria.test', rol },
  });
}

export function entrarComoCliente(page, { verificado = false } = {}) {
  return conSesion(page, { tipo: 'cliente', usuario: { ...CLIENTE, correoVerificado: verificado } },
    { '/api/v1/mi-cuenta': { ...MI_CUENTA, correoVerificado: verificado } });
}

