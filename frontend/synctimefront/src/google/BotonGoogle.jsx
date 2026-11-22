import { useEffect, useId, useRef, useState } from 'react';
import { Aviso, Boton } from '../componentes/ui';
import { ErrorApi } from '../api/cliente';
import { google } from '../api/google';
import { cargarGoogle, MENSAJE_SCRIPT_BLOQUEADO } from './cargarGoogle';
import { googleDisponible, idClienteGoogle } from './configuracion';
import { avisoDeErrorGoogle } from './mensajes';
import estilos from './Google.module.css';

const ANCHO_MINIMO = 200;
const ANCHO_MAXIMO = 400;

/** La raya con texto entre el botón de Google y el formulario: «o con tu correo». Las rayas son decoración. */
export function SeparadorGoogle({ texto = 'o con tu correo' }) {
  return <p className={estilos.separador}><span>{texto}</span></p>;
}

/**
 * «Continuar con Google» en dos clics (fase 14B, decisión 5): el primero (este botón del sistema) carga el script de
 * Google Identity Services y pide un nonce; recién entonces se muestra el botón OFICIAL de Google, que conserva su aspecto
 * y es el que exige Google para el ingreso. Así Google no se entera de la visita a la página.
 *
 * - `alCredencial({ idToken, nonce })`: recibe el token y hace lo que toque (ingresar, vincular…). Si lanza un error, se
 *   explica con `alFallo` y se pide un NONCE NUEVO antes de poder reintentar: el nonce se gasta siempre.
 * - `alFallo(error)`: devuelve el aviso { tono, titulo, texto? } a mostrar, `null` si quien llama ya lo mostró (por ejemplo,
 *   un error de la contraseña junto a su campo, donde también deja el foco) o `undefined` para el mensaje de siempre.
 * - `antesDeActivar()`: false cancela la activación (por ejemplo, falta la contraseña para vincular).
 * - El token y el nonce solo pasan por la memoria: nunca se guardan ni se escriben en la URL.
 *
 * Foco (el botón del sistema y el iframe de Google se desmontan al cambiar de estado, y el foco no debe caer al cuerpo):
 * al aparecer el botón de Google el foco va a su zona (que se lee con su texto); al verificar y tras un fallo, a la zona de
 * avisos, que contiene el mensaje; si no se pudo renovar el nonce, de vuelta al botón del sistema.
 * Sin ID de cliente configurado no dibuja nada.
 */
export function BotonGoogle({ alCredencial, alFallo, antesDeActivar, etiqueta = 'Continuar con Google', variante = 'secundario', textoAyuda }) {
  const idAyuda = useId();
  const [fase, setFase] = useState('reposo');
  const [aviso, setAviso] = useState(null);
  const [version, setVersion] = useState(0);
  const refContenedor = useRef(null);
  const refRegion = useRef(null);
  const refAvisos = useRef(null);
  const refBoton = useRef(null);
  const nonce = useRef(null);
  const ocupado = useRef(false);
  const vivo = useRef(true);
  // Adónde va el foco cuando aparece el botón de Google: 'region' (la primera vez), 'avisos' (tras un fallo) o 'ninguno'
  // (quien llama ya mostró el error junto a su campo y dejó el foco allí).
  const focoAlListo = useRef('region');
  const focoAlReposo = useRef(false);
  const refAlCredencial = useRef(alCredencial);
  const refAlFallo = useRef(alFallo);

  useEffect(() => {
    refAlCredencial.current = alCredencial;
    refAlFallo.current = alFallo;
  });

  useEffect(() => {
    vivo.current = true;
    return () => { vivo.current = false; };
  }, []);

  // Mientras se verifica no hay nada enfocable: el foco va a la zona de avisos. Si no se pudo seguir, vuelve al botón.
  useEffect(() => {
    if (fase === 'procesando') refAvisos.current?.focus({ preventScroll: true });
    if (fase === 'reposo' && focoAlReposo.current) {
      focoAlReposo.current = false;
      refBoton.current?.focus();
    }
  }, [fase]);

  /** Muestra el fallo. true si quien llama ya lo mostró (y manejó el foco). */
  function mostrarFallo(error) {
    const propio = refAlFallo.current?.(error);
    setAviso(propio === undefined ? avisoDeErrorGoogle(error) : propio);
    return propio === null;
  }

  async function pedirNonce() {
    const respuesta = await google.nonce();
    nonce.current = respuesta.nonce;
  }

  async function activar() {
    if (fase !== 'reposo' || ocupado.current) return;
    if (antesDeActivar && !antesDeActivar()) return;
    focoAlListo.current = 'region';
    setAviso(null);
    setFase('preparando');
    try {
      // Primero el script (si está bloqueado no se pide ningún nonce), después el nonce, lo más tarde posible.
      await cargarGoogle();
      await pedirNonce();
    } catch (error) {
      if (!vivo.current) return;
      mostrarFallo(error);
      setFase('reposo');
      return;
    }
    if (!vivo.current) return;
    setVersion((v) => v + 1);
    setFase('listo');
  }

  async function recibir({ credential } = {}) {
    // Sin nonce vigente (ya se usó, o no se pudo renovar) un token no sirve: un callback repetido o tardío de Google se ignora.
    if (!credential || ocupado.current || !nonce.current) return;
    ocupado.current = true;
    const usado = nonce.current;
    nonce.current = null;
    setAviso(null);
    setFase('procesando');
    try {
      await refAlCredencial.current({ idToken: credential, nonce: usado });
      if (vivo.current) setFase('reposo');
    } catch (error) {
      if (!vivo.current) return;
      const manejado = mostrarFallo(error);
      // El nonce ya se gastó (salga bien o mal): se pide uno nuevo antes de volver a mostrar el botón de Google.
      try {
        await pedirNonce();
        if (!vivo.current) return;
        focoAlListo.current = manejado ? 'ninguno' : 'avisos';
        setVersion((v) => v + 1);
        setFase('listo');
      } catch {
        if (!vivo.current) return;
        focoAlReposo.current = !manejado;
        setFase('reposo');
      }
    } finally {
      ocupado.current = false;
    }
  }

  // Cada vez que hay un nonce nuevo, Google se inicializa con él y dibuja su botón oficial.
  useEffect(() => {
    if (fase !== 'listo') return;
    const identidad = window.google?.accounts?.id;
    const contenedor = refContenedor.current;
    try {
      if (!identidad || !contenedor) throw new ErrorApi({ estado: 0, codigo: 'google_script', mensaje: MENSAJE_SCRIPT_BLOQUEADO });
      const delDibujo = nonce.current;
      // El callback queda atado al nonce de ESTE dibujo: un token repetido o tardío de un dibujo anterior se ignora.
      const alRecibir = (respuesta) => (nonce.current === delDibujo ? recibir(respuesta) : undefined);
      identidad.initialize({ client_id: idClienteGoogle(), nonce: delDibujo, callback: alRecibir, auto_select: false });
      identidad.renderButton(contenedor, {
        type: 'standard', theme: 'outline', size: 'large', text: 'continue_with', shape: 'rectangular', logo_alignment: 'left', locale: 'es',
        width: Math.min(Math.max(Math.floor(contenedor.clientWidth) || ANCHO_MAXIMO, ANCHO_MINIMO), ANCHO_MAXIMO),
      });
    } catch (error) {
      // Si Google no se puede inicializar no queda un texto con un contenedor vacío: se explica y se vuelve a empezar.
      nonce.current = null;
      mostrarFallo(error);
      focoAlReposo.current = true;
      setFase('reposo');
      return;
    }
    const destino = focoAlListo.current;
    focoAlListo.current = 'region';
    if (destino === 'avisos') refAvisos.current?.focus();
    else if (destino === 'region') refRegion.current?.focus();
    // `recibir` usa solo referencias y funciones de estado: no cambia lo que hace entre dibujos.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fase, version]);

  if (!googleDisponible()) return null;

  const esPeligro = aviso?.tono === 'peligro';
  const paso = fase === 'reposo' || fase === 'preparando';
  const hayAvisos = Boolean(aviso) || fase === 'procesando';

  return (
    <div className={estilos.bloque}>
      {/* Zona de avisos: siempre en la página (vacía se colapsa sin salir del árbol de accesibilidad, para que lo que
          aparece se anuncie) y enfocable por programa, para llevar allí el foco tras un fallo. */}
      <div ref={refAvisos} tabIndex={-1} className={`${estilos.avisos} ${hayAvisos ? '' : estilos.sinAvisos}`}>
        <div role="status" aria-live="polite" className={estilos.estado}>
          {fase === 'preparando' && <p className="solo-lectores">Cargando Google…</p>}
          {aviso && !esPeligro && <Aviso role="none" tono={aviso.tono} titulo={aviso.titulo}>{aviso.texto}</Aviso>}
          {fase === 'procesando' && !aviso && <p className={estilos.verificando}>Verificando con Google…</p>}
        </div>
        {esPeligro && <Aviso tono="peligro" titulo={aviso.titulo}>{aviso.texto}</Aviso>}
      </div>

      {paso && (
        <Boton ref={refBoton} variante={variante} anchoCompleto cargando={fase === 'preparando'} textoCargando="Cargando Google…" onClick={activar}>
          {etiqueta}
        </Boton>
      )}

      {fase === 'listo' && (
        <div ref={refRegion} tabIndex={-1} role="group" aria-labelledby={idAyuda} className={estilos.region}>
          <p id={idAyuda} className={estilos.ayuda}>
            {textoAyuda ?? 'Pulsa el botón «Continuar con Google» de abajo para elegir tu cuenta. Google sabrá que estás en esta página; nosotros solo recibimos tu nombre y tu correo.'}
          </p>
          <div ref={refContenedor} className={estilos.contenedor} />
        </div>
      )}
    </div>
  );
}
