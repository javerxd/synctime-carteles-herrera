import { api } from './cliente';
import { comoPaginado } from './solicitudes';

// Fase 11: bitácora de auditoría y consentimientos (solo gestión, solo lectura).
// Registro: { idAuditoria, fecha, usuario: { tipo: 'empleado' | 'cliente' | 'sistema', id, nombre }, rol, entidad,
//   entidadTexto, idEntidad, accion, accionTexto, detalle (objeto), ip }, del más reciente al más antiguo.
// Consentimiento: { titular: { tipo, id, nombre }, tipo, version, acepta, fecha }.

export const auditoria = {
  /** { pagina, tamano, desde, hasta, entidad, accion, idEmpleado, q } → paginado. */
  listar: (filtros = {}, { senal } = {}) => api.get('/auditoria', { parametros: filtros, senal }).then((r) => comoPaginado(r, filtros)),
  /** { pagina, tamano, tipo, desde, hasta } → paginado. */
  consentimientos: (filtros = {}, { senal } = {}) => api.get('/auditoria/consentimientos', { parametros: filtros, senal })
    .then((r) => comoPaginado(r, filtros)),
};
