import { api } from './cliente';
import { comoPaginado, siVersion } from './solicitudes';

// Mantenedores (fase 9): categorías, tipos de letrero (productos), vitrina (catálogo), materiales y personal.
// Contrato: docs/plan/fase-09-especificacion.md, «Mantenedores». Listados con q, activo, orden, pagina y tamano
// → { elementos, total, pagina, tamano, totalPaginas }. Cada ficha trae su rowVersion: editar, activar o
// desactivar la envía como If-Match (409 version_obsoleta si otra persona la cambió). No hay borrado físico.

function mantenedor(ruta) {
  return {
    listar: (filtros = {}, { senal } = {}) => api.get(ruta, { parametros: filtros, senal }).then((r) => comoPaginado(r, filtros)),
    obtener: (id) => api.get(`${ruta}/${id}`),
    crear: (datos) => api.post(ruta, datos),
    editar: (id, datos, rowVersion) => api.put(`${ruta}/${id}`, datos, siVersion(rowVersion)),
    cambiarActivo: (id, activo, rowVersion) => api.patch(`${ruta}/${id}/activo`, { activo }, siVersion(rowVersion)),
  };
}

/** { nombre, descripcion? } */
export const categorias = mantenedor('/categorias');
/** Tipos de letrero: { idCategoria, nombre, descripcion?, precioBaseRef? } */
export const productos = {
  ...mantenedor('/productos'),
  /**
   * Fase 12D: { anchoMinMm, anchoMaxMm, altoMinMm, altoMaxMm, medidasFijas: [{ anchoMm, altoMm }], opciones: [{ grupo, valores }] }
   * (milímetros). Exige la versión vista (If-Match): 409 version_obsoleta si otra persona lo cambió.
   */
  editarMedidasYOpciones: (id, datos, rowVersion) => api.put(`/productos/${id}/medidas-y-opciones`, datos, siVersion(rowVersion)),
};
/** { nombre, unidadMedida, costoUnitarioAprox } */
export const materialesTaller = mantenedor('/materiales');

/**
 * Publicaciones de la vitrina: { idProducto, titulo, descripcion, idImagen?, anchoMm?, altoMm?, precioReferencial?,
 * incluyeLogoCliente, visible }. Gestión ve todas (también las ocultas). Hacer visible una con logo sin
 * autorización responde 409 logo_sin_autorizacion.
 */
export const vitrina = {
  listar: (filtros = {}, { senal } = {}) => api.get('/catalogo', { parametros: { incluirOcultos: true, ...filtros }, senal })
    .then((r) => comoPaginado(r, filtros)),
  obtener: (id) => api.get(`/catalogo/${id}`),
  crear: (datos) => api.post('/catalogo', datos),
  editar: (id, datos, rowVersion) => api.put(`/catalogo/${id}`, datos, siVersion(rowVersion)),
  cambiarVisible: (id, visible, rowVersion) => api.patch(`/catalogo/${id}/visible`, { visible }, siVersion(rowVersion)),
  /** Registra (o retira) la autorización del cliente para mostrar su logo (RNFO17). */
  autorizarLogo: (id, autorizado, rowVersion) => api.put(`/catalogo/${id}/autorizacion-logo`, { autorizado }, siVersion(rowVersion)),
  /** POST /imagenes (multipart, campo «archivo», fase 4/5) → 201 { idImagen?, clave, ancho, alto, urlGrande, urlMiniatura }. */
  subirImagen: (archivo, opciones) => api.subir('/imagenes', archivo, opciones),
};

/**
 * Personal: { rut, nombre, apellido, telefono, email, rol, password (solo al crear) }. El alta exige además la
 * contraseña de quien la da (passwordActual). 409 propio (sobre uno mismo) o ultimo_dueno.
 */
export const personal = {
  ...mantenedor('/empleados'),
  crear: (datos) => api.post('/empleados', datos),
};

const lista = (r) => (Array.isArray(r) ? r : r?.elementos ?? []);

/** Lista corta del personal activo { idEmpleado, nombre, rol }, que puede ver todo el personal (editor de pedidos). */
export const personalActivo = {
  listar: () => api.get('/empleados/activos').then(lista),
};

/**
 * Todas las filas de un listado paginado (hasta `maximo` páginas de 100). Para las listas de los formularios
 * (materiales de la cotización, categorías del tipo de letrero), que necesitan el catálogo completo.
 */
export async function todasLasPaginas(ruta, parametros = {}, { maximo = 10 } = {}) {
  const filas = [];
  for (let pagina = 1; pagina <= maximo; pagina += 1) {
    const respuesta = await api.get(ruta, { parametros: { ...parametros, pagina, tamano: 100 } });
    if (Array.isArray(respuesta)) return respuesta;
    filas.push(...(respuesta?.elementos ?? []));
    if (!respuesta?.totalPaginas || pagina >= respuesta.totalPaginas) break;
  }
  return filas;
}
