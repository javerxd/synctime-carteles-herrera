import { TEXTOS_ESTADO_SOLICITUD } from '../../../componentes/ui/estados';
import { nombreCompleto, ORIGENES_SOLICITUD } from '../reglasSolicitud';

const ORDENES = ['fecha_desc', 'fecha_asc'];
const FECHA = /^\d{4}-\d{2}-\d{2}$/;
const VALIDOS = {
  estado: (v) => v in TEXTOS_ESTADO_SOLICITUD,
  origen: (v) => v in ORIGENES_SOLICITUD,
  orden: (v) => ORDENES.includes(v),
  desde: (v) => FECHA.test(v) && !Number.isNaN(Date.parse(v)),
  hasta: (v) => FECHA.test(v) && !Number.isNaN(Date.parse(v)),
  q: (v) => v.length <= 100,
};

// Filtros de la bandeja de solicitudes y datos de cada fila.
export const POR_PAGINA_BANDEJA = 20;
export const ORDEN_PREDETERMINADO = 'fecha_desc';
const CAMPOS = ['estado', 'origen', 'desde', 'hasta', 'q', 'orden'];

/** Filtros desde la dirección (así se puede volver atrás o compartir la vista). */
export function filtrosDesde(parametros) {
  // Un valor que la API no acepta (estado, origen u orden desconocido, fecha mal escrita) se ignora.
  const filtros = Object.fromEntries(CAMPOS.map((c) => {
    const valor = parametros.get(c) ?? '';
    return [c, valor && VALIDOS[c] && !VALIDOS[c](valor) ? '' : valor];
  }));
  filtros.orden = filtros.orden || ORDEN_PREDETERMINADO;
  filtros.pagina = Math.max(1, Number.parseInt(parametros.get('pagina') ?? '1', 10) || 1);
  return filtros;
}

/** Los parámetros de la dirección que sobran o no sirven (para limpiarlos). */
export function parametrosInvalidos(parametros) {
  const filtros = filtrosDesde(parametros);
  const malos = [];
  for (const [clave, valor] of parametros.entries()) {
    if (clave === 'pagina') {
      if (String(filtros.pagina) !== valor) malos.push(clave);
    } else if (!CAMPOS.includes(clave) || filtros[clave] !== valor) {
      malos.push(clave);
    }
  }
  return malos;
}

/** Quita de la dirección los campos que la API rechazó con 400 (errores por campo). */
export function camposRechazados(error) {
  if (error?.estado !== 400 || !error.errores) return [];
  return Object.keys(error.errores).map((c) => c.charAt(0).toLowerCase() + c.slice(1)).filter((c) => [...CAMPOS, 'pagina'].includes(c));
}

/** Los parámetros que se piden a la API. Un rango de fechas al revés no se envía. */
export function parametrosApi(filtros) {
  const rangoInvalido = filtros.desde && filtros.hasta && filtros.hasta < filtros.desde;
  return {
    estado: filtros.estado || undefined,
    origen: filtros.origen || undefined,
    desde: rangoInvalido ? undefined : filtros.desde || undefined,
    hasta: rangoInvalido ? undefined : filtros.hasta || undefined,
    q: filtros.q.trim() || undefined,
    orden: filtros.orden,
    pagina: filtros.pagina,
    tamano: POR_PAGINA_BANDEJA,
  };
}

/** Quién la registró: el empleado, o el propio cliente si llegó en línea. */
export function registradaPor(s) {
  const quien = s.registradaPor;
  if (typeof quien === 'string' && quien) return quien;
  if (quien?.nombre) return nombreCompleto(quien);
  return s.origen === 'online' ? 'El cliente, en línea' : '—';
}

/** Lo que muestra el inicio del panel: las «Solicitadas», de la más antigua a la más nueva. */
export const FILTRO_PENDIENTES = { estado: 'solicitado', orden: 'fecha_asc', pagina: 1, tamano: 5 };

/** «Banners», o «Banners y 2 más» cuando la solicitud trae varios letreros (fila corta de «Solicitudes por atender»). */
export function resumenProducto(solicitud) {
  const cantidad = solicitud.cantidadItems ?? 0;
  if (!solicitud.primerProducto) return cantidad ? `${cantidad} ${cantidad === 1 ? 'letrero' : 'letreros'}` : '';
  return cantidad > 1 ? `${solicitud.primerProducto} y ${cantidad - 1} más` : solicitud.primerProducto;
}
