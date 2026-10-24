import { useState, useEffect, useRef } from 'react';
import PropTypes from 'prop-types';

/**
 * SolicitudForm
 * —————————————
 * Formulario modal accesible para crear o editar una solicitud.
 * Cumple con WCAG 2.2 AA:
 * - role="dialog", aria-modal="true", aria-labelledby
 * - Trampa de foco (Tab / Shift+Tab) y cierre con Escape
 * - aria-describedby para mensajes de error de campo
 * - aria-invalid en campos con error
 * - Consumo del recurso relacionado: selectores de Clientes y Productos
 */
export default function SolicitudForm({
  solicitud,
  clientes,
  productos,
  loading,
  onSubmit,
  onCancel,
}) {
  const isEditing = Boolean(solicitud);
  const modalRef = useRef(null);
  const firstInputRef = useRef(null);

  const [formData, setFormData] = useState({
    idCliente: solicitud?.idCliente || '',
    idProducto: solicitud?.idProducto || '',
    cantidad: solicitud?.cantidad ?? 1,
    telefonoContacto: solicitud?.telefonoContacto || '',
    materialSolicitado: solicitud?.materialSolicitado || '',
    tamano: solicitud?.tamano || '',
    notasWhatsapp: solicitud?.notasWhatsapp || '',
  });

  const [errors, setErrors] = useState({});
  const [serverError, setServerError] = useState('');


  /* ───── Foco inicial y trampa de foco ───── */
  useEffect(() => {
    firstInputRef.current?.focus();

    const handleKeyDown = (e) => {
      if (e.key === 'Escape' && !loading) {
        onCancel();
        return;
      }

      if (e.key === 'Tab' && modalRef.current) {
        const focusableElements = modalRef.current.querySelectorAll(
          'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
        );
        const first = focusableElements[0];
        const last = focusableElements[focusableElements.length - 1];

        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [loading, onCancel]);

  /* ───── Manejo de cambios ───── */
  const handleChange = (e) => {
    const { name, value, type } = e.target;
    const parsedValue = type === 'number' ? (value === '' ? '' : Number(value)) : value;

    setFormData((prev) => ({
      ...prev,
      [name]: parsedValue,
    }));

    // Limpiar error del campo al escribir
    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: undefined }));
    }
  };

  /* ───── Validación en el cliente ───── */
  const validate = () => {
    const newErrors = {};

    if (!formData.idCliente) {
      newErrors.idCliente = 'Debe seleccionar un cliente.';
    }

    if (!formData.idProducto) {
      newErrors.idProducto = 'Debe seleccionar un producto o servicio.';
    }

    if (!formData.cantidad || formData.cantidad < 1) {
      newErrors.cantidad = 'La cantidad debe ser mayor o igual a 1.';
    }

    if (!formData.telefonoContacto || !formData.telefonoContacto.trim()) {
      newErrors.telefonoContacto = 'El teléfono de contacto es obligatorio.';
    } else if (formData.telefonoContacto.trim().length < 8) {
      newErrors.telefonoContacto = 'El teléfono debe tener al menos 8 dígitos.';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  /* ───── Envío del formulario ───── */
  const handleSubmit = async (e) => {
    e.preventDefault();
    setServerError('');

    if (!validate()) {
      return;
    }

    try {
      const payload = {
        ...formData,
        idCliente: Number(formData.idCliente),
        idProducto: Number(formData.idProducto),
        cantidad: Number(formData.cantidad),
        telefonoContacto: formData.telefonoContacto.trim(),
        tamano: formData.tamano?.trim() || null,
        materialSolicitado: formData.materialSolicitado?.trim() || null,
        notasWhatsapp: formData.notasWhatsapp?.trim() || null,
      };

      await onSubmit(payload);
    } catch (err) {
      // Si la API devolvió errores por campo (ProblemDetails 400/422)
      if (err?.errors && Object.keys(err.errors).length > 0) {
        const fieldErrors = {};
        for (const [key, msgs] of Object.entries(err.errors)) {
          // Normalizar casing (ej: IdCliente -> idCliente)
          const normalizedKey = key.charAt(0).toLowerCase() + key.slice(1);
          fieldErrors[normalizedKey] = Array.isArray(msgs) ? msgs[0] : msgs;
        }
        setErrors(fieldErrors);
      }
      setServerError(err?.message || 'Error al procesar la solicitud.');
    }
  };

  return (
    <div
      className="modal-backdrop"
      onClick={(e) => {
        if (e.target === e.currentTarget && !loading) onCancel();
      }}
      role="presentation"
    >
      <div
        className="modal modal-lg"
        ref={modalRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="modal-solicitud-title"
      >
        <div className="modal-header">
          <h2 id="modal-solicitud-title" className="modal-title">
            {isEditing ? `Editar Solicitud #${solicitud.idSolicitud}` : 'Nueva Solicitud'}
          </h2>
          <button
            type="button"
            className="modal-close"
            onClick={onCancel}
            disabled={loading}
            aria-label="Cerrar ventana modal"
          >
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit} noValidate>
          <div className="modal-body">
            {serverError && (
              <div className="alert alert-danger" role="alert" style={{ marginBottom: 'var(--space-4)' }}>
                {serverError}
              </div>
            )}

            <div className="form-grid">
              {/* Cliente (Recurso Relacionado) */}
              <div className="form-group">
                <label htmlFor="field-idCliente" className="form-label required">
                  Cliente
                </label>
                <select
                  id="field-idCliente"
                  name="idCliente"
                  ref={firstInputRef}
                  className={`form-select ${errors.idCliente ? 'is-invalid' : ''}`}
                  value={formData.idCliente}
                  onChange={handleChange}
                  aria-invalid={Boolean(errors.idCliente)}
                  aria-describedby={errors.idCliente ? 'error-idCliente' : undefined}
                  required
                >
                  <option value="">-- Seleccionar Cliente --</option>
                  {clientes.map((cli) => (
                    <option key={cli.idCliente} value={cli.idCliente}>
                      {cli.nombre} {cli.rut ? `(${cli.rut})` : ''}
                    </option>
                  ))}
                </select>
                {errors.idCliente && (
                  <span id="error-idCliente" className="form-error" role="alert">
                    {errors.idCliente}
                  </span>
                )}
              </div>

              {/* Producto / Servicio (Recurso Relacionado) */}
              <div className="form-group">
                <label htmlFor="field-idProducto" className="form-label required">
                  Producto / Servicio
                </label>
                <select
                  id="field-idProducto"
                  name="idProducto"
                  className={`form-select ${errors.idProducto ? 'is-invalid' : ''}`}
                  value={formData.idProducto}
                  onChange={handleChange}
                  aria-invalid={Boolean(errors.idProducto)}
                  aria-describedby={errors.idProducto ? 'error-idProducto' : undefined}
                  required
                >
                  <option value="">-- Seleccionar Producto --</option>
                  {productos.map((prod) => (
                    <option key={prod.idProducto} value={prod.idProducto}>
                      {prod.nombre} {prod.precioBase ? `- $${prod.precioBase.toLocaleString('es-CL')}` : ''}
                    </option>
                  ))}
                </select>
                {errors.idProducto && (
                  <span id="error-idProducto" className="form-error" role="alert">
                    {errors.idProducto}
                  </span>
                )}
              </div>

              {/* Cantidad */}
              <div className="form-group">
                <label htmlFor="field-cantidad" className="form-label required">
                  Cantidad
                </label>
                <input
                  type="number"
                  id="field-cantidad"
                  name="cantidad"
                  min="1"
                  step="1"
                  className={`form-input ${errors.cantidad ? 'is-invalid' : ''}`}
                  value={formData.cantidad}
                  onChange={handleChange}
                  aria-invalid={Boolean(errors.cantidad)}
                  aria-describedby={errors.cantidad ? 'error-cantidad' : undefined}
                  required
                />
                {errors.cantidad && (
                  <span id="error-cantidad" className="form-error" role="alert">
                    {errors.cantidad}
                  </span>
                )}
              </div>

              {/* Teléfono de Contacto */}
              <div className="form-group">
                <label htmlFor="field-telefonoContacto" className="form-label required">
                  Teléfono de Contacto
                </label>
                <input
                  type="tel"
                  id="field-telefonoContacto"
                  name="telefonoContacto"
                  placeholder="+56 9 1234 5678"
                  className={`form-input ${errors.telefonoContacto ? 'is-invalid' : ''}`}
                  value={formData.telefonoContacto}
                  onChange={handleChange}
                  aria-invalid={Boolean(errors.telefonoContacto)}
                  aria-describedby={errors.telefonoContacto ? 'error-telefonoContacto' : undefined}
                  required
                />
                {errors.telefonoContacto && (
                  <span id="error-telefonoContacto" className="form-error" role="alert">
                    {errors.telefonoContacto}
                  </span>
                )}
              </div>

              {/* Tamaño */}
              <div className="form-group">
                <label htmlFor="field-tamano" className="form-label">
                  Tamaño
                </label>
                <input
                  type="text"
                  id="field-tamano"
                  name="tamano"
                  placeholder="Ej: 2m x 1m"
                  className="form-input"
                  value={formData.tamano}
                  onChange={handleChange}
                />
              </div>

              {/* Material Solicitado */}
              <div className="form-group">
                <label htmlFor="field-materialSolicitado" className="form-label">
                  Material Solicitado
                </label>
                <input
                  type="text"
                  id="field-materialSolicitado"
                  name="materialSolicitado"
                  placeholder="Ej: Acrílico 5mm con retroiluminación LED"
                  className="form-input"
                  value={formData.materialSolicitado}
                  onChange={handleChange}
                />
              </div>
            </div>

            {/* Notas WhatsApp / Observaciones */}
            <div className="form-group" style={{ marginTop: 'var(--space-4)' }}>
              <label htmlFor="field-notasWhatsapp" className="form-label">
                Notas adicionales / WhatsApp
              </label>
              <textarea
                id="field-notasWhatsapp"
                name="notasWhatsapp"
                rows="3"
                className="form-textarea"
                placeholder="Detalles acordados con el cliente, dimensiones, colores..."
                value={formData.notasWhatsapp}
                onChange={handleChange}
              />
            </div>
          </div>

          <div className="modal-footer">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={onCancel}
              disabled={loading}
            >
              Cancelar
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={loading}
            >
              {loading ? (
                <>
                  <span className="spinner-border spinner-border-sm" role="status" aria-hidden="true" />
                  Guardando…
                </>
              ) : isEditing ? (
                'Actualizar Solicitud'
              ) : (
                'Crear Solicitud'
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

SolicitudForm.propTypes = {
  solicitud: PropTypes.shape({
    idSolicitud: PropTypes.number,
    idCliente: PropTypes.number,
    idProducto: PropTypes.number,
    cantidad: PropTypes.number,
    telefonoContacto: PropTypes.string,
    tamano: PropTypes.string,
    materialSolicitado: PropTypes.string,
    estado: PropTypes.string,
    notasWhatsapp: PropTypes.string,
  }),
  clientes: PropTypes.arrayOf(
    PropTypes.shape({
      idCliente: PropTypes.number.isRequired,
      nombre: PropTypes.string.isRequired,
      rut: PropTypes.string,
    })
  ).isRequired,
  productos: PropTypes.arrayOf(
    PropTypes.shape({
      idProducto: PropTypes.number.isRequired,
      nombre: PropTypes.string.isRequired,
      precioBase: PropTypes.number,
    })
  ).isRequired,
  loading: PropTypes.bool.isRequired,
  onSubmit: PropTypes.func.isRequired,
  onCancel: PropTypes.func.isRequired,
};
