import {
  BarChart3, CalendarDays, ClipboardList, FileSpreadsheet, FileText, House, Package, Settings2, Store, UserRound, Users,
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
      { ruta: '/panel/solicitudes', etiqueta: 'Solicitudes', icono: ClipboardList, roles: PERSONAL, fase: 7 },
      { ruta: '/panel/cotizaciones', etiqueta: 'Cotizaciones', icono: FileText, roles: PERSONAL, fase: 8 },
      { ruta: '/panel/pedidos', etiqueta: 'Pedidos', icono: Package, roles: PERSONAL },
      { ruta: '/panel/calendario', etiqueta: 'Calendario de entregas', icono: CalendarDays, roles: PERSONAL, fase: 8 },
      { ruta: '/panel/clientes', etiqueta: 'Clientes', icono: Users, roles: PERSONAL, fase: 7 },
      { ruta: '/panel/local', etiqueta: 'Estado del local', icono: Store, roles: PERSONAL },
    ],
  },
  {
    grupo: 'Gestión',
    entradas: [
      { ruta: '/panel/indicadores', etiqueta: 'Indicadores', icono: BarChart3, roles: GESTION },
      { ruta: '/panel/reportes', etiqueta: 'Reportes', icono: FileSpreadsheet, roles: GESTION },
      { ruta: '/panel/mantenedores', etiqueta: 'Catálogo y mantenedores', icono: Settings2, roles: GESTION, fase: 9 },
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
