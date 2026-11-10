import { useEffect, useRef, useCallback } from 'react';
import PropTypes from 'prop-types';

/**
 * ConfirmModal
 * ————————————
 * Modal accesible con trampa de foco y soporte de tecla Escape.
 * Se usa principalmente para confirmar eliminaciones.
 */
export default function ConfirmModal({
  open,
  title,
  message,
  confirmLabel = 'Confirmar',
  cancelLabel = 'Cancelar',
  variant = 'danger',
  loading = false,
  onConfirm,
  onCancel,
}) {
  const modalRef = useRef(null);
  const cancelBtnRef = useRef(null);

  // Trampa de foco: al abrir, enfocar el botón de cancelar
  useEffect(() => {
    if (open && cancelBtnRef.current) {
      cancelBtnRef.current.focus();
    }
  }, [open]);

  // Cerrar con Escape
  const handleKeyDown = useCallback((e) => {
    if (e.key === 'Escape' && !loading) {
      onCancel();
    }
    // Trampa de foco
    if (e.key === 'Tab' && modalRef.current) {
      const focusable = modalRef.current.querySelectorAll(
        'button:not(:disabled), [href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])'
      );
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    }
  }, [loading, onCancel]);

  if (!open) return null;

  return (
    <div
      className="modal-overlay"
      onClick={loading ? undefined : onCancel}
      onKeyDown={handleKeyDown}
      role="dialog"
      aria-modal="true"
      aria-labelledby="confirm-modal-title"
      aria-describedby="confirm-modal-desc"
    >
      <div
        className="modal"
        ref={modalRef}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="modal-header">
          <h2 id="confirm-modal-title">{title}</h2>
        </div>
        <div className="modal-body">
          <p id="confirm-modal-desc">{message}</p>
        </div>
        <div className="modal-footer">
          <button
            ref={cancelBtnRef}
            type="button"
            className="btn btn-secondary"
            onClick={onCancel}
            disabled={loading}
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            className={`btn btn-${variant}`}
            onClick={onConfirm}
            disabled={loading}
          >
            {loading ? 'Procesando…' : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

ConfirmModal.propTypes = {
  open: PropTypes.bool.isRequired,
  title: PropTypes.string.isRequired,
  message: PropTypes.string.isRequired,
  confirmLabel: PropTypes.string,
  cancelLabel: PropTypes.string,
  variant: PropTypes.string,
  loading: PropTypes.bool,
  onConfirm: PropTypes.func.isRequired,
  onCancel: PropTypes.func.isRequired,
};
