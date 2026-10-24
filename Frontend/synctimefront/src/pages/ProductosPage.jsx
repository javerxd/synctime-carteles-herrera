import { useState, useEffect, useCallback } from 'react';
import productoService from '../api/productoService';

/**
 * ProductosPage
 * —————————————
 * Catálogo del recurso relacionado: Productos y Servicios de Letreros Herrera.
 * Muestra el catálogo con filtrado por categoría/nombre y precios base.
 * Accesible bajo WCAG 2.2 AA con vista responsiva.
 */
export default function ProductosPage() {
  const [productos, setProductos] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState('');

  const fetchProductos = useCallback(async (isRetry = false) => {
    if (isRetry) setLoading(true);
    setError(null);
    try {
      const data = await productoService.getAll();
      setProductos(data || []);
    } catch (err) {
      setError(err?.message || 'Error al cargar los productos.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchProductos(false);
  }, [fetchProductos]);

  const filtered = productos.filter((p) => {
    if (!search) return true;
    const q = search.toLowerCase();
    const matchName = p.nombre?.toLowerCase().includes(q);
    const matchDesc = p.descripcion?.toLowerCase().includes(q);
    const matchCat = p.categoria?.toLowerCase().includes(q);
    return matchName || matchDesc || matchCat;
  });

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Productos y Servicios</h1>
          <p>
            {filtered.length} de {productos.length} productos disponibles
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
            placeholder="Buscar por nombre, categoría o descripción…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Buscar productos"
          />
        </div>
      </div>

      {loading && (
        <div className="loading-state" role="status" aria-live="polite">
          <div className="spinner" aria-hidden="true" />
          <p>Cargando productos…</p>
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
            onClick={fetchProductos}
            style={{ marginTop: 'var(--space-2)' }}
          >
            Reintentar
          </button>
        </div>
      )}

      {!loading && !error && filtered.length === 0 && (
        <div className="empty-state">
          <svg
            className="empty-icon"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.5"
            aria-hidden="true"
          >
            <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
            <polyline points="3.27 6.96 12 12.01 20.73 6.96" />
            <line x1="12" y1="22.08" x2="12" y2="12" />
          </svg>
          <h3>Sin productos</h3>
          <p>
            {search
              ? 'No se encontraron productos que coincidan con la búsqueda.'
              : 'No hay productos disponibles en el catálogo.'}
          </p>
        </div>
      )}

      {!loading && !error && filtered.length > 0 && (
        <div className="table-container">
          <table className="table">
            <thead>
              <tr>
                <th>ID</th>
                <th>Nombre</th>
                <th>Categoría</th>
                <th>Precio Base</th>
                <th>Descripción</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((p) => (
                <tr key={p.idProducto}>
                  <td>
                    <strong>#{p.idProducto}</strong>
                  </td>
                  <td>{p.nombre}</td>
                  <td>
                    <span className="badge badge-primary">{p.categoria || 'General'}</span>
                  </td>
                  <td>
                    {p.precioBase != null
                      ? `$${Number(p.precioBase).toLocaleString('es-CL')}`
                      : 'A cotizar'}
                  </td>
                  <td>{p.descripcion || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
