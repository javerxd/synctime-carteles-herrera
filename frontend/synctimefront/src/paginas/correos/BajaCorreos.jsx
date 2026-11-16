import { useEffect, useId, useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { CircleAlert, CircleCheck, MailX, RefreshCw } from 'lucide-react';
import { Aviso, Boton, Casilla, Esqueleto, Tarjeta } from '../../componentes/ui';
import { bajaCorreos } from '../../api/correos';
import { useConsulta } from '../../utilidades/useConsulta';
import estilos from './Correos.module.css';

const OPCIONES = [
  { clave: 'avisosEstado', etiqueta: 'Avisos del estado de tus solicitudes y pedidos', sigue: 'los avisos del estado de tus solicitudes y pedidos' },
  { clave: 'marketing', etiqueta: 'Novedades y promociones', sigue: 'las novedades y promociones' },
];

const SEGURIDAD = 'Los correos de seguridad de tu cuenta (confirmar el correo o recuperar la contraseña) siempre se envían.';

/** Mientras esta página está abierta, el navegador no envía su dirección como Referer (el enlace llega con el token). */
function useSinReferer() {
  useEffect(() => {
    const meta = document.createElement('meta');
    meta.name = 'referrer';
    meta.content = 'no-referrer';
    document.head.appendChild(meta);
    return () => meta.remove();
  }, []);
}

function EnlaceInvalido({ incompleto, refTitulo }) {
  return (
    <>
      <CircleAlert size={40} strokeWidth={1.5} aria-hidden="true" color="var(--tono-peligro-solido)" />
      <h1 ref={refTitulo} tabIndex={-1} className={estilos.tituloBaja}>Este enlace no sirve</h1>
      <p>
        {incompleto
          ? 'El enlace está incompleto. Ábrelo de nuevo desde el correo.'
          : 'El enlace no es válido o ya venció (por ejemplo, si cambiaste tu correo). Usa el de un correo más reciente o elige qué correos recibes desde tu cuenta.'}
      </p>
      <div className={estilos.pie}>
        <Boton como={Link} to="/cuenta" variante="secundario">Ir a mi cuenta</Boton>
        <Boton como={Link} to="/" variante="fantasma">Ir a la portada</Boton>
      </div>
    </>
  );
}

/**
 * /baja?t=… (pública, fase 11): el enlace de baja de los correos opcionales. NO actúa al abrirse (algunos correos
 * abren los enlaces solos para revisarlos): muestra el correo enmascarado y lo que recibe, y la baja ocurre solo al
 * pulsar «Confirmar baja». El token se lee una vez, se saca de la dirección y no viaja como Referer.
 */
export default function BajaCorreos() {
  const [parametros] = useSearchParams();
  const navegar = useNavigate();
  const [token] = useState(() => parametros.get('t') ?? '');
  const consulta = useConsulta(`baja:${token}`, () => (token ? bajaCorreos.consultar(token) : Promise.resolve(null)));
  const [marcados, setMarcados] = useState(null);
  const [estado, setEstado] = useState({ enviando: false, error: null, sinElegir: false, resultado: null, invalido: false });
  const refTitulo = useRef(null);
  const refPrimera = useRef(null);
  const idError = useId();
  useSinReferer();

  useEffect(() => {
    if (parametros.has('t')) navegar('/baja', { replace: true });
  }, [parametros, navegar]);

  const datos = consulta.datos;
  // Las casillas parten marcadas en lo que hoy recibe: lo habitual es darse de baja de todo lo que llega.
  if (datos && marcados === null) setMarcados({ avisosEstado: Boolean(datos.avisosEstado), marketing: Boolean(datos.marketing) });

  const invalido = !token || estado.invalido || [400, 404].includes(consulta.error?.estado);
  const listo = Boolean(estado.resultado);
  useEffect(() => {
    if (invalido || listo) refTitulo.current?.focus();
  }, [invalido, listo]);

  const activas = OPCIONES.filter((o) => datos?.[o.clave]);

  async function confirmar(evento) {
    evento.preventDefault();
    if (estado.enviando) return;
    const elegidas = activas.filter((o) => marcados?.[o.clave]);
    if (!elegidas.length) {
      setEstado((e) => ({ ...e, sinElegir: true, error: null }));
      refPrimera.current?.focus();
      return;
    }
    setEstado((e) => ({ ...e, enviando: true, error: null, sinElegir: false }));
    // Solo viaja lo que se apaga: los dos, o el elegido.
    const cuerpo = { t: token, ...Object.fromEntries(elegidas.map((o) => [o.clave, false])) };
    try {
      const resultado = await bajaCorreos.confirmar(cuerpo);
      setEstado((e) => ({
        ...e,
        enviando: false,
        resultado: {
          avisosEstado: resultado?.avisosEstado ?? (cuerpo.avisosEstado === false ? false : Boolean(datos.avisosEstado)),
          marketing: resultado?.marketing ?? (cuerpo.marketing === false ? false : Boolean(datos.marketing)),
        },
      }));
    } catch (falla) {
      if ([400, 404].includes(falla?.estado)) setEstado((e) => ({ ...e, enviando: false, invalido: true }));
      else setEstado((e) => ({ ...e, enviando: false, error: falla?.message || 'No pudimos darte de baja. Inténtalo de nuevo.' }));
    }
  }

  let contenido;
  if (invalido) {
    contenido = <EnlaceInvalido incompleto={!token} refTitulo={refTitulo} />;
  } else if (listo) {
    const siguen = OPCIONES.filter((o) => estado.resultado[o.clave]);
    contenido = (
      <>
        <CircleCheck size={40} strokeWidth={1.5} aria-hidden="true" color="var(--tono-exito-solido)" />
        <h1 ref={refTitulo} tabIndex={-1} className={estilos.tituloBaja}>Listo, te diste de baja</h1>
        {siguen.length
          ? siguen.map((o) => <p key={o.clave}>Seguirás recibiendo {o.sigue}.</p>)
          : <p>Ya no te enviaremos correos opcionales a <strong className="cifras">{datos.correoEnmascarado}</strong>.</p>}
        <p className={estilos.nota}>{SEGURIDAD} Puedes volver a activarlos desde tu cuenta.</p>
        <div className={estilos.pie}>
          <Boton como={Link} to="/cuenta" variante="secundario">Ir a mi cuenta</Boton>
          <Boton como={Link} to="/" variante="fantasma">Ir a la portada</Boton>
        </div>
      </>
    );
  } else if (consulta.error) {
    contenido = (
      <>
        <h1 className={estilos.tituloBaja}>No pudimos revisar el enlace</h1>
        <Aviso tono="peligro" titulo={consulta.error.message || 'Revisa tu conexión e inténtalo de nuevo.'}
          accion={<Boton variante="secundario" tamano="compacto" iconoInicio={RefreshCw} onClick={consulta.recargar}>Reintentar</Boton>} />
      </>
    );
  } else if (!datos) {
    contenido = (
      <>
        <h1 className={estilos.tituloBaja}>Revisando el enlace…</h1>
        <Esqueleto lineas={3} />
      </>
    );
  } else {
    contenido = (
      <>
        <MailX size={40} strokeWidth={1.5} aria-hidden="true" color="var(--color-texto-secundario)" />
        <h1 className={estilos.tituloBaja}>Darte de baja de los correos</h1>
        <p>Correo: <strong className={`cifras ${estilos.correo}`}>{datos.correoEnmascarado}</strong></p>
        {activas.length === 0 ? (
          <p>Ya no recibes correos opcionales en este correo. {SEGURIDAD}</p>
        ) : (
          <form onSubmit={confirmar} noValidate className={estilos.cuerpoDialogo}>
            <fieldset className={estilos.opciones} aria-describedby={estado.sinElegir ? idError : undefined}>
              <legend className={estilos.leyenda}>¿De qué correos quieres darte de baja?</legend>
              {activas.map((o, i) => (
                <Casilla key={o.clave} ref={i === 0 ? refPrimera : undefined} etiqueta={o.etiqueta} checked={Boolean(marcados?.[o.clave])}
                  disabled={estado.enviando} aria-describedby={estado.sinElegir ? idError : undefined}
                  onChange={(e) => {
                    const valor = e.target.checked;
                    setMarcados((m) => ({ ...m, [o.clave]: valor }));
                    setEstado((s) => ({ ...s, sinElegir: false }));
                  }} />
              ))}
              <p id={idError} className={estilos.errorGrupo} aria-live="polite">{estado.sinElegir ? 'Marca al menos un tipo de correo.' : ''}</p>
            </fieldset>
            <p className={estilos.nota}>{SEGURIDAD}</p>
            {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
            <div className={estilos.pie}>
              <Boton type="submit" cargando={estado.enviando} textoCargando="Confirmando…">Confirmar baja</Boton>
            </div>
          </form>
        )}
      </>
    );
  }

  const tituloDocumento = invalido ? 'Este enlace no sirve' : listo ? 'Listo, te diste de baja' : 'Darte de baja de los correos';
  useEffect(() => {
    document.title = `${tituloDocumento} · Letreros Herrera`;
  }, [tituloDocumento]);

  // La región de estado existe desde el inicio y solo cambia su texto: así el lector anuncia el resultado.
  return (
    <div className={estilos.paginaBaja}>
      <p role="status" className="solo-lectores">{!invalido && !listo && !datos && !consulta.error ? 'Revisando el enlace…' : ''}</p>
      <Tarjeta como="section" aria-label="Baja de correos" className={estilos.tarjetaBaja}>
        {contenido}
      </Tarjeta>
    </div>
  );
}
