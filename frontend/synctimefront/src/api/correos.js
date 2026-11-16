import { api } from './cliente';
import { comoPaginado } from './solicitudes';

// Fase 11: correos que no salieron (gestión) y la baja de los correos opcionales (pública, sin sesión).
// Contrato: docs/plan/fase-11-especificacion.md, «Avisos del taller: preferencia, no enviados y reenvío» y «Enlace de baja».
//
// Correo no enviado: { idCorreo, tipo, tipoTexto, referencia, enlace, destinoEnmascarado, intentos, motivo,
//   fechaCreacion, fechaFallo, revisado, reenviable, reenviadoComo }. Nunca trae el asunto, el cuerpo ni la dirección.

export const correos = {
  /** { pagina, tamano, tipo, desde, hasta, revisado } → paginado. */
  noEnviados: (filtros = {}, { senal } = {}) => api.get('/correos/no-enviados', { parametros: filtros, senal })
    .then((r) => comoPaginado(r, filtros)),
  /** { sinRevisar } para el aviso del inicio del panel. */
  resumen: ({ senal } = {}) => api.get('/correos/no-enviados/resumen', { senal }),
  /** 202 { idCorreoNuevo }. 409 ya_reenviado (con su id) o sin_contenido (el cuerpo ya se purgó); 404 si no existe. */
  reenviar: (id) => api.post(`/correos/no-enviados/${id}/reenviar`),
  /** Lo marca como revisado sin reenviar → 204. */
  async marcarRevisado(id) {
    await api.post(`/correos/no-enviados/${id}/revisado`);
  },
};

export const bajaCorreos = {
  /** GET /correos/baja?t= → { correoEnmascarado, avisosEstado, marketing }. No cambia nada. 404 si el enlace no sirve. */
  consultar: (t) => api.get('/correos/baja', { parametros: { t }, sinRenovar: true }),
  /** { t, avisosEstado?: false, marketing?: false } (sin campos apaga los dos) → { avisosEstado, marketing }. Idempotente. */
  confirmar: (cuerpo) => api.post('/correos/baja', cuerpo, { sinRenovar: true }),
};
