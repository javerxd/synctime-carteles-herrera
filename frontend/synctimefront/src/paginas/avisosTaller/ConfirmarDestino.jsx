import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { CircleAlert, CircleCheck } from 'lucide-react';
import { Boton, Esqueleto, Tarjeta } from '../../componentes/ui';
import { avisosTaller } from '../../api/avisosTaller';
import estilos from './AvisosTaller.module.css';

// El enlace es de un solo uso: si la pantalla se monta dos veces (StrictMode, volver atrás), las dos esperan la
// misma petición en vez de gastarlo con la primera y fallar con la segunda (como /cuenta/verificar).
const confirmaciones = new Map();

const MENSAJE_INVALIDO = 'El enlace no es válido o ya venció. Pide uno nuevo desde el perfil.';

function confirmarUnaVez(token) {
  if (!confirmaciones.has(token)) {
    confirmaciones.set(token, avisosTaller.confirmar(token).then(
      (respuesta) => ({ confirmado: true, correo: respuesta?.correo ?? '' }),
      (error) => {
        // Un corte de conexión no gasta el enlace: se puede volver a intentar recargando.
        if (error?.estado === 0) confirmaciones.delete(token);
        return { confirmado: false, mensaje: error?.message || MENSAJE_INVALIDO };
      },
    ));
  }
  return confirmaciones.get(token);
}

const SIN_TOKEN = { confirmado: false, mensaje: 'El enlace está incompleto. Ábrelo de nuevo desde el correo o pide uno nuevo desde el perfil.' };

/**
 * /avisos/confirmar?token=…: confirma el correo que recibirá los avisos del taller. Es pública: la abre quien
 * recibe el correo, sin sesión. No dice qué correo era si el enlace no sirve.
 */
export default function ConfirmarDestino() {
  const [parametros] = useSearchParams();
  const navegar = useNavigate();
  // El token se lee una vez y se saca de la dirección: no queda en el historial.
  const [token] = useState(() => parametros.get('token') ?? '');
  const [resultado, setResultado] = useState({ token: null, valor: null });
  const refTitulo = useRef(null);

  useEffect(() => {
    if (parametros.has('token')) navegar('/avisos/confirmar', { replace: true });
  }, [parametros, navegar]);

  useEffect(() => {
    if (!token) return undefined;
    let vigente = true;
    confirmarUnaVez(token).then((valor) => {
      if (vigente) setResultado({ token, valor });
    });
    return () => { vigente = false; };
  }, [token]);

  // Mientras esta página está abierta, el navegador no envía su dirección (con el token) como Referer.
  useEffect(() => {
    const meta = document.createElement('meta');
    meta.name = 'referrer';
    meta.content = 'no-referrer';
    document.head.appendChild(meta);
    return () => meta.remove();
  }, []);

  const listo = resultado.token === token ? resultado.valor : null;
  useEffect(() => {
    if (listo || !token) refTitulo.current?.focus();
  }, [listo, token]);

  const valor = token ? listo : SIN_TOKEN;

  // Las regiones de estado existen desde el inicio y solo cambia su texto: así el lector las anuncia.
  const estadoTexto = !valor ? 'Confirmando el correo…' : valor.confirmado ? `Correo confirmado: ${valor.correo || 'este correo'} recibe los avisos.` : '';
  const alerta = valor && !valor.confirmado ? valor.mensaje : '';

  return (
    <div className={estilos.paginaConfirmar}>
      <p role="status" className="solo-lectores">{estadoTexto}</p>
      <p role="alert" className="solo-lectores">{alerta}</p>
      <Tarjeta como="section" aria-labelledby="titulo-confirmar-aviso" className={estilos.tarjetaConfirmar}>
        {!valor && (
          <>
            <h1 id="titulo-confirmar-aviso" className={estilos.tituloConfirmar}>Confirmando el correo…</h1>
            <Esqueleto lineas={2} />
          </>
        )}
        {valor?.confirmado && (
          <>
            <CircleCheck size={40} strokeWidth={1.5} aria-hidden="true" color="var(--tono-exito-solido)" />
            <h1 ref={refTitulo} tabIndex={-1} id="titulo-confirmar-aviso" className={estilos.tituloConfirmar}>Correo confirmado</h1>
            <p>
              Desde ahora, <strong className="cifras">{valor.correo || 'este correo'}</strong> recibe los avisos de solicitudes nuevas de Letreros Herrera.
            </p>
            <p>Puedes cerrar esta página.</p>
          </>
        )}
        {valor && !valor.confirmado && (
          <>
            <CircleAlert size={40} strokeWidth={1.5} aria-hidden="true" color="var(--tono-peligro-solido)" />
            <h1 ref={refTitulo} tabIndex={-1} id="titulo-confirmar-aviso" className={estilos.tituloConfirmar}>No pudimos confirmar el correo</h1>
            <p>{valor.mensaje}</p>
            <div><Boton como={Link} to="/" variante="secundario">Ir a la portada</Boton></div>
          </>
        )}
      </Tarjeta>
    </div>
  );
}
