import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { ScanFace } from 'lucide-react';
import { Aviso, Boton, Campo, Enlace, Tarjeta } from '../../componentes/ui';
import authService from '../../api/authService';
import { useAvisos } from '../../avisos/Avisos';
import FlujoFacial from '../../rostro/FlujoFacial';
import { pasosDelDesafio } from '../../rostro/desafio';
import { mensajeDeError } from '../../rostro/motorFacial';
import { useDevolverFoco } from '../../rostro/useDevolverFoco';
import { rutaInterna } from '../../sesion/rutas';
import { useSesion } from '../../sesion/sesion';
import estilos from './IngresoPersonal.module.css';

// El mismo mensaje para todo rechazo del servidor: no revela si el correo existe, su rol, si tiene rostro,
// si el equipo está autorizado ni qué comprobación de la prueba de vida falló.
const ROSTRO_NO_VERIFICADO = 'No pudimos confirmar que eres tú. Inténtalo de nuevo o entra con tu contraseña.';
const DEMASIADOS_CON_ROSTRO = 'Demasiados intentos con rostro. Espera un momento o entra con tu contraseña.';
// Tras estos rechazos seguidos, la pantalla lleva a la contraseña.
const RECHAZOS_PARA_CONTRASENA = 3;

const CORREO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * Acceso del personal del taller: correo y contraseña, o rostro con prueba de vida (solo dueño y administrador).
 * La contraseña está siempre a la vista; tras 3 rechazos seguidos del rostro, el foco va a ella.
 */
export default function IngresoPersonal() {
  const { ingresarPersonal, ingresarPersonalConRostro } = useSesion();
  const { anunciar } = useAvisos();
  const navegar = useNavigate();
  const ubicacion = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [cargando, setCargando] = useState(false);
  const [errores, setErrores] = useState({});
  const [capturando, setCapturando] = useState(false);
  const [verificando, setVerificando] = useState(false);
  const [rechazos, setRechazos] = useState(0);
  const [llevarAContrasena, setLlevarAContrasena] = useState(0);
  const refDesafio = useRef(null);
  const refPassword = useRef(null);
  const refEmail = useRef(null);
  const botonRostro = useDevolverFoco(capturando);
  const sugerirContrasena = rechazos >= RECHAZOS_PARA_CONTRASENA;

  // Va después de useDevolverFoco: si ambos actúan en el mismo cambio, gana la contraseña.
  useEffect(() => {
    if (llevarAContrasena > 0) refPassword.current?.focus();
  }, [llevarAContrasena]);

  // Tras salir no se vuelve a la pantalla del usuario anterior: quien entre después parte en el inicio.
  const destino = rutaInterna(ubicacion.state?.motivo !== 'salida' ? ubicacion.state?.desde?.pathname : null, '/panel');
  const sesionVencida = ubicacion.state?.motivo === 'vencida';
  const avisoRecibido = ubicacion.state?.aviso;

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

  function mensajeDeFallo(error) {
    if (error?.estado === 401) return 'Correo o contraseña incorrectos.';
    if (error?.estado === 429) return 'Demasiados intentos. Espera un minuto y vuelve a intentarlo.';
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
      setRechazos(0);
      setErrores({ formulario: mensajeDeFallo(error) });
    } finally {
      setCargando(false);
    }
  }

  function alEntrarConRostro() {
    const errorCorreo = validarCorreo();
    setErrores(errorCorreo ? { email: errorCorreo } : {});
    if (errorCorreo) refEmail.current?.focus();
    else setCapturando(true);
  }

  // Se pide con la cámara ya encendida, para que el desafío (unos 30 s) no se gaste esperando el permiso.
  // Con la señal, la respuesta tardía de un intento cancelado no pisa el desafío del intento nuevo.
  async function prepararPasos(senal) {
    refDesafio.current = null;
    const respuesta = await authService.desafioFacial(email.trim(), senal);
    const pasos = pasosDelDesafio(respuesta);
    if (!senal?.aborted) refDesafio.current = respuesta.desafio;
    return pasos;
  }

  async function alTerminarFlujo(capturas) {
    setVerificando(true);
    setCargando(true);
    try {
      const respuesta = await ingresarPersonalConRostro(email.trim(), { desafio: refDesafio.current, capturas });
      const nombre = respuesta?.nombre ?? respuesta?.usuario?.nombre;
      const rol = respuesta?.rol ?? respuesta?.usuario?.rol;
      anunciar({ tono: 'exito', titulo: nombre && rol ? `Entraste como ${nombre} (${rol})` : 'Entraste con tu rostro' });
      navegar(destino, { replace: true });
    } catch (error) {
      if (error?.estado === 401) {
        const siguientes = rechazos + 1;
        setRechazos(siguientes);
        setErrores({ formulario: ROSTRO_NO_VERIFICADO });
        if (siguientes >= RECHAZOS_PARA_CONTRASENA) setLlevarAContrasena((n) => n + 1);
      } else if (error?.estado === 429) {
        setRechazos((n) => Math.max(n, RECHAZOS_PARA_CONTRASENA));
        setErrores({ formulario: DEMASIADOS_CON_ROSTRO });
        setLlevarAContrasena((n) => n + 1);
      } else {
        // Un error de red no es un rechazo: no suma ni reinicia los rechazos seguidos.
        setErrores({ formulario: mensajeDeError(error) });
      }
      setCapturando(false);
    } finally {
      refDesafio.current = null;
      setCargando(false);
      setVerificando(false);
    }
  }

  function alFallarFlujo(error) {
    refDesafio.current = null;
    setCapturando(false);
    setErrores({ formulario: mensajeDeError(error) });
  }

  function alCancelarFlujo() {
    refDesafio.current = null;
    setCapturando(false);
  }

  const bloqueado = cargando || capturando;

  return (
    <div className={estilos.pagina}>
      <Tarjeta como="section" aria-labelledby="titulo-ingreso" className={estilos.tarjeta}>
        <img src="/marca/logo-lh-256.webp" alt="" width="56" height="56" className={estilos.logo} />
        <h1 id="titulo-ingreso" className={estilos.titulo}>Acceso del personal</h1>
        <p className={estilos.bajada}>Panel de SyncTime para el equipo de Letreros Herrera.</p>

        {ubicacion.state?.errorSalida && <Aviso tono="peligro" titulo={ubicacion.state.errorSalida} />}
        {avisoRecibido && !errores.formulario && <Aviso tono="exito" titulo={avisoRecibido} />}
        {sesionVencida && !avisoRecibido && !errores.formulario && (
          <Aviso tono="info" titulo="Tu sesión venció">Vuelve a ingresar para seguir donde estabas.</Aviso>
        )}
        {errores.formulario && <Aviso tono="peligro" titulo={errores.formulario} />}

        <form className={estilos.formulario} onSubmit={alEnviar} noValidate>
          <Campo
            ref={refEmail}
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
            ref={refPassword}
            etiqueta="Contraseña"
            type="password"
            maxLength={100}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            disabled={bloqueado}
            error={errores.password}
            ayuda={sugerirContrasena ? 'Entra con tu contraseña: siempre funciona, aunque el rostro no se confirme.' : undefined}
            className={sugerirContrasena ? estilos.resaltado : undefined}
          />
          <p className={estilos.olvido}>
            <Enlace como={Link} to="/cuenta/recuperar?tipo=personal">¿Olvidaste tu contraseña?</Enlace>
          </p>
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
          <FlujoFacial
            prepararPasos={prepararPasos}
            alTerminar={alTerminarFlujo}
            alError={alFallarFlujo}
            alCancelar={alCancelarFlujo}
          />
        )}

        <p className={estilos.nota}>
          ¿Eres cliente? <Enlace como={Link} to="/ingresar">Ingresa desde aquí</Enlace>.
        </p>
      </Tarjeta>
    </div>
  );
}
