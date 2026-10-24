import { useEffect, useRef, useCallback } from 'react';
import PropTypes from 'prop-types';

/**
 * DetailModal
 * ———————————
 * Modal accesible de solo lectura para consultar la ficha completa de un registro.
 * Cumple WCAG 2.2 AA:
 * - role="dialog", aria-modal="true", aria-labelledby
 * - Trampa de foco (Tab / Shift+Tab)
 * - Cierre con tecla Escape
 * - Foco automático al botón de cierre al abrirse
 */
export default function DetailModal({ open, title, children, onClose }) {
  const modalRef = useRef(null);
  const closeBtnRef = useRef(null);

  /* ───── Foco inicial al abrirse ───── */
  useEffect(() => {
    if (open && closeBtnRef.current) {
      closeBtnRef.current.focus();
    }
  }, [open]);

  /* ───── Escape y trampa de foco ───── */
  const handleKeyDown = useCallback((e) => {
    if (e.key === 'Escape') {
      onClose();
    }
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
  }, [onClose]);

  if (!open) return null;

  return (
    <div
      className="modal-overlay"
      onClick={onClose}
      onKeyDown={handleKeyDown}
      role="dialog"
      aria-modal="true"
      aria-labelledby="detail-modal-title"
    >
      <div
        className="modal modal-lg"
        ref={modalRef}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="modal-header">
          <h2 id="detail-modal-title" className="modal-title">{title}</h2>
          <button
            ref={closeBtnRef}
            type="button"
            className="modal-close"
            onClick={onClose}
            aria-label="Cerrar ventana de detalle"
          >
            ✕
          </button>
        </div>
        <div className="modal-body">
          {children}
        </div>
        <div className="modal-footer">
          <button
            type="button"
            className="btn btn-secondary"
            onClick={onClose}
          >
            Cerrar
          </button>
        </div>
      </div>
    </div>
  );
}

DetailModal.propTypes = {
  open: PropTypes.bool.isRequired,
  title: PropTypes.string.isRequired,
  children: PropTypes.node.isRequired,
  onClose: PropTypes.func.isRequired,
};
