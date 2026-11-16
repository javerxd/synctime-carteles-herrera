// Reglas de la sección «Avisos del taller» (fase 9): enmascarar correos, leer los destinos y decidir cuándo
// dejar de consultar el estado del correo de prueba. Sin React, para probarlas por separado.

/** Cada cuánto se consulta el estado del correo de prueba, y hasta cuándo. */
export const INTERVALO_PRUEBA_MS = 2000;
export const TOPE_PRUEBA_MS = 60000;
export const CONSULTAS_MAXIMAS = TOPE_PRUEBA_MS / INTERVALO_PRUEBA_MS;

/** «contacto@gmail.com» → «c•••o@gmail.com»: la primera y la última letra antes de la @, y el dominio completo. */
export function enmascararCorreo(correo) {
  const texto = String(correo ?? '').trim();
  const arroba = texto.lastIndexOf('@');
  if (arroba < 1) return texto ? '•••' : '';
  const usuario = texto.slice(0, arroba);
  const dominio = texto.slice(arroba);
  if (usuario.length === 1) return `${usuario}•••${dominio}`;
  return `${usuario[0]}•••${usuario[usuario.length - 1]}${dominio}`;
}

const CORREO = /^[^\s@;,"]+@[^\s@;,"]+\.[^\s@;,"]+$/;

/** El mismo criterio de correo de la fase 6 (la API vuelve a validar). */
export function errorCorreoAviso(correo) {
  const c = String(correo ?? '').trim();
  if (!c) return 'Escribe el correo que recibirá los avisos.';
  if (!CORREO.test(c) || c.length > 254) return 'Escribe un correo válido, por ejemplo avisos@letrerosherrera.cl.';
  return null;
}

/** Los activos primero y, dentro de cada estado, en el orden de llegada. */
export function ordenarDestinos(destinos = []) {
  const peso = (d) => (d.estado === 'activo' ? 0 : 1);
  return [...destinos].sort((a, b) => peso(a) - peso(b) || a.idDestinatario - b.idDestinatario);
}

export const activos = (destinos = []) => destinos.filter((d) => d.estado === 'activo');

/** El cambio pendiente que reemplazaría a este activo, si lo hay. */
export const reemplazoPendiente = (destino, destinos = []) =>
  destinos.find((d) => d.estado === 'pendiente' && d.reemplazaA?.idDestinatario === destino.idDestinatario) ?? null;

/** Un activo solo se quita si queda otro (decisión 3 del plan): para cambiar el último, se reemplaza. */
export const puedeQuitar = (destino, destinos = []) => destino.estado === 'activo' && activos(destinos).length > 1;

export const MOTIVO_NO_QUITAR = 'Debe quedar al menos un correo activo. Para cambiarlo, usa «Reemplazar».';

/** ¿Ya venció el enlace de un pendiente? (`ahora` en milisegundos). */
export function enlaceVencido(destino, ahora = Date.now()) {
  if (destino?.estado !== 'pendiente' || !destino.venceChile) return false;
  const vence = new Date(destino.venceChile).getTime();
  return Number.isFinite(vence) && vence <= ahora;
}

export const TEXTOS_ACCION = {
  agregar: 'Se pidió agregar',
  reemplazar: 'Se pidió reemplazar por',
  confirmar: 'Se confirmó',
  cancelar: 'Se canceló el cambio a',
  quitar: 'Se quitó',
  reenviar: 'Se reenvió el enlace a',
  probar: 'Correo de prueba a',
  preferencia: 'Se cambió qué avisos recibe',
};

// ── Preferencia de avisos por destino (fase 11, decisión 1) ──

/** Lo guardado de un destino; si la API aún no lo trae, lo de la base por defecto (solo en línea). */
export const preferenciaDe = (destino) => ({
  recibeEnLinea: destino?.recibeEnLinea !== false,
  recibePresenciales: destino?.recibePresenciales === true,
});

/** Al menos una de las dos (la base y la API también lo exigen). */
export const errorPreferencia = ({ recibeEnLinea, recibePresenciales }) =>
  (recibeEnLinea || recibePresenciales ? null : 'Marca al menos un tipo de solicitud.');

export function textoPreferencia({ recibeEnLinea, recibePresenciales }) {
  if (recibeEnLinea && recibePresenciales) return 'los avisos de las solicitudes en línea y presenciales';
  if (recibePresenciales) return 'solo los avisos de las solicitudes presenciales';
  return 'solo los avisos de las solicitudes en línea';
}

// ── Correo de prueba ──

/** ¿Hay que volver a consultar? Mientras algún correo siga en cola y no se llegue al tope de 60 segundos. */
export function debeSeguirConsultando(estados, consultasHechas) {
  if (consultasHechas >= CONSULTAS_MAXIMAS) return false;
  if (!estados?.length) return true;
  return estados.some((e) => e.estado === 'pendiente');
}

/**
 * Lo que se dice de cada destinatario: «Salió bien», «No salió: …» o, mientras se consulta, «Enviando…»; si se
 * llegó al tope y sigue en cola, «Sigue en cola».
 */
export function textoEstadoPrueba(estado, { terminado = false } = {}) {
  if (estado?.estado === 'enviado') return { tono: 'exito', texto: 'Salió bien' };
  if (estado?.estado === 'fallido') return { tono: 'peligro', texto: `No salió: ${estado.error || 'el servidor de correo no lo aceptó'}` };
  return terminado
    ? { tono: 'aviso', texto: 'Sigue en cola. Saldrá cuando el servidor de correo responda; revisa la bandeja en unos minutos.' }
    : { tono: 'neutro', texto: 'Enviando…' };
}

/** Une lo que dijo el POST (destinatarios) con el último estado consultado. */
export function filasPrueba({ idsCorreos = [], destinatarios = [] }, estados = []) {
  return idsCorreos.map((idCorreo, i) => {
    const estado = estados.find((e) => e.idCorreo === idCorreo);
    return { idCorreo, destinatario: estado?.destinatario ?? destinatarios[i] ?? '', estado: estado?.estado ?? 'pendiente', error: estado?.error ?? null };
  });
}
