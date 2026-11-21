import { useEffect, useRef, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { UserPlus } from 'lucide-react';
import { Aviso, Boton, Campo, Enlace, Tarjeta } from '../../componentes/ui';
import { IngresoGoogle } from '../../google/IngresoGoogle';
import { rutaInterna } from '../../sesion/rutas';
import { useSesion } from '../../sesion/sesion';
import { errorCorreo } from './reglasCuenta';
import estilos from './Cuenta.module.css';

/** Adónde volver tras ingresar: lo que pidió la guarda, salvo que venga de una salida (otra persona). */
function destinoDe(estado) {
  const desde = estado?.motivo !== 'salida' ? estado?.desde : null;
  return rutaInterna(desde?.pathname ? `${desde.pathname}${desde.search ?? ''}` : null, '/cuenta');
}

/** /ingresar: el cliente entra con su correo y su contraseña. El acceso con rostro es solo del personal. */
export default function IngresoCliente() {
  const { ingresarCliente, esCliente, comprobando } = useSesion();
  const navegar = useNavigate();
  const ubicacion = useLocation();
  const destino = destinoDe(ubicacion.state);

  const [correo, setCorreo] = useState('');
  const [password, setPassword] = useState('');
  const [errores, setErrores] = useState({});
  const [cargando, setCargando] = useState(false);
  const refPassword = useRef(null);

  // Tras un rechazo los campos vuelven a habilitarse: el foco va a la contraseña para corregirla.
  useEffect(() => {
    if (errores.formulario && !cargando) refPassword.current?.focus();
  }, [errores, cargando]);

  // Con la sesión ya abierta (por ejemplo, al volver atrás), no tiene sentido pedir los datos otra vez.
  if (esCliente && !comprobando && !cargando) return <Navigate to={destino} replace />;

  function validar() {
    const encontrados = {};
    const error = errorCorreo(correo);
    if (error) encontrados.correo = error;
    if (!password) encontrados.password = 'Escribe tu contraseña.';
    return encontrados;
  }

  async function alEnviar(evento) {
    evento.preventDefault();
    const encontrados = validar();
    setErrores(encontrados);
    if (encontrados.correo || encontrados.password) {
      evento.currentTarget.querySelector(`[name="${encontrados.correo ? 'correo' : 'password'}"]`)?.focus();
      return;
    }
    setCargando(true);
    try {
      await ingresarCliente(correo.trim(), password);
      navegar(destino, { replace: true });
    } catch (error) {
      setCargando(false);
      if (error?.estado === 401) setErrores({ formulario: 'Correo o contraseña incorrectos. Revisa los dos y vuelve a intentarlo.' });
      else setErrores({ formulario: error?.message || 'No pudimos conectar con el servidor. Inténtalo de nuevo.' });
    }
  }

  const avisoRecibido = ubicacion.state?.aviso;
  const sesionVencida = ubicacion.state?.motivo === 'vencida';

  return (
    <div className={estilos.pagina}>
      <Tarjeta como="section" aria-labelledby="titulo-ingreso" className={estilos.tarjeta}>
        <img src="/marca/logo-lh-256.webp" alt="" width="56" height="56" className={estilos.logo} />
        <h1 id="titulo-ingreso" className={estilos.titulo}>Ingresar a tu cuenta</h1>
        <p className={estilos.bajada}>Sigue tus pedidos, cotizaciones y saldos de Letreros Herrera.</p>

        {/* `avisoTono` lo usa quien trae a la persona de vuelta (por ejemplo, un registro con Google que venció). */}
        {avisoRecibido && !errores.formulario && <Aviso role={ubicacion.state?.avisoTono ? 'alert' : undefined} tono={ubicacion.state?.avisoTono ?? 'exito'} titulo={avisoRecibido} />}
        {sesionVencida && !avisoRecibido && !errores.formulario && (
          <Aviso tono="info" titulo="Tu sesión venció">Vuelve a ingresar para seguir donde estabas.</Aviso>
        )}
        {errores.formulario && <Aviso tono="peligro" titulo={errores.formulario} />}

        {/* Fase 14B: «Continuar con Google» va ARRIBA del correo, con su separador (solo si hay ID de cliente de Google). */}
        <IngresoGoogle destino={destino} />

        <form className={estilos.formulario} onSubmit={alEnviar} noValidate>
          <Campo
            etiqueta="Correo electrónico"
            name="correo"
            type="email"
            inputMode="email"
            maxLength={100}
            autoComplete="email"
            value={correo}
            onChange={(e) => setCorreo(e.target.value)}
            disabled={cargando}
            error={errores.correo}
          />
          <Campo
            ref={refPassword}
            etiqueta="Contraseña"
            name="password"
            type="password"
            maxLength={200}
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={cargando}
            error={errores.password}
          />
          <p className={estilos.olvido}>
            <Enlace como={Link} to="/cuenta/recuperar">¿Olvidaste tu contraseña?</Enlace>
          </p>
          <Boton type="submit" anchoCompleto cargando={cargando} textoCargando="Ingresando…">Ingresar</Boton>
        </form>

        <div className={estilos.separador}>
          <p>¿Aún no tienes cuenta? Créala en un minuto con tu correo.</p>
          <Boton como={Link} to="/cuenta/registro" state={ubicacion.state} variante="secundario" anchoCompleto iconoInicio={UserPlus}>
            Crear cuenta
          </Boton>
        </div>

        <p className={estilos.nota}>
          ¿Trabajas en el taller? <Enlace como={Link} to="/personal/ingresar">Acceso del personal</Enlace>.
        </p>
      </Tarjeta>
    </div>
  );
}
