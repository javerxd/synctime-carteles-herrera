// Reglas del visor de auditoría (fase 11): filtros, nombres en español y el detalle legible. Sin React.
import { esFecha } from '../../utilidades/fechas';
import { parametrosBase } from '../../utilidades/filtrosUrl';
import { TEXTOS_TIPO_CORREO } from '../correos/reglasCorreos';

/**
 * Las entidades que registra el interceptor (especificación, «Bitácora de auditoría»), con su nombre en español.
 * La fila trae su propio `entidadTexto`; esta lista arma el filtro.
 */
export const ENTIDADES = {
  Solicitud: 'Solicitud',
  SolicitudDetalle: 'Letrero de una solicitud',
  Cotizacion: 'Cotización',
  Pedido: 'Pedido',
  Pago: 'Abono',
  Catalogo: 'Publicación de la vitrina',
  Producto: 'Tipo de letrero',
  Categoria: 'Categoría',
  Material: 'Material',
  Cliente: 'Cliente',
  Empleado: 'Personal',
  DestinatarioAviso: 'Destino de avisos',
  RostroEmpleado: 'Rostro registrado',
  HorarioAtencion: 'Horario',
  CartelDigital: 'Cartel del local',
  CorreoSaliente: 'Correo',
};

export const ACCIONES = {
  crear: 'Creó',
  modificar: 'Modificó',
  eliminar: 'Eliminó',
  aviso_enviado: 'Aviso enviado',
  correo_fallido: 'Correo no enviado',
  correo_reenviado: 'Correo reenviado',
  baja_correo: 'Baja de correos',
  baja_cuenta: 'Baja de la cuenta',
  descarga_datos: 'Descarga de datos',
};

export const TIPOS_CONSENTIMIENTO = {
  terminos: 'Términos y condiciones',
  privacidad: 'Política de privacidad',
  marketing: 'Novedades y promociones',
  avisos_estado: 'Avisos de estado',
  cookies: 'Cookies de analítica',
};

/** Nombres de campos frecuentes; el resto se lee separando las palabras («fechaEntregaReal» → «Fecha entrega real»). */
const ETIQUETAS = {
  estado: 'Estado',
  estadoPedido: 'Estado del pedido',
  estadoPago: 'Estado del pago',
  direccionInstalacion: 'Dirección de instalación',
  fechaEntregaEstimada: 'Fecha de entrega estimada',
  fechaEntregaReal: 'Fecha de entrega real',
  montoTotalFinal: 'Monto total',
  montoBaseTrabajo: 'Monto del trabajo',
  costoInstalacionAdicional: 'Costo de instalación',
  costoManoObra: 'Mano de obra',
  otrosCostos: 'Otros costos',
  numeroFactura: 'Número de factura',
  medioPago: 'Medio de pago',
  fechaPago: 'Fecha de pago',
  motivoAnulacion: 'Motivo de anulación',
  idEmpleadoResponsable: 'Responsable',
  recibeEnLinea: 'Recibe solicitudes en línea',
  recibePresenciales: 'Recibe solicitudes presenciales',
  abiertoEnLocal: 'Abierto en el local',
  mensajeEstado: 'Mensaje del cartel',
  horaInicio: 'Apertura',
  horaFin: 'Cierre',
  descripcionGeneral: 'Descripción',
  destinoEnmascarado: 'Destino',
  referencia: 'Referencia',
  intentos: 'Intentos',
  motivo: 'Motivo',
  tipo: 'Tipo',
  monto: 'Monto',
  activo: 'Activo',
  visible: 'Visible',
  idCorreoNuevo: 'Correo nuevo',
  comprobante: 'Comprobante',
};

export function etiquetaCampo(campo) {
  if (Object.hasOwn(ETIQUETAS, campo)) return ETIQUETAS[campo];
  const palabras = String(campo ?? '').replace(/[_-]+/g, ' ').replace(/([a-záéíóúñ])([A-Z])/g, '$1 $2').trim().toLowerCase();
  return palabras ? palabras.charAt(0).toUpperCase() + palabras.slice(1) : 'Campo';
}

/** Claves que nunca se muestran aunque la API las mandara por error (la API ya no debe guardarlas). */
const SENSIBLE = /pass|token|secret|hash|clave|codigo|authorization|descriptor|cuerpo|asunto/i;
const CORREO = /^[^\s@]+@[^\s@]+$/;
const LARGO_MAXIMO = 200;

const enmascarar = (correo) => {
  const [usuario, dominio] = correo.split('@');
  return `${usuario[0]}•••${usuario.length > 1 ? usuario[usuario.length - 1] : ''}@${dominio}`;
};

/** Un valor del detalle como texto: sí/no, números, textos; null si no viene. Un correo completo se enmascara. */
export function textoValor(valor) {
  if (valor === null || valor === undefined || valor === '') return null;
  if (typeof valor === 'boolean') return valor ? 'Sí' : 'No';
  let texto = typeof valor === 'object' ? JSON.stringify(valor) : String(valor);
  if (CORREO.test(texto)) texto = enmascarar(texto);
  return texto.length > LARGO_MAXIMO ? `${texto.slice(0, LARGO_MAXIMO)}…` : texto;
}

const tieneValor = (objeto, ...claves) => claves.some((c) => objeto && Object.hasOwn(objeto, c) && objeto[c] !== undefined);

function cambioDesde(campo, antes, despues, conValores) {
  return { campo, etiqueta: etiquetaCampo(campo), antes: conValores ? textoValor(antes) : null, despues: conValores ? textoValor(despues) : null, conValores };
}

/**
 * El detalle (objeto JSON de la API) como { cambios, otros }:
 * - `cambios`: los campos cambiados. Acepta `campos: ['estado', …]`, `campos: [{ campo, antes?, despues? }]` y
 *   `cambios: { campo: { antes, despues } }`. Antes y después solo si vienen (si no, se dice «cambió»).
 * - `otros`: el resto de los datos (tipo, referencia, destino enmascarado, intentos, motivo, montos…).
 */
export function detalleLegible(detalle) {
  if (detalle === null || detalle === undefined || detalle === '') return { cambios: [], otros: [] };
  if (typeof detalle !== 'object' || Array.isArray(detalle)) return { cambios: [], otros: [{ etiqueta: 'Detalle', valor: textoValor(detalle) }] };
  const cambios = [];
  // La API manda `campos: ['estado', …]` y, solo para los campos permitidos, `valores: { estado: { antes, despues } }`.
  const valores = detalle.valores && typeof detalle.valores === 'object' && !Array.isArray(detalle.valores) ? detalle.valores : {};
  const usados = new Set();
  for (const item of Array.isArray(detalle.campos) ? detalle.campos : []) {
    if (typeof item === 'string') {
      const par = Object.hasOwn(valores, item) && !SENSIBLE.test(item) ? valores[item] : null;
      if (par) usados.add(item);
      cambios.push(par && typeof par === 'object'
        ? cambioDesde(item, par.antes, par.despues, tieneValor(par, 'antes', 'despues'))
        : cambioDesde(item, null, null, false));
    }
    else if (item && typeof item === 'object') {
      const campo = item.campo ?? item.nombre ?? '';
      // Un campo sensible se dice que cambió, nunca su valor.
      if (SENSIBLE.test(campo)) {
        cambios.push(cambioDesde(campo, null, null, false));
        continue;
      }
      const antes = item.antes ?? item.anterior;
      const despues = item.despues ?? item.nuevo;
      cambios.push(cambioDesde(campo, antes, despues, tieneValor(item, 'antes', 'anterior', 'despues', 'nuevo')));
    }
  }
  if (detalle.cambios && typeof detalle.cambios === 'object' && !Array.isArray(detalle.cambios)) {
    for (const [campo, valor] of Object.entries(detalle.cambios)) {
      const conValores = !SENSIBLE.test(campo) && valor && typeof valor === 'object' && tieneValor(valor, 'antes', 'despues');
      cambios.push(cambioDesde(campo, valor?.antes, valor?.despues, Boolean(conValores)));
    }
  }
  // Valores de contexto que no cambiaron (por ejemplo, el estado del registro al crearlo).
  const contexto = Object.entries(valores)
    .filter(([campo, par]) => !usados.has(campo) && !SENSIBLE.test(campo) && par && typeof par === 'object')
    .map(([campo, par]) => [campo, par.despues ?? par.antes]);
  const otros = [...Object.entries(detalle), ...contexto]
    .filter(([clave]) => clave !== 'campos' && clave !== 'cambios' && clave !== 'valores' && !SENSIBLE.test(clave))
    .map(([clave, valor]) => ({
      etiqueta: etiquetaCampo(clave),
      valor: clave === 'tipo' && Object.hasOwn(TEXTOS_TIPO_CORREO, valor) ? TEXTOS_TIPO_CORREO[valor] : textoValor(valor),
    }))
    .filter((o) => o.valor !== null);
  return { cambios, otros };
}

/** Quién: el nombre, «Sistema» o, si falta el nombre, el tipo con su número. */
export function nombreUsuario(usuario) {
  if (!usuario || usuario.tipo === 'sistema') return 'Sistema';
  if (usuario.nombre) return usuario.nombre;
  const tipo = usuario.tipo === 'cliente' ? 'Cliente' : 'Personal';
  return usuario.id ? `${tipo} #${usuario.id}` : tipo;
}

const propio = (tabla, clave) => (Object.hasOwn(tabla, clave) ? tabla[clave] : null);
export const textoEntidad = (r) => `${r.entidadTexto || propio(ENTIDADES, r.entidad) || r.entidad || 'Registro'}${r.idEntidad ? ` #${r.idEntidad}` : ''}`;
export const textoAccion = (r) => r.accionTexto || propio(ACCIONES, r.accion) || r.accion || '—';

export const FILTROS_AUDITORIA = {
  campos: {
    desde: esFecha,
    hasta: esFecha,
    entidad: (v) => Object.hasOwn(ENTIDADES, v),
    accion: (v) => /^[a-z_]{1,30}$/.test(v),
    idEmpleado: (v) => /^[1-9]\d{0,9}$/.test(v),
    q: (v) => v.length <= 40,
  },
  predeterminados: {},
  tamano: 20,
};

export const FILTROS_CONSENTIMIENTOS = {
  campos: {
    tipo: (v) => /^[a-z_]{1,30}$/.test(v),
    desde: esFecha,
    hasta: esFecha,
  },
  predeterminados: {},
  tamano: 20,
};

/** Lo que se pide a la API; un rango de fechas al revés no viaja. */
export function parametrosDe(definicion) {
  return (filtros) => {
    const base = parametrosBase(filtros, definicion);
    if (base.desde && base.hasta && base.hasta < base.desde) return { ...base, desde: undefined, hasta: undefined };
    return base;
  };
}
