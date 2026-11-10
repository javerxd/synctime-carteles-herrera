import { useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { useToast } from '../context/ToastContext';
import authService from '../api/authService';
import CapturaFacial from '../components/CapturaFacial';
import ConfirmModal from '../components/ConfirmModal';
import { mensajeDeError } from '../rostro/motorFacial';
import { borrarTokenEquipo, guardarTokenEquipo, leerTokenEquipo } from '../rostro/tokenEquipo';
import { useDevolverFoco } from '../rostro/useDevolverFoco';
import './PerfilPage.css';

const CAPTURAS_PARA_ACTIVAR = 5;

const fechaLegible = (iso) =>
  iso ? new Date(iso).toLocaleDateString('es-CL', { day: 'numeric', month: 'long', year: 'numeric' }) : '';

/**
 * PerfilPage
 * ——————————
 * Datos de la cuenta y gestión del acceso con rostro: activarlo con consentimiento,
 * autorizar el equipo cuando el rol lo exige, y desactivarlo borrando los datos.
 */
export default function PerfilPage() {
  const { user } = useAuth();
  const { addToast } = useToast();

  const [estado, setEstado] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState('');
  const [aceptado, setAceptado] = useState(false);
  const [password, setPassword] = useState('');
  const [capturando, setCapturando] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [confirmando, setConfirmando] = useState(false);
  const botonActivar = useDevolverFoco(capturando || enviando);

  useEffect(() => {
    let cancelado = false;

    authService.estadoRostro()
      .then((datos) => {
        if (!cancelado) setEstado(datos);
      })
      .catch((err) => {
        if (!cancelado) setError(err?.message || 'No se pudo consultar el acceso con rostro.');
      })
      .finally(() => {
        if (!cancelado) setCargando(false);
      });

    return () => { cancelado = true; };
  }, []);

  // El cambio ya se guardó: si falla esta consulta, no debe informarse como si el cambio hubiera fallado.
  const recargar = async () => {
    try {
      setEstado(await authService.estadoRostro());
    } catch {
      setError('El cambio se guardó, pero no se pudo actualizar esta vista. Recargue la página.');
    }
  };

  const guardarToken = (token) => {
    if (token && !guardarTokenEquipo(user.email, token)) {
      setError('El navegador no permitió guardar la autorización de este equipo. Revise su configuración de privacidad.');
    }
  };

  const handleIniciarActivacion = (e) => {
    e.preventDefault();
    if (!aceptado) {
      setError('Debe aceptar el consentimiento para activar el acceso con rostro.');
    } else if (!password) {
      setError('Escriba su contraseña para confirmar que es usted.');
    } else {
      setError('');
      setCapturando(true);
    }
  };

  const handleCaptura = async (descriptores) => {
    setCapturando(false);
    setEnviando(true);
    try {
      const respuesta = await authService.activarRostro({
        password,
        versionConsentimiento: estado.consentimiento.version,
        descriptores,
      });
      guardarToken(respuesta.tokenEquipo);
      setPassword('');
      setAceptado(false);
      addToast('Acceso con rostro activado.', 'success');
      await recargar();
    } catch (err) {
      setError(err?.message || 'No se pudo activar el acceso con rostro.');
    } finally {
      setEnviando(false);
    }
  };

  const handleAutorizarEquipo = async (e) => {
    e.preventDefault();
    if (!password) {
      setError('Escriba su contraseña para confirmar que es usted.');
      return;
    }
    setError('');
    setEnviando(true);
    try {
      const respuesta = await authService.autorizarEquipo(password);
      guardarToken(respuesta.tokenEquipo);
      setPassword('');
      addToast('Este equipo quedó autorizado.', 'success');
      await recargar();
    } catch (err) {
      setError(err?.message || 'No se pudo autorizar este equipo.');
    } finally {
      setEnviando(false);
    }
  };

  const handleDesactivar = async () => {
    setEnviando(true);
    try {
      await authService.desactivarRostro();
      borrarTokenEquipo(user.email);
      setError('');
      addToast('Acceso con rostro desactivado. Sus datos biométricos fueron borrados.', 'success');
      await recargar();
    } catch (err) {
      setError(err?.message || 'No se pudo desactivar el acceso con rostro.');
    } finally {
      setEnviando(false);
      setConfirmando(false);
    }
  };

  const equipoAutorizado = estado?.equipoVigente && !!leerTokenEquipo(user.email);

  const campoPassword = (
    <div className="form-group">
      <label htmlFor="perfil-password" className="form-label">Contraseña actual</label>
      <input
        id="perfil-password"
        type="password"
        maxLength={100}
        className="form-input"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        autoComplete="current-password"
        disabled={enviando}
      />
    </div>
  );

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Mi perfil</h1>
          <p>{user?.nombre} {user?.apellido} · {user?.email} · {user?.rol}</p>
        </div>
      </div>

      <section className="card" aria-labelledby="perfil-rostro-titulo">
        <div className="card-header">
          <h2 id="perfil-rostro-titulo">Acceso con rostro</h2>
        </div>
        <div className="card-body perfil-rostro">
          {error && <div className="alert alert-error" role="alert">{error}</div>}

          {cargando && <p role="status">Consultando…</p>}

          {!cargando && estado && !estado.disponible && (
            <div className="alert alert-info">El acceso con rostro no está disponible en este momento.</div>
          )}

          {!cargando && estado?.disponible && !estado.activo && (
            <form onSubmit={handleIniciarActivacion} noValidate>
              <p>Es opcional: siempre podrá entrar con su contraseña. Lea lo siguiente antes de activarlo:</p>
              <ul>
                {estado.consentimiento.texto.map((parrafo) => (
                  <li key={parrafo}>{parrafo}</li>
                ))}
              </ul>

              <div className="form-group form-group-amplio">
                <label>
                  <input
                    type="checkbox"
                    checked={aceptado}
                    onChange={(e) => setAceptado(e.target.checked)}
                    disabled={enviando || capturando}
                  />{' '}
                  Leí la información anterior y autorizo el tratamiento de mi dato biométrico para esta finalidad.
                </label>
              </div>

              {campoPassword}

              {capturando ? (
                <CapturaFacial
                  cantidad={CAPTURAS_PARA_ACTIVAR}
                  onCaptura={handleCaptura}
                  onError={(err) => {
                    setCapturando(false);
                    setError(mensajeDeError(err));
                  }}
                  onCancelar={() => setCapturando(false)}
                />
              ) : (
                <button ref={botonActivar} type="submit" className="btn btn-primary" disabled={enviando}>
                  {enviando ? 'Activando…' : 'Activar con mi rostro'}
                </button>
              )}
            </form>
          )}

          {!cargando && estado?.disponible && estado.activo && (
            <>
              <p>Activado el {fechaLegible(estado.fechaConsentimiento)}.</p>

              {estado.requiereEquipo && (
                <form onSubmit={handleAutorizarEquipo} noValidate>
                  <p>
                    {equipoAutorizado
                      ? `Este equipo está autorizado hasta el ${fechaLegible(estado.equipoVence)}. Autorizar otro anula este.`
                      : 'Este equipo no está autorizado: desde aquí no podrá entrar con su rostro. Autorizarlo anula el equipo anterior.'}
                  </p>
                  {campoPassword}
                  <button type="submit" className="btn btn-secondary" disabled={enviando}>
                    Autorizar este equipo
                  </button>
                </form>
              )}

              <button
                type="button"
                className="btn btn-danger"
                onClick={() => setConfirmando(true)}
                disabled={enviando}
              >
                Desactivar y borrar mis datos
              </button>
            </>
          )}
        </div>
      </section>

      <ConfirmModal
        open={confirmando}
        title="Desactivar el acceso con rostro"
        message="Se borrarán sus datos biométricos y seguirá entrando con su contraseña. Solo se conserva la constancia de las fechas de aceptación y retiro."
        confirmLabel="Desactivar y borrar"
        loading={enviando}
        onConfirm={handleDesactivar}
        onCancel={() => setConfirmando(false)}
      />
    </>
  );
}
