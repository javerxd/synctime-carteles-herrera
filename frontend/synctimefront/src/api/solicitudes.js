import { api, conClave, rutaRelativa } from './cliente';

// Solicitudes, adjuntos y clientes (fase 7). Contrato: docs/plan/fase-07-especificacion.md.
//
// Comprobante: { idSolicitud, numero, fechaChile, origen, estado, cliente: { nombre, apellido, telefono, correo },
//   detalles: [{ producto, descripcion, anchoMm, altoMm, cantidad, areaM2 }], areaTotalM2, descripcionGeneral,
//   adjuntos: [{ idAdjunto, tipo, nombre, bytes, urlMiniatura }], rowVersion }
// Listados paginados: { elementos, total, pagina, tamano, totalPaginas }.

/** La versión que vio quien edita, como etiqueta de entidad (If-Match: "AAAAAAAAB9E="). */
export const siVersion = (rowVersion) => (rowVersion ? { cabeceras: { 'If-Match': `"${rowVersion}"` } } : undefined);

/** Lo que entrega la API como listado: si llega una lista simple, se pagina aquí para que la pantalla no cambie. */
export function comoPaginado(respuesta, { pagina = 1, tamano = 20 } = {}) {
  if (!Array.isArray(respuesta)) return respuesta;
  const inicio = (pagina - 1) * tamano;
  return {
    elementos: respuesta.slice(inicio, inicio + tamano),
    total: respuesta.length,
    pagina,
    tamano,
    totalPaginas: Math.max(1, Math.ceil(respuesta.length / tamano)),
  };
}

/** Del cliente con sesión: sus adjuntos en borrador y sus solicitudes. */
export const misSolicitudes = {
  /** POST /mis/adjuntos (multipart, campo «archivo») → 201 { idAdjunto, tipo, nombre, bytes, ancho, alto, urlMiniatura }. */
  subirAdjunto: (archivo, opciones) => api.subir('/mis/adjuntos', archivo, opciones),
  /** GET /mis/adjuntos: los borradores del cliente aún sin enviar (se recuperan al volver al formulario). */
  borradores: () => api.get('/mis/adjuntos').then((r) => (Array.isArray(r) ? r : r?.elementos ?? [])),
  /** DELETE /mis/adjuntos/{id} → 204 (solo un borrador propio). */
  async quitarAdjunto(idAdjunto) {
    await api.delete(`/mis/adjuntos/${idAdjunto}`);
  },
  /**
   * POST /mis/solicitudes { descripcionGeneral, detalles, adjuntos } → 201 comprobante.
   * 403 correo_sin_confirmar · 409 contacto_directo · 400 con errores por campo.
   * `clave` (fase 11): Idempotency-Key del formulario abierto; la misma en cada reintento.
   */
  enviar: (solicitud, { clave } = {}) => api.post('/mis/solicitudes', solicitud, conClave(clave)),
  listar: ({ pagina = 1, tamano = 10 } = {}) => api.get('/mis/solicitudes', { parametros: { pagina, tamano } })
    .then((r) => comoPaginado(r, { pagina, tamano })),
  /** Con el seguimiento (fase 8): `cotizacion` { idCotizacion, numero, total, estado } y `pedido` (S04), o null. */
  obtener: (id) => api.get(`/mis/solicitudes/${id}`),
};

/** Archivos privados de un adjunto (cliente dueño o personal). Devuelven un Blob. */
export const adjuntos = {
  miniatura: (adjunto, { senal } = {}) => api.archivo(adjunto.urlMiniatura ? rutaRelativa(adjunto.urlMiniatura) : `/adjuntos/${adjunto.idAdjunto}/miniatura`, { senal }),
  archivo: (adjunto, { senal } = {}) => api.archivo(adjunto.urlArchivo ? rutaRelativa(adjunto.urlArchivo) : `/adjuntos/${adjunto.idAdjunto}/archivo`, { senal }),
};

/** Del personal del taller. */
export const solicitudes = {
  /** Bandeja: { estado, origen, desde, hasta, q, orden, pagina, tamano } → paginado. */
  listar: (filtros = {}) => api.get('/solicitudes', { parametros: filtros }).then((r) => comoPaginado(r, filtros)),
  /** Comprobante + historial + registradaPor; en la fase 8, también sus cotizaciones (ver api/cotizaciones.js). */
  obtener: (id) => api.get(`/solicitudes/${id}`),
  /** Registro presencial: { idCliente, descripcionGeneral, detalles } → 201 comprobante. `clave`: Idempotency-Key. */
  crear: (solicitud, { clave } = {}) => api.post('/solicitudes', solicitud, conClave(clave)),
  /** Edición con control de concurrencia: 409 si otra persona la modificó. */
  editar: (id, cambios, rowVersion) => api.put(`/solicitudes/${id}`, cambios, siVersion(rowVersion)),
  /**
   * { estado: 'rechazado', motivo } o { estado: 'solicitado' }, con la versión vista (If-Match).
   * 409 transicion_invalida o version_obsoleta; 400 sin_version.
   */
  cambiarEstado: (id, cambio, rowVersion) => api.patch(`/solicitudes/${id}/estado`, cambio, siVersion(rowVersion)),
  /** Borrado lógico, con la versión vista (If-Match, sin cuerpo). */
  async eliminar(id, rowVersion) {
    await api.patch(`/solicitudes/${id}/eliminar-logico`, undefined, siVersion(rowVersion));
  },
  /** Sube y vincula de inmediato (tope de 5). */
  subirAdjunto: (id, archivo, opciones) => api.subir(`/solicitudes/${id}/adjuntos`, archivo, opciones),
  async quitarAdjunto(id, idAdjunto) {
    await api.delete(`/solicitudes/${id}/adjuntos/${idAdjunto}`);
  },
};

export const clientes = {
  /** { q, pagina, tamano } → paginado (q busca por teléfono, RUT o nombre). */
  listar: (filtros = {}) => api.get('/clientes', { parametros: filtros }).then((r) => comoPaginado(r, filtros)),
  obtener: (id) => api.get(`/clientes/${id}`),
  /** GET /clientes/buscar?telefono=&rut= → arreglo de 0 o 1 filas; aquí, el cliente o null. */
  async buscar({ telefono, rut }) {
    try {
      const respuesta = await api.get('/clientes/buscar', { parametros: { telefono, rut } });
      if (Array.isArray(respuesta)) return respuesta[0] ?? null;
      return respuesta?.idCliente ? respuesta : null;
    } catch (error) {
      if (error?.estado === 404) return null;
      throw error;
    }
  },
  /** { rut?, nombre, apellido, telefono, email?, direccion? } → cliente. 409 si el teléfono ya existe. */
  crear: (cliente) => api.post('/clientes', { activo: true, ...cliente }),
  /** Con la versión vista (If-Match, fase 9, RNFP10): 409 version_obsoleta si otra persona cambió la ficha. */
  actualizar: (id, cliente, rowVersion) => api.put(`/clientes/${id}`, cliente, siVersion(rowVersion)),
};
