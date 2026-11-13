import { useCallback, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { ScanFace } from 'lucide-react';
import { Aviso, Boton, Campo, Enlace, Tarjeta } from '../../componentes/ui';
import CapturaFacial from '../../componentes/CapturaFacial';
import { mensajeDeError } from '../../rostro/motorFacial';
import { useDevolverFoco } from '../../rostro/useDevolverFoco';
import { useSesion } from '../../sesion/sesion';
import estilos from './IngresoPersonal.module.css';

// El mismo mensaje para todo fallo: no debe revelar si el correo existe, su rol ni si tiene el rostro activado.
const ROSTRO_NO_VERIFICADO =
  'No pudimos verificar tu rostro. Entra con tu contraseña. Si eres dueño o administrador, este equipo debe estar autorizado desde Mi perfil.';

const CORREO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Acceso del personal del taller: correo y contraseña, o rostro (solo dueño y administrador). */
export default function IngresoPersonal() {
  const { ingresarPersonal, ingresarPersonalConRostro } = useSesion();
  const navegar = useNavigate();
  const ubicacion = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [cargando, setCargando] = useState(false);
  const [errores, setErrores] = useState({});
  const [capturando, setCapturando] = useState(false);
  const [verificando, setVerificando] = useState(false);
  const botonRostro = useDevolverFoco(capturando || verificando);

  const destino = ubicacion.state?.desde?.pathname || '/panel';
  const sesionVencida = ubicacion.state?.motivo === 'vencida';

  const validarCorreo = useCallback(() => {
    const valor = email.trim();
    if (!valor) return 'El correo es obligatorio.';
    if (!CORREO.test(valor)) return 'Escribe un correo válido, por ejemplo nombre@letreria.cl.';
    if (valor.length > 100) return 'El correo no puede superar los 100 caracteres.';
    return null;
  }, [email]);

  function validar() {
    const encontrados = {};
    const errorCorreo = validarCorreo();
    if (errorCorreo) encontrados.email = errorCorreo;
    if (!password) encontrados.password = 'La contraseña es obligatoria.';
    else if (password.length > 100) encontrados.password = 'La contraseña no puede superar los 100 caracteres.';
    return encontrados;
  }

  function mensajeDeFallo(error, { conRostro }) {
    if (error?.estado === 401) return conRostro ? ROSTRO_NO_VERIFICADO : 'Correo o contraseña incorrectos.';
    if (error?.estado === 429) {
      return conRostro
        ? 'Demasiados intentos con rostro. Espera un momento o entra con tu contraseña.'
        : 'Demasiados intentos. Espera un minuto y vuelve a intentarlo.';
    }
    return error?.message || 'No pudimos conectar con el servidor.';
  }

  async function alEnviar(evento) {
    evento.preventDefault();
    const encontrados = validar();
    setErrores(encontrados);
    if (Object.keys(encontrados).length > 0) return;
    setCargando(true);
    try {
      await ingresarPersonal(email.trim(), password);
      navegar(destino, { replace: true });
    } catch (error) {
      setErrores({ formulario: mensajeDeFallo(error, { conRostro: false }) });
    } finally {
      setCargando(false);
    }
  }

  function alEntrarConRostro() {
    const errorCorreo = validarCorreo();
    setErrores(errorCorreo ? { email: errorCorreo } : {});
    if (!errorCorreo) setCapturando(true);
  }

  async function alCapturar([descriptor]) {
    setCapturando(false);
    setVerificando(true);
    setCargando(true);
    try {
      await ingresarPersonalConRostro(email.trim(), descriptor);
      navegar(destino, { replace: true });
    } catch (error) {
      setErrores({ formulario: mensajeDeFallo(error, { conRostro: true }) });
    } finally {
      setCargando(false);
      setVerificando(false);
    }
  }

  function alFallarCaptura(error) {
    setCapturando(false);
    setErrores({ formulario: mensajeDeError(error) });
  }

  const bloqueado = cargando || capturando;

  return (
    <div className={estilos.pagina}>
      <Tarjeta como="section" aria-labelledby="titulo-ingreso" className={estilos.tarjeta}>
        <img src="/marca/logo-lh-256.webp" alt="" width="56" height="56" className={estilos.logo} />
        <h1 id="titulo-ingreso" className={estilos.titulo}>Acceso del personal</h1>
        <p className={estilos.bajada}>Panel de SyncTime para el equipo de Letrería Los Herrera.</p>

        {sesionVencida && !errores.formulario && (
          <Aviso tono="info" titulo="Tu sesión venció">Vuelve a ingresar para seguir donde estabas.</Aviso>
        )}
        {errores.formulario && <Aviso tono="peligro" titulo={errores.formulario} />}

        <form className={estilos.formulario} onSubmit={alEnviar} noValidate>
          <Campo
            etiqueta="Correo electrónico"
            type="email"
            maxLength={100}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="email"
            autoFocus
            disabled={bloqueado}
            error={errores.email}
          />
          <Campo
            etiqueta="Contraseña"
            type="password"
            maxLength={100}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            disabled={bloqueado}
            error={errores.password}
          />
          <Boton type="submit" anchoCompleto cargando={cargando && !verificando} textoCargando="Iniciando sesión…" disabled={capturando}>
            Iniciar sesión
          </Boton>
          {!capturando && (
            <Boton ref={botonRostro} variante="secundario" anchoCompleto iconoInicio={ScanFace}
              onClick={alEntrarConRostro} disabled={cargando}>
              Entrar con rostro
            </Boton>
          )}
        </form>

        {capturando && (
          <CapturaFacial onCaptura={alCapturar} onError={alFallarCaptura} onCancelar={() => setCapturando(false)} />
        )}

        <p className={estilos.nota}>
          ¿Eres cliente? <Enlace como={Link} to="/ingresar">Ingresa desde aquí</Enlace>.
        </p>
      </Tarjeta>
    </div>
  );
}
