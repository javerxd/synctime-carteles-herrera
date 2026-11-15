// Reglas de los mantenedores (fase 9, E12–E15): filtros del listado en la dirección, validación de cada
// formulario con los mismos límites que la API y el paso entre el formulario y la API. Sin React.
import { errorPassword, esTelefono } from '../cuenta/reglasCuenta';
import { pesosDesdeTexto } from '../cotizaciones/reglasCotizacion';
import { cmAMm, numero } from '../solicitudes/reglasSolicitud';
import { parametrosBase } from '../../utilidades/filtrosUrl';
import { validarRut } from '../../utilidades/rut';

// ── Listados ──

export const TEXTOS_ACTIVO = { true: 'Activos', false: 'Inactivos', todos: 'Todos' };
export const TEXTOS_VISIBLE = { '': 'Todas', true: 'Visibles', false: 'Ocultas' };

/** Filtros de un mantenedor: búsqueda y activo (por defecto, los activos). */
export const FILTROS_MANTENEDOR = {
  campos: {
    q: (v) => v.length <= 100,
    activo: (v) => v in TEXTOS_ACTIVO,
  },
  predeterminados: { activo: 'true' },
  tamano: 20,
};

/** La vitrina no tiene «activo»: se filtra por visible u oculta (por defecto, todas). */
export const FILTROS_VITRINA = {
  campos: {
    q: (v) => v.length <= 100,
    visible: (v) => v === 'true' || v === 'false',
  },
  tamano: 12,
};

/** Los parámetros para la API: «todos» no se envía (la API entrega activos e inactivos). */
export function parametrosMantenedor(filtros, definicion = FILTROS_MANTENEDOR) {
  const base = parametrosBase(filtros, definicion);
  if (base.activo === 'todos') base.activo = undefined;
  return base;
}

// ── Utilidades del formulario ──

const texto = (v) => String(v ?? '').trim();
const opcional = (v) => (texto(v) === '' ? null : texto(v));
const entero = (v) => (v === null || v === undefined || v === '' ? '' : String(v));

function largo(valor, { min = 0, max, vacio, etiqueta }) {
  const v = texto(valor);
  if (!v) return min > 0 ? vacio : null;
  if (v.length < min) return `${etiqueta} debe tener al menos ${min} caracteres.`;
  if (v.length > max) return `${etiqueta} puede tener hasta ${max} caracteres.`;
  return null;
}

/** Pesos sin decimales: vacío permitido si es opcional. */
function errorPesos(valor, { requerido = false, etiqueta }) {
  const monto = pesosDesdeTexto(valor);
  if (monto === null) return requerido ? `Escribe ${etiqueta}.` : null;
  if (!Number.isInteger(monto) || monto < 0) return 'Escribe un monto en pesos, sin decimales; por ejemplo 12.990.';
  if (monto > 999_999_999) return 'El monto es demasiado grande.';
  return null;
}

const montoParaApi = (valor) => pesosDesdeTexto(valor);
const limpiar = (errores) => Object.fromEntries(Object.entries(errores).filter(([, m]) => m));

// ── Categorías ──

export const LIMITES = {
  nombreCategoria: 80, descripcionCategoria: 300, nombreProducto: 100, descripcionProducto: 500,
  nombreMaterial: 100, unidad: 20, titulo: 100, descripcionPublicacion: 1000, nombrePersona: 80, correo: 254, medidaCm: 5000,
};

export const CATEGORIA_VACIA = { nombre: '', descripcion: '' };
export const categoriaDesdeApi = (c) => ({ nombre: c?.nombre ?? '', descripcion: c?.descripcion ?? '' });
export const categoriaParaApi = (f) => ({ nombre: texto(f.nombre), descripcion: opcional(f.descripcion) });
export function validarCategoria(f) {
  return limpiar({
    nombre: largo(f.nombre, { min: 2, max: LIMITES.nombreCategoria, vacio: 'Escribe el nombre de la categoría.', etiqueta: 'El nombre' }),
    descripcion: largo(f.descripcion, { max: LIMITES.descripcionCategoria, etiqueta: 'La descripción' }),
  });
}

// ── Tipos de letrero (productos) ──

export const PRODUCTO_VACIO = { idCategoria: '', nombre: '', descripcion: '', precioBaseRef: '' };
export const productoDesdeApi = (p) => ({
  idCategoria: entero(p?.idCategoria ?? p?.categoria?.idCategoria), nombre: p?.nombre ?? '', descripcion: p?.descripcion ?? '',
  precioBaseRef: entero(p?.precioBaseRef),
});
export const productoParaApi = (f) => ({
  idCategoria: Number(f.idCategoria), nombre: texto(f.nombre), descripcion: opcional(f.descripcion), precioBaseRef: montoParaApi(f.precioBaseRef),
});
export function validarProducto(f) {
  return limpiar({
    idCategoria: f.idCategoria ? null : 'Elige la categoría.',
    nombre: largo(f.nombre, { min: 2, max: LIMITES.nombreProducto, vacio: 'Escribe el nombre del tipo de letrero.', etiqueta: 'El nombre' }),
    descripcion: largo(f.descripcion, { max: LIMITES.descripcionProducto, etiqueta: 'La descripción' }),
    precioBaseRef: errorPesos(f.precioBaseRef, { etiqueta: 'el precio base' }),
  });
}

// ── Materiales ──

export const MATERIAL_VACIO = { nombre: '', unidadMedida: '', costoUnitarioAprox: '' };
export const materialDesdeApi = (m) => ({ nombre: m?.nombre ?? '', unidadMedida: m?.unidadMedida ?? '', costoUnitarioAprox: entero(m?.costoUnitarioAprox) });
export const materialParaApi = (f) => ({ nombre: texto(f.nombre), unidadMedida: texto(f.unidadMedida), costoUnitarioAprox: montoParaApi(f.costoUnitarioAprox) });
export function validarMaterial(f) {
  return limpiar({
    nombre: largo(f.nombre, { min: 2, max: LIMITES.nombreMaterial, vacio: 'Escribe el nombre del material.', etiqueta: 'El nombre' }),
    unidadMedida: largo(f.unidadMedida, { min: 1, max: LIMITES.unidad, vacio: 'Escribe la unidad de medida, por ejemplo m2.', etiqueta: 'La unidad' }),
    costoUnitarioAprox: errorPesos(f.costoUnitarioAprox, { requerido: true, etiqueta: 'el costo unitario aproximado' }),
  });
}

// ── Vitrina (publicaciones) ──

export const MENSAJE_LOGO = 'Lleva el logo de un cliente: para mostrarla, primero registra su autorización.';

/** ¿Se puede mostrar? Una publicación con el logo de un cliente necesita su autorización registrada (RNFO17). */
export const puedeSerVisible = ({ incluyeLogoCliente, logoAutorizado }) => !incluyeLogoCliente || Boolean(logoAutorizado);

/**
 * ¿Sigue valiendo la autorización registrada? Vale para la pieza que el cliente vio: si cambia la imagen o se
 * marca de nuevo el logo, la API la borra y hay que registrarla otra vez.
 */
export function logoVigente(form, registro) {
  if (!registro?.logoAutorizado || !form.incluyeLogoCliente || !registro.incluyeLogoCliente) return false;
  const antes = registro.idImagen ?? registro.imagen?.idImagen ?? registro.imagen?.urlGrande ?? null;
  const ahora = form.imagen?.idImagen ?? form.imagen?.urlGrande ?? null;
  return antes === ahora;
}

const mmACm = (mm) => (mm ? String(Math.round(mm) / 10).replace('.', ',') : '');

export const PUBLICACION_VACIA = {
  idProducto: '', titulo: '', descripcion: '', imagen: null, ancho: '', alto: '', precioReferencial: '', incluyeLogoCliente: false, visible: false,
};
export const publicacionDesdeApi = (p) => ({
  idProducto: entero(p?.idProducto),
  titulo: p?.titulo ?? '',
  descripcion: p?.descripcion ?? '',
  imagen: p?.imagen ? { idImagen: p.idImagen ?? p.imagen.idImagen ?? null, urlMiniatura: p.imagen.urlMiniatura, urlGrande: p.imagen.urlGrande } : null,
  ancho: mmACm(p?.anchoMm),
  alto: mmACm(p?.altoMm),
  precioReferencial: entero(p?.precioReferencial),
  incluyeLogoCliente: Boolean(p?.incluyeLogoCliente),
  visible: Boolean(p?.visible),
});
export function publicacionParaApi(f) {
  const ancho = numero(f.ancho);
  const alto = numero(f.alto);
  return {
    idProducto: Number(f.idProducto),
    titulo: texto(f.titulo),
    descripcion: texto(f.descripcion),
    idImagen: f.imagen?.idImagen ?? null,
    anchoMm: ancho ? cmAMm(ancho) : null,
    altoMm: alto ? cmAMm(alto) : null,
    precioReferencial: montoParaApi(f.precioReferencial),
    incluyeLogoCliente: Boolean(f.incluyeLogoCliente),
    visible: Boolean(f.visible),
  };
}
function errorMedida(valor, etiqueta) {
  const n = numero(valor);
  if (n === null) return null;
  if (!Number.isFinite(n) || n <= 0) return `Escribe ${etiqueta} en centímetros, por ejemplo 120 o 60,5.`;
  if (n > LIMITES.medidaCm) return `${etiqueta.charAt(0).toUpperCase()}${etiqueta.slice(1)} puede ser hasta ${LIMITES.medidaCm} cm.`;
  return null;
}
export function validarPublicacion(f, { logoAutorizado = false } = {}) {
  const errores = {
    idProducto: f.idProducto ? null : 'Elige el tipo de letrero.',
    titulo: largo(f.titulo, { min: 2, max: LIMITES.titulo, vacio: 'Escribe el título del trabajo.', etiqueta: 'El título' }),
    descripcion: largo(f.descripcion, { max: LIMITES.descripcionPublicacion, etiqueta: 'La descripción' }),
    ancho: errorMedida(f.ancho, 'el ancho'),
    alto: errorMedida(f.alto, 'el alto'),
    precioReferencial: errorPesos(f.precioReferencial, { etiqueta: 'el precio' }),
    visible: f.visible && !puedeSerVisible({ incluyeLogoCliente: f.incluyeLogoCliente, logoAutorizado }) ? MENSAJE_LOGO : null,
  };
  if (!errores.ancho && !errores.alto && (numero(f.ancho) === null) !== (numero(f.alto) === null)) {
    errores[numero(f.ancho) === null ? 'ancho' : 'alto'] = 'Escribe el ancho y el alto, o deja los dos en blanco.';
  }
  return limpiar(errores);
}

// ── Personal ──

export const ROLES = {
  dueño: 'Dueño',
  administrador: 'Administrador',
  diseñador: 'Diseñador',
  operario: 'Operario',
};

const CORREO = /^[^\s@;,"]+@[^\s@;,"]+\.[^\s@;,"]+$/;

export const EMPLEADO_VACIO = {
  rut: '', nombre: '', apellido: '', telefono: '', email: '', rol: '', password: '', passwordActual: '',
};
export const empleadoDesdeApi = (e) => ({
  rut: e?.rut ?? '', nombre: e?.nombre ?? '', apellido: e?.apellido ?? '', telefono: e?.telefono ?? '', email: e?.email ?? '',
  rol: e?.rol ?? '', password: '', passwordActual: '',
});
/**
 * Cambiar el correo o el rol de otra persona pide la contraseña de quien lo cambia (la API cierra además las
 * sesiones de esa persona). Los demás datos se editan sin ella.
 */
export function requierePasswordEmpleado(f, registro) {
  if (!registro) return false;
  const correo = texto(f.email).toLowerCase() !== String(registro.email ?? '').trim().toLowerCase();
  return correo || f.rol !== (registro.rol ?? '');
}

export function empleadoParaApi(f, { nuevo = false, requierePassword = false } = {}) {
  const datos = {
    rut: texto(f.rut), nombre: texto(f.nombre), apellido: texto(f.apellido), telefono: texto(f.telefono).replace(/\s/g, ''),
    email: texto(f.email).toLowerCase(), rol: f.rol,
  };
  if (nuevo) return { ...datos, password: f.password, passwordActual: f.passwordActual };
  return requierePassword ? { ...datos, passwordActual: f.passwordActual } : datos;
}
export function validarEmpleado(f, { nuevo = false, requierePassword = false } = {}) {
  const correo = texto(f.email);
  const errores = {
    rut: !texto(f.rut) ? 'Escribe el RUT.' : validarRut(f.rut) ? null : 'El RUT no es válido. Revisa el número y el dígito verificador, por ejemplo 12.345.678-5.',
    nombre: largo(f.nombre, { min: 1, max: LIMITES.nombrePersona, vacio: 'Escribe el nombre.', etiqueta: 'El nombre' }),
    apellido: largo(f.apellido, { min: 1, max: LIMITES.nombrePersona, vacio: 'Escribe el apellido.', etiqueta: 'El apellido' }),
    telefono: esTelefono(f.telefono) ? null : 'Escribe un teléfono de 9 dígitos, por ejemplo 9 1234 5678.',
    email: !correo ? 'Escribe el correo con que ingresará.' : CORREO.test(correo) && correo.length <= LIMITES.correo ? null : 'Escribe un correo válido, por ejemplo nombre@correo.cl.',
    rol: f.rol in ROLES ? null : 'Elige el rol.',
  };
  if (nuevo) {
    errores.password = errorPassword(f.password, { vacia: 'Escribe la contraseña inicial.' });
    errores.passwordActual = f.passwordActual ? null : 'Escribe tu contraseña para confirmar el alta.';
  } else if (requierePassword) {
    errores.passwordActual = f.passwordActual ? null : 'Escribe tu contraseña para confirmar el cambio de correo o de rol.';
  }
  return limpiar(errores);
}

// ── Errores de la API ──

/** { Nombre: ['…'], 'Datos.Rut': ['…'] } → { nombre: '…', rut: '…' }. */
export function erroresDeApi(errores) {
  const resultado = {};
  for (const [clave, mensajes] of Object.entries(errores ?? {})) {
    const campo = clave.split('.').at(-1);
    const nombre = campo.charAt(0).toLowerCase() + campo.slice(1);
    const destino = { anchoMm: 'ancho', altoMm: 'alto', idImagen: 'imagen' }[nombre] ?? nombre;
    resultado[destino] = Array.isArray(mensajes) ? mensajes[0] : String(mensajes);
  }
  return resultado;
}

/** Un 409 que no es de versión: qué decir (la API manda el texto; estos son los de respaldo). */
export function textoConflicto(falla) {
  const respaldo = {
    propio: 'No puedes desactivarte ni cambiarte el rol a ti mismo. Pídeselo a otra persona de gestión.',
    ultimo_dueno: 'El taller debe tener al menos un dueño activo. Asigna otro dueño antes de hacer este cambio.',
    logo_sin_autorizacion: MENSAJE_LOGO,
    ya_existe: 'Ya existe un registro con ese nombre.',
  };
  if (falla?.codigo in respaldo && (!falla.message || /^Otra persona cambió/.test(falla.message))) return respaldo[falla.codigo];
  return falla?.message || respaldo[falla?.codigo] || 'No se pudo guardar.';
}

/** El campo al que corresponde un 409 de nombre repetido, para mostrarlo junto a él. */
export function campoDeConflicto(falla, tipo) {
  if (falla?.estado !== 409) return null;
  if (falla.codigo === 'logo_sin_autorizacion') return 'visible';
  if (falla.codigo === 'ya_existe' || /ya existe|repetid|en uso/i.test(falla.message ?? '')) {
    if (tipo === 'personal') return /rut/i.test(falla.message ?? '') ? 'rut' : 'email';
    return tipo === 'vitrina' ? 'titulo' : 'nombre';
  }
  return null;
}
