import { useState, useEffect, useCallback, useRef } from 'react';
import solicitudService from '../api/solicitudService';
import clienteService from '../api/clienteService';
import productoService from '../api/productoService';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';
import RoleGuard from '../components/RoleGuard';
import ConfirmModal from '../components/ConfirmModal';
import DetailModal from '../components/DetailModal';
import SolicitudForm from './SolicitudForm';

/**
 * SolicitudesPage
 * ————————————————
 * CRUD completo de solicitudes con búsqueda, filtros por estado,
 * formulario modal para crear/editar y confirmación de eliminación.
 */
export default function SolicitudesPage() {
  const { hasRole } = useAuth();
  const { addToast } = useToast();
  const isAdmin = hasRole(['dueno', 'admin']);

  // Datos
  const [solicitudes, setSolicitudes] = useState([]);
  const [clientes, setClientes] = useState([]);
  const [productos, setProductos] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Búsqueda y filtro
  const [search, setSearch] = useState('');
  const [filterEstado, setFilterEstado] = useState('todos');
  const searchTimeout = useRef(null);
  const [debouncedSearch, setDebouncedSearch] = useState('');

  // Modal formulario
  const [formOpen, setFormOpen] = useState(false);
  const [editingSolicitud, setEditingSolicitud] = useState(null);
  const [formLoading, setFormLoading] = useState(false);

  // Modal confirmar eliminación
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleteLoading, setDeleteLoading] = useState(false);

  // Modal detalle (Consultar)
  const [detailTarget, setDetailTarget] = useState(null);

  // Estado de actualización de selector de estado (Admin)
  const [updatingStatusId, setUpdatingStatusId] = useState(null);

  // Live region para anunciar cambios a screen readers
  const [liveMessage, setLiveMessage] = useState('');

  /* ───── Carga inicial ───── */
  const fetchData = useCallback(async (isRetry = false) => {
    if (isRetry) setLoading(true);
    setError(null);
    try {
      const [sol, cli, prod] = await Promise.all([
        solicitudService.getAll(),
        clienteService.getAll(),
        productoService.getAll(),
      ]);
      setSolicitudes(sol);
      setClientes(cli);
      setProductos(prod);
    } catch (err) {
      setError(err?.message || 'Error al cargar los datos.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData(false);
  }, [fetchData]);

  /* ───── Debounce de búsqueda ───── */
  useEffect(() => {
    clearTimeout(searchTimeout.current);
    searchTimeout.current = setTimeout(() => {
      setDebouncedSearch(search);
    }, 300);
    return () => clearTimeout(searchTimeout.current);
  }, [search]);

  /* ───── Filtrado ───── */
  const filtered = solicitudes.filter((s) => {
    // Filtro por estado
    if (filterEstado !== 'todos' && s.estado !== filterEstado) return false;

    // Búsqueda por teléfono, material o nombre de cliente
    if (debouncedSearch) {
      const q = debouncedSearch.toLowerCase();
      const cliente = clientes.find((c) => c.idCliente === s.idCliente);
      const matchTel = s.telefonoContacto?.toLowerCase().includes(q);
      const matchMat = s.materialSolicitado?.toLowerCase().includes(q);
      const matchCli = cliente?.nombre?.toLowerCase().includes(q);
      const matchNotas = s.notasWhatsapp?.toLowerCase().includes(q);
      if (!matchTel && !matchMat && !matchCli && !matchNotas) return false;
    }

    return true;
  });

  const estados = ['todos', 'pendiente', 'aceptado', 'rechazado'];

  /* ───── Cambiar estado (exclusivo Admin) ───── */
  const handleStatusChange = async (idSolicitud, nuevoEstado) => {
    if (!isAdmin) return;
    setUpdatingStatusId(idSolicitud);
    try {
      await solicitudService.cambiarEstado(idSolicitud, nuevoEstado);
      setSolicitudes((prev) =>
        prev.map((s) =>
          s.idSolicitud === idSolicitud ? { ...s, estado: nuevoEstado } : s
        )
      );
      addToast(`Estado de solicitud #${idSolicitud} actualizado a ${nuevoEstado}.`, 'success');
      setLiveMessage(`Estado de solicitud #${idSolicitud} actualizado a ${nuevoEstado}.`);
    } catch (err) {
      if (err?.status === 403) {
        addToast('No tiene permisos para modificar el estado de la solicitud.', 'error');
      } else {
        addToast(err?.message || 'Error al actualizar el estado de la solicitud.', 'error');
      }
    } finally {
      setUpdatingStatusId(null);
    }
  };

  /* ───── Helpers para nombres ───── */
  const getClienteName = (idCliente) => {
    const c = clientes.find((cl) => cl.idCliente === idCliente);
    return c?.nombre || `Cliente #${idCliente}`;
  };

  const getProductoName = (idProducto) => {
    if (!idProducto) return '—';
    const p = productos.find((pr) => pr.idProducto === idProducto);
    return p?.nombre || `Producto #${idProducto}`;
  };

  /* ───── Crear / Editar ───── */
  const openCreate = () => {
    setEditingSolicitud(null);
    setFormOpen(true);
  };

  const openEdit = (solicitud) => {
    setEditingSolicitud(solicitud);
    setFormOpen(true);
  };

  const handleFormSubmit = async (formData) => {
    setFormLoading(true);
    try {
      if (editingSolicitud) {
        await solicitudService.update(editingSolicitud.idSolicitud, formData);
        addToast('Solicitud actualizada correctamente.', 'success');
        setLiveMessage('Solicitud actualizada.');
      } else {
        await solicitudService.create(formData);
        addToast('Solicitud creada correctamente.', 'success');
        setLiveMessage('Solicitud creada.');
      }
      setFormOpen(false);
      setEditingSolicitud(null);
      await fetchData();
    } catch (err) {
      addToast(err?.message || 'Error al guardar la solicitud.', 'error');
      throw err; // Re-throw para que el formulario muestre errores de campo
    } finally {
      setFormLoading(false);
    }
  };

  /* ───── Eliminar ───── */
  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleteLoading(true);
    try {
      await solicitudService.remove(deleteTarget.idSolicitud);
      addToast('Solicitud eliminada correctamente.', 'success');
      setLiveMessage('Solicitud eliminada.');
      setDeleteTarget(null);
      await fetchData();
    } catch (err) {
      if (err?.status === 403) {
        addToast('No tiene permisos para eliminar esta solicitud.', 'error');
      } else {
        addToast(err?.message || 'Error al eliminar la solicitud.', 'error');
      }
    } finally {
      setDeleteLoading(false);
    }
  };

  /* ───── Formato de fecha ───── */
  const formatDate = (dateStr) => {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleDateString('es-CL', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    });
  };

  const formatDateTime = (dateStr) => {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleString('es-CL', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  /* ═══════════ RENDER ═══════════ */

  if (loading) {
    return (
      <div className="loading-screen" role="status" aria-label="Cargando solicitudes">
        <div className="spinner" />
        <p>Cargando solicitudes…</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="alert alert-error" role="alert">
        <svg className="alert-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
          <circle cx="12" cy="12" r="10" />
          <line x1="12" y1="8" x2="12" y2="12" />
          <line x1="12" y1="16" x2="12.01" y2="16" />
        </svg>
        <div>
          <strong>Error:</strong> {error}
          <br />
          <button type="button" className="btn btn-sm btn-secondary" onClick={fetchData} style={{ marginTop: '0.5rem' }}>
            Reintentar
          </button>
        </div>
      </div>
    );
  }

  return (
    <>
      {/* Screen reader live region */}
      <div aria-live="polite" className="sr-only">{liveMessage}</div>

      <div className="page-header">
        <div>
          <h1>Solicitudes</h1>
          <p>{filtered.length} de {solicitudes.length} solicitudes</p>
        </div>
        <RoleGuard roles={['dueno', 'disenador', 'operario']}>
          <button type="button" className="btn btn-primary" onClick={openCreate}>
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <line x1="12" y1="5" x2="12" y2="19" />
              <line x1="5" y1="12" x2="19" y2="12" />
            </svg>
            Nueva solicitud
          </button>
        </RoleGuard>
      </div>

      {/* Toolbar */}
      <div className="toolbar">
        <div className="search-bar">
          <svg className="search-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input
            type="search"
            className="form-input"
            placeholder="Buscar por cliente, teléfono, material…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Buscar solicitudes"
          />
        </div>
        <div className="toolbar-filters">
          {estados.map((estado) => (
            <button
              key={estado}
              type="button"
              className={`filter-chip ${filterEstado === estado ? 'active' : ''}`}
              onClick={() => setFilterEstado(estado)}
              aria-pressed={filterEstado === estado}
            >
              {estado === 'todos' ? 'Todos' : estado}
            </button>
          ))}
        </div>
      </div>

      {/* Tabla */}
      {filtered.length === 0 ? (
        <div className="empty-state">
          <svg className="empty-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z" />
            <path d="M14 2v6h6" />
          </svg>
          <h3>Sin solicitudes</h3>
          <p>
            {debouncedSearch || filterEstado !== 'todos'
              ? 'No se encontraron solicitudes con los filtros actuales.'
              : 'Aún no hay solicitudes registradas.'}
          </p>
          {!debouncedSearch && filterEstado === 'todos' && (
            <RoleGuard roles={['dueno', 'disenador', 'operario']}>
              <button type="button" className="btn btn-primary" onClick={openCreate}>
                Crear primera solicitud
              </button>
            </RoleGuard>
          )}
        </div>
      ) : (
        <div className="table-container">
          <table className="table">
            <thead>
              <tr>
                <th>ID</th>
                <th>Cliente</th>
                <th>Producto</th>
                <th>Cantidad</th>
                <th>Teléfono</th>
                <th>Fecha</th>
                <th>Estado</th>
                <th style={{ textAlign: 'right' }}>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((s) => (
                <tr key={s.idSolicitud}>
                  <td data-label="ID"><strong>#{s.idSolicitud}</strong></td>
                  <td data-label="Cliente">{getClienteName(s.idCliente)}</td>
                  <td data-label="Producto">{getProductoName(s.idProducto)}</td>
                  <td data-label="Cantidad">{s.cantidad}</td>
                  <td data-label="Teléfono">{s.telefonoContacto}</td>
                  <td data-label="Fecha">{formatDate(s.fechaSolicitud)}</td>
                  <td data-label="Estado">
                    {isAdmin ? (
                      <div className="status-select-container">
                        <select
                          className={`status-select status-select-${s.estado}`}
                          value={s.estado}
                          onChange={(e) => handleStatusChange(s.idSolicitud, e.target.value)}
                          disabled={updatingStatusId === s.idSolicitud}
                          aria-label={`Cambiar estado de solicitud #${s.idSolicitud}`}
                        >
                          <option value="pendiente">Pendiente</option>
                          <option value="aceptado">Aceptado</option>
                          <option value="rechazado">Rechazado</option>
                        </select>
                        {updatingStatusId === s.idSolicitud && (
                          <span className="spinner-border spinner-border-sm" role="status" aria-label="Actualizando estado" />
                        )}
                      </div>
                    ) : (
                      <span className={`badge badge-${s.estado}`}>
                        {s.estado}
                      </span>
                    )}
                  </td>
                  <td data-label="Acciones">
                    <div className="actions-cell">
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm btn-icon"
                        title="Ver detalle"
                        aria-label={`Ver detalle de solicitud #${s.idSolicitud}`}
                        onClick={() => setDetailTarget(s)}
                      >
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                          <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                          <circle cx="12" cy="12" r="3" />
                        </svg>
                      </button>

                      <RoleGuard roles={['dueno', 'disenador', 'operario']}>
                        <button
                          type="button"
                          className="btn btn-ghost btn-sm btn-icon"
                          title="Editar solicitud"
                          aria-label={`Editar solicitud #${s.idSolicitud}`}
                          onClick={() => openEdit(s)}
                        >
                          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                            <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                          </svg>
                        </button>
                      </RoleGuard>

                      {hasRole('dueno') ? (
                        <button
                          type="button"
                          className="btn btn-ghost btn-sm btn-icon"
                          title="Eliminar solicitud"
                          aria-label={`Eliminar solicitud #${s.idSolicitud}`}
                          onClick={() => setDeleteTarget(s)}
                        >
                          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" style={{ color: 'var(--color-danger)' }}>
                            <polyline points="3 6 5 6 21 6" />
                            <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                          </svg>
                        </button>
                      ) : (
                        <button
                          type="button"
                          className="btn btn-ghost btn-sm btn-icon"
                          disabled
                          title="Solo el dueño puede eliminar"
                          aria-label="Eliminar no disponible para su rol"
                        >
                          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <polyline points="3 6 5 6 21 6" />
                            <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                          </svg>
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Modal Formulario */}
      {formOpen && (
        <SolicitudForm
          solicitud={editingSolicitud}
          clientes={clientes}
          productos={productos}
          loading={formLoading}
          onSubmit={handleFormSubmit}
          onCancel={() => {
            setFormOpen(false);
            setEditingSolicitud(null);
          }}
        />
      )}

      {/* Modal Detalle (Consultar) */}
      <DetailModal
        open={!!detailTarget}
        title={`Detalle de Solicitud #${detailTarget?.idSolicitud || ''}`}
        onClose={() => setDetailTarget(null)}
      >
        {detailTarget && (
          <dl className="detail-grid">
            <div className="detail-item">
              <dt>ID Solicitud</dt>
              <dd><strong>#{detailTarget.idSolicitud}</strong></dd>
            </div>
            <div className="detail-item">
              <dt>Cliente</dt>
              <dd>{getClienteName(detailTarget.idCliente)}</dd>
            </div>
            <div className="detail-item">
              <dt>Producto / Servicio</dt>
              <dd>{getProductoName(detailTarget.idProducto)}</dd>
            </div>
            <div className="detail-item">
              <dt>Cantidad</dt>
              <dd>{detailTarget.cantidad}</dd>
            </div>
            <div className="detail-item">
              <dt>Teléfono de Contacto</dt>
              <dd>{detailTarget.telefonoContacto}</dd>
            </div>
            <div className="detail-item">
              <dt>Tamaño</dt>
              <dd>{detailTarget.tamano || '—'}</dd>
            </div>
            <div className="detail-item">
              <dt>Material Solicitado</dt>
              <dd>{detailTarget.materialSolicitado || '—'}</dd>
            </div>
            <div className="detail-item">
              <dt>Estado</dt>
              <dd><span className={`badge badge-${detailTarget.estado}`}>{detailTarget.estado}</span></dd>
            </div>
            <div className="detail-item">
              <dt>Fecha de Solicitud</dt>
              <dd>{formatDateTime(detailTarget.fechaSolicitud)}</dd>
            </div>
            <div className="detail-item">
              <dt>Fecha de Decisión</dt>
              <dd>{formatDateTime(detailTarget.fechaDecision)}</dd>
            </div>
            <div className="detail-item detail-item-full">
              <dt>Notas WhatsApp</dt>
              <dd>{detailTarget.notasWhatsapp || '—'}</dd>
            </div>
          </dl>
        )}
      </DetailModal>

      {/* Modal Confirmar Eliminación */}
      <ConfirmModal
        open={!!deleteTarget}
        title="Eliminar solicitud"
        message={`¿Está seguro de que desea eliminar la solicitud #${deleteTarget?.idSolicitud}? Esta acción no se puede deshacer.`}
        confirmLabel="Eliminar"
        variant="danger"
        loading={deleteLoading}
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </>
  );
}
