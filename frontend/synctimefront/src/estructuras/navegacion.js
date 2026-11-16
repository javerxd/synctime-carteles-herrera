import {
  BarChart3, CalendarDays, ClipboardList, FileSpreadsheet, FileText, House, MailWarning, Package, ScanFace, ScrollText, Settings2, Store, UserRound,
  Users,
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
