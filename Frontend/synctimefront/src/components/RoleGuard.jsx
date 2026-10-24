import PropTypes from 'prop-types';
import { useAuth } from '../context/AuthContext';

/**
 * RoleGuard
 * —————————
 * Renderiza sus hijos solo si el usuario tiene uno de los roles permitidos.
 * Si no tiene permiso, muestra un fallback o nada.
 */
export default function RoleGuard({ roles, children, fallback = null }) {
  const { hasRole } = useAuth();

  if (!hasRole(roles)) {
    return fallback;
  }

  return children;
}

RoleGuard.propTypes = {
  roles: PropTypes.oneOfType([
    PropTypes.string,
    PropTypes.arrayOf(PropTypes.string),
  ]).isRequired,
  children: PropTypes.node.isRequired,
  fallback: PropTypes.node,
};
