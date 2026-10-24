import { useState, useEffect, useCallback } from 'react';
import clienteService from '../api/clienteService';

/**
 * ClientesPage
 * ————————————
 * Listado del recurso relacionado: Clientes de SyncTime / Letreros Herrera.
 * Permite buscar clientes por nombre o RUT y ver su información de contacto.
 * Cumple con WCAG 2.2 AA y diseño responsivo adaptado a móvil.
 */
export default function ClientesPage() {
  const [clientes, setClientes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState('');

  const fetchClientes = useCallback(async (isRetry = false) => {
    if (isRetry) setLoading(true);
    setError(null);
    try {
      const data = await clienteService.getAll();
      setClientes(data || []);
    } catch (err) {
      setError(err?.message || 'Error al cargar los clientes.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchClientes(false);
  }, [fetchClientes]);

  const filteredClientes = clientes.filter((c) => {
    if (!search) return true;
    const q = search.toLowerCase();
    const matchName = c.nombre?.toLowerCase().includes(q);
    const matchRut = c.rut?.toLowerCase().includes(q);
    const matchEmail = c.email?.toLowerCase().includes(q);
    const matchTel = c.telefono?.toLowerCase().includes(q);
    return matchName || matchRut || matchEmail || matchTel;
  });

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Clientes</h1>
          <p>
            {filteredClientes.length} de {clientes.length} clientes registrados
          </p>
        </div>
      </div>

      <div className="toolbar">
        <div className="search-bar">
          <svg
            className="search-icon"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input
            type="search"
            className="form-input"
            placeholder="Buscar por nombre, RUT, teléfono o email…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Buscar clientes"
          />
        </div>
      </div>

      {loading && (
        <div className="loading-state" role="status" aria-live="polite">
          <div className="spinner" aria-hidden="true" />
          <p>Cargando clientes…</p>
        </div>
      )}

      {error && !loading && (
        <div className="alert alert-danger" role="alert">
          <div>
            <strong>Error:</strong> {error}
          </div>
          <button
            type="button"
            className="btn btn-secondary btn-sm"
            onClick={fetchClientes}
            style={{ marginTop: 'var(--space-2)' }}
          >
            Reintentar
          </button>
        </div>
      )}

      {!loading && !error && filteredClientes.length === 0 && (
        <div className="empty-state">
          <svg
            className="empty-icon"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.5"
            aria-hidden="true"
          >
            <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
            <circle cx="9" cy="7" r="4" />
            <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
            <path d="M16 3.13a4 4 0 0 1 0 7.75" />
          </svg>
          <h3>Sin clientes</h3>
          <p>
            {search
              ? 'No se encontraron clientes que coincidan con la búsqueda.'
              : 'No hay clientes registrados en el sistema.'}
          </p>
        </div>
      )}

      {!loading && !error && filteredClientes.length > 0 && (
        <div className="table-container">
          <table className="table">
            <thead>
              <tr>
                <th>ID</th>
                <th>Nombre</th>
                <th>RUT</th>
                <th>Teléfono</th>
                <th>Email</th>
              </tr>
            </thead>
            <tbody>
              {filteredClientes.map((c) => (
                <tr key={c.idCliente}>
                  <td>
                    <strong>#{c.idCliente}</strong>
                  </td>
                  <td>{c.nombre}</td>
                  <td>{c.rut || '—'}</td>
                  <td>{c.telefono || '—'}</td>
                  <td>{c.email || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
