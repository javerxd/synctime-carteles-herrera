import { useEffect, useRef, useState } from 'react';
import { ShieldCheck, Trash2 } from 'lucide-react';
import { Aviso, Boton, Campo, Dialogo, Esqueleto, Insignia, Tarjeta } from '../../componentes/ui';
import authService from '../../api/authService';
import { google } from '../../api/google';
import { useAvisos } from '../../avisos/Avisos';
import { googleDisponible } from '../../google/configuracion';
import { IngresoConGoogle } from '../cuenta/IngresoConGoogle';
import { useConsulta } from '../../utilidades/useConsulta';
import RegistroRostro from '../../rostro/RegistroRostro';
import { borrarTokenEquipo, guardarTokenEquipo, leerTokenEquipo } from '../../rostro/tokenEquipo';
import { GESTION } from '../../estructuras/navegacion';
import { useSesion } from '../../sesion/sesion';
import { SeccionAvisosTaller } from '../avisosTaller/SeccionAvisosTaller';
import estilosCuenta from '../cuenta/Cuenta.module.css';
import estilos from './Panel.module.css';

const fechaLegible = (iso) => (iso ? new Date(iso).toLocaleDateString('es-CL', { day: 'numeric', month: 'long', year: 'numeric' }) : '');

const TEXTOS_REGISTRO = {
  boton: 'Registrar mi rostro',
  guardar: 'Guardar mi rostro',
  consentimiento: 'Leí la información anterior y autorizo el tratamiento de mi dato biométrico para esta finalidad.',
  password: 'Contraseña actual',
};

/**
 * Mi perfil: datos de la cuenta. Solo gestión (dueño y administrador) ve además:
 * - el acceso con rostro (fase 10): registro con tres muestras y prueba de vida, autorizar el equipo y desactivar;
 * - «Avisos del taller» (fase 9), que es configuración del taller y no de su cuenta.
 * Para los demás roles nada de eso se dibuja ni se consulta.
 */
export default function Perfil() {
  const { usuario, tieneRol } = useSesion();
  const esGestion = tieneRol(GESTION);

  return (
    <div className={estilos.pagina}>
      <div>
        <h1 className={estilos.titulo}>Mi perfil</h1>
        <p className={estilos.bajada}>{usuario?.nombre} {usuario?.apellido} · {usuario?.email} · {usuario?.rol}</p>
      </div>
      {/* Fase 14C: el personal también entra con Google; vincularlo es de los cuatro roles. */}
      <SeccionGoogle email={usuario?.email} />
      {esGestion && <SeccionRostro email={usuario?.email} />}
      {esGestion && <SeccionAvisosTaller />}
    </div>
  );
}

/**
 * Mi perfil → «Ingreso con Google» (fase 14C, RFS03, RFU05): el empleado vincula o desvincula su Google con su contraseña actual
 * (la misma sección de Mi cuenta del cliente, contra /mi-perfil/google). Sin ID de cliente de Google no se muestra ni consulta nada.
 */
function SeccionGoogle({ email }) {
  if (!googleDisponible()) return null;
  return <SeccionGoogleConEstado email={email} />;
}

function SeccionGoogleConEstado({ email }) {
  const consulta = useConsulta('mi-perfil-google', (senal) => google.perfil.estado({ senal }));
  const [actualizada, setActualizada] = useState(null);
  const estado = actualizada ?? consulta.datos;
  // Cuenta los reintentos: mientras reintenta, el aviso y su botón siguen montados (la consulta borra el error al empezar de nuevo).
  const [reintentos, setReintentos] = useState(0);

  if (!estado) {
    // El encabezado y la región existen siempre; el botón «Reintentar» sigue montado mientras reintenta (así no pierde el foco).
    const reintentando = !consulta.error && consulta.cargando && reintentos > 0;
    const error = consulta.error ?? (reintentando ? { message: 'Reintentando…' } : null);
    return (
      <Tarjeta como="section" aria-labelledby="titulo-google-estado" className={estilosCuenta.seccion}>
        <h2 id="titulo-google-estado" className={estilosCuenta.subtitulo}>Ingreso con Google</h2>
        {error ? (
          <Aviso tono="peligro" titulo="No pudimos consultar tu ingreso con Google"
            accion={(
              <Boton variante="secundario" tamano="compacto" cargando={reintentando} textoCargando="Reintentando…" onClick={() => { setReintentos((n) => n + 1); consulta.recargar(); }}>
                Reintentar
              </Boton>
            )}>
            {error?.message || 'Revisa tu conexión e inténtalo de nuevo.'}
          </Aviso>
        ) : (
          <>
            <p role="status" className="solo-lectores">Consultando tu ingreso con Google…</p>
            <Esqueleto lineas={2} />
          </>
        )}
      </Tarjeta>
    );
  }

  // Todo empleado tiene contraseña: nunca se da el caso «sin contraseña» de los clientes.
  const datosCuenta = { googleVinculado: estado.vinculado === true, tieneContrasena: true, correo: estado.correo ?? email };
  const guardar = (cambio) => setActualizada((previa) => {
    const siguiente = typeof cambio === 'function' ? cambio({ googleVinculado: (previa ?? estado).vinculado === true }) : cambio;
    return { ...(previa ?? estado), vinculado: siguiente.googleVinculado };
  });
  return <IngresoConGoogle datosCuenta={datosCuenta} alGuardar={guardar} servicio={google.perfil} />;
}

function SeccionRostro({ email }) {
  const { anunciar } = useAvisos();
  const [estado, setEstado] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState('');
  const [password, setPassword] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [confirmando, setConfirmando] = useState(false);
  const refCancelar = useRef(null);
  const refTitulo = useRef(null);

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
    if (token && !guardarTokenEquipo(email, token)) {
      setError('El navegador no permitió guardar la autorización de este equipo. Revisa su configuración de privacidad.');
    }
  }

  // RegistroRostro muestra el error junto a la muestra o arriba: aquí solo se relanza.
  async function alRegistrar(datos) {
    const respuesta = await authService.activarRostro(datos);
    setError('');
    // El token del registro anterior ya no vale: no debe quedar en el navegador si no llega uno nuevo.
    borrarTokenEquipo(email);
    guardarToken(respuesta?.tokenEquipo);
    anunciar({ tono: 'exito', titulo: 'Rostro registrado', texto: 'Se guardaron las tres muestras.' });
    await recargar();
    // El formulario de registro desaparece: el foco vuelve al título de la sección.
    refTitulo.current?.focus();
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
      borrarTokenEquipo(email);
      setError('');
      anunciar({ tono: 'exito', titulo: 'Acceso con rostro desactivado', texto: 'Tus datos biométricos fueron borrados.' });
      await recargar();
      refTitulo.current?.focus();
    } catch (err) {
      setError(err?.message || 'No pudimos desactivar el acceso con rostro.');
    } finally {
      setEnviando(false);
      setConfirmando(false);
    }
  }

  const disponible = !cargando && estado?.disponible && estado.puedeUsarRostro !== false;
  const debeRegistrar = disponible && (!estado.activo || estado.debeRegistrarseDeNuevo);
  const equipoAutorizado = estado?.equipoVigente && Boolean(leerTokenEquipo(email));

  return (
    <>
      <Tarjeta como="section" aria-labelledby="titulo-rostro" className={estilos.ficha}>
        <h2 id="titulo-rostro" ref={refTitulo} tabIndex={-1} className={estilos.subtitulo}>Acceso con rostro</h2>
        {error && <Aviso tono="peligro" titulo={error} />}
        {cargando && <><p role="status" className="solo-lectores">Consultando…</p><Esqueleto lineas={3} /></>}

        {!cargando && estado && !disponible && (
          <Aviso tono="info" titulo="El acceso con rostro no está disponible en este momento" />
        )}

        {disponible && estado.debeRegistrarseDeNuevo && (
          <Aviso tono="aviso" titulo="Tu rostro debe registrarse de nuevo con las tres muestras">
            Mientras tanto, entra con tu contraseña.
          </Aviso>
        )}

        {debeRegistrar && <RegistroRostro consentimiento={estado.consentimiento} textos={TEXTOS_REGISTRO} alGuardar={alRegistrar} />}

        {disponible && estado.activo && (
          <>
            {!estado.debeRegistrarseDeNuevo && (
              <p><Insignia tono="exito" icono={ShieldCheck}>Activo</Insignia> Registrado el {fechaLegible(estado.fechaConsentimiento)}.</p>
            )}
            {/* Registrar de nuevo entrega un token de equipo nuevo: mientras tanto no se ofrece autorizar. */}
            {estado.requiereEquipo && !estado.debeRegistrarseDeNuevo && (
              <form className={estilos.formulario} onSubmit={alAutorizarEquipo} noValidate>
                <p>
                  {equipoAutorizado
                    ? `Este equipo está autorizado hasta el ${fechaLegible(estado.equipoVence)}. Autorizar otro anula este.`
                    : 'Este equipo no está autorizado: desde aquí no podrás entrar con tu rostro. Autorizarlo anula el equipo anterior.'}
                </p>
                <Campo etiqueta="Contraseña actual" type="password" maxLength={100} value={password}
                  onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" disabled={enviando} />
                <div><Boton type="submit" variante="secundario" disabled={enviando}>Autorizar este equipo</Boton></div>
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

      <Dialogo
        abierto={confirmando}
        alCerrar={() => setConfirmando(false)}
        enfoqueInicial={refCancelar}
        titulo="Desactivar el acceso con rostro"
        descripcion="Se borrarán tus tres muestras y seguirás entrando con tu contraseña. Solo se conserva la constancia de las fechas de aceptación y retiro."
        pie={(
          <>
            <Boton ref={refCancelar} variante="secundario" onClick={() => setConfirmando(false)}>Cancelar</Boton>
            <Boton variante="peligro" onClick={alDesactivar} cargando={enviando} textoCargando="Desactivando…">Desactivar y borrar</Boton>
          </>
        )}
      />
    </>
  );
}
