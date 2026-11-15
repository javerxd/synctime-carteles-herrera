import { useEffect, useRef, useState } from 'react';
import { ScanFace, ShieldCheck, Trash2 } from 'lucide-react';
import { Aviso, Boton, Campo, Casilla, Dialogo, Esqueleto, Insignia, Tarjeta } from '../../componentes/ui';
import authService from '../../api/authService';
import { useAvisos } from '../../avisos/Avisos';
import CapturaFacial from '../../componentes/CapturaFacial';
import { mensajeDeError } from '../../rostro/motorFacial';
import { borrarTokenEquipo, guardarTokenEquipo, leerTokenEquipo } from '../../rostro/tokenEquipo';
import { useDevolverFoco } from '../../rostro/useDevolverFoco';
import { GESTION } from '../../estructuras/navegacion';
import { useSesion } from '../../sesion/sesion';
import { SeccionAvisosTaller } from '../avisosTaller/SeccionAvisosTaller';
import estilos from './Panel.module.css';

const CAPTURAS_PARA_ACTIVAR = 5;
const fechaLegible = (iso) => (iso ? new Date(iso).toLocaleDateString('es-CL', { day: 'numeric', month: 'long', year: 'numeric' }) : '');

/**
 * Mi perfil: datos de la cuenta y acceso con rostro (activar con consentimiento, autorizar el
 * equipo cuando el rol lo exige y desactivar borrando los datos). La lógica es la de la fase 1.
 * Gestión ve además «Avisos del taller» (fase 9), que es configuración del taller y no de su cuenta.
 */
export default function Perfil() {
  const { usuario, tieneRol } = useSesion();
  const { anunciar } = useAvisos();
  const [estado, setEstado] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState('');
  const [aceptado, setAceptado] = useState(false);
  const [password, setPassword] = useState('');
  const [capturando, setCapturando] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [confirmando, setConfirmando] = useState(false);
  const botonActivar = useDevolverFoco(capturando || enviando);
  const refCancelar = useRef(null);

  useEffect(() => {
    let cancelado = false;
    authService.estadoRostro()
      .then((datos) => { if (!cancelado) setEstado(datos); })
      .catch((err) => { if (!cancelado) setError(err?.message || 'No pudimos consultar el acceso con rostro.'); })
      .finally(() => { if (!cancelado) setCargando(false); });
    return () => { cancelado = true; };
  }, []);

  // El cambio ya se guardó: si falla esta consulta, no debe informarse como si el cambio hubiera fallado.
  async function recargar() {
    try {
      setEstado(await authService.estadoRostro());
    } catch {
      setError('El cambio se guardó, pero no pudimos actualizar esta vista. Recarga la página.');
    }
  }

  function guardarToken(token) {
    if (token && !guardarTokenEquipo(usuario.email, token)) {
      setError('El navegador no permitió guardar la autorización de este equipo. Revisa su configuración de privacidad.');
    }
  }

  function alIniciarActivacion(evento) {
    evento.preventDefault();
    if (!aceptado) setError('Debes aceptar el consentimiento para activar el acceso con rostro.');
    else if (!password) setError('Escribe tu contraseña para confirmar que eres tú.');
    else {
      setError('');
      setCapturando(true);
    }
  }

  async function alCapturar(descriptores) {
    setCapturando(false);
    setEnviando(true);
    try {
      const respuesta = await authService.activarRostro({ password, versionConsentimiento: estado.consentimiento.version, descriptores });
      guardarToken(respuesta.tokenEquipo);
      setPassword('');
      setAceptado(false);
      anunciar({ tono: 'exito', titulo: 'Acceso con rostro activado' });
      await recargar();
    } catch (err) {
      setError(err?.message || 'No pudimos activar el acceso con rostro.');
    } finally {
      setEnviando(false);
    }
  }

  async function alAutorizarEquipo(evento) {
    evento.preventDefault();
    if (!password) {
      setError('Escribe tu contraseña para confirmar que eres tú.');
      return;
    }
    setError('');
    setEnviando(true);
    try {
      const respuesta = await authService.autorizarEquipo(password);
      guardarToken(respuesta.tokenEquipo);
      setPassword('');
      anunciar({ tono: 'exito', titulo: 'Este equipo quedó autorizado' });
      await recargar();
    } catch (err) {
      setError(err?.message || 'No pudimos autorizar este equipo.');
    } finally {
      setEnviando(false);
    }
  }

  async function alDesactivar() {
    setEnviando(true);
    try {
      await authService.desactivarRostro();
      borrarTokenEquipo(usuario.email);
      setError('');
      anunciar({ tono: 'exito', titulo: 'Acceso con rostro desactivado', texto: 'Tus datos biométricos fueron borrados.' });
      await recargar();
    } catch (err) {
      setError(err?.message || 'No pudimos desactivar el acceso con rostro.');
    } finally {
      setEnviando(false);
      setConfirmando(false);
    }
  }

  const equipoAutorizado = estado?.equipoVigente && Boolean(leerTokenEquipo(usuario?.email));
  const campoPassword = (
    <Campo etiqueta="Contraseña actual" type="password" maxLength={100} value={password}
      onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" disabled={enviando} />
  );

  return (
    <div className={estilos.pagina}>
      <div>
        <h1 className={estilos.titulo}>Mi perfil</h1>
        <p className={estilos.bajada}>{usuario?.nombre} {usuario?.apellido} · {usuario?.email} · {usuario?.rol}</p>
      </div>

      <Tarjeta como="section" aria-labelledby="titulo-rostro" className={estilos.ficha}>
        <h2 id="titulo-rostro" className={estilos.subtitulo}>Acceso con rostro</h2>
        {error && <Aviso tono="peligro" titulo={error} />}
        {cargando && <><p role="status" className="solo-lectores">Consultando…</p><Esqueleto lineas={3} /></>}

        {!cargando && estado && !estado.disponible && (
          <Aviso tono="info" titulo="El acceso con rostro no está disponible en este momento" />
        )}

        {!cargando && estado?.disponible && !estado.activo && (
          <form className={estilos.formulario} onSubmit={alIniciarActivacion} noValidate>
            <p>Es opcional: siempre podrás entrar con tu contraseña. Lee lo siguiente antes de activarlo:</p>
            <ul>{estado.consentimiento.texto.map((parrafo) => <li key={parrafo}>{parrafo}</li>)}</ul>
            <Casilla
              etiqueta="Leí la información anterior y autorizo el tratamiento de mi dato biométrico para esta finalidad."
              checked={aceptado} onChange={(e) => setAceptado(e.target.checked)} disabled={enviando || capturando}
            />
            {campoPassword}
            {capturando ? (
              <CapturaFacial cantidad={CAPTURAS_PARA_ACTIVAR} onCaptura={alCapturar}
                onError={(err) => { setCapturando(false); setError(mensajeDeError(err)); }}
                onCancelar={() => setCapturando(false)} />
            ) : (
              <Boton ref={botonActivar} type="submit" iconoInicio={ScanFace} cargando={enviando} textoCargando="Activando…">
                Activar con mi rostro
              </Boton>
            )}
          </form>
        )}

        {!cargando && estado?.disponible && estado.activo && (
          <>
            <p><Insignia tono="exito" icono={ShieldCheck}>Activo</Insignia> Activado el {fechaLegible(estado.fechaConsentimiento)}.</p>
            {estado.requiereEquipo && (
              <form className={estilos.formulario} onSubmit={alAutorizarEquipo} noValidate>
                <p>
                  {equipoAutorizado
                    ? `Este equipo está autorizado hasta el ${fechaLegible(estado.equipoVence)}. Autorizar otro anula este.`
                    : 'Este equipo no está autorizado: desde aquí no podrás entrar con tu rostro. Autorizarlo anula el equipo anterior.'}
                </p>
                {campoPassword}
                <Boton type="submit" variante="secundario" disabled={enviando}>Autorizar este equipo</Boton>
              </form>
            )}
            <div>
              <Boton variante="peligro" iconoInicio={Trash2} onClick={() => setConfirmando(true)} disabled={enviando}>
                Desactivar y borrar mis datos
              </Boton>
            </div>
          </>
        )}
      </Tarjeta>

      {/* Configuración del taller (fase 9): solo dueño y administrador. Para los demás no se dibuja. */}
      {tieneRol(GESTION) && <SeccionAvisosTaller />}

      <Dialogo
        abierto={confirmando}
        alCerrar={() => setConfirmando(false)}
        enfoqueInicial={refCancelar}
        titulo="Desactivar el acceso con rostro"
        descripcion="Se borrarán tus datos biométricos y seguirás entrando con tu contraseña. Solo se conserva la constancia de las fechas de aceptación y retiro."
        pie={(
          <>
            <Boton ref={refCancelar} variante="secundario" onClick={() => setConfirmando(false)}>Cancelar</Boton>
            <Boton variante="peligro" onClick={alDesactivar} cargando={enviando} textoCargando="Desactivando…">Desactivar y borrar</Boton>
          </>
        )}
      />
    </div>
  );
}
