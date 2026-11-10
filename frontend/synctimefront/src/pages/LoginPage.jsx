import { useState, useCallback } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';
import CapturaFacial from '../components/CapturaFacial';
import { mensajeDeError } from '../rostro/motorFacial';
import { useDevolverFoco } from '../rostro/useDevolverFoco';
import './LoginPage.css';

// El mismo mensaje para todo fallo: no debe revelar si el email existe, su rol ni si tiene el rostro activado.
const ROSTRO_NO_VERIFICADO =
  'No se pudo verificar su rostro. Entre con su contraseña. Si es dueño o administrador, este equipo debe estar autorizado desde Mi perfil.';

/**
 * LoginPage
 * —————————
 * Formulario accesible con validación en cliente y manejo de errores HTTP.
 */
export default function LoginPage() {
  const { login, loginFacial } = useAuth();
  const { addToast } = useToast();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [errors, setErrors] = useState({});
  const [capturando, setCapturando] = useState(false);
  const [verificando, setVerificando] = useState(false);
  const botonRostro = useDevolverFoco(capturando || verificando);

  const from = location.state?.from?.pathname || '/dashboard';

  const validateEmail = useCallback(() => {
    if (!email.trim()) {
      return 'El email es obligatorio.';
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      return 'Ingrese un correo electrónico válido (ej: usuario@letrerosherrera.cl).';
    }
    if (email.trim().length > 100) {
      return 'El correo no puede superar los 100 caracteres.';
    }
    return null;
  }, [email]);

  const validate = useCallback(() => {
    const errs = {};
    const emailError = validateEmail();
    if (emailError) {
      errs.email = emailError;
    }

    if (!password) {
      errs.password = 'La contraseña es obligatoria.';
    } else if (password.length < 4) {
      errs.password = 'La contraseña es demasiado corta (mínimo 4 caracteres).';
    } else if (password.length > 100) {
      errs.password = 'La contraseña no puede superar los 100 caracteres.';
    }
    return errs;
  }, [validateEmail, password]);

  const handleEntrarConRostro = () => {
    const emailError = validateEmail();
    setErrors(emailError ? { email: emailError } : {});
    if (!emailError) setCapturando(true);
  };

  const handleCaptura = async ([descriptor]) => {
    setCapturando(false);
    setVerificando(true);
    setLoading(true);
    try {
      const usuario = await loginFacial(email.trim(), descriptor);
      addToast(`Bienvenido/a, ${usuario.nombre}`, 'success');
      navigate(from, { replace: true });
    } catch (err) {
      if (err?.status === 401) {
        setErrors({ form: ROSTRO_NO_VERIFICADO });
      } else if (err?.status === 429) {
        setErrors({ form: 'Demasiados intentos con rostro. Espere un momento, o entre con su contraseña.' });
      } else {
        setErrors({ form: err?.message || 'Error de conexión con el servidor.' });
      }
    } finally {
      setLoading(false);
      setVerificando(false);
    }
  };

  const handleErrorCaptura = (error) => {
    setCapturando(false);
    setErrors({ form: mensajeDeError(error) });
  };

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
              disabled={loading || capturando}
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
              disabled={loading || capturando}
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
            disabled={loading || capturando}
          >
            {loading ? 'Iniciando sesión…' : 'Iniciar sesión'}
          </button>

          {!capturando && (
            <button
              ref={botonRostro}
              type="button"
              className="btn btn-secondary"
              onClick={handleEntrarConRostro}
              disabled={loading}
            >
              Entrar con rostro
            </button>
          )}
        </form>

        {capturando && (
          <div className="login-rostro">
            <CapturaFacial
              onCaptura={handleCaptura}
              onError={handleErrorCaptura}
              onCancelar={() => setCapturando(false)}
            />
          </div>
        )}
      </div>
    </div>
  );
}