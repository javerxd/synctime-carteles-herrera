import { Navigate, useLocation } from 'react-router-dom';
import PropTypes from 'prop-types';
import { useAuth } from '../context/AuthContext';

/**
 * ProtectedRoute
 * ——————————————
 * Envuelve rutas que requieren autenticación.
 * Si no hay token, redirige a /login conservando la ruta original.
 */
export default function ProtectedRoute({ children }) {
  const { isAuthenticated, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return (
      <div className="loading-screen" role="status" aria-label="Cargando sesión">
        <div className="spinner" />
        <p>Cargando sesión…</p>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return children;
}

ProtectedRoute.propTypes = {
  children: PropTypes.node.isRequired,
};
