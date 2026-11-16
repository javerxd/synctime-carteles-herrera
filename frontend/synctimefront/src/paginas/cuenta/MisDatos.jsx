import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Download, UserX } from 'lucide-react';
import { Aviso, Boton, Campo, Dialogo, Tarjeta } from '../../componentes/ui';
import { cuenta as apiCuenta } from '../../api/cuenta';
import { useSesion } from '../../sesion/sesion';
import { FRASE_BAJA, nombreArchivoDatos, validarBaja } from './reglasCuenta';
import estilos from './Cuenta.module.css';

const campoDe = (falla, nombre) => Object.entries(falla?.errores ?? {}).find(([c]) => c.toLowerCase() === nombre)?.[1];

/**
 * La API rechaza la contraseña con 400 datos_invalidos (no 401, que cerraría la sesión). Se reconoce por su campo
 * (errores.password); un 400 sin campos también se toma como contraseña rechazada, salvo que el error sea de la frase.
 */
const esPasswordMala = (falla) => falla?.estado === 400 && (Boolean(campoDe(falla, 'password')) || !campoDe(falla, 'confirmacion'));

function textoPasswordMala(falla) {
  const delCampo = Object.entries(falla?.errores ?? {}).find(([c]) => c.toLowerCase() === 'password')?.[1];
  return (Array.isArray(delCampo) ? delCampo[0] : delCampo) || falla?.message || 'La contraseña no es correcta.';
}

const mensajeFalla = (falla, respaldo) => (falla?.estado === 429
  ? 'Hiciste demasiados intentos. Espera un minuto y vuelve a intentarlo.'
  : falla?.message || respaldo);

/** Guarda el archivo en el equipo con un enlace temporal; la URL se libera al poco rato. */
function guardarArchivo(blob, nombre) {
  const url = URL.createObjectURL(blob);
  const enlace = Object.assign(document.createElement('a'), { href: url, download: nombre });
  document.body.append(enlace);
  enlace.click();
  enlace.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** «Descargar mis datos»: pide la contraseña y guarda el archivo JSON (RFS12, portabilidad). */
function DescargarDatos() {
  const [password, setPassword] = useState('');
  const [estado, setEstado] = useState({ enviando: false, errorPassword: null, error: null, exito: null });
  const refPassword = useRef(null);
  // El campo se deshabilita mientras se envía: el foco vuelve a él cuando la API rechaza la contraseña.
  useEffect(() => {
    if (estado.errorPassword && !estado.enviando) refPassword.current?.focus();
  }, [estado]);

  async function descargar(evento) {
    evento.preventDefault();
    if (estado.enviando) return;
    if (!password) {
      setEstado({ enviando: false, errorPassword: 'Escribe tu contraseña para descargar tus datos.', error: null, exito: null });
      return;
    }
    setEstado({ enviando: true, errorPassword: null, error: null, exito: null });
    try {
      const archivo = await apiCuenta.descargarDatos(password);
      const nombre = nombreArchivoDatos();
      guardarArchivo(archivo, nombre);
      setPassword('');
      setEstado({ enviando: false, errorPassword: null, error: null, exito: `Descargamos tus datos en el archivo ${nombre}.` });
    } catch (falla) {
      if (esPasswordMala(falla)) {
        setPassword('');
        setEstado({ enviando: false, errorPassword: textoPasswordMala(falla), error: null, exito: null });
        return;
      }
      setPassword('');
      setEstado({ enviando: false, errorPassword: null, error: mensajeFalla(falla, 'No pudimos preparar tus datos. Inténtalo de nuevo.'), exito: null });
    }
  }

  return (
    <form className={estilos.formulario} onSubmit={descargar} noValidate aria-labelledby="titulo-descargar-datos">
      <h3 id="titulo-descargar-datos" className={estilos.subtituloPaso}>Descargar mis datos</h3>
      <p className={estilos.explicacion}>
        Un archivo JSON con tu cuenta, tus consentimientos, tus solicitudes (con los nombres de los archivos adjuntos), cotizaciones, pedidos y abonos.
      </p>
      <div role="status" aria-live="polite">{estado.exito && <Aviso tono="exito" titulo={estado.exito} />}</div>
      {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
      <Campo ref={refPassword} etiqueta="Contraseña" type="password" autoComplete="current-password" maxLength={200} requerido
        ayuda="Para confirmar que eres tú." value={password} onChange={(e) => setPassword(e.target.value)} error={estado.errorPassword}
        disabled={estado.enviando} />
      <div className={estilos.pieSeccion}>
        <Boton type="submit" variante="secundario" iconoInicio={Download} cargando={estado.enviando} textoCargando="Preparando…">
          Descargar mis datos
        </Boton>
      </div>
    </form>
  );
}

const VACIO = { password: '', confirmacion: '' };

/**
 * «Dar de baja mi cuenta»: explica qué se conserva y qué se borra, y pide la contraseña y escribir DAR DE BAJA. Al
 * terminar lleva a la portada con el comprobante y cierra la sesión (la API ya cerró todas las del servidor).
 */
function DialogoBaja({ abierto, alCerrar }) {
  const navegar = useNavigate();
  const { salir } = useSesion();
  const [datos, setDatos] = useState(VACIO);
  const [errores, setErrores] = useState({});
  const [estado, setEstado] = useState({ enviando: false, error: null });
  const [abiertoAntes, setAbiertoAntes] = useState(abierto);
  const refCancelar = useRef(null);
  const refPassword = useRef(null);
  const refFrase = useRef(null);
  // Tras un rechazo de la API el campo vuelve a habilitarse: entonces recibe el foco.
  const focoPendiente = useRef(null);
  useEffect(() => {
    if (!estado.enviando && focoPendiente.current) {
      focoPendiente.current.current?.focus();
      focoPendiente.current = null;
    }
  }, [estado]);
  if (abierto !== abiertoAntes) {
    setAbiertoAntes(abierto);
    if (abierto) {
      setDatos(VACIO);
      setErrores({});
      setEstado({ enviando: false, error: null });
    }
  }

  const cambiar = (campo) => (e) => {
    const valor = e.target.value;
    setDatos((d) => ({ ...d, [campo]: valor }));
    setErrores(({ [campo]: _quitado, ...resto }) => resto);
  };

  async function confirmar(evento) {
    evento.preventDefault();
    if (estado.enviando) return;
    const problemas = validarBaja(datos);
    setErrores(problemas);
    if (problemas.password) return refPassword.current?.focus();
    if (problemas.confirmacion) return refFrase.current?.focus();
    setEstado({ enviando: true, error: null });
    try {
      const respuesta = await apiCuenta.darDeBaja({ password: datos.password, confirmacion: FRASE_BAJA });
      // Primero se sale de la página protegida y luego se cierra la sesión local (como «Cerrar sesión»).
      navegar('/', { replace: true, state: { bajaCuenta: respuesta?.comprobante ?? {} } });
      salir().catch(() => {});
    } catch (falla) {
      if (esPasswordMala(falla)) {
        setDatos((d) => ({ ...d, password: '' }));
        setErrores({ password: textoPasswordMala(falla) });
        focoPendiente.current = refPassword;
        setEstado({ enviando: false, error: null });
        return;
      }
      setDatos((d) => ({ ...d, password: '' }));
      const frase = campoDe(falla, 'confirmacion');
      if (frase) setErrores({ confirmacion: Array.isArray(frase) ? frase[0] : String(frase) });
      setEstado({ enviando: false, error: frase ? null : mensajeFalla(falla, 'No pudimos dar de baja tu cuenta. Inténtalo de nuevo.') });
    }
  }

  return (
    <Dialogo abierto={abierto} alCerrar={() => !estado.enviando && alCerrar()} titulo="Dar de baja mi cuenta" enfoqueInicial={refCancelar}
      ancho="34rem" cerrarAlPulsarFondo={false}
      descripcion="Dejarás de poder ingresar y de recibir correos. Esta acción no se puede deshacer desde el portal."
      pie={(
        <>
          <Boton ref={refCancelar} variante="secundario" onClick={alCerrar} disabled={estado.enviando}>Cancelar</Boton>
          <Boton type="submit" form="formulario-baja" variante="peligro" iconoInicio={UserX} cargando={estado.enviando} textoCargando="Dando de baja…">
            Dar de baja
          </Boton>
        </>
      )}>
      <form id="formulario-baja" className={estilos.formulario} onSubmit={confirmar} noValidate>
        <ul className={estilos.listaBaja}>
          <li>Se conservan tus solicitudes, cotizaciones, pedidos y abonos, porque son parte de la historia y la contabilidad del taller.</li>
          <li>Se borran tu contraseña, tus códigos y tus sesiones; se retiran los correos opcionales.</li>
          <li>Te enviaremos un comprobante con la fecha a tu correo, si lo tenías confirmado.</li>
        </ul>
        {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
        <Campo ref={refPassword} etiqueta="Contraseña" type="password" autoComplete="current-password" maxLength={200} requerido
          value={datos.password} onChange={cambiar('password')} error={errores.password} disabled={estado.enviando} />
        <Campo ref={refFrase} etiqueta={`Escribe ${FRASE_BAJA} para confirmar`} autoComplete="off" autoCapitalize="characters" spellCheck={false}
          maxLength={20} requerido value={datos.confirmacion} onChange={cambiar('confirmacion')} error={errores.confirmacion} disabled={estado.enviando} />
      </form>
    </Dialogo>
  );
}

/** Mi cuenta → «Mis datos» (fase 11, RFS12, RFU34): descargarlos y dar de baja la cuenta. */
export function MisDatos() {
  const [baja, setBaja] = useState(false);
  return (
    <Tarjeta como="section" aria-labelledby="titulo-mis-datos" className={estilos.seccion}>
      <h2 id="titulo-mis-datos" className={estilos.subtitulo}>Mis datos</h2>
      <DescargarDatos />
      <div className={estilos.separadorSeccion}>
        <h3 className={estilos.subtituloPaso}>Dar de baja mi cuenta</h3>
        <p className={estilos.explicacion}>
          Cierra tu cuenta: dejas de ingresar y de recibir correos. Tus solicitudes, cotizaciones, pedidos y abonos se conservan.
        </p>
        <div className={estilos.pieSeccion}>
          <Boton variante="peligro" iconoInicio={UserX} onClick={() => setBaja(true)}>Dar de baja mi cuenta</Boton>
        </div>
      </div>
      <DialogoBaja abierto={baja} alCerrar={() => setBaja(false)} />
    </Tarjeta>
  );
}
