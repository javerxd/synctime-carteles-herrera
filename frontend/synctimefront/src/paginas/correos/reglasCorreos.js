// Reglas de «Correos no enviados» y de la baja (fase 11). Sin React, para probarlas por separado.
import { esFecha } from '../../utilidades/fechas';
import { parametrosBase } from '../../utilidades/filtrosUrl';

/** Los avisos del taller (correos operativos): al reenviarlos van a los destinos activos de hoy (decisión 3). */
export const TIPOS_AVISO_TALLER = ['aviso_solicitud_nueva', 'aviso_tope_solicitudes', 'consulta_contacto'];

export const esAvisoDelTaller = (tipo) => TIPOS_AVISO_TALLER.includes(tipo);

/**
 * Nombres de los tipos de correo para el filtro. La fila trae su propio `tipoTexto` (lo manda la API); esta lista
 * solo arma el selector y sirve de respaldo.
 */
export const TEXTOS_TIPO_CORREO = {
  aviso_solicitud_nueva: 'Aviso de solicitud nueva',
  aviso_tope_solicitudes: 'Aviso de tope de solicitudes',
  consulta_contacto: 'Consulta de contacto',
  confirmacion_solicitud: 'Confirmación de solicitud',
  cotizacion_lista: 'Cotización lista',
  estado_solicitud: 'Estado de la solicitud',
  estado_pedido: 'Estado del pedido',
  verificacion_correo: 'Verificación del correo',
  recuperacion_cuenta: 'Recuperación de la cuenta',
  aviso_cambio_correo: 'Aviso de cambio de correo',
  baja_cuenta: 'Comprobante de baja de la cuenta',
  confirmacion_destino_avisos: 'Confirmación de destino de avisos',
  destino_avisos_agregado: 'Destino de avisos agregado',
  destino_avisos_cambiado: 'Destino de avisos cambiado',
  destino_avisos_quitado: 'Destino de avisos quitado',
  aviso_cambio_correo_personal: 'Aviso de cambio de correo del personal',
  aviso_acceso_reasignado: 'Aviso de acceso reasignado',
  prueba_avisos: 'Correo de prueba de los avisos',
};

export const textoTipo = (correo) => correo?.tipoTexto || TEXTOS_TIPO_CORREO[correo?.tipo] || correo?.tipo || 'Correo';

/** A quién irá el reenvío, en palabras (lo que se lee en la confirmación). */
export function destinoDelReenvio(correo) {
  if (esAvisoDelTaller(correo?.tipo)) {
    return 'Irá a los destinos activos de hoy de «Avisos del taller», según lo que recibe cada uno. No necesariamente al correo de la primera vez.';
  }
  return `Irá a la misma dirección de la primera vez: ${correo?.destinoEnmascarado || 'la del destinatario original'}.`;
}

/** Solo rutas internas del panel: un enlace raro de la API no se convierte en un vínculo a otro sitio. */
export function enlaceSeguro(enlace) {
  const texto = String(enlace ?? '');
  return /^\/panel\/[\w\-/]*$/.test(texto) ? texto : null;
}

const NOMBRES_REFERENCIA = { solicitud: 'Solicitud', cotizacion: 'Cotización', pedido: 'Pedido', cliente: 'Cliente', contacto: 'Consulta' };

/** «solicitud:123» → «Solicitud #123». Lo que no tenga esa forma se muestra tal cual. */
export function textoReferencia(referencia) {
  const m = String(referencia ?? '').match(/^([a-z_]+):(.+)$/i);
  if (!m) return referencia || '—';
  const nombre = NOMBRES_REFERENCIA[m[1].toLowerCase()];
  return nombre ? `${nombre} #${m[2]}` : referencia;
}

/** Por qué no se puede reenviar (o null si se puede). */
export function motivoNoReenviable(correo) {
  if (correo?.reenviable) return null;
  if (correo?.reenviadoComo) return `Ya se reenvió como el correo #${correo.reenviadoComo}: si vuelve a fallar, aparecerá de nuevo aquí.`;
  return 'Ya no se puede reenviar: su contenido se borró por el plazo de conservación.';
}

export const TEXTOS_REVISADO = { false: 'Sin revisar', true: 'Revisados' };

export const FILTROS_CORREOS = {
  campos: {
    tipo: (v) => /^[a-z_]{1,40}$/.test(v),
    desde: esFecha,
    hasta: esFecha,
    revisado: (v) => v === 'true' || v === 'false',
  },
  predeterminados: {},
  tamano: 20,
};

/** Lo que se pide a la API; un rango de fechas al revés no viaja. */
export function parametrosCorreos(filtros) {
  const base = parametrosBase(filtros, FILTROS_CORREOS);
  if (base.desde && base.hasta && base.hasta < base.desde) return { ...base, desde: undefined, hasta: undefined };
  return base;
}

/** «3 correos no se pudieron enviar» / «1 correo no se pudo enviar». */
export const textoSinRevisar = (n) => (n === 1 ? '1 correo no se pudo enviar' : `${n} correos no se pudieron enviar`);
