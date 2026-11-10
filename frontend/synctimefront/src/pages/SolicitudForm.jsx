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

    // Validación y filtrado especial en tiempo real para teléfono
    if (name === 'telefonoContacto') {
      // Solo permitir dígitos, espacios, guión y '+' inicial
      let filtered = value.replace(/[^\d+ -]/g, '');
      if (filtered.includes('+')) {
        filtered = (filtered.startsWith('+') ? '+' : '') + filtered.slice(filtered.startsWith('+') ? 1 : 0).replace(/\+/g, '');
      }
      // Limitar a máximo 16 caracteres para no exceder la columna de BD
      filtered = filtered.slice(0, 16);

      setFormData((prev) => ({
        ...prev,
        [name]: filtered,
      }));

      if (errors[name]) {
        setErrors((prev) => ({ ...prev, [name]: undefined }));
      }
      return;
    }

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

    // 1. Cliente (obligatorio)
    if (!formData.idCliente || Number(formData.idCliente) <= 0) {
      newErrors.idCliente = 'Debe seleccionar un cliente de la lista.';
    }

    // 2. Producto / Servicio (obligatorio)
    if (!formData.idProducto || Number(formData.idProducto) <= 0) {
      newErrors.idProducto = 'Debe seleccionar un producto o servicio.';
    }

    // 3. Cantidad (obligatorio, entero positivo entre 1 y 100.000)
    if (formData.cantidad === '' || formData.cantidad === null || formData.cantidad === undefined) {
      newErrors.cantidad = 'La cantidad es obligatoria.';
    } else if (!Number.isInteger(Number(formData.cantidad)) || Number(formData.cantidad) < 1) {
      newErrors.cantidad = 'La cantidad debe ser un número entero mayor o igual a 1.';
    } else if (Number(formData.cantidad) > 100000) {
      newErrors.cantidad = 'La cantidad no puede superar las 100.000 unidades.';
    }

    // 4. Teléfono de Contacto (obligatorio, solo números y +, entre 8 y 12 dígitos, referencia Chile)
    if (!formData.telefonoContacto || !formData.telefonoContacto.trim()) {
      newErrors.telefonoContacto = 'El teléfono de contacto es obligatorio.';
    } else {
      const cleanPhone = formData.telefonoContacto.trim();
      const digitsOnly = cleanPhone.replace(/\D/g, '');

      if (!/^(\+?\d[\d\s-]*)$/.test(cleanPhone)) {
        newErrors.telefonoContacto = 'El teléfono solo debe contener números (se permite prefijo "+").';
      } else if (digitsOnly.length < 8) {
        newErrors.telefonoContacto = 'El teléfono debe tener al menos 8 dígitos numéricos.';
      } else if (digitsOnly.length > 12) {
        newErrors.telefonoContacto = 'El teléfono no puede superar los 12 dígitos numéricos (referencia Chile: +56 9 1234 5678).';
      }
    }

    // 5. Tamaño (opcional, máx 50 caracteres según esquema BD)
    if (formData.tamano && formData.tamano.trim().length > 50) {
      newErrors.tamano = 'El tamaño no puede superar los 50 caracteres.';
    }

    // 6. Material Solicitado (opcional, máx 100 caracteres según esquema BD)
    if (formData.materialSolicitado && formData.materialSolicitado.trim().length > 100) {
      newErrors.materialSolicitado = 'El material no puede superar los 100 caracteres.';
    }

    // 7. Notas WhatsApp (opcional, máx 255 caracteres según esquema BD)
    if (formData.notasWhatsapp && formData.notasWhatsapp.trim().length > 255) {
      newErrors.notasWhatsapp = 'Las notas no pueden superar los 255 caracteres.';
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
                  maxLength={16}
                  className={`form-input ${errors.telefonoContacto ? 'is-invalid' : ''}`}
                  value={formData.telefonoContacto}
                  onChange={handleChange}
                  aria-invalid={Boolean(errors.telefonoContacto)}
                  aria-describedby={errors.telefonoContacto ? 'error-telefonoContacto' : 'hint-telefonoContacto'}
                  required
                />
                <span id="hint-telefonoContacto" className="form-hint" style={{ fontSize: '0.8rem', color: 'var(--color-text-muted)', display: 'block', marginTop: '0.25rem' }}>
                  Solo números (referencia Chile: +56 9 1234 5678, máx. 12 dígitos).
                </span>
                {errors.telefonoContacto && (
                  <span id="error-telefonoContacto" className="form-error" role="alert">
                    {errors.telefonoContacto}
                  </span>
                )}
              </div>

              {/* Tamaño */}
              <div className="form-group">
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <label htmlFor="field-tamano" className="form-label">
                    Tamaño
                  </label>
                  <span style={{ fontSize: '0.75rem', color: (formData.tamano?.length || 0) > 45 ? 'var(--color-danger)' : 'var(--color-text-muted)' }}>
                    {formData.tamano?.length || 0}/50
                  </span>
                </div>
                <input
                  type="text"
                  id="field-tamano"
                  name="tamano"
                  maxLength={50}
                  placeholder="Ej: 2m x 1m"
                  className={`form-input ${errors.tamano ? 'is-invalid' : ''}`}
                  value={formData.tamano}
                  onChange={handleChange}
                  aria-invalid={Boolean(errors.tamano)}
                  aria-describedby={errors.tamano ? 'error-tamano' : undefined}
                />
                {errors.tamano && (
                  <span id="error-tamano" className="form-error" role="alert">
                    {errors.tamano}
                  </span>
                )}
              </div>

              {/* Material Solicitado */}
              <div className="form-group">
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <label htmlFor="field-materialSolicitado" className="form-label">
                    Material Solicitado
                  </label>
                  <span style={{ fontSize: '0.75rem', color: (formData.materialSolicitado?.length || 0) > 90 ? 'var(--color-danger)' : 'var(--color-text-muted)' }}>
                    {formData.materialSolicitado?.length || 0}/100
                  </span>
                </div>
                <input
                  type="text"
                  id="field-materialSolicitado"
                  name="materialSolicitado"
                  maxLength={100}
                  placeholder="Ej: Acrílico 5mm con retroiluminación LED"
                  className={`form-input ${errors.materialSolicitado ? 'is-invalid' : ''}`}
                  value={formData.materialSolicitado}
                  onChange={handleChange}
                  aria-invalid={Boolean(errors.materialSolicitado)}
                  aria-describedby={errors.materialSolicitado ? 'error-materialSolicitado' : undefined}
                />
                {errors.materialSolicitado && (
                  <span id="error-materialSolicitado" className="form-error" role="alert">
                    {errors.materialSolicitado}
                  </span>
                )}
              </div>
            </div>

            {/* Notas WhatsApp / Observaciones */}
            <div className="form-group" style={{ marginTop: 'var(--space-4)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <label htmlFor="field-notasWhatsapp" className="form-label">
                  Notas adicionales / WhatsApp
                </label>
                <span style={{ fontSize: '0.75rem', color: (formData.notasWhatsapp?.length || 0) > 240 ? 'var(--color-danger)' : 'var(--color-text-muted)' }}>
                  {formData.notasWhatsapp?.length || 0}/255
                </span>
              </div>
              <textarea
                id="field-notasWhatsapp"
                name="notasWhatsapp"
                rows="3"
                maxLength={255}
                className={`form-textarea ${errors.notasWhatsapp ? 'is-invalid' : ''}`}
                placeholder="Detalles acordados con el cliente, dimensiones, colores..."
                value={formData.notasWhatsapp}
                onChange={handleChange}
                aria-invalid={Boolean(errors.notasWhatsapp)}
                aria-describedby={errors.notasWhatsapp ? 'error-notasWhatsapp' : undefined}
              />
              {errors.notasWhatsapp && (
                <span id="error-notasWhatsapp" className="form-error" role="alert">
                  {errors.notasWhatsapp}
                </span>
              )}
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
