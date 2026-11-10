import { Link } from 'react-router-dom';

/**
 * NotFoundPage (404)
 * ——————————————————
 * Página amigable y accesible para rutas inexistentes.
 */
export default function NotFoundPage() {
  return (
    <div className="status-page">
      <div className="status-card">
        <div className="status-icon" aria-hidden="true">
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
            <circle cx="12" cy="12" r="10" />
            <line x1="12" y1="8" x2="12" y2="12" />
            <line x1="12" y1="16" x2="12.01" y2="16" />
          </svg>
        </div>
        <h1>404 — Página no encontrada</h1>
        <p>
          La ruta que intenta consultar no existe o ha sido movida.
        </p>
        <div className="status-actions">
          <Link to="/" className="btn btn-primary">
            Volver al Inicio
          </Link>
          <Link to="/solicitudes" className="btn btn-secondary">
            Ver Solicitudes
          </Link>
        </div>
      </div>
    </div>
  );
}
