import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Pagina403 } from '../paginas/errores/PaginasDeError';
import { useSesion } from './sesion';

export const RUTA_INGRESO_PERSONAL = '/personal/ingresar';

/**
 * Protege las rutas del panel del taller. Sin sesión del personal, lleva al ingreso y recuerda
 * adónde quería ir; con sesión pero sin el rol, muestra la página 403 en vez de esconder el motivo.
 * Los roles reales son dueño, administrador, diseñador y operario (docs/api/permisos.md).
 */
export function RequierePersonal({ roles, children }) {
  const { usuario, esPersonal, tieneRol, motivoCierre } = useSesion();
  const ubicacion = useLocation();

  if (!usuario || !esPersonal) {
    return <Navigate to={RUTA_INGRESO_PERSONAL} replace state={{ desde: ubicacion, motivo: motivoCierre }} />;
  }
  if (roles && !tieneRol(roles)) return <Pagina403 />;
  return children ?? <Outlet />;
}
