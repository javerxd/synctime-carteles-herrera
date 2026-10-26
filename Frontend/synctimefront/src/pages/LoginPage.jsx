import { useState, useCallback } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';
import './LoginPage.css';

/**
 * LoginPage
 * —————————
 * Formulario accesible con validación en cliente y manejo de errores HTTP.
 */
export default function LoginPage() {
  const { login } = useAuth();
  const { addToast } = useToast();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [errors, setErrors] = useState({});

  const from = location.state?.from?.pathname || '/dashboard';

  const validate = useCallback(() => {
    const errs = {};
    if (!email.trim()) {
      errs.email = 'El email es obligatorio.';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      errs.email = 'Ingrese un correo electrónico válido (ej: usuario@letrerosherrera.cl).';
    } else if (email.trim().length > 100) {
      errs.email = 'El correo no puede superar los 100 caracteres.';
    }

    if (!password) {
      errs.password = 'La contraseña es obligatoria.';
    } else if (password.length < 4) {
      errs.password = 'La contraseña es demasiado corta (mínimo 4 caracteres).';
    } else if (password.length > 100) {
      errs.password = 'La contraseña no puede superar los 100 caracteres.';
    }
    return errs;
  }, [email, password]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const errs = validate();
    setErrors(errs);
    if (Object.keys(errs).length > 0) return;

    setLoading(true);
    try {
      const usuario = await login(email.trim(), password);
      addToast(`Bienvenido/a, ${usuario.nombre}`, 'success');
      navigate(from, { replace: true });
    } catch (err) {
      if (err?.status === 401) {
        setErrors({ form: 'Email o contraseña incorrectos.' });
      } else if (err?.status === 429) {
        setErrors({ form: 'Demasiados intentos. Espere un momento.' });
      } else {
        setErrors({ form: err?.message || 'Error de conexión con el servidor.' });
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-brand">
          <div className="login-icon" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" className="brand-mark">
              <path
                d="M12 2L3 7v10l9 5 9-5V7l-9-5z"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinejoin="round"
              />
              <path
                d="M12 8v4l3 2"
                stroke="currentColor"
                strokeWidth="1.75"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </div>
          <h1>SyncTime</h1>
          <p>Letreros Herrera — Panel de Gestión</p>
        </div>

        {errors.form && (
          <div className="alert alert-error" role="alert">
            <svg
              className="alert-icon"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.75"
              aria-hidden="true"
            >
              <circle cx="12" cy="12" r="9" />
              <line x1="12" y1="8" x2="12" y2="12.5" />
              <circle cx="12" cy="15.5" r="0.5" fill="currentColor" />
            </svg>
            <span>{errors.form}</span>
          </div>
        )}

        <form className="login-form" onSubmit={handleSubmit} noValidate>
          <div className="form-group">
            <label htmlFor="login-email" className="form-label">
              Correo electrónico
            </label>
            <input
              id="login-email"
              type="email"
              maxLength={100}
              className={`form-input ${errors.email ? 'error' : ''}`}
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="email"
              autoFocus
              disabled={loading}
              aria-describedby={errors.email ? 'login-email-error' : undefined}
              aria-invalid={!!errors.email}
            />
            {errors.email && (
              <span id="login-email-error" className="form-error" role="alert">
                {errors.email}
              </span>
            )}
          </div>

          <div className="form-group">
            <label htmlFor="login-password" className="form-label">
              Contraseña
            </label>
            <input
              id="login-password"
              type="password"
              maxLength={100}
              className={`form-input ${errors.password ? 'error' : ''}`}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              disabled={loading}
              aria-describedby={errors.password ? 'login-password-error' : undefined}
              aria-invalid={!!errors.password}
            />
            {errors.password && (
              <span id="login-password-error" className="form-error" role="alert">
                {errors.password}
              </span>
            )}
          </div>

          <button
            type="submit"
            className="btn btn-primary"
            disabled={loading}
          >
            {loading ? 'Iniciando sesión…' : 'Iniciar sesión'}
          </button>
        </form>
      </div>
    </div>
  );
}