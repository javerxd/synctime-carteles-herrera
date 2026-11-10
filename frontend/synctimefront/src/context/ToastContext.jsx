import { useState, useCallback, createContext, useContext, useMemo } from 'react';
import PropTypes from 'prop-types';

const ToastContext = createContext(null);

let toastId = 0;

/**
 * ToastProvider — sistema de notificaciones accesible.
 * Los toasts se anuncian a lectores de pantalla vía aria-live.
 */
export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);

  const addToast = useCallback((message, type = 'info', duration = 5000) => {
    const id = ++toastId;
    setToasts((prev) => [...prev, { id, message, type }]);
    if (duration > 0) {
      setTimeout(() => {
        setToasts((prev) => prev.filter((t) => t.id !== id));
      }, duration);
    }
    return id;
  }, []);

  const removeToast = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const value = useMemo(() => ({ addToast, removeToast }), [addToast, removeToast]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="toast-container" aria-live="polite" aria-label="Notificaciones">
        {toasts.map((toast) => (
          <div key={toast.id} className={`toast alert alert-${toast.type}`} role="alert">
            <span>{toast.message}</span>
            <button
              type="button"
              className="btn btn-ghost btn-sm btn-icon"
              onClick={() => removeToast(toast.id)}
              aria-label="Cerrar notificación"
            >
              ✕
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

ToastProvider.propTypes = {
  children: PropTypes.node.isRequired,
};

export function useToast() {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error('useToast debe usarse dentro de un ToastProvider.');
  }
  return context;
}
