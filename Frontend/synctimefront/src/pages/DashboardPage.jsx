import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import solicitudService from '../api/solicitudService';
import clienteService from '../api/clienteService';
import productoService from '../api/productoService';

/**
 * DashboardPage
 * ——————————————
 * Vista resumen con estadísticas rápidas del sistema.
 */
export default function DashboardPage() {
  const { user } = useAuth();
  const [stats, setStats] = useState({
    solicitudes: 0,
    pendientes: 0,
    clientes: 0,
    productos: 0,
  });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function fetchStats() {
      try {
        const [solicitudes, clientes, productos] = await Promise.all([
          solicitudService.getAll(),
          clienteService.getAll(),
          productoService.getAll(),
        ]);

        if (!cancelled) {
          const pendientes = solicitudes.filter(
            (s) => s.estado === 'pendiente'
          ).length;

          setStats({
            solicitudes: solicitudes.length,
            pendientes,
            clientes: clientes.length,
            productos: productos.length,
          });
        }
      } catch {
        // Error silencioso en dashboard: datos de resumen no son críticos
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    fetchStats();
    return () => { cancelled = true; };
  }, []);

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Dashboard</h1>
          <p>Bienvenido/a, {user?.nombre || 'Usuario'}</p>
        </div>
      </div>

      {loading ? (
        <div className="loading-screen" role="status" aria-label="Cargando estadísticas">
          <div className="spinner" />
          <p>Cargando estadísticas…</p>
        </div>
      ) : (
        <>
          <div className="stats-grid">
            <div className="stat-card">
              <div className="stat-label">Solicitudes</div>
              <div className="stat-value">{stats.solicitudes}</div>
              <div className="stat-hint">Total registradas</div>
            </div>
            <div className="stat-card">
              <div className="stat-label">Pendientes</div>
              <div className="stat-value">{stats.pendientes}</div>
              <div className="stat-hint">Requieren atención</div>
            </div>
            <div className="stat-card">
              <div className="stat-label">Clientes</div>
              <div className="stat-value">{stats.clientes}</div>
              <div className="stat-hint">En el sistema</div>
            </div>
            <div className="stat-card">
              <div className="stat-label">Productos</div>
              <div className="stat-value">{stats.productos}</div>
              <div className="stat-hint">Catálogo disponible</div>
            </div>
          </div>

          <div className="card">
            <div className="card-header">
              <h2>Acciones rápidas</h2>
            </div>
            <div className="card-body" style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap' }}>
              <Link to="/solicitudes" className="btn btn-primary">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z" />
                  <path d="M14 2v6h6" />
                </svg>
                Ver solicitudes
              </Link>
              <Link to="/clientes" className="btn btn-secondary">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                  <circle cx="9" cy="7" r="4" />
                </svg>
                Gestionar clientes
              </Link>
              <Link to="/productos" className="btn btn-secondary">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
                </svg>
                Catálogo de productos
              </Link>
            </div>
          </div>
        </>
      )}
    </>
  );
}
