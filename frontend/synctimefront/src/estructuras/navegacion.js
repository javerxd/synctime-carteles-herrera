import {
  BarChart3, CalendarDays, ClipboardList, FilePlus2, FileSpreadsheet, FileText, House, MailWarning, Package, ScanFace, ScrollText, Settings2, Store,
  UserCog, UserRound, Users,
} from 'lucide-react';

export const GESTION = ['dueño', 'administrador'];
export const PERSONAL = ['dueño', 'administrador', 'diseñador', 'operario'];

/**
 * Menú del panel, filtrado por rol (docs/api/permisos.md). `fase` marca las secciones que aún no
 * tienen su lógica: la pantalla lo dice con un estado vacío, sin fingir datos.
 */
export const MENU_PANEL = [
  {
    grupo: 'Taller',
    entradas: [
      { ruta: '/panel', etiqueta: 'Inicio', icono: House, roles: PERSONAL, exacta: true },
      { ruta: '/panel/solicitudes', etiqueta: 'Solicitudes', icono: ClipboardList, roles: PERSONAL },
      { ruta: '/panel/cotizaciones', etiqueta: 'Cotizaciones', icono: FileText, roles: PERSONAL },
      { ruta: '/panel/pedidos', etiqueta: 'Pedidos', icono: Package, roles: PERSONAL },
      { ruta: '/panel/calendario', etiqueta: 'Calendario', icono: CalendarDays, roles: PERSONAL },
      { ruta: '/panel/clientes', etiqueta: 'Clientes', icono: Users, roles: PERSONAL },
      { ruta: '/panel/local', etiqueta: 'Estado del local', icono: Store, roles: PERSONAL },
    ],
  },
  {
    grupo: 'Gestión',
    entradas: [
      { ruta: '/panel/indicadores', etiqueta: 'Indicadores', icono: BarChart3, roles: GESTION },
      { ruta: '/panel/reportes', etiqueta: 'Reportes', icono: FileSpreadsheet, roles: GESTION },
      { ruta: '/panel/mantenedores', etiqueta: 'Catálogo y mantenedores', icono: Settings2, roles: GESTION },
      // Fase 12D: administrar al personal (alta, datos, rol y desactivar). Solo dueño y administrador.
      { ruta: '/panel/empleados', etiqueta: 'Empleados', icono: UserCog, roles: GESTION },
      { ruta: '/panel/rostros', etiqueta: 'Rostros registrados', icono: ScanFace, roles: GESTION },
      // Fase 11: solo dueño y administrador (la API también lo exige).
      { ruta: '/panel/correos-no-enviados', etiqueta: 'Correos no enviados', icono: MailWarning, roles: GESTION },
      { ruta: '/panel/auditoria', etiqueta: 'Auditoría', icono: ScrollText, roles: GESTION },
    ],
  },
  {
    grupo: 'Cuenta',
    entradas: [{ ruta: '/panel/perfil', etiqueta: 'Mi perfil', icono: UserRound, roles: PERSONAL }],
  },
];

/**
 * Menú de la cuenta del cliente (fase 12D): lo que antes eran accesos dentro de «Mi cuenta» ahora vive en el
 * menú lateral. Registro, recuperación y verificación no lo llevan (todavía no hay sesión).
 */
export const MENU_CUENTA = [
  {
    grupo: 'Mi cuenta',
    entradas: [
      { ruta: '/cuenta/solicitudes', etiqueta: 'Mis solicitudes', icono: ClipboardList },
      { ruta: '/cuenta/solicitud', etiqueta: 'Solicitar cotización', icono: FilePlus2, exacta: true },
      { ruta: '/cuenta', etiqueta: 'Mi cuenta', icono: UserRound, exacta: true },
    ],
  },
];

/** El menú que ve un usuario: solo las entradas de su rol y sin grupos vacíos. */
export function menuPara(tieneRol) {
  return MENU_PANEL
    .map((grupo) => ({ ...grupo, entradas: grupo.entradas.filter((e) => tieneRol(e.roles)) }))
    .filter((grupo) => grupo.entradas.length > 0);
}

/**
 * Adónde lleva «Solicitar cotización» (fase 7): al formulario del cliente, /cuenta/solicitud, que exige
 * sesión con correo confirmado (sin sesión, la guarda pasa por /ingresar y vuelve). El personal registra
 * la solicitud presencial desde el panel. Con un trabajo de la vitrina («Cotizar uno similar»), el tipo de
 * letrero llega elegido y el trabajo queda como referencia.
 */
export function destinoSolicitud({ esPersonal = false, trabajo = null } = {}) {
  if (esPersonal) return '/panel/solicitudes/nueva';
  if (!trabajo) return '/cuenta/solicitud';
  const parametros = new URLSearchParams();
  if (trabajo.idProducto) parametros.set('producto', String(trabajo.idProducto));
  if (trabajo.titulo) parametros.set('referencia', trabajo.titulo);
  const texto = parametros.toString();
  return `/cuenta/solicitud${texto ? `?${texto}` : ''}`;
}
