import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { CircleAlert, CircleCheck } from 'lucide-react';
import { Boton, Esqueleto, Tarjeta } from '../../componentes/ui';
import authService from '../../api/authService';
import { useSesion } from '../../sesion/sesion';
import estilos from './Cuenta.module.css';

// Un enlace se usa una sola vez: si la pantalla se monta dos veces (StrictMode, volver atrás), las dos
// esperan la misma petición en vez de gastar el enlace con la primera y fallar con la segunda.
const verificaciones = new Map();

function verificarUnaVez(token) {
  if (!verificaciones.has(token)) {
    verificaciones.set(token, authService.verificarCorreo(token).then(
      () => ({ verificado: true }),
      (error) => {
        // Un corte de conexión no gasta el enlace: se puede volver a intentar recargando.
        if (error?.estado === 0) verificaciones.delete(token);
        return { verificado: false, mensaje: error?.message };
      },
    ));
  }
  return verificaciones.get(token);
}

const SIN_TOKEN = { verificado: false, mensaje: 'El enlace está incompleto. Ábrelo de nuevo desde el correo o pide uno nuevo desde tu cuenta.' };

/** /cuenta/verificar?token=…: confirma el correo del cliente con el enlace que le enviamos. */
export default function Verificar() {
  const [parametros] = useSearchParams();
  const navegar = useNavigate();
  // El token se lee una vez y se saca de la dirección: no queda en el historial ni en capturas de pantalla.
  const [token] = useState(() => parametros.get('token') ?? '');
  const { esCliente, actualizarUsuario } = useSesion();
  const [resultado, setResultado] = useState({ token: null, valor: null });
  const refTitulo = useRef(null);

  useEffect(() => {
    if (parametros.has('token')) navegar('/cuenta/verificar', { replace: true });
  }, [parametros, navegar]);

  useEffect(() => {
    if (!token) return undefined;
    let vigente = true;
    verificarUnaVez(token).then((valor) => {
      if (vigente) setResultado({ token, valor });
    });
    return () => { vigente = false; };
  }, [token]);

  const listo = resultado.token === token ? resultado.valor : null;
  // Solo un cliente con sesión tiene un correo que marcar como confirmado en pantalla.
  useEffect(() => {
    if (listo?.verificado && esCliente) actualizarUsuario({ correoVerificado: true });
  }, [listo, esCliente, actualizarUsuario]);

  // Al resolverse, el foco va al título del resultado: el lector lo anuncia.
  useEffect(() => {
    if (listo || !token) refTitulo.current?.focus();
  }, [listo, token]);

  const valor = token ? listo : SIN_TOKEN;
  const accion = esCliente
    ? <Boton como={Link} to="/cuenta">Ir a mi cuenta</Boton>
    : <Boton como={Link} to="/ingresar">Ingresar</Boton>;

  return (
    <div className={estilos.pagina}>
      <Tarjeta como="section" aria-labelledby="titulo-verificar" className={estilos.tarjeta}>
        {!valor && (
          <>
            <h1 id="titulo-verificar" className={estilos.titulo}>Confirmando tu correo…</h1>
            <p role="status" className="solo-lectores">Confirmando tu correo…</p>
            <Esqueleto lineas={2} />
          </>
        )}
        {valor?.verificado && (
          <>
            <CircleCheck size={40} strokeWidth={1.5} aria-hidden="true" color="var(--tono-exito-solido)" />
            <h1 ref={refTitulo} tabIndex={-1} id="titulo-verificar" className={estilos.titulo}>Tu correo está confirmado</h1>
            <p role="status">Gracias. Ya puedes seguir usando tu cuenta con este correo.</p>
            <div>{accion}</div>
          </>
        )}
        {valor && !valor.verificado && (
          <>
            <CircleAlert size={40} strokeWidth={1.5} aria-hidden="true" color="var(--tono-peligro-solido)" />
            <h1 ref={refTitulo} tabIndex={-1} id="titulo-verificar" className={estilos.titulo}>No pudimos confirmar tu correo</h1>
            <p role="alert">{valor.mensaje || 'El enlace no es válido o ya venció. Ingresa a tu cuenta y pide uno nuevo.'}</p>
            <div>{accion}</div>
          </>
        )}
      </Tarjeta>
    </div>
  );
}
