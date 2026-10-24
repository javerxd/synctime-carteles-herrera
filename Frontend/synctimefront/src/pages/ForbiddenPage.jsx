import { Link } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

/**
 * ForbiddenPage (403 Acceso Denegado)
 * ——————————————————————————————————
 * Cumple con el requisito de la pauta sobre autorización por roles:
 * "manejo de 403 y página de acceso denegado".
 */
export default function ForbiddenPage() {
  const { user } = useAuth();

  return (
    <div className="status-page">
      <div className="status-card">
        <div className="status-icon status-icon-warning" aria-hidden="true">
          <svg
            width="48"
            height="48"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
            <path d="M7 11V7a5 5 0 0 1 10 0v4" />
          </svg>
        </div>
        <h1>403 — Acceso Denegado</h1>
        <p>
          No tiene los privilegios necesarios para acceder a esta sección o recurso.
        </p>
        {user && (
          <p className="status-hint">
            Sesión actual: <strong>{user.nombre || user.email}</strong> | Rol asignado:{' '}
            <span className="badge badge-warning">{user.rol}</span>
          </p>
        )}
        <div className="status-actions">
          <Link to="/" className="btn btn-primary">
            Volver al Inicio
          </Link>
          <Link to="/solicitudes" className="btn btn-secondary">
            Ir a Solicitudes
          </Link>
        </div>
      </div>
    </div>
  );
}
